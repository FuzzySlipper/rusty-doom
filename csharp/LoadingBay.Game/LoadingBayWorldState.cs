using System.Numerics;
using Rusty.Engine.Entities;

namespace LoadingBay.Game;

/// <summary>Typed, saveable E1M1 progression meaning. Engine owns the trigger/query and motion realizations.</summary>
internal enum LoadingBayDoorState { Closed, Opening, Open, Closing }
internal enum LoadingBayFloorState { Armed, Lowering, Lowered }
internal enum LoadingBayLiftState { Raised, Lowering, Waiting, Raising }
internal sealed record LoadingBayDoorSnapshot(ulong EntityId, LoadingBayDoorState State, ulong DueStep);
internal sealed record LoadingBayFloorSnapshot(ulong EntityId, LoadingBayFloorState State, ulong DueStep);
internal sealed record LoadingBayLiftSnapshot(ulong EntityId, LoadingBayLiftState State, ulong DueStep);
internal sealed record LoadingBayBarrelSnapshot(ulong EntityId, int Health, bool Exploded);
internal sealed record LoadingBayHazardSnapshot(ulong EntityId, ulong ReadyAtStep);
internal sealed record LoadingBayWorldSnapshot(LoadingBayDoorSnapshot[] Doors, LoadingBayFloorSnapshot[] Floors, LoadingBayLiftSnapshot[] Lifts, LoadingBayBarrelSnapshot[] Barrels, LoadingBayHazardSnapshot[] Hazards);
internal sealed record WorldInteractionFact(string Family, ulong EntityId, string State, ulong Tick, ulong DueStep) : LoadingBayFact;
internal sealed record BarrelExplosionFact(ulong BarrelEntityId, int Damage, double Radius, ulong Tick, bool Chained) : LoadingBayFact;

/// <summary>
/// Product policy only: state transitions, due steps, and canonical E1M1 values remain explicit and inspectable.
/// Live progression lives in small class components over canonical entities; snapshot records exist only at
/// save/diagnostic boundaries.
/// </summary>
internal sealed class LoadingBayWorldState
{
    private readonly EntityStore _entities;
    private readonly LoadingBayEntityMap _entityMap;

