using System.Collections.Concurrent;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

// CopyRange shares the source's full segments; whatever is read from a copy must be what an entry-by-entry copy holds.
public sealed class JournalCopyRangeTests
{
    private static readonly Guid[] Sessions = [.. Enumerable.Range(1, 6).Select(static i => new Guid(i, 0, 0, new byte[8]))];

    [Fact]
    public void Copies_Of_A_Trimmed_Journal_Read_Like_Entry_By_Entry_Copies_And_Stay_So()
    {
        var rng = new Random(7);
        var source = new ObservedEventJournal(1);
        // Sessions mixed first, then one after the other.
        for (var i = 0; i < 1_300; i++)
            AppendRandom(source, rng, Sessions[rng.Next(3)]);
        for (var i = 0; i < 2_700; i++)
            AppendRandom(source, rng, Sessions[3 + Math.Min(2, i / 1_000)]);
        source.CompleteFlush(42);
        source.TrimBefore(1_700);
        Assert.Equal(1_536, source.FirstObservationOrdinal);

        var copies = new List<(ObservedEventJournal Expected, ObservedEventJournal Actual)>();
        foreach (var (start, end) in Ranges(source, rng, 24))
        {
            var (expected, actual) = (CopyByEntries(source, start, end), source.CopyRange(start, end));
            AssertSame(expected, actual, start);
            copies.Add((expected, actual));
        }

        // Copies of copies (an archive of a played-back archive), half of them appended to first.
        for (var k = 0; k < copies.Count; k += 4)
        {
            var (expected, actual) = copies[k];
            if (k % 8 == 0)
            {
                var (expectedRng, actualRng) = (new Random(k), new Random(k));
                for (var i = 0; i < 700; i++)
                {
                    AppendRandom(expected, expectedRng, Sessions[k % Sessions.Length]);
                    AppendRandom(actual, actualRng, Sessions[k % Sessions.Length]);
                }

                AssertSame(expected, actual, expected.FirstObservationOrdinal);
            }

            foreach (var (start, end) in Ranges(expected, rng, 2))
                AssertSame(CopyByEntries(expected, start, end), actual.CopyRange(start, end), start);
        }

        // The source goes on (fills its last segment, starts new ones) and lets go of everything: no copy changes.
        for (var i = 0; i < 3_000; i++)
            AppendRandom(source, rng, Sessions[5]);
        source.TrimBefore(source.NextObservationOrdinal);
        foreach (var (expected, actual) in copies)
            Assert.Equal(Dump(expected, long.MinValue / 4), Dump(actual, long.MinValue / 4));
    }

    // As live: the capture thread appends and trims while archives copy and their copies are read, for 200 checked copies
    // or 1 s, whichever comes first.
    [Fact]
    public async Task Copies_Made_While_The_Journal_Grows_And_Trims_Hold_Exactly_Their_Range()
    {
        var source = new ObservedEventJournal(1);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var copies = new BlockingCollection<ObservedEventJournal>(64);
        var failures = new ConcurrentQueue<string>();
        var writer = Task.Run(() =>
        {
            for (long ordinal = 0; !stop.IsCancellationRequested; ordinal++)
            {
                var header = Header(ordinal);
                source.Append(in header, new CombatWireObservation { SkillCode = (int)(ordinal * 7) });
                if (ordinal % 4_000 == 0)
                    source.TrimBefore(ordinal - 1_500);
            }
        }, TestContext.Current.CancellationToken);
        var copier = Task.Run(() =>
        {
            var rng = new Random(3);
            while (!stop.IsCancellationRequested)
            {
                var (first, next) = (source.FirstObservationOrdinal, source.NextObservationOrdinal);
                if (next - first < 2)
                    continue;

                var start = first + rng.NextInt64(next - first);
                try { copies.Add(source.CopyRange(start, start + rng.NextInt64(next - start + 1))); }
                catch (Exception ex) { failures.Enqueue(ex.ToString()); }
            }

            copies.CompleteAdding();
        }, TestContext.Current.CancellationToken);

        var checkedCopies = 0;
        foreach (var copy in copies.GetConsumingEnumerable(TestContext.Current.CancellationToken))
        {
            var ordinal = copy.FirstObservationOrdinal;
            var cursor = copy.CreateCursor(long.MinValue / 4);
            while (copy.ReadEntries(cursor, 512, entries =>
            {
                foreach (var entry in entries)
                {
                    var expected = Header(ordinal);
                    if (entry.Stamp != expected.Stamp || entry.SceneSessionId != expected.SceneSessionId || entry.Raw != expected.Raw || entry.Combat.SkillCode != (int)(ordinal * 7))
                        failures.Enqueue($"entry {ordinal} of a copy");
                    ordinal++;
                }
            }) is { Count: > 0 } read)
                cursor = read.Cursor;

            if (ordinal != copy.NextObservationOrdinal || ordinal - copy.FirstObservationOrdinal != copy.Count)
                failures.Enqueue($"copy of {copy.Count} entries read {ordinal - copy.FirstObservationOrdinal}");
            if (++checkedCopies == 200)
                stop.Cancel();
        }

        await writer;
        await copier;
        Assert.True(failures.IsEmpty, string.Join(Environment.NewLine, failures.Take(5)));
        Assert.True(checkedCopies > 10, $"only {checkedCopies} copies checked");

        static ObservedEventHeader Header(long ordinal) =>
            new(Sessions[(int)(ordinal / 977 % Sessions.Length)], new TimelineStamp(ordinal * 10, ordinal, ordinal / 5), (int)ordinal, -(int)ordinal,
                new RawPacketReference((ushort)(ordinal % 65_000), (int)(ordinal % 999), ordinal));
    }

    // The copy as it was made before segments were shared: every entry appended again.
    private static ObservedEventJournal CopyByEntries(ObservedEventJournal source, long start, long end)
    {
        var copy = ObservedEventJournal.StartingAt(Math.Max(start, source.FirstObservationOrdinal));
        var cursor = source.CreateCursor(start);
        while (source.ReadEntries(cursor, end, ObservedEventJournal.SegmentCapacity, batch =>
        {
            foreach (var entry in batch)
            {
                var header = new ObservedEventHeader(entry.SceneSessionId, entry.Stamp, entry.SourceEntityId, entry.TargetEntityId, entry.Raw);
                switch (entry.Domain)
                {
                    case ObservedEventDomain.Combat: copy.Append(in header, in entry.Combat); break;
                    case ObservedEventDomain.Action: copy.Append(in header, in entry.Action); break;
                    case ObservedEventDomain.State: copy.Append(in header, in entry.State); break;
                    case ObservedEventDomain.EntityVital: copy.Append(in header, in entry.EntityVital); break;
                    case ObservedEventDomain.Aura: copy.Append(in header, in entry.Aura); break;
                    case ObservedEventDomain.Scene: copy.Append(in header, in entry.Scene); break;
                    default: copy.AppendDiagnostic(in header); break;
                }
            }
        }) is { Count: > 0 } read)
            cursor = read.Cursor;

        if (source.LastCompletedFlushId >= 0)
            copy.CompleteFlush(source.LastCompletedFlushId);
        return copy;
    }

    private static void AssertSame(ObservedEventJournal expected, ObservedEventJournal actual, long start)
    {
        Assert.Equal(
            (expected.Count, expected.FirstObservationOrdinal, expected.NextObservationOrdinal, expected.LastCompletedFlushId),
            (actual.Count, actual.FirstObservationOrdinal, actual.NextObservationOrdinal, actual.LastCompletedFlushId));
        Assert.Equal(Dump(expected, long.MinValue / 4, 7), Dump(actual, long.MinValue / 4, 1_000));
        Assert.Equal(Dump(expected, start + 3), Dump(actual, start + 3));

        foreach (var ordinal in new[] { expected.FirstObservationOrdinal - 1, expected.FirstObservationOrdinal, expected.NextObservationOrdinal - 1, expected.NextObservationOrdinal, expected.FirstObservationOrdinal + 600 })
        {
            string? expectedEntry = null, actualEntry = null;
            Assert.Equal(expected.TryReadEntry(ordinal, entry => expectedEntry = Line(entry)), actual.TryReadEntry(ordinal, entry => actualEntry = Line(entry)));
            Assert.Equal(expectedEntry, actualEntry);
        }
    }

