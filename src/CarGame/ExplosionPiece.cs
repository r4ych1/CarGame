using System.Numerics;
using Box2D.NET;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;

namespace CarGame;

internal enum ExplosionPieceKind
{
    BodyPanel,
    Wheel,
    EngineBlock,
    Bumper,
    MiscellaneousDebris,
}

internal sealed class ExplosionPiece
{
    private const float GroundDecelerationMetersPerSecondSquared = 7.36f;
    private const float GroundAngularDecelerationRadiansPerSecondSquared = 12.0f;
    private const float StopSpinningSpeedMetersPerSecond = 1.0f;
    private readonly B2BodyId bodyId;

    public ExplosionPiece(
        B2WorldId worldId,
        ExplosionPieceKind kind,
        Vector2 sizeMeters,
        float massKilograms,
        Vector2 positionMeters,
        float rotationRadians,
        Vector2 linearVelocityMetersPerSecond,
        float angularVelocityRadiansPerSecond,
        Vector2 impulseNewtonSeconds)
    {
        Kind = kind;
        SizeMeters = sizeMeters;
        MassKilograms = massKilograms;

        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_dynamicBody;
        bodyDefinition.position = new B2Vec2(positionMeters.X, positionMeters.Y);
        bodyDefinition.rotation = b2MakeRot(rotationRadians);
        bodyDefinition.enableSleep = false;
        bodyId = b2CreateBody(worldId, bodyDefinition);

        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        shapeDefinition.density = 1.0f;
        B2Polygon shape = b2MakeBox(sizeMeters.X / 2.0f, sizeMeters.Y / 2.0f);
        b2CreatePolygonShape(bodyId, shapeDefinition, shape);

        float inertia = massKilograms *
            ((sizeMeters.X * sizeMeters.X) + (sizeMeters.Y * sizeMeters.Y)) /
            12.0f;
        b2Body_SetMassData(bodyId, new B2MassData(massKilograms, default, inertia));
        b2Body_SetLinearDamping(bodyId, 0.22f);
        b2Body_SetAngularDamping(bodyId, 0.35f);
        b2Body_SetLinearVelocity(
            bodyId,
            new B2Vec2(linearVelocityMetersPerSecond.X, linearVelocityMetersPerSecond.Y));
        b2Body_SetAngularVelocity(bodyId, angularVelocityRadiansPerSecond);
        b2Body_ApplyLinearImpulseToCenter(
            bodyId,
            new B2Vec2(impulseNewtonSeconds.X, impulseNewtonSeconds.Y),
            true);
    }

    public ExplosionPieceKind Kind { get; }

    public Vector2 SizeMeters { get; }

    public float MassKilograms { get; }

    public Vector2 Position => SimulationUnits.ToPixels(b2Body_GetPosition(bodyId));

    public Vector2 PositionMeters
    {
        get
        {
            B2Vec2 position = b2Body_GetPosition(bodyId);
            return new Vector2(position.X, position.Y);
        }
    }

    public Vector2 LinearVelocityMetersPerSecond
    {
        get
        {
            B2Vec2 velocity = b2Body_GetLinearVelocity(bodyId);
            return new Vector2(velocity.X, velocity.Y);
        }
    }

    public float RotationRadians => b2Rot_GetAngle(b2Body_GetRotation(bodyId));

    public float AngularVelocityRadiansPerSecond => b2Body_GetAngularVelocity(bodyId);

    public void ApplyGroundResistance(float fixedTimeStep)
    {
        B2Vec2 velocityValue = b2Body_GetLinearVelocity(bodyId);
        Vector2 velocity = new(velocityValue.X, velocityValue.Y);
        float speed = velocity.Length();
        if (speed <= 0.0f)
        {
            b2Body_SetAngularVelocity(bodyId, 0.0f);
            return;
        }

        float reducedSpeed = MathF.Max(
            0.0f,
            speed - (GroundDecelerationMetersPerSecondSquared * fixedTimeStep));
        Vector2 reducedVelocity = velocity * (reducedSpeed / speed);
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(reducedVelocity.X, reducedVelocity.Y));

        float angularVelocity = b2Body_GetAngularVelocity(bodyId);
        if (reducedSpeed <= StopSpinningSpeedMetersPerSecond)
        {
            b2Body_SetAngularVelocity(bodyId, 0.0f);
            return;
        }

        float reducedAngularSpeed = MathF.Max(
            0.0f,
            MathF.Abs(angularVelocity) - GroundAngularDecelerationRadiansPerSecondSquared * fixedTimeStep);
        b2Body_SetAngularVelocity(bodyId, MathF.CopySign(reducedAngularSpeed, angularVelocity));
    }

    public void Destroy() => b2DestroyBody(bodyId);
}
