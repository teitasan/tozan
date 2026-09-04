using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Burst-compatible geometry surface classification for markerless climbing.
/// Uses Unity Physics distance queries only — no CustomTags / HandlePoints.
/// </summary>
public static class TozanSurfaceProbe
{
    public static bool IsGeometryClimbableSurface(
        in PhysicsWorld physicsWorld,
        int rigidBodyIndex,
        float3 faceNormal,
        float3 groundingUp,
        in TozanSurfaceProbeConfig config)
    {
        if (rigidBodyIndex < 0 || rigidBodyIndex >= physicsWorld.NumBodies)
            return false;

        if (PhysicsUtilities.IsBodyDynamic(physicsWorld, rigidBodyIndex))
            return false;

        if (!math.all(math.isfinite(faceNormal)))
            return false;

        faceNormal = math.normalizesafe(faceNormal, float3.zero);
        if (math.lengthsq(faceNormal) < 1e-6f)
            return false;

        float upDot = math.dot(faceNormal, groundingUp);

        // Reject walkable ground and ceilings.
        if (upDot > config.MaxGroundNormalDot)
            return false;
        if (upDot < config.MinCeilingNormalDot)
            return false;

        // Require near-vertical (or steep) faces.
        if (math.abs(upDot) > config.MinSteepNormalDot)
            return false;

        return true;
    }

    public static bool TryClusterClimbNormals(
        in NativeList<DistanceHit> hits,
        in PhysicsWorld physicsWorld,
        float3 groundingUp,
        in TozanSurfaceProbeConfig config,
        out float3 avgClimbingSurfaceNormal,
        out DistanceHit closestClimbableHit,
        out DistanceHit closestUnclimbableHit,
        bool addUnclimbableHitsAsVelocityProjectionHits,
        ref DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits)
    {
        int climbableNormalsCounter = 0;
        avgClimbingSurfaceNormal = default;
        closestClimbableHit = default;
        closestUnclimbableHit = default;

        if (hits.Length == 0)
            return false;

        closestClimbableHit.Fraction = float.MaxValue;
        closestUnclimbableHit.Fraction = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            DistanceHit tmpHit = hits[i];
            float3 faceNormal = tmpHit.SurfaceNormal;

            if (PhysicsUtilities.GetHitFaceNormal(physicsWorld.Bodies[tmpHit.RigidBodyIndex], tmpHit.ColliderKey, out float3 tmpFaceNormal))
                faceNormal = tmpFaceNormal;

            if (math.dot(faceNormal, tmpHit.SurfaceNormal) <= KinematicCharacterUtilities.Constants.DotProductSimilarityEpsilon)
                continue;

            bool isClimbable = IsGeometryClimbableSurface(in physicsWorld, tmpHit.RigidBodyIndex, faceNormal, groundingUp, in config);

            if (isClimbable)
            {
                if (tmpHit.Fraction < closestClimbableHit.Fraction)
                    closestClimbableHit = tmpHit;

                if (climbableNormalsCounter == 0 ||
                    math.dot(math.normalizesafe(avgClimbingSurfaceNormal), math.normalizesafe(faceNormal)) >= config.CornerNormalMergeDot)
                {
                    avgClimbingSurfaceNormal += faceNormal;
                    climbableNormalsCounter++;
                }
            }
            else
            {
                if (tmpHit.Fraction < closestUnclimbableHit.Fraction)
                    closestUnclimbableHit = tmpHit;

                if (addUnclimbableHitsAsVelocityProjectionHits)
                {
                    velocityProjectionHits.Add(new KinematicVelocityProjectionHit(new BasicHit(tmpHit), false));
                }
            }
        }

        if (climbableNormalsCounter >= config.MinClusterNormals)
        {
            avgClimbingSurfaceNormal = math.normalizesafe(avgClimbingSurfaceNormal / climbableNormalsCounter, float3.zero);
            return math.lengthsq(avgClimbingSurfaceNormal) > 1e-6f;
        }

        return false;
    }

    public static bool PredictSurfaceRelease(
        in PlatformerCharacterProcessor processor,
        ref PlatformerCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        float3 characterPosition,
        quaternion characterRotation,
        float characterScale,
        float3 climbNormal,
        float3 groundingUp,
        in TozanSurfaceProbeConfig config)
    {
        ref KinematicCharacterProperties characterProperties = ref processor.CharacterDataAccess.CharacterProperties.ValueRW;

        float3 predictOffset = -math.normalizesafe(climbNormal, float3.zero) * config.ReleasePredictDistance;
        if (math.lengthsq(predictOffset) < 1e-8f)
            return true;

        float3 predictedPosition = characterPosition + predictOffset;

        KinematicCharacterUtilities.CalculateDistanceAllCollisions(
            in processor,
            ref context,
            ref baseContext,
            processor.CharacterDataAccess.PhysicsCollider.ValueRO,
            processor.CharacterDataAccess.CharacterEntity,
            predictedPosition,
            characterRotation,
            characterScale,
            0f,
            characterProperties.ShouldIgnoreDynamicBodies(),
            out baseContext.TmpDistanceHits);

        DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits = processor.CharacterDataAccess.VelocityProjectionHits;
        return TryClusterClimbNormals(
            in baseContext.TmpDistanceHits,
            in baseContext.PhysicsWorld,
            groundingUp,
            in config,
            out _,
            out _,
            out _,
            false,
            ref velocityProjectionHits);
    }
}
