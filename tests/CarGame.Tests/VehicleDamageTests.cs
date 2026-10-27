using Box2D.NET;
using Xunit;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2MathFunction;

namespace CarGame.Tests;

public sealed class VehicleDamageTests
{
    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(4.0f, 0.0f)]
    [InlineData(10.0f, 8.0f)]
    [InlineData(18.0f, 35.0f)]
    [InlineData(28.0f, 70.0f)]
    [InlineData(40.0f, 70.0f)]
    public void CollisionDamageScalesFromSafeBumpsToSevereImpacts(float speed, float expectedDamage)
    {
        Assert.Equal(expectedDamage, Vehicle.CalculateCollisionDamage(speed), 0.001f);
    }

    [Fact]
    public void OnlyCarsAndIntactStructuresAreCollisionDamageSources()
    {
        using GameWorld world = new();

        Assert.All(
            world.Environment.Scenery.Where(item => item.Kind is
                SceneryKind.Building or SceneryKind.Shack or SceneryKind.ParkedCar or SceneryKind.ParkedTruck),
            item => Assert.True(world.Environment.CausesPlayerCollisionDamage(item.BodyId)));
        Assert.All(
            world.Environment.Props,
            prop => Assert.False(world.Environment.CausesPlayerCollisionDamage(prop.BodyId)));
        Assert.All(
            world.Environment.Traffic.Vehicles,
            vehicle => Assert.True(world.Environment.CausesPlayerCollisionDamage(vehicle.BodyId)));

        SceneryObject building = world.Environment.Scenery.First(item => item.Kind == SceneryKind.Building);
        world.Environment.Explode(building.PositionMeters);

        Assert.False(building.HasBody);
        Assert.False(world.Environment.CausesPlayerCollisionDamage(building.BodyId));
    }

    [Fact]
    public void MaximumDamageDisablesDriveButStillAllowsDetonationAndReset()
    {
        using GameWorld world = new();
        world.Vehicle.ApplyCollisionDamage(28.0f);
        world.Vehicle.ApplyCollisionDamage(28.0f);

        Assert.Equal(100.0f, world.Vehicle.DamagePercent);
        Assert.True(world.Vehicle.IsDisabled);

        for (int index = 0; index < 120; index++)
        {
            world.Step(new VehicleInput(1.0f, 1.0f), 1.0f / 60.0f, 4);
        }

        Assert.Equal(0.0f, world.Vehicle.SpeedMetersPerSecond);
        world.ExplodeVehicle();
        Assert.True(world.Vehicle.IsExploded);

        world.ResetVehicle();
        Assert.Equal(0.0f, world.Vehicle.DamagePercent);
        Assert.False(world.Vehicle.IsDisabled);
    }

    [Fact]
    public void PhysicsHitAgainstBuildingAppliesCollisionDamage()
    {
        using GameWorld world = new();
        SceneryObject building = world.Environment.Scenery.First(item =>
            item.Kind == SceneryKind.Building && item.PositionMeters == new System.Numerics.Vector2(-32.0f, -105.0f));
        float startingY = building.PositionMeters.Y + building.SizeMeters.Y * 0.5f +
            world.Vehicle.Definition.Chassis.LengthMeters * 0.5f + 0.5f;
        b2Body_SetTransform(
            world.Vehicle.BodyId,
            new B2Vec2(building.PositionMeters.X, startingY),
            b2MakeRot(0.0f));
        b2Body_SetLinearVelocity(world.Vehicle.BodyId, new B2Vec2(0.0f, -12.0f));

        for (int index = 0; index < 20 && world.Vehicle.DamagePercent == 0.0f; index++)
        {
            world.Step(default, 1.0f / 60.0f, 4);
        }

        Assert.True(world.Vehicle.DamagePercent > 0.0f);
    }

    [Fact]
    public void GuardRoundsApplyFixedDamage()
    {
        using GameWorld world = new();

        world.Vehicle.ApplyGuardRoundDamage();
        world.Vehicle.ApplyGuardRoundDamage();

        Assert.Equal(16.0f, world.Vehicle.DamagePercent);
    }
}
