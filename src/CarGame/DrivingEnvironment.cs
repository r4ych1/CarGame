using System.Numerics;
using Box2D.NET;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;

namespace CarGame;

internal enum SceneryKind { Tree, Bush, Rock, Building, Shack, ParkedCar, ParkedTruck }
internal enum PhysicsPropKind { TrashCan, GuardRail, TrafficCone, Crate }
internal readonly record struct WallSegment(Vector2 PositionMeters, Vector2 SizeMeters);

internal static class BlastPhysics
{
    public static float LaunchSpeed(float distanceMeters, float radiusMeters, float edgeSpeed, float centerSpeed)
    {
        float proximity = Math.Clamp(1.0f - (distanceMeters / radiusMeters), 0.0f, 1.0f);
        float falloff = MathF.Pow(proximity, 1.6f);
        return edgeSpeed + (centerSpeed - edgeSpeed) * falloff;
    }
}

internal sealed class PhysicsProp
{
    private readonly B2BodyId bodyId;

    public PhysicsProp(
        B2WorldId worldId,
        PhysicsPropKind kind,
        Vector2 position,
        Vector2 size,
        float rotation,
        bool isAnchored = false)
    {
        Kind = kind;
        SizeMeters = size;
        IsAnchored = isAnchored;
        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = isAnchored ? B2BodyType.b2_staticBody : B2BodyType.b2_dynamicBody;
        bodyDefinition.position = new B2Vec2(position.X, position.Y);
        bodyDefinition.rotation = Box2D.NET.B2MathFunction.b2MakeRot(rotation);
        bodyDefinition.enableSleep = false;
        bodyId = b2CreateBody(worldId, bodyDefinition);
        if (!isAnchored)
        {
            b2Body_SetBullet(bodyId, true);
        }

        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        shapeDefinition.density = kind switch
        {
            PhysicsPropKind.TrafficCone => 2.0f,
            PhysicsPropKind.TrashCan => 45.0f,
            PhysicsPropKind.Crate => 90.0f,
            _ => 180.0f,
        };
        shapeDefinition.material.friction = kind == PhysicsPropKind.TrafficCone ? 0.45f : 0.7f;
        shapeDefinition.material.restitution = kind switch
        {
            PhysicsPropKind.TrafficCone => 0.48f,
            PhysicsPropKind.TrashCan => 0.28f,
            PhysicsPropKind.Crate => 0.12f,
            _ => 0.08f,
        };
        b2CreatePolygonShape(bodyId, shapeDefinition, b2MakeBox(size.X / 2.0f, size.Y / 2.0f));
        b2Body_SetLinearDamping(bodyId, 0.35f);
        b2Body_SetAngularDamping(bodyId, kind == PhysicsPropKind.GuardRail ? 8.0f : 0.55f);
    }

    public PhysicsPropKind Kind { get; }
    public Vector2 SizeMeters { get; }
    public bool IsAnchored { get; }
    public float MassKilograms => b2Body_GetMass(bodyId);
    public Vector2 PositionMeters { get { B2Vec2 p = b2Body_GetPosition(bodyId); return new(p.X, p.Y); } }
    public Vector2 LinearVelocityMetersPerSecond { get { B2Vec2 v = b2Body_GetLinearVelocity(bodyId); return new(v.X, v.Y); } }
    public float RotationRadians => Box2D.NET.B2MathFunction.b2Rot_GetAngle(b2Body_GetRotation(bodyId));
    public float AngularVelocityRadiansPerSecond => b2Body_GetAngularVelocity(bodyId);

    public void ApplyBlast(Vector2 center, float radius)
    {
        if (IsAnchored) return;

        Vector2 offset = PositionMeters - center;
        float distance = offset.Length();
        if (distance > radius) return;
        Vector2 direction = distance > 0.01f ? offset / distance : Vector2.UnitX;
        (float edgeSpeed, float centerSpeed) = Kind switch
        {
            PhysicsPropKind.TrafficCone => (15.0f, 100.0f),
            PhysicsPropKind.TrashCan => (12.0f, 77.0f),
            PhysicsPropKind.Crate => (10.0f, 62.0f),
            _ => (7.5f, 42.0f),
        };
        float deltaSpeed = BlastPhysics.LaunchSpeed(distance, radius, edgeSpeed, centerSpeed);
        float mass = b2Body_GetMass(bodyId);
        b2Body_ApplyLinearImpulseToCenter(bodyId, new B2Vec2(direction.X * deltaSpeed * mass, direction.Y * deltaSpeed * mass), true);
        float spinScale = Kind == PhysicsPropKind.GuardRail ? 0.03f : 1.0f;
        b2Body_ApplyAngularImpulse(bodyId, mass * (0.4f + distance * 0.025f) * spinScale, true);
    }

    public void Step(float timeStep)
    {
        if (Kind == PhysicsPropKind.GuardRail)
        {
            float angularVelocity = Math.Clamp(b2Body_GetAngularVelocity(bodyId), -0.65f, 0.65f);
            b2Body_SetAngularVelocity(bodyId, angularVelocity);
        }

        Vector2 velocity = LinearVelocityMetersPerSecond;
        float speed = velocity.Length();
        if (speed <= 0.0f) return;
        float next = MathF.Max(0.0f, speed - 4.2f * timeStep);
        velocity *= next / speed;
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(velocity.X, velocity.Y));
    }

    public void Destroy() => b2DestroyBody(bodyId);
}

