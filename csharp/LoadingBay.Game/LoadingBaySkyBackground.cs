using Rusty.Engine;

namespace LoadingBay.Game;

/// <summary>
/// Keeps Loading Bay's authored sky in the Engine camera-view service for the
/// lifetime of the product, independently of individual gameplay sessions.
/// </summary>
internal sealed class LoadingBaySkyBackground : IDisposable
{
    internal const string SkySourcePath = "loading-bay/sky/mountain-panorama.png";

    private readonly ContentReference _content;
    private readonly RenderResource _resource;
    private readonly ICameraViewService _cameraView;
    private readonly LoadingBaySkyReadout _readout;
    private bool _disposed;

    internal LoadingBaySkyBackground(
        IContentService content,
        IGraphicsService appearance,
        ICameraViewService cameraView)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(appearance);
        _cameraView = cameraView ?? throw new ArgumentNullException(nameof(cameraView));

        ContentReference? skyContent = null;
        RenderResource? resource = null;
        try
        {
            // First-party Engine delivery is trusted by path. Engine reports a
            // missing artifact through its own open failure; no hash revalidation.
            skyContent = content.OpenReference(new ContentOpenRequest(SkySourcePath));
            ContentReferenceInfo skyInfo = LoadingBayAdmittedContent.RequireSingle(
                content.ReadReferenceInfo(skyContent), SkySourcePath);

            // Appearance accepts both content-relative and content-prefixed paths. Keep the
            // canonical product identity relative while making the renderer resource path
            // match the authored catalog's sourcePath exactly.
            RenderResourceInfo sky = appearance.OpenResource(new RenderResourceRequest($"content/{SkySourcePath}"));
            _resource = resource = sky.Handle;
            if (sky.Kind != RenderResourceKind.Texture || sky.ByteLength == 0 || sky.Handle.Handle.Value == 0)
                throw new InvalidOperationException("Engine did not admit Loading Bay's generated mountain sky texture.");
            _cameraView.SetSkyBackground(sky.Handle);
            _content = skyContent;
            _readout = new LoadingBaySkyReadout(SkySourcePath, skyInfo.Sha256, skyInfo.ByteLength, sky.Handle.Handle.Value, true, true);
        }
        catch
        {
            resource?.Dispose();
            skyContent?.Dispose();
            throw;
        }
    }

    internal LoadingBaySkyReadout Readout => _readout;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception>? failures = null;
        try { _cameraView.ClearSkyBackground(new ClearSkyBackgroundRequest()); }
        catch (Exception exception) { failures = [exception]; }
        try { _resource.Dispose(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        try { _content.Dispose(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        if (failures is { Count: > 0 }) throw new AggregateException(failures);
    }
}
