using System.Numerics;

namespace CarGame;

internal readonly record struct SteeringAngles(float FrontLeftRadians, float FrontRightRadians)
{
    public float AverageRadians => (FrontLeftRadians + FrontRightRadians) * 0.5f;
}

internal static class VehicleMath
{
    public static Vector2 Forward(float angleRadians) => new(MathF.Sin(angleRadians), -MathF.Cos(angleRadians));

    public static Vector2 Right(float angleRadians) => new(MathF.Cos(angleRadians), MathF.Sin(angleRadians));

    public static float SteeringAngle(float steering, float longitudinalSpeed, VehicleDefinition definition)
    {
        ChassisSettings chassis = definition.Chassis;
        float speedRatio = Math.Clamp(
            MathF.Abs(longitudinalSpeed) / chassis.MaximumForwardSpeedMetersPerSecond,
            0.0f,
            1.0f);
        float steeringScale = 1.0f + ((chassis.HighSpeedSteeringScale - 1.0f) * speedRatio);
        float maximumAngleRadians = chassis.MaximumSteeringAngleDegrees * (MathF.PI / 180.0f);
        return steering * steeringScale * maximumAngleRadians;
    }

    public static SteeringAngles AckermannSteering(float centerAngleRadians, ChassisSettings chassis)
    {
        float magnitude = MathF.Abs(centerAngleRadians);
        if (magnitude < 0.0001f)
        {
            return default;
        }

        float turnRadius = chassis.WheelbaseMeters / MathF.Tan(magnitude);
        float inner = MathF.Atan(chassis.WheelbaseMeters / MathF.Max(0.05f, turnRadius - (chassis.AxleTrackMeters * 0.5f)));
        float outer = MathF.Atan(chassis.WheelbaseMeters / (turnRadius + (chassis.AxleTrackMeters * 0.5f)));
        float sign = MathF.Sign(centerAngleRadians);
        return centerAngleRadians > 0.0f
            ? new SteeringAngles(sign * outer, sign * inner)
            : new SteeringAngles(sign * inner, sign * outer);
    }
}
