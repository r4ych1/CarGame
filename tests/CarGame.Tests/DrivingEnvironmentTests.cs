using System.Numerics;
using Xunit;

namespace CarGame.Tests;

public sealed class DrivingEnvironmentTests
{
    [Fact]
    public void EnvironmentBoundaryHasFinitePlayableBounds()
    {
        Assert.Equal(new Vector2(-80.0f, -380.0f), DrivingEnvironment.BoundaryMinimumMeters);
        Assert.Equal(new Vector2(80.0f, 70.0f), DrivingEnvironment.BoundaryMaximumMeters);
        Assert.True(DrivingEnvironment.BoundaryMaximumMeters.X > DrivingEnvironment.BoundaryMinimumMeters.X);
        Assert.True(DrivingEnvironment.BoundaryMaximumMeters.Y > DrivingEnvironment.BoundaryMinimumMeters.Y);
    }

    [Fact]
    public void SouthernBoundaryStopsReversingVehicle()
    {
        using GameWorld world = new();

        for (int index = 0; index < 1_200; index++)
        {
            world.Step(new VehicleInput(-1.0f, 0.0f), 1.0f / 60.0f, 4);
        }

        Assert.True(world.Vehicle.PositionMeters.Y < DrivingEnvironment.BoundaryMaximumMeters.Y);
        Assert.True(world.Vehicle.PositionMeters.Y > 60.0f);
    }

    [Fact]
    public void LayoutContainsRuralVegetationRocksAndTownBuildingRows()
    {
        using GameWorld world = new();

        Assert.Contains(world.Environment.Scenery, item => item.Kind == SceneryKind.Tree);
        Assert.Contains(world.Environment.Scenery, item => item.Kind == SceneryKind.Bush);
        Assert.Contains(world.Environment.Scenery, item => item.Kind == SceneryKind.Rock);
        Assert.True(world.Environment.Scenery.Count(item => item.Kind == SceneryKind.Building) >= 10);
        Assert.Contains(world.Environment.Scenery, item => item.Kind == SceneryKind.Shack);
        Assert.Contains(world.Environment.Scenery, item => item.Kind == SceneryKind.ParkedCar);
        Assert.Contains(world.Environment.Scenery, item => item.Kind == SceneryKind.ParkedTruck);
    }

    [Fact]
    public void StreamIsDeepOutsideBridgeAndSafeOnBridge()
    {
        using GameWorld world = new();

        Assert.True(world.Environment.IsDeepWater(new Vector2(25.0f, DrivingEnvironment.StreamCenterYMeters)));
        Assert.False(world.Environment.IsDeepWater(new Vector2(
            DrivingEnvironment.BridgeCenterXMeters,
            DrivingEnvironment.StreamCenterYMeters)));
        Assert.False(world.Environment.IsDeepWater(new Vector2(25.0f, -105.0f)));
    }

    [Fact]
    public void PeopleSpawnOutsideStreamAndFleeAlongItsBank()
    {
        using GameWorld world = new();
        Assert.DoesNotContain(world.Environment.Characters, character =>
            DrivingEnvironment.IsPedestrianInStream(character.PositionMeters));
        Assert.All(world.Environment.Characters, character =>
            Assert.DoesNotContain(character.AuthoredPath, DrivingEnvironment.IsPedestrianInStream));

        AmbientCharacter watersideCharacter = Assert.Single(world.Environment.Characters, character =>
            character.PositionMeters == new Vector2(-26.0f, -104.0f));
        watersideCharacter.ReactToExplosion(new Vector2(-26.0f, -90.0f), 44.0f);

        for (int index = 0; index < 360; index++)
        {
            world.Step(default, 1.0f / 60.0f, 4);
            Assert.False(DrivingEnvironment.IsPedestrianInStream(watersideCharacter.PositionMeters));
        }

        Assert.True(watersideCharacter.IsPanicking);
    }

    [Fact]
    public void RoadIsStraightAndCenteredAcrossBridge()
    {
        float[] crossingSamples = [-136.0f, -132.0f, -120.0f, -108.0f, -104.0f];

        Assert.All(crossingSamples, y =>
            Assert.Equal(DrivingEnvironment.BridgeCenterXMeters, DrivingEnvironment.RoadCenterXMeters(y), 0.0001f));
        Assert.NotEqual(
            DrivingEnvironment.BridgeCenterXMeters,
            DrivingEnvironment.RoadCenterXMeters(-80.0f));
    }

