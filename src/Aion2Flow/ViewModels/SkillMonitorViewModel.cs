using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cloris.Aion2Flow.ViewModels;

public sealed class SkillMonitorViewModel : ObservableObject
{
    private const int JournalReadBatchSize = 2_048;
    private readonly WinDivertCaptureService _captureService;
    private readonly GameResourceService _resources;
    private readonly LocalizationService _localization;
    private readonly SettingsService _settings;
    private readonly Predicate<int> _cooldownSelection;
    private readonly PacketCooldownTracker _cooldowns = new();
    private readonly SkillMonitorSkillSlotBuilder _skillSlotBuilder = new();
    private readonly SkillMonitorSkillSlotPresentationTracker _skillSlotPresentationTracker = new();
    private readonly List<AuraInstanceState> _activeAuras = [];
    private readonly List<AuraInstanceState> _targetAuras = [];
    private readonly List<SkillMonitorAuraCandidate> _auraCandidates = [];
    private readonly List<SkillMonitorAuraCandidate> _targetBuffCandidates = [];
    private readonly List<SkillMonitorAuraCandidate> _debuffCandidates = [];
    // Who of the openers of the auras on your target is on the other side from you (SceneLiveReadModel.CopyLocalPlayerAurasTo).
    private readonly HashSet<int> _enemies = [];
    // The game data's buff or debuff per aura effect (IsDebuff), looked up once each: the frames come on the UI thread, and
    // the combat registry's game data never changes after the first load (GameResourceService.Reload).
    private readonly Dictionary<uint, AuraDisposition> _dispositions = [];
    private readonly List<SkillMonitorSkillSlotData> _skillRowData = [];
    private readonly SkillMonitorSkillRowCollection _skillRows = [];
    private Guid _sessionId;
    private JournalCursor _journalCursor;
    // Your target as far as it's known (ReadCooldownJournal); 0 for none yet.
    private int _targetEntityId;
    private EncounterTimeDisplayFormat _encounterTimeDisplayFormat;

    public SkillMonitorViewModel(
        WinDivertCaptureService captureService,
        GameResourceService resources,
        LocalizationService localization,
        SettingsService settings)
    {
        _captureService = captureService;
        _resources = resources;
        _localization = localization;
        _settings = settings;
        _cooldownSelection = IsCooldownMonitored;
        _encounterTimeDisplayFormat = settings.Current.EncounterTimeDisplayFormat;
    }

    public ObservableCollection<SkillMonitorSkillSlot> SkillRows => _skillRows;

    public EncounterTimeDisplayFormat EncounterTimeDisplayFormat
    {
        get => _encounterTimeDisplayFormat;
        set => SetProperty(ref _encounterTimeDisplayFormat, value);
    }

    // The tutorial's demo fight at its end (TutorialDemoScene), shown instead of the game while the tour is at this frame.
    internal SceneLiveReadModel? DemoScene { get; set; }

    // slotsShown: false while the frame is minimized (FrameChrome), its slots unseen.
    public void ProcessUiFrame(bool slotsShown = true)
    {
        var scene = DemoScene ?? _captureService.Scene;
        if (DemoScene is null && !_captureService.IsDriverActive)
        {
            if (_skillRows.Count != 0)
                _skillRows.Clear();
            _skillSlotPresentationTracker.Clear();
            return;
        }

        // The target as the last frame's journal left it: a new one shows a frame later.
        var frame = scene.CopyLocalPlayerAurasTo(_activeAuras, _targetEntityId, _targetAuras, _enemies);
        EnsureSession(in frame);
        ReadCooldownJournal(scene.Journal, in frame);
        RefreshSkillRows(CollectionsMarshal.AsSpan(_activeAuras), CollectionsMarshal.AsSpan(_targetAuras), frame.EntityId, frame.TargetIsAlly, frame.TargetIsNpc,
            frame.ObservedAtMilliseconds, slotsShown);
    }

    private void EnsureSession(in SceneLocalPlayerFrame frame)
    {
        if (_sessionId == frame.SessionId)
            return;

        _sessionId = frame.SessionId;
        // Ids are per map, and a reset empties the scene's auras too.
        _targetEntityId = 0;
        _targetAuras.Clear();
        _cooldowns.Clear();
        _skillSlotPresentationTracker.Clear();
        _journalCursor = new JournalCursor(frame.StartObservationOrdinal);
    }

    private void ReadCooldownJournal(ObservedEventJournal journal, in SceneLocalPlayerFrame frame)
    {
        var localPlayerEntityId = frame.EntityId;

        while (_journalCursor.NextObservationOrdinal < frame.EndObservationOrdinalExclusive)
        {
            var result = journal.ReadEntries(_journalCursor, frame.EndObservationOrdinalExclusive, JournalReadBatchSize, entries =>
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    if (entry.Domain == ObservedEventDomain.Combat)
                    {
                        var combat = entry.Combat;
                        ObserveSkillPacket(
                            entry.ObservedAtMilliseconds,
                            entry.SourceEntityId,
                            localPlayerEntityId,
                            entry.Opcode,
                            in combat);
                        // ponytail: the game never sends your own target (0x3238 names only others'), so whom your last
                        // aimed skill went at (0x0238 flag 2, its target in ChainId) stands in; a target picked without
                        // casting at it isn't followed. Take the game's own should a packet ever name it.
                        if (entry.Opcode == 0x0238 && combat.Flag == 2 && combat.ChainId > 0 && combat.ChainId != localPlayerEntityId &&
                            PacketCooldownSourceFilter.MatchesKnownLocalPlayer(localPlayerEntityId, entry.SourceEntityId))
                            _targetEntityId = combat.ChainId;
                    }
                    else if (entry.Domain == ObservedEventDomain.State && entry.State.StateCode == StateCodes.CooldownStart0238)
                    {
                        ObserveCooldownStart(
                            entry.ObservedAtMilliseconds,
                            entry.SourceEntityId,
                            localPlayerEntityId,
                            entry.State.Value0,
                            entry.State.Value1,
                            entry.State.DetailRaw);
                    }
                    else if (entry.Domain == ObservedEventDomain.State && entry.State.StateCode == StateCodes.Cooldown4738)
                    {
                        ObserveCooldownUpdate(
                            entry.ObservedAtMilliseconds,
                            entry.State.Value0,
                            entry.State.Value1);
                    }
                    else if (entry.Domain == ObservedEventDomain.State && entry.State.StateCode == StateCodes.CooldownCharge2238)
                    {
                        ObserveCooldownCharge(
                            entry.ObservedAtMilliseconds,
                            entry.State.Value0,
                            entry.State.Value1,
                            entry.State.DetailRaw);
                    }
                }
            });

