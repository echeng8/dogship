#nullable enable

namespace Yarn.Unity.Samples
{
    using UnityEngine;
    using System.Threading;
    using Yarn.Unity.Attributes;
    using System.Collections.Generic;
    using UnityEngine.Events;
    using System.Threading.Tasks;

    public class SimpleCharacter : MonoBehaviour
    {
        public enum CharacterMode
        {
            PlayerControlledMovement,
            ExternallyControlledMovement,
            PathMovement,
            Interact,
        }

        public CharacterMode Mode { get; private set; }

        public bool CanInteract => Mode == CharacterMode.PlayerControlledMovement;
        public bool HasPath => followPath != null;

        [SerializeField] bool isPlayerControlled;

        public bool IsAlive { get; private set; } = true;

        #region Movement Variables

        [Group("Movement")]
        [SerializeField] float speed;
        [Group("Movement")]
        [SerializeField] float gravity = 10;
        [Group("Movement")]
        [SerializeField] float turnSpeed;

        [Group("Movement")]
        [SerializeField] float acceleration = 0.5f;
        [Group("Movement")]
        [SerializeField] float deceleration = 0.1f;
        [Group("Movement")]
        public Transform? lookTarget;
        [Group("Movement")]
        [ShowIf(nameof(isPlayerControlled))]
        [SerializeField] float outOfBoundsYPosition = -5;

        [HideIf(nameof(isPlayerControlled))]
        [SerializeField] SimplePath? followPath;
        [HideIf(nameof(isPlayerControlled))]
        [SerializeField] float pathDestinationTolerance = 0.1f;

        [Group("Movement")]
        [ShowIf(nameof(isPlayerControlled))]
        [SerializeField] InputAxisVector2 movementInput = new();

        [Group("Movement")]
        [ShowIf(nameof(isPlayerControlled))]
        [SerializeField] InputAxisButton interactInput = new();

        private int currentDestinationPathIndex = -1;
        private float remainingPathWaitTime = 0f;

        // Store target direction relative to parent (planet) coordinate system
        // This keeps the character's facing direction fixed relative to the planet surface
        private Vector3 planetRelativeTargetForward = Vector3.forward;

        public float CurrentSpeedFactor { get; private set; } = 0f;

        private float lastFrameSpeed = 0f;
        private float lastFrameSpeedChange = 0f;
        private Vector3 lastFrameWorldPosition;

        private CharacterController? characterController;

        private Vector3 lastGroundedPosition;

        #endregion

        #region Animation Variables
        [Group("Animation")]
        [SerializeField] private Animator? animator;
        [Group("Animation")]
        [SerializeField] SerializableDictionary<string, string> facialExpressions = new();
        [Group("Animation")]
        [SerializeField] string facialExpressionsLayer = "Face";
        private int facialExpressionsLayerID = 0;

        [SerializeField] Texture2D? deathMouthTexture;

        [Group("Animation")]
        [Header("Blinking")]
        [SerializeField] float meanBlinkTime = 2f;
        [Group("Animation")]
        [SerializeField] float blinkTimeVariance = 0.5f;

        [Group("Animation Parameters", true)]
        [AnimationParameter(nameof(animator), AnimatorControllerParameterType.Float)]
        [SerializeField] private string speedParameter = "Speed";
        [Group("Animation Parameters", true)]
        [AnimationParameter(nameof(animator), AnimatorControllerParameterType.Float)]
        [SerializeField] string sideTiltParameter = "Side Tilt";
        [Group("Animation Parameters", true)]
        [AnimationParameter(nameof(animator), AnimatorControllerParameterType.Float)]
        [SerializeField] string forwardTiltParameter = "Forward Tilt";
        [Group("Animation Parameters", true)]
        [AnimationParameter(nameof(animator), AnimatorControllerParameterType.Float)]
        [SerializeField] string turnParameter = "Turn";
        [Group("Animation Parameters", true)]
        [AnimationParameter(nameof(animator), AnimatorControllerParameterType.Trigger)]
        [SerializeField] string blinkTriggerName = "Blink";
        [Group("Animation Parameters", true)]
        [AnimationParameter(nameof(animator), AnimatorControllerParameterType.Float)]
        [SerializeField] string cycleOffsetParameter = "Cycle Offset";
        [Group("Animation Parameters", true)]
        [AnimationParameter(nameof(animator), AnimatorControllerParameterType.Bool)]
        [SerializeField] string aliveParameter = "Alive";

        [Group("Animation Parameters")]
        [AnimationLayer(nameof(animator))]

