using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Sector 62/70 upper eastern route, with a walkable stair adaptation.</summary>
internal static class LoadingBayEastGalleryRecipe
{
    internal const float FloorHeight = 3.25f;
    internal const float CeilingHeight = 5.5f;
    private static readonly Vector2[] UpperRoute = [new(72, 11), new(75, 12), new(72, 17), new(66.8f, 17)];

    internal static void Compose(IImplicitSurfacesService service, Material wall, Material floor,
        Material ceiling, Action<RecipeSurface> emit, Action<RecipeJoin>? join = null)
    {
        RecipeWriter writer = new(service, new(.125f, 0, .2f, ImplicitMaterialBoundaryMode.Interpolated),
            surface => emit(surface.Material.Equals(wall) ? surface
                : surface with { Sampling = surface.Sampling with { TextureRepeats = .5f } }));
        void Surface(string name, ImplicitRecipe f, ImplicitNode shape, Material material)
            => writer.Surface(name, f, shape, new(65.7f, -2.3f, -12.7f), new(78.7f, 6.2f, 20.3f),
                material, LoadingBayRoomRecipe.Identity);
        ImplicitNode Gallery(ImplicitRecipe f, float bottom, float top, float expansion = 0)
            => f.Intersect(PlanarRecipes.Walkway(f, UpperRoute, 2 + expansion, bottom, top),
                f.Box(new(66, bottom, 11 - expansion), new(78.4f, top, 19.6f + expansion)));
        ImplicitNode Footprint(ImplicitRecipe f, float bottom, float top, float expansion = 0)
            => f.Union(Gallery(f, bottom, top, expansion), f.Union(
                f.Box(new(66, bottom, -12 - expansion), new(74 + expansion, top, -8 + expansion)),
                f.Box(new(70 - expansion, bottom, -8 - expansion), new(74 + expansion, top, 11 + expansion))));

        ImplicitNode Floor(ImplicitRecipe f)
        {
            ImplicitNode solid = f.Box(new(66, -2, -12.3f), new(74.3f, -1.5f, -8));
            // Nineteen quarter-unit rises replace the source's 152-unit moving access.
            // One retained solid owns every tread and the upper landing.
            for (int i = 0; i < 19; i++)
                solid = f.Union(solid, f.Box(new(69.7f, -2, -8 + i), new(74.3f, -1.25f + i * .25f, -7 + i)));
            solid = f.Union(solid, f.Intersect(Gallery(f, 2.75f, FloorHeight, .3f),
                f.Box(new(66.05f, 2.75f, 10.7f), new(78.3f, FloorHeight, 19.9f))));
            return solid;
        }
        using (ImplicitRecipe f = writer.Begin())
            Surface("east gallery stairs and floor", f, Floor(f), floor);
        using (ImplicitRecipe f = writer.Begin())
        {
            ImplicitNode roof = f.Intersect(Footprint(f, CeilingHeight, 5.9f, .4f),
                f.Box(new(66, CeilingHeight, -12.4f), new(78.4f, 5.9f, 20)));
            Surface("east gallery ceiling", f, roof, ceiling);
        }
        using (ImplicitRecipe f = writer.Begin())
        {
            ImplicitNode seat = f.Intersect(Footprint(f, 5.4f, 5.65f, .35f),
                f.Box(new(66.1f, 5.4f, -12.3f), new(78.3f, 5.65f, 19.9f)));
            ImplicitNode shell = f.Subtract(f.Union(Footprint(f, -1.5f, CeilingHeight, .4f), seat),
                Footprint(f, -1.6f, 5.8f));
            shell = f.Intersect(shell, f.Box(new(66.4f, -1.5f, -12.4f), new(78.4f, 5.65f, 20)));
            // Floor owns the stair/landing contacts; remove its volume from the shell.
            shell = f.Subtract(shell, Floor(f));
            Surface("east gallery wall shell", f, shell, wall);
        }
        join?.Invoke(new("east gallery ceiling-wall", "east gallery ceiling", "east gallery wall shell",
            new(74.2f, CeilingHeight, 0), new(0, 0, 4), new(.15f, 0, 0)));
        join?.Invoke(new("east gallery lower entrance floor", "east gallery stairs and floor", "nukage basin",
            new(66, -1.75f, -10), new(0, 0, 1.5f), new(0, .12f, 0)));
    }
}
