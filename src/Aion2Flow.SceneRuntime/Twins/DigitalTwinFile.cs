using System.IO.Compression;
using System.Text.Json;
using Cloris.Aion2Flow.SceneRuntime.Archive;

namespace Cloris.Aion2Flow.SceneRuntime.Twins;

// One digital twin as a file of its own (gzip JSON, the same form as in exported history entries), for the twin library.
public static class DigitalTwinFile
{
    public static void Write(string path, DigitalTwin twin)
    {
        using var stream = File.Create(path);
        using var gzip = new GZipStream(stream, CompressionLevel.Optimal);
        JsonSerializer.Serialize(gzip, twin, SceneArchiveJsonContext.Default.DigitalTwin);
    }

    public static DigitalTwin? Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        return JsonSerializer.Deserialize(gzip, SceneArchiveJsonContext.Default.DigitalTwin);
    }
}
