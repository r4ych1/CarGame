using System.Numerics;
using Xunit;

namespace CarGame.Tests;

public sealed class DrivingEnvironmentTests
{
    [Fact]
    public void EnvironmentBoundaryHasFinitePlayableBounds()
    {
        Assert.Equal(new Vector2(-220.0f, -380.0f), DrivingEnvironment.BoundaryMinimumMeters);
        Assert.Equal(new Vector2(220.0f, 70.0f), DrivingEnvironment.BoundaryMaximumMeters);
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
        SceneryObject shack = Assert.Single(world.Environment.Scenery, item => item.Kind == SceneryKind.Shack);

        Assert.NotEqual(southBarrier.PositionMeters.X, northBarrier.PositionMeters.X);
        Assert.NotEqual(southBarrier.PositionMeters.Y, northBarrier.PositionMeters.Y);
        Assert.Equal(DrivingEnvironment.CheckpointShackMeters, shack.PositionMeters);
    }

    [Fact]
    public void RoadHasOnlyThreeGuardRailsAndCheckpointApproachCones()
    {
        using GameWorld world = new();
        PhysicsProp[] guardRails = world.Environment.Props.Where(prop => prop.Kind == PhysicsPropKind.GuardRail).ToArray();
        PhysicsProp[] cones = world.Environment.Props.Where(prop => prop.Kind == PhysicsPropKind.TrafficCone).ToArray();

        Assert.Equal(3, guardRails.Length);
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
    public void BlastCreatesDeterministicLayeredVisualEffectThatExpires()
    {
        using GameWorld world = new();
        world.ExplodeVehicle();

        BlastVisualEffect effect = Assert.Single(world.Environment.BlastEffects);
        Assert.Equal(28, effect.Smoke.Count);
        Assert.Equal(18, effect.Fire.Count);
        Assert.Equal(22, effect.GroundFire.Count);
        Assert.Equal(36, effect.Debris.Count);
        Assert.Equal(Vector2.Zero, effect.CenterMeters);

        for (int index = 0; index < 240; index++)
            world.Step(default, 1.0f / 60.0f, 4);

        BlastVisualEffect lingeringEffect = Assert.Single(world.Environment.BlastEffects);
        Assert.Equal(22, lingeringEffect.GroundFire.Count);
        Assert.Contains(lingeringEffect.GroundFire, particle => particle.OffsetMeters.Length() > 20.0f);

        for (int index = 0; index < 481; index++)
            world.Step(default, 1.0f / 60.0f, 4);

        Assert.Empty(world.Environment.BlastEffects);
    }
}
