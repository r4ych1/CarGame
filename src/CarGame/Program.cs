using Raylib_cs;

namespace CarGame;

internal static class Program
{
    private const int ScreenWidth = 1280;
    private const int ScreenHeight = 720;

    private static void Main()
    {
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "CarGame");
        Raylib.SetTargetFPS(60);

        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);
            Raylib.DrawText("CarGame project foundation", 32, 32, 32, Color.DarkGray);
            Raylib.DrawText("Raylib-cs rendering | Box2D.NET physics", 32, 76, 20, Color.Gray);
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
