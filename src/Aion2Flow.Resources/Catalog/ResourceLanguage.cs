namespace Cloris.Aion2Flow.Resources.Catalog;

public static class ResourceLanguage
{
    public const string English = "en-US";
    public const string Korean = "ko-KR";
    public const string German = "de-DE";

    public static bool IsSupported(string language) => language is English or Korean or German;
}
