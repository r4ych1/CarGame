using Xunit;

namespace CarGame.Tests;

public sealed class VehiclePhysicsTests
{
    private const float FixedTimeStep = 1.0f / 60.0f;
    private const int SubSteps = 4;

    [Fact]
    public void SteeringAloneDoesNotRotateStationaryVehicle()
    {
        using GameWorld world = new();

        Step(world, new VehicleInput(0.0f, 1.0f), 120);

        Assert.Equal(0.0f, world.Vehicle.RotationRadians, 0.0001f);
    }

    [Fact]
    public void FrontAxleSteeringPullsMovingVehicleIntoRightTurn()
    {
        using GameWorld world = new();

        Step(world, new VehicleInput(1.0f, 1.0f), 120);

        Assert.True(world.Vehicle.RotationRadians > 0.1f);
        Assert.True(world.Vehicle.Position.X > 1.0f);
    }

    [Fact]
    public void ReverseSteeringTurnsInDirectionExpectedForBackingCar()
    {
        using GameWorld world = new();

        Step(world, new VehicleInput(-1.0f, 1.0f), 180);

        Assert.True(world.Vehicle.RotationRadians < -0.05f);
        Assert.True(world.Vehicle.Position.X > 0.25f);
    }

    [Fact]
    public void ThrottleAndSteeringKeepEveryWheelFiniteAndUseFrontGrip()
    {
        using GameWorld world = new();

        Step(world, new VehicleInput(1.0f, 0.65f), 240);

        FourWheels<WheelDiagnostics> wheels = world.Vehicle.Diagnostics.Wheels;
        AssertWheelFinite(wheels.FrontLeft);
        AssertWheelFinite(wheels.FrontRight);
        AssertWheelFinite(wheels.RearLeft);
        AssertWheelFinite(wheels.RearRight);
        Assert.True(wheels.FrontLeft.GripUsage > 0.0f || wheels.FrontRight.GripUsage > 0.0f);
    }

    [Fact]
    public void HardBrakingActivatesAbsAndSlowsWithoutSustainedLock()
    {
        using GameWorld world = new();
        Step(world, new VehicleInput(1.0f, 0.0f), 240);
        float initialSpeed = world.Vehicle.SpeedMetersPerSecond;
        bool sawAbs = false;
        int consecutiveHighSpeedLockSteps = 0;
        int longestHighSpeedLock = 0;

        for (int index = 0; index < 180; index++)
        {
            world.Step(new VehicleInput(-1.0f, 0.15f), FixedTimeStep, SubSteps);
            sawAbs |= world.Vehicle.Diagnostics.AnyAbsIntervention;
            float speed = world.Vehicle.SpeedMetersPerSecond;
            float wheelSurfaceSpeed =
                MathF.Abs(world.Vehicle.SimulationState.Wheels.FrontLeft.AngularSpeedRadiansPerSecond) *
                VehicleCatalog.FirstCar.Tires.WheelRadiusMeters;
            consecutiveHighSpeedLockSteps = speed > 3.0f && wheelSurfaceSpeed < speed * 0.1f
                ? consecutiveHighSpeedLockSteps + 1
                : 0;
            longestHighSpeedLock = Math.Max(longestHighSpeedLock, consecutiveHighSpeedLockSteps);
        }

        Assert.True(initialSpeed > 2.0f);
        Assert.True(sawAbs);
        Assert.True(world.Vehicle.SpeedMetersPerSecond < initialSpeed);
        Assert.True(longestHighSpeedLock < 15);
        Assert.True(float.IsFinite(world.Vehicle.SimulationState.Wheels.FrontLeft.AngularSpeedRadiansPerSecond));
    }

    [Fact]
    public void ResetRestoresPlanarAndFourWheelState()
    {
        using GameWorld world = new();
        Step(world, new VehicleInput(1.0f, 0.7f), 180);
        Step(world, new VehicleInput(-1.0f, 0.0f), 20);

        world.ResetVehicle();

        Assert.Equal(0.0f, world.Vehicle.RotationRadians, 0.0001f);
        Assert.Equal(0.0f, world.Vehicle.SpeedMetersPerSecond, 0.0001f);
        Assert.Equal(1, world.Vehicle.SimulationState.CurrentGear);
        Assert.Equal(VehicleCatalog.FirstCar.Powertrain.IdleRevolutionsPerMinute, world.Vehicle.SimulationState.EngineRevolutionsPerMinute);
        Assert.Equal(0.0f, world.Vehicle.SimulationState.HeaveMeters);
        Assert.Equal(0.0f, world.Vehicle.SimulationState.RollRadians);
        Assert.Equal(0.0f, world.Vehicle.SimulationState.PitchRadians);
        Assert.Equal(0.0f, world.Vehicle.SimulationState.Wheels.FrontLeft.AngularSpeedRadiansPerSecond);
        Assert.Equal(1.0f, world.Vehicle.SimulationState.Wheels.FrontLeft.AbsModulation);
    }

    private static void Step(GameWorld world, VehicleInput input, int stepCount)
    {
        for (int index = 0; index < stepCount; index++)
        {
            world.Step(input, FixedTimeStep, SubSteps);
        }
    }

    private static void AssertWheelFinite(WheelDiagnostics wheel)
    {
        Assert.True(float.IsFinite(wheel.NormalLoadNewtons));
        Assert.True(float.IsFinite(wheel.SlipAngleRadians));
        Assert.True(float.IsFinite(wheel.SlipRatio));
        Assert.True(float.IsFinite(wheel.LongitudinalForceNewtons));
        Assert.True(float.IsFinite(wheel.LateralForceNewtons));
        Assert.True(float.IsFinite(wheel.GripUsage));
    }
}