    private static List<string> Dump(ObservedEventJournal journal, long from, int batch = 333)
    {
        var lines = new List<string>();
        var cursor = journal.CreateCursor(from);
        while (journal.ReadEntries(cursor, batch, entries =>
        {
            foreach (var entry in entries)
                lines.Add(Line(entry));
        }) is { Count: > 0 } read)
            cursor = read.Cursor;

        return lines;
    }

    private static string Line(ObservedEventEntry entry)
    {
        var payload = entry.Domain switch
        {
            ObservedEventDomain.Combat => entry.Combat.ToString(),
            ObservedEventDomain.Action => entry.Action.ToString(),
            ObservedEventDomain.State => entry.State.ToString(),
            ObservedEventDomain.EntityVital => entry.EntityVital.ToString(),
            ObservedEventDomain.Aura => entry.Aura.ToString(),
            ObservedEventDomain.Scene => entry.Scene.ToString(),
            _ => "diagnostic"
        };
        return $"{entry.Stamp}|{entry.SceneSessionId}|{entry.Domain}|{entry.SourceEntityId}|{entry.TargetEntityId}|{entry.Raw}|{payload}";
    }

    private static void AppendRandom(ObservedEventJournal journal, Random rng, Guid session)
    {
        var ordinal = journal.NextObservationOrdinal;
        var raw = new RawPacketReference((ushort)rng.Next(65_536), rng.Next(1_000), ordinal * 3);
        var header = new ObservedEventHeader(session, new TimelineStamp(ordinal * 100 + rng.Next(50), ordinal, ordinal / 7), rng.Next(1_000), rng.Next(1_000), raw);
        switch (rng.Next(7))
        {
            case 0: journal.Append(in header, new CombatWireObservation { SkillCode = rng.Next(), Damage = rng.Next() }); break;
            case 1: journal.Append(in header, new ActionObservation { SourceEntityId = rng.Next(), Phase = rng.Next(5) }); break;
            case 2: journal.Append(in header, new StateObservation(rng.Next(), rng.Next(), rng.Next(), rng.Next(), rng.Next(), "t" + ordinal)); break;
            case 3: journal.Append(in header, new EntityVitalObservation(rng.Next(), rng.Next(), rng.Next(2) == 0 ? null : rng.Next())); break;
            case 4: journal.Append(in header, new AuraObservation { EntityId = rng.Next(), InstanceSequenceId = rng.Next() }); break;
            case 5: journal.Append(in header, new SceneObservation((uint)rng.Next(), (uint)rng.Next(), SceneObservationKind.CurrentMap)); break;
            default: journal.AppendDiagnostic(in header); break;
        }
    }

    // Whole, inner and empty ranges, ranges on and across segment edges and outside the journal, and random ones.
    private static List<(long Start, long End)> Ranges(ObservedEventJournal journal, Random rng, int random)
    {
        var (first, next) = (journal.FirstObservationOrdinal, journal.NextObservationOrdinal);
        List<(long, long)> ranges =
        [
            (first, next), (first + 1, next - 1), (first + 511, first + 513), (first + 512, first + 1_024), (first + 512, first + 1_025),
            (first - 100, first + 10), (next - 5, next + 100), (first + 700, first + 700), (first + 900, first + 800), (next, next + 10),
            (long.MinValue / 4, long.MaxValue), (first, first + 1), (next - 1, next), (first + 511, first + 512), (0, 1), (0, first + 3)
        ];
        for (var i = 0; i < random; i++)
        {
            var start = first + rng.NextInt64(Math.Max(1, next - first));
            ranges.Add((start, start + rng.NextInt64(Math.Max(1, next - start + 1))));
        }

        return ranges;
    }
}
