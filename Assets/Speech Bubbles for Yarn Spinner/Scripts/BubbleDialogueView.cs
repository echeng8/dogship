namespace Yarn.Unity.Addons.SpeechBubbles
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using Yarn.Unity;
    using Yarn.Unity.Attributes;

#nullable enable

    /// <summary>
    /// A <see cref="DialogueViewBase"/> subclass that handles the specific scenario of presenting, managing, and removing speech bubbles.
    /// </summary>
    public class BubbleDialogueView : DialoguePresenterBase
    {
        public enum ContentType
        {
            None,
            Line,
            Options
        }

        public ContentType CurrentContentType
        {
            get
            {
                if (currentContent.HasContent == false)
                {
                    return ContentType.None;
                }
                else if (currentContent.IsOptions)
                {
                    return ContentType.Options;
                }
                else
                {
                    return ContentType.Line;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this bubble dialogue view is
        /// currently displaying a bubble.
        /// </summary>
        public bool IsShowingBubble => currentBubble != null;

        /// <summary>
        /// The prefab to use when needing to instantiate a new bubble.
        /// </summary>
        /// <remarks>
        /// If the <see cref="CharacterBubbleAnchor.characterBubblePrefab"/> exists that will be used instead.
        /// </remarks>
        [SerializeField] private Bubble? bubblePrefab;

        /// <summary>
        /// The canvas upon which all bubbles will live
        /// </summary>
        [SerializeField] private Canvas? bubbleCanvas;

        [Header("Bubble Targets")]

        /// <summary>
        /// The name of the player character, this is used when a <see cref="Line"/> has no character value.
        /// </summary>
        [Tooltip("If a line has no character name, which " + nameof(CharacterBubbleAnchor) + " should the bubble be attached to?")]
        [SerializeField] private string PlayerName = "Player";

        [Header("Interactivity")]
        /// <summary>
        /// Should dialogue autoadvance onto the next line or not?
        /// </summary>
        [SerializeField] private bool autoAdvance = false;

        [Header("Timing")]

        /// <summary>
        /// How long the bubble takes to shrink down when it is dismissed.
        /// </summary>
        [SerializeField] private float bubbleHideDuration = 0.4f;

        /// <summary>
        /// How long the bubble takes to grow when being presented.
        /// </summary>
        [SerializeField] private float bubbleShowDuration = 0.4f;

        /// <summary>
        /// How long an already existing presented bubble takes to resize itself to present a new line
        /// </summary>
        [SerializeField] private float bubbleAdjustSizeDuration = 0.25f;

        /// <summary>
        /// How long will the text remain before advancing to the next line.
        /// </summary>
        /// <remarks>
        /// Only applicable when <see cref="autoAdvance"/> is set to true.
        /// </remarks>
        [ShowIf(nameof(autoAdvance))]
        [SerializeField] private float textRestDuration = 1f;


        [Header("Options")]

        /// <summary>
        /// When presenting options, can you wrap around from one end to the
        /// other?
        /// </summary>
        /// <remarks>
        /// Essentially if I have the first option selected, can I go to the
        /// last option directly from here?
        /// </remarks>
        [SerializeField] private bool AllowOptionWrapAround = true;

        /// <summary>
        /// Do we allow showing the unavailable options?
        /// </summary>
        /// <remarks>
        /// An option is unavailable if it has a condition, and that condition
        /// evaluates to false at run-time.
        /// </remarks>
        [SerializeField] private bool ShowUnavailableOptions = false;

        /// <summary>
        /// Do we allow selecting unavailable options?
        /// </summary>
        /// <remarks>
        /// This is very uncommon but sometimes might be what you want.
        /// Does nothing if <see cref="ShowUnavailableOptions"/> is set to false.
        /// </remarks>
        [ShowIf(nameof(ShowUnavailableOptions))]
        [SerializeField] private bool AllowSelectingUnavailableOption = false;

        [Header("Bubble Management")]
        [Tooltip("Should this dialogue view respond to commands like <<" + HideBubbleCommandName + ">>?")]
        [SerializeField] bool affectedByBubbleCommands = true;

        /// <summary>
        /// A dictionary of every character and the anchor target for that character.
        /// </summary>
        private readonly Dictionary<string, CharacterBubbleAnchor> characterTargets = new Dictionary<string, CharacterBubbleAnchor>();

        /// <summary>
        /// A dictionary mapping every character to their bubble.
        /// </summary>
        /// <remarks>
        /// Lets us reuse bubbles after finishing with one otherwise we'd have to instantiate a new bubble each time the conversation switch active speaker
        /// </remarks>
        private readonly Dictionary<string, Bubble> bubbles = new Dictionary<string, Bubble>();

        [Tooltip("When this view starts, pre-create a Bubble for every Character Anchor in the scene.")]
        [SerializeField] private bool preCreateBubblesOnStart = false;

        /// <summary>
        /// The current bubble being used to present a line/option
        /// </summary>
        private Bubble? currentBubble;

        /// <summary>
        /// The bubble content currently being presented in <see
        /// cref="currentBubble"/>.
        /// </summary>
        private Bubble.BubbleContent currentContent = Bubble.BubbleContent.None;

        private bool isPerformingBubbleAnimation = false;

        private YarnTaskCompletionSource<int>? optionSelectedCompletionSource;

        [SerializeField] private ActionMarkupHandler[]? eventHandlers;

        const string HideBubbleCommandName = "hide_bubbles";

        // we add in a special command <<hide_bubble>> you can use to dismiss a bubble
        // this is because we use a change in speaker as our means of dismissing bubbles
        // we can't tell if a command should also do this, hence this lets you control it
        [YarnCommand(HideBubbleCommandName)]
        public static async YarnTask HideBubbles()
        {
            foreach (var bubbleDialogueView in FindObjectsByType<BubbleDialogueView>(FindObjectsSortMode.None))
            {
                if (bubbleDialogueView.affectedByBubbleCommands)
                {
                    await bubbleDialogueView.BubbleCommandHide();
                }
            }
        }

        void Start()
        {
            if (eventHandlers != null)
            {
                ActionMarkupHandlers.AddRange(eventHandlers);
            }
            UpdateCharacterAnchors();
        }

        private void UpdateCharacterAnchors()
        {
            IEnumerable<CharacterBubbleAnchor> allAnchors = FindObjectsByType<CharacterBubbleAnchor>(FindObjectsSortMode.None);

            characterTargets.Clear();

            // finding and associating every character target with their name
            foreach (var anchor in allAnchors)
            {
                if (anchor.CharacterName == null)
                {
                    Debug.LogWarning($"Can't add anchor {anchor.name}: {nameof(anchor.CharacterName)} is null");
                    continue;
                }

                if (characterTargets.TryAdd(anchor.CharacterName, anchor))
                {
                    if (this.preCreateBubblesOnStart)
                    {
                        CreateBubbleForAnchor(anchor);
                    }
                }
                else
                {
                    // We failed to add an entry for this anchor's character
                    // name - there must already be an entry with this name.
                    Debug.LogWarning($"More than one {nameof(CharacterBubbleAnchor)} has the character name {anchor.CharacterName}. Each {nameof(CharacterBubbleAnchor)} must have a unique character name.");
                }
            }
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            return YarnTask.CompletedTask;
        }

        public override async YarnTask OnDialogueCompleteAsync()
        {
            // dialogue is finished, remove every bubble
            if (currentBubble != null)
            {
                await DestroyAllBubbles();
            }
            currentBubble = null;
        }

        /// <summary>
        /// Goes through every bubble and removes them.
        /// </summary>
        /// <remarks>
        /// The current bubble is hidden first before being destroyed.
        /// </remarks>
        /// <returns>The task that hides and then destroys the bubbles</returns>
        private async YarnTask DestroyAllBubbles()
        {
            // If we have a bubble up, dismiss it.
            if (currentBubble != null)
            {
                await currentBubble.DismissBubble(bubbleHideDuration, destroyCancellationToken);
            }
            // set ourselves back to fresh
            currentBubble = null;
            this.currentContent = Bubble.BubbleContent.None;
            // destroy all the cached bubbles
            foreach (var bubble in bubbles)
            {
                Destroy(bubble.Value.gameObject);
            }
            bubbles.Clear();
        }

        public async YarnTask BubbleCommandHide()
        {
            if (currentBubble == null)
            {
                Debug.LogWarning("asked by a command to hide bubbles yet have no bubble to hide");
                return;
            }

            // dismiss the current bubble
            await currentBubble.DismissBubble(bubbleHideDuration, destroyCancellationToken);

            // null out the current bubble so that when the next run line comes it it will act as if this is a first time
            // but still use the cached bubbles
            currentBubble = null;
            currentContent = Bubble.BubbleContent.None;
        }

        private async YarnTask PresentContentInBubble(Bubble bubbleForThisLine, Bubble.BubbleContent content, CancellationToken token)
        {
            this.isPerformingBubbleAnimation = true;

            if (bubbleForThisLine == currentBubble)
            {
                await bubbleForThisLine.UpdateBubble(content, bubbleAdjustSizeDuration, token);
            }
            else
            {
                // This bubble's speaker is different to the previous one
                // (or we don't have a bubble at all.) Dismiss the previous
                // one, if any, and present the current one.
                if (currentBubble != null)
                {
                    await currentBubble.DismissBubble(bubbleHideDuration, token);
                }
                currentBubble = bubbleForThisLine;
                await bubbleForThisLine.PresentBubble(content, bubbleShowDuration, token);
            }

            isPerformingBubbleAnimation = false;
        }

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            this.currentContent = new Bubble.BubbleContent(line, PlayerName, ActionMarkupHandlers);

            var bubbleForThisLine = GetOrCreateBubble(this.currentContent);

            if (bubbleForThisLine == null)
            {
                // We don't have a bubble we can use! That's an error.
                Debug.LogError($"No ${nameof(CharacterBubbleAnchor)} for the character {this.currentContent.BubbleOwnerName} was found. Cannot show a bubble for this character.");
                return;
            }

            await PresentContentInBubble(bubbleForThisLine, this.currentContent, token.HurryUpToken);

            if (autoAdvance)
            {
                int time = (int)(this.textRestDuration * 1000);
                await YarnTask.Delay(time, token.NextLineToken).SuppressCancellationThrow();
            }
            else
            {
                await YarnTask.WaitUntilCanceled(token.NextLineToken);
            }

            // the line is now finished, so we call the OnLineWillDismiss for every handler
            foreach (var handler in ActionMarkupHandlers)
            {
                handler.OnLineWillDismiss();
            }
        }

        public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, CancellationToken cancellationToken)
        {
            this.currentContent = new Bubble.BubbleContent(dialogueOptions, AllowOptionWrapAround, ShowUnavailableOptions, PlayerName, ActionMarkupHandlers);

            var bubbleForTheseOptions = GetOrCreateBubble(this.currentContent);

            if (bubbleForTheseOptions == null)
            {
                // We don't have a bubble we can use! That's an error.
                Debug.LogError($"Cannot show a bubble for the character {this.currentContent.BubbleOwnerName}. Hanging here!");
                return null;
            }

            // configuring the completion source for the options bundle
            optionSelectedCompletionSource = new YarnTaskCompletionSource<int>();
            // if the options task itself is cancelled we want to also cancel waiting for the user to select them
            cancellationToken.Register(() =>
            {
                optionSelectedCompletionSource?.TrySetCanceled();
            });

            // show the current option
            await PresentContentInBubble(bubbleForTheseOptions, this.currentContent, cancellationToken);

            try
            {
                // wait until the user selects on the options
                int index = await optionSelectedCompletionSource.Task;
                return dialogueOptions[index];
            }
            catch (OperationCanceledException)
            {
                // if the options were cancelled then return null
                return null;
            }
            finally
            {
                // in all cases we null out the completion source
                optionSelectedCompletionSource = null;
            }
        }

        /// <summary>
        /// Called by a number of different pieces to change which is the currently displayed option.
        /// </summary>
        /// <param name="diff">the amount the <see cref="bundle.currentOptionIndex"/> should change by</param>
        public async YarnTask ChangeOption(int diff, CancellationToken token)
        {
            // we need a bubble up or else we can't do anything
            if (currentBubble == null)
            {
                Debug.LogError("asked to change which option is shown yet no bubble exists");
                return;
            }
            if (currentContent.HasContent == false)
            {
                Debug.LogError("asked to change which option is shown but the current bubble has invalid content");
                return;
            }
            if (currentContent.IsOptions == false)
            {
                Debug.LogError("asked to change which option is shown but the current bubble is not showing options");
                return;
            }

            var previousIndex = this.currentContent.CurrentElementIndex;

            this.currentContent = this.currentContent.MoveToContent(diff);

            // It is possible with being asked to move through a lot of options
            // we are back where we started. If that's the case, don't re-present
            // the same option
            if (previousIndex == this.currentContent.CurrentElementIndex)
            {
                return;
            }

            var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(token, destroyCancellationToken);

            await PresentContentInBubble(this.currentBubble, this.currentContent, linkedSource.Token);
            linkedSource.Dispose();
        }

        /// <summary>
        /// Chooses the currently presented option and informs the dialogue runner of this.
        /// </summary>
        /// <remarks>
        /// Whatever the id of the <see cref="bundle.currentOptionIndex"/> is sent to the dialogue runners through the <see cref="bundle.onSelect"/> action.
        /// Bundle is then nullified to prevent any more selections.
        /// </remarks>
        public void SelectOption()
        {
            // we also need a bubble to be up and showing the option
            if (currentBubble == null)
            {
                Debug.LogWarning($"{nameof(SelectOption)} was called, but this {nameof(BubbleDialogueView)} is not currently presenting a bubble.");
                return;
            }

            // it needs to be showing options, otherwise we can't select them
            if (currentContent.IsOptions == false)
            {
                // we need an option bundle, otherwise we can't do anything
                Debug.LogWarning($"{nameof(SelectOption)} was called, but this {nameof(BubbleDialogueView)} is not currently presenting options.");
                return;
            }

            // if we don't have a completion source we can't return the selected option
            if (optionSelectedCompletionSource == null)
            {
                Debug.LogWarning($"{nameof(SelectOption)} was called, but this {nameof(BubbleDialogueView)} does not have a completions source configured.");
                return;
            }

            if (isPerformingBubbleAnimation)
            {
                // We're in the middle of animating the bubble. Don't accept
                // this input at the moment, because telling the dialogue runner
                // that we've selected an option could cause it to issue an
                // instruction to dismiss the bubble in the middle of this
                // animatiom (which would confuse us!)
                return;
            }

            if (currentContent.CurrentElement.IsAvailable == false && !AllowSelectingUnavailableOption)
            {
                // We're currently presenting options, but we can't select THIS
                // option because it's unavailable and we're configured to not
                // selecting it.
                return;
            }

            if (!this.optionSelectedCompletionSource.TrySetResult(currentContent.CurrentElement.OptionID))
            {
                Debug.LogWarning("was unable to set the selected option");
                return;
            }

            // null out the completion source so that we can't run this code again if the user double taps the button or whatever
            this.optionSelectedCompletionSource = null;

            // We don't dismiss the bubble here, because we may want to re-use
            // this current bubble with the next piece of content. If we need to
            // dismiss or change the bubble, then that'll be up to the next
            // piece of incoming content.
        }

        /// <summary>
        /// Returns the specific bubble that is associated with that character.
        /// </summary>
        /// <remarks>
        /// If one doesn't exist, make a new one for that character and then
        /// return that. Where possible attempts to use the character specific
        /// prefab, otherwise will use the fallback one.
        /// </remarks>
        /// <param name="content">The bubble content to use. Its <see
        /// cref="Bubble.BubbleContent.BubbleOwnerName"/> must be non-null and
        /// must match a key inside of <see cref="characterTargets"/>.</param>
        /// <returns>The <see cref="Bubble"/> for the particular <paramref
        /// name="character"/></returns>
        private Bubble? GetOrCreateBubble(Bubble.BubbleContent content)
        {
            Bubble bubble;

            string? character = content.BubbleOwnerName;
            if (character == null)
            {
                // We don't have a name we can use to find who'll own this
                // bubble. We can't create the bubble!
                Debug.LogError($"Can't create a bubble because no character name was provided and the default character name isn't known!", this);
                return null;
            }

            // Try and get an existing bubble for the character.
            if (!bubbles.TryGetValue(character, out bubble))
            {
                // We don't have a bubble for this character. We'll need to
                // create a new one.


                if (characterTargets.TryGetValue(character, out var bubbleAnchor) == false)
                {
                    // We don't know about the character we're trying to create
                    // a bubble for! Try to update our list of characters - we
                    // might be trying to create a bubble for a character that
                    // has recently been added.
                    UpdateCharacterAnchors();

                    if (characterTargets.TryGetValue(character, out bubbleAnchor) == false)
                    {
                        // We still don't know! We can't create the bubble!
                        Debug.LogError($"Can't create a bubble for character {character} because no anchor named \"{character}\" could be found!", this);
                        return null;
                    }
                }

                return CreateBubbleForAnchor(bubbleAnchor);
            }

            // Return the bubble that we either created or fetched.
            return bubble;
        }

        private Bubble? CreateBubbleForAnchor(CharacterBubbleAnchor bubbleAnchor)
        {
            var character = bubbleAnchor.CharacterName;
            if (bubbleCanvas == null)
            {
                // We don't have a canvas we can add the bubble to!
                Debug.LogError($"Can't create a bubble for character {character} because {nameof(bubbleCanvas)} is null!", this);
                return null;
            }

            if (character == null)
            {
                // We don't have a name we can use for the bubbler!
                Debug.LogError($"Can't create a bubble for character {character} because {nameof(bubbleAnchor.CharacterName)} is null!", this);
                return null;

            }

            // If this character has their own specific bubble, use that.
            var prefab = bubbleAnchor.characterBubblePrefab;
            if (prefab == null)
            {
                // Otherwise, we will use the generic one.
                if (bubblePrefab == null)
                {
                    // We don't have a prefab we can use for this bubble!
                    Debug.LogError($"Can't create a bubble for character {character} because this character doesn't define their own bubble prefab, and the default bubble prefab was not set!", this);
                    return null;
                }
                prefab = bubblePrefab;
            }

            var bubble = GameObject.Instantiate<Bubble>(prefab, bubbleCanvas.transform);
            bubble.gameObject.name = $"{prefab.name} ({character})";
            bubbles[character] = bubble;
            // defaulting the bubble
            bubble.Target = bubbleAnchor;

            // Return the bubble that we either created or fetched.
            return bubble;
        }
    }
}
