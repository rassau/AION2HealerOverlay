using System.Globalization;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Localization;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.ViewModels;

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

    // +15 every 3 hours from 01:00 UTC: histories/Odyle_plus_15 began at 00:59:38 UTC on 27 Nov 2026 and got it at 01:00:22.
    [Fact]
    public void The_Next_Plus_15_Comes_Every_Three_Hours_From_One_Utc()
    {
        var recorded = new DateTimeOffset(2026, 11, 27, 0, 59, 38, TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromSeconds(22), OdyleEnergy.UntilRecharge(recorded));
        Assert.Equal(TimeSpan.FromHours(3), OdyleEnergy.UntilRecharge(recorded.AddSeconds(22)));
        // 02:30 in Germany is 01:30 UTC; 23:00 UTC waits for 01:00 the next day.
        Assert.Equal(TimeSpan.FromMinutes(150), OdyleEnergy.UntilRecharge(new DateTimeOffset(2026, 11, 27, 2, 30, 0, TimeSpan.FromHours(1))));
        Assert.Equal(TimeSpan.FromHours(2), OdyleEnergy.UntilRecharge(new DateTimeOffset(2026, 11, 27, 23, 0, 0, TimeSpan.Zero)));
    }

    // The bottom bar's count and the hover text: rewards at the set price from the odyle and its bracket together, runs with
    // premium as half of them ("run" up to 1), the time to the next +15; in every language without a broken placeholder.
    [Fact]
    public void Rewards_Runs_And_The_Next_Plus_15_Read_As_Asked()
    {
        var english = CultureInfo.GetCultureInfo(LanguageService.English);
        string Text(string key) => Strings.ResourceManager.GetString(key, english)!;
        (string, string) Counts(int value, int bracket) => MainViewModel.OdyleTexts(value, bracket, 40, TimeSpan.Zero, Text, english) is var t ? (t.Rewards, t.Runs) : default;
        string Countdown(TimeSpan left) => MainViewModel.OdyleTexts(0, 0, 40, left, Text, english).ToolTip.Split(Environment.NewLine)[^1];

        Assert.Equal(string.Join(Environment.NewLine, "Odyle energy: 120(+160)/840", "7 rewards at 40 odyle each", "3.5 runs with premium (2 rewards each)",
            "Next +15 in 1h 30m"), MainViewModel.OdyleTexts(120, 160, 40, TimeSpan.FromMinutes(90), Text, english).ToolTip);
        Assert.Equal(("3 rewards", "1.5 runs"), Counts(120, 0));
        Assert.Equal(("2 rewards", "1 run"), Counts(80, 0));
        Assert.Equal(("1 reward", "0.5 run"), Counts(79, 0));
        Assert.Equal(("0 rewards", "0 run"), Counts(39, 0));
        Assert.Equal("Next +15 in 3h 0m", Countdown(TimeSpan.FromHours(3)));
        Assert.Equal("Next +15 in 59m", Countdown(TimeSpan.FromSeconds(3_570)));
        Assert.Equal("Next +15 in 45s", Countdown(TimeSpan.FromSeconds(44.2)));

        foreach (var language in LanguageService.SupportedLanguages)
        {
            var culture = CultureInfo.GetCultureInfo(language);
            var texts = MainViewModel.OdyleTexts(120, 160, 40, TimeSpan.FromMinutes(90), key => Strings.ResourceManager.GetString(key, culture)!, culture);
            Assert.Equal(4, texts.ToolTip.Split(Environment.NewLine).Length);
            if (language == LanguageService.German)
                Assert.Equal("3,5 Durchläufe", texts.Runs);
        }
    }

    // [length][opcode][body], the length one byte as these frames are short.
    private static byte[] Frame(byte opcode0, params byte[] body) => [(byte)(3 + body.Length), opcode0, 0x61, .. body];
}