        private float timeUntilNextBlink = 0f;
        private Dictionary<int, CancellationTokenSource> activeAnimationLerps = new();

        #endregion

        #region Interaction Variables
        [Group("Interaction")]
        [ShowIf(nameof(isPlayerControlled))]
        [SerializeField] float interactionRadius = 1f;
        [Group("Interaction")]
        [ShowIf(nameof(isPlayerControlled))]
        [SerializeField] Vector3 offset = Vector3.zero;

        [Group("Interaction")]
        [ShowIf(nameof(isPlayerControlled))]
        [SerializeField] UnityEvent<Interactable>? onInteracting;

        private List<Interactable> interactables = new();

        private Interactable? currentInteractable = null;

        #endregion

        #region Animation Commands

        [YarnCommand("tilt_forward")]
        public YarnTask TiltForward(float destination, float time = 0f, bool wait = false)
        {
            var task = TweenAnimationParameter(forwardTiltParameter, destination, time, EasingFunctions.InOutQuad, destroyCancellationToken);
            return wait ? task : YarnTask.CompletedTask;
        }

        [YarnCommand("tilt_side")]
        public YarnTask TiltSide(float destination, float time = 0f, bool wait = false)
        {
            var task = TweenAnimationParameter(sideTiltParameter, destination, time, EasingFunctions.InOutQuad, destroyCancellationToken);
            return wait ? task : YarnTask.CompletedTask;
        }

        [YarnCommand("turn")]
        public YarnTask TurnCharacter(float destination, float time = 0f, bool wait = false)
        {
            var task = TweenAnimationParameter(turnParameter, destination, time, EasingFunctions.InOutQuad, destroyCancellationToken);
            return wait ? task : YarnTask.CompletedTask;
        }

        [YarnCommand("tween_animation_parameter")]
        public YarnTask TweenParameter(string parameter, float destination, float time, bool wait = false)
        {
            var task = TweenAnimationParameter(parameter, destination, time, EasingFunctions.InOutQuad, destroyCancellationToken);
            return wait ? task : YarnTask.CompletedTask;
        }

        [YarnCommand("set_animator_bool")]
        public void SetAnimatorBool(string parameterName, bool value)
        {
            if (animator == null)
            {
                Debug.LogError($"Can't set parameter {parameterName}: animator is not set");
                return;
            }
            animator.SetBool(parameterName, value);
        }

        [YarnCommand("play_animation")]
        public YarnTask PlayAnimation(string layerName, string stateName, bool wait = false)
        {
            if (animator == null)
            {
                Debug.LogError($"Can't play animation {stateName}: animator is not set");
                return YarnTask.CompletedTask;
            }

            var layerIndex = animator.GetLayerIndex(layerName);
            if (layerIndex == -1)
            {
                Debug.LogError($"Can't play animation {stateName}: no layer {layerName} found");
                return YarnTask.CompletedTask;
            }

            var stateHash = Animator.StringToHash(stateName);
            if (animator.HasState(layerIndex, stateHash) == false)
            {
                Debug.LogError($"Can't play animation {stateName}: no state {stateName} found in layer {layerName}");
                return YarnTask.CompletedTask;
            }

            animator.Play(stateHash, layerIndex);

            if (wait)
            {
                return WaitUntilAnimationComplete(animator, stateHash, layerIndex);
            }
            else
            {
                return YarnTask.CompletedTask;
            }

            static async YarnTask WaitUntilAnimationComplete(Animator animator, int stateNameHash, int layerIndex)
            {
                AnimatorStateInfo stateInfo;

                // Wait until the animator starts playing this state
                do
                {
                    stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
                    await YarnTask.Yield();
                } while (stateInfo.shortNameHash != stateNameHash);

                // Wait until the animator is no longer playing this state
                // or has reached the end of the state
                do
                {
                    stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
                    await YarnTask.Yield();
                } while (stateInfo.shortNameHash == stateNameHash || stateInfo.normalizedTime >= 1);
            }
        }

        [YarnCommand("face")]
        public void SetFacialExpression(string name, float crossfadeTime = 0)
        {
            if (animator == null)
            {
                Debug.LogWarning($"{name} has no {nameof(Animator)}");
                return;
            }

            if (!facialExpressions.TryGetValue(name, out var stateName))
            {
                Debug.LogWarning($"{name} is not a valid facial expression (expected {string.Join(", ", facialExpressions.Keys)})");
                return;
            }

            if (crossfadeTime <= 0)
            {
                animator.Play(stateName, facialExpressionsLayerID);
            }
            else
            {
                animator.CrossFadeInFixedTime(stateName, crossfadeTime, facialExpressionsLayerID);
            }
        }

