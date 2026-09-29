using System.Numerics;
using Rusty.Engine;

namespace LoadingBay.Game;

/// <summary>
/// Product-owned E1M1 semantics projected through the generated Perception,
/// Presentation, Animation, and Audio boundaries. Engine retains realization.
/// </summary>
internal sealed class LoadingBayWorldServices : IDisposable
{
    private readonly LoadingBayPerceptionProjection _perception;
    private readonly LoadingBayExitPresentation _presentation;
    private readonly LoadingBayExitButtonAnimation _animation;
    private readonly LoadingBayAudioPolicy _audio;
    private bool _observationPending;
    private bool _sharedRealizationsActive;
    private bool _disposed;

    internal LoadingBayWorldServices(
        IEngineContext engine,
        LoadingBayTuning tuning,
        LoadingBayExitPresentation exitPresentation,
        LoadingBayExitButtonAnimation exitButtonAnimation)
    {
        ArgumentNullException.ThrowIfNull(exitPresentation);
        ArgumentNullException.ThrowIfNull(exitButtonAnimation);
        List<IDisposable> constructed = [];
        try
        {
            _perception = new LoadingBayPerceptionProjection(engine.Perception, tuning);
            _presentation = exitPresentation;
            _animation = exitButtonAnimation;
            _audio = new LoadingBayAudioPolicy(engine.Audio, tuning);
        }
        catch (Exception constructionFailure)
        {
            List<Exception> failures = [constructionFailure];
            for (int index = constructed.Count - 1; index >= 0; index--)
            {
                try { constructed[index].Dispose(); }
                catch (Exception cleanupFailure) { failures.Add(cleanupFailure); }
            }
            throw failures.Count == 1 ? constructionFailure : new AggregateException(failures);
        }
    }

    internal void Update(ProductUpdate update)
    {
        ThrowIfDisposed();
        if (HasAdmittedSteps(update.Facts))
            _observationPending = true;
    }

    internal LoadingBayEngineServiceReadout Publish(LoadingBayReadout readout, LoadingBayPlayerScene player)
    {
        ThrowIfDisposed();
        if (_observationPending)
        {
            _perception.Observe(readout.Player.Value, player);
            _observationPending = false;
        }

        LoadingBayPerceptionReadout perception = _perception.Readout;
        if (_sharedRealizationsActive)
        {
            _presentation.Update(perception);
            _animation.Publish(readout.Complete);
        }
        return new LoadingBayEngineServiceReadout(
            perception,
            _presentation.Readout,
            _animation.Readout,
            _audio.Readout,
            LoadingBayVoxelSceneReadout.Empty,
            LoadingBaySkyReadout.Empty);
    }

    internal void ActivateSharedRealizations()
    {
        ThrowIfDisposed();
        _sharedRealizationsActive = true;
    }

    internal void DeactivateSharedRealizations()
    {
        ThrowIfDisposed();
        _sharedRealizationsActive = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception>? failures = null;
        // The authored landmark billboard and animation span gameplay generations
        // and are retired by LoadingBayProduct after the active session's HUD and
        // other Engine services are gone.
        try { _audio.Dispose(); } catch (Exception error) { (failures ??= []).Add(error); }
        if (failures is { Count: > 0 }) throw new AggregateException(failures);
    }

    private static bool HasAdmittedSteps(ProductUpdateFacts facts) => facts.AdmittedStepCount > 0
        && double.IsFinite(facts.FixedDeltaSeconds)
        && facts.FixedDeltaSeconds > 0d
        && facts.FixedDeltaSeconds <= float.MaxValue;

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LoadingBayWorldServices));
    }
}

/// <summary>
/// One retained Engine animated-mesh appearance for the authored E1M1 exit button.
/// The direct primary path packs the committed external texture closure; no clip pack,
/// rig, controller, local playback loop, or source parsing belongs to Loading Bay.
/// </summary>
internal sealed class LoadingBayExitButtonAnimation : IDisposable
{
    private readonly IGraphicsService _appearanceService;
    private readonly IAnimationService _animation;
    private readonly LoadingBayE1M1AnimationCue _cue;
    private readonly Appearance _appearance;
    private readonly AnimationInstance _instance;
    private readonly RenderResource _mesh;
    private LoadingBayAnimationReadout _readout;
    private bool _completionObserved;
    private bool _disposed;

