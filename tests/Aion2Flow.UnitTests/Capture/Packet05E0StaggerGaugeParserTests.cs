using Cloris.Aion2Flow.Protocol.Packets;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class Packet05E0StaggerGaugeParserTests
{
    [Fact]
    public void Reads_The_Gauge_And_The_Break()
    {
        // Boss 31338 in a recorded fight: 290 of its 300 left, and later the gauge running out.
        Assert.True(Packet05E0StaggerGaugeParser.TryParse(Convert.FromHexString("1405E0EAF40103012C0100002201000002"), out var gauge));
        Assert.Equal(new Packet05E0StaggerGauge(31_338, 290, 300), gauge);

        Assert.True(Packet05E0StaggerGaugeParser.TryParse(Convert.FromHexString("0C05E0EAF401000302"), out var broke));
        Assert.Equal(new Packet05E0StaggerGauge(31_338, 0, null), broke);
    }
}
