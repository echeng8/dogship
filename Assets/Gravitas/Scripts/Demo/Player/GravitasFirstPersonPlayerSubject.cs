using System;
using UnityEngine;
using Coherence.Toolkit;
using IngameDebugConsole;

namespace Gravitas.Demo
{
    /// <summary>
    /// Implementation of a third person player controller that interacts with Gravitas systems.
    /// Handles player movement and interaction with any IInteractable objects.
    /// Camera control is handled by Cinemachine.
    /// </summary>
    public class GravitasFirstPersonPlayerSubject : GravitasSubject
    {
        #region Constants
        private const float INTERACTION_DISTANCE = 2.75f;
        #endregion

        #region Events
        public event Action<string> OnInteractionTargetEvent;
        #endregion

        #region Public Properties
        public Camera PlayerCamera => playerCamera;
        public PlayerStats PlayerStats => playerStats;
        public bool IsDashing => dashTimer > 0;
        #endregion

        #region SerializeField Variables
        [Header("Components")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private ParticleSystem playerParticleSystem;
        [SerializeField] private Transform aimCore;

        [Header("Settings")]
        [SerializeField] private LayerMask interactableLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private float jetpackForce = 15f;
        [SerializeField] private float jumpForce = 10f;
        [SerializeField, Tooltip("Target movement speed when walking/running on ground")]
        private float moveSpeed = 20f;
        [SerializeField] private float turnSpeed = 5f;

        [Header("Movement Limits")]
        [SerializeField, Tooltip("Maximum speed when walking normally on ground")]
        private float maxGroundSpeed = 8f;
        [SerializeField, Tooltip("Maximum speed when dashing/sprinting on ground")]
        private float maxSprintSpeed = 12f;
        [SerializeField, Tooltip("Force multiplier applied when dashing")]
        private float dashForceMultiplier = 3f;

        [Header("Acceleration Control")]
        [SerializeField, Tooltip("How quickly the player accelerates to moveSpeed when on ground")]
        private float groundAcceleration = 50f;
        [SerializeField, Tooltip("How quickly the player decelerates when no input on ground")]
        private float groundDeceleration = 30f;
        [SerializeField, Tooltip("How quickly the player accelerates in air (jetpack)")]
        private float airAcceleration = 25f;
        [SerializeField, Tooltip("How long dash speed boost lasts")]
        private float dashDuration = 0.3f;

        [Header("Camera/Look Controls")]
        [SerializeField, Tooltip("Maximum upward look angle in degrees")]
        private float maxLookUpAngle = 80f;
        [SerializeField, Tooltip("Maximum downward look angle in degrees")]
        private float maxLookDownAngle = 80f;
        #endregion

        #region Private Variables
        private Vector2 keyInput;
        private float verticalInput; // Stored vertical input from jumping or jetpack thrust
        private bool interact;
        private bool dashInput;
        private bool isCursorLocked = true;
        private CoherenceSync _sync;
        private PlayerStats playerStats;
        private float dashTimer; // Tracks remaining dash time
        private float currentVerticalRotation = 0f; // Track current vertical rotation for clamping
        #endregion

        #region Unity Lifecycle Methods
        void Start()
        {
            Debug.Log($"Initializing player camera. Current camera: {(playerCamera != null ? playerCamera.name : "null")}");
            if (playerCamera == null && _sync.HasInputAuthority)
            {
                playerCamera = Camera.main;
                Debug.Log($"Using main camera: {(playerCamera != null ? playerCamera.name : "null")}");
            }

            // Get or add PlayerStats component
            playerStats = GetComponent<PlayerStats>();
            if (playerStats == null)
            {
                playerStats = gameObject.AddComponent<PlayerStats>();
                Debug.Log("Added PlayerStats component to player");
            }
        }

        protected override void OnSubjectAwake()
        {
            _sync = GetComponent<CoherenceSync>();

            base.OnSubjectAwake();

            if (_sync.HasInputAuthority)
            {
                SetCursorState(true);
                Input.ResetInputAxes();
            }
        }

        protected override void OnSubjectUpdate()
        {
            base.OnSubjectUpdate();

            if (!_sync.HasInputAuthority)
                return;

            HandleControlInputs();

            if (isCursorLocked &&
               (!DebugLogManager.Instance?.IsLogWindowVisible ?? true))
            {
                ProcessMovementInput();
                ProcessMouseLookInput();
                ProcessVerticalInput();
                ProcessInteractionInput();
            }
            else
            {
                ResetInputs();
            }
        }

        protected override void OnSubjectFixedUpdate()
        {
            base.OnSubjectFixedUpdate();

            if (!_sync.HasInputAuthority)
                return;

            ApplyMovementForces();
            ProcessInteractionRaycast();
            interact = false;
        }

        #endregion

        #region Input Processing Methods
        private void HandleControlInputs()
        {
            // Toggle cursor lock with Tab
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                SetCursorState(!isCursorLocked);
            }

            // Reload scene control
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R))
            {
                GravitasSceneManager.ReloadMainScene();
            }
        }

        private void ProcessMovementInput()
        {
            keyInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            // Handle dash input
            if (!dashInput)
                dashInput = Input.GetKeyDown(KeyCode.LeftShift);
        }

        private void ProcessMouseLookInput()
        {
            Transform t = gravitasBody.CurrentTransform;
            Vector2 mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

            // Player rotation (horizontal - X axis)
            t.rotation *= Quaternion.AngleAxis(mouseInput.x * turnSpeed, Vector3.up);

            // Aim core rotation (vertical - Y axis) with limits
            if (aimCore != null)
            {
                float verticalDelta = -mouseInput.y * turnSpeed;
                float newVerticalRotation = currentVerticalRotation + verticalDelta;

                // Clamp the vertical rotation within the specified limits
                newVerticalRotation = Mathf.Clamp(newVerticalRotation, -maxLookDownAngle, maxLookUpAngle);

                // Calculate the actual delta to apply (in case we hit the limits)
                float clampedDelta = newVerticalRotation - currentVerticalRotation;
                currentVerticalRotation = newVerticalRotation;

                // Apply the clamped rotation
                aimCore.localRotation *= Quaternion.AngleAxis(clampedDelta, Vector3.right);
            }

            if (gravitasBody.IsLanded)
            {
                if (Input.GetKeyDown(KeyCode.Space)) // Jump input
                    gravitasBody.Velocity += t.up * jumpForce;
            }
        }

        private void ProcessVerticalInput()
        {
            // Use different keys for jetpack - Q for up, LeftControl for down
            // This prevents conflict with sprint (LeftShift)
            if (Input.GetKey(KeyCode.Q))
                verticalInput = 1; // Up
            else if (Input.GetKey(KeyCode.LeftControl))
                verticalInput = -1; // Down
            else
                verticalInput = 0; // None
        }

        private void ProcessInteractionInput()
        {
            if (!interact)
                interact = Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);
        }

        private void ResetInputs()
        {
            keyInput = Vector2.zero;
            verticalInput = 0;
            interact = false;
            dashInput = false;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Convenience method to instantly set player position, orientation, and stop all velocity.
        /// </summary>
        /// <param name="position">Position to set player's subject to</param>
        /// <param name="forward">Direction to make player's subject look at</param>
        public void SetPlayerSubjectPositionAndRotation(Vector3 position, Vector3 forward)
        {
            // Setting position and stopping velocity
            //gravitasBody.ProxyPosition = position;
            gravitasBody.Velocity = Vector3.zero;

            // Setting rotation
            //SetProxyLookRotation(forward);
            gravitasBody.AngularVelocity = Vector3.zero;
        }
        #endregion

        #region Movement Methods
        private void ApplyMovementForces()
        {
            Transform t = gravitasBody.CurrentTransform;
            bool isLanded = gravitasBody.IsLanded;

            // Handle dash input (works in air and on ground)
            if (dashInput && keyInput != Vector2.zero && playerStats != null && playerStats.PerformDash())
            {
                dashTimer = dashDuration; // Start dash timer
            }

            // Update dash timer
            if (dashTimer > 0)
                dashTimer -= Time.deltaTime;

            if (!isLanded && keyInput != Vector2.zero && playerParticleSystem != null)
                playerParticleSystem.Play();

            if (isLanded)
            {
                ApplyGroundMovement(t, dashTimer > 0);
            }
            else
            {
                ApplyAirMovement(t, dashTimer > 0);
            }

            // Reset dash input after processing
            dashInput = false;
        }

        private void ApplyGroundMovement(Transform t, bool isDashing)
        {
            Vector3 currentVelocity = gravitasBody.Velocity;
            Vector3 targetVelocity = GetTargetGroundVelocity(t, isDashing);
            float currentSpeedLimit = isDashing ? maxSprintSpeed : maxGroundSpeed;

            // Calculate desired velocity change
            Vector3 velocityDifference = targetVelocity - currentVelocity;

            // Apply acceleration or deceleration
            Vector3 acceleration;
            if (keyInput.magnitude > 0.1f)
            {
                // Player is giving input - accelerate toward target
                float accel = isDashing ? groundAcceleration * dashForceMultiplier : groundAcceleration;
                acceleration = velocityDifference.normalized * accel;
            }
            else
            {
                // No input - decelerate
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(currentVelocity, t.up);
                acceleration = -horizontalVelocity.normalized * groundDeceleration;
            }

            // Clamp the resulting velocity to speed limits
            Vector3 newVelocity = currentVelocity + acceleration * Time.deltaTime;
            Vector3 horizontalNewVelocity = Vector3.ProjectOnPlane(newVelocity, t.up);

            if (horizontalNewVelocity.magnitude > currentSpeedLimit)
            {
                horizontalNewVelocity = horizontalNewVelocity.normalized * currentSpeedLimit;
                newVelocity = horizontalNewVelocity + Vector3.Project(newVelocity, t.up);
            }

            gravitasBody.AddForce((newVelocity - currentVelocity) / Time.deltaTime, ForceMode.Force);

            // Handle jumping (separate from movement acceleration)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                gravitasBody.AddForce(t.up * jumpForce, ForceMode.VelocityChange);
            }
        }

        private void ApplyAirMovement(Transform t, bool isDashing)
        {
            Vector3 inputDirection = Vector3.zero;

            // Horizontal movement
            inputDirection += keyInput.x * t.right;
            inputDirection += keyInput.y * t.forward;

            // Vertical movement (jetpack)
            inputDirection += verticalInput * t.up;

            if (inputDirection.magnitude > 0.1f)
            {
                float force = isDashing ? airAcceleration * dashForceMultiplier : airAcceleration;
                gravitasBody.AddForce(inputDirection.normalized * force, ForceMode.Force);
            }
        }

        private Vector3 GetTargetGroundVelocity(Transform t, bool isDashing)
        {
            Vector3 targetVelocity = Vector3.zero;
            float speed = isDashing ? moveSpeed * dashForceMultiplier : moveSpeed;

            // Calculate target velocity based on input
            targetVelocity += keyInput.x * t.right * speed;
            targetVelocity += keyInput.y * t.forward * speed;

            return targetVelocity;
        }

        /// <summary>
        /// Calculates target velocity for ground movement based on input and player state.
        /// </summary>
        /// <param name="t">Current transform reference</param>
        /// <param name="dashPerformed">Whether a dash was just performed</param>
        /// <returns>The target velocity vector for ground movement</returns>
        #endregion

        #region Interaction Methods
        /// <summary>
        /// Processes raycast-based interactions with world objects.
        /// Handles any object that implements IInteractable.
        /// </summary>
        private void ProcessInteractionRaycast()
        {
            if (Physics.Raycast(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                out RaycastHit hitInfo,
                INTERACTION_DISTANCE,
                interactableLayers,
                QueryTriggerInteraction.Ignore))
            {
                //Debug.Log($"Hit object: {hitInfo.collider.name}");
                HandleInteraction(hitInfo.collider);
            }
            else
            {
                OnInteractionTargetEvent?.Invoke(string.Empty);
            }
        }

        /// <summary>
        /// Handles interaction with any object that implements IInteractable.
        /// </summary>
        /// <param name="collider">The collider of the object to interact with</param>
        private void HandleInteraction(Collider collider)
        {
            // Check if the object implements IInteractable
            if (collider.TryGetComponent(out IInteractable interactable))
            {
                if (interactable.CanInteract)
                {
                    if (interact && _sync.HasInputAuthority)
                    {
                        interactable.Interact(this);
                        OnInteractionTargetEvent?.Invoke(string.Empty);
                    }
                    else if (_sync.HasInputAuthority)
                    {
                        OnInteractionTargetEvent?.Invoke(interactable.InteractionPrompt);
                    }
                }
            }
        }
        #endregion

        #region Utility Methods
        private void SetCursorState(bool locked)
        {
            isCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnApplicationFocus(bool focusStatus)
        {
            Input.ResetInputAxes();
        }
        #endregion
    }
}