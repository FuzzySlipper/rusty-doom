using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Product construction vocabulary; Engine owns evaluation and extraction.</summary>
internal static class LoadingBayRecipeShapes
{
    internal static ImplicitNode Prism(ImplicitRecipe field, Vector2[] clockwise, float bottom, float top, float expansion = 0)
    {
        ImplicitNode solid = field.Intersect(field.Plane(Vector3.UnitY, top), field.Plane(-Vector3.UnitY, -bottom));
        for (int i = 0; i < clockwise.Length; i++)
        {
            Vector2 a = clockwise[i], edge = clockwise[(i + 1) % clockwise.Length] - a;
            Vector3 normal = Vector3.Normalize(new(-edge.Y, 0, edge.X));
            solid = field.Intersect(solid, field.Plane(normal, normal.X * a.X + normal.Z * a.Y + expansion));
        }
        return solid;
    }

    // Square-capped segments union into one retained raised walkway, without stacked coplanar floors.
    internal static ImplicitNode Walkway(ImplicitRecipe field, Vector2[] centerline, float halfWidth, float bottom, float top)
    {
        ImplicitNode? result = null;
        for (int i = 0; i < centerline.Length - 1; i++)
        {
            Vector2 a = centerline[i], b = centerline[i + 1], direction = Vector2.Normalize(b - a);
            Vector2 normal = new(-direction.Y, direction.X);
            a -= direction * halfWidth;
            b += direction * halfWidth;
            ImplicitNode part = Prism(field, [a - normal * halfWidth, a + normal * halfWidth, b + normal * halfWidth, b - normal * halfWidth], bottom, top);
            result = result is { } previous ? field.Union(previous, part) : part;
        }
        return result ?? throw new ArgumentException("A walkway needs at least two points.", nameof(centerline));
    }
}