            _journalCursor = result.Cursor;
            if (result.Count == 0)
                return;
        }
    }

    private bool ObserveCooldownStart(
        long observedAtMilliseconds,
        int sourceEntityId,
        int localPlayerEntityId,
        long skillValue,
        long remainingValue,
        long detailRaw)
    {
        if (skillValue is <= 0 or > int.MaxValue || remainingValue is <= 0 or > int.MaxValue)
        {
            return false;
        }
        if (!CooldownStartObservationDetail.TryDecode(detailRaw, out _, out var availableCountAfterControl))
            return false;

        var packetSkillCode = (int)skillValue;
        if (!TryNormalizePacketSkillCode(packetSkillCode, out var normalizedSkillId))
            return false;
        return _cooldowns.ObserveStart0238(
            localPlayerEntityId,
            sourceEntityId,
            normalizedSkillId,
            packetSkillCode,
            (int)remainingValue,
            observedAtMilliseconds,
            _resources.ResolveMaxAvailableCount(packetSkillCode),
            availableCountAfterControl);
    }

    private bool ObserveCooldownUpdate(long observedAtMilliseconds, long rowBaseSkillValue, long remainingValue)
    {
        if (rowBaseSkillValue is <= 0 or > int.MaxValue || remainingValue is < 0 or > int.MaxValue)
            return false;

        var packetSkillCode = (int)rowBaseSkillValue;
        if (!TryNormalizePacketSkillCode(packetSkillCode, out var rowBaseSkillId))
            return false;
        return _cooldowns.ObserveUpdate4738(
            rowBaseSkillId,
            packetSkillCode,
            (int)remainingValue,
            observedAtMilliseconds,
            _resources.ResolveMaxAvailableCount(packetSkillCode));
    }

    private bool ObserveCooldownCharge(
        long observedAtMilliseconds,
        long skillValue,
        long remainingValue,
        long detailRaw)
    {
        if (skillValue is <= 0 or > int.MaxValue || remainingValue is < 0 or > int.MaxValue ||
            !CooldownChargeObservationDetail.TryDecode(detailRaw, out _, out var availableCount))
        {
            return false;
        }

        var packetSkillCode = (int)skillValue;
        if (!TryNormalizePacketSkillCode(packetSkillCode, out var rowBaseSkillId))
            return false;
        return _cooldowns.ObserveCharge2238(
            rowBaseSkillId,
            packetSkillCode,
            availableCount,
            (int)remainingValue,
            observedAtMilliseconds);
    }

    private bool ObserveSkillPacket(
        long observedAtMilliseconds,
        int sourceEntityId,
        int localPlayerEntityId,
        ushort opcode,
        in CombatWireObservation combat)
    {
        if (!PacketCooldownSourceFilter.MatchesKnownLocalPlayer(localPlayerEntityId, sourceEntityId) ||
            opcode is not (0x0238 or 0x0438))
            return false;

        var hasSkillCode = combat.SkillCode > 0 || combat.BodySkillVariantRaw > 0 || combat.BodyCodeRaw > 0;
        if (!hasSkillCode)
            return false;

        var packetSkillCode = combat.SkillCode > 0
            ? combat.SkillCode
            : combat.BodySkillVariantRaw > 0
                ? combat.BodySkillVariantRaw
                : combat.BodyCodeRaw <= int.MaxValue
                    ? (int)combat.BodyCodeRaw
                    : 0;
        if (!TryNormalizePacketSkillCode(packetSkillCode, out var rowBaseSkillId))
            return false;

        return opcode == 0x0238 &&
            _cooldowns.ObserveControl0238(
                rowBaseSkillId,
                packetSkillCode,
                sourceEntityId,
                observedAtMilliseconds,
                _resources.ResolveMaxAvailableCount(packetSkillCode));
    }

    private bool TryNormalizePacketSkillCode(int packetSkillCode, out int normalizedSkillId)
    {
        if (packetSkillCode <= 0)
        {
            normalizedSkillId = 0;
            return false;
        }

        var skillId = packetSkillCode;
        if (!_resources.ContainsSkill(packetSkillCode) &&
            CombatResourceRegistry.TryResolveSkillIdByEffectRef(
                ResourceEffectRef.FromRaw(unchecked((uint)packetSkillCode)),
                out var effectSkillId))
        {
            skillId = effectSkillId;
        }

        normalizedSkillId = _resources.ResolveBaseSkillIdForCode(skillId);
        return normalizedSkillId > 0;
    }

    // auras: on you, targetAuras: everyone's on your target, an ally when targetIsAlly, an NPC when targetIsNpc
    // (SceneLocalPlayerFrame).
    private void RefreshSkillRows(ReadOnlySpan<AuraInstanceState> auras, ReadOnlySpan<AuraInstanceState> targetAuras, int localPlayerEntityId, bool targetIsAlly,
        bool targetIsNpc, long nowMilliseconds, bool slotsShown)
    {
        // The "from anyone" settings are about your target only. There the game data tells a buff from a debuff
        // (IsDebuff); for a skill it labels both or neither, who opened the aura does: one opened from the other side than
        // the one it's on is a debuff. On an ally that's an enemy player's (_enemies), on an enemy player anyone's but its
        // own and its side's, on an NPC (an Abyss boss both factions fight) anyone's but its own.
        // ponytail: sides go by faction (your party and force always yours, a player who fought you or them on this map
        // always the other side), and anyone else not known to be of the other faction counts as yours: for such skills,
        // buffs from an enemy's allies who read as yours (no known faction, or yours as in battlefields) count as debuffs
        // (its own don't), and such an enemy's debuffs on your allies, and your side's on them (yours too: with no buff
        // ticked they're gone), as buffs, until they fought you or your group.
        var settings = _settings.Current;

        // Buffs on you, whoever put them there: a debuff there too, as before the settings.
        _auraCandidates.Clear();
        for (var index = 0; index < auras.Length; index++)
        {
            var aura = auras[index];
            if (!TryResolveAuraSkillId(aura.ResourceEffectRef, out var rowBaseSkillId))
                continue;
            if (!IsBuffMonitored(rowBaseSkillId))
                continue;

            _auraCandidates.Add(new SkillMonitorAuraCandidate(
                aura.TargetEntityId,
                rowBaseSkillId,
                aura.DurationMilliseconds,
                aura.ExpiresAtMilliseconds));
        }

        // On your target, by who opened it and with what: a renewal overwrites both in the store (AuraStore). Yours there
        // count as debuffs, as before these settings, unless "Buffs from anyone" takes them as buffs; "Buffs from anyone"
        // takes everyone's buffs there, "Debuffs from anyone" everyone's debuffs.
        // ponytail: your summons' debuffs (Elementalist spirits) count only from anyone; add the owner lookup
        // (SceneSinkFactory's PlayerOf) when a summoner asks for them.
        _targetBuffCandidates.Clear();
        _debuffCandidates.Clear();
        var fromAnyone = settings.SkillMonitorBuffsFromAnyone || settings.SkillMonitorDebuffsFromAnyone;
        for (var index = 0; index < targetAuras.Length; index++)
        {
            var aura = targetAuras[index];
            var mine = localPlayerEntityId > 0 && aura.OpenerEntityId == localPlayerEntityId;
            var debuff = fromAnyone && IsDebuff(aura.OpenerEffectRef, targetIsAlly
                ? _enemies.Contains(aura.OpenerEntityId)
                : aura.OpenerEntityId != aura.TargetEntityId && (targetIsNpc || !_enemies.Contains(aura.OpenerEntityId)));
            var buff = settings.SkillMonitorBuffsFromAnyone && !debuff;
            if (!(buff || mine || settings.SkillMonitorDebuffsFromAnyone && debuff) ||
                !TryResolveAuraSkillId(aura.OpenerEffectRef, out var rowBaseSkillId) ||
                !(buff ? IsBuffMonitored(rowBaseSkillId) : IsDebuffMonitored(rowBaseSkillId)))
                continue;

            (buff ? _targetBuffCandidates : _debuffCandidates).Add(new SkillMonitorAuraCandidate(
                aura.TargetEntityId,
                rowBaseSkillId,
                aura.DurationMilliseconds,
                aura.ExpiresAtMilliseconds));
        }

        var slots = _skillSlotBuilder.Build(
            CollectionsMarshal.AsSpan(_auraCandidates),
            localPlayerEntityId,
            _cooldowns.States,
            _cooldownSelection,
            nowMilliseconds,
            CollectionsMarshal.AsSpan(_debuffCandidates),
            CollectionsMarshal.AsSpan(_targetBuffCandidates));
        var presentations = _skillSlotPresentationTracker.Update(
            slots,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        // ponytail: minimized, the cooldowns, the slots and when a cooldown ended (the flash) are followed above, as they
        // keep state; the slots' texts and the rows catch up as the frame shows again (FrameChrome's showing), before
        // it's laid out or drawn.
        if (!slotsShown)
            return;

        _skillRowData.Clear();
        for (var index = 0; index < presentations.Length; index++)
        {
            var presentation = presentations[index];
            var slot = presentation.Slot;
            var buffRemainingText = slot.BuffTimer is { } buffTimer ? FormatTimer(in buffTimer) : string.Empty;
            var cooldownRemainingText = slot.CooldownTimer is { } cooldownTimer ? FormatTimer(in cooldownTimer) : string.Empty;
            var debuffRemainingText = slot.DebuffTimer is { } debuffTimer ? FormatTimer(in debuffTimer) : string.Empty;
            _skillRowData.Add(new SkillMonitorSkillSlotData(
                slot.RowBaseSkillId,
                _resources.ResolveSkillIconAssetName(slot.RowBaseSkillId),
                CreateSkillToolTip(in slot, buffRemainingText, cooldownRemainingText, debuffRemainingText),
                slot.BuffTimer?.ProgressValue ?? 0d,
                slot.BuffTimer is not null,
                buffRemainingText,
                slot.CooldownTimer?.ProgressValue ?? 0d,
                cooldownRemainingText,
                slot.CooldownTimer is not null,
                slot.AvailableCount,
                slot.AvailableCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                slot.AvailableCount is not null,
                presentation.CompletionStartedUtcMilliseconds,
                slot.DebuffTimer is not null,
                debuffRemainingText));
        }

        _skillRows.Reconcile(CollectionsMarshal.AsSpan(_skillRowData));
    }

    // A debuff as the game data labels the aura (AuraSemanticEvidenceResolver.ResolveDisposition); where it says both or
    // neither, as otherSide (opened from the other side than the one it's on).
    private bool IsDebuff(ResourceEffectRef effect, bool otherSide)
    {
        if (!_dispositions.TryGetValue(effect.RawId, out var disposition))
            _dispositions.Add(effect.RawId, disposition = AuraSemanticEvidenceResolver.ResolveDisposition(effect));

        return disposition switch
        {
            AuraDisposition.Debuff => true,
            AuraDisposition.Buff => false,
            _ => otherSide
        };
    }

    private bool IsBuffMonitored(int rowBaseSkillId)
        => SkillMonitorSelection.IncludesBuff(_settings.Current, _resources, rowBaseSkillId);

    private bool IsCooldownMonitored(int rowBaseSkillId)
        => SkillMonitorSelection.IncludesCooldown(_settings.Current, _resources, rowBaseSkillId);

    private bool IsDebuffMonitored(int rowBaseSkillId)
        => SkillMonitorSelection.IncludesDebuff(_settings.Current, _resources, rowBaseSkillId);

    private string CreateSkillToolTip(
        in SkillMonitorSkillSlotState slot,
        string buffRemainingText,
        string cooldownRemainingText,
        string debuffRemainingText)
    {
        var name = ResolveLocalizedSkillName(slot.RowBaseSkillId);
        // Each case in one string: no part built only to be copied into a longer one.
        if (slot.BuffTimer is null && slot.CooldownTimer is null)
            return slot.DebuffTimer is null ? name : $"{name}\n{_localization["SkillMonitor_Debuffs"]}: {debuffRemainingText}";

        var buffs = slot.BuffTimer is null ? "-" : buffRemainingText;
        var cooldowns = slot.CooldownTimer is null ? "-" : cooldownRemainingText;
        return slot.DebuffTimer is null
            ? $"{name}\n{_localization["SkillMonitor_Buffs"]}: {buffs}\n{_localization["SkillMonitor_Cooldowns"]}: {cooldowns}"
            : $"{name}\n{_localization["SkillMonitor_Buffs"]}: {buffs}\n{_localization["SkillMonitor_Cooldowns"]}: {cooldowns}\n{_localization["SkillMonitor_Debuffs"]}: {debuffRemainingText}";
    }

    private bool TryResolveAuraSkillId(ResourceEffectRef effectRef, out int normalizedSkillId)
    {
        normalizedSkillId = 0;
        if (effectRef.IsEmpty)
        {
            return false;
        }

        if (CombatResourceRegistry.TryResolveSkillIdByEffectRef(effectRef, out var ownerSkillId))
        {
            normalizedSkillId = _resources.ResolveBaseSkillIdForCode(ownerSkillId);
            if (normalizedSkillId > 0)
            {
                return true;
            }
        }

        if (CombatResourceRegistry.TryResolveAuraResourceSemantics(effectRef, out var resolution) &&
            resolution.Slot is { SkillId: > 0 } slot)
        {
            normalizedSkillId = _resources.ResolveBaseSkillIdForCode(slot.SkillId);
            if (normalizedSkillId > 0)
            {
                return true;
            }
        }

        if (effectRef.RawId <= int.MaxValue && _resources.ContainsSkill((int)effectRef.RawId))
        {
            normalizedSkillId = _resources.ResolveBaseSkillIdForCode((int)effectRef.RawId);
            return normalizedSkillId > 0;
        }

        normalizedSkillId = 0;
        return false;
    }

    private string ResolveLocalizedSkillName(int skillId)
        => TryResolveLocalizedSkillName(skillId, out var name)
            ? name
            : _localization["SkillMonitor_UnknownSkill"];

    private bool TryResolveLocalizedSkillName(int skillId, out string name)
    {
        if (skillId <= 0)
        {
            name = string.Empty;
            return false;
        }

        var rowBaseSkillId = _resources.ResolveBaseSkillIdForCode(skillId);
        name = _resources.ResolveSkillName(rowBaseSkillId);
        if (string.IsNullOrWhiteSpace(name) ||
            string.Equals(name, rowBaseSkillId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            name = string.Empty;
            return false;
        }

        return true;
    }

    private string FormatDuration(long milliseconds)
        => SkillMonitorTimeFormatter.Format(milliseconds, EncounterTimeDisplayFormat);

    private string FormatTimer(in SkillMonitorTimer timer)
        => timer.IsIndefinite ? _localization["SkillMonitor_Indefinite"] : FormatDuration(timer.RemainingMilliseconds);

}
