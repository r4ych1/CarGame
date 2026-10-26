using System.Numerics;
using Box2D.NET;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;

namespace CarGame;

internal readonly record struct TrafficRoutePoint(Vector2 PositionMeters, float SpeedMetersPerSecond);
internal readonly record struct TrafficObstacle(Vector2 PositionMeters, float RadiusMeters);
internal enum TrafficRecoveryState { Driving, WaitingAfterImpact }

internal sealed class TrafficVehicle
{
    internal const float ImpactResponseSeconds = 1.4f;
    internal const float UncontrolledImpactSeconds = 0.55f;
    internal const float BlastEdgeSpeedMetersPerSecond = 6.0f;
    internal const float BlastCenterSpeedMetersPerSecond = 30.0f;
    internal const float BlastDisabledGroundDecelerationMetersPerSecondSquared = 2.5f;
    private readonly B2BodyId bodyId;
    private readonly IReadOnlyList<TrafficRoutePoint> route;
    private int routeIndex;
    private float respawnDelaySeconds;
    private Vector2 velocityBeforePhysics;
    private float rotationBeforePhysics;
    private float recoveryTimerSeconds;
    private bool rollsWhileDisabled;

    public TrafficVehicle(B2WorldId worldId, IReadOnlyList<TrafficRoutePoint> route, ColorRgb color)
    {
        this.route = route;
        Color = color;
        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_dynamicBody;
        bodyDefinition.enableSleep = false;
        bodyId = b2CreateBody(worldId, bodyDefinition);
        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        shapeDefinition.density = 155.0f;
        shapeDefinition.material.friction = 0.75f;
        shapeDefinition.material.restitution = 0.08f;
        b2CreatePolygonShape(bodyId, shapeDefinition, b2MakeBox(0.9f, 2.1f));
        b2Body_SetLinearDamping(bodyId, 0.2f);
        b2Body_SetAngularDamping(bodyId, 2.5f);
    }

    public ColorRgb Color { get; }
    public bool IsActive { get; private set; }
    public bool IsWrecked { get; private set; }
    public bool IsDisabled { get; private set; }
    public int RouteIndex => routeIndex;
    public float RespawnDelaySeconds => respawnDelaySeconds;
    public float DamageEffectAgeSeconds { get; private set; }
    public Vector2 SizeMeters => new(1.8f, 4.2f);
    public Vector2 PositionMeters { get { B2Vec2 p = b2Body_GetPosition(bodyId); return new(p.X, p.Y); } }
    public Vector2 LinearVelocityMetersPerSecond { get { B2Vec2 v = b2Body_GetLinearVelocity(bodyId); return new(v.X, v.Y); } }
    public float RotationRadians => b2Rot_GetAngle(b2Body_GetRotation(bodyId));
    public TrafficRecoveryState RecoveryState { get; private set; }

    public void PlaceAtRouteIndex(int index)
    {
        routeIndex = Math.Clamp(index, 0, route.Count - 1);
        IsActive = true;
        IsWrecked = false;
        IsDisabled = false;
        rollsWhileDisabled = false;
        respawnDelaySeconds = 0.0f;
        DamageEffectAgeSeconds = 0.0f;
        RecoveryState = TrafficRecoveryState.Driving;
        recoveryTimerSeconds = 0.0f;
        b2Body_Enable(bodyId);
        SetPose(routeIndex);
    }

