using Rusty.Engine;
using Rusty.Engine.Application;
using Rusty.Engine.Entities;
using Rusty.Engine.Persistence;
using Mechanics = Rusty.Engine.Mechanics;

namespace LoadingBay.Game;

/// <summary>Authoritative admitted-step product state plus narrowly owned generated Engine service adapters.</summary>
internal sealed class LoadingBaySession : ILoadingBaySession, ILoadingBayDebugSession
{
    private const double HudDiagnosticsCadenceSeconds = .25d;
    private readonly LoadingBayTuning _tuning;
    private readonly EntityStore _entities = new([
        EngineComponentTypes.Transform,
        EngineComponentTypes.SpatialCollider,
        EngineComponentTypes.Kinematic,
    ]);
    private readonly LoadingBayEntityMap _entityMap;
    private readonly Mechanics.InventoryStore _inventory = new();
    private SimulationScheduler _scheduler = new();
    private readonly LoadingBayWorldState _world;
    private ProductStateStore<LoadingBaySnapshot>? _store;
    private LoadingBayEngineServices? _engineServices;
    private IEngineContext? _engineContext;
    private LoadingBayExitPresentation? _exitPresentation;
    private LoadingBayExitButtonAnimation? _exitButtonAnimation;
    private LoadingBaySkyReadout _skyReadout;
    private bool _sharedRealizationsActive;
    private readonly Queue<LoadingBayFact> _journal = new();
    private readonly Queue<Exception> _retiredProjectionFailures = new();
    private ulong _droppedRetiredProjectionFailures;
    private readonly LoadingBayCombat _combat;
    private readonly LoadingBayWorld _worldPolicy;
    private readonly LoadingBayPickups _pickups;
    // Direct fixture-only collection keys are deliberately separate from canonical E1M1 state.
    // Canonical pickups have exactly one authority: the attached pickup component.
    private readonly Mechanics.StatsComponent _playerStats;
    private LoadingBayPlayerSnapshot _playerSnapshot;
    private readonly EntityId _player;
    private ProductUpdateFacts _facts;
    private bool _hasFacts;
    private bool _hudDirty = true;
    private bool _hudDiagnosticsEnabled;
    private double _hudDiagnosticElapsed;
    private bool _disposed;
    private ulong _dropped;
    private Action<EntityStore>? _debugEntityWorldChanged;

    public LoadingBaySession(LoadingBayTuning? tuning = null)
    {
        _tuning = tuning ?? LoadingBayTuning.E1M1;
        _entityMap = LoadingBayEntityMap.Bootstrap(_entities);
        _world = new LoadingBayWorldState(_entities, _entityMap);
        _player = _entityMap.Runtime(LoadingBayEntityMap.PlayerAuthoredId);
        _playerStats = LoadingBayStats.ForPlayer(_tuning);
        _entities.Add(_player, _playerStats);
        _inventory.RegisterInventory(new Mechanics.InventoryState(_player, [new Mechanics.InventoryCapacityLimit(Mechanics.CapacityMetricId.Parse("loading-bay.inventory.slots"), _tuning.InventorySlots)]));
        _inventory.RegisterEquipment(new Mechanics.EquipmentState(_player));
        _combat = new LoadingBayCombat(_inventory, _player, _playerStats, _tuning, _entities, _entityMap, Record, () => _hasFacts ? _facts.SimulationStep : 0);
        _pickups = new LoadingBayPickups(_inventory, _player, _playerStats, _tuning, _entities, _entityMap, _combat, Record, () => _hasFacts ? _facts.SimulationStep : 0);
        _worldPolicy = new LoadingBayWorld(_world, _combat, _playerStats, Record, ScheduleWorldContinuation);
        _playerSnapshot = InitialPlayerSnapshot(_tuning);
        foreach (LoadingBayE1M1PickupPlacement pickup in LoadingBayE1M1SemanticCatalog.Pickups)
        {
            EntityId entity = _entityMap.Runtime(pickup.EntityId);
            _entities.Add(entity, new LoadingBayPickupStateComponent(
                pickup.StartsDormant ? LoadingBayPickupLifecycle.Dormant : LoadingBayPickupLifecycle.Active, "bootstrap", 0, 0));
        }
        foreach (LoadingBayE1M1EnemyDefinition enemy in LoadingBayE1M1SemanticCatalog.Enemies)
        {
            EntityId entity = _entityMap.Runtime(enemy.EntityId);
            _entities.Add(entity, LoadingBayStats.ForEnemy(enemy.MaximumHealth));
            _entities.Add(entity, new LoadingBayEnemyStateComponent(LoadingBayEnemyPosture.Dormant, 0));
        }
        ApplyPlayerSetup(LoadingBayE1M1SemanticCatalog.PlayerSetup("player/e1m1-pistol-start"));
        Record(new SessionStartedFact(_player));
    }