    [Fact]
    public void BushesRemainOutsideMainRoadAndDirtRoadBypassesCheckpoint()
    {
        using GameWorld world = new();
        SceneryObject[] bushes = world.Environment.Scenery.Where(item => item.Kind == SceneryKind.Bush).ToArray();

        Assert.All(bushes, bush =>
            Assert.True(MathF.Abs(bush.PositionMeters.X - DrivingEnvironment.RoadCenterXMeters(bush.PositionMeters.Y)) > 7.0f));
        Assert.All(bushes, bush =>
            Assert.True(DrivingEnvironment.IsBushClearOfRoads(
                bush.PositionMeters,
                bush.SizeMeters.X * 0.65f)));

        Assert.False(DrivingEnvironment.IsBushClearOfRoads(
            new Vector2(DrivingEnvironment.RoadCenterXMeters(-80.0f), -80.0f),
            1.0f));
        Assert.False(DrivingEnvironment.IsBushClearOfRoads(
            DrivingEnvironment.DirtRoadPointsMeters[4],
            1.0f));
        Assert.False(DrivingEnvironment.IsBushClearOfRoads(
            new Vector2(0.0f, -264.5f),
            1.0f));

        IReadOnlyList<Vector2> bypass = DrivingEnvironment.DirtRoadPointsMeters;
        Assert.Equal(DrivingEnvironment.RoadCenterXMeters(bypass[0].Y), bypass[0].X, 0.0001f);
        Assert.Equal(DrivingEnvironment.RoadCenterXMeters(bypass[^1].Y), bypass[^1].X, 0.0001f);
        Assert.Contains(bypass, point => point.X > 45.0f && point.Y < -175.0f && point.Y > -225.0f);
    }

    [Fact]
    public void StreamCorridorHasNoVegetationAndFlowAdvancesOnFixedStep()
    {
        using GameWorld world = new();
        float exclusionHalfWidth = DrivingEnvironment.StreamWidthMeters * 0.5f + 4.0f;
        IEnumerable<SceneryObject> vegetation = world.Environment.Scenery.Where(item =>
            item.Kind is SceneryKind.Tree or SceneryKind.Bush);

        Assert.All(vegetation, item =>
            Assert.True(MathF.Abs(item.PositionMeters.Y - DrivingEnvironment.StreamCenterYMeters) > exclusionHalfWidth));

        world.Environment.Step(0.5f);
        Assert.Equal(0.5f, world.Environment.WaterFlowAgeSeconds);
    }

    [Fact]
    public void TownWallsEncloseBuildingRowsWithOneGuardedEntrance()
    {
        using GameWorld world = new();
        IEnumerable<SceneryObject> townBuildings = world.Environment.Scenery.Where(item =>
            item.Kind == SceneryKind.Building && item.PositionMeters.Y < -240.0f);

        Assert.Equal(5, world.Environment.TownWalls.Count);
        Assert.All(townBuildings, building =>
        {
            Assert.InRange(building.PositionMeters.X, DrivingEnvironment.TownMinimumMeters.X, DrivingEnvironment.TownMaximumMeters.X);
            Assert.InRange(building.PositionMeters.Y, DrivingEnvironment.TownMinimumMeters.Y, DrivingEnvironment.TownMaximumMeters.Y);
        });
        Assert.Single(world.Environment.Props, prop =>
            prop.Kind == PhysicsPropKind.GuardRail &&
            prop.PositionMeters == DrivingEnvironment.TownEntranceCenterMeters);
    }

