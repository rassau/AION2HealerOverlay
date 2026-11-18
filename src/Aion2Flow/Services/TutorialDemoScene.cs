using System.Numerics;
using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.Services;

// The tutorial's demo fight: four made-up players, HappyCat (the user, a cleric), TankyBear (templar), FlameFox (sorcerer)
// and AngryRaccoon (gladiator), kill Vakron on Vakron Sky Island in a minute and forty seconds. It's built packet by packet
// into a scene of its own, the way the capture journals a real fight, so the meter, the details, the fight graphs and the
// playback read it like any history entry, and the frames can read how it stood at the end. Nothing of it reaches the live
// scene, the history or the disk unless the tutorial puts it there. Every run builds the same fight.
internal static class TutorialDemoScene
{
    public const int HappyCatId = 21_001;
    public const int TankyBearId = 21_002;
    public const int FlameFoxId = 21_003;
    public const int AngryRaccoonId = 21_004;
    public const int VakronId = 31_001;
    // Every demo player's home server: KR Siel, which no digital twin lookup covers (DigitalTwinService.RegionOf), so none
    // of the made-up names can reach the website.
    public const int ServerId = 1001;
    public const int VakronNpcCode = 2_300_582;
    public const uint MapId = 600_071; // Vakron Sky Island
    public const long VakronMaxHp = 12_600_000;
    // AngryRaccoon's Ruinous Blow takes what's left of Vakron; the scene ends a moment later, the way the frames show it.
    private const long KillAt = 100_600;

    // From the fight's start to the end of the scene.
    public static TimeSpan Length { get; } = TimeSpan.FromMilliseconds(KillAt + 1_400);

    // started: when the fight began (the tutorial passes now minus Length, so the scene ends now).
    public static TutorialDemo Build(DateTimeOffset started)
    {
        // The skill data decides what a value is (a shield grant is only a shield by it); loaded at start-up already.
        CombatResourceRegistry.EnsureCombatResources();
        var endedAt = started + Length;
        var scene = new SceneLiveReadModel(started, new FixedTimeProvider(endedAt));
        new Fight(SceneSinkFactory.CreateForLive(scene)(), started.ToUnixTimeMilliseconds()).Run();
        var record = new ArchivedEncounterRecord { ArchivedAt = started, Trigger = EncounterArchiveService.TutorialTrigger, ScenePayload = scene.CreateArchivePayload() };
        return new TutorialDemo(record, scene, endedAt);
    }

    // One second of the fight is one flush (FlushId 1 again and again; CompleteFlush makes each a new one), its turns a
    // fixed beat apart: Vakron at 0, TankyBear at 150, HappyCat's attack at 300, FlameFox at 450, AngryRaccoon at 600,
    // HappyCat's heal at 700, the HoT and DoT ticks at 800, positions and ping at 900. So no two hits on Vakron are less
    // than 100 ms apart, and a stagger gauge drop 5 ms after its hit goes to that hit alone (StaggerAttributor looks 30 ms
    // back). Every hit and heal on a player is followed by their HP (0x008D, as the game sends it on every change): real
    // healing is what that HP shows, so heals on someone near full are partly overhealing.
    private sealed class Fight(IRuntimeObservationSink sink, long startedAt)
    {
        private const int RapidSlice = 1_802_435, FlameBreath = 1_806_580, SurgingLava = 1_806_535, VakronsGaze = 1_900_700;
        private const int HealingLight = 17_100_140, RadiantRecovery = 17_120_010, LightOfRegeneration = 17_090_350, YustielsPower = 17_420_010,
            LightOfProtection = 17_410_030, Bolt = 17_060_010, JudgmentThunder = 17_040_010, DebilitatingMark = 17_080_240;
        private const int ViciousStrike = 12_010_010, ShieldSmite = 12_100_010, Judgment = 12_241_450;
        private const int FlameArrow = 15_210_010, Blaze = 15_050_010, Hellfire = 15_060_010;
        private const int RendingBlow = 11_010_350, OverheadSlam = 11_170_450, RuinousBlow = 11_100_450;
        // Yustiel's Power's shield effect, as its grant names it.
        private const uint YustielsPowerShield = 1_742_001_011;

