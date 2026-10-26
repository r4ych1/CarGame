using System.Numerics;
using Box2D.NET;
using Xunit;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2Worlds;

namespace CarGame.Tests;

public sealed class TrafficSystemTests
{
    [Fact]
    public void FourCarsBeginActiveAtDistinctRouteStages()
    {
        using GameWorld world = new();
        IReadOnlyList<TrafficVehicle> vehicles = world.Environment.Traffic.Vehicles;
        int[] initialStages = vehicles.Select(vehicle => vehicle.RouteIndex).ToArray();

        Assert.Equal(4, vehicles.Count);
        Assert.All(vehicles, vehicle => Assert.True(vehicle.IsActive));
        Assert.Equal(4, vehicles.Select(vehicle => vehicle.RouteIndex).Distinct().Count());
        Assert.All(
            PairwiseDistances(vehicles.Select(vehicle => vehicle.PositionMeters).ToArray()),
            distance => Assert.True(distance >= 65.0f));

        world.ResetVehicle();
        Assert.False(initialStages.SequenceEqual(vehicles.Select(vehicle => vehicle.RouteIndex)));
        Assert.All(
            PairwiseDistances(vehicles.Select(vehicle => vehicle.PositionMeters).ToArray()),
            distance => Assert.True(distance >= 65.0f));
    }

    [Fact]
    public void InitialSpawnStagesRemainClearOfWorldObstacles()
    {
        using GameWorld world = new();
        IReadOnlyList<TrafficRoutePoint> route = world.Environment.Traffic.Route;
        IEnumerable<(Vector2 Position, float Radius)> obstacles = world.Environment.Props
            .Select(prop => (prop.PositionMeters, prop.SizeMeters.Length() * 0.5f))
            .Concat(world.Environment.Scenery.Where(item => item.HasBody)
                .Select(item => (item.PositionMeters, item.SizeMeters.Length() * 0.5f)));
        float trafficRadius = new Vector2(1.8f, 4.2f).Length() * 0.5f;

        foreach (int[] candidates in TrafficSystem.InitialSpawnStageCandidates)
        {
            foreach (int stage in candidates)
            {
                Vector2 spawn = route[stage].PositionMeters;
                Assert.All(obstacles, obstacle => Assert.True(
                    Vector2.Distance(spawn, obstacle.Position) > trafficRadius + obstacle.Radius,
                    $"Route stage {stage} at {spawn} overlaps an obstacle at {obstacle.Position}."));
            }
        }

        IReadOnlyList<int[]> spawnZones = TrafficSystem.InitialSpawnStageCandidates;
        for (int firstZone = 0; firstZone < spawnZones.Count; firstZone++)
        {
            for (int secondZone = firstZone + 1; secondZone < spawnZones.Count; secondZone++)
            {
                foreach (int firstStage in spawnZones[firstZone])
                {
                    foreach (int secondStage in spawnZones[secondZone])
                    {
                        Assert.True(Vector2.Distance(
                            route[firstStage].PositionMeters,
                            route[secondStage].PositionMeters) >= 65.0f);
                    }
                }
            }
        }
    }

    [Fact]
    public void RouteUsesSlowCheckpointAndReturnsThroughDirtBypass()
    {
        using GameWorld world = new();
        IReadOnlyList<TrafficRoutePoint> route = world.Environment.Traffic.Route;
        IReadOnlyList<Vector2> dirt = DrivingEnvironment.DirtRoadPointsMeters;

        Assert.InRange(route[0].PositionMeters.Y, 60.0f, DrivingEnvironment.BoundaryMaximumMeters.Y);
        Assert.InRange(route[^1].PositionMeters.Y, 60.0f, DrivingEnvironment.BoundaryMaximumMeters.Y);
        Assert.Contains(route, point => point.SpeedMetersPerSecond == 3.5f && point.PositionMeters.X < -20.0f);
        Assert.Contains(route, point => point.SpeedMetersPerSecond == 3.5f && point.PositionMeters.X > -14.0f);
        TrafficRoutePoint[] checkpoint = route.Where(point => point.SpeedMetersPerSecond == 3.5f).ToArray();
        Assert.True(checkpoint.First().PositionMeters.Y > DrivingEnvironment.CheckpointSouthBarrierMeters.Y);
        Assert.True(checkpoint.Last().PositionMeters.Y < DrivingEnvironment.CheckpointNorthBarrierMeters.Y);

        int firstDirtIndex = route.ToList().FindIndex(point => point.SpeedMetersPerSecond == 6.0f);
        Assert.Equal(dirt[^3], route[firstDirtIndex].PositionMeters);
        Assert.Equal(dirt[0], route[firstDirtIndex + dirt.Count - 3].PositionMeters);
        Assert.Contains(route, point => point.SpeedMetersPerSecond < 3.5f && point.PositionMeters.Y < -230.0f);
    }