    [Fact]
    public void CheckpointHasStaggeredLaneBarriersAndRoadsideShack()
    {
        using GameWorld world = new();

        PhysicsProp southBarrier = Assert.Single(world.Environment.Props, prop =>
            prop.Kind == PhysicsPropKind.GuardRail &&
            prop.PositionMeters == DrivingEnvironment.CheckpointSouthBarrierMeters);
        PhysicsProp northBarrier = Assert.Single(world.Environment.Props, prop =>
            prop.Kind == PhysicsPropKind.GuardRail &&
            prop.PositionMeters == DrivingEnvironment.CheckpointNorthBarrierMeters);
        SceneryObject shack = Assert.Single(world.Environment.Scenery, item =>
            item.Kind == SceneryKind.Shack &&
            item.PositionMeters == DrivingEnvironment.CheckpointShackMeters);

        Assert.NotEqual(southBarrier.PositionMeters.X, northBarrier.PositionMeters.X);
        Assert.NotEqual(southBarrier.PositionMeters.Y, northBarrier.PositionMeters.Y);
        Assert.True(MathF.Abs(southBarrier.PositionMeters.Y - northBarrier.PositionMeters.Y) >= 18.0f);
        Assert.False(southBarrier.IsAnchored);
        Assert.False(northBarrier.IsAnchored);
        Assert.Equal(DrivingEnvironment.CheckpointShackMeters, shack.PositionMeters);
    }

    [Fact]
    public void RoadHasCheckpointAndAnchoredBridgeGuardRailsAndApproachCones()
    {
        using GameWorld world = new();
        PhysicsProp[] guardRails = world.Environment.Props.Where(prop => prop.Kind == PhysicsPropKind.GuardRail).ToArray();
        PhysicsProp[] cones = world.Environment.Props.Where(prop => prop.Kind == PhysicsPropKind.TrafficCone).ToArray();
        PhysicsProp[] bridgeRails = guardRails.Where(prop =>
            MathF.Abs(prop.PositionMeters.Y - DrivingEnvironment.StreamCenterYMeters) <
            DrivingEnvironment.StreamWidthMeters * 0.5f).ToArray();

        Assert.Equal(11, guardRails.Length);
        Assert.Equal(8, bridgeRails.Length);
        Assert.All(bridgeRails, rail => Assert.True(rail.IsAnchored));
        Assert.Equal(4, bridgeRails.Count(rail => rail.PositionMeters.X < DrivingEnvironment.BridgeCenterXMeters));
        Assert.Equal(4, bridgeRails.Count(rail => rail.PositionMeters.X > DrivingEnvironment.BridgeCenterXMeters));
        Assert.Equal(8, cones.Length);
        Assert.All(cones, cone => Assert.InRange(cone.PositionMeters.Y, -240.0f, -200.0f));
    }

    [Fact]
    public void BlastOnlyDestroysNearbyObjectsAndCreatesCraterAndDebris()
    {
        using GameWorld world = new();
        SceneryObject near = world.Environment.Scenery.OrderBy(item => item.PositionMeters.Length()).First();
        SceneryObject far = world.Environment.Scenery.OrderByDescending(item => item.PositionMeters.Length()).First();

        world.ExplodeVehicle();

        Assert.True(near.PositionMeters.Length() <= DrivingEnvironment.BlastRadiusMeters);
        Assert.True(near.IsDestroyed);
        Assert.False(far.IsDestroyed);
        Assert.Single(world.Environment.Craters);
        Assert.NotEmpty(world.Environment.Debris);
        Assert.All(world.Environment.Debris, item =>
        {
            Assert.True(float.IsFinite(item.PositionMeters.X));
            Assert.True(float.IsFinite(item.PositionMeters.Y));
        });
    }

    [Fact]
    public void BlastedTreeUsesLongTrunkAndStrongLimitedKnockback()
    {
        using GameWorld world = new();
        SceneryObject tree = world.Environment.Scenery.First(item => item.Kind == SceneryKind.Tree);

        world.Environment.Explode(tree.PositionMeters);
        world.Environment.Step(1.0f / 60.0f);

        WorldDebris fallenTree = world.Environment.Debris.First(item => item.Kind == SceneryKind.Tree);
        Assert.Equal(new Vector2(0.7f, 5.5f), fallenTree.SizeMeters);
        Assert.True(fallenTree.LinearVelocityMetersPerSecond.Length() > 20.0f);
        Assert.InRange(fallenTree.AngularVelocityRadiansPerSecond, -0.55f, 0.55f);
    }

