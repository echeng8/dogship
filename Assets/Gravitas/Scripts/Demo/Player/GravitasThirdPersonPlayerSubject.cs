using UnityEngine;

namespace Gravitas.Demo
{
    internal sealed class GravitasThirdPersonPlayerSubject : GravitasSubject
    {
        private const float MAX_GROUND_SPEED = 8f; // Maximum speed to clamp player movement to when player is landed

        private Camera playerCamera;
        [SerializeField] private ParticleSystem playerParticleSystem;
        private Vector2 keyInput;
        [SerializeField] private float cameraOrbitSpeed = 10f;
        [SerializeField] private float jumpForce = 20f;
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float scrollSpeed = 2f;
        [SerializeField] private float turnSpeed = 5f;
        private float
            angleX,
            camDist = 10f;

        protected override void OnSubjectAwake()
        {
            // Locking cursor to window and making it invisible
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Acquiring the relevant player components
            playerCamera = GetComponentInChildren<Camera>();

            base.OnSubjectAwake();
        }

        protected override void OnSubjectUpdate()
        {
            base.OnSubjectUpdate();

            // Reload scene control
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R))
            {
                GravitasSceneManager.ReloadMainScene();

                return;
            }

            Transform t = gravitasBody.CurrentTransform; // Reference to either the player or the player's proxy transform
            bool isLanded = gravitasBody.IsLanded;

            // Movement input processing
            keyInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            // Player rotating
            Vector2 mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

            t.rotation *= Quaternion.AngleAxis(mouseInput.x * turnSpeed, Vector3.up);

            angleX -= mouseInput.y * turnSpeed;
            angleX = Mathf.Clamp(angleX, 0f, 90f);
            Quaternion qt = Quaternion.AngleAxis(angleX, Vector3.right);
            playerCamera.transform.parent.localRotation = Quaternion.Lerp(playerCamera.transform.parent.localRotation, qt, Time.deltaTime * cameraOrbitSpeed);

            float mouseScroll = Input.GetAxis("Mouse ScrollWheel");
            if (mouseScroll != 0)
            {
                float scrollAmt = mouseScroll * scrollSpeed;
                scrollAmt *= camDist * 0.3f;

                camDist += scrollAmt * -1f;
                camDist = Mathf.Clamp(camDist, 1.5f, 100f);
            }

            if (playerCamera.transform.localPosition.z != camDist * 1f)
            {
                playerCamera.transform.localPosition = new Vector3(0f, 0f, Mathf.Lerp(playerCamera.transform.localPosition.z, camDist * -1f, Time.deltaTime * scrollSpeed));
            }

            // Jump input
            if (isLanded && Input.GetKeyDown(KeyCode.Space))
            {
                gravitasBody.AddForce(t.up * jumpForce, ForceMode.Impulse);
            }
        }

        protected override void OnSubjectFixedUpdate()
        {
            Transform t = gravitasBody.CurrentTransform; // Reference to either the player or the player's proxy transform
            bool isLanded = gravitasBody.IsLanded;

            Vector3 inputVelocity = CalculatePlayerVelocity();

            if (isLanded && inputVelocity != Vector3.zero && playerParticleSystem != null)
            {
                playerParticleSystem.Play();
            }

            if (!isLanded || gravitasBody.Velocity.magnitude < MAX_GROUND_SPEED)
                gravitasBody.AddForce(inputVelocity * Time.deltaTime, ForceMode.VelocityChange);

            base.OnSubjectFixedUpdate();

            Vector3 CalculatePlayerVelocity()
            {
                Vector3 velocity = Vector3.zero;

                // Left-Right movement
                Vector3 right = isLanded ? t.right : transform.right;
                velocity += keyInput.x * moveSpeed * right;

                // Forward-Back movement
                Vector3 forward = isLanded ? t.forward : transform.forward;
                velocity += keyInput.y * moveSpeed * forward;

                return velocity;
            }
        }
    }
}