    [Fact]
    public void DriverTraversesCheckpointAndTurnsBeforeTownWallWithoutHittingProps()
    {
        B2WorldDef worldDefinition = b2DefaultWorldDef();
        worldDefinition.gravity = default;
        worldDefinition.enableSleep = false;
        B2WorldId worldId = b2CreateWorld(worldDefinition);

        try
        {
            TrafficRoutePoint[] route = TrafficSystem.CreateRoute();
            int checkpointStart = Array.FindIndex(route, point => point.SpeedMetersPerSecond == 3.5f);
            int dirtStart = Array.FindIndex(route, point => point.SpeedMetersPerSecond == 6.0f);
            TrafficVehicle vehicle = new(worldId, route, new ColorRgb(52, 119, 176));
            PhysicsProp southRail = new(
                worldId,
                PhysicsPropKind.GuardRail,
                DrivingEnvironment.CheckpointSouthBarrierMeters,
                new Vector2(5.0f, 0.45f),
                0.0f);
            PhysicsProp northRail = new(
                worldId,
                PhysicsPropKind.GuardRail,
                DrivingEnvironment.CheckpointNorthBarrierMeters,
                new Vector2(5.0f, 0.45f),
                0.0f);
            _ = new PhysicsProp(
                worldId,
                PhysicsPropKind.GuardRail,
                new Vector2(-32.5f, DrivingEnvironment.TownMaximumMeters.Y),
                new Vector2(31.0f, 2.5f),
                0.0f,
                true);
            _ = new PhysicsProp(
                worldId,
                PhysicsPropKind.GuardRail,
                new Vector2(19.5f, DrivingEnvironment.TownMaximumMeters.Y),
                new Vector2(57.0f, 2.5f),
                0.0f,
                true);
            PhysicsProp[] turnCones =
            [
                new(worldId, PhysicsPropKind.TrafficCone, new Vector2(-26.0f, -230.0f), new Vector2(0.55f), 0.0f),
                new(worldId, PhysicsPropKind.TrafficCone, new Vector2(-6.0f, -230.0f), new Vector2(0.55f), 0.08f),
                new(worldId, PhysicsPropKind.TrafficCone, new Vector2(-27.0f, -240.0f), new Vector2(0.55f), -0.08f),
                new(worldId, PhysicsPropKind.TrafficCone, new Vector2(5.0f, -240.0f), new Vector2(0.55f), 0.0f),
            ];
            Vector2[] originalConePositions = turnCones.Select(cone => cone.PositionMeters).ToArray();
            TrafficObstacle[] turnObstacles = turnCones.Select(cone =>
                new TrafficObstacle(cone.PositionMeters, cone.SizeMeters.X * 0.5f)).ToArray();
            vehicle.PlaceAtRouteIndex(checkpointStart - 1);

            for (int step = 0; step < 2_400 && vehicle.RouteIndex < dirtStart + 2; step++)
            {
                Vector2 avoidance = TrafficSystem.CalculateAvoidance(
                    vehicle.PositionMeters,
                    vehicle.RotationRadians,
                    turnObstacles,
                    1.0f);
                vehicle.Step(1.0f / 60.0f, () => 10.0f, avoidance);
                b2World_Step(worldId, 1.0f / 60.0f, 4);
                vehicle.PostPhysicsStep();
            }

            Assert.True(vehicle.RouteIndex >= dirtStart + 2);
            Assert.True(vehicle.IsActive);
            Assert.False(vehicle.IsDisabled);
            Assert.Equal(TrafficRecoveryState.Driving, vehicle.RecoveryState);
            Assert.Equal(DrivingEnvironment.CheckpointSouthBarrierMeters, southRail.PositionMeters);
            Assert.Equal(DrivingEnvironment.CheckpointNorthBarrierMeters, northRail.PositionMeters);
            Assert.Equal(originalConePositions, turnCones.Select(cone => cone.PositionMeters).ToArray());
        }
        finally
        {
            b2DestroyWorld(worldId);
        }
    }

    [Fact]
    public void CompletedCarWaitsFiveToFifteenSecondsThenRespawnsInbound()
    {
        using GameWorld world = new();
        TrafficSystem traffic = world.Environment.Traffic;
        TrafficVehicle vehicle = traffic.Vehicles[0];
        vehicle.PlaceAtRouteIndex(traffic.Route.Count - 1);

        traffic.Step(1.0f / 60.0f, []);

        Assert.False(vehicle.IsActive);
        Assert.InRange(vehicle.RespawnDelaySeconds, 5.0f, 15.0f);

        for (int index = 0; index < 901 && !vehicle.IsActive; index++)
            traffic.Step(1.0f / 60.0f, []);

        Assert.True(vehicle.IsActive);
        Assert.Equal(0, vehicle.RouteIndex);
    }

