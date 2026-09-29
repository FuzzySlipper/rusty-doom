using System.Numerics;
using System.Text.Json;
using Rusty.Engine;
namespace AttachmentExample;
public sealed class Product : IEngineProduct
{
    private readonly IEngineContext engine;
    private readonly RenderResource bodyResource,childResource;
    private readonly Appearance body,child;
    private readonly AnimationInstance animation;
    private readonly Camera camera;
    public Product(ProductCreateContext context)
    {
        engine=context.Engine;
        using var reference=engine.Content.OpenReference(new("asset.rusty.json"));
        using var descriptor=new PortableAssetContent(engine.Content,reference,"grip");
        var binding=descriptor.Facts.Attachments.Span[0];
        using var bodySource=descriptor.OpenMember(binding.TargetId);
        using var childSource=descriptor.OpenMember(binding.ChildId);
        bodyResource=engine.Animation.OpenAnimatedMeshFromContent(new(bodySource));
        childResource=engine.Animation.OpenAnimatedMeshFromContent(new(childSource));
        body=engine.Animation.CreateAnimatedMeshAppearance(new(bodyResource));
        child=engine.Animation.CreateAnimatedMeshAppearance(new(childResource));
        CameraQueries.TryLookAtPose(new(4,3,6),new(0,1.5f,0),0,out var pose);
        camera=engine.CameraView.CreateCamera(new(pose,CameraBasisMode.Derived,default,new(CameraProjectionKind.Perspective,50,0,.05f,100),CameraViewports.Full));
        engine.CameraView.SetActiveCamera(camera);
        engine.Graphics.PublishChanges(new(new AppearanceFact[]{
            new(1,false,0,new(Vector3.Zero,Quaternion.Identity,Vector3.One),body,true,RenderLayer.Scene),
            new(2,true,1,binding.Transform,child,true,RenderLayer.Scene)
        },ReadOnlyMemory<ulong>.Empty,new MeshJointAttachment[]{new(2,binding.Joint)}));
        animation=engine.Animation.CreateInstance(new(body,1));
        engine.Animation.SetPlayback(new(animation,AnimationPlaybackKind.Sample,"run",AnimationLoopMode.Repeat,1,1,true,0,false,.5f));
        string report=JsonSerializer.Serialize(new{joint=binding.Joint,target=binding.TargetId,child=binding.ChildId,position=new[]{binding.Transform.Translation.X,binding.Transform.Translation.Y,binding.Transform.Translation.Z},scale=new[]{binding.Transform.Scale.X,binding.Transform.Scale.Y,binding.Transform.Scale.Z},pose=.5});
        if(Environment.GetEnvironmentVariable("PORTABLE_REPORT") is string path) File.WriteAllText(path,report);
        Console.WriteLine("ATTACHMENT_CONSUMED "+report);
    }
    public void Start(){} public ProductUpdateResult Update(ProductUpdate update)=>ProductUpdateResult.None;
    public void Pause(){} public void Resume(){} public void Restart(){} public void Shutdown(){}
    public void Dispose(){animation.Dispose();engine.Graphics.PublishSnapshot(ReadOnlySpan<AppearanceFact>.Empty);child.Dispose();body.Dispose();childResource.Dispose();bodyResource.Dispose();camera.Dispose();}
}
