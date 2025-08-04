using UnityEngine;
using UnityEngine.UI;

using Gravitas.Demo;

namespace Gravitas.UI
{
    /// <summary>
    /// UI class for displaying information about the potential interaction a player can perform.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class InteractionText : MonoBehaviour
    {
        [SerializeField] private GravitasFirstPersonPlayerSubject playerController; // The player to generate interaction events
        private Text interactionText; // This text object to update

        private void Awake()
        {
            // Warnings if either player is not assigned or cannot find text object to update
            if (playerController == null)
            {
                Debug.LogWarning($@"Gravitas: Interaction text ""{gameObject.name}"" player controller is not assigned!");
                Destroy(this);

                return;
            }

            if (!TryGetComponent(out interactionText))
            {
                Debug.LogWarning($@"Gravitas: Interaction text ""{gameObject.name}"" text field is not assigned!");
                Destroy(this);
            }
        }

        private void OnEnable()
        {
            // Subscribing to the player's on interaction target event, if applicable
            if (playerController != null)
                playerController.OnInteractionTargetEvent += DisplayInteractionText;
        }

        private void OnDisable()
        {
            // Subscribing to the player's on interaction target event, if applicable
            if (playerController != null)
                playerController.OnInteractionTargetEvent -= DisplayInteractionText;
        }

        /// <summary>
        /// Called from the interaction target event, used to update the attached text object with the interaction target name.
        /// </summary>
        /// <param name="interactionName">The name of the interactable target, if any</param>
        private void DisplayInteractionText(string interactionName)
        {
            if (interactionText != null)
            {
                if (interactionName != string.Empty)
                    interactionText.text = $"Interact with <b><color=yellow>{interactionName}</color></b>";
                else
                    interactionText.text = string.Empty;
            }
        }
    }
}
