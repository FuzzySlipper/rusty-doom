using System.Numerics;
using System.Text.Json;
using Rusty.Engine;
namespace PortableExample;
public sealed class Product : IEngineProduct
{
    private readonly IEngineContext engine;
    private readonly PortableAssetContent sprite;
    private readonly RenderResource texture, model;
    private readonly SpriteAtlas atlas;
    private readonly Appearance image;
    private readonly SpritePlayback playback;
    public Product(ProductCreateContext context)
    {
        engine = context.Engine;
        using var descriptor = engine.Content.OpenReference(new("sprite/asset.rusty.json"));
        sprite = new(engine.Content, descriptor, "sprite");
        using var pixels = sprite.OpenMember("atlas");
        texture = engine.Graphics.OpenResourceFromContent(new(pixels,TextureFilter.Nearest,TextureWrap.Clamp)).Handle;
        atlas = sprite.CreateAtlas(engine.Graphics,texture,"atlas");
        var frame = sprite.Facts.Frames.Span[0];
        image = engine.Graphics.CreateSpriteFromAtlas(new(atlas,0,frame.Pivot/frame.Canvas,frame.Extent,BillboardMode.None,SpriteSizeMode.Pixel,100,SpriteDepthPolicy.DepthTestOff,new(1,1,1,1)));
        engine.Graphics.SetSpriteViewport(new(image,true,new(.2f,.1f),new(.6f,.8f),new(.5f,.5f),SpriteViewportFit.Contain));
        playback = sprite.CreatePlayback(engine.Graphics,image,atlas,sprite.FindAnimation("walk","south")!);
        engine.Graphics.ControlSpritePlayback(new(playback,SpritePlaybackControl.Start));
        using var modelDescriptor = engine.Content.OpenReference(new("model/asset.rusty.json"));
        using var body = new PortableAssetContent(engine.Content,modelDescriptor,"model");
        using var bytes = body.OpenMember("model");
        model = engine.Animation.OpenAnimatedMeshFromContent(new(bytes));
        var clips = engine.Animation.ReadClips(model).ToArray();
        if (clips.Length == 0) throw new InvalidOperationException("Expected an animated model");
        string report = JsonSerializer.Serialize(new { frames=sprite.Facts.Frames.Length, directions=sprite.Facts.Actions.Length,
            clips=clips.Select(c=>c.Name).ToArray(), animations=sprite.Facts.AnimationFrames.ToArray().Select(f=>f.Animation).Distinct().ToArray() });
        Console.WriteLine("PORTABLE_CONSUMED " + report);
        if (Environment.GetEnvironmentVariable("PORTABLE_REPORT") is string path) File.WriteAllText(path,report);
        engine.Graphics.PublishSnapshot(new AppearanceFact[]{new(8655,false,0,new(Vector3.Zero,Quaternion.Identity,Vector3.One),image,true,RenderLayer.Viewmodel)});
    }
    public void Start() {} public ProductUpdateResult Update(ProductUpdate update) { engine.Graphics.AdvanceSpritePlayback(new(playback)); return ProductUpdateResult.None; }
    public void Pause() {} public void Resume() {} public void Restart() {} public void Shutdown() {}
    public void Dispose() { engine.Graphics.PublishSnapshot(ReadOnlySpan<AppearanceFact>.Empty); playback.Dispose();image.Dispose();atlas.Dispose();texture.Dispose();model.Dispose();sprite.Dispose(); }
}
