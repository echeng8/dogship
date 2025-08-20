#nullable enable

namespace Yarn.Unity.Samples
{
    using UnityEngine;
    using UnityEngine.Events;

    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] protected UnityEvent<bool>? onActiveChanged;
        [SerializeField] protected UnityEvent? onInteractionStarted;
        [SerializeField] protected UnityEvent? onInteractionEnded;

        private bool _isCurrent;

        public virtual bool IsCurrent
        {
            get => _isCurrent; set
            {
                _isCurrent = value;

                onActiveChanged?.Invoke(value);
            }
        }

        public abstract YarnTask Interact(GameObject interactor);

        public virtual bool InteractorShouldTurnToFaceWhenInteracted => false;
    }

    public class DialogueInteractable : Interactable
    {
        [SerializeField] DialogueReference dialogue = new();
        [SerializeField] public DialogueRunner? dialogueRunner;

        [SerializeField] bool turnsToInteractor = true;

        public override bool InteractorShouldTurnToFaceWhenInteracted => turnsToInteractor;

        public void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
            {
                return;
            }
#endif

            if (dialogueRunner == null)
            {
                dialogueRunner = FindAnyObjectByType<DialogueRunner>();
            }
            if (dialogueRunner != null && dialogueRunner.YarnProject != null && dialogue.project == null)
            {
                dialogue.project = dialogueRunner.YarnProject;
            }
        }

        public override bool IsCurrent
        {
            set
            {
                Debug.LogWarning($"[DialogueInteractable] IsCurrent setter called with value: {value}");

                if (value == true)
                {
                    // We've been told we're active. Double check that we
                    // actually CAN be active based on the additional
                    // information we have about what would happen if we were
                    // interacted with.

                    if (dialogue == null || dialogue.IsValid == false || dialogue.nodeName == null)
                    {
                        Debug.LogWarning($"[DialogueInteractable] Invalid dialogue: dialogue={dialogue != null}, IsValid={dialogue?.IsValid}, nodeName={dialogue?.nodeName}");
                        return;
                    }

                    if (dialogueRunner == null)
                    {
                        Debug.LogWarning($"[DialogueInteractable] No dialogue runner");
                        onActiveChanged?.Invoke(false);
                        return;
                    }

                    if (dialogueRunner.YarnProject == null)
                    {
                        Debug.LogWarning($"[DialogueInteractable] Dialogue runner has no Yarn Project");
                        onActiveChanged?.Invoke(false);
                        return;
                    }

                    // TODO: remove this once YS core is updated
                    if (dialogueRunner.Dialogue.ContentSaliencyStrategy == null)
                    {
                        dialogueRunner.Dialogue.ContentSaliencyStrategy = new Yarn.Saliency.FirstSaliencyStrategy();
                    }

                    var runnableContent = dialogueRunner.Dialogue.GetSaliencyOptionsForNodeGroup(dialogue.nodeName);
                    var content = dialogueRunner.Dialogue.ContentSaliencyStrategy.QueryBestContent(runnableContent);

                    if (content == null)
                    {
                        Debug.LogWarning($"[DialogueInteractable] No runnable content for node: {dialogue.nodeName}");
                        onActiveChanged?.Invoke(false);
                        return;
                    }

                    Debug.LogWarning($"[DialogueInteractable] All validation passed, setting IsCurrent to true");
                }

                base.IsCurrent = value;
            }
        }

        protected void Awake()
        {
            IsCurrent = false;
        }

        public override async YarnTask Interact(GameObject interactor)
        {
            Debug.LogWarning($"[DialogueInteractable] Interact called");

            if (dialogue == null)
            {
                Debug.LogWarning($"[DialogueInteractable] dialogue is null");
                return;
            }
            if (dialogueRunner == null)
            {
                Debug.LogWarning($"Can't run dialogue {dialogue}: dialogue runner not set");
                return;
            }
            if (!dialogue.IsValid || dialogue.nodeName == null)
            {
                Debug.LogWarning($"Can't run dialogue {dialogue}: not a valid dialogue reference");
                return;
            }
            if (dialogueRunner.IsDialogueRunning)
            {
                Debug.LogWarning($"Can't run dialogue {dialogue}: dialogue runner is already running");
                return;
            }

            Debug.LogWarning($"[DialogueInteractable] Starting dialogue: {dialogue.nodeName}");

            onInteractionStarted?.Invoke();

            dialogueRunner.StartDialogue(dialogue.nodeName);

            if (turnsToInteractor)
            {
                if (TryGetComponent<SimpleCharacter>(out var character))
                {
                    character.lookTarget = interactor.transform;
                }
                if (TryGetComponent<SimpleCharacter2D>(out var character2D))
                {
                    character2D.lookTarget = interactor.transform;
                }
            }

            var destroyCancellation = destroyCancellationToken;

            await dialogueRunner.DialogueTask;

            if (destroyCancellation.IsCancellationRequested)
            {
                return;
            }

            if (turnsToInteractor)
            {
                if (TryGetComponent<SimpleCharacter>(out var character))
                {
                    character.lookTarget = null;
                }
                if (TryGetComponent<SimpleCharacter2D>(out var character2D))
                {
                    character2D.lookTarget = null;
                }
            }
        }
    }
}
