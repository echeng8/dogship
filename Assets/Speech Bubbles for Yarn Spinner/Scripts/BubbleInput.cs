namespace Yarn.Unity.Addons.SpeechBubbles
{
    using UnityEngine;
    using Yarn.Unity;
    using System.Threading;
    using System.Collections.Generic;

#if USE_INPUTSYSTEM
    using UnityEngine.InputSystem;
#endif

#nullable enable

    public sealed class BubbleInput : MonoBehaviour
    {
        [SerializeField] LineAdvancer? lineAdvancer;
        [SerializeField] private bool independentAdvancerConfiguration = false;

        [Space]
        [SerializeField] BubbleDialogueView? view;
        [SerializeField] DialogueRunner? runner;

        [Space]
        [SerializeField] private bool multiAdvanceIsCancel = false;
        [Yarn.Unity.Attributes.ShowIf(nameof(multiAdvanceIsCancel))]
        [SerializeField] private int advanceRequestsBeforeCancellingLine = 2;
        [Space]
        [SerializeField] LineAdvancer.InputMode inputMode = LineAdvancer.InputMode.KeyCodes;

        // these are the keycodes to be forwarded onto the line advancer
        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.KeyCodes)]
        [Yarn.Unity.Attributes.HideIf(nameof(independentAdvancerConfiguration))]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] KeyCode hurryUpLineKeyCode = KeyCode.Space;

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.KeyCodes)]
        [Yarn.Unity.Attributes.HideIf(nameof(independentAdvancerConfiguration))]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] KeyCode nextLineKeyCode = KeyCode.Escape;

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.KeyCodes)]
        [Yarn.Unity.Attributes.HideIf(nameof(independentAdvancerConfiguration))]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] KeyCode cancelDialogueKeyCode = KeyCode.None;

        // these are the keycodes that are bubble specific
        [Space]
        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.KeyCodes)]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] KeyCode nextOptionKey = KeyCode.RightArrow;

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.KeyCodes)]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] KeyCode prevOptionKey = KeyCode.LeftArrow;

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.KeyCodes)]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] KeyCode selectOptionKey = KeyCode.Space;

