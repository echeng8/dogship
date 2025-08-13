using UnityEngine;
using UnityEngine.Events;
using Coherence.Toolkit;
using Coherence;

namespace Gravitas.Demo
{
    /// <summary>
    /// Another utility controls class representing functionality to move the spaceship object in scene.
    /// </summary>
    public sealed class GravitasSpaceshipSubject : GravitasSubject
    {
        public GravitasFirstPersonPlayerSubject PlayerSubject { get; private set; }
        public bool IsControlled => PlayerSubject != null;

        [Sync] public bool NetworkIsControlled { get; private set; }
        [Sync] public string ControllingPlayerID { get; private set; } = "";

        [SerializeField] private ParticleSystem[] spaceshipParticles;
        private Quaternion previousCameraRotation;
        private Transform playerCameraTransform;
        [SerializeField] private Transform spaceshipCameraPosition;
        [SerializeField] private UnityEvent onEnterShip, onExitShip;
        private Vector3 previousCameraPosition;
        private Vector2 keyInput;
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float turnSpeed = 5f;
        [SerializeField] private float rollSpeed = 2.5f;
        [SerializeField] private float verticalMoveMultiplier = 3f;
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
        public void NetworkSetPlayerController(string playerID, Vector3 localPos)
        {
            Debug.Log($"NetworkSetPlayerController received. PlayerID: {playerID}, NetworkIsControlled: {NetworkIsControlled}");

            if (!NetworkIsControlled && !string.IsNullOrEmpty(playerID))
            {
                var player = FindPlayerByID(playerID);
                Debug.Log($"Found player by ID: {player?.name}");

                if (player != null)
                {
                    SetPlayerControllerInternal(player, localPos);
                    NetworkIsControlled = true;
                    ControllingPlayerID = playerID;
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
                ControllingPlayerID = "";
            }
        }

        public void SetPlayerController(GravitasFirstPersonPlayerSubject player, Vector3 localPos)
        {
            Debug.Log($"SetPlayerController called with player: {player?.name}");

            if (player && _sync)
            {
                var playerSync = player.GetComponent<CoherenceSync>();
                string playerID = playerSync?.ManualUniqueId ?? "";

                Debug.Log($"Player ID: {playerID}, Sync component: {_sync != null}");

                if (!string.IsNullOrEmpty(playerID))
                {
                    Debug.Log("Sending NetworkSetPlayerController command");

                    // Send command to authority to set player controller
                    _sync.SendCommand<GravitasSpaceshipSubject>(
                        nameof(NetworkSetPlayerController),
                        MessageTarget.AuthorityOnly,
                        playerID,
                        localPos
                    );

                    // Request authority transfer after setting controller
                    Debug.Log("Requesting authority transfer");
                    _sync.RequestAuthority(AuthorityType.Full);
                }
                else
                {
                    Debug.LogError("Player ID is null or empty!");
                }
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

            if (spaceshipCameraPosition)
            {
                playerCameraTransform = PlayerSubject.PlayerCamera.transform;
                playerCameraTransform.GetLocalPositionAndRotation(out previousCameraPosition, out previousCameraRotation);
                playerCameraTransform.SetParent(spaceshipCameraPosition);
                playerCameraTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            onEnterShip?.Invoke();
        }

        private void ExitSpaceshipInternal()
        {
            if (PlayerSubject)
            {
                PlayerSubject.enabled = true;

                if (playerCameraTransform)
                {
                    playerCameraTransform.SetParent(PlayerSubject.transform);
                    playerCameraTransform.SetLocalPositionAndRotation(previousCameraPosition, previousCameraRotation);
                }

                PlayerSubject = null;
                playerCameraTransform = null;
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
                var controllingPlayer = FindPlayerByID(ControllingPlayerID);
                if (controllingPlayer)
                {
                    SetPlayerControllerInternal(controllingPlayer, Vector3.zero);
                }
            }
            else if (!NetworkIsControlled && IsControlled)
            {
                ExitSpaceshipInternal();
            }

            // Only process input if we have both a controlling player AND authority
            if (PlayerSubject && _sync && _sync.HasInputAuthority && _sync.HasStateAuthority)
            {
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

        private GravitasFirstPersonPlayerSubject FindPlayerByID(string uniqueId)
        {
            var allPlayers = FindObjectsOfType<GravitasFirstPersonPlayerSubject>();
            foreach (var player in allPlayers)
            {
                var sync = player.GetComponent<CoherenceSync>();
                if (sync && sync.ManualUniqueId == uniqueId)
                {
                    return player;
                }
            }
            return null;
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
