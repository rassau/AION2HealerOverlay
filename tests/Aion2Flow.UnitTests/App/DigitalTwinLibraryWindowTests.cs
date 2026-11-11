using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class DigitalTwinLibraryWindowTests
{
    // Adding manually: the region's servers by name with their side, the player's own server chosen to start with; the
    // lookup goes to the twin service, which puts the twin in the library when done. Another region lists its own servers.
    [Fact]
    public void Add_Manually_Looks_Up_A_Character_On_The_Chosen_Server()
    {
        AvaloniaTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
            using var localization = new LocalizationService(new LanguageService());
            using var twins = new DigitalTwinService(new HttpClient(new SilentWebsite()), TimeSpan.Zero);
            var servers = new Dictionary<int, string> { [2304] = "Lumiel", [1301] = "Siel", [2301] = "Israphel", [1101] = "Siel", [1007] = "Test" };
            using var viewModel = new DigitalTwinLibraryViewModel(new DigitalTwinLibrary(folder), twins, localization, servers, homeServerId: 2304);

            Assert.Equal(("eu", 2304), (viewModel.SelectedRegion!.Code, viewModel.SelectedServer!.Id));
            Assert.Equal([2301, 2304, 1301], viewModel.Servers.Select(static server => server.Id));
            Assert.Equal($"Lumiel · {localization["TwinLibrary_Asmodian"]}", viewModel.SelectedServer.ToString());
            Assert.False(viewModel.AddCommand.CanExecute(null));

            viewModel.LookUpName = " Healy ";
            Assert.True(viewModel.AddCommand.CanExecute(null));
            viewModel.AddCommand.Execute(null);

            Assert.Contains(twins.Get(2304, "Healy").State, (DigitalTwinState[])[DigitalTwinState.Queued, DigitalTwinState.Loading]);
            Assert.Contains("Healy", viewModel.AddStatus);
            Assert.Contains("Lumiel", viewModel.AddStatus);

            viewModel.SelectedRegion = viewModel.Regions.Single(static region => region.Code == "nae");
            Assert.Equal([1101], viewModel.Servers.Select(static server => server.Id));
            Assert.Null(viewModel.SelectedServer);
            Assert.False(viewModel.AddCommand.CanExecute(null));
        });
    }

    // A snapshot's trash button asks first ("Delete?"), the second click deletes its file; another row's click asks anew.
    [Fact]
    public void Delete_Asks_Once_Then_Deletes_The_Snapshot()
    {
        AvaloniaTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                using var twins = new DigitalTwinService(new HttpClient(new SilentWebsite()), TimeSpan.Zero);
                var library = new DigitalTwinLibrary(folder);
                var twin = Cloris.Aion2Flow.Tests.SceneRuntime.DigitalTwinBuilderTests.Build();
                library.Add(twin);
                library.Add(twin with { FetchedAt = twin.FetchedAt.AddHours(1) });
                using var viewModel = new DigitalTwinLibraryViewModel(library, twins, localization, new Dictionary<int, string>(), homeServerId: null);
                var load = viewModel.LoadAsync();
                var until = DateTime.UtcNow.AddSeconds(10);
                while (!load.IsCompleted && DateTime.UtcNow < until)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(10);
                }

                var (newest, oldest) = (viewModel.Twins[0], viewModel.Twins[1]);
                newest.DeleteCommand.Execute(null);
                Assert.True(newest.IsDeleteArmed);
                oldest.DeleteCommand.Execute(null);
                Assert.Equal((false, true), (newest.IsDeleteArmed, oldest.IsDeleteArmed));
                Assert.Equal(2, library.Load().Count);

                oldest.DeleteCommand.Execute(null);
                Assert.Equal([newest], viewModel.Twins);
                Assert.Equal(twin.FetchedAt.AddHours(1), Assert.Single(library.Load()).Twin.FetchedAt);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    // The website never answers, so the lookup stays under way.
    private sealed class SilentWebsite : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
