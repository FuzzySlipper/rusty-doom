using Rusty.Engine;

namespace LoadingBay.Game;

/// <summary>
/// Keeps Loading Bay's authored sky in the Engine camera-view service for the
/// lifetime of the product, independently of individual gameplay sessions.
/// </summary>
internal sealed class LoadingBaySkyBackground : IDisposable
{
    internal const string SkySourcePath = "loading-bay/sky/mountain-panorama.png";
    private const ulong SkyByteLength = 2540675;
    private static readonly ContentSha256 SkySha256 = new(
        0xe964e788aea7d984UL, 0xf4c7f9725fd5d92dUL, 0xfa37362d67e14cd2UL, 0xe01599ff13719a94UL);

    private readonly ContentReference _content;
    private readonly RenderResource _resource;
    private readonly ICameraViewService _cameraView;
    private readonly LoadingBaySkyReadout _readout;
    private bool _disposed;

    internal LoadingBaySkyBackground(
        IContentService content,
        ProductContent admitted,
        IGraphicsService appearance,
        ICameraViewService cameraView)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(admitted);
        ArgumentNullException.ThrowIfNull(appearance);
        _cameraView = cameraView ?? throw new ArgumentNullException(nameof(cameraView));

        LoadingBayAdmittedContent.RequireAdmitted(admitted, SkySourcePath);
        ContentReference? skyContent = null;
        RenderResource? resource = null;
        try
        {
            skyContent = content.OpenReference(new ContentOpenRequest(SkySourcePath));
            ContentReferenceInfo skyInfo = LoadingBayAdmittedContent.RequireExact(
                content.ReadReferenceInfo(skyContent), SkySourcePath, SkySha256);
            if (skyInfo.ByteLength != SkyByteLength)
                throw new InvalidOperationException($"Loading Bay's generated sky provenance length changed: {skyInfo.ByteLength}.");

            // Appearance accepts both content-relative and content-prefixed paths. Keep the
            // canonical product identity relative while making the renderer resource path
            // match the authored catalog's sourcePath exactly.
            RenderResourceInfo sky = appearance.OpenResource(new RenderResourceRequest($"content/{SkySourcePath}"));
            _resource = resource = sky.Handle;
            if (sky.Kind != RenderResourceKind.Texture || sky.ByteLength != SkyByteLength || sky.Handle.Handle.Value == 0)
                throw new InvalidOperationException("Engine did not admit Loading Bay's generated mountain sky texture.");
            _cameraView.SetSkyBackground(sky.Handle);
            _content = skyContent;
            _readout = new LoadingBaySkyReadout(SkySourcePath, SkySha256, skyInfo.ByteLength, sky.Handle.Handle.Value, true, true);
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
