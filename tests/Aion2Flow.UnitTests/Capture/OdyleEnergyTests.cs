using Cloris.Aion2Flow.Capture.Streams;

namespace Cloris.Aion2Flow.Tests.Capture;

// The odyle energy (resource 60,000,001) from the login list (0x0b61) and its changes (0x0c61), frames as recorded on
// 27 Nov 2026 (histories/Odyle_*) after their length byte and opcode.
public sealed class OdyleEnergyTests
{
    // A change: 120 (+30) without an item; 120 (+160) after a +40 recharge item, the 160 in two bytes; another resource
    // (id 10, 14) isn't the odyle.
    [Fact]
    public void A_Change_Brings_The_Odyle_And_Its_Bracket()
    {
        Assert.True(OdyleEnergy.TryReadChange(Frame(0x0c, 0x00, 0x0c, 0x01, 0x87, 0x93, 0x03, 0x78, 0x1e, 0x02), out var value, out var bracket));
        Assert.Equal((120, 30), (value, bracket));
        Assert.True(OdyleEnergy.TryReadChange(Frame(0x0c, 0x01, 0x0c, 0x01, 0x87, 0x93, 0x03, 0x78, 0xa0, 0x01, 0x01, 0x28, 0x00, 0x00, 0x00), out value, out bracket));
        Assert.Equal((120, 160), (value, bracket));
        Assert.False(OdyleEnergy.TryReadChange(Frame(0x0c, 0x00, 0x04, 0x0a, 0x00, 0x00, 0x00, 0x0e, 0x02), out _, out _));
    }

    // The login list: a count, then resources of each kind seen (a time, none, the odyle, a number); one with a mask bit
    // not seen yet ends the reading, as its fields can't be skipped.
    [Fact]
    public void The_Login_List_Holds_The_Odyle_Among_The_Resources()
    {
        byte[] time = [0x01, 0x03, 0x00, 0x00, 0x00, 0x00, 0xd0, 0x7e, 0x01, 0x00, 0x00, 0x00, 0x00];
        byte[] none = [0x00, 0x81, 0x96, 0x98, 0x00];
        byte[] odyle = [0x0c, 0x01, 0x87, 0x93, 0x03, 0x78, 0x50];
        byte[] number = [0x04, 0x02, 0x87, 0x93, 0x03, 0x0e];
        Assert.True(OdyleEnergy.TryReadList(Frame(0x0b, [0x04, .. time, .. none, .. odyle, .. number]), out var value, out var bracket));
        Assert.Equal((120, 80), (value, bracket));
        Assert.False(OdyleEnergy.TryReadList(Frame(0x0b, [0x03, .. time, 0x02, 0x05, 0x00, 0x00, 0x00, .. odyle]), out _, out _));
        Assert.False(OdyleEnergy.TryReadList(Frame(0x0b, [0x02, .. time, .. none]), out _, out _));
    }

    // From the dispatcher: only 0x0b61 and 0x0c61 count, and the last one read stays.
    [Fact]
    public void Only_The_Resource_Opcodes_Change_It()
    {
        var change = Frame(0x0c, 0x01, 0x0c, 0x01, 0x87, 0x93, 0x03, 0x78, 0xa0, 0x01, 0x01, 0x28, 0x00, 0x00, 0x00);
        OdyleEnergy.Clear();
        OdyleEnergy.Observe(0x0c, 0x62, change);
        Assert.Null(OdyleEnergy.Current);
        OdyleEnergy.Observe(0x0c, 0x61, change);
        Assert.Equal((120, 160), OdyleEnergy.Current);
        OdyleEnergy.Observe(0x0c, 0x61, Frame(0x0c, 0x00, 0x04, 0x0a, 0x00, 0x00, 0x00, 0x0e, 0x02));
        Assert.Equal((120, 160), OdyleEnergy.Current);
        OdyleEnergy.Clear();
    }

    // [length][opcode][body], the length one byte as these frames are short.
    private static byte[] Frame(byte opcode0, params byte[] body) => [(byte)(3 + body.Length), opcode0, 0x61, .. body];
}
