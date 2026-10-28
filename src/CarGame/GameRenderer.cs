using System.Numerics;
using Raylib_cs;

namespace CarGame;

internal sealed class GameRenderer
{
    private sealed class TireTrailSegment(
        object owner,
        int tireIndex,
        Vector2 startMeters,
        Vector2 endMeters)
    {
        public object Owner { get; } = owner;
        public int TireIndex { get; } = tireIndex;
        public Vector2 StartMeters { get; } = startMeters;
        public Vector2 EndMeters { get; } = endMeters;
        public float AgeSeconds { get; set; }
    }

    private readonly record struct TireSample(Vector2 LeftMeters, Vector2 RightMeters);

    private const float GridSpacingPixels = SimulationUnits.PixelsPerMeter * 5.0f;
    private const int GridExtent = 40;
    private const float MinimumZoom = 0.35f;
    private const float MaximumZoom = 1.35f;
    private const float ZoomStep = 1.12f;
    private const float TireTrailFadeSeconds = 2.0f;
    private const float MaximumTireTrailLengthMeters = 12.0f;
    private const float MissionDismissSpeedMetersPerSecond = 0.5f;
    private const float MissionFadeSeconds = 0.75f;
    private const string MissionLineOne = "Advance up the main road to the";
    private const string MissionLineTwo = "enemy base and complete the mission.";
    private const string MissionLineThree = "Watch out for the checkpoint.";
    private static readonly Vector2 GroundShadowDirection = Vector2.Normalize(new Vector2(0.65f, 1.0f));
    private readonly int screenWidth;
    private readonly int screenHeight;
    private readonly Vector2 screenCenter;
    private readonly Dictionary<object, TireSample> tireSamples = [];
    private readonly List<TireTrailSegment> tireTrails = [];
    private Vector2 cameraTarget;
    private bool wasExploded;
    private bool missionDismissed;
    private float missionOpacity = 1.0f;
    private float zoom = 0.5f;

    public GameRenderer(int screenWidth, int screenHeight)
    {
        this.screenWidth = screenWidth;
        this.screenHeight = screenHeight;
        screenCenter = new Vector2(screenWidth / 2.0f, screenHeight / 2.0f);
    }

    public void Draw(GameWorld world, float elapsedSeconds)
    {
        UpdateZoom(Raylib.GetMouseWheelMove());
        UpdateTireTrails(world, elapsedSeconds);
        Vehicle vehicle = world.Vehicle;
        UpdateMissionBriefing(vehicle.SpeedMetersPerSecond, elapsedSeconds);
        Camera2D camera = new()
        {
            Target = cameraTarget,
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
        DrawGuardRounds(world.Environment.GuardRounds);
        Raylib.EndMode2D();
        DrawHud(world, world.RunStatistics.HudOpacity);
        DrawGameOverOverlay(world.RunStatistics);
        Raylib.EndDrawing();
    }

    internal float Zoom => zoom;
    internal Vector2 CameraTarget => cameraTarget;
    internal float MissionOpacity => missionOpacity;

    internal void ResetVisualEffects()
    {
        tireSamples.Clear();
        tireTrails.Clear();
        missionDismissed = false;
        missionOpacity = 1.0f;
    }

    internal void UpdateCamera(Vehicle vehicle)
    {
        if (!vehicle.IsExploded)
        {
            cameraTarget = vehicle.Position;
            wasExploded = false;
            return;
        }

        if (!wasExploded)
        {
            cameraTarget = vehicle.CameraTarget;
            wasExploded = true;
        }
    }

    internal void UpdateZoom(float wheelMovement)
    {
        if (wheelMovement == 0.0f)
        {
            return;
        }

        zoom *= MathF.Pow(ZoomStep, wheelMovement);
        zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
    }

    internal void UpdateMissionBriefing(float speedMetersPerSecond, float elapsedSeconds)
    {
        if (speedMetersPerSecond > MissionDismissSpeedMetersPerSecond)
        {
            missionDismissed = true;
        }

        if (!missionDismissed)
        {
            return;
        }

        float fadeStep = Math.Clamp(elapsedSeconds, 0.0f, 0.25f) / MissionFadeSeconds;
        missionOpacity = MathF.Max(0.0f, missionOpacity - fadeStep);
    }

    private void UpdateTireTrails(GameWorld world, float elapsedSeconds)
    {
        float clampedElapsed = Math.Clamp(elapsedSeconds, 0.0f, 0.25f);
        for (int index = tireTrails.Count - 1; index >= 0; index--)
        {
            tireTrails[index].AgeSeconds += clampedElapsed;
            if (tireTrails[index].AgeSeconds >= TireTrailFadeSeconds)
            {
                tireTrails.RemoveAt(index);
            }
        }

        Vehicle player = world.Vehicle;
        SampleTires(
            player,
            player.PositionMeters,
            player.RotationRadians,
            player.Definition.Chassis.AxleTrackMeters * 0.5f,
            player.Definition.Chassis.WheelbaseMeters * 0.5f,
            player.SpeedMetersPerSecond,
            !player.IsExploded && !player.IsSunk);

        foreach (TrafficVehicle trafficVehicle in world.Environment.Traffic.Vehicles)
        {
            SampleTires(
                trafficVehicle,
                trafficVehicle.PositionMeters,
                trafficVehicle.RotationRadians,
                0.68f,
                1.42f,
                trafficVehicle.LinearVelocityMetersPerSecond.Length(),
                trafficVehicle.IsActive && !trafficVehicle.IsDisabled && !trafficVehicle.IsWrecked);
        }
    }

    private void SampleTires(
        object key,
        Vector2 centerMeters,
        float rotationRadians,
        float halfTrackMeters,
        float rearAxleOffsetMeters,
        float speedMetersPerSecond,
        bool active)
    {
        if (!active)
        {
            tireSamples.Remove(key);
            return;
        }

        Vector2 forward = VehicleMath.Forward(rotationRadians);
        Vector2 right = VehicleMath.Right(rotationRadians);
        Vector2 rearCenter = centerMeters - forward * rearAxleOffsetMeters;
        TireSample current = new(
            rearCenter - right * halfTrackMeters,
            rearCenter + right * halfTrackMeters);
        if (!tireSamples.TryGetValue(key, out TireSample previous) || speedMetersPerSecond < 1.5f)
        {
            tireSamples[key] = current;
            return;
        }

        float travelMeters = Vector2.Distance(previous.LeftMeters, current.LeftMeters);
        if (travelMeters > 2.0f)
        {
            tireSamples[key] = current;
            return;
        }

        if (travelMeters < 0.08f)
        {
            return;
        }

        tireTrails.Add(new(key, 0, previous.LeftMeters, current.LeftMeters));
        tireTrails.Add(new(key, 1, previous.RightMeters, current.RightMeters));
        TrimTireTrail(key, 0);
        TrimTireTrail(key, 1);
        tireSamples[key] = current;
    }

    private void TrimTireTrail(object owner, int tireIndex)
    {
        float retainedLengthMeters = 0.0f;
        for (int index = tireTrails.Count - 1; index >= 0; index--)
        {
            TireTrailSegment trail = tireTrails[index];
            if (!ReferenceEquals(trail.Owner, owner) || trail.TireIndex != tireIndex)
            {
                continue;
            }

            float segmentLengthMeters = Vector2.Distance(trail.StartMeters, trail.EndMeters);
            if (retainedLengthMeters + segmentLengthMeters > MaximumTireTrailLengthMeters)
            {
                tireTrails.RemoveAt(index);
                continue;
            }

            retainedLengthMeters += segmentLengthMeters;
        }
    }

    private void DrawTireTrails()
    {
        float scale = SimulationUnits.PixelsPerMeter;
        foreach (TireTrailSegment trail in tireTrails)
        {
            float opacity = Math.Clamp(1.0f - trail.AgeSeconds / TireTrailFadeSeconds, 0.0f, 1.0f);
            Raylib.DrawLineEx(
                trail.StartMeters * scale,
                trail.EndMeters * scale,
                0.10f * scale,
                new Color(28, 29, 27, (int)(58.0f * opacity)));
        }
    }

    private void DrawEnvironment(DrivingEnvironment environment)
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
        DrawTireTrails();
        foreach (SceneryObject item in environment.Scenery)
        {
            if (item.IsDestroyed) continue;
            DrawScenery(item.Kind, item.PositionMeters, item.SizeMeters, item.RotationRadians);
            if (item.IsCracked) DrawBuildingCracks(item);
        }
        foreach (WorldDebris item in environment.Debris)
            DrawWorldDebris(item);
        foreach (PhysicsProp prop in environment.Props)
            DrawPhysicsProp(prop);
        foreach (AmbientCharacter character in environment.Characters)
            DrawCharacter(character);
        foreach (TrafficVehicle trafficVehicle in environment.Traffic.Vehicles)
            if (trafficVehicle.IsActive) DrawTrafficVehicle(trafficVehicle);
        DrawBlastEffects(environment.BlastEffects);
    }

