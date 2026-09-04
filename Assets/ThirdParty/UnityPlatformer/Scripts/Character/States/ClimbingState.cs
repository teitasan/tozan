using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Transforms;

public struct ClimbingState : IPlatformerCharacterState
{
    public float3 LastKnownClimbNormal;

    private bool _foundValidClimbSurface;

    public void OnStateEnter(CharacterState previousState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        ref PlatformerCharacterComponent character = ref processor.Character.ValueRW;
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref processor.CharacterDataAccess.CharacterProperties.ValueRW;
        ref quaternion characterRotation = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Rotation;
        
        processor.SetCapsuleGeometry(character.ClimbingGeometry.ToCapsuleGeometry());

        characterProperties.EvaluateGrounding = false;
        characterProperties.DetectMovementCollisions = false;
        characterProperties.DecollideFromOverlaps = false;
        characterBody.IsGrounded = false;

        LastKnownClimbNormal = -MathUtilities.GetForwardFromRotation(characterRotation);
    }

    public void OnStateExit(CharacterState nextState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref processor.CharacterDataAccess.CharacterProperties.ValueRW;
        
        KinematicCharacterUtilities.SetOrUpdateParentBody(ref baseContext, ref characterBody, default, default); 
        characterProperties.EvaluateGrounding = true;
        characterProperties.DetectMovementCollisions = true;
        characterProperties.DecollideFromOverlaps = true;
    }

    public void OnStatePhysicsUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref processor.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref processor.CharacterControl.ValueRW;
        ref float3 characterPosition = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Position;
        
        processor.HandlePhysicsUpdatePhase1(ref context, ref baseContext, true, false);

        _foundValidClimbSurface = false;
        if (ClimbingDetection(ref context, ref baseContext, in processor, true, out LastKnownClimbNormal, out DistanceHit closestClimbableHit, out DistanceHit closestUnclimbableHit))
        {
            _foundValidClimbSurface = true;

            characterPosition += -closestClimbableHit.Distance * closestClimbableHit.SurfaceNormal;
            characterPosition += (character.ClimbingGeometry.Radius - character.ClimbingDistanceFromSurface) * -closestClimbableHit.SurfaceNormal;

            if (closestUnclimbableHit.Entity != Entity.Null)
            {
                characterPosition += -closestUnclimbableHit.Distance * closestUnclimbableHit.SurfaceNormal;
            }

            float3 climbMoveVector = math.normalizesafe(MathUtilities.ProjectOnPlane(characterControl.MoveVector, LastKnownClimbNormal)) * math.length(characterControl.MoveVector);
            float3 targetVelocity = climbMoveVector * character.ClimbingSpeed;
            CharacterControlUtilities.InterpolateVelocityTowardsTarget(ref characterBody.RelativeVelocity, targetVelocity, deltaTime, character.ClimbingMovementSharpness);
            characterBody.RelativeVelocity = MathUtilities.ProjectOnPlane(characterBody.RelativeVelocity, LastKnownClimbNormal);

            if (processor.CharacterDataAccess.VelocityProjectionHits.Length > 0)
            {
                bool tmpCharacterGrounded = false;
                BasicHit tmpCharacterGroundHit = default;
                processor.ProjectVelocityOnHits(
                    ref context,
                    ref baseContext,
                    ref characterBody.RelativeVelocity,
                    ref tmpCharacterGrounded,
                    ref tmpCharacterGroundHit,
                    in processor.CharacterDataAccess.VelocityProjectionHits,
                    math.normalizesafe(characterBody.RelativeVelocity));
            }
            
            characterPosition += characterBody.RelativeVelocity * baseContext.Time.DeltaTime;
            
            KinematicCharacterUtilities.SetOrUpdateParentBody(ref baseContext, ref characterBody, closestClimbableHit.Entity, closestClimbableHit.Position); 
        }
        else
        {
            KinematicCharacterUtilities.SetOrUpdateParentBody(ref baseContext, ref characterBody, default, default); 
        }
        
        processor.HandlePhysicsUpdatePhase2(ref context, ref baseContext, false, false, false, false, true);
        