    internal LoadingBayWorldState(EntityStore entities, LoadingBayEntityMap entityMap)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
        foreach (LoadingBayE1M1DoorDefinition door in LoadingBayE1M1SemanticCatalog.Doors)
            _entities.Add(_entityMap.Runtime(door.EntityId), new LoadingBayDoorStateComponent(LoadingBayDoorState.Closed, 0));
        foreach (LoadingBayE1M1FloorDefinition floor in LoadingBayE1M1SemanticCatalog.Floors)
            _entities.Add(_entityMap.Runtime(floor.EntityId), new LoadingBayFloorStateComponent(LoadingBayFloorState.Armed, 0));
        foreach (LoadingBayE1M1LiftDefinition lift in LoadingBayE1M1SemanticCatalog.Lifts)
            _entities.Add(_entityMap.Runtime(lift.EntityId), new LoadingBayLiftStateComponent(LoadingBayLiftState.Raised, 0));
        foreach (LoadingBayE1M1BarrelDefinition barrel in LoadingBayE1M1SemanticCatalog.Barrels)
            _entities.Add(_entityMap.Runtime(barrel.EntityId), new LoadingBayBarrelStateComponent(barrel.MaximumHealth, false));
        foreach (LoadingBayE1M1HazardDefinition hazard in LoadingBayE1M1SemanticCatalog.Hazards)
            _entities.Add(_entityMap.Runtime(hazard.EntityId), new LoadingBayHazardStateComponent(0));
    }

    private LoadingBayDoorStateComponent Door(ulong entityId) => _entities.Get<LoadingBayDoorStateComponent>(_entityMap.Runtime(entityId));
    private LoadingBayFloorStateComponent Floor(ulong entityId) => _entities.Get<LoadingBayFloorStateComponent>(_entityMap.Runtime(entityId));
    private LoadingBayLiftStateComponent Lift(ulong entityId) => _entities.Get<LoadingBayLiftStateComponent>(_entityMap.Runtime(entityId));
    private LoadingBayBarrelStateComponent Barrel(ulong entityId) => _entities.Get<LoadingBayBarrelStateComponent>(_entityMap.Runtime(entityId));
    private LoadingBayHazardStateComponent Hazard(ulong entityId) => _entities.Get<LoadingBayHazardStateComponent>(_entityMap.Runtime(entityId));

    /// <summary>Live single-entity reads for ordinary gameplay; Capture stays at save/diagnostic boundaries.</summary>
    internal LoadingBayDoorSnapshot DoorState(ulong entityId)
    {
        LoadingBayE1M1DoorDefinition _ = LoadingBayE1M1SemanticCatalog.Doors.Single(value => value.EntityId == entityId);
        LoadingBayDoorStateComponent state = Door(entityId);
        return new(entityId, state.State, state.DueStep);
    }

    internal LoadingBayFloorSnapshot FloorState(ulong entityId)
    {
        LoadingBayE1M1FloorDefinition _ = LoadingBayE1M1SemanticCatalog.Floors.Single(value => value.EntityId == entityId);
        LoadingBayFloorStateComponent state = Floor(entityId);
        return new(entityId, state.State, state.DueStep);
    }

    internal LoadingBayLiftSnapshot LiftState(ulong entityId)
    {
        LoadingBayE1M1LiftDefinition _ = LoadingBayE1M1SemanticCatalog.Lifts.Single(value => value.EntityId == entityId);
        LoadingBayLiftStateComponent state = Lift(entityId);
        return new(entityId, state.State, state.DueStep);
    }

    internal LoadingBayWorldSnapshot Capture() => new(
        LoadingBayE1M1SemanticCatalog.Doors.OrderBy(value => value.EntityId)
            .Select(value => new LoadingBayDoorSnapshot(value.EntityId, Door(value.EntityId).State, Door(value.EntityId).DueStep)).ToArray(),
        LoadingBayE1M1SemanticCatalog.Floors.OrderBy(value => value.EntityId)
            .Select(value => new LoadingBayFloorSnapshot(value.EntityId, Floor(value.EntityId).State, Floor(value.EntityId).DueStep)).ToArray(),
        LoadingBayE1M1SemanticCatalog.Lifts.OrderBy(value => value.EntityId)
            .Select(value => new LoadingBayLiftSnapshot(value.EntityId, Lift(value.EntityId).State, Lift(value.EntityId).DueStep)).ToArray(),
        LoadingBayE1M1SemanticCatalog.Barrels.OrderBy(value => value.EntityId)
            .Select(value => new LoadingBayBarrelSnapshot(value.EntityId, Barrel(value.EntityId).Health, Barrel(value.EntityId).Exploded)).ToArray(),
        LoadingBayE1M1SemanticCatalog.Hazards.OrderBy(value => value.EntityId)
            .Select(value => new LoadingBayHazardSnapshot(value.EntityId, Hazard(value.EntityId).ReadyAtStep)).ToArray());

    /// <summary>Validates a world DTO without touching live components.</summary>
    internal bool Validate(LoadingBayWorldSnapshot snapshot)
    {
        HashSet<ulong> doors = LoadingBayE1M1SemanticCatalog.Doors.Select(value => value.EntityId).ToHashSet();
        HashSet<ulong> floors = LoadingBayE1M1SemanticCatalog.Floors.Select(value => value.EntityId).ToHashSet();
        HashSet<ulong> lifts = LoadingBayE1M1SemanticCatalog.Lifts.Select(value => value.EntityId).ToHashSet();
        HashSet<ulong> barrels = LoadingBayE1M1SemanticCatalog.Barrels.Select(value => value.EntityId).ToHashSet();
        HashSet<ulong> hazards = LoadingBayE1M1SemanticCatalog.Hazards.Select(value => value.EntityId).ToHashSet();
        if (!Same(snapshot.Doors, doors, value => value.EntityId) || !Same(snapshot.Floors, floors, value => value.EntityId) || !Same(snapshot.Lifts, lifts, value => value.EntityId) || !Same(snapshot.Barrels, barrels, value => value.EntityId) || snapshot.Hazards.Length != hazards.Count || snapshot.Hazards.Select(value => value.EntityId).Distinct().Count() != snapshot.Hazards.Length || snapshot.Hazards.Any(value => !hazards.Contains(value.EntityId))) return false;
        if (snapshot.Doors.Any(value => !Enum.IsDefined(value.State)) || snapshot.Floors.Any(value => !Enum.IsDefined(value.State)) || snapshot.Lifts.Any(value => !Enum.IsDefined(value.State))) return false;
        if (snapshot.Doors.Any(value => value.State == LoadingBayDoorState.Closed ? value.DueStep != 0 : value.DueStep == 0)
            || snapshot.Floors.Any(value => value.State is LoadingBayFloorState.Armed or LoadingBayFloorState.Lowered ? value.DueStep != 0 : value.DueStep == 0)
            || snapshot.Lifts.Any(value => value.State == LoadingBayLiftState.Raised ? value.DueStep != 0 : value.DueStep == 0)) return false;
        if (snapshot.Barrels.Any(value => value.Health < 0 || value.Health > LoadingBayE1M1SemanticCatalog.Barrels.Single(definition => definition.EntityId == value.EntityId).MaximumHealth || (value.Exploded && value.Health != 0))) return false;
        return true;
    }

    /// <summary>Applies a validated world DTO to live components.</summary>
    internal void Apply(LoadingBayWorldSnapshot snapshot)
    {
        foreach (LoadingBayDoorSnapshot value in snapshot.Doors) { LoadingBayDoorStateComponent state = Door(value.EntityId); state.State = value.State; state.DueStep = value.DueStep; }
        foreach (LoadingBayFloorSnapshot value in snapshot.Floors) { LoadingBayFloorStateComponent state = Floor(value.EntityId); state.State = value.State; state.DueStep = value.DueStep; }
        foreach (LoadingBayLiftSnapshot value in snapshot.Lifts) { LoadingBayLiftStateComponent state = Lift(value.EntityId); state.State = value.State; state.DueStep = value.DueStep; }
        foreach (LoadingBayBarrelSnapshot value in snapshot.Barrels) { LoadingBayBarrelStateComponent state = Barrel(value.EntityId); state.Health = value.Health; state.Exploded = value.Exploded; }
        foreach (LoadingBayHazardSnapshot cooldown in snapshot.Hazards) Hazard(cooldown.EntityId).ReadyAtStep = cooldown.ReadyAtStep;
    }

    internal bool HazardReady(ulong hazardEntityId, ulong tick)
    {
        if (LoadingBayEntityMap.KindFor(hazardEntityId) != LoadingBayEntityKinds.Hazard
            || !_entityMap.TryRuntime(hazardEntityId, out EntityId entity))
            return false;
        ulong due = _entities.Get<LoadingBayHazardStateComponent>(entity).ReadyAtStep;
        return tick >= due;
    }
    internal LoadingBayE1M1HazardDefinition ApplyHazard(ulong hazardEntityId, ulong tick, Action<int, string> damage, Action<LoadingBayFact> record)
    {
        LoadingBayE1M1HazardDefinition hazard = LoadingBayE1M1SemanticCatalog.Hazards.Single(value => value.EntityId == hazardEntityId);
        if (!HazardReady(hazardEntityId, tick)) throw new InvalidOperationException("Hazard cooldown is not ready.");
        ulong due = checked(tick + (ulong)hazard.CooldownTicks);
        Hazard(hazardEntityId).ReadyAtStep = due;
        damage(hazard.Damage, $"hazard.{hazard.Label}");
        record(new WorldInteractionFact("hazard", hazard.EntityId, "cooldown", tick, due));
        return hazard;
    }

    internal LoadingBayDoorSnapshot ActivateDoor(ulong entityId, ulong tick, Action<LoadingBayFact> record)
    {
        LoadingBayE1M1DoorDefinition definition = LoadingBayE1M1SemanticCatalog.Doors.Single(value => value.EntityId == entityId);
        LoadingBayDoorStateComponent state = Door(entityId);
        if (state.State is LoadingBayDoorState.Opening or LoadingBayDoorState.Open) return new(entityId, state.State, state.DueStep);
        state.State = LoadingBayDoorState.Opening;
        state.DueStep = checked(tick + (ulong)definition.MotionDurationTicks);
        record(new WorldInteractionFact("door", entityId, "opening", tick, state.DueStep));
        return new(entityId, state.State, state.DueStep);
    }

    internal LoadingBayFloorSnapshot ActivateFloor(ulong entityId, ulong tick, Action<LoadingBayFact> record)
    {
        LoadingBayE1M1FloorDefinition definition = LoadingBayE1M1SemanticCatalog.Floors.Single(value => value.EntityId == entityId);
        LoadingBayFloorStateComponent state = Floor(entityId);
        if (state.State != LoadingBayFloorState.Armed) return new(entityId, state.State, state.DueStep);
        state.State = LoadingBayFloorState.Lowering;
        state.DueStep = checked(tick + (ulong)definition.MotionDurationTicks);
        record(new WorldInteractionFact("floor", entityId, "lowering", tick, state.DueStep));
        return new(entityId, state.State, state.DueStep);
    }

    internal LoadingBayLiftSnapshot ActivateLift(ulong entityId, ulong tick, Action<LoadingBayFact> record)
    {
        LoadingBayE1M1LiftDefinition definition = LoadingBayE1M1SemanticCatalog.Lifts.Single(value => value.EntityId == entityId);
        LoadingBayLiftStateComponent state = Lift(entityId);
        if (state.State is LoadingBayLiftState.Lowering or LoadingBayLiftState.Waiting) return new(entityId, state.State, state.DueStep);
        state.State = LoadingBayLiftState.Lowering;
        state.DueStep = checked(tick + (ulong)definition.MotionDurationTicks);
        record(new WorldInteractionFact("lift", entityId, "lowering", tick, state.DueStep));
        return new(entityId, state.State, state.DueStep);
    }

    internal void Advance(ulong tick, Action<LoadingBayFact> record)
    {
        foreach (LoadingBayDoorSnapshot state in LoadingBayE1M1SemanticCatalog.Doors
            .Select(value => new LoadingBayDoorSnapshot(value.EntityId, Door(value.EntityId).State, Door(value.EntityId).DueStep))
            .Where(value => value.DueStep != 0 && value.DueStep <= tick).ToArray())
        {
            LoadingBayE1M1DoorDefinition definition = LoadingBayE1M1SemanticCatalog.Doors.Single(value => value.EntityId == state.EntityId);
            LoadingBayDoorStateComponent live = Door(state.EntityId);
            LoadingBayDoorState next = state.State switch
            {
                LoadingBayDoorState.Opening => LoadingBayDoorState.Open,
                LoadingBayDoorState.Open => LoadingBayDoorState.Closing,
                LoadingBayDoorState.Closing => LoadingBayDoorState.Closed,
                _ => state.State,
            };
            ulong due = state.State switch
            {
                LoadingBayDoorState.Opening => checked(tick + (ulong)definition.AutoCloseAfterTicks),
                LoadingBayDoorState.Open => checked(tick + (ulong)definition.MotionDurationTicks),
                _ => 0UL,
            };
            live.State = next; live.DueStep = due;
            record(new WorldInteractionFact("door", state.EntityId, next.ToString().ToLowerInvariant(), tick, due));
        }
        foreach (LoadingBayFloorSnapshot state in LoadingBayE1M1SemanticCatalog.Floors
            .Select(value => new LoadingBayFloorSnapshot(value.EntityId, Floor(value.EntityId).State, Floor(value.EntityId).DueStep))
            .Where(value => value.DueStep != 0 && value.DueStep <= tick).ToArray())
        {
            LoadingBayFloorStateComponent live = Floor(state.EntityId);
            live.State = LoadingBayFloorState.Lowered; live.DueStep = 0;
            record(new WorldInteractionFact("floor", state.EntityId, "lowered", tick, 0));
        }
        foreach (LoadingBayLiftSnapshot state in LoadingBayE1M1SemanticCatalog.Lifts
            .Select(value => new LoadingBayLiftSnapshot(value.EntityId, Lift(value.EntityId).State, Lift(value.EntityId).DueStep))
            .Where(value => value.DueStep != 0 && value.DueStep <= tick).ToArray())
        {
            LoadingBayE1M1LiftDefinition definition = LoadingBayE1M1SemanticCatalog.Lifts.Single(value => value.EntityId == state.EntityId);
            LoadingBayLiftStateComponent live = Lift(state.EntityId);
            (LoadingBayLiftState next, ulong due) = state.State switch
            {
                LoadingBayLiftState.Lowering => (LoadingBayLiftState.Waiting, checked(tick + (ulong)definition.LoweredWaitTicks)),
                LoadingBayLiftState.Waiting => (LoadingBayLiftState.Raising, checked(tick + (ulong)definition.MotionDurationTicks)),
                LoadingBayLiftState.Raising => (LoadingBayLiftState.Raised, 0UL),
                _ => (state.State, state.DueStep),
            };
            live.State = next; live.DueStep = due;
            record(new WorldInteractionFact("lift", state.EntityId, next.ToString().ToLowerInvariant(), tick, due));
        }
    }

    internal IEnumerable<LoadingBayE1M1BarrelDefinition> DamageBarrel(ulong entityId, int damage, ulong tick, Func<LoadingBayE1M1BarrelDefinition, LoadingBayE1M1BarrelDefinition, bool> occluded, Action<LoadingBayFact> record)
    {
        if (damage <= 0) return [];
        ArgumentNullException.ThrowIfNull(occluded);
        ArgumentNullException.ThrowIfNull(record);
        // Unknown barrels reject before touching canonical state.
        LoadingBayE1M1BarrelDefinition first = LoadingBayE1M1SemanticCatalog.Barrels.Single(value => value.EntityId == entityId);
        // Bounded work queue with one explosion per barrel. Each barrel commits immediately:
        // a failed Engine line-of-effect query leaves earlier barrels committed (no rollback promise
        // for terminal callbacks). Occlusion is resolved locally when a neighbor is discovered.
        Queue<(LoadingBayE1M1BarrelDefinition Barrel, int Damage, bool Chained)> pending = new();
        pending.Enqueue((first, damage, false));
        List<(LoadingBayE1M1BarrelDefinition Barrel, bool Chained)> exploded = [];
        while (pending.TryDequeue(out (LoadingBayE1M1BarrelDefinition Barrel, int Damage, bool Chained) item))
        {
            LoadingBayBarrelStateComponent state = Barrel(item.Barrel.EntityId);
            if (state.Exploded) continue;
            int health = Math.Max(0, state.Health - item.Damage);
            state.Health = health;
            if (health != 0) continue;
            state.Exploded = true;
            exploded.Add((item.Barrel, item.Chained));
            record(new BarrelExplosionFact(item.Barrel.EntityId, item.Barrel.Damage, item.Barrel.Radius, tick, item.Chained));
            foreach (LoadingBayE1M1BarrelDefinition candidate in LoadingBayE1M1SemanticCatalog.Barrels.Where(value => value.EntityId != item.Barrel.EntityId))
            {
                double distance = Vector3.Distance(item.Barrel.Translation, candidate.Translation);
                if (distance > item.Barrel.Radius || occluded(item.Barrel, candidate)) continue;
                int scaled = (int)Math.Ceiling(item.Barrel.Damage * (1d - distance / item.Barrel.Radius));
                if (scaled > 0) pending.Enqueue((candidate, scaled, true));
            }
        }
        return exploded.Select(value => value.Barrel).ToArray();
    }

    /// <summary>Semantic continuations only; Engine scheduler handles are deliberately not part of product persistence.</summary>
    internal IEnumerable<ulong> DueSteps() => LoadingBayE1M1SemanticCatalog.Doors
        .Select(value => Door(value.EntityId).DueStep)
        .Concat(LoadingBayE1M1SemanticCatalog.Floors.Select(value => Floor(value.EntityId).DueStep))
        .Concat(LoadingBayE1M1SemanticCatalog.Lifts.Select(value => Lift(value.EntityId).DueStep))
        .Where(value => value != 0).Distinct();

    private static bool Same<T>(IReadOnlyCollection<T> values, ICollection<ulong> keys, Func<T, ulong> id) => values.Count == keys.Count && values.Select(id).Distinct().Count() == values.Count && values.All(value => keys.Contains(id(value)));
}
