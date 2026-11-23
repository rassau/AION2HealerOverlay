using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Simple;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Diagnostics;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Logging;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;
using Cloris.Aion2Flow.WinDivert;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using MainAppWindow = Cloris.Aion2Flow.Views.MainWindow;

namespace Cloris.Aion2Flow;

internal static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        using var appInstance = AppSingleInstance.TryAcquire();
        if (!appInstance.IsPrimary)
            return;

        // Velopack before the log: the install's root, where the log's folder goes, is only known after it
        // (WorkingDirectoryResolver). Before it, the log landed in current\, which every update replaces. After the
        // single-instance check, though: Run() applies a downloaded update, for which Update.exe ends the running overlay,
        // so a second start returns before it. A failure in it is logged once the log is up (the try below).
        ExceptionDispatchInfo? velopackFailure = null;
        try
        {
            VelopackApp.Build().Run();
        }
        catch (Exception exception)
        {
            velopackFailure = ExceptionDispatchInfo.Capture(exception);
        }

        using var logWriter = new AppLogWriter(
#if DEBUG
            AppLogLevel.Debug
#else
            AppLogLevel.Info
#endif
        );
        AppLog.Initialize(logWriter);

        void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppLog.Write(AppLogLevel.Error, $"Unhandled application exception. IsTerminating={e.IsTerminating}: {e.ExceptionObject}");
            if (e.IsTerminating)
                logWriter.Dispose();
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        try
        {
            velopackFailure?.Throw();
            await RunApplicationAsync(args).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AppLog.Write(AppLogLevel.Error, $"Application terminated with an exception: {exception}");
            throw;
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        }
    }

    private static async Task RunApplicationAsync(string[] args)
    {
        // This thread runs the UI, so it takes Avalonia's dispatcher before any service starts work in the background: the
        // first thread to touch it owns it, and the setup below fails on any other. Making the services already posts to
        // it from a worker (the twin library's preload); those posts wait for the UI and run once it's up.
        _ = Avalonia.Threading.Dispatcher.UIThread;
        // The language-free game data decodes on a worker while the services are made; GameResourceService waits for the
        // same load (Lazy: run once, an exception kept and thrown there as before).
        _ = Task.Run(Cloris.Aion2Flow.Resources.Catalog.ResourceCatalog.LoadShared);
        var serviceProvider = CreateServiceProvider();
        var mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();
        try
        {
            AppBuilder
                .Configure(serviceProvider.GetRequiredService<App>)
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            serviceProvider.GetRequiredService<ProcessForegroundWatcher>().Dispose();
            await ShutdownAsync(serviceProvider, mainViewModel).ConfigureAwait(false);
        }
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        CaptureLog.Sink = static (level, message) => AppLog.Write(MapLogLevel(level), message);
        WinDivertLog.Sink = static (level, message) => AppLog.Write(MapLogLevel(level), $"[WinDivert] {message}");
        WinDivertRuntime.Initialize();
        RawPacketDump.ConfigureLogDirectory(LogDirectoryResolver.GetDefaultLogDirectory());

        services.AddSingleton<SettingsService>();
        services.AddSingleton<App>();
        services.AddSingleton<LanguageService>();
        services.AddSingleton<GameResourceService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<PlayerNameDisplayService>();
        services.AddSingleton<UiScaleService>();
        services.AddSingleton<AppUpdateService>();
        services.AddSingleton<EncounterArchiveService>();
        // The twin library lives next to the settings, like the alert sounds.
        services.AddSingleton(provider => new DigitalTwinLibrary(
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(provider.GetRequiredService<SettingsService>().FilePath)) ?? ".", "twin-library")));
        services.AddSingleton(provider => new DigitalTwinService(provider.GetRequiredService<DigitalTwinLibrary>()));
        // The personal bests too, each with its history entry; ranked as the fight graphs' UHS and Rec. switches say.
        services.AddSingleton(provider => new BestRunStore(
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(provider.GetRequiredService<SettingsService>().FilePath)) ?? ".", "best-runs"),
            provider.GetRequiredService<SettingsService>()));
        services.AddSingleton(provider => new BestRunRecorder(
            provider.GetRequiredService<BestRunStore>(), provider.GetRequiredService<WinDivertCaptureService>().Scene, provider.GetRequiredService<GameResourceService>()));
        services.AddSingleton<AvaloniaFrameClockService>();
        services.AddSingleton<UiFrameBatchService>();
        services.AddSingleton<IScenePlaybackTickSourceFactory, AvaloniaScenePlaybackTickSourceFactory>();
        services.AddSingleton<CombatantDetailsFlyoutViewModel>();
        services.AddSingleton<ProcessPortDiscoveryService>();
        services.AddSingleton<ProcessForegroundWatcher>();
        services.AddSingleton<WinDivertCaptureService>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddSingleton<SkillMonitorWindowController>();
        services.AddSingleton<GroupFrameWindowController>();
        services.AddSingleton<FightGraphsWindowController>();
        services.AddSingleton<HiddenCubesWindowController>();
        services.AddSingleton<SettingsFlyoutViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainAppWindow>();

        var serviceProvider = services.BuildServiceProvider();
        Ioc.Default.ConfigureServices(serviceProvider);
        return serviceProvider;
    }

    private static Task ShutdownAsync(ServiceProvider serviceProvider, MainViewModel mainViewModel)
    {
        DisposeUiThreadServices(serviceProvider);
        return Task.Run(async () =>
        {
            try
            {
                await mainViewModel.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    try
                    {
                        RawPacketDump.Shutdown();
                        // An unsaved recording stays readable in the log folder.
                        PacketRecording.Stop();
                    }
                    finally
                    {
                        WinDivertRuntime.Shutdown();
                    }
                }
                finally
                {
                    await serviceProvider.DisposeAsync().ConfigureAwait(false);
                }
            }
        });
    }

    internal static void DisposeUiThreadServices(IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<UiScaleService>().Dispose();
        // After the windows' last saves (their positions, as they close).
        serviceProvider.GetRequiredService<SettingsService>().Flush();
    }

    private static AppLogLevel MapLogLevel(CaptureLogLevel level) => level switch
    {
        CaptureLogLevel.Debug => AppLogLevel.Debug,
        CaptureLogLevel.Info => AppLogLevel.Info,
        CaptureLogLevel.Warning => AppLogLevel.Warning,
        CaptureLogLevel.Error => AppLogLevel.Error,
        _ => AppLogLevel.Info
    };

    private static AppLogLevel MapLogLevel(WinDivertLogLevel level) => level switch
    {
        WinDivertLogLevel.Debug => AppLogLevel.Debug,
        WinDivertLogLevel.Info => AppLogLevel.Info,
        WinDivertLogLevel.Warning => AppLogLevel.Warning,
        WinDivertLogLevel.Error => AppLogLevel.Error,
        _ => AppLogLevel.Info
    };
}

file sealed class App(IServiceProvider serviceProvider) : Application
{
    public override void Initialize()
    {
        SmoothScrollBehavior.Initialize();
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        Styles.Add(new SimpleTheme());
        Styles.Add(AppTextRenderingPolicy.CreateTopLevelStyle());
        Resources.MergedDictionaries.Add(new IconGeometries());
        DataTemplates.Add(SkillTipTemplate.Create());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += OnDesktopExit;
            desktop.MainWindow = serviceProvider.GetRequiredService<MainAppWindow>();
        }

        var settings = serviceProvider.GetRequiredService<SettingsService>();
        Task.Run(() => serviceProvider.GetRequiredService<AppUpdateService>().Start(settings.Current.UpdateAutomatically));
        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        serviceProvider.GetRequiredService<AppUpdateService>()?.PreparePendingUpdateForShutdown(
            serviceProvider.GetRequiredService<SettingsService>().Current.UpdateAutomatically);
    }
}