    public LoadingBaySession(
        IEngineContext engine,
        LoadingBayExitPresentation exitPresentation,
        LoadingBayExitButtonAnimation exitButtonAnimation,
        LoadingBaySkyReadout skyReadout)
        : this()
    {
        ProductStateStore<LoadingBaySnapshot>? store = null;
        try
        {
            store = new ProductStateStore<LoadingBaySnapshot>(engine, "loading-bay", new LoadingBaySnapshotCodec());
            _engineContext = engine; _exitPresentation = exitPresentation; _exitButtonAnimation = exitButtonAnimation; _skyReadout = skyReadout;
            _engineServices = new LoadingBayEngineServices(engine, _tuning, _entities, _entityMap, _player, exitPresentation, exitButtonAnimation, skyReadout);
            _combat.MaterializeDrop = _engineServices.MaterializeEnemyDrop;
            _playerSnapshot = _engineServices.CapturePlayer();
            _store = store;
        }
        catch
        {
            _engineServices?.Dispose();
            store?.Dispose();
            throw;
        }
    }

    /// <summary>Focused persistence seam: it composes the public Engine store without creating presentation services.</summary>
    internal LoadingBaySession(IPersistenceService persistence)
        : this()
    {
        _store = new ProductStateStore<LoadingBaySnapshot>(new PersistenceOnlyContext(persistence), "loading-bay", new LoadingBaySnapshotCodec());
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        ThrowIfDisposed();
        _facts = update.Facts;
        _hasFacts = true;
        _scheduler.Advance(update);
        for (uint offset = 0; offset < update.Facts.AdmittedStepCount; offset++)
            _world.Advance(LoadingBayAdmittedStepTicks.At(update.Facts, offset), Record);
        _engineServices?.Update(update, _tuning, Record, _pickups, _worldPolicy, _world, _combat, DamageCanonicalBarrel);
        if (_engineServices is not null) _playerSnapshot = _engineServices.CapturePlayer();
        PublishFromUpdate(update.Facts);
        return ProductUpdateResult.None;
    }

    // Exercise-facing forwards to the combat owner; the session root composes but does not duplicate policy.
    internal LoadingBayReceipt ApplyDamage(string target, int damage, string cause)
    {
        ThrowIfDisposed();
        return _combat.ApplyDamage(target, damage, cause);
    }

    internal LoadingBayReceipt ActivateEncounter(ulong encounterEntityId, ulong tick)
    {
        ThrowIfDisposed();
        return _combat.ActivateEncounter(encounterEntityId, tick);
    }

    internal LoadingBayReceipt ApplyWeaponDamage(ulong enemyEntityId, string weaponId, int damage, ulong tick)
    {
        ThrowIfDisposed();
        return _combat.ApplyWeaponDamage(enemyEntityId, weaponId, damage, tick);
    }

    internal IReadOnlyList<LoadingBayEnemyAttackPlan> PrepareEnemyAttacks(ulong tick, IReadOnlySet<ulong> visibleEnemies, uint visibilityCasts, uint occlusionRejects)
    {
        ThrowIfDisposed();
        return _combat.PrepareEnemyAttacks(tick, visibleEnemies, visibilityCasts, occlusionRejects);
    }

    internal LoadingBayReceipt SettleEnemyAttack(LoadingBayEnemyAttackPlan plan, bool hitPlayer, string cause)
    {
        ThrowIfDisposed();
        return _combat.SettleEnemyAttack(plan, hitPlayer, cause);
    }

    // Exercise-facing forwards to the world owner; Engine-service composition stays here.
    internal LoadingBayReceipt ApplyCanonicalHazard(ulong hazardEntityId, ulong tick)
    {
        ThrowIfDisposed();
        return _worldPolicy.ApplyHazard(hazardEntityId, tick);
    }

    internal LoadingBayReceipt ActivateCanonicalDoor(ulong doorEntityId, ulong tick)
    {
        ThrowIfDisposed();
        return _worldPolicy.ActivateDoor(doorEntityId, tick);
    }

