using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Recipes use map east/north; Engine world uses east/-Z.</summary>
internal static class LoadingBayStudyCoordinates
{
    internal static Vector3 World(Vector3 point) => new(point.X, point.Y, -point.Z);
    internal static Vector3 Minimum(Vector3 min, Vector3 max) => new(min.X, min.Y, -max.Z);
    internal static Vector3 Maximum(Vector3 min, Vector3 max) => new(max.X, max.Y, -min.Z);
    internal static LoadingBayStudyDoor Door(string name, ulong entity, Vector3 min, Vector3 max)
        => new(new(name, entity, Minimum(min, max), Maximum(min, max)));

    internal static RecipeSurface World(IImplicitSurfacesService service, RecipeSurface surface)
    {
        // Reflect the field before extraction, so Engine emits outward winding and normals.
        // Conjugating the proper placement rotation preserves the recipe's local UV axes.
        Quaternion q = surface.Placement.Rotation;
        return surface with
        {
            Root = service.Transform(new(surface.Field, surface.Root,
                new(Vector3.Zero, Quaternion.Identity, new(1, 1, -1)))),
            Sampling = surface.Sampling with { TextureMapping = surface.Sampling.TextureMapping.Enabled
                && surface.Sampling.TextureMapping.Projection == ImplicitTextureProjection.Basis
                ? surface.Sampling.TextureMapping with { UAxis = World(surface.Sampling.TextureMapping.UAxis), VAxis = World(surface.Sampling.TextureMapping.VAxis) }
                : surface.Sampling.TextureMapping },
            Min = Minimum(surface.Min, surface.Max),
            Max = Maximum(surface.Min, surface.Max),
            Placement = new(World(surface.Placement.Translation), new(-q.X, -q.Y, q.Z, q.W), surface.Placement.Scale),
        };
    }
}
