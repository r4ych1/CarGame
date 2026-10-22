using System.Numerics;
using Raylib_cs;

namespace CarGame;

internal sealed class GameRenderer
{
    private const float GridSpacingPixels = SimulationUnits.PixelsPerMeter * 5.0f;
    private const int GridExtent = 40;
    private readonly Vector2 screenCenter;

    public GameRenderer(int screenWidth, int screenHeight)
    {
        screenCenter = new Vector2(screenWidth / 2.0f, screenHeight / 2.0f);
    }

    public void Draw(Vehicle vehicle)
    {
        Camera2D camera = new()
        {
            Target = vehicle.Position,
            Offset = screenCenter,
            Rotation = 0.0f,
            Zoom = 1.0f,
        };

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(28, 35, 32, 255));
        Raylib.BeginMode2D(camera);
        DrawGrid();
        DrawVehicle(vehicle);
        Raylib.EndMode2D();
        DrawHud(vehicle);
        Raylib.EndDrawing();
    }

    private static void DrawGrid()
    {
        float extent = GridExtent * GridSpacingPixels;
        Color minor = new(47, 57, 53, 255);

        for (int index = -GridExtent; index <= GridExtent; index++)
        {
            float coordinate = index * GridSpacingPixels;
            Raylib.DrawLineV(new Vector2(coordinate, -extent), new Vector2(coordinate, extent), minor);
            Raylib.DrawLineV(new Vector2(-extent, coordinate), new Vector2(extent, coordinate), minor);
        }

        Raylib.DrawLineV(new Vector2(-extent, 0.0f), new Vector2(extent, 0.0f), new Color(76, 91, 84, 255));
        Raylib.DrawLineV(new Vector2(0.0f, -extent), new Vector2(0.0f, extent), new Color(76, 91, 84, 255));
    }

    private static void DrawVehicle(Vehicle vehicle)
    {
        float width = vehicle.Definition.Chassis.WidthMeters * SimulationUnits.PixelsPerMeter;
        float length = vehicle.Definition.Chassis.LengthMeters * SimulationUnits.PixelsPerMeter;
        float rotationDegrees = vehicle.RotationRadians * (180.0f / MathF.PI);
        Vector2 visualOffset =
            (VehicleMath.Right(vehicle.RotationRadians) * -vehicle.Diagnostics.ChassisRollRadians * 22.0f) +
            (VehicleMath.Forward(vehicle.RotationRadians) * vehicle.Diagnostics.ChassisPitchRadians * 22.0f);
        if (visualOffset.LengthSquared() > 16.0f)
        {
            visualOffset = Vector2.Normalize(visualOffset) * 4.0f;
        }

        Vector2 visualCenter = vehicle.Position + visualOffset;
        Rectangle body = new(visualCenter.X, visualCenter.Y, width, length);
        Vector2 origin = new(width / 2.0f, length / 2.0f);
        Vector2 windshieldCenter = visualCenter +
            (VehicleMath.Forward(vehicle.RotationRadians) * length * 0.17f);

        DrawWheels(vehicle, rotationDegrees);
        Raylib.DrawRectanglePro(
            new Rectangle(vehicle.Position.X + 2.0f, vehicle.Position.Y + 2.0f, width, length),
            origin,
            rotationDegrees,
            new Color(91, 38, 34, 180));
        Raylib.DrawRectanglePro(body, origin, rotationDegrees, new Color(218, 69, 57, 255));
        Raylib.DrawRectanglePro(
            new Rectangle(windshieldCenter.X, windshieldCenter.Y, width * 0.72f, length * 0.25f),
            new Vector2(width * 0.36f, length * 0.125f),
            rotationDegrees,
            new Color(139, 201, 218, 255));

        Vector2 nose = visualCenter + (VehicleMath.Forward(vehicle.RotationRadians) * length * 0.43f);
        Raylib.DrawCircleV(nose, 4.0f, Color.Gold);
    }

    private static void DrawWheels(Vehicle vehicle, float bodyRotationDegrees)
    {
        float halfTrack = vehicle.Definition.Chassis.AxleTrackMeters / 2.0f;
        float halfWheelbase = vehicle.Definition.Chassis.WheelbaseMeters / 2.0f;
        FourWheels<WheelDiagnostics> diagnostics = vehicle.Diagnostics.Wheels;
        FourWheels<WheelSimulationState> states = vehicle.SimulationState.Wheels;

        DrawWheel(vehicle, -halfTrack, -halfWheelbase, bodyRotationDegrees, diagnostics.FrontLeft, states.FrontLeft);
        DrawWheel(vehicle, halfTrack, -halfWheelbase, bodyRotationDegrees, diagnostics.FrontRight, states.FrontRight);
        DrawWheel(vehicle, -halfTrack, halfWheelbase, bodyRotationDegrees, diagnostics.RearLeft, states.RearLeft);
        DrawWheel(vehicle, halfTrack, halfWheelbase, bodyRotationDegrees, diagnostics.RearRight, states.RearRight);
    }

    private static void DrawWheel(
        Vehicle vehicle,
        float localX,
        float localY,
        float bodyRotationDegrees,
        WheelDiagnostics diagnostics,
        WheelSimulationState state)
    {
        const float wheelWidthMeters = 0.34f;
        const float wheelLengthMeters = 0.78f;
        Vector2 right = VehicleMath.Right(vehicle.RotationRadians);
        Vector2 forward = VehicleMath.Forward(vehicle.RotationRadians);
        Vector2 center = vehicle.Position +
            ((right * localX - forward * localY) * SimulationUnits.PixelsPerMeter);
        float wheelWidth = wheelWidthMeters * SimulationUnits.PixelsPerMeter;
        float wheelLength = wheelLengthMeters * SimulationUnits.PixelsPerMeter;
        float rotationDegrees = bodyRotationDegrees + (diagnostics.SteeringAngleRadians * (180.0f / MathF.PI));

        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, wheelWidth, wheelLength),
            new Vector2(wheelWidth / 2.0f, wheelLength / 2.0f),
            rotationDegrees,
            new Color(18, 19, 20, 255));

        Vector2 wheelForward = VehicleMath.Forward(vehicle.RotationRadians + diagnostics.SteeringAngleRadians);
        Vector2 wheelRight = VehicleMath.Right(vehicle.RotationRadians + diagnostics.SteeringAngleRadians);
        Vector2 markerCenter = center + (wheelForward * MathF.Sin(state.RotationRadians) * wheelLength * 0.32f);
        Raylib.DrawLineEx(
            markerCenter - (wheelRight * wheelWidth * 0.35f),
            markerCenter + (wheelRight * wheelWidth * 0.35f),
            2.0f,
            new Color(190, 190, 180, 255));
    }

    private static void DrawHud(Vehicle vehicle)
    {
        float speedKilometersPerHour = vehicle.SpeedMetersPerSecond * 3.6f;
        VehicleDiagnostics state = vehicle.Diagnostics;
        string gear = state.CurrentGear < 0 ? "R" : state.CurrentGear.ToString();
        string abs = state.AnyAbsIntervention ? "ACTIVE" : "ready";

        Raylib.DrawRectangle(18, 18, 650, 188, new Color(12, 16, 14, 210));
        Raylib.DrawText($"{vehicle.Definition.Name}  |  FWD", 34, 30, 22, Color.RayWhite);
        Raylib.DrawText("WASD / ARROWS  Drive    R  Reset", 34, 60, 18, Color.LightGray);
        Raylib.DrawText(
            $"Speed  {speedKilometersPerHour,5:0} km/h    Gear {gear}    RPM {state.EngineRevolutionsPerMinute,4:0}",
            34,
            88,
            24,
            Color.Gold);
        Raylib.DrawText(
            $"ABS {abs}    Shift {state.ShiftState}",
            34,
            124,
            17,
            state.AnyAbsIntervention ? Color.Orange : Color.LightGray);
        Raylib.DrawText(
            WheelSummary("FL", state.Wheels.FrontLeft) + "   " + WheelSummary("FR", state.Wheels.FrontRight),
            34,
            152,
            17,
            Color.LightGray);
        Raylib.DrawText(
            WheelSummary("RL", state.Wheels.RearLeft) + "   " + WheelSummary("RR", state.Wheels.RearRight),
            34,
            178,
            17,
            Color.LightGray);
    }

    private static string WheelSummary(string name, WheelDiagnostics wheel) =>
        $"{name} {wheel.NormalLoadNewtons / 1_000.0f:0.0}kN {wheel.GripUsage * 100.0f:0}%" +
        (wheel.AbsIntervening ? " ABS" : string.Empty);
}
