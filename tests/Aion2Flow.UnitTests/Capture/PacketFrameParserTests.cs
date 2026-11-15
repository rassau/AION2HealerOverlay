using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Runtime;

using static Cloris.Aion2Flow.Tests.Capture.TransportTestData;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class PacketFrameParserTests
{
    private static readonly TcpConnection Connection = new(1, 2, 3, 4);
    private static readonly PacketProcessingTimestamp At = new(1_794_600_000_000, 0);

    // An unknown frame in a compressed batch, its body with ff ff at [2..3] and a compressed container at [10..]: the
    // recovery parse decodes that container while the outer batch is still read from the retained buffer. It decoded
    // into that same buffer, so the frame after it (here 0x4a36, the own entity) was lost.
    [Fact]
    public void A_Compressed_Batch_Nested_In_Another_Leaves_The_Rest_Of_The_Outer_Batch_Intact()
    {
        var nested = BuildCompressedFrame(BuildFrame(0x77, 0x77, new byte[50]));
        var unknown = BuildFrame(0x77, 0x77, [0x01, 0x00, 0xff, 0xff, 0, 0, 0, 0, 0, 0, .. nested]);
        var ownEntity = BuildFrame(0x4a, 0x36, [0x92, 0x21]);
        var group = new PacketPlayerGroupState();
        using var parser = new PacketFrameParser(SceneSinkFactory.CreateForLive(new SceneLiveReadModel())(), null, null, group);

        Assert.True(parser.ParsePacketEntry(BuildCompressedFrame(Concat(unknown, ownEntity)), in Connection, in At, 1));
        Assert.Equal(4_242, group.LocalPlayerId);
    }

    // A raw 40 36 in an unknown frame, an NPC code after the summon id (200), and no owner: these bytes are no summon, but
    // the scan put the code and the Summon kind on entity 200, once per scan of the frame (twice).
    [Fact]
    public void An_Embedded_Summon_Without_An_Owner_Journals_Nothing()
    {
        var (journal, sink) = CreateSink();
        using var parser = new PacketFrameParser(sink, null);

        parser.ParsePacketEntry(BuildFrame(0x7a, 0x7b, [.. EmbeddedSummon, 0, 0, 0, 0, 0]), in Connection, in At, 1);

        Assert.Equal(0, journal.Count);
        Assert.False(sink.TryGetNpcRuntimeState(200, out _));
    }

    [Fact]
    public void An_Embedded_Summon_With_Its_Owner_Gets_Its_Npc_Code()
    {
        var (_, sink) = CreateSink();
        using var parser = new PacketFrameParser(sink, null);
        byte[] owner = [0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x07, 0x02, 0x06, 0x39, 0x30];

        Assert.True(parser.ParsePacketEntry(BuildFrame(0x7a, 0x7b, [.. EmbeddedSummon, .. owner, 0, 0]), in Connection, in At, 1));

        Assert.True(sink.HasSummonOwner(200));
        Assert.True(sink.TryGetNpcRuntimeState(200, out var state));
        Assert.Equal(2_090_027, state.NpcCode);
        Assert.Equal(NpcKind.Summon, state.Kind);
    }

    // Six bytes, then 40 36, summon id 200, three bytes and NPC code 2,090,027.
    private static ReadOnlySpan<byte> EmbeddedSummon => [0, 0, 0, 0, 0, 0, 0x40, 0x36, 0xc8, 0x01, 0x10, 0x00, 0x00, 0x2b, 0xe4, 0x1f, 0x00];

    private static (ObservedEventJournal Journal, JournalingRuntimeObservationSink Sink) CreateSink()
    {
        var journal = new ObservedEventJournal();
        return (journal, new JournalingRuntimeObservationSink(journal, new SceneRuntimeClock(At.TimelineUnixMilliseconds), Guid.NewGuid()));
    }
}
