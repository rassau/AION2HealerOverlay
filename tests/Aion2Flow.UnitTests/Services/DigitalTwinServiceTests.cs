using System.Net;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Tests.SceneRuntime;

namespace Cloris.Aion2Flow.Tests.Services;

public sealed class DigitalTwinServiceTests
{
    // Global servers only; the hundreds digit is the region, the thousands digit the faction.
    [Theory]
    [InlineData(1101, "nae")]
    [InlineData(2205, "naw")]
    [InlineData(1301, "eu")]
    [InlineData(2323, "eu")]
    [InlineData(1401, "la")]
    [InlineData(2510, "as")]
    [InlineData(1007, null)]
    [InlineData(2023, null)]
    [InlineData(999, null)]
    public void Region_Comes_From_The_Server_Id(int serverId, string? region) => Assert.Equal(region, DigitalTwinService.RegionOf(serverId));

    // Names are matched whole (the search marks the matched part), on that server; the id comes escaped already.
    [Fact]
    public void Character_Id_Is_The_Exact_Name_On_The_Server()
    {
        const string search = """
            {"list":[{"characterId":"abc%3D","name":"<strong>Heal</strong>er","serverId":1301},
                     {"characterId":"x/y","name":"Healer","serverId":1302},
                     {"characterId":"z","name":"<strong>Heal</strong>","serverId":1303}]}
            """;

        Assert.Equal("abc%3D", DigitalTwinService.FindCharacterId(search, "Healer", 1301));
        Assert.Equal("x%2Fy", DigitalTwinService.FindCharacterId(search, "healer", 1302));
        Assert.Null(DigitalTwinService.FindCharacterId(search, "Heal", 1301));
        Assert.Null(DigitalTwinService.FindCharacterId("""{"list":[]}""", "Healer", 1301));
    }

    // A lookup: search, profile, gear, each item, each open board; then the twin is there, and asking again without
    // "again" doesn't look the player up a second time.
    [Fact]
    public async Task Lookup_Fetches_Everything_And_Builds_The_Twin()
    {
        var website = new FakeWebsite(url => url switch
        {
            _ when url.StartsWith("https://api-search.plaync.com/aion2global/search/v2/character?keyword=Healy&region=eu&serverId=1301", StringComparison.Ordinal)
                => """{"list":[{"characterId":"abc%3D","name":"<strong>Healy</strong>","serverId":1301}]}""",
            _ when url.StartsWith("https://aion2.plaync.com/api/character/info?characterId=abc%3D&serverId=1301&lang=en-US&region=eu", StringComparison.Ordinal) => DigitalTwinBuilderTests.Info,
            _ when url.StartsWith("https://aion2.plaync.com/api/character/equipment?characterId=abc%3D", StringComparison.Ordinal) => DigitalTwinBuilderTests.Equipment,
            _ when url.Contains("equipment/item?id=110730051&enchantLevel=15&slotPos=1&characterId=abc%3D", StringComparison.Ordinal) => DigitalTwinBuilderTests.Weapon,
            _ when url.Contains("equipment/item?id=40000001&enchantLevel=4&slotPos=41&", StringComparison.Ordinal) => DigitalTwinBuilderTests.Arcana,
            _ when url.Contains("daevanion/detail?boardId=71&characterId=abc%3D", StringComparison.Ordinal) => DigitalTwinBuilderTests.Board,
            _ => null
        });
        var library = new DigitalTwinLibrary(Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}"));
        using var service = new DigitalTwinService(new HttpClient(website), TimeSpan.Zero, library);
        var done = new TaskCompletionSource();
        service.Changed += (serverId, name) =>
        {
            if (service.Get(serverId, name).State is DigitalTwinState.Ready or DigitalTwinState.Failed or DigitalTwinState.NotFound)
                done.TrySetResult();
        };

        try
        {
            service.Request(1301, "Healy");
            await done.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            var lookup = service.Get(1301, "healy");
            Assert.Equal(DigitalTwinState.Ready, lookup.State);
            Assert.Equal(("Healy", 2, 2), (lookup.Twin!.Name, lookup.Twin.Gear.Length, lookup.Twin.Boards.Length));
            Assert.Equal(6, website.Requests.Count);
            Assert.DoesNotContain(website.Requests, static url => url.Contains("boardId=72", StringComparison.Ordinal));
            // The twin is in the library too.
            Assert.Equal("Healy", Assert.Single(library.Load()).Twin.Name);

            service.Request(1301, "Healy");
            Assert.Equal(DigitalTwinState.Ready, service.Get(1301, "Healy").State);
        }
        finally
        {
            if (Directory.Exists(library.Folder))
                Directory.Delete(library.Folder, recursive: true);
        }
    }

    // Snapshots from the library are there from the start; an automatic lookup still fetches a fresh one once per session
    // (here the website answers nothing, so the kept snapshot stays). Deleting one from the library takes it out of the session.
    [Fact]
    public async Task Library_Snapshots_Are_There_From_The_Start_And_Go_When_Deleted()
    {
        var website = new FakeWebsite(static _ => null);
        using var service = new DigitalTwinService(new HttpClient(website), TimeSpan.Zero);
        var kept = DigitalTwinBuilderTests.Build();

        service.Preload([kept]);
        Assert.Equal((DigitalTwinState.Ready, kept.FetchedAt), (service.Get(kept.ServerId, kept.Name).State, service.Get(kept.ServerId, kept.Name).Twin!.FetchedAt));

        var done = new TaskCompletionSource();
        service.Changed += (serverId, name) =>
        {
            if (service.Get(serverId, name).State is DigitalTwinState.NotFound or DigitalTwinState.Failed)
                done.TrySetResult();
        };
        service.Request(kept.ServerId, kept.Name);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Single(website.Requests);
        Assert.Equal(kept.FetchedAt, service.Get(kept.ServerId, kept.Name).Twin!.FetchedAt);

        service.Request(kept.ServerId, kept.Name);
        Assert.Single(website.Requests);

        service.Remove(kept);
        Assert.Null(service.Get(kept.ServerId, kept.Name).Twin);
    }

    [Fact]
    public void Other_Servers_Are_Not_Looked_Up()
    {
        var website = new FakeWebsite(static _ => null);
        using var service = new DigitalTwinService(new HttpClient(website), TimeSpan.Zero);

        service.Request(1007, "Someone");

        Assert.Equal(DigitalTwinState.Unsupported, service.Get(1007, "Someone").State);
        Assert.Empty(website.Requests);
    }

    private sealed class FakeWebsite(Func<string, string?> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            lock (Requests)
                Requests.Add(url);
            return Task.FromResult(answer(url) is { } json
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
