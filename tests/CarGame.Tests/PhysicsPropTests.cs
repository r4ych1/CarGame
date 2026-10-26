using System.Numerics;
using Xunit;

namespace CarGame.Tests;

public sealed class PhysicsPropTests
{
    [Fact]
    public void EnvironmentContainsNumerousPropsOfEveryKind()
    {
        using GameWorld world = new();

        Assert.True(world.Environment.Props.Count >= 50);
        foreach (PhysicsPropKind kind in Enum.GetValues<PhysicsPropKind>())
            Assert.Contains(world.Environment.Props, prop => prop.Kind == kind);
    }

    [Fact]
    public void BlastMovesNearbyPropsOutwardButNotDistantProps()
    {
        using GameWorld world = new();
        PhysicsProp near = world.Environment.Props.OrderBy(prop => prop.PositionMeters.Length()).First();
        PhysicsProp far = world.Environment.Props.OrderByDescending(prop => prop.PositionMeters.Length()).First();
        Vector2 farVelocityBefore = far.LinearVelocityMetersPerSecond;

        world.ExplodeVehicle();

        Vector2 direction = Vector2.Normalize(near.PositionMeters);
        Assert.True(Vector2.Dot(near.LinearVelocityMetersPerSecond, direction) > 20.0f);
        Assert.Equal(farVelocityBefore, far.LinearVelocityMetersPerSecond);
    }

    [Fact]
    public void ResetRestoresPropLayoutAndStopsMotion()
    {
        using GameWorld world = new();
        Vector2 originalPosition = world.Environment.Props[0].PositionMeters;
        world.ExplodeVehicle();
        world.Step(default, 1.0f / 60.0f, 4);

        world.ResetVehicle();

        Assert.Equal(59, world.Environment.Props.Count);
        Assert.Equal(originalPosition, world.Environment.Props[0].PositionMeters);
        Assert.Equal(Vector2.Zero, world.Environment.Props[0].LinearVelocityMetersPerSecond);
    }

    [Fact]
    public void GuardRailYawIsLimitedAfterDirectBlast()
    {
        using GameWorld world = new();
        PhysicsProp guardRail = world.Environment.Props.First(prop => prop.Kind == PhysicsPropKind.GuardRail);

        world.Environment.Explode(guardRail.PositionMeters);
        world.Step(default, 1.0f / 60.0f, 4);

        Assert.InRange(guardRail.AngularVelocityRadiansPerSecond, -0.65f, 0.65f);
        Assert.True(guardRail.LinearVelocityMetersPerSecond.Length() > 20.0f);
    }

    [Fact]
    public void TrafficConesAreTheLightestMovableProps()
    {
        using GameWorld world = new();
        PhysicsProp cone = world.Environment.Props.First(prop => prop.Kind == PhysicsPropKind.TrafficCone);
        PhysicsProp[] otherMovableProps = world.Environment.Props.Where(prop =>
            !prop.IsAnchored && prop.Kind != PhysicsPropKind.TrafficCone).ToArray();

        Assert.NotEmpty(otherMovableProps);
        Assert.All(otherMovableProps, prop => Assert.True(cone.MassKilograms < prop.MassKilograms));
    }

    [Fact]
    public void MovableCheckpointGuardRailRespondsToBlast()
    {
        using GameWorld world = new();
        PhysicsProp guardRail = world.Environment.Props.Single(prop =>
            prop.PositionMeters == DrivingEnvironment.CheckpointSouthBarrierMeters);
        Vector2 originalPosition = guardRail.PositionMeters;

        world.Environment.Explode(originalPosition);
        world.Step(default, 1.0f / 60.0f, 4);

        Assert.False(guardRail.IsAnchored);
        Assert.NotEqual(originalPosition, guardRail.PositionMeters);
        Assert.NotEqual(Vector2.Zero, guardRail.LinearVelocityMetersPerSecond);
    }
}
