using System.Globalization;
using Cloris.Aion2Flow.Services.Logging;

namespace Cloris.Aion2Flow.Tests.Services;

public sealed class AppLogWriterTests
{
    // app.log's timestamps read the same whatever the Windows region: Finnish puts '.' between hours and minutes, Thai counts
    // the Buddhist era's years.
    [Theory]
    [InlineData("fi-FI")]
    [InlineData("th-TH")]
    public void Timestamps_Do_Not_Follow_The_Region(string region)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-log-{Guid.NewGuid():N}");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            // The writer's thread takes the culture along from here.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(region);
            using (var writer = new AppLogWriter(AppLogLevel.Info, directory))
                writer.Enqueue(AppLogLevel.Info, "started", null, 0);
            CultureInfo.CurrentCulture = culture;

            var line = Assert.Single(File.ReadAllLines(Path.Combine(directory, "app.log")));
            Assert.Equal("] [INF] started", line[24..]);
            var written = DateTime.ParseExact(line[1..24], "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            Assert.InRange(written, DateTime.Now.AddMinutes(-5), DateTime.Now.AddMinutes(5));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            Directory.Delete(directory, recursive: true);
        }
    }
}