        // The party in list order, with max HP; where each stands (cm, Vakron at 0): the tank and AngryRaccoon at his sides,
        // HappyCat 16 m back, FlameFox 20 m off.
        private static readonly (int Id, string Name, CharacterClass Class, long MaxHp, Vector3 Spot)[] Party =
        [
            (HappyCatId, "HappyCat", CharacterClass.Cleric, 61_000, new(-1_400, -700, 0)),
            (TankyBearId, "TankyBear", CharacterClass.Templar, 92_000, new(250, 0, 0)),
            (FlameFoxId, "FlameFox", CharacterClass.Sorcerer, 54_000, new(-2_100, 1_100, 0)),
            (AngryRaccoonId, "AngryRaccoon", CharacterClass.Gladiator, 57_000, new(-100, -250, 0)),
        ];

        private readonly Random _random = new(7);
        private readonly Dictionary<int, long> _hp = Party.ToDictionary(static member => member.Id, static member => member.MaxHp);
        private readonly Dictionary<int, long> _maxHp = Party.ToDictionary(static member => member.Id, static member => member.MaxHp);
        // When HappyCat's Light of Regeneration runs out on whom.
        private readonly Dictionary<int, long> _regeneration = [];
        private long _sequence;
        private int _auraSequence = 100;
        private long _vakronHp = VakronMaxHp;
        private int _vakronTarget = TankyBearId;
        private int _gauge = 100;
        private long _staggeredUntil;

        public void Run()
        {
            Setup();
            for (var second = 1; ; second++)
            {
                var t = second * 1_000L;
                VakronTurn(second, t);
                TankyBearTurn(second, t + 150);
                HappyCatAttacks(second, t + 300);
                FlameFoxTurn(second, t + 450);
                if (t + 600 == KillAt)
                {
                    Hit(KillAt, AngryRaccoonId, VakronId, RuinousBlow, _vakronHp);
                    break;
                }

                AngryRaccoonTurn(second, t + 600);
                HappyCatHeals(second, t + 700);
                Ticks(second, t + 800);
                Surroundings(second, t + 900);
                sink.CompleteFlush(1);
            }

            // Vakron is down (his 0 HP came with the blow), the boss frame lets go, everyone stays where they stood.
            sink.AppendBossTarget(At(KillAt + 300, 0x2092), 0);
            sink.SetNpcBattle(At(KillAt + 400, 0x218D), VakronId, false);
            Positions(KillAt + 900, 101);
            sink.CompleteFlush(1);
        }

        // The map first: a map set after combat would start a new scene. Then who's there (the user's own character
        // 0x3336, the others' spawns 0x4536), the party list (0x0092, one list), HappyCat's group buff, and Vakron.
        private void Setup()
        {
            sink.SetCurrentMap(At(0, 0x5196), MapId);
            foreach (var member in Party)
            {
                var isMe = member.Id == HappyCatId;
                sink.AppendNickname(At(50, isMe ? (ushort)0x3336 : (ushort)0x4536), member.Id, member.Name, characterClass: member.Class, isLocalPlayer: isMe, originServerId: ServerId);
            }

            PartyList(60);
            foreach (var member in Party)
                SetHp(70, member.Id, member.MaxHp);
            Positions(80, 0);
            sink.AppendNpcCode(At(300, 0x4036), VakronId, VakronNpcCode);
            sink.AppendNpcKind(At(300, 0x4036), VakronId, NpcKind.Boss);
            sink.AppendNpcHp(At(300, 0x008D), VakronId, VakronMaxHp, VakronMaxHp);
            sink.AppendStaggerGauge(At(300, 0x05E0), VakronId, 100, 100);
            foreach (var member in Party)
                Aura(500, member.Id, HappyCatId, LightOfProtection, 60_000);
            sink.SetNpcBattle(At(800, 0x218D), VakronId, true);
            sink.AppendBossTarget(At(900, 0x2092), TankyBearId);
            sink.CompleteFlush(1);
        }

