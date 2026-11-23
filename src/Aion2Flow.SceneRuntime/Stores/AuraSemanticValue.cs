using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.SceneRuntime.Combat;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

public enum AuraDisposition : byte
{
    Unknown = 0,
    Buff = 1,
    Debuff = 2
}

public enum AuraSemanticMatchKind : byte
{
    None = 0,
    ExactNode = 1,
    UnambiguousSlot = 2
}

public readonly record struct AuraSemanticTrace(
    AuraSemanticMatchKind Match,
    SkillSemanticValue DirectSemantics,
    SkillSemanticValue Semantics,
    ResourceEffectRef ResourceEffectRef,
    SkillSemanticResourceNodeKind ResourceNodeKind,
    int ResourceNodeId,
    int ResourceSkillId,
    int EffectSlot,
    int ResourceCandidateSlotCount)
{
    public bool HasResourceEvidence => ResourceNodeId > 0;
}

public readonly record struct AuraSemanticValue(
    AuraDisposition Disposition,
    AuraSemanticTrace Trace)
{
    public bool IsAnnotated => Disposition != AuraDisposition.Unknown;
}

public static class AuraSemanticEvidenceResolver
{
    public static AuraSemanticValue Evaluate(ResourceEffectRef resourceEffectRef)
    {
        if (!CombatResourceRegistry.TryResolveAuraResourceSemantics(resourceEffectRef, out var resolution))
        {
            return new AuraSemanticValue(
                AuraDisposition.Unknown,
                new AuraSemanticTrace(
                    AuraSemanticMatchKind.None,
                    default,
                    default,
                    resourceEffectRef,
                    default,
                    0,
                    0,
                    -1,
                    0));
        }

        return Evaluate(in resolution);
    }

    // Buff or debuff as the game data labels the aura's own record, its abnormal (Buff or DeBuff). An effect id that is
    // such a record keeps its label; a class skill's aura comes with the bare skill code, whose records are code x 10 +
    // slot 1-9: Buff when they're only buffs, Debuff when only debuffs, Unknown when both or none. An effect group at
    // those ids doesn't count: its label sums up whatever the skill applies, to the caster and the target alike (in the
    // replay fixtures one read the Shadowstrike an Assassin puts on themselves as a debuff). Nor does a record the data
    // gives to another skill: Murderous Burst's only one, 118000081 (Buff), is 11800010's, and read as its own it made the
    // Murderous Burst players put on bosses a buff.
    // ponytail: a record names its skill only when one skill slot uses it (ResourceSkillId); one that only several other
    // skills use still counts (Grapple's Buff 122200002 is 12220020-12220050's). Look the code up among the record's
    // candidate slots (SkillSemanticRuntimeIndex) if such a label ever shows wrong.
    public static AuraDisposition ResolveDisposition(ResourceEffectRef resourceEffectRef)
    {
        var own = Evaluate(resourceEffectRef);
        if (IsAbnormalLabel(in own))
            return own.Disposition;
        if (resourceEffectRef.IsEmpty || resourceEffectRef.RawId > (uint.MaxValue - 9) / 10)
            return AuraDisposition.Unknown;

        var (buff, debuff) = (false, false);
        for (var slot = 1u; slot <= 9; slot++)
        {
            var value = Evaluate(ResourceEffectRef.FromRaw(resourceEffectRef.RawId * 10 + slot));
            if (value.Trace.ResourceSkillId != 0 && value.Trace.ResourceSkillId != (int)resourceEffectRef.RawId)
                continue;

            buff |= IsAbnormalLabel(in value) && value.Disposition == AuraDisposition.Buff;
            debuff |= IsAbnormalLabel(in value) && value.Disposition == AuraDisposition.Debuff;
        }

        return buff == debuff ? AuraDisposition.Unknown : buff ? AuraDisposition.Buff : AuraDisposition.Debuff;
    }

    private static bool IsAbnormalLabel(in AuraSemanticValue value) =>
        value.IsAnnotated && value.Trace.ResourceNodeKind == SkillSemanticResourceNodeKind.SkillAbnormal;

    internal static AuraSemanticValue Evaluate(in SkillSemanticResourceResolution resolution)
    {
        var match = GetMatch(in resolution);
        var trace = new AuraSemanticTrace(
            match,
            resolution.DirectSemantics,
            resolution.Semantics,
            resolution.RawId == 0 ? default : ResourceEffectRef.FromRaw(resolution.RawId),
            resolution.NodeKind,
            resolution.NodeId,
            resolution.Slot?.SkillId ?? 0,
            resolution.Slot?.Slot ?? -1,
            resolution.CandidateSlotCount);
        if (match == AuraSemanticMatchKind.None)
            return new AuraSemanticValue(AuraDisposition.Unknown, trace);

        var disposition = resolution.Semantics.AuraFacets switch
        {
            SkillAuraFacet.Buff => AuraDisposition.Buff,
            SkillAuraFacet.Debuff => AuraDisposition.Debuff,
            _ => AuraDisposition.Unknown
        };
        return new AuraSemanticValue(disposition, trace);
    }

    private static AuraSemanticMatchKind GetMatch(in SkillSemanticResourceResolution resolution)
    {
        if (resolution.NodeId <= 0)
            return AuraSemanticMatchKind.None;

        if (resolution.RawId == unchecked((uint)resolution.NodeId))
            return AuraSemanticMatchKind.ExactNode;

        return resolution.HasUnambiguousSlot
            ? AuraSemanticMatchKind.UnambiguousSlot
            : AuraSemanticMatchKind.None;
    }
}
