using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Manual courtyard-to-east loop informed by sectors 16–23, 48–50, 63–68 and 77.</summary>
internal static class LoadingBaySouthPassageRecipe
{
    internal const string DoorSurface = "lower passage east door";
    internal static readonly Vector3 DoorMin = new(51.1f, -.75f, -12);
    internal static readonly Vector3 DoorMax = new(51.4f, 1.5f, -8);
    private static readonly Vector2[] LowerRoute = [new(26, -11), new(26, -13),
        new(31, -16), new(34, -16), new(38, -10), new(42, -10)];

    internal static void Compose(IImplicitSurfacesService service, Material wall, Material floor,
        Material trim, Material ceiling, Material door, Action<RecipeSurface> emit)
    {
        RecipeWriter writer = new(service, new(.125f, 0, .2f, ImplicitMaterialBoundaryMode.Interpolated),
            surface => emit(surface.Material.Equals(floor) || surface.Material.Equals(ceiling)
                ? surface with { Sampling = surface.Sampling with { TextureRepeats = .5f } } : surface));
        void Surface(string name, ImplicitRecipe field, ImplicitNode shape, Vector3 min, Vector3 max, Material material)
            => writer.Surface(name, field, shape, min - new Vector3(.25f), max + new Vector3(.25f), material, LoadingBayRoomRecipe.Identity);
        void Box(string name, Vector3 min, Vector3 max, Material material, bool sideWall = false)
        {
            using ImplicitRecipe field = writer.Begin();
            Transform placement = LoadingBayRoomRecipe.Identity;
            writer.Surface(name, field, field.Box(min, max), min - new Vector3(.25f), max + new Vector3(.25f), material, placement, textureMapping: sideWall
                ? ImplicitTextureMapping.Basis(-Vector3.UnitZ, Vector3.UnitY, new Vector2(.2f), Vector2.Zero) : null);
        }
        ImplicitNode Lower(ImplicitRecipe field, float bottom, float top, float halfWidth = 2)
            => field.Intersect(PlanarRecipes.Walkway(field, LowerRoute, halfWidth, bottom, top),
                field.Union(field.Box(new(23, bottom, -19), new(42, top, -11)),
                    field.Box(new(30, bottom, -19), new(42, top, -7))));
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode stairs = field.Box(new(24, -4.75f, -6), new(28, -1.75f, -4));
            for (int i = 0; i < 10; i++)
                stairs = field.Union(stairs, field.Box(new(24, -4.75f, -6.5f - i * .5f),
                    new(28, -2 - i * .25f, -6 - i * .5f)));
            stairs = field.Union(stairs, Lower(field, -4.75f, -4.25f));
            Surface("southern descent and low passage floor", field, stairs, new(23, -4.75f, -19), new(42, -1.75f, -4), floor);
        }
        Box("southern descent west wall", new(23.6f, -4.25f, -11), new(24, .5f, -4.4f), wall, true);
        Box("southern descent east wall", new(28, -4.25f, -11), new(28.4f, .5f, -4.4f), wall, true);
        Box("southern descent ceiling", new(24, .5f, -11), new(28, .9f, -4.4f), ceiling);
        Box("southern low ceiling transition", new(24, -1.25f, -11.4f), new(28, .9f, -11), trim);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode roof = field.Intersect(Lower(field, -1.25f, -.85f),
                field.Union(field.Box(new(23, -1.25f, -19), new(41.6f, -.85f, -11.4f)),
                    field.Box(new(30, -1.25f, -19), new(41.6f, -.85f, -7))));
            Surface("southern low passage ceiling", field, roof, new(23, -1.25f, -19), new(41.6f, -.85f, -7), ceiling);
        }
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode shell = field.Subtract(Lower(field, -4.25f, -1.25f, 2.4f), Lower(field, -4.35f, -1.15f));
            Surface("southern low passage walls", field, shell, new(23, -4.25f, -19), new(42, -1.25f, -7), wall);
        }
        Box("southern ascent ceiling transition", new(41.6f, -1.25f, -12), new(42, 1.9f, -8), trim, true);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode stairs = field.Box(new(42, -4.75f, -12), new(42.5f, -4, -8));
            for (int i = 1; i < 14; i++)
                stairs = field.Union(stairs, field.Box(new(42 + i * .5f, -4.75f, -12),
                    new(42.5f + i * .5f, -4 + i * .25f, -8)));
            stairs = field.Union(stairs, field.Box(new(49, -4.75f, -12), new(52, -.75f, -8)));
            Surface("southern east ascent and door landing", field, stairs, new(42, -4.75f, -12), new(52, -.75f, -8), floor);
        }
        // Existing east-hall wall owns x=45.6..46. Split adjacent pieces there.
        foreach ((float left, float right) in new[] { (42f, 45.6f), (46f, 52f) })
        {
            Box($"southern ascent north wall {left}", new(left, -4.25f, -8), new(right, 1.5f, -7.6f), wall);
            Box($"southern ascent south wall {left}", new(left, -4.25f, -12.4f), new(right, 1.5f, -12), wall);
            Box($"southern ascent ceiling {left}", new(left, 1.5f, -12), new(right, 1.9f, -8), ceiling);
        }
        Box(DoorSurface, DoorMin, DoorMax, door, true);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode frame = field.Subtract(field.Box(new(8, -4.75f, 52), new(12, 1.9f, 52.4f)),
                field.Box(new(8, -4.85f, 51.9f), new(12, 1.5f, 52.5f)));
            writer.Surface("southern passage east header", field, frame, new(7.75f, -5, 51.75f), new(12.25f, 2.15f, 52.65f), trim,
                new(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One));
        }
    }
}
