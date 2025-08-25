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
        #endregion

        #region Events
        public event Action<float, float> OnStaminaChanged; // current, max
        #endregion

        #region Public Properties
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public bool CanSprint => currentStamina > 0;
        public bool IsSprinting => isSprinting;
        #endregion

        #region SerializeField Variables
        [Header("Stamina Settings")]
        public float maxStamina = 100f;
        public float currentStamina = 100f;
        #endregion

        #region Private Variables
        private bool isSprinting = false;
        private bool previousFrameSprinting = false;
        private CoherenceSync _sync;
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

            // Initialize stamina
            currentStamina = maxStamina;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
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
            isSprinting = wantsToSprint && CanSprint;
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

            // Force UI update
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            Debug.Log($"Player max stamina increased by {amount}. New max: {maxStamina}, Current: {currentStamina}");
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
        #endregion

        #region Private Methods
        private void UpdateStamina()
        {
            bool sprintingAtStart = previousFrameSprinting;
            float previousStamina = currentStamina;

            if (isSprinting)
            {
                // Drain stamina when sprinting
                currentStamina -= STAMINA_DRAIN_RATE * Time.deltaTime;
                currentStamina = Mathf.Max(0, currentStamina);

                // Stop sprinting if out of stamina
                if (currentStamina <= 0)
                {
                    isSprinting = false;
                }
            }
            else if (currentStamina < maxStamina)
            {
                // Regenerate stamina when not sprinting
                currentStamina += STAMINA_REGEN_RATE * Time.deltaTime;
                currentStamina = Mathf.Min(maxStamina, currentStamina);
            }

            // Notify if stamina changed or sprinting state changed
            if (sprintingAtStart != isSprinting || Mathf.Abs(currentStamina - previousStamina) > 0.1f)
            {
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }

            // Store current sprinting state for next frame
            previousFrameSprinting = isSprinting;
        }
        #endregion
    }
}
