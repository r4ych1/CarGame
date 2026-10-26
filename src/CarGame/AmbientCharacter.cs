using System.Numerics;
using Box2D.NET;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;

namespace CarGame;

internal enum CharacterActivity
{
    Standing,
    Talking,
    Working,
    Walking,
}

internal enum CharacterLimbKind
{
    Torso,
    Head,
    Arm,
    Leg,
}

internal sealed class CharacterLimb
{
    private const float SettleTimeSeconds = 0.75f;
    private const float BloodPoolSpreadTimeSeconds = 0.8f;
    private readonly B2BodyId bodyId;
    private bool bodyDisabled;
    private float bloodPoolAgeSeconds;

    public CharacterLimb(
        B2WorldId worldId,
        CharacterLimbKind kind,
        Vector2 positionMeters,
        Vector2 sizeMeters,
        float rotationRadians,
        Vector2 velocityMetersPerSecond,
        float angularVelocityRadiansPerSecond,
        ColorRgb clothingColor,
        ColorRgb skinColor)
    {
        Kind = kind;
        SizeMeters = sizeMeters;
        ClothingColor = clothingColor;
        SkinColor = skinColor;
        BloodPoolPositionMeters = positionMeters;
        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_dynamicBody;
        bodyDefinition.position = new B2Vec2(positionMeters.X, positionMeters.Y);
        bodyDefinition.rotation = b2MakeRot(rotationRadians);
        bodyDefinition.enableSleep = false;
        bodyId = b2CreateBody(worldId, bodyDefinition);
        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        shapeDefinition.density = 36.0f;
        shapeDefinition.material.friction = 0.65f;
        shapeDefinition.material.restitution = 0.08f;
        b2CreatePolygonShape(bodyId, shapeDefinition, b2MakeBox(sizeMeters.X * 0.5f, sizeMeters.Y * 0.5f));
        b2Body_SetLinearDamping(bodyId, 0.55f);
        b2Body_SetAngularDamping(bodyId, 0.75f);
        b2Body_SetBullet(bodyId, true);
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(velocityMetersPerSecond.X, velocityMetersPerSecond.Y));
        b2Body_SetAngularVelocity(bodyId, angularVelocityRadiansPerSecond);
    }

    public CharacterLimbKind Kind { get; }
    public Vector2 SizeMeters { get; }
    public ColorRgb ClothingColor { get; }
    public ColorRgb SkinColor { get; }
    public float AgeSeconds { get; private set; }
    public Vector2 PositionMeters
    {
        get
        {
            B2Vec2 position = b2Body_GetPosition(bodyId);
            return new(position.X, position.Y);
        }
    }
    public float RotationRadians => b2Rot_GetAngle(b2Body_GetRotation(bodyId));
    public Vector2 LinearVelocityMetersPerSecond
    {
        get
        {
            B2Vec2 velocity = b2Body_GetLinearVelocity(bodyId);
            return new(velocity.X, velocity.Y);
        }
    }
    public Vector2 BloodPoolPositionMeters { get; private set; }
    public bool HasBloodPool => bodyDisabled;
    public float BloodPoolRadiusMeters
    {
        get
        {
            if (!HasBloodPool)
            {
                return 0.0f;
            }

            float maximumRadius = Kind switch
            {
                CharacterLimbKind.Torso => 0.58f,
                CharacterLimbKind.Head => 0.38f,
                CharacterLimbKind.Leg => 0.40f,
                _ => 0.34f,
            };
            float spreadProgress = Math.Clamp(bloodPoolAgeSeconds / BloodPoolSpreadTimeSeconds, 0.0f, 1.0f);
            return maximumRadius * (0.3f + spreadProgress * 0.7f);
        }
    }

    public void Step(float timeStep)
    {
        AgeSeconds += timeStep;
        if (bodyDisabled)
        {
            bloodPoolAgeSeconds += timeStep;
            return;
        }

        if (!bodyDisabled && AgeSeconds >= SettleTimeSeconds)
        {
            BloodPoolPositionMeters = PositionMeters;
            b2Body_SetLinearVelocity(bodyId, default);
            b2Body_SetAngularVelocity(bodyId, 0.0f);
            b2Body_Disable(bodyId);
            bodyDisabled = true;
        }
    }

    public void Destroy() => b2DestroyBody(bodyId);
}

