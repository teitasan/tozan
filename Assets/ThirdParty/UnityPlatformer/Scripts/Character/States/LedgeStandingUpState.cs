using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics;

public struct LedgeStandingUpState : IPlatformerCharacterState
{
    public float3 StandingPoint;
    
    private float3 _mantleStartPosition;
    private float3 _mantleTargetPosition;
    private float _mantleElapsedTime;
    private float _mantleDuration;
    private float _mantleCollisionSkin;
    private bool _shouldExitState;
    private bool _mantleFailed;

    public void OnStateEnter(CharacterState previousState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref processor.CharacterDataAccess.CharacterProperties.ValueRW;
        ref PlatformerCharacterComponent character = ref processor.Character.ValueRW;
        ref float3 characterPosition = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Rotation;
        
        processor.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());
        
        characterBody.RelativeVelocity = default;
        characterBody.IsGrounded = false;

        characterProperties.EvaluateGrounding = false;
        characterProperties.DetectMovementCollisions = false;
        characterProperties.DecollideFromOverlaps = false;

        _mantleStartPosition = characterPosition;
        _mantleElapsedTime = 0f;
        _mantleDuration = processor.HasTozanGeometry ? processor.TozanGeometry.MantleDuration : 0.45f;
        _mantleCollisionSkin = processor.HasTozanGeometry ? processor.TozanGeometry.MantleCollisionSkin : 0.02f;
        _shouldExitState = false;
        _mantleFailed = false;

        // StandingPoint is the official detector's support-surface hit.  The
        // downward obstruction cast used by LedgeDetection is not a valid
        // replacement for this pivot position.
        float3 target = StandingPoint;

        if (!TozanMantleUtility.TryValidateMantleTarget(
                in processor,
                ref context,
                ref baseContext,
                target,
                characterRotation,
                _mantleCollisionSkin,
                out float3 validatedTarget))
        {
            _mantleFailed = true;
            _shouldExitState = true;
            return;
        }

        _mantleTargetPosition = validatedTarget;
    }

    public void OnStateExit(CharacterState nextState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref processor.CharacterDataAccess.CharacterProperties.ValueRW;
        
        characterProperties.EvaluateGrounding = true;
        characterProperties.DetectMovementCollisions = true;
        characterProperties.DecollideFromOverlaps = true;

        KinematicCharacterUtilities.SetOrUpdateParentBody(ref baseContext, ref characterBody, default, default); 
    }

    public void OnStatePhysicsUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        ref float3 characterPosition = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref processor.CharacterDataAccess.LocalTransform.ValueRW.Rotation;
        
        processor.HandlePhysicsUpdatePhase1(ref context, ref baseContext, true, false);

        if (_mantleFailed)
        {
            processor.HandlePhysicsUpdatePhase2(ref context, ref baseContext, false, false, false, false, true);
            DetectTransitions(ref context, ref baseContext, in processor);
            return;
        }

        _mantleElapsedTime += baseContext.Time.DeltaTime;
        float t = TozanMantleUtility.SmoothStep01(_mantleElapsedTime / math.max(_mantleDuration, 1e-4f));
        float3 mantleUp = math.normalizesafe(characterBody.GroundingUp, math.up());
        float3 mantleDelta = _mantleTargetPosition - _mantleStartPosition;
        float targetHeight = math.dot(mantleDelta, mantleUp);
        float3 horizontalDelta = mantleDelta - mantleUp * targetHeight;
        float clearanceHeight = math.max(targetHeight + (_mantleCollisionSkin * 2f), _mantleCollisionSkin * 2f);
        float3 liftWaypoint = _mantleStartPosition + mantleUp * clearanceHeight;
        float3 traverseWaypoint = liftWaypoint + horizontalDelta;
        float3 desiredPosition;

        // Clear the ledge vertically before moving across its top, then lower
        // onto the support surface.  A single straight interpolation would
        // sweep the standing capsule through the lip's front face.
        if (t < 0.45f)
        {
            desiredPosition = math.lerp(_mantleStartPosition, liftWaypoint, t / 0.45f);
        }
        else if (t < 0.85f)
        {
            desiredPosition = math.lerp(liftWaypoint, traverseWaypoint, (t - 0.45f) / 0.4f);
        }
        else
        {
            desiredPosition = math.lerp(traverseWaypoint, _mantleTargetPosition, (t - 0.85f) / 0.15f);
        }

        if (!TozanMantleUtility.TryAdvanceMantlePosition(
                in processor,
                ref context,
                ref baseContext,
                characterPosition,
                desiredPosition,
                characterRotation,
                _mantleCollisionSkin,
                out float3 safePosition))
        {
            _mantleFailed = true;
            _shouldExitState = true;
        }
        else
        {
            characterPosition = safePosition;
        }

        if (t >= 1f && !_mantleFailed)
        {
            if (!TozanMantleUtility.TryValidateMantleTarget(
                    in processor,
                    ref context,
                    ref baseContext,
                    _mantleTargetPosition,
                    characterRotation,
                    _mantleCollisionSkin,
                    out _))
            {
                _mantleFailed = true;
                _shouldExitState = true;
            }
            else
            {
                ref KinematicCharacterProperties characterProperties = ref processor.CharacterDataAccess.CharacterProperties.ValueRW;
                ref PlatformerCharacterComponent character = ref processor.Character.ValueRW;
                float characterScale = processor.CharacterDataAccess.LocalTransform.ValueRO.Scale;

                float3 probeStart = _mantleTargetPosition + characterBody.GroundingUp * 0.05f;
                if (KinematicCharacterUtilities.RaycastClosestCollisions(
                        in processor,
                        ref context,
                        ref baseContext,
                        processor.CharacterDataAccess.CharacterEntity,
                        probeStart,
                        -characterBody.GroundingUp,
                        character.StandingGeometry.Height,
                        characterProperties.ShouldIgnoreDynamicBodies(),
                        processor.CharacterDataAccess.PhysicsCollider.ValueRO,
                        out RaycastHit groundHit,
                        out _))
                {
                    characterBody.IsGrounded = processor.IsGroundedOnHit(
                        ref context,
                        ref baseContext,
                        new BasicHit(groundHit),
                        0);
                }

                _shouldExitState = true;
            }
        }
        
        processor.HandlePhysicsUpdatePhase2(ref context, ref baseContext, false, false, false, false, true);

        DetectTransitions(ref context, ref baseContext, in processor);
    }

    public void OnStateVariableUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {

    }

    public void GetCameraParameters(in PlatformerCharacterComponent character, out Entity cameraTarget, out bool calculateUpFromGravity)
    {
        cameraTarget = character.DefaultCameraTargetEntity;
        calculateUpFromGravity = true;
    }

    public void GetMoveVectorFromPlayerInput(in PlatformerPlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
    {
        PlatformerCharacterProcessor.GetCommonMoveVectorFromPlayerInput(in inputs, cameraRotation, out moveVector);
    }

    public bool DetectTransitions(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterProcessor processor)
    {
        ref KinematicCharacterBody characterBody = ref processor.CharacterDataAccess.CharacterBody.ValueRW;
        ref PlatformerCharacterStateMachine stateMachine = ref processor.StateMachine.ValueRW;
        
        if (_shouldExitState)
        {
            if (_mantleFailed)
            {
                stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in processor);
                return true;
            }

            if (characterBody.IsGrounded)
            {
                stateMachine.TransitionToState(CharacterState.GroundMove, ref context, ref baseContext, in processor);
                return true;
            }

            stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in processor);
            return true;
        }

        return processor.DetectGlobalTransitions(ref context, ref baseContext);
    }
}
