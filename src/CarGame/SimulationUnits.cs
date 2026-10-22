using System.Numerics;
using Box2D.NET;

namespace CarGame;

internal static class SimulationUnits
{
    public const float PixelsPerMeter = 32.0f;

    public static Vector2 ToPixels(B2Vec2 meters)
    {
        return new Vector2(meters.X, meters.Y) * PixelsPerMeter;
    }
}
