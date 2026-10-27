using System.Numerics;
using Box2D.NET;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;

namespace CarGame;

internal sealed class Vehicle
{
    private const float ExplosionImpulsePerKilogram = 18.0f;
    internal const float GuardRoundDamage = 8.0f;
    private readonly B2BodyId bodyId;
    private readonly B2WorldId worldId;
    private readonly List<ExplosionPiece> explosionPieces = [];

    public Vehicle(B2WorldId worldId, VehicleDefinition definition)
    {
        definition.Validate();
        Definition = definition;
        this.worldId = worldId;

        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_dynamicBody;
        bodyDefinition.position = new B2Vec2(0.0f, 0.0f);
        bodyDefinition.enableSleep = false;
        bodyId = b2CreateBody(worldId, bodyDefinition);

        ChassisSettings chassis = definition.Chassis;
        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        shapeDefinition.density = 1.0f;
        shapeDefinition.enableHitEvents = true;
        B2Polygon bodyShape = b2MakeBox(chassis.WidthMeters / 2.0f, chassis.LengthMeters / 2.0f);
        b2CreatePolygonShape(bodyId, shapeDefinition, bodyShape);

        float yawInertia = chassis.MassKilograms *
            ((chassis.WidthMeters * chassis.WidthMeters) +
             (chassis.LengthMeters * chassis.LengthMeters)) /
            12.0f;
        B2MassData massData = new(
            chassis.MassKilograms,
            new B2Vec2(0.0f, definition.CenterOfGravityOffsetFromGeometricCenterMeters),
            yawInertia);
        b2Body_SetMassData(bodyId, massData);

        SimulationState = VehicleDynamics.CreateInitialState(definition);
        Diagnostics = VehicleDynamics.CreateInitialDiagnostics(definition);
    }

    public Vector2 Position => SimulationUnits.ToPixels(b2Body_GetPosition(bodyId));

    public Vector2 PositionMeters
    {
        get { B2Vec2 position = b2Body_GetPosition(bodyId); return new(position.X, position.Y); }
    }

    public Vector2 CameraTarget => IsExploded
        ? explosionPieces.Aggregate(Vector2.Zero, (sum, piece) => sum + piece.Position) / explosionPieces.Count
        : Position;

    public float RotationRadians => b2Rot_GetAngle(b2Body_GetRotation(bodyId));

    public float SpeedMetersPerSecond
    {
        get
        {
            B2Vec2 velocity = b2Body_GetLinearVelocity(bodyId);
            return MathF.Sqrt((velocity.X * velocity.X) + (velocity.Y * velocity.Y));
        }
    }

    public Vector2 VelocityMetersPerSecond
    {
        get
        {
            B2Vec2 velocity = b2Body_GetLinearVelocity(bodyId);
            return new(velocity.X, velocity.Y);
        }
    }

    public VehicleDefinition Definition { get; }

    internal B2BodyId BodyId => bodyId;

    public bool IsExploded { get; private set; }

    public bool IsSunk { get; private set; }

    public float DamagePercent { get; private set; }

    public bool IsDisabled => DamagePercent >= 100.0f;

    public IReadOnlyList<ExplosionPiece> ExplosionPieces => explosionPieces;

    public VehicleSimulationState SimulationState { get; private set; }

    public VehicleDiagnostics Diagnostics { get; private set; }

    public float CurrentSteeringAngleRadians =>
        (Diagnostics.Wheels.FrontLeft.SteeringAngleRadians +
         Diagnostics.Wheels.FrontRight.SteeringAngleRadians) * 0.5f;

