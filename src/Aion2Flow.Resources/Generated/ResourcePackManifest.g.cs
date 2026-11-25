namespace Cloris.Aion2Flow.Resources.Generated;

internal readonly record struct ResourceLocalePackEntry(string Language, string ResourceName, int UncompressedLength, ulong Checksum);

internal static class ResourcePackManifest
{
    public const string SharedResourceName = "Cloris.Aion2Flow.Resources.Packs.shared.bin";
    public const int SharedUncompressedLength = 2684195;
    public const ulong SharedChecksum = 5949163530752889943UL;

    // The Steam client's data (upstream's steam pack, revision f6ddff51, client build 26728906).
    public static IReadOnlyList<ResourceLocalePackEntry> Locales { get; } =
    [
        new("de-DE", "Cloris.Aion2Flow.Resources.Packs.de-DE.bin", 699537, 16715665160837909140UL),
        new("en-US", "Cloris.Aion2Flow.Resources.Packs.en-US.bin", 641242, 16320890359623512680UL),
        new("ko-KR", "Cloris.Aion2Flow.Resources.Packs.ko-KR.bin", 690530, 6776757211987039980UL)
    ];

    public static ResourceLocalePackEntry GetLocale(string language)
    {
        foreach (var entry in Locales)
        {
            if (string.Equals(entry.Language, language, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported resource language.");
    }
}
