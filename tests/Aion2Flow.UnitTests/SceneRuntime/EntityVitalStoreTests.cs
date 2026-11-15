using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class EntityVitalStoreTests
{
    [Fact]
    public void Apply_StoresCurrentAndMaximumHp()
    {
        var store = new EntityVitalStore();
        var observation = new EntityVitalObservation(56_688, 22_847, 9_000_000);

        var state = store.Apply(in observation, observedAtMilliseconds: 1_000, observationOrdinal: 7);

        Assert.Equal(56_688, state.EntityId);
        Assert.Equal(22_847, state.CurrentHp);
        Assert.Equal(9_000_000, state.MaxHp);
        Assert.Equal(1_000, state.ObservedAtMilliseconds);
        Assert.Equal(7, state.ObservationOrdinal);
        Assert.Equal(1, state.Revision);
        Assert.Equal(1, store.Revision);
    }

    // The unhealed health from the sums kept with the samples is what the walk over every sample gave: also with late
    // samples moved up to the last one's time, deaths, unchanged values, and windows before, around and past the samples.
    [Fact]
    public void GetUnhealedHealthSeconds_Is_The_Walk_Over_The_Samples()
    {
        var random = new Random(1234);
        for (var round = 0; round < 200; round++)
        {
            var store = new EntityVitalStore();
            var samples = new List<(long At, long Missing)>();
            var at = (long)random.Next(0, 5_000);
            for (var i = random.Next(1, 120); i > 0; i--)
            {
                at = Math.Max(0, at + random.Next(-300, 2_000));
                var hp = random.Next(0, 5) == 0 ? 0 : random.Next(1, 10_001);
                store.Apply(new EntityVitalObservation(7, hp, 10_000), at, 0);
                var missing = hp == 0 ? 0 : 10_000 - hp;
                if (samples.Count == 0)
                    samples.Add((at, missing));
                else if (samples[^1].Missing != missing)
                    samples.Add((Math.Max(at, samples[^1].At), missing));
            }

            for (var query = 0; query < 30; query++)
            {
                var start = random.Next(-1_000, (int)at + 1_000);
                var end = start + random.Next(-100, 30_000);
                Assert.Equal(Walk(samples, start, end), store.GetUnhealedHealthSeconds(7, start, end));
            }
        }

        Assert.Equal(0, new EntityVitalStore().GetUnhealedHealthSeconds(7, 0, 1_000));

        static long Walk(List<(long At, long Missing)> samples, long start, long end)
        {
            if (end <= start)
                return 0;

            var total = 0L;
            for (var i = 0; i < samples.Count; i++)
            {
                var from = Math.Max(samples[i].At, start);
                var to = i + 1 < samples.Count ? Math.Min(samples[i + 1].At, end) : end;
                if (to > from)
                    total += samples[i].Missing * (to - from);
            }

            return total / 1000;
        }
    }

    [Fact]
    public void Apply_DoesNotInferMaximumFromCurrentHp()
    {
        var store = new EntityVitalStore();
        var observation = new EntityVitalObservation(56_688, 22_847, null);

        var state = store.Apply(in observation, observedAtMilliseconds: 1_000, observationOrdinal: 0);

        Assert.Equal(22_847, state.CurrentHp);
        Assert.Null(state.MaxHp);
    }

    [Fact]
    public void Apply_PreservesKnownMaximumWhenLaterObservationOmitsIt()
    {
        var store = new EntityVitalStore();
        var initial = new EntityVitalObservation(56_688, 49_200, 49_200);
        var update = new EntityVitalObservation(56_688, 22_847, null);

        store.Apply(in initial, observedAtMilliseconds: 900, observationOrdinal: 0);
        var state = store.Apply(in update, observedAtMilliseconds: 1_000, observationOrdinal: 1);

        Assert.Equal(22_847, state.CurrentHp);
        Assert.Equal(49_200, state.MaxHp);
        Assert.Equal(2, state.Revision);
    }

    [Fact]
    public void CreateStateSnapshot_ReturnsStatesInEntityOrder()
    {
        var store = new EntityVitalStore();
        var second = new EntityVitalObservation(200, 20, 100);
        var first = new EntityVitalObservation(100, 10, 100);
        store.Apply(in second, observedAtMilliseconds: 1, observationOrdinal: 0);
        store.Apply(in first, observedAtMilliseconds: 2, observationOrdinal: 1);

        var snapshot = store.CreateStateSnapshot();

        Assert.Collection(
            snapshot,
            state => Assert.Equal(100, state.EntityId),
            state => Assert.Equal(200, state.EntityId));
    }
}
