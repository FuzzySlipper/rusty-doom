using System.Numerics;
using Rusty.Engine;

namespace LoadingBay.Game;

internal enum RecipePickupKind { Bullets, Shells, Stimpack, Medikit, Armor, Shotgun }
internal sealed record RecipePickup(ulong Id, RecipePickupKind Kind, Vector3 Position, string Sprite);
internal sealed class RecipeEnemy(ulong id, Vector3 position, bool imp)
{
    internal readonly Vector3 Spawn = position;
    internal ulong Id = id;
    internal Vector3 Position = position;
    internal readonly bool Imp = imp;
    internal int Health = imp ? 60 : 30;
    internal double ReadyAt;
    internal double AttackStarted = double.NegativeInfinity;
    internal double PainUntil, DeathStarted;
    internal bool AttackPending, Moving;
    internal string? PublishedFrame;
    internal bool Awake;
    internal CharacterMotion Motion;
    internal ulong Sequence;
    internal double AttackDuration => LoadingBayRecipeAnimation.Duration(Imp ? LoadingBayRecipeAnimation.ImpAttack : LoadingBayRecipeAnimation.TrooperAttack);
    internal string Sprite(double time)
    {
        if (Health <= 0) return LoadingBayRecipeAnimation.At(Imp ? LoadingBayRecipeAnimation.ImpDeath : LoadingBayRecipeAnimation.TrooperDeath, time - DeathStarted, true)!;
        if (time < PainUntil) return Imp ? "TROOH1" : "POSSG1";
        if (LoadingBayRecipeAnimation.At(Imp ? LoadingBayRecipeAnimation.ImpAttack : LoadingBayRecipeAnimation.TrooperAttack, time - AttackStarted) is { } attack) return attack;
        int frame = Moving ? (int)(time / ((Imp ? 6 : 8) * LoadingBayRecipeAnimation.Tic)) % 4 : (int)(time / (10 * LoadingBayRecipeAnimation.Tic)) % 2;
        return (Imp ? "TROO" : "POSS") + (char)('A' + frame) + "1";
    }
}