        [YarnCommand("set_alive")]
        public void SetAlive(bool alive, bool immediate = false)
        {
            this.IsAlive = alive;
            if (animator != null)
            {
                animator.SetBool(aliveParameter, alive);
                if (immediate)
                {
                    async YarnTask RunAnimationAtHighSpeed(Animator animator)
                    {
                        var previousSpeed = animator.speed;
                        animator.speed = 10000;
                        await YarnTask.Yield();
                        animator.speed = previousSpeed;
                    }
                    RunAnimationAtHighSpeed(animator).Forget();
                }
            }
            if (this.TryGetComponent<MouthView>(out var mouthView))
            {
                if (alive)
                {
                    mouthView.ClearOverride();
                }
                else if (deathMouthTexture != null)
                {
                    mouthView.SetOverride(deathMouthTexture);
                }
            }
            if (this.characterController != null)
            {
                this.characterController.enabled = IsAlive;
            }
        }

        #endregion

        #region Animation Logic

        protected void SetupAnimation()
        {
            characterController = GetComponent<CharacterController>();

            if (animator != null)
            {
                facialExpressionsLayerID = animator.GetLayerIndex(facialExpressionsLayer);

                // Randomly offset the cycle for the base pose so that
                // characters don't sync up
                animator.SetFloat(cycleOffsetParameter, Random.value);
            }

            timeUntilNextBlink = GetNextBlinkTime();
        }

        private float GetNextBlinkTime()
        {
            return meanBlinkTime + Mathf.Lerp(-blinkTimeVariance, blinkTimeVariance, UnityEngine.Random.value);
        }

        public void UpdateAnimation()
        {

            if (animator == null)
            {
                return;
            }

            timeUntilNextBlink -= Time.deltaTime;

            if (timeUntilNextBlink <= 0 && !string.IsNullOrEmpty(blinkTriggerName))
            {
                animator.SetTrigger(blinkTriggerName);
                timeUntilNextBlink = GetNextBlinkTime();
            }

            animator.SetFloat(speedParameter, CurrentSpeedFactor);
        }

        private async YarnTask TweenAnimationParameter(string animationParameter, float to, float duration, System.Func<float, float> easingFunction, CancellationToken cancellationToken)
        {
            if (animator == null)
            {
                return;
            }

            var hash = Animator.StringToHash(animationParameter);
            var currentValue = animator.GetFloat(hash);

            // If a tween was already running for this parameter, cancel it now
            if (activeAnimationLerps.TryGetValue(hash, out var cancellationTokenSource))
            {
                cancellationTokenSource.Cancel();
            }

            // Create and store a cancellation token source for this animation
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            activeAnimationLerps[hash] = cts;

            // Run the tween
            await Tweening.TweenValue(currentValue, to, duration, easingFunction, value => animator.SetFloat(hash, value), cts.Token);

            // Clean up
            activeAnimationLerps.Remove(hash);
        }
        #endregion

        #region Movement Commands
        [YarnCommand("pause_path_movement")]
        public void StopFollowingPath()
        {
            if (!HasPath)
            {
                Debug.LogError($"{name} is not currently following a path");
                return;
            }
            this.Mode = CharacterMode.ExternallyControlledMovement;
        }
        [YarnCommand("resume_path_movement")]
        public void ResumeFollowingPath()
        {
            if (!HasPath)
            {
                Debug.LogError($"{name} is not currently following a path");
                return;
            }
            this.Mode = CharacterMode.PathMovement;
        }

        #endregion

        #region Movement Logic

        private void SetupMovement()
        {
            // Initialize planet-relative target forward from current transform forward
            SetPlanetRelativeTargetFromWorldDirection(transform.forward);

            if (!isPlayerControlled && followPath != null && followPath.Count >= 2)
            {
                // If we have a follow path, start there, and look at the 
                var startPoint = followPath.GetWorldPosition(0);
                var nextPoint = followPath.GetWorldPosition(1);
                transform.position = startPoint;
                
                // Set target direction toward next point, projected onto local plane
                var pathDirection = nextPoint - startPoint;
                var localUp = transform.up;
                var pathDirectionOnLocalPlane = Vector3.ProjectOnPlane(pathDirection, localUp);
                if (pathDirectionOnLocalPlane.sqrMagnitude > 0.0001f)
                {
                    SetPlanetRelativeTargetFromWorldDirection(pathDirectionOnLocalPlane.normalized);
                }
                
                transform.rotation = GetCurrentLookDirection();

                currentDestinationPathIndex = 0;
                Mode = CharacterMode.PathMovement;
            }            // Start facing our look target, if any
            if (lookTarget != null)
            {
                transform.rotation = GetCurrentLookDirection();
            }

            lastFrameWorldPosition = transform.position;
            lastGroundedPosition = transform.position;

            if (isPlayerControlled)
            {
                movementInput.Enable();
                interactInput.Enable();
            }
        }

