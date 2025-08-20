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

        public bool CanInteract
        {
            get
            {
                bool canInteract = dialogueInteractable != null && dialogueInteractable.IsCurrent;
                //Debug.LogWarning($"[DialogueAdapter] CanInteract: {canInteract} (dialogueInteractable: {dialogueInteractable != null}, IsCurrent: {dialogueInteractable?.IsCurrent})");
                return canInteract;
            }
        }
        public string InteractionPrompt => "Talk";

        void Awake()
        {
            if (dialogueInteractable == null)
                dialogueInteractable = GetComponent<DialogueInteractable>();

            Debug.LogWarning($"[DialogueAdapter] Awake - dialogueInteractable found: {dialogueInteractable != null}");
        }

        void Start()
        {
            // Trigger the dialogue interactable to check if it should be active
            if (dialogueInteractable != null)
            {
                dialogueInteractable.IsCurrent = true;
                Debug.LogWarning($"[DialogueAdapter] Start - Set IsCurrent to true, result: {dialogueInteractable.IsCurrent}");
            }
        }

        public async void Interact(GravitasFirstPersonPlayerSubject player)
        {
            Debug.LogWarning($"[DialogueAdapter] Interact called - CanInteract: {CanInteract}");

            if (dialogueInteractable != null)
            {
                // If dialogue is already running, stop it
                if (dialogueInteractable.dialogueRunner != null && dialogueInteractable.dialogueRunner.IsDialogueRunning)
                {
                    Debug.LogWarning($"[DialogueAdapter] Stopping running dialogue");
                    dialogueInteractable.dialogueRunner.Stop();
                    return;
                }

                if (CanInteract)
                {
                    Debug.LogWarning($"[DialogueAdapter] Starting dialogue interaction");
                    await dialogueInteractable.Interact(player.gameObject);
                    Debug.LogWarning($"[DialogueAdapter] Dialogue interaction completed");
                }
                else
                {
                    Debug.LogWarning($"[DialogueAdapter] Cannot interact - CanInteract: {CanInteract}");
                }
            }
        }
    }
}