    private static void DrawCharacter(AmbientCharacter character)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        if (character.IsDead)
        {
            if (character.HasExplodedIntoLimbs)
            {
                foreach (CharacterLimb limb in character.LimbFragments)
                {
                    DrawCharacterLimb(limb, scale);
                }
                return;
            }

            DrawDeadCharacter(character, scale);
            return;
        }

        if (character.IsKnockedDown)
        {
            DrawKnockedDownCharacter(character, scale);
            return;
        }

        float figureScale = scale * 2.0f;
        Vector2 forward = new(MathF.Cos(character.FacingRadians), MathF.Sin(character.FacingRadians));
        Vector2 side = new(-forward.Y, forward.X);
        float phase = character.AnimationAgeSeconds * 8.0f + character.AnimationOffset;
        float walkSwing = character.Activity switch
        {
            CharacterActivity.Walking => MathF.Sin(phase),
            CharacterActivity.Working => MathF.Sin(phase * 0.55f) * 0.28f,
            CharacterActivity.Talking => MathF.Sin(phase * 0.55f) * 0.12f,
            _ => MathF.Sin(phase * 0.32f) * 0.08f,
        };
        Vector2 center = character.PositionMeters * scale;
        Vector2 headCenter = character.Role == CharacterRole.Guard
            ? center
            : center + forward * (0.15f * figureScale);
        Vector2 shoulder = center + forward * (0.06f * figureScale);
        Vector2 hips = center - forward * (0.14f * figureScale);
        Color clothing = ToRaylibColor(character.ClothingColor);
        Color skin = ToRaylibColor(character.SkinColor);
        Color trouser = new(54, 57, 61, 255);
        Vector2 shadowOffset = GroundShadowDirection * (0.15f * scale);
        Vector2 torsoSize = new(0.28f * figureScale, 0.38f * figureScale);
        float rotationDegrees = (character.FacingRadians * 180.0f / MathF.PI) - 90.0f;

        Raylib.DrawRectanglePro(
            new Rectangle(center.X + shadowOffset.X, center.Y + shadowOffset.Y, torsoSize.X, torsoSize.Y),
            torsoSize * 0.5f,
            rotationDegrees,
            new Color(24, 28, 25, 115));
        Raylib.DrawCircleV(headCenter + shadowOffset, 0.13f * figureScale, new Color(24, 28, 25, 120));

        Vector2 leftArmStart = shoulder - side * (0.10f * figureScale);
        Vector2 rightArmStart = shoulder + side * (0.10f * figureScale);
        Vector2 leftArmEnd = leftArmStart - side * (0.15f * figureScale) - forward * (walkSwing * 0.18f * figureScale);
        Vector2 rightArmEnd = rightArmStart + side * (0.15f * figureScale) + forward * (walkSwing * 0.18f * figureScale);
        Vector2 leftLegStart = hips - side * (0.065f * figureScale);
        Vector2 rightLegStart = hips + side * (0.065f * figureScale);
        Vector2 leftFoot = center - forward * ((0.22f - walkSwing * 0.12f) * figureScale) - side * (0.075f * figureScale);
        Vector2 rightFoot = center - forward * ((0.22f + walkSwing * 0.12f) * figureScale) + side * (0.075f * figureScale);

