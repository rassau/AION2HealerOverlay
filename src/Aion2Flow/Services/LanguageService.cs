using System.Globalization;
using Cloris.Aion2Flow.Services.Settings;

namespace Cloris.Aion2Flow.Services;

public sealed class LanguageService
{
    public const string English = "en-US";
    public const string Korean = "ko-KR";
    public const string TraditionalChinese = "zh-TW";
    public const string German = "de-DE";

    public static readonly IReadOnlyList<string> SupportedLanguages =
    [
        English,
        Korean,
        TraditionalChinese,
        German,
    ];

    private CultureInfo _currentCulture;

    public LanguageService()
        : this(null)
    {
    }

    // The saved language from the start (SettingsFlyoutViewModel's SetLanguage then changes nothing), so the game data
    // isn't first built for the Windows language as well.
    public LanguageService(SettingsService? settings)
    {
        _currentCulture = settings?.Current.Language is { } saved && SupportedLanguages.Contains(saved, StringComparer.Ordinal)
            ? CultureInfo.GetCultureInfo(saved)
            : ResolveDefaultCulture();
        ApplyToCurrentThread(_currentCulture);
    }

    public event EventHandler<string>? LanguageChanged;

    public string CurrentLanguage => _currentCulture.Name;

    public CultureInfo CurrentCulture => _currentCulture;

    public bool SetLanguage(string language)
    {
        if (!SupportedLanguages.Contains(language, StringComparer.Ordinal))
        {
            return false;
        }

        if (string.Equals(_currentCulture.Name, language, StringComparison.Ordinal))
        {
            return false;
        }

        _currentCulture = CultureInfo.GetCultureInfo(language);
        ApplyToCurrentThread(_currentCulture);
        LanguageChanged?.Invoke(this, language);
        return true;
    }

    private static CultureInfo ResolveDefaultCulture()
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "ko" => CultureInfo.GetCultureInfo(Korean),
            "zh" => CultureInfo.GetCultureInfo(TraditionalChinese),
            "de" => CultureInfo.GetCultureInfo(German),
            _ => CultureInfo.GetCultureInfo(English),
        };

    private static void ApplyToCurrentThread(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
