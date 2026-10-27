namespace CarGame;

internal enum GameOverReason
{
    None,
    Disabled,
    Exploded,
}

internal readonly record struct ScoreAwards(
    int CivilianInjuries = 0,
    int CivilianKills = 0,
    int CivilianExplosions = 0,
    int SoldierInjuries = 0,
    int SoldierKills = 0,
    int SoldierExplosions = 0,
    int DisabledVehicles = 0,
    int DestroyedVehicles = 0,
    int DamagedStructures = 0,
    int DestroyedStructures = 0,
    int DestroyedScenery = 0);

internal sealed class RunStatistics
{
    internal const int InjuredPersonPoints = 5;
    internal const int KilledPersonPoints = 10;
    internal const int ExplodedPersonPoints = 15;
    internal const int DisabledVehiclePoints = 10;
    internal const int DestroyedVehiclePoints = 20;
    internal const int DamagedStructurePoints = 10;
    internal const int DestroyedStructurePoints = 50;
    internal const int DestroyedSceneryPoints = 5;
    internal const float OverlayDelaySeconds = 5.0f;
    internal const float OverlayFadeSeconds = 1.0f;
    internal const float HudFadeSeconds = 0.35f;

    public ScoreAwards Breakdown { get; private set; }
    public int TotalScore =>
        (Breakdown.CivilianInjuries + Breakdown.SoldierInjuries) * InjuredPersonPoints +
        (Breakdown.CivilianKills + Breakdown.SoldierKills) * KilledPersonPoints +
        (Breakdown.CivilianExplosions + Breakdown.SoldierExplosions) * ExplodedPersonPoints +
        Breakdown.DisabledVehicles * DisabledVehiclePoints +
        Breakdown.DestroyedVehicles * DestroyedVehiclePoints +
        Breakdown.DamagedStructures * DamagedStructurePoints +
        Breakdown.DestroyedStructures * DestroyedStructurePoints +
        Breakdown.DestroyedScenery * DestroyedSceneryPoints;
    public float ElapsedSeconds { get; private set; }
    public float TopSpeedKilometersPerHour { get; private set; }
    public float DamageTakenPercent { get; private set; }
    public GameOverReason EndReason { get; private set; }
    public float GameOverAgeSeconds { get; private set; }
    public bool IsGameOver => EndReason != GameOverReason.None;
    public float OverlayOpacity => Math.Clamp(
        (GameOverAgeSeconds - OverlayDelaySeconds) / OverlayFadeSeconds,
        0.0f,
        1.0f);
    public float HudOpacity => 1.0f - Math.Clamp(
        GameOverAgeSeconds / HudFadeSeconds,
        0.0f,
        1.0f);

    public void Step(float fixedTimeStep, Vehicle vehicle)
    {
        if (IsGameOver)
        {
            GameOverAgeSeconds += fixedTimeStep;
            return;
        }

        ElapsedSeconds += fixedTimeStep;
        CaptureVehicleState(vehicle);
        if (vehicle.IsDisabled)
        {
            Finish(GameOverReason.Disabled, vehicle);
        }
        else if (vehicle.IsExploded)
        {
            Finish(GameOverReason.Exploded, vehicle);
        }
    }

    public void Award(ScoreAwards awards)
    {
        if (IsGameOver)
        {
            return;
        }

        Breakdown = new ScoreAwards(
            Breakdown.CivilianInjuries + awards.CivilianInjuries,
            Breakdown.CivilianKills + awards.CivilianKills,
            Breakdown.CivilianExplosions + awards.CivilianExplosions,
            Breakdown.SoldierInjuries + awards.SoldierInjuries,
            Breakdown.SoldierKills + awards.SoldierKills,
            Breakdown.SoldierExplosions + awards.SoldierExplosions,
            Breakdown.DisabledVehicles + awards.DisabledVehicles,
            Breakdown.DestroyedVehicles + awards.DestroyedVehicles,
            Breakdown.DamagedStructures + awards.DamagedStructures,
            Breakdown.DestroyedStructures + awards.DestroyedStructures,
            Breakdown.DestroyedScenery + awards.DestroyedScenery);
    }

    public void Finish(GameOverReason reason, Vehicle vehicle)
    {
        if (IsGameOver || reason == GameOverReason.None)
        {
            return;
        }

        CaptureVehicleState(vehicle);
        EndReason = reason;
        GameOverAgeSeconds = 0.0f;
    }

    public void Reset()
    {
        Breakdown = default;
        ElapsedSeconds = 0.0f;
        TopSpeedKilometersPerHour = 0.0f;
        DamageTakenPercent = 0.0f;
        EndReason = GameOverReason.None;
        GameOverAgeSeconds = 0.0f;
    }

    private void CaptureVehicleState(Vehicle vehicle)
    {
        float speedKilometersPerHour = vehicle.SpeedMetersPerSecond * 3.6f;
        if (float.IsFinite(speedKilometersPerHour))
        {
            TopSpeedKilometersPerHour = MathF.Max(TopSpeedKilometersPerHour, speedKilometersPerHour);
        }

        DamageTakenPercent = Math.Clamp(vehicle.DamagePercent, 0.0f, 100.0f);
    }
}