    internal LoadingBayExitButtonAnimation(IGraphicsService appearanceService, IAnimationService animation, LoadingBayE1M1AnimationCue cue)
    {
        _appearanceService = appearanceService ?? throw new ArgumentNullException(nameof(appearanceService));
        _animation = animation ?? throw new ArgumentNullException(nameof(animation));
        _cue = cue;

        Appearance? appearance = null;
        AnimationInstance? instance = null;
        RenderResource? mesh = null;
        try
        {
            mesh = _animation.OpenAnimatedMesh(new AnimatedMeshResourceRequest(cue.SourcePath));
            _mesh = mesh;
            if (mesh.Handle.Value == 0) throw new InvalidOperationException("Engine Animation returned an invalid E1M1 exit-button mesh handle.");
            Appearance createdAppearance = _animation.CreateAnimatedMeshAppearance(new AnimatedMeshAppearanceRequest(mesh));
            appearance = createdAppearance;
            AnimationInstance createdInstance = _animation.CreateInstance(new AnimationInstanceRequest(createdAppearance, cue.ObjectId));
            instance = createdInstance;
            _appearance = createdAppearance;
            _instance = createdInstance;
            PublishAppearanceSnapshot();
            _animation.SetPlayback(SampleOff());
            _readout = Read(false, 0);
            ValidateReadout(_readout);
        }
        catch
        {
            instance?.Dispose();
            appearance?.Dispose();
            mesh?.Dispose();
            throw;
        }
    }

    internal LoadingBayAnimationReadout Readout => _readout;

