using System;
using UnityEngine;
using Coherence.Toolkit;
using IngameDebugConsole;

namespace Gravitas.Demo
{
    /// <summary>
    /// Implementation of a first person player controller that interacts with Gravitas systems.
    /// Handles player movement, camera control, and interaction with game objects.
    /// </summary>
    public class GravitasFirstPersonPlayerSubject : GravitasSubject
    {
        #region Constants
        private const float MAX_GROUND_SPEED = 8f;
        private const float INTERACTION_DISTANCE = 2.75f;
        #endregion

        #region Events
        public event Action<string> OnInteractionTargetEvent;
        #endregion

        #region Public Properties
        public Camera PlayerCamera => playerCamera;
        #endregion

        #region SerializeField Variables
        [Header("Components")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private ParticleSystem playerParticleSystem;

        [Header("Settings")]
        [SerializeField] private LayerMask interactableLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private float jetpackForce = 15f;
        [SerializeField] private float jumpForce = 10f;
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float turnSpeed = 5f;
        #endregion

        #region Private Variables
        private Vector2 keyInput;
        private float angleX; // Stored camera pitch value
        private float verticalInput; // Stored vertical input from jumping or jetpack thrust
        private bool interact;
        private bool isCursorLocked = true;
        private CoherenceSync _sync;
        #endregion

        #region Unity Lifecycle Methods
        void Start()
        {
            Debug.Log($"Initializing player camera. Current camera: {(playerCamera != null ? playerCamera.name : "null")}");
            if (playerCamera == null && _sync.HasInputAuthority)
            {
                playerCamera = Camera.main;
                Debug.Log($"Using main camera: {(playerCamera != null ? playerCamera.name : "null")}");
                playerCamera.transform.SetParent(transform);
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
        }

        private void ProcessMouseLookInput()
        {
            Transform t = gravitasBody.CurrentTransform;
            Vector2 mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

            // Player rotating
            t.rotation *= Quaternion.AngleAxis(mouseInput.x * turnSpeed, Vector3.up);

            // Camera pitching
            angleX += -mouseInput.y * turnSpeed;

            if (gravitasBody.IsLanded)
            {
                angleX = Mathf.Clamp(angleX, -90f, 90f);

                if (Input.GetKeyDown(KeyCode.Space)) // Jump input
                    gravitasBody.Velocity += t.up * jumpForce;
            }

            playerCamera.transform.localRotation = Quaternion.Euler(angleX, 0, 0);
        }

        private void ProcessVerticalInput()
        {
            if (Input.GetKey(KeyCode.LeftShift))
                verticalInput = 1; // Up
            else if (Input.GetKey(KeyCode.LeftControl))
                verticalInput = -1; // Down
            else
                verticalInput = 0; // None
        }

        private void ProcessInteractionInput()
        {
            if (!interact)
                interact = Input.GetKeyDown(KeyCode.E);
        }

        private void ResetInputs()
        {
            keyInput = Vector2.zero;
            verticalInput = 0;
            interact = false;
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

            // Setting rotation and stopping angular velocity
            if (playerCamera != null)
                playerCamera.transform.localRotation = Quaternion.identity;
            //SetProxyLookRotation(forward);
            gravitasBody.AngularVelocity = Vector3.zero;
        }
        #endregion

        #region Movement Methods
        private void ApplyMovementForces()
        {
            Transform t = gravitasBody.CurrentTransform;
            bool isLanded = gravitasBody.IsLanded;
            Vector3 inputVelocity = GetInputVelocity(t, isLanded);

            if (!isLanded && inputVelocity != Vector3.zero && playerParticleSystem != null)
                playerParticleSystem.Play();

            if (!isLanded || gravitasBody.Velocity.magnitude < MAX_GROUND_SPEED)
                gravitasBody.AddForce(inputVelocity * Time.deltaTime, ForceMode.VelocityChange);
        }

        /// <summary>
        /// Calculates movement velocity based on current input and player state.
        /// </summary>
        /// <param name="t">Current transform reference</param>
        /// <param name="isLanded">Whether the player is currently landed</param>
        /// <returns>The calculated velocity vector</returns>
        private Vector3 GetInputVelocity(Transform t, bool isLanded)
        {
            Vector3 velocity = Vector3.zero;

            // Left-Right movement
            Vector3 right = t.right;
            float xForce = isLanded ? moveSpeed : jetpackForce;
            velocity += keyInput.x * xForce * right;

            // Up-Down movement
            float yForce = isLanded ? jumpForce : jetpackForce;
            velocity += verticalInput * yForce * t.up;

            // Forward-Back movement
            Vector3 forward = t.forward;
            float zForce = isLanded ? moveSpeed : jetpackForce;
            velocity += keyInput.y * zForce * forward;

            return velocity;
        }
        #endregion

        #region Interaction Methods
        /// <summary>
        /// Processes raycast-based interactions with world objects.
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
                Debug.Log($"Hit object: {hitInfo.collider.name}");
                HandleInteractableObject(hitInfo.collider);
            }
            else
            {
                OnInteractionTargetEvent?.Invoke(string.Empty);
            }
        }

        /// <summary>
        /// Handles interaction with any IInteractable object.
        /// </summary>
        /// <param name="collider">The collider of the object to interact with</param>
        private void HandleInteractableObject(Collider collider)
        {
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