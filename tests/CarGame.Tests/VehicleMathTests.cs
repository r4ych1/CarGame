using System.Numerics;
using Xunit;

namespace CarGame.Tests;

public sealed class VehicleMathTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void ZeroRotationFacesUp()
    {
        Assert.Equal(new Vector2(0.0f, -1.0f), VehicleMath.Forward(0.0f));
    }

    [Fact]
    public void ForwardAndRightRemainPerpendicular()
    {
        const float angle = 1.23f;

        Assert.Equal(0.0f, Vector2.Dot(VehicleMath.Forward(angle), VehicleMath.Right(angle)), Tolerance);
    }

    [Fact]
    public void NeutralSteeringKeepsFrontWheelsStraight()
    {
        float steeringAngle = VehicleMath.SteeringAngle(0.0f, 10.0f, VehicleCatalog.FirstCar);

        Assert.Equal(0.0f, steeringAngle);
    }

    [Fact]
    public void SteeringUsesMaximumAngleAtRest()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        float maximumAngleRadians = definition.Chassis.MaximumSteeringAngleDegrees * (MathF.PI / 180.0f);

        Assert.Equal(maximumAngleRadians, VehicleMath.SteeringAngle(1.0f, 0.0f, definition), Tolerance);
    }

    [Fact]
    public void HighSpeedSteeringIsReduced()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        float maximumAngleRadians = definition.Chassis.MaximumSteeringAngleDegrees * (MathF.PI / 180.0f);
        float steeringAngle = VehicleMath.SteeringAngle(
            1.0f,
            definition.Chassis.MaximumForwardSpeedMetersPerSecond,
            definition);

        Assert.Equal(maximumAngleRadians * definition.Chassis.HighSpeedSteeringScale, steeringAngle, Tolerance);
    }

    [Fact]
    public void ReverseDoesNotInvertPhysicalFrontWheelAngle()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;

        float forwardAngle = VehicleMath.SteeringAngle(1.0f, 8.0f, definition);
        float reverseAngle = VehicleMath.SteeringAngle(1.0f, -8.0f, definition);

        Assert.Equal(forwardAngle, reverseAngle, Tolerance);
    }

    [Theory]
    [InlineData(0.4f)]
    [InlineData(-0.4f)]
    public void AckermannSteeringGivesInnerWheelLargerAngle(float centerAngle)
    {
        SteeringAngles angles = VehicleMath.AckermannSteering(centerAngle, VehicleCatalog.FirstCar.Chassis);

        if (centerAngle > 0.0f)
        {
            Assert.True(MathF.Abs(angles.FrontRightRadians) > MathF.Abs(angles.FrontLeftRadians));
        }
        else
        {
            Assert.True(MathF.Abs(angles.FrontLeftRadians) > MathF.Abs(angles.FrontRightRadians));
        }
    }
}