        DetectTransitions(ref context, ref baseContext, in processor);
    }

    public void OnStateVariableUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref processor.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref processor.CharacterControl.ValueRW;
        ref float3 characterPosition = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Rotation;

        float3 geometryCenter = GetGeometryCenter(in processor);
        
        // Keep the character upright relative to the world while climbing.
        // Using MoveVector here would roll the capsule ninety degrees when
        // the player strafes horizontally across a wall.
        float3 targetCharacterUp = math.normalizesafe(
            MathUtilities.ProjectOnPlane(math.up(), LastKnownClimbNormal),
            math.up());
        quaternion targetRotation = quaternion.LookRotationSafe(-LastKnownClimbNormal, targetCharacterUp);
        quaternion smoothedRotation = math.slerp(characterRotation, targetRotation, MathUtilities.GetSharpnessInterpolant(character.ClimbingRotationSharpness, deltaTime));
        MathUtilities.SetRotationAroundPoint(ref characterRotation, ref characterPosition, geometryCenter, smoothedRotation);
    }

    public void GetCameraParameters(in PlatformerCharacterComponent character, out Entity cameraTarget, out bool calculateUpFromGravity)
    {
        cameraTarget = character.ClimbingCameraTargetEntity;
        calculateUpFromGravity = true;
    }

    public void GetMoveVectorFromPlayerInput(in PlatformerPlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
    {
        moveVector = GetWallRelativeMoveVector(inputs.Move, LastKnownClimbNormal, cameraRotation);
    }

    /// <summary>
    /// Wall-tangent WASD: W/S = up/down on the surface, A/D = left/right along the surface.
    /// W/S follow world-up projected onto the wall; A/D follow the camera's
    /// screen-right projected onto the wall. Never pushes away from the wall.
    /// </summary>
    public static float3 GetWallRelativeMoveVector(float2 moveInput, float3 climbNormal)
    {
        return GetWallRelativeMoveVector(moveInput, climbNormal, quaternion.identity);
    }

    public static float3 GetWallRelativeMoveVector(float2 moveInput, float3 climbNormal, quaternion cameraRotation)
    {
        float3 normal = math.normalizesafe(climbNormal, float3.zero);
        if (math.lengthsq(normal) < 1e-6f || math.lengthsq(moveInput) < 1e-6f)
            return float3.zero;

        float3 wallUp = math.normalizesafe(MathUtilities.ProjectOnPlane(math.up(), normal), float3.zero);
        if (math.lengthsq(wallUp) < 1e-6f)
            wallUp = math.normalizesafe(MathUtilities.ProjectOnPlane(math.forward(), normal), math.right());

        float3 cameraRight = math.mul(cameraRotation, math.right());
        float3 wallRight = math.normalizesafe(MathUtilities.ProjectOnPlane(cameraRight, normal), float3.zero);
        if (math.lengthsq(wallRight) < 1e-6f)
            wallRight = math.normalizesafe(math.cross(wallUp, normal), math.right());
        float3 moveVector = (wallUp * moveInput.y) + (wallRight * moveInput.x);
        return MathUtilities.ProjectOnPlane(moveVector, normal);
    }

    public bool DetectTransitions(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        ref PlatformerCharacterControl characterControl = ref processor.CharacterControl.ValueRW;
        ref PlatformerCharacterStateMachine stateMachine = ref processor.StateMachine.ValueRW;
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        
        if (_foundValidClimbSurface &&
            LedgeGrabState.CanGrabLedge(ref context, ref baseContext, in processor, out Entity ledgeEntity, out ColliderCastHit ledgeSurfaceHit))
        {
            stateMachine.TransitionToState(CharacterState.LedgeGrab, ref context, ref baseContext, in processor);
            KinematicCharacterUtilities.SetOrUpdateParentBody(ref baseContext, ref characterBody, ledgeEntity, ledgeSurfaceHit.Position);
            return true;
        }

        if (!_foundValidClimbSurface || characterControl.JumpPressed || characterControl.DashPressed || characterControl.ClimbPressed)
        {
            stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in processor);
            return true;
        }

        if (processor.HasTozanGeometry &&
            !TozanSurfaceProbe.PredictSurfaceRelease(
                in processor,
                ref context,
                ref baseContext,
                processor.CharacterDataAccess.LocalTransform.ValueRO.Position,
                processor.CharacterDataAccess.LocalTransform.ValueRO.Rotation,
                processor.CharacterDataAccess.LocalTransform.ValueRO.Scale,
                LastKnownClimbNormal,
                characterBody.GroundingUp,
                processor.TozanGeometry.Probe))
        {
            stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in processor);
            return true;
        }

        return processor.DetectGlobalTransitions(ref context, ref baseContext);
    }

    public static float3 GetGeometryCenter(in PlatformerCharacterProcessor processor)
    {
        float3 characterPosition = processor.CharacterDataAccess.LocalTransform.ValueRW.Position;
        quaternion characterRotation = processor.CharacterDataAccess.LocalTransform.ValueRW.Rotation;
        PlatformerCharacterComponent character = processor.Character.ValueRW;
        
        RigidTransform characterTransform = new RigidTransform(characterRotation, characterPosition);
        float3 geometryCenter = math.transform(characterTransform, math.up() * character.ClimbingGeometry.Height * 0.5f);
        return geometryCenter;
    }

    public static bool CanStartClimbing(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        ref PlatformerCharacterComponent character = ref processor.Character.ValueRW;
        
        processor.SetCapsuleGeometry(character.ClimbingGeometry.ToCapsuleGeometry());
        bool canStart = ClimbingDetection(ref context, ref baseContext, in processor, false, out float3 avgClimbingSurfaceNormal, out DistanceHit closestClimbableHit, out DistanceHit closestUnclimbableHit);
        processor.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());

        return canStart;
    }

    public static bool ClimbingDetection(
        ref PlatformerCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        in PlatformerCharacterProcessor processor,
        bool addUnclimbableHitsAsVelocityProjectionHits,
        out float3 avgClimbingSurfaceNormal,
        out DistanceHit closestClimbableHit,
        out DistanceHit closestUnclimbableHit)
    {
        int climbableNormalsCounter = 0;
        avgClimbingSurfaceNormal = default;
        closestClimbableHit = default;
        closestUnclimbableHit = default;

        ref KinematicCharacterProperties characterProperties = ref processor.CharacterDataAccess.CharacterProperties.ValueRW;
        ref PlatformerCharacterComponent character = ref processor.Character.ValueRW;
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        ref float3 characterPosition = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Rotation;
        float characterScale = processor.CharacterDataAccess.LocalTransform.ValueRO.Scale;

        KinematicCharacterUtilities.CalculateDistanceAllCollisions(
            in processor,
            ref context,
            ref baseContext,
            processor.CharacterDataAccess.PhysicsCollider.ValueRO,
            processor.CharacterDataAccess.CharacterEntity,
            characterPosition,
            characterRotation,
            characterScale,
            0f,
            characterProperties.ShouldIgnoreDynamicBodies(),
            out baseContext.TmpDistanceHits);

        if (baseContext.TmpDistanceHits.Length == 0)
            return false;

        bool useGeometry = processor.HasTozanGeometry &&
                           processor.TozanGeometry.DetectionMode == TozanSurfaceDetectionMode.GeometryOnly;

        if (useGeometry)
        {
            DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits = processor.CharacterDataAccess.VelocityProjectionHits;
            return TozanSurfaceProbe.TryClusterClimbNormals(
                in baseContext.TmpDistanceHits,
                in baseContext.PhysicsWorld,
                characterBody.GroundingUp,
                processor.TozanGeometry.Probe,
                out avgClimbingSurfaceNormal,
                out closestClimbableHit,
                out closestUnclimbableHit,
                addUnclimbableHitsAsVelocityProjectionHits,
                ref velocityProjectionHits);
        }

        closestClimbableHit.Fraction = float.MaxValue;
        closestUnclimbableHit.Fraction = float.MaxValue;

        for (int i = 0; i < baseContext.TmpDistanceHits.Length; i++)
        {
            DistanceHit tmpHit = baseContext.TmpDistanceHits[i];

            float3 faceNormal = tmpHit.SurfaceNormal;

            if (PhysicsUtilities.GetHitFaceNormal(baseContext.PhysicsWorld.Bodies[tmpHit.RigidBodyIndex], tmpHit.ColliderKey, out float3 tmpFaceNormal))
            {
                faceNormal = tmpFaceNormal;
            }

            if (math.dot(faceNormal, tmpHit.SurfaceNormal) > KinematicCharacterUtilities.Constants.DotProductSimilarityEpsilon)
            {
                bool isClimbable = false;
                if (character.ClimbableTag.Value > CustomPhysicsBodyTags.Nothing.Value)
                {
                    if ((baseContext.PhysicsWorld.Bodies[tmpHit.RigidBodyIndex].CustomTags & character.ClimbableTag.Value) > 0)
                    {
                        isClimbable = true;
                    }
                }

                if (isClimbable)
                {
                    if (tmpHit.Fraction < closestClimbableHit.Fraction)
                    {
                        closestClimbableHit = tmpHit;
                    }

                    avgClimbingSurfaceNormal += faceNormal;
                    climbableNormalsCounter++;
                }
                else
                {
                    if (tmpHit.Fraction < closestUnclimbableHit.Fraction)
                    {
                        closestUnclimbableHit = tmpHit;
                    }

                    if (addUnclimbableHitsAsVelocityProjectionHits)
                    {
                        KinematicVelocityProjectionHit velProjHit = new KinematicVelocityProjectionHit(new BasicHit(tmpHit), false);
                        processor.CharacterDataAccess.VelocityProjectionHits.Add(velProjHit);
                    }
                }
            }
        }

        if (climbableNormalsCounter > 0)
        {
            avgClimbingSurfaceNormal = avgClimbingSurfaceNormal / climbableNormalsCounter;
            return true;
        }

        return false;
    }
}