    [Fact]
    public void BlastTurnsNearbyTrafficIntoFiniteMovingWreckageAndResetRestoresIt()
    {
        using GameWorld world = new();
        TrafficVehicle vehicle = world.Environment.Traffic.Vehicles[0];

        world.Environment.Explode(vehicle.PositionMeters);

        Assert.True(vehicle.IsWrecked);
        Assert.True(float.IsFinite(vehicle.LinearVelocityMetersPerSecond.X));
        Assert.True(vehicle.LinearVelocityMetersPerSecond.Length() > 20.0f);

        world.ResetVehicle();
        Assert.All(world.Environment.Traffic.Vehicles, resetVehicle =>
        {
            Assert.True(resetVehicle.IsActive);
            Assert.False(resetVehicle.IsWrecked);
            Assert.False(resetVehicle.IsDisabled);
        });
    }

    [Fact]
    public void TrafficBlastSpeedIsStrongestNearGroundZero()
    {
        const float radiusMeters = 32.0f;
        float centerSpeed = BlastPhysics.LaunchSpeed(
            0.0f,
            radiusMeters,
            TrafficVehicle.BlastEdgeSpeedMetersPerSecond,
            TrafficVehicle.BlastCenterSpeedMetersPerSecond);
        float middleSpeed = BlastPhysics.LaunchSpeed(
            radiusMeters * 0.5f,
            radiusMeters,
            TrafficVehicle.BlastEdgeSpeedMetersPerSecond,
            TrafficVehicle.BlastCenterSpeedMetersPerSecond);
        float edgeSpeed = BlastPhysics.LaunchSpeed(
            radiusMeters,
            radiusMeters,
            TrafficVehicle.BlastEdgeSpeedMetersPerSecond,
            TrafficVehicle.BlastCenterSpeedMetersPerSecond);

        Assert.Equal(30.0f, centerSpeed);
        Assert.Equal(6.0f, edgeSpeed);
        Assert.InRange(middleSpeed, edgeSpeed, centerSpeed);
        Assert.True(centerSpeed - middleSpeed > middleSpeed - edgeSpeed);
    }

    [Fact]
    public void FrontalAndSevereHighSpeedImpactsDisableButLightNudgesDoNot()
    {
        Assert.True(TrafficVehicle.IsDisablingImpact(
            new Vector2(0.0f, -10.0f),
            new Vector2(0.0f, -2.0f),
            0.0f));
        Assert.True(TrafficVehicle.IsDisablingImpact(
            new Vector2(0.0f, -12.0f),
            new Vector2(12.0f, -12.0f),
            0.0f));
        Assert.False(TrafficVehicle.IsDisablingImpact(
            new Vector2(0.0f, -9.0f),
            new Vector2(0.0f, -2.0f),
            0.0f));
        Assert.False(TrafficVehicle.IsDisablingImpact(
            new Vector2(0.0f, -9.0f),
            new Vector2(2.0f, -8.5f),
            0.0f));
        Assert.False(TrafficVehicle.IsDisablingImpact(
            new Vector2(0.0f, -4.0f),
            Vector2.Zero,
            0.0f));
        Assert.True(TrafficVehicle.IsNonDisablingCollisionImpact(
            new Vector2(0.0f, -7.0f),
            new Vector2(2.5f, -6.0f),
            0.0f));
        Assert.True(TrafficVehicle.IsNonDisablingCollisionImpact(
            new Vector2(0.0f, -1.0f),
            new Vector2(0.35f, -0.9f),
            0.0f));
        Assert.False(TrafficVehicle.IsNonDisablingCollisionImpact(
            new Vector2(0.0f, -0.4f),
            Vector2.Zero,
            0.0f));
        Assert.True(TrafficVehicle.IsNonDisablingCollisionImpact(
            new Vector2(0.0f, -9.0f),
            new Vector2(0.0f, -2.0f),
            0.0f));
        Assert.Equal(1.4f, TrafficVehicle.ImpactResponseSeconds);
        Assert.Equal(0.55f, TrafficVehicle.UncontrolledImpactSeconds);
        Assert.Equal(
            [TrafficRecoveryState.Driving, TrafficRecoveryState.WaitingAfterImpact],
            Enum.GetValues<TrafficRecoveryState>());
    }

    [Fact]
    public void DriverSteersLaterallyAroundObstacleAhead()
    {
        Vector2 avoidance = TrafficSystem.CalculateAvoidance(
            Vector2.Zero,
            0.0f,
            [new TrafficObstacle(new Vector2(0.0f, -7.0f), 1.5f)],
            1.0f);

        Assert.True(avoidance.X > 0.0f);
        Assert.True(avoidance.Length() > 0.1f);
    }

    private static IEnumerable<float> PairwiseDistances(IReadOnlyList<Vector2> positions)
    {
        for (int first = 0; first < positions.Count; first++)
        {
            for (int second = first + 1; second < positions.Count; second++)
            {
                yield return Vector2.Distance(positions[first], positions[second]);
            }
        }
    }
}