    public void ApplyControls(VehicleInput input, float fixedTimeStep)
    {
        if (IsExploded)
        {
            foreach (ExplosionPiece piece in explosionPieces)
            {
                piece.ApplyGroundResistance(fixedTimeStep);
            }

            return;
        }

        if (IsSunk)
        {
            b2Body_SetLinearVelocity(bodyId, default);
            b2Body_SetAngularVelocity(bodyId, 0.0f);
            return;
        }

        if (IsDisabled)
        {
            input = default;
        }

        B2Vec2 velocity = b2Body_GetLinearVelocity(bodyId);
        float angularVelocity = b2Body_GetAngularVelocity(bodyId);
        if (ShouldSettleAtRest(input, velocity, angularVelocity))
        {
            SettleAtRest();
            velocity = default;
            angularVelocity = 0.0f;
        }

        ChassisSample sample = new(
            new Vector2(velocity.X, velocity.Y),
            angularVelocity,
            RotationRadians);
        DynamicsStepResult result = VehicleDynamics.Step(
            Definition,
            SimulationState,
            sample,
            input,
            fixedTimeStep);

        ApplyWheelForce(result.WheelForces.FrontLeft);
        ApplyWheelForce(result.WheelForces.FrontRight);
        ApplyWheelForce(result.WheelForces.RearLeft);
        ApplyWheelForce(result.WheelForces.RearRight);
        ApplyRoadResistance(sample.LinearVelocityMetersPerSecond);
        ApplyYawDamping();
        SimulationState = result.State;
        Diagnostics = result.Diagnostics;
    }

    private bool ShouldSettleAtRest(VehicleInput input, B2Vec2 velocity, float angularVelocity)
    {
        float speedSquared = (velocity.X * velocity.X) + (velocity.Y * velocity.Y);
        float restSpeed = Definition.Chassis.RestLinearSpeedMetersPerSecond;
        return input.Throttle == 0.0f &&
            speedSquared <= restSpeed * restSpeed &&
            MathF.Abs(angularVelocity) <= Definition.Chassis.RestAngularSpeedRadiansPerSecond;
    }

    private void SettleAtRest()
    {
        b2Body_SetLinearVelocity(bodyId, default);
        b2Body_SetAngularVelocity(bodyId, 0.0f);
        SimulationState = SimulationState with
        {
            Wheels = new FourWheels<WheelSimulationState>(
                StopWheel(SimulationState.Wheels.FrontLeft),
                StopWheel(SimulationState.Wheels.FrontRight),
                StopWheel(SimulationState.Wheels.RearLeft),
                StopWheel(SimulationState.Wheels.RearRight)),
        };
    }

    private static WheelSimulationState StopWheel(WheelSimulationState wheel) =>
        wheel with { AngularSpeedRadiansPerSecond = 0.0f };

