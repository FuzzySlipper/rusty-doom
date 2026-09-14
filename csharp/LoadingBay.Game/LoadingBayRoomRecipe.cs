using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Manual E1M1 starting-room recipe, measured from sectors 14/15 and 37–41.</summary>
internal static class LoadingBayRoomRecipe
{
    internal static readonly Vector3 SpawnBase = new(-7, 0, -3);
    internal static readonly Transform Identity = new(Vector3.Zero, Quaternion.Identity, Vector3.One);
    private static readonly Vector2[] South = [new(-10, -4), new(-14, -1), new(0, -1), new(-4, -4)];
    private static readonly Vector2[] North = [new(-14, 18), new(-9.75f, 20), new(2, 20), new(2, 18)];
    private static readonly Vector2[] Blue = [new(-12, 4), new(-12, 14), new(-2, 14), new(2, 13), new(2, 5), new(-2, 4)];
    private static readonly Vector2[] BlueEast = [new(-2, 4), new(-2, 14), new(2, 13), new(2, 5)];


    internal static void Compose(IImplicitSurfacesService service, Material wall, Material floor,
        Material carpet, Material trim, Material ceiling, Material door, Material brown, Action<RecipeSurface> emit, Action<RecipeJoin>? join = null)
    {
        RecipeWriter writer = new(service, new(.125f, 0, .25f, ImplicitMaterialBoundaryMode.Interpolated),
            surface => emit(surface.Material.Equals(floor) || surface.Material.Equals(carpet) || surface.Material.Equals(ceiling)
                ? surface with { Sampling = surface.Sampling with { TextureRepeats = .5f } } : surface));
        void Surface(string name, ImplicitRecipe field, ImplicitNode solid, Vector3 min, Vector3 max, Material material)
            => writer.Surface(name, field, solid, min - new Vector3(.3f), max + new Vector3(.3f), material, Identity);
        void Box(string name, Vector3 min, Vector3 max, Material material, bool sideWall = false)
        {
            using ImplicitRecipe field = writer.Begin();
            Transform placement = Identity;
            writer.Surface(name, field, field.Box(min, max), min - new Vector3(.3f), max + new Vector3(.3f), material, placement, textureMapping: sideWall
                ? ImplicitTextureMapping.Basis(-Vector3.UnitZ, Vector3.UnitY, new Vector2(.25f), Vector2.Zero) : null);
        }
        ImplicitNode Outline(ImplicitRecipe f, float bottom, float top, float e = 0)
            => f.Union(f.Union(f.Box(new(-18-e, bottom, -1-e), new(2+e, top, 18+e)),
                PlanarRecipes.ConvexPrism(f, South, bottom, top, e)),
                f.Union(PlanarRecipes.ConvexPrism(f, North, bottom, top, e),
                    f.Box(new(-8-e, bottom, -5-e), new(-6+e, top, -4+e))));
        ImplicitNode HighRoom(ImplicitRecipe f, float bottom, float top, float e = 0)
            => f.Union(f.Box(new(-11-e, bottom, 5-e), new(-2+e, top, 13+e)),
                PlanarRecipes.ConvexPrism(f, BlueEast, bottom, top, e));
        ImplicitNode Rim(ImplicitRecipe f, float bottom, float top, float e = 0)
            => f.Union(f.Box(new(-12-e, bottom, 4-e), new(-11+e, top, 14+e)),
                f.Union(f.Box(new(-11-e, bottom, 4-e), new(-3+e, top, 5+e)),
                    f.Box(new(-11-e, bottom, 13-e), new(-3+e, top, 14+e))));
        ImplicitNode Mouth(ImplicitRecipe f, float bottom, float top, float e = 0)
        {
            // The small source jamb notches are omitted; keep the measured tapered mouth.
            Vector2[] contour = [new(-24.5f, 6.75f), new(-24.5f, 11.25f), new(-24, 12),
                new(-18.75f, 13), new(-18, 13), new(-18, 5), new(-18.75f, 5), new(-24, 6)];
            return PlanarRecipes.ConvexPrism(f, contour, bottom, top, e);
        }
        using (ImplicitRecipe f = writer.Begin())
        {
            ImplicitNode slab = f.Subtract(Outline(f, -1, 0, .4f), PlanarRecipes.ConvexPrism(f, Blue, -1.1f, .1f));
            slab = f.Intersect(slab, f.Box(new(-19, -1, -6), new(4, 0, 20)));
            slab = f.Subtract(slab, f.Box(new(-19, -1.1f, 5), new(-18, .1f, 13)));
            slab = f.Subtract(slab, f.Box(new(2, -1.1f, 5), new(3, .1f, 13)));
            Surface("spawn perimeter floor", f, slab, new(-18.4f, -1, -5.4f), new(2.4f, 0, 20), floor);
        }
        using (ImplicitRecipe f = writer.Begin())
            Surface("spawn recessed blue floor and rim", f, f.Union(HighRoom(f, -.9f, -.5f, .2f), Rim(f, -.9f, -.25f, .1f)),
                new(-12.1f, -1, 3.8f), new(2.4f, -.25f, 14.2f), carpet);
        using (ImplicitRecipe f = writer.Begin())
        {
            // One stepped ceiling solid owns all height transitions, avoiding independently
            // extracted diagonal edges between the low walkway, rim and tall central area.
            ImplicitNode roof = Outline(f, 6.25f, 6.65f, .4f);
            roof = f.Union(roof, f.Subtract(Outline(f, 2.25f, 6.65f, .4f),
                PlanarRecipes.ConvexPrism(f, Blue, 2.15f, 6.75f)));
            roof = f.Union(roof, Rim(f, 3.75f, 6.65f));
            roof = f.Subtract(roof, f.Box(new(-2.4f, 2, 20), new(3, 7, 21)));
            roof = f.Subtract(roof, f.Box(new(-19, 2, 5), new(-18, 7, 13)));
            roof = f.Subtract(roof, f.Box(new(2, 2, 5), new(3, 6.25f, 13)));
            Surface("spawn stepped ceiling", f, roof, new(-18.4f, 2.25f, -5.4f), new(2.4f, 6.65f, 20.4f), ceiling);
        }
        using (ImplicitRecipe f = writer.Begin())
        {
            ImplicitNode shell = f.Subtract(f.Union(Outline(f, 0, 2.15f, .4f), Outline(f, 2.05f, 2.4f, .35f)),
                Outline(f, -.1f, 2.5f));
            shell = f.Subtract(shell, f.Box(new(-19, -.1f, 5), new(-17.9f, 2.6f, 13)));
            shell = f.Subtract(shell, f.Box(new(1.9f, -.1f, 5), new(3, 2.6f, 13)));
            shell = f.Subtract(shell, f.Box(new(-2.4f, -.1f, 20), new(3, 2.6f, 21)));
            shell = f.Subtract(shell, f.Box(new(-8, -.1f, -5.5f), new(-6, 2.5f, -4.9f)));
            Surface("spawn outer wall shell", f, shell, new(-18.4f, 0, -5.4f), new(2.4f, 2.4f, 20.4f), wall);
        }
        foreach (float z in new[] { 4f, 13f })
            Box("spawn attached support", new(-3, -1, z), new(-2, 6.25f, z+1), brown);
        // Two tall source window openings face the outdoor court. One shared frame owns
        // their jambs, central pier, sill and lintel; no stacked decorative coplanar slabs.
        using (ImplicitRecipe f = writer.Begin())
        {
            ImplicitNode frame = f.Box(new(-13, -3, 2), new(-5, 6.25f, 3));
            foreach ((float lo, float hi) in new[] { (5f, 8f), (10f, 13f) })
                frame = f.Subtract(frame, f.Box(new(-hi + .2f, .25f, 1.9f), new(-lo - .2f, 6, 3.1f)));
            writer.Surface("spawn eastern window frame", f, frame, new(-13.3f, -3.3f, 1.7f), new(-4.7f, 6.55f, 3.3f), wall,
                new(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One));
        }
        // Source start niche: decorative panel, not a newly invented usable door.
        Box("spawn start niche panel", new(-8, 0, -5.4f), new(-6, 2.25f, -5), door);
        using (ImplicitRecipe f = writer.Begin())
        {
            ImplicitNode portal = f.Subtract(f.Box(new(-2.4f, 0, 20), new(2.4f, 4.9f, 20.7f)),
                f.Box(new(-2, -.1f, 19.9f), new(2, 2.25f, 20.8f)));
            Surface("exit portal", f, portal, new(-2.4f, 0, 20), new(2.4f, 4.9f, 20.7f), wall);
        }
        using (ImplicitRecipe f = writer.Begin())
            Surface("spawn west alcove floor", f, f.Intersect(Mouth(f, -.75f, -.25f, .4f),
                f.Box(new(-24.5f, -.75f, 4), new(-18, -.25f, 14))), new(-24.5f, -.75f, 4.6f), new(-18, -.25f, 13.4f), floor);
        using (ImplicitRecipe f = writer.Begin())
            Surface("spawn west alcove ceiling", f, f.Intersect(Mouth(f, 3.75f, 4.15f, .4f),
                f.Box(new(-24.1f, 3.75f, 4), new(-18.4f, 4.15f, 14))), new(-24.1f, 3.75f, 4.6f), new(-18.4f, 4.15f, 13.4f), ceiling);
        using (ImplicitRecipe f = writer.Begin())
        {
            ImplicitNode seat = f.Intersect(Mouth(f, 3.65f, 3.85f, .35f),
                f.Box(new(-24, 3.65f, 4.7f), new(-18.5f, 3.85f, 13.3f)));
            ImplicitNode shell = f.Subtract(f.Union(Mouth(f, -.25f, 3.75f, .4f), seat), Mouth(f, -.35f, 3.95f));
            shell = f.Intersect(shell, f.Box(new(-24.1f, -.25f, 4), new(-18.4f, 3.85f, 14)));
            Surface("spawn west alcove walls", f, shell, new(-24.1f, -.25f, 4.6f), new(-18.4f, 3.85f, 13.4f), wall);
        }
        Box("spawn west ceiling transition", new(-18.4f, 2.25f, 5), new(-18, 4.15f, 13), wall, true);
        LoadingBayWestWingRecipe.Compose(service, wall, floor, carpet, trim, ceiling, emit);
        LoadingBayNorthWingRecipe.Compose(service, wall, floor, carpet, trim, ceiling, door, emit, join);
        join?.Invoke(new("spawn west wall-ceiling", "spawn stepped ceiling", "spawn outer wall shell",
            new(-18.2f, 2.25f, 1.5f), new(0, 0, 1.5f), new(.15f, 0, 0)));
        join?.Invoke(new("spawn west wall-floor", "spawn perimeter floor", "spawn outer wall shell",
            new(-18.2f, 0, 1.5f), new(0, 0, 1.5f), new(.15f, 0, 0)));
    }
}