        // Rapid Slice at whom the boss frame names every other second, Flame Breath on everyone three times, Surging Lava
        // under FlameFox and AngryRaccoon (the second one ends FlameFox for a while, the last leaves them low), his Gaze on
        // AngryRaccoon. Staggered he does nothing until the gauge is back.
        private void VakronTurn(int second, long t)
        {
            if (_gauge == 0)
            {
                if (t < _staggeredUntil)
                    return;

                _gauge = 100;
                sink.AppendStaggerGauge(At(t, 0x05E0), VakronId, 100, 100);
            }

            switch (second)
            {
                case 25 or 55 or 85:
                    foreach (var member in Party)
                    {
                        if (_hp[member.Id] > 0)
                            Hit(t + 50, VakronId, member.Id, FlameBreath, Vary(11_000));
                    }

                    break;
                case 40 or 80:
                    Hit(t + 50, VakronId, FlameFoxId, SurgingLava, second == 80 ? _hp[FlameFoxId] + Vary(4_000) : Vary(15_000));
                    Hit(t + 60, VakronId, AngryRaccoonId, SurgingLava, Vary(15_000));
                    break;
                case 90:
                    // A debuff names Vakron as its origin here (the game names the target), so the Buffs tab leaves it out.
                    Aura(t, AngryRaccoonId, VakronId, VakronsGaze, 20_500);
                    break;
                case 100:
                    Hit(t + 50, VakronId, FlameFoxId, SurgingLava, Math.Max(Vary(9_000), _hp[FlameFoxId] - _maxHp[FlameFoxId] * 45 / 100));
                    Hit(t + 60, VakronId, AngryRaccoonId, SurgingLava, Math.Max(Vary(9_000), _hp[AngryRaccoonId] - _maxHp[AngryRaccoonId] * 25 / 100));
                    break;
                default:
                    if (second % 2 == 0)
                    {
                        // The aimed cast (0x0238 flag 2, its target in the echo) is what the scene's aggro counts.
                        sink.RegisterCompactControl0238(At(t, 0x0238), VakronId, 0, RapidSlice, 2, 2, _vakronTarget);
                        Hit(t + 50, VakronId, _vakronTarget, RapidSlice, Vary(6_500));
                    }

                    break;
            }
        }

        private void TankyBearTurn(int second, long t)
        {
            var (skill, damage) = second % 8 == 4 ? (ShieldSmite, 15_000) : second % 12 == 6 ? (Judgment, 32_000) : (ViciousStrike, 22_000);
            Hit(t, TankyBearId, VakronId, skill, Vary(damage));
            if (skill == ShieldSmite)
                Stagger(t, 6);
        }

        // About 5.5 % of Vakron's HP over the fight, at an even pace: past 3.5 % of the damage dealt from early on, past
        // 3.5 % of his HP only in the second half. Debilitating Mark every 10 s, aimed at Vakron (0x0238 flag 2, its target
        // in the echo), its debuff on him for 10 s; the last one still runs at the end, where the skill monitor shows it.
        private void HappyCatAttacks(int second, long t)
        {
            if (second % 10 == 5)
            {
                sink.RegisterCompactControl0238(At(t, 0x0238), HappyCatId, 0, DebilitatingMark, 2, 2, VakronId);
                Aura(t + 50, VakronId, HappyCatId, DebilitatingMark, 10_000);
            }
            else if (second % 10 == 6)
            {
                Hit(t, HappyCatId, VakronId, JudgmentThunder, Vary(20_000));
                Stagger(t, 5);
            }
            else if (second % 2 == 0)
            {
                Hit(t, HappyCatId, VakronId, Bolt, Vary(8_600));
            }
        }

        private void FlameFoxTurn(int second, long t)
        {
            if (_hp[FlameFoxId] == 0)
                return;

            var (skill, damage) = second % 15 == 7 ? (Hellfire, 150_000) : second % 6 == 2 ? (Blaze, 62_000) : (FlameArrow, 40_000);
            Hit(t, FlameFoxId, VakronId, skill, Vary(damage));
        }

        private void AngryRaccoonTurn(int second, long t)
        {
            var (skill, damage) = second % 12 == 9 ? (RuinousBlow, 140_000) : second % 5 == 1 ? (OverheadSlam, 48_000) : (RendingBlow, 31_000);
            Hit(t, AngryRaccoonId, VakronId, skill, Vary(damage));
            if (skill == OverheadSlam)
                Stagger(t, 2);
        }

