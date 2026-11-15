using System.Collections;

namespace Cloris.Aion2Flow.Resources.Catalog;

public sealed class SkillDefinitionCatalog(int capacity) : IReadOnlyCollection<SkillDefinition>
{
    // Enumerates in the order added (the pack's): nothing is ever removed. SkillMonitorSettingsViewModel.RebuildGroups
    // relies on that order.
    private readonly Dictionary<int, SkillDefinition> _bySkillId = capacity > 0 ? new(capacity) : [];

    public SkillDefinitionCatalog()
        : this(0)
    {
    }

    public int Count => _bySkillId.Count;

    public SkillDefinition this[int skillId] => _bySkillId[skillId];

    public void Add(SkillDefinition item) => _bySkillId.Add(item.SkillId, item);

    public bool Contains(int skillId) => _bySkillId.ContainsKey(skillId);

    public bool TryGetValue(int skillId, out SkillDefinition skill) => _bySkillId.TryGetValue(skillId, out skill);

    public IEnumerator<SkillDefinition> GetEnumerator() => _bySkillId.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
