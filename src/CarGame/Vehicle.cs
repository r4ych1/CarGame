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
    private readonly B2BodyId bodyId;

    public Vehicle(B2WorldId worldId, VehicleDefinition definition)
    {
        definition.Validate();
        Definition = definition;

        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_dynamicBody;
        bodyDefinition.position = new B2Vec2(0.0f, 0.0f);
        bodyDefinition.enableSleep = false;
        bodyId = b2CreateBody(worldId, bodyDefinition);

        ChassisSettings chassis = definition.Chassis;
        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        shapeDefinition.density = 1.0f;
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

    public float RotationRadians => b2Rot_GetAngle(b2Body_GetRotation(bodyId));

    public float SpeedMetersPerSecond
    {
        get
        {
            B2Vec2 velocity = b2Body_GetLinearVelocity(bodyId);
            return MathF.Sqrt((velocity.X * velocity.X) + (velocity.Y * velocity.Y));
        }
    }

    public VehicleDefinition Definition { get; }

    public VehicleSimulationState SimulationState { get; private set; }

    public VehicleDiagnostics Diagnostics { get; private set; }

    public float CurrentSteeringAngleRadians =>
        (Diagnostics.Wheels.FrontLeft.SteeringAngleRadians +
         Diagnostics.Wheels.FrontRight.SteeringAngleRadians) * 0.5f;

    public void ApplyControls(VehicleInput input, float fixedTimeStep)
    {
        B2Vec2 velocity = b2Body_GetLinearVelocity(bodyId);
        ChassisSample sample = new(
            new Vector2(velocity.X, velocity.Y),
            b2Body_GetAngularVelocity(bodyId),
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

    public void Reset()
    {
        b2Body_SetTransform(bodyId, new B2Vec2(0.0f, 0.0f), b2MakeRot(0.0f));
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(0.0f, 0.0f));
        b2Body_SetAngularVelocity(bodyId, 0.0f);
        SimulationState = VehicleDynamics.CreateInitialState(Definition);
        Diagnostics = VehicleDynamics.CreateInitialDiagnostics(Definition);
    }

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
