using System.Globalization;
using Avalonia.Media;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class GroupFrameShieldLayoutTests
{
    // At full HP from the left, the oldest first: 50% about to run out (faded right now) and 25%.
    [Fact]
    public void Each_Shield_Gets_Its_Own_Piece_And_Number_And_One_Running_Out_Blinks()
    {
        var (segments, text) = GroupFrameViewModel.LayoutShields(1, [(0.5, true), (0.25, false)], fromRight: false, fadedNow: true);

        Assert.Equal([new BarSegment(0, 0.5, GroupFrameViewModel.ShieldFadedBrush), new BarSegment(0.5, 0.75, GroupFrameViewModel.ShieldBrush)], segments);
        Assert.Equal("(50,25) 75%", string.Concat(text.Select(part => part.Text)));
        Assert.Equal([1, GroupFrameViewModel.FadedOpacity, 1, 1, 1], text.Select(part => part.Opacity));

        // From the right the oldest is outermost too.
        (segments, _) = GroupFrameViewModel.LayoutShields(1, [(0.25, false), (0.125, false)], fromRight: true, fadedNow: false);
        Assert.Equal([new BarSegment(0.75, 1, GroupFrameViewModel.ShieldBrush), new BarSegment(0.625, 0.75, GroupFrameViewModel.ShieldBrush)], segments);
    }

    // Green up to 25 m, orange up to 35 m, red up to the range at 40 m, grey past it.
    [Fact]
    public void The_Distance_Is_Coloured_By_The_Marks_And_Grey_Past_The_Range()
    {
        static IBrush At(int meters) => GroupFrameViewModel.DistanceBrush(meters, 25, 35, 40);
        Assert.Equal(
            [GroupFrameViewModel.DistanceGreenBrush, GroupFrameViewModel.DistanceGreenBrush, GroupFrameViewModel.DistanceOrangeBrush, GroupFrameViewModel.DistanceOrangeBrush,
             GroupFrameViewModel.DistanceRedBrush, GroupFrameViewModel.DistanceRedBrush, GroupFrameViewModel.DistanceOutOfRangeBrush],
            new[] { 0, 25, 26, 35, 36, 40, 41 }.Select(At));

        // Marks out of order: a range below green still dims, and an orange mark below green just never shows.
        Assert.Same(GroupFrameViewModel.DistanceOutOfRangeBrush, GroupFrameViewModel.DistanceBrush(22, 25, 35, 20));
        Assert.Same(GroupFrameViewModel.DistanceRedBrush, GroupFrameViewModel.DistanceBrush(32, 30, 20, 40));
    }

    // The hover over the HP bar: the real numbers in the system's format, each shield's amount, the oldest first.
    [Fact]
    public void The_Hp_Tool_Tip_Has_The_Real_Numbers_And_Each_Shield()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal("24.212 / 24.212 HP", GroupFrameViewModel.HpToolTip(24_212, 24_212, [], "Shield: {0}", german));
        Assert.Equal("20.580 / 24.212 HP\nShield: 5.000", GroupFrameViewModel.HpToolTip(20_580, 24_212, [5_000], "Shield: {0}", german));
        Assert.Equal("20.580 / 24.212 HP\nShield: 5.000 + 3.200 = 8.200", GroupFrameViewModel.HpToolTip(20_580, 24_212, [5_000, 3_200], "Shield: {0}", german));
    }

    [Fact]
    public void A_Shield_Turns_Violet_Where_No_Hp_Is_Under_It()
    {
        var (left, text) = GroupFrameViewModel.LayoutShields(0.25, [(0.5, false)], fromRight: false, fadedNow: false);
        Assert.Equal([new BarSegment(0, 0.25, GroupFrameViewModel.ShieldBrush), new BarSegment(0.25, 0.5, GroupFrameViewModel.ShieldOverHpBrush)], left);
        Assert.Equal("50%", Assert.Single(text).Text);

        var (right, _) = GroupFrameViewModel.LayoutShields(0.875, [(0.25, false)], fromRight: true, fadedNow: false);
        Assert.Equal([new BarSegment(0.75, 0.875, GroupFrameViewModel.ShieldBrush), new BarSegment(0.875, 1, GroupFrameViewModel.ShieldOverHpBrush)], right);

        var (none, _) = GroupFrameViewModel.LayoutShields(0.5, [(0.25, false)], fromRight: true, fadedNow: false);
        Assert.Equal([new BarSegment(0.75, 1, GroupFrameViewModel.ShieldOverHpBrush)], none);
    }
}
