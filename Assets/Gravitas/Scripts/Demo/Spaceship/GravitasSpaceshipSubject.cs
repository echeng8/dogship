using UnityEngine;
using UnityEngine.Events;

namespace Gravitas.Demo
{
    /// <summary>
    /// Another utility controls class representing functionality to move the spaceship object in scene.
    /// </summary>
    internal sealed class GravitasSpaceshipSubject : GravitasSubject
    {
        public GravitasFirstPersonPlayerSubject PlayerSubject
        {
            get; private set;
        }

        [SerializeField] private ParticleSystem[] spaceshipParticles; // The movement spaceship particles
        private Quaternion previousCameraRotation;
        private Transform playerCameraTransform;
        [SerializeField] private Transform spaceshipCameraPosition;
        [SerializeField] private UnityEvent
            onEnterShip,
            onExitShip;
        private Vector3 previousCameraPosition;
        private Vector2 keyInput;
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float turnSpeed = 5f;
        [SerializeField] private float rollSpeed = 2.5f;
        [SerializeField] private float verticalMoveMultiplier = 3f;
        private float
            rollInput,
            verticalInput;

        public void SetPlayerController(GravitasFirstPersonPlayerSubject player, Vector3 localPos)
        {
            // Assigning and resetting player position, disabling player controls
            PlayerSubject = player;
            PlayerSubject.SetPlayerSubjectPositionAndRotation
            (
                localPos + new Vector3(0, 0.5f, -1f),
                Vector3.forward
            );
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

        /// <summary>
        /// Intended to be called from a spaceship reset button, resets spaceship velocity and reorients to default.
        /// </summary>
        public void ResetSpaceship()
        {
            gravitasBody.AngularVelocity = Vector3.zero;
            gravitasBody.Velocity = Vector3.zero;
            gravitasBody.CurrentTransform.rotation = Quaternion.identity;
        }

        protected override void OnSubjectUpdate()
        {
            base.OnSubjectUpdate();

            GravitasFirstPersonPlayerSubject playerSubject = PlayerSubject;
            if (playerSubject) // If spaceship is controlled
            {
                Transform t = gravitasBody.CurrentTransform; // Reference to either the spaceship or its proxy's transform

                if (Input.GetKeyDown(KeyCode.E)) // Exit from controlling spaceship
                {
                    playerSubject.enabled = true;

                    if (playerCameraTransform)
                    {
                        playerCameraTransform.SetParent(PlayerSubject.transform);
                        playerCameraTransform.SetLocalPositionAndRotation(previousCameraPosition, previousCameraRotation);
                    }

                    PlayerSubject = null;
                    playerCameraTransform = null;

                    onExitShip?.Invoke();

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
                {
                    gravitasBody.AngularVelocity = Vector3.zero;
                }
                else if (Input.GetKeyDown(KeyCode.X)) // Reset velocity
                {
                    gravitasBody.Velocity = Vector3.zero;
                }
            }
        }

        protected override void OnSubjectFixedUpdate()
        {
            base.OnSubjectFixedUpdate();

            if (PlayerSubject) // If spaceship is controlled
            {
                Transform t = gravitasBody.CurrentTransform; // Reference to either the spaceship or its proxy's transform

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
    }
}
