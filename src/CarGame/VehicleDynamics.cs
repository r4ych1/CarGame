using System.Numerics;

namespace CarGame;

internal enum WheelCorner
{
    FrontLeft,
    FrontRight,
    RearLeft,
    RearRight,
}

internal enum ShiftState
{
    Engaged,
    TorqueCut,
}

internal readonly record struct FourWheels<T>(T FrontLeft, T FrontRight, T RearLeft, T RearRight)
{
    public T this[WheelCorner corner] => corner switch
    {
        WheelCorner.FrontLeft => FrontLeft,
        WheelCorner.FrontRight => FrontRight,
        WheelCorner.RearLeft => RearLeft,
        WheelCorner.RearRight => RearRight,
        _ => throw new ArgumentOutOfRangeException(nameof(corner)),
    };

    public FourWheels<T> With(WheelCorner corner, T value) => corner switch
    {
        WheelCorner.FrontLeft => this with { FrontLeft = value },
        WheelCorner.FrontRight => this with { FrontRight = value },
        WheelCorner.RearLeft => this with { RearLeft = value },
        WheelCorner.RearRight => this with { RearRight = value },
        _ => throw new ArgumentOutOfRangeException(nameof(corner)),
    };
}

internal readonly record struct WheelSimulationState(
    float AngularSpeedRadiansPerSecond,
    float RotationRadians,
    float SuspensionCompressionMeters,
    float SuspensionVelocityMetersPerSecond,
    float AbsModulation);

internal readonly record struct VehicleSimulationState(
    FourWheels<WheelSimulationState> Wheels,
    float HeaveMeters,
    float HeaveVelocityMetersPerSecond,
    float RollRadians,
    float RollVelocityRadiansPerSecond,
    float PitchRadians,
    float PitchVelocityRadiansPerSecond,
    int CurrentGear,
    int PendingGear,
    float ShiftTimeRemainingSeconds,
    float EngineRevolutionsPerMinute);

internal readonly record struct ChassisSample(
    Vector2 LinearVelocityMetersPerSecond,
    float AngularVelocityRadiansPerSecond,
    float RotationRadians);

internal readonly record struct WheelForce(
    WheelCorner Corner,
    Vector2 LocalPointMeters,
    Vector2 WorldForceNewtons);

internal readonly record struct TireForce(float LongitudinalNewtons, float LateralNewtons)
{
    public float MagnitudeNewtons => MathF.Sqrt(
        (LongitudinalNewtons * LongitudinalNewtons) +
        (LateralNewtons * LateralNewtons));
}

internal readonly record struct WheelDiagnostics(
    float NormalLoadNewtons,
    float SlipAngleRadians,
    float SlipRatio,
    float LongitudinalForceNewtons,
    float LateralForceNewtons,
    float GripUsage,
    float SteeringAngleRadians,
    bool AbsIntervening);

internal readonly record struct VehicleDiagnostics(
    int CurrentGear,
    float EngineRevolutionsPerMinute,
    ShiftState ShiftState,
    float ChassisHeaveMeters,
    float ChassisRollRadians,
    float ChassisPitchRadians,
    FourWheels<WheelDiagnostics> Wheels)
{
    public bool AnyAbsIntervention =>
        Wheels.FrontLeft.AbsIntervening ||
        Wheels.FrontRight.AbsIntervening ||
        Wheels.RearLeft.AbsIntervening ||
        Wheels.RearRight.AbsIntervening;
}

internal readonly record struct DynamicsStepResult(
    VehicleSimulationState State,
    FourWheels<WheelForce> WheelForces,
    VehicleDiagnostics Diagnostics);

internal static class VehicleDynamics
{
    public const float GravityMetersPerSecondSquared = 9.81f;
    private const float TwoPi = 2.0f * MathF.PI;

    private static readonly WheelCorner[] Corners =
    [
        WheelCorner.FrontLeft,
        WheelCorner.FrontRight,
        WheelCorner.RearLeft,
        WheelCorner.RearRight,
    ];

