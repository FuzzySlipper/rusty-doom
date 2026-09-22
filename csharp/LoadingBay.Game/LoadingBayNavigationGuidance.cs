using System.Numerics;

namespace LoadingBay.Game;

/// <summary>Product-owned destination meaning and arrival policy for the read-only navigation aid.</summary>
internal static class LoadingBayNavigationGuidance
{
    internal const float ArrivalRadius = 1.25f;
    internal const float FootClearance = .02f;

    private const string WesternShotgunId = "pickup-shotgun-west";
    private const string SouthernShotgunId = "pickup-shotgun-south";
    private const string NorthDoorId = "door-north-wing";
    private const string SouthernDoorId = "door-south-study";
    private const string EastImpId = "enemy-east-imp";
    private const string TerminalRoomId = "room-south-terminal";
    private const string ExitId = "exit-hangar";

    private const ulong WesternShotgunPickup = 30003;
    private const ulong SouthernShotgunPickup = 30007;
    private const ulong EastImp = 31005;
    private const ulong NorthDoor = 20000;
    private const ulong SouthernDoor = 20001;

    private static readonly Vector3 SouthernTerminalCenter = new(54f, -.75f, 37f);

    internal static LoadingBayNavigationTarget[] Targets(LoadingBayRecipeGameplay gameplay, IReadOnlyList<LoadingBayStudyDoor> doors)
    {
        ArgumentNullException.ThrowIfNull(gameplay);
        ArgumentNullException.ThrowIfNull(doors);

        RecipePickup westShotgun = Pickup(gameplay, WesternShotgunPickup);
        RecipePickup southernShotgun = Pickup(gameplay, SouthernShotgunPickup);
        LoadingBayStudyDoor northDoor = Door(doors, NorthDoor);
        LoadingBayStudyDoor southernDoor = Door(doors, SouthernDoor);
        RecipeEnemy eastImp = Enemy(gameplay, EastImp);

        return
        [
            PickupTarget(WesternShotgunId, "western shotgun", westShotgun, gameplay.IsCollected(westShotgun.Id)),
            DoorTarget(NorthDoorId, "north wing doorway", northDoor),
            PickupTarget(SouthernShotgunId, "southern shotgun", southernShotgun, gameplay.IsCollected(southernShotgun.Id)),
            DoorTarget(SouthernDoorId, "southern study doorway", southernDoor),
            new(EastImpId, "east hall imp", "hostile", eastImp.Position, eastImp.Health > 0,
                eastImp.Health > 0 ? "alive" : "defeated", false, null),
            new(TerminalRoomId, "southern terminal room", "room", SouthernTerminalCenter, true, "available", false, null),
            new(ExitId, "hangar exit", "exit", LoadingBayRecipeGameplay.ExitPosition, !gameplay.Complete,
                gameplay.Complete ? "complete" : "available", false, null)
        ];
    }

    internal static bool TryFind(LoadingBayNavigationTarget[] targets, string id, out LoadingBayNavigationTarget target)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (string.IsNullOrWhiteSpace(id))
        {
            target = default;
            return false;
        }

