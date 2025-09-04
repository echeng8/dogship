using UnityEngine;
using System;
using Coherence.Toolkit;
using Coherence;

namespace Gravitas
{
    /// <summary>
    /// Manages player statistics including stamina, health, and other player attributes.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        #region Constants
        public const float STAMINA_DRAIN_RATE = 20f;
        public const float STAMINA_REGEN_RATE = 15f;
        public const float DASH_COST = 10f;
        public const float DASH_COOLDOWN = 0.5f;
        #endregion

        #region Events
        public event Action<float, float> OnStaminaChanged; // current, max
        public event Action<int> OnPoopAmmoChanged; // current poop ammo
        #endregion

        #region Public Properties
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public bool CanDash => currentStamina >= DASH_COST && Time.time >= lastDashTime + DASH_COOLDOWN;
        public bool IsSprinting => isSprinting;
        public bool CanPoop => Time.time >= lastPoopTime + poopCooldown && poopPrefab != null && poopAmmo > 0;
        public int PoopAmmo => poopAmmo;
        #endregion

        #region SerializeField Variables
        [Header("Stamina Settings")]
        public float maxStamina = 100f;
        public float currentStamina = 100f;

        [Header("Poop Settings")]
        [SerializeField] private GameObject poopPrefab;
        [SerializeField] private float poopThrowForce = 5f;
        [SerializeField] private float poopCooldown = 2f;
        #endregion

        #region Private Variables
        private bool isSprinting = false;
        private bool previousFrameSprinting = false;
        private CoherenceSync _sync;
        private GravitasSubject _gravitasSubject;
        private float lastPoopTime = 0f;
        private float lastDashTime = 0f;
        private Camera playerCamera;
        private int poopAmmo = 0;
        #endregion

        #region Unity Lifecycle Methods
        void Start()
        {
            // Get CoherenceSync component
            _sync = GetComponent<CoherenceSync>();
            if (_sync == null)
            {
                Debug.LogWarning($"PlayerStats on {name} does not have a CoherenceSync component. Some functionality may not work in multiplayer.");
            }

            // Verify GravitasSubject exists for poop spawning
            _gravitasSubject = GetComponent<GravitasSubject>();
            if (_gravitasSubject == null)
            {
                Debug.LogWarning($"PlayerStats on {name} could not find a GravitasSubject component. Poop spawning may not work correctly!");
            }

            // Get player camera for poop direction
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                playerCamera = Camera.main; // Fallback to main camera
            }