        protected void UpdateMovement()
        {

            if (isPlayerControlled && Mode == CharacterMode.PlayerControlledMovement)
            {
                Vector2 input = movementInput.Value;

                ApplyMovement(input);
            }
            else if (Mode == CharacterMode.ExternallyControlledMovement)
            {
                // Our movement is externally controlled; update our animator
                // based how quickly we're moving
                var currentSpeed = (lastFrameWorldPosition - transform.position).magnitude / Time.deltaTime;
                CurrentSpeedFactor = Mathf.Clamp01(currentSpeed / speed);
            }
            else if (Mode == CharacterMode.PathMovement && followPath != null)
            {
                if (currentDestinationPathIndex == -1 || followPath.Count < 1)
                {
                    // No current path location.
                    CurrentSpeedFactor = 0;
                }
                else if (remainingPathWaitTime > 0)
                {
                    CurrentSpeedFactor = 0;
                    remainingPathWaitTime -= Time.deltaTime;
                }
                else
                {
                    // Move towards current path node
                    // var nextPath = followPath.GetPositionData(currentDestinationPathIndex);

                    var offset = followPath.GetWorldPosition(currentDestinationPathIndex) - transform.position;
                    var input = new Vector2(offset.x, offset.z).normalized;
                    ApplyMovement(input);

                    if (offset.magnitude <= pathDestinationTolerance)
                    {
                        // We've reached the destination
                        currentDestinationPathIndex = (currentDestinationPathIndex + 1) % followPath.Count;
                        remainingPathWaitTime = followPath.GetDelay(currentDestinationPathIndex);
                    }
                }
            }
            else
            {
                CurrentSpeedFactor = 0;
            }

            // If the player falls out of bounds, warp them to the last point
            // they were on the ground
            if (isPlayerControlled && Mode == CharacterMode.PlayerControlledMovement)
            {
                if (transform.position.y < outOfBoundsYPosition)
                {
                    if (characterController != null)
                    {
                        transform.position = lastGroundedPosition;
                    }
                }

                if (characterController != null && characterController.isGrounded)
                {
                    lastGroundedPosition = transform.position;
                }
            }

            if (this.IsAlive)
            {
                // Rotate towards our current look direction if we're alive
                // Use the character's current rotation instead of reconstructing it with world Y-up
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    GetCurrentLookDirection(),
                    turnSpeed * Time.deltaTime
                );
            }

            lastFrameWorldPosition = transform.position;

            void ApplyMovement(Vector2 input)
            {
                float rawSpeed = input.magnitude < 0.001 ? 0f : Mathf.Clamp01(input.magnitude) * speed;

                var dampingTime = (rawSpeed > lastFrameSpeed) ? acceleration : deceleration;

                var dampedSpeed = Mathf.SmoothDamp(lastFrameSpeed, rawSpeed, ref lastFrameSpeedChange, dampingTime);
                lastFrameSpeed = dampedSpeed;

                var movement = new Vector3(
                    input.x,
                    0,
                    input.y
                );

                if (movement.magnitude > 0)
                {
                    // If we're moving, update the direction we want to be looking
                    // at when we have no look target
                    // Project movement onto local horizontal plane to respect planet surface
                    var localUp = transform.up;
                    var movementOnLocalPlane = Vector3.ProjectOnPlane(movement, localUp);
                    if (movementOnLocalPlane.sqrMagnitude > 0.0001f)
                    {
                        // Store the movement direction as our planet-relative target forward
                        SetPlanetRelativeTargetFromWorldDirection(movementOnLocalPlane.normalized);
                    }
                }

                movement = movement.normalized * dampedSpeed;
                movement.y = -gravity;

                if (characterController != null)
                {
                    characterController.Move(movement * Time.deltaTime);
                }

                CurrentSpeedFactor = Mathf.Clamp01(dampedSpeed / speed);
            }
        }

        private Quaternion GetCurrentLookDirection()
        {
            // Convert planet-relative forward direction to world rotation
            Quaternion direction = GetWorldRotationFromPlanetRelativeForward();
            if (lookTarget != null)
            {
                var lookDirection = lookTarget.position - transform.position;
                // Project the look direction onto the character's local horizontal plane
                // (perpendicular to the character's up direction)
                var localUp = transform.up;
                var lookDirectionOnLocalPlane = Vector3.ProjectOnPlane(lookDirection, localUp);

                // Only create the rotation if we have a valid direction
                if (lookDirectionOnLocalPlane.sqrMagnitude > 0.0001f)
                {
                    direction = Quaternion.LookRotation(lookDirectionOnLocalPlane, localUp);
                }
            }
            return direction;
        }

