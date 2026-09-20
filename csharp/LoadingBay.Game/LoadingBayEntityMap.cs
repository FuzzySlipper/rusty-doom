using Rusty.Engine.Entities;

namespace LoadingBay.Game;

/// <summary>Creation-time kind metadata for canonical E1M1 entities.</summary>
internal static class LoadingBayEntityKinds
{
    internal const string Player = "loading-bay.player";
    internal const string Enemy = "loading-bay.enemy";
    internal const string Pickup = "loading-bay.pickup";
    internal const string Encounter = "loading-bay.encounter";
    internal const string Barrel = "loading-bay.barrel";
    internal const string Hazard = "loading-bay.hazard";
    internal const string Door = "loading-bay.door";
    internal const string Floor = "loading-bay.floor";
    internal const string Lift = "loading-bay.lift";
    internal const string Secret = "loading-bay.secret";
    internal const string Exit = "loading-bay.exit";
    internal const string WorldObject = "loading-bay.world-object";
}

/// <summary>
/// Explicit authored-E1M1-identity to runtime-entity mapping over one
/// <see cref="EntityStore"/>. The store allocates runtime <see cref="EntityId"/>
/// values in whatever order construction requests; this map is the only path
/// from an authored catalog identity to the live runtime entity. Engine
/// spatial, perception, and trigger facts keep using authored identities by
/// Engine authority and are never translated here.
/// </summary>
internal sealed class LoadingBayEntityMap
{
    internal const ulong PlayerAuthoredId = 1;

    private static readonly HashSet<ulong> Enemies = LoadingBayE1M1SemanticCatalog.Enemies.Select(enemy => enemy.EntityId).ToHashSet();
    private static readonly HashSet<ulong> Pickups = LoadingBayE1M1SemanticCatalog.Pickups.Select(pickup => pickup.EntityId).ToHashSet();
    private static readonly HashSet<ulong> Encounters = LoadingBayE1M1SemanticCatalog.Encounters.Select(encounter => encounter.EntityId).ToHashSet();
    private static readonly HashSet<ulong> Barrels = LoadingBayE1M1SemanticCatalog.Barrels.Select(barrel => barrel.EntityId).ToHashSet();
    private static readonly HashSet<ulong> Hazards = LoadingBayE1M1SemanticCatalog.Hazards.Select(hazard => hazard.EntityId).ToHashSet();
    private static readonly HashSet<ulong> Doors = LoadingBayE1M1SemanticCatalog.Doors.Select(door => door.EntityId).ToHashSet();
    private static readonly HashSet<ulong> Floors = LoadingBayE1M1SemanticCatalog.Floors.Select(floor => floor.EntityId).ToHashSet();
    private static readonly HashSet<ulong> Lifts = LoadingBayE1M1SemanticCatalog.Lifts.Select(lift => lift.EntityId).ToHashSet();
    private static readonly HashSet<ulong> Secrets = LoadingBayE1M1SemanticCatalog.Secrets.Select(secret => secret.EntityId).ToHashSet();
    private static readonly HashSet<ulong> Exits = LoadingBayE1M1SemanticCatalog.Exits.Select(exit => exit.EntityId).ToHashSet();

    private readonly Dictionary<ulong, EntityId> _runtime = new();

    private LoadingBayEntityMap()
    {
    }

    /// <summary>Authored creation order: preserves the historical allocation sequence.</summary>
    internal static LoadingBayEntityMap Bootstrap(EntityStore store)
    {
        List<ulong> order = [];
        for (ulong authored = 1; authored <= LoadingBayE1M1SemanticCatalog.CanonicalEntityCount; authored++) order.Add(authored);
        return Bootstrap(store, order);
    }

    /// <summary>
    /// Explicit creation order. The order must cover exactly the canonical
    /// authored identities, so the store shape never changes while allocation
    /// order is free to differ. Used by the lifecycle exercise to prove
    /// authored resolution does not depend on allocation order.
    /// </summary>
    internal static LoadingBayEntityMap Bootstrap(EntityStore store, IReadOnlyList<ulong> order)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(order);
        if (order.Count != (int)LoadingBayE1M1SemanticCatalog.CanonicalEntityCount
            || order.Distinct().Count() != order.Count
            || order.Any(authored => authored < 1 || authored > LoadingBayE1M1SemanticCatalog.CanonicalEntityCount))
            throw new ArgumentOutOfRangeException(nameof(order), "Entity bootstrap must cover exactly the canonical authored E1M1 identities.");
        LoadingBayEntityMap map = new();
        foreach (ulong authored in order)
        {
            EntityId entity = store.Create(new EntityTypeId(KindFor(authored)));
            if (!map._runtime.TryAdd(authored, entity))
                throw new InvalidOperationException($"Duplicate authored E1M1 identity {authored}.");
        }
        return map;
    }

    internal static string KindFor(ulong authored)
    {
        if (authored == PlayerAuthoredId) return LoadingBayEntityKinds.Player;
        if (Enemies.Contains(authored)) return LoadingBayEntityKinds.Enemy;
        if (Pickups.Contains(authored)) return LoadingBayEntityKinds.Pickup;
        if (Encounters.Contains(authored)) return LoadingBayEntityKinds.Encounter;
        if (Barrels.Contains(authored)) return LoadingBayEntityKinds.Barrel;
        if (Hazards.Contains(authored)) return LoadingBayEntityKinds.Hazard;
        if (Doors.Contains(authored)) return LoadingBayEntityKinds.Door;
        if (Floors.Contains(authored)) return LoadingBayEntityKinds.Floor;
        if (Lifts.Contains(authored)) return LoadingBayEntityKinds.Lift;
        if (Secrets.Contains(authored)) return LoadingBayEntityKinds.Secret;
        if (Exits.Contains(authored)) return LoadingBayEntityKinds.Exit;
        return LoadingBayEntityKinds.WorldObject;
    }

    internal EntityId Runtime(ulong authored) =>
        _runtime.TryGetValue(authored, out EntityId entity)
            ? entity
            : throw new ArgumentOutOfRangeException(nameof(authored), $"Unknown authored E1M1 identity {authored}.");

    internal bool TryRuntime(ulong authored, out EntityId entity) => _runtime.TryGetValue(authored, out entity);
}