        // Light of Regeneration kept up on the tank, Light of Protection renewed, Yustiel's Power before the big hits and
        // at the end, FlameFox raised, herself topped up before the end; otherwise Healing Light on the lowest, or Radiant
        // Recovery when most are hurt.
        private void HappyCatHeals(int second, long t)
        {
            switch (second)
            {
                case 2 or 17 or 32 or 47 or 62 or 77 or 92:
                    Regenerate(t, TankyBearId);
                    return;
                case 91:
                    Regenerate(t, AngryRaccoonId);
                    return;
                case 61:
                    foreach (var member in Party)
                        Aura(t, member.Id, HappyCatId, LightOfProtection, 60_000);
                    return;
                case 24 or 99:
                    Shield(t, TankyBearId, Vary(12_000));
                    return;
                case 54 or 98:
                    Shield(t, HappyCatId, Vary(9_000));
                    return;
                case 97:
                    Heal(t, HappyCatId, HealingLight, Vary(18_000));
                    return;
                case 87:
                    SetHp(t, FlameFoxId, _maxHp[FlameFoxId] * 40 / 100);
                    return;
            }

            var living = Party.Where(member => _hp[member.Id] > 0).Select(static member => member.Id).ToList();
            var lowest = living.MinBy(Health);
            var hurt = living.Count(id => Health(id) < 0.85);
            if (Health(lowest) < 0.6 || Health(lowest) < 0.8 && hurt < 3)
            {
                Heal(t, lowest, HealingLight, Vary(18_000));
            }
            else if (hurt >= 3)
            {
                foreach (var id in living)
                    Heal(t, id, RadiantRecovery, Vary(7_000));
            }
        }

        private void Regenerate(long t, int target)
        {
            Aura(t, target, HappyCatId, LightOfRegeneration, 15_000);
            _regeneration[target] = t + 15_000;
        }

        // On even seconds Light of Regeneration ticks (0x0538 periodic healing), on odd ones Debilitating Mark's damage over
        // time on Vakron.
        private void Ticks(int second, long t)
        {
            if (second % 2 == 0)
            {
                foreach (var (target, endsAt) in _regeneration)
                {
                    if (t < endsAt && _hp[target] > 0)
                        Heal(t, target, LightOfRegeneration, Vary(3_000), tick: true);
                }
            }
            else if (second >= 3)
            {
                Hit(t + 50, HappyCatId, VakronId, DebilitatingMark, Vary(1_750), tick: true);
            }
        }

        // Positions every 2 s (the buff uptime counts who was in range), the user's ping every 5 s; the boss frame turns
        // to HappyCat for five seconds and back.
        private void Surroundings(int second, long t)
        {
            if (second % 2 == 0)
                Positions(t, second);
            if (second % 5 == 0)
                sink.AppendPing(At(t + 20, 0x0336), 38 + second / 5 % 4 * 2.5);
            if (second == 60)
                PartyList(t + 30);
            if (second is 43 or 48)
            {
                _vakronTarget = second == 43 ? HappyCatId : TankyBearId;
                sink.AppendBossTarget(At(t + 50, 0x2092), _vakronTarget);
            }
        }

        private void Positions(long t, int second)
        {
            for (var i = 0; i < Party.Length; i++)
            {
                var step = new Vector3((second * 37 + i * 11) % 9 - 4, (second * 23 + i * 7) % 7 - 3, 0) * 25;
                sink.AppendPosition(At(t, 0x1B37), Party[i].Id, Party[i].Spot + step);
            }
        }

        // One list: the group frame counts the members of the newest list, told apart by its capture sequence.
        private void PartyList(long t)
        {
            var list = new PacketObservationSource(startedAt + t, 1, 0x0092, 0, ++_sequence, default);
            for (var slot = 0; slot < Party.Length; slot++)
                sink.AppendPlayerGroupMember(list, Party[slot].Id, PlayerGroupMembership.Party((byte)slot));
        }

        // The gauge drops right after the hit that wore it down; empty, Vakron is staggered for 8 s, then it's full again.
        private void Stagger(long t, int points)
        {
            if (_gauge == 0)
                return;

            _gauge = Math.Max(0, _gauge - points);
            sink.AppendStaggerGauge(At(t + 5, 0x05E0), VakronId, _gauge, _gauge == 0 ? 0 : 100);
            if (_gauge == 0)
                _staggeredUntil = t + 8_000;
        }