    internal LoadingBayReceipt ActivateCanonicalFloor(ulong floorEntityId, ulong tick)
    {
        ThrowIfDisposed();
        return _worldPolicy.ActivateFloor(floorEntityId, tick);
    }

    internal LoadingBayReceipt ActivateCanonicalLift(ulong liftEntityId, ulong tick)
    {
        ThrowIfDisposed();
        return _worldPolicy.ActivateLift(liftEntityId, tick);
    }

    internal LoadingBayReceipt DiscoverCanonicalSecret(ulong secretEntityId)
    {
        ThrowIfDisposed();
        return _worldPolicy.DiscoverSecretByEntity(secretEntityId);
    }

    internal LoadingBayReceipt CompleteCanonicalExit(ulong exitEntityId)
    {
        ThrowIfDisposed();
        return _worldPolicy.CompleteExitByEntity(exitEntityId);
    }

    internal LoadingBayReceipt DamageCanonicalBarrel(ulong barrelEntityId, int damage, ulong tick)
    {
        ThrowIfDisposed();
        Func<LoadingBayE1M1BarrelDefinition, LoadingBayE1M1BarrelDefinition, bool> occluded = _engineServices is null
            ? static (_, _) => false
            : _engineServices.BarrelOccluded;
        return _worldPolicy.DamageBarrel(barrelEntityId, damage, tick, occluded);
    }

    // Exercise-facing forwards to the combat owner; the session root composes but does not duplicate policy.
    internal LoadingBayWeaponFirePlan? PrepareWeaponFire(ulong tick)
    {
        ThrowIfDisposed();
        return _combat.PrepareWeaponFire(tick);
    }

    internal LoadingBayReceipt SettleWeaponFire(LoadingBayWeaponFirePlan plan, IReadOnlyList<LoadingBayWeaponImpact> impacts)
    {
        ThrowIfDisposed();
        return _combat.SettleWeaponFire(plan, impacts);
    }

    internal LoadingBayReceipt ApplyProjectileDamage(ulong enemyEntityId, int damage, ulong tick)
    {
        ThrowIfDisposed();
        return _combat.ApplyProjectileDamage(enemyEntityId, damage, tick);
    }

    internal void RecordProjectileOutcome(ulong enemyEntityId, ulong tick, string cause)
    {
        ThrowIfDisposed();
        _combat.RecordProjectileOutcome(enemyEntityId, tick, cause);
    }

    // Exercise-facing forwards to the pickup owner; the session root composes but does not duplicate policy.
    internal LoadingBayReceipt CollectPickup(string pickup, LoadingBayItem item, ulong quantity)
    {
        ThrowIfDisposed();
        return _pickups.CollectPickup(pickup, item, quantity);
    }

    internal LoadingBayReceipt CollectCanonicalPickup(ulong entityId)
    {
        ThrowIfDisposed();
        return _pickups.CollectCanonicalPickup(entityId);
    }

    internal bool CanCollectCanonicalPickup(ulong entityId)
    {
        ThrowIfDisposed();
        return _pickups.CanCollectCanonicalPickup(entityId);
    }

    public void Publish()
    {
        ThrowIfDisposed();
        PublishHud(force: true);
    }

    /// <summary>Enables the bounded developer-only HUD diagnostic cadence for this live session.</summary>
    public string Diagnostics(bool enabled)
    {
        ThrowIfDisposed();
        _hudDiagnosticsEnabled = enabled;
        _hudDiagnosticElapsed = 0d;
        PublishHud(force: true);
        return $"diagnostics={enabled};cadenceSeconds={HudDiagnosticsCadenceSeconds:R}";
    }

    public void Attach()
    {
        ThrowIfDisposed();
        if (_engineServices is null)
        {
            Publish();
            return;
        }
        _engineServices.Attach(Readout(), _hudDiagnosticsEnabled);
    }

    public void ActivateSharedRealizations()
    {
        ThrowIfDisposed();
        _engineServices?.ActivateSharedRealizations();
        _sharedRealizationsActive = true;
    }

    public void DeactivateSharedRealizations()
    {
        ThrowIfDisposed();
        _engineServices?.DeactivateSharedRealizations();
        _sharedRealizationsActive = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debugEntityWorldChanged = null;
        _journal.Clear();
        List<Exception>? failures = null;
        try { _engineServices?.Dispose(); }
        catch (Exception failure) { (failures ??= []).Add(failure); }
        try { _store?.Dispose(); }
        catch (Exception failure) { (failures ??= []).Add(failure); }
        try { _entities.Dispose(); }
        catch (Exception failure) { (failures ??= []).Add(failure); }
        foreach (Exception failure in _retiredProjectionFailures) (failures ??= []).Add(failure);
        if (_droppedRetiredProjectionFailures != 0)
            (failures ??= []).Add(new InvalidOperationException($"Loading Bay dropped {_droppedRetiredProjectionFailures} retired Engine-projection disposal failures."));
        if (failures is { Count: > 0 }) throw new AggregateException(failures);
    }

