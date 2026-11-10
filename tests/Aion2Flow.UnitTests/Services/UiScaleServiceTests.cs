using Avalonia.Controls;
using Avalonia.Threading;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Cloris.Aion2Flow.Tests.Services;

[Collection(AvaloniaTestCollection.Name)]
public sealed class UiScaleServiceTests
{
    [Fact]
    public void UiServicesAreDisposedOnUiThreadBeforeBackgroundProviderShutdown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        AvaloniaTestHost.Run(() =>
        {
            var settings = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-ui-scale-{Guid.NewGuid():N}.json"));
            var originalContent = new Border();
            var window = new Window
            {
                Width = 300,
                Height = 200,
                Content = originalContent
            };
            using var provider = new ServiceCollection()
                .AddSingleton(settings)
                .AddSingleton<UiScaleService>()
                .BuildServiceProvider();

            window.Show();
            try
            {
                var uiScale = provider.GetRequiredService<UiScaleService>();
                uiScale.RegisterWindow(window);

                var scaleHost = Assert.IsType<LayoutTransformControl>(window.Content);
                Assert.Same(originalContent, scaleHost.Child);

                Program.DisposeUiThreadServices(provider);

                Assert.Same(originalContent, window.Content);
                var exception = Record.Exception(
                    () => Task.Run(provider.Dispose, cancellationToken).GetAwaiter().GetResult());
                Assert.Null(exception);
            }
            finally
            {
                if (window.IsVisible)
                    window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    // Like the group frame: sized to its content, with a scale of its own.
    [Fact]
    public void A_Window_Sized_To_Its_Content_Follows_Its_Own_Scale_And_Still_Grows_With_The_Content()
    {
        AvaloniaTestHost.Run(() =>
        {
            var settings = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-ui-scale-{Guid.NewGuid():N}.json"));
            using var uiScale = new UiScaleService(settings);
            var content = new Border { Width = 100, Height = 50 };
            var window = new Window
            {
                WindowDecorations = WindowDecorations.None,
                SizeToContent = SizeToContent.WidthAndHeight,
                Content = content
            };
            var scalePercent = 100;

            window.Show();
            try
            {
                uiScale.RegisterWindow(window, () => scalePercent);
                Dispatcher.UIThread.RunJobs();
                AssertClientSize(window, 100, 50);

                scalePercent = 150;
                uiScale.RefreshWindow(window);
                Dispatcher.UIThread.RunJobs();
                AssertClientSize(window, 150, 75);

                content.Width = 200;
                Dispatcher.UIThread.RunJobs();
                AssertClientSize(window, 300, 75);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    private static void AssertClientSize(Window window, double width, double height)
    {
        Assert.Equal(width, window.ClientSize.Width, 0.5);
        Assert.Equal(height, window.ClientSize.Height, 0.5);
    }
}
