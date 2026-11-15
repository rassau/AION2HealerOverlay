using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Runtime;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class JournalingRuntimeObservationSinkTests
{
    // A 0x0238 whose mode is a 5-byte varint (negative), with a cooldown tail: the cooldown entry threw after CreateStamp
    // had taken its ordinal, so the clock stayed one ahead of the journal and every later observation threw until restart.
    [Fact]
    public void A_Negative_0238_Mode_With_A_Cooldown_Keeps_The_Journal_In_Step_With_The_Clock()
    {
        var journal = new ObservedEventJournal();
        var clock = new SceneRuntimeClock(1_794_600_000_000);
        var sink = new JournalingRuntimeObservationSink(journal, clock, Guid.NewGuid());
        var source = new PacketObservationSource(1_794_600_000_100, 0, 0x0238, 20, 0, default);

        sink.RegisterCompactControl0238(in source, 7, -1, 17_410_020, 24, 0, 7, availableCountAfterControl: null, cooldownMilliseconds: 1_500);
        Assert.Equal(1, journal.Count);

        sink.AppendNpcHp(in source, 8, 900, 1_000);
        Assert.True(journal.Count > 1);
        Assert.Equal(clock.NextObservationOrdinal, journal.NextObservationOrdinal);
    }

    // A count below zero can't be encoded either. The parser never passes one, but the sink is public: the cooldown
    // detail is encoded before CreateStamp, so the call throws with the clock still in step with the journal.
    [Fact]
    public void A_0238_Cooldown_That_Cannot_Be_Encoded_Throws_Before_The_Clock_Moves()
    {
        var journal = new ObservedEventJournal();
        var clock = new SceneRuntimeClock(1_794_600_000_000);
        var sink = new JournalingRuntimeObservationSink(journal, clock, Guid.NewGuid());
        var source = new PacketObservationSource(1_794_600_000_100, 0, 0x0238, 20, 0, default);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sink.RegisterCompactControl0238(in source, 7, 4, 17_410_020, 24, 0, 7, availableCountAfterControl: -1, cooldownMilliseconds: 1_500));
        Assert.Equal(1, journal.Count);
        Assert.Equal(clock.NextObservationOrdinal, journal.NextObservationOrdinal);

        sink.AppendNpcHp(in source, 8, 900, 1_000);
        Assert.True(journal.Count > 1);
        Assert.Equal(clock.NextObservationOrdinal, journal.NextObservationOrdinal);
    }
}
