using System.Numerics;

namespace CarGame;

internal readonly record struct ShotObstacle(
    Vector2 PositionMeters,
    Vector2 SizeMeters,
    float RotationRadians);

internal sealed class GuardRound
{
    internal const float SpeedMetersPerSecond = 65.0f;
    private const float MaximumLifetimeSeconds = 0.8f;
    private const float ImpactLifetimeSeconds = 0.14f;
    private float ageSeconds;
    private bool vehicleImpactConsumed;

    public GuardRound(Vector2 positionMeters, Vector2 direction)
    {
        PositionMeters = positionMeters;
        PreviousPositionMeters = positionMeters;
        VelocityMetersPerSecond = Vector2.Normalize(direction) * SpeedMetersPerSecond;
    }

    public Vector2 PositionMeters { get; private set; }
    public Vector2 PreviousPositionMeters { get; private set; }
    public Vector2 VelocityMetersPerSecond { get; }
    public bool HasImpacted { get; private set; }
    public bool HitVehicle { get; private set; }
    public bool IsExpired => HasImpacted
        ? ageSeconds >= ImpactLifetimeSeconds
        : ageSeconds >= MaximumLifetimeSeconds;

    public void Step(
        float timeStep,
        bool canHitVehicle,
        Vector2 vehiclePositionMeters,
        float vehicleRotationRadians,
        Vector2 vehicleSizeMeters,
        IReadOnlyList<ShotObstacle>? obstacles = null)
    {
        ageSeconds += timeStep;
        if (HasImpacted)
        {
            return;
        }

        PreviousPositionMeters = PositionMeters;
        Vector2 nextPosition = PositionMeters + VelocityMetersPerSecond * timeStep;
        float nearestIntersection = float.PositiveInfinity;
        bool hitVehicle = false;
        if (canHitVehicle && TryIntersectOrientedRectangle(
            PreviousPositionMeters,
            nextPosition,
            vehiclePositionMeters,
            vehicleRotationRadians,
            vehicleSizeMeters,
            out float intersectionFraction))
        {
            nearestIntersection = intersectionFraction;
            hitVehicle = true;
        }

        foreach (ShotObstacle obstacle in obstacles ?? [])
        {
            if (TryIntersectOrientedRectangle(
                PreviousPositionMeters,
                nextPosition,
                obstacle.PositionMeters,
                obstacle.RotationRadians,
                obstacle.SizeMeters,
                out float obstacleIntersection) &&
                obstacleIntersection <= nearestIntersection)
            {
                nearestIntersection = obstacleIntersection;
                hitVehicle = false;
            }
        }

        if (float.IsFinite(nearestIntersection))
        {
            PositionMeters = Vector2.Lerp(PreviousPositionMeters, nextPosition, nearestIntersection);
            HasImpacted = true;
            HitVehicle = hitVehicle;
            ageSeconds = 0.0f;
            return;
        }

        PositionMeters = nextPosition;
    }

    public bool ConsumeVehicleImpact()
    {
        if (!HitVehicle || vehicleImpactConsumed)
        {
            return false;
        }

        vehicleImpactConsumed = true;
        return true;
    }

    internal static bool TryIntersectOrientedRectangle(
        Vector2 segmentStart,
        Vector2 segmentEnd,
        Vector2 rectangleCenter,
        float rectangleRotationRadians,
        Vector2 rectangleSize,
        out float intersectionFraction)
    {
        Vector2 right = VehicleMath.Right(rectangleRotationRadians);
        Vector2 forward = VehicleMath.Forward(rectangleRotationRadians);
        Vector2 startOffset = segmentStart - rectangleCenter;
        Vector2 endOffset = segmentEnd - rectangleCenter;
        Vector2 localStart = new(Vector2.Dot(startOffset, right), Vector2.Dot(startOffset, forward));
        Vector2 localEnd = new(Vector2.Dot(endOffset, right), Vector2.Dot(endOffset, forward));
        Vector2 delta = localEnd - localStart;
        Vector2 halfSize = rectangleSize * 0.5f;
        float entry = 0.0f;
        float exit = 1.0f;

        if (!ClipAxis(localStart.X, delta.X, -halfSize.X, halfSize.X, ref entry, ref exit) ||
            !ClipAxis(localStart.Y, delta.Y, -halfSize.Y, halfSize.Y, ref entry, ref exit))
        {
            intersectionFraction = 0.0f;
            return false;
        }

        intersectionFraction = entry;
        return true;
    }

    private static bool ClipAxis(
        float start,
        float delta,
        float minimum,
        float maximum,
        ref float entry,
        ref float exit)
    {
        if (MathF.Abs(delta) < 0.00001f)
        {
            return start >= minimum && start <= maximum;
        }

        float first = (minimum - start) / delta;
        float second = (maximum - start) / delta;
        if (first > second)
        {
            (first, second) = (second, first);
        }

        entry = MathF.Max(entry, first);
        exit = MathF.Min(exit, second);
        return entry <= exit;
    }
}
