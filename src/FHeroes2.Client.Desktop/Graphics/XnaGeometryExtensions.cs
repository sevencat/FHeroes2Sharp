using FHeroes2.Core.Geometry;
using Microsoft.Xna.Framework;
using EnginePoint = FHeroes2.Core.Geometry.Point;

namespace FHeroes2.Client.Desktop.Graphics;

/// <summary>Bridges the engine's own geometry types with MonoGame's.</summary>
public static class XnaGeometryExtensions
{
    public static Rectangle ToXnaRectangle(this in Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    public static Rect ToRect(this Rectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    public static Vector2 ToVector2(this EnginePoint point) => new(point.X, point.Y);
}
