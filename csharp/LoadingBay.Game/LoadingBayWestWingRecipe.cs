using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Manual western chamber and gallery interpretation, informed by sectors 24–45.</summary>
internal static class LoadingBayWestWingRecipe
{
    private static readonly Vector2[] Chamber = [new(-24.5f, 6), new(-26, 2), new(-36, 2),
        new(-38, 4), new(-38, 14), new(-36, 16), new(-26, 16), new(-24.5f, 12)];

    internal static void Compose(IImplicitSurfacesService service, Material wall, Material floor,
        Material carpet, Material trim, Material ceiling, Action<RecipeSurface> emit)
    {
        RecipeWriter writer = new(service, new(.125f, 0, .2f, ImplicitMaterialBoundaryMode.Interpolated),
            surface => emit(surface.Material.Equals(floor) || surface.Material.Equals(carpet) || surface.Material.Equals(ceiling)
                ? surface with { Sampling = surface.Sampling with { TextureRepeats = .5f } } : surface));
        void Surface(string name, ImplicitRecipe field, ImplicitNode solid, Vector3 min, Vector3 max, Material material)
            => writer.Surface(name, field, solid, min - new Vector3(.25f), max + new Vector3(.25f), material, LoadingBayRoomRecipe.Identity);
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
            Surface("western chamber floor", field, LoadingBayRecipeShapes.Prism(field, Chamber, -.75f, -.25f),
                new(-38, -.75f, 2), new(-24.5f, -.25f, 16), floor);
        using (ImplicitRecipe field = writer.Begin())
            Surface("western chamber ceiling", field, LoadingBayRecipeShapes.Prism(field, Chamber, 7, 7.4f),
                new(-38, 7, 2), new(-24.5f, 7.4f, 16), ceiling);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode shell = field.Subtract(LoadingBayRecipeShapes.Prism(field, Chamber, -.25f, 7, .4f),
                LoadingBayRecipeShapes.Prism(field, Chamber, -.35f, 7.1f));
            shell = field.Subtract(shell, field.Box(new(-25, -.35f, 6.75f), new(-24, 3.75f, 11.25f)));
            shell = field.Subtract(shell, field.Box(new(-38.5f, -.35f, 6), new(-37.9f, 6, 12)));
            Surface("western chamber wall shell", field, shell, new(-38.4f, -.25f, 1.6f), new(-24.1f, 7, 16.4f), wall);
        }
        // Split each source half-unit rise into two quarter-unit treads for the
        // study controller. One solid owns every riser and the upper landing.
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode stairs = field.Box(new(-30.5f, -.25f, 8), new(-30, 0, 10));
            for (int i = 1; i < 14; i++)
                stairs = field.Union(stairs, field.Box(new(-30.5f - i * .5f, -.25f, 8),
                    new(-30 - i * .5f, .25f * i, 10)));
            stairs = field.Union(stairs, field.Box(new(-42, -.25f, 6), new(-37, 3.25f, 12)));
            Surface("western stairs and upper landing", field, stairs, new(-42, -.25f, 6), new(-30, 3.25f, 12), carpet);
        }
        foreach (float z in new[] { 4f, 12f })
        {
            Box($"western raised block {z}", new(-32, -.25f, z), new(-30, 1.25f, z + 2), trim);
            Box($"western suspended block {z}", new(-32, 5.75f, z), new(-30, 7, z + 2), trim);
        }
        // The chamber shell owns x=-38.4..-38; gallery walls begin behind it.
        Box("western gallery near wall", new(-41.6f, 3.25f, 5.6f), new(-38.4f, 6, 6), wall);
        Box("western gallery far wall", new(-41.6f, 3.25f, 12), new(-38.4f, 6, 12.4f), wall);
        Box("western gallery ceiling", new(-41.6f, 6, 6), new(-38.4f, 6.4f, 12), ceiling);
        // Larger outer chamber interprets sectors 28/30 around the raised gallery.
        Box("western outer floor", new(-60, -.5f, 0), new(-42, 0, 18), floor);
        Box("western outer ceiling", new(-60, 8.25f, 0), new(-42, 8.65f, 18), ceiling);
        Box("western outer rear", new(-60.4f, 0, -.4f), new(-60, 8.25f, 18.4f), wall, true);
        Box("western outer near", new(-60, 0, -.4f), new(-42, 8.25f, 0), wall);
        Box("western outer far", new(-60, 0, 18), new(-42, 8.25f, 18.4f), wall);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode opening = field.Subtract(field.Box(new(-18, 0, -42), new(0, 8.25f, -41.6f)),
                field.Box(new(-12, -.1f, -42.1f), new(-6, 6, -41.5f)));
            writer.Surface("western outer gallery opening", field, opening, new(-18.25f, -.25f, -42.25f),
                new(.25f, 8.5f, -41.35f), wall,
                new(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One));
        }
        // A broad platform and a product-authored return stair make the lower hall
        // recoverable. This stair is a refinement, not a copied source sector.
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode platform = field.Box(new(-50, 0, 6), new(-42, 3.25f, 12));
            for (int i = 0; i < 13; i++)
                platform = field.Union(platform, field.Box(new(-49, 0, 5.6f - i * .4f),
                    new(-46, 3.25f - i * .25f, 6 - i * .4f)));
            Surface("western overlook and return stairs", field, platform, new(-50, 0, -.5f), new(-42, 3.25f, 12), carpet);
        }
        Box("western gallery centerpiece", new(-48, 3.25f, 8), new(-46, 4, 10), trim);
    }
}