internal readonly record struct BlastParticle(Vector2 OffsetMeters, Vector2 VelocityMetersPerSecond, float SizeMeters, float DelaySeconds);

internal sealed class BlastVisualEffect
{
    public BlastVisualEffect(Vector2 centerMeters)
    {
        CenterMeters = centerMeters;
        Smoke = CreateParticles(28, 31.0f, 2.4f, 0.7f);
        Fire = CreateParticles(18, 10.0f, 8.0f, 0.0f);
        GroundFire = CreateParticles(22, 30.0f, 0.0f, 1.0f);
        GroundSmoke = CreateParticles(25, 5.0f, 0.8f, 0.0f);
        Debris = CreateParticles(36, 5.0f, 22.0f, 0.0f);
    }

    public Vector2 CenterMeters { get; }
    public float AgeSeconds { get; private set; }
    public IReadOnlyList<BlastParticle> Smoke { get; }
    public IReadOnlyList<BlastParticle> Fire { get; }
    public IReadOnlyList<BlastParticle> GroundFire { get; }
    public IReadOnlyList<BlastParticle> GroundSmoke { get; }
    public IReadOnlyList<BlastParticle> Debris { get; }
    public bool IsFinished => AgeSeconds >= 12.0f;
    public void Step(float timeStep) => AgeSeconds += timeStep;

    private static BlastParticle[] CreateParticles(int count, float radius, float speed, float maximumDelay)
    {
        BlastParticle[] particles = new BlastParticle[count];
        const float goldenAngle = 2.3999632f;
        for (int index = 0; index < count; index++)
        {
            float ratio = (index + 0.5f) / count;
            float angle = index * goldenAngle;
            Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
            particles[index] = new(
                direction * radius * MathF.Sqrt(ratio),
                direction * speed * (0.55f + 0.45f * ratio),
                0.25f + (index % 5) * 0.13f,
                maximumDelay * ((index * 7) % count) / count);
        }
        return particles;
    }
}

internal sealed class SceneryObject
{
    public required SceneryKind Kind { get; init; }
    public required Vector2 PositionMeters { get; init; }
    public required Vector2 SizeMeters { get; init; }
    public required float RotationRadians { get; init; }
    public B2BodyId BodyId { get; set; }
    public bool HasBody { get; set; }
    public bool IsDestroyed { get; set; }
    public bool IsCracked { get; set; }
}

internal sealed class WorldDebris
{
    private readonly B2BodyId bodyId;

    public WorldDebris(
        B2WorldId worldId,
        SceneryKind kind,
        Vector2 position,
        Vector2 size,
        float rotation,
        Vector2 velocity,
        float spin,
        bool hasFireSmoke = false)
    {
        Kind = kind;
        SizeMeters = size;
        HasFireSmoke = hasFireSmoke;
        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_dynamicBody;
        bodyDefinition.position = new B2Vec2(position.X, position.Y);
        bodyDefinition.rotation = Box2D.NET.B2MathFunction.b2MakeRot(rotation);
        bodyDefinition.enableSleep = false;
        bodyId = b2CreateBody(worldId, bodyDefinition);
        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        shapeDefinition.density = 80.0f;
        b2CreatePolygonShape(bodyId, shapeDefinition, b2MakeBox(size.X / 2.0f, size.Y / 2.0f));
        b2Body_SetLinearDamping(bodyId, 0.45f);
        b2Body_SetAngularDamping(bodyId, kind == SceneryKind.Tree ? 5.0f : 0.8f);
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(velocity.X, velocity.Y));
        b2Body_SetAngularVelocity(bodyId, spin);
    }

    public SceneryKind Kind { get; }
    public Vector2 SizeMeters { get; }
    public bool HasFireSmoke { get; }
    public float AgeSeconds { get; private set; }
    public Vector2 PositionMeters { get { B2Vec2 p = b2Body_GetPosition(bodyId); return new(p.X, p.Y); } }
    public Vector2 LinearVelocityMetersPerSecond { get { B2Vec2 v = b2Body_GetLinearVelocity(bodyId); return new(v.X, v.Y); } }
    public float RotationRadians => Box2D.NET.B2MathFunction.b2Rot_GetAngle(b2Body_GetRotation(bodyId));
    public float AngularVelocityRadiansPerSecond => b2Body_GetAngularVelocity(bodyId);

    public void Step(float timeStep)
    {
        AgeSeconds += timeStep;
        if (Kind == SceneryKind.Tree)
        {
            b2Body_SetAngularVelocity(bodyId, Math.Clamp(b2Body_GetAngularVelocity(bodyId), -0.55f, 0.55f));
        }

        B2Vec2 value = b2Body_GetLinearVelocity(bodyId);
        Vector2 velocity = new(value.X, value.Y);
        float speed = velocity.Length();
        if (speed <= 0.0f) return;
        float next = MathF.Max(0.0f, speed - (5.0f * timeStep));
        velocity *= next / speed;
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(velocity.X, velocity.Y));
    }

    public void Destroy() => b2DestroyBody(bodyId);
}