internal sealed class AmbientCharacter
{
    private const float DeathReactionDurationSeconds = 0.65f;
    private const float VehicleImpactReactionDurationSeconds = 0.22f;
    private const float PanicRunningSpeedMetersPerSecond = 8.0f;
    private const float CharacterBodyHalfWidthMeters = 0.36f;
    private const float CharacterBodyHalfLengthMeters = 0.44f;
    private const float PanicLookAheadMeters = 5.0f;
    private const float PanicWallClearanceMeters = 0.65f;
    internal const float LimbBurstRadiusMeters = 10.0f;
    private const float MinimumLimbLaunchSpeedMetersPerSecond = 5.0f;
    private const float MaximumLimbLaunchSpeedMetersPerSecond = 14.0f;
    private readonly B2BodyId bodyId;
    private readonly B2WorldId worldId;
    private readonly List<CharacterLimb> limbFragments = [];
    private readonly Vector2[] path;
    private readonly float[] segmentLengths;
    private readonly float walkSpeedMetersPerSecond;
    private readonly float animationOffset;
    private readonly Vector2 homePositionMeters;
    private readonly float initialHeadingRadians;
    private int currentSegmentIndex;
    private float distanceIntoCurrentSegmentMeters;
    private bool bodyDisabled;
    private float collisionDisableDelaySeconds = DeathReactionDurationSeconds;
    private Vector2 panicDirection;
    private float panicTimerSeconds;
    private float knockedDownTimerSeconds;
    private float knockedDownAgeSeconds;
    private bool knockedDownBodyDisabled;

    public AmbientCharacter(
        B2WorldId worldId,
        CharacterActivity activity,
        Vector2 positionMeters,
        float headingRadians,
        ColorRgb clothingColor,
        ColorRgb skinColor,
        float animationOffset,
        float walkSpeedMetersPerSecond = 0.0f,
        Vector2[]? path = null)
    {
        this.worldId = worldId;
        Activity = activity;
        homePositionMeters = positionMeters;
        initialHeadingRadians = headingRadians;
        FacingRadians = headingRadians;
        ClothingColor = clothingColor;
        SkinColor = skinColor;
        this.animationOffset = animationOffset;
        this.walkSpeedMetersPerSecond = walkSpeedMetersPerSecond;
        this.path = path ?? [];
        segmentLengths = new float[this.path.Length];

        for (int index = 0; index < this.path.Length; index++)
        {
            Vector2 next = this.path[(index + 1) % this.path.Length];
            segmentLengths[index] = Vector2.Distance(this.path[index], next);
        }

        B2BodyDef bodyDefinition = b2DefaultBodyDef();
        bodyDefinition.type = B2BodyType.b2_dynamicBody;
        bodyDefinition.position = new B2Vec2(positionMeters.X, positionMeters.Y);
        bodyDefinition.rotation = b2MakeRot(headingRadians);
        bodyDefinition.enableSleep = false;
        bodyId = b2CreateBody(worldId, bodyDefinition);

        B2ShapeDef shapeDefinition = b2DefaultShapeDef();
        shapeDefinition.density = 65.0f;
        shapeDefinition.material.friction = 0.55f;
        shapeDefinition.material.restitution = 0.02f;
        b2CreatePolygonShape(
            bodyId,
            shapeDefinition,
            b2MakeBox(CharacterBodyHalfWidthMeters, CharacterBodyHalfLengthMeters));
        b2Body_SetLinearDamping(bodyId, 1.4f);
        b2Body_SetAngularDamping(bodyId, 5.0f);
    }

    public CharacterActivity Activity { get; }
    public Vector2 PositionMeters
    {
        get
        {
            B2Vec2 position = b2Body_GetPosition(bodyId);
            return new(position.X, position.Y);
        }
    }
    public Vector2 LinearVelocityMetersPerSecond
    {
        get
        {
            B2Vec2 velocity = b2Body_GetLinearVelocity(bodyId);
            return new(velocity.X, velocity.Y);
        }
    }
    public float FacingRadians { get; private set; }
    public float RotationRadians => b2Rot_GetAngle(b2Body_GetRotation(bodyId));
    public float AnimationAgeSeconds { get; private set; }
    public float DeathAgeSeconds { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsPanicking => panicTimerSeconds > 0.0f;
    public bool IsKnockedDown => knockedDownTimerSeconds > 0.0f;
    public bool HasExplodedIntoLimbs { get; private set; }
    internal bool IsCollisionDisabled => bodyDisabled;
    internal Vector2 PanicDirection => panicDirection;
    internal IReadOnlyList<Vector2> AuthoredPath => path;
    public IReadOnlyList<CharacterLimb> LimbFragments => limbFragments;
    public ColorRgb ClothingColor { get; }
    public ColorRgb SkinColor { get; }
    public float AnimationOffset => animationOffset;
    public float MassKilograms => b2Body_GetMass(bodyId);

    public void Step(float timeStep, IReadOnlyList<WallSegment>? panicObstacles = null)
    {
        AnimationAgeSeconds += timeStep;
        foreach (CharacterLimb limb in limbFragments)
        {
            limb.Step(timeStep);
        }

        if (IsDead)
        {
            DeathAgeSeconds += timeStep;
            if (!bodyDisabled)
            {
                ApplyCorpseResistance(timeStep);
                if (DeathAgeSeconds >= collisionDisableDelaySeconds)
                {
                    b2Body_SetLinearVelocity(bodyId, default);
                    b2Body_SetAngularVelocity(bodyId, 0.0f);
                    b2Body_Disable(bodyId);
                    bodyDisabled = true;
                }
            }
            return;
        }

        if (knockedDownTimerSeconds > 0.0f)
        {
            knockedDownTimerSeconds = MathF.Max(0.0f, knockedDownTimerSeconds - timeStep);
            knockedDownAgeSeconds += timeStep;
            panicTimerSeconds = MathF.Max(0.0f, panicTimerSeconds - timeStep);
            if (!knockedDownBodyDisabled)
            {
                ApplyCorpseResistance(timeStep);
                if (knockedDownAgeSeconds >= DeathReactionDurationSeconds)
                {
                    b2Body_SetLinearVelocity(bodyId, default);
                    b2Body_SetAngularVelocity(bodyId, 0.0f);
                    b2Body_Disable(bodyId);
                    knockedDownBodyDisabled = true;
                }
            }

            if (knockedDownTimerSeconds <= 0.0f)
            {
                if (knockedDownBodyDisabled)
                {
                    b2Body_Enable(bodyId);
                    knockedDownBodyDisabled = false;
                }
                B2Vec2 position = b2Body_GetPosition(bodyId);
                b2Body_SetTransform(bodyId, position, b2MakeRot(FacingRadians));
                knockedDownAgeSeconds = 0.0f;
            }
            else
            {
                return;
            }
        }

        if (panicTimerSeconds > 0.0f)
        {
            panicTimerSeconds = MathF.Max(0.0f, panicTimerSeconds - timeStep);
            Vector2 steeringDirection = ChoosePanicDirection(
                PositionMeters,
                panicDirection,
                panicObstacles ?? []);
            Vector2 panicVelocity = LinearVelocityMetersPerSecond;
            float runningSpeed = MathF.Max(
                PanicRunningSpeedMetersPerSecond,
                Vector2.Dot(panicVelocity, steeringDirection));
            Vector2 panicTargetVelocity = steeringDirection * runningSpeed;
            Vector2 panicAcceleration = (panicTargetVelocity - panicVelocity) * 2.4f;
            const float maximumPanicAcceleration = 14.0f;
            if (panicAcceleration.LengthSquared() > maximumPanicAcceleration * maximumPanicAcceleration)
            {
                panicAcceleration = Vector2.Normalize(panicAcceleration) * maximumPanicAcceleration;
            }
            Vector2 panicForce = panicAcceleration * MassKilograms;
            b2Body_ApplyForceToCenter(bodyId, new B2Vec2(panicForce.X, panicForce.Y), true);
            FacingRadians = MathF.Atan2(steeringDirection.Y, steeringDirection.X);
            return;
        }

        Vector2 targetPosition = Activity == CharacterActivity.Walking && path.Length > 1
            ? AdvanceAlongPath(walkSpeedMetersPerSecond * timeStep)
            : homePositionMeters;
        Vector2 positionError = targetPosition - PositionMeters;
        Vector2 currentVelocity = LinearVelocityMetersPerSecond;
        Vector2 desiredVelocity;
        float maximumAcceleration;
        if (Activity == CharacterActivity.Walking && path.Length > 1)
        {
            Vector2 direction = targetPosition - PositionMeters;
            if (direction.LengthSquared() < 0.0001f)
            {
                direction = PathDirection();
            }

            desiredVelocity = direction.LengthSquared() > 0.0001f
                ? Vector2.Normalize(direction) * walkSpeedMetersPerSecond
                : Vector2.Zero;
            maximumAcceleration = 4.5f;
            if (direction.LengthSquared() > 0.0001f)
            {
                FacingRadians = MathF.Atan2(direction.Y, direction.X);
            }
        }
        else
        {
            desiredVelocity = positionError * 2.0f;
            if (desiredVelocity.LengthSquared() > 0.6f * 0.6f)
            {
                desiredVelocity = Vector2.Normalize(desiredVelocity) * 0.6f;
            }
            maximumAcceleration = 1.8f;
            FacingRadians = initialHeadingRadians +
                (Activity == CharacterActivity.Talking ? 0.18f : 0.3f) *
                MathF.Sin((AnimationAgeSeconds + animationOffset) * 0.65f);
        }

        Vector2 acceleration = (desiredVelocity - currentVelocity) * 2.4f;
        if (acceleration.LengthSquared() > maximumAcceleration * maximumAcceleration)
        {
            acceleration = Vector2.Normalize(acceleration) * maximumAcceleration;
        }

        Vector2 force = acceleration * MassKilograms;
        b2Body_ApplyForceToCenter(bodyId, new B2Vec2(force.X, force.Y), true);
    }

    public void Die()
    {
        if (IsDead)
        {
            return;
        }

        if (knockedDownBodyDisabled)
        {
            b2Body_Enable(bodyId);
            knockedDownBodyDisabled = false;
            knockedDownTimerSeconds = 0.0f;
        }

        knockedDownTimerSeconds = 0.0f;
        IsDead = true;
        DeathAgeSeconds = 0.0f;
        collisionDisableDelaySeconds = DeathReactionDurationSeconds;
        B2Vec2 position = b2Body_GetPosition(bodyId);
        b2Body_SetTransform(bodyId, position, b2MakeRot(FacingRadians));
    }

    public void DieFromVehicleImpact(Vector2 vehicleVelocityMetersPerSecond)
    {
        Die();
        collisionDisableDelaySeconds = VehicleImpactReactionDurationSeconds;
        float vehicleSpeed = vehicleVelocityMetersPerSecond.Length();
        if (vehicleSpeed > 0.01f)
        {
            Vector2 impactDirection = vehicleVelocityMetersPerSecond / vehicleSpeed;
            float transferredSpeed = Math.Clamp(vehicleSpeed * 0.35f, 2.0f, 8.0f);
            float mass = MassKilograms;
            b2Body_ApplyLinearImpulseToCenter(
                bodyId,
                new B2Vec2(
                    impactDirection.X * mass * transferredSpeed,
                    impactDirection.Y * mass * transferredSpeed),
                true);
            float spinDirection = animationOffset % 2.0f < 1.0f ? 1.0f : -1.0f;
            b2Body_ApplyAngularImpulse(bodyId, mass * 1.8f * spinDirection, true);
        }
    }

    public void KeepOutOfStream(
        float streamCenterYMeters,
        float streamWidthMeters,
        float bridgeCenterXMeters,
        float bridgeWidthMeters)
    {
        if (IsDead)
        {
            return;
        }

        Vector2 position = PositionMeters;
        float bridgeHalfWidth = bridgeWidthMeters * 0.5f - CharacterBodyHalfWidthMeters;
        if (MathF.Abs(position.X - bridgeCenterXMeters) <= bridgeHalfWidth)
        {
            return;
        }

        float protectedHalfWidth = streamWidthMeters * 0.5f + CharacterBodyHalfLengthMeters;
        float streamOffset = position.Y - streamCenterYMeters;
        if (MathF.Abs(streamOffset) >= protectedHalfWidth)
        {
            return;
        }

        float bankSide = streamOffset < 0.0f ? -1.0f : 1.0f;
        Vector2 safePosition = new(
            position.X,
            streamCenterYMeters + bankSide * protectedHalfWidth);
        b2Body_SetTransform(bodyId, new B2Vec2(safePosition.X, safePosition.Y), b2Body_GetRotation(bodyId));
        B2Vec2 velocity = b2Body_GetLinearVelocity(bodyId);
        bool movingIntoStream = velocity.Y * bankSide < 0.0f;
        if (movingIntoStream)
        {
            b2Body_SetLinearVelocity(bodyId, new B2Vec2(velocity.X, 0.0f));
        }
    }

    public bool ApplyBlast(Vector2 centerMeters, float radiusMeters, float deadlyRadiusMeters)
    {
        if (bodyDisabled)
        {
            return false;
        }

        Vector2 offset = PositionMeters - centerMeters;
        float distance = offset.Length();
        if (distance > radiusMeters)
        {
            return false;
        }

        bool isLethal = distance <= deadlyRadiusMeters;
        if (isLethal)
        {
            Die();
        }
        else
        {
            BeginPanic(distance > 0.01f ? offset / distance : Vector2.UnitX, 5.0f);
        }

        Vector2 direction = distance > 0.01f ? offset / distance : Vector2.UnitX;
        Vector2 preBlastVelocity = LinearVelocityMetersPerSecond;
        float launchSpeed = isLethal
            ? BlastPhysics.LaunchSpeed(distance, radiusMeters, 18.0f, 105.0f)
            : BlastPhysics.LaunchSpeed(distance, radiusMeters, 28.0f, 150.0f);
        float mass = MassKilograms;
        b2Body_ApplyLinearImpulseToCenter(
            bodyId,
            new B2Vec2(direction.X * mass * launchSpeed, direction.Y * mass * launchSpeed),
            true);
        float spinDirection = animationOffset % 2.0f < 1.0f ? 1.0f : -1.0f;
        b2Body_ApplyAngularImpulse(bodyId, mass * 4.0f * spinDirection, true);
        if (isLethal && distance <= LimbBurstRadiusMeters)
        {
            CreateLimbBurst(direction, preBlastVelocity, LimbLaunchSpeed(launchSpeed));
        }
        else if (!isLethal)
        {
            KnockDown(direction, 2.8f);
        }
        return true;
    }

    private void KnockDown(Vector2 direction, float durationSeconds)
    {
        knockedDownTimerSeconds = MathF.Max(knockedDownTimerSeconds, durationSeconds);
        knockedDownAgeSeconds = 0.0f;
        if (direction.LengthSquared() > 0.0001f)
        {
            FacingRadians = MathF.Atan2(direction.Y, direction.X);
        }
        B2Vec2 position = b2Body_GetPosition(bodyId);
        b2Body_SetTransform(bodyId, position, b2MakeRot(FacingRadians));
    }

    public void TryFleeFromVehicle(
        Vector2 vehiclePosition,
        Vector2 vehicleVelocity,
        float triggerDistanceMeters)
    {
        if (IsDead || panicTimerSeconds > 0.0f || vehicleVelocity.LengthSquared() < 4.0f)
        {
            return;
        }

        Vector2 toVehicle = vehiclePosition - PositionMeters;
        float distance = toVehicle.Length();
        if (distance < 0.01f || distance > triggerDistanceMeters)
        {
            return;
        }

        Vector2 towardVehicle = toVehicle / distance;
        Vector2 facing = new(MathF.Cos(FacingRadians), MathF.Sin(FacingRadians));
        Vector2 vehicleForward = Vector2.Normalize(vehicleVelocity);
        if (Vector2.Dot(facing, towardVehicle) <= 0.45f ||
            Vector2.Dot(vehicleForward, -towardVehicle) <= 0.45f)
        {
            return;
        }

        Vector2 vehicleRight = new(-vehicleForward.Y, vehicleForward.X);
        Vector2 fromVehicle = PositionMeters - vehiclePosition;
        float side = Vector2.Dot(fromVehicle, vehicleRight) >= 0.0f ? 1.0f : -1.0f;
        BeginPanic(vehicleRight * side, 3.5f);
    }

    public void ReactToExplosion(Vector2 centerMeters, float panicRadiusMeters)
    {
        if (IsDead)
        {
            return;
        }

        Vector2 awayFromBlast = PositionMeters - centerMeters;
        float distance = awayFromBlast.Length();
        if (distance <= panicRadiusMeters)
        {
            BeginPanic(distance > 0.01f ? awayFromBlast / distance : Vector2.UnitX, 8.0f);
        }
    }

    public void TryFleeFromCorpse(Vector2 corpsePositionMeters, float triggerDistanceMeters)
    {
        if (IsDead || panicTimerSeconds > 0.0f)
        {
            return;
        }

        Vector2 toCorpse = corpsePositionMeters - PositionMeters;
        float distance = toCorpse.Length();
        if (distance < 0.01f || distance > triggerDistanceMeters)
        {
            return;
        }

        Vector2 towardCorpse = toCorpse / distance;
        Vector2 facing = new(MathF.Cos(FacingRadians), MathF.Sin(FacingRadians));
        if (Vector2.Dot(facing, towardCorpse) > 0.35f)
        {
            BeginPanic(-towardCorpse, 5.0f);
        }
    }

    public bool IsOverlappedByVehicle(Vector2 vehiclePosition, float vehicleRotation, Vector2 vehicleSize)
    {
        Vector2 offset = PositionMeters - vehiclePosition;
        Vector2 forward = VehicleMath.Forward(vehicleRotation);
        Vector2 right = VehicleMath.Right(vehicleRotation);
        float longitudinalDistance = MathF.Abs(Vector2.Dot(offset, forward));
        float lateralDistance = MathF.Abs(Vector2.Dot(offset, right));
        const float collisionPaddingMeters = 0.18f;
        return longitudinalDistance <= (vehicleSize.Y * 0.5f) + CharacterBodyHalfLengthMeters + collisionPaddingMeters &&
            lateralDistance <= (vehicleSize.X * 0.5f) + CharacterBodyHalfWidthMeters + collisionPaddingMeters;
    }

    public void Destroy()
    {
        b2DestroyBody(bodyId);
        foreach (CharacterLimb limb in limbFragments) limb.Destroy();
    }

    private void BeginPanic(Vector2 direction, float durationSeconds)
    {
        if (direction.LengthSquared() < 0.0001f)
        {
            return;
        }

        panicDirection = Vector2.Normalize(direction);
        panicTimerSeconds = MathF.Max(panicTimerSeconds, durationSeconds);
    }

    internal static Vector2 ChoosePanicDirection(
        Vector2 positionMeters,
        Vector2 desiredDirection,
        IReadOnlyList<WallSegment> obstacles)
    {
        if (desiredDirection.LengthSquared() < 0.0001f)
        {
            return Vector2.Zero;
        }

        Vector2 desired = Vector2.Normalize(desiredDirection);
        float[] candidateAngles =
        [
            0.0f,
            MathF.PI / 4.0f,
            -MathF.PI / 4.0f,
            MathF.PI / 3.0f,
            -MathF.PI / 3.0f,
            MathF.PI / 2.0f,
            -MathF.PI / 2.0f,
        ];
        foreach (float angle in candidateAngles)
        {
            float cosine = MathF.Cos(angle);
            float sine = MathF.Sin(angle);
            Vector2 candidate = new(
                desired.X * cosine - desired.Y * sine,
                desired.X * sine + desired.Y * cosine);
            if (!IsPanicRouteBlocked(positionMeters, candidate, obstacles))
            {
                return candidate;
            }
        }

        return -desired;
    }

    private static bool IsPanicRouteBlocked(
        Vector2 start,
        Vector2 direction,
        IReadOnlyList<WallSegment> obstacles)
    {
        foreach (WallSegment obstacle in obstacles)
        {
            Vector2 halfSize = obstacle.SizeMeters * 0.5f +
                new Vector2(PanicWallClearanceMeters);
            Vector2 minimum = obstacle.PositionMeters - halfSize;
            Vector2 maximum = obstacle.PositionMeters + halfSize;
            bool startsWithinClearance = start.X >= minimum.X && start.X <= maximum.X &&
                start.Y >= minimum.Y && start.Y <= maximum.Y;
            if (startsWithinClearance)
            {
                Vector2 awayFromObstacle = start - obstacle.PositionMeters;
                if (Vector2.Dot(direction, awayFromObstacle) < -0.0001f)
                {
                    return true;
                }

                continue;
            }

            if (SegmentIntersectsBounds(start, direction, PanicLookAheadMeters, minimum, maximum))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentIntersectsBounds(
        Vector2 start,
        Vector2 direction,
        float length,
        Vector2 minimum,
        Vector2 maximum)
    {
        float minimumDistance = 0.0f;
        float maximumDistance = length;
        return IntersectsAxis(start.X, direction.X, minimum.X, maximum.X, ref minimumDistance, ref maximumDistance) &&
            IntersectsAxis(start.Y, direction.Y, minimum.Y, maximum.Y, ref minimumDistance, ref maximumDistance);
    }

    private static bool IntersectsAxis(
        float start,
        float direction,
        float minimum,
        float maximum,
        ref float minimumDistance,
        ref float maximumDistance)
    {
        if (MathF.Abs(direction) < 0.0001f)
        {
            return start >= minimum && start <= maximum;
        }

        float firstDistance = (minimum - start) / direction;
        float secondDistance = (maximum - start) / direction;
        if (firstDistance > secondDistance)
        {
            (firstDistance, secondDistance) = (secondDistance, firstDistance);
        }

        minimumDistance = MathF.Max(minimumDistance, firstDistance);
        maximumDistance = MathF.Min(maximumDistance, secondDistance);
        return minimumDistance <= maximumDistance;
    }

    internal static float LimbLaunchSpeed(float fatalLaunchSpeed) =>
        Math.Clamp(
            fatalLaunchSpeed * 0.12f,
            MinimumLimbLaunchSpeedMetersPerSecond,
            MaximumLimbLaunchSpeedMetersPerSecond);

    private void CreateLimbBurst(
        Vector2 blastDirection,
        Vector2 preBlastVelocity,
        float launchSpeed)
    {
        HasExplodedIntoLimbs = true;
        Vector2 center = PositionMeters;
        float facing = FacingRadians;
        Vector2 forward = new(MathF.Cos(facing), MathF.Sin(facing));
        Vector2 side = new(-forward.Y, forward.X);
        (CharacterLimbKind Kind, Vector2 Offset, Vector2 Size, float SpeedScale, float Spin)[] parts =
        [
            (CharacterLimbKind.Torso, Vector2.Zero, new(0.48f, 0.68f), 0.72f, 4.0f),
            (CharacterLimbKind.Head, forward * 0.48f, new(0.34f, 0.34f), 1.02f, -6.0f),
            (CharacterLimbKind.Arm, forward * 0.08f - side * 0.40f, new(0.20f, 0.48f), 0.92f, 8.0f),
            (CharacterLimbKind.Arm, forward * 0.08f + side * 0.40f, new(0.20f, 0.48f), 1.08f, -9.0f),
            (CharacterLimbKind.Leg, -forward * 0.42f - side * 0.15f, new(0.24f, 0.56f), 0.82f, -5.0f),
            (CharacterLimbKind.Leg, -forward * 0.42f + side * 0.15f, new(0.24f, 0.56f), 1.12f, 7.0f),
        ];

        for (int index = 0; index < parts.Length; index++)
        {
            (CharacterLimbKind kind, Vector2 localOffset, Vector2 size, float speedScale, float spin) = parts[index];
            Vector2 partPosition = center + localOffset;
            Vector2 spread = Vector2.Transform(blastDirection, Matrix3x2.CreateRotation((index - 2.5f) * 0.16f));
            Vector2 velocity = preBlastVelocity + spread * launchSpeed * speedScale;
            limbFragments.Add(new CharacterLimb(
                worldId,
                kind,
                partPosition,
                size,
                facing + (index - 2.5f) * 0.24f,
                velocity,
                spin,
                ClothingColor,
                SkinColor));
        }
    }

    private Vector2 AdvanceAlongPath(float distanceMeters)
    {
        float remainingDistance = distanceMeters;
        while (remainingDistance > 0.0f)
        {
            float segmentLength = segmentLengths[currentSegmentIndex];
            float segmentRemaining = segmentLength - distanceIntoCurrentSegmentMeters;
            if (segmentLength <= 0.0f || remainingDistance >= segmentRemaining)
            {
                remainingDistance -= segmentRemaining;
                distanceIntoCurrentSegmentMeters = 0.0f;
                currentSegmentIndex = (currentSegmentIndex + 1) % segmentLengths.Length;
            }
            else
            {
                distanceIntoCurrentSegmentMeters += remainingDistance;
                remainingDistance = 0.0f;
            }
        }

        Vector2 start = path[currentSegmentIndex];
        Vector2 end = path[(currentSegmentIndex + 1) % path.Length];
        float segmentLengthMeters = segmentLengths[currentSegmentIndex];
        float progress = segmentLengthMeters > 0.0f
            ? distanceIntoCurrentSegmentMeters / segmentLengthMeters
            : 0.0f;
        Vector2 direction = end - start;
        if (direction.LengthSquared() > 0.0f)
        {
            FacingRadians = MathF.Atan2(direction.Y, direction.X);
        }
        return Vector2.Lerp(start, end, progress);
    }

    private Vector2 PathDirection()
    {
        Vector2 direction = path[(currentSegmentIndex + 1) % path.Length] - path[currentSegmentIndex];
        return direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : Vector2.Zero;
    }

    private void ApplyCorpseResistance(float timeStep)
    {
        Vector2 velocity = LinearVelocityMetersPerSecond;
        velocity *= MathF.Max(0.0f, 1.0f - 2.8f * timeStep);
        b2Body_SetLinearVelocity(bodyId, new B2Vec2(velocity.X, velocity.Y));
        b2Body_SetAngularVelocity(bodyId, b2Body_GetAngularVelocity(bodyId) * MathF.Max(0.0f, 1.0f - 4.0f * timeStep));
    }
}
