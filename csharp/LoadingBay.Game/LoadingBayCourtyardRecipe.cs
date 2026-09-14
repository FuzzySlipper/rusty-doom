using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Manual central court and recessed pool, guided by sectors 5/13.</summary>
internal static class LoadingBayCourtyardRecipe
{
    // The west edge now meets the source-derived starting-room window frame.
    private static readonly Vector2[] Pool = [new(16, 13), new(23, 13), new(28, 8),
        new(25, 3), new(17, 2.5f), new(12, 5), new(12, 10)];
    internal static void Compose(IImplicitSurfacesService service, Material wall, Material floor,
        Material liquid, Material trim, Action<RecipeSurface> emit)
    {
        RecipeWriter writer = new(service, new(.125f, 0, .2f, ImplicitMaterialBoundaryMode.Interpolated),
            surface => emit(surface.Material.Equals(floor) || surface.Material.Equals(liquid)
                ? surface with { Sampling = surface.Sampling with { TextureRepeats = .5f } } : surface));
        void Surface(string name, ImplicitRecipe field, ImplicitNode shape, Vector3 min, Vector3 max, Material material)
            => writer.Surface(name, field, shape, min - new Vector3(.25f), max + new Vector3(.25f), material, LoadingBayRoomRecipe.Identity);
        void Box(string name, Vector3 min, Vector3 max, Material material, bool sideWall = false)
        {
            using ImplicitRecipe field = writer.Begin();
            Transform placement = LoadingBayRoomRecipe.Identity;
            if (sideWall)
            {
                (min, max) = (new(-max.Z, min.Y, min.X), new(-min.Z, max.Y, max.X));
                placement = new(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One);
            }
            writer.Surface(name, field, field.Box(min, max), min - new Vector3(.25f), max + new Vector3(.25f), material, placement);
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode ground = field.Subtract(field.Box(new(3, -3, -4), new(45, -1.75f, 20)),
                LoadingBayRecipeShapes.Prism(field, Pool, -3.1f, -1.65f));
            Surface("courtyard ground around pool", field, ground, new(3, -3, -4), new(45, -1.75f, 20), floor);
        }
        using (ImplicitRecipe field = writer.Begin())
            Surface("courtyard recessed nukage", field, LoadingBayRecipeShapes.Prism(field, Pool, -3, -2.5f),
                new(12, -3, 2.5f), new(28, -2.5f, 13), liquid);
        // One stair solid ends at the courtyard boundary. Quarter-unit rises work
        // in either direction; north hall owns the upper reveal at z=23.6..24.
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode stairs = field.Box(new(18, -2.25f, 23.5f), new(22, 0, 24));
            for (int i = 1; i < 8; i++)
                stairs = field.Union(stairs, field.Box(new(18, -2.25f, 23.5f - i * .5f),
                    new(22, -.25f * i, 24 - i * .5f)));
            Surface("courtyard north descent", field, stairs, new(18, -2.25f, 20), new(22, 0, 24), floor);
        }
        Box("courtyard stair west wall", new(17.6f, -1.75f, 20.4f), new(18, 3, 23.6f), wall, true);
        Box("courtyard stair east wall", new(22, -1.75f, 20.4f), new(22.4f, 3, 23.6f), wall, true);
        Box("courtyard west boundary south", new(2.6f, -1.75f, -4), new(3, 4.5f, 5), wall, true);
        Box("courtyard west boundary north", new(2.6f, -1.75f, 13), new(3, 4.5f, 20), wall, true);
        Box("courtyard east boundary", new(45, -1.75f, -4), new(45.4f, 4.5f, 20), wall, true);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode entry = field.Subtract(field.Box(new(2.6f, -1.75f, -4.4f), new(45.4f, 4.5f, -4)),
                field.Box(new(24, -1.85f, -4.5f), new(28, .5f, -3.9f)));
            Surface("courtyard southern passage opening", field, entry, new(2.6f, -1.75f, -4.4f), new(45.4f, 4.5f, -4), wall);
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode north = field.Subtract(field.Box(new(2.6f, -1.75f, 20), new(45.4f, 4.5f, 20.4f)),
                field.Box(new(18, -1.85f, 19.9f), new(22, 4.6f, 20.5f)));
            Surface("courtyard north boundary opening", field, north, new(2.6f, -1.75f, 20), new(45.4f, 4.5f, 20.4f), wall);
        }
        // Three quarter-unit steps meet the pool's straight north edge. Merge the
        // treads and trim their footprint against the pool, avoiding ground overlap.
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode steps = field.Box(new(18, -2.5f, 10.75f), new(21, -2.25f, 11.5f));
            steps = field.Union(steps, field.Box(new(18, -2.5f, 11.5f), new(21, -2, 12.25f)));
            steps = field.Union(steps, field.Box(new(18, -2.5f, 12.25f), new(21, -1.75f, 13)));
            Surface("courtyard pool recovery steps", field, steps, new(18, -2.5f, 10.75f), new(21, -1.75f, 13), trim);
        }
    }
}
