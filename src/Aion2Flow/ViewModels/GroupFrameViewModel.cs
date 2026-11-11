using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Audio;
using Cloris.Aion2Flow.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cloris.Aion2Flow.ViewModels;

// You and your party with HP as soon as it changes, the debuffs on them, your own auras (HoTs, buffs) and the buffs
// others put on them, with the time they have left; each kind can be turned off in the settings.
public sealed partial class GroupFrameViewModel(WinDivertCaptureService captureService, GameResourceService resources, LocalizationService localization, SettingsFlyoutViewModel settings,
    DigitalTwinService? digitalTwins = null) : ObservableObject
{
    private static readonly IBrush HighHpBrush = new ImmutableSolidColorBrush(Color.Parse("#6FD38A"));
    private static readonly IBrush MidHpBrush = new ImmutableSolidColorBrush(Color.Parse("#E6C65A"));
    private static readonly IBrush LowHpBrush = new ImmutableSolidColorBrush(Color.Parse("#F07C82"));
    internal static readonly IBrush ShieldBrush = new ImmutableSolidColorBrush(Color.Parse("#8CD8FF"));
    internal static readonly IBrush ShieldFadedBrush = new ImmutableSolidColorBrush(Color.Parse("#408CD8FF"));
    // The part of a shield with no HP under it.
    internal static readonly IBrush ShieldOverHpBrush = new ImmutableSolidColorBrush(Color.Parse("#A78BFA"));
    internal static readonly IBrush ShieldOverHpFadedBrush = new ImmutableSolidColorBrush(Color.Parse("#40A78BFA"));
    // A shield blinks for this long before its aura ends, faded every other 250 ms.
    private const long ShieldBlinkMilliseconds = 3_000;
    internal const double FadedOpacity = 0.25;
    private static readonly IBrush AboveBrush = new ImmutableSolidColorBrush(Color.Parse("#2E8B3A"));
    private static readonly IBrush BelowBrush = new ImmutableSolidColorBrush(Color.Parse("#B3261E"));
    private static readonly IBrush LevelBrush = new ImmutableSolidColorBrush(Color.Parse("#1F5FBF"));
    // The distance beside the arrow: green, orange and red up to the marks in the settings, grey past the range.
    internal static readonly IBrush DistanceGreenBrush = HighHpBrush;
    internal static readonly IBrush DistanceOrangeBrush = new ImmutableSolidColorBrush(Color.Parse("#F5A03C"));
    internal static readonly IBrush DistanceRedBrush = LowHpBrush;
    internal static readonly IBrush DistanceOutOfRangeBrush = new ImmutableSolidColorBrush(Color.Parse("#9AA0A6"));
    // Ours comes only from the auras we cast. Up to 2 s old it's where we are; on the move (7-25 m/s) an older one is soon
    // 15 m and more off, so up to 10 s it's a guess (arrow and distance faded), then nothing: no arrow, no range. A
    // member's also comes from 0x1C92, and while they're in sight it holds until they move or leave.
    private const long OwnPositionFreshMilliseconds = 2_000;
    private const long OwnPositionGuessMilliseconds = 10_000;
    private const long MemberPositionStaleMilliseconds = 30_000;
    private const double SameLevelMeters = 3;
    // Longer than this left (or no duration at all) shows no timer and counts as a buff without duration.
    private const long NoTimerMilliseconds = 3_600_000;
    private readonly List<GroupFrameMember> _members = [];
    private readonly List<GroupFrameAura> _auras = [];
    private readonly List<GroupFrameShield> _shields = [];
    private readonly List<(double Part, bool Blinks)> _rowShields = [];
    // What's left of each of the member's shields, the oldest first, for the HP tool tip.
    private readonly List<long> _rowShieldAmounts = [];
    private readonly List<(int SkillId, long ExpiresAt, int CasterId)> _rowAuras = [];
    // Per skill and the caster's level of it (0: not known), see CreateIcon.
    private readonly Dictionary<(int SkillId, int Level), (string? IconAssetName, string Name, string? Effect, string? EffectNote, string? Specializations)> _skills = [];
    // A digital twin came in since the icons were made (DigitalTwinService.Version): they're made again, with the levels.
    private int _twinsVersion;
    private bool _iconsStale;
    // The Hidden Cube finder (see UpdateCubes): cubes are dropped past this distance (the server announces them within ~170 m)
    // or, while your own position isn't known, once not announced again for this long.
    private const double CubeDropMeters = 250;
    private const long CubeStaleMilliseconds = 600_000;
    internal static readonly IBrush CubeBrush = new ImmutableSolidColorBrush(Color.Parse("#E6C65A"));
    private readonly List<HiddenCubes.Cube> _cubes = [];
    private readonly List<(HiddenCubes.Cube Cube, double Meters)> _shownCubes = [];
    private readonly HashSet<int> _announcedCubes = [];

    public ObservableCollection<GroupFrameMemberRow> Members { get; } = [];

    // A line for each Hidden Cube the server announced, under the members (see UpdateCubes).
    public ObservableCollection<GroupFrameMemberRow> Cubes { get; } = [];

    // The frame's own gear opens its part of these.
    public SettingsFlyoutViewModel Settings => settings;

    // A boss's last aimed attack or spell went at you.
    [ObservableProperty]
    public partial bool IsBossTargetingMe { get; set; }

    // The boss just turned to you: the alert sound, if it's on (and not again within the quiet time set for it).
    partial void OnIsBossTargetingMeChanged(bool value)
    {
        if (value && settings.GroupFrameAlertEnabled && settings.AlertSounds.TryAlert(Environment.TickCount64, settings.GroupFrameAlertQuietSeconds * 1_000L))
            settings.PlayAlertSound();
    }

    public void ProcessUiFrame()
    {
        if (!captureService.IsDriverActive)
        {
            Members.Clear();
            Cubes.Clear();
            IsBossTargetingMe = false;
            return;
        }

        var now = captureService.Scene.CopyGroupFrameTo(_members, _auras, _shields);
        var fadedNow = now / 250 % 2 == 1;
        if (digitalTwins is not null && digitalTwins.Version != _twinsVersion)
        {
            _twinsVersion = digitalTwins.Version;
            _iconsStale = true;
        }

        if (!Members.Select(static row => row.EntityId).SequenceEqual(_members.Select(static member => member.EntityId)))
        {
            Members.Clear();
            foreach (var member in _members)
                Members.Add(new GroupFrameMemberRow(member.EntityId));
        }

        var self = _members.Find(static member => member.IsLocalPlayer).EntityId;
        var me = FreshPosition(self, now, OwnPositionGuessMilliseconds, holdInSight: false);
        var guessed = FreshPosition(self, now, OwnPositionFreshMilliseconds, holdInSight: false) is null;
        for (var i = 0; i < _members.Count; i++)
        {
            var member = _members[i];
            var row = Members[i];
            UpdatePlacement(row, in member, me, guessed, now);
            row.Name = member.Nickname.Length > 0 ? member.Nickname : "?";
            row.IsDead = member.CurrentHp == 0;
            row.IsBossTarget = member.IsBossTarget;
            row.Deaths = settings.GroupFrameShowDeaths ? member.Deaths : 0;
            if (member.CurrentHp is long hp)
            {
                var maxHp = Math.Max(1, Math.Max(hp, member.MaxHp ?? hp));
                var ratio = hp / (double)maxHp;
                row.HpBar = new ProgressSegment(ratio, ratio > 0.6 ? HighHpBrush : ratio > 0.3 ? MidHpBrush : LowHpBrush);
                // Shields as parts of max HP over the HP bar, reading "5% + 75%".
                CollectShields(member.EntityId, hp > 0 ? maxHp : 0, now);
                var (segments, parts) = LayoutShields(ratio, _rowShields, settings.GroupFrameShieldOnRight, fadedNow);
                row.SetShields(segments, parts);
                // Rounded down, so a member who still misses some HP never reads 100%.
                row.HpText = hp == 0
                    ? localization["GroupFrame_Dead"]
                    : string.Create(CultureInfo.InvariantCulture, $"{(_rowShields.Count > 0 ? "+ " : "")}{Math.Max(1, (int)(ratio * 100))}%");
                row.HpToolTip = HpToolTip(hp, maxHp, _rowShieldAmounts, localization["GroupFrame_ShieldTip"], CultureInfo.CurrentCulture);
            }
            else
            {
                row.HpBar = null;
                row.SetShields([], []);
                row.HpText = string.Empty;
                row.HpToolTip = null;
            }

            // The purple frame shows a debuff even when their icons are turned off.
            row.HasDebuff = HasDebuff(member.EntityId);
            UpdateAuras(row.Debuffs, member.EntityId, GroupFrameAuraKind.Debuff, settings.GroupFrameShowDebuffs, now);
            UpdateAuras(row.MyAuras, member.EntityId, GroupFrameAuraKind.Mine, settings.GroupFrameShowMyBuffs, now);
            UpdateAuras(row.OtherAuras, member.EntityId, GroupFrameAuraKind.Other, settings.GroupFrameShowOtherBuffs, now);
        }

        _iconsStale = false;
        IsBossTargetingMe = _members.Exists(static member => member.IsLocalPlayer && member.IsBossTarget);
        UpdateCubes(me, guessed, now);
    }

    // The Hidden Cube finder (experimental): a line per cube the server announced (HiddenCubes), nearest first, with the
    // arrow and distance like a member's, in gold; without your own position just the line, as the cube is within ~170 m.
    // With the sound on, a cube new to this session plays the blip once.
    internal void UpdateCubes(Vector3? me, bool guessed, long now)
    {
        var mode = settings.GroupFrameCubeFinder;
        HiddenCubes.CopyTo(_cubes);
        _shownCubes.Clear();
        if (mode != GroupFrameCubeFinder.Off)
        {
            foreach (var cube in _cubes)
            {
                var meters = me is { } mine ? PlayerPositions.Between(mine, cube.Position).Meters : (double?)null;
                if (meters > CubeDropMeters || meters is null && now - cube.SeenAtMilliseconds > CubeStaleMilliseconds)
                    continue;
                _shownCubes.Add((cube, meters ?? double.MaxValue));
            }
        }

        _shownCubes.Sort(static (left, right) => left.Meters.CompareTo(right.Meters));
        if (!Cubes.Select(static row => row.EntityId).SequenceEqual(_shownCubes.Select(static shown => shown.Cube.EntityId)))
        {
            Cubes.Clear();
            foreach (var (cube, _) in _shownCubes)
                Cubes.Add(new GroupFrameMemberRow(cube.EntityId) { Name = localization["GroupFrame_HiddenCube"] });
        }

        var sound = false;
        for (var i = 0; i < Cubes.Count; i++)
        {
            var row = Cubes[i];
            sound |= _announcedCubes.Add(row.EntityId) && mode == GroupFrameCubeFinder.Sound;
            if (me is not { } mine)
            {
                row.HasArrow = false;
                continue;
            }

            var (meters, bearing, rise) = PlayerPositions.Between(mine, _shownCubes[i].Cube.Position);
            row.HasArrow = true;
            row.IsPlacementGuessed = guessed;
            row.DistanceText = string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Round(meters)} m");
            row.DistanceBrush = CubeBrush;
            row.ArrowRotation.Angle = bearing;
            row.ArrowBrush = rise > SameLevelMeters ? AboveBrush : rise < -SameLevelMeters ? BelowBrush : LevelBrush;
        }

        if (sound)
            settings.AlertSounds.Play(AlertSounds.BlipName, settings.GroupFrameAlertVolumePercent);
    }

    // One icon per skill with the longest time left, in skill order so the icons don't jump around.
    private void UpdateAuras(ObservableCollection<GroupFrameAuraIcon> icons, int targetEntityId, GroupFrameAuraKind kind, bool show, long now)
    {
        _rowAuras.Clear();
        var hidePermanent = kind != GroupFrameAuraKind.Debuff && !settings.GroupFrameShowPermanentBuffs;
        foreach (var aura in _auras)
        {
            if (!show || aura.TargetEntityId != targetEntityId || aura.Kind != kind)
                continue;

            var expiresAt = aura.ExpiresAtMilliseconds ?? long.MaxValue;
            if (hidePermanent && expiresAt - now > NoTimerMilliseconds)
                continue;
            var index = _rowAuras.FindIndex(existing => existing.SkillId == aura.SkillId);
            if (index < 0)
                _rowAuras.Add((aura.SkillId, expiresAt, aura.CasterEntityId));
            else if (expiresAt > _rowAuras[index].ExpiresAt)
                _rowAuras[index] = (aura.SkillId, expiresAt, aura.CasterEntityId);
        }

        _rowAuras.Sort(static (left, right) => left.SkillId.CompareTo(right.SkillId));
        if (_iconsStale || !icons.Select(static icon => (icon.SkillId, icon.CasterId)).SequenceEqual(_rowAuras.Select(static aura => (aura.SkillId, aura.CasterId))))
        {
            icons.Clear();
            foreach (var (skillId, _, casterId) in _rowAuras)
                icons.Add(CreateIcon(skillId, kind, casterId));
        }

        for (var i = 0; i < icons.Count; i++)
            icons[i].RemainingText = FormatRemaining(_rowAuras[i].ExpiresAt - now);
    }

    private GroupFrameAuraIcon CreateIcon(int skillId, GroupFrameAuraKind kind, int casterId)
    {
        var caster = _members.Find(member => member.EntityId == casterId);
        var level = CasterSkillLevel(digitalTwins, caster.ServerId, caster.Nickname, skillId);
        if (!_skills.TryGetValue((skillId, level ?? 0), out var skill))
        {
            var name = resources.ResolveSkillName(skillId);
            if (string.IsNullOrWhiteSpace(name) || name == skillId.ToString(CultureInfo.InvariantCulture))
                name = string.Empty;
            var icon = resources.ResolveSkillIconAssetName(skillId) ?? resources.ResolveSkillIconAssetName(resources.ResolveBaseSkillIdForCode(skillId));
            var (effect, effectNote, specializations) = DescribeAura(skillId, localization, level, caster.Nickname, name, resources.ResolveSkillName(skillId / 10_000 * 10_000));
            _skills[(skillId, level ?? 0)] = skill = (icon, name, effect, effectNote, specializations);
        }

        return new GroupFrameAuraIcon(
            skillId,
            skill.IconAssetName,
            skill.Name.Length > 0 ? skill.Name : localization["SkillMonitor_UnknownSkill"],
            skill.Name.Length > 0 ? skill.Name[..Math.Min(2, skill.Name.Length)] : "?",
            kind,
            skill.Effect,
            skill.EffectNote,
            skill.Specializations,
            casterId);
    }

    // A player's level of a skill from their digital twin, when it's been looked up (see DigitalTwin.SkillLevel).
    internal static int? CasterSkillLevel(DigitalTwinService? twins, int serverId, string? name, int skillId) =>
        twins is not null && serverId > 0 && !string.IsNullOrWhiteSpace(name) ? twins.Get(serverId, name).Twin?.SkillLevel(skillId) : null;

    // What hovering an aura shows below its name: what the skill does (its tooltip, the numbers at the caster's level of
    // it when their digital twin tells it, otherwise from skill level 1 to max), and the caster's specializations where
    // the aura's id names them (see SkillSpecializationCodes). Nothing for skills without a text, such as a boss's debuffs.
    // auraName and skillName: the aura's own name and its skill's; when they differ (Earth's Blessing from Earth
    // Punishment) the skill's text is about the skill, so it says whose effect it is and what of the text and the
    // specializations names it: the caster's picks where the id tells them (a stigma's number covers the ones below it).
    internal static (string? Effect, string? EffectNote, string? Specializations) DescribeAura(int skillId, LocalizationService localization, int? level = null, string? casterName = null,
        string? auraName = null, string? skillName = null)
    {
        if (SkillEffects.Get(skillId) is not { } effect)
            return (null, null, null);

        var culture = CultureInfo.CurrentCulture;
        if (auraName?.Trim() is { Length: > 0 } ownName && skillName?.Trim() is { Length: > 0 } fromName && !string.Equals(ownName, fromName, StringComparison.OrdinalIgnoreCase))
        {
            var tag = $"[{ownName}]";
            var ownerId = skillId / 10_000 * 10_000;
            var ownerPicks = (skillId % 10_000 / 10).ToString(CultureInfo.InvariantCulture).Select(static digit => digit - '0').Where(static pick => pick is >= 1 and <= 5).ToArray();
            var picked = ownerPicks.Length >= 2 || ownerPicks.Length == 1 && SkillSpecializations.Get(ownerId) is { IsStigma: true };
            var named = (picked
                    ? SkillSpecializations.EffectsOf(ownerId, ownerPicks)
                    : (SkillSpecializations.Get(ownerId)?.Specializations ?? []).Select(static (spec, index) => (Number: index + 1, spec.Effect)))
                .Where(spec => spec.Effect.Contains(tag, StringComparison.OrdinalIgnoreCase))
                .Select(spec => string.Format(culture, localization["GroupFrame_Specialization"], spec.Number, spec.Effect))
                .ToList();
            var said = string.Join(" ", SentenceBreak().Split(effect.Text).Where(sentence => sentence.Contains(tag, StringComparison.OrdinalIgnoreCase)));
            var from = string.Format(culture, localization["GroupFrame_EffectOf"], fromName);
            return (said.Length > 0 ? $"{from} {said}" : from,
                named.Count > 0 && !picked ? localization["GroupFrame_EffectPicksUnknown"] : null,
                named.Count > 0 ? string.Join(Environment.NewLine, named) : null);
        }

        var specializations = Cloris.Aion2Flow.SceneRuntime.Twins.SkillSpecializationCodes.TryDecode(skillId, out var baseId, out var picks)
            ? string.Join(Environment.NewLine, SkillSpecializations.EffectsOf(baseId, picks).Select(pick => string.Format(culture, localization["GroupFrame_Specialization"], pick.Number, pick.Effect)))
            : "";
        var atLevel = level is { } known ? effect.At(known) : null;
        var note = atLevel is not null
            ? string.Format(culture, localization["GroupFrame_EffectLevel"], level, casterName)
            : string.Format(culture, localization["GroupFrame_EffectLevels"], effect.MaxLevel);
        return (atLevel ?? effect.Text, note, specializations.Length > 0 ? specializations : null);
    }

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceBreak();

    // A member's shields as parts of max HP, the oldest first, each blinking in its last 3 s; with the animation off, one
    // for all of them that doesn't blink.
    private void CollectShields(int entityId, long maxHp, long now)
    {
        _rowShields.Clear();
        _rowShieldAmounts.Clear();
        if (maxHp <= 0)
            return;

        var each = settings.GroupFrameShieldAnimation;
        foreach (var shield in _shields)
        {
            if (shield.TargetEntityId != entityId)
                continue;

            var part = shield.Remaining / (double)maxHp;
            _rowShieldAmounts.Add(shield.Remaining);
            if (!each && _rowShields.Count > 0)
                _rowShields[0] = (_rowShields[0].Part + part, false);
            else
                _rowShields.Add((part, each && shield.ExpiresAtMilliseconds - now <= ShieldBlinkMilliseconds));
        }
    }

    // The shields from the left or right end, the oldest outermost, violet where no HP is under them; a blinking one
    // is faded while fadedNow. The text: "15%", or with more than one "(15,5) 20%".
    internal static (BarSegment[] Segments, ShieldTextPart[] Text) LayoutShields(double hpRatio, IReadOnlyList<(double Part, bool Blinks)> shields, bool fromRight, bool fadedNow)
    {
        if (shields.Count == 0)
            return ([], []);

        var segments = new List<BarSegment>();
        var text = new List<ShieldTextPart>();
        var covered = 0d;
        var total = 0d;
        for (var i = 0; i < shields.Count; i++)
        {
            var (part, blinks) = shields[i];
            var faded = blinks && fadedNow;
            var length = Math.Min(part, 1 - covered);
            var (start, end) = fromRight ? (1 - covered - length, 1 - covered) : (covered, covered + length);
            covered += length;
            total += part;
            if (length > 0 && start < hpRatio)
                segments.Add(new BarSegment(start, Math.Min(end, hpRatio), faded ? ShieldFadedBrush : ShieldBrush));
            if (length > 0 && end > hpRatio)
                segments.Add(new BarSegment(Math.Max(start, hpRatio), end, faded ? ShieldOverHpFadedBrush : ShieldOverHpBrush));
            if (shields.Count > 1)
                text.Add(new ShieldTextPart(i == 0 ? "(" : ",", 1));
            text.Add(new ShieldTextPart(shields.Count > 1 ? Points(part) : Points(part) + "%", faded ? FadedOpacity : 1));
        }

        if (shields.Count > 1)
            text.Add(new ShieldTextPart(") " + Points(total) + "%", 1));
        return ([.. segments], [.. text]);
    }

    // The real numbers behind the bar, in the system's number format: "20.580 / 24.212 HP", and below it what's left of
    // each shield, the oldest first: "Shield: 5.000 + 3.200 = 8.200".
    internal static string HpToolTip(long hp, long maxHp, IReadOnlyList<long> shields, string shieldFormat, CultureInfo culture)
    {
        var text = string.Format(culture, "{0:N0} / {1:N0} HP", hp, maxHp);
        if (shields.Count == 0)
            return text;

        var amounts = string.Join(" + ", shields.Select(amount => amount.ToString("N0", culture)));
        if (shields.Count > 1)
            amounts += " = " + shields.Sum().ToString("N0", culture);
        return text + "\n" + string.Format(culture, shieldFormat, amounts);
    }

    private static string Points(double part) =>
        Math.Max(1, (int)Math.Round(Math.Min(1, part) * 100)).ToString(CultureInfo.InvariantCulture);

    // Where a member is from you: an arrow (north up) coloured by height, the distance in whole metres coloured by the
    // marks in the settings, and dimmed past the range (the last mark). Yours comes from the auras you cast: for 2 s after
    // the last it's where you are, then for up to 10 s a guess, the arrow and distance faded. Out of your sight without a
    // position, they're farther than ~45 m: dimmed, as the game does.
    private void UpdatePlacement(GroupFrameMemberRow row, in GroupFrameMember member, Vector3? me, bool guessed, long now)
    {
        if (member.IsLocalPlayer || me is not { } mine || FreshPosition(member.EntityId, now, MemberPositionStaleMilliseconds, holdInSight: true) is not { } theirs)
        {
            row.HasArrow = false;
            row.IsOutOfRange = !member.IsLocalPlayer && PlayerPositions.IsOutOfSight(member.EntityId);
            return;
        }

        var (meters, bearing, rise) = PlayerPositions.Between(mine, theirs);
        // Coloured by the number shown, so "25 m" stays green with green up to 25.
        var shown = (int)Math.Round(meters);
        row.HasArrow = true;
        row.IsPlacementGuessed = guessed;
        row.IsOutOfRange = shown > settings.GroupFrameRangeMeters;
        row.DistanceText = string.Create(CultureInfo.InvariantCulture, $"{shown} m");
        row.DistanceBrush = DistanceBrush(shown, settings.GroupFrameGreenMeters, settings.GroupFrameOrangeMeters, settings.GroupFrameRangeMeters);
        row.ArrowRotation.Angle = bearing;
        row.ArrowBrush = rise > SameLevelMeters ? AboveBrush : rise < -SameLevelMeters ? BelowBrush : LevelBrush;
    }

    // Past the range first, so marks set out of order leave a colour out instead of painting an out-of-range member green.
    internal static IBrush DistanceBrush(int meters, int greenUpTo, int orangeUpTo, int range) =>
        meters > range ? DistanceOutOfRangeBrush
        : meters <= greenUpTo ? DistanceGreenBrush
        : meters <= orangeUpTo ? DistanceOrangeBrush
        : DistanceRedBrush;

    // Ours never holds just for being "in sight": the server only now and then sends our own moves, never all of them.
    private static Vector3? FreshPosition(int entityId, long now, long staleAfter, bool holdInSight) =>
        entityId > 0 && PlayerPositions.TryGet(entityId, out var fix) && (holdInSight && fix.InSight || now - fix.AtMilliseconds <= staleAfter) ? fix.Position : null;

    private bool HasDebuff(int entityId)
    {
        foreach (var aura in _auras)
        {
            if (aura.TargetEntityId == entityId && aura.Kind == GroupFrameAuraKind.Debuff)
                return true;
        }

        return false;
    }

    private static string FormatRemaining(long milliseconds) =>
        milliseconds > NoTimerMilliseconds
            ? string.Empty
            : milliseconds >= 60_000
                ? string.Create(CultureInfo.InvariantCulture, $"{milliseconds / 60_000}m")
                : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, (milliseconds + 999) / 1_000)}");
}

