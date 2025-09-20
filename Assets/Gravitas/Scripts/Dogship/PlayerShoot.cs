using UnityEngine;
using UnityEngine.Events;
using Coherence.Toolkit;
using Coherence;

namespace Gravitas
{
    /// <summary>
    /// Handles player shooting mechanics with raycast hit detection and UnityEvents for animations.
    /// 
    /// Network Behavior:
    /// - Uses SendCommand which automatically handles online/offline scenarios
    /// - Online: Commands are sent over network to all clients  
    /// - Offline: Commands are automatically routed to local method calls
    /// - Authority handling: online respects HasInputAuthority, offline assumes local authority
    /// - Requires CoherenceSync component for networking, gracefully handles when missing
    /// </summary>
    public class PlayerShoot : MonoBehaviour
    {
        #region Unity Events
        [Header("Animation Events")]
        [Tooltip("Called when the player starts shooting")]
        public UnityEvent OnShootStart;

        [Tooltip("Called when the player hits a target - passes hit info")]
        public UnityEvent<Vector3, Vector3> OnHitTarget; // startPos, endPos

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
        private CoherenceSync _sync;
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

            // Get CoherenceSync component for networking (optional)
            _sync = GetComponent<CoherenceSync>();

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
            // Only process input if we have authority (online) or if offline (authority assumed)
            if (_sync != null && !_sync.HasInputAuthority)
                return;

            if (Input.GetKeyDown(shootKey))
            {
                TryShoot();
            }
        }
        #endregion

        #region Network Commands
        /// <summary>
        /// Network command to trigger shoot start effects on all clients
        /// </summary>
        [Command]
        public void NetworkShootStart()
        {
            OnShootStart?.Invoke();
        }

        /// <summary>
        /// Network command to trigger hit target effects on all clients
        /// </summary>
        [Command]
        public void NetworkShootHit(Vector3 startPos, Vector3 endPos)
        {
            OnHitTarget?.Invoke(startPos, endPos);
        }

        /// <summary>
        /// Network command to trigger miss effects on all clients
        /// </summary>
        [Command]
        public void NetworkShootMiss(Vector3 startPos, Vector3 endPos)
        {
            OnShootMiss?.Invoke(startPos, endPos);
        }

        /// <summary>
        /// Network command to trigger shoot failed effects on all clients
        /// </summary>
        [Command]
        public void NetworkShootFailed()
        {
            OnShootFailed?.Invoke();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Attempts to shoot, checking stamina and performing raycast.
        /// SendCommand automatically routes to local calls when offline.
        /// </summary>
        public void TryShoot()
        {
            // Check if we have enough stamina
            if (!playerStats.PerformShoot())
            {
                Debug.Log("Cannot shoot - not enough stamina!");
                if (_sync != null)
                {
                    _sync.SendCommand<PlayerShoot>(nameof(NetworkShootFailed), MessageTarget.All);
                }
                return;
            }

            // Send network command for shoot start effects
            if (_sync != null)
            {
                _sync.SendCommand<PlayerShoot>(nameof(NetworkShootStart), MessageTarget.All);
            }

            // Get shoot origin and direction
            Vector3 shootOrigin = shootPoint.position;
            Vector3 shootDirection = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0)).direction;

            if (Physics.Raycast(shootOrigin, shootDirection, out RaycastHit hit, shootRange, shootableLayers))
            {
                // We hit something!
                Debug.Log($"Shot hit: {hit.collider.name} at distance {hit.distance:F2}");

                if (_sync != null)
                {
                    _sync.SendCommand<PlayerShoot>(nameof(NetworkShootHit), MessageTarget.All,
                       shootOrigin, hit.point);
                }

                // Show debug ray in green if enabled
                if (showDebugRay)
                    Debug.DrawRay(shootOrigin, shootDirection * hit.distance, Color.green, debugRayDuration);
            }
            else
            {
                // We missed
                Debug.Log("Shot missed - no target hit");
                Vector3 missEndPoint = shootOrigin + shootDirection * shootRange;

                if (_sync != null)
                {
                    _sync.SendCommand<PlayerShoot>(nameof(NetworkShootMiss), MessageTarget.All,
                        shootOrigin, missEndPoint);
                }

                // Show debug ray in red if enabled
                if (showDebugRay)
                    Debug.DrawRay(shootOrigin, shootDirection * shootRange, Color.red, debugRayDuration);
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
