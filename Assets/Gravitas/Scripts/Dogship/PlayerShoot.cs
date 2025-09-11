using UnityEngine;
using UnityEngine.Events;

namespace Gravitas
{
    /// <summary>
    /// Handles player shooting mechanics with raycast hit detection and UnityEvents for animations.
    /// </summary>
    public class PlayerShoot : MonoBehaviour
    {
        #region Unity Events
        [Header("Animation Events")]
        [Tooltip("Called when the player starts shooting")]
        public UnityEvent OnShootStart;

        [Tooltip("Called when the player hits a target - passes hit info")]
        public UnityEvent<Vector3, Vector3, GameObject> OnHitTarget; // startPos, endPos, hitObject

        [Tooltip("Called when the shot misses (no hit) - passes shoot info")]
        public UnityEvent<Vector3, Vector3> OnShootMiss; // startPos, endPos

        [Tooltip("Called when the player cannot shoot (not enough stamina)")]
        public UnityEvent OnShootFailed;
        #endregion

        #region SerializeField Variables
        [Header("Shooting Settings")]
        [SerializeField] private Transform shootPoint;
        [SerializeField] private float shootRange = 50f;
        [SerializeField] private LayerMask shootableLayers = -1;
        [SerializeField] private bool showDebugRay = true;
        [SerializeField] private float debugRayDuration = 1f;

        [Header("Input")]
        [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;
        #endregion

        #region Public Properties
        public float ShootRange => shootRange;
        public Transform ShootPoint => shootPoint;
        #endregion

        #region Private Variables
        private PlayerStats playerStats;
        private Camera playerCamera;
        #endregion

        #region Unity Lifecycle Methods
        void Start()
        {
            // Get required components
            playerStats = GetComponent<PlayerStats>();
            if (playerStats == null)
            {
                Debug.LogError($"PlayerShoot on {name} requires a PlayerStats component!");
                enabled = false;
                return;
            }

            // Get player camera
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                playerCamera = Camera.main; // Fallback to main camera
            }

            if (playerCamera == null)
            {
                Debug.LogError($"PlayerShoot on {name} could not find a camera!");
                enabled = false;
                return;
            }

            // Set up shoot point fallback
            if (shootPoint == null)
            {
                Debug.LogWarning($"PlayerShoot on {name} has no shootPoint assigned! Using camera as fallback.");
                shootPoint = playerCamera.transform;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(shootKey))
            {
                TryShoot();
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Attempts to shoot, checking stamina and performing raycast.
        /// </summary>
        public void TryShoot()
        {
            // Check if we have enough stamina
            if (!playerStats.PerformShoot())
            {
                Debug.Log("Cannot shoot - not enough stamina!");
                OnShootFailed?.Invoke();
                return;
            }

            // Trigger shoot start animation
            OnShootStart?.Invoke();

            // Get shoot origin and direction
            Vector3 shootOrigin = shootPoint.position;
            Vector3 shootDirection;

            // Use camera aim direction for accuracy (crosshair aiming)
            Ray aimRay = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            shootDirection = aimRay.direction;

            if (Physics.Raycast(shootOrigin, shootDirection, out RaycastHit hit, shootRange, shootableLayers))
            {
                // We hit something!
                Debug.Log($"Shot hit: {hit.collider.name} at distance {hit.distance:F2}");

                // Trigger hit animation with shoot info and target
                OnHitTarget?.Invoke(shootOrigin, hit.point, hit.collider.gameObject);

                // Show debug ray in green if enabled
                if (showDebugRay)
                {
                    Debug.DrawRay(shootOrigin, shootDirection * hit.distance, Color.green, debugRayDuration);
                }
            }
            else
            {
                // We missed
                Debug.Log("Shot missed - no target hit");
                Vector3 missEndPoint = shootOrigin + shootDirection * shootRange;
                OnShootMiss?.Invoke(shootOrigin, missEndPoint);

                // Show debug ray in red if enabled
                if (showDebugRay)
                {
                    Debug.DrawRay(shootOrigin, shootDirection * shootRange, Color.red, debugRayDuration);
                }
            }
        }

        /// <summary>
        /// Sets the shoot key programmatically.
        /// </summary>
        /// <param name="key">The key to use for shooting</param>
        public void SetShootKey(KeyCode key)
        {
            shootKey = key;
        }

        /// <summary>
        /// Sets the shootable layers mask.
        /// </summary>
        /// <param name="layers">The layers that can be hit by shots</param>
        public void SetShootableLayers(LayerMask layers)
        {
            shootableLayers = layers;
        }
        #endregion

        #region Gizmos
        void OnDrawGizmosSelected()
        {
            if (playerCamera == null) return;

            // Draw shooting range
            Gizmos.color = Color.yellow;
            Vector3 forward = playerCamera.transform.forward;
            Gizmos.DrawRay(playerCamera.transform.position, forward * shootRange);

            // Draw a small sphere at max range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(playerCamera.transform.position + forward * shootRange, 0.5f);
        }
        #endregion
    }
}