    [Fact]
    public void ResetRestoresDestroyedEnvironment()
    {
        using GameWorld world = new();
        int originalCount = world.Environment.Scenery.Count;
        world.ExplodeVehicle();

        world.ResetVehicle();

        Assert.Equal(originalCount, world.Environment.Scenery.Count);
        Assert.DoesNotContain(world.Environment.Scenery, item => item.IsDestroyed);
        Assert.Empty(world.Environment.Debris);
        Assert.Empty(world.Environment.Craters);
        Assert.Equal(Vector2.Zero, world.Vehicle.PositionMeters);
    }

    [Fact]
    public void BlastCreatesVariedLayeredVisualEffectThatPersistsUntilReset()
    {
        using GameWorld world = new();
        world.ExplodeVehicle();

        BlastVisualEffect effect = Assert.Single(world.Environment.BlastEffects);
        Assert.Equal(28, effect.Smoke.Count);
        Assert.Equal(18, effect.Fire.Count);
        Assert.Equal(22, effect.GroundFire.Count);
        Assert.Equal(36, effect.Debris.Count);
        Assert.Equal(Vector2.Zero, effect.CenterMeters);
        Assert.True(effect.GroundFire.Max(particle => particle.SizeMeters) >=
            effect.GroundFire.Min(particle => particle.SizeMeters) * 4.0f);
        Assert.True(effect.Smoke.Max(particle => particle.SizeMeters) >=
            effect.Smoke.Min(particle => particle.SizeMeters) * 4.0f);
        float[] initialGroundSmokeRadii = Enumerable.Range(0, BlastVisualEffect.GroundSmokeLayerCount)
            .Select(effect.GroundSmokeLayerRadiusMeters)
            .ToArray();
        Assert.Equal(4, initialGroundSmokeRadii.Length);
        Assert.Equal(initialGroundSmokeRadii.Order(), initialGroundSmokeRadii);

        for (int index = 0; index < 240; index++)
            world.Step(default, 1.0f / 60.0f, 4);

        BlastVisualEffect lingeringEffect = Assert.Single(world.Environment.BlastEffects);
        Assert.Equal(22, lingeringEffect.GroundFire.Count);
        Assert.Contains(lingeringEffect.GroundFire, particle => particle.OffsetMeters.Length() > 20.0f);
        Assert.InRange(lingeringEffect.GroundFireOpacity, 0.65f, 0.68f);
        Assert.InRange(lingeringEffect.GroundSmokeOpacity, 0.65f, 0.68f);
        Assert.True(lingeringEffect.GroundSmokeLayerRadiusMeters(0) > initialGroundSmokeRadii[0]);
        float firstRingProgress = lingeringEffect.SmokeRingProgress(lingeringEffect.Smoke[0], 0);
        float laterRingProgress = lingeringEffect.SmokeRingProgress(lingeringEffect.Smoke[0], 1);
        Assert.True(firstRingProgress > laterRingProgress);

        for (int index = 0; index < 3_600; index++)
            world.Step(default, 1.0f / 60.0f, 4);

        Assert.Same(lingeringEffect, Assert.Single(world.Environment.BlastEffects));
        Assert.Equal(0.0f, lingeringEffect.GroundFireOpacity);
        Assert.Equal(0.0f, lingeringEffect.GroundSmokeOpacity);
        Assert.InRange(lingeringEffect.SmokeRingProgress(lingeringEffect.Smoke[0], 0), 0.0f, 1.0f);

        world.ResetVehicle();

        Assert.Empty(world.Environment.BlastEffects);
    }