internal sealed class DrivingEnvironment
{
    public const float BlastRadiusMeters = 32.0f;
    public const float DeadlyBlastRadiusMeters = 16.0f;
    private const float ExplosionPanicRadiusMeters = BlastRadiusMeters + 12.0f;
    public static readonly Vector2 BoundaryMinimumMeters = new(-80.0f, -380.0f);
    public static readonly Vector2 BoundaryMaximumMeters = new(80.0f, 70.0f);
    public const float StreamCenterYMeters = -120.0f;
    public const float StreamWidthMeters = 20.0f;
    public const float BridgeCenterXMeters = -2.0f;
    public const float BridgeWidthMeters = 12.0f;
    public static readonly Vector2 TownMinimumMeters = new(-48.0f, -332.0f);
    public static readonly Vector2 TownMaximumMeters = new(48.0f, -244.0f);
    public static readonly Vector2 TownEntranceCenterMeters = new(-13.0f, -244.0f);
    public static readonly Vector2 CheckpointSouthBarrierMeters = new(-20.1f, -209.0f);
    public static readonly Vector2 CheckpointNorthBarrierMeters = new(-14.1f, -230.0f);
    public static readonly Vector2 CheckpointShackMeters = new(-4.0f, -219.0f);
    private static readonly Vector2[] dirtRoadPointsMeters =
    [
        new(RoadCenterXMeters(-145.0f), -145.0f),
        new(6.0f, -150.0f),
        new(20.0f, -158.0f),
        new(35.0f, -168.0f),
        new(47.0f, -181.0f),
        new(52.0f, -196.0f),
        new(50.0f, -210.0f),
        new(42.0f, -222.0f),
        new(28.0f, -230.0f),
        new(10.0f, -234.0f),
        new(RoadCenterXMeters(-238.0f), -238.0f),
    ];
    private const float BoundaryThicknessMeters = 4.0f;
    private readonly B2WorldId worldId;
    private readonly List<SceneryObject> scenery = [];
    private readonly List<WorldDebris> debris = [];
    private readonly List<Vector2> craters = [];
    private readonly List<BlastVisualEffect> blastEffects = [];
    private readonly List<PhysicsProp> props = [];
    private readonly List<WallSegment> townWalls = [];
    private readonly List<AmbientCharacter> characters = [];

    public DrivingEnvironment(B2WorldId worldId)
    {
        this.worldId = worldId;
        CreateBoundaries();
        CreateTownWalls();
        Build();
        BuildProps();
        BuildCharacters();
        Traffic = new TrafficSystem(worldId);
    }

    public IReadOnlyList<SceneryObject> Scenery => scenery;
    public IReadOnlyList<WorldDebris> Debris => debris;
    public IReadOnlyList<Vector2> Craters => craters;
    public IReadOnlyList<BlastVisualEffect> BlastEffects => blastEffects;
    public IReadOnlyList<PhysicsProp> Props => props;
    public IReadOnlyList<WallSegment> TownWalls => townWalls;
    public IReadOnlyList<AmbientCharacter> Characters => characters;
    public static IReadOnlyList<Vector2> DirtRoadPointsMeters => dirtRoadPointsMeters;
    public TrafficSystem Traffic { get; }
    public float WaterFlowAgeSeconds { get; private set; }

    public void Step(float timeStep, Vector2? playerObstacleMeters = null)
    {
        WaterFlowAgeSeconds += timeStep;
        UpdateCharacterFear();
        foreach (AmbientCharacter character in characters) character.Step(timeStep);
        foreach (WorldDebris item in debris) item.Step(timeStep);
        foreach (BlastVisualEffect effect in blastEffects) effect.Step(timeStep);
        foreach (PhysicsProp prop in props) prop.Step(timeStep);
        List<TrafficObstacle> trafficObstacles = [];
        foreach (SceneryObject item in scenery)
        {
            if (!item.IsDestroyed && item.HasBody)
                trafficObstacles.Add(new(item.PositionMeters, MathF.Max(item.SizeMeters.X, item.SizeMeters.Y) * 0.5f));
        }
        foreach (PhysicsProp prop in props)
            if (!prop.IsAnchored && prop.Kind != PhysicsPropKind.GuardRail)
                trafficObstacles.Add(new(prop.PositionMeters, MathF.Max(prop.SizeMeters.X, prop.SizeMeters.Y) * 0.5f));
        if (playerObstacleMeters.HasValue)
            trafficObstacles.Add(new(playerObstacleMeters.Value, 1.5f));
        Traffic.Step(timeStep, trafficObstacles);
        blastEffects.RemoveAll(effect => effect.IsFinished);
    }

