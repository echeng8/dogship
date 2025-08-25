using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

namespace Gravitas
{
    /// <summary>
    /// UI component that displays player stamina information from PlayerStats
    /// Supports TextMeshPro components
    /// </summary>
    public class PlayerStatsUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("TextMeshPro UI component to display stamina information")]
        public TextMeshProUGUI staminaText;

        [Tooltip("Optional separate TextMeshPro UI component to display max stamina only")]
        public TextMeshProUGUI maxStaminaText;

        [Header("Player References")]
        [Tooltip("PlayerStats component to get stamina data from")]
        public PlayerStats playerStats;

        [Header("Display Settings")]
        [Tooltip("Format string for stamina display. Use {0} for current, {1} for max")]
        public string staminaFormat = "Stamina: {0:F0}/{1:F0}";

        [Tooltip("Format string for max stamina display. Use {0} for max value")]
        public string maxStaminaFormat = "Max Stamina: {0:F0}";

        private void Start()
        {
            // Try to find PlayerStats if not assigned
            if (playerStats == null)
            {
                playerStats = FindFirstObjectByType<PlayerStats>();
            }

            // Subscribe to stamina changes
            if (playerStats != null)
            {
                playerStats.OnStaminaChanged += OnStaminaChanged;
                // Initialize display with current values
                OnStaminaChanged(playerStats.CurrentStamina, playerStats.MaxStamina);
            }
            else
            {
                Debug.LogWarning($"PlayerStatsUI on {name} could not find PlayerStats component.");
            }

            // Validate UI reference
            if (staminaText == null)
            {
                Debug.LogWarning($"PlayerStatsUI on {name} is missing staminaText reference. Please assign a TextMeshPro component.");
            }

            if (maxStaminaText != null)
            {
                Debug.Log($"PlayerStatsUI on {name} has separate max stamina display enabled.");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            if (playerStats != null)
            {
                playerStats.OnStaminaChanged -= OnStaminaChanged;
            }
        }

        /// <summary>
        /// Called when stamina values change in PlayerStats
        /// </summary>
        /// <param name="currentStamina">Current stamina value</param>
        /// <param name="maxStamina">Maximum stamina value</param>
        private void OnStaminaChanged(float currentStamina, float maxStamina)
        {
            // Update main stamina display
            if (staminaText != null)
            {
                staminaText.text = string.Format(staminaFormat, currentStamina, maxStamina);
            }

            // Update separate max stamina display if available
            if (maxStaminaText != null)
            {
                maxStaminaText.text = string.Format(maxStaminaFormat, maxStamina);
            }
        }

        /// <summary>
        /// Manually update the display (useful for testing)
        /// </summary>
        public void UpdateDisplay()
        {
            if (playerStats != null)
            {
                OnStaminaChanged(playerStats.CurrentStamina, playerStats.MaxStamina);
            }
        }
    }
}
