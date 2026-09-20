using Rusty.Engine.Entities;
using Mechanics = Rusty.Engine.Mechanics;

namespace LoadingBay.Game;

/// <summary>
/// Named combat-domain owner: player damage application, weapon fire and
/// equipment, enemy attacks, encounters, and their facts. Coordinators call
/// these typed operations directly; no per-update delegate list.
/// </summary>
internal sealed class LoadingBayCombat
{
    private readonly Mechanics.InventoryStore _inventory;
    private readonly EntityId _player;
    private readonly Mechanics.StatsComponent _playerStats;
    private readonly LoadingBayTuning _tuning;
    private readonly EntityStore _entities;
    private readonly LoadingBayEntityMap _entityMap;
    private readonly Action<LoadingBayFact> _record;
    private readonly Func<ulong> _currentTick;
    private readonly Dictionary<string, ulong> _weaponReadyAt = new(StringComparer.Ordinal);
    private readonly HashSet<ulong> _activatedEncounters = [];

    internal LoadingBayCombat(
        Mechanics.InventoryStore inventory,
        EntityId player,
        Mechanics.StatsComponent playerStats,
        LoadingBayTuning tuning,
        EntityStore entities,
        LoadingBayEntityMap entityMap,
        Action<LoadingBayFact> record,
        Func<ulong> currentTick)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _playerStats = playerStats ?? throw new ArgumentNullException(nameof(playerStats));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _currentTick = currentTick ?? throw new ArgumentNullException(nameof(currentTick));
        _player = player;
    }

    internal LoadingBayArmorProtection ArmorProtection { get; set; } = LoadingBayArmorProtection.None;

    internal IReadOnlySet<ulong> ActivatedEncounters => _activatedEncounters;

    /// <summary>
    /// The one Engine-integration seam: spawn-side trigger synchronization for materialized
    /// enemy drops, set once when Engine services are composed (null without Engine services,
    /// matching the headless rule that drops update components but synchronize no triggers).
    /// </summary>
    internal Func<ulong, System.Numerics.Vector3, ulong, CanonicalPickupTriggerStateFact>? MaterializeDrop { get; set; }

    private Mechanics.Track HealthTrack => _playerStats.GetTrack(LoadingBayStatIds.Health);
    private Mechanics.Track ArmorTrack => _playerStats.GetTrack(LoadingBayStatIds.Armor);
    private Mechanics.StatsComponent EnemyStats(EntityId entity) => _entities.Get<Mechanics.StatsComponent>(entity);
    private LoadingBayEnemyStateComponent EnemyState(EntityId entity) => _entities.Get<LoadingBayEnemyStateComponent>(entity);
    private Mechanics.Track EnemyVitality(EntityId entity) => EnemyStats(entity).GetTrack(LoadingBayStatIds.Vitality);
    private static int EnemyHealth(Mechanics.Track vitality) => checked((int)vitality.ValueInt64);
    private bool TryEnemyEntity(ulong enemyEntityId, out EntityId entity)
    {
        if (!_entityMap.TryRuntime(enemyEntityId, out entity)
            || LoadingBayEntityMap.KindFor(enemyEntityId) != LoadingBayEntityKinds.Enemy)
        {
            entity = default;
            return false;
        }
        return true;
    }

    private void Record(LoadingBayFact fact) => _record(fact);
    private LoadingBayReceipt Accept(string code, string? correlation = null) => new(true, code, correlation);
    private LoadingBayReceipt Reject(string code, string? correlation = null) { Record(new RejectedFact(code, correlation)); return new(false, code, correlation); }

    internal LoadingBayReceipt ApplyDamage(string target, int damage, string cause)
    {
        if (target != "player") return Reject("damage.unknown-target");
        if (damage <= 0) return Reject("damage.invalid");
        if (HealthTrack.ValueInt64 == 0) return Reject("damage.target-defeated");
        long absorbed = ArmorProtection.AbsorptionDivisor == 0 ? 0 : Math.Min(ArmorTrack.ValueInt64, damage / ArmorProtection.AbsorptionDivisor);
        if (absorbed > 0) ArmorTrack.Spend(absorbed);
        long applied = Math.Min(HealthTrack.ValueInt64, damage - absorbed);
        if (applied > 0) HealthTrack.Spend(applied);
        bool defeated = HealthTrack.ValueInt64 == 0;
        Record(new DamageAppliedFact(target, damage, absorbed, applied, cause, defeated));
        return Accept(defeated ? "damage.defeated" : "damage.applied");
    }

    internal LoadingBayReceipt ActivateEncounter(ulong encounterEntityId, ulong tick)
    {
        LoadingBayE1M1EncounterDefinition encounter = LoadingBayE1M1SemanticCatalog.Encounters.Single(value => value.EntityId == encounterEntityId);
        if (!_activatedEncounters.Add(encounter.EntityId)) return Reject("encounter.already-active");
        Record(new EncounterActivatedFact(encounter.EntityId, encounter.Label, encounter.ActivationRadius, tick));
        foreach (ulong member in encounter.Members)
        {
            LoadingBayE1M1EnemyDefinition enemy = LoadingBayE1M1SemanticCatalog.Enemy(member);
            EntityId entity = _entityMap.Runtime(member);
            LoadingBayEnemyStateComponent state = EnemyState(entity);
            if (state.Posture == LoadingBayEnemyPosture.Dormant)
            {
                state.Posture = LoadingBayEnemyPosture.Active;
                Record(new EnemyPostureChangedFact(member, state.Posture, EnemyHealth(EnemyVitality(entity)), tick, "encounter.activated"));
            }
        }
        return Accept("encounter.activated");
    }

    internal LoadingBayReceipt ActivateEncounterByLabel(string label)
    {
        LoadingBayE1M1EncounterDefinition? encounter = LoadingBayE1M1SemanticCatalog.Encounters.SingleOrDefault(value => value.Label == label);
        return encounter is null ? Reject("encounter.unknown") : ActivateEncounter(encounter.EntityId, _currentTick());
    }

    internal LoadingBayReceipt ApplyWeaponDamage(ulong enemyEntityId, string weaponId, int damage, ulong tick)
    {
        if (damage <= 0 || !LoadingBayDefinitions.Weapons.ContainsKey(weaponId)) return Reject("combat.invalid-hit");
        if (!TryEnemyEntity(enemyEntityId, out EntityId entity)) return Reject("combat.unknown-enemy");
        LoadingBayEnemyStateComponent state = EnemyState(entity);
        Mechanics.Track vitality = EnemyVitality(entity);
        if (state.Posture is LoadingBayEnemyPosture.Dormant or LoadingBayEnemyPosture.Defeated) return Reject("combat.ineligible-target");
        vitality.SetCurrent(Math.Max(0, EnemyHealth(vitality) - damage));
        Record(new EnemyHitFact(enemyEntityId, weaponId, damage, EnemyHealth(vitality), tick));
        if (EnemyHealth(vitality) > 0)
        {
            LoadingBayE1M1EnemyDefinition enemy = LoadingBayE1M1SemanticCatalog.Enemy(enemyEntityId);
            state.Posture = LoadingBayEnemyPosture.Pained;
            state.ReadyAtTick = checked(tick + (ulong)enemy.PainDurationTicks);
            Record(new EnemyPostureChangedFact(enemyEntityId, state.Posture, EnemyHealth(vitality), tick, "combat.pain"));
            return Accept("combat.hit");
        }
        state.Posture = LoadingBayEnemyPosture.Defeated;
        state.Visible = false;
        LoadingBayE1M1EnemyDefinition defeated = LoadingBayE1M1SemanticCatalog.Enemy(enemyEntityId);
        Record(new EnemyPostureChangedFact(enemyEntityId, state.Posture, 0, tick, "combat.defeated"));
        Record(new EnemyDefeatedFact(enemyEntityId, defeated.DropPickupEntityId, tick));
        if (defeated.DropPickupEntityId != 0)
        {
            LoadingBayE1M1PickupPlacement drop = LoadingBayE1M1SemanticCatalog.Pickup(defeated.DropPickupEntityId);
            LoadingBayPickupStateComponent dropState = PickupState(drop.EntityId);
            dropState.Lifecycle = LoadingBayPickupLifecycle.Active;
            dropState.Cause = "enemy.drop-materialized";
            dropState.Tick = tick;
            dropState.TriggerRevision = 0;
            if (MaterializeDrop is not null) Record(MaterializeDrop(defeated.DropPickupEntityId, defeated.Translation, tick));
        }
        foreach (LoadingBayE1M1EncounterDefinition encounter in LoadingBayE1M1SemanticCatalog.Encounters.Where(encounter => _activatedEncounters.Contains(encounter.EntityId) && encounter.Members.Contains(enemyEntityId)))
        {
            if (encounter.Members.All(member => EnemyState(_entityMap.Runtime(member)).Posture == LoadingBayEnemyPosture.Defeated))
                Record(new EncounterChangedFact(encounter.Label, true));
        }
        return Accept("combat.defeated");
    }

    internal LoadingBayWeaponFirePlan? PrepareWeaponFire(ulong tick)
    {
        string? weaponId = EquippedWeaponId();
        if (weaponId is null) { Reject("combat.no-equipped-weapon"); return null; }
        LoadingBayE1M1Weapon weapon = LoadingBayE1M1SemanticCatalog.Item<LoadingBayE1M1Weapon>(weaponId);
        if (_weaponReadyAt.TryGetValue(weaponId, out ulong readyAt) && tick < readyAt) { Reject("combat.weapon-cooldown"); return null; }
        if (weapon.AmmunitionCost > 0 && ItemQuantity(LoadingBayDefinitions.Item(weapon.AmmunitionId)) < (ulong)weapon.AmmunitionCost) { Reject("combat.insufficient-ammunition"); return null; }
        return new LoadingBayWeaponFirePlan(weaponId, weapon.AmmunitionId, weapon.AmmunitionCost, weapon.DamageRolls, weapon.Damage, weapon.PelletCount == 0 ? 1 : weapon.PelletCount, weapon.SpreadDegrees, weapon.MaximumDistance, tick);
    }

    internal IReadOnlySet<ulong> EligibleEnemyEntities() => LoadingBayE1M1SemanticCatalog.Enemies
        .Where(enemy => EnemyState(_entityMap.Runtime(enemy.EntityId)).Posture is LoadingBayEnemyPosture.Active or LoadingBayEnemyPosture.Pained)
        .Select(enemy => enemy.EntityId).ToHashSet();

    internal LoadingBayReceipt SettleWeaponFire(LoadingBayWeaponFirePlan plan, IReadOnlyList<LoadingBayWeaponImpact> impacts)
    {
        if (plan.WeaponId != EquippedWeaponId()) return Reject("combat.stale-fire-plan");
        LoadingBayE1M1Weapon weapon = LoadingBayE1M1SemanticCatalog.Item<LoadingBayE1M1Weapon>(plan.WeaponId);
        if (_weaponReadyAt.TryGetValue(plan.WeaponId, out ulong readyAt) && plan.Tick < readyAt) return Reject("combat.weapon-cooldown");
        try
        {
            if (plan.AmmunitionCost > 0) _inventory.Consume(_player, LoadingBayDefinitions.Item(plan.AmmunitionId).MechanicsDefinition, (ulong)plan.AmmunitionCost);
        }
        catch (Mechanics.MechanicsException) { return Reject("combat.insufficient-ammunition"); }
        _weaponReadyAt[plan.WeaponId] = checked(plan.Tick + (ulong)weapon.CooldownTicks);
        Record(new WeaponFiredFact(plan.WeaponId, plan.Tick, plan.PelletCount, impacts.Count));
        foreach (LoadingBayWeaponImpact impact in impacts)
        {
            if (impact.EnemyEntityId == 0) Record(new WeaponMissedFact(plan.WeaponId, plan.Tick, impact.PelletIndex, impact.WorldOccluded ? "combat.world-occluded" : "combat.miss"));
            else _ = ApplyWeaponDamage(impact.EnemyEntityId, plan.WeaponId, impact.Damage, plan.Tick);
        }
        return Accept("combat.fired");
    }

    internal IReadOnlyList<LoadingBayEnemyAttackPlan> PrepareEnemyAttacks(ulong tick, IReadOnlySet<ulong> visibleEnemies, uint visibilityCasts, uint occlusionRejects)
    {
        List<LoadingBayEnemyAttackPlan> plans = [];
        foreach (LoadingBayE1M1EnemyDefinition enemy in LoadingBayE1M1SemanticCatalog.Enemies)
        {
            EntityId entity = _entityMap.Runtime(enemy.EntityId);
            LoadingBayEnemyStateComponent state = EnemyState(entity);
            Mechanics.Track vitality = EnemyVitality(entity);
            if (state.Posture == LoadingBayEnemyPosture.Pained && tick >= state.ReadyAtTick)
            {
                state.Posture = LoadingBayEnemyPosture.Active;
                Record(new EnemyPostureChangedFact(enemy.EntityId, state.Posture, EnemyHealth(vitality), tick, "combat.pain-recovered"));
            }
            bool visible = visibleEnemies.Contains(enemy.EntityId);
            if ((state.Posture is LoadingBayEnemyPosture.Active or LoadingBayEnemyPosture.Pained) && state.Visible != visible)
            {
                state.Visible = visible;
                Record(new EnemyPerceptionFact(enemy.EntityId, visible, tick, visibilityCasts, occlusionRejects));
            }
            if (!visible || state.Posture != LoadingBayEnemyPosture.Active || tick < state.ReadyAtTick) continue;
            plans.Add(new LoadingBayEnemyAttackPlan(
                enemy.EntityId, enemy.AttackKind, enemy.Translation + enemy.AttackOriginOffset,
                enemy.AttackDamage, enemy.AttackRange, enemy.AttackCooldownTicks,
                (float)enemy.ProjectileMass, (float)enemy.ProjectileRadius, (float)enemy.ProjectileImpulse,
                (float)enemy.ProjectileGravityScale, enemy.ProjectileLifetimeTicks, (float)enemy.ProjectileRestitution, tick));
        }
        return plans;
    }

    internal LoadingBayReceipt SettleEnemyAttack(LoadingBayEnemyAttackPlan plan, bool hitPlayer, string cause)
    {
        if (!TryEnemyEntity(plan.EnemyEntityId, out EntityId entity)) return Reject("combat.stale-enemy-attack");
        LoadingBayEnemyStateComponent state = EnemyState(entity);
        if (state.Posture != LoadingBayEnemyPosture.Active || state.ReadyAtTick > plan.Tick)
            return Reject("combat.stale-enemy-attack");
        LoadingBayE1M1EnemyDefinition enemy = LoadingBayE1M1SemanticCatalog.Enemy(plan.EnemyEntityId);
        state.ReadyAtTick = checked(plan.Tick + (ulong)enemy.AttackCooldownTicks);
        Record(new EnemyAttackFact(plan.EnemyEntityId, plan.Kind, hitPlayer, plan.Tick, cause));
        if (plan.Kind == LoadingBayE1M1EnemyAttackKind.Projectile)
            Record(new EnemyProjectileFact(plan.EnemyEntityId, plan.Tick, "combat.projectile-realized"));
        return hitPlayer ? ApplyDamage("player", plan.Damage, $"enemy.{enemy.Label}") : Accept(cause);
    }

    internal void RecordProjectileOutcome(ulong enemyEntityId, ulong tick, string cause) => Record(new EnemyProjectileFact(enemyEntityId, tick, cause));

    internal LoadingBayReceipt ApplyProjectileDamage(ulong enemyEntityId, int damage, ulong tick)
    {
        if (!TryEnemyEntity(enemyEntityId, out _)) return Reject("combat.invalid-projectile-impact");
        LoadingBayE1M1EnemyDefinition enemy = LoadingBayE1M1SemanticCatalog.Enemy(enemyEntityId);
        LoadingBayEnemyStateComponent state = EnemyState(_entityMap.Runtime(enemyEntityId));
        if (state.Posture == LoadingBayEnemyPosture.Defeated || damage <= 0)
            return Reject("combat.invalid-projectile-impact");
        Record(new EnemyAttackFact(enemyEntityId, LoadingBayE1M1EnemyAttackKind.Projectile, true, tick, "combat.projectile-player-impact"));
        return ApplyDamage("player", damage, $"enemy.{enemy.Label}.projectile");
    }

    internal bool CanApplyWeaponStarter(LoadingBayE1M1PickupPlacement pickup)
    {
        if (pickup.StarterAmmunitionItemId is null || pickup.StarterAmmunitionQuantity == 0 || !LoadingBayDefinitions.Weapons.TryGetValue(pickup.ItemId, out LoadingBayWeapon? weapon)) return false;
        LoadingBayItem starterAmmo = LoadingBayDefinitions.Item(pickup.StarterAmmunitionItemId);
        ulong quantity = ItemQuantity(starterAmmo);
        if (quantity > starterAmmo.MechanicsDefinition.MaximumQuantity - pickup.StarterAmmunitionQuantity) return false;
        try
        {
            Mechanics.InventoryEdit candidate = _inventory.Prepare();
            if (!OwnedWeaponIds().Contains(weapon.Id, StringComparer.Ordinal))
            {
                candidate.MaterializeUnique(new Mechanics.ItemState(new EntityId(_entities.NextEntityValue), weapon.MechanicsDefinition), _player);
            }
            candidate.Grant(_player, starterAmmo.MechanicsDefinition, pickup.StarterAmmunitionQuantity);
            candidate.Validate();
            return true;
        }
        catch (Mechanics.MechanicsException) { return false; }
    }

    internal LoadingBayReceipt CollectWeaponStarter(LoadingBayE1M1PickupPlacement pickup)
    {
        string key = CanonicalPickupKey(pickup.EntityId);
        if (PickupState(pickup.EntityId).Lifecycle == LoadingBayPickupLifecycle.Collected) return Reject("pickup.already-collected");
        if (HealthTrack.ValueInt64 == 0) return Reject("pickup.player-defeated");
        if (!CanApplyWeaponStarter(pickup)) return Reject("pickup.not-needed");
        if (pickup.StarterAmmunitionItemId is null || !LoadingBayDefinitions.Weapons.TryGetValue(pickup.ItemId, out LoadingBayWeapon? weapon)) return Reject("pickup.invalid-catalog");
        EntityId? newWeaponEntity = null;
        try
        {
            Mechanics.InventoryEdit candidate = _inventory.Prepare();
            if (!OwnedWeaponIds().Contains(weapon.Id, StringComparer.Ordinal))
            {
                newWeaponEntity = _entities.Create();
                candidate.MaterializeUnique(new Mechanics.ItemState(newWeaponEntity.Value, weapon.MechanicsDefinition), _player);
            }
            candidate.Grant(_player, LoadingBayDefinitions.Item(pickup.StarterAmmunitionItemId).MechanicsDefinition, pickup.StarterAmmunitionQuantity);
            candidate.Publish();
            Record(new PickupCollectedFact(key, pickup.ItemId, pickup.Quantity));
            Record(new PickupLoadoutChangedFact(pickup.EntityId, pickup.ItemId, pickup.ProgramId, false, newWeaponEntity is null ? "pickup.weapon-ammunition" : "pickup.weapon-acquired"));
            return Accept("pickup.collected");
        }
        catch (Mechanics.MechanicsException)
        {
            if (newWeaponEntity is EntityId entity && _entities.IsAlive(entity)) _entities.Destroy(entity);
            return Reject("pickup.inventory-rejected");
        }
    }

    internal void RestoreVitals(long health, long armor, LoadingBayArmorProtection protection)
    {
        HealthTrack.SetCurrent(health);
        ArmorTrack.SetCurrent(armor);
        ArmorProtection = protection;
    }

    internal void RestoreLoadout(string[] desiredWeapons, string? desiredEquipped, LoadingBayWeaponCooldownSnapshot[] cooldowns)
    {
        RestoreWeapons(desiredWeapons, desiredEquipped);
        _weaponReadyAt.Clear();
        foreach (LoadingBayWeaponCooldownSnapshot cooldown in cooldowns) _weaponReadyAt.Add(cooldown.WeaponId, cooldown.ReadyAtTick);
    }

    internal void RestoreEncounters(LoadingBayEncounterSnapshot[] encounters)
    {
        _activatedEncounters.Clear();
        foreach (LoadingBayEncounterSnapshot encounter in encounters.Where(encounter => encounter.Activated)) _activatedEncounters.Add(encounter.EntityId);
    }

    internal LoadingBayEncounterSnapshot[] EncounterSnapshots() => LoadingBayE1M1SemanticCatalog.Encounters
        .Select(encounter => new LoadingBayEncounterSnapshot(encounter.EntityId, _activatedEncounters.Contains(encounter.EntityId), encounter.Members.All(member => EnemyState(_entityMap.Runtime(member)).Posture == LoadingBayEnemyPosture.Defeated)))
        .ToArray();

    internal LoadingBayEnemyReadout[] ActorReadouts() => LoadingBayE1M1SemanticCatalog.Enemies.Select(enemy =>
    {
        EntityId entity = _entityMap.Runtime(enemy.EntityId);
        LoadingBayEnemyStateComponent state = EnemyState(entity);
        return new LoadingBayEnemyReadout(enemy.EntityId, enemy.Label, EnemyHealth(EnemyVitality(entity)), state.Posture, state.Visible, state.ReadyAtTick, enemy.DropPickupEntityId);
    }).ToArray();

    internal LoadingBayActorSnapshot[] ActorSnapshots() => LoadingBayE1M1SemanticCatalog.Enemies.Select(enemy =>
    {
        EntityId entity = _entityMap.Runtime(enemy.EntityId);
        LoadingBayEnemyStateComponent state = EnemyState(entity);
        return new LoadingBayActorSnapshot(enemy.EntityId, EnemyHealth(EnemyVitality(entity)), state.Posture, state.Visible, state.ReadyAtTick);
    }).ToArray();

    internal LoadingBayWeaponCooldownSnapshot[] WeaponCooldowns() => _weaponReadyAt.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new LoadingBayWeaponCooldownSnapshot(pair.Key, pair.Value)).ToArray();

    internal ulong BulletQuantity() => _inventory.Read(_player).Stacks.SingleOrDefault(stack => stack.Definition == LoadingBayDefinitions.Bullets.MechanicsDefinition.Id).Quantity;
    internal ulong ShellQuantity() => _inventory.Read(_player).Stacks.SingleOrDefault(stack => stack.Definition == LoadingBayDefinitions.Shells.MechanicsDefinition.Id).Quantity;
    internal ulong ItemQuantity(LoadingBayItem item) => _inventory.Read(_player).Stacks.SingleOrDefault(stack => stack.Definition == item.MechanicsDefinition.Id).Quantity;

    internal void SetBulletQuantity(ulong quantity)
    {
        ulong current = BulletQuantity();
        if (current < quantity) _inventory.Grant(_player, LoadingBayDefinitions.Bullets.MechanicsDefinition, quantity - current);
        else if (current > quantity) _inventory.Consume(_player, LoadingBayDefinitions.Bullets.MechanicsDefinition, current - quantity);
    }

    internal void SetShellQuantity(ulong quantity)
    {
        ulong current = ShellQuantity();
        if (current < quantity) _inventory.Grant(_player, LoadingBayDefinitions.Shells.MechanicsDefinition, quantity - current);
        else if (current > quantity) _inventory.Consume(_player, LoadingBayDefinitions.Shells.MechanicsDefinition, current - quantity);
    }

    internal string[] OwnedWeaponIds() => _inventory.View(_player).UniqueItems
        .Select(item => WeaponId(item.Definition))
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();

    internal string? EquippedWeaponId()
    {
        if (!_inventory.TryGetEquipment(_player, out Mechanics.EquipmentState? equipment)) throw new InvalidOperationException("Player equipment is unavailable.");
        Mechanics.EquipmentAssignment assignment = equipment!.Assignments.SingleOrDefault(value => value.Slot == LoadingBayDefinitions.WeaponSlot.Id);
        return assignment.Slot is null ? null : WeaponIdForEntity(assignment.Item);
    }

    internal void MaterializeWeapon(LoadingBayWeapon weapon)
    {
        EntityId entity = _entities.Create();
        _inventory.MaterializeUnique(new Mechanics.ItemState(entity, weapon.MechanicsDefinition), _player);
    }

    internal void EquipWeapon(string weapon)
    {
        _inventory.Equip(_player, WeaponEntity(weapon), [LoadingBayDefinitions.WeaponSlot]);
    }

    private EntityId WeaponEntity(string weapon) => _inventory.View(_player).UniqueItems
        .Where(item => WeaponId(item.Definition) == weapon)
        .Select(item => item.Entity)
        .Single();

    private string WeaponIdForEntity(EntityId entity)
    {
        if (!_inventory.TryGetItem(entity, out Mechanics.ItemState? item) || item is null) throw new InvalidOperationException("Equipped weapon item is unavailable.");
        return WeaponId(item.Definition.Id);
    }

    private static string WeaponId(Mechanics.ItemDefinitionId definition) => LoadingBayDefinitions.Weapons.Values
        .Single(weapon => weapon.MechanicsDefinition.Id == definition).Id;

    private void RestoreEquippedWeapon(string? desired)
    {
        string? current = EquippedWeaponId();
        if (current == desired) return;
        Mechanics.InventoryEdit candidate = _inventory.Prepare();
        if (current is null) candidate.Equip(_player, WeaponEntity(desired!), [LoadingBayDefinitions.WeaponSlot]);
        else if (desired is null) candidate.Unequip(_player, WeaponEntity(current));
        else candidate.Swap(_player, WeaponEntity(current), WeaponEntity(desired), [LoadingBayDefinitions.WeaponSlot]);
        candidate.Publish();
    }

    private void RestoreWeapons(string[] desiredWeapons, string? desiredEquipped)
    {
        Dictionary<string, EntityId> current = _inventory.View(_player).UniqueItems
            .ToDictionary(item => WeaponId(item.Definition), item => item.Entity, StringComparer.Ordinal);
        HashSet<string> target = desiredWeapons.ToHashSet(StringComparer.Ordinal);
        List<EntityId> created = [];
        List<EntityId> removed = [];
        try
        {
            Mechanics.InventoryEdit candidate = _inventory.Prepare();
            string? equipped = EquippedWeaponId();
            if (equipped is not null && !target.Contains(equipped)) candidate.Unequip(_player, current[equipped]);
            foreach ((string weaponId, EntityId entity) in current.Where(pair => !target.Contains(pair.Key)).ToArray())
            {
                candidate.DestroyUnique(entity);
                removed.Add(entity);
                current.Remove(weaponId);
            }
            foreach (string weaponId in target.Where(id => !current.ContainsKey(id)))
            {
                EntityId entity = _entities.Create();
                created.Add(entity);
                candidate.MaterializeUnique(new Mechanics.ItemState(entity, LoadingBayDefinitions.Weapons[weaponId].MechanicsDefinition), _player);
                current.Add(weaponId, entity);
            }
            if (desiredEquipped != equipped)
            {
                if (desiredEquipped is null && equipped is not null && current.ContainsKey(equipped)) candidate.Unequip(_player, current[equipped]);
                else if (desiredEquipped is not null && (equipped is null || !current.ContainsKey(equipped))) candidate.Equip(_player, current[desiredEquipped], [LoadingBayDefinitions.WeaponSlot]);
                else if (desiredEquipped is not null && equipped is not null) candidate.Swap(_player, current[equipped], current[desiredEquipped], [LoadingBayDefinitions.WeaponSlot]);
            }
            candidate.Publish();
            foreach (EntityId entity in removed) if (_entities.IsAlive(entity)) _entities.Destroy(entity);
        }
        catch
        {
            foreach (EntityId entity in created) if (_entities.IsAlive(entity)) _entities.Destroy(entity);
            throw;
        }
    }

    private LoadingBayPickupStateComponent PickupState(ulong pickupEntityId) =>
        _entities.Get<LoadingBayPickupStateComponent>(_entityMap.Runtime(pickupEntityId));

    private static string CanonicalPickupKey(ulong entityId) => $"e1m1.pickup.{entityId}";
}
