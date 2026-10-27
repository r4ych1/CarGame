using System.Numerics;
using Xunit;

namespace CarGame.Tests;

public sealed class CameraZoomTests
{
    [Fact]
    public void MouseWheelZoomsInAndOut()
    {
        GameRenderer renderer = new(1280, 720);

        renderer.UpdateZoom(1.0f);
        Assert.True(renderer.Zoom > 1.0f);

        renderer.UpdateZoom(-1.0f);
        Assert.Equal(1.0f, renderer.Zoom, 0.0001f);
    }

    [Fact]
    public void ZoomIsClampedToUsableLimits()
    {
        GameRenderer renderer = new(1280, 720);

        renderer.UpdateZoom(100.0f);
        Assert.Equal(1.65f, renderer.Zoom);

        renderer.UpdateZoom(-200.0f);
        Assert.Equal(0.35f, renderer.Zoom);
    }

    [Fact]
    public void WasdPansCameraOnlyAfterExplosion()
    {
        using GameWorld world = new();
        GameRenderer renderer = new(1280, 720);
        VehicleInput input = new(1.0f, 1.0f);

        renderer.UpdateCamera(world.Vehicle, input, 1.0f);
        Assert.Equal(world.Vehicle.Position, renderer.CameraTarget);

        world.ExplodeVehicle();
        renderer.UpdateCamera(world.Vehicle, input, 1.0f);

        Vector2 movement = renderer.CameraTarget - world.Vehicle.CameraTarget;
        float expectedDistance = 28.0f * SimulationUnits.PixelsPerMeter;
        Assert.Equal(expectedDistance, movement.Length(), 0.01f);
        Assert.True(movement.X > 0.0f);
        Assert.True(movement.Y < 0.0f);
    }

    [Fact]
    public void ExplodedCameraPanStaysInsidePlayableBoundary()
    {
        using GameWorld world = new();
        GameRenderer renderer = new(1280, 720);
        world.ExplodeVehicle();

        renderer.UpdateCamera(world.Vehicle, new VehicleInput(-1.0f, -1.0f), 100.0f);

        Vector2 expected = new(
            DrivingEnvironment.BoundaryMinimumMeters.X * SimulationUnits.PixelsPerMeter,
            DrivingEnvironment.BoundaryMaximumMeters.Y * SimulationUnits.PixelsPerMeter);
        Assert.Equal(expected, renderer.CameraTarget);
    }
}
