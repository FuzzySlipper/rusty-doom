using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Manually refined sectors 2/3/4/0/7: dogleg, door vestibule and northern room.</summary>
internal static class LoadingBayNorthWingRecipe
{
    internal const string DoorSurface = "north wing door";
    internal static readonly Vector3 DoorMin = new(8.1f, 0, 30);
    internal static readonly Vector3 DoorMax = new(8.4f, 2.25f, 34);
    // Reference mapping for this extension: (mapX - 1280) / 32, 20 + (mapY + 2880) / 32.
    // The existing stylized spawn room is retained. These points are authored, not parsed at runtime.
    private static readonly Vector2[] NearArm = [new(-2, 20), new(-1, 31), new(3.25f, 29), new(2, 20)];
    private static readonly Vector2[] FarArm = [new(-1, 31), new(6, 34), new(6, 30), new(3.25f, 29)];

    internal static void Compose(IImplicitSurfacesService service, Material wall, Material floor,
        Material carpet, Material trim, Material ceiling, Material door, Action<RecipeSurface> emit, Action<RecipeJoin>? join = null)
    {
        RecipeWriter writer = new(service, new(.2f, 0, .2f, ImplicitMaterialBoundaryMode.Interpolated),
            surface => emit(surface.Material.Equals(floor) || surface.Material.Equals(carpet) || surface.Material.Equals(ceiling)
                ? surface with { Sampling = surface.Sampling with { TextureRepeats = .5f } } : surface));
        void Surface(string name, ImplicitRecipe field, ImplicitNode solid, Vector3 min, Vector3 max, Material material)
            => writer.Surface(name, field, solid, min - new Vector3(.3f), max + new Vector3(.3f), material, LoadingBayRoomRecipe.Identity);
        void Box(string name, Vector3 min, Vector3 max, Material material, bool sideWall = false)
        {
            using ImplicitRecipe field = writer.Begin();
            Transform placement = LoadingBayRoomRecipe.Identity;
            writer.Surface(name, field, field.Box(min, max), min - new Vector3(.3f), max + new Vector3(.3f), material, placement, textureMapping: sideWall
                ? ImplicitTextureMapping.Basis(-Vector3.UnitZ, Vector3.UnitY, new Vector2(.2f), Vector2.Zero) : null);
        }
        ImplicitNode Corridor(ImplicitRecipe field, float bottom, float top, float expansion = 0)
            => field.Union(PlanarRecipes.ConvexPrism(field, NearArm, bottom, top, expansion), PlanarRecipes.ConvexPrism(field, FarArm, bottom, top, expansion));
        // Slabs cover the wall thickness: a shared contact patch survives extraction
        // at diagonal corners, whereas matching only the inner edge leaves cracks.
        using (ImplicitRecipe field = writer.Begin())
            Surface("dogleg floor", field, field.Intersect(Corridor(field, -.5f, 0, .4f),
                field.Union(field.Box(new(-3, -.6f, 20.7f), new(6, .1f, 35)),
                    field.Box(new(-2, -.6f, 20), new(2, .1f, 20.7f)))), new(-3, -.5f, 20), new(6, 0, 35), floor);
        using (ImplicitRecipe field = writer.Begin())
            Surface("dogleg ceiling", field, field.Intersect(Corridor(field, 4.5f, 4.9f, .4f),
                field.Box(new(-3, 4.5f, 20.7f), new(6, 4.9f, 35))), new(-3, 4.5f, 20), new(6, 4.9f, 35), ceiling);
        using (ImplicitRecipe field = writer.Begin())
        {
            // Seat wall tops inside the ceiling slab; independent DC corners must not
            // rely on a zero-depth contact at y=4.5. The visible ceiling stays at 4.5.
            ImplicitNode seat = field.Intersect(Corridor(field, 4.4f, 4.6f, .35f),
                field.Box(new(-3, 4.4f, 20.9f), new(5.9f, 4.6f, 35)));
            ImplicitNode walls = field.Subtract(field.Union(Corridor(field, 0, 4.5f, .4f), seat),
                Corridor(field, -.1f, 4.7f));
            // The old portal owns its reveal through z=20.7. The next vestibule starts at x=6.
            walls = field.Intersect(walls, field.Box(new(-3, 0, 20.7f), new(6, 4.6f, 35)));
            walls = field.Subtract(walls, field.Box(new(5.5f, -.1f, 30), new(6.1f, 4.7f, 34)));
            Surface("dogleg wall shell", field, walls, new(-3, 0, 20.7f), new(6, 4.6f, 35), wall);
        }
        Box("vestibule floor", new(6, -.5f, 30), new(12, 0, 34), floor);
        Box("vestibule near wall", new(6, 0, 29.6f), new(11.6f, 4.5f, 30), wall);
        Box("vestibule far wall", new(6, 0, 34), new(11.6f, 4.5f, 34.4f), wall);
        Box("vestibule approach ceiling", new(6, 2.75f, 30), new(8, 4.9f, 34), ceiling);
        Box("door header", new(8, 2.25f, 30), new(8.5f, 4.9f, 34), trim, sideWall: true);
        Box("vestibule inner ceiling", new(8.5f, 2.25f, 30), new(11.6f, 4.9f, 34), ceiling);
        // The moving mesh and obstacle share translation; the mesh local frame keeps V upright.
        Box(DoorSurface, DoorMin, DoorMax, door, sideWall: true);

        Box("north hall floor", new(12, -.5f, 24), new(38, 0, 44), floor);
        Box("north hall ceiling", new(12, 7, 24), new(38, 7.4f, 44), ceiling);
        Box("north hall rear wall", new(12, 0, 44), new(38, 7, 44.4f), wall);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode opening = field.Subtract(field.Box(new(12, 0, 23.6f), new(38, 7, 24)),
                field.Box(new(18, -.1f, 23.5f), new(22, 3, 24.1f)));
            Surface("north hall courtyard opening", field, opening, new(12, 0, 23.6f), new(38, 7, 24), wall);
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode exit = field.Subtract(field.Box(new(-44, 0, 38), new(-24, 7, 38.4f)),
                field.Box(new(-30, -.4f, 37.9f), new(-26, 2.25f, 38.5f)));
            writer.Surface("north hall east opening", field, exit, new(-44.3f, -.3f, 37.7f), new(-23.7f, 7.3f, 38.7f), wall,
                new(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One));
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            // Extract the entry wall in its own local frame, preserving upright panel UVs.
            ImplicitNode entry = field.Subtract(field.Box(new(-44, 0, 11.6f), new(-24, 7, 12)),
                field.Box(new(-34, -.1f, 11.5f), new(-30, 2.25f, 12.1f)));
            writer.Surface("north hall entry wall", field, entry, new(-44.3f, -.3f, 11.3f), new(-23.7f, 7.3f, 12.3f), wall,
                new(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One));
        }
        // Source sectors 10/12 are low overhead bays, not full-height solid columns.
        foreach (float z in new[] { 28f, 38f })
            Box("hanging service bay", new(16, 3, z), new(22, 7, z + 2), trim);
        // Source 8/51/52 suggest three 8-unit rises. At this study scale each is 0.25.
        for (int step = 0; step < 3; step++)
            Box("north dais step", new(27 + step, 0, 30), new(step == 2 ? 32 : 28 + step, .25f * (step + 1), 38), carpet);
        Vector3 along = new(2.5f, 0, 2.5f * 3 / 7);
        Vector3 across = Vector3.Normalize(new(-3, 0, 7)) * .15f;
        Vector3 edge = new Vector3(2.5f, 0, 32.5f) + Vector3.Normalize(new(-3, 0, 7)) * .2f;
        join?.Invoke(new("dogleg north ceiling-wall", "dogleg ceiling", "dogleg wall shell", edge with { Y = 4.5f }, along, across));
        join?.Invoke(new("dogleg north floor-wall", "dogleg floor", "dogleg wall shell", edge, along, across));
        join?.Invoke(new("door approach ceiling transition", "dogleg ceiling", "vestibule approach ceiling",
            new(6, 4.7f, 32), new(0, .12f, 0), new(0, 0, 1.8f)));
    }

}
