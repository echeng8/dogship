using UnityEngine;
using Yarn.Unity.Samples;

namespace Gravitas.Demo
{
    /// <summary>
    /// Adapter to make Yarn Spinner DialogueInteractable compatible with Gravitas interaction system.
    /// </summary>
    public class DialogueInteractableAdapter : MonoBehaviour, IInteractable
    {
        [SerializeField] private DialogueInteractable dialogueInteractable;

        public bool CanInteract => dialogueInteractable != null && dialogueInteractable.IsCurrent;
        public string InteractionPrompt => "Talk";

        void Awake()
        {
            if (dialogueInteractable == null)
                dialogueInteractable = GetComponent<DialogueInteractable>();
        }

        public async void Interact(GravitasFirstPersonPlayerSubject player)
        {
            if (dialogueInteractable != null && CanInteract)
            {
                await dialogueInteractable.Interact(player.gameObject);
            }
        }
    }
}