        // Yustiel's Power: its aura on the target, then the shield pool it grants (0x0538 mode 9), chained to that aura.
        private void Shield(long t, int target, long amount)
        {
            var sequence = Aura(t, target, HappyCatId, YustielsPower, 12_000);
            sink.AppendCombatWireObservation(At(t, 0x0538), HappyCatId, target, new CombatWireObservation
            {
                SkillCode = YustielsPower,
                BodyResourceEffectRef = ResourceEffectRef.FromRaw(YustielsPowerShield),
                Damage = amount,
                HitCount = 1,
                AttemptCount = 1,
                ChainId = sequence,
                PeriodicRelation = target == HappyCatId ? PeriodicEffectRelation.Self : PeriodicEffectRelation.Target,
                PeriodicMode = 9,
                PeriodicTailSkillCodeRaw = YustielsPower,
                PeriodicTailLength = 4
            });
        }

        // An aura opening (0x2A38) with its duration; returns its instance sequence.
        private int Aura(long t, int target, int origin, int effect, ushort durationMilliseconds)
        {
            var sequence = ++_auraSequence;
            sink.RegisterObservation2A38(At(t, 0x2A38), target, 1, 19, sequence, 0, durationMilliseconds, 0, 0, 0, origin, 1, ResourceEffectRef.FromRaw((uint)effect), 0, 0, 0);
            return sequence;
        }

        private void Hit(long t, int source, int target, int skill, long damage, bool tick = false) =>
            Value(t, source, target, skill, damage, CombatResourceKind.Unknown, tick);

        private void Heal(long t, int target, int skill, long amount, bool tick = false) =>
            Value(t, HappyCatId, target, skill, amount, CombatResourceKind.Health, tick);

        // A hit or heal as the capture passes it on (0x0438, or 0x0538 for a tick), with ChainId 0 so the pool
        // canonicalizer leaves it alone; then the target's HP.
        private void Value(long t, int source, int target, int skill, long amount, CombatResourceKind kind, bool tick)
        {
            sink.AppendCombatWireObservation(At(t, tick ? (ushort)0x0538 : (ushort)0x0438), source, target, new CombatWireObservation
            {
                SkillCode = skill,
                Damage = amount,
                HitCount = 1,
                AttemptCount = 1,
                ResourceKind = kind,
                PeriodicRelation = tick ? PeriodicEffectRelation.Target : PeriodicEffectRelation.None,
                PeriodicMode = tick ? 2 : 0,
                PeriodicTailSkillCodeRaw = tick ? skill : 0
            });
            if (target == VakronId)
            {
                _vakronHp = Math.Max(0, _vakronHp - amount);
                sink.AppendNpcHp(At(t, 0x008D), VakronId, _vakronHp, VakronMaxHp);
            }
            else
            {
                SetHp(t, target, kind == CombatResourceKind.Health ? _hp[target] + amount : _hp[target] - amount);
            }
        }

        private void SetHp(long t, int id, long hp)
        {
            _hp[id] = Math.Clamp(hp, 0, _maxHp[id]);
            sink.AppendNpcHp(At(t, 0x008D), id, _hp[id], _maxHp[id]);
        }

        private double Health(int id) => (double)_hp[id] / _maxHp[id];

        // ±10 %, now and then a critical (x1.5); seeded, so the same on every run.
        private long Vary(long amount) => (long)(amount * (0.9 + _random.NextDouble() * 0.2) * (_random.Next(8) == 0 ? 1.5 : 1));

        private PacketObservationSource At(long t, ushort opcode) => new(startedAt + t, 1, opcode, 0, ++_sequence, default);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        // Its timestamp stands still too: the demo's time doesn't run on while it shows.
        public override long GetTimestamp() => 0;
    }
}

// The demo as a history entry (Trigger "tutorial", no name, so it shows the map's name) and the scene it came from, read
// at its end (EndedAt) by the frames.
internal sealed record TutorialDemo(ArchivedEncounterRecord Record, SceneLiveReadModel Scene, DateTimeOffset EndedAt);