    internal LoadingBayReadout Readout() => new(_player, _hasFacts ? _facts : default, HealthTrack.ValueInt64, ArmorTrack.ValueInt64, _combat.ArmorProtection, _combat.BulletQuantity(), _combat.ShellQuantity(), _combat.OwnedWeaponIds(), _combat.EquippedWeaponId(), _combat.WeaponCooldowns(), _playerSnapshot, _pickups.PickupSnapshots(), _combat.ActorReadouts(), _worldPolicy.Complete, _scheduler.Readout.Pending, _tuning, _journal.ToArray(), _dropped);

    LoadingBayReadout ILoadingBaySession.Readout() => Readout();

    LoadingBayEngineServiceReadout ILoadingBaySession.EngineReadout()
        => _engineServices?.Readout ?? LoadingBayEngineServiceReadout.Empty;

    EntityStore ILoadingBayDebugSession.DebugEntityWorld => _engineServices?.EntityStore ?? _entities;

    void ILoadingBayDebugSession.SetDebugEntityWorldChanged(Action<EntityStore>? callback)
        => _debugEntityWorldChanged = callback;









    /// <summary>Product policy consumes Engine visibility evidence and admits only active, ready actors.</summary>

    /// <summary>Only a completed Engine combat execution advances the corresponding actor's readiness.</summary>





    /// <summary>Called only after Engine's overlap coordinator has established a canonical hazard enter/stay fact.</summary>







    /// <summary>Consumes already-resolved world facts; it deliberately performs no spatial query or presentation work.</summary>


    internal LoadingBayReceipt DeveloperSetTrack(ulong generation, string track, int value, string correlation)
    {
        ThrowIfDisposed();
        ulong currentGeneration = _hasFacts ? _facts.Generation : 0;
        if (generation != currentGeneration) return Reject("developer.stale-generation", correlation);
        Mechanics.Track selected = track switch { "health" => HealthTrack, "armor" => ArmorTrack, _ => throw new ArgumentOutOfRangeException(nameof(track)) };
        try { selected.SetCurrent(value); }
        catch (ArgumentOutOfRangeException) { return Reject("developer.track-rejected", correlation); }
        Record(new DeveloperTrackChangedFact(track, value, correlation)); return Accept("developer.track-set", correlation);
    }

    LoadingBayReceipt ILoadingBaySession.DeveloperSetTrack(ulong generation, string track, int value, string correlation)
        => DeveloperSetTrack(generation, track, value, correlation);

    internal LoadingBaySnapshot Capture(string contentIdentity) => new(contentIdentity, HealthTrack.ValueInt64, ArmorTrack.ValueInt64, _combat.ArmorProtection, _combat.BulletQuantity(), _combat.ShellQuantity(), _combat.OwnedWeaponIds(), _combat.EquippedWeaponId(), _combat.WeaponCooldowns(), _engineServices?.CapturePlayer() ?? _playerSnapshot, _pickups.PickupSnapshots(), _worldPolicy.SecretSnapshots(), _worldPolicy.Complete, _worldPolicy.DoorSnapshots(), _combat.ActorSnapshots(), _combat.EncounterSnapshots(), _world.Capture());