#if USE_INPUTSYSTEM
        // these are the actions to be forwarded onto the line advancer
        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.InputActions)]
        [Yarn.Unity.Attributes.HideIf(nameof(independentAdvancerConfiguration))]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] InputActionReference? hurryUpLineAction;

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.InputActions)]
        [Yarn.Unity.Attributes.HideIf(nameof(independentAdvancerConfiguration))]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] InputActionReference? nextLineAction;

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.InputActions)]
        [Yarn.Unity.Attributes.HideIf(nameof(independentAdvancerConfiguration))]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] InputActionReference? cancelDialogueAction;

        // these are the actions specific to the bubble
        [Space]
        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.InputActions)]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] InputActionReference? nextOptionInput;

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.InputActions)]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] InputActionReference? prevOptionInput;

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.InputActions)]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] InputActionReference? selectOptionInput;
#endif
        // these are the axes to be forwarded onto the line advancer
        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.LegacyInputAxes)]
        [Yarn.Unity.Attributes.HideIf(nameof(independentAdvancerConfiguration))]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] string hurryUpLineAxis = "Jump";

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.LegacyInputAxes)]
        [Yarn.Unity.Attributes.HideIf(nameof(independentAdvancerConfiguration))]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] string nextLineAxis = "Cancel";

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.LegacyInputAxes)]
        [Yarn.Unity.Attributes.HideIf(nameof(independentAdvancerConfiguration))]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] string cancelDialogueAxis = "";

        // these are the axes specific to the bubble
        [Space]
        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.LegacyInputAxes)]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] string nextOptionAxisName = "Horizontal";

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.LegacyInputAxes)]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] string selectionOptionButtonName = "Submit";

        [Yarn.Unity.Attributes.ShowIf(nameof(inputMode), LineAdvancer.InputMode.LegacyInputAxes)]
        [Yarn.Unity.Attributes.Indent]
        [SerializeField] float axisThreshold = 0.4f;

        private YarnTaskCompletionSource? changeOptionCompletionSource = null;
        private CancellationTokenSource? cancellationSource;

        private bool isMovingBetweenOptions
        {
            get
            {
                return changeOptionCompletionSource != null && !changeOptionCompletionSource.Task.IsCompleted();
            }
        }

        void Start()
        {
            ConfigureAndEnableInputActions();
        }

        void OnEnable()
        {
            if (lineAdvancer == null)
            {
                return;
            }
            if (independentAdvancerConfiguration)
            {
                return;
            }
            lineAdvancer.hideFlags |= HideFlags.NotEditable;
        }
        void OnDisable()
        {
            DisableInputAction();
            if (lineAdvancer == null)
            {
                return;
            }
            if (independentAdvancerConfiguration)
            {
                return;
            }
            lineAdvancer.hideFlags &= ~HideFlags.NotEditable;
        }

        void OnDestroy()
        {
            if (lineAdvancer == null)
            {
                return;
            }
            if (independentAdvancerConfiguration)
            {
                return;
            }
            lineAdvancer.hideFlags &= ~HideFlags.NotEditable;
        }

        void OnValidate()
        {
            // for now just use one I manually connect
            if (lineAdvancer == null)
            {
                lineAdvancer = GetComponent<LineAdvancer>();
            }

            if (!independentAdvancerConfiguration)
            {
                lineAdvancer.hideFlags |= HideFlags.NotEditable;
            }
            else
            {
                lineAdvancer.hideFlags &= ~HideFlags.NotEditable;
                return;
            }

            // ok so also need to set the input type on the line advancer

#if UNITY_EDITOR
            // ok now to grab into the guts of the line advancer and start to fuck with it
            var type = typeof(LineAdvancer);

            var translation = new Dictionary<string, object?>
        {
            { "presenter", view },
            { "runner", runner },
            { "inputMode", inputMode},
        };

            lineAdvancer.multiAdvanceIsCancel = multiAdvanceIsCancel;
            lineAdvancer.advanceRequestsBeforeCancellingLine = advanceRequestsBeforeCancellingLine;

            switch (inputMode)
            {
                case LineAdvancer.InputMode.KeyCodes:
                    translation.Add("hurryUpLineKeyCode", hurryUpLineKeyCode);
                    translation.Add("nextLineKeyCode", nextLineKeyCode);
                    translation.Add("cancelDialogueKeyCode", cancelDialogueKeyCode);

                    break;
                case LineAdvancer.InputMode.LegacyInputAxes:
                    translation.Add("hurryUpLineAxis", hurryUpLineAxis);
                    translation.Add("nextLineAxis", nextLineAxis);
                    translation.Add("cancelDialogueAxis", cancelDialogueAxis);

                    break;
                case LineAdvancer.InputMode.InputActions:
#if USE_INPUTSYSTEM
                    translation.Add("hurryUpLineAction", hurryUpLineAction);
                    translation.Add("nextLineAction", nextLineAction);
                    translation.Add("cancelDialogueAction", cancelDialogueAction);
#endif
                    break;
            }

            foreach (var pair in translation)
            {
                var field = type.GetField(pair.Key, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field == null)
                {
                    Debug.LogWarning($"unable to find the field {pair.Key} on Line Advancer");
                    continue;
                }
                if (pair.Value == null)
                {
                    // Debug.LogWarning($"Found the field {pair.Key} on the Line Advancer but it has no value associated with it");
                    continue;
                }
                field.SetValue(lineAdvancer, pair.Value);
            }
#endif
        }


#if USE_INPUTSYSTEM
        void ConfigureAndEnableInputActions()
        {
            if (inputMode != LineAdvancer.InputMode.InputActions)
            {
                return;
            }
            if (nextOptionInput == null)
            {
                return;
            }
            if (prevOptionInput == null)
            {
                return;
            }
            if (selectOptionInput == null)
            {
                return;
            }

            nextOptionInput.action.performed += NextOptionInputFired;
            prevOptionInput.action.performed += PrevOptionInputFired;
            selectOptionInput.action.performed += SelectOptionInput;
        }
        void DisableInputAction()
        {
            if (inputMode != LineAdvancer.InputMode.InputActions)
            {
                return;
            }
            if (nextOptionInput == null)
            {
                return;
            }
            if (prevOptionInput == null)
            {
                return;
            }
            if (selectOptionInput == null)
            {
                return;
            }

            nextOptionInput.action.performed -= NextOptionInputFired;
            prevOptionInput.action.performed -= PrevOptionInputFired;
            selectOptionInput.action.performed -= SelectOptionInput;
        }
        private void SelectOptionInput(InputAction.CallbackContext context)
        {
            SelectCurrentOption();
        }
        private void PrevOptionInputFired(InputAction.CallbackContext context)
        {
            if (isMovingBetweenOptions)
            {
                ChangeOptionInternal(-1, true);
            }
            else
            {
                ChangeOptionInternal(-1, false);
            }
        }
        private void NextOptionInputFired(InputAction.CallbackContext context)
        {
            if (isMovingBetweenOptions)
            {
                ChangeOptionInternal(1, true);
            }
            else
            {
                ChangeOptionInternal(1, false);
            }
        }
