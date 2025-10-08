using UnityEngine;
using Gravitas.Demo;
using Coherence.Toolkit;
using Coherence;

namespace Gravitas
{
    /// <summary>
    /// A food ball that increases the player's max stamina when eaten.
    /// Now harvested from trees rather than spawned automatically.
    /// Tracks food ball type for win condition.
    /// </summary>
    public class FoodBall : MonoBehaviour, IInteractable
    {
        [Header("Food Ball Settings")]
        public float maxStaminaDelta = 10f;

        [Header("Food Ball Type")]
        [Tooltip("The type of this food ball (Yellow or Green)")]
        public FoodBallType foodBallType = FoodBallType.Yellow;

        private CoherenceSync _sync;

        public bool CanInteract => true;
        public string InteractionPrompt => $"Eat {foodBallType} Food Ball";

        private void Awake()
        {
            _sync = GetComponent<CoherenceSync>();
            if (_sync == null)
            {
                Debug.LogError($"FoodBall {name} requires a CoherenceSync component!");
            }
        }

        public void Interact(GravitasFirstPersonPlayerSubject player)
        {
            if (player != null && _sync != null)
            {
                // Send command to authority to handle eating
                _sync.SendCommand<FoodBall>(
                    nameof(NetworkEatFoodBall),
                    MessageTarget.AuthorityOnly,
                    player.gameObject
                );
            }
        }

        [Command]
        public void NetworkEatFoodBall(GameObject playerGameObject)
        {
            Debug.Log($"NetworkEatFoodBall called for player: {playerGameObject?.name}, FoodBall type: {foodBallType}");

            if (playerGameObject != null)
            {
                // Get PlayerStats component from the player
                PlayerStats playerStats = playerGameObject.GetComponent<PlayerStats>();

                if (playerStats != null)
                {
                    Debug.Log($"Found PlayerStats, increasing stamina by {maxStaminaDelta}");
                    // The PlayerStats component will handle the network command to its own authority
                    playerStats.IncreaseMaxStamina(maxStaminaDelta);
                    Debug.Log($"Food ball consumed! Stamina boost of {maxStaminaDelta} sent to player.");
                }
                else
                {
                    Debug.LogWarning("PlayerStats component not found on player!");
                }

                // Track the eaten food ball in FoodManager
                if (FoodManager.Instance != null)
                {
                    FoodManager.Instance.RecordFoodBallEaten(foodBallType);
                }
                else
                {
                    Debug.LogWarning("FoodManager instance not found!");
                }

                // Destroy the food ball after eating (only authority can do this)
                if (_sync != null && _sync.HasStateAuthority)
                {
                    Debug.Log("Destroying food ball after consumption");
                    Destroy(gameObject);
                }
            }
            else
            {
                Debug.LogError("NetworkEatFoodBall: playerGameObject is null!");
            }
        }
    }
}
