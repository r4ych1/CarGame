namespace CarGame;

internal static class VehicleCatalog
{
    public static VehicleDefinition FirstCar { get; } = new(
        "First Car",
        new ChassisSettings(
            MassKilograms: 1_300.0f,
            WidthMeters: 1.8f,
            LengthMeters: 4.2f,
            WheelbaseMeters: 2.7f,
            AxleTrackMeters: 1.5f,
            FrontStaticWeightFraction: 0.60f,
            CenterOfGravityHeightMeters: 0.55f,
            MaximumForwardSpeedMetersPerSecond: 32.0f,
            MaximumReverseSpeedMetersPerSecond: 11.0f,
            RollingResistanceNewtonsPerMeterPerSecond: 110.0f,
            AerodynamicDragNewtonsPerSpeedSquared: 2.8f,
            MaximumSteeringAngleDegrees: 32.0f,
            HighSpeedSteeringScale: 0.45f,
            YawDampingPerSecond: 0.15f,
            RestLinearSpeedMetersPerSecond: 0.5f,
            RestAngularSpeedRadiansPerSecond: 0.1f),
        new SuspensionSettings(
            FrontSpringRateNewtonsPerMeter: 30_000.0f,
            RearSpringRateNewtonsPerMeter: 25_000.0f,
            DampingRatio: 0.70f,
            BumpTravelMeters: 0.16f,
            DroopTravelMeters: 0.10f,
            RollInertiaKilogramMetersSquared: 520.0f,
            PitchInertiaKilogramMetersSquared: 1_850.0f),
        new TireSettings(
            PeakFrictionCoefficient: 1.05f,
            LongitudinalShapeB: 10.0f,
            LongitudinalShapeC: 1.9f,
            LongitudinalCurvatureE: 0.97f,
            LateralShapeB: 5.0f,
            LateralShapeC: 1.3f,
            LoadSensitivityExponent: 0.92f,
            MinimumSlipSpeedMetersPerSecond: 0.75f,
            LowSpeedForceBlendMetersPerSecond: 2.0f,
            WheelRadiusMeters: 0.31f,
            WheelInertiaKilogramMetersSquared: 1.25f),
        new PowertrainSettings(
            IdleRevolutionsPerMinute: 850.0f,
            RedlineRevolutionsPerMinute: 6_500.0f,
            DrivelineEfficiency: 0.90f,
            EngineBrakingTorqueNewtonMeters: 45.0f,
            TorqueCurve:
            [
                new(850.0f, 125.0f),
                new(2_000.0f, 175.0f),
                new(3_500.0f, 205.0f),
                new(5_000.0f, 195.0f),
                new(6_500.0f, 150.0f),
            ]),
        new TransmissionSettings(
            ForwardRatios: [3.55f, 2.10f, 1.36f, 1.03f, 0.82f],
            ReverseRatio: 3.40f,
            FinalDriveRatio: 3.90f,
            UpshiftRevolutionsPerMinute: 4_800.0f,
            DownshiftRevolutionsPerMinute: 1_800.0f,
            ShiftDurationSeconds: 0.14f,
            DirectionSelectionSpeedMetersPerSecond: 0.5f),
        new BrakeSettings(
            MaximumBrakingForceNewtons: 12_000.0f,
            FrontBias: 0.68f,
            AbsSlipThreshold: 0.15f,
            AbsReleaseThreshold: 0.08f,
            AbsReleaseRatePerSecond: 8.0f,
            AbsReapplyRatePerSecond: 2.5f,
            AbsMinimumSpeedMetersPerSecond: 1.5f));
}