#else
        void ConfigureAndEnableInputActions()
        {
            // this is a no-op just for being cleaner to call it when not in input systems code
        }
        void DisableInputAction()
        {
            // this is a no-op just for being cleaner to call it when not in input systems code
        }
#endif

        void Update()
        {
            if (inputMode == LineAdvancer.InputMode.KeyCodes)
            {
                if (Input.GetKeyUp(nextOptionKey))
                {
                    if (isMovingBetweenOptions)
                    {
                        ChangeOptionInternal(1, true);
                    }
                    else
                    {
                        ChangeOptionInternal(1, false);
                    }
                    return;
                }
                if (Input.GetKeyUp(prevOptionKey))
                {
                    if (isMovingBetweenOptions)
                    {
                        ChangeOptionInternal(-1, true);
                    }
                    else
                    {
                        ChangeOptionInternal(-1, false);
                    }
                    return;
                }
                if (Input.GetKeyUp(selectOptionKey))
                {
                    SelectCurrentOption();
                    return;
                }
            }
            else if (inputMode == LineAdvancer.InputMode.LegacyInputAxes)
            {
                var axis = Input.GetAxis(nextOptionAxisName);

                if (axis > axisThreshold)
                {
                    ShowNextOption();
                }
                if (axis < -axisThreshold)
                {
                    ShowPreviousOption();
                }

                if (Input.GetButtonUp(selectionOptionButtonName))
                {
                    SelectCurrentOption();
                }
            }
        }

        private async YarnTask ChangeOption(int direction)
        {
            // if this method is called and we don't have a completion source something has gone wrong and it needs to be resolved
            if (changeOptionCompletionSource == null)
            {
                return;
            }
            // likewise if the view is null we can't do anything
            if (view == null)
            {
                return;
            }
            if (cancellationSource == null)
            {
                return;
            }

            // we let the animation finish
            await view.ChangeOption(direction, cancellationSource.Token);

            // we've finished so we want to do some clean up
            // this will also be done later in case it is skipped here
            // but it feels good to be sure here
            changeOptionCompletionSource.TrySetResult();
            cancellationSource?.Dispose();
            cancellationSource = null;
        }

        private void ChangeOptionInternal(int direction, bool cancelCurrentPresentation)
        {
            // if we don't have a view OR it isn't showing options we can't change options
            if (view == null)
            {
                return;
            }
            if (view.CurrentContentType != BubbleDialogueView.ContentType.Options)
            {
                return;
            }

            // there is a change source already
            if (changeOptionCompletionSource != null)
            {
                // if it isn't finished then we just ignore this and move on
                if (!changeOptionCompletionSource.Task.IsCompleted())
                {
                    if (cancelCurrentPresentation)
                    {
                        cancellationSource?.Cancel();
                    }
                    else
                    {
                        return;
                    }
                }
            }

            // likewise we will need to clean up the cancellationSource before going any further
            if (cancellationSource != null)
            {
                cancellationSource.Dispose();
            }

            // at this point now the changeOptionCompletionSource either doesn't exist or we need a fresh one regardless
            // likewise for the token source
            // building a new cancellation token and a completion source for this change
            cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            changeOptionCompletionSource = new YarnTaskCompletionSource();
            cancellationSource.Token.Register(() =>
            {
                changeOptionCompletionSource?.TrySetCanceled();
            });

            // finally we can now request the change
            ChangeOption(direction).Forget();
        }

        public void ShowNextOption()
        {
            ChangeOptionInternal(1, false);
        }
        public void ShowPreviousOption()
        {
            ChangeOptionInternal(-1, false);
        }
        public void SelectCurrentOption()
        {
            if (view == null)
            {
                return;
            }
            if (view.CurrentContentType == BubbleDialogueView.ContentType.Options)
            {
                view.SelectOption();
            }
        }
    }
}