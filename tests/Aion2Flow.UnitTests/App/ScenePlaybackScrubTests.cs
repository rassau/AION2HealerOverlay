using System.Reflection;
using Avalonia.Threading;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class ScenePlaybackScrubTests
{
    // A timeline drag sends a seek on every pointer move, and the seek coordinator starts one only once the pointer rests
    // for its 33 ms spacing, so a drag showed no frame until it stopped. The playhead and the time follow the pointer, a
    // frame of an earlier seek that lands meanwhile doesn't pull them back, and the drag ends on its last position.
    [Fact]
    public void TimelineDrag_PlayheadFollowsThePointer()
    {
        WithViewModel(CreatePayload(10, 1), viewModel =>
        {
            viewModel.RequestSeek(4_000);
            Assert.Equal(4_000, viewModel.PositionMilliseconds);
            Thread.Sleep(200);
            viewModel.RequestSeek(7_500);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(7_500, viewModel.PositionMilliseconds);
            Assert.Equal("00:07.500", viewModel.PositionText);

            Pump(() => !viewModel.IsLoading);
            Assert.False(viewModel.IsLoading);
            Assert.Equal(7_500, viewModel.PositionMilliseconds);
        });
    }

    // Paused at 10 s, a click on the ruler at 50 s, then "zoom in" and "+1 s" before that seek ends. The playhead shows the
    // target, so both act from it: the zoom keeps 50 s in place and the step goes to 51 s. Before, both acted from the old
    // position (10 s), and the step cancelled the click's seek.
    [Fact]
    public void ButtonsDuringAPendingSeek_ActFromItsTarget()
    {
        WithViewModel(CreatePayload(100, 100), viewModel =>
        {
            Load(viewModel, 10_000);
            Assert.Equal(new PlaybackTimelineViewport(0, 100_000), viewModel.TimelineViewport);

            viewModel.RequestSeek(50_000);
            viewModel.ZoomTimelineInCommand.Execute(null);
            Assert.Equal(new PlaybackTimelineViewport(25_000, 75_000), viewModel.TimelineViewport);
            viewModel.StepForwardCommand.Execute(null);
            Assert.Equal(51_000, viewModel.PositionMilliseconds);

            Pump(() => !viewModel.IsLoading);
            Assert.False(viewModel.IsLoading);
            Assert.Equal(51_000, viewModel.PositionMilliseconds);
            Assert.Equal(new PlaybackTimelineViewport(25_000, 75_000), viewModel.TimelineViewport);
        });
    }

    // A seek while zoomed in (a click, or an event row), and a frame that lands before it ends: SetSpeed re-applies the
    // current frame, as a playback tick does when the user seeks during play. The frame moves neither the playhead nor the
    // viewport off the target, and the strips are still those of the viewport afterwards.
    [Fact]
    public void ZoomedSeek_AFrameLandingMeanwhileLeavesThePlayheadAndTheViewportOnTheTarget()
    {
        WithViewModel(CreatePayload(100, 100), viewModel =>
        {
            Load(viewModel, 86_000);
            viewModel.ZoomTimelineAt(0.1, 81_500);
            var zoomed = viewModel.TimelineViewport;
            Assert.False(zoomed.Contains(86_000));
            Pump(() => StripMatches(viewModel.GlobalTimeline, zoomed));
            Assert.Equal(zoomed, viewModel.TimelineViewport);
            Assert.True(StripMatches(viewModel.GlobalTimeline, zoomed));

            viewModel.RequestSeek(82_000);
            viewModel.SetSpeedCommand.Execute("1");
            Assert.Equal(82_000, viewModel.PositionMilliseconds);
            Assert.Equal(zoomed, viewModel.TimelineViewport);

            Pump(() => !viewModel.IsLoading);
            Assert.Equal(82_000, viewModel.PositionMilliseconds);
            Assert.Equal(zoomed, viewModel.TimelineViewport);
            Assert.True(StripMatches(viewModel.GlobalTimeline, zoomed));
        });
    }

    // Zoomed in away from the playhead, a frame that lands while a step loads moves the viewport back to the playhead, and
    // nothing is projected while loading. The step's own frame lands inside that viewport: the strips must be projected
    // for it then, not keep the previous viewport's markers until the next zoom.
    [Fact]
    public void ViewportMovedWhileLoading_StripsFollowItOnceLoaded()
    {
        WithViewModel(CreatePayload(100, 100), viewModel =>
        {
            Load(viewModel, 86_000);
            viewModel.ZoomTimelineAt(0.1, 20_000);
            var zoomed = viewModel.TimelineViewport;
            Assert.False(zoomed.Contains(86_000));
            Pump(() => StripMatches(viewModel.GlobalTimeline, zoomed));
            Assert.Equal(zoomed, viewModel.TimelineViewport);
            Assert.True(StripMatches(viewModel.GlobalTimeline, zoomed));

            // A step back replays from the start, so it is still loading when the frame is re-applied.
            viewModel.StepEventBackwardCommand.Execute(null);
            viewModel.SetSpeedCommand.Execute("1");
            Assert.NotEqual(zoomed, viewModel.TimelineViewport);

            Pump(() => !viewModel.IsLoading && StripMatches(viewModel.GlobalTimeline, viewModel.TimelineViewport));
            Assert.False(viewModel.IsLoading);
            Assert.True(viewModel.TimelineViewport.Contains(viewModel.PositionMilliseconds));
            Assert.True(StripMatches(viewModel.GlobalTimeline, viewModel.TimelineViewport));
        });
    }

    private static void WithViewModel(SceneArchivePayload payload, Action<ScenePlaybackViewModel> test)
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, frameBatch) = ViewTestServices.Get();
            var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-scrub-{Guid.NewGuid():N}");
            var language = new LanguageService();
            using var resources = new GameResourceService(language);
            var settingsService = new SettingsService(Path.Combine(directory, "settings.json"));
            using var uiScale = new UiScaleService(settingsService);
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
            var viewModel = new ScenePlaybackViewModel(new ArchivedScenePlaybackSource(payload), new SceneDisplayContext(payload.IdentityScope, null, payload.Snapshot, resources, "Unknown"),
                localization, PeriodicScenePlaybackTickSourceFactory.Shared, frameBatch, settings);
            try
            {
                test(viewModel);
            }
            finally
            {
                var disposal = viewModel.DisposeAsync().AsTask();
                Pump(() => disposal.IsCompleted);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        });
    }

    // Paused at the position, with no checkpoint rebuild running (opening starts none with the default options): a frame
    // a rebuild publishes after a zoom would bring the viewport back to the playhead. The view model does not expose the
    // rebuild, and its last publish follows the flag at once.
    private static void Load(ScenePlaybackViewModel viewModel, double positionMilliseconds)
    {
        var controller = (ScenePlaybackController)typeof(ScenePlaybackViewModel).GetField("_controller", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel)!;
        Pump(() => viewModel.GlobalTimeline.Count > 0 && !viewModel.IsLoading && !controller.IsCheckpointing);
        Assert.False(controller.IsCheckpointing);
        Settle(100);
        viewModel.RequestSeek(positionMilliseconds);
        Pump(() => !viewModel.IsLoading);
        Assert.Equal(positionMilliseconds, viewModel.PositionMilliseconds);
    }

    // The strip has markers and all of them lie in the viewport: it was projected for this viewport, not an earlier one.
    private static bool StripMatches(PlaybackTimelineStrip strip, PlaybackTimelineViewport viewport)
    {
        var any = false;
        foreach (var band in strip.Bands)
        {
            foreach (var marker in band.Markers)
            {
                if (marker.PositionMilliseconds < viewport.StartMilliseconds - 1 || marker.PositionMilliseconds > viewport.EndMilliseconds + 1)
                    return false;
                any = true;
            }
        }

        return any;
    }

    private static void Pump(Func<bool> done)
    {
        var deadline = Environment.TickCount64 + 10_000;
        while (!done() && Environment.TickCount64 < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
    }

    private static void Settle(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
    }

    private static SceneArchivePayload CreatePayload(int seconds, int eventsPerSecond)
    {
        var journal = new ObservedEventJournal();
        var sceneId = Guid.NewGuid();
        for (var i = 1; i <= seconds * eventsPerSecond; i++)
        {
            var header = new ObservedEventHeader(
                sceneId,
                new TimelineStamp { OffsetTicks = i * 1_000L / eventsPerSecond * TimeSpan.TicksPerMillisecond, ObservationOrdinal = i - 1, FlushId = 1 },
                100,
                200,
                new RawPacketReference(0x0438, 0, i));
            var observation = new CombatWireObservation { SkillCode = 11000010, Damage = 100 * i, HitCount = 1, AttemptCount = 1 };
            journal.Append(in header, in observation);
        }

        journal.CompleteFlush(1);
        var owner = new SceneReadModelOwner(journal, sceneId, DateTimeOffset.Now);
        owner.CreateSnapshot();
        return owner.CreateArchivePayload();
    }
}