    [Fact]
    public void CloseBlastUsesExpandedBreakupRadiusAndReducedLimbSpeed()
    {
        using GameWorld world = new();
        AmbientCharacter closeCharacter = world.Environment.Characters[0];
        Vector2 closeBlastCenter = closeCharacter.PositionMeters - new Vector2(9.0f, 0.0f);

        closeCharacter.ApplyBlast(
            closeBlastCenter,
            DrivingEnvironment.BlastRadiusMeters,
            DrivingEnvironment.CharacterDeadlyBlastRadiusMeters);

        Assert.Equal(10.0f, AmbientCharacter.LimbBurstRadiusMeters);
        Assert.True(closeCharacter.HasExplodedIntoLimbs);
        Assert.Equal(6, closeCharacter.LimbFragments.Count);
        Assert.All(closeCharacter.LimbFragments, limb =>
            Assert.InRange(limb.LinearVelocityMetersPerSecond.Length(), 4.0f, 15.8f));
        Assert.All(closeCharacter.LimbFragments, limb => Assert.False(limb.HasBloodPool));

        closeCharacter.Step(0.76f);
        Assert.All(closeCharacter.LimbFragments, limb =>
        {
            Assert.True(limb.HasBloodPool);
            Assert.True(limb.BloodPoolRadiusMeters > 0.0f);
            Assert.Equal(limb.PositionMeters, limb.BloodPoolPositionMeters);
        });

        float initialPoolRadius = closeCharacter.LimbFragments[0].BloodPoolRadiusMeters;
        closeCharacter.Step(0.8f);
        Assert.True(closeCharacter.LimbFragments[0].BloodPoolRadiusMeters > initialPoolRadius);

        AmbientCharacter fartherCharacter = world.Environment.Characters[1];
        Vector2 fartherBlastCenter = fartherCharacter.PositionMeters - new Vector2(10.1f, 0.0f);
        fartherCharacter.ApplyBlast(
            fartherBlastCenter,
            DrivingEnvironment.BlastRadiusMeters,
            DrivingEnvironment.CharacterDeadlyBlastRadiusMeters);

        Assert.True(fartherCharacter.IsDead);
        Assert.False(fartherCharacter.HasExplodedIntoLimbs);
    }

    [Fact]
    public void PanicSteeringSelectsClearAngledRouteAroundWall()
    {
        WallSegment[] walls = [new(new Vector2(4.0f, 0.0f), new Vector2(2.0f, 12.0f))];

        Vector2 direction = AmbientCharacter.ChoosePanicDirection(
            Vector2.Zero,
            Vector2.UnitX,
            walls);

        Assert.True(Vector2.Dot(direction, Vector2.UnitX) > -0.0001f);
        Assert.True(MathF.Abs(direction.Y) > 0.9f);
        Assert.Equal(
            Vector2.UnitX,
            AmbientCharacter.ChoosePanicDirection(Vector2.Zero, Vector2.UnitX, []));
    }

    [Fact]
    public void VehicleImpactBrieflyTransfersMomentumBeforeDisablingCollision()
    {
        using GameWorld world = new();
        AmbientCharacter character = world.Environment.Characters[0];
        Vector2 impactPosition = character.PositionMeters;

        character.DieFromVehicleImpact(new Vector2(0.0f, -20.0f));

        Assert.True(character.IsDead);
        Assert.False(character.IsCollisionDisabled);
        Assert.InRange(character.LinearVelocityMetersPerSecond.Length(), 6.9f, 7.1f);

        character.Step(0.23f, world.Environment.TownWalls);

        Assert.True(character.IsCollisionDisabled);
        Assert.Equal(impactPosition, character.PositionMeters);
        Assert.True(character.DeathAgeSeconds > 0.0f);
    }

    [Fact]
    public void ApproachingVehicleTriggersLateralFleeDirection()
    {
        using GameWorld world = new();
        AmbientCharacter character = world.Environment.Characters[0];
        Vector2 facing = new(MathF.Cos(character.FacingRadians), MathF.Sin(character.FacingRadians));
        Vector2 vehiclePosition = character.PositionMeters + facing * 5.0f;
        Vector2 vehicleVelocity = -facing * 12.0f;

        character.TryFleeFromVehicle(vehiclePosition, vehicleVelocity, 10.0f);

        Assert.True(character.IsPanicking);
        Assert.InRange(MathF.Abs(Vector2.Dot(character.PanicDirection, vehicleVelocity)), 0.0f, 0.0001f);
    }

