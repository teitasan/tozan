using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Preflight and validate a collision-safe mantle path for LedgeStandingUpState.
/// </summary>
public static class TozanMantleUtility
{
    public static bool TryValidateMantleTarget(
        in PlatformerCharacterProcessor processor,
        ref PlatformerCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        float3 mantleTargetPosition,
        quaternion characterRotation,
        float collisionSkin,
        out float3 validatedTarget)
    {
        validatedTarget = mantleTargetPosition;
        ref PlatformerCharacterComponent character = ref processor.Character.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref processor.CharacterDataAccess.CharacterProperties.ValueRW;
        float characterScale = processor.CharacterDataAccess.LocalTransform.ValueRO.Scale;

        if (!math.all(math.isfinite(mantleTargetPosition)))
            return false;

        processor.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());

        bool obstructed = KinematicCharacterUtilities.CalculateDistanceClosestCollisions(
            in processor,
            ref context,
            ref baseContext,
            processor.CharacterDataAccess.CharacterEntity,
            processor.CharacterDataAccess.PhysicsCollider.ValueRO,
            mantleTargetPosition,
            characterRotation,
            characterScale,
            collisionSkin,
            characterProperties.ShouldIgnoreDynamicBodies(),
            out DistanceHit hit);

        // Contact with the support surface is valid.  Only actual penetration
        // beyond the skin is a failed target; rejecting every near hit would
        // reject a standing capsule whose feet are intentionally on the ledge.
        if (obstructed && hit.Distance < -collisionSkin)
            return false;

        return true;
    }

    public static bool TryAdvanceMantlePosition(
        in PlatformerCharacterProcessor processor,
        ref PlatformerCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        float3 fromPosition,
        float3 toPosition,
        quaternion characterRotation,
        float collisionSkin,
        out float3 safePosition)
    {
        safePosition = fromPosition;
        ref PlatformerCharacterComponent character = ref processor.Character.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref processor.CharacterDataAccess.CharacterProperties.ValueRW;
        float characterScale = processor.CharacterDataAccess.LocalTransform.ValueRO.Scale;

        float3 delta = toPosition - fromPosition;
        float distance = math.length(delta);
        if (distance < 1e-5f)
        {
            safePosition = toPosition;
            return true;
        }

        float3 direction = delta / distance;
        processor.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());

        if (KinematicCharacterUtilities.CastColliderClosestCollisions(
                in processor,
                ref context,
                ref baseContext,
                processor.CharacterDataAccess.CharacterEntity,
                processor.CharacterDataAccess.PhysicsCollider.ValueRO,
                fromPosition,
                characterRotation,
                characterScale,
                direction,
                distance,
                false,
                characterProperties.ShouldIgnoreDynamicBodies(),
                out ColliderCastHit castHit,
                out float hitDistance))
        {
            float allowed = math.max(0f, hitDistance - collisionSkin);
            safePosition = fromPosition + direction * allowed;
            return allowed >= distance - collisionSkin * 2f;
        }

        safePosition = toPosition;
        return true;
    }

    public static float SmoothStep01(float t)
    {
        t = math.clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
