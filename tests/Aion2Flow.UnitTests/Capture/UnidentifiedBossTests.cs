using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Model;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class UnidentifiedBossTests
{
    [Fact]
    public void Npc_Without_Code_Becomes_A_Boss_From_Its_Hp_Pool()
    {
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);

        // HP updates (0x008D) as Deus Research Base sends them: a boss at 10.68M and a trash mob at 344k, neither with a code.
        Assert.True(processor.AppendAndProcess(Envelope("14008D93A101020100" + "76EEA20000000000"), in connection, 1_800_000_000_000));
        Assert.True(processor.AppendAndProcess(Envelope("14008D9D8701020100" + "F640050000000000"), in connection, 1_800_000_000_050));
        _ = scene.CreateFrame();

        Assert.True(scene.Owner.Entities.TryGet(20_627, out var boss));
        Assert.Equal(NpcKind.Boss, boss.Kind);
        Assert.True(scene.Owner.EntityVitals.TryGet(20_627, out var bossVital));
        Assert.Equal(10_677_878, bossVital.MaxHp);
        Assert.NotEqual(NpcKind.Boss, scene.Owner.Entities.TryGet(17_309, out var trash) ? trash.Kind : NpcKind.Unknown);
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