    internal void Publish(bool complete)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LoadingBayExitButtonAnimation));
        if (complete && !_completionObserved)
        {
            // Completion changes the visible cue. Reissue this animation's complete
            // graphics snapshot once, while stationary frames keep its retained
            // appearance and Engine-owned realization baseline untouched.
            PublishAppearanceSnapshot();
            _animation.SetPlayback(PlayOn());
            _completionObserved = true;
            _readout = Read(true, checked(_readout.CompletionTransitionCount + 1));
        }
        else
        {
            _readout = Read(_completionObserved, _readout.CompletionTransitionCount);
        }
        ValidateReadout(_readout);
    }

    /// <summary>Starts a newly committed gameplay generation from the authored off sample.</summary>
    internal void ResetForGeneration()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LoadingBayExitButtonAnimation));
        // The new generation begins from the authored off sample. A failed
        // replacement publication propagates to the host; the prior cue is not restored.
        _animation.SetPlayback(SampleOff());
        LoadingBayAnimationReadout reset = Read(false, 0);
        ValidateReadout(reset);
        _completionObserved = false;
        _readout = reset;
        // A committed gameplay generation begins from the authored off sample.
        PublishAppearanceSnapshot();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception>? failures = null;
        try { _instance.Dispose(); }
        catch (Exception exception) { failures = [exception]; }
        try { _appearanceService.PublishSnapshot(ReadOnlySpan<AppearanceFact>.Empty); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        try { _appearance.Dispose(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        try { _mesh.Dispose(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        if (failures is { Count: > 0 }) throw new AggregateException(failures);
    }

    private void PublishAppearanceSnapshot() => _appearanceService.PublishSnapshot(new[] { AppearanceFact() });

    private AppearanceFact AppearanceFact() => new(_cue.ObjectId, false, 0, _cue.Transform, _appearance, true, RenderLayer.Scene);

    private AnimationPlaybackRequest SampleOff() => new(
        _instance, AnimationPlaybackKind.Sample, _cue.OffClip, AnimationLoopMode.Once,
        _cue.PlaybackSpeed, _cue.PlaybackWeight, false, 0f, false, _cue.OffNormalizedTime);

    private AnimationPlaybackRequest PlayOn() => new(
        _instance, AnimationPlaybackKind.Play, _cue.OnClip, AnimationLoopMode.Once,
        _cue.PlaybackSpeed, _cue.PlaybackWeight, true, 0f, false, 0f);

    private LoadingBayAnimationReadout Read(bool completionObserved, uint transitionCount)
    {
        AnimationReadout engine = _animation.Read();
        AnimationRealizationResult realization = _animation.ReadRealization();
        return new LoadingBayAnimationReadout(
            _cue.Id, true, completionObserved, transitionCount, engine.AdmittedMeshes,
            engine.RetainedInstances, engine.PendingPlaybackCommands,
            (uint)realization.Facts.Length, realization.EvictedFactCount);
    }

    private static void ValidateReadout(LoadingBayAnimationReadout readout)
    {
        if (!readout.RetainedAppearance || readout.AdmittedMeshes != 1 || readout.RetainedInstances != 1 ||
            (readout.CompletionObserved && readout.CompletionTransitionCount != 1) ||
            (!readout.CompletionObserved && readout.CompletionTransitionCount != 0))
            throw new InvalidOperationException("Engine Animation did not retain one coherent E1M1 exit-button realization.");
    }
}

/// <summary>One named authored E1M1 landmark visibility query; Engine owns the occlusion cast.</summary>
internal sealed class LoadingBayPerceptionProjection
{
    private readonly IPerceptionService _perception;
    private readonly LoadingBayTuning _tuning;
    private LoadingBayPerceptionReadout _readout;

    internal LoadingBayPerceptionProjection(IPerceptionService perception, LoadingBayTuning tuning)
    {
        _perception = perception ?? throw new ArgumentNullException(nameof(perception));
        _tuning = tuning;
        _readout = new LoadingBayPerceptionReadout(tuning.ExitLandmark.Id, false, 0, 0, 0);
    }

    internal LoadingBayPerceptionReadout Readout => _readout;

    internal void Observe(ulong playerEntity, LoadingBayPlayerScene player)
    {
        PerceptionObserver observer = player.CreatePerceptionObserver(playerEntity, _tuning);
        LoadingBayE1M1Landmark landmark = _tuning.ExitLandmark;
        PerceptionReadoutResult receipt = _perception.QueryVisibility(new PerceptionQueryRequest(
            player.Session,
            new[] { observer },
            new[] { new PerceptionTarget(landmark.EntityId, landmark.Position) },
            ReadOnlyMemory<SpatialEntityCollider>.Empty,
            0,
            0,
            64));
        if (receipt.SelectedObservers != 1 || receipt.SelectedTargets != 1 || receipt.Pairs.Length != 1)
            throw new InvalidOperationException("Engine Perception did not return one bounded E1M1 landmark visibility query.");

        PerceptionPair pair = receipt.Pairs.Span[0];
        if (pair.Observer != playerEntity || pair.Target != landmark.EntityId)
            throw new InvalidOperationException("Engine Perception returned an invalid E1M1 landmark visibility pair.");
        bool visible = pair.Kind == PerceptionPairKind.Visible;
        if (visible != _readout.Visible)
            _readout = new LoadingBayPerceptionReadout(landmark.Id, visible, checked(_readout.Revision + 1), receipt.VisibilityCasts, receipt.OcclusionRejects);
        else
            _readout = _readout with { VisibilityCasts = receipt.VisibilityCasts, OcclusionRejects = receipt.OcclusionRejects };
    }
}

/// <summary>Retained Engine structured billboard for the authored exit landmark; this is not a DOM HUD duplicate.</summary>
internal sealed class LoadingBayExitPresentation : IDisposable
{
    private static readonly Color Foreground = new(0.8f, 1f, 0.8f, 1f);
    private static readonly Color Background = new(0.02f, 0.08f, 0.02f, 0.85f);
    private static readonly Color Border = new(0.3f, 0.9f, 0.3f, 1f);
    private readonly IPresentationService _presentation;
    private readonly LoadingBayTuning _tuning;
    private readonly PresentationBillboard _billboard;
    private PresentationFactsResult _readout;
    private bool _disposed;

    internal LoadingBayExitPresentation(IPresentationService presentation, LoadingBayTuning tuning)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _tuning = tuning;
        LoadingBayPerceptionReadout initial = new(tuning.ExitLandmark.Id, false, 0, 0, 0);
        _billboard = _presentation.CreateStructuredBillboard(Descriptor(initial));
        _readout = _presentation.Read();
        Validate(_readout);
    }

    internal PresentationFactsResult Readout => _readout;

    internal void Update(LoadingBayPerceptionReadout observation)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LoadingBayExitPresentation));
        _presentation.UpdateStructuredBillboard(_billboard, Descriptor(observation));
        _readout = _presentation.Read();
        Validate(_readout);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _billboard.Dispose();
    }

    private PresentationStructuredBillboardDescriptor Descriptor(LoadingBayPerceptionReadout observation)
    {
        LoadingBayE1M1Landmark landmark = _tuning.ExitLandmark;
        bool visible = observation.Visible;
        string status = visible ? "Exit visible" : "Exit occluded";
        PresentationBillboardMeter[] meters =
        [
            new PresentationBillboardMeter("exit-visibility", "loading-bay.exit.visibility", "Exit visibility", visible ? 1f : 0f, 0f, 1f, false, 0f,
                PresentationBillboardMeterFillDirection.LeftToRight, 1, Foreground, Foreground, Background, Border)
        ];
        PresentationBillboardStatusCue[] cues =
        [
            new PresentationBillboardStatusCue("exit-visibility", "loading-bay.exit.status", status, false, default)
        ];
        return new PresentationStructuredBillboardDescriptor(
            landmark.EntityId,
            new PresentationAnchor(PresentationAnchorKind.World, landmark.Position, 0, Vector3.Zero),
            true, "loading-bay.exit.label", landmark.Id,
            false, default,
            "loading-bay.exit.accessible", "E1M1 exit visibility",
            meters, cues,
            180f, 6f, PresentationBillboardAlignment.Center,
            new PresentationBillboardStyle(0.9f, Background, Border, 4f),
            new PresentationBillboardLayout(10, PresentationBillboardLayoutSizing.DistanceScaled, 24f, 0.75f, 1.5f,
                new PresentationBillboardSafeArea(8f, 8f, 8f, 8f), PresentationBillboardEdgeBehavior.Clamp, PresentationBillboardOverlapBehavior.Suppress),
            PresentationFontKind.System, default, "sans-serif", 16f, Foreground, Background, (float)_tuning.PerceptionMaximumDistance,
            PresentationBillboardLayer.Occluded, true);
    }

    private static void Validate(PresentationFactsResult readout)
    {
        if (readout.ActiveBillboards == 0)
            throw new InvalidOperationException("Engine Presentation did not retain the E1M1 exit billboard.");
    }
}

