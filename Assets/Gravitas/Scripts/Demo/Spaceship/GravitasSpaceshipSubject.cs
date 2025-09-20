using UnityEngine;
using UnityEngine.Events;
using Coherence.Toolkit;
using Coherence;
using Unity.Cinemachine;

namespace Gravitas.Demo
{
    /// <summary>
    /// Another utility controls class representing functionality to move the spaceship object in scene.
    /// </summary>
    public sealed class GravitasSpaceshipSubject : GravitasSubject
    {
        public GravitasFirstPersonPlayerSubject PlayerSubject { get; private set; }
        public bool IsControlled => PlayerSubject != null;

        [Sync] public bool NetworkIsControlled;
        [Sync] public GameObject ControllingPlayerGameObject;

        [SerializeField] private ParticleSystem[] spaceshipParticles;
        [SerializeField] private CinemachineCamera spaceshipCamera;
        [SerializeField] private UnityEvent onEnterShip, onExitShip;
        private Vector2 keyInput;
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float turnSpeed = 5f;
        [SerializeField] private float rollSpeed = 2.5f;
        [SerializeField] private float verticalMoveMultiplier = 3f;

        [Header("Angular Velocity Settings")]
        [Tooltip("If true, disables physics-based angular velocity and only allows player input control")]
        [SerializeField] private bool disablePhysicsAngularVelocity = true;
        [Tooltip("Current angular velocity controlled only by player input")]
        private Vector3 playerControlledAngularVelocity = Vector3.zero;

        [Header("Stamina Settings")]
        [Tooltip("Rate at which the spaceship drains stamina per second from the controlling player")]
        public float staminaDrainRate = 25f;

        [Header("Match Velocity Settings")]
        [Tooltip("Layers to search for target subjects to match velocity with")]
        [SerializeField] private LayerMask targetLayers = -1;
        [Tooltip("Maximum detection range for finding target subjects")]
        [SerializeField] private float detectionRange = 10000000f;
        [Tooltip("Key to hold for match velocity mode")]
        [SerializeField] private KeyCode matchVelocityKey = KeyCode.G;

        private float rollInput, verticalInput;
        private CoherenceSync _sync;
        private bool isMatchingVelocity;
        private GravitasSubject targetSubject;

        protected override void OnSubjectAwake()
        {
            base.OnSubjectAwake();
            _sync = GetComponent<CoherenceSync>();

            // Configure rigidbody for angular velocity control mode
            if (disablePhysicsAngularVelocity && gravitasBody?.CurrentRigidbody != null)
            {
                // Increase angular damping to help dampen unwanted rotation
                gravitasBody.CurrentRigidbody.angularDamping = 5f;
                Debug.Log($"Spaceship {name}: Physics angular velocity disabled - using player input only");
            }

            // Subscribe to authority events
            _sync.OnStateAuthority.AddListener(OnGainedAuthority);
            _sync.OnStateRemote.AddListener(OnLostAuthority);
        }

        [Command]
        public void NetworkSetPlayerController(GameObject playerGameObject, Vector3 localPos)
        {
            Debug.Log($"NetworkSetPlayerController received. PlayerGameObject: {playerGameObject?.name}, NetworkIsControlled: {NetworkIsControlled}");

            if (!NetworkIsControlled && playerGameObject != null)
            {
                var player = playerGameObject.GetComponent<GravitasFirstPersonPlayerSubject>();
                if (player != null)
                {
                    Debug.Log($"Found player component: {player.name}");

                    SetPlayerControllerInternal(player, localPos);
                    NetworkIsControlled = true;
                    ControllingPlayerGameObject = playerGameObject;
                    Debug.Log($"Successfully set player controller for {player.name}");
                }
            }
        }

        [Command]
        public void NetworkExitSpaceship()
        {
            if (NetworkIsControlled && PlayerSubject)
            {
                ExitSpaceshipInternal();
                NetworkIsControlled = false;
                ControllingPlayerGameObject = null;
            }
        }

        [Command]
        public void NetworkTeleportToEarth()
        {
            // Find the earth field and teleport the ship to its position
            var earthField = FindEarthField();
            if (earthField != null)
            {
                // Find the earth position by tag
                GameObject earthPosition = GameObject.FindGameObjectWithTag("EarthPosition");
                if (earthPosition != null)
                {
                    ScheduleTeleport(earthPosition.transform.position, earthField);
                }
            }
        }

