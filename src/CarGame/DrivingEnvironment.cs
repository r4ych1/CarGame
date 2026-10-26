using System.Numerics;
using Box2D.NET;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;

namespace CarGame;

internal enum SceneryKind { Tree, Bush, Rock, Building, Shack }
internal enum PhysicsPropKind { TrashCan, GuardRail, TrafficCone, Crate }
internal readonly record struct WallSegment(Vector2 PositionMeters, Vector2 SizeMeters);

internal sealed class PhysicsProp
{
    private readonly B2BodyId bodyId;

    public PhysicsProp(B2WorldId worldId, PhysicsPropKind kind, Vector2 position, Vector2 size, float rotation)
    {
        Kind = kind;
        SizeMeters = size;
        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_dynamicBody;
        bodyDefinition.position = new B2Vec2(position.X, position.Y);
        bodyDefinition.rotation = Box2D.NET.B2MathFunction.b2MakeRot(rotation);
        bodyDefinition.enableSleep = false;
        bodyId = b2CreateBody(worldId, bodyDefinition);
        b2Body_SetBullet(bodyId, true);

        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        shapeDefinition.density = kind switch
        {
            PhysicsPropKind.TrafficCone => 12.0f,
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
    public Vector2 PositionMeters { get { B2Vec2 p = b2Body_GetPosition(bodyId); return new(p.X, p.Y); } }
    public Vector2 LinearVelocityMetersPerSecond { get { B2Vec2 v = b2Body_GetLinearVelocity(bodyId); return new(v.X, v.Y); } }
    public float RotationRadians => Box2D.NET.B2MathFunction.b2Rot_GetAngle(b2Body_GetRotation(bodyId));
    public float AngularVelocityRadiansPerSecond => b2Body_GetAngularVelocity(bodyId);

    public void ApplyBlast(Vector2 center, float radius)
    {
        Vector2 offset = PositionMeters - center;
        float distance = offset.Length();
        if (distance > radius) return;
        Vector2 direction = distance > 0.01f ? offset / distance : Vector2.UnitX;
        float materialScale = Kind switch
        {
            PhysicsPropKind.TrafficCone => 1.30f,
            PhysicsPropKind.TrashCan => 1.00f,
            PhysicsPropKind.Crate => 0.80f,
            _ => 0.55f,
        };
        float deltaSpeed = (20.0f + 55.0f * (1.0f - distance / radius)) * materialScale;
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
        Debris = CreateParticles(36, 5.0f, 22.0f, 0.0f);
    }

    public Vector2 CenterMeters { get; }
    public float AgeSeconds { get; private set; }
    public IReadOnlyList<BlastParticle> Smoke { get; }
    public IReadOnlyList<BlastParticle> Fire { get; }
    public IReadOnlyList<BlastParticle> GroundFire { get; }
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
}

internal sealed class WorldDebris
{
    private readonly B2BodyId bodyId;

    public WorldDebris(B2WorldId worldId, SceneryKind kind, Vector2 position, Vector2 size, float rotation, Vector2 velocity, float spin)
    {
        Kind = kind;
        SizeMeters = size;
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
        b2Body_SetAngularDamping(bodyId, 0.8f);
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(velocity.X, velocity.Y));
        b2Body_SetAngularVelocity(bodyId, spin);
    }

    public SceneryKind Kind { get; }
    public Vector2 SizeMeters { get; }
    public Vector2 PositionMeters { get { B2Vec2 p = b2Body_GetPosition(bodyId); return new(p.X, p.Y); } }
    public float RotationRadians => Box2D.NET.B2MathFunction.b2Rot_GetAngle(b2Body_GetRotation(bodyId));

    public void Step(float timeStep)
    {
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
    public const float BlastRadiusMeters = 35.0f;
    public static readonly Vector2 BoundaryMinimumMeters = new(-220.0f, -380.0f);
    public static readonly Vector2 BoundaryMaximumMeters = new(220.0f, 70.0f);
    public static readonly Vector2 TownMinimumMeters = new(-48.0f, -332.0f);
    public static readonly Vector2 TownMaximumMeters = new(48.0f, -244.0f);
    public static readonly Vector2 TownEntranceCenterMeters = new(-13.0f, -244.0f);
    public static readonly Vector2 CheckpointSouthBarrierMeters = new(-20.1f, -214.0f);
    public static readonly Vector2 CheckpointNorthBarrierMeters = new(-14.1f, -224.0f);
    public static readonly Vector2 CheckpointShackMeters = new(-4.0f, -219.0f);
    private const float BoundaryThicknessMeters = 4.0f;
    private readonly B2WorldId worldId;
    private readonly List<SceneryObject> scenery = [];
    private readonly List<WorldDebris> debris = [];
    private readonly List<Vector2> craters = [];
    private readonly List<BlastVisualEffect> blastEffects = [];
    private readonly List<PhysicsProp> props = [];
    private readonly List<WallSegment> townWalls = [];

    public DrivingEnvironment(B2WorldId worldId)
    {
        this.worldId = worldId;
        CreateBoundaries();
        CreateTownWalls();
        Build();
        BuildProps();
    }

    public IReadOnlyList<SceneryObject> Scenery => scenery;
    public IReadOnlyList<WorldDebris> Debris => debris;
    public IReadOnlyList<Vector2> Craters => craters;
    public IReadOnlyList<BlastVisualEffect> BlastEffects => blastEffects;
    public IReadOnlyList<PhysicsProp> Props => props;
    public IReadOnlyList<WallSegment> TownWalls => townWalls;

    public void Step(float timeStep)
    {
        foreach (WorldDebris item in debris) item.Step(timeStep);
        foreach (BlastVisualEffect effect in blastEffects) effect.Step(timeStep);
        foreach (PhysicsProp prop in props) prop.Step(timeStep);
        blastEffects.RemoveAll(effect => effect.IsFinished);
    }

    public void Explode(Vector2 center)
    {
        craters.Add(center);
        blastEffects.Add(new(center));
        foreach (PhysicsProp prop in props) prop.ApplyBlast(center, BlastRadiusMeters);
        foreach (SceneryObject item in scenery)
        {
            if (item.IsDestroyed) continue;
            Vector2 offset = item.PositionMeters - center;
            float distance = offset.Length();
            if (distance > BlastRadiusMeters) continue;
            item.IsDestroyed = true;
            if (item.HasBody) { b2DestroyBody(item.BodyId); item.HasBody = false; }
            if (item.Kind == SceneryKind.Bush) continue;
            Vector2 direction = distance > 0.01f ? offset / distance : Vector2.UnitX;
            float speed = 4.0f + (14.0f * (1.0f - (distance / BlastRadiusMeters)));
            int count = item.Kind is SceneryKind.Building or SceneryKind.Shack ? 6 : 1;
            for (int index = 0; index < count; index++)
            {
                float angle = (index - ((count - 1) * 0.5f)) * 0.28f;
                Vector2 launch = Vector2.Transform(direction, Matrix3x2.CreateRotation(angle)) * speed;
                Vector2 size = count == 1 ? item.SizeMeters : item.SizeMeters * new Vector2(0.28f, 0.42f);
                Vector2 position = item.PositionMeters + new Vector2((index % 3 - 1) * size.X, (index / 3 - 0.5f) * size.Y);
                debris.Add(new(worldId, item.Kind, position, size, item.RotationRadians, launch, (index - 2.5f) * 1.4f));
            }
        }
    }

    public void Reset()
    {
        foreach (SceneryObject item in scenery) if (item.HasBody) b2DestroyBody(item.BodyId);
        foreach (WorldDebris item in debris) item.Destroy();
        foreach (PhysicsProp prop in props) prop.Destroy();
        scenery.Clear(); debris.Clear(); craters.Clear(); blastEffects.Clear(); props.Clear(); Build(); BuildProps();
    }

    private void Build()
    {
        // Rural approach: vegetation and sparse structures along the curving road.
        for (int index = 0; index < 18; index++)
        {
            float y = -20.0f - (index * 13.0f);
            float roadX = MathF.Sin(index * 0.42f) * 18.0f;
            Add(index % 3 == 0 ? SceneryKind.Rock : SceneryKind.Tree, new(roadX - 18.0f - (index % 4) * 3.0f, y), index % 3 == 0 ? new(2.4f, 2.0f) : new(2.2f, 2.2f));
            Add(SceneryKind.Bush, new(roadX + 15.0f + (index % 5), y + 4.0f), new(2.5f, 2.0f));
        }
        Add(SceneryKind.Building, new(-32.0f, -105.0f), new(12.0f, 8.0f));
        Add(SceneryKind.Building, new(34.0f, -165.0f), new(10.0f, 9.0f));
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

        // Rural clutter remains well off the pavement.
        for (int index = 0; index < 8; index++)
        {
            float y = 22.0f - index * 27.0f;
            float roadX = MathF.Sin(index * 0.36f) * 18.0f;
            AddProp(PhysicsPropKind.Crate, new(roadX - 15.0f, y), new(1.15f, 1.15f), index * 0.17f);
            AddProp(PhysicsPropKind.TrashCan, new(roadX + 15.0f, y + 3.0f), new(0.9f, 1.1f), index * -0.11f);
        }

        // Cones mark only the checkpoint edges and the short final approach.
        AddProp(PhysicsPropKind.TrafficCone, new(-23.0f, -207.0f), new(0.55f, 0.55f), 0.0f);
        AddProp(PhysicsPropKind.TrafficCone, new(-12.5f, -207.0f), new(0.55f, 0.55f), 0.1f);
        AddProp(PhysicsPropKind.TrafficCone, new(-23.0f, -218.5f), new(0.55f, 0.55f), -0.1f);
        AddProp(PhysicsPropKind.TrafficCone, new(-11.5f, -218.5f), new(0.55f, 0.55f), 0.05f);
        AddProp(PhysicsPropKind.TrafficCone, new(-21.0f, -230.0f), new(0.55f, 0.55f), 0.0f);
        AddProp(PhysicsPropKind.TrafficCone, new(-9.5f, -230.0f), new(0.55f, 0.55f), 0.08f);
        AddProp(PhysicsPropKind.TrafficCone, new(-19.0f, -238.0f), new(0.55f, 0.55f), -0.08f);
        AddProp(PhysicsPropKind.TrafficCone, new(-8.0f, -238.0f), new(0.55f, 0.55f), 0.0f);

        // Outskirts and town service objects.
        for (int row = 0; row < 6; row++)
        {
            float y = -238.0f - row * 17.0f;
            AddProp(PhysicsPropKind.TrashCan, new(-11.0f, y), new(0.9f, 1.1f), 0.08f * row);
            AddProp(PhysicsPropKind.TrashCan, new(12.0f, y + 3.0f), new(0.9f, 1.1f), -0.06f * row);
            AddProp(PhysicsPropKind.Crate, new(-35.0f, y + 2.0f), new(1.25f, 1.25f), 0.12f * row);
            AddProp(PhysicsPropKind.Crate, new(34.0f, y - 3.0f), new(1.1f, 1.1f), -0.1f * row);
        }
    }

    private void AddProp(PhysicsPropKind kind, Vector2 position, Vector2 size, float rotation) =>
        props.Add(new PhysicsProp(worldId, kind, position, size, rotation));

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
