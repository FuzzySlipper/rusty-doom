using System.Numerics;
using LoadingBay.Game;

internal static class StudyDoorExercise
{
    internal static void Run()
    {
        LoadingBayStudyDoor door = new();
        Require(!door.Use(Vector3.Zero) && !door.Advance(1), "Remote use opened the study door");
        Require(door.Height == 0, "Door must initially block traversal");
        Require(door.Use(new(7, 1, 32)), "Close use did not initiate opening");
        Require(!door.Use(new(7, 1, 32)), "Repeated use restarted opening");
        Require(!door.Advance(float.NaN) && !door.Advance(-1), "Invalid timing moved the door");
        Require(door.Advance(.5f) && door.Height == .75f, "Door did not advance on admitted time");
        Require(door.Obstacle.Transform.Translation.Y == door.Height, "Moving collision and presentation disagree");
        Require(door.Advance(10) && door.Height == LoadingBayStudyDoor.Travel, "Door did not clamp at full travel");
        Require(!door.Advance(10) && door.Obstacle.LinearVelocity == Vector3.Zero, "Fully open door continued moving");
        Require(LoadingBayNorthWingRecipe.DoorMin.Y + door.Height > 1.8f, "Open door does not clear a standing player");
        Require(!door.Use(new(9, 1, 32)), "Return use unexpectedly closed the route");
        LoadingBayStudyDoor second = new(new(LoadingBayEastWingRecipe.DoorSurface, 20001,
            LoadingBayEastWingRecipe.DoorMin, LoadingBayEastWingRecipe.DoorMax));
        Require(!second.Opening && second.Height == 0 && second.Obstacle.Entity != door.Obstacle.Entity,
            "Independent study doors shared state or collision identity");
        Require(!second.Use(new(7, 1, 32)), "North-door use opened the southern door");
        Require(second.Use(new(54, .2f, -14)) && second.Advance(2), "Southern-door bounds did not admit local use");
        Require(second.Obstacle.Transform.Translation.Y == LoadingBayStudyDoor.Travel && door.Height == LoadingBayStudyDoor.Travel,
            "Independent door motion did not retain the first opening");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
