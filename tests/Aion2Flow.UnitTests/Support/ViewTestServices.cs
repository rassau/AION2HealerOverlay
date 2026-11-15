using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Cloris.Aion2Flow.Tests.Support;

internal static class ViewTestServices
{
    private static readonly Lock s_gate = new();
    private static LocalizationService? s_localization;
    private static UiFrameBatchService? s_frameBatch;

    internal static (LocalizationService Localization, UiFrameBatchService FrameBatch) Get()
    {
        lock (s_gate)
        {
            if (s_localization is null)
            {
                var language = new LanguageService();
                language.SetLanguage(LanguageService.TraditionalChinese);
                s_frameBatch = new UiFrameBatchService();
                s_localization = new LocalizationService(language);
                // Also what the windows that take their services from Ioc need (the skill monitor), settings in a file of their own
                // (deleted when the test process exits).
                var settings = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-view-tests-{Guid.NewGuid():N}.json"));
                AppDomain.CurrentDomain.ProcessExit += (_, _) => File.Delete(settings.FilePath);
                Ioc.Default.ConfigureServices(new ServiceCollection()
                    .AddSingleton(s_localization)
                    .AddSingleton(settings)
                    .AddSingleton(new UiScaleService(settings))
                    .AddSingleton(new AvaloniaFrameClockService())
                    .BuildServiceProvider());
            }

            return (s_localization, s_frameBatch!);
        }
    }
}