        foreach (LoadingBayNavigationTarget candidate in targets)
            if (string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                return true;
            }
        target = default;
        return false;
    }

    internal static LoadingBayNavigationTarget PickupTarget(string id, string label, RecipePickup pickup, bool collected)
        => new(id, label, "pickup", pickup.Position, !collected, collected ? "collected" : "available", false, null);

    internal static LoadingBayNavigationProgress Progress(LoadingBayNavigationTarget target, Vector3 playerFeet)
    {
        float distance = Vector3.Distance(playerFeet, TargetFeet(target));
        return new(target.Id, target.Label, CurrentRegion(playerFeet), distance, distance <= ArrivalRadius,
            target.Available, target.State);
    }

    internal static Vector3 PlayerFeet(Vector3 playerCenter, float standingHeight)
        => playerCenter - (Vector3.UnitY * (standingHeight * .5f));

    internal static Vector3 TargetFeet(LoadingBayNavigationTarget target)
        => target.Position + (Vector3.UnitY * FootClearance);

    internal static float BearingDegrees(Vector3 forward, Vector3 from, Vector3 target)
    {
        Vector3 planarForward = new(forward.X, 0f, forward.Z);
        if (planarForward.LengthSquared() <= float.Epsilon) return 0f;
        planarForward = Vector3.Normalize(planarForward);
        Vector3 offset = target - from;
        offset.Y = 0f;
        if (offset.LengthSquared() <= float.Epsilon) return 0f;
        Vector3 right = new(-planarForward.Z, 0f, planarForward.X);
        return MathF.Atan2(Vector3.Dot(offset, right), Vector3.Dot(offset, planarForward)) * (180f / MathF.PI);
    }

    internal static string CurrentRegion(Vector3 position)
    {
        // These coarse authored zones describe where the player is for a run transcript.
        // They do not decide walkability or replace the Engine collision projection.
        if (Contains(position, -19f, 4f, -20f, 6f)) return "spawn";
        if (Contains(position, -61f, -23f, -19f, 0f)) return "western-chamber";
        if (Contains(position, -4f, 39f, -45f, -20f)) return "north-wing";
        if (Contains(position, 37f, 67f, -31f, 16f)) return "east-hall";
        if (Contains(position, 43f, 65f, 19f, 33f)) return "southern-room";
        if (Contains(position, 50f, 58f, 32f, 43f)) return "terminal";
        if (Contains(position, 22f, 53f, 3f, 20f)) return "south-passage";
        return "unclassified";
    }

    private static LoadingBayNavigationTarget DoorTarget(string id, string label, LoadingBayStudyDoor door)
    {
        Vector3 center = (door.Definition.Min + door.Definition.Max) * .5f;
        Vector3 approach = door.Definition.Entity == NorthDoor
            ? center + new Vector3(-1.25f, 0f, 0f)
            : center + new Vector3(0f, 0f, 1.25f);
        approach.Y = door.Definition.Min.Y;
        bool requiresUse = !door.Opening;
        return new(id, label, "door", approach, true, DoorState(door), requiresUse, door.Definition.Entity);
    }

    private static string DoorState(LoadingBayStudyDoor door)
        => !door.Opening ? "closed" : door.Height >= LoadingBayStudyDoor.Travel ? "open" : "opening";

    private static RecipePickup Pickup(LoadingBayRecipeGameplay gameplay, ulong id)
        => gameplay.Pickups.Single(pickup => pickup.Id == id);

    private static RecipeEnemy Enemy(LoadingBayRecipeGameplay gameplay, ulong id)
        => gameplay.Enemies.Single(enemy => enemy.Id == id);

    private static LoadingBayStudyDoor Door(IReadOnlyList<LoadingBayStudyDoor> doors, ulong id)
        => doors.Single(door => door.Definition.Entity == id);

    private static bool Contains(Vector3 position, float minimumX, float maximumX, float minimumZ, float maximumZ)
        => position.X >= minimumX && position.X <= maximumX && position.Z >= minimumZ && position.Z <= maximumZ;
}

internal readonly record struct LoadingBayNavigationTarget(
    string Id,
    string Label,
    string Kind,
    Vector3 Position,
    bool Available,
    string State,
    bool RequiresUse,
    ulong? InteractionEntity);

internal readonly record struct LoadingBayNavigationProgress(
    string TargetId,
    string TargetLabel,
    string CurrentRegion,
    float Distance,
    bool Arrived,
    bool TargetAvailable,
    string TargetState);

/// <summary>Per-RoomStudy-run visit facts for transcript comparison; it never directs player motion.</summary>
internal sealed class LoadingBayNavigationVisits
{
    private readonly HashSet<string> _visited = new(StringComparer.Ordinal);

    internal void Reset() => _visited.Clear();

    internal void Observe(LoadingBayNavigationTarget[] targets, Vector3 playerFeet)
    {
        foreach (LoadingBayNavigationTarget target in targets)
            if (LoadingBayNavigationGuidance.Progress(target, playerFeet).Arrived)
                _visited.Add(target.Id);
    }

    internal bool Contains(string targetId) => _visited.Contains(targetId);
}
