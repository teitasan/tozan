using UnityEngine;
using UnityEngine.Assertions;

namespace Traverser
{
    [RequireComponent(typeof(TraverserCharacterController))]

    public class TraverserAbilityController : MonoBehaviour // Layer to control all of the character's abilities 
    {
        // --- Private Variables ---

        [HideInInspector]
        public TraverserInputController inputController;
        [HideInInspector]
        public TraverserRagdollController ragdollController;

        private TraverserCharacterController controller;
        private TraverserAnimationController animationController;
        private TraverserAnimationController.AnimatorParameters animatorParameters;
        private TraverserAbility[] abilities;
        private TraverserAbility currentAbility;

        // --------------------------------

        // --- Basic methods ---
        public void Start()
        {
            inputController = GetComponent<TraverserInputController>();
            controller = GetComponent<TraverserCharacterController>();
            animationController = GetComponent<TraverserAnimationController>();
            ragdollController = GetComponent<TraverserRagdollController>();
            abilities = GetComponents<TraverserAbility>();

            Assert.IsTrue(controller != null);

            // --- Set animator parameters --- 
            animationController.InitializeAnimatorParameters(ref animatorParameters);
        }

        // MYTODO: If order of update is important, it would be wise to add a priority to abilities,
        // instead of following the arbitrary order in which they were added as components

        public void Update()
        {
            if (!controller.isActiveAndEnabled)
                return;

            // --- Keep updating our current ability ---
            bool isEnabled = currentAbility == null ? false : currentAbility.IsAbilityEnabled();

            if (currentAbility != null && isEnabled)
            {
                TraverserAbility result = currentAbility.OnUpdate(Time.deltaTime);

                if (result == null)
                {
                    currentAbility = result;
                }
                else if (!result.Equals(currentAbility))
                {
                    if (currentAbility != null)
                        currentAbility.OnExit();

                    currentAbility = result;
                    currentAbility.OnEnter();
                }
            }

            // --- If no ability is in control, look for one ---
            if (currentAbility == null || !isEnabled)
            {
                // --- Iterate all abilities and update each one until one takes control ---
                foreach (TraverserAbility ability in abilities)
                {
                    if (!ability.IsAbilityEnabled())
                        continue;

                    TraverserAbility result = ability.OnUpdate(Time.deltaTime);

                    // --- If an ability asks to take control, break ---
                    if (result != null)
                    {
                        if (currentAbility != null)
                            currentAbility.OnExit();

                        currentAbility = result;
                        currentAbility.OnEnter();
                        break;
                    }
                }
            }

            // --- Send updated animator parameters to animation controller ---
            if (animationController.isActiveAndEnabled)
            {
                // --- We must prevent the animator from activating a transition to another state while we are trying to trigger another ---
                // --- Only one transition can be active at once!!! ---
                if(!animationController.transition.isON)
                    animatorParameters.Move = inputController.GetMoveIntensity() > 0.0f;

                // --- We are not interested in Y speed, since then gravity would make us run in the animator! ---
                Vector2 velocity;
                velocity.x = controller.targetVelocity.x;
                velocity.y = controller.targetVelocity.z;

                // TODO: Should we interpolate these values?

                animatorParameters.Speed = velocity.magnitude;
                animatorParameters.Heading = controller.targetHeading;
                animatorParameters.DirectionX = Mathf.Lerp(animatorParameters.DirectionX, velocity.x, Time.deltaTime / Time.fixedDeltaTime);
                animationController.UpdateAnimator(ref animatorParameters);
            }
        }

        // MYTODO: If order of update is important, it would be wise to add a priority to abilities,
        // instead of following the arbitrary order in which they were added as components

        private void FixedUpdate()
        {
            if (!controller.isActiveAndEnabled)
                return;

            bool isEnabled = currentAbility == null ? false : currentAbility.IsAbilityEnabled();

            // --- Keep updating our current ability ---
            if (currentAbility != null && isEnabled)
            {
                TraverserAbility result = currentAbility.OnFixedUpdate(Time.fixedDeltaTime);

                if(result == null)
                {
                    currentAbility = result;
                }
                else if (!result.Equals(currentAbility))
                {
                    if (currentAbility != null)
                        currentAbility.OnExit();

                    currentAbility = result;
                    currentAbility.OnEnter();
                }
            }

            // --- If no ability is in control, look for one ---
            if (currentAbility == null || !isEnabled)
            {
                // --- Iterate all abilities and update each one until one takes control ---
                foreach (TraverserAbility ability in abilities)
                {
                    if (!ability.IsAbilityEnabled())
                        continue;

                    TraverserAbility result = ability.OnFixedUpdate(Time.fixedDeltaTime);

                    // --- If an ability asks to take control, break ---
                    if (result != null)
                    {
                        if(currentAbility != null)
                            currentAbility.OnExit();

                        currentAbility = result;
                        currentAbility.OnEnter();

                        break;
                    }
                }
            }

            ApplyPredictedStep();
        }

        void ApplyPredictedStep()
        {
            if (animationController != null && animationController.transition.isON)
                return;
            if (controller.targetDisplacement.sqrMagnitude < 1e-8f)
                return;
            controller.ForceMove(transform.position + controller.targetDisplacement);
            controller.targetDisplacement = Vector3.zero;
        }

        private void OnAnimatorMove()
        {
            // TOZAN: dummy Idle has no clip, so this callback is unreliable.
            // Predicted displacement is applied in FixedUpdate instead.
        }

        // --------------------------------

        // --- Utilites ---

        public bool isCurrent(TraverserAbility ability)
        {
            return currentAbility != null && currentAbility.Equals(ability);
        }

        public float GetDirectionX()
        {
            return animatorParameters.DirectionX;
        }

        // --------------------------------
    }
}