    public void PostPhysicsStep(Vehicle playerVehicle)
    {
        foreach (AmbientCharacter character in characters)
        {
            if (!playerVehicle.IsExploded && !playerVehicle.IsSunk)
            {
                character.TryFleeFromVehicle(
                    playerVehicle.PositionMeters,
                    playerVehicle.VelocityMetersPerSecond,
                    10.0f);
            }

            if (character.IsDead)
            {
                continue;
            }

            if (!playerVehicle.IsExploded && !playerVehicle.IsSunk && playerVehicle.SpeedMetersPerSecond > 2.0f &&
                character.IsOverlappedByVehicle(
                    playerVehicle.PositionMeters,
                    playerVehicle.RotationRadians,
                    new Vector2(
                        playerVehicle.Definition.Chassis.WidthMeters,
                        playerVehicle.Definition.Chassis.LengthMeters)))
            {
                character.Die();
                continue;
            }

            foreach (TrafficVehicle trafficVehicle in Traffic.Vehicles)
            {
                if (trafficVehicle.IsActive)
                {
                    character.TryFleeFromVehicle(
                        trafficVehicle.PositionMeters,
                        trafficVehicle.LinearVelocityMetersPerSecond,
                        10.0f);
                }

                if (trafficVehicle.IsActive && trafficVehicle.LinearVelocityMetersPerSecond.Length() > 2.0f &&
                    character.IsOverlappedByVehicle(
                        trafficVehicle.PositionMeters,
                        trafficVehicle.RotationRadians,
                        trafficVehicle.SizeMeters))
                {
                    character.Die();
                    break;
                }
            }
        }

        Traffic.PostPhysicsStep();
    }

    public void Explode(Vector2 center)
    {
        craters.Add(center);
        blastEffects.Add(new(center));
        foreach (AmbientCharacter character in characters)
        {
            character.ReactToExplosion(center, ExplosionPanicRadiusMeters);
            character.ApplyBlast(center, BlastRadiusMeters, DeadlyBlastRadiusMeters);
        }

        foreach (PhysicsProp prop in props) prop.ApplyBlast(center, BlastRadiusMeters);
        Traffic.ApplyBlast(center, BlastRadiusMeters);
        foreach (SceneryObject item in scenery)
        {
            if (item.IsDestroyed) continue;
            Vector2 offset = item.PositionMeters - center;
            float distance = offset.Length();
            if (distance > BlastRadiusMeters) continue;
            if (item.Kind is SceneryKind.Building or SceneryKind.Shack && distance > DeadlyBlastRadiusMeters)
            {
                item.IsCracked = true;
                continue;
            }

            item.IsDestroyed = true;
            if (item.HasBody) { b2DestroyBody(item.BodyId); item.HasBody = false; }
            if (item.Kind == SceneryKind.Bush) continue;
            Vector2 direction = distance > 0.01f ? offset / distance : Vector2.UnitX;
            if (item.Kind is SceneryKind.Building or SceneryKind.Shack)
            {
                CreateBuildingDebris(item, center, direction);
                continue;
            }

            (int columns, int rows) = item.Kind switch
            {
                SceneryKind.ParkedTruck or SceneryKind.ParkedCar => (2, 2),
                _ => (1, 1),
            };
            int count = columns * rows;
            Vector2 fragmentSize = item.Kind == SceneryKind.Tree
                ? new Vector2(0.7f, 5.5f)
                : item.SizeMeters * new Vector2(0.86f / columns, 0.86f / rows);
            for (int index = 0; index < count; index++)
            {
                int column = index % columns;
                int row = index / columns;
                Vector2 localOffset = columns == 1 && rows == 1
                    ? Vector2.Zero
                    : new Vector2(
                        ((column + 0.5f) / columns - 0.5f) * item.SizeMeters.X,
                        ((row + 0.5f) / rows - 0.5f) * item.SizeMeters.Y);
                Vector2 position = item.PositionMeters + Vector2.Transform(localOffset, Matrix3x2.CreateRotation(item.RotationRadians));
                Vector2 fragmentOffset = position - center;
                float fragmentDistance = fragmentOffset.Length();
                Vector2 fragmentDirection = fragmentDistance > 0.01f ? fragmentOffset / fragmentDistance : direction;
                float edgeSpeed = item.Kind == SceneryKind.Tree ? 8.0f : 4.0f;
                float centerSpeed = item.Kind == SceneryKind.Tree ? 70.0f : 56.0f;
                float speed = BlastPhysics.LaunchSpeed(fragmentDistance, BlastRadiusMeters, edgeSpeed, centerSpeed);
                float spin = item.Kind == SceneryKind.Tree ? 0.35f : ((column - (columns - 1) * 0.5f) + (row - (rows - 1) * 0.5f)) * 1.4f;
                debris.Add(new(worldId, item.Kind, position, fragmentSize, item.RotationRadians, fragmentDirection * speed, spin));
            }
        }
    }

    private void UpdateCharacterFear()
    {
        for (int observerIndex = 0; observerIndex < characters.Count; observerIndex++)
        {
            AmbientCharacter observer = characters[observerIndex];
            if (observer.IsDead) continue;

            for (int corpseIndex = 0; corpseIndex < characters.Count; corpseIndex++)
            {
                AmbientCharacter corpse = characters[corpseIndex];
                if (!corpse.IsDead || observerIndex == corpseIndex) continue;

                if (corpse.HasExplodedIntoLimbs)
                {
                    foreach (CharacterLimb limb in corpse.LimbFragments)
                    {
                        observer.TryFleeFromCorpse(limb.PositionMeters, 8.0f);
                        if (observer.IsPanicking) break;
                    }
                }
                else
                {
                    observer.TryFleeFromCorpse(corpse.PositionMeters, 8.0f);
                }

                if (observer.IsPanicking) break;
            }
        }
    }

