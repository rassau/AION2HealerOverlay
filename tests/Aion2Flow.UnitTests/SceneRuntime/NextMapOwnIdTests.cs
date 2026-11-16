using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

// Leaving a map, the next map's own-id packet (0x4a36, on that map's new connection) comes before the packets that end
// this one: the scene being left keeps its player, the next one starts with the new id.
public sealed class NextMapOwnIdTests
{
    private const int Me = 100;
    private const int Other = 101;
    private const int NextMapId = 200;
    private const int Boss = 300;

    [Fact]
    public void The_Next_Maps_Own_Id_Waits_For_The_Next_Scene()
    {
        var registry = new RuntimeMetadataRegistry();
        registry.UpsertPcMetadata(Me, "Healer", Faction.Dark, CharacterClass.Cleric, isLocalPlayer: true, originServerId: 2304, legionName: "Aether");

        registry.UpsertPcMetadata(NextMapId, string.Empty, isLocalPlayer: true);

        Assert.Equal(Me, registry.LocalPlayerEntityId);
        Assert.True(registry.TryGetPcMetadata(Me, out var player) && player.IsLocalPlayer);
        Assert.False(registry.TryGetPcMetadata(NextMapId, out _));

        var next = new RuntimeMetadataRegistry(registry.CreateContinuity());
        Assert.Equal(NextMapId, next.LocalPlayerEntityId);
        Assert.True(next.TryGetPcMetadata(NextMapId, out var carried));
        Assert.Equal(("Healer", 2304, Faction.Dark, true), (carried.Nickname, carried.OriginServerId, carried.Faction, carried.IsLocalPlayer));
        // Not the class and legion: after a character switch the id is another character's, and 0x3336 sends neither.
        Assert.Null(carried.CharacterClass);
        Assert.Equal(string.Empty, carried.LegionName);
        Assert.False(next.TryGetPcMetadata(Me, out _));
    }

    // A map change inside one scene (nothing fought yet): that map's own-character packet names the new id, the player from
    // then on.
    [Fact]
    public void The_Own_Character_Packet_Makes_The_Waiting_Id_The_Player()
    {
        var registry = new RuntimeMetadataRegistry();
        registry.UpsertPcMetadata(Me, "Healer", isLocalPlayer: true, originServerId: 2304);
        registry.UpsertPcMetadata(NextMapId, string.Empty, isLocalPlayer: true);

        registry.UpsertPcMetadata(NextMapId, "Healer", isLocalPlayer: true, originServerId: 2304);

        Assert.Equal(NextMapId, registry.LocalPlayerEntityId);
        Assert.Equal(NextMapId, new RuntimeMetadataRegistry(registry.CreateContinuity()).LocalPlayerEntityId);
    }

    // Entries saved before build 73 named the next map's id as the player. Opened again, the player is the one who fought
    // with that server and name.
    [Fact]
    public async Task An_Entry_Saved_With_The_Next_Maps_Id_Finds_The_Player_By_Server_And_Name()
    {
        var scene = new SceneLiveReadModel();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(10), Me, "Healer", isLocalPlayer: true, originServerId: 2304);
        sink.AppendNickname(Source(10), Other, "Ranger", originServerId: 1311);
        sink.AppendPlayerGroupMember(Source(20), Me, PlayerGroupMembership.Party(1));
        Hit(sink, Me, 30);
        Hit(sink, Other, 40);
        // As those builds saved it: the next map's id with the player's server and name, the player a party member.
        sink.AppendNickname(Source(50), NextMapId, "Healer", isLocalPlayer: true, originServerId: 2304);
        var payload = scene.CreateArchivePayload();
        Assert.True(payload.IdentityScope.TryGetPcMetadata(NextMapId, out var saved) && saved.IsLocalPlayer);

        using var file = new MemoryStream();
        await SceneArchiveFile.WriteAsync(file, [new ArchivedEncounterRecord { ScenePayload = payload }], TestContext.Current.CancellationToken);
        var scope = Assert.Single(await SceneArchiveFile.ReadAsync(new MemoryStream(file.ToArray()), TestContext.Current.CancellationToken)).ScenePayload.IdentityScope;

        Assert.Equal([Me], scope.PcMetadataSpan.ToArray().Where(static pc => pc.Metadata.IsLocalPlayer).Select(static pc => pc.EntityId));
        Assert.True(scope.TryGetPcMetadata(Me, out var player));
        Assert.Equal(PlayerGroupRelation.Unknown, player.GroupRelation);
    }

    private static void Hit(IRuntimeObservationSink sink, int sourceId, long offsetMilliseconds)
    {
        var hit = new CombatWireObservation { SkillCode = 11_000_010, Damage = 1_000, HitCount = 1, AttemptCount = 1 };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds), sourceId, Boss, in hit);
    }

    private static PacketObservationSource Source(long offsetMilliseconds) =>
        new(1_794_600_000_000 + offsetMilliseconds, 0, 0, 0, offsetMilliseconds, default);
}
