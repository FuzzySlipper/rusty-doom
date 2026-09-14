using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Authored interpretation of sectors 54/53/71, the eastern zigzag and southern door/room.</summary>
internal static class LoadingBayEastWingRecipe
{
    internal const string DoorSurface = "southern study door";
    internal static readonly Vector3 DoorMin = new(52, -.75f, -15.9f);
    internal static readonly Vector3 DoorMax = new(56, 1.5f, -15.6f);
    private static readonly Vector2[][] Descent = [
        [new(38, 26), new(38, 30), new(42, 30), new(42, 26)],
        [new(42, 26), new(42, 30), new(48, 26), new(46, 22)],
        [new(46, 22), new(48, 26), new(52, 24), new(52, 20), new(48, 20)]
    ];
    internal static readonly Vector2[] WalkwayCenterline = [new(50, 20), new(56, 16), new(52, 12),
        new(58, 8), new(54, 3), new(56, -3), new(54, -6), new(54, -15)];

    internal static void Compose(IImplicitSurfacesService service, Material wall, Material floor,
        Material liquid, Material trim, Material ceiling, Material door, Action<RecipeSurface> emit, Action<RecipeJoin>? join = null)
    {
        RecipeWriter writer = new(service, new(.25f, 0, .2f, ImplicitMaterialBoundaryMode.Interpolated),
            surface => emit(surface.Material.Equals(floor) || surface.Material.Equals(liquid) || surface.Material.Equals(ceiling)
                ? surface with { Sampling = surface.Sampling with { TextureRepeats = .5f } } : surface));
        void Surface(string name, ImplicitRecipe field, ImplicitNode shape, Vector3 min, Vector3 max, Material material, float cellSize = .25f)
            => writer.Surface(name, field, shape, min - new Vector3(.3f), max + new Vector3(.3f), material, LoadingBayRoomRecipe.Identity, cellSize: cellSize);
        void Box(string name, Vector3 min, Vector3 max, Material material, bool sideWall = false)
        {
            using ImplicitRecipe field = writer.Begin();
            Transform placement = LoadingBayRoomRecipe.Identity;
            writer.Surface(name, field, field.Box(min, max), min - new Vector3(.3f), max + new Vector3(.3f), material, placement, textureMapping: sideWall
                ? ImplicitTextureMapping.Basis(-Vector3.UnitZ, Vector3.UnitY, new Vector2(.2f), Vector2.Zero) : null);
        }
        ImplicitNode Connector(ImplicitRecipe field, float bottom, float top, float expansion = 0)
        {
            ImplicitNode result = PlanarRecipes.ConvexPrism(field, Descent[0], bottom, top, expansion);
            for (int i = 1; i < Descent.Length; i++) result = field.Union(result,
                PlanarRecipes.ConvexPrism(field, Descent[i], bottom, top, expansion));
            return result;
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            // One stepped solid removes buried shared faces between the diagonal landings.
            ImplicitNode stepped = PlanarRecipes.ConvexPrism(field, Descent[0], -1.25f, -.25f);
            for (int i = 1; i < Descent.Length; i++) stepped = field.Union(stepped,
                PlanarRecipes.ConvexPrism(field, Descent[i], -1.25f, -.25f * (i + 1)));
            // Short sloped lead-ins keep diagonal joins traversable without relying on a
            // capsule's discrete step probe at the exact seam between two contour pieces.
            foreach ((Vector2 a, Vector2 b, float high) in new[] {
                (new Vector2(42, 26), new Vector2(42, 30), -.25f),
                (new Vector2(46, 22), new Vector2(48, 26), -.5f) })
            {
                Vector2 edge = b - a, downhill = Vector2.Normalize(new(edge.Y, -edge.X));
                const float run = .75f, rise = .25f;
                ImplicitNode leadIn = PlanarRecipes.ConvexPrism(field,
                    [a, b, b + downhill * run, a + downhill * run], -1.25f, high);
                Vector3 normal = new(downhill.X * rise / run, 1, downhill.Y * rise / run);
                leadIn = field.Intersect(leadIn, field.Plane(normal, high + normal.X * a.X + normal.Z * a.Y));
                stepped = field.Union(stepped, leadIn);
            }
            stepped = field.Intersect(stepped, Connector(field, -1.25f, 0));
            writer.Surface("east descent floor", field, stepped, new(37.7f, -1.55f, 19.7f), new(52.3f, .3f, 30.3f),
                floor, LoadingBayRoomRecipe.Identity, cellSize: .125f);
        }
        using (ImplicitRecipe field = writer.Begin())
            Surface("east connector ceiling", field, field.Intersect(Connector(field, 2.25f, 2.65f, .4f),
                field.Box(new(38.4f, 2.25f, 20.4f), new(53, 2.65f, 31))), new(38.4f, 2.25f, 20.4f), new(53, 2.65f, 31), ceiling, .125f);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode seat = field.Intersect(Connector(field, 2.15f, 2.4f, .35f),
                field.Box(new(38.5f, 2.15f, 20.5f), new(52.8f, 2.4f, 30.8f)));
            ImplicitNode shell = field.Subtract(field.Union(Connector(field, -.75f, 2.25f, .4f), seat),
                Connector(field, -.85f, 2.5f));
            shell = field.Intersect(shell, field.Box(new(38.4f, -.75f, 20.4f), new(53, 2.4f, 31)));
            Surface("east connector shell", field, shell, new(38.4f, -.75f, 20.4f), new(53, 2.4f, 31), wall, .125f);
        }

        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode basin = field.Subtract(field.Box(new(46, -2, -15), new(66, -1.5f, 20)),
                field.Box(new(45.9f, -2.1f, -12.4f), new(52, -1.4f, -7.6f)));
            Surface("nukage basin", field, basin, new(46, -2, -15), new(66, -1.5f, 20), liquid);
        }
        Box("east hall ceiling", new(46, 5.5f, -15), new(66, 5.9f, 20), ceiling);
        using (ImplicitRecipe field = writer.Begin())
        {
            // The original wall owns both reveals; the new floor occupies the upper cut.
            ImplicitNode outer = field.Box(new(-20, -1.5f, 66), new(15, 5.5f, 66.4f));
            outer = field.Subtract(outer, field.Box(new(8, -1.6f, 65.9f), new(12, 2.25f, 66.5f)));
            outer = field.Subtract(outer, field.Box(new(-19, 2.75f, 65.9f), new(-15, 5.6f, 66.5f)));
            writer.Surface("east hall gallery openings", field, outer, new(-20.3f, -1.8f, 65.7f), new(15.3f, 5.8f, 66.7f), wall,
                new(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One));
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode opening = field.Subtract(field.Box(new(-20, -4.25f, 45.6f), new(15, 5.5f, 46)),
                field.Box(new(8, -4.85f, 45.5f), new(12, 1.5f, 46.1f)));
            writer.Surface("east hall inner passage opening", field, opening, new(-20.3f, -4.55f, 45.3f), new(15.3f, 5.8f, 46.3f), wall,
                new(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One));
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode wallShape = field.Subtract(field.Box(new(46, -1.5f, 20), new(66, 5.5f, 20.4f)),
                field.Box(new(48, -1.6f, 19.9f), new(52, 2.25f, 20.5f)));
            Surface("east hall entry opening", field, wallShape, new(46, -1.5f, 20), new(66, 5.5f, 20.4f), wall);
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode wallShape = field.Subtract(field.Box(new(46, -1.5f, -15.4f), new(66, 5.5f, -15)),
                field.Box(new(52, -1.6f, -15.5f), new(56, 1.5f, -14.9f)));
            Surface("east hall southern opening", field, wallShape, new(46, -1.5f, -15.4f), new(66, 5.5f, -15), wall);
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode walk = PlanarRecipes.Walkway(field, WalkwayCenterline, 1.75f, -1.5f, -.75f);
            walk = field.Union(walk, field.Box(new(52, -1.5f, -12), new(54, -.75f, -8)));
            walk = field.Intersect(walk, field.Box(new(46, -1.5f, -15), new(66, -.75f, 20)));
            Surface("raised zigzag walkway", field, walk, new(46, -1.5f, -15), new(66, -.75f, 20), floor);
        }
        // Escape steps make the lower basin recoverable without a precise jump.
        Box("basin recovery lower step", new(57.75f, -1.5f, -10), new(58.75f, -1.25f, -7), floor);
        Box("basin recovery upper step", new(55.75f, -1.5f, -10), new(57.75f, -1, -7), floor);

        Box("southern vestibule floor", new(52, -1.25f, -20), new(56, -.75f, -15), floor);
        Box("southern vestibule left", new(51.6f, -.75f, -19.6f), new(52, 3.25f, -15.4f), wall, sideWall: true);
        Box("southern vestibule right", new(56, -.75f, -19.6f), new(56.4f, 3.25f, -15.4f), wall, sideWall: true);
        Box("southern vestibule ceiling", new(52, 1.5f, -19.6f), new(56, 3.25f, -15.4f), trim);
        Box(DoorSurface, DoorMin, DoorMax, door);
        Box("southern room floor", new(44, -1.25f, -32), new(64, -.75f, -20), floor);
        Box("southern room ceiling", new(44, 3.25f, -32), new(64, 3.65f, -20), ceiling);
        Box("southern room west wall", new(43.6f, -.75f, -32), new(44, 3.25f, -20), wall, sideWall: true);
        Box("southern room east wall", new(64, -.75f, -32), new(64.4f, 3.25f, -20), wall, sideWall: true);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode end = field.Subtract(field.Box(new(44, -.75f, -32.4f), new(64, 3.25f, -32)),
                field.Box(new(52, -.85f, -32.5f), new(56, 2.25f, -31.9f)));
            Surface("southern room terminal opening", field, end, new(44, -.75f, -32.4f), new(64, 3.25f, -32), wall);
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode wallShape = field.Subtract(field.Box(new(44, -.75f, -20), new(64, 3.25f, -19.6f)),
                field.Box(new(52, -.85f, -20.1f), new(56, 1.5f, -19.5f)));
            Surface("southern room entry", field, wallShape, new(44, -.75f, -20), new(64, 3.25f, -19.6f), wall);
        }
        Box("southern west soffit", new(44, 1.5f, -32), new(49, 3.25f, -20), trim);
        Box("southern east soffit", new(59, 1.5f, -32), new(64, 3.25f, -20), trim);
        Vector3 across = Vector3.Normalize(new Vector3(2, 0, 4));
        join?.Invoke(new("east connector ceiling-wall", "east connector ceiling", "east connector shell",
            new Vector3(50, 2.25f, 25) + across * .2f, new(1.6f, 0, -.8f), across * .15f));
    }
}
