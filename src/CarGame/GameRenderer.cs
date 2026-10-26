using System.Numerics;
using Raylib_cs;

namespace CarGame;

internal sealed class GameRenderer
{
    private const float GridSpacingPixels = SimulationUnits.PixelsPerMeter * 5.0f;
    private const int GridExtent = 40;
    private const float MinimumZoom = 0.35f;
    private const float MaximumZoom = 2.5f;
    private const float ZoomStep = 1.12f;
    private readonly Vector2 screenCenter;
    private float zoom = 1.0f;

    public GameRenderer(int screenWidth, int screenHeight)
    {
        screenCenter = new Vector2(screenWidth / 2.0f, screenHeight / 2.0f);
    }

    public void Draw(GameWorld world)
    {
        UpdateZoom(Raylib.GetMouseWheelMove());
        Vehicle vehicle = world.Vehicle;
        Camera2D camera = new()
        {
            Target = vehicle.CameraTarget,
            Offset = screenCenter,
            Rotation = 0.0f,
            Zoom = zoom,
        };

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(28, 35, 32, 255));
        Raylib.BeginMode2D(camera);
        DrawEnvironment(world.Environment);
        if (vehicle.IsExploded)
        {
            DrawExplosionPieces(vehicle.ExplosionPieces);
        }
        else
        {
            DrawVehicle(vehicle);
        }
        Raylib.EndMode2D();
        DrawHud(vehicle);
        Raylib.EndDrawing();
    }

    internal float Zoom => zoom;

    internal void UpdateZoom(float wheelMovement)
    {
        if (wheelMovement == 0.0f)
        {
            return;
        }

        zoom *= MathF.Pow(ZoomStep, wheelMovement);
        zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
    }

    private static void DrawEnvironment(DrivingEnvironment environment)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 mapMinimum = DrivingEnvironment.BoundaryMinimumMeters * scale;
        Vector2 mapMaximum = DrivingEnvironment.BoundaryMaximumMeters * scale;
        Raylib.DrawRectangle(
            (int)mapMinimum.X,
            (int)mapMinimum.Y,
            (int)(mapMaximum.X - mapMinimum.X),
            (int)(mapMaximum.Y - mapMinimum.Y),
            new Color(65, 94, 57, 255));
        DrawEnvironmentBoundary(scale);

        float streamTop = (DrivingEnvironment.StreamCenterYMeters - DrivingEnvironment.StreamWidthMeters * 0.5f) * scale;
        Raylib.DrawRectangle(
            (int)mapMinimum.X,
            (int)(streamTop - scale),
            (int)(mapMaximum.X - mapMinimum.X),
            (int)((DrivingEnvironment.StreamWidthMeters + 2.0f) * scale),
            new Color(57, 82, 61, 255));
        Raylib.DrawRectangle(
            (int)mapMinimum.X,
            (int)streamTop,
            (int)(mapMaximum.X - mapMinimum.X),
            (int)(DrivingEnvironment.StreamWidthMeters * scale),
            new Color(42, 112, 151, 255));
        DrawRiverFlow(environment, mapMinimum, mapMaximum, scale);

        DrawDirtRoad(scale);

        // Full-width circles at each sample act as round joins, filling the
        // outside corners that separate thick rectangular line segments.
        Vector2[] roadPoints = new Vector2[46];
        for (int index = 0; index < roadPoints.Length; index++)
        {
            float y = 60.0f - index * 10.0f;
            roadPoints[index] = new(DrivingEnvironment.RoadCenterXMeters(y) * scale, y * scale);
        }

        Color roadColor = new(73, 75, 72, 255);
        for (int index = 1; index < roadPoints.Length; index++)
        {
            Raylib.DrawLineEx(roadPoints[index - 1], roadPoints[index], 10.0f * scale, roadColor);
            Raylib.DrawCircleV(roadPoints[index], 5.0f * scale, roadColor);
        }

        for (int index = 1; index < roadPoints.Length; index++)
        {
            Raylib.DrawLineEx(
                roadPoints[index - 1],
                roadPoints[index],
                0.18f * scale,
                new Color(218, 190, 89, 255));
        }

        Vector2 bridgeCenter = new(
            DrivingEnvironment.BridgeCenterXMeters * scale,
            DrivingEnvironment.StreamCenterYMeters * scale);
        Vector2 bridgeSize = new(
            DrivingEnvironment.BridgeWidthMeters * scale,
            (DrivingEnvironment.StreamWidthMeters + 4.0f) * scale);
        Raylib.DrawRectanglePro(
            new Rectangle(bridgeCenter.X, bridgeCenter.Y, bridgeSize.X, bridgeSize.Y),
            bridgeSize * 0.5f,
            0.0f,
            new Color(86, 85, 79, 255));
        Raylib.DrawLineEx(
            bridgeCenter - new Vector2(bridgeSize.X * 0.5f, bridgeSize.Y * 0.5f),
            bridgeCenter - new Vector2(bridgeSize.X * 0.5f, -bridgeSize.Y * 0.5f),
            0.28f * scale,
            new Color(174, 179, 174, 255));
        Raylib.DrawLineEx(
            bridgeCenter + new Vector2(bridgeSize.X * 0.5f, -bridgeSize.Y * 0.5f),
            bridgeCenter + new Vector2(bridgeSize.X * 0.5f, bridgeSize.Y * 0.5f),
            0.28f * scale,
            new Color(174, 179, 174, 255));

        // Town cross streets.
        for (int row = 0; row < 4; row++)
            Raylib.DrawRectangle(-42 * (int)scale, (int)((-268 - row * 16) * scale), 84 * (int)scale, 7 * (int)scale, new Color(73, 75, 72, 255));
        foreach (WallSegment wall in environment.TownWalls)
        {
            Vector2 center = wall.PositionMeters * scale;
            Vector2 size = wall.SizeMeters * scale;
            Raylib.DrawRectanglePro(
                new Rectangle(center.X, center.Y, size.X, size.Y),
                size * 0.5f,
                0.0f,
                new Color(103, 95, 82, 255));
        }

        foreach (Vector2 crater in environment.Craters)
        {
            Vector2 center = crater * scale;
            Raylib.DrawCircleV(center, 7.0f * scale, new Color(45, 36, 29, 255));
            Raylib.DrawCircleLines((int)center.X, (int)center.Y, 8.0f * scale, new Color(93, 70, 45, 255));
        }
        foreach (SceneryObject item in environment.Scenery)
            if (!item.IsDestroyed) DrawScenery(item.Kind, item.PositionMeters, item.SizeMeters, item.RotationRadians);
        foreach (WorldDebris item in environment.Debris)
            DrawWorldDebris(item);
        foreach (PhysicsProp prop in environment.Props)
            DrawPhysicsProp(prop);
        foreach (TrafficVehicle trafficVehicle in environment.Traffic.Vehicles)
            if (trafficVehicle.IsActive) DrawTrafficVehicle(trafficVehicle);
        DrawBlastEffects(environment.BlastEffects);
    }

    private static void DrawTrafficVehicle(TrafficVehicle vehicle)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 center = vehicle.PositionMeters * scale;
        Vector2 size = vehicle.SizeMeters * scale;
        float rotation = vehicle.RotationRadians * 180.0f / MathF.PI;
        ColorRgb rgb = vehicle.Color;
        Color bodyColor = vehicle.IsWrecked
            ? new Color(72, 70, 66, 255)
            : new Color((int)rgb.Red, (int)rgb.Green, (int)rgb.Blue, 255);
        Raylib.DrawRectanglePro(
            new Rectangle(center.X + 2.0f, center.Y + 2.0f, size.X, size.Y),
            size * 0.5f,
            rotation,
            new Color(24, 28, 27, 150));
        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, size.X, size.Y),
            size * 0.5f,
            rotation,
            bodyColor);
        Vector2 forward = VehicleMath.Forward(vehicle.RotationRadians);
        Vector2 windowCenter = center + forward * size.Y * 0.12f;
        Vector2 windowSize = new(size.X * 0.72f, size.Y * 0.24f);
        Raylib.DrawRectanglePro(
            new Rectangle(windowCenter.X, windowCenter.Y, windowSize.X, windowSize.Y),
            windowSize * 0.5f,
            rotation,
            new Color(126, 181, 197, 255));
        if (vehicle.IsDisabled || vehicle.IsWrecked)
        {
            DrawDisabledTrafficEngine(vehicle, center, size, scale);
        }
    }

    private static void DrawDisabledTrafficEngine(
        TrafficVehicle vehicle,
        Vector2 center,
        Vector2 size,
        float scale)
    {
        Vector2 forward = VehicleMath.Forward(vehicle.RotationRadians);
        Vector2 right = VehicleMath.Right(vehicle.RotationRadians);
        Vector2 engine = center + forward * size.Y * 0.32f;
        float age = vehicle.DamageEffectAgeSeconds;
        float flicker = 0.82f + 0.18f * MathF.Sin(age * 13.0f);
        float outerRadius = 0.32f * scale * flicker;
        Raylib.DrawCircleV(engine, outerRadius, new Color(198, 54, 18, 220));
        Raylib.DrawCircleV(engine + forward * outerRadius * 0.18f, outerRadius * 0.62f, new Color(244, 132, 28, 235));
        Raylib.DrawCircleV(engine + forward * outerRadius * 0.32f, outerRadius * 0.28f, new Color(255, 214, 73, 245));

        for (int index = 0; index < 4; index++)
        {
            float phase = (age * 0.42f + index * 0.24f) % 1.0f;
            Vector2 smokeCenter = engine - forward * phase * 1.15f * scale +
                right * MathF.Sin((phase + index) * 5.0f) * 0.18f * scale;
            float radius = (0.20f + phase * 0.42f) * scale;
            byte alpha = (byte)(150.0f * (1.0f - phase));
            Raylib.DrawCircleV(smokeCenter, radius, new Color(61, 61, 58, (int)alpha));
        }
    }

    private static void DrawRiverFlow(
        DrivingEnvironment environment,
        Vector2 mapMinimum,
        Vector2 mapMaximum,
        float scale)
    {
        float width = mapMaximum.X - mapMinimum.X;
        float streamTop = (DrivingEnvironment.StreamCenterYMeters - DrivingEnvironment.StreamWidthMeters * 0.5f) * scale;
        for (int index = 0; index < 18; index++)
        {
            float phase = (environment.WaterFlowAgeSeconds * (0.75f + index % 3 * 0.16f) + index * 7.7f) * scale;
            float x = mapMinimum.X + ((phase % (width + 10.0f * scale)) - 5.0f * scale);
            float y = streamTop + (1.2f + (index % 6) * 3.15f) * scale;
            float length = (2.5f + (index % 4) * 0.8f) * scale;
            Raylib.DrawLineEx(
                new Vector2(x, y),
                new Vector2(MathF.Min(x + length, mapMaximum.X), y),
                0.10f * scale,
                new Color(133, 190, 207, 105));
        }
    }

    private static void DrawDirtRoad(float scale)
    {
        IReadOnlyList<Vector2> points = DrivingEnvironment.DirtRoadPointsMeters;
        Color dirt = new(132, 104, 68, 255);
        for (int index = 1; index < points.Count; index++)
        {
            Vector2 previous = points[index - 1] * scale;
            Vector2 current = points[index] * scale;
            Raylib.DrawLineEx(previous, current, 5.0f * scale, dirt);
            Raylib.DrawCircleV(current, 2.5f * scale, dirt);
        }

        for (int index = 1; index < points.Count; index++)
        {
            Raylib.DrawLineEx(
                points[index - 1] * scale,
                points[index] * scale,
                0.10f * scale,
                new Color(174, 142, 91, 180));
        }
    }

    private static void DrawPhysicsProp(PhysicsProp prop)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 center = prop.PositionMeters * scale;
        Vector2 size = prop.SizeMeters * scale;
        float rotation = prop.RotationRadians * 180.0f / MathF.PI;
        switch (prop.Kind)
        {
            case PhysicsPropKind.TrafficCone:
                Raylib.DrawCircleV(center, size.X * 0.52f, new Color(238, 112, 30, 255));
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, size.X * 0.30f, Color.RayWhite);
                break;
            case PhysicsPropKind.TrashCan:
                Raylib.DrawRectanglePro(new Rectangle(center.X, center.Y, size.X, size.Y), size * 0.5f, rotation,
                    new Color(65, 88, 82, 255));
                Raylib.DrawLineEx(center - new Vector2(size.X * 0.35f, 0.0f), center + new Vector2(size.X * 0.35f, 0.0f), 2.0f,
                    new Color(151, 163, 153, 255));
                break;
            case PhysicsPropKind.GuardRail:
                Raylib.DrawRectanglePro(new Rectangle(center.X, center.Y, size.X, size.Y), size * 0.5f, rotation,
                    new Color(176, 183, 181, 255));
                break;
            case PhysicsPropKind.Crate:
                Raylib.DrawRectanglePro(new Rectangle(center.X, center.Y, size.X, size.Y), size * 0.5f, rotation,
                    new Color(133, 91, 49, 255));
                Vector2 right = VehicleMath.Right(prop.RotationRadians) * size.X * 0.35f;
                Raylib.DrawLineEx(center - right, center + right, 2.0f, new Color(82, 52, 29, 255));
                break;
        }
    }

    private static void DrawBlastEffects(IReadOnlyList<BlastVisualEffect> effects)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        foreach (BlastVisualEffect effect in effects)
        {
            float age = effect.AgeSeconds;
            Vector2 center = effect.CenterMeters * scale;

            if (age < 0.25f)
            {
                float flashFade = 1.0f - (age / 0.25f);
                Raylib.DrawCircleV(center, (4.0f + age * 35.0f) * scale, Fade(Color.RayWhite, flashFade));
            }

            if (age < 0.9f)
            {
                float progress = age / 0.9f;
                float radius = DrivingEnvironment.BlastRadiusMeters * scale * progress;
                Raylib.DrawRing(center, MathF.Max(0.0f, radius - 0.7f * scale), radius, 0.0f, 360.0f, 96,
                    Fade(new Color(255, 223, 151, 255), 1.0f - progress));
            }

            if (age < 3.0f)
            {
                float fade = 1.0f - (age / 3.0f);
                foreach (BlastParticle particle in effect.Debris)
                {
                    Vector2 start = center + particle.OffsetMeters * scale * 0.12f;
                    Vector2 end = start + particle.VelocityMetersPerSecond * age * scale;
                    Vector2 direction = Vector2.Normalize(particle.VelocityMetersPerSecond);
                    Raylib.DrawLineEx(end - direction * particle.SizeMeters * scale * 3.0f, end,
                        MathF.Max(1.0f, particle.SizeMeters * scale), Fade(new Color(91, 67, 43, 255), fade));
                }

                foreach (BlastParticle particle in effect.Fire)
                {
                    float particleAge = age - particle.DelaySeconds;
                    if (particleAge < 0.0f) continue;
                    Vector2 position = center + particle.OffsetMeters * scale * 0.25f +
                        particle.VelocityMetersPerSecond * particleAge * scale * 0.18f;
                    float radius = particle.SizeMeters * scale * (2.8f - particleAge * 0.45f);
                    Raylib.DrawCircleV(position, MathF.Max(2.0f, radius), Fade(Color.Orange, fade));
                    Raylib.DrawCircleV(position, MathF.Max(1.0f, radius * 0.48f), Fade(Color.Gold, fade));
                }

            }

            for (int index = 0; index < effect.GroundFire.Count; index++)
            {
                BlastParticle particle = effect.GroundFire[index];
                float particleAge = age - particle.DelaySeconds;
                if (particleAge < 0.0f) continue;
                float endFade = Math.Clamp((12.0f - particleAge) / 3.0f, 0.0f, 1.0f);
                float flicker = 0.82f + 0.18f * MathF.Sin((particleAge * 9.0f) + (index * 1.7f));
                Vector2 position = center + particle.OffsetMeters * scale;
                float radius = particle.SizeMeters * scale * (0.38f + flicker * 0.32f);
                Raylib.DrawCircleV(position, radius, Fade(new Color(170, 49, 18, 190), endFade * 0.48f));
                Raylib.DrawCircleV(position - new Vector2(0.0f, radius * 0.22f), radius * 0.68f,
                    Fade(Color.Orange, endFade * flicker * 0.68f));
                Raylib.DrawCircleV(position - new Vector2(0.0f, radius * 0.38f), radius * 0.30f,
                    Fade(Color.Gold, endFade * flicker * 0.55f));
            }

            foreach (BlastParticle particle in effect.Smoke)
            {
                float particleAge = age - particle.DelaySeconds;
                if (particleAge < 0.0f) continue;
                float fade = Math.Clamp(1.0f - (particleAge / 12.0f), 0.0f, 1.0f);
                Vector2 position = center + particle.OffsetMeters * scale +
                    particle.VelocityMetersPerSecond * MathF.Min(particleAge, 3.0f) * scale * 0.12f;
                float radius = (particle.SizeMeters + MathF.Min(particleAge, 4.0f) * 0.35f) * scale;
                Raylib.DrawCircleV(position, radius, Fade(new Color(57, 58, 54, 190), fade * 0.72f));
                Raylib.DrawCircleV(position - new Vector2(radius * 0.18f), radius * 0.62f,
                    Fade(new Color(89, 87, 79, 150), fade * 0.65f));
            }
        }
    }

    private static Color Fade(Color color, float opacity) => new(
        color.R,
        color.G,
        color.B,
        (byte)Math.Clamp((int)(color.A * opacity), 0, 255));

    private static void DrawEnvironmentBoundary(float scale)
    {
        Vector2 minimum = DrivingEnvironment.BoundaryMinimumMeters * scale;
        Vector2 maximum = DrivingEnvironment.BoundaryMaximumMeters * scale;
        Rectangle bounds = new(minimum.X, minimum.Y, maximum.X - minimum.X, maximum.Y - minimum.Y);
        Raylib.DrawRectangleLinesEx(bounds, 4.0f * scale, new Color(108, 82, 50, 255));
        Raylib.DrawRectangleLinesEx(bounds, 0.35f * scale, new Color(197, 165, 105, 255));
    }

    private static void DrawWorldDebris(WorldDebris item)
    {
        if (item.Kind != SceneryKind.Tree)
        {
            SceneryKind debrisKind = item.Kind == SceneryKind.Shack ? SceneryKind.Building : item.Kind;
            DrawScenery(debrisKind, item.PositionMeters, item.SizeMeters, item.RotationRadians);
            return;
        }

        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 center = item.PositionMeters * scale;
        Vector2 trunkSize = new(0.65f * scale, 5.5f * scale);
        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, trunkSize.X, trunkSize.Y),
            trunkSize * 0.5f,
            item.RotationRadians * 180.0f / MathF.PI,
            new Color(91, 62, 38, 255));
        Vector2 canopyCenter = center +
            VehicleMath.Forward(item.RotationRadians) * trunkSize.Y * 0.44f;
        Raylib.DrawCircleV(canopyCenter, 1.2f * scale, new Color(35, 91, 43, 255));
    }

    private static void DrawScenery(SceneryKind kind, Vector2 positionMeters, Vector2 sizeMeters, float rotation)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 center = positionMeters * scale;
        if (kind is SceneryKind.Tree or SceneryKind.Bush)
        {
            Color color = kind == SceneryKind.Tree ? new(35, 91, 43, 255) : new(53, 117, 57, 255);
            Raylib.DrawCircleV(center, sizeMeters.X * scale * 0.65f, color);
            if (kind == SceneryKind.Tree) Raylib.DrawCircleV(center, sizeMeters.X * scale * 0.20f, new Color(91, 62, 38, 255));
            return;
        }
        Color fill = kind switch
        {
            SceneryKind.Building => new(177, 157, 126, 255),
            SceneryKind.Shack => new(123, 91, 62, 255),
            SceneryKind.ParkedCar => new(55, 91, 133, 255),
            SceneryKind.ParkedTruck => new(142, 137, 119, 255),
            _ => new(104, 105, 99, 255),
        };
        Raylib.DrawRectanglePro(new Rectangle(center.X, center.Y, sizeMeters.X * scale, sizeMeters.Y * scale), sizeMeters * scale * 0.5f, rotation * 180.0f / MathF.PI, fill);
        if (kind == SceneryKind.Shack)
        {
            Raylib.DrawRectanglePro(
                new Rectangle(center.X, center.Y, sizeMeters.X * scale * 0.34f, sizeMeters.Y * scale * 0.45f),
                new Vector2(sizeMeters.X * scale * 0.17f, sizeMeters.Y * scale * 0.225f),
                rotation * 180.0f / MathF.PI,
                new Color(48, 55, 52, 255));
        }
        else if (kind is SceneryKind.ParkedCar or SceneryKind.ParkedTruck)
        {
            Vector2 windowSize = new(sizeMeters.X * scale * 0.72f, sizeMeters.Y * scale * 0.23f);
            Raylib.DrawRectanglePro(
                new Rectangle(center.X, center.Y, windowSize.X, windowSize.Y),
                windowSize * 0.5f,
                rotation * 180.0f / MathF.PI,
                new Color(111, 164, 178, 255));
        }
    }

    private static void DrawExplosionPieces(IReadOnlyList<ExplosionPiece> pieces)
    {
        foreach (ExplosionPiece piece in pieces)
        {
            float width = piece.SizeMeters.X * SimulationUnits.PixelsPerMeter;
            float length = piece.SizeMeters.Y * SimulationUnits.PixelsPerMeter;
            float rotationDegrees = piece.RotationRadians * (180.0f / MathF.PI);
            Color color = piece.Kind == ExplosionPieceKind.Wheel
                ? new Color(18, 19, 20, 255)
                : new Color(190, 55, 46, 255);
            Raylib.DrawRectanglePro(
                new Rectangle(piece.Position.X, piece.Position.Y, width, length),
                new Vector2(width / 2.0f, length / 2.0f),
                rotationDegrees,
                color);
        }
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
        Raylib.DrawRectanglePro(body, origin, rotationDegrees,
            vehicle.IsSunk ? new Color(91, 103, 101, 255) : new Color(218, 69, 57, 255));
        Raylib.DrawRectanglePro(
            new Rectangle(windshieldCenter.X, windshieldCenter.Y, width * 0.72f, length * 0.25f),
            new Vector2(width * 0.36f, length * 0.125f),
            rotationDegrees,
            new Color(139, 201, 218, 255));

        Vector2 nose = visualCenter + (VehicleMath.Forward(vehicle.RotationRadians) * length * 0.43f);
        Raylib.DrawCircleV(nose, 4.0f, Color.Gold);
        if (vehicle.IsSunk)
        {
            Raylib.DrawCircleLines((int)vehicle.Position.X, (int)vehicle.Position.Y, width * 0.8f, new Color(126, 190, 211, 220));
        }
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
        Raylib.DrawText("WASD / ARROWS Drive   WHEEL Zoom   SPACE Explode   R Reset", 34, 60, 18, Color.LightGray);
        if (vehicle.IsExploded)
        {
            Raylib.DrawText("EXPLODED    Press R to reset", 34, 92, 24, Color.Orange);
            return;
        }
        if (vehicle.IsSunk)
        {
            Raylib.DrawText("SUNK IN DEEP WATER    Press R to reset", 34, 92, 24, new Color(107, 190, 222, 255));
            return;
        }
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
