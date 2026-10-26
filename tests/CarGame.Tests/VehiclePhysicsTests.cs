using System.Numerics;
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
    public void DirectServiceBrakeSlowsCarWithoutSelectingReverse()
    {
        using GameWorld world = new();
        Step(world, new VehicleInput(1.0f, 0.0f), 180);
        float initialSpeed = world.Vehicle.SpeedMetersPerSecond;

        Step(world, new VehicleInput(0.0f, 0.0f, 1.0f), 90);

        Assert.True(initialSpeed > 2.0f);
        Assert.True(world.Vehicle.SpeedMetersPerSecond < initialSpeed);
        Assert.True(world.Vehicle.SimulationState.CurrentGear > 0);
    }

    [Fact]
    public void CoastDownSettlesAtCompleteRest()
    {
        using GameWorld world = new();
        Step(world, new VehicleInput(1.0f, 0.0f), 120);

        Step(world, default, 2_400);

        Assert.Equal(0.0f, world.Vehicle.SpeedMetersPerSecond);
        Assert.Equal(0.0f, world.Vehicle.SimulationState.Wheels.FrontLeft.AngularSpeedRadiansPerSecond);
        Assert.Equal(0.0f, world.Vehicle.SimulationState.Wheels.FrontRight.AngularSpeedRadiansPerSecond);
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

    [Fact]
    public void ExplosionCreatesNineteenFiniteMovingPiecesOnlyOnce()
    {
        using GameWorld world = new();

        world.ExplodeVehicle();
        world.ExplodeVehicle();
        Step(world, new VehicleInput(1.0f, 1.0f), 30);

        Assert.True(world.Vehicle.IsExploded);
        Assert.Equal(19, world.Vehicle.ExplosionPieces.Count);
        Assert.All(world.Vehicle.ExplosionPieces, piece =>
        {
            Assert.True(float.IsFinite(piece.PositionMeters.X));
            Assert.True(float.IsFinite(piece.PositionMeters.Y));
            Assert.True(float.IsFinite(piece.RotationRadians));
            Assert.True(piece.LinearVelocityMetersPerSecond.Length() > 0.1f);
        });
        Assert.Equal(
            world.Vehicle.Definition.Chassis.MassKilograms,
            world.Vehicle.ExplosionPieces.Sum(piece => piece.MassKilograms),
            0.01f);
        Assert.Equal(6, world.Vehicle.ExplosionPieces.Count(piece =>
            piece.Kind == ExplosionPieceKind.MiscellaneousDebris));
        Assert.Equal(2, world.Vehicle.ExplosionPieces.Count(piece =>
            piece.Kind == ExplosionPieceKind.Bumper));
        Assert.Single(world.Vehicle.ExplosionPieces, piece =>
            piece.Kind == ExplosionPieceKind.EngineBlock);
    }

    [Fact]
    public void ExplosionLaunchesEngineBlockStronglyForward()
    {
        using GameWorld world = new();

        world.ExplodeVehicle();

        ExplosionPiece engine = Assert.Single(world.Vehicle.ExplosionPieces, piece =>
            piece.Kind == ExplosionPieceKind.EngineBlock);
        float forwardSpeed = Vector2.Dot(
            engine.LinearVelocityMetersPerSecond,
            VehicleMath.Forward(0.0f));
        Assert.True(forwardSpeed >= 44.0f);
    }

    [Fact]
    public void ExplosionBurstsOutwardAndGroundResistanceReducesSpeed()
    {
        using GameWorld world = new();
        world.ExplodeVehicle();

        foreach (ExplosionPiece piece in world.Vehicle.ExplosionPieces)
        {
            Vector2 radialDirection = Vector2.Normalize(piece.PositionMeters);
            float outwardSpeed = Vector2.Dot(piece.LinearVelocityMetersPerSecond, radialDirection);
            Assert.True(outwardSpeed >= 17.99f);
        }

        float initialSpeed = world.Vehicle.ExplosionPieces[0].LinearVelocityMetersPerSecond.Length();
        Step(world, default, 60);
        float laterSpeed = world.Vehicle.ExplosionPieces[0].LinearVelocityMetersPerSecond.Length();

        Assert.True(laterSpeed < initialSpeed - 6.0f);
    }

    [Fact]
    public void ExplosionIsDeterministicAndResetRestoresIntactVehicle()
    {
        using GameWorld first = new();
        using GameWorld second = new();
        Step(first, new VehicleInput(1.0f, 0.4f), 90);
        Step(second, new VehicleInput(1.0f, 0.4f), 90);

        first.ExplodeVehicle();
        second.ExplodeVehicle();
        Step(first, default, 20);
        Step(second, default, 20);

        for (int index = 0; index < first.Vehicle.ExplosionPieces.Count; index++)
        {
            Vector2 firstPosition = first.Vehicle.ExplosionPieces[index].PositionMeters;
            Vector2 secondPosition = second.Vehicle.ExplosionPieces[index].PositionMeters;
            Assert.Equal(firstPosition.X, secondPosition.X, 0.0001f);
            Assert.Equal(firstPosition.Y, secondPosition.Y, 0.0001f);
        }

        first.ResetVehicle();

        Assert.False(first.Vehicle.IsExploded);
        Assert.Empty(first.Vehicle.ExplosionPieces);
        Assert.Equal(Vector2.Zero, first.Vehicle.Position);
        first.ExplodeVehicle();
        Assert.True(first.Vehicle.IsExploded);
    }

    [Fact]
    public void SunkVehicleIgnoresControlsUntilReset()
    {
        using GameWorld world = new();
        world.Vehicle.Sink();

        Step(world, new VehicleInput(1.0f, 1.0f), 120);

        Assert.True(world.Vehicle.IsSunk);
        Assert.Equal(0.0f, world.Vehicle.SpeedMetersPerSecond);
        Assert.Equal(Vector2.Zero, world.Vehicle.PositionMeters);

        world.ResetVehicle();
        Assert.False(world.Vehicle.IsSunk);
    }

    [Fact]
    public void SunkVehicleCannotExplodeOrDamageEnvironment()
    {
        using GameWorld world = new();
        world.Vehicle.Sink();

        world.ExplodeVehicle();
        world.Vehicle.Explode();

        Assert.False(world.Vehicle.IsExploded);
        Assert.Empty(world.Vehicle.ExplosionPieces);
        Assert.Empty(world.Environment.BlastEffects);
        Assert.Empty(world.Environment.Craters);
        Assert.DoesNotContain(world.Environment.Scenery, item => item.IsDestroyed);
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