/// <summary>
/// E1M1 effects policy: the Sfx bus state and the WAD-derived weapon and door
/// sounds (<c>content/doom-e1m1/sounds</c>, see docs/source-provenance.md).
/// Only the room-study scene emits them; its doors open once and never close.
/// The product decides when a sound plays; the Engine realizes it.
/// </summary>
internal sealed class LoadingBayAudioPolicy : IDisposable
{
    private const string SoundRoot = "content/doom-e1m1/sounds/";
    private const float FullVolume = 1f;
    private const float NormalPitch = 1f;
    private const float CenterPan = 0f;
    /// <summary>Weapon sounds come from the player, so they are not positioned.</summary>
    private const float PlayerSpatialBlend = 0f;
    private const float PlayerAttenuation = 1f;
    /// <summary>Door sounds come from the door; Doom's 1200-unit sound range at 16 units per metre.</summary>
    private const float WorldSpatialBlend = 1f;
    private const float WorldAttenuation = 75f;
    private readonly IAudioService _audio;
    private readonly AudioBusReadout _readout;
    private readonly AudioClip _pistol;
    private readonly AudioClip _shotgun;
    private readonly AudioClip _punch;
    private readonly AudioClip _doorOpen;
    private ulong _nextSignal;

    internal LoadingBayAudioPolicy(IAudioService audio, LoadingBayTuning tuning)
    {
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
        // First-party tuning and Engine delivery are trusted. The bus readout
        // remains for the HUD; no round-trip revalidation.
        _audio.SetBusVolume(new AudioBusVolumeRequest(AudioBus.Sfx, tuning.EffectsVolume));
        _audio.SetBusMuted(new AudioBusMutedRequest(AudioBus.Sfx, tuning.EffectsMuted));
        _readout = _audio.ReadBus(new AudioBusReadRequest(AudioBus.Sfx));
        List<AudioClip> opened = [];
        AudioClip Open(string lump)
        {
            AudioClip clip = _audio.OpenClip(new AudioClipRequest(SoundRoot + lump + ".wav"));
            opened.Add(clip);
            return clip;
        }
        try
        {
            _pistol = Open("DSPISTOL");
            _shotgun = Open("DSSHOTGN");
            _punch = Open("DSPUNCH");
            _doorOpen = Open("DSDOROPN");
        }
        catch
        {
            foreach (AudioClip clip in opened) clip.Dispose();
            throw;
        }
    }

