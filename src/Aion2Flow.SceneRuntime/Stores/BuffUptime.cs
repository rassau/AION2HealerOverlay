using System.Numerics;
using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

// A buff as the uptime counts it: whose, and which skill (its effect when the skill isn't known).
public readonly record struct BuffKey(int CasterId, int SkillId, ResourceEffectRef EffectRef);

// How long a buff was on one member, as a share of the stretch.
public readonly record struct BuffOnMember(BuffKey Buff, int MemberId, double Uptime);

// A caster's buff: on themselves (null: never), and on the group (null: it reached no one else), i.e. of the time the others
// could have had it, the share they did.
public readonly record struct BuffOfCaster(BuffKey Buff, double? SelfUptime, double? GroupUptime);

public sealed record BuffUptimeReport(long FromMilliseconds, long ToMilliseconds, IReadOnlyList<BuffOnMember> OnMembers, IReadOnlyList<BuffOfCaster> OfCasters)
{
    public static BuffUptimeReport Empty { get; } = new(0, 0, [], []);
}

// Who kept which buff up over a stretch of a scene (all of it, or one boss fight), read from the journal the way the
// playback reads auras, so live and from the history alike; sampled every 250 ms. Buffs are what group members put on group
// members. For a caster's group uptime, another member counts at a moment they're in the caster's range or have the buff:
// a HoT still running on someone who walked off counts, and can't push it past 100 %. Without the buff, time they or the
// caster are dead, or their position is unknown, counts for nothing; so does any time the caster's position is unknown,
// and with none known at all (histories from before positions were kept) there is no group uptime.
public static class BuffUptime
{
    public const long StepMilliseconds = 250;
    // Older positions are unknown: in dungeons the others' come every ~2 s, in sight with every move, ours with our auras.
    public const long FreshPositionMilliseconds = 10_000;

    public static BuffUptimeReport Compute(SceneJournalSegment segment, long fromMilliseconds, long toMilliseconds, IReadOnlyCollection<int> memberIds, double rangeMeters, CancellationToken cancellationToken = default)
    {
        var samples = (int)((toMilliseconds - fromMilliseconds + StepMilliseconds - 1) / StepMilliseconds);
        if (segment.IsEmpty || samples <= 0 || memberIds.Count == 0)
            return BuffUptimeReport.Empty;

        var members = memberIds as IReadOnlySet<int> ?? new HashSet<int>(memberIds);
        var buffed = new Dictionary<BuffKey, Dictionary<int, bool[]>>();
        var coverages = ScenePlaybackAuraTimelineReader.Read(segment, members, toMilliseconds, cancellationToken).Coverages;
        // A renewal (0x2B38: recasting a buff that's still on, like a HoT kept up) overwrites the aura's origin with an id
        // that is no entity, and names its effect by the effect's own code (17090350 renewed reads 170903501): the caster
        // who opened the instance and its skill hold for all of it. Without this a kept-up HoT counted until its first recast.
        var openers = new Dictionary<(int Target, int Sequence), int>();
        foreach (var coverage in coverages)
        {
            if (members.Contains(coverage.OriginEntityId))
                openers.TryAdd((coverage.EntityId, coverage.InstanceSequenceId), coverage.OriginEntityId);
        }

        foreach (var coverage in coverages)
        {
            var caster = members.Contains(coverage.OriginEntityId)
                ? coverage.OriginEntityId
                : openers.GetValueOrDefault((coverage.EntityId, coverage.InstanceSequenceId));
            if (caster == 0 || coverage.Semantics.Disposition == AuraDisposition.Debuff)
                continue;

            var skillCode = SkillCodeOf(in coverage);
            var buff = new BuffKey(caster, skillCode, skillCode > 0 ? default : coverage.DisplayResourceEffectRef);
            if (!buffed.TryGetValue(buff, out var receivers))
                buffed[buff] = receivers = [];
            if (!receivers.TryGetValue(coverage.EntityId, out var marks))
                receivers[coverage.EntityId] = marks = new bool[samples];
            Array.Fill(marks, true, SampleAt(coverage.StartMilliseconds), Math.Max(0, SampleAt(coverage.EndMilliseconds) - SampleAt(coverage.StartMilliseconds)));
        }

        var (positions, dead) = ReadPositionsAndDeaths(segment, members, fromMilliseconds, samples, cancellationToken);
        var onMembers = new List<BuffOnMember>();
        var ofCasters = new List<BuffOfCaster>();
        foreach (var (buff, receivers) in buffed)
        {
            foreach (var (memberId, marks) in receivers)
                onMembers.Add(new BuffOnMember(buff, memberId, Count(marks) / (double)samples));

            double? self = receivers.TryGetValue(buff.CasterId, out var own) ? Count(own) / (double)samples : null;
            double? group = null;
            if (receivers.Keys.Any(memberId => memberId != buff.CasterId))
            {
                long had = 0, could = 0;
                foreach (var other in members)
                {
                    if (other == buff.CasterId)
                        continue;

                    receivers.TryGetValue(other, out var marks);
                    for (var i = 0; i < samples; i++)
                    {
                        // Where the caster stood isn't known: who was in range neither (histories from before positions).
                        if (positions[buff.CasterId][i] is null)
                            continue;

                        var has = marks?[i] == true;
                        if (has || !dead[other][i] && !dead[buff.CasterId][i] && InRange(positions[buff.CasterId][i], positions[other][i], rangeMeters))
                        {
                            could++;
                            if (has)
                                had++;
                        }
                    }
                }

                group = could > 0 ? had / (double)could : null;
            }

            ofCasters.Add(new BuffOfCaster(buff, self, group));
        }

        return new BuffUptimeReport(fromMilliseconds, toMilliseconds, onMembers, ofCasters);

        // Sample i stands for the moment from + (i + ½) step; this is the first one at or after the given time.
        int SampleAt(long milliseconds) => Math.Clamp((int)Math.Ceiling((milliseconds - fromMilliseconds) / (double)StepMilliseconds - 0.5), 0, samples);
    }

