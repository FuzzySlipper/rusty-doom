using Rusty.Engine.Entities;
using Mechanics = Rusty.Engine.Mechanics;

namespace LoadingBay.Game;

/// <summary>
/// Named world-domain owner: hazards, movers, secrets, doors, exit, barrels,
/// and their facts. Coordinators call these typed operations directly; no
/// per-update delegate list. Engine spatial queries arrive as explicit
/// arguments; the session root retains Engine-service composition.
/// </summary>
internal sealed class LoadingBayWorld
{
    private readonly LoadingBayWorldState _world;
    private readonly LoadingBayCombat _combat;
    private readonly Mechanics.StatsComponent _playerStats;
    private readonly Action<LoadingBayFact> _record;
    private readonly Action<ulong> _schedule;
    private readonly HashSet<string> _secrets = new(StringComparer.Ordinal);
    private bool _complete;

    internal LoadingBayWorld(
        LoadingBayWorldState world,
        LoadingBayCombat combat,
        Mechanics.StatsComponent playerStats,
        Action<LoadingBayFact> record,
        Action<ulong> schedule)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _playerStats = playerStats ?? throw new ArgumentNullException(nameof(playerStats));
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
    }

    internal bool Complete => _complete;
    internal string[] SecretSnapshots() => _secrets.OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private void Record(LoadingBayFact fact) => _record(fact);
    private LoadingBayReceipt Accept(string code, string? correlation = null) => new(true, code, correlation);
    private LoadingBayReceipt Reject(string code, string? correlation = null) { Record(new RejectedFact(code, correlation)); return new(false, code, correlation); }

    /// <summary>Called only after Engine's overlap coordinator has established a canonical hazard enter/stay fact.</summary>
    internal LoadingBayReceipt ApplyHazard(ulong hazardEntityId, ulong tick)
    {
        if (!_world.HazardReady(hazardEntityId, tick)) return Reject("hazard.cooldown");
        try { _world.ApplyHazard(hazardEntityId, tick, (damage, cause) => _combat.ApplyDamage("player", damage, cause), Record); return Accept("hazard.applied"); }
        catch (InvalidOperationException) { return Reject("hazard.unknown"); }
    }

    internal LoadingBayReceipt ActivateFloor(ulong floorEntityId, ulong tick)
    {
        try
        {
            if (_world.FloorState(floorEntityId).State != LoadingBayFloorState.Armed) return Reject("floor.unavailable");
            LoadingBayFloorSnapshot state = _world.ActivateFloor(floorEntityId, tick, Record); if (state.DueStep > tick) _schedule(state.DueStep); return Accept("floor.lowering");
        }
        catch (InvalidOperationException) { return Reject("floor.unknown"); }
    }

    internal LoadingBayReceipt ActivateLift(ulong liftEntityId, ulong tick)
    {
        try
        {
            if (_world.LiftState(liftEntityId).State != LoadingBayLiftState.Raised) return Reject("lift.unavailable");
            LoadingBayLiftSnapshot state = _world.ActivateLift(liftEntityId, tick, Record); if (state.DueStep > tick) _schedule(state.DueStep); return Accept("lift.lowering");
        }
        catch (InvalidOperationException) { return Reject("lift.unknown"); }
    }

    internal LoadingBayReceipt DiscoverSecret(string secret)
    {
        if (!_secrets.Add(secret)) return Reject("secret.already-discovered");
        Record(new SecretDiscoveredFact(secret)); return Accept("secret.discovered");
    }

    internal LoadingBayReceipt DiscoverSecretByEntity(ulong secretEntityId)
    {
        try { return DiscoverSecret(LoadingBayE1M1SemanticCatalog.Secrets.Single(value => value.EntityId == secretEntityId).Label); }
        catch (InvalidOperationException) { return Reject("secret.unknown"); }
    }

    internal LoadingBayReceipt ActivateDoor(ulong doorEntityId, ulong tick)
    {
        try
        {
            if (_world.DoorState(doorEntityId).State is not (LoadingBayDoorState.Closed or LoadingBayDoorState.Closing)) return Reject("door.unavailable");
            LoadingBayDoorSnapshot state = _world.ActivateDoor(doorEntityId, tick, Record);
            if (state.DueStep > tick) _schedule(state.DueStep);
            Record(new DoorChangedFact(LoadingBayE1M1SemanticCatalog.Doors.Single(value => value.EntityId == doorEntityId).Label, true));
            return Accept("door.opening");
        }
        catch (InvalidOperationException) { return Reject("door.unknown"); }
    }

    internal LoadingBayReceipt CompleteExit(string exit)
    {
        if (_playerStats.GetTrack(LoadingBayStatIds.Health).ValueInt64 == 0) return Reject("exit.player-defeated");
        if (_complete) return Reject("exit.already-complete");
        _complete = true; Record(new ExitCompletedFact(exit)); return Accept("exit.completed");
    }

    internal LoadingBayReceipt CompleteExitByEntity(ulong exitEntityId)
    {
        try { return CompleteExit(LoadingBayE1M1SemanticCatalog.Exits.Single(value => value.EntityId == exitEntityId).Label); }
        catch (InvalidOperationException) { return Reject("exit.unknown"); }
    }

    internal LoadingBayReceipt DamageBarrel(
        ulong barrelEntityId,
        int damage,
        ulong tick,
        Func<LoadingBayE1M1BarrelDefinition, LoadingBayE1M1BarrelDefinition, bool> occluded)
    {
        try
        {
            foreach (LoadingBayE1M1BarrelDefinition barrel in _world.DamageBarrel(barrelEntityId, damage, tick, occluded, Record))
                RecordWorldAction("barrel.exploded", barrel.Label);
            return Accept("barrel.damage-applied");
        }
        catch (InvalidOperationException) { return Reject("barrel.unknown"); }
    }

    internal void RestoreSecrets(string[] secrets)
    {
        _secrets.Clear();
        foreach (string id in secrets) _secrets.Add(id);
    }

    internal void RestoreCompletion(bool complete) => _complete = complete;

    /// <summary>
    /// Derived read-only projection of canonical door state for save/diagnostic shape.
    /// Restore validates the shape only; the canonical world snapshot owns the truth.
    /// </summary>
    internal LoadingBayNamedState[] DoorSnapshots() => LoadingBayE1M1SemanticCatalog.Doors
        .OrderBy(door => door.Label, StringComparer.Ordinal)
        .Select(door => new LoadingBayNamedState(door.Label, _world.DoorState(door.EntityId).State is LoadingBayDoorState.Open or LoadingBayDoorState.Opening))
        .ToArray();

    private LoadingBayReceipt RecordWorldAction(string code, string subject) { Record(new WorldActionFact(code, subject)); return Accept(code); }
}