    public static VehicleSimulationState CreateInitialState(VehicleDefinition definition)
    {
        WheelSimulationState wheel = new(0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
        return new VehicleSimulationState(
            new FourWheels<WheelSimulationState>(wheel, wheel, wheel, wheel),
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            1,
            1,
            0.0f,
            definition.Powertrain.IdleRevolutionsPerMinute);
    }

    public static VehicleDiagnostics CreateInitialDiagnostics(VehicleDefinition definition)
    {
        FourWheels<float> loads = CalculateNormalLoads(definition, CreateInitialState(definition));
        WheelDiagnostics frontLeft = new(loads.FrontLeft, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, false);
        WheelDiagnostics frontRight = frontLeft with { NormalLoadNewtons = loads.FrontRight };
        WheelDiagnostics rearLeft = frontLeft with { NormalLoadNewtons = loads.RearLeft };
        WheelDiagnostics rearRight = frontLeft with { NormalLoadNewtons = loads.RearRight };
        return new VehicleDiagnostics(
            1,
            definition.Powertrain.IdleRevolutionsPerMinute,
            ShiftState.Engaged,
            0.0f,
            0.0f,
            0.0f,
            new FourWheels<WheelDiagnostics>(frontLeft, frontRight, rearLeft, rearRight));
    }

    public static DynamicsStepResult Step(
        VehicleDefinition definition,
        VehicleSimulationState state,
        ChassisSample chassis,
        VehicleInput input,
        float fixedTimeStep)
    {
        if (!float.IsFinite(fixedTimeStep) || fixedTimeStep <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedTimeStep));
        }

        ChassisSettings chassisSettings = definition.Chassis;
        Vector2 bodyForward = VehicleMath.Forward(chassis.RotationRadians);
        float bodyLongitudinalSpeed = Vector2.Dot(chassis.LinearVelocityMetersPerSecond, bodyForward);
        float centerSteering = VehicleMath.SteeringAngle(input.Steering, bodyLongitudinalSpeed, definition);
        SteeringAngles steering = VehicleMath.AckermannSteering(centerSteering, chassisSettings);
        FourWheels<float> steeringAngles = new(steering.FrontLeftRadians, steering.FrontRightRadians, 0.0f, 0.0f);
        FourWheels<float> loads = CalculateNormalLoads(definition, state);

        VehicleSimulationState transmissionState = UpdateTransmission(
            definition,
            state,
            input,
            bodyLongitudinalSpeed,
            fixedTimeStep);
        DriveAndBrakeCommand command = CalculateDriveAndBrakeCommand(
            definition,
            transmissionState,
            input,
            bodyLongitudinalSpeed,
            loads);

        FourWheels<WheelSimulationState> nextWheels = transmissionState.Wheels;
        FourWheels<WheelForce> forces = default;
        FourWheels<WheelDiagnostics> diagnostics = default;
        float totalBodyLongitudinalForce = 0.0f;
        float totalBodyLateralForce = 0.0f;