    internal LoadingBayReceipt Restore(LoadingBaySnapshot snapshot, string identity)
    {
        ThrowIfDisposed();
        if (snapshot.ContentIdentity != identity) return Reject("save.content-identity-mismatch");
        if (snapshot.Health < 0 || snapshot.Health > _tuning.MaximumHealth || snapshot.Armor < 0 || snapshot.Armor > _tuning.MaximumArmor || !LoadingBayDefinitions.IsKnownArmorProtection(snapshot.ArmorProtection)) return Reject("save.invalid-track");
        if (snapshot.Bullets > LoadingBayDefinitions.Bullets.MechanicsDefinition.MaximumQuantity || snapshot.Shells > LoadingBayDefinitions.Shells.MechanicsDefinition.MaximumQuantity) return Reject("save.invalid-inventory");
        LoadingBayWorldSnapshot worldBeforeValidation = _world.Capture();
        bool validWorld = _world.TryRestore(snapshot.World);
        _world.TryRestore(worldBeforeValidation);
        if (!ValidPickupSet(snapshot.Pickups) || !ValidPlayer(snapshot.Player) || !ValidCooldowns(snapshot.WeaponCooldowns, snapshot.OwnedWeapons) || !ValidDistinct(snapshot.Secrets) || !ValidState(snapshot.Doors) || !ValidActors(snapshot.Actors) || !ValidWeapons(snapshot.OwnedWeapons, snapshot.EquippedWeapon) || !ValidEncounters(snapshot.Encounters, snapshot.Actors) || !validWorld) return Reject("save.invalid-collection");
        LoadingBaySnapshot previous = Capture(identity);
        IReadOnlyList<CanonicalPickupTriggerStateFact>? triggerFacts = null;
        try { ApplySnapshot(snapshot); }
        catch (Exception applicationFailure)
        {
            List<Exception> failures = [applicationFailure];
            try { ApplySnapshot(previous); }
            catch (Exception rollbackFailure) { failures.Add(rollbackFailure); }
            if (failures.Count > 1) throw new AggregateException(failures);
            if (applicationFailure is Mechanics.MechanicsException) return Reject("save.invalid-equipment");
            throw;
        }
        try
        {
            if (_engineServices is not null)
            {
                // Engine continuations restore only into a fresh compatible Spatial session.
                LoadingBayEngineServices replacement = CreateFreshEngineServices();
                try
                {
                    replacement.RestorePlayer(snapshot.Player);
                    triggerFacts = replacement.RestoreSemanticPickups(snapshot.Pickups);
                    replacement.RestoreEncounterActivations(_combat.ActivatedEncounters);
                    replacement.RestoreWorldMotion(snapshot.World, _hasFacts ? _facts.SimulationStep : 0);
                    if (_sharedRealizationsActive) replacement.ActivateSharedRealizations();
                }
                catch
                {
                    replacement.Dispose();
                    throw;
                }
                LoadingBayEngineServices previousServices = _engineServices;
                _engineServices = replacement;
                _combat.MaterializeDrop = replacement.MaterializeEnemyDrop;
                try
                {
                    _debugEntityWorldChanged?.Invoke(replacement.EntityStore);
                }
                catch (Exception debugWorldFailure)
                {
                    _engineServices = previousServices;
                    _combat.MaterializeDrop = previousServices is null ? null : previousServices.MaterializeEnemyDrop;
                    try { replacement.Dispose(); }
                    catch (Exception replacementCleanupFailure) { throw new AggregateException(debugWorldFailure, replacementCleanupFailure); }
                    throw;
                }
                RetireProjection(previousServices);
            }
        }
        catch
        {
            // Product state and derived Engine motion must return together; no partial save restore is observable.
            ApplySnapshot(previous);
            return Reject("save.world-motion-restore-rejected");
        }
        if (triggerFacts is not null) foreach (CanonicalPickupTriggerStateFact triggerFact in triggerFacts) Record(triggerFact);
        Record(new SnapshotRestoredFact(identity));
        PublishHud(force: true);
        return Accept("save.restored");
    }

    internal LoadingBayReceipt Save(string slot)
    {
        ThrowIfDisposed();
        if (_store is null) return Reject("save.persistence-unavailable");
        _store.Save(slot, Capture(_tuning.ContentIdentity));
        return Accept("save.written");
    }

    internal LoadingBayReceipt Load(string slot)
    {
        ThrowIfDisposed();
        if (_store is null) return Reject("save.persistence-unavailable");
        ProductStateLoad<LoadingBaySnapshot> loaded = _store.Load(slot);
        return !loaded.Present || loaded.State is null ? Reject("save.empty") : Restore(loaded.State, _tuning.ContentIdentity);
    }