        private GravitasField FindEarthField()
        {
            // Find the earth position by tag
            GameObject earthPosition = GameObject.FindGameObjectWithTag("EarthPosition");
            if (earthPosition == null)
            {
                return null;
            }

            // Check if earth position or its parent has a GravitasField component
            GravitasField earthField = earthPosition.GetComponent<GravitasField>();
            if (earthField == null && earthPosition.transform.parent != null)
            {
                // Try to find field on parent
                earthField = earthPosition.transform.parent.GetComponent<GravitasField>();
            }

            return earthField;
        }

        public void SetPlayerController(GravitasFirstPersonPlayerSubject player, Vector3 localPos)
        {
            Debug.Log($"SetPlayerController called with player: {player?.name}");

            if (player && _sync)
            {
                Debug.Log($"Player: {player.name}, Sync component: {_sync != null}");

                Debug.Log("Sending NetworkSetPlayerController command");

                // Send command to authority to set player controller
                _sync.SendCommand<GravitasSpaceshipSubject>(
                    nameof(NetworkSetPlayerController),
                    MessageTarget.AuthorityOnly,
                    player.gameObject,
                    localPos
                );

                // Request authority transfer after setting controller
                Debug.Log("Requesting authority transfer");
                _sync.RequestAuthority(AuthorityType.Full);
            }
            else
            {
                Debug.LogError($"SetPlayerController failed. Player: {player != null}, Sync: {_sync != null}");
            }
        }

        public void ExitSpaceship()
        {
            if (_sync && PlayerSubject)
            {
                _sync.SendCommand<GravitasSpaceshipSubject>(
                    nameof(NetworkExitSpaceship),
                    MessageTarget.AuthorityOnly
                );
            }
        }

        public void TeleportToEarth()
        {
            if (_sync)
            {
                _sync.SendCommand<GravitasSpaceshipSubject>(
                    nameof(NetworkTeleportToEarth),
                    MessageTarget.AuthorityOnly
                );
            }
            else
            {
                // Fallback for non-networked scenarios
                NetworkTeleportToEarth();
            }
        }

        private void SetPlayerControllerInternal(GravitasFirstPersonPlayerSubject player, Vector3 localPos)
        {
            PlayerSubject = player;
            PlayerSubject.SetPlayerSubjectPositionAndRotation(localPos + new Vector3(0, 0.5f, -1f), Vector3.forward);
            PlayerSubject.enabled = false;

            // Enable Cinemachine camera for local player only
            if (spaceshipCamera && PlayerSubject.PlayerCamera != null)
            {
                spaceshipCamera.enabled = true;
            }

            onEnterShip?.Invoke();
        }

        private void ExitSpaceshipInternal()
        {
            if (PlayerSubject)
            {
                PlayerSubject.enabled = true;

                // Disable Cinemachine camera for local player only
                if (spaceshipCamera && PlayerSubject.PlayerCamera != null)
                {
                    spaceshipCamera.enabled = false;
                }

                PlayerSubject = null;
                onExitShip?.Invoke();
            }
        }

        public void ResetSpaceship()
        {
            if (_sync && _sync.HasStateAuthority)
            {
                if (disablePhysicsAngularVelocity)
                {
                    playerControlledAngularVelocity = Vector3.zero;
                    gravitasBody.AngularVelocity = Vector3.zero;
                }
                else
                {
                    gravitasBody.AngularVelocity = Vector3.zero;
                }
                gravitasBody.Velocity = Vector3.zero;
                gravitasBody.CurrentTransform.rotation = Quaternion.identity;
            }
        }

        private GravitasSubject FindNearestTargetSubject()
        {
            Vector3 shipPosition = gravitasBody.CurrentTransform.position;
            GravitasSubject nearestTarget = null;
            float nearestDistance = detectionRange;

            // Find all colliders within detection range
            Collider[] colliders = Physics.OverlapSphere(shipPosition, detectionRange, targetLayers);

            foreach (Collider col in colliders)
            {
                // Skip self
                if (col.transform == gravitasBody.CurrentTransform)
                    continue;

                // Look for GravitasSubject component
                GravitasSubject subject = col.GetComponent<GravitasSubject>();
                if (subject == null)
                    subject = col.GetComponentInParent<GravitasSubject>();

                if (subject != null && subject != this)
                {
                    float distance = Vector3.Distance(shipPosition, subject.GravitasBody.CurrentTransform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestTarget = subject;
                    }
                }
            }

            return nearestTarget;
        }