        foreach (WheelCorner corner in Corners)
        {
            WheelSimulationState wheelState = transmissionState.Wheels[corner];
            float steeringAngle = steeringAngles[corner];
            WheelKinematics kinematics = CalculateWheelKinematics(definition, chassis, corner, steeringAngle);
            float slipRatio = CalculateSlipRatio(
                wheelState.AngularSpeedRadiansPerSecond,
                definition.Tires.WheelRadiusMeters,
                kinematics.LongitudinalSpeedMetersPerSecond,
                definition.Tires.MinimumSlipSpeedMetersPerSecond);
            float slipAngle = CalculateSlipAngle(
                kinematics.LongitudinalSpeedMetersPerSecond,
                kinematics.LateralSpeedMetersPerSecond,
                definition.Tires.MinimumSlipSpeedMetersPerSecond);

            bool braking = command.BrakeFraction > 0.0f;
            AbsResult abs = UpdateAbs(
                definition.Brakes,
                wheelState.AbsModulation,
                slipRatio,
                MathF.Abs(kinematics.LongitudinalSpeedMetersPerSecond),
                braking,
                fixedTimeStep);
            float brakeTorque = BrakeTorqueForCorner(definition, corner) * command.BrakeFraction * abs.Modulation;
            float driveTorque = IsFront(corner) ? command.EqualFrontShaftTorqueNewtonMeters : 0.0f;
            TireForce tireForce = CalculateCombinedTireForce(
                definition.Tires,
                slipRatio,
                slipAngle,
                loads[corner],
                StaticReferenceWheelLoad(definition),
                kinematics.SpeedMetersPerSecond);

            float brakeDirection = MathF.Sign(wheelState.AngularSpeedRadiansPerSecond);
            if (brakeDirection == 0.0f)
            {
                brakeDirection = MathF.Sign(kinematics.LongitudinalSpeedMetersPerSecond);
            }

            float appliedTorque = driveTorque - (brakeTorque * brakeDirection);
            float angularAcceleration =
                (appliedTorque - (tireForce.LongitudinalNewtons * definition.Tires.WheelRadiusMeters)) /
                definition.Tires.WheelInertiaKilogramMetersSquared;
            float nextAngularSpeed = wheelState.AngularSpeedRadiansPerSecond + (angularAcceleration * fixedTimeStep);
            float rollingAngularSpeed = kinematics.LongitudinalSpeedMetersPerSecond / definition.Tires.WheelRadiusMeters;

            // The contact impulse may bring a wheel to road speed during this step,
            // but must not numerically throw it through road speed and reverse slip.
            if (CrossedTarget(wheelState.AngularSpeedRadiansPerSecond, nextAngularSpeed, rollingAngularSpeed))
            {
                nextAngularSpeed = rollingAngularSpeed;
                float stableLongitudinalForce =
                    (appliedTorque -
                     (definition.Tires.WheelInertiaKilogramMetersSquared *
                      (rollingAngularSpeed - wheelState.AngularSpeedRadiansPerSecond) /
                      fixedTimeStep)) /
                    definition.Tires.WheelRadiusMeters;
                tireForce = tireForce with
                {
                    LongitudinalNewtons = Math.Clamp(
                        stableLongitudinalForce,
                        -MathF.Abs(tireForce.LongitudinalNewtons),
                        MathF.Abs(tireForce.LongitudinalNewtons)),
                };
            }

            nextAngularSpeed = FiniteOrZero(Math.Clamp(nextAngularSpeed, -400.0f, 400.0f));
            float nextRotation = WrapAngle(wheelState.RotationRadians + (nextAngularSpeed * fixedTimeStep));
            WheelSimulationState nextWheel = wheelState with
            {
                AngularSpeedRadiansPerSecond = nextAngularSpeed,
                RotationRadians = nextRotation,
                AbsModulation = abs.Modulation,
            };
            nextWheels = nextWheels.With(corner, nextWheel);

            Vector2 worldForce =
                (kinematics.WheelForward * tireForce.LongitudinalNewtons) +
                (kinematics.WheelRight * tireForce.LateralNewtons);
            forces = forces.With(corner, new WheelForce(corner, kinematics.LocalPointMeters, worldForce));
            float capacity = PeakForce(definition.Tires, loads[corner], StaticReferenceWheelLoad(definition));
            diagnostics = diagnostics.With(corner, new WheelDiagnostics(
                loads[corner],
                slipAngle,
                slipRatio,
                tireForce.LongitudinalNewtons,
                tireForce.LateralNewtons,
                capacity > 0.0f ? Math.Clamp(tireForce.MagnitudeNewtons / capacity, 0.0f, 1.0f) : 0.0f,
                steeringAngle,
                abs.Intervening));

            totalBodyLongitudinalForce += Vector2.Dot(worldForce, bodyForward);
            totalBodyLateralForce += Vector2.Dot(worldForce, VehicleMath.Right(chassis.RotationRadians));
        }

