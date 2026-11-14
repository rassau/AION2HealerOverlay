using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Protocol.Packets;
using Cloris.Aion2Flow.SceneRuntime;

namespace Cloris.Aion2Flow.Tests.Capture;

// From a recording (14 Nov 2026) started after logging in, at the training dummies: no 0x3336, and as a dummy hurts no one,
// no 0x4a36 either. The user (12969) cast Earth's Retribution, then Thunder and Lightning, at a dummy (24446); each cast's
// batch held the own-skill packet (0x0138) for it, a stranger's cast too.
public sealed class OwnSkillPacketTests
{
    private const string StrangersCast = "190238D03700E5A709015300D03795635643FA560100";
    private const string EarthsRetribution = "260238A96500408E03011702FEBE0135180DC37F3799C7381FEFC700370947D4680100";
    private const string ThunderAndLightning = "260238A9650050B503011802FEBE0135180DC37F3799C7381FEFC700370947D4680100";

    [Fact]
    public void Two_Own_Skill_Packets_After_The_Users_Casts_Make_Them_The_Player()
    {
        const long at = 1_794_600_000_000;
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        // An HP update first, as the stream is only picked up at an opcode it knows.
        Assert.True(processor.AppendAndProcess(Envelope("14008D93A101020100" + "76EEA20000000000"), in connection, at));

        Batch(processor, connection, at + 100, StrangersCast, EarthsRetribution, "0D0138000000408E0301");
        Assert.Equal(0, LocalPlayer(scene));

        // One for a skill no one just cast (Discharge) changes nothing.
        Batch(processor, connection, at + 400, "0D013800000060DC0301");
        Batch(processor, connection, at + 650, ThunderAndLightning, StrangersCast, "0D013800000050B50301");
        Assert.Equal(12969, LocalPlayer(scene));
    }

    private static void Batch(PacketStreamProcessor processor, TcpConnection connection, long at, params string[] frames)
    {
        foreach (var frame in frames)
            processor.AppendAndProcess(Envelope(frame), in connection, at);
    }

    private static int LocalPlayer(SceneLiveReadModel scene)
    {
        scene.CreateFrame();
        return scene.Owner.MetadataRegistry.LocalPlayerEntityId;
    }

    private static byte[] Envelope(string frameHex)
    {
        var frame = Convert.FromHexString(frameHex);
        var envelope = new byte[PacketTransportCodec.LengthPrefixedHeaderLength + frame.Length];
        BitConverter.TryWriteBytes(envelope, frame.Length);
        frame.CopyTo(envelope, PacketTransportCodec.LengthPrefixedHeaderLength);
        return envelope;
    }
}