        protected override void OnSubjectUpdate()
        {
            base.OnSubjectUpdate();

            // Sync local player state with network state
            if (NetworkIsControlled && !IsControlled)
            {
                if (ControllingPlayerGameObject)
                {
                    var controllingPlayer = ControllingPlayerGameObject.GetComponent<GravitasFirstPersonPlayerSubject>();
                    if (controllingPlayer)
                    {
                        SetPlayerControllerInternal(controllingPlayer, Vector3.zero);
                    }
                }
            }
            else if (!NetworkIsControlled && IsControlled)
            {
                ExitSpaceshipInternal();
            }

            // Only process input if we have both a controlling player AND authority
            if (PlayerSubject && _sync && _sync.HasInputAuthority && _sync.HasStateAuthority)
            {
                // Drain stamina from the controlling player
                var playerStats = PlayerSubject.GetComponent<Gravitas.PlayerStats>();
                if (playerStats != null)
                {
                    playerStats.DrainStamina(staminaDrainRate * Time.deltaTime);
                }

                Transform t = gravitasBody.CurrentTransform;

                if (Input.GetKeyDown(KeyCode.E))
                {
                    ExitSpaceship();
                    return;
                }

                // Match velocity input handling
                isMatchingVelocity = Input.GetKey(matchVelocityKey);
                if (isMatchingVelocity)
                {
                    // Find or update target
                    if (targetSubject == null || Vector3.Distance(gravitasBody.CurrentTransform.position, targetSubject.GravitasBody.CurrentTransform.position) > detectionRange)
                    {
                        GravitasSubject newTarget = FindNearestTargetSubject();
                        if (newTarget != targetSubject)
                        {
                            targetSubject = newTarget;
                            if (targetSubject != null)
                            {
                                Debug.Log($"Match Velocity: Now targeting {targetSubject.name} at distance {Vector3.Distance(gravitasBody.CurrentTransform.position, targetSubject.GravitasBody.CurrentTransform.position):F1}m");
                            }
                            else
                            {
                                Debug.Log("Match Velocity: No target found within range");
                            }
                        }
                    }
                }
                else
                {
                    if (targetSubject != null)
                    {
                        Debug.Log("Match Velocity: Deactivated");
                        targetSubject = null;
                    }
                }

                // Movement input collection
                keyInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

                Vector2 mouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                t.rotation *= Quaternion.AngleAxis(mouseInput.x * turnSpeed, Vector3.up);
                t.rotation *= Quaternion.AngleAxis(-mouseInput.y * turnSpeed, Vector3.right);

                rollInput = Input.GetAxis("Roll");

                // Vertical input processing
                if (Input.GetKey(KeyCode.LeftShift))
                    verticalInput = 1;
                else if (Input.GetKey(KeyCode.LeftControl))
                    verticalInput = -1;
                else
                    verticalInput = 0;

                if (Input.GetKeyDown(KeyCode.R)) // Reset angular velocity
                {
                    if (disablePhysicsAngularVelocity)
                    {
                        playerControlledAngularVelocity = Vector3.zero;
                        gravitasBody.AngularVelocity = Vector3.zero;
                    }
                    else
                    {
                        gravitasBody.AngularVelocity = Vector3.zero;
                    }
                }
                else if (Input.GetKeyDown(KeyCode.X)) // Reset velocity
                    gravitasBody.Velocity = Vector3.zero;
            }
        }