/// <summary>Recipe-owned placements and combat rules; Engine owns all collision and overlap queries.</summary>
internal sealed class LoadingBayRecipeGameplay : IDisposable
{
    internal const double DiagnosticsInterval = .25;
    private const double EnemyInterval = .05;
    private readonly IEngineContext _engine;
    private readonly DynamicsWorld _projectileWorld;
    private sealed class Fireball(ulong id, DynamicsBody body, Vector3 position, Vector3 impulse, double expires)
    {
        internal ulong Id = id; internal DynamicsBody Body = body; internal Vector3 Position = position;
        internal Vector3 Impulse = impulse; internal double Expires = expires;
    }
    private readonly List<Fireball> _fireballs = [];
    private sealed record Impact(ulong Id, Vector3 Position, double Started);
    private readonly List<Impact> _impacts = [];
    private ulong _nextFireball = 34000;
    private readonly LoadingBayPlayerScene _player;
    private readonly LoadingBayStudyDoor[] _doors;
    private readonly Dictionary<string, RenderResource> _textures = [];
    private readonly Dictionary<string, Appearance> _sprites = [];
    private readonly HashSet<ulong> _collected = [];
    private readonly HashSet<ulong> _overlapping = [];
    private readonly SpatialEntityCollider[] _triggerColliders;
    private readonly CharacterControllerConfig _enemyController;
    private double _time, _nextEnemy, _nextHazard, _readyAt, _damageUntil, _messageUntil;
    private bool _lastShot, _lastDamage;
    private double _weaponStarted = double.NegativeInfinity;
    private bool _weaponPending;
    private RecipeWeapon _firingWeapon;
    internal const float PunchReach = 2f;
    internal const int PunchDamage = 20;
    private string? _publishedWeaponFrame, _publishedFlashFrame;
    private ulong _revision;
    internal ulong Revision => _revision;
    internal ulong TriggerPasses { get; private set; }
    internal int Health { get; private set; } = 100;
    internal int Armor { get; private set; }
    internal int Bullets { get; private set; } = 50;
    internal int Shells { get; private set; }
    internal bool HasShotgun { get; private set; }
    internal RecipeWeapon SelectedWeapon { get; private set; } = RecipeWeapon.Pistol;
    internal bool Complete { get; private set; }
    internal bool Dead => Health <= 0;
    internal bool WeaponFlash => FlashFrame is not null;
    private string WeaponFrame => LoadingBayRecipeAnimation.At(LoadingBayRecipeAnimation.Frames(SelectedWeapon), _time - _weaponStarted) ?? LoadingBayRecipeAnimation.Idle(SelectedWeapon);
    private string? FlashFrame => _firingWeapon == RecipeWeapon.Fist ? null : LoadingBayRecipeAnimation.At(_firingWeapon == RecipeWeapon.Shotgun ? LoadingBayRecipeAnimation.ShotgunFlash : LoadingBayRecipeAnimation.PistolFlash, _time - _weaponStarted - LoadingBayRecipeAnimation.FireDelay(_firingWeapon));
    internal bool DamageFlash => _time < _damageUntil;
    internal int Kills => Enemies.Count(e => e.Health <= 0);
    internal int Collected => _collected.Count;
    internal bool IsCollected(ulong pickupId) => _collected.Contains(pickupId);
    internal string Describe() => $"weapon={Weapon};shells={Shells};fireballs={_fireballs.Count};actors=" + string.Join('|', Enemies.Select(e => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{e.Id}:{e.Health}@{e.Position.X:R},{e.Position.Y:R},{e.Position.Z:R}")));
    internal string Weapon => SelectedWeapon.ToString();
    internal string Message { get; private set; } = "Find supplies. Reach the southern terminal. E opens doors.";
    internal bool GeometryDirty { get; set; } = true;
    internal static readonly Vector3 ExitPosition = new(54, -.75f, 40);
    // Positions are recipe-world coordinates, deliberately refined for the authored traversal.
    internal readonly RecipePickup[] Pickups = [
        new(30001, RecipePickupKind.Bullets, new(-7,0,-2), "CLIPA0"),
        new(30002, RecipePickupKind.Armor, new(-7,-.5f,-9), "ARM1A0"),
        new(30003, RecipePickupKind.Shotgun, new(-27,-.25f,-9), "SHOTA0"),
        new(30004, RecipePickupKind.Shells, new(-25,-.25f,-9), "SHELA0"),
        new(30005, RecipePickupKind.Stimpack, new(-15,0,-16), "STIMA0"),
        new(30006, RecipePickupKind.Bullets, new(0,0,-25), "AMMOA0"),
        new(30007, RecipePickupKind.Shotgun, new(16,0,-32), "SHOTA0"),
        new(30008, RecipePickupKind.Medikit, new(20,0,-31), "MEDIA0"),
        new(30009, RecipePickupKind.Shells, new(31,.75f,-32), "SBOXA0"),
        new(30010, RecipePickupKind.Bullets, new(50,-.75f,-20), "CLIPA0"),
        new(30011, RecipePickupKind.Stimpack, new(54,-.75f,6), "STIMA0"),
        new(30012, RecipePickupKind.Shells, new(54,-.75f,14), "SHELA0"),
        new(30013, RecipePickupKind.Medikit, new(54,-.75f,24), "MEDIA0"),
        new(30014, RecipePickupKind.Armor, new(66.5f,3.25f,-17), "ARM2A0"),
        new(30015, RecipePickupKind.Shells, new(26,-1.75f,8), "SBOXA0"),
        new(30016, RecipePickupKind.Bullets, new(38,-1.75f,10), "AMMOA0")];
    internal readonly RecipeEnemy[] Enemies = [
        new(31001,new(0,0,-25),false), new(31002,new(-29,-.25f,-10),false),
        new(31003,new(22,0,-32),false), new(31004,new(31,.75f,-31),false),
        new(31005,new(56,-.75f,-16),true), new(31006,new(58,-.75f,-8),true),
        new(31007,new(54,-.75f,12),false), new(31008,new(54,-.75f,27),true),
        new(31009,new(54,-.75f,39),false), new(31010,new(72,3.25f,-17),true),
        new(31011,new(26,-1.75f,11),false), new(31012,new(39,-1.75f,10),true)];

    internal LoadingBayRecipeGameplay(IEngineContext engine, LoadingBayPlayerScene player, LoadingBayStudyDoor[] doors)
    {
        _engine = engine; _player = player; _doors = doors;
        _projectileWorld = engine.Dynamics.CreateWorld(new(Vector3.Zero));
        // Fireballs use explicit continuous Spatial sweeps below. Binding the full room
        // into Dynamics as well would duplicate collision work on every physics step.
        var config = engine.Spatial.DefaultCharacterControllerConfig();
        _enemyController = config with
        {
            Shape = config.Shape with { StandingHeight = 1.7f, CrouchedHeight = 1.1f, Radius = .35f },
            Ground = config.Ground with { ForwardSpeed = 1.4f, BackwardSpeed = 1.4f, StrafeSpeed = 1.4f },
            Surface = config.Surface with { MaximumStepHeight = .3f },
        };
        _triggerColliders = new SpatialEntityCollider[Pickups.Length + 2];
        for (int i = 0; i < Pickups.Length; i++)
        {
            var p = Pickups[i];
            _triggerColliders[i] = new(p.Id, p.Position - new Vector3(.45f, .1f, .45f), p.Position + new Vector3(.45f, 1.1f, .45f), 0, 0, true, true, true);
            engine.Spatial.RegisterTrigger(new(player.Session, p.Id, "recipe", "pickup", SpatialTriggerGeometry.EntityBounds));
        }
        // Low liquid surface only: the raised zigzag walkway remains safe.
        _triggerColliders[^2] = new(32000, new(48, -1.6f, -22), new(69, -1.35f, 16), 0, 0, true, true, true);
        engine.Spatial.RegisterTrigger(new(player.Session, 32000, "recipe", "hazard", SpatialTriggerGeometry.EntityBounds));
    }

    internal void Advance(float seconds, ulong tick)
    {
        _time += seconds;
        if (_weaponPending && _time >= _weaponStarted + LoadingBayRecipeAnimation.FireDelay(_firingWeapon))
        { _weaponPending = false; if (!Dead && !Complete) Shoot(_firingWeapon); }
        if (!Dead && !Complete)
        {
            _triggerColliders[^1] = new(1, _player.Position - new Vector3(.3f, .9f, .3f), _player.Position + new Vector3(.3f, .9f, .3f), 1, uint.MaxValue, true, false, false);
            var receipt = _engine.Spatial.ReconcileTriggers(new(_player.Session, tick, SpatialTriggerCause.Movement, _triggerColliders));
            TriggerPasses++;
            for (uint i = 0; i < receipt.FactCount; i++)
            {
                var fact = _engine.Spatial.ReadTriggerFactAt(new(_player.Session, i));
                if (!fact.Present || fact.Subject != 1) continue;
                if (fact.Enter) _overlapping.Add(fact.Trigger); else _overlapping.Remove(fact.Trigger);
            }
            // Engine facts own membership. Retrying an ineligible pickup lets damage/ammo use
            // make a stationary overlap eligible without another projection/reconcile pass.
            foreach (var pickup in Pickups)
                if (_overlapping.Contains(pickup.Id) && !_collected.Contains(pickup.Id) && Collect(pickup.Kind))
                {
                    _collected.Add(pickup.Id);
                    _overlapping.Remove(pickup.Id);
                    var before = _engine.Spatial.ReadTrigger(new(_player.Session, pickup.Id));
                    _engine.Spatial.SetTriggerActive(new(_player.Session, pickup.Id, before.Revision, false, tick));
                    GeometryDirty = true;
                }
            if (_overlapping.Contains(32000) && _time >= _nextHazard)
            { _nextHazard = _time + 1; Damage(5, "Toxic waste! Find the raised walkway."); }
            AdvanceFireballs(seconds);
            if (_time >= _nextEnemy)
            { _nextEnemy = _time + EnemyInterval; AdvanceEnemies(); }
        }
        foreach (var enemy in Enemies)
        {
            string frame = enemy.Sprite(_time);
            if (frame != enemy.PublishedFrame) { enemy.PublishedFrame = frame; GeometryDirty = true; }
        }
        if (_impacts.RemoveAll(i => _time - i.Started >= LoadingBayRecipeAnimation.Duration(LoadingBayRecipeAnimation.FireballImpact)) > 0 || _impacts.Count > 0) GeometryDirty = true;
        if (_publishedWeaponFrame != WeaponFrame || _publishedFlashFrame != FlashFrame)
        { _publishedWeaponFrame = WeaponFrame; _publishedFlashFrame = FlashFrame; GeometryDirty = true; }
        if (_lastShot != WeaponFlash || _lastDamage != DamageFlash)
        { _lastShot = WeaponFlash; _lastDamage = DamageFlash; _revision++; GeometryDirty = true; }
        if (_messageUntil > 0 && _time >= _messageUntil && !Dead && !Complete)
        { Message = ""; _messageUntil = 0; _revision++; }
    }

    internal void Restart()
    {
        foreach (var ball in _fireballs) ball.Body.Dispose();
        _fireballs.Clear(); _impacts.Clear();
        _collected.Clear(); _overlapping.Clear();
        _time = _nextEnemy = _nextHazard = _readyAt = _damageUntil = _messageUntil = 0;
        _lastShot = _lastDamage = false;
        _weaponStarted = double.NegativeInfinity; _weaponPending = false;
        _publishedWeaponFrame = _publishedFlashFrame = null;
        Health = 100; Armor = 0; Bullets = 50; Shells = 0;
        HasShotgun = Complete = false; SelectedWeapon = RecipeWeapon.Pistol;
        Message = "Find supplies. Reach the southern terminal. E opens doors.";
        foreach (var enemy in Enemies)
        {
            enemy.Position = enemy.Spawn; enemy.Health = enemy.Imp ? 60 : 30;
            enemy.ReadyAt = enemy.PainUntil = enemy.DeathStarted = 0; enemy.Awake = enemy.AttackPending = enemy.Moving = false;
            enemy.AttackStarted = double.NegativeInfinity; enemy.PublishedFrame = null;
            enemy.Motion = default; enemy.Sequence = 0;
        }
        _triggerColliders[^1] = new(1, _player.Position - new Vector3(.3f, .9f, .3f), _player.Position + new Vector3(.3f, .9f, .3f), 1, uint.MaxValue, true, false, false);
        var before = _engine.Spatial.ReadTrigger(new(_player.Session, Pickups[0].Id));
        _engine.Spatial.RestoreTriggers(new(_player.Session, before.Revision,
            Pickups.Select(p => p.Id).Append(32000UL).ToArray(), _triggerColliders));
        // Spawn is outside every trigger, so the restored overlap baseline is empty.
        GeometryDirty = true; _revision++;
    }

    private bool Collect(RecipePickupKind kind)
    {
        switch (kind)
        {
            case RecipePickupKind.Bullets when Bullets < 200: Bullets = Math.Min(200, Bullets + 20); break;
            case RecipePickupKind.Shells when Shells < 50: Shells = Math.Min(50, Shells + 8); break;
            case RecipePickupKind.Stimpack when Health < 100: Health = Math.Min(100, Health + 10); break;
            case RecipePickupKind.Medikit when Health < 100: Health = Math.Min(100, Health + 25); break;
            case RecipePickupKind.Armor when Armor < 100: Armor = 100; break;
            case RecipePickupKind.Shotgun when !HasShotgun || Shells < 50:
                HasShotgun = true; if (_time >= _readyAt) SelectWeapon(RecipeWeapon.Shotgun); Shells = Math.Min(50, Shells + 8); break;
            default: return false;
        }
        Say($"Picked up {kind}."); return true;
    }

    internal void SelectWeapon(RecipeWeapon weapon)
    { if (_time < _readyAt || (weapon == RecipeWeapon.Shotgun && !HasShotgun)) return; SelectedWeapon = weapon; _weaponStarted = double.NegativeInfinity; _revision++; GeometryDirty = true; }
    internal void Fire()
    {
        if (Dead || Complete || _time < _readyAt) return;
        if ((SelectedWeapon == RecipeWeapon.Shotgun && Shells == 0) || (SelectedWeapon == RecipeWeapon.Pistol && Bullets == 0)) { Say("Out of ammo. 1 fist / 2 pistol / 3 shotgun."); return; }
        _firingWeapon = SelectedWeapon;
        _weaponStarted = _time; _weaponPending = true;
        _readyAt = _time + LoadingBayRecipeAnimation.Duration(LoadingBayRecipeAnimation.Frames(SelectedWeapon));
        GeometryDirty = true;
    }
    private void Shoot(RecipeWeapon weapon)
    {
        bool shotgun = weapon == RecipeWeapon.Shotgun, fist = weapon == RecipeWeapon.Fist;
        if (shotgun) Shells--; else if (!fist) Bullets--;
        _revision++;
        var colliders = CombatColliders();
        int pellets = shotgun ? 7 : 1;
        for (int i = 0; i < pellets; i++)
        {
            Vector3 direction = _player.Forward;
            if (pellets > 1) direction = Vector3.Transform(direction, Quaternion.CreateFromAxisAngle(Vector3.UnitY, (i - 3) * .018f));
            var hit = _engine.Spatial.CastRay(new(_player.Session, Eye, direction, fist ? PunchReach : 70, new(1, uint.MaxValue), colliders, new ulong[] { 1 }, colliders));
            if (!hit.Present || hit.Kind != SpatialHitKind.Entity) continue;
            var enemy = Enemies.FirstOrDefault(e => e.Id == hit.Entity && e.Health > 0);
            if (enemy is null) continue;
            enemy.Health = Math.Max(0, enemy.Health - (fist ? PunchDamage : shotgun ? 12 : 15)); enemy.Awake = true;
            enemy.AttackPending = false; enemy.AttackStarted = double.NegativeInfinity;
            enemy.Moving = false;
            if (enemy.Health == 0) { enemy.DeathStarted = _time; Say(enemy.Imp ? "Imp down." : "Trooper down."); }
            else { enemy.PainUntil = _time + (enemy.Imp ? 4 : 6) * LoadingBayRecipeAnimation.Tic; if (fist) Say("Punch hit!"); }
            GeometryDirty = true;
        }
    }

    private Vector3 Eye => _player.Position + Vector3.UnitY * _player.Tuning.EyeOffsetFromCenter;
    private SpatialEntityCollider[] CombatColliders() => Enemies.Where(e => e.Health > 0).Select(e => new SpatialEntityCollider(e.Id, e.Position - new Vector3(.4f, 0, .4f), e.Position + new Vector3(.4f, 1.75f, .4f), 1, uint.MaxValue, true, false, false))
        .Concat(_doors.Select(d => new SpatialEntityCollider(d.Definition.Entity, d.Definition.Min + d.Placement.Translation, d.Definition.Max + d.Placement.Translation, 1, uint.MaxValue, true, true, false))).ToArray();
    private void AdvanceEnemies()
    {
        foreach (var enemy in Enemies)
        {
            enemy.Moving = false;
            if (enemy.Health <= 0 || _time < enemy.PainUntil) continue;
            Vector3 origin = enemy.Position + Vector3.UnitY * 1.3f;
            Vector3 offset = Eye - origin;
            float distance = offset.Length();
            if (distance < .01f) continue;
            if (_time - enemy.AttackStarted < enemy.AttackDuration)
            {
                if (enemy.AttackPending && _time - enemy.AttackStarted >= (enemy.Imp ? 16 : 10) * LoadingBayRecipeAnimation.Tic)
                {
                    enemy.AttackPending = false;
                    if (enemy.Imp) SpawnFireball(origin, Vector3.Normalize(offset));
                    else
                    {
                        var blockers = _doors.Select(d => new SpatialEntityCollider(d.Definition.Entity, d.Definition.Min + d.Placement.Translation, d.Definition.Max + d.Placement.Translation, 1, uint.MaxValue, true, true, false)).ToArray();
                        if (!_engine.Spatial.CastSegment(new(_player.Session, origin, Eye, new(1, uint.MaxValue), blockers, ReadOnlyMemory<ulong>.Empty, blockers)).Present) Damage(5, "Under fire!");
                    }
                }
                continue;
            }
            if (distance > 25) continue;
            var doors = _doors.Select(d => new SpatialEntityCollider(d.Definition.Entity, d.Definition.Min + d.Placement.Translation, d.Definition.Max + d.Placement.Translation, 1, uint.MaxValue, true, true, false)).ToArray();
            var hit = _engine.Spatial.CastSegment(new(_player.Session, origin, Eye, new(1, uint.MaxValue), doors, ReadOnlyMemory<ulong>.Empty, doors));
            if (hit.Present) continue;
            if (!enemy.Awake) { enemy.Awake = true; enemy.ReadyAt = _time + 1.2; }
            if (distance > (enemy.Imp ? 7 : 10))
            {
                enemy.Moving = true;
                float yaw = MathF.Atan2(offset.X, -offset.Z);
                // This call supplies this actor's motion and sequence. Player continuation is
                // captured immediately after the player proposal, before this callback runs.
                var step = _engine.Spatial.ProposeCharacterStep(new(_player.Session, enemy.Position + Vector3.UnitY * .85f, enemy.Motion, default, _doors.Select(d => d.Obstacle).ToArray(), _enemyController,
                    new(new Vector2(0, 1), yaw, false, false, false, Vector3.Zero, Vector3.Zero, (float)EnemyInterval, ++enemy.Sequence)));
                enemy.Position = step.Transform.Translation - Vector3.UnitY * .85f; enemy.Motion = step.Motion;
                GeometryDirty = true;
            }
            if (_time < enemy.ReadyAt || distance > 18) continue;
            enemy.ReadyAt = _time + (enemy.Imp ? 2.2 : 1.8);
            enemy.AttackStarted = _time; enemy.AttackPending = true; enemy.Moving = false;
            GeometryDirty = true;
        }
    }
    private void SpawnFireball(Vector3 origin, Vector3 direction)
    {
        if (_fireballs.Count >= 32) return;
        DynamicsBodyProperties properties = new(1, new(DynamicsMassPolicyKind.DeriveFromShapeAndMass, default), Vector3.Zero, Vector3.Zero,
            new(false, false, false, false, false, false), 0, 0, 0, 0, 0, 1, 0, true, false, false);
        var body = _engine.Dynamics.CreateSphereBodyWithProperties(new(_projectileWorld, new(new(origin, Quaternion.Identity, Vector3.One), .15f, properties)));
        _fireballs.Add(new(++_nextFireball, body, origin, direction * 8, _time + 5));
    }
    private void AdvanceFireballs(float seconds)
    {
        if (_fireballs.Count == 0) return;
        var receipt = _engine.Dynamics.StepAndRead(new(_projectileWorld, seconds, 1,
            _fireballs.Select(p => new DynamicsAction(p.Body, Vector3.Zero, Vector3.Zero, p.Impulse, Vector3.Zero, true)).ToArray(),
            _fireballs.Select(p => p.Body).ToArray()));
        var targets = _doors.Select(d => new SpatialEntityCollider(d.Definition.Entity, d.Definition.Min + d.Placement.Translation, d.Definition.Max + d.Placement.Translation, 1, uint.MaxValue, true, true, false))
            .Append(new SpatialEntityCollider(1, _player.Position - new Vector3(.3f, .9f, .3f), _player.Position + new Vector3(.3f, .9f, .3f), 1, uint.MaxValue, true, false, false)).ToArray();
        for (int i = _fireballs.Count - 1; i >= 0; i--)
        {
            var p = _fireballs[i]; p.Impulse = Vector3.Zero;
            DynamicsStepAndReadBody? state = null;
            foreach (var row in receipt.Bodies.Span) if (row.Body.Value == p.Body.Handle.Value) state = row;
            if (state is null) throw new InvalidOperationException("Engine omitted an active recipe projectile.");
            Vector3 next = state.Value.Readout.Transform.Translation;
            var hit = _engine.Spatial.CastCapsule(new(_player.Session, p.Position, .15, .15, next - p.Position, 0, new(1, uint.MaxValue), targets, ReadOnlyMemory<ulong>.Empty));
            if (hit.Present || _time >= p.Expires)
            {
                if (hit.Present && hit.Kind == SpatialHitKind.Entity && hit.Entity == 1) Damage(8, "Imp fireball!");
                if (hit.Present) _impacts.Add(new(p.Id, p.Position, _time));
                p.Body.Dispose(); _fireballs.RemoveAt(i);
            }
            else p.Position = next;
        }
        GeometryDirty = true;
    }
    internal void Use()
    {
        if (Dead || Complete) return;
        if (Vector3.Distance(_player.Position, ExitPosition + Vector3.UnitY) < 2.5f)
        { Complete = true; Say($"Hangar clear — {Kills}/{Enemies.Length} kills, {Collected}/{Pickups.Length} supplies. R to restart."); }
    }
    private void Damage(int damage, string message)
    {
        int absorbed = Math.Min(Armor, damage / 3); Armor -= absorbed; Health = Math.Max(0, Health - damage + absorbed);
        _damageUntil = _time + .2; Say(Dead ? "You died. Press R to restart." : message);
    }
    internal bool SetTrack(string track, int value)
    {
        if (value < 0 || value > 100) return false;
        if (track == "health") Health = value; else if (track == "armor") Armor = value; else return false;
        Say($"Developer: {track} = {value}"); return true;
    }
    private void Say(string message) { Message = message; _messageUntil = _time + 3; _revision++; }
    internal IEnumerable<AppearanceFact> Appearances()
    {
        yield return Fact(35000, Vector3.Zero, "view/" + WeaponFrame);
        if (FlashFrame is { } flash) yield return Fact(35001, Vector3.Zero, "view/" + flash);
        foreach (var pickup in Pickups)
            if (!_collected.Contains(pickup.Id)) yield return Fact(pickup.Id, pickup.Position, pickup.Sprite);
        foreach (var enemy in Enemies) yield return Fact(enemy.Id, enemy.Position, enemy.Sprite(_time));
        foreach (var ball in _fireballs) yield return Fact(ball.Id, ball.Position, (int)(_time / (4 * LoadingBayRecipeAnimation.Tic)) % 2 == 0 ? "BAL1A0" : "BAL1B0");
        foreach (var impact in _impacts)
            if (LoadingBayRecipeAnimation.At(LoadingBayRecipeAnimation.FireballImpact, _time - impact.Started) is { } frame) yield return Fact(impact.Id, impact.Position, frame);
        // Retained authored exit marker; E at the terminal completes the run.
        yield return Fact(33000, new(54, .3f, 41.7f), "exit-sign");
    }
    private AppearanceFact Fact(ulong id, Vector3 position, string name)
    {
        bool viewmodel = id is 35000 or 35001;
        var frame = name == "exit-sign" ? new RecipeSpriteFrame("EXITSIGN.png", Vector2.Zero, Vector2.One, new(1.5f, .6f), new(.5f, 0)) : LoadingBayRecipeSprites.Frame(name);
        // Engine pivots are normalized. Doom sometimes places its origin below the
        // patch; preserve that offset as a world translation instead of an invalid pivot.
        Vector2 pivot = Vector2.Clamp(frame.Pivot, Vector2.Zero, Vector2.One);
        if (!viewmodel) position.Y += (pivot.Y - frame.Pivot.Y) * frame.Size.Y;
        if (!_sprites.TryGetValue(name, out var sprite))
        {
            if (!_textures.TryGetValue(frame.Atlas, out var texture))
            {
                texture = _engine.Graphics.OpenResource(new((viewmodel ? "loading-bay/" : name == "exit-sign" ? "doom-e1m1/textures/wall/" : "doom-e1m1/sprites/") + frame.Atlas, TextureFilter.Nearest, TextureWrap.Clamp)).Handle;
                _textures.Add(frame.Atlas, texture);
            }
            sprite = _engine.Graphics.CreateSprite(new(texture, frame.Min, frame.Max, pivot, frame.Size, viewmodel ? BillboardMode.None : BillboardMode.Cylindrical, SpriteSizeMode.World, id == 35001 ? 1 : 0, viewmodel ? SpriteDepthPolicy.DepthTestOff : SpriteDepthPolicy.Default, new Color(1, 1, 1, 1)));
            // Bottom-anchor the art behind the compact DOM status bar. The muzzle is
            // presentation only; camera-space aiming never depends on this rectangle.
            if (viewmodel) _engine.Graphics.SetSpriteViewport(new(sprite, true, Vector2.Zero, new Vector2(1, .84f), new Vector2(.5f, 0), SpriteViewportFit.Contain));
            _sprites.Add(name, sprite);
        }
        return new(id, false, 0, new(position, Quaternion.Identity, Vector3.One), sprite, true, viewmodel ? RenderLayer.Viewmodel : RenderLayer.Scene);
    }
    public void Dispose()
    { foreach (var ball in _fireballs) ball.Body.Dispose(); _projectileWorld.Dispose(); foreach (var sprite in _sprites.Values) sprite.Dispose(); foreach (var texture in _textures.Values) texture.Dispose(); }
}
