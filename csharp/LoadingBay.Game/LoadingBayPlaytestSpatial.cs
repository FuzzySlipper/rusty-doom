using System.Numerics;
using System.Text.Json;
using Rusty.Engine;
using Rusty.Engine.Debugging;

namespace LoadingBay.Game;

internal sealed partial class LoadingBayRoomStudy
{
    internal DebugCommandResult InspectGrid(int radius, int verticalRadius, double cellSize)
    {
        if (radius < 0 || radius > 15 || verticalRadius < 0 || verticalRadius > 15 ||
            !double.IsFinite(cellSize) || cellSize < .125 || cellSize > 2 ||
            (2 * radius + 1) * (2 * radius + 1) * (2 * verticalRadius + 1) > SpatialGridSnapshot.MaximumCells)
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "Grid: radii 0..15, cellSize .125..2, at most 8192 cells.");
        Vector3 feet = _player.Position - Vector3.UnitY * (_tuning.StandingCharacterHeight * .5f);
        Vector3 origin = feet - new Vector3((float)((radius + .5) * cellSize), (float)((verticalRadius + .5) * cellSize), (float)((radius + .5) * cellSize));
        uint size = (uint)(2 * radius + 1);
        var request = new SpatialMapRequest(_player.Session, origin, cellSize, size, size, origin.Y, origin.Y + cellSize, origin.Y, origin.Y + cellSize, SpatialMapDoorColliders());
        return DebugCommandResult.Success(SpatialGridSnapshot.Capture(_engine.Spatial, request, (uint)(2 * verticalRadius + 1),
            new SpatialMapObservation($"generation:{_facts.Generation};step:{_facts.SimulationStep}", _player.Position, _player.Forward)));
    }

    internal DebugCommandResult InspectProbe(double distance)
    {
        if (!double.IsFinite(distance) || distance <= 0 || distance > 8)
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "Probe distance must be in (0,8].");
        Vector3 feet = _player.Position - Vector3.UnitY * (_tuning.StandingCharacterHeight * .5f);
        using var rays = JsonDocument.Parse(PlaytestTraversal.Probe(_engine.Spatial, _player.Session, feet,
            _tuning.StandingCharacterHeight, _tuning.MaximumStepHeight, (float)distance, SpatialMapDoorColliders()));
        return DebugCommandResult.Success(JsonSerializer.Serialize(new { stamp = _facts.SimulationStep,
            movement = _player.InspectMovement(), rays = rays.RootElement }, CombatObservationJson));
    }

    internal DebugCommandResult InspectClearance(double x, double y, double z)
    {
        if (!FitsSinglePrecision(x) || !FitsSinglePrecision(y) || !FitsSinglePrecision(z))
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "Target feet must be finite world XYZ.");
        Vector3 targetFeet = new((float)x, (float)y, (float)z);
        Vector3 feet = LoadingBayNavigationGuidance.PlayerFeet(_player.Position, _tuning.StandingCharacterHeight);
        if (Vector3.Distance(feet, targetFeet) > 8)
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "Clearance target must be within 8 world units of player feet.");
        return DebugCommandResult.Success(SpatialClearanceSnapshot.Capture(_engine.Spatial, _player.Session,
            _player.Position, targetFeet, _tuning.StandingCharacterHeight, _player.ControllerConfig, SpatialMapDoorColliders()));
    }

    private PlaytestAction InspectJumpAction(bool active, string? reason)
    {
        if (!active) return new("jump", "Space", 0, false, false, reason);
        Vector3 feet = LoadingBayNavigationGuidance.PlayerFeet(_player.Position, _tuning.StandingCharacterHeight);
        var plan = PlaytestTraversal.JumpToward(feet, _player.Forward, feet, _player.ControllerConfig,
            _player.Grounded, "Space", "KeyW");
        return new("jump", "Space", plan.MoveMs + plan.SettleMs, false, plan.Available, plan.Reason);
    }

    internal DebugCommandResult InspectJump(double x, double y, double z)
    {
        if (!FitsSinglePrecision(x) || !FitsSinglePrecision(y) || !FitsSinglePrecision(z))
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "Target feet must be finite world XYZ.");
        var plan = PlaytestTraversal.JumpToward(_player.Position - Vector3.UnitY * (_tuning.StandingCharacterHeight * .5f),
            _player.Forward, new Vector3((float)x, (float)y, (float)z), _player.ControllerConfig,
            _player.Grounded && !_gameplay.Dead && !_gameplay.Complete, "Space", "KeyW");
        return DebugCommandResult.Success(JsonSerializer.Serialize(plan, CombatObservationJson));
    }
}
