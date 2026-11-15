using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Cloris.Aion2Flow.Tests.Services;

public sealed class SettingsServiceTests
{
    // settings.json held by another process (OneDrive, a virus scanner) right when the last change was saved: the shutdown
    // saves it again once that's over.
    [Fact]
    public void Shutdown_Saves_A_Change_That_Met_A_Held_Settings_File()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var settings = new SettingsService(path);
            settings.Update(static s => s.MaxVisibleCombatantRows = 7);
            // Open without FileShare.Delete: nothing can replace the file meanwhile.
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                settings.Update(static s => s.MaxVisibleCombatantRows = 9);
            Assert.Equal(7, new SettingsService(path).Current.MaxVisibleCombatantRows);

            using var provider = new ServiceCollection().AddSingleton(settings).AddSingleton<UiScaleService>().BuildServiceProvider();
            Program.DisposeUiThreadServices(provider);

            Assert.Equal(9, new SettingsService(path).Current.MaxVisibleCombatantRows);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
