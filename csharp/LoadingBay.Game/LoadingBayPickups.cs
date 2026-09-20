using Rusty.Engine.Entities;
using Mechanics = Rusty.Engine.Mechanics;

namespace LoadingBay.Game;

/// <summary>
/// Named pickup-domain owner: canonical and fixture pickup collection,
/// pickup lifecycle components, and their facts. Coordinators call these
/// typed operations directly; no per-update delegate list.
/// </summary>
internal sealed class LoadingBayPickups
{
    private readonly Mechanics.InventoryStore _inventory;
    private readonly EntityId _player;
    private readonly Mechanics.StatsComponent _playerStats;
    private readonly LoadingBayTuning _tuning;
    private readonly EntityStore _entities;
    private readonly LoadingBayEntityMap _entityMap;
    private readonly LoadingBayCombat _combat;
    private readonly Action<LoadingBayFact> _record;
    private readonly Func<ulong> _currentTick;
    private readonly HashSet<string> _manualPickupKeys = new(StringComparer.Ordinal);

    internal LoadingBayPickups(
        Mechanics.InventoryStore inventory,
        EntityId player,
        Mechanics.StatsComponent playerStats,
        LoadingBayTuning tuning,
        EntityStore entities,
        LoadingBayEntityMap entityMap,
        LoadingBayCombat combat,
        Action<LoadingBayFact> record,
        Func<ulong> currentTick)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _playerStats = playerStats ?? throw new ArgumentNullException(nameof(playerStats));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _currentTick = currentTick ?? throw new ArgumentNullException(nameof(currentTick));
        _player = player;
    }

    private Mechanics.Track HealthTrack => _playerStats.GetTrack(LoadingBayStatIds.Health);
    private Mechanics.Track ArmorTrack => _playerStats.GetTrack(LoadingBayStatIds.Armor);
    private LoadingBayPickupStateComponent PickupState(ulong pickupEntityId) =>
        _entities.Get<LoadingBayPickupStateComponent>(_entityMap.Runtime(pickupEntityId));
    private static string CanonicalPickupKey(ulong entityId) => $"e1m1.pickup.{entityId}";

    private void Record(LoadingBayFact fact) => _record(fact);
    private LoadingBayReceipt Accept(string code, string? correlation = null) => new(true, code, correlation);
    private LoadingBayReceipt Reject(string code, string? correlation = null) { Record(new RejectedFact(code, correlation)); return new(false, code, correlation); }

    internal LoadingBayReceipt CollectPickup(string pickup, LoadingBayItem item, ulong quantity)
    {
        if (_manualPickupKeys.Contains(pickup)) return Reject("pickup.already-collected");
        if (HealthTrack.ValueInt64 == 0) { _manualPickupKeys.Remove(pickup); return Reject("pickup.player-defeated"); }
        try
        {
            if (!CanApplyPickup(item)) return Reject("pickup.not-needed");
            if (item.PickupPolicy is LoadingBayPickupPolicy.Restore(var amount, var maximum, _))
            {
                long boundedMaximum = Math.Min(_tuning.MaximumHealth, maximum);
                if (!TryRestoreWithinPickupCap(HealthTrack, amount, boundedMaximum)) return Reject("pickup.inventory-rejected");
            }
            else if (item.PickupPolicy is LoadingBayPickupPolicy.SetMinimum(var minimum, var setProtection))
            {
                ArmorTrack.SetCurrent(Math.Max(ArmorTrack.Value, minimum));
                _combat.ArmorProtection = setProtection;
            }
            else if (item.PickupPolicy is LoadingBayPickupPolicy.RestoreArmor(var armorAmount, var armorMaximum, _, var armorProtection))
            {
                long boundedMaximum = Math.Min(_tuning.MaximumArmor, armorMaximum);
                bool hadProtection = ArmorTrack.ValueInt64 > 0 && _combat.ArmorProtection.Mode != LoadingBayArmorProtectionMode.None;
                if (!TryRestoreWithinPickupCap(ArmorTrack, armorAmount, boundedMaximum)) return Reject("pickup.inventory-rejected");
                // E1M1's bonus armor preserves an existing green/blue armor class.
                if (!hadProtection) _combat.ArmorProtection = armorProtection;
            }
            else _inventory.Grant(_player, item.MechanicsDefinition, quantity);
            _manualPickupKeys.Add(pickup);
            Record(new PickupCollectedFact(pickup, item.Id, quantity));
            return Accept("pickup.collected");
        }
        catch (Mechanics.MechanicsException) { _manualPickupKeys.Remove(pickup); return Reject("pickup.inventory-rejected"); }
    }

    internal LoadingBayReceipt CollectCanonicalPickup(ulong entityId)
    {
        LoadingBayE1M1PickupPlacement pickup = LoadingBayE1M1SemanticCatalog.Pickup(entityId);
        if (PickupState(entityId).Lifecycle == LoadingBayPickupLifecycle.Collected) return Reject("pickup.already-collected");
        if (PickupState(entityId).Lifecycle == LoadingBayPickupLifecycle.Dormant) return Reject("pickup.dormant");
        LoadingBayReceipt outcome = pickup.ProgramId == "pickup/weapon-starter"
            ? _combat.CollectWeaponStarter(pickup)
            : CollectPickup(CanonicalPickupKey(entityId), LoadingBayDefinitions.Item(pickup.ItemId), pickup.Quantity);
        _manualPickupKeys.Remove(CanonicalPickupKey(entityId));
        UpdatePickupState(pickup, outcome.Accepted ? LoadingBayPickupLifecycle.Collected : PickupState(entityId).Lifecycle, outcome.Code, _currentTick(), 0);
        return outcome;
    }

    internal bool CanCollectCanonicalPickup(ulong entityId)
    {
        LoadingBayE1M1PickupPlacement pickup = LoadingBayE1M1SemanticCatalog.Pickup(entityId);
        if (PickupState(entityId).Lifecycle != LoadingBayPickupLifecycle.Active || HealthTrack.ValueInt64 == 0) return false;
        return pickup.ProgramId == "pickup/weapon-starter"
            ? _combat.CanApplyWeaponStarter(pickup)
            : CanApplyPickup(LoadingBayDefinitions.Item(pickup.ItemId));
    }

    internal void ApplyLifecycleFact(PickupLifecycleFact lifecycle)
    {
        LoadingBayPickupStateComponent state = PickupState(lifecycle.PickupEntityId);
        state.Lifecycle = lifecycle.Lifecycle;
        state.Cause = lifecycle.Cause;
        state.Tick = lifecycle.Tick;
        state.TriggerRevision = lifecycle.TriggerRevision;
    }

    internal void RestorePickups(LoadingBayPickupSnapshot[] pickups)
    {
        foreach (LoadingBayPickupSnapshot pickup in pickups)
        {
            LoadingBayPickupStateComponent state = PickupState(pickup.EntityId);
            state.Lifecycle = pickup.Lifecycle;
            state.Cause = pickup.Cause;
            state.Tick = pickup.Tick;
            state.TriggerRevision = pickup.TriggerRevision;
        }
    }

    internal LoadingBayPickupSnapshot[] PickupSnapshots() => LoadingBayE1M1SemanticCatalog.Pickups
        .OrderBy(pickup => pickup.EntityId)
        .Select(pickup =>
        {
            LoadingBayPickupStateComponent state = PickupState(pickup.EntityId);
            return new LoadingBayPickupSnapshot(pickup.EntityId, pickup.ItemId, pickup.ProgramId, state.Lifecycle, state.Cause, state.Tick, state.TriggerRevision);
        }).ToArray();

    private bool CanApplyPickup(LoadingBayItem item) => item.PickupPolicy switch
    {
        LoadingBayPickupPolicy.Restore(_, var maximum, var consumeAtCap) => HealthTrack.ValueInt64 < Math.Min(_tuning.MaximumHealth, maximum) || consumeAtCap,
        LoadingBayPickupPolicy.SetMinimum(var minimum, _) => ArmorTrack.ValueInt64 < minimum,
        LoadingBayPickupPolicy.RestoreArmor(_, var maximum, var consumeAtCap, _) => ArmorTrack.ValueInt64 < Math.Min(_tuning.MaximumArmor, maximum) || consumeAtCap,
        _ => true,
    };

    private static bool TryRestoreWithinPickupCap(Mechanics.Track track, long amount, long maximum)
    {
        if (track.Value > maximum) return false;
        track.SetCurrent(Math.Min(maximum, track.Value + amount));
        return true;
    }

    private void UpdatePickupState(LoadingBayE1M1PickupPlacement pickup, LoadingBayPickupLifecycle lifecycle, string cause, ulong tick, ulong triggerRevision)
    {
        LoadingBayPickupStateComponent state = PickupState(pickup.EntityId);
        state.Lifecycle = lifecycle;
        state.Cause = cause;
        state.Tick = tick;
        state.TriggerRevision = triggerRevision;
    }
}
