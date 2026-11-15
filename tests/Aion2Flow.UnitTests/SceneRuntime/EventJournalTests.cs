using System.Runtime.CompilerServices;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class EventJournalTests
{
    [Fact]
    public void Freeze_AllEventDomainsShareRecordsAndKeepFixedBounds()
    {
        VerifyFreeze(static revision => new CombatEventRecord { Revision = revision });
        VerifyFreeze(static revision => new CombatMechanicEventRecord { Revision = revision });
        VerifyFreeze(static revision => new CombatResourceEventRecord { Revision = revision });
    }

    // A late credit to a record an archive holds goes to a copy of its segment; one to a record after the archive's end
    // leaves the segment shared.
    [Fact]
    public void GetEventForUpdate_CopiesOnlyTheRecordsAnArchiveHolds()
    {
        var journal = new EventJournal<CombatEventRecord>();
        journal.Append(new CombatEventRecord { Revision = 1 });
        var frozen = journal.Freeze();
        journal.Append(new CombatEventRecord { Revision = 2 });

        journal.GetEventForUpdate(1) = new CombatEventRecord { Revision = 20 };
        Assert.True(Unsafe.AreSame(ref Unsafe.AsRef(in journal.GetEvent(0)), ref Unsafe.AsRef(in frozen[0])));

        journal.GetEventForUpdate(0) = new CombatEventRecord { Revision = 10 };
        Assert.Equal(1, Assert.Single(frozen).Revision);
        Assert.Equal([10L, 20L], journal.Select(static record => record.Revision));
    }

    private static void VerifyFreeze<T>(Func<int, T> create) where T : struct
    {
        var count = EventJournal<T>.SegmentCapacity + 1;
        var journal = new EventJournal<T>(count);
        for (var i = 0; i < count; i++)
            journal.Append(create(i + 1));
        _ = journal.Freeze();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var frozen = journal.Freeze();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0, 64 + 2 * IntPtr.Size);

        ref readonly var live = ref journal.GetEvent(count - 1);
        ref readonly var archived = ref frozen[count - 1];
        Assert.True(Unsafe.AreSame(ref Unsafe.AsRef(in live), ref Unsafe.AsRef(in archived)));

        var range = journal.AsRange()[1..];
        journal.Append(create(count + 1));
        Assert.Equal(count - 1, range.Length);
        Assert.Equal(create(count), range[^1]);
        Assert.Equal(count, frozen.Count);
        Assert.Equal(create(1), frozen[0]);
        Assert.Equal(create(count), frozen[count - 1]);
        Assert.Equal(Enumerable.Range(1, count).Select(create), (IEnumerable<T>)frozen);

        journal = new EventJournal<T>();
        journal.Append(create(-1));
        Assert.Equal(create(count), frozen[count - 1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => frozen.GetEvent(count));
    }
}