    [Fact]
    public void SixGuardsSpawnAtCheckpointAndInsideTownEntrance()
    {
        using GameWorld world = new();
        AmbientCharacter[] guards = world.Environment.Characters
            .Where(character => character.Role == CharacterRole.Guard)
            .ToArray();

        Assert.Equal(6, guards.Length);
        Assert.Equal(3, guards.Count(guard => guard.PositionMeters.Y > DrivingEnvironment.TownMaximumMeters.Y));
        Assert.Equal(3, guards.Count(guard => guard.PositionMeters.Y < DrivingEnvironment.TownMaximumMeters.Y));
        Assert.Contains(guards, guard => guard.GuardPatrolsAtPost);
        Assert.Contains(guards, guard => !guard.GuardPatrolsAtPost);
        Assert.All(guards.Where(guard => guard.PositionMeters.Y < DrivingEnvironment.TownMaximumMeters.Y), guard =>
        {
            Assert.InRange(guard.PositionMeters.X, DrivingEnvironment.TownMinimumMeters.X, DrivingEnvironment.TownMaximumMeters.X);
            Assert.InRange(guard.PositionMeters.Y, DrivingEnvironment.TownMinimumMeters.Y, DrivingEnvironment.TownMaximumMeters.Y);
        });
    }

    [Fact]
    public void ReadyGuardFiresAtNearbyTargetAndResidentsScatter()
    {
        using GameWorld world = new();
        AmbientCharacter guard = Assert.Single(
            world.Environment.Characters,
            character => character.PositionMeters == new Vector2(-26.0f, -212.0f));
        AmbientCharacter resident = world.Environment.Characters
            .Where(character => character.Role == CharacterRole.Resident)
            .MinBy(character => Vector2.Distance(character.PositionMeters, guard.PositionMeters))!;
        guard.Step(1.0f, world.Environment.TownWalls);
        Vector2 target = guard.PositionMeters + Vector2.UnitX * 10.0f;
        guard.UpdateGuardAggro(
            target,
            DrivingEnvironment.GuardEngagementRangeMeters,
            true);

        bool fired = guard.TryFireAt(
            target,
            DrivingEnvironment.GuardEngagementRangeMeters,
            out GuardRound? round);
        resident.ReactToGunfire(guard.PositionMeters, DrivingEnvironment.GunfirePanicRadiusMeters);

        Assert.True(fired);
        Assert.NotNull(round);
        Assert.True(guard.MuzzleFlashAgeSeconds > 0.0f);
        Assert.True(resident.IsPanicking);
        Assert.True(Vector2.Dot(resident.PanicDirection, resident.PositionMeters - guard.PositionMeters) > 0.0f);
    }

    [Fact]
    public void GuardKeepsAggroThroughCoverUntilCarLeavesRange()
    {
        using GameWorld world = new();
        AmbientCharacter guard = world.Environment.Characters
            .First(character => character.Role == CharacterRole.Guard);
        Vector2 nearbyCar = guard.PositionMeters + Vector2.UnitX * 12.0f;

        guard.UpdateGuardAggro(
            nearbyCar,
            DrivingEnvironment.GuardEngagementRangeMeters,
            true);
        Assert.True(guard.IsAggroed);

        guard.UpdateGuardAggro(
            nearbyCar,
            DrivingEnvironment.GuardEngagementRangeMeters,
            false);
        Assert.True(guard.IsAggroed);

        guard.UpdateGuardAggro(
            guard.PositionMeters + Vector2.UnitX * 37.0f,
            DrivingEnvironment.GuardEngagementRangeMeters,
            false);
        Assert.False(guard.IsAggroed);
        Assert.True(guard.IsReturningToPost);
    }

    [Fact]
    public void ExpandedCharacterDeathRadiusKillsAtEighteenMeters()
    {
        using GameWorld world = new();
        AmbientCharacter character = world.Environment.Characters[0];

        world.Environment.Explode(character.PositionMeters - Vector2.UnitX * 18.0f);

        Assert.Equal(20.0f, DrivingEnvironment.CharacterDeadlyBlastRadiusMeters);
        Assert.True(character.IsDead);
    }

    [Fact]
    public void GuardRoundStopsWhenItCrossesRotatedVehicleBody()
    {
        GuardRound round = new(new Vector2(-5.0f, 0.0f), Vector2.UnitX);

        round.Step(
            0.2f,
            true,
            Vector2.Zero,
            MathF.PI * 0.5f,
            new Vector2(2.0f, 4.2f));

        Assert.True(round.HasImpacted);
        Assert.InRange(round.PositionMeters.X, -2.11f, -2.09f);
    }