        // Helper method to convert planet-relative forward direction to world rotation
        private Quaternion GetWorldRotationFromPlanetRelativeForward()
        {
            var localUp = transform.up;
            
            // Convert planet-relative forward to world coordinates
            Vector3 worldForward;
            if (transform.parent != null)
            {
                // Transform the planet-relative direction to world space through the parent
                worldForward = transform.parent.TransformDirection(planetRelativeTargetForward);
                // Project onto the character's current local horizontal plane
                worldForward = Vector3.ProjectOnPlane(worldForward, localUp).normalized;
            }
            else
            {
                // No parent, fall back to using the stored direction directly
                worldForward = Vector3.ProjectOnPlane(planetRelativeTargetForward, localUp).normalized;
            }
            
            if (worldForward.sqrMagnitude < 0.0001f)
            {
                // If the forward direction is parallel to up, use transform forward as fallback
                worldForward = Vector3.ProjectOnPlane(transform.forward, localUp).normalized;
                if (worldForward.sqrMagnitude < 0.0001f)
                {
                    worldForward = Vector3.ProjectOnPlane(Vector3.forward, localUp).normalized;
                }
            }
            return Quaternion.LookRotation(worldForward, localUp);
        }

        // Helper method to set target direction from world rotation
        private void SetPlanetRelativeTargetFromWorldDirection(Vector3 worldDirection)
        {
            if (transform.parent != null)
            {
                // Convert world direction to parent (planet) relative coordinates
                planetRelativeTargetForward = transform.parent.InverseTransformDirection(worldDirection).normalized;
            }
            else
            {
                // No parent, store in world coordinates
                planetRelativeTargetForward = worldDirection.normalized;
            }
        }

        public async YarnTask MoveTo(Vector3 position, CancellationToken cancellationToken)
        {
            if (Vector3.Distance(position, transform.position) <= 0.0001f)
            {
                // We're already at the position; nothing to do
                return;
            }
            // Look in the direction we're moving, not at any look target
            var lookDirection = position - transform.position;
            // Project look direction onto local horizontal plane to respect planet surface
            var localUp = transform.up;
            var lookDirectionOnLocalPlane = Vector3.ProjectOnPlane(lookDirection, localUp);
            if (lookDirectionOnLocalPlane.sqrMagnitude > 0.0001f)
            {
                // Store the look direction as our planet-relative target forward
                SetPlanetRelativeTargetFromWorldDirection(lookDirectionOnLocalPlane.normalized);
            }

            var previousLookTarget = lookTarget;
            lookTarget = null;

            var previousMode = Mode;

            Mode = CharacterMode.ExternallyControlledMovement;

            do
            {
                transform.position = Vector3.MoveTowards(transform.position, position, speed * Time.deltaTime);
                this.CurrentSpeedFactor = 1;

                await YarnTask.Yield();

            } while (Vector3.Distance(transform.position, position) > 0.05f && !cancellationToken.IsCancellationRequested);

            lookTarget = previousLookTarget;

            Mode = previousMode;

            this.CurrentSpeedFactor = 0;
        }

        public void SetLookDirection(Quaternion rotation, bool immediate = false)
        {
            // Convert world rotation to planet-relative target forward
            var worldForward = rotation * Vector3.forward;
            SetPlanetRelativeTargetFromWorldDirection(worldForward);

            if (immediate)
            {
                transform.rotation = rotation;
            }
        }
        #endregion

        #region Interaction Logic

        public void SetupInteraction()
        {
            // FPS interaction handled by player controller raycasting
        }

        protected void UpdateInteraction()
        {
            // FPS interaction handled by player controller raycasting  
        }

        #endregion

        #region Core Logic

        protected void Awake()
        {
            Mode = CharacterMode.PlayerControlledMovement;

            SetupMovement();
            SetupAnimation();
            SetupInteraction();
        }

        protected void Update()
        {
            UpdateMovement();
            UpdateAnimation();
            UpdateInteraction();
        }

        protected void OnDrawGizmosSelected()
        {
            if (isPlayerControlled)
            {
                // Show interaction volume
                Gizmos.color = Color.yellow;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireSphere(offset, interactionRadius);
            }
        }
        #endregion
    }
}
