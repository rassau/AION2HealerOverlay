using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Tests.Support;
using SkiaSharp;

namespace Cloris.Aion2Flow.Tests.Controls;

[Collection(AvaloniaTestCollection.Name)]
public sealed class PerimeterProgressBarTests
{
    [Fact]
    public void PartialProgress_FillsEveryTraversedCorner()
    {
        AvaloniaTestHost.Run(() =>
        {
            var progressBar = new PerimeterProgressBar
            {
                Width = 32d,
                Height = 32d,
                Value = 0.8d,
                Fill = Brushes.White,
                Thickness = 4d
            };
            progressBar.Measure(new Size(32d, 32d));
            progressBar.Arrange(new Rect(0d, 0d, 32d, 32d));

            using var renderTarget = new RenderTargetBitmap(new PixelSize(32, 32));
            renderTarget.Render(progressBar);
            using var stream = new MemoryStream();
            renderTarget.Save(stream, PngBitmapEncoderOptions.Default);
            stream.Position = 0;
            using var bitmap = SKBitmap.Decode(stream);

            AssertCornerFilled(bitmap, 1, 1);
            AssertCornerFilled(bitmap, 30, 1);
            AssertCornerFilled(bitmap, 30, 30);
            AssertCornerFilled(bitmap, 1, 30);
        });
    }

    // The ring keeps its pen from one render to the next: a new fill or thickness must still show.
    [Fact]
    public void FillAndThicknessChanges_ShowAfterARender()
    {
        AvaloniaTestHost.Run(() =>
        {
            var progressBar = new PerimeterProgressBar
            {
                Width = 32d,
                Height = 32d,
                Value = 1d,
                Fill = Brushes.White,
                Thickness = 4d
            };
            progressBar.Measure(new Size(32d, 32d));
            progressBar.Arrange(new Rect(0d, 0d, 32d, 32d));

            using (var bitmap = Render(progressBar))
            {
                Assert.Equal(new SKColor(255, 255, 255), bitmap.GetPixel(1, 16));
                Assert.Equal(0, bitmap.GetPixel(6, 16).Alpha);
            }

            progressBar.Fill = Brushes.Red;
            progressBar.Thickness = 8d;
            using (var bitmap = Render(progressBar))
            {
                Assert.Equal(new SKColor(255, 0, 0), bitmap.GetPixel(1, 16));
                Assert.Equal(new SKColor(255, 0, 0), bitmap.GetPixel(6, 16));
            }
        });
    }

    private static SKBitmap Render(PerimeterProgressBar progressBar)
    {
        using var renderTarget = new RenderTargetBitmap(new PixelSize(32, 32));
        renderTarget.Render(progressBar);
        using var stream = new MemoryStream();
        renderTarget.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }

    private static void AssertCornerFilled(SKBitmap bitmap, int x, int y)
        => Assert.True(bitmap.GetPixel(x, y).Alpha >= 250);
}