    [Fact]
    public void SolidObstacleBlocksSightAndStopsRoundBeforeVehicle()
    {
        ShotObstacle blocker = new(Vector2.Zero, new Vector2(2.0f, 6.0f), 0.0f);
        Assert.False(DrivingEnvironment.HasLineOfSight(
            new Vector2(-5.0f, 0.0f),
            new Vector2(5.0f, 0.0f),
            [blocker]));
        Assert.True(DrivingEnvironment.HasLineOfSight(
            new Vector2(-5.0f, 4.0f),
            new Vector2(5.0f, 4.0f),
            [blocker]));

        GuardRound round = new(new Vector2(-5.0f, 0.0f), Vector2.UnitX);
        round.Step(
            0.2f,
            true,
            new Vector2(5.0f, 0.0f),
            0.0f,
            new Vector2(2.0f, 4.2f),
            [blocker]);

        Assert.True(round.HasImpacted);
        Assert.InRange(round.PositionMeters.X, -1.01f, -0.99f);
    }

    [Fact]
    public void GuardFleesClearlyVisibleApproachingCarAtCloseRange()
    {
        using GameWorld world = new();
        AmbientCharacter guard = world.Environment.Characters
            .First(character => character.Role == CharacterRole.Guard);
        Vector2 facing = new(MathF.Cos(guard.FacingRadians), MathF.Sin(guard.FacingRadians));
        Vector2 carPosition = guard.PositionMeters + facing * 6.0f;
        Vector2 carVelocity = -facing * 12.0f;

        Assert.True(DrivingEnvironment.HasLineOfSight(
            guard.PositionMeters,
            carPosition,
            []));
        guard.TryFleeFromVehicle(carPosition, carVelocity, 10.0f);

        Assert.True(guard.IsPanicking);
        Assert.InRange(MathF.Abs(Vector2.Dot(guard.PanicDirection, carVelocity)), 0.0f, 0.0001f);
    }

    [Fact]
    public void GuardsFleeInsideBlastAreaAndInvestigateOutsideIt()
    {
        using GameWorld world = new();
        AmbientCharacter[] guards = world.Environment.Characters
            .Where(character => character.Role == CharacterRole.Guard)
            .Take(2)
            .ToArray();

        guards[0].ReactToExplosion(
            guards[0].PositionMeters - Vector2.UnitX * 20.0f,
            44.0f,
            DrivingEnvironment.BlastRadiusMeters);
        guards[1].ReactToExplosion(
            guards[1].PositionMeters - Vector2.UnitX * 140.0f,
            44.0f,
            DrivingEnvironment.BlastRadiusMeters);

        Assert.True(guards[0].IsPanicking);
        Assert.False(guards[0].IsInvestigating);
        Assert.False(guards[1].IsPanicking);
        Assert.True(guards[1].IsInvestigating);
    }

    [Fact]
    public void InvestigatingGuardKeepsWalkingAroundGroundZeroAfterArrival()
    {
        using GameWorld world = new();
        AmbientCharacter guard = world.Environment.Characters
            .First(character => character.PositionMeters == new Vector2(-26.0f, -212.0f));
        Vector2 blastCenter = guard.PositionMeters - Vector2.UnitX * 34.0f;
        world.Environment.Explode(blastCenter);

        for (int step = 0; step < 480; step++)
        {
            world.Step(default, 1.0f / 60.0f, 4);
        }
        Vector2 firstPatrolPosition = guard.PositionMeters;

        for (int step = 0; step < 120; step++)
        {
            world.Step(default, 1.0f / 60.0f, 4);
        }
        Vector2 secondPatrolPosition = guard.PositionMeters;

        Assert.True(guard.IsInvestigating);
        Assert.InRange(Vector2.Distance(firstPatrolPosition, blastCenter), 3.0f, 10.0f);
        Assert.InRange(Vector2.Distance(secondPatrolPosition, blastCenter), 3.0f, 10.0f);
        Assert.True(Vector2.Distance(firstPatrolPosition, secondPatrolPosition) > 0.5f);
    }
}
