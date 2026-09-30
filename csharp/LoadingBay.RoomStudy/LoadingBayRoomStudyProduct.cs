using System.Text.Json;
using System.Text.Json.Serialization;
using Rusty.Engine;
using Rusty.Engine.Debugging;

namespace LoadingBay.Game;

/// <summary>Optional experiment using the existing product lifecycle and Engine host.</summary>
public sealed class LoadingBayRoomStudyProduct : IEngineProduct, IDebugCommandModuleSource
{
    private readonly LoadingBayProduct _product;
    public LoadingBayRoomStudyProduct(ProductCreateContext context)
    {
        var settings = JsonSerializer.Deserialize(context.Content.ReadBytes("loading-bay/room-study.settings.json").Span,
            RoomStudyJsonContext.Default.RoomStudySettings)!;
        _product = new LoadingBayProduct(context,
            (engine, sky) => new LoadingBayRoomStudy(engine, sky, settings.AuditGeometry));
    }
    public void Attach() => _product.Attach();
    public void Start() => _product.Start();
    public ProductUpdateResult Update(ProductUpdate update) => _product.Update(update);
    public void Pause() => _product.Pause();
    public void Resume() => _product.Resume();
    public void Restart() => _product.Restart();
    public void Shutdown() => _product.Shutdown();
    public void Dispose() => _product.Dispose();
    public void RegisterDebugCommands(IDebugCommandModuleRegistrar registrar) => _product.RegisterDebugCommands(registrar);
}

internal sealed record RoomStudySettings(bool AuditGeometry);
[JsonSerializable(typeof(RoomStudySettings))]
internal partial class RoomStudyJsonContext : JsonSerializerContext;