    public void Reset()
    {
        foreach (ExplosionPiece piece in explosionPieces)
        {
            piece.Destroy();
        }

        explosionPieces.Clear();
        IsExploded = false;
        IsSunk = false;
        DamagePercent = 0.0f;
        b2Body_Enable(bodyId);
        b2Body_SetTransform(bodyId, new B2Vec2(0.0f, 0.0f), b2MakeRot(0.0f));
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(0.0f, 0.0f));
        b2Body_SetAngularVelocity(bodyId, 0.0f);
        SimulationState = VehicleDynamics.CreateInitialState(Definition);
        Diagnostics = VehicleDynamics.CreateInitialDiagnostics(Definition);
    }

    public void Explode()
    {
        if (IsExploded || IsSunk)
        {
            return;
        }

        B2Vec2 centerValue = b2Body_GetPosition(bodyId);
        B2Vec2 velocityValue = b2Body_GetLinearVelocity(bodyId);
        Vector2 center = new(centerValue.X, centerValue.Y);
        Vector2 centerVelocity = new(velocityValue.X, velocityValue.Y);
        float rotation = RotationRadians;
        float angularVelocity = b2Body_GetAngularVelocity(bodyId);

        ExplosionPieceDefinition[] pieces = CreateExplosionLayout();
        foreach (ExplosionPieceDefinition piece in pieces)
        {
            Vector2 worldOffset = ToWorldOffset(piece.LocalOffsetMeters, rotation);
            Vector2 radialDirection = Vector2.Normalize(worldOffset);
            Vector2 tangent = new(-radialDirection.Y, radialDirection.X);
            Vector2 pointVelocity = centerVelocity +
                new Vector2(-angularVelocity * worldOffset.Y, angularVelocity * worldOffset.X);
            Vector2 impulse =
                (radialDirection + (tangent * piece.TangentialImpulseScale)) *
                piece.MassKilograms *
                ExplosionImpulsePerKilogram *
                piece.ImpulseScale;

            explosionPieces.Add(new ExplosionPiece(
                worldId,
                piece.Kind,
                piece.SizeMeters,
                piece.MassKilograms,
                center + worldOffset,
                rotation,
                pointVelocity,
                angularVelocity + piece.AddedAngularVelocityRadiansPerSecond,
                impulse));
        }

        IsExploded = true;
        b2Body_Disable(bodyId);
    }

    public void Sink()
    {
        if (IsExploded || IsSunk)
        {
            return;
        }

        IsSunk = true;
        b2Body_SetLinearVelocity(bodyId, default);
        b2Body_SetAngularVelocity(bodyId, 0.0f);
    }

    public void ApplyCollisionDamage(float approachSpeedMetersPerSecond) =>
        ApplyDamage(CalculateCollisionDamage(approachSpeedMetersPerSecond));

    public void ApplyGuardRoundDamage() => ApplyDamage(GuardRoundDamage);

    internal static float CalculateCollisionDamage(float approachSpeedMetersPerSecond)
    {
        if (approachSpeedMetersPerSecond <= 4.0f)
        {
            return 0.0f;
        }

        if (approachSpeedMetersPerSecond <= 10.0f)
        {
            return 8.0f * (approachSpeedMetersPerSecond - 4.0f) / 6.0f;
        }

        if (approachSpeedMetersPerSecond <= 18.0f)
        {
            return 8.0f + 27.0f * (approachSpeedMetersPerSecond - 10.0f) / 8.0f;
        }

        float severeDamage = 35.0f + 35.0f * (approachSpeedMetersPerSecond - 18.0f) / 10.0f;
        return Math.Clamp(severeDamage, 35.0f, 70.0f);
    }

    private void ApplyDamage(float amount)
    {
        if (IsExploded || IsSunk || amount <= 0.0f)
        {
            return;
        }

        DamagePercent = Math.Clamp(DamagePercent + amount, 0.0f, 100.0f);
    }

    private ExplosionPieceDefinition[] CreateExplosionLayout()
    {
        ChassisSettings chassis = Definition.Chassis;
        float bodyPieceMass = chassis.MassKilograms * 0.075f;
        float wheelPieceMass = chassis.MassKilograms * 0.055f;
        float engineMass = chassis.MassKilograms * 0.25f;
        float bumperMass = chassis.MassKilograms * 0.025f;
        float miscellaneousPieceMass = chassis.MassKilograms * 0.005f;
        float bodyWidth = chassis.WidthMeters * 0.46f;
        float bodyLength = chassis.LengthMeters * 0.30f;
        float side = chassis.WidthMeters * 0.27f;
        float end = chassis.LengthMeters * 0.32f;
        float wheelX = chassis.AxleTrackMeters * 0.62f;
        float wheelY = chassis.WheelbaseMeters * 0.50f;

        return
        [
            new(ExplosionPieceKind.BodyPanel, new(-side, -end), new(bodyWidth, bodyLength), bodyPieceMass, 1.0f, -0.20f, -2.8f),
            new(ExplosionPieceKind.BodyPanel, new(side, -end), new(bodyWidth, bodyLength), bodyPieceMass, 1.0f, 0.18f, 2.5f),
            new(ExplosionPieceKind.BodyPanel, new(-side, 0.0f), new(bodyWidth, bodyLength), bodyPieceMass, 1.0f, 0.12f, 3.2f),
            new(ExplosionPieceKind.BodyPanel, new(side, 0.0f), new(bodyWidth, bodyLength), bodyPieceMass, 1.0f, -0.15f, -3.4f),
            new(ExplosionPieceKind.BodyPanel, new(-side, end), new(bodyWidth, bodyLength), bodyPieceMass, 1.0f, 0.22f, 2.2f),
            new(ExplosionPieceKind.BodyPanel, new(side, end), new(bodyWidth, bodyLength), bodyPieceMass, 1.0f, -0.18f, -2.6f),
            new(ExplosionPieceKind.Wheel, new(-wheelX, -wheelY), new(0.34f, 0.78f), wheelPieceMass, 1.0f, -0.25f, -5.0f),
            new(ExplosionPieceKind.Wheel, new(wheelX, -wheelY), new(0.34f, 0.78f), wheelPieceMass, 1.0f, 0.25f, 5.4f),
            new(ExplosionPieceKind.Wheel, new(-wheelX, wheelY), new(0.34f, 0.78f), wheelPieceMass, 1.0f, 0.28f, 4.6f),
            new(ExplosionPieceKind.Wheel, new(wheelX, wheelY), new(0.34f, 0.78f), wheelPieceMass, 1.0f, -0.28f, -5.2f),
            new(ExplosionPieceKind.EngineBlock, new(0.0f, -chassis.LengthMeters * 0.18f), new(1.10f, 0.95f), engineMass, 2.5f, 0.04f, 1.4f),
            new(ExplosionPieceKind.Bumper, new(0.0f, -chassis.LengthMeters * 0.47f), new(chassis.WidthMeters * 0.88f, 0.24f), bumperMass, 1.35f, -0.18f, -4.4f),
            new(ExplosionPieceKind.Bumper, new(0.0f, chassis.LengthMeters * 0.47f), new(chassis.WidthMeters * 0.88f, 0.24f), bumperMass, 1.25f, 0.16f, 4.0f),
            new(ExplosionPieceKind.MiscellaneousDebris, new(-side * 0.55f, -end * 0.62f), new(0.28f, 0.42f), miscellaneousPieceMass, 1.25f, -0.34f, -7.0f),
            new(ExplosionPieceKind.MiscellaneousDebris, new(side * 0.72f, -end * 0.48f), new(0.22f, 0.36f), miscellaneousPieceMass, 1.30f, 0.38f, 7.8f),
            new(ExplosionPieceKind.MiscellaneousDebris, new(-side * 0.82f, -0.12f), new(0.18f, 0.30f), miscellaneousPieceMass, 1.18f, 0.42f, 8.6f),
            new(ExplosionPieceKind.MiscellaneousDebris, new(side * 0.64f, 0.16f), new(0.32f, 0.20f), miscellaneousPieceMass, 1.22f, -0.40f, -8.2f),
            new(ExplosionPieceKind.MiscellaneousDebris, new(-side * 0.68f, end * 0.55f), new(0.24f, 0.34f), miscellaneousPieceMass, 1.28f, -0.30f, -7.5f),
            new(ExplosionPieceKind.MiscellaneousDebris, new(side * 0.78f, end * 0.68f), new(0.20f, 0.27f), miscellaneousPieceMass, 1.20f, 0.36f, 8.0f),
        ];
    }

    private static Vector2 ToWorldOffset(Vector2 localOffset, float rotationRadians) =>
        (VehicleMath.Right(rotationRadians) * localOffset.X) -
        (VehicleMath.Forward(rotationRadians) * localOffset.Y);

    private void ApplyWheelForce(WheelForce force)
    {
        B2Vec2 worldPoint = b2Body_GetWorldPoint(
            bodyId,
            new B2Vec2(force.LocalPointMeters.X, force.LocalPointMeters.Y));
        b2Body_ApplyForce(
            bodyId,
            new B2Vec2(force.WorldForceNewtons.X, force.WorldForceNewtons.Y),
            worldPoint,
            true);
    }

    private void ApplyRoadResistance(Vector2 velocity)
    {
        Vector2 forward = VehicleMath.Forward(RotationRadians);
        float longitudinalSpeed = Vector2.Dot(velocity, forward);
        Vector2 rollingResistance =
            -forward * longitudinalSpeed * Definition.Chassis.RollingResistanceNewtonsPerMeterPerSecond;
        float speed = velocity.Length();
        Vector2 aerodynamicDrag = speed > 0.0f
            ? -velocity * speed * Definition.Chassis.AerodynamicDragNewtonsPerSpeedSquared
            : Vector2.Zero;
        Vector2 resistance = rollingResistance + aerodynamicDrag;
        b2Body_ApplyForceToCenter(bodyId, new B2Vec2(resistance.X, resistance.Y), true);
    }

    private void ApplyYawDamping()
    {
        float angularVelocity = b2Body_GetAngularVelocity(bodyId);
        float inertia = b2Body_GetRotationalInertia(bodyId);
        float torque = -angularVelocity * inertia * Definition.Chassis.YawDampingPerSecond;
        b2Body_ApplyTorque(bodyId, torque, true);
    }
}

internal readonly record struct ExplosionPieceDefinition(
    ExplosionPieceKind Kind,
    Vector2 LocalOffsetMeters,
    Vector2 SizeMeters,
    float MassKilograms,
    float ImpulseScale,
    float TangentialImpulseScale,
    float AddedAngularVelocityRadiansPerSecond);