        VehicleSimulationState nextState = IntegrateSuspension(
            definition,
            transmissionState with { Wheels = nextWheels },
            loads,
            totalBodyLongitudinalForce,
            totalBodyLateralForce,
            fixedTimeStep);
        VehicleDiagnostics vehicleDiagnostics = new(
            nextState.CurrentGear,
            nextState.EngineRevolutionsPerMinute,
            nextState.ShiftTimeRemainingSeconds > 0.0f ? ShiftState.TorqueCut : ShiftState.Engaged,
            nextState.HeaveMeters,
            nextState.RollRadians,
            nextState.PitchRadians,
            diagnostics);
        return new DynamicsStepResult(nextState, forces, vehicleDiagnostics);
    }

    public static FourWheels<float> CalculateNormalLoads(
        VehicleDefinition definition,
        VehicleSimulationState state)
    {
        float frontStatic =
            definition.Chassis.MassKilograms * GravityMetersPerSecondSquared *
            definition.Chassis.FrontStaticWeightFraction * 0.5f;
        float rearStatic =
            definition.Chassis.MassKilograms * GravityMetersPerSecondSquared *
            (1.0f - definition.Chassis.FrontStaticWeightFraction) * 0.5f;
        FourWheels<float> result = default;

        foreach (WheelCorner corner in Corners)
        {
            (float compression, float velocity) = SuspensionDisplacement(definition, state, corner);
            float springRate = IsFront(corner)
                ? definition.Suspension.FrontSpringRateNewtonsPerMeter
                : definition.Suspension.RearSpringRateNewtonsPerMeter;
            float cornerMass = (IsFront(corner) ? frontStatic : rearStatic) / GravityMetersPerSecondSquared;
            float damping = 2.0f * definition.Suspension.DampingRatio * MathF.Sqrt(springRate * cornerMass);
            float staticLoad = IsFront(corner) ? frontStatic : rearStatic;
            float load = MathF.Max(0.0f, staticLoad + (springRate * compression) + (damping * velocity));
            result = result.With(corner, FiniteOrZero(load));
        }

        return result;
    }

    public static float CalculateSlipRatio(
        float angularSpeedRadiansPerSecond,
        float radiusMeters,
        float longitudinalSpeedMetersPerSecond,
        float minimumReferenceSpeedMetersPerSecond)
    {
        float surfaceSpeed = angularSpeedRadiansPerSecond * radiusMeters;
        float reference = MathF.Max(
            minimumReferenceSpeedMetersPerSecond,
            MathF.Max(MathF.Abs(surfaceSpeed), MathF.Abs(longitudinalSpeedMetersPerSecond)));
        return FiniteOrZero((surfaceSpeed - longitudinalSpeedMetersPerSecond) / reference);
    }

    public static float CalculateSlipAngle(
        float longitudinalSpeedMetersPerSecond,
        float lateralSpeedMetersPerSecond,
        float minimumReferenceSpeedMetersPerSecond)
    {
        float reference = MathF.Max(MathF.Abs(longitudinalSpeedMetersPerSecond), minimumReferenceSpeedMetersPerSecond);
        return FiniteOrZero(MathF.Atan2(lateralSpeedMetersPerSecond, reference));
    }

    public static TireForce CalculateCombinedTireForce(
        TireSettings settings,
        float slipRatio,
        float slipAngleRadians,
        float normalLoadNewtons,
        float referenceLoadNewtons,
        float contactSpeedMetersPerSecond)
    {
        float peak = PeakForce(settings, normalLoadNewtons, referenceLoadNewtons);
        if (peak <= 0.0f)
        {
            return default;
        }

        float longitudinalShape = MagicFormula(
            slipRatio,
            settings.LongitudinalShapeB,
            settings.LongitudinalShapeC,
            settings.LongitudinalCurvatureE);
        float lateralShape = MagicFormula(
            slipAngleRadians,
            settings.LateralShapeB,
            settings.LateralShapeC,
            0.0f);
        float lowSpeedBlend = Math.Clamp(contactSpeedMetersPerSecond / settings.LowSpeedForceBlendMetersPerSecond, 0.0f, 1.0f);
        float longitudinal = peak * longitudinalShape;
        float lateral = -peak * lateralShape * lowSpeedBlend;
        float usage = MathF.Sqrt(
            (longitudinal * longitudinal) / (peak * peak) +
            (lateral * lateral) / (peak * peak));
        if (usage > 1.0f)
        {
            longitudinal /= usage;
            lateral /= usage;
        }

        return new TireForce(FiniteOrZero(longitudinal), FiniteOrZero(lateral));
    }

    public static float EngineTorque(PowertrainSettings settings, float revolutionsPerMinute)
    {
        float speed = Math.Clamp(revolutionsPerMinute, 0.0f, settings.RedlineRevolutionsPerMinute);
        for (int index = 1; index < settings.TorqueCurve.Length; index++)
        {
            EngineTorquePoint upper = settings.TorqueCurve[index];
            if (speed <= upper.RevolutionsPerMinute)
            {
                EngineTorquePoint lower = settings.TorqueCurve[index - 1];
                float amount = Math.Clamp(
                    (speed - lower.RevolutionsPerMinute) /
                    (upper.RevolutionsPerMinute - lower.RevolutionsPerMinute),
                    0.0f,
                    1.0f);
                return lower.TorqueNewtonMeters + ((upper.TorqueNewtonMeters - lower.TorqueNewtonMeters) * amount);
            }
        }

        return settings.TorqueCurve[^1].TorqueNewtonMeters;
    }

    public static AbsResult UpdateAbs(
        BrakeSettings settings,
        float currentModulation,
        float slipRatio,
        float wheelLongitudinalSpeedMetersPerSecond,
        bool braking,
        float fixedTimeStep)
    {
        if (!braking || wheelLongitudinalSpeedMetersPerSecond < settings.AbsMinimumSpeedMetersPerSecond)
        {
            return new AbsResult(1.0f, false);
        }

        float modulation = currentModulation;
        bool intervening = slipRatio < -settings.AbsSlipThreshold;
        if (intervening)
        {
            modulation -= settings.AbsReleaseRatePerSecond * fixedTimeStep;
        }
        else if (slipRatio > -settings.AbsReleaseThreshold)
        {
            modulation += settings.AbsReapplyRatePerSecond * fixedTimeStep;
        }

        modulation = Math.Clamp(modulation, 0.0f, 1.0f);
        return new AbsResult(modulation, intervening || modulation < 0.999f);
    }

    public static (float LeftTorque, float RightTorque) OpenDifferentialTorque(float totalAxleTorque)
    {
        float shaftTorque = totalAxleTorque * 0.5f;
        return (shaftTorque, shaftTorque);
    }

    private static VehicleSimulationState UpdateTransmission(
        VehicleDefinition definition,
        VehicleSimulationState state,
        VehicleInput input,
        float longitudinalSpeed,
        float fixedTimeStep)
    {
        TransmissionSettings transmission = definition.Transmission;
        int gear = state.CurrentGear;
        int pendingGear = state.PendingGear;
        float shiftTime = state.ShiftTimeRemainingSeconds;

        if (shiftTime > 0.0f)
        {
            shiftTime = MathF.Max(0.0f, shiftTime - fixedTimeStep);
            if (shiftTime <= 0.0f)
            {
                gear = pendingGear;
            }
        }
        else if (MathF.Abs(longitudinalSpeed) <= transmission.DirectionSelectionSpeedMetersPerSecond)
        {
            if (input.Throttle < 0.0f)
            {
                gear = -1;
                pendingGear = -1;
            }
            else if (input.Throttle > 0.0f && gear < 0)
            {
                gear = 1;
                pendingGear = 1;
            }
        }

        float engineSpeed = CalculateEngineSpeed(definition, state.Wheels, gear);
        if (shiftTime <= 0.0f && gear > 0)
        {
            if (engineSpeed >= transmission.UpshiftRevolutionsPerMinute && gear < transmission.ForwardRatios.Length)
            {
                pendingGear = gear + 1;
                shiftTime = transmission.ShiftDurationSeconds;
            }
            else if (engineSpeed <= transmission.DownshiftRevolutionsPerMinute && gear > 1)
            {
                pendingGear = gear - 1;
                shiftTime = transmission.ShiftDurationSeconds;
            }
        }

        return state with
        {
            CurrentGear = gear,
            PendingGear = pendingGear,
            ShiftTimeRemainingSeconds = shiftTime,
            EngineRevolutionsPerMinute = engineSpeed,
        };
    }

    private static DriveAndBrakeCommand CalculateDriveAndBrakeCommand(
        VehicleDefinition definition,
        VehicleSimulationState state,
        VehicleInput input,
        float longitudinalSpeed,
        FourWheels<float> loads)
    {
        float inputDirection = MathF.Sign(input.Throttle);
        float pedal = Math.Clamp(MathF.Abs(input.Throttle), 0.0f, 1.0f);
        float brakePedal = Math.Clamp(input.Brake, 0.0f, 1.0f);
        float selectionSpeed = definition.Transmission.DirectionSelectionSpeedMetersPerSecond;
        bool directionChangeBraking =
            (inputDirection > 0.0f && longitudinalSpeed < -selectionSpeed) ||
            (inputDirection < 0.0f && longitudinalSpeed > selectionSpeed);
        if (brakePedal > 0.0f || directionChangeBraking)
        {
            float brakeFraction = MathF.Max(brakePedal, directionChangeBraking ? pedal : 0.0f);
            return new DriveAndBrakeCommand(0.0f, brakeFraction);
        }

        if (pedal <= 0.0f)
        {
            if (state.ShiftTimeRemainingSeconds > 0.0f || MathF.Abs(longitudinalSpeed) <= selectionSpeed)
            {
                return default;
            }

            float engineBrakingAxleTorque =
                definition.Powertrain.EngineBrakingTorqueNewtonMeters *
                GearRatio(definition.Transmission, state.CurrentGear) *
                definition.Transmission.FinalDriveRatio *
                definition.Powertrain.DrivelineEfficiency;
            float opposingTorque = -MathF.CopySign(engineBrakingAxleTorque, longitudinalSpeed);
            (float engineBrakingLeftTorque, _) = OpenDifferentialTorque(opposingTorque);
            return new DriveAndBrakeCommand(engineBrakingLeftTorque, 0.0f);
        }

        float maximumSpeed = state.CurrentGear < 0
            ? definition.Chassis.MaximumReverseSpeedMetersPerSecond
            : definition.Chassis.MaximumForwardSpeedMetersPerSecond;
        if (MathF.Abs(longitudinalSpeed) >= maximumSpeed || state.ShiftTimeRemainingSeconds > 0.0f)
        {
            return default;
        }

        float ratio = GearRatio(definition.Transmission, state.CurrentGear);
        float totalAxleTorque =
            EngineTorque(definition.Powertrain, state.EngineRevolutionsPerMinute) *
            ratio * definition.Transmission.FinalDriveRatio *
            definition.Powertrain.DrivelineEfficiency * pedal *
            MathF.Sign(state.CurrentGear);
        (float leftTorque, _) = OpenDifferentialTorque(totalAxleTorque);
        float leftLimit = PeakForce(definition.Tires, loads.FrontLeft, StaticReferenceWheelLoad(definition)) * definition.Tires.WheelRadiusMeters;
        float rightLimit = PeakForce(definition.Tires, loads.FrontRight, StaticReferenceWheelLoad(definition)) * definition.Tires.WheelRadiusMeters;
        float openDifferentialLimit = MathF.Min(leftLimit, rightLimit);
        float equalShaftTorque = Math.Clamp(leftTorque, -openDifferentialLimit, openDifferentialLimit);
        return new DriveAndBrakeCommand(equalShaftTorque, 0.0f);
    }

    private static VehicleSimulationState IntegrateSuspension(
        VehicleDefinition definition,
        VehicleSimulationState state,
        FourWheels<float> loads,
        float longitudinalForce,
        float lateralForce,
        float fixedTimeStep)
    {
        ChassisSettings chassis = definition.Chassis;
        SuspensionSettings suspension = definition.Suspension;
        float totalLoad = loads.FrontLeft + loads.FrontRight + loads.RearLeft + loads.RearRight;
        float heaveAcceleration =
            (totalLoad - (chassis.MassKilograms * GravityMetersPerSecondSquared)) /
            chassis.MassKilograms;
        float leftLoad = loads.FrontLeft + loads.RearLeft;
        float rightLoad = loads.FrontRight + loads.RearRight;
        float frontLoad = loads.FrontLeft + loads.FrontRight;
        float rearLoad = loads.RearLeft + loads.RearRight;
        float rollTorque =
            (lateralForce * chassis.CenterOfGravityHeightMeters) +
            ((rightLoad - leftLoad) * chassis.AxleTrackMeters * 0.5f);
        float pitchTorque =
            (-longitudinalForce * chassis.CenterOfGravityHeightMeters) +
            ((rearLoad - frontLoad - StaticAxleLoadDifference(definition)) * chassis.WheelbaseMeters * 0.5f);

        float heaveVelocity = state.HeaveVelocityMetersPerSecond + (heaveAcceleration * fixedTimeStep);
        float rollVelocity = state.RollVelocityRadiansPerSecond +
            ((rollTorque / suspension.RollInertiaKilogramMetersSquared) * fixedTimeStep);
        float pitchVelocity = state.PitchVelocityRadiansPerSecond +
            ((pitchTorque / suspension.PitchInertiaKilogramMetersSquared) * fixedTimeStep);
        float heave = Math.Clamp(
            state.HeaveMeters + (heaveVelocity * fixedTimeStep),
            -suspension.BumpTravelMeters,
            suspension.DroopTravelMeters);
        float maximumRoll = (suspension.BumpTravelMeters + suspension.DroopTravelMeters) / chassis.AxleTrackMeters;
        float maximumPitch = (suspension.BumpTravelMeters + suspension.DroopTravelMeters) / chassis.WheelbaseMeters;
        float roll = Math.Clamp(state.RollRadians + (rollVelocity * fixedTimeStep), -maximumRoll, maximumRoll);
        float pitch = Math.Clamp(state.PitchRadians + (pitchVelocity * fixedTimeStep), -maximumPitch, maximumPitch);
        VehicleSimulationState integrated = state with
        {
            HeaveMeters = FiniteOrZero(heave),
            HeaveVelocityMetersPerSecond = FiniteOrZero(heaveVelocity),
            RollRadians = FiniteOrZero(roll),
            RollVelocityRadiansPerSecond = FiniteOrZero(rollVelocity),
            PitchRadians = FiniteOrZero(pitch),
            PitchVelocityRadiansPerSecond = FiniteOrZero(pitchVelocity),
        };

        FourWheels<WheelSimulationState> wheels = integrated.Wheels;
        foreach (WheelCorner corner in Corners)
        {
            (float compression, float velocity) = SuspensionDisplacement(definition, integrated, corner);
            wheels = wheels.With(corner, wheels[corner] with
            {
                SuspensionCompressionMeters = compression,
                SuspensionVelocityMetersPerSecond = velocity,
            });
        }

        return integrated with { Wheels = wheels };
    }

    private static (float Compression, float Velocity) SuspensionDisplacement(
        VehicleDefinition definition,
        VehicleSimulationState state,
        WheelCorner corner)
    {
        float side = IsLeft(corner) ? 1.0f : -1.0f;
        float axle = IsFront(corner) ? 1.0f : -1.0f;
        float compression =
            -state.HeaveMeters +
            (side * state.RollRadians * definition.Chassis.AxleTrackMeters * 0.5f) +
            (axle * state.PitchRadians * definition.Chassis.WheelbaseMeters * 0.5f);
        float velocity =
            -state.HeaveVelocityMetersPerSecond +
            (side * state.RollVelocityRadiansPerSecond * definition.Chassis.AxleTrackMeters * 0.5f) +
            (axle * state.PitchVelocityRadiansPerSecond * definition.Chassis.WheelbaseMeters * 0.5f);
        compression = Math.Clamp(
            compression,
            -definition.Suspension.DroopTravelMeters,
            definition.Suspension.BumpTravelMeters);
        return (FiniteOrZero(compression), FiniteOrZero(velocity));
    }

    private static WheelKinematics CalculateWheelKinematics(
        VehicleDefinition definition,
        ChassisSample chassis,
        WheelCorner corner,
        float steeringAngle)
    {
        float x = IsLeft(corner)
            ? -definition.Chassis.AxleTrackMeters * 0.5f
            : definition.Chassis.AxleTrackMeters * 0.5f;
        float forwardOffset = IsFront(corner)
            ? definition.Chassis.WheelbaseMeters * 0.5f
            : -definition.Chassis.WheelbaseMeters * 0.5f;
        float cgForwardOffset = -definition.CenterOfGravityOffsetFromGeometricCenterMeters;
        Vector2 bodyRight = VehicleMath.Right(chassis.RotationRadians);
        Vector2 bodyForward = VehicleMath.Forward(chassis.RotationRadians);
        Vector2 relativeFromCenterOfMass =
            (bodyRight * x) + (bodyForward * (forwardOffset - cgForwardOffset));
        Vector2 rotationalVelocity = new(
            -chassis.AngularVelocityRadiansPerSecond * relativeFromCenterOfMass.Y,
            chassis.AngularVelocityRadiansPerSecond * relativeFromCenterOfMass.X);
        Vector2 pointVelocity = chassis.LinearVelocityMetersPerSecond + rotationalVelocity;
        Vector2 wheelForward = VehicleMath.Forward(chassis.RotationRadians + steeringAngle);
        Vector2 wheelRight = VehicleMath.Right(chassis.RotationRadians + steeringAngle);
        return new WheelKinematics(
            new Vector2(x, -forwardOffset),
            wheelForward,
            wheelRight,
            Vector2.Dot(pointVelocity, wheelForward),
            Vector2.Dot(pointVelocity, wheelRight),
            pointVelocity.Length());
    }

    private static float CalculateEngineSpeed(
        VehicleDefinition definition,
        FourWheels<WheelSimulationState> wheels,
        int gear)
    {
        float averageDrivenSpeed =
            (MathF.Abs(wheels.FrontLeft.AngularSpeedRadiansPerSecond) +
             MathF.Abs(wheels.FrontRight.AngularSpeedRadiansPerSecond)) * 0.5f;
        float revolutionsPerMinute =
            averageDrivenSpeed * GearRatio(definition.Transmission, gear) *
            definition.Transmission.FinalDriveRatio * 60.0f / TwoPi;
        return Math.Clamp(
            FiniteOrZero(revolutionsPerMinute),
            definition.Powertrain.IdleRevolutionsPerMinute,
            definition.Powertrain.RedlineRevolutionsPerMinute);
    }

    private static float GearRatio(TransmissionSettings settings, int gear) =>
        gear < 0 ? settings.ReverseRatio : settings.ForwardRatios[Math.Clamp(gear - 1, 0, settings.ForwardRatios.Length - 1)];

    private static float BrakeTorqueForCorner(VehicleDefinition definition, WheelCorner corner)
    {
        float axleFraction = IsFront(corner) ? definition.Brakes.FrontBias : 1.0f - definition.Brakes.FrontBias;
        return definition.Brakes.MaximumBrakingForceNewtons * definition.Tires.WheelRadiusMeters * axleFraction * 0.5f;
    }

    private static float StaticReferenceWheelLoad(VehicleDefinition definition) =>
        definition.Chassis.MassKilograms * GravityMetersPerSecondSquared * 0.25f;

    private static float StaticAxleLoadDifference(VehicleDefinition definition) =>
        definition.Chassis.MassKilograms * GravityMetersPerSecondSquared *
        ((1.0f - definition.Chassis.FrontStaticWeightFraction) - definition.Chassis.FrontStaticWeightFraction);

    private static float PeakForce(TireSettings settings, float normalLoad, float referenceLoad)
    {
        if (normalLoad <= 0.0f || referenceLoad <= 0.0f)
        {
            return 0.0f;
        }

        return settings.PeakFrictionCoefficient * referenceLoad *
            MathF.Pow(normalLoad / referenceLoad, settings.LoadSensitivityExponent);
    }

    private static float MagicFormula(float slip, float shapeB, float shapeC, float curvatureE)
    {
        float value = shapeB * slip;
        return MathF.Sin(shapeC * MathF.Atan(value - (curvatureE * (value - MathF.Atan(value)))));
    }

    private static bool CrossedTarget(float before, float after, float target) =>
        ((before - target) * (after - target)) < 0.0f;

    private static bool IsFront(WheelCorner corner) =>
        corner is WheelCorner.FrontLeft or WheelCorner.FrontRight;

    private static bool IsLeft(WheelCorner corner) =>
        corner is WheelCorner.FrontLeft or WheelCorner.RearLeft;

    private static float WrapAngle(float angle) => MathF.IEEERemainder(angle, TwoPi);

    private static float FiniteOrZero(float value) => float.IsFinite(value) ? value : 0.0f;

    internal readonly record struct AbsResult(float Modulation, bool Intervening);

    private readonly record struct DriveAndBrakeCommand(
        float EqualFrontShaftTorqueNewtonMeters,
        float BrakeFraction);

    private readonly record struct WheelKinematics(
        Vector2 LocalPointMeters,
        Vector2 WheelForward,
        Vector2 WheelRight,
        float LongitudinalSpeedMetersPerSecond,
        float LateralSpeedMetersPerSecond,
        float SpeedMetersPerSecond);
}
