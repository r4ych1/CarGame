using Box2D.NET;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2Worlds;

namespace CarGame;

internal sealed class GameWorld : IDisposable
{
    private readonly B2WorldId worldId;
    private bool disposed;

    public GameWorld()
    {
        B2WorldDef worldDefinition = b2DefaultWorldDef();
        worldDefinition.gravity = new B2Vec2(0.0f, 0.0f);
        worldDefinition.enableSleep = false;
        worldId = b2CreateWorld(worldDefinition);
        Vehicle = new Vehicle(worldId, VehicleCatalog.FirstCar);
        Environment = new DrivingEnvironment(worldId);
        RunStatistics = new RunStatistics();
    }

    public Vehicle Vehicle { get; }
    public DrivingEnvironment Environment { get; }
    public RunStatistics RunStatistics { get; }

    public void Step(VehicleInput input, float fixedTimeStep, int subSteps)
    {
        Vehicle.ApplyControls(input, fixedTimeStep);
        if (Environment.IsDeepWater(Vehicle.PositionMeters))
        {
            Vehicle.Sink();
        }
        Environment.Step(fixedTimeStep, Vehicle);
        b2World_Step(worldId, fixedTimeStep, subSteps);
        ApplyVehicleCollisionDamage();
        ScoreAwards collisionAwards = Environment.PostPhysicsStep(Vehicle);
        RunStatistics.Award(collisionAwards);
        RunStatistics.Step(fixedTimeStep, Vehicle);
    }

    public void ResetVehicle()
    {
        Vehicle.Reset();
        Environment.Reset();
        RunStatistics.Reset();
    }

    public void ExplodeVehicle()
    {
        if (!Vehicle.IsExploded && !Vehicle.IsSunk && !Vehicle.IsDisabled)
        {
            ScoreAwards awards = Environment.Explode(Vehicle.PositionMeters);
            RunStatistics.Award(awards);
            Vehicle.Explode();
            RunStatistics.Finish(GameOverReason.Exploded, Vehicle);
        }
    }

    private void ApplyVehicleCollisionDamage()
    {
        if (Vehicle.IsExploded || Vehicle.IsSunk)
        {
            return;
        }

        B2ContactEvents contactEvents = b2World_GetContactEvents(worldId);
        for (int index = 0; index < contactEvents.hitCount; index++)
        {
            B2ContactHitEvent hit = contactEvents.hitEvents[index];
            B2BodyId bodyA = b2Shape_GetBody(hit.shapeIdA);
            B2BodyId bodyB = b2Shape_GetBody(hit.shapeIdB);
            B2BodyId otherBody;
            if (Vehicle.BodyId == bodyA)
            {
                otherBody = bodyB;
            }
            else if (Vehicle.BodyId == bodyB)
            {
                otherBody = bodyA;
            }
            else
            {
                continue;
            }

            if (Environment.CausesPlayerCollisionDamage(otherBody))
            {
                Vehicle.ApplyCollisionDamage(hit.approachSpeed);
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        b2DestroyWorld(worldId);
        disposed = true;
    }
}