    internal AudioBusReadout Readout => _readout;

    /// <summary>A pistol or shotgun shot. The fist is silent unless it lands, as in Doom.</summary>
    internal void WeaponFired(RecipeWeapon weapon)
    {
        if (weapon == RecipeWeapon.Pistol) EmitFromPlayer(_pistol);
        else if (weapon == RecipeWeapon.Shotgun) EmitFromPlayer(_shotgun);
    }

    internal void PunchLanded() => EmitFromPlayer(_punch);

    internal void DoorOpening(Vector3 position) => EmitAt(_doorOpen, position);

    private void EmitFromPlayer(AudioClip clip)
        => Emit(clip, PlayerSpatialBlend, PlayerAttenuation, AudioEmitterKind.Global2d, Vector3.Zero);

    private void EmitAt(AudioClip clip, Vector3 position)
        => Emit(clip, WorldSpatialBlend, WorldAttenuation, AudioEmitterKind.World3d, position);

    private void Emit(AudioClip clip, float spatialBlend, float attenuation, AudioEmitterKind kind, Vector3 position)
        => _audio.Emit(new AudioEmitRequest($"loading-bay.sfx.{_nextSignal++}", new AudioSourceDescriptor(
            clip, AudioBus.Sfx, FullVolume, NormalPitch, false, spatialBlend, attenuation, CenterPan,
            kind, position, 0, Vector3.Zero)));

    public void Dispose()
    {
        _pistol.Dispose();
        _shotgun.Dispose();
        _punch.Dispose();
        _doorOpen.Dispose();
    }
}

internal readonly record struct LoadingBayPerceptionReadout(string LandmarkId, bool Visible, ulong Revision, uint VisibilityCasts, uint OcclusionRejects);
internal readonly record struct LoadingBayAnimationReadout(
    string CueId,
    bool RetainedAppearance,
    bool CompletionObserved,
    uint CompletionTransitionCount,
    uint AdmittedMeshes,
    uint RetainedInstances,
    uint PendingPlaybackCommands,
    uint RetainedRealizationFacts,
    ulong EvictedRealizationFacts);

internal readonly record struct LoadingBayEngineServiceReadout(
    LoadingBayPerceptionReadout Perception,
    PresentationFactsResult Presentation,
    LoadingBayAnimationReadout Animation,
    AudioBusReadout Audio,
    LoadingBayVoxelSceneReadout VoxelScene,
    LoadingBaySkyReadout Sky)
{
    internal static LoadingBayEngineServiceReadout Empty => new(
        default,
        default,
        default,
        default,
        LoadingBayVoxelSceneReadout.Empty,
        LoadingBaySkyReadout.Empty);
}
