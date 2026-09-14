using System.Numerics;
using Rusty.Engine;

namespace LoadingBay.Game;

/// <summary>Study policy: use nearby to raise once and leave the return route open.</summary>
internal sealed class LoadingBayStudyDoor
{
    internal const ulong Entity = 20000;
    internal const float Travel = 2.5f;
    internal const float Speed = 1.5f;
    internal const float UseDistance = 2.5f;
    internal LoadingBayStudyDoorDefinition Definition { get; }
    internal LoadingBayStudyDoor() : this(new(LoadingBayNorthWingRecipe.DoorSurface, Entity,
        LoadingBayNorthWingRecipe.DoorMin, LoadingBayNorthWingRecipe.DoorMax)) { }
    internal LoadingBayStudyDoor(LoadingBayStudyDoorDefinition definition) => Definition = definition;
    internal bool Opening { get; private set; }
    internal float Height { get; private set; }
    internal Transform Placement => new(new(0, Height, 0), Quaternion.Identity, Vector3.One);
    internal CharacterObstacle Obstacle => new(Definition.Entity, Placement, Definition.Min,
        Definition.Max, true, new(0, Opening && Height < Travel ? Speed : 0, 0), Vector3.Zero);

    internal bool Use(Vector3 playerCenter)
    {
        Vector3 nearest = Vector3.Clamp(playerCenter, Definition.Min, Definition.Max);
        if (Opening || Vector3.Distance(playerCenter, nearest) > UseDistance) return false;
        Opening = true;
        return true;
    }
    internal bool Advance(float seconds)
    {
        if (!Opening || !float.IsFinite(seconds) || seconds <= 0) return false;
        float next = Math.Min(Travel, Height + Speed * seconds);
        if (next == Height) return false;
        Height = next;
        return true;
    }
}

internal sealed record LoadingBayStudyDoorDefinition(string SurfaceName, ulong Entity, Vector3 Min, Vector3 Max);
