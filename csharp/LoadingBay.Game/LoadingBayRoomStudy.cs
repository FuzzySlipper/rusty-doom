using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Opt-in construction study: one retained DC room, mesh collision, and the ordinary FPS controller.</summary>
internal sealed class LoadingBayRoomStudy : ILoadingBaySession
{
    private LoadingBayStudyAudit? _studyAudit;
    private string? _geometryAudit;
    internal string GeometryAudit
    {
        get
        {
            if (_geometryAudit is not null) return _geometryAudit;
            if (_studyAudit is null) return "Audit disabled. Launch with LOADING_BAY_STUDY_AUDIT=1, then run loading-bay.geometry-audit.";
            string report = _studyAudit.Read();
            _studyAudit.Dispose();
            _studyAudit = null;
            return _geometryAudit = report;
        }
    }
    private readonly IEngineContext _engine;
    private readonly LoadingBaySkyReadout _sky;
    private readonly List<Material> _materials = [];
    private readonly List<RenderResource> _textures = [];
    private readonly List<MeshResource> _meshes = [];
    private readonly List<Appearance> _appearances = [];
    private readonly List<Transform> _placements = [];
    private readonly LoadingBayStudyDoor[] _doors = [
        LoadingBayStudyCoordinates.Door(LoadingBayNorthWingRecipe.DoorSurface, 20000, LoadingBayNorthWingRecipe.DoorMin, LoadingBayNorthWingRecipe.DoorMax),
        LoadingBayStudyCoordinates.Door(LoadingBayEastWingRecipe.DoorSurface, 20001, LoadingBayEastWingRecipe.DoorMin, LoadingBayEastWingRecipe.DoorMax),
        LoadingBayStudyCoordinates.Door(LoadingBayTerminalRecipe.DoorSurface, 20002, LoadingBayTerminalRecipe.DoorMin, LoadingBayTerminalRecipe.DoorMax),
        LoadingBayStudyCoordinates.Door(LoadingBaySouthPassageRecipe.DoorSurface, 20003, LoadingBaySouthPassageRecipe.DoorMin, LoadingBaySouthPassageRecipe.DoorMax)];
    private readonly CharacterObstacle[] _doorObstacles = new CharacterObstacle[4];
    private readonly int[] _doorIndices = [-1, -1, -1, -1];
    private readonly LoadingBayPlayerScene _player = null!;
    private readonly UiStream _hud = null!;
    private ulong _hudSequence;
    private ProductUpdateFacts _facts;
    private bool _active;
    private bool _disposed;
    private readonly LoadingBayTuning _tuning = LoadingBayTuning.E1M1 with
    {
        ContentIdentity = "doom-room-study",
        AuthoredPlayerPosition = LoadingBayStudyCoordinates.World(LoadingBayRoomRecipe.SpawnBase),
        InitialYawDegrees = 0,
        AuthoredPlayerKinematicHalfHeight = 0,
        AuthoredBaseEyeHeight = 1.62f,
        InitialPitchDegrees = 0,
        MaximumStepHeight = .3f,
        CameraFarPlane = 100,
    };