    private void Record(LoadingBayFact fact)
    {
        if (fact is PickupLifecycleFact lifecycle) _pickups.ApplyLifecycleFact(lifecycle);
        if (_journal.Count == _tuning.FactJournalCapacity)
        {
            LoadingBayFact evicted = _journal.Dequeue();
            _dropped++;
            if (evicted is not SemanticInputFact) _hudDirty = true;
        }
        _journal.Enqueue(fact);
        // Input intent alone can be reported every frame. Its resulting accepted,
        // rejected, or gameplay fact still marks the read-only HUD dirty.
        if (fact is not SemanticInputFact) _hudDirty = true;
    }
    private void PublishFromUpdate(ProductUpdateFacts update)
    {
        bool diagnosticSample = AdvanceHudDiagnostics(update);
        PublishHud(force: _hudDirty || diagnosticSample);
    }
    private void PublishHud(bool force)
    {
        if (_engineServices is null) return;
        _engineServices.Publish(Readout(), force, _hudDiagnosticsEnabled);
        if (force) _hudDirty = false;
    }
    private bool AdvanceHudDiagnostics(ProductUpdateFacts update)
    {
        if (!_hudDiagnosticsEnabled || update.AdmittedStepCount == 0 ||
            !double.IsFinite(update.FixedDeltaSeconds) || update.FixedDeltaSeconds <= 0d)
            return false;
        _hudDiagnosticElapsed += update.FixedDeltaSeconds * update.AdmittedStepCount;
        if (_hudDiagnosticElapsed < HudDiagnosticsCadenceSeconds) return false;
        _hudDiagnosticElapsed %= HudDiagnosticsCadenceSeconds;
        return true;
    }
    private void ApplyPlayerSetup(LoadingBayE1M1PlayerSetup setup)
    {
        foreach (LoadingBayE1M1ItemGrant grant in setup.Grants)
        {
            if (LoadingBayDefinitions.Weapons.TryGetValue(grant.ItemId, out LoadingBayWeapon? weapon)) _combat.MaterializeWeapon(weapon);
            else _inventory.Grant(_player, LoadingBayDefinitions.Item(grant.ItemId).MechanicsDefinition, grant.Quantity);
        }
        _combat.EquipWeapon(setup.EquippedWeaponId);
    }
    private void ApplySnapshot(LoadingBaySnapshot snapshot)
    {
        _combat.RestoreLoadout(snapshot.OwnedWeapons, snapshot.EquippedWeapon, snapshot.WeaponCooldowns);
        _combat.RestoreVitals(snapshot.Health, snapshot.Armor, snapshot.ArmorProtection);
        _combat.SetBulletQuantity(snapshot.Bullets);
        _combat.SetShellQuantity(snapshot.Shells);
        _pickups.RestorePickups(snapshot.Pickups);
        _playerSnapshot = snapshot.Player;
        _worldPolicy.RestoreSecrets(snapshot.Secrets);
        foreach (LoadingBayActorSnapshot actor in snapshot.Actors)
        {
            EntityId entity = _entityMap.Runtime(actor.EntityId);
            _entities.Get<Mechanics.StatsComponent>(entity).GetTrack(LoadingBayStatIds.Vitality).SetCurrent(actor.Health);
            LoadingBayEnemyStateComponent state = _entities.Get<LoadingBayEnemyStateComponent>(entity);
            state.Posture = actor.Posture;
            state.Visible = actor.Visible;
            state.ReadyAtTick = actor.ReadyAtTick;
        }
        _combat.RestoreEncounters(snapshot.Encounters);
        _worldPolicy.RestoreCompletion(snapshot.Complete);
        if (!_world.TryRestore(snapshot.World)) throw new InvalidOperationException("A prevalidated world snapshot could not be restored.");
        _scheduler = new SimulationScheduler();
        foreach (ulong dueStep in _world.DueSteps()) ScheduleWorldContinuation(dueStep);
    }
    private void ScheduleWorldContinuation(ulong dueStep) => _scheduler.ScheduleAt(dueStep, context => _world.Advance(context.SimulationStep, Record));
    private LoadingBayEngineServices CreateFreshEngineServices()
    {
        if (_engineContext is null || _exitPresentation is null || _exitButtonAnimation is null)
            throw new InvalidOperationException("Loading Bay cannot rebuild an Engine-backed continuation without its product composition inputs.");
        EntityStore projection = new([
            EngineComponentTypes.Transform,
            EngineComponentTypes.SpatialCollider,
            EngineComponentTypes.Kinematic,
        ]);
        try
        {
            LoadingBayEntityMap projectionMap = LoadingBayEntityMap.Bootstrap(projection);
            EntityId projectionPlayer = projectionMap.Runtime(LoadingBayEntityMap.PlayerAuthoredId);
            return new LoadingBayEngineServices(_engineContext, _tuning, projection, projectionMap, projectionPlayer, _exitPresentation, _exitButtonAnimation, _skyReadout, ownsProjectionEntities: true);
        }
        catch
        {
            projection.Dispose();
            throw;
        }
    }
    private void RetireProjection(LoadingBayEngineServices projection)
    {
        try { projection.Dispose(); }
        catch (Exception failure)
        {
            const int MaximumRetirementFailures = 8;
            if (_retiredProjectionFailures.Count == MaximumRetirementFailures) { _retiredProjectionFailures.Dequeue(); _droppedRetiredProjectionFailures++; }
            _retiredProjectionFailures.Enqueue(failure);
            Record(new RejectedFact("save.previous-projection-dispose-failed", null));
        }
    }
    private LoadingBayReceipt Accept(string code, string? correlation = null) => new(true, code, correlation);
    private LoadingBayReceipt Reject(string code, string? correlation = null) { Record(new RejectedFact(code, correlation)); return new(false, code, correlation); }
    /// <summary>
    /// Derived read-only projection of canonical door state for save/diagnostic shape.
    /// Restore validates the shape only; the canonical world snapshot owns the truth.
    /// </summary>
    private Mechanics.Track HealthTrack => _playerStats.GetTrack(LoadingBayStatIds.Health);
    private Mechanics.Track ArmorTrack => _playerStats.GetTrack(LoadingBayStatIds.Armor);
    private static LoadingBayPlayerSnapshot InitialPlayerSnapshot(LoadingBayTuning tuning) => new(
        tuning.InitialPosition,
        new LookState(-(tuning.InitialYawDegrees * (MathF.PI / 180f)), tuning.InitialPitchDegrees * (MathF.PI / 180f)),
        null);
    private static bool ValidDistinct(string[] values) => values.Length <= 256 && values.All(value => !string.IsNullOrWhiteSpace(value)) && values.Distinct(StringComparer.Ordinal).Count() == values.Length;
    private static bool ValidState(LoadingBayNamedState[] values) => values.Length <= 256 && values.All(value => !string.IsNullOrWhiteSpace(value.Id)) && values.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() == values.Length;
    private static bool ValidPickupSet(LoadingBayPickupSnapshot[] pickups)
    {
        if (pickups.Length != LoadingBayE1M1SemanticCatalog.Pickups.Length || pickups.Select(pickup => pickup.EntityId).Distinct().Count() != pickups.Length) return false;
        foreach (LoadingBayPickupSnapshot state in pickups)
        {
            LoadingBayE1M1PickupPlacement placement;
            try { placement = LoadingBayE1M1SemanticCatalog.Pickup(state.EntityId); }
            catch (ArgumentOutOfRangeException) { return false; }
            if (state.ItemId != placement.ItemId || state.ProgramId != placement.ProgramId || string.IsNullOrWhiteSpace(state.Cause)) return false;
            if (placement.StartsDormant
                ? state.Lifecycle is not (LoadingBayPickupLifecycle.Dormant or LoadingBayPickupLifecycle.Active or LoadingBayPickupLifecycle.Collected)
                : state.Lifecycle is not (LoadingBayPickupLifecycle.Active or LoadingBayPickupLifecycle.Collected)) return false;
            if (placement.StartsDormant && state.Lifecycle is not LoadingBayPickupLifecycle.Dormant &&
                !LoadingBayE1M1SemanticCatalog.Enemies.Any(enemy => enemy.DropPickupEntityId == placement.EntityId)) return false;
        }
        return true;
    }
    private static bool ValidCooldowns(LoadingBayWeaponCooldownSnapshot[] cooldowns, string[] weapons) =>
        cooldowns.Length <= LoadingBayDefinitions.Weapons.Count
        && cooldowns.All(cooldown => !string.IsNullOrWhiteSpace(cooldown.WeaponId) && weapons.Contains(cooldown.WeaponId, StringComparer.Ordinal))
        && cooldowns.Select(cooldown => cooldown.WeaponId).Distinct(StringComparer.Ordinal).Count() == cooldowns.Length;
    private static bool ValidPlayer(LoadingBayPlayerSnapshot player)
    {
        if (!Finite(player.Position) || !float.IsFinite(player.Look.YawRadians) || !float.IsFinite(player.Look.PitchRadians)) return false;
        if (player.Continuation is null) return true;
        CharacterMotion motion = player.Continuation.Motion;
        return Finite(motion.ControlledVelocity) && Finite(motion.ExternalVelocity) && Enum.IsDefined(motion.Stance)
            && float.IsFinite(motion.JumpBufferRemaining) && float.IsFinite(motion.CoyoteRemaining) && float.IsFinite(motion.LandingLockoutRemaining)
            && Finite(motion.SupportLocalAnchor) && Finite(motion.SupportPreviousTranslation) && Finite(motion.SupportPointVelocity)
            && float.IsFinite(motion.FallOriginY) && float.IsFinite(motion.PeakY)
            && float.IsFinite(motion.SupportPreviousRotation.X) && float.IsFinite(motion.SupportPreviousRotation.Y) && float.IsFinite(motion.SupportPreviousRotation.Z) && float.IsFinite(motion.SupportPreviousRotation.W);
    }
    private static bool Finite(System.Numerics.Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool ValidWeapons(string[] weapons, string? equipped) =>
        ValidDistinct(weapons)
        && weapons.All(LoadingBayDefinitions.Weapons.ContainsKey)
        && weapons.Contains(LoadingBayDefinitions.Fist.Id, StringComparer.Ordinal)
        && (equipped is null || weapons.Contains(equipped, StringComparer.Ordinal));
    private static bool ValidActors(LoadingBayActorSnapshot[] actors)
    {
        if (actors.Length != LoadingBayE1M1SemanticCatalog.Enemies.Length || actors.Select(actor => actor.EntityId).Distinct().Count() != actors.Length) return false;
        foreach (LoadingBayActorSnapshot actor in actors)
        {
            LoadingBayE1M1EnemyDefinition enemy;
            try { enemy = LoadingBayE1M1SemanticCatalog.Enemy(actor.EntityId); }
            catch (InvalidOperationException) { return false; }
            if (actor.Health < 0 || actor.Health > enemy.MaximumHealth || !Enum.IsDefined(actor.Posture)) return false;
            if (actor.Posture == LoadingBayEnemyPosture.Defeated && actor.Health != 0) return false;
            if (actor.Posture != LoadingBayEnemyPosture.Defeated && actor.Health == 0) return false;
        }
        return true;
    }
    private static bool ValidEncounters(LoadingBayEncounterSnapshot[] encounters, LoadingBayActorSnapshot[] actors)
    {
        if (encounters.Length != LoadingBayE1M1SemanticCatalog.Encounters.Length || encounters.Select(encounter => encounter.EntityId).Distinct().Count() != encounters.Length) return false;
        Dictionary<ulong, LoadingBayActorSnapshot> states = actors.ToDictionary(actor => actor.EntityId);
        foreach (LoadingBayEncounterSnapshot encounter in encounters)
        {
            LoadingBayE1M1EncounterDefinition definition;
            try { definition = LoadingBayE1M1SemanticCatalog.Encounters.Single(value => value.EntityId == encounter.EntityId); }
            catch (InvalidOperationException) { return false; }
            if (!encounter.Activated && (encounter.Cleared || definition.Members.Any(member => states[member].Posture != LoadingBayEnemyPosture.Dormant))) return false;
            if (encounter.Cleared != definition.Members.All(member => states[member].Posture == LoadingBayEnemyPosture.Defeated)) return false;
        }
        return true;
    }
    private sealed class PersistenceOnlyContext(IPersistenceService persistence) : IEngineContext
    {
        public IDiagnosticsService Diagnostics => throw new NotSupportedException();
        public IDynamicsService Dynamics => throw new NotSupportedException();
        public IMotionService Motion => throw new NotSupportedException();
        public IKinematicService Kinematic => throw new NotSupportedException();
        public ISpatialService Spatial => throw new NotSupportedException();
        public IPerceptionService Perception => throw new NotSupportedException();
        public IWorldOriginService WorldOrigin => throw new NotSupportedException();
        public IVoxelService Voxel => throw new NotSupportedException();
        public IVoxelContentService VoxelContent => throw new NotSupportedException();
        public IImplicitSurfacesService ImplicitSurfaces => throw new NotSupportedException();
        public IContentService Content => throw new NotSupportedException();
        public IAuthoredContentService AuthoredContent => throw new NotSupportedException();
        public IGraphicsService Graphics => throw new NotSupportedException();
        public IPresentationService Presentation => throw new NotSupportedException();
        public IAnimationService Animation => throw new NotSupportedException();
        public IAudioService Audio => throw new NotSupportedException();
        public ICameraViewService CameraView => throw new NotSupportedException();
        public IRandomService Random => throw new NotSupportedException();
        public IVoxelScenePresentationService VoxelScenePresentation => throw new NotSupportedException();
        public IPersistenceService Persistence { get; } = persistence;
        public IContentStoreService ContentStore => throw new NotSupportedException();
        public IUiService Ui => throw new NotSupportedException();
    }
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(LoadingBaySession)); }
}
