using System.Collections;

namespace Cloris.Aion2Flow.Resources.Catalog;

public sealed class SkillDisplayCatalog(int capacity = 0) : IReadOnlyCollection<SkillDisplayEntry>
{
    // Enumerates in the order added (the definitions'): nothing is ever removed. SkillMonitorSettingsViewModel.RebuildGroups
    // relies on that order.
    private readonly Dictionary<int, SkillDisplayEntry> _bySkillId = capacity > 0 ? new Dictionary<int, SkillDisplayEntry>(capacity) : [];

    public int Count => _bySkillId.Count;

    public SkillDisplayEntry this[int skillId] => _bySkillId[skillId];

    public void Add(SkillDisplayEntry item) => _bySkillId.Add(item.SkillId, item);

    public bool Contains(int skillId) => _bySkillId.ContainsKey(skillId);

    public bool TryGetValue(int skillId, out SkillDisplayEntry skill) => _bySkillId.TryGetValue(skillId, out skill);

    public IEnumerator<SkillDisplayEntry> GetEnumerator() => _bySkillId.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
