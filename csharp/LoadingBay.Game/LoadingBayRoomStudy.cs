using System.Numerics;
using System.Text.Json;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Engine.Entities;
using Rusty.Engine.Implicit;
using Rusty.Engine.Interaction;

namespace LoadingBay.Game;

/// <summary>Authored DC level with retained geometry and recipe-owned gameplay.</summary>
internal sealed class LoadingBayRoomStudy : ILoadingBaySession, ILoadingBaySpatialObservationSession, ILoadingBayInteractionDebugSession, IWorldInteractionScene
{
    private const int SpatialMapMaximumRadius = 15;
    private const double SpatialMapHalfCell = .5d;
    private const int SpatialMapMaximumAnnotations = 128;
    private const double SpatialMapFloorClearance = .05d;
    private const double SpatialMapCeilingClearance = .05d;
    private const double SpatialMapNavigationBelowSupport = .05d;
    private const uint SpatialMapDoorCollisionGroup = 1;
    private const uint SpatialMapDoorCollisionMask = uint.MaxValue;
    private const float CombatObservationRange = 64f;
    private const float InteractionAcquireAngleRadians = .32f;
    private const float InteractionReleaseAngleRadians = .48f;
    private const float InteractionMaximumDistance = LoadingBayStudyDoor.UseDistance;
    private const ulong ExitInteractionEntity = 33000;
    private const float RadiansToDegrees = 180f / MathF.PI;
    private static readonly JsonSerializerOptions CombatObservationJson = new(JsonSerializerDefaults.Web);
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
    private readonly LoadingBayPlayerSnapshot _spawn = null!;
    private readonly UiStream _hud = null!;
    private ulong _hudSequence;
    private readonly LoadingBayRecipeGameplay _gameplay = null!;
    private readonly AimAssist _gamepadAim = new();
    private readonly WorldInteraction _worldInteraction = null!;
    private readonly InteractionDebugModule _interactionDebug = null!;
    private AimAssistReadout? _aimReadout;
    private ulong _interactionIncarnation = 1;
    private ulong _publishedRevision = ulong.MaxValue;
    private bool _diagnostics;
    private double _diagnosticElapsed;
    public string Diagnostics(bool enabled) { _diagnostics = enabled; PublishHud(); return DiagnosticReadout; }
    internal string DiagnosticReadout => $"diagnostics={_diagnostics};hudPublications={_hudSequence};triggerPasses={_gameplay.TriggerPasses};kills={_gameplay.Kills};collected={_gameplay.Collected};{_gameplay.Describe()}";
    private ProductUpdateFacts _facts;
    private bool _active;
    private bool _disposed;
    private readonly LoadingBayTuning _tuning = LoadingBayTuning.E1M1 with
    {
        ContentIdentity = "doom-room-study",
        MaximumHealth = 100,
        MaximumArmor = 100,
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
                    surface.Sampling.CreaseDegrees, surface.Sampling.TextureRepeats, surface.Sampling.TextureMapping, surface.Material, surface.Regions,
                    surface.Sampling.MaterialBoundaries, surface.Sampling.MaterialSampleSpacing, surface.Sampling.MaxExtractionVertices, surface.Sampling.MaxExtractionTriangles));
                _studyAudit?.Capture(surface, mesh);
                for (int i = 0; i < _doors.Length; i++)
                    if (surface.Name == _doors[i].Definition.SurfaceName) _doorIndices[i] = _meshes.Count;
                _meshes.Add(mesh);
                _placements.Add(surface.Placement);
                _appearances.Add(engine.Graphics.CreateMeshAppearance(mesh));
            }
            void Join(RecipeJoin join) => _studyAudit?.Register(join);
            Material brownWall = Material("wall/BROWN1.png"), southernFloor = Material("flat/FLOOR5_2.png");
            LoadingBayRoomRecipe.Compose(engine.ImplicitSurfaces, wall, floor, carpet, trim, ceiling, door, brownWall, Emit, Join);
            Material liquid = Material("flat/NUKAGE3.png");
            LoadingBayEastWingRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor,
                liquid, trim, ceiling, door, Emit, Join);
            LoadingBayEastGalleryRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor, ceiling, Emit, Join);
            LoadingBayCourtyardRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor, liquid, trim, Emit);
            LoadingBayTerminalRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor, trim, ceiling, door, Emit);
            LoadingBaySouthPassageRecipe.Compose(engine.ImplicitSurfaces, brownWall, southernFloor, trim, ceiling, door, Emit);
            if (_doorIndices.Any(i => i < 0)) throw new InvalidOperationException("Study door surface is missing.");
            _player = new LoadingBayPlayerScene(engine, _tuning);
            _spawn = _player.Capture();
            var assets = _meshes.Select((mesh, i) => new StaticMeshAsset((ulong)i + 10000, new MeshResourceReference(mesh), 0, 0, 0, 0)).ToArray();
            var instances = _meshes.Select((_, i) => i).Where(i => !_doorIndices.Contains(i)).Select(i => new StaticMeshInstance((ulong)i + 10000, (ulong)i + 10000, _placements[i])).ToArray();
            _player.PublishRoomMeshes(assets, instances);
            _gameplay = new(engine, _player, _doors);
            _worldInteraction = new WorldInteraction(this);
            _interactionDebug = new InteractionDebugModule(_worldInteraction);
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
        }, tick => _gameplay.Advance(deltaSeconds, tick), !_gameplay.Dead && !_gameplay.Complete,
            adjustGamepadLook: ApplyGamepadAimAssistance);
        _ = _worldInteraction.Update();
        foreach (var key in update.Input)
            if (key.Kind == InputEventKind.Key && key.Edge == InputEdge.Pressed)
            { if (key.Keyboard == KeyboardControl.Digit1) _gameplay.SelectWeapon(RecipeWeapon.Fist); if (key.Keyboard == KeyboardControl.Digit2) _gameplay.SelectWeapon(RecipeWeapon.Pistol); if (key.Keyboard == KeyboardControl.Digit3) _gameplay.SelectWeapon(RecipeWeapon.Shotgun); }
        if (input.FireRequested) _gameplay.Fire(CorrectShotDirection);
        if (input.UseRequested && !_gameplay.Dead)
            _ = _worldInteraction.UseFocused();
        if (moved)
        {
            for (int i = 0; i < _doors.Length; i++)
                _placements[_doorIndices[i]] = _placements[_doorIndices[i]] with { Translation = _doors[i].Placement.Translation };
        }
        if (moved || _gameplay.GeometryDirty) PublishGeometry();
        _diagnosticElapsed += deltaSeconds * update.Facts.AdmittedStepCount;
        if (_publishedRevision != _gameplay.Revision || (_diagnostics && _diagnosticElapsed >= LoadingBayRecipeGameplay.DiagnosticsInterval)) PublishHud();
        return ProductUpdateResult.None;
    }

    public void ActivateSharedRealizations()
    {
        if (!_active) _player.ActivateCamera();
        _active = true;
    }
    public void DeactivateSharedRealizations() => _active = false;
    internal void Restart()
    {
        // Fixture restart rebuilds motion from pose; only the canonical path saves pose.
        _player.Restore(_spawn.Position, _spawn.Look, _tuning);
        for (int i = 0; i < _doors.Length; i++)
        {
            _doors[i].Reset();
            _doorObstacles[i] = _doors[i].Obstacle;
            _placements[_doorIndices[i]] = _placements[_doorIndices[i]] with { Translation = Vector3.Zero };
        }
        _gameplay.Restart();
        _interactionIncarnation = checked(_interactionIncarnation + 1);
        Publish();
    }
    public void Attach() => Publish();
    public void Publish()
    {
        if (!_active) return;
        PublishGeometry();
        PublishHud();
    }
    private void PublishHud()
    {
        LoadingBayUiValueBuilder value = new();
        List<(string, uint)> fields = [];
        foreach (string key in new[] { "droppedFacts", "pendingSchedules",
            "exitVisibilityRevision", "presentationBillboards", "effectsVolume", "admittedSteps", "droppedSteps", "materialMappingCount" })
            fields.Add((key, value.Number(0)));
        fields.Add(("health", value.Number(_gameplay.Health)));
        fields.Add(("armor", value.Number(_gameplay.Armor)));
        fields.Add(("bullets", value.Number(_gameplay.Bullets)));
        fields.Add(("shells", value.Number(_gameplay.Shells)));
        fields.Add(("generation", value.Number(_facts.Generation)));
        fields.Add(("step", value.Number(_diagnostics ? _facts.SimulationStep : 0)));
        fields.Add(("weapon", value.String(_gameplay.Weapon)));
        fields.Add(("kills", value.Number(_gameplay.Kills)));
        fields.Add(("totalEnemies", value.Number(_gameplay.Enemies.Length)));
        fields.Add(("collected", value.Number(_gameplay.Collected)));
        fields.Add(("totalPickups", value.Number(_gameplay.Pickups.Length)));
        fields.Add(("dead", value.Bool(_gameplay.Dead)));
        fields.Add(("complete", value.Bool(_gameplay.Complete)));
        fields.Add(("message", value.String(_gameplay.Message)));
        fields.Add(("weaponFlash", value.Bool(_gameplay.WeaponFlash)));
        fields.Add(("damageFlash", value.Bool(_gameplay.DamageFlash)));
        fields.Add(("materialCount", value.Number(_materials.Count)));
        foreach (string key in new[] { "exitVisibility", "effectsMuted", "voxelPresentationRealized" })
            fields.Add((key, value.Bool(false)));
        foreach (string key in new[] { "animationCue", "catalogHash" }) fields.Add((key, value.String("")));
        fields.Add(("skyResourceRealized", value.Bool(_sky.ResourceRealized)));
        fields.Add(("skyBackgroundSelected", value.Bool(_sky.BackgroundSelected)));
        fields.Add(("skyPath", value.String(_sky.SourcePath)));
        fields.Add(("skyHash", value.String(_sky.SourceHash.ToString())));
        fields.Add(("content", value.String("doom-room-study")));
        fields.Add(("updateMode", value.String("Recipe gameplay")));
        fields.Add(("lifecycle", value.String("Ready")));
        fields.Add(("facts", value.Array([])));
        _engine.Ui.PublishProjection(new UiProjection(_hud, ++_hudSequence, value.Build(value.Object(fields.ToArray()))));
        _publishedRevision = _gameplay.Revision;
        _diagnosticElapsed = 0;
    }
    private void PublishGeometry()
    {
        _engine.Graphics.PublishSnapshot(_appearances.Select((appearance, i) => new AppearanceFact(
        (ulong)i + 10000, false, 0, _placements[i], appearance, true, RenderLayer.Scene)).Concat(_gameplay.Appearances()).ToArray());
        _gameplay.GeometryDirty = false;
    }

    public LoadingBayEngineServiceReadout EngineReadout() => LoadingBayEngineServiceReadout.Empty with { Sky = _sky };
    public LoadingBayReadout Readout() => new(new EntityId(1), _facts, _gameplay.Health, _gameplay.Armor, LoadingBayArmorProtection.None,
        (ulong)_gameplay.Bullets, (ulong)_gameplay.Shells, [], null, [], _player.Capture(), [], [], _gameplay.Complete, 0, _tuning, [], 0);
    public LoadingBayReceipt DeveloperSetTrack(ulong generation, string track, int value, string correlation)
        => new(_gameplay.SetTrack(track, value), "recipe.track", correlation);

    public IDebugCommandModule InteractionDebugModule => _interactionDebug;

    public InteractionSceneSnapshot ReadInteraction()
    {
        Vector3 player = _player.Position;
        Vector3 eye = player + (Vector3.UnitY * _tuning.EyeOffsetFromCenter);
        InteractionQuery query = new(eye, _player.Forward,
            InteractionAcquireAngleRadians, InteractionReleaseAngleRadians,
            InteractionMaximumDistance, InteractionMaximumDistance,
            DistanceOrigin: player);
        SpatialEntityCollider[] colliders = _gameplay.CombatColliders();
        List<InteractionCandidate> candidates = [];
        foreach (LoadingBayStudyDoor door in _doors)
        {
            Vector3 minimum = door.Definition.Min + door.Placement.Translation;
            Vector3 maximum = door.Definition.Max + door.Placement.Translation;
            Vector3 point = Vector3.Clamp(player, minimum, maximum);
            InteractionVisibility visibility = InteractionVisibilityQuery.Cast(_engine.Spatial, _player.Session, eye, point,
                new SpatialQueryFilter(1, uint.MaxValue), colliders, new[] { door.Definition.Entity });
            candidates.Add(new InteractionCandidate(new InteractionTarget(door.Definition.Entity, InteractionRevision), door.Definition.SurfaceName,
                point, LoadingBayStudyDoor.UseDistance, visibility,
                door.Opening ? InteractionAvailability.Unavailable : InteractionAvailability.Available));
        }
        Vector3 exitPoint = LoadingBayRecipeGameplay.ExitPosition + Vector3.UnitY;
        InteractionVisibility exitVisibility = InteractionVisibilityQuery.Cast(_engine.Spatial, _player.Session, eye, exitPoint,
            new SpatialQueryFilter(1, uint.MaxValue), colliders, ReadOnlyMemory<ulong>.Empty);
        candidates.Add(new InteractionCandidate(new InteractionTarget(ExitInteractionEntity, InteractionRevision), "hangar-exit", exitPoint,
            LoadingBayStudyDoor.UseDistance, exitVisibility,
            _gameplay.Complete ? InteractionAvailability.Unavailable : InteractionAvailability.Available));
        string stamp = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"generation:{_facts.Generation};step:{_facts.SimulationStep};gameplay:{_gameplay.Revision}");
        return new(query, candidates.ToArray(), stamp, "use");
    }

    public InteractionActionResult UseInteraction(InteractionTarget target)
    {
        LoadingBayStudyDoor? door = _doors.SingleOrDefault(value => value.Definition.Entity == target.Id);
        if (door is not null)
        {
            bool opened = door.Use(_player.Position);
            if (opened) _gameplay.Use();
            return new(opened, opened ? "Door opening." : "Door is unavailable.");
        }
        if (target.Id != ExitInteractionEntity) return new(false, "Unknown interaction target.");
        bool complete = _gameplay.Complete;
        _gameplay.Use();
        return new(!complete && _gameplay.Complete,
            _gameplay.Complete ? "Hangar exit completed." : "Move closer to the hangar exit.");
    }

    private Vector2 ApplyGamepadAimAssistance(Vector2 lookDelta, float simulationSeconds, bool gamepadActive)
    {
        AimAssistConfig config = lookDelta.LengthSquared() > 0f
            ? LoadingBayTuning.GamepadAimAssist
            : LoadingBayTuning.GamepadAimAssist with { TrackingRadiansPerSecond = 0f };
        _aimReadout = _gamepadAim.Update(AimCandidates(), AimQuery(), lookDelta, simulationSeconds, config, gamepadActive);
        return _aimReadout.LookDeltaRadians;
    }

    private Vector3 CorrectShotDirection(Vector3 direction)
    {
        bool active = _aimReadout?.Active ?? false;
        return _gamepadAim.CorrectShot(direction, AimCandidates(), AimQuery(), LoadingBayTuning.GamepadAimAssist, active).Direction;
    }

    private InteractionQuery AimQuery()
    {
        Vector3 player = _player.Position;
        return new(player + (Vector3.UnitY * _tuning.EyeOffsetFromCenter), _player.Forward,
            LoadingBayTuning.GamepadAimAssist.SlowdownAngleRadians,
            LoadingBayTuning.GamepadAimAssist.SlowdownAngleRadians * 1.5f,
            _gameplay.CurrentWeaponRange, _gameplay.CurrentWeaponRange,
            DistanceOrigin: player);
    }

    private InteractionCandidate[] AimCandidates()
    {
        Vector3 eye = _player.Position + (Vector3.UnitY * _tuning.EyeOffsetFromCenter);
        SpatialEntityCollider[] colliders = _gameplay.CombatColliders();
        return _gameplay.Enemies.Where(enemy => enemy.Health > 0).Select(enemy =>
        {
            Vector3 point = enemy.Position + new Vector3(0f, .85f, 0f);
            InteractionVisibility visibility = InteractionVisibilityQuery.Cast(_engine.Spatial, _player.Session, eye, point,
                new SpatialQueryFilter(1, uint.MaxValue), colliders, new[] { 1UL, enemy.Id });
            return new InteractionCandidate(new InteractionTarget(enemy.Id, InteractionRevision), $"enemy-{enemy.Id}", point,
                _gameplay.CurrentWeaponRange, visibility, InteractionAvailability.Available);
        }).ToArray();
    }

    private ulong InteractionRevision => checked(_facts.Generation + _interactionIncarnation);

    public DebugCommandResult ReadSpatialMap(string format, int radius, double cellSize)
    {
        Vector3 playerPosition = _player.Position;
        double supportY = playerPosition.Y - (_tuning.StandingCharacterHeight * .5d);
        return CaptureSpatialMap(format, playerPosition.X, playerPosition.Z, supportY, radius, cellSize);
    }

    public DebugCommandResult ReadSpatialMapAt(string format, double centerX, double centerZ, double supportY, int radius, double cellSize)
        => CaptureSpatialMap(format, centerX, centerZ, supportY, radius, cellSize);

    public DebugCommandResult ReadCombatObservation()
    {
        try
        {
            Vector3 playerPosition = _player.Position;
            Vector3 eye = playerPosition + (Vector3.UnitY * _tuning.EyeOffsetFromCenter);
            RecipeEnemy[] enemies = _gameplay.Enemies
                .Where(enemy => enemy.Health > 0 && Vector3.Distance(playerPosition, enemy.Position) <= CombatObservationRange)
                .OrderBy(enemy => Vector3.DistanceSquared(playerPosition, enemy.Position))
                .ToArray();
            Dictionary<ulong, bool> lineOfSight = QueryEnemyLineOfSight(enemies);
            LoadingBayPlayerSnapshot player = _player.Capture();
            Vector3 forward = _player.Forward;
            RecipeAimHit rawAimHit = _gameplay.ObserveAimHit(forward);
            AimShotReadout shot = _gamepadAim.CorrectShot(forward, AimCandidates(), AimQuery(),
                LoadingBayTuning.GamepadAimAssist, _aimReadout?.Active ?? false);
            RecipeAimHit assistedAimHit = _gameplay.ObserveAimHit(shot.Direction);
            WorldInteractionReadout interaction = _worldInteraction.Inspect();
            object? aimTarget = _aimReadout?.Focus.Selected is {} selectedAim
                ? new { id = selectedAim.Id, revision = selectedAim.Revision }
                : null;
            object? shotTarget = shot.Target is {} selectedShot
                ? new { id = selectedShot.Id, revision = selectedShot.Revision }
                : null;
            Vector3 planarForward = Vector3.Normalize(new Vector3(forward.X, 0f, forward.Z));
            Vector3 right = new(-planarForward.Z, 0f, planarForward.X);

            var observation = new
            {
                stamp = new { generation = _facts.Generation, step = _facts.SimulationStep },
                controls = new
                {
                    move = "WASD",
                    yaw = new { left = "J", right = "L", degreesPerSecond = LoadingBayTuning.KeyboardLookDegreesPerSecond, positive = "right" },
                    pitch = new { up = "I", down = "K", degreesPerSecond = LoadingBayTuning.KeyboardLookDegreesPerSecond, positive = "up" },
                    precisionLook = new { key = "ShiftLeft", multiplier = LoadingBayTuning.KeyboardPrecisionLookMultiplier, degreesPerSecond = LoadingBayTuning.KeyboardLookDegreesPerSecond * LoadingBayTuning.KeyboardPrecisionLookMultiplier },
                    fire = "ControlLeft",
                    useKey = "E",
                    gamepad = new { look = "right-stick", fire = "RT", use = "X", aimAssist = "last gamepad input; neutral stick retains focus without tracking" },
                    bearingDegrees = "positive right",
                    aimPitchErrorDegrees = "positive means aim up"
                },
                player = new
                {
                    position = VectorValue(playerPosition),
                    yawDegrees = player.Look.YawRadians * RadiansToDegrees,
                    pitchDegrees = player.Look.PitchRadians * RadiansToDegrees,
                    health = _gameplay.Health,
                    dead = _gameplay.Dead,
                    ammo = new { bullets = _gameplay.Bullets, shells = _gameplay.Shells },
                    weapon = _gameplay.Weapon,
                    weaponReady = _gameplay.WeaponReady,
                    aimHit = new { present = rawAimHit.Present, kind = rawAimHit.Kind, entity = rawAimHit.Entity, distance = rawAimHit.Distance, range = rawAimHit.Range },
                    aimAssist = new
                    {
                        active = _aimReadout?.Active ?? false,
                        target = aimTarget,
                        slowdownScale = _aimReadout?.SlowdownScale ?? 1f,
                        appliedLookScale = Vector2Value(_aimReadout?.AppliedLookScale ?? Vector2.One),
                        correctionRadians = Vector2Value(_aimReadout?.CorrectionRadians ?? Vector2.Zero),
                        shot = new
                        {
                            assisted = shot.Assisted,
                            target = shotTarget,
                            correctionRadians = shot.CorrectionRadians,
                            rawDirection = VectorValue(forward),
                            assistedDirection = VectorValue(shot.Direction),
                            rawHit = new { present = rawAimHit.Present, kind = rawAimHit.Kind, entity = rawAimHit.Entity, distance = rawAimHit.Distance },
                            assistedHit = new { present = assistedAimHit.Present, kind = assistedAimHit.Kind, entity = assistedAimHit.Entity, distance = assistedAimHit.Distance }
                        }
                    },
                    kills = _gameplay.Kills
                },
                enemies = enemies.Select(enemy => EnemyObservation(enemy, eye, planarForward, right, lineOfSight[enemy.Id])).ToArray(),
                doors = _doors.Select(door => new
                {
                    id = door.Definition.Entity,
                    state = DoorState(door),
                    raised = door.Height
                }).ToArray(),
                interaction = new
                {
                    selected = interaction.Focus.Selected is {} selectedInteraction
                        ? new { id = selectedInteraction.Id, revision = selectedInteraction.Revision }
                        : null,
                    reason = interaction.Focus.Reason.ToString(),
                    stamp = interaction.Scene.Stamp,
                    targetedUseEnabled = interaction.TargetedUseEnabled
                }
            };
            return DebugCommandResult.Success(JsonSerializer.Serialize(observation, CombatObservationJson));
        }
        catch (InvalidOperationException error)
        {
            return DebugCommandResult.Failure(DebugCommandStatus.Failed, $"Combat observation failed: {error.Message}");
        }
    }

    private DebugCommandResult CaptureSpatialMap(string format, double centerX, double centerZ, double supportY, int radius, double cellSize)
    {
        if (!TryMapFormat(format, out bool json))
            return InvalidSpatialMapArguments("Format must be ascii or json.");
        if (radius < 0 || radius > SpatialMapMaximumRadius)
            return InvalidSpatialMapArguments($"Radius must be between 0 and {SpatialMapMaximumRadius} cells.");
        if (!double.IsFinite(cellSize) || cellSize <= 0d)
            return InvalidSpatialMapArguments("Cell size must be finite and greater than zero.");
        if (!double.IsFinite(centerX) || !double.IsFinite(centerZ) || !double.IsFinite(supportY))
            return InvalidSpatialMapArguments("Center and supportY must be finite.");

        double originX = centerX - ((radius + SpatialMapHalfCell) * cellSize);
        double originZ = centerZ - ((radius + SpatialMapHalfCell) * cellSize);
        double collisionMinimumY = supportY + SpatialMapFloorClearance;
        double collisionMaximumY = supportY + _tuning.StandingCharacterHeight - SpatialMapCeilingClearance;
        double navigationMinimumY = supportY - SpatialMapNavigationBelowSupport;
        double navigationMaximumY = supportY + _tuning.MaximumStepHeight;
        if (!FitsSinglePrecision(originX) || !FitsSinglePrecision(originZ) || !FitsSinglePrecision(supportY)
            || !double.IsFinite(collisionMinimumY) || !double.IsFinite(collisionMaximumY)
            || !double.IsFinite(navigationMinimumY) || !double.IsFinite(navigationMaximumY))
            return InvalidSpatialMapArguments("Map geometry is outside the supported finite world range.");

        uint dimension = checked((uint)(radius * 2 + 1));
        try
        {
            SpatialMapRequest request = new(
                _player.Session,
                new Vector3((float)originX, 0f, (float)originZ),
                cellSize,
                dimension,
                dimension,
                collisionMinimumY,
                collisionMaximumY,
                navigationMinimumY,
                navigationMaximumY,
                SpatialMapDoorColliders());
            SpatialMapSnapshot snapshot = SpatialMapSnapshot.Capture(
                _engine.Spatial,
                request,
                new SpatialMapObservation(
                    $"generation={_facts.Generation};step={_facts.SimulationStep}",
                    _player.Position,
                    _player.Forward),
                SpatialMapAnnotations(),
                SpatialMapMaximumAnnotations);
            return DebugCommandResult.Success(json ? snapshot.ToJson() : snapshot.ToAscii());
        }
        catch (ArgumentException error)
        {
            return InvalidSpatialMapArguments(error.Message);
        }
        catch (OverflowException error)
        {
            return InvalidSpatialMapArguments(error.Message);
        }
        catch (InvalidOperationException error)
        {
            return DebugCommandResult.Failure(DebugCommandStatus.Failed, $"Spatial map capture failed: {error.Message}");
        }
    }

    private SpatialEntityCollider[] SpatialMapDoorColliders()
    {
        SpatialEntityCollider[] colliders = new SpatialEntityCollider[_doors.Length];
        for (int index = 0; index < _doors.Length; index++)
        {
            CharacterObstacle obstacle = _doors[index].Obstacle;
            Vector3 translation = obstacle.Transform.Translation;
            colliders[index] = new SpatialEntityCollider(
                obstacle.Entity,
                obstacle.BoundsMin + translation,
                obstacle.BoundsMax + translation,
                SpatialMapDoorCollisionGroup,
                SpatialMapDoorCollisionMask,
                obstacle.CollisionEnabled,
                true,
                false);
        }
        return colliders;
    }

    private Dictionary<ulong, bool> QueryEnemyLineOfSight(RecipeEnemy[] enemies)
    {
        if (enemies.Length == 0) return [];
        Vector3 eye = _player.Position + (Vector3.UnitY * _tuning.EyeOffsetFromCenter);
        SpatialEntityCollider[] doors = SpatialMapDoorColliders();
        Dictionary<ulong, bool> result = new(enemies.Length);
        foreach (RecipeEnemy enemy in enemies)
        {
            Vector3 target = enemy.Position + (Vector3.UnitY * .875f);
            SpatialHit hit = _engine.Spatial.CastSegment(new SpatialSegmentCastRequest(
                _player.Session,
                eye,
                target,
                new SpatialQueryFilter(1, uint.MaxValue),
                doors,
                ReadOnlyMemory<ulong>.Empty,
                doors));
            result.Add(enemy.Id, !hit.Present);
        }
        return result;
    }

    private object EnemyObservation(RecipeEnemy enemy, Vector3 eye, Vector3 planarForward, Vector3 right, bool lineOfSight)
    {
        Vector3 target = enemy.Position + (Vector3.UnitY * .875f);
        Vector3 offset = target - eye;
        float horizontalDistance = MathF.Sqrt((offset.X * offset.X) + (offset.Z * offset.Z));
        float distance = offset.Length();
        float bearingDegrees = MathF.Atan2(Vector3.Dot(offset, right), Vector3.Dot(offset, planarForward)) * RadiansToDegrees;
        float targetPitch = MathF.Atan2(offset.Y, horizontalDistance);
        float currentPitch = MathF.Asin(Math.Clamp(_player.Forward.Y, -1f, 1f));
        return new
        {
            id = enemy.Id,
            kind = enemy.Imp ? "imp" : "trooper",
            position = VectorValue(enemy.Position),
            health = enemy.Health,
            awake = enemy.Awake,
            distance,
            bearingDegrees,
            aimPitchErrorDegrees = (targetPitch - currentPitch) * RadiansToDegrees,
            lineOfSight
        };
    }

    private static string DoorState(LoadingBayStudyDoor door)
        => !door.Opening ? "closed" : door.Height >= LoadingBayStudyDoor.Travel ? "open" : "opening";

    private static object VectorValue(Vector3 value) => new { x = value.X, y = value.Y, z = value.Z };

    private static object Vector2Value(Vector2 value) => new { x = value.X, y = value.Y };

    private SpatialMapAnnotation[] SpatialMapAnnotations()
    {
        List<SpatialMapAnnotation> annotations = new(_doors.Length + _gameplay.Enemies.Length + _gameplay.Pickups.Length + 1);
        foreach (LoadingBayStudyDoor door in _doors)
        {
            CharacterObstacle obstacle = door.Obstacle;
            // Keep the doorway annotation at the passage even when its slab rises out of the slice.
            Vector3 position = (obstacle.BoundsMin + obstacle.BoundsMax) * .5f;
            string state = !door.Opening ? "closed" : door.Height >= LoadingBayStudyDoor.Travel ? "open" : "opening";
            state = FormattableString.Invariant($"{state};raised={door.Height:G9}");
            annotations.Add(new($"door:{door.Definition.Entity}", door.Definition.SurfaceName + " doorway", "door", state, position));
        }
        foreach (RecipeEnemy actor in _gameplay.Enemies)
        {
            string label = actor.Imp ? "imp" : "trooper";
            string state = $"health={actor.Health};awake={actor.Awake.ToString().ToLowerInvariant()}";
            annotations.Add(new($"actor:{actor.Id}", label, "hostile", state, actor.Position));
        }
        foreach (RecipePickup pickup in _gameplay.Pickups)
        {
            if (_gameplay.IsCollected(pickup.Id)) continue;
            annotations.Add(new($"pickup:{pickup.Id}", pickup.Kind.ToString(), "pickup", "available", pickup.Position));
        }
        annotations.Add(new("exit:terminal", "southern terminal", "exit", _gameplay.Complete ? "complete" : "available", LoadingBayRecipeGameplay.ExitPosition));
        return annotations.ToArray();
    }

    private static bool TryMapFormat(string format, out bool json)
    {
        json = false;
        if (string.Equals(format, "ascii", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)) { json = true; return true; }
        return false;
    }

    private static bool FitsSinglePrecision(double value)
        => double.IsFinite(value) && value >= float.MinValue && value <= float.MaxValue;

    private static DebugCommandResult InvalidSpatialMapArguments(string message)
        => DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, message);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_active) { _engine.Graphics.PublishSnapshot([]); _active = false; }
        List<Exception> errors = [];
        void Release(IDisposable? resource) { try { resource?.Dispose(); } catch (Exception error) { errors.Add(error); } }
        Release(_studyAudit);
        _studyAudit = null;
        Release(_hud);
        Release(_gameplay);
        Release(_player);
        foreach (Appearance appearance in _appearances) Release(appearance);
        foreach (MeshResource mesh in _meshes) Release(mesh);
        foreach (Material material in _materials) Release(material);
        foreach (RenderResource texture in _textures) Release(texture);
        if (errors.Count > 0) throw new AggregateException(errors);
    }
}
