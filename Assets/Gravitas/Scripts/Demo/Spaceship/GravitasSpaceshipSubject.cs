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

        [Header("Stamina Settings")]
        [Tooltip("Rate at which the spaceship drains stamina per second from the controlling player")]
        public float staminaDrainRate = 25f;

        private float rollInput, verticalInput;
        private CoherenceSync _sync;

        protected override void OnSubjectAwake()
        {
            base.OnSubjectAwake();
            _sync = GetComponent<CoherenceSync>();

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
                gravitasBody.AngularVelocity = Vector3.zero;
                gravitasBody.Velocity = Vector3.zero;
                gravitasBody.CurrentTransform.rotation = Quaternion.identity;
            }
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
                    gravitasBody.AngularVelocity = Vector3.zero;
                else if (Input.GetKeyDown(KeyCode.X)) // Reset velocity
                    gravitasBody.Velocity = Vector3.zero;
            }
        }

        protected override void OnSubjectFixedUpdate()
        {
            base.OnSubjectFixedUpdate();

            // Only apply forces if we have both a controlling player AND authority
            if (PlayerSubject && _sync && _sync.HasInputAuthority && _sync.HasStateAuthority)
            {
                Transform t = gravitasBody.CurrentTransform;

                Vector3 velocity = Vector3.zero;
                velocity += keyInput.x * t.right;
                velocity += verticalInput * verticalMoveMultiplier * t.up;
                velocity += keyInput.y * t.forward;
                velocity *= moveSpeed * Time.deltaTime;

                gravitasBody.AddForce(velocity, ForceMode.VelocityChange);

                Vector3 angularVelocity = gravitasBody.CurrentTransform.forward * -rollInput;
                angularVelocity *= rollSpeed * Time.fixedDeltaTime;

                gravitasBody.AddTorque(angularVelocity, ForceMode.VelocityChange);

                // Spaceship movement particle playing
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
    }
}
