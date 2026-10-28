using System.Numerics;
using Xunit;

namespace CarGame.Tests;

public sealed class CameraZoomTests
{
    [Fact]
    public void MouseWheelZoomsInAndOut()
    {
        GameRenderer renderer = new(1280, 720);

        Assert.Equal(0.5f, renderer.Zoom);
        renderer.UpdateZoom(1.0f);
        Assert.True(renderer.Zoom > 0.5f);

        renderer.UpdateZoom(-1.0f);
        Assert.Equal(0.5f, renderer.Zoom, 0.0001f);
    }

    [Fact]
    public void ZoomIsClampedToUsableLimits()
    {
        GameRenderer renderer = new(1280, 720);

        renderer.UpdateZoom(100.0f);
        Assert.Equal(1.35f, renderer.Zoom);

        renderer.UpdateZoom(-200.0f);
        Assert.Equal(0.35f, renderer.Zoom);
    }

    [Fact]
    public void WasdDoesNotPanCameraAfterExplosion()
    {
        using GameWorld world = new();
        GameRenderer renderer = new(1280, 720);

        renderer.UpdateCamera(world.Vehicle);
        Assert.Equal(world.Vehicle.Position, renderer.CameraTarget);

        world.ExplodeVehicle();
        renderer.UpdateCamera(world.Vehicle);
        Vector2 explosionTarget = renderer.CameraTarget;
        renderer.UpdateCamera(world.Vehicle);

        Assert.Equal(world.Vehicle.CameraTarget, explosionTarget);
        Assert.Equal(explosionTarget, renderer.CameraTarget);
    }

    [Fact]
    public void ResetRestoresVehicleFollowCamera()
    {
        using GameWorld world = new();
        GameRenderer renderer = new(1280, 720);
        world.ExplodeVehicle();
        renderer.UpdateCamera(world.Vehicle);

        world.ResetVehicle();
        renderer.UpdateCamera(world.Vehicle);

        Assert.Equal(world.Vehicle.Position, renderer.CameraTarget);
    }

    [Fact]
    public void MissionBriefingFadesAfterCarStartsMovingAndResetRestoresIt()
    {
        GameRenderer renderer = new(1280, 720);

        renderer.UpdateMissionBriefing(0.5f, 1.0f);
        Assert.Equal(1.0f, renderer.MissionOpacity);

        renderer.UpdateMissionBriefing(0.6f, 0.25f);
        Assert.InRange(renderer.MissionOpacity, 0.66f, 0.67f);

        renderer.UpdateMissionBriefing(0.0f, 0.5f);
        renderer.UpdateMissionBriefing(0.0f, 0.25f);
        Assert.Equal(0.0f, renderer.MissionOpacity);

        renderer.ResetVisualEffects();
        Assert.Equal(1.0f, renderer.MissionOpacity);
    }
}
