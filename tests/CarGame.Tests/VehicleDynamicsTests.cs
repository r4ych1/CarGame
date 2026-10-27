using System.Numerics;
using Xunit;

namespace CarGame.Tests;

public sealed class VehicleDynamicsTests
{
    [Fact]
    public void FirstCarUsesEarlierQuickAutomaticUpshifts()
    {
        TransmissionSettings transmission = VehicleCatalog.FirstCar.Transmission;

        Assert.Equal(4_800.0f, transmission.UpshiftRevolutionsPerMinute);
        Assert.Equal(0.14f, transmission.ShiftDurationSeconds);
        Assert.Equal(5, transmission.ForwardRatios.Length);
    }

    private const float FixedTimeStep = 1.0f / 60.0f;
    private const float Tolerance = 0.02f;

    [Fact]
    public void StaticLoadsAreSymmetricAndMatchDistribution()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        FourWheels<float> loads = VehicleDynamics.CalculateNormalLoads(
            definition,
            VehicleDynamics.CreateInitialState(definition));
        float weight = definition.Chassis.MassKilograms * VehicleDynamics.GravityMetersPerSecondSquared;

        Assert.Equal(loads.FrontLeft, loads.FrontRight, Tolerance);
        Assert.Equal(loads.RearLeft, loads.RearRight, Tolerance);
        Assert.Equal(weight * 0.60f, loads.FrontLeft + loads.FrontRight, Tolerance);
        Assert.Equal(weight * 0.40f, loads.RearLeft + loads.RearRight, Tolerance);
        Assert.Equal(weight, Sum(loads), Tolerance);
    }

    [Fact]
    public void PitchTransfersLoadInExpectedDirection()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        VehicleSimulationState initial = VehicleDynamics.CreateInitialState(definition);
        FourWheels<float> staticLoads = VehicleDynamics.CalculateNormalLoads(definition, initial);
        FourWheels<float> accelerationLoads = VehicleDynamics.CalculateNormalLoads(
            definition,
            initial with { PitchRadians = -0.02f });
        FourWheels<float> brakingLoads = VehicleDynamics.CalculateNormalLoads(
            definition,
            initial with { PitchRadians = 0.02f });

        Assert.True(accelerationLoads.RearLeft + accelerationLoads.RearRight > staticLoads.RearLeft + staticLoads.RearRight);
        Assert.True(accelerationLoads.FrontLeft + accelerationLoads.FrontRight < staticLoads.FrontLeft + staticLoads.FrontRight);
        Assert.True(brakingLoads.FrontLeft + brakingLoads.FrontRight > staticLoads.FrontLeft + staticLoads.FrontRight);
    }

    [Fact]
    public void TireForceMomentsCreateAccelerationAndBrakingPitch()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        VehicleSimulationState accelerating = VehicleDynamics.CreateInitialState(definition);
        for (int index = 0; index < 60; index++)
        {
            accelerating = VehicleDynamics.Step(
                definition,
                accelerating,
                default,
                new VehicleInput(1.0f, 0.0f),
                FixedTimeStep).State;
        }

        const float roadSpeed = 10.0f;
        float wheelSpeed = roadSpeed / definition.Tires.WheelRadiusMeters;
        VehicleSimulationState braking = WithAllWheelSpeeds(
            VehicleDynamics.CreateInitialState(definition),
            wheelSpeed);
        ChassisSample moving = new(VehicleMath.Forward(0.0f) * roadSpeed, 0.0f, 0.0f);
        for (int index = 0; index < 60; index++)
        {
            braking = VehicleDynamics.Step(
                definition,
                braking,
                moving,
                new VehicleInput(-1.0f, 0.0f),
                FixedTimeStep).State;
        }

        Assert.True(accelerating.PitchRadians < 0.0f);
        Assert.True(braking.PitchRadians > 0.0f);
    }

    [Fact]
    public void RollLoadsOutsideWheelsAndNeverProducesNegativeLoad()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        VehicleSimulationState state = VehicleDynamics.CreateInitialState(definition) with { RollRadians = 0.12f };

        FourWheels<float> loads = VehicleDynamics.CalculateNormalLoads(definition, state);

        Assert.True(loads.FrontLeft > loads.FrontRight);
        Assert.True(loads.RearLeft > loads.RearRight);
        Assert.All(
            new[] { loads.FrontLeft, loads.FrontRight, loads.RearLeft, loads.RearRight },
            load => Assert.True(load >= 0.0f));
    }

    [Fact]
    public void SuspensionReturnsTowardEquilibriumAtFixedStep()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        VehicleSimulationState state = VehicleDynamics.CreateInitialState(definition) with { HeaveMeters = -0.05f };
        float initialDisplacement = MathF.Abs(state.HeaveMeters);

        for (int index = 0; index < 240; index++)
        {
            state = VehicleDynamics.Step(definition, state, default, default, FixedTimeStep).State;
            Assert.True(float.IsFinite(state.HeaveMeters));
        }

        Assert.True(MathF.Abs(state.HeaveMeters) < initialDisplacement * 0.1f);
    }

    [Fact]
    public void TireCurveIsZeroAtZeroSlipAndFallsAfterPeak()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        float load = definition.Chassis.MassKilograms * VehicleDynamics.GravityMetersPerSecondSquared * 0.25f;

        TireForce zero = Tire(definition, 0.0f, 0.0f, load);
        TireForce building = Tire(definition, 0.03f, 0.0f, load);
        TireForce nearPeak = Tire(definition, 0.10f, 0.0f, load);
        TireForce beyondPeak = Tire(definition, 3.0f, 0.0f, load);

        Assert.Equal(0.0f, zero.MagnitudeNewtons, Tolerance);
        Assert.True(building.LongitudinalNewtons > 0.0f);
        Assert.True(nearPeak.LongitudinalNewtons > building.LongitudinalNewtons);
        Assert.True(beyondPeak.LongitudinalNewtons < nearPeak.LongitudinalNewtons);
    }

    [Fact]
    public void TireLoadSensitivityReducesForcePerUnitLoad()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        float reference = definition.Chassis.MassKilograms * VehicleDynamics.GravityMetersPerSecondSquared * 0.25f;
        TireForce light = Tire(definition, 0.1f, 0.0f, reference * 0.5f);
        TireForce heavy = Tire(definition, 0.1f, 0.0f, reference * 1.5f);

        Assert.True(heavy.LongitudinalNewtons > light.LongitudinalNewtons);
        Assert.True(heavy.LongitudinalNewtons / (reference * 1.5f) < light.LongitudinalNewtons / (reference * 0.5f));
    }

    [Fact]
    public void CombinedSlipConsumesLateralCapacity()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        float load = definition.Chassis.MassKilograms * VehicleDynamics.GravityMetersPerSecondSquared * 0.25f;
        TireForce cornering = Tire(definition, 0.0f, 0.20f, load);
        TireForce combined = Tire(definition, 0.12f, 0.20f, load);

        Assert.True(MathF.Abs(combined.LateralNewtons) < MathF.Abs(cornering.LateralNewtons));
    }

    [Fact]
    public void DriveTorqueChangesWheelSpeedAndOpenDifferentialIsEqual()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        VehicleSimulationState initial = VehicleDynamics.CreateInitialState(definition);

        VehicleSimulationState driven = VehicleDynamics.Step(
            definition,
            initial,
            default,
            new VehicleInput(1.0f, 0.0f),
            FixedTimeStep).State;
        (float left, float right) = VehicleDynamics.OpenDifferentialTorque(800.0f);

        Assert.True(driven.Wheels.FrontLeft.AngularSpeedRadiansPerSecond > 0.0f);
        Assert.Equal(driven.Wheels.FrontLeft.AngularSpeedRadiansPerSecond, driven.Wheels.FrontRight.AngularSpeedRadiansPerSecond, Tolerance);
        Assert.Equal(left, right);
    }

    [Fact]
    public void ClosedThrottleAppliesEngineBrakingThroughDrivenWheels()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        const float roadSpeed = 5.0f;
        float rollingAngularSpeed = roadSpeed / definition.Tires.WheelRadiusMeters;
        VehicleSimulationState state = WithAllWheelSpeeds(
            VehicleDynamics.CreateInitialState(definition),
            rollingAngularSpeed);
        ChassisSample moving = new(VehicleMath.Forward(0.0f) * roadSpeed, 0.0f, 0.0f);

        DynamicsStepResult firstStep = VehicleDynamics.Step(
            definition,
            state,
            moving,
            default,
            FixedTimeStep);
        DynamicsStepResult secondStep = VehicleDynamics.Step(
            definition,
            firstStep.State,
            moving,
            default,
            FixedTimeStep);

        Assert.True(firstStep.State.Wheels.FrontLeft.AngularSpeedRadiansPerSecond < rollingAngularSpeed);
        Assert.True(secondStep.Diagnostics.Wheels.FrontLeft.LongitudinalForceNewtons < 0.0f);
        Assert.True(secondStep.Diagnostics.Wheels.FrontRight.LongitudinalForceNewtons < 0.0f);
        Assert.Equal(0.0f, secondStep.Diagnostics.Wheels.RearLeft.LongitudinalForceNewtons, Tolerance);
    }

    [Fact]
    public void AutomaticShiftUsesTorqueCutAndOneGearTransition()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        VehicleSimulationState state = VehicleDynamics.CreateInitialState(definition);
        float ratio = definition.Transmission.ForwardRatios[0] * definition.Transmission.FinalDriveRatio;
        float wheelSpeed = 6_000.0f * (2.0f * MathF.PI) / (60.0f * ratio);
        WheelSimulationState wheel = state.Wheels.FrontLeft with { AngularSpeedRadiansPerSecond = wheelSpeed };
        state = state with
        {
            Wheels = state.Wheels with { FrontLeft = wheel, FrontRight = wheel },
        };
        ChassisSample matchingSample = new(
            VehicleMath.Forward(0.0f) * wheelSpeed * definition.Tires.WheelRadiusMeters,
            0.0f,
            0.0f);

        state = VehicleDynamics.Step(definition, state, matchingSample, new VehicleInput(1.0f, 0.0f), FixedTimeStep).State;

        Assert.Equal(1, state.CurrentGear);
        Assert.Equal(2, state.PendingGear);
        Assert.True(state.ShiftTimeRemainingSeconds > 0.0f);
        for (int index = 0; index < 20; index++)
        {
            state = VehicleDynamics.Step(definition, state, matchingSample, default, FixedTimeStep).State;
        }

        Assert.Equal(2, state.CurrentGear);
        Assert.InRange(state.EngineRevolutionsPerMinute, definition.Powertrain.IdleRevolutionsPerMinute, definition.Powertrain.RedlineRevolutionsPerMinute);
    }

    [Fact]
    public void AutomaticShiftThresholdsProvideHysteresisAndDownshift()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        VehicleSimulationState state = VehicleDynamics.CreateInitialState(definition) with
        {
            CurrentGear = 3,
            PendingGear = 3,
        };
        float ratio = definition.Transmission.ForwardRatios[2] * definition.Transmission.FinalDriveRatio;
        float midWheelSpeed = 3_000.0f * (2.0f * MathF.PI) / (60.0f * ratio);
        state = WithAllWheelSpeeds(state, midWheelSpeed);
        ChassisSample matching = new(
            VehicleMath.Forward(0.0f) * midWheelSpeed * definition.Tires.WheelRadiusMeters,
            0.0f,
            0.0f);

        state = VehicleDynamics.Step(definition, state, matching, default, FixedTimeStep).State;
        Assert.Equal(0.0f, state.ShiftTimeRemainingSeconds);

        float lowWheelSpeed = 1_500.0f * (2.0f * MathF.PI) / (60.0f * ratio);
        state = WithAllWheelSpeeds(state, lowWheelSpeed);
        matching = matching with
        {
            LinearVelocityMetersPerSecond = VehicleMath.Forward(0.0f) * lowWheelSpeed * definition.Tires.WheelRadiusMeters,
        };
        state = VehicleDynamics.Step(definition, state, matching, default, FixedTimeStep).State;

        Assert.Equal(3, state.CurrentGear);
        Assert.Equal(2, state.PendingGear);
        Assert.True(state.ShiftTimeRemainingSeconds > 0.0f);
    }

    [Fact]
    public void ReverseEngagesOnlyNearRest()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        VehicleSimulationState initial = VehicleDynamics.CreateInitialState(definition);
        ChassisSample moving = new(VehicleMath.Forward(0.0f) * 5.0f, 0.0f, 0.0f);

        VehicleSimulationState braking = VehicleDynamics.Step(
            definition,
            initial,
            moving,
            new VehicleInput(-1.0f, 0.0f),
            FixedTimeStep).State;
        VehicleSimulationState reversing = VehicleDynamics.Step(
            definition,
            initial,
            default,
            new VehicleInput(-1.0f, 0.0f),
            FixedTimeStep).State;

        Assert.Equal(1, braking.CurrentGear);
        Assert.Equal(-1, reversing.CurrentGear);
    }

    [Fact]
    public void AbsReleasesAndReappliesIndividualWheelPressure()
    {
        BrakeSettings brakes = VehicleCatalog.FirstCar.Brakes;

        VehicleDynamics.AbsResult releasing = VehicleDynamics.UpdateAbs(
            brakes,
            1.0f,
            -0.30f,
            10.0f,
            true,
            FixedTimeStep);
        VehicleDynamics.AbsResult reapplying = VehicleDynamics.UpdateAbs(
            brakes,
            releasing.Modulation,
            -0.02f,
            10.0f,
            true,
            FixedTimeStep);

        Assert.True(releasing.Intervening);
        Assert.True(releasing.Modulation < 1.0f);
        Assert.True(reapplying.Modulation > releasing.Modulation);
    }

    [Fact]
    public void AbsIntervenesOnOnlyTheOverSlippingWheel()
    {
        VehicleDefinition definition = VehicleCatalog.FirstCar;
        const float speed = 12.0f;
        float rollingSpeed = speed / definition.Tires.WheelRadiusMeters;
        VehicleSimulationState state = WithAllWheelSpeeds(
            VehicleDynamics.CreateInitialState(definition),
            rollingSpeed);
        state = state with
        {
            Wheels = state.Wheels with
            {
                FrontLeft = state.Wheels.FrontLeft with { AngularSpeedRadiansPerSecond = 0.0f },
            },
        };
        ChassisSample sample = new(VehicleMath.Forward(0.0f) * speed, 0.0f, 0.0f);

        VehicleDiagnostics diagnostics = VehicleDynamics.Step(
            definition,
            state,
            sample,
            new VehicleInput(-1.0f, 0.0f),
            FixedTimeStep).Diagnostics;

        Assert.True(diagnostics.Wheels.FrontLeft.AbsIntervening);
        Assert.False(diagnostics.Wheels.FrontRight.AbsIntervening);
    }

    [Fact]
    public void InvalidNestedDefinitionIsRejected()
    {
        VehicleDefinition source = VehicleCatalog.FirstCar;
        VehicleDefinition invalid = source with
        {
            Chassis = source.Chassis with { WheelbaseMeters = 0.0f },
        };

        Assert.Throws<ArgumentOutOfRangeException>(invalid.Validate);
    }

    private static TireForce Tire(
        VehicleDefinition definition,
        float slipRatio,
        float slipAngle,
        float load)
    {
        float reference = definition.Chassis.MassKilograms * VehicleDynamics.GravityMetersPerSecondSquared * 0.25f;
        return VehicleDynamics.CalculateCombinedTireForce(
            definition.Tires,
            slipRatio,
            slipAngle,
            load,
            reference,
            10.0f);
    }

    private static float Sum(FourWheels<float> values) =>
        values.FrontLeft + values.FrontRight + values.RearLeft + values.RearRight;

    private static VehicleSimulationState WithAllWheelSpeeds(VehicleSimulationState state, float angularSpeed)
    {
        WheelSimulationState wheel = state.Wheels.FrontLeft with { AngularSpeedRadiansPerSecond = angularSpeed };
        return state with { Wheels = new FourWheels<WheelSimulationState>(wheel, wheel, wheel, wheel) };
    }
}
