using Raylib_cs;

namespace CarGame;

internal static class Program
{
    private const int ScreenWidth = 1280;
    private const int ScreenHeight = 720;
    private const double FixedTimeStep = 1.0 / 60.0;
    private const double MaximumFrameTime = 0.25;
    private const int PhysicsSubSteps = 4;

    private static void Main()
    {
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "CarGame - Vehicle Physics Test");
        Raylib.SetTargetFPS(144);

        using GameWorld gameWorld = new();
        GameRenderer renderer = new(ScreenWidth, ScreenHeight);
        double accumulator = 0.0;

        try
        {
            while (!Raylib.WindowShouldClose())
            {
                float frameTime = MathF.Min(Raylib.GetFrameTime(), (float)MaximumFrameTime);
                accumulator += frameTime;

                VehicleInput input = ReadInput();
                if (Raylib.IsKeyPressed(KeyboardKey.R))
                {
                    gameWorld.ResetVehicle();
                    accumulator = 0.0;
                }

                while (accumulator >= FixedTimeStep)
                {
                    gameWorld.Step(input, (float)FixedTimeStep, PhysicsSubSteps);
                    accumulator -= FixedTimeStep;
                }

                renderer.Draw(gameWorld.Vehicle);
            }
        }
        finally
        {
            Raylib.CloseWindow();
        }
    }

    private static VehicleInput ReadInput()
    {
        float throttle = Axis(KeyboardKey.S, KeyboardKey.Down, KeyboardKey.W, KeyboardKey.Up);
        float steering = Axis(KeyboardKey.A, KeyboardKey.Left, KeyboardKey.D, KeyboardKey.Right);
        return new VehicleInput(throttle, steering);
    }

    private static float Axis(
        KeyboardKey negativePrimary,
        KeyboardKey negativeAlternate,
        KeyboardKey positivePrimary,
        KeyboardKey positiveAlternate)
    {
        float positive = Raylib.IsKeyDown(positivePrimary) || Raylib.IsKeyDown(positiveAlternate) ? 1.0f : 0.0f;
        float negative = Raylib.IsKeyDown(negativePrimary) || Raylib.IsKeyDown(negativeAlternate) ? 1.0f : 0.0f;
        return positive - negative;
    }
}