        Raylib.DrawLineEx(leftLegStart + shadowOffset, leftFoot + shadowOffset, 0.09f * figureScale, new Color(25, 27, 29, 145));
        Raylib.DrawLineEx(rightLegStart + shadowOffset, rightFoot + shadowOffset, 0.09f * figureScale, new Color(25, 27, 29, 145));
        Raylib.DrawLineEx(leftArmStart + shadowOffset, leftArmEnd + shadowOffset, 0.065f * figureScale, new Color(25, 27, 29, 145));
        Raylib.DrawLineEx(rightArmStart + shadowOffset, rightArmEnd + shadowOffset, 0.065f * figureScale, new Color(25, 27, 29, 145));
        Raylib.DrawLineEx(
            leftLegStart,
            leftFoot,
            0.09f * figureScale,
            trouser);
        Raylib.DrawLineEx(
            rightLegStart,
            rightFoot,
            0.09f * figureScale,
            trouser);
        Raylib.DrawLineEx(
            leftArmStart,
            leftArmEnd,
            0.065f * figureScale,
            clothing);
        Raylib.DrawLineEx(
            rightArmStart,
            rightArmEnd,
            0.065f * figureScale,
            clothing);
        Raylib.DrawLineEx(
            leftFoot,
            leftFoot + forward * (0.10f * figureScale),
            0.07f * figureScale,
            new Color(76, 61, 53, 255));
        Raylib.DrawLineEx(
            rightFoot,
            rightFoot + forward * (0.10f * figureScale),
            0.07f * figureScale,
            new Color(76, 61, 53, 255));

        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, torsoSize.X, torsoSize.Y),
            torsoSize * 0.5f,
            rotationDegrees,
            new Color(42, 46, 49, 255));
        Vector2 innerTorsoSize = torsoSize * 0.78f;
        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, innerTorsoSize.X, innerTorsoSize.Y),
            innerTorsoSize * 0.5f,
            rotationDegrees,
            clothing);
        Raylib.DrawLineEx(
            center - side * (0.085f * scale) - forward * (0.12f * scale),
            center - side * (0.085f * scale) + forward * (0.12f * scale),
            0.025f * figureScale,
            new Color(230, 224, 207, 190));
        Raylib.DrawCircleV(headCenter, 0.12f * figureScale, new Color(62, 45, 37, 255));
        Raylib.DrawCircleV(headCenter, 0.085f * figureScale, skin);
        Raylib.DrawCircleV(headCenter - side * (0.03f * figureScale) - forward * (0.025f * figureScale), 0.028f * figureScale, new Color(255, 225, 198, 225));
        if (character.Role == CharacterRole.Guard)
        {
            DrawGuardEquipment(character, center, headCenter, forward, side, figureScale);
        }
    }

    private static void DrawGuardEquipment(
        AmbientCharacter character,
        Vector2 center,
        Vector2 headCenter,
        Vector2 forward,
        Vector2 side,
        float figureScale)
    {
        Color rifleColor = new(40, 43, 38, 255);
        Vector2 rifleRear = center - forward * (0.18f * figureScale) + side * (0.16f * figureScale);
        Vector2 rifleFront = center + forward * (0.58f * figureScale) + side * (0.16f * figureScale);
        Raylib.DrawLineEx(rifleRear, rifleFront, 0.075f * figureScale, rifleColor);
        Raylib.DrawLineEx(
            center + side * (0.16f * figureScale),
            center + forward * (0.12f * figureScale) - side * (0.04f * figureScale),
            0.045f * figureScale,
            new Color(104, 76, 48, 255));
        Raylib.DrawCircleV(headCenter - forward * (0.015f * figureScale), 0.13f * figureScale, new Color(55, 70, 43, 255));
        Raylib.DrawLineEx(
            headCenter - side * (0.13f * figureScale),
            headCenter + side * (0.13f * figureScale),
            0.035f * figureScale,
            new Color(35, 44, 31, 255));

        if (character.MuzzleFlashAgeSeconds > 0.0f)
        {
            Raylib.DrawCircleV(rifleFront, 0.12f * figureScale, new Color(255, 229, 112, 235));
            Raylib.DrawCircleV(rifleFront + forward * (0.10f * figureScale), 0.065f * figureScale, new Color(255, 151, 53, 245));
        }
    }

    private static void DrawGuardRounds(IReadOnlyList<GuardRound> rounds)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        foreach (GuardRound round in rounds)
        {
            Vector2 position = round.PositionMeters * scale;
            if (round.HasImpacted)
            {
                const float sparkRadiusMeters = 0.34f;
                for (int ray = 0; ray < 4; ray++)
                {
                    float angle = ray * MathF.PI * 0.5f + MathF.PI * 0.25f;
                    Vector2 offset = new(MathF.Cos(angle), MathF.Sin(angle));
                    Raylib.DrawLineEx(
                        position,
                        position + offset * sparkRadiusMeters * scale,
                        0.07f * scale,
                        new Color(255, 205, 83, 235));
                }
                continue;
            }

            Vector2 velocityDirection = Vector2.Normalize(round.VelocityMetersPerSecond);
            Vector2 trailStart = round.PreviousPositionMeters * scale - velocityDirection * (0.32f * scale);
            Raylib.DrawLineEx(trailStart, position, 0.09f * scale, new Color(255, 226, 132, 230));
            Raylib.DrawCircleV(position, 0.07f * scale, new Color(255, 248, 205, 255));
        }
    }

    private static void DrawCharacterLimb(CharacterLimb limb, float scale)
    {
        Vector2 center = limb.PositionMeters * scale;
        if (limb.HasBloodPool)
        {
            Vector2 poolCenter = limb.BloodPoolPositionMeters * scale;
            float poolRadius = limb.BloodPoolRadiusMeters * scale;
            Raylib.DrawCircleV(poolCenter, poolRadius, new Color(76, 8, 15, 225));
            Raylib.DrawCircleV(
                poolCenter + new Vector2(poolRadius * 0.55f, -poolRadius * 0.18f),
                poolRadius * 0.58f,
                new Color(95, 10, 18, 220));
            Raylib.DrawCircleV(
                poolCenter + new Vector2(-poolRadius * 0.42f, poolRadius * 0.24f),
                poolRadius * 0.45f,
                new Color(119, 14, 22, 215));
            Raylib.DrawCircleV(
                poolCenter - new Vector2(poolRadius * 0.12f, poolRadius * 0.16f),
                poolRadius * 0.32f,
                new Color(148, 21, 28, 205));
        }

        Vector2 size = limb.SizeMeters * scale;
        float rotation = limb.RotationRadians * 180.0f / MathF.PI - 90.0f;
        Color color = limb.Kind switch
        {
            CharacterLimbKind.Head => ToRaylibColor(limb.SkinColor),
            CharacterLimbKind.Arm => ToRaylibColor(limb.ClothingColor),
            CharacterLimbKind.Leg => new Color(48, 52, 58, 255),
            _ => ToRaylibColor(limb.ClothingColor),
        };
        if (limb.Kind == CharacterLimbKind.Head)
        {
            Raylib.DrawCircleV(center, MathF.Max(size.X, size.Y) * 0.5f, new Color(63, 45, 38, 255));
            Raylib.DrawCircleV(center, MathF.Max(size.X, size.Y) * 0.38f, color);
        }
        else
        {
            Raylib.DrawRectanglePro(
                new Rectangle(center.X, center.Y, size.X, size.Y),
                size * 0.5f,
                rotation,
                color);
        }
    }

    private static void DrawKnockedDownCharacter(AmbientCharacter character, float scale)
    {
        float figureScale = scale * 1.2f;
        Vector2 center = character.PositionMeters * scale;
        Vector2 forward = new(MathF.Cos(character.RotationRadians), MathF.Sin(character.RotationRadians));
        Vector2 side = new(-forward.Y, forward.X);
        Color clothing = ToRaylibColor(character.ClothingColor);
        Color trousers = new(48, 52, 58, 255);
        Vector2 shoulder = center + forward * (0.18f * figureScale);
        Vector2 hips = center - forward * (0.28f * figureScale);

        Raylib.DrawLineEx(shoulder - side * (0.10f * figureScale), shoulder - side * (0.72f * figureScale) + forward * (0.04f * figureScale),
            0.14f * figureScale, clothing);
        Raylib.DrawLineEx(shoulder + side * (0.10f * figureScale), shoulder + side * (0.68f * figureScale) - forward * (0.02f * figureScale),
            0.14f * figureScale, clothing);
        Raylib.DrawLineEx(hips - side * (0.08f * figureScale), hips - side * (0.44f * figureScale) - forward * (0.20f * figureScale),
            0.18f * figureScale, trousers);
        Raylib.DrawLineEx(hips + side * (0.08f * figureScale), hips + side * (0.48f * figureScale) + forward * (0.10f * figureScale),
            0.18f * figureScale, trousers);

        Vector2 torsoSize = new(0.48f * figureScale, 0.68f * figureScale);
        float rotation = character.RotationRadians * 180.0f / MathF.PI - 90.0f;
        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, torsoSize.X, torsoSize.Y),
            torsoSize * 0.5f,
            rotation,
            clothing);
        Raylib.DrawCircleV(center + forward * (0.52f * figureScale), 0.18f * figureScale,
            new Color(63, 45, 38, 255));
        Raylib.DrawCircleV(center + forward * (0.52f * figureScale), 0.14f * figureScale,
            ToRaylibColor(character.SkinColor));
    }

    private static void DrawDeadCharacter(AmbientCharacter character, float scale)
    {
        Vector2 center = character.PositionMeters * scale;
        float figureScale = scale * 2.0f;
        float deathProgress = Math.Clamp(character.DeathAgeSeconds / 0.65f, 0.0f, 1.0f);
        float poolProgress = Math.Clamp(character.DeathAgeSeconds / 1.1f, 0.0f, 1.0f);
        float poolRadius = (0.08f + poolProgress * 0.48f) * scale;
        Raylib.DrawCircleV(center, poolRadius, new Color(112, 14, 22, 205));
        Raylib.DrawCircleV(center + new Vector2(-0.12f, 0.08f) * scale, poolRadius * 0.58f, new Color(77, 10, 17, 230));
        for (int drop = 0; drop < 4; drop++)
        {
            float angle = character.AnimationOffset + drop * MathF.PI * 0.5f;
            float distance = poolRadius * (0.9f + (drop % 2) * 0.25f);
            Vector2 bloodDrop = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            Raylib.DrawCircleV(bloodDrop, (0.045f + (drop % 2) * 0.018f) * scale, new Color(128, 18, 24, 220));
        }

        float spread = 0.45f + deathProgress * 0.55f;
        Vector2 forward = new(MathF.Cos(character.RotationRadians), MathF.Sin(character.RotationRadians));
        Vector2 side = new(-forward.Y, forward.X);
        Color darkClothing = new(48, 48, 48, 255);
        Color skin = ToRaylibColor(character.SkinColor);
        Vector2 body = center + forward * (0.08f * figureScale);
        Raylib.DrawLineEx(body - forward * (0.27f * spread * figureScale), body + forward * (0.28f * spread * figureScale), 0.34f * figureScale, darkClothing);
        Raylib.DrawLineEx(body - side * (0.07f * figureScale), body - side * (0.32f * spread * figureScale) + forward * (0.08f * figureScale), 0.09f * figureScale, darkClothing);
        Raylib.DrawLineEx(body + side * (0.07f * figureScale), body + side * (0.32f * spread * figureScale) - forward * (0.06f * figureScale), 0.09f * figureScale, darkClothing);
        Raylib.DrawLineEx(body - forward * (0.12f * figureScale), body - forward * (0.34f * spread * figureScale) - side * (0.11f * figureScale), 0.10f * figureScale, new Color(49, 52, 57, 255));
        Raylib.DrawLineEx(body - forward * (0.12f * figureScale), body - forward * (0.34f * spread * figureScale) + side * (0.11f * figureScale), 0.10f * figureScale, new Color(49, 52, 57, 255));
        Raylib.DrawCircleV(body + forward * (0.36f * spread * figureScale), 0.13f * figureScale, new Color(63, 45, 38, 255));
        Raylib.DrawCircleV(body + forward * (0.36f * spread * figureScale), 0.09f * figureScale, skin);
    }

    private static Color ToRaylibColor(ColorRgb color) =>
        new((int)color.Red, (int)color.Green, (int)color.Blue, 255);

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
        DrawRectangularGroundShadow(
            center,
            size,
            rotation,
            GroundShadowDirection * (0.20f * scale),
            1.10f,
            92);
        DrawSideViewMirrors(center, size, vehicle.RotationRadians);
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
        else
        {
            DrawRearTireSmoke(
                vehicle.PositionMeters,
                vehicle.RotationRadians,
                0.68f,
                1.42f,
                vehicle.LinearVelocityMetersPerSecond.Length());
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

    private static void DrawRearTireSmoke(
        Vector2 centerMeters,
        float rotationRadians,
        float halfTrackMeters,
        float rearAxleOffsetMeters,
        float speedMetersPerSecond)
    {
        if (speedMetersPerSecond < 9.0f)
        {
            return;
        }

        float scale = SimulationUnits.PixelsPerMeter;
        float age = (float)Raylib.GetTime();
        float speedOpacity = Math.Clamp((speedMetersPerSecond - 9.0f) / 10.0f, 0.0f, 1.0f);
        Vector2 forward = VehicleMath.Forward(rotationRadians);
        Vector2 right = VehicleMath.Right(rotationRadians);
        Vector2 rearCenter = centerMeters - forward * rearAxleOffsetMeters;
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            Vector2 tire = rearCenter + right * side * halfTrackMeters;
            for (int index = 0; index < 2; index++)
            {
                float phase = (age * 1.7f + index * 0.51f + (side > 0.0f ? 0.19f : 0.0f)) % 1.0f;
                Vector2 puff = tire - forward * (0.12f + phase * 0.62f) +
                    right * MathF.Sin((phase + index) * 5.0f) * 0.05f;
                float radius = (0.045f + phase * 0.085f) * scale;
                int alpha = (int)(45.0f * speedOpacity * (1.0f - phase));
                Raylib.DrawCircleV(puff * scale, radius, new Color(120, 120, 113, alpha));
            }
        }
    }

    private static void DrawDisabledPlayerEngine(Vehicle vehicle, Vector2 center, float length)
    {
        Vector2 forward = VehicleMath.Forward(vehicle.RotationRadians);
        Vector2 right = VehicleMath.Right(vehicle.RotationRadians);
        Vector2 engine = center + forward * length * 0.32f;
        float age = vehicle.DamageEffectAgeSeconds;
        float scale = SimulationUnits.PixelsPerMeter;
        for (int index = 0; index < 5; index++)
        {
            float phase = (age * 0.38f + index * 0.21f) % 1.0f;
            Vector2 smokeCenter = engine - forward * phase * 1.25f * scale +
                right * MathF.Sin((phase + index) * 4.7f) * 0.16f * scale;
            float radius = (0.16f + phase * 0.38f) * scale;
            int alpha = (int)(155.0f * (1.0f - phase));
            Raylib.DrawCircleV(smokeCenter, radius, new Color(57, 58, 55, alpha));
        }
    }

    private static void DrawWindshieldCracks(
        Vehicle vehicle,
        Vector2 windshieldCenter,
        float width,
        float length)
    {
        int crackCount = Math.Clamp((int)MathF.Ceiling(vehicle.DamagePercent / 25.0f), 0, 4);
        if (crackCount == 0)
        {
            return;
        }

        Vector2 forward = VehicleMath.Forward(vehicle.RotationRadians);
        Vector2 right = VehicleMath.Right(vehicle.RotationRadians);
        Vector2 crackOrigin = windshieldCenter - right * width * 0.10f + forward * length * 0.015f;
        Vector2[] branchOffsets =
        [
            right * width * 0.22f + forward * length * 0.06f,
            -right * width * 0.18f + forward * length * 0.08f,
            right * width * 0.26f - forward * length * 0.05f,
            -right * width * 0.25f - forward * length * 0.07f,
        ];
        for (int index = 0; index < crackCount; index++)
        {
            Vector2 midpoint = crackOrigin + branchOffsets[index] * 0.48f +
                (index % 2 == 0 ? right : -right) * 1.5f;
            Vector2 endpoint = crackOrigin + branchOffsets[index];
            Raylib.DrawLineEx(crackOrigin, midpoint, 1.4f, new Color(43, 57, 61, 220));
            Raylib.DrawLineEx(midpoint, endpoint, 1.1f, new Color(220, 235, 237, 205));
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
        if (prop.Kind == PhysicsPropKind.TrafficCone)
        {
            Raylib.DrawEllipse(
                (int)(center.X + GroundShadowDirection.X * 0.10f * scale),
                (int)(center.Y + GroundShadowDirection.Y * 0.10f * scale),
                size.X * 0.56f,
                size.Y * 0.38f,
                new Color(22, 25, 23, 60));
        }
        else
        {
            DrawRectangularGroundShadow(
                center,
                size,
                rotation,
                GroundShadowDirection * (0.12f * scale),
                1.07f,
                62);
        }
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

            for (int index = 0; index < effect.GroundFire.Count && effect.GroundFireOpacity > 0.0f; index++)
            {
                BlastParticle particle = effect.GroundFire[index];
                float particleAge = age - particle.DelaySeconds;
                if (particleAge < 0.0f) continue;
                float flicker = 0.82f + 0.18f * MathF.Sin((particleAge * 9.0f) + (index * 1.7f));
                Vector2 position = center + particle.OffsetMeters * scale;
                float radius = particle.SizeMeters * scale * (0.38f + flicker * 0.32f);
                Raylib.DrawCircleV(position, radius,
                    Fade(new Color(170, 49, 18, 190), effect.GroundFireOpacity * 0.48f));
                Raylib.DrawCircleV(position - new Vector2(0.0f, radius * 0.22f), radius * 0.68f,
                    Fade(Color.Orange, effect.GroundFireOpacity * flicker * 0.68f));
                Raylib.DrawCircleV(position - new Vector2(0.0f, radius * 0.38f), radius * 0.30f,
                    Fade(Color.Gold, effect.GroundFireOpacity * flicker * 0.55f));
            }

            foreach (BlastParticle particle in effect.Smoke)
            {
                for (int ringIndex = 0; ringIndex < BlastVisualEffect.SmokeRingsPerTrail; ringIndex++)
                {
                    float progress = effect.SmokeRingProgress(particle, ringIndex);
                    if (progress < 0.0f)
                    {
                        continue;
                    }

                    Vector2 position = center + particle.OffsetMeters * scale +
                        particle.VelocityMetersPerSecond * progress * 3.0f * scale;
                    float radius = (particle.SizeMeters * 0.55f +
                        progress * (1.4f + particle.SizeMeters * 1.2f)) * scale;
                    float opacity = (1.0f - progress) * 0.52f;
                    Raylib.DrawCircleV(
                        position,
                        radius,
                        Fade(new Color(57, 58, 54, 190), opacity));
                    Raylib.DrawCircleV(
                        position - new Vector2(radius * 0.12f, radius * 0.08f),
                        radius * 0.58f,
                        Fade(new Color(89, 87, 79, 150), opacity * 0.82f));
                }
            }

            if (effect.GroundSmokeOpacity > 0.0f)
            {
                for (int layerIndex = BlastVisualEffect.GroundSmokeLayerCount - 1;
                    layerIndex >= 0;
                    layerIndex--)
                {
                    float radius = effect.GroundSmokeLayerRadiusMeters(layerIndex) * scale;
                    float layerOpacity = effect.GroundSmokeOpacity * (0.20f + (3 - layerIndex) * 0.08f);
                    Color layerColor = layerIndex % 2 == 0
                        ? new Color(48, 49, 48, 225)
                        : new Color(69, 69, 65, 205);
                    Raylib.DrawCircleV(center, radius, Fade(layerColor, layerOpacity));
                }
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
        if (item.Kind is SceneryKind.Building or SceneryKind.Shack)
        {
            DrawCharredBuildingFragment(item);
            return;
        }

        if (item.Kind != SceneryKind.Tree)
        {
            DrawScenery(item.Kind, item.PositionMeters, item.SizeMeters, item.RotationRadians);
            return;
        }

        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 center = item.PositionMeters * scale;
        Vector2 trunkSize = new(0.65f * scale, 5.5f * scale);
        float rotationDegrees = item.RotationRadians * 180.0f / MathF.PI;
        Vector2 canopyCenter = center +
            VehicleMath.Forward(item.RotationRadians) * trunkSize.Y * 0.44f;
        DrawRectangularGroundShadow(
            center,
            trunkSize,
            rotationDegrees,
            GroundShadowDirection * (0.14f * scale),
            1.05f,
            58);
        Vector2 canopyShadowCenter = canopyCenter + GroundShadowDirection * (0.22f * scale);
        Raylib.DrawEllipse(
            (int)canopyShadowCenter.X,
            (int)canopyShadowCenter.Y,
            1.25f * scale,
            0.82f * scale,
            new Color(20, 24, 21, 58));
        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, trunkSize.X, trunkSize.Y),
            trunkSize * 0.5f,
            rotationDegrees,
            new Color(91, 62, 38, 255));
        Raylib.DrawCircleV(canopyCenter, 1.2f * scale, new Color(35, 91, 43, 255));
    }

    private static void DrawCharredBuildingFragment(WorldDebris item)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 center = item.PositionMeters * scale;
        Vector2 size = item.SizeMeters * scale;
        float rotation = item.RotationRadians * 180.0f / MathF.PI;
        Raylib.DrawRectanglePro(
            new Rectangle(center.X + 2.0f, center.Y + 3.0f, size.X, size.Y),
            size * 0.5f,
            rotation,
            new Color(15, 16, 16, 180));
        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, size.X, size.Y),
            size * 0.5f,
            rotation,
            new Color(43, 42, 39, 255));

        Vector2 ashSize = size * new Vector2(0.56f, 0.19f);
        Raylib.DrawRectanglePro(
            new Rectangle(center.X - size.X * 0.08f, center.Y - size.Y * 0.17f, ashSize.X, ashSize.Y),
            ashSize * 0.5f,
            rotation + 11.0f,
            new Color(84, 82, 76, 255));
        Vector2 sootSize = size * new Vector2(0.20f, 0.52f);
        Raylib.DrawRectanglePro(
            new Rectangle(center.X + size.X * 0.22f, center.Y + size.Y * 0.13f, sootSize.X, sootSize.Y),
            sootSize * 0.5f,
            rotation - 17.0f,
            new Color(28, 29, 28, 255));
        Raylib.DrawLineEx(
            RotateAround(center - new Vector2(size.X * 0.35f, size.Y * 0.02f), center, rotation),
            RotateAround(center + new Vector2(size.X * 0.36f, size.Y * 0.08f), center, rotation),
            MathF.Max(1.0f, 0.07f * scale),
            new Color(109, 105, 96, 220));

        if (item.HasFireSmoke)
        {
            float smokeFade = Math.Clamp(1.0f - item.AgeSeconds / 9.0f, 0.0f, 1.0f);
            float fireFade = Math.Clamp(1.0f - item.AgeSeconds / 3.5f, 0.0f, 1.0f);
            float drift = MathF.Min(item.AgeSeconds, 6.0f) * 0.22f * scale;
            for (int puff = 0; puff < 4; puff++)
            {
                Vector2 smokeCenter = center + new Vector2(
                    (puff - 1.5f) * size.X * 0.16f,
                    -drift - puff * size.Y * 0.14f);
                float radius = (0.22f + puff * 0.09f) * scale;
                Raylib.DrawCircleV(smokeCenter, radius, Fade(new Color(45, 46, 44, 200), smokeFade * 0.72f));
                Raylib.DrawCircleV(smokeCenter - new Vector2(radius * 0.15f), radius * 0.62f,
                    Fade(new Color(79, 78, 73, 170), smokeFade * 0.65f));
            }

            if (fireFade > 0.0f)
            {
                Vector2 flameCenter = center + new Vector2(size.X * 0.14f, -size.Y * 0.08f);
                float flameRadius = MathF.Max(2.0f, (0.24f + 0.12f * MathF.Sin(item.AgeSeconds * 11.0f)) * scale);
                Raylib.DrawCircleV(flameCenter, flameRadius, Fade(new Color(217, 78, 22, 230), fireFade));
                Raylib.DrawCircleV(flameCenter - new Vector2(0.0f, flameRadius * 0.18f), flameRadius * 0.52f,
                    Fade(new Color(255, 183, 58, 245), fireFade));
            }
        }
    }

    private static void DrawBuildingCracks(SceneryObject building)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 center = building.PositionMeters * scale;
        Vector2 size = building.SizeMeters * scale;
        float rotation = building.RotationRadians * 180.0f / MathF.PI;
        Vector2[][] cracks =
        [
            [new(-0.42f, -0.32f), new(-0.20f, -0.18f), new(-0.28f, 0.02f), new(-0.05f, 0.16f), new(0.02f, 0.38f)],
            [new(0.34f, -0.38f), new(0.18f, -0.18f), new(0.31f, -0.02f), new(0.12f, 0.12f), new(0.20f, 0.34f)],
            [new(-0.05f, -0.12f), new(0.08f, -0.02f), new(-0.02f, 0.12f), new(0.12f, 0.22f)],
        ];

        foreach (Vector2[] crack in cracks)
        {
            for (int index = 1; index < crack.Length; index++)
            {
                Vector2 startLocal = crack[index - 1] * size;
                Vector2 endLocal = crack[index] * size;
                Vector2 start = center + RotateAround(startLocal, Vector2.Zero, rotation);
                Vector2 end = center + RotateAround(endLocal, Vector2.Zero, rotation);
                Raylib.DrawLineEx(start, end, MathF.Max(1.0f, 0.075f * scale), new Color(37, 34, 31, 245));
                Raylib.DrawLineEx(start + new Vector2(0.0f, 1.0f), end + new Vector2(0.0f, 1.0f),
                    MathF.Max(1.0f, 0.025f * scale), new Color(191, 173, 146, 220));
            }
        }
    }

    private static void DrawScenery(SceneryKind kind, Vector2 positionMeters, Vector2 sizeMeters, float rotation)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 center = positionMeters * scale;
        if (kind is SceneryKind.Tree or SceneryKind.Bush)
        {
            Color color = kind == SceneryKind.Tree ? new(35, 91, 43, 255) : new(53, 117, 57, 255);
            float foliageRadius = sizeMeters.X * scale * 0.65f;
            float foliageShadowLengthMeters = kind == SceneryKind.Tree ? 0.30f : 0.14f;
            Vector2 foliageShadowCenter = center +
                GroundShadowDirection * foliageShadowLengthMeters * scale;
            Raylib.DrawEllipse(
                (int)foliageShadowCenter.X,
                (int)foliageShadowCenter.Y,
                foliageRadius * 1.04f,
                foliageRadius * 0.72f,
                new Color(21, 27, 22, kind == SceneryKind.Tree ? 70 : 50));
            Raylib.DrawCircleV(center, foliageRadius, color);
            if (kind == SceneryKind.Tree) Raylib.DrawCircleV(center, sizeMeters.X * scale * 0.20f, new Color(91, 62, 38, 255));
            return;
        }
        Vector2 size = sizeMeters * scale;
        float rotationDegrees = rotation * 180.0f / MathF.PI;
        byte shadowAlpha = kind is SceneryKind.Building or SceneryKind.Shack ? (byte)72 : (byte)64;
        float shadowExpansion = kind is SceneryKind.ParkedCar or SceneryKind.ParkedTruck ? 1.08f : 1.04f;
        float shadowLengthMeters = kind switch
        {
            SceneryKind.Building or SceneryKind.Shack => 0.34f,
            SceneryKind.ParkedCar or SceneryKind.ParkedTruck => 0.20f,
            _ => 0.14f,
        };
        DrawRectangularGroundShadow(
            center,
            size,
            rotationDegrees,
            GroundShadowDirection * shadowLengthMeters * scale,
            shadowExpansion,
            shadowAlpha);
        Color fill = kind switch
        {
            SceneryKind.Building => new(177, 157, 126, 255),
            SceneryKind.Shack => new(123, 91, 62, 255),
            SceneryKind.ParkedCar => new(55, 91, 133, 255),
            SceneryKind.ParkedTruck => new(142, 137, 119, 255),
            _ => new(104, 105, 99, 255),
        };
        if (kind is SceneryKind.ParkedCar or SceneryKind.ParkedTruck)
        {
            DrawSideViewMirrors(center, size, rotation);
        }
        Raylib.DrawRectanglePro(new Rectangle(center.X, center.Y, size.X, size.Y), size * 0.5f, rotationDegrees, fill);
        if (kind == SceneryKind.Building)
        {
            DrawBuildingRoof(center, sizeMeters, positionMeters, scale, rotation);
        }
        else if (kind == SceneryKind.Shack)
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

    private static void DrawBuildingRoof(
        Vector2 center,
        Vector2 sizeMeters,
        Vector2 positionMeters,
        float scale,
        float rotation)
    {
        Color[] roofColors =
        [
            new Color(126, 75, 59, 255),
            new Color(76, 91, 101, 255),
            new Color(112, 101, 78, 255),
            new Color(91, 105, 78, 255),
        ];
        int roofIndex = Math.Abs((int)(positionMeters.X * 13.0f + positionMeters.Y * 7.0f)) % roofColors.Length;
        Vector2 roofSize = sizeMeters * scale * 0.84f;
        float rotationDegrees = rotation * 180.0f / MathF.PI;
        Raylib.DrawRectanglePro(
            new Rectangle(center.X + 0.08f * scale, center.Y + 0.10f * scale, roofSize.X, roofSize.Y),
            roofSize * 0.5f,
            rotationDegrees,
            new Color(47, 45, 40, 170));
        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, roofSize.X, roofSize.Y),
            roofSize * 0.5f,
            rotationDegrees,
            roofColors[roofIndex]);

        Vector2 ridgeStart = center - new Vector2(roofSize.X * 0.5f, 0.0f);
        Vector2 ridgeEnd = center + new Vector2(roofSize.X * 0.5f, 0.0f);
        Raylib.DrawLineEx(
            RotateAround(ridgeStart, center, rotationDegrees),
            RotateAround(ridgeEnd, center, rotationDegrees),
            0.10f * scale,
            new Color(194, 172, 133, 235));

        for (int seam = -1; seam <= 1; seam++)
        {
            float y = center.Y + seam * roofSize.Y * 0.24f;
            Vector2 seamStart = new(center.X - roofSize.X * 0.43f, y);
            Vector2 seamEnd = new(center.X + roofSize.X * 0.43f, y);
            Raylib.DrawLineEx(
                RotateAround(seamStart, center, rotationDegrees),
                RotateAround(seamEnd, center, rotationDegrees),
                0.045f * scale,
                new Color(43, 45, 44, 185));
        }

        Vector2[] roofCorners =
        [
            new(-roofSize.X * 0.5f, -roofSize.Y * 0.5f),
            new(roofSize.X * 0.5f, -roofSize.Y * 0.5f),
            new(roofSize.X * 0.5f, roofSize.Y * 0.5f),
            new(-roofSize.X * 0.5f, roofSize.Y * 0.5f),
        ];
        for (int index = 0; index < roofCorners.Length; index++)
        {
            Vector2 edgeStart = RotateAround(center + roofCorners[index], center, rotationDegrees);
            Vector2 edgeEnd = RotateAround(center + roofCorners[(index + 1) % roofCorners.Length], center, rotationDegrees);
            Raylib.DrawLineEx(edgeStart, edgeEnd, 0.075f * scale, new Color(47, 45, 40, 230));
        }
    }

    private static Vector2 RotateAround(Vector2 point, Vector2 center, float rotationDegrees)
    {
        float radians = rotationDegrees * MathF.PI / 180.0f;
        Vector2 offset = point - center;
        return center + new Vector2(
            offset.X * MathF.Cos(radians) - offset.Y * MathF.Sin(radians),
            offset.X * MathF.Sin(radians) + offset.Y * MathF.Cos(radians));
    }

    private static void DrawRectangularGroundShadow(
        Vector2 center,
        Vector2 size,
        float rotationDegrees,
        Vector2 shadowOffset,
        float expansion,
        byte alpha)
    {
        Vector2 shadowSize = size * expansion;
        Vector2 shadowCenter = center + shadowOffset;
        Raylib.DrawRectanglePro(
            new Rectangle(shadowCenter.X, shadowCenter.Y, shadowSize.X, shadowSize.Y),
            shadowSize * 0.5f,
            rotationDegrees,
            new Color(19, 23, 21, (int)alpha));
    }

    private static void DrawExplosionPieces(IReadOnlyList<ExplosionPiece> pieces)
    {
        foreach (ExplosionPiece piece in pieces)
        {
            float width = piece.SizeMeters.X * SimulationUnits.PixelsPerMeter;
            float length = piece.SizeMeters.Y * SimulationUnits.PixelsPerMeter;
            float rotationDegrees = piece.RotationRadians * (180.0f / MathF.PI);
            Color color = piece.Kind switch
            {
                ExplosionPieceKind.Wheel => new Color(18, 19, 20, 255),
                ExplosionPieceKind.EngineBlock => new Color(62, 67, 66, 255),
                ExplosionPieceKind.Bumper => new Color(164, 169, 166, 255),
                ExplosionPieceKind.MiscellaneousDebris => new Color(112, 73, 49, 255),
                _ => new Color(190, 55, 46, 255),
            };
            DrawRectangularGroundShadow(
                piece.Position,
                new Vector2(width, length),
                rotationDegrees,
                GroundShadowDirection * (0.12f * SimulationUnits.PixelsPerMeter),
                1.06f,
                58);
            Raylib.DrawRectanglePro(
                new Rectangle(piece.Position.X, piece.Position.Y, width, length),
                new Vector2(width / 2.0f, length / 2.0f),
                rotationDegrees,
                color);
            if (piece.Kind == ExplosionPieceKind.EngineBlock)
            {
                Raylib.DrawCircleV(piece.Position, MathF.Min(width, length) * 0.24f,
                    new Color(29, 32, 32, 255));
            }
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

        DrawRectangularGroundShadow(
            vehicle.Position,
            new Vector2(width, length),
            rotationDegrees,
            GroundShadowDirection * (0.20f * SimulationUnits.PixelsPerMeter),
            1.11f,
            96);
        DrawWheels(vehicle, rotationDegrees);
        DrawSideViewMirrors(
            visualCenter,
            new Vector2(width, length),
            vehicle.RotationRadians);
        Raylib.DrawRectanglePro(body, origin, rotationDegrees,
            vehicle.IsSunk ? new Color(91, 103, 101, 255) : new Color(218, 69, 57, 255));
        Raylib.DrawRectanglePro(
            new Rectangle(windshieldCenter.X, windshieldCenter.Y, width * 0.72f, length * 0.25f),
            new Vector2(width * 0.36f, length * 0.125f),
            rotationDegrees,
            new Color(139, 201, 218, 255));
        DrawWindshieldCracks(vehicle, windshieldCenter, width, length);

        Vector2 nose = visualCenter + (VehicleMath.Forward(vehicle.RotationRadians) * length * 0.43f);
        Raylib.DrawCircleV(nose, 4.0f, Color.Gold);
        if (vehicle.IsSunk)
        {
            Raylib.DrawCircleLines((int)vehicle.Position.X, (int)vehicle.Position.Y, width * 0.8f, new Color(126, 190, 211, 220));
        }
        else if (vehicle.IsDisabled)
        {
            DrawDisabledPlayerEngine(vehicle, visualCenter, length);
        }
        else
        {
            DrawRearTireSmoke(
                vehicle.PositionMeters,
                vehicle.RotationRadians,
                vehicle.Definition.Chassis.AxleTrackMeters * 0.5f,
                vehicle.Definition.Chassis.WheelbaseMeters * 0.5f,
                vehicle.SpeedMetersPerSecond);
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

    private void DrawHud(GameWorld world, float opacity)
    {
        if (opacity <= 0.0f)
        {
            return;
        }

        Vehicle vehicle = world.Vehicle;
        RunStatistics run = world.RunStatistics;
        float speedKilometersPerHour = vehicle.SpeedMetersPerSecond * 3.6f;
        VehicleDiagnostics state = vehicle.Diagnostics;
        string gear = state.CurrentGear < 0 ? "R" : state.CurrentGear.ToString();
        const int bottomBarHeight = 72;
        int bottomBarY = screenHeight - bottomBarHeight;

        const int smallFontSize = 16;
        Color hudLight = WithOpacity(Color.LightGray, opacity);
        Color hudText = WithOpacity(Color.RayWhite, opacity);
        Raylib.DrawText("TIME", 34, bottomBarY + 9, smallFontSize, hudLight);
        Raylib.DrawText(FormatTime(run.ElapsedSeconds), 34, bottomBarY + 31, 20, hudText);

        string speedStatus = $"{speedKilometersPerHour:0} km/h";
        const int speedFontSize = 28;
        int speedX = (screenWidth - Raylib.MeasureText(speedStatus, speedFontSize)) / 2;
        Raylib.DrawText(speedStatus, speedX, bottomBarY + 5, speedFontSize, hudText);
        string gearStatus = $"GEAR {gear}";
        int gearX = (screenWidth - Raylib.MeasureText(gearStatus, smallFontSize)) / 2;
        Raylib.DrawText(gearStatus, gearX, bottomBarY + 43, smallFontSize, hudLight);

        const int hpBarWidth = 210;
        const int hpBarHeight = 18;
        int hpBarX = screenWidth - hpBarWidth - 34;
        int hpBarY = bottomBarY + 31;
        float healthFraction = Math.Clamp(1.0f - vehicle.DamagePercent / 100.0f, 0.0f, 1.0f);
        Color hpColor = healthFraction > 0.5f
            ? new Color(76, 190, 92, 255)
            : healthFraction > 0.2f
                ? new Color(226, 166, 55, 255)
                : new Color(207, 67, 55, 255);
        Raylib.DrawText("HP", hpBarX, bottomBarY + 9, smallFontSize, hudLight);
        Raylib.DrawRectangle(hpBarX, hpBarY, hpBarWidth, hpBarHeight, WithOpacity(new Color(36, 42, 40, 235), opacity));
        Raylib.DrawRectangle(hpBarX + 2, hpBarY + 2, (int)((hpBarWidth - 4) * healthFraction), hpBarHeight - 4, WithOpacity(hpColor, opacity));
        Raylib.DrawRectangleLines(hpBarX, hpBarY, hpBarWidth, hpBarHeight, WithOpacity(new Color(145, 153, 149, 230), opacity));

        string? condition = vehicle.IsExploded
            ? null
            : vehicle.IsSunk
                ? "SUNK IN DEEP WATER - R Reset"
                : null;
        if (condition is not null)
        {
            const int conditionFontSize = 18;
            int conditionX = (screenWidth - Raylib.MeasureText(condition, conditionFontSize)) / 2;
            Color conditionColor = vehicle.IsSunk ? new Color(107, 190, 222, 255) : Color.Orange;
            Raylib.DrawRectangle(
                conditionX - 10,
                bottomBarY - 28,
                Raylib.MeasureText(condition, conditionFontSize) + 20,
                24,
                WithOpacity(new Color(10, 14, 13, 178), opacity));
            Raylib.DrawText(condition, conditionX, bottomBarY - 27, conditionFontSize, WithOpacity(conditionColor, opacity));
        }

        const int sidebarWidth = 222;
        const int sidebarHeight = 140;
        int sidebarX = screenWidth - sidebarWidth - 18;
        const int sidebarY = 18;
        Raylib.DrawRectangle(sidebarX, sidebarY, sidebarWidth, sidebarHeight, WithOpacity(new Color(10, 14, 13, 168), opacity));
        Raylib.DrawText("CONTROLS", sidebarX + 16, sidebarY + 14, 19, hudText);
        Raylib.DrawText("WASD / Arrows   Drive", sidebarX + 16, sidebarY + 44, 17, hudLight);
        Raylib.DrawText("Mouse-wheel   Zoom", sidebarX + 16, sidebarY + 68, 17, hudLight);
        Raylib.DrawText("Space   Detonator", sidebarX + 16, sidebarY + 92, 17, hudLight);
        Raylib.DrawText("R   Reset", sidebarX + 16, sidebarY + 116, 17, hudLight);

        DrawMissionBriefing(opacity * missionOpacity);
    }

    private static void DrawSideViewMirrors(
        Vector2 center,
        Vector2 vehicleSize,
        float rotationRadians)
    {
        float scale = SimulationUnits.PixelsPerMeter;
        Vector2 forward = VehicleMath.Forward(rotationRadians);
        Vector2 right = VehicleMath.Right(rotationRadians);
        Vector2 cabinPosition = center + forward * vehicleSize.Y * 0.20f;
        float rotationDegrees = rotationRadians * 180.0f / MathF.PI;
        Vector2 mirrorSize = new(0.30f * scale, 0.18f * scale);
        Color mirrorBlack = new(16, 18, 18, 255);
        Color highlight = new(48, 52, 51, 255);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            Vector2 bodyEdge = cabinPosition + right * side * vehicleSize.X * 0.48f;
            Vector2 mirrorCenter = cabinPosition + right * side * (vehicleSize.X * 0.5f + mirrorSize.X * 0.42f);
            Raylib.DrawLineEx(bodyEdge, mirrorCenter, 0.08f * scale, mirrorBlack);
            Raylib.DrawRectanglePro(
                new Rectangle(mirrorCenter.X, mirrorCenter.Y, mirrorSize.X, mirrorSize.Y),
                mirrorSize * 0.5f,
                rotationDegrees,
                mirrorBlack);
            Raylib.DrawLineEx(
                mirrorCenter - forward * mirrorSize.Y * 0.28f,
                mirrorCenter + forward * mirrorSize.Y * 0.28f,
                0.07f * scale,
                highlight);
        }
    }

    private static void DrawMissionBriefing(float opacity)
    {
        const int panelX = 18;
        const int panelY = 18;
        const int panelWidth = 510;
        const int panelHeight = 148;
        const int radioX = panelX + 20;
        const int radioY = panelY + 25;
        const int textX = panelX + 122;

        Color panel = WithOpacity(new Color(10, 14, 13, 188), opacity);
        Color radioBody = WithOpacity(new Color(59, 67, 61, 255), opacity);
        Color radioEdge = WithOpacity(new Color(132, 142, 134, 255), opacity);
        Color radioDark = WithOpacity(new Color(20, 25, 22, 255), opacity);
        Color display = WithOpacity(new Color(132, 174, 137, 255), opacity);
        Color heading = WithOpacity(new Color(235, 151, 66, 255), opacity);
        Color text = WithOpacity(new Color(225, 229, 222, 255), opacity);

        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, panel);
        Raylib.DrawRectangleLines(panelX, panelY, panelWidth, panelHeight, WithOpacity(new Color(82, 91, 85, 210), opacity));

        // The asymmetrical silhouette and visible controls keep the small icon readable as a radio.
        Raylib.DrawLineEx(new Vector2(radioX + 18, radioY + 5), new Vector2(radioX + 10, radioY - 20), 5.0f, radioDark);
        Raylib.DrawCircle(radioX + 70, radioY + 2, 7.0f, radioDark);
        Raylib.DrawRectangle(radioX + 4, radioY, 74, 103, radioBody);
        Raylib.DrawRectangleLines(radioX + 4, radioY, 74, 103, radioEdge);
        Raylib.DrawRectangle(radioX + 16, radioY + 13, 50, 22, radioDark);
        Raylib.DrawRectangle(radioX + 21, radioY + 18, 40, 12, display);
        for (int speakerLine = 0; speakerLine < 4; speakerLine++)
        {
            int lineY = radioY + 48 + speakerLine * 8;
            Raylib.DrawLine(radioX + 18, lineY, radioX + 64, lineY, radioDark);
        }

        Raylib.DrawCircle(radioX + 26, radioY + 89, 7.0f, radioDark);
        Raylib.DrawCircle(radioX + 56, radioY + 89, 7.0f, radioDark);

        Raylib.DrawText("MISSION", textX, panelY + 17, 20, heading);
        Raylib.DrawText(MissionLineOne, textX, panelY + 49, 17, text);
        Raylib.DrawText(MissionLineTwo, textX, panelY + 73, 17, text);
        Raylib.DrawText(MissionLineThree, textX, panelY + 105, 17, heading);
    }

    private void DrawGameOverOverlay(RunStatistics run)
    {
        float opacity = run.OverlayOpacity;
        if (opacity <= 0.0f)
        {
            return;
        }

        int panelAlpha = (int)(145.0f * opacity);
        int textAlpha = (int)(255.0f * opacity);

        const int panelWidth = 760;
        const int panelHeight = 690;
        int panelX = (screenWidth - panelWidth) / 2;
        int panelY = (screenHeight - panelHeight) / 2;
        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(15, 20, 19, panelAlpha));

        Color primary = new(245, 239, 221, textAlpha);
        Color secondary = new(194, 201, 195, textAlpha);
        Color accent = new(235, 151, 66, textAlpha);
        DrawCenteredText("GAME OVER", panelY + 18, 38, accent);
        string reason = run.EndReason == GameOverReason.Exploded
            ? "VEHICLE EXPLODED"
            : "VEHICLE DISABLED";
        DrawCenteredText(reason, panelY + 63, 19, secondary);

        ScoreAwards score = run.Breakdown;
        (string Label, int Count, int Value)[] rows =
        [
            ("Civilians injured", score.CivilianInjuries, RunStatistics.InjuredPersonPoints),
            ("Civilians killed", score.CivilianKills, RunStatistics.KilledPersonPoints),
            ("Civilians exploded", score.CivilianExplosions, RunStatistics.ExplodedPersonPoints),
            ("Soldiers injured", score.SoldierInjuries, RunStatistics.InjuredPersonPoints),
            ("Soldiers killed", score.SoldierKills, RunStatistics.KilledPersonPoints),
            ("Soldiers exploded", score.SoldierExplosions, RunStatistics.ExplodedPersonPoints),
            ("Vehicles disabled", score.DisabledVehicles, RunStatistics.DisabledVehiclePoints),
            ("Vehicles destroyed", score.DestroyedVehicles, RunStatistics.DestroyedVehiclePoints),
            ("Structures damaged", score.DamagedStructures, RunStatistics.DamagedStructurePoints),
            ("Structures destroyed", score.DestroyedStructures, RunStatistics.DestroyedStructurePoints),
            ("Other scenery destroyed", score.DestroyedScenery, RunStatistics.DestroyedSceneryPoints),
        ];

        int eventX = panelX + 42;
        int countRightX = panelX + 430;
        int valueRightX = panelX + 555;
        int subtotalRightX = panelX + 710;
        int headerY = panelY + 96;
        Raylib.DrawText("EVENT", eventX, headerY, 16, secondary);
        DrawRightAlignedText("COUNT", countRightX, headerY, 16, secondary);
        DrawRightAlignedText("EACH", valueRightX, headerY, 16, secondary);
        DrawRightAlignedText("POINTS", subtotalRightX, headerY, 16, secondary);
        Raylib.DrawLine(panelX + 36, headerY + 22, panelX + panelWidth - 36, headerY + 22, new Color(100, 111, 105, textAlpha));

        int rowY = headerY + 30;
        foreach ((string label, int count, int value) in rows)
        {
            Raylib.DrawText(label, eventX, rowY, 17, primary);
            DrawRightAlignedText(count.ToString(), countRightX, rowY, 17, primary);
            DrawRightAlignedText($"{value:N0}", valueRightX, rowY, 17, secondary);
            DrawRightAlignedText($"{count * value:N0}", subtotalRightX, rowY, 17, primary);
            rowY += 23;
        }

        Raylib.DrawLine(panelX + 36, rowY + 1, panelX + panelWidth - 36, rowY + 1, new Color(100, 111, 105, textAlpha));
        DrawCenteredText($"TOTAL SCORE  {run.TotalScore:N0}", rowY + 17, 29, accent);
        DrawCenteredText($"DAMAGE TAKEN  {run.DamageTakenPercent:0}%", rowY + 62, 18, secondary);
        DrawCenteredText($"TOP SPEED  {run.TopSpeedKilometersPerHour:0} km/h", rowY + 90, 18, secondary);
        DrawCenteredText($"RUN TIME  {FormatTime(run.ElapsedSeconds)}", rowY + 118, 18, secondary);
        DrawCenteredText("PRESS R TO RESET", rowY + 158, 20, accent);
    }

    private static void DrawRightAlignedText(string text, int rightX, int y, int fontSize, Color color)
    {
        Raylib.DrawText(text, rightX - Raylib.MeasureText(text, fontSize), y, fontSize, color);
    }

    private void DrawCenteredText(string text, int y, int fontSize, Color color)
    {
        int x = (screenWidth - Raylib.MeasureText(text, fontSize)) / 2;
        Raylib.DrawText(text, x, y, fontSize, color);
    }

    private static Color WithOpacity(Color color, float opacity) =>
        new(color.R, color.G, color.B, (int)(color.A * Math.Clamp(opacity, 0.0f, 1.0f)));

    internal static string FormatTime(float seconds)
    {
        int totalTenths = Math.Max(0, (int)MathF.Floor(seconds * 10.0f));
        int minutes = totalTenths / 600;
        int wholeSeconds = totalTenths / 10 % 60;
        int tenths = totalTenths % 10;
        return $"{minutes:00}:{wholeSeconds:00}.{tenths}";
    }
}
