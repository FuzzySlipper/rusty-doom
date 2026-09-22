using System.Numerics;
using LoadingBay.Game;

internal static class NavigationGuidanceExercise
{
    internal static void Run()
    {
        LoadingBayNavigationTarget[] targets =
        [
            new("pickup-shotgun-west", "western shotgun", "pickup", new(-27f, -.25f, -9f), true, "available", false, null),
            new("door-north-wing", "north wing doorway", "door", new(7f, 0f, -32f), true, "closed", true, 20000)
        ];

        Require(LoadingBayNavigationGuidance.TryFind(targets, "PICKUP-SHOTGUN-WEST", out LoadingBayNavigationTarget shotgun)
            && shotgun.Label == "western shotgun", "navigation targets did not retain case-insensitive stable identity");
        Require(!LoadingBayNavigationGuidance.TryFind(targets, "pickup-not-real", out _), "navigation accepted an unknown target");

        RecipePickup canonicalWestShotgun = new(30003, RecipePickupKind.Shotgun, new(-27f, -.25f, -9f), "SHOTA0");
        LoadingBayNavigationTarget canonicalTarget = LoadingBayNavigationGuidance.PickupTarget("pickup-shotgun-west", "western shotgun", canonicalWestShotgun, false);
        Require(canonicalTarget.Position == canonicalWestShotgun.Position && canonicalTarget.Position.Z == -9f,
            "navigation changed the canonical gameplay-world pickup coordinate");

        LoadingBayNavigationProgress arrived = LoadingBayNavigationGuidance.Progress(shotgun, LoadingBayNavigationGuidance.TargetFeet(shotgun) + new Vector3(.5f, 0f, 0f));
        LoadingBayNavigationProgress distant = LoadingBayNavigationGuidance.Progress(shotgun, new Vector3(-7f, -.9f, 3f));
        Require(arrived.Arrived && !distant.Arrived && distant.Distance > LoadingBayNavigationGuidance.ArrivalRadius,
            "navigation arrival policy did not remain separate from an Engine route result");
        Require(arrived.CurrentRegion == "western-chamber" && distant.CurrentRegion == "spawn",
            "navigation progress did not retain the authored transcript regions");

        Vector3 feet = LoadingBayNavigationGuidance.PlayerFeet(new Vector3(2f, 3f, 4f), 1.8f);
        Require(feet == new Vector3(2f, 2.1f, 4f) && LoadingBayNavigationGuidance.TargetFeet(shotgun).Y == -.23f,
            "navigation feet conversion drifted from the player-body convention");
        Require(MathF.Abs(LoadingBayNavigationGuidance.BearingDegrees(-Vector3.UnitZ, Vector3.Zero, Vector3.UnitX) - 90f) < .001f,
            "navigation bearing no longer reports positive right");

        var visits = new LoadingBayNavigationVisits();
        visits.Observe(targets, LoadingBayNavigationGuidance.TargetFeet(shotgun));
        Require(visits.Contains(shotgun.Id) && !visits.Contains("door-north-wing"),
            "navigation visits did not record only product arrival facts");
        visits.Reset();
        Require(!visits.Contains(shotgun.Id), "navigation visits survived the ordinary run reset");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
