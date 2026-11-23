using Cloris.Aion2Flow.SceneRuntime.Identity;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

// The player known by their id alone (0x4a36 or their own skills, 0x0138; the overlay started after the own-character packet
// went by): a packet that names that id names the player. An Abyss farm (19 Nov 2026) lost "you" at the player's own kill
// message (0x048D, with their server), and nothing claimed them again until the next map.
public sealed class PlayerKnownByIdTests
{
    [Fact]
    public void A_Packet_Naming_The_Player_Known_By_Id_Alone_Names_The_Player()
    {
        var registry = new RuntimeMetadataRegistry();
        registry.UpsertPcMetadata(4507, string.Empty, isLocalPlayer: true);

        registry.UpsertPcMetadata(4507, "Abcd", originServerId: 1001);

        Assert.Equal(4507, registry.LocalPlayerEntityId);
        Assert.Equal((1001, "Abcd"), registry.LocalPlayerIdentity);
    }

    // An install that remembers another character (switched while the overlay was closed) goes by that one until a packet
    // names the player.
    [Fact]
    public void The_Named_Character_Replaces_Another_Remembered_One()
    {
        var registry = new RuntimeMetadataRegistry();
        registry.RememberLocalPlayerIdentity(1001, "Main");
        registry.UpsertPcMetadata(4507, string.Empty, isLocalPlayer: true);

        registry.UpsertPcMetadata(4507, "Alt", originServerId: 1001);

        Assert.Equal(4507, registry.LocalPlayerEntityId);
        Assert.Equal((1001, "Alt"), registry.LocalPlayerIdentity);
    }

    // The server alone (0x2933), another one than the remembered character's: no mix of that server and the remembered name
    // (which the next start would fill in, or find on a stranger), the player stays nameless until a packet names them.
    [Fact]
    public void Another_Server_Alone_Ends_The_Remembered_Guess_Without_A_Mix()
    {
        var registry = new RuntimeMetadataRegistry();
        registry.RememberLocalPlayerIdentity(1001, "Main");
        registry.UpsertPcMetadata(4507, string.Empty, isLocalPlayer: true);

        registry.UpsertPcMetadata(4507, string.Empty, originServerId: 1002);

        Assert.True(registry.TryGetPcMetadata(4507, out var player));
        Assert.Equal((string.Empty, 1002, true), (player.Nickname, player.OriginServerId, player.IsLocalPlayer));
        Assert.Equal((1001, "Main"), registry.LocalPlayerIdentity);

        registry.UpsertPcMetadata(4507, "Alt", originServerId: 1002);
        Assert.Equal((4507, (1002, "Alt")), (registry.LocalPlayerEntityId, registry.LocalPlayerIdentity));
    }

    // Leaving a map, the next map's own id came before the boundary: the next scene starts with it (CreateContinuity), and a
    // packet naming it there names the player too.
    [Fact]
    public void The_Next_Maps_Id_Keeps_The_Mark_When_Named()
    {
        var previousMap = new RuntimeMetadataRegistry();
        previousMap.UpsertPcMetadata(4507, string.Empty, isLocalPlayer: true);
        previousMap.UpsertPcMetadata(5012, string.Empty, isLocalPlayer: true);
        var registry = new RuntimeMetadataRegistry(previousMap.CreateContinuity());

        registry.UpsertPcMetadata(5012, "Abcd", originServerId: 1001);

        Assert.Equal(5012, registry.LocalPlayerEntityId);
        Assert.Equal((1001, "Abcd"), registry.LocalPlayerIdentity);
    }

    // Named by the game's own-character packet (0x3336), the player stays named by it when a new connection sends their own
    // id again (0x4a36; the capture keeps its last id per connection, 3 of 68 recordings): a packet naming the id otherwise
    // drops the mark as before, and the name and server stay the game's.
    [Fact]
    public void A_Claim_By_Id_Alone_After_The_Own_Character_Packet_Keeps_Its_Name()
    {
        var registry = new RuntimeMetadataRegistry();
        registry.UpsertPcMetadata(4507, "Abcd", isLocalPlayer: true, originServerId: 1001);
        registry.UpsertPcMetadata(4507, string.Empty, isLocalPlayer: true);

        registry.UpsertPcMetadata(4507, "Efgh", originServerId: 1002);

        Assert.Equal((0, (1001, "Abcd")), (registry.LocalPlayerEntityId, registry.LocalPlayerIdentity));
    }

    // The map before's id carried over without the next map's own id before the boundary: here it may be someone else's,
    // whom a packet naming it shows.
    [Fact]
    public void The_Map_Befores_Id_Carried_Over_Goes_When_Named_As_Someone_Else()
    {
        var previousMap = new RuntimeMetadataRegistry();
        previousMap.UpsertPcMetadata(4507, string.Empty, isLocalPlayer: true);
        var registry = new RuntimeMetadataRegistry(previousMap.CreateContinuity());
        Assert.Equal(4507, registry.LocalPlayerEntityId);

        registry.UpsertPcMetadata(4507, "Efgh", originServerId: 1002);

        Assert.Equal(0, registry.LocalPlayerEntityId);
        Assert.Null(registry.LocalPlayerIdentity);
    }
}
