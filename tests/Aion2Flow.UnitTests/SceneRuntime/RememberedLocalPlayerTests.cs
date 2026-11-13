using Cloris.Aion2Flow.SceneRuntime.Identity;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class RememberedLocalPlayerTests
{
    // The remembered character is the player wherever the game names it: one seen already right away, a new id (another
    // map) as soon as it's named; another name never. The game's own-character packet wins, and isn't overruled later.
    [Fact]
    public void Remembered_Character_Is_The_Player_Until_The_Game_Says_Otherwise()
    {
        var registry = new RuntimeMetadataRegistry();
        registry.UpsertPcMetadata(466, "Healer", originServerId: 2304);
        registry.UpsertPcMetadata(7195, "Other", originServerId: 2304);

        Assert.True(registry.RememberLocalPlayerIdentity(2304, "Healer"));
        Assert.Equal(466, registry.LocalPlayerEntityId);

        registry.UpsertPcMetadata(9001, "Healer", originServerId: 2304);
        Assert.Equal(9001, registry.LocalPlayerEntityId);
        Assert.True(registry.TryGetPcMetadata(9001, out var player) && player.IsLocalPlayer);
        Assert.False(registry.TryGetPcMetadata(466, out var old) && old.IsLocalPlayer);

        registry.UpsertPcMetadata(5000, "Alt", isLocalPlayer: true, originServerId: 2304);
        Assert.Equal((5000, (2304, "Alt")), (registry.LocalPlayerEntityId, registry.LocalPlayerIdentity));
        Assert.False(registry.RememberLocalPlayerIdentity(2304, "Healer"));
        Assert.Equal(5000, registry.LocalPlayerEntityId);
    }

    // Known by its id alone (0x4a36, the overlay started after the own-character packet went by), the player goes by the
    // remembered character until the game's own-character packet names them.
    [Fact]
    public void Player_Known_By_Id_Alone_Goes_By_The_Remembered_Character()
    {
        var registry = new RuntimeMetadataRegistry();
        registry.RememberLocalPlayerIdentity(2304, "Healer");

        registry.UpsertPcMetadata(4793, string.Empty, isLocalPlayer: true);
        Assert.Equal(4793, registry.LocalPlayerEntityId);
        Assert.True(registry.TryGetPcMetadata(4793, out var player));
        Assert.Equal(("Healer", 2304, true), (player.Nickname, player.OriginServerId, player.IsLocalPlayer));

        registry.UpsertPcMetadata(4793, "Alt", isLocalPlayer: true, originServerId: 1302);
        Assert.True(registry.TryGetPcMetadata(4793, out player));
        Assert.Equal(("Alt", 1302, true), (player.Nickname, player.OriginServerId, player.IsLocalPlayer));
        Assert.Equal((1302, "Alt"), registry.LocalPlayerIdentity);
    }
}