public sealed partial class GroupFrameMemberRow(int entityId) : ObservableObject
{
    public int EntityId { get; } = entityId;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ProgressSegment? HpBar { get; set; }

    [ObservableProperty]
    public partial string HpText { get; set; } = string.Empty;

    // Shown on hover: the HP and shield amounts the bar stands for (null: no HP known, nothing to show).
    [ObservableProperty]
    public partial string? HpToolTip { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<BarSegment> ShieldSegments { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ShieldTextPart> ShieldParts { get; set; } = [];

    [ObservableProperty]
    public partial bool IsDead { get; set; }

    [ObservableProperty]
    public partial bool HasDebuff { get; set; }

    // A boss's last aimed attack or spell went at them: red frame.
    [ObservableProperty]
    public partial bool IsBossTarget { get; set; }

    // How often they died in the current boss fight, beside the name (0: none, or turned off).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDeaths))]
    public partial int Deaths { get; set; }

    public bool HasDeaths => Deaths > 0;

    [ObservableProperty]
    public partial bool HasArrow { get; set; }

    [ObservableProperty]
    public partial IBrush? ArrowBrush { get; set; }

    // Past the range in the settings (40 m unless changed).
    [ObservableProperty]
    public partial bool IsOutOfRange { get; set; }

    // "23 m" beside the arrow, while it shows.
    [ObservableProperty]
    public partial string DistanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IBrush? DistanceBrush { get; set; }

    // Your own position is a few seconds old: the arrow and distance are a guess, shown faded.
    [ObservableProperty]
    public partial bool IsPlacementGuessed { get; set; }

    public RotateTransform ArrowRotation { get; } = new();

    // Laid out every frame; only a change goes to the view.
    public void SetShields(BarSegment[] segments, ShieldTextPart[] parts)
    {
        if (!ShieldSegments.SequenceEqual(segments))
            ShieldSegments = segments;
        if (!ShieldParts.SequenceEqual(parts))
            ShieldParts = parts;
    }

    public ObservableCollection<GroupFrameAuraIcon> Debuffs { get; } = [];

    public ObservableCollection<GroupFrameAuraIcon> MyAuras { get; } = [];

    public ObservableCollection<GroupFrameAuraIcon> OtherAuras { get; } = [];
}

// A piece of the shield text, faded while its shield blinks.
public sealed record ShieldTextPart(string Text, double Opacity);

public sealed partial class GroupFrameAuraIcon(int skillId, string? iconAssetName, string toolTip, string fallbackText, GroupFrameAuraKind kind,
    string? effect = null, string? effectNote = null, string? specializations = null, int casterId = 0) : ObservableObject
{
    public int SkillId { get; } = skillId;

    // The player whose aura it is (whose level the numbers are for), 0 for an NPC's.
    public int CasterId { get; } = casterId;

    public string? IconAssetName { get; } = iconAssetName;

    // The name, and when hovered below it what the skill does, the caster's specializations and which skill levels the
    // numbers span (see GroupFrameViewModel.DescribeAura).
    public string ToolTip { get; } = toolTip;

    public string? Effect { get; } = effect;

    public string? EffectNote { get; } = effectNote;

    public string? Specializations { get; } = specializations;

    // Shown where the game data has no icon.
    public string FallbackText { get; } = fallbackText;

    public bool IsDebuff { get; } = kind == GroupFrameAuraKind.Debuff;

    public bool IsOther { get; } = kind == GroupFrameAuraKind.Other;

    [ObservableProperty]
    public partial string RemainingText { get; set; } = string.Empty;
}