        protected override void OnSubjectFixedUpdate()
        {
            base.OnSubjectFixedUpdate();

            // Ensure physics angular velocity is disabled when feature is active
            if (disablePhysicsAngularVelocity && _sync && _sync.HasStateAuthority)
            {
                // Override any physics-applied angular velocity with our player-controlled value
                // This ensures external forces/torques don't affect rotation
                gravitasBody.AngularVelocity = playerControlledAngularVelocity;
            }

            // Only apply forces if we have both a controlling player AND authority
            if (PlayerSubject && _sync && _sync.HasInputAuthority && _sync.HasStateAuthority)
            {
                Transform t = gravitasBody.CurrentTransform;

                // Start with player input
                Vector2 finalKeyInput = keyInput;
                float finalVerticalInput = verticalInput;

                // Add simulated input from match velocity system if active and we have a target
                if (isMatchingVelocity && targetSubject != null)
                {
                    // Calculate velocity difference between our ship and the target
                    Vector3 ourVelocity = AbsoluteVelocity;
                    Vector3 targetVelocity = targetSubject.AbsoluteVelocity;
                    Vector3 velocityDifference = targetVelocity - ourVelocity;

                    // Only apply input if there's a meaningful velocity difference
                    if (velocityDifference.magnitude > 0.01f)
                    {
                        // Convert velocity difference to local space input
                        Vector3 localVelocityDiff = t.InverseTransformDirection(velocityDifference);

                        // Calculate additional input based on velocity difference (precise, no scaling)
                        // Normalize by move speed to convert from velocity units to input units
                        Vector2 additionalInput = new Vector2(
                            localVelocityDiff.x / moveSpeed,
                            localVelocityDiff.z / moveSpeed
                        );

                        float additionalVerticalInput = localVelocityDiff.y / (moveSpeed * verticalMoveMultiplier);

                        // Add simulated input to player input, clamping to realistic input ranges
                        finalKeyInput.x = Mathf.Clamp(finalKeyInput.x + additionalInput.x, -1f, 1f);
                        finalKeyInput.y = Mathf.Clamp(finalKeyInput.y + additionalInput.y, -1f, 1f);
                        finalVerticalInput = Mathf.Clamp(finalVerticalInput + additionalVerticalInput, -1f, 1f);
                    }
                }

                // Calculate movement forces using combined input (player + simulated)
                Vector3 velocity = Vector3.zero;
                velocity += finalKeyInput.x * t.right;
                velocity += finalVerticalInput * verticalMoveMultiplier * t.up;
                velocity += finalKeyInput.y * t.forward;
                velocity *= moveSpeed * Time.deltaTime;

                gravitasBody.AddForce(velocity, ForceMode.VelocityChange);

                // Handle angular velocity based on physics mode
                if (disablePhysicsAngularVelocity)
                {
                    // Player-controlled angular velocity only
                    Vector3 rollTorque = gravitasBody.CurrentTransform.forward * -rollInput;
                    rollTorque *= rollSpeed * Time.fixedDeltaTime;

                    // Add to player-controlled angular velocity instead of using AddTorque
                    playerControlledAngularVelocity += rollTorque;

                    // Apply drag to prevent infinite acceleration
                    float angularDrag = 0.95f; // Adjust this value to control roll responsiveness
                    playerControlledAngularVelocity *= angularDrag;

                    // Directly set the angular velocity, overriding any physics influences
                    gravitasBody.AngularVelocity = playerControlledAngularVelocity;
                }
                else
                {
                    // Standard physics-based angular velocity
                    Vector3 angularVelocity = gravitasBody.CurrentTransform.forward * -rollInput;
                    angularVelocity *= rollSpeed * Time.fixedDeltaTime;
                    gravitasBody.AddTorque(angularVelocity, ForceMode.VelocityChange);
                }

                // Spaceship movement particle playing (based on combined input including match velocity)
                if (spaceshipParticles != null && velocity != Vector3.zero)
                {
                    for (int i = 0; i < spaceshipParticles.Length; i++)
                    {
                        spaceshipParticles[i].Play();
                    }
                }
            }
        }

        private void OnGainedAuthority()
        {
            Debug.Log($"Spaceship {name} gained authority");
        }

        private void OnLostAuthority()
        {
            Debug.Log($"Spaceship {name} lost authority");
        }

        private void OnDestroy()
        {
            if (_sync)
            {
                _sync.OnStateAuthority.RemoveListener(OnGainedAuthority);
                _sync.OnStateRemote.RemoveListener(OnLostAuthority);
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || gravitasBody == null) return;

            // Draw detection range when match velocity is active
            if (isMatchingVelocity)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(gravitasBody.CurrentTransform.position, detectionRange);

                // Draw line to target if we have one
                if (targetSubject != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(gravitasBody.CurrentTransform.position, targetSubject.GravitasBody.CurrentTransform.position);

                    // Draw target indicator
                    Gizmos.DrawWireSphere(targetSubject.GravitasBody.CurrentTransform.position, 2f);
                }
            }

            // Visual indicator for physics angular velocity disabled mode
            if (disablePhysicsAngularVelocity)
            {
                Gizmos.color = Color.red;
                Vector3 pos = gravitasBody.CurrentTransform.position;
                // Draw a small cross to indicate physics angular velocity is disabled
                Gizmos.DrawLine(pos + Vector3.up * 3f, pos + Vector3.up * 5f);
                Gizmos.DrawLine(pos + Vector3.right * 4f, pos + Vector3.right * 6f);
                Gizmos.DrawLine(pos + Vector3.right * -4f, pos + Vector3.right * -6f);
            }
        }
    }
}
