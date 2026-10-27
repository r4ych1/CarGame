using System.Collections.Immutable;

namespace CarGame;

internal sealed record VehicleDefinition(
    string Name,
    ChassisSettings Chassis,
    SuspensionSettings Suspension,
    TireSettings Tires,
    PowertrainSettings Powertrain,
    TransmissionSettings Transmission,
    BrakeSettings Brakes)
{
    public float CenterOfGravityOffsetFromGeometricCenterMeters =>
        (0.5f - Chassis.FrontStaticWeightFraction) * Chassis.WheelbaseMeters;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("A vehicle name is required.", nameof(Name));
        }

        Chassis.Validate();
        Suspension.Validate();
        Tires.Validate();
        Powertrain.Validate();
        Transmission.Validate();
        Brakes.Validate();
    }
}

internal sealed record ChassisSettings(
    float MassKilograms,
    float WidthMeters,
    float LengthMeters,
    float WheelbaseMeters,
    float AxleTrackMeters,
    float FrontStaticWeightFraction,
    float CenterOfGravityHeightMeters,
    float MaximumForwardSpeedMetersPerSecond,
    float MaximumReverseSpeedMetersPerSecond,
    float RollingResistanceNewtonsPerMeterPerSecond,
    float AerodynamicDragNewtonsPerSpeedSquared,
    float MaximumSteeringAngleDegrees,
    float HighSpeedSteeringScale,
    float YawDampingPerSecond,
    float RestLinearSpeedMetersPerSecond,
    float RestAngularSpeedRadiansPerSecond)
{
    public void Validate()
    {
        Validation.Positive(MassKilograms, nameof(MassKilograms));
        Validation.Positive(WidthMeters, nameof(WidthMeters));
        Validation.Positive(LengthMeters, nameof(LengthMeters));
        Validation.Positive(WheelbaseMeters, nameof(WheelbaseMeters));
        Validation.Positive(AxleTrackMeters, nameof(AxleTrackMeters));
        Validation.FractionExclusive(FrontStaticWeightFraction, nameof(FrontStaticWeightFraction));
        Validation.NonNegative(CenterOfGravityHeightMeters, nameof(CenterOfGravityHeightMeters));
        Validation.Positive(MaximumForwardSpeedMetersPerSecond, nameof(MaximumForwardSpeedMetersPerSecond));
        Validation.Positive(MaximumReverseSpeedMetersPerSecond, nameof(MaximumReverseSpeedMetersPerSecond));
        Validation.NonNegative(RollingResistanceNewtonsPerMeterPerSecond, nameof(RollingResistanceNewtonsPerMeterPerSecond));
        Validation.NonNegative(AerodynamicDragNewtonsPerSpeedSquared, nameof(AerodynamicDragNewtonsPerSpeedSquared));
        Validation.Range(MaximumSteeringAngleDegrees, 0.0f, 89.0f, nameof(MaximumSteeringAngleDegrees));
        Validation.Fraction(HighSpeedSteeringScale, nameof(HighSpeedSteeringScale));
        Validation.NonNegative(YawDampingPerSecond, nameof(YawDampingPerSecond));
        Validation.NonNegative(RestLinearSpeedMetersPerSecond, nameof(RestLinearSpeedMetersPerSecond));
        Validation.NonNegative(RestAngularSpeedRadiansPerSecond, nameof(RestAngularSpeedRadiansPerSecond));
    }
}

internal sealed record SuspensionSettings(
    float FrontSpringRateNewtonsPerMeter,
    float RearSpringRateNewtonsPerMeter,
    float DampingRatio,
    float BumpTravelMeters,
    float DroopTravelMeters,
    float RollInertiaKilogramMetersSquared,
    float PitchInertiaKilogramMetersSquared)
{
    public void Validate()
    {
        Validation.Positive(FrontSpringRateNewtonsPerMeter, nameof(FrontSpringRateNewtonsPerMeter));
        Validation.Positive(RearSpringRateNewtonsPerMeter, nameof(RearSpringRateNewtonsPerMeter));
        Validation.Range(DampingRatio, 0.0f, 2.0f, nameof(DampingRatio));
        Validation.Positive(BumpTravelMeters, nameof(BumpTravelMeters));
        Validation.Positive(DroopTravelMeters, nameof(DroopTravelMeters));
        Validation.Positive(RollInertiaKilogramMetersSquared, nameof(RollInertiaKilogramMetersSquared));
        Validation.Positive(PitchInertiaKilogramMetersSquared, nameof(PitchInertiaKilogramMetersSquared));
    }
}

internal sealed record TireSettings(
    float PeakFrictionCoefficient,
    float LongitudinalShapeB,
    float LongitudinalShapeC,
    float LongitudinalCurvatureE,
    float LateralShapeB,
    float LateralShapeC,
    float LoadSensitivityExponent,
    float MinimumSlipSpeedMetersPerSecond,
    float LowSpeedForceBlendMetersPerSecond,
    float WheelRadiusMeters,
    float WheelInertiaKilogramMetersSquared)
{
    public void Validate()
    {
        Validation.NonNegative(PeakFrictionCoefficient, nameof(PeakFrictionCoefficient));
        Validation.Positive(LongitudinalShapeB, nameof(LongitudinalShapeB));
        Validation.Positive(LongitudinalShapeC, nameof(LongitudinalShapeC));
        Validation.Range(LongitudinalCurvatureE, 0.0f, 1.0f, nameof(LongitudinalCurvatureE));
        Validation.Positive(LateralShapeB, nameof(LateralShapeB));
        Validation.Positive(LateralShapeC, nameof(LateralShapeC));
        Validation.Range(LoadSensitivityExponent, 0.5f, 1.5f, nameof(LoadSensitivityExponent));
        Validation.Positive(MinimumSlipSpeedMetersPerSecond, nameof(MinimumSlipSpeedMetersPerSecond));
        Validation.Positive(LowSpeedForceBlendMetersPerSecond, nameof(LowSpeedForceBlendMetersPerSecond));
        Validation.Positive(WheelRadiusMeters, nameof(WheelRadiusMeters));
        Validation.Positive(WheelInertiaKilogramMetersSquared, nameof(WheelInertiaKilogramMetersSquared));
    }
}

internal readonly record struct EngineTorquePoint(float RevolutionsPerMinute, float TorqueNewtonMeters);

internal sealed record PowertrainSettings(
    float IdleRevolutionsPerMinute,
    float RedlineRevolutionsPerMinute,
    float DrivelineEfficiency,
    float EngineBrakingTorqueNewtonMeters,
    ImmutableArray<EngineTorquePoint> TorqueCurve)
{
    public void Validate()
    {
        Validation.Positive(IdleRevolutionsPerMinute, nameof(IdleRevolutionsPerMinute));
        Validation.Positive(RedlineRevolutionsPerMinute, nameof(RedlineRevolutionsPerMinute));
        if (RedlineRevolutionsPerMinute <= IdleRevolutionsPerMinute)
        {
            throw new ArgumentOutOfRangeException(nameof(RedlineRevolutionsPerMinute));
        }

        Validation.FractionExclusive(DrivelineEfficiency, nameof(DrivelineEfficiency));
        Validation.NonNegative(EngineBrakingTorqueNewtonMeters, nameof(EngineBrakingTorqueNewtonMeters));
        if (TorqueCurve.Length < 2)
        {
            throw new ArgumentException("The torque curve needs at least two points.", nameof(TorqueCurve));
        }

        float previousSpeed = -1.0f;
        foreach (EngineTorquePoint point in TorqueCurve)
        {
            Validation.NonNegative(point.RevolutionsPerMinute, nameof(TorqueCurve));
            Validation.NonNegative(point.TorqueNewtonMeters, nameof(TorqueCurve));
            if (point.RevolutionsPerMinute <= previousSpeed)
            {
                throw new ArgumentException("Torque curve speeds must increase.", nameof(TorqueCurve));
            }

            previousSpeed = point.RevolutionsPerMinute;
        }
    }
}

internal sealed record TransmissionSettings(
    ImmutableArray<float> ForwardRatios,
    float ReverseRatio,
    float FinalDriveRatio,
    ImmutableArray<float> UpshiftSpeedsMetersPerSecond,
    ImmutableArray<float> DownshiftSpeedsMetersPerSecond,
    float ShiftDurationSeconds,
    float DirectionSelectionSpeedMetersPerSecond)
{
    public void Validate()
    {
        if (ForwardRatios.Length == 0)
        {
            throw new ArgumentException("At least one forward ratio is required.", nameof(ForwardRatios));
        }

        foreach (float ratio in ForwardRatios)
        {
            Validation.Positive(ratio, nameof(ForwardRatios));
        }

        Validation.Positive(ReverseRatio, nameof(ReverseRatio));
        Validation.Positive(FinalDriveRatio, nameof(FinalDriveRatio));
        int shiftBoundaryCount = ForwardRatios.Length - 1;
        if (UpshiftSpeedsMetersPerSecond.Length != shiftBoundaryCount)
        {
            throw new ArgumentException(
                "One upshift speed is required between each pair of forward gears.",
                nameof(UpshiftSpeedsMetersPerSecond));
        }
        if (DownshiftSpeedsMetersPerSecond.Length != shiftBoundaryCount)
        {
            throw new ArgumentException(
                "One downshift speed is required between each pair of forward gears.",
                nameof(DownshiftSpeedsMetersPerSecond));
        }

        float previousUpshiftSpeed = 0.0f;
        float previousDownshiftSpeed = 0.0f;
        for (int index = 0; index < shiftBoundaryCount; index++)
        {
            float upshiftSpeed = UpshiftSpeedsMetersPerSecond[index];
            float downshiftSpeed = DownshiftSpeedsMetersPerSecond[index];
            Validation.Positive(upshiftSpeed, nameof(UpshiftSpeedsMetersPerSecond));
            Validation.Positive(downshiftSpeed, nameof(DownshiftSpeedsMetersPerSecond));
            if (upshiftSpeed <= previousUpshiftSpeed || downshiftSpeed <= previousDownshiftSpeed)
            {
                throw new ArgumentException("Forward shift speeds must increase by gear.");
            }
            if (downshiftSpeed >= upshiftSpeed)
            {
                throw new ArgumentException("Each downshift speed must be below its matching upshift speed.");
            }

            previousUpshiftSpeed = upshiftSpeed;
            previousDownshiftSpeed = downshiftSpeed;
        }

        Validation.NonNegative(ShiftDurationSeconds, nameof(ShiftDurationSeconds));
        Validation.NonNegative(DirectionSelectionSpeedMetersPerSecond, nameof(DirectionSelectionSpeedMetersPerSecond));
    }
}

internal sealed record BrakeSettings(
    float MaximumBrakingForceNewtons,
    float FrontBias,
    float AbsSlipThreshold,
    float AbsReleaseThreshold,
    float AbsReleaseRatePerSecond,
    float AbsReapplyRatePerSecond,
    float AbsMinimumSpeedMetersPerSecond)
{
    public void Validate()
    {
        Validation.NonNegative(MaximumBrakingForceNewtons, nameof(MaximumBrakingForceNewtons));
        Validation.Fraction(FrontBias, nameof(FrontBias));
        Validation.Range(AbsSlipThreshold, 0.0f, 1.0f, nameof(AbsSlipThreshold));
        Validation.Range(AbsReleaseThreshold, 0.0f, AbsSlipThreshold, nameof(AbsReleaseThreshold));
        Validation.Positive(AbsReleaseRatePerSecond, nameof(AbsReleaseRatePerSecond));
        Validation.Positive(AbsReapplyRatePerSecond, nameof(AbsReapplyRatePerSecond));
        Validation.NonNegative(AbsMinimumSpeedMetersPerSecond, nameof(AbsMinimumSpeedMetersPerSecond));
    }
}

internal static class Validation
{
    public static void Positive(float value, string name) => Range(value, float.Epsilon, float.MaxValue, name);

    public static void NonNegative(float value, string name) => Range(value, 0.0f, float.MaxValue, name);

    public static void Fraction(float value, string name) => Range(value, 0.0f, 1.0f, name);

    public static void FractionExclusive(float value, string name) => Range(value, float.Epsilon, 1.0f - float.Epsilon, name);

    public static void Range(float value, float minimum, float maximum, string name)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name, $"Value must be finite and between {minimum} and {maximum}.");
        }
    }
}
