using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Cloris.Aion2Flow.Controls;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class OverlayLayoutTests
{
    [Fact]
    public void DurationBlock_UsesTheBottomBarMetricStyle()
    {
        AvaloniaTestHost.Run(() =>
        {
            var style = new StyleInclude(new Uri("avares://Aion2Heal/"))
            {
                Source = new Uri("avares://Aion2Heal/Styles/OverlayTheme.axaml")
            };
            Application.Current!.Styles.Add(style);
            var timer = new DurationBlock();
            timer.Classes.Add("BottomBarMetric");
            var window = new Window { Content = timer };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(
                    FontWeight.SemiBold,
                    timer.DecimalSecondsBlockForDiagnostics.FontWeight);
                Assert.NotNull(
                    timer.DecimalSecondsBlockForDiagnostics.Foreground);
                Assert.Equal(
                    FontWeight.SemiBold,
                    timer.MinutesBlockForDiagnostics.FontWeight);
                Assert.Equal(
                    FontWeight.SemiBold,
                    timer.SecondsBlockForDiagnostics.FontWeight);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Styles.Remove(style);
            }
        });
    }
}
