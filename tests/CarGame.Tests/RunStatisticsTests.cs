using System.Numerics;
using Box2D.NET;
using Xunit;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2MathFunction;

namespace CarGame.Tests;

public sealed class RunStatisticsTests
{
    private const float FixedTimeStep = 1.0f / 60.0f;

    [Fact]
    public void ScoreAwardsUseConfiguredValues()
    {
        RunStatistics run = new();

        run.Award(new ScoreAwards(
            CivilianInjuries: 1,
            CivilianKills: 1,
            CivilianExplosions: 1,
            SoldierInjuries: 1,
            SoldierKills: 1,
            SoldierExplosions: 1,
            DisabledVehicles: 1,
            DestroyedVehicles: 1,
            DamagedStructures: 1,
            DestroyedStructures: 1,
            DestroyedScenery: 1));

        Assert.Equal(155, run.TotalScore);
        Assert.Equal(1, run.Breakdown.CivilianKills);
        Assert.Equal(1, run.Breakdown.SoldierKills);
        Assert.Equal(1, run.Breakdown.DestroyedVehicles);
    }

    [Fact]
    public void TimerStopsAndOverlayFadesAfterFiveSecondDelay()
    {
        using GameWorld world = new();
        world.Step(default, FixedTimeStep, 4);
        world.Vehicle.ApplyCollisionDamage(28.0f);
        world.Vehicle.ApplyCollisionDamage(28.0f);
        world.Step(default, FixedTimeStep, 4);
        float terminalTime = world.RunStatistics.ElapsedSeconds;

        Assert.True(world.RunStatistics.IsGameOver);
        Assert.Equal(GameOverReason.Disabled, world.RunStatistics.EndReason);
        Assert.Equal(100.0f, world.RunStatistics.DamageTakenPercent);

        for (int index = 0; index < 300; index++)
        {
            world.Step(default, FixedTimeStep, 4);
        }

        Assert.Equal(terminalTime, world.RunStatistics.ElapsedSeconds);
        Assert.Equal(0.0f, world.RunStatistics.OverlayOpacity, 0.001f);

        for (int index = 0; index < 60; index++)
        {
            world.Step(default, FixedTimeStep, 4);
        }

        Assert.Equal(1.0f, world.RunStatistics.OverlayOpacity, 0.001f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HudFadesImmediatelyWhenRunEndsBeforeGameOverTableAppears(bool disableVehicle)
    {
        using GameWorld world = new();
        if (disableVehicle)
        {
            world.Vehicle.ApplyCollisionDamage(28.0f);
            world.Vehicle.ApplyCollisionDamage(28.0f);
            world.Step(default, FixedTimeStep, 4);
        }
        else
        {
            world.ExplodeVehicle();
        }

        Assert.Equal(1.0f, world.RunStatistics.HudOpacity, 0.001f);
        Assert.Equal(0.0f, world.RunStatistics.OverlayOpacity, 0.001f);

        for (int index = 0; index < 11; index++)
        {
            world.Step(default, FixedTimeStep, 4);
        }

        Assert.InRange(world.RunStatistics.HudOpacity, 0.0f, 1.0f);
        Assert.True(world.RunStatistics.HudOpacity < 1.0f);
        Assert.Equal(0.0f, world.RunStatistics.OverlayOpacity, 0.001f);

        for (int index = 11; index < 21; index++)
        {
            world.Step(default, FixedTimeStep, 4);
        }

        Assert.Equal(0.0f, world.RunStatistics.HudOpacity, 0.001f);
        Assert.Equal(0.0f, world.RunStatistics.OverlayOpacity, 0.001f);
    }

    [Fact]
    public void TerminalExplosionAwardsOutcomesBeforeEndingRun()
    {
        using GameWorld world = new();
        int expectedScore = CalculateExplosionScore(world.Environment, world.Vehicle.PositionMeters);

        world.ExplodeVehicle();

        Assert.True(expectedScore > 0);
        Assert.True(world.RunStatistics.IsGameOver);
        Assert.Equal(GameOverReason.Exploded, world.RunStatistics.EndReason);
        Assert.Equal(expectedScore, world.RunStatistics.TotalScore);
    }

    [Fact]
    public void PlayerRunOverScoresOnceAndResetClearsRunState()
    {
        using GameWorld world = new();
        AmbientCharacter character = world.Environment.Characters[0];
        Vector2 impactVelocity = new(0.0f, -8.0f);
        b2Body_SetTransform(
            world.Vehicle.BodyId,
            new B2Vec2(character.PositionMeters.X, character.PositionMeters.Y),
            b2MakeRot(0.0f));
        b2Body_SetLinearVelocity(
            world.Vehicle.BodyId,
            new B2Vec2(impactVelocity.X, impactVelocity.Y));

        world.Step(default, FixedTimeStep, 4);
        int scoreAfterImpact = world.RunStatistics.TotalScore;
        world.Step(default, FixedTimeStep, 4);

        Assert.True(character.IsDead);
        Assert.Equal(RunStatistics.KilledPersonPoints, scoreAfterImpact);
        Assert.Equal(1, world.RunStatistics.Breakdown.CivilianKills);
        Assert.Equal(0, world.RunStatistics.Breakdown.SoldierKills);
        Assert.Equal(scoreAfterImpact, world.RunStatistics.TotalScore);

        world.ResetVehicle();

        Assert.Equal(0, world.RunStatistics.TotalScore);
        Assert.Equal(0.0f, world.RunStatistics.ElapsedSeconds);
        Assert.Equal(0.0f, world.RunStatistics.TopSpeedKilometersPerHour);
        Assert.False(world.RunStatistics.IsGameOver);
    }

    [Theory]
    [InlineData(0.0f, "00:00.0")]
    [InlineData(65.49f, "01:05.4")]
    public void RunTimeUsesStableMinuteSecondFormatting(float seconds, string expected)
    {
        Assert.Equal(expected, GameRenderer.FormatTime(seconds));
    }

    private static int CalculateExplosionScore(DrivingEnvironment environment, Vector2 center)
    {
        int score = 0;
        foreach (AmbientCharacter character in environment.Characters)
        {
            float distance = Vector2.Distance(character.PositionMeters, center);
            if (distance <= AmbientCharacter.LimbBurstRadiusMeters)
            {
                score += RunStatistics.ExplodedPersonPoints;
            }
            else if (distance <= DrivingEnvironment.CharacterDeadlyBlastRadiusMeters)
            {
                score += RunStatistics.KilledPersonPoints;
            }
            else if (distance <= DrivingEnvironment.BlastRadiusMeters)
            {
                score += RunStatistics.InjuredPersonPoints;
            }
        }

        foreach (SceneryObject item in environment.Scenery)
        {
            float distance = Vector2.Distance(item.PositionMeters, center);
            if (distance > DrivingEnvironment.BlastRadiusMeters)
            {
                continue;
            }

            if (item.Kind is SceneryKind.Building or SceneryKind.Shack)
            {
                score += distance <= DrivingEnvironment.DeadlyBlastRadiusMeters
                    ? RunStatistics.DestroyedStructurePoints
                    : RunStatistics.DamagedStructurePoints;
            }
            else if (item.Kind is SceneryKind.ParkedCar or SceneryKind.ParkedTruck)
            {
                score += RunStatistics.DestroyedVehiclePoints;
            }
            else
            {
                score += RunStatistics.DestroyedSceneryPoints;
            }
        }

        foreach (TrafficVehicle vehicle in environment.Traffic.Vehicles)
        {
            if (!vehicle.IsActive || vehicle.IsWrecked)
            {
                continue;
            }

            float distance = Vector2.Distance(vehicle.PositionMeters, center);
            if (distance <= DrivingEnvironment.DeadlyBlastRadiusMeters)
            {
                score += RunStatistics.DestroyedVehiclePoints;
            }
            else if (distance <= DrivingEnvironment.BlastRadiusMeters)
            {
                score += RunStatistics.DisabledVehiclePoints;
            }
        }

        return score;
    }
}