            // Initialize stamina
            currentStamina = maxStamina;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            OnPoopAmmoChanged?.Invoke(poopAmmo);
        }

        void Update()
        {
            UpdateStamina();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Updates the sprinting state based on input and stamina availability.
        /// </summary>
        /// <param name="wantsToSprint">Whether the player wants to sprint (input + movement)</param>
        public void UpdateSprinting(bool wantsToSprint)
        {
            isSprinting = wantsToSprint && currentStamina > 0;
        }

        /// <summary>
        /// Performs a dash if possible, consuming stamina and returning success.
        /// </summary>
        /// <returns>True if dash was performed, false otherwise</returns>
        public bool PerformDash()
        {
            if (!CanDash)
                return false;

            currentStamina -= DASH_COST;
            lastDashTime = Time.time;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);

            Debug.Log($"Dash performed! Stamina: {currentStamina}/{maxStamina}");
            return true;
        }

        /// <summary>
        /// Sets the sprinting state for stamina consumption.
        /// </summary>
        /// <param name="sprinting">Whether the player is currently sprinting</param>
        public void SetSprinting(bool sprinting)
        {
            isSprinting = sprinting && currentStamina > 0;
        }

        /// <summary>
        /// Increases the maximum stamina by the specified amount.
        /// This method should be called by external systems like FoodBall.
        /// </summary>
        /// <param name="amount">Amount to increase max stamina by</param>
        public void IncreaseMaxStamina(float amount)
        {
            Debug.Log($"IncreaseMaxStamina called with amount: {amount}");

            if (_sync != null && _sync.HasStateAuthority)
            {
                Debug.Log("Has state authority, applying change directly");
                // We have authority, apply the change directly
                NetworkIncreaseMaxStamina(amount);
            }
            else if (_sync != null)
            {
                Debug.Log("Sending command to authority");
                // Send command to authority
                _sync.SendCommand<PlayerStats>(
                    nameof(NetworkIncreaseMaxStamina),
                    MessageTarget.AuthorityOnly,
                    amount
                );
            }
            else
            {
                Debug.Log("No networking, applying directly (single player fallback)");
                // No networking, apply directly (fallback for single player)
                NetworkIncreaseMaxStamina(amount);
            }
        }

        /// <summary>
        /// Network command to increase max stamina. Only executed by authority.
        /// </summary>
        /// <param name="amount">Amount to increase max stamina by</param>
        [Command]
        public void NetworkIncreaseMaxStamina(float amount)
        {
            maxStamina += amount;
            currentStamina = maxStamina; // Fill stamina when max increases

            // Increase poop ammo by 1 when eating
            poopAmmo++;
            OnPoopAmmoChanged?.Invoke(poopAmmo);

            // Force UI update
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            Debug.Log($"Player max stamina increased by {amount}. New max: {maxStamina}, Current: {currentStamina}, Poop ammo: {poopAmmo}");
        }

        /// <summary>
        /// Network command to spawn poop. Only executed by authority.
        /// </summary>
        [Command]
        public void NetworkPoop()
        {
            if (!CanPoop || poopPrefab == null)
            {
                Debug.LogWarning($"Cannot poop: CanPoop={CanPoop}, poopPrefab={poopPrefab != null}, poopAmmo={poopAmmo}");
                return;
            }

            lastPoopTime = Time.time;
            poopAmmo--;
            OnPoopAmmoChanged?.Invoke(poopAmmo);
            SpawnPoop();
        }

        /// <summary>
        /// Restores stamina by the specified amount.
        /// </summary>
        /// <param name="amount">Amount of stamina to restore</param>
        public void RestoreStamina(float amount)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        /// <summary>
        /// Drains stamina by the specified amount.
        /// </summary>
        /// <param name="amount">Amount of stamina to drain</param>
        public void DrainStamina(float amount)
        {
            currentStamina = Mathf.Max(0, currentStamina - amount);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        /// <summary>
        /// Attempts to poop, spawning a poop prefab in the direction the player is looking.
        /// </summary>
        public void Poop()
        {
            if (!CanPoop)
            {
                Debug.Log($"Cannot poop yet. Cooldown remaining: {(lastPoopTime + poopCooldown - Time.time):F1}s");
                return;
            }

            if (_sync != null && _sync.HasStateAuthority)
            {
                // We have authority, apply the change directly
                NetworkPoop();
            }
            else if (_sync != null)
            {
                // Send command to authority
                _sync.SendCommand<PlayerStats>(
                    nameof(NetworkPoop),
                    MessageTarget.AuthorityOnly
                );
            }
            else
            {
                // No networking, apply directly (fallback for single player)
                NetworkPoop();
            }
        }

        /// <summary>
        /// Attempts to spawn a cryptid near the player.
        /// </summary>
        public void SpawnCryptid()
        {
            CryptidManager cryptidManager = FindFirstObjectByType<CryptidManager>();
            if (cryptidManager == null)
            {
                Debug.LogError("CryptidManager not found in scene!");
                return;
            }

            cryptidManager.SpawnCryptid(transform);
        }
        #endregion

        #region Private Methods
        private void UpdateStamina()
        {
            float previousStamina = currentStamina;

            // Only regenerate stamina when not at max
            if (currentStamina < maxStamina)
            {
                // Regenerate stamina when not sprinting
                currentStamina += STAMINA_REGEN_RATE * Time.deltaTime;
                currentStamina = Mathf.Min(maxStamina, currentStamina);
            }

            // Notify if stamina changed
            if (Mathf.Abs(currentStamina - previousStamina) > 0.1f)
            {
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        private void SpawnPoop()
        {
            if (poopPrefab == null || playerCamera == null)
            {
                Debug.LogWarning("Cannot spawn poop: missing prefab or camera");
                return;
            }

            // Calculate spawn position and direction
            Vector3 spawnPosition = transform.position + transform.forward * 1f;
            Vector3 throwDirection = playerCamera.transform.forward;
            Quaternion spawnRotation = Quaternion.LookRotation(throwDirection);

            // Get current field from GravitasSubject
            IGravitasField currentField = _gravitasSubject?.CurrentField;

            // Spawn poop using gravitas field if available
            GameObject spawnedPoop = null;
            if (currentField != null)
            {
                print("Spawning poop in gravitas field");
                spawnedPoop = currentField.SpawnAndAddToField(poopPrefab, spawnPosition, spawnRotation);
            }
            else
            {
                print("Spawning poop outside of gravitas field");
                spawnedPoop = Instantiate(poopPrefab, spawnPosition, spawnRotation);
            }

            if (spawnedPoop != null)
            {
                // Add initial velocity
                Rigidbody poopRb = spawnedPoop.GetComponent<Rigidbody>();
                if (poopRb != null)
                {
                    //poopRb.linearVelocity = throwDirection * poopThrowForce;
                }

                Debug.Log($"Spawned poop: {spawnedPoop.name}");
            }
        }

        #endregion
    }
}