    public void Step(float timeStep, Func<float> nextRespawnDelay, Vector2 avoidance)
    {
        if (!IsActive)
        {
            respawnDelaySeconds -= timeStep;
            if (respawnDelaySeconds <= 0.0f)
            {
                PlaceAtRouteIndex(0);
            }
            return;
        }

        if (IsWrecked)
        {
            DamageEffectAgeSeconds += timeStep;
            ApplyWreckGroundResistance(timeStep);
            return;
        }

        if (IsDisabled)
        {
            DamageEffectAgeSeconds += timeStep;
            if (rollsWhileDisabled)
            {
                ApplyGroundResistance(
                    timeStep,
                    BlastDisabledGroundDecelerationMetersPerSecondSquared);
            }
            else
            {
                b2Body_SetLinearVelocity(bodyId, default);
                b2Body_SetAngularVelocity(bodyId, 0.0f);
            }
            return;
        }

        if (RecoveryState == TrafficRecoveryState.WaitingAfterImpact)
        {
            recoveryTimerSeconds -= timeStep;
            if (recoveryTimerSeconds <= ImpactResponseSeconds - UncontrolledImpactSeconds)
            {
                Vector2 impactVelocity = LinearVelocityMetersPerSecond;
                float impactSpeed = impactVelocity.Length();
                if (impactSpeed > 0.0f)
                {
                    float brakingAcceleration = MathF.Min(6.0f, impactSpeed / MathF.Max(timeStep, 0.0001f));
                    Vector2 brakingForce = -Vector2.Normalize(impactVelocity) *
                        brakingAcceleration *
                        b2Body_GetMass(bodyId);
                    b2Body_ApplyForceToCenter(bodyId, new B2Vec2(brakingForce.X, brakingForce.Y), true);
                }
                float brakingTorque = -b2Body_GetAngularVelocity(bodyId) *
                    b2Body_GetRotationalInertia(bodyId) *
                    4.0f;
                b2Body_ApplyTorque(bodyId, brakingTorque, true);
            }
            if (recoveryTimerSeconds <= 0.0f)
            {
                b2Body_SetLinearVelocity(bodyId, default);
                b2Body_SetAngularVelocity(bodyId, 0.0f);
                RecoveryState = TrafficRecoveryState.Driving;
                velocityBeforePhysics = LinearVelocityMetersPerSecond;
                rotationBeforePhysics = RotationRadians;
            }
            return;
        }

        Vector2 position = PositionMeters;
        TrafficRoutePoint target = route[routeIndex];
        Vector2 offset = target.PositionMeters - position;
        if (offset.LengthSquared() < 4.0f)
        {
            routeIndex++;
            if (routeIndex >= route.Count)
            {
                IsActive = false;
                b2Body_Disable(bodyId);
                respawnDelaySeconds = nextRespawnDelay();
                return;
            }
            target = route[routeIndex];
            offset = target.PositionMeters - position;
        }

        Vector2 direction = offset.LengthSquared() > 0.0001f ? Vector2.Normalize(offset) : -Vector2.UnitY;
        float avoidanceStrength = Math.Clamp(avoidance.Length(), 0.0f, 1.0f);
        if (avoidanceStrength > 0.0f)
            direction = Vector2.Normalize(direction + avoidance * 1.35f);
        float desiredHeading = MathF.Atan2(direction.X, -direction.Y);
        float headingError = NormalizeAngle(desiredHeading - RotationRadians);
        float obstacleSpeedScale = 1.0f - avoidanceStrength * 0.42f;
        Vector2 desiredVelocity = direction * target.SpeedMetersPerSecond * obstacleSpeedScale;
        Vector2 currentVelocity = LinearVelocityMetersPerSecond;
        velocityBeforePhysics = currentVelocity;
        rotationBeforePhysics = RotationRadians;

        float mass = b2Body_GetMass(bodyId);
        Vector2 acceleration = (desiredVelocity - currentVelocity) * 1.15f;
        if (acceleration.LengthSquared() > 3.2f * 3.2f)
            acceleration = Vector2.Normalize(acceleration) * 3.2f;
        Vector2 force = acceleration * mass;
        b2Body_ApplyForceToCenter(bodyId, new B2Vec2(force.X, force.Y), true);

        float inertia = b2Body_GetRotationalInertia(bodyId);
        float angularVelocity = b2Body_GetAngularVelocity(bodyId);
        float angularAcceleration = Math.Clamp((headingError * 3.0f) - (angularVelocity * 1.8f), -3.5f, 3.5f);
        b2Body_ApplyTorque(bodyId, inertia * angularAcceleration, true);
    }

    public void PostPhysicsStep()
    {
        if (!IsActive || IsWrecked || IsDisabled || RecoveryState != TrafficRecoveryState.Driving) return;
        Vector2 velocityAfter = LinearVelocityMetersPerSecond;
        if (IsDisablingImpact(velocityBeforePhysics, velocityAfter, rotationBeforePhysics))
        {
            IsDisabled = true;
            rollsWhileDisabled = false;
            DamageEffectAgeSeconds = 0.0f;
            RecoveryState = TrafficRecoveryState.Driving;
            b2Body_SetLinearVelocity(bodyId, default);
            b2Body_SetAngularVelocity(bodyId, 0.0f);
            return;
        }

        if (RecoveryState == TrafficRecoveryState.Driving &&
            IsNonDisablingCollisionImpact(velocityBeforePhysics, velocityAfter, rotationBeforePhysics))
        {
            RecoveryState = TrafficRecoveryState.WaitingAfterImpact;
            recoveryTimerSeconds = ImpactResponseSeconds;
        }
    }

    internal static bool IsDisablingImpact(Vector2 velocityBefore, Vector2 velocityAfter, float headingRadians)
    {
        float speedBefore = velocityBefore.Length();
        if (speedBefore < 6.5f) return false;
        Vector2 forward = VehicleMath.Forward(headingRadians);
        float forwardVelocityLoss = Vector2.Dot(velocityBefore - velocityAfter, forward);
        float velocityChange = (velocityAfter - velocityBefore).Length();
        return forwardVelocityLoss >= 7.5f || (speedBefore >= 10.0f && velocityChange >= 11.0f);
    }