    internal LoadingBayRoomStudy(IEngineContext engine, LoadingBaySkyReadout sky)
    {
        _engine = engine;
        _sky = sky;
        try
        {
            _studyAudit = Environment.GetEnvironmentVariable("LOADING_BAY_STUDY_AUDIT") == "1"
                ? new(engine.ImplicitSurfaces) : null;
            Material wall = Material("wall/STARTAN3.png");
            Material floor = Material("flat/FLOOR4_8.png");
            Material carpet = Material("flat/FLAT14.png");
            Material trim = Material("wall/COMPSPAN.png");
            Material ceiling = Material("flat/CEIL3_5.png");
            Material door = Material("wall/DOOR3.png");
            void Emit(RecipeSurface surface)
            {
                surface = LoadingBayStudyCoordinates.World(engine.ImplicitSurfaces, surface);
                MeshResource mesh = engine.ImplicitSurfaces.Generate(new ImplicitGenerateRequest(
                    surface.Field, surface.Root, surface.Min, surface.Max, surface.Sampling.CellSize,
                    surface.Sampling.CreaseDegrees, surface.Sampling.TextureRepeats, surface.Material, surface.Regions,
                    surface.Sampling.MaterialBoundaries));
                _studyAudit?.Capture(surface, mesh);
                for (int i = 0; i < _doors.Length; i++)
                    if (surface.Name == _doors[i].Definition.SurfaceName) _doorIndices[i] = _meshes.Count;
                _meshes.Add(mesh);
                _placements.Add(surface.Placement);
                _appearances.Add(engine.Graphics.CreateMeshAppearance(mesh));
            }
            Material brownWall = Material("wall/BROWN1.png"), southernFloor = Material("flat/FLOOR5_2.png");
            LoadingBayRoomRecipe.Compose(engine.ImplicitSurfaces, wall, floor, carpet, trim, ceiling, door, brownWall, Emit);
            Material liquid = Material("flat/NUKAGE3.png");
            LoadingBayEastWingRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor,
                liquid, trim, ceiling, door, Emit);
            LoadingBayEastGalleryRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor, ceiling, Emit);
            LoadingBayCourtyardRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor, liquid, trim, Emit);
            LoadingBayTerminalRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor, trim, ceiling, door, Emit);
            LoadingBaySouthPassageRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor, trim, ceiling, door, Emit);
            if (_doorIndices.Any(i => i < 0)) throw new InvalidOperationException("Study door surface is missing.");
            _player = new LoadingBayPlayerScene(engine, _tuning);
            _player.PublishRoomMeshes(
                _meshes.Select((mesh, i) => new StaticMeshAsset((ulong)i + 10000, new MeshResourceReference(mesh), 0, 0, 0, 0)).ToArray(),
                _meshes.Select((_, i) => i).Where(i => !_doorIndices.Contains(i)).Select(i => new StaticMeshInstance((ulong)i + 10000, (ulong)i + 10000, _placements[i])).ToArray());
            _hud = engine.Ui.OpenStream(new UiStreamRequest("loading-bay.hud", "loading-bay.hud.snapshot.v1"));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private Material Material(string texturePath)
    {
        RenderResourceInfo texture = _engine.Graphics.OpenResource(new RenderResourceRequest(
            "doom-e1m1/textures/" + texturePath, TextureFilter.Nearest, TextureWrap.Repeat));
        _textures.Add(texture.Handle);
        Material material = _engine.Graphics.CreateMaterial(new MaterialRequest(
            new Color(1, 1, 1, 1), texture.Handle, .9f, new Color(1, 1, 1, 1), Vector3.Zero, 0, false,
            MaterialAlphaMode.Opaque, .5f));
        _materials.Add(material);
        return material;
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        _facts = update.Facts;
        bool moved = false;
        float deltaSeconds = (float)update.Facts.FixedDeltaSeconds;
        LoadingBaySemanticInput input = _player.Update(update, _tuning, (_, _, _) =>
        {
            for (int i = 0; i < _doors.Length; i++)
            {
                moved |= _doors[i].Advance(deltaSeconds);
                _doorObstacles[i] = _doors[i].Obstacle;
            }
            return new(default, _doorObstacles);
        }, _ => { });
        if (input.UseRequested)
        {
            Vector3 position = _player.Capture().Position;
            foreach (LoadingBayStudyDoor doorState in _doors) if (doorState.Use(position)) break;
        }
        if (moved)
        {
            for (int i = 0; i < _doors.Length; i++)
                _placements[_doorIndices[i]] = _placements[_doorIndices[i]] with { Translation = _doors[i].Placement.Translation };
            PublishGeometry();
        }
        return ProductUpdateResult.None;
    }

    public void ActivateSharedRealizations()
    {
        if (!_active) _player.ActivateCamera();
        _active = true;
    }
    public void DeactivateSharedRealizations() => _active = false;
    public void Attach() => Publish();
    public void Publish()
    {
        if (!_active) return;
        PublishGeometry();
        LoadingBayUiValueBuilder value = new();
        List<(string, uint)> fields = [];
        foreach (string key in new[] { "health", "armor", "bullets", "shells", "generation", "step", "droppedFacts", "pendingSchedules",
            "exitVisibilityRevision", "presentationBillboards", "effectsVolume", "admittedSteps", "droppedSteps", "materialMappingCount" })
            fields.Add((key, value.Number(0)));
        fields.Add(("materialCount", value.Number(_materials.Count)));
        foreach (string key in new[] { "complete", "exitVisibility", "effectsMuted", "voxelPresentationRealized" })
            fields.Add((key, value.Bool(false)));
        foreach (string key in new[] { "animationCue", "catalogHash" }) fields.Add((key, value.String("")));
        fields.Add(("skyResourceRealized", value.Bool(_sky.ResourceRealized)));
        fields.Add(("skyBackgroundSelected", value.Bool(_sky.BackgroundSelected)));
        fields.Add(("skyPath", value.String(_sky.SourcePath)));
        fields.Add(("skyHash", value.String(_sky.SourceHash.ToString())));
        fields.Add(("content", value.String("doom-room-study")));
        fields.Add(("updateMode", value.String("Construction study")));
        fields.Add(("lifecycle", value.String("Ready")));
        fields.Add(("facts", value.Array([])));
        _engine.Ui.PublishProjection(new UiProjection(_hud, ++_hudSequence, value.Build(value.Object(fields.ToArray()))));
    }
    private void PublishGeometry() => _engine.Graphics.PublishSnapshot(_appearances.Select((appearance, i) => new AppearanceFact(
        (ulong)i + 10000, false, 0, _placements[i], appearance, true, RenderLayer.Scene)).ToArray());

    public LoadingBayEngineServiceReadout EngineReadout() => LoadingBayEngineServiceReadout.Empty with { Sky = _sky };
    public LoadingBayReadout Readout() => new(new EntityId(1), _facts, 100, 0, LoadingBayArmorProtection.None,
        0, 0, [], null, [], _player.Capture(), [], [], false, 0, _tuning, [], 0);
    public LoadingBayReceipt DeveloperSetTrack(ulong generation, string track, int value, string correlation)
        => new(false, "room-study.no-combat-tracks", correlation);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception> errors = [];
        void Release(IDisposable? resource) { try { resource?.Dispose(); } catch (Exception error) { errors.Add(error); } }
        Release(_studyAudit);
        _studyAudit = null;
        Release(_hud);
        Release(_player);
        foreach (Appearance appearance in _appearances) Release(appearance);
        foreach (MeshResource mesh in _meshes) Release(mesh);
        foreach (Material material in _materials) Release(material);
        foreach (RenderResource texture in _textures) Release(texture);
        if (errors.Count > 0) throw new AggregateException(errors);
    }
}
