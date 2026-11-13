namespace Cloris.Aion2Flow.Resources.Generated;

internal readonly record struct ResourceLocalePackEntry(string Language, string ResourceName, int UncompressedLength, ulong Checksum);

internal static class ResourcePackManifest
{
    public const string SharedResourceName = "Cloris.Aion2Flow.Resources.Packs.shared.bin";
    public const int SharedUncompressedLength = 2684081;
    public const ulong SharedChecksum = 2331297009174430690UL;

    // The Steam client's data (upstream's steam pack, revision a4743540, client build 26581260).
    public static IReadOnlyList<ResourceLocalePackEntry> Locales { get; } =
    [
        new("de-DE", "Cloris.Aion2Flow.Resources.Packs.de-DE.bin", 698894, 12701287539990566187UL),
        new("en-US", "Cloris.Aion2Flow.Resources.Packs.en-US.bin", 640657, 291178280718273769UL),
        new("ko-KR", "Cloris.Aion2Flow.Resources.Packs.ko-KR.bin", 689912, 7610518329959747783UL)
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
