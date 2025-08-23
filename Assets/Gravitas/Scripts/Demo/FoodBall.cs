using UnityEngine;
using Gravitas.Demo;

namespace Gravitas.Demo
{
    /// <summary>
    /// A food ball that increases the player's max stamina when eaten.
    /// </summary>
    public class FoodBall : MonoBehaviour, IInteractable
    {
        [Header("Food Ball Settings")]
        [SerializeField] private float maxStaminaDelta = 10f;

        public bool CanInteract => true;
        public string InteractionPrompt => "Eat Food Ball";

        public void Interact(GravitasFirstPersonPlayerSubject player)
        {
            if (player != null)
            {
                // Increase player's max stamina
                player.IncreaseMaxStamina(maxStaminaDelta);

                Debug.Log($"Player ate food ball! Max stamina increased by {maxStaminaDelta}");

                // Destroy the food ball after eating
                Destroy(gameObject);
            }
        }
    }
}
