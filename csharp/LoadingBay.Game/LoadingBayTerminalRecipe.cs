using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Manually refined sectors 78–84, aligned with the study's southern room.</summary>
internal static class LoadingBayTerminalRecipe
{
    internal const string DoorSurface = "terminal study door";
    internal static readonly Vector3 DoorMin = new(53, -.75f, -35.25f);
    internal static readonly Vector3 DoorMax = new(55, 1.5f, -34.75f);

    internal static void Compose(IImplicitSurfacesService service, Material wall, Material floor,
        Material trim, Material ceiling, Material door, Action<RecipeSurface> emit)
    {
        RecipeWriter writer = new(service, new(.125f, 0, .2f, ImplicitMaterialBoundaryMode.Interpolated),
            surface => emit(surface.Material.Equals(floor) || surface.Material.Equals(ceiling)
                ? surface with { Sampling = surface.Sampling with { TextureRepeats = .5f } } : surface));
        void Box(string name, Vector3 min, Vector3 max, Material material, bool sideWall = false)
        {
            using ImplicitRecipe field = writer.Begin();
            Transform placement = LoadingBayRoomRecipe.Identity;
            if (sideWall)
            {
                (min, max) = (new(-max.Z, min.Y, min.X), new(-min.Z, max.Y, max.X));
                placement = new(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One);
            }
            writer.Surface(name, field, field.Box(min, max), min - new Vector3(.2f), max + new Vector3(.2f), material, placement);
        }
        // One floor owns the approach, constriction, and chamber; adjacent slabs end at z=-32.
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode floorShape = field.Union(field.Box(new(52, -1.25f, -34), new(56, -.75f, -32)),
                field.Box(new(53, -1.25f, -36), new(55, -.75f, -34)));
            floorShape = field.Union(floorShape, field.Box(new(51, -1.25f, -42), new(57, -.75f, -36)));
            writer.Surface("terminal connected floor", field, floorShape, new(50.8f, -1.45f, -42.2f),
                new(57.2f, -.55f, -31.8f), floor, LoadingBayRoomRecipe.Identity);
        }
        // The southern-room end wall owns the reveal through z=-32.4.
        Box("terminal approach west", new(51.6f, -.75f, -33.6f), new(52, 2.25f, -32.4f), wall, true);
        Box("terminal approach east", new(56, -.75f, -33.6f), new(56.4f, 2.25f, -32.4f), wall, true);
        Box("terminal approach ceiling", new(52, 2.25f, -33.6f), new(56, 2.65f, -32.4f), ceiling);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode frame = field.Subtract(field.Box(new(51.6f, -.75f, -34), new(56.4f, 2.65f, -33.6f)),
                field.Box(new(53, -.85f, -34.1f), new(55, 1.5f, -33.5f)));
            writer.Surface("terminal narrowing frame", field, frame, new(51.4f, -.95f, -34.2f),
                new(56.6f, 2.85f, -33.4f), trim, LoadingBayRoomRecipe.Identity);
        }
        Box("terminal throat west", new(52.6f, -.75f, -35.6f), new(53, 2.25f, -34), wall, true);
        Box("terminal throat east", new(55, -.75f, -35.6f), new(55.4f, 2.25f, -34), wall, true);
        Box("terminal throat ceiling", new(53, 1.5f, -35.6f), new(55, 2.25f, -34), ceiling);
        Box(DoorSurface, DoorMin, DoorMax, door);
        using (ImplicitRecipe field = writer.Begin())
        {
            ImplicitNode frame = field.Subtract(field.Box(new(50.6f, -.75f, -36), new(57.4f, 2.75f, -35.6f)),
                field.Box(new(53, -.85f, -36.1f), new(55, 1.5f, -35.5f)));
            writer.Surface("terminal chamber entry", field, frame, new(50.4f, -.95f, -36.2f),
                new(57.6f, 2.95f, -35.4f), wall, LoadingBayRoomRecipe.Identity);
        }
        Box("terminal chamber west", new(50.6f, -.75f, -42), new(51, 2.75f, -36), wall, true);
        Box("terminal chamber east", new(57, -.75f, -42), new(57.4f, 2.75f, -36), wall, true);
        Box("terminal chamber rear", new(50.6f, -.75f, -42.4f), new(57.4f, 2.75f, -42), wall);
        Box("terminal chamber ceiling", new(51, 2.75f, -42), new(57, 3.15f, -36), ceiling);
        // Source sectors 79/83 suggest suspended strips; keep their undersides visibly separate.
        Box("terminal approach light housing", new(53.5f, 1.75f, -33.4f), new(54.5f, 2.25f, -33.15f), trim);
        Box("terminal rear light housing", new(53.5f, 2.25f, -41.5f), new(54.5f, 2.75f, -41.25f), trim);
    }
}
