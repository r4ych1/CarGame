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
        Assert.Equal(2.5f, renderer.Zoom);

        renderer.UpdateZoom(-200.0f);
        Assert.Equal(0.35f, renderer.Zoom);
    }
}