    internal static bool IsNonDisablingCollisionImpact(Vector2 velocityBefore, Vector2 velocityAfter, float headingRadians)
    {
        if (IsDisablingImpact(velocityBefore, velocityAfter, headingRadians)) return false;
        return velocityBefore.Length() >= 0.5f && (velocityAfter - velocityBefore).Length() >= 0.3f;
    }

    public void ApplyBlast(Vector2 center, float radius, float deadlyRadius)
    {
        if (!IsActive || IsWrecked) return;
        Vector2 offset = PositionMeters - center;
        float distance = offset.Length();
        if (distance > radius) return;
        DamageEffectAgeSeconds = 0.0f;
        RecoveryState = TrafficRecoveryState.Driving;
        if (distance > deadlyRadius)
        {
            IsWrecked = false;
            IsDisabled = true;
            rollsWhileDisabled = true;
            return;
        }

        Vector2 direction = distance > 0.01f ? offset / distance : Vector2.UnitX;
        float deltaSpeed = BlastPhysics.LaunchSpeed(
            distance,
            radius,
            BlastEdgeSpeedMetersPerSecond,
            BlastCenterSpeedMetersPerSecond);
        float mass = b2Body_GetMass(bodyId);
        IsWrecked = true;
        IsDisabled = false;
        rollsWhileDisabled = false;
        b2Body_ApplyLinearImpulseToCenter(bodyId, new B2Vec2(direction.X * mass * deltaSpeed, direction.Y * mass * deltaSpeed), true);
        b2Body_ApplyAngularImpulse(bodyId, mass * 0.7f, true);
    }

    private void ApplyWreckGroundResistance(float timeStep)
    {
        ApplyGroundResistance(timeStep, 6.0f);
    }

    private void ApplyGroundResistance(float timeStep, float decelerationMetersPerSecondSquared)
    {
        Vector2 velocity = LinearVelocityMetersPerSecond;
        float speed = velocity.Length();
        if (speed <= 0.0f) return;
        float nextSpeed = MathF.Max(0.0f, speed - decelerationMetersPerSecondSquared * timeStep);
        velocity *= nextSpeed / speed;
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(velocity.X, velocity.Y));
    }

    private void SetPose(int index)
    {
        TrafficRoutePoint point = route[index];
        TrafficRoutePoint next = route[Math.Min(index + 1, route.Count - 1)];
        Vector2 direction = next.PositionMeters - point.PositionMeters;
        if (direction.LengthSquared() < 0.0001f) direction = -Vector2.UnitY;
        direction = Vector2.Normalize(direction);
        float heading = MathF.Atan2(direction.X, -direction.Y);
        b2Body_SetTransform(bodyId, new B2Vec2(point.PositionMeters.X, point.PositionMeters.Y), b2MakeRot(heading));
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(direction.X * point.SpeedMetersPerSecond, direction.Y * point.SpeedMetersPerSecond));
        b2Body_SetAngularVelocity(bodyId, 0.0f);
        velocityBeforePhysics = direction * point.SpeedMetersPerSecond;
        rotationBeforePhysics = heading;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle < -MathF.PI) angle += MathF.Tau;
        return angle;
    }
}

internal readonly record struct ColorRgb(byte Red, byte Green, byte Blue);

internal sealed class TrafficSystem
{
    private const uint InitialRandomState = 0xC0FFEEu;
    private static readonly int[][] initialSpawnStageCandidates =
    [
        [0, 1],
        [8, 9],
        [39, 40],
        [46, 49],
    ];
    private readonly List<TrafficVehicle> vehicles = [];
    private uint randomState = InitialRandomState;

    public TrafficSystem(B2WorldId worldId)
    {
        Route = CreateRoute();
        vehicles.Add(new(worldId, Route, new(52, 119, 176)));
        vehicles.Add(new(worldId, Route, new(232, 151, 54)));
        vehicles.Add(new(worldId, Route, new(122, 172, 91)));
        vehicles.Add(new(worldId, Route, new(154, 94, 168)));
        Reset();
    }

    public IReadOnlyList<TrafficVehicle> Vehicles => vehicles;
    public IReadOnlyList<TrafficRoutePoint> Route { get; }
    internal static IReadOnlyList<int[]> InitialSpawnStageCandidates => initialSpawnStageCandidates;

    public void Step(float timeStep, IReadOnlyList<TrafficObstacle> worldObstacles)
    {
        for (int index = 0; index < vehicles.Count; index++)
        {
            TrafficVehicle vehicle = vehicles[index];
            Vector2 avoidance = vehicle.IsActive && !vehicle.IsDisabled && !vehicle.IsWrecked
                ? ComputeAvoidance(vehicle, index, worldObstacles)
                : Vector2.Zero;
            vehicle.Step(timeStep, NextRespawnDelay, avoidance);
        }
    }

