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
    }

    public Vehicle Vehicle { get; }

    public void Step(VehicleInput input, float fixedTimeStep, int subSteps)
    {
        Vehicle.ApplyControls(input, fixedTimeStep);
        b2World_Step(worldId, fixedTimeStep, subSteps);
    }

    public void ResetVehicle()
    {
        Vehicle.Reset();
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
