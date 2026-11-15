using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;
using Cloris.Aion2Flow.WinDivert;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Cloris.Aion2Flow.Tests.App;

// No work for what isn't seen: a minimized group frame keeps its rows as they were (the boss alert and your heading still
// follow), a minimized skill monitor its slots, and the frame clock goes on without a new callback per frame. Shown again,
// a frame is what one that never hid shows, from the moment it shows.
[Collection(AvaloniaTestCollection.Name)]
public sealed class HiddenFrameWorkTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
    private const int Me = 100;
    private const int Tank = 200;
    private const int Dps = 300;
    private const int Boss = 500;

    [Fact]
    public void A_Minimized_Group_Frame_Shows_The_Rows_Of_One_That_Never_Hid_Once_Shown()
    {
        CombatResourceRegistry.EnsureCombatResources();
        // Positions are kept process-wide; this collection runs alone (AvaloniaTestCollection).
        PlayerPositions.Clear();
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(1) };
        var scene = new SceneLiveReadModel(Started, clock);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var (localization, _) = ViewTestServices.Get();
        var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-hidden-{Guid.NewGuid():N}.json"));
        var language = new LanguageService();
        using var uiScale = new UiScaleService(settingsService);
        var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
            new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService())
        {
            GroupFrameShowOtherBuffs = true,
        };
        var capture = CaptureOf(scene);
        var resources = new GameResourceService(language);
        using var twins = new DigitalTwinService();
        var seen = new GroupFrameViewModel(capture, resources, localization, settings, twins);
        var hidden = new GroupFrameViewModel(capture, resources, localization, settings, twins);

        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
        sink.AppendNickname(Source(0, 0x048D), Dps, "Dps");
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Me, PlayerGroupMembership.Party(0));
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Tank, PlayerGroupMembership.Party(1));
        sink.AppendNpcKind(Source(1, 0x0140), Boss, NpcKind.Boss);
        sink.AppendNpcHp(Source(1, 0x008D), Tank, 5_000, 10_000);
        sink.AppendNpcHp(Source(1, 0x008D), Me, 9_000, 9_500);
        OpenAura(sink, 1, target: Me, origin: Me, sequence: 4, effect: 999_000_004, durationMilliseconds: 30_000);
        // You stand still, the tank 20 m north of you (an arrow, no heading yet).
        Place(Me, 1.5, 0, 0);
        Place(Tank, 1.5, 0, -2_000);
        clock.Now = Started.AddSeconds(1.5);
        seen.ProcessUiFrame();
        hidden.ProcessUiFrame();
        Assert.Equal(Describe(seen), Describe(hidden));
        Assert.True(hidden.Members[1].HasArrow);
        var kept = Describe(hidden, withAlert: false);

        // Minimized from here on: a new member, auras of every kind, a shield, HP; the boss turns to you and away; you go
        // 10 m north, then 10 m east (heading east).
        sink.AppendPlayerGroupMember(Source(2, 0x0092, sequence: 2), Me, PlayerGroupMembership.Party(0));
        sink.AppendPlayerGroupMember(Source(2, 0x0092, sequence: 2), Tank, PlayerGroupMembership.Party(1));
        sink.AppendPlayerGroupMember(Source(2, 0x0092, sequence: 2), Dps, PlayerGroupMembership.Party(2));
        sink.AppendNpcHp(Source(2, 0x008D), Dps, 7_000, 8_000);
        OpenAura(sink, 2, target: Tank, origin: Me, sequence: 1, effect: 999_000_001, durationMilliseconds: 30_000);
        OpenAura(sink, 2, target: Dps, origin: Boss, sequence: 2, effect: 999_000_002, durationMilliseconds: 8_000);
        OpenAura(sink, 2, target: Dps, origin: Tank, sequence: 3, effect: 999_000_003, durationMilliseconds: 20_000);
        var grant = new CombatWireObservation
        {
            SkillCode = 17_420_010,
            Damage = 3_119,
            ChainId = 79,
            BodyResourceEffectRef = ResourceEffectRef.FromRaw(1_742_001_011),
            PeriodicRelation = PeriodicEffectRelation.Target,
            PeriodicMode = 9,
            PeriodicTailSkillCodeRaw = 17_420_010,
            PeriodicTailLength = 4
        };
        var shieldSkillId = (uint)CombatSemanticEvidenceResolver.Evaluate(in grant).Resource.Slot!.Value.SkillId;
        OpenAura(sink, 2, target: Tank, origin: Me, sequence: 79, effect: shieldSkillId, durationMilliseconds: 10_000);
        sink.AppendCombatWireObservation(Source(2, 0x0438), Me, Tank, in grant);
        Place(Me, 2.5, 0, -1_000);
        Frame(2.5);

        sink.AppendBossTarget(Source(3, 0x2092), Me);
        Place(Me, 3.5, 1_000, -1_000);
        Frame(3.5);
        Assert.True(hidden.IsBossTargetingMe);

        sink.AppendNpcHp(Source(4, 0x008D), Dps, 0, 8_000);
        sink.AppendBossTarget(Source(5, 0x2092), Tank);
        Place(Me, 5.5, 1_000, -1_000);
        Frame(5.5);
        Assert.False(hidden.IsBossTargetingMe);

        // The debuff ran out, the shield is about to (it blinks).
        Place(Me, 10.4, 1_000, -1_000);
        Frame(10.4);

        hidden.ProcessUiFrame(rowsShown: true);
        Assert.Equal(Describe(seen), Describe(hidden));
        Assert.Equal(3, hidden.Members.Count);
        Assert.NotEmpty(hidden.Members[1].ShieldSegments);
        Assert.NotEmpty(hidden.Members[2].OtherAuras);
        // The tank lies north-west, 14 m away; heading east (north, then east while minimized), the arrow points back left.
        Assert.Equal(("14 m", 225d), (hidden.Members[1].DistanceText, Math.Round(hidden.Members[1].ArrowYaw, 6)));

        // Minimized again, and only a digital twin comes in: shown again, the icons are made anew (with the levels a twin
        // tells), though no aura changed.
        kept = Describe(hidden, withAlert: false);
        var ownIcon = Assert.Single(hidden.Members[0].MyAuras);
        twins.Preload([new DigitalTwin("Someone", 1301, "Server", "Cleric", 45, 0, 0, Started, [], [], [], [], [], [])]);
        Place(Me, 11.4, 1_000, -1_000);
        Frame(11.4);
        hidden.ProcessUiFrame(rowsShown: true);
        Assert.Equal(Describe(seen), Describe(hidden));
        Assert.NotSame(ownIcon, Assert.Single(hidden.Members[0].MyAuras));

        void Frame(double atSeconds)
        {
            clock.Now = Started.AddSeconds(atSeconds);
            seen.ProcessUiFrame();
            hidden.ProcessUiFrame(rowsShown: false);
            Assert.Equal(seen.IsBossTargetingMe, hidden.IsBossTargetingMe);
            Assert.Equal(kept, Describe(hidden, withAlert: false));
        }
    }

    // Shown again (its eye, or "minimize all"), the frame brings its rows up to date at once (FrameChrome's showing), before
    // anything is laid out or drawn: the paint after the window grows can come before the next frame tick. With no tick
    // in between, the rows are those of a frame that never hid.
    [Fact]
    public void A_Group_Frame_Shown_Again_Has_Current_Rows_Before_Its_Next_Frame()
    {
        AvaloniaTestHost.Run(() =>
        {
            CombatResourceRegistry.EnsureCombatResources();
            var clock = new SettableTimeProvider { Now = Started.AddSeconds(1) };
            var scene = new SceneLiveReadModel(Started, clock);
            var sink = SceneSinkFactory.CreateForLive(scene)();
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-hidden-{Guid.NewGuid():N}.json"));
            var language = new LanguageService();
            using var uiScale = new UiScaleService(settingsService);
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService())
            {
                GroupFrameEnabled = true,
            };
            var capture = CaptureOf(scene);
            var resources = new GameResourceService(language);
            var seen = new GroupFrameViewModel(capture, resources, localization, settings);
            var viewModel = new GroupFrameViewModel(capture, resources, localization, settings);
            var frameClock = new AvaloniaFrameClockService();
            var window = new GroupFrameWindow(frameClock, localization, uiScale, settingsService) { DataContext = viewModel };
            var screen = window.Screens.Primary!.WorkingArea;
            settingsService.Update(value => value.GroupFramePosition = new System.Drawing.Point(screen.X + screen.Width / 2, screen.Y + 100));
            sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
            sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
            sink.AppendNickname(Source(0, 0x048D), Dps, "Dps");
            sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Me, PlayerGroupMembership.Party(0));
            sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Tank, PlayerGroupMembership.Party(1));
            sink.AppendNpcHp(Source(1, 0x008D), Tank, 9_000, 10_000);
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                // No clock ticks from here on: the rows change only through ticks by hand (Tick) and the frame's own calls.
                frameClock.Dispose();
                clock.Now = Started.AddSeconds(1.5);
                Tick(window);
                seen.ProcessUiFrame();
                Assert.Equal(Describe(seen), Describe(viewModel));
                settings.ToggleMinimized(SettingsFlyoutViewModel.GroupFrameKey);
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("minimized", window.FrameRoot.Classes);
                var minimized = Describe(viewModel);

                // Minimized: a third member, the tank's HP down.
                sink.AppendPlayerGroupMember(Source(2, 0x0092, sequence: 2), Me, PlayerGroupMembership.Party(0));
                sink.AppendPlayerGroupMember(Source(2, 0x0092, sequence: 2), Tank, PlayerGroupMembership.Party(1));
                sink.AppendPlayerGroupMember(Source(2, 0x0092, sequence: 2), Dps, PlayerGroupMembership.Party(2));
                sink.AppendNpcHp(Source(2, 0x008D), Tank, 2_000, 10_000);
                clock.Now = Started.AddSeconds(2.5);
                Tick(window);
                Assert.Equal(minimized, Describe(viewModel));

                settings.ToggleMinimized(SettingsFlyoutViewModel.GroupFrameKey);
                Assert.DoesNotContain("minimized", window.FrameRoot.Classes);
                seen.ProcessUiFrame();
                Assert.Equal(Describe(seen), Describe(viewModel));
                Assert.Equal(3, viewModel.Members.Count);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
            }
        });
    }

    // The skill monitor alike, opened by its controller as in the app. Minimized, it follows its cooldowns and when one ended
    // (the flash) but leaves its slots. Shown again, with no frame tick in between, its slots are those of a monitor that
    // never hid: a cooldown that ran out while minimized doesn't flash again.
    [Fact]
    public void A_Skill_Monitor_Shown_Again_Has_Current_Slots_Before_Its_Next_Frame()
    {
        AvaloniaTestHost.Run(() =>
        {
            const uint RunsOut = 13_060_250;
            const uint Starts = 13_130_230;
            CombatResourceRegistry.EnsureCombatResources();
            var clock = new SettableTimeProvider { Now = Started.AddSeconds(1) };
            var scene = new SceneLiveReadModel(Started, clock);
            var sink = SceneSinkFactory.CreateForLive(scene)();
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            // The window takes these from Ioc (ViewTestServices).
            var settingsService = Ioc.Default.GetRequiredService<SettingsService>();
            var uiScale = Ioc.Default.GetRequiredService<UiScaleService>();
            var frameClock = Ioc.Default.GetRequiredService<AvaloniaFrameClockService>();
            var language = new LanguageService();
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
            var capture = CaptureOf(scene);
            var resources = new GameResourceService(language);
            var controller = new SkillMonitorWindowController(capture, resources, localization, settingsService, uiScale, settings, new ProcessForegroundWatcher());
            var seen = new SkillMonitorViewModel(capture, resources, localization, settingsService);
            var owner = OffScreenWindow();
            sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
            sink.RegisterCompactControl0238(Source(1, 0x0238), Me, 0, RunsOut, 0, 0, 0, cooldownMilliseconds: 2_000);
            try
            {
                owner.Show();
                controller.ShowOrActivate(owner);
                var window = Assert.Single(owner.OwnedWindows.OfType<SkillMonitorWindow>());
                var monitor = window.DataContext;
                Dispatcher.UIThread.RunJobs();
                // No clock ticks from here on: the slots change only through ticks by hand (Tick) and the monitor's own calls.
                frameClock.Detach(window);
                clock.Now = Started.AddSeconds(1.5);
                seen.ProcessUiFrame();
                Tick(window);
                Assert.Equal(Describe(seen), Describe(monitor));
                Assert.Single(monitor.SkillRows);
                settings.ToggleMinimized(SettingsFlyoutViewModel.SkillMonitorKey);
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("minimized", window.MonitorRoot.Classes);
                var minimized = Describe(monitor);

                // Minimized: the cooldown runs out (its flash starts) and another one starts.
                sink.RegisterCompactControl0238(Source(3, 0x0238), Me, 0, Starts, 0, 0, 0, cooldownMilliseconds: 5_000);
                clock.Now = Started.AddSeconds(3.5);
                seen.ProcessUiFrame();
                Tick(window);
                Assert.Equal(minimized, Describe(monitor));
                Assert.Contains(seen.SkillRows, slot => slot.CompletionStartedUtcMilliseconds > 0);
                // The flash goes by the real clock; it's over when the monitor shows again.
                Thread.Sleep(TimeSpan.FromMilliseconds(CooldownSkillVisualClientAnimation.DurationMilliseconds + 50));

                settings.ToggleMinimized(SettingsFlyoutViewModel.SkillMonitorKey);
                Assert.DoesNotContain("minimized", window.MonitorRoot.Classes);
                seen.ProcessUiFrame();
                Assert.Equal(Describe(seen), Describe(monitor));
                Assert.True(Assert.Single(monitor.SkillRows).HasCooldown);
            }
            finally
            {
                controller.Close();
                owner.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
            }
        });

        static string Describe(SkillMonitorViewModel monitor) => string.Join('\n', monitor.SkillRows.Select(slot => string.Join(';', slot.RowBaseSkillId, slot.IconAssetName,
            slot.ToolTipText, slot.BuffProgressValue, slot.HasBuff, slot.BuffRemainingText, slot.CooldownProgressValue, slot.CooldownRemainingText, slot.HasCooldown,
            slot.AvailableCount, slot.AvailableCountText, slot.HasAvailableCount, slot.CompletionStartedUtcMilliseconds)));
    }

    // An aura's timer text from the tables reads as it was formatted before: whole seconds rounded up, minutes from 60 s,
    // none past an hour.
    [Fact]
    public void Aura_Timers_Read_As_They_Were_Formatted()
    {
        static string Formatted(long milliseconds) =>
            milliseconds > 3_600_000
                ? string.Empty
                : milliseconds >= 60_000
                    ? string.Create(CultureInfo.InvariantCulture, $"{milliseconds / 60_000}m")
                    : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, (milliseconds + 999) / 1_000)}");

        for (var milliseconds = -5_000L; milliseconds <= 3_605_000L; milliseconds++)
        {
            if (Formatted(milliseconds) != GroupFrameViewModel.FormatRemaining(milliseconds))
                Assert.Equal(Formatted(milliseconds), GroupFrameViewModel.FormatRemaining(milliseconds));
        }

        foreach (var milliseconds in (long[])[long.MinValue, int.MinValue, int.MaxValue, long.MaxValue])
            Assert.Equal(Formatted(milliseconds), GroupFrameViewModel.FormatRemaining(milliseconds));
    }

    // One frame per pulse, every frame with its own timestamp in the one args instance; the loop stops with its last window
    // and starts again with the next, also when that comes before the waiting callback ran, and goes on when the window the
    // waiting request came from closes.
    [Fact]
    public void The_Frame_Clock_Runs_One_Frame_Per_Pulse_And_Starts_Again_After_Its_Last_Window_Went()
    {
        AvaloniaTestHost.Run(() =>
        {
            var clock = new AvaloniaFrameClockService();
            var window = OffScreenWindow();
            var second = OffScreenWindow();
            var stamps = new List<TimeSpan>();
            var args = new HashSet<AvaloniaFrameEventArgs>(ReferenceEqualityComparer.Instance);
            clock.Frame += (_, e) =>
            {
                args.Add(e);
                stamps.Add(e.Timestamp);
            };
            window.Show();
            try
            {
                clock.Attach(window);
                Pump(() => stamps.Count >= 3);
                // Attached again while running: still one frame per pulse.
                clock.Attach(window);
                Pump(() => stamps.Count >= 6);

                // Gone and back before the waiting callback ran.
                clock.Detach(window);
                clock.Attach(window);
                var count = stamps.Count;
                Pump(() => stamps.Count >= count + 3);

                // Gone: no frames; back later: frames again.
                clock.Detach(window);
                Pump(() => false, milliseconds: 150);
                count = stamps.Count;
                Pump(() => false, milliseconds: 150);
                Assert.Equal(count, stamps.Count);
                clock.Attach(window);
                Pump(() => stamps.Count >= count + 3);

                // A second window, the last attached: from its first frame on, the requests come from it. It closes while
                // one waits (as a frame window detaches): that request still runs, and frames go on.
                second.Show();
                clock.Attach(second);
                count = stamps.Count;
                Pump(() => stamps.Count >= count + 2);
                second.Closed += (_, _) => clock.Detach(second);
                second.Close();
                count = stamps.Count;
                Pump(() => stamps.Count >= count + 3);

                Assert.Equal(stamps.Count, stamps.Distinct().Count());
                Assert.Single(args);
                clock.Dispose();
                count = stamps.Count;
                Pump(() => false, milliseconds: 150);
                Assert.InRange(stamps.Count, count, count + 1);
            }
            finally
            {
                window.Close();
                if (second.IsVisible)
                    second.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });

        // The real message loop, as the render timer's ticks come as window messages, until done or the time is up.
        static void Pump(Func<bool> done, int milliseconds = 5_000, [CallerLineNumber] int line = 0)
        {
            using var stop = new CancellationTokenSource();
            var watch = Stopwatch.StartNew();
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(5), DispatcherPriority.Background, (_, _) =>
            {
                if (done() || watch.ElapsedMilliseconds > milliseconds)
                    stop.Cancel();
            });
            timer.Start();
            Dispatcher.UIThread.MainLoop(stop.Token);
            timer.Stop();
            Assert.True(done() || milliseconds < 5_000, $"The frame clock stopped at line {line}.");
        }
    }

    private static Window OffScreenWindow() => new()
    {
        Width = 40,
        Height = 40,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(-30_000, -30_000),
    };

    // A frame tick through the window's own handler, as the clock gives it: the window tells its view model whether the
    // frame is minimized.
    private static void Tick(Window window) => window.GetType().GetMethod("OnAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(window, [null, new AvaloniaFrameEventArgs(TimeSpan.Zero)]);

    // The capture service as if WinDivert were open (the frames read the scene only while the driver is active), on this scene.
    private static WinDivertCaptureService CaptureOf(SceneLiveReadModel scene)
    {
        var capture = new WinDivertCaptureService(new ProcessPortDiscoveryService());
        typeof(WinDivertCaptureService).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(capture, scene);
        typeof(WinDivertCaptureService).GetField("_divert", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(capture, RuntimeHelpers.GetUninitializedObject(typeof(WinDivertSession)));
        return capture;
    }

    // Everything a row shows, its aura icons' tool tips included; the distance and the arrow while it has one (without, they
    // keep their last values, unseen).
    private static string Describe(GroupFrameViewModel frame, bool withAlert = true)
    {
        var text = new StringBuilder();
        if (withAlert)
            text.Append(frame.IsBossTargetingMe).Append('\n');
        foreach (var row in frame.Members)
        {
            text.AppendJoin(';', row.EntityId, row.Name, row.HpBar?.Ratio, Color(row.HpBar?.Brush), row.HpText, row.HpToolTip, row.IsDead, row.HasDebuff,
                row.IsBossTarget, row.Deaths, row.HasArrow, row.IsOutOfRange);
            if (row.HasArrow)
                text.AppendJoin(';', row.DistanceText, Color(row.DistanceBrush), row.IsPlacementGuessed, row.ArrowYaw, row.ArrowPitch, Color(row.ArrowBrush));
            foreach (var segment in row.ShieldSegments)
                text.Append(';').Append(segment.Start).Append('-').Append(segment.End).Append(Color(segment.Brush));
            foreach (var part in row.ShieldParts)
                text.Append(';').Append(part.Text).Append('@').Append(part.Opacity);
            foreach (var icon in row.Debuffs.Concat(row.MyAuras).Concat(row.OtherAuras))
            {
                text.Append(";[").AppendJoin(',', icon.SkillId, icon.CasterId, icon.IconAssetName, icon.ToolTip, icon.Effect, icon.EffectNote, icon.Specializations,
                    icon.FallbackText, icon.IsDebuff, icon.IsOther, icon.RemainingText).Append(']');
            }

            text.Append('\n');
        }

        return text.ToString();

        static string Color(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color.ToString() : "-";
    }

    // Where a player stands at that moment (cm; x east, y south), as their own auras tell it.
    private static void Place(int entityId, double atSeconds, float east, float south)
        => PlayerPositions.UpdateEntity(entityId, new Vector3(east, south, 0), Started.AddSeconds(atSeconds).ToUnixTimeMilliseconds());

    private static void OpenAura(IRuntimeObservationSink sink, double atSeconds, int target, int origin, int sequence, uint effect, ushort durationMilliseconds)
        => sink.RegisterObservation2A38(Source(atSeconds, 0x2A38), target, 1, 19, sequence, 0, durationMilliseconds, 0, 0, 0, origin, 1, ResourceEffectRef.FromRaw(effect), 0, 0, 0);

    private static PacketObservationSource Source(double atSeconds, ushort opcode, long sequence = 0)
        => new(Started.AddSeconds(atSeconds).ToUnixTimeMilliseconds(), 0, opcode, 0, sequence, default);

    private sealed class SettableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
