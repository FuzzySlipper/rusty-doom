using System.Text;
using LoadingBay.Game;
using Rusty.Engine;
using Xunit;

public sealed class HudProjectionTests
{
    [Fact]
    public void VoxelHudPublishesTheVisibleWeaponAndGameplayTotals()
    {
        using var game = new LoadingBaySession();
        var ui = new RecordingUi();
        using var hud = new LoadingBayHudProjection(ui);
        var services = LoadingBayEngineServiceReadout.Empty;
        services = services with { Animation = services.Animation with { CueId = "" } };
        hud.Publish(game.Readout(), "project", "voxel", services, false);
        var weapon = ui.Field("weapon");
        Assert.Equal("Pistol", Encoding.UTF8.GetString(ui.Value!.Utf8.Span.Slice((int)weapon.TextOffset, (int)weapon.TextLen)));
        Assert.Equal(LoadingBayE1M1SemanticCatalog.Enemies.Length, ui.Field("totalEnemies").NumberValue);
        Assert.Equal(LoadingBayE1M1SemanticCatalog.Pickups.Length, ui.Field("totalPickups").NumberValue);
        Assert.Equal(0d, ui.Field("collected").NumberValue);
        Assert.Equal(0, ui.Field("kills").NumberValue);
        Assert.Equal(0u, ui.Field("dead").BoolValue);
        var pickup = LoadingBayE1M1SemanticCatalog.Pickups.First(p => !p.StartsDormant && p.ItemId == "ammo/bullets");
        Assert.True(game.CollectCanonicalPickup(pickup.EntityId).Accepted);
        Assert.True(game.ApplyDamage("player", 100, "test").Accepted);
        hud.Publish(game.Readout(), "project", "voxel", services, false);
        Assert.Equal(1u, ui.Field("dead").BoolValue);
        Assert.Equal(1d, ui.Field("collected").NumberValue);
    }

    private sealed class RecordingUi : IUiService
    {
        internal UiValue? Value { get; private set; }
        public UiStream OpenStream(UiStreamRequest request) => new(default, () => { });
        public void PublishProjection(UiProjection projection) => Value = projection.Value;
        internal StructuredValueNode Field(string key)
        {
            var value = Value!;
            var root = value.Nodes.Span[(int)value.Root];
            foreach (uint edge in value.Edges.Span.Slice((int)root.FirstEdge, (int)root.ChildCount))
            {
                var node = value.Nodes.Span[(int)edge];
                if (Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)node.KeyOffset, (int)node.KeyLen)) == key) return node;
            }
            throw new InvalidOperationException($"Missing HUD field {key}");
        }
    }
}
