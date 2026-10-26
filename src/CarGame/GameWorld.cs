using System.Numerics;
using Box2D.NET;
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
    }

    public Vehicle Vehicle { get; }
    public DrivingEnvironment Environment { get; }

    public void Step(VehicleInput input, float fixedTimeStep, int subSteps)
    {
        Vehicle.ApplyControls(input, fixedTimeStep);
        if (Environment.IsDeepWater(Vehicle.PositionMeters))
        {
            Vehicle.Sink();
        }
        Vector2? playerObstacle = Vehicle.IsExploded ? null : Vehicle.PositionMeters;
        Environment.Step(fixedTimeStep, playerObstacle);
        b2World_Step(worldId, fixedTimeStep, subSteps);
        Environment.PostPhysicsStep(Vehicle);
    }

    public void ResetVehicle()
    {
        Vehicle.Reset();
        Environment.Reset();
    }

    public void ExplodeVehicle()
    {
        if (!Vehicle.IsExploded && !Vehicle.IsSunk)
        {
            Environment.Explode(Vehicle.PositionMeters);
            Vehicle.Explode();
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