    private void CreateBuildingDebris(SceneryObject building, Vector2 blastCenter, Vector2 fallbackDirection)
    {
        bool isLargeBuilding = building.Kind == SceneryKind.Building &&
            building.SizeMeters.X * building.SizeMeters.Y >= 100.0f;
        int largeFragmentCount = isLargeBuilding ? 24 : building.Kind == SceneryKind.Building ? 18 : 12;
        const int smallFragmentCount = 24;
        uint randomState = unchecked(
            (uint)(MathF.Abs(building.PositionMeters.X) * 1009.0f + MathF.Abs(building.PositionMeters.Y) * 9176.0f +
                building.SizeMeters.X * 6113.0f + building.SizeMeters.Y * 3571.0f));
        if (randomState == 0) randomState = 0xA341316Cu;

        for (int index = 0; index < largeFragmentCount + smallFragmentCount; index++)
        {
            bool isSmallFragment = index >= largeFragmentCount;
            float sizeX;
            float sizeY;
            if (isSmallFragment)
            {
                sizeX = 0.18f + NextDeterministicUnit(ref randomState) * 0.48f;
                sizeY = 0.18f + NextDeterministicUnit(ref randomState) * 0.48f;
            }
            else
            {
                float widthRatio = 0.12f + NextDeterministicUnit(ref randomState) * 0.18f;
                float lengthRatio = 0.12f + NextDeterministicUnit(ref randomState) * 0.18f;
                sizeX = MathF.Min(building.SizeMeters.X * widthRatio, building.SizeMeters.X * 0.32f);
                sizeY = MathF.Min(building.SizeMeters.Y * lengthRatio, building.SizeMeters.Y * 0.32f);
            }

            Vector2 fragmentSize = new(
                MathF.Min(sizeX, building.SizeMeters.X),
                MathF.Min(sizeY, building.SizeMeters.Y));
            Vector2 localPosition = new(
                (NextDeterministicUnit(ref randomState) - 0.5f) * (building.SizeMeters.X - fragmentSize.X),
                (NextDeterministicUnit(ref randomState) - 0.5f) * (building.SizeMeters.Y - fragmentSize.Y));
            Vector2 position = building.PositionMeters +
                Vector2.Transform(localPosition, Matrix3x2.CreateRotation(building.RotationRadians));
            Vector2 outwardOffset = position - blastCenter;
            float fragmentDistance = outwardOffset.Length();
            Vector2 direction = fragmentDistance > 0.01f ? outwardOffset / fragmentDistance : fallbackDirection;
            float speed = BlastPhysics.LaunchSpeed(fragmentDistance, BlastRadiusMeters, 4.0f, 56.0f);
            float spin = (NextDeterministicUnit(ref randomState) - 0.5f) * (isSmallFragment ? 9.0f : 5.0f);
            bool hasFireSmoke = isSmallFragment ? index % 8 == 0 : index % 4 == 1;
            debris.Add(new(
                worldId,
                building.Kind,
                position,
                fragmentSize,
                building.RotationRadians,
                direction * speed,
                spin,
                hasFireSmoke));
        }
    }