    private Vector2 ComputeAvoidance(
        TrafficVehicle vehicle,
        int vehicleIndex,
        IReadOnlyList<TrafficObstacle> worldObstacles)
    {
        List<TrafficObstacle> obstacles = new(worldObstacles.Count + vehicles.Count - 1);
        obstacles.AddRange(worldObstacles);
        for (int index = 0; index < vehicles.Count; index++)
        {
            if (index == vehicleIndex || !vehicles[index].IsActive) continue;
            obstacles.Add(new(vehicles[index].PositionMeters, 1.5f));
        }
        return CalculateAvoidance(vehicle.PositionMeters, vehicle.RotationRadians, obstacles, vehicleIndex % 2 == 0 ? 1.0f : -1.0f);
    }

    internal static Vector2 CalculateAvoidance(
        Vector2 position,
        float rotationRadians,
        IReadOnlyList<TrafficObstacle> obstacles,
        float preferredSide)
    {
        Vector2 forward = VehicleMath.Forward(rotationRadians);
        Vector2 right = VehicleMath.Right(rotationRadians);
        float strongest = 0.0f;
        float selectedSide = preferredSide;
        foreach (TrafficObstacle obstacle in obstacles)
        {
            Vector2 relative = obstacle.PositionMeters - position;
            float forwardDistance = Vector2.Dot(relative, forward);
            float lateralDistance = Vector2.Dot(relative, right);
            float corridorHalfWidth = 1.4f + obstacle.RadiusMeters;
            if (forwardDistance <= 0.0f || forwardDistance >= 14.0f || MathF.Abs(lateralDistance) >= corridorHalfWidth)
                continue;

            float strength = (1.0f - forwardDistance / 14.0f) *
                (1.0f - MathF.Abs(lateralDistance) / corridorHalfWidth);
            if (strength <= strongest) continue;
            strongest = strength;
            selectedSide = MathF.Abs(lateralDistance) > 0.15f ? -MathF.Sign(lateralDistance) : preferredSide;
        }
        return right * selectedSide * strongest;
    }

    public void ApplyBlast(Vector2 center, float radius, float deadlyRadius)
    {
        foreach (TrafficVehicle vehicle in vehicles) vehicle.ApplyBlast(center, radius, deadlyRadius);
    }

    public void PostPhysicsStep()
    {
        foreach (TrafficVehicle vehicle in vehicles) vehicle.PostPhysicsStep();
    }

    public void Reset()
    {
        for (int index = 0; index < vehicles.Count; index++)
        {
            int[] candidates = initialSpawnStageCandidates[index];
            int stage = candidates[(int)(NextRandom01() * candidates.Length)];
            vehicles[index].PlaceAtRouteIndex(stage);
        }
    }

    private float NextRespawnDelay() => 5.0f + NextRandom01() * 10.0f;

    private float NextRandom01()
    {
        randomState = randomState * 1_664_525u + 1_013_904_223u;
        return (randomState & 0x00FFFFFFu) / 16_777_216.0f;
    }

    internal static TrafficRoutePoint[] CreateRoute()
    {
        List<TrafficRoutePoint> route = [];
        for (float y = 64.0f; y >= -198.0f; y -= 10.0f)
            route.Add(new(new Vector2(DrivingEnvironment.RoadCenterXMeters(y) + 2.0f, y), 9.0f));

        route.Add(new(new Vector2(-12.5f, -203.0f), 3.5f));
        route.Add(new(new Vector2(-12.5f, -212.0f), 3.5f));
        route.Add(new(new Vector2(-17.25f, -219.0f), 3.5f));
        route.Add(new(new Vector2(-22.0f, -226.0f), 3.5f));
        route.Add(new(new Vector2(-22.0f, -233.0f), 3.5f));
        route.Add(new(new Vector2(-19.0f, -235.0f), 2.8f));
        route.Add(new(new Vector2(-14.0f, -236.0f), 2.8f));
        route.Add(new(new Vector2(-8.0f, -236.0f), 2.8f));
        route.Add(new(new Vector2(1.0f, -235.0f), 3.2f));
        route.Add(new(new Vector2(10.0f, -234.0f), 3.5f));

        IReadOnlyList<Vector2> dirt = DrivingEnvironment.DirtRoadPointsMeters;
        for (int index = dirt.Count - 3; index >= 0; index--)
            route.Add(new(dirt[index], 6.0f));

        for (float y = -135.0f; y <= 64.0f; y += 10.0f)
            route.Add(new(new Vector2(DrivingEnvironment.RoadCenterXMeters(y) - 2.0f, y), 9.0f));
        route.Add(new(new Vector2(DrivingEnvironment.RoadCenterXMeters(64.0f) - 2.0f, 64.0f), 9.0f));
        return route.ToArray();
    }
}
