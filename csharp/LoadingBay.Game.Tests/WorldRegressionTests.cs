using System.Numerics;
using LoadingBay.Game;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Xunit;

public sealed class WorldRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlatformContinuationUsesTheProjectedRuntimeIdentity(bool reverseAllocation)
    {
        using var entities = new EntityStore([EngineComponentTypes.Transform, EngineComponentTypes.Kinematic, EngineComponentTypes.SpatialCollider]);
        var order = Enumerable.Range(1, (int)LoadingBayE1M1SemanticCatalog.CanonicalEntityCount).Select(id => (ulong)id).ToArray();
        if (reverseAllocation) Array.Reverse(order);
        var map = LoadingBayEntityMap.Bootstrap(entities, order);
        var definition = LoadingBayE1M1SemanticCatalog.Lifts.First();
        var lift = map.Runtime(definition.PlatformEntityId);
        var advanced = new Transform(new Vector3(112, 6, 76), Quaternion.Identity, Vector3.One);
        entities.Set(lift, EngineComponentTypes.Transform, advanced);
        entities.Set(lift, EngineComponentTypes.Kinematic, new Kinematic(Vector3.One, -Vector3.UnitY));
        entities.Set(lift, EngineComponentTypes.SpatialCollider, new SpatialCollider(-Vector3.One, Vector3.One, 2, uint.MaxValue, true, false, false));
        HashSet<ulong> platforms = [definition.PlatformEntityId];
        var obstacle = Assert.Single(LoadingBayWorldInteractionCoordinator.ProjectPlatformObstacles(platforms, entities, map));
        Assert.Equal(lift.Value, obstacle.Entity);
        if (reverseAllocation) Assert.NotEqual(definition.PlatformEntityId, obstacle.Entity);

        // CharacterStep carries this projected runtime identity into the next support query.
        var support = LoadingBayWorldInteractionCoordinator.ResolvePlatformSupport(true, obstacle.Entity, platforms, entities, map);
        Assert.True(support.Present);
        Assert.Equal(CharacterSupportLifecycle.Active, support.Lifecycle);
        Assert.Equal(obstacle.Entity, support.Entity);
        Assert.Equal(advanced, support.Transform);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public void DoorArrivesOnItsDueTick(int batchSize, bool reverseAllocation)
    {
        using var entities = new EntityStore([EngineComponentTypes.Transform, EngineComponentTypes.Kinematic, EngineComponentTypes.SpatialCollider]);
        var order = Enumerable.Range(1, (int)LoadingBayE1M1SemanticCatalog.CanonicalEntityCount).Select(id => (ulong)id).ToArray();
        if (reverseAllocation) Array.Reverse(order);
        var map = LoadingBayEntityMap.Bootstrap(entities, order);
        var world = new LoadingBayWorldState(entities, map);
        var definition = LoadingBayE1M1SemanticCatalog.Doors.First();
        var door = map.Runtime(definition.EntityId);
        entities.Set(door, EngineComponentTypes.Transform, new Transform(definition.ClosedTranslation, Quaternion.Identity, Vector3.One));
        entities.Set(door, EngineComponentTypes.Kinematic, new Kinematic(Vector3.One, Vector3.Zero));
        entities.Set(door, EngineComponentTypes.SpatialCollider, new SpatialCollider(-Vector3.One, Vector3.One, 2, uint.MaxValue, true, false, false));
        var moving = world.ActivateDoor(definition.EntityId, 1, _ => { });
        var service = new FreeKinematicService();
        var motion = new EntityKinematicMotion(entities, service, EngineComponentTypes.SpatialCollider);
        using var spatial = new SpatialSession(default, () => { });
        for (ulong batch = 2; batch <= moving.DueStep; batch += (ulong)batchSize)
        {
            for (ulong tick = batch; tick < batch + (ulong)batchSize && tick <= moving.DueStep; tick++)
            {
                // Same production order: motion first, then the semantic due transition.
                LoadingBayWorldInteractionCoordinator.RealizeMotion(tick, 1f / 60f, world, entities, map, motion, spatial);
                world.Advance(tick, _ => { });
                if (tick < moving.DueStep) Assert.Equal(LoadingBayDoorState.Opening, world.DoorState(definition.EntityId).State);
            }
        }
        Assert.Equal(LoadingBayDoorState.Open, world.DoorState(definition.EntityId).State);
        Assert.InRange(Vector3.Distance(definition.OpenTranslation, entities.Get(door, EngineComponentTypes.Transform).Translation), 0, .0001f);
    }

    [Theory]
    [InlineData(10f)]
    [InlineData(float.NaN)]
    public void InvalidLookRejectsBeforeApplyingGameplay(float pitch)
    {
        using var game = new LoadingBaySession();
        var saved = game.Capture("doom-e1m1");
        Assert.True(game.ApplyDamage("player", 20, "regression").Accepted);
        long current = game.Readout().Health;
        var invalid = saved with { Player = saved.Player with { Look = new LookState(0, pitch) } };
        Assert.False(game.Restore(invalid, "doom-e1m1").Accepted);
        Assert.Equal(current, game.Readout().Health);
    }

    [Fact]
    public void ExplodedBarrelLeavesEnemyInTheShotRaycastCandidates()
    {
        using var entities = new EntityStore();
        var map = LoadingBayEntityMap.Bootstrap(entities);
        var world = new LoadingBayWorldState(entities, map);
        var barrel = LoadingBayE1M1SemanticCatalog.Barrels.First();
        var enemy = LoadingBayE1M1SemanticCatalog.Enemies.First();
        SpatialEntityCollider[] barrels = [new(barrel.EntityId, Vector3.Zero, Vector3.One, 2, uint.MaxValue, true, false, false)];
        SpatialEntityCollider[] enemies = [new(enemy.EntityId, Vector3.UnitZ * 5, Vector3.UnitZ * 6, 2, uint.MaxValue, true, false, false)];
        HashSet<ulong> eligible = [enemy.EntityId];
        Assert.Equal(2, LoadingBayCombatCoordinator.ShotHitboxes(enemies, barrels, eligible, world).Length);
        world.DamageBarrel(barrel.EntityId, barrel.MaximumHealth, 1, (_, _) => false, _ => { });
        var candidates = LoadingBayCombatCoordinator.ShotHitboxes(enemies, barrels, eligible, world);
        Assert.Equal(enemy.EntityId, Assert.Single(candidates).Entity);
    }

    // A call-local Engine-service double applies free kinematics; it owns no product rules.
    private sealed class FreeKinematicService : IKinematicService
    {
        public IntegrationResult Integrate(KinematicIntegrationRequest request) => throw new NotSupportedException();
        public IntegrationResult IntegrateSpatial(KinematicSpatialIntegrationRequest request) => throw new NotSupportedException();
        public KinematicMotionResult RunMotion(KinematicMotionRequest request)
        {
            var selected = request.SelectedEntityIds.ToArray().ToHashSet();
            var rows = request.Rows.ToArray().Where(row => !request.SelectionPresent || selected.Contains(row.EntityId)).ToArray();
            var candidates = rows.Select(row => new KinematicMotionCandidate(row.EntityId, row.Transform,
                row.Transform with { Translation = row.Transform.Translation + row.Velocity * request.DeltaSeconds }, row.Velocity, row.Velocity)).ToArray();
            return new(candidates, ReadOnlyMemory<KinematicMotionFact>.Empty, (ulong)rows.Length, (ulong)rows.Length, 0);
        }
    }
}