    // The skill a buff is counted under: the one the game data gives its effect, else its effect's code (a skill code with
    // the specialization picks, for most class buffs). Effect codes are skill codes with a digit or two more (170903501,
    // 1742001011 for the skill codes 17090350, 17420010): they're cut back to the skill's eight digits.
    internal static int SkillCodeOf(in ScenePlaybackAuraCoverage coverage)
    {
        long code = coverage.Semantics.Trace.ResourceSkillId > 0 ? coverage.Semantics.Trace.ResourceSkillId : coverage.DisplayResourceEffectRef.RawId;
        while (code >= 100_000_000)
            code /= 10;
        return (int)code;
    }

    // Each member's position (null: unknown) and whether they're dead, at every sample.
    private static (Dictionary<int, Vector3?[]> Positions, Dictionary<int, bool[]> Dead) ReadPositionsAndDeaths(
        SceneJournalSegment segment, IReadOnlySet<int> members, long fromMilliseconds, int samples, CancellationToken cancellationToken)
    {
        var fixes = members.ToDictionary(static id => id, static _ => new List<(long At, Vector3 Position)>());
        var vitals = members.ToDictionary(static id => id, static _ => new List<(long At, bool Dead)>());
        var cursor = segment.CreateCursor();
        while (cursor.NextObservationOrdinal < segment.CurrentEndObservationOrdinalExclusive)
        {
            var read = segment.ReadEntries(cursor, ScenePlaybackTimeline.DefaultReadBatchSize, entries =>
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = entries[i];
                    var at = ScenePlaybackTimeline.ResolveOffsetMilliseconds(entry);
                    if (entry.Domain == ObservedEventDomain.State && entry.State.StateCode == StateCodes.Position && fixes.TryGetValue(entry.State.EntityId, out var memberFixes))
                        memberFixes.Add((at, StateCodes.UnpackPosition(entry.State.Value0, entry.State.Value1)));
                    else if (entry.Domain == ObservedEventDomain.EntityVital && vitals.TryGetValue(entry.EntityVital.EntityId, out var memberVitals))
                        memberVitals.Add((at, entry.EntityVital.CurrentHp == 0));
                }
            });
            if (read.Count == 0)
                break;

            cursor = read.Cursor;
        }

        var positions = new Dictionary<int, Vector3?[]>();
        var dead = new Dictionary<int, bool[]>();
        foreach (var member in members)
        {
            var at = new Vector3?[samples];
            var isDead = new bool[samples];
            var fix = -1;
            var vital = -1;
            for (var i = 0; i < samples; i++)
            {
                var moment = fromMilliseconds + i * StepMilliseconds + StepMilliseconds / 2;
                while (fix + 1 < fixes[member].Count && fixes[member][fix + 1].At <= moment)
                    fix++;
                while (vital + 1 < vitals[member].Count && vitals[member][vital + 1].At <= moment)
                    vital++;
                if (fix >= 0 && moment - fixes[member][fix].At <= FreshPositionMilliseconds)
                    at[i] = fixes[member][fix].Position;
                isDead[i] = vital >= 0 && vitals[member][vital].Dead;
            }

            positions[member] = at;
            dead[member] = isDead;
        }

        return (positions, dead);
    }

    // Positions are in cm.
    private static bool InRange(Vector3? caster, Vector3? other, double rangeMeters) =>
        caster is { } from && other is { } to && Vector3.Distance(from, to) <= rangeMeters * 100;

    private static int Count(bool[] marks)
    {
        var count = 0;
        foreach (var mark in marks)
        {
            if (mark)
                count++;
        }

        return count;
    }
}
