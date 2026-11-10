using Avalonia.Threading;
using Cloris.Aion2Flow.Services;
using Velopack;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class AppUpdateServiceTests
{
    // "Update automatically" off (the default): nothing at start, and an update downloaded anyway isn't installed on
    // closing. On: the start checks and downloads, and closing installs it without a restart.
    [Fact]
    public void Updates_Happen_By_Themselves_Only_When_Turned_On()
    {
        AvaloniaTestHost.Run(() =>
        {
            var off = new FakeSource(Release("1.0.55"));
            var service = off.Service();
            Settle(service.Start(automatic: false));
            Assert.Equal((AppUpdateState.Idle, 0), (service.State, off.Checks));

            Settle(service.Start(automatic: true));
            Assert.Equal((AppUpdateState.ReadyToRestart, 1, 1), (service.State, off.Checks, off.Downloads));
            service.PreparePendingUpdateForShutdown(automatic: false);
            Assert.Empty(off.Staged);

            var on = new FakeSource(Release("1.0.55"));
            service = on.Service();
            Settle(service.Start(automatic: true));
            Assert.Empty(on.Staged);
            service.PreparePendingUpdateForShutdown(automatic: true);
            Assert.Equal(new[] { false }, on.Staged);
            Assert.Equal(0, on.Shutdowns);
        });
    }

    // "Update now": a check, the download and the restart into the new version; one already downloaded restarts at once;
    // with nothing new it just says so.
    [Fact]
    public void Update_Now_Restarts_Into_The_New_Version()
    {
        AvaloniaTestHost.Run(() =>
        {
            var fresh = new FakeSource(Release("1.0.55"));
            Settle(fresh.Service().UpdateNowAsync());
            Assert.Equal((1, 1, 1), (fresh.Checks, fresh.Downloads, fresh.Shutdowns));
            Assert.Equal(new[] { true }, fresh.Staged);

            var downloaded = new FakeSource(Release("1.0.55"));
            var service = downloaded.Service();
            Settle(service.Start(automatic: true));
            Settle(service.UpdateNowAsync());
            Assert.Equal((1, 1, 1), (downloaded.Checks, downloaded.Downloads, downloaded.Shutdowns));
            Assert.Equal(new[] { true }, downloaded.Staged);
            service.PreparePendingUpdateForShutdown(automatic: true);
            Assert.Equal(new[] { true }, downloaded.Staged);

            var current = new FakeSource(null);
            service = current.Service();
            Settle(service.UpdateNowAsync());
            Assert.Equal((AppUpdateState.UpToDate, 0, 0), (service.State, current.Staged.Count, current.Shutdowns));
        });
    }

    // The update work runs in the background and reports to the UI thread, which is this one.
    private static void Settle(Task work)
    {
        work.GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
    }

    private static UpdateInfo Release(string version) =>
        new(new VelopackAsset { PackageId = "AION2HealerOverlay", Version = SemanticVersion.Parse(version), FileName = $"AION2HealerOverlay-{version}-full.nupkg" }, false, null!, []);

    // A release page with this update (or none), counting what the service asks of it.
    private sealed class FakeSource(UpdateInfo? update) : IAppUpdateEndpoint
    {
        public int Checks { get; private set; }

        public int Downloads { get; private set; }

        public int Shutdowns { get; private set; }

        // Each install staged for after closing: whether it restarts into the new version.
        public List<bool> Staged { get; } = [];

        public string Name => "Fake";

        public bool IsManaged => true;

        public VelopackAsset? UpdatePendingRestart => null;

        public AppUpdateService Service() => new(new AppUpdateCoordinator([this]), () => Shutdowns++);

        public Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken)
        {
            Checks++;
            return Task.FromResult(update);
        }

        public Task DownloadAsync(UpdateInfo update, Action<int> progress, CancellationToken cancellationToken)
        {
            Downloads++;
            progress(100);
            return Task.CompletedTask;
        }

        public void StageForExit(VelopackAsset asset, bool restart) => Staged.Add(restart);
    }
}