    private static float NextDeterministicUnit(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state >> 8) * (1.0f / 16777216.0f);
    }

    public void Reset()
    {
        foreach (SceneryObject item in scenery) if (item.HasBody) b2DestroyBody(item.BodyId);
        foreach (WorldDebris item in debris) item.Destroy();
        foreach (PhysicsProp prop in props) prop.Destroy();
        foreach (AmbientCharacter character in characters) character.Destroy();
        scenery.Clear(); debris.Clear(); craters.Clear(); blastEffects.Clear(); props.Clear(); characters.Clear(); Build(); BuildProps(); BuildCharacters();
        WaterFlowAgeSeconds = 0.0f;
        Traffic.Reset();
    }

    public bool IsDeepWater(Vector2 positionMeters)
    {
        float deepHalfWidth = (StreamWidthMeters * 0.5f) - 3.0f;
        bool insideDeepWater = MathF.Abs(positionMeters.Y - StreamCenterYMeters) <= deepHalfWidth;
        bool onBridge = MathF.Abs(positionMeters.X - BridgeCenterXMeters) <= BridgeWidthMeters * 0.5f;
        return insideDeepWater && !onBridge;
    }

    public static float RoadCenterXMeters(float yMeters)
    {
        float sampleIndex = (60.0f - yMeters) / 10.0f;
        float curvedCenter = MathF.Sin(sampleIndex * 0.18f) * 18.0f;
        float distanceFromStream = MathF.Abs(yMeters - StreamCenterYMeters);
        if (distanceFromStream <= 16.0f)
        {
            return BridgeCenterXMeters;
        }

        if (distanceFromStream >= 32.0f)
        {
            return curvedCenter;
        }

        float transition = (distanceFromStream - 16.0f) / 16.0f;
        transition = transition * transition * (3.0f - (2.0f * transition));
        return BridgeCenterXMeters + ((curvedCenter - BridgeCenterXMeters) * transition);
    }

    private void Build()
    {
        // Rural approach: vegetation and sparse structures along the curving road.
        for (int index = 0; index < 18; index++)
        {
            float y = -20.0f - (index * 13.0f);
            float vegetationExclusion = (StreamWidthMeters * 0.5f) + 4.0f;
            if (MathF.Abs(y - StreamCenterYMeters) <= vegetationExclusion ||
                MathF.Abs((y + 4.0f) - StreamCenterYMeters) <= vegetationExclusion)
                continue;
            float roadX = RoadCenterXMeters(y);
            Add(index % 3 == 0 ? SceneryKind.Rock : SceneryKind.Tree, new(roadX - 24.0f - (index % 4) * 3.0f, y), index % 3 == 0 ? new(2.4f, 2.0f) : new(2.2f, 2.2f));
            Add(SceneryKind.Bush, new(roadX + 15.0f + (index % 5), y + 4.0f), new(2.5f, 2.0f));
        }
        Add(SceneryKind.Building, new(-32.0f, -105.0f), new(12.0f, 8.0f));
        Add(SceneryKind.Building, new(-43.0f, -165.0f), new(10.0f, 9.0f));
        Add(SceneryKind.Shack, new(-38.0f, -72.0f), new(6.0f, 5.0f));
        Add(SceneryKind.Shack, new(-42.0f, -184.0f), new(5.5f, 4.5f));
        Add(SceneryKind.ParkedCar, new(24.0f, -92.0f), new(2.0f, 4.5f));
        Add(SceneryKind.ParkedCar, new(-31.0f, -153.0f), new(2.0f, 4.5f));
        Add(SceneryKind.ParkedCar, new(31.0f, -251.0f), new(2.0f, 4.5f));
        Add(SceneryKind.ParkedTruck, new(-35.0f, -190.0f), new(2.6f, 7.5f));
        Add(SceneryKind.ParkedTruck, new(-43.0f, -225.0f), new(2.6f, 7.5f));
        Add(SceneryKind.Shack, CheckpointShackMeters, new(5.5f, 4.5f));

        // Town rows around the north-south main street.
        for (int row = 0; row < 4; row++)
            for (int side = -1; side <= 1; side += 2)
            {
                Add(SceneryKind.Building, new(side * 18.0f, -265.0f - row * 16.0f), new(12.0f, 10.0f));
                Add(SceneryKind.Tree, new(side * 29.0f, -265.0f - row * 16.0f), new(2.4f, 2.4f));
            }
    }

    private void CreateBoundaries()
    {
        float width = BoundaryMaximumMeters.X - BoundaryMinimumMeters.X;
        float height = BoundaryMaximumMeters.Y - BoundaryMinimumMeters.Y;
        float centerX = (BoundaryMinimumMeters.X + BoundaryMaximumMeters.X) * 0.5f;
        float centerY = (BoundaryMinimumMeters.Y + BoundaryMaximumMeters.Y) * 0.5f;
        CreateBoundary(new(centerX, BoundaryMinimumMeters.Y), new(width, BoundaryThicknessMeters));
        CreateBoundary(new(centerX, BoundaryMaximumMeters.Y), new(width, BoundaryThicknessMeters));
        CreateBoundary(new(BoundaryMinimumMeters.X, centerY), new(BoundaryThicknessMeters, height));
        CreateBoundary(new(BoundaryMaximumMeters.X, centerY), new(BoundaryThicknessMeters, height));
    }

    private void CreateTownWalls()
    {
        const float thickness = 2.5f;
        const float entranceWidth = 8.0f;
        float width = TownMaximumMeters.X - TownMinimumMeters.X;
        float height = TownMaximumMeters.Y - TownMinimumMeters.Y;
        float centerX = (TownMinimumMeters.X + TownMaximumMeters.X) * 0.5f;
        float centerY = (TownMinimumMeters.Y + TownMaximumMeters.Y) * 0.5f;
        float entranceLeft = TownEntranceCenterMeters.X - entranceWidth * 0.5f;
        float entranceRight = TownEntranceCenterMeters.X + entranceWidth * 0.5f;

        AddTownWall(new(centerX, TownMinimumMeters.Y), new(width, thickness));
        AddTownWall(new(TownMinimumMeters.X, centerY), new(thickness, height));
        AddTownWall(new(TownMaximumMeters.X, centerY), new(thickness, height));
        AddTownWall(
            new((TownMinimumMeters.X + entranceLeft) * 0.5f, TownMaximumMeters.Y),
            new(entranceLeft - TownMinimumMeters.X, thickness));
        AddTownWall(
            new((entranceRight + TownMaximumMeters.X) * 0.5f, TownMaximumMeters.Y),
            new(TownMaximumMeters.X - entranceRight, thickness));
    }

    private void AddTownWall(Vector2 position, Vector2 size)
    {
        townWalls.Add(new(position, size));
        CreateBoundary(position, size);
    }

    private void BuildProps()
    {
        AddProp(PhysicsPropKind.GuardRail, TownEntranceCenterMeters, new(7.5f, 0.45f), 0.0f);
        AddProp(PhysicsPropKind.GuardRail, CheckpointSouthBarrierMeters, new(5.0f, 0.45f), 0.0f);
        AddProp(PhysicsPropKind.GuardRail, CheckpointNorthBarrierMeters, new(5.0f, 0.45f), 0.0f);

        const int bridgeRailSectionsPerSide = 4;
        const float bridgeRailLengthMeters = 5.0f;
        float bridgeEdgeOffset = (BridgeWidthMeters * 0.5f) - 0.3f;
        float bridgeRailStartY = StreamCenterYMeters - (StreamWidthMeters * 0.5f) +
            (bridgeRailLengthMeters * 0.5f);
        for (int section = 0; section < bridgeRailSectionsPerSide; section++)
        {
            float y = bridgeRailStartY + section * bridgeRailLengthMeters;
            AddProp(
                PhysicsPropKind.GuardRail,
                new(BridgeCenterXMeters - bridgeEdgeOffset, y),
                new(bridgeRailLengthMeters, 0.45f),
                MathF.PI * 0.5f,
                true);
            AddProp(
                PhysicsPropKind.GuardRail,
                new(BridgeCenterXMeters + bridgeEdgeOffset, y),
                new(bridgeRailLengthMeters, 0.45f),
                MathF.PI * 0.5f,
                true);
        }

        // Rural clutter remains well off the pavement.
        for (int index = 0; index < 8; index++)
        {
            float y = 22.0f - index * 27.0f;
            float roadX = MathF.Sin(index * 0.36f) * 18.0f;
            AddProp(PhysicsPropKind.Crate, new(roadX - 15.0f, y), new(1.15f, 1.15f), index * 0.17f);
            AddProp(PhysicsPropKind.TrashCan, new(roadX + 15.0f, y + 3.0f), new(0.9f, 1.1f), index * -0.11f);
        }

        // Cones mark only the checkpoint edges and the short final approach.
        AddProp(PhysicsPropKind.TrafficCone, new(-25.0f, -207.0f), new(0.55f, 0.55f), 0.0f);
        AddProp(PhysicsPropKind.TrafficCone, new(-5.0f, -207.0f), new(0.55f, 0.55f), 0.1f);
        AddProp(PhysicsPropKind.TrafficCone, new(-26.0f, -218.5f), new(0.55f, 0.55f), -0.1f);
        AddProp(PhysicsPropKind.TrafficCone, new(-7.0f, -218.5f), new(0.55f, 0.55f), 0.05f);
        AddProp(PhysicsPropKind.TrafficCone, new(-26.0f, -230.0f), new(0.55f, 0.55f), 0.0f);
        AddProp(PhysicsPropKind.TrafficCone, new(-6.0f, -230.0f), new(0.55f, 0.55f), 0.08f);
        AddProp(PhysicsPropKind.TrafficCone, new(-27.0f, -240.0f), new(0.55f, 0.55f), -0.08f);
        AddProp(PhysicsPropKind.TrafficCone, new(5.0f, -240.0f), new(0.55f, 0.55f), 0.0f);

        // Outskirts and town service objects.
        for (int row = 0; row < 6; row++)
        {
            float y = -255.0f - row * 17.0f;
            AddProp(PhysicsPropKind.TrashCan, new(-11.0f, y), new(0.9f, 1.1f), 0.08f * row);
            AddProp(PhysicsPropKind.TrashCan, new(12.0f, y + 3.0f), new(0.9f, 1.1f), -0.06f * row);
            AddProp(PhysicsPropKind.Crate, new(-35.0f, y + 2.0f), new(1.25f, 1.25f), 0.12f * row);
            AddProp(PhysicsPropKind.Crate, new(34.0f, y - 3.0f), new(1.1f, 1.1f), -0.1f * row);
        }
    }

    private void BuildCharacters()
    {
        ColorRgb[] clothingColors =
        [
            new(54, 105, 145),
            new(171, 91, 67),
            new(93, 133, 82),
            new(194, 160, 88),
            new(105, 87, 139),
            new(66, 128, 120),
            new(161, 125, 91),
            new(174, 177, 168),
        ];
        ColorRgb[] skinColors =
        [
            new(231, 190, 149),
            new(194, 139, 104),
            new(139, 91, 67),
            new(239, 207, 174),
        ];
        int characterIndex = 0;

        void AddResident(Vector2 position, float heading, CharacterActivity activity)
        {
            characters.Add(new AmbientCharacter(
                worldId,
                activity,
                position,
                heading,
                clothingColors[characterIndex % clothingColors.Length],
                skinColors[(characterIndex * 3) % skinColors.Length],
                characterIndex * 0.73f));
            characterIndex++;
        }

        void AddWalker(Vector2[] path)
        {
            characters.Add(new AmbientCharacter(
                worldId,
                CharacterActivity.Walking,
                path[0],
                0.0f,
                clothingColors[characterIndex % clothingColors.Length],
                skinColors[(characterIndex * 3) % skinColors.Length],
                characterIndex * 0.61f,
                1.5f + (characterIndex % 4) * 0.27f,
                path));
            characterIndex++;
        }

        float[] roadsideY = [-40.0f, -61.0f, -82.0f, -143.0f, -168.0f, -193.0f];
        for (int index = 0; index < roadsideY.Length; index++)
        {
            float y = roadsideY[index];
            float roadX = RoadCenterXMeters(y);
            float side = index % 2 == 0 ? 1.0f : -1.0f;
            float shoulderX = roadX + side * 7.5f;
            AddResident(new(shoulderX, y), side > 0.0f ? MathF.PI : 0.0f,
                index % 2 == 0 ? CharacterActivity.Standing : CharacterActivity.Working);
            AddWalker(
            [
                new(roadX - side * 7.5f, y - 2.2f),
                new(roadX - side * 8.5f, y),
                new(roadX - side * 7.5f, y + 2.2f),
            ]);
        }

        AddResident(new(-26.0f, -99.0f), 0.0f, CharacterActivity.Standing);
        AddResident(new(-26.0f, -111.0f), MathF.PI * 0.5f, CharacterActivity.Working);
        AddResident(new(-34.0f, -68.0f), MathF.PI, CharacterActivity.Standing);
        AddResident(new(-34.0f, -76.0f), 0.0f, CharacterActivity.Talking);
        AddResident(new(-36.0f, -160.0f), 0.0f, CharacterActivity.Standing);
        AddResident(new(-36.0f, -170.0f), MathF.PI, CharacterActivity.Working);
        AddResident(new(-35.0f, -181.0f), 0.0f, CharacterActivity.Talking);
        AddResident(new(-35.0f, -188.0f), MathF.PI, CharacterActivity.Standing);

        for (int row = 0; row < 4; row++)
        {
            float y = -265.0f - row * 16.0f;
            float northSide = y - 5.8f;
            float southSide = y + 5.8f;
            AddResident(new(-27.0f, northSide), MathF.PI * 0.5f, CharacterActivity.Standing);
            AddResident(new(-9.0f, northSide), 0.0f, CharacterActivity.Working);
            AddResident(new(9.0f, southSide), MathF.PI, CharacterActivity.Standing);
            AddResident(new(27.0f, southSide), -MathF.PI * 0.5f, CharacterActivity.Working);
            AddResident(new(-27.0f, southSide), MathF.PI * 0.5f, CharacterActivity.Standing);
            AddResident(new(27.0f, northSide), -MathF.PI * 0.5f, CharacterActivity.Standing);

            if (row % 2 == 0)
            {
                float gatheringY = y + 2.0f;
                AddResident(new(-5.0f, gatheringY), 0.0f, CharacterActivity.Talking);
                AddResident(new(-3.5f, gatheringY), MathF.PI, CharacterActivity.Talking);
            }

            AddWalker(
            [
                new(-9.5f, y - 7.0f),
                new(-8.0f, y - 7.0f),
                new(-8.0f, y + 7.0f),
                new(-9.5f, y + 7.0f),
            ]);
            AddWalker(
            [
                new(8.0f, y + 7.0f),
                new(9.5f, y + 7.0f),
                new(9.5f, y - 7.0f),
                new(8.0f, y - 7.0f),
            ]);
            AddWalker(
            [
                new(-9.0f, y - 1.0f),
                new(9.0f, y - 1.0f),
                new(9.0f, y + 1.0f),
                new(-9.0f, y + 1.0f),
            ]);
        }
    }

    private void AddProp(
        PhysicsPropKind kind,
        Vector2 position,
        Vector2 size,
        float rotation,
        bool isAnchored = false) =>
        props.Add(new PhysicsProp(worldId, kind, position, size, rotation, isAnchored));

    private void CreateBoundary(Vector2 position, Vector2 size)
    {
        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_staticBody;
        bodyDefinition.position = new B2Vec2(position.X, position.Y);
        B2BodyId boundaryBody = b2CreateBody(worldId, bodyDefinition);
        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        b2CreatePolygonShape(boundaryBody, shapeDefinition, b2MakeBox(size.X / 2.0f, size.Y / 2.0f));
    }

    private void Add(SceneryKind kind, Vector2 position, Vector2 size)
    {
        SceneryObject item = new() { Kind = kind, PositionMeters = position, SizeMeters = size, RotationRadians = 0.0f };
        if (kind != SceneryKind.Bush)
        {
            B2BodyDef bodyDefinition = b2DefaultBodyDef();
            bodyDefinition.type = B2BodyType.b2_staticBody;
            bodyDefinition.position = new B2Vec2(position.X, position.Y);
            item.BodyId = b2CreateBody(worldId, bodyDefinition);
            B2ShapeDef shapeDefinition = b2DefaultShapeDef();
            b2CreatePolygonShape(item.BodyId, shapeDefinition, b2MakeBox(size.X / 2.0f, size.Y / 2.0f));
            item.HasBody = true;
        }
        scenery.Add(item);
    }
}
