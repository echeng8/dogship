using UnityEditor;

namespace Yarn.Unity.Addons.SpeechBubbles
{
    using UnityEngine;
    using System;
    using System.Collections.Generic;
    using Yarn.Unity.Attributes;

#nullable enable

    /// <summary>
    /// Which edge of the bubble's rectangle will the stem be coming out of?
    /// </summary>
    public enum EdgePositioning : short
    {
        Left,
        Above,
        Right,
        Below,
    }

    /// <summary>
    /// How will the stem respond to the bubble target moving out of frame?
    /// </summary>
    public enum StemEdgeRule
    {
        /// <summary>
        /// The end point of the stem continues to follow the target unabaited.
        /// </summary>
        /// <remarks>
        /// This can result in VERY long stems if left alone to wander but for things *just* off frame it works.
        /// </remarks>
        Stretch,
        /// <summary>
        /// The end point of the stem is locked to the edge of the frame.
        /// </summary>
        /// <remarks>
        /// Very good for showing the direction of where a line is coming from but if allowed over great distances loses impact.
        /// </remarks>
        Clip,
        /// <summary>
        /// Once the bubble target leaves the frame the stem will hide itself.
        /// </summary>
        Disappear,
    }

    /// <summary>
    /// Represents the bubble itself.
    /// </summary>
    /// <remarks>
    /// Is responsible for all layout, positioning, and tracking of the bubble.
    /// The content inside of the bubble will be responsibility of the <see cref="BubbleContentView"/> class.
    /// </remarks>
    [ExecuteAlways]
    public sealed class Bubble : MonoBehaviour
    {
        [System.Serializable]
        public struct BubbleContent
        {
            internal static readonly BubbleContent None = new BubbleContent
            {
                ActionMarkupHandlers = Array.Empty<IActionMarkupHandler>(),
            };

            [System.Serializable]
            public struct Element
            {
                public string Text
                {
                    get
                    {
                        return Line.Text;
                    }
                }
                public Markup.MarkupParseResult Line;
                public bool IsAvailable;
                [HideInInspector]
                public int OptionID;
            }

            [HideInInspector]
            public string? BubbleOwnerName;

            public Element[] Elements;

            [Min(0)]
            public int CurrentElementIndex;

            [Label("Show Option UI")]
            [SerializeField] private bool isOptions;

            [ShowIf(nameof(isOptions))]
            public bool AllowOptionWrapping;

            public IEnumerable<IActionMarkupHandler> ActionMarkupHandlers;

            public readonly bool IsLine => HasContent && isOptions == false;
            public readonly bool IsOptions => HasContent && isOptions == true;
            public readonly Element CurrentElement
            {
                get
                {
                    if (HasContent && CurrentElementIndex >= 0 && CurrentElementIndex < Elements.Length)
                    {
                        // The index is valid. Return the corresponding element.
                        return Elements[CurrentElementIndex];
                    }
                    else if (Elements != null && Elements.Length > 0)
                    {
                        // The index is out of range, but we have content, so
                        // clamp the index to the available elements and use
                        // that
                        return Elements[Mathf.Clamp(CurrentElementIndex, 0, Elements.Length - 1)];
                    }
                    else
                    {
                        // We don't have any content. Return an empty element.
                        return new Element();
                    }
                }
            }

            public readonly bool HasContent { get => this.Elements != null && this.Elements.Length > 0; }

            public readonly bool CanMovePrevious
            {
                get
                {
                    if (AllowOptionWrapping)
                    {
                        return true;
                    }
                    else
                    {
                        Wrap(CurrentElementIndex - 1, 0, Elements.Length, out bool wrapped);
                        return !wrapped;
                    }
                }
            }

            public readonly bool CanMoveNext
            {
                get
                {
                    if (AllowOptionWrapping)
                    {
                        return true;
                    }
                    else
                    {
                        Wrap(CurrentElementIndex + 1, 0, Elements.Length, out bool wrapped);
                        return !wrapped;
                    }
                }
            }

            public BubbleContent(LocalizedLine line, string defaultCharacterName, IEnumerable<IActionMarkupHandler>? markupHandlers)
            {
                Elements = new[]
                {
                    new Element
                    {
                        Line = line.TextWithoutCharacterName,
                        IsAvailable = true,
                        OptionID = -1,
                    }
                };
                BubbleOwnerName = line.CharacterName ?? defaultCharacterName;
                CurrentElementIndex = 0;
                isOptions = false;
                this.AllowOptionWrapping = false;

                if (markupHandlers != null)
                {
                    ActionMarkupHandlers = markupHandlers;
                }
                else
                {
                    ActionMarkupHandlers = Array.Empty<IActionMarkupHandler>();
                }
            }

            public BubbleContent(DialogueOption[] dialogueOptions, bool allowOptionWrapping, bool showUnavailableOptions, string defaultCharacterName, IEnumerable<IActionMarkupHandler>? markupHandlers)
            {
                var content = new List<Element>();
                string? ownerName = null;

                int firstAvailable = -1;
                int i = 0;

                foreach (var option in dialogueOptions)
                {
                    if (!option.IsAvailable && showUnavailableOptions == false)
                    {
                        // Skip this option - it's unavailable and we aren't
                        // showing unavailable options.
                        continue;
                    }
                    if (option.Line.CharacterName != null)
                    {
                        ownerName = option.Line.CharacterName;
                    }
                    content.Add(new Element
                    {
                        Line = option.Line.TextWithoutCharacterName,
                        IsAvailable = option.IsAvailable,
                        OptionID = option.DialogueOptionID,
                    });
                    if (firstAvailable == -1 && option.IsAvailable)
                    {
                        firstAvailable = i;
                    }
                }

                if (content.Count == 0)
                {
                    // No options were added. Return the empty content.
                    this = BubbleContent.None;
                    return;
                }

                // If nothing was available, default the selection to the first item
                if (firstAvailable == -1)
                {
                    firstAvailable = 0;
                }

                Elements = content.ToArray();
                CurrentElementIndex = firstAvailable;
                BubbleOwnerName = ownerName ?? defaultCharacterName;
                isOptions = true;
                AllowOptionWrapping = allowOptionWrapping;

                if (markupHandlers != null)
                {
                    ActionMarkupHandlers = markupHandlers;
                }
                else
                {
                    ActionMarkupHandlers = Array.Empty<IActionMarkupHandler>();
                }
            }

            private static int Wrap(int value, int minInclusive, int maxExclusive, out bool isWrapped)
            {
                if (minInclusive == maxExclusive)
                {
                    throw new ArgumentException("Both the minimum inclusive and maximum exclusive values are the same.");
                }

                if (value < minInclusive)
                {
                    isWrapped = true;
                    return maxExclusive - (Math.Abs(minInclusive - value) % maxExclusive);
                }
                if (value >= maxExclusive)
                {
                    isWrapped = true;
                    return value % maxExclusive;
                }

                isWrapped = false;
                return value;
            }

            public readonly BubbleContent MoveNext()
            {
                return MoveToContent(1);
            }

            public readonly BubbleContent MovePrevious()
            {
                return MoveToContent(-1);
            }

            public readonly BubbleContent MoveToContent(int offset)
            {
                var start = this.CurrentElementIndex;
                var current = start;
                var next = current += offset;

                // Proceed to the next index, wrapping if necessary (and
                // noting if we wrapped.)
                next = Wrap(next, 0, this.Elements.Length, out bool isWrapped);

                if (isWrapped && this.AllowOptionWrapping == false)
                {
                    // We've reached the end of the list, and we aren't
                    // allowed to wrap. Stop here.                        
                }
                else
                {
                    // Update to this new index.
                    current = next;
                }

                // Create a new copy of this content where the current index is
                // updated
                var newContent = this;
                newContent.CurrentElementIndex = current;

                return newContent;
            }
        }

        [System.Serializable]
        public class PresentationState
        {
            [Range(0f, 1f)]
            public float presentationFactor = 0f;

            // The current content being displayed by this bubble.
            public BubbleContent content = default;

            public CharacterBubbleAnchor? target;
        }

        /// <summary>
        /// The character anchor that this bubble follows as it's target.
        /// </summary>
        /// <remarks>
        /// The position of this character will be the main determinant of the position of this bubble.
        /// </remarks>
        public CharacterBubbleAnchor? Target
        {
            get
            {
                return this.State.target;
            }
            set
            {
                if (content != null)
                {
                    if (value != null)
                    {
                        content.SetCharacter(value.characterBubbleData);
                    }
                    else
                    {
                        content.SetCharacter(null);
                    }
                }
                this.State.target = value;
            }
        }

        [SerializeField] PresentationState previewState = new PresentationState();
        PresentationState runtimePresentationState = new PresentationState();

        [Header("Bubble Elements")]

        /// <summary>
        /// The rectangle that contains the contents of the bubble.
        /// </summary>
        /// <remarks>
        /// This needs to be separate from the bubble itself to avoid an endless cycle of movements.
        /// Every element of the bubble should be contained within this rectangle lest weird things happen.
        /// In particular the graphic, content, and stem all need to belong in this
        /// </remarks>
        public RectTransform? bubbleContainer;

        /// <summary>
        /// The content (so text, indicators, etc) of the bubble.
        /// </summary>
        /// <remarks>
        /// Is it's own type just so that it can be more easily modified without impacting the positioning code.
        /// </remarks>
        public BubbleContentView? content;

        /// <summary>
        /// The canvas this bubble and it's <see cref="bubbleContainer"/> lives within.
        /// </summary>
        /// <remarks>
        /// Necessary for coordinate calculations
        /// </remarks>
        private Canvas? _canvas;

        [Header("Stem")]

        /// <summary>
        /// The stem of the bubble.
        /// </summary>
        /// <remarks>
        /// This can be null if you want bubbles just floating around ominously.
        /// </remarks>
        [SerializeField] private IsoscelesStem? stem;

        /// <summary>
        /// The length of the stem.
        /// </summary>
        /// <remarks>
        /// This doesn't actually change the length of the <see cref="stem"/> itself, it moves the bubble this much distance away from <see cref="Target"/>.
        /// Because the stem is drawn from the centre to the target this in-effect makes the stem this long.
        /// </remarks>
        [ShowIf("stem")]
        [SerializeField] private float stemLength = 10f;


        /// <summary>
        /// Controls what the <see cref="stem"/> will do once the <see cref="Target"/> leaves the view port.
        /// </summary>
        /// <remarks>
        /// Does nothing if <see cref="constrainToViewPort"/> is set to false.
        /// </remarks>
        [ShowIf(nameof(stem))]
        [UnityEngine.Serialization.FormerlySerializedAs("stemEdgeRule")]
        [SerializeField] private StemEdgeRule stemEdgeBehaviour;

        /// <summary>
        /// The number of canvas distance units to shrink the available space for the stem by.
        /// </summary>
        /// <remarks>
        /// This exists because during development we found that we often wanted the bubble to be
        /// able to go hard up against the view port (so 0 for <see cref="viewPortMarginPercentage"/>)
        /// but when the stem did the same it looked a little odd.
        /// So this exists to give the stem it's own smaller margin that doesn't impact the rest of the calculations.
        /// </remarks>
        [ShowIf(nameof(stem))]
        [ShowIf(nameof(StemIsConstrained))]
        [Range(0, 25)]
        [Label("Stem Viewport Inset")]
        [SerializeField] private int screenFudge;

        internal bool StemIsConstrained => stemEdgeBehaviour != StemEdgeRule.Stretch;

        [Header("Positioning")]

        /// <summary>
        /// From which edge does the bubble follow the <see cref="Target"/>
        /// </summary>
        /// <remarks>
        /// For example if this is set to <see cref="EdgePositioning.Above"/> the bubble will be positioned above the <see cref="Target"/>.
        /// </remarks>
        [UnityEngine.Serialization.FormerlySerializedAs("edgeFollow")]
        [SerializeField] private EdgePositioning positionRelativeToAnchor;

        /// <summary>
        /// By how much should the bubble be offset from the centre of the calculated tracking position?
        /// </summary>        
        [SerializeField] private Vector2 CentreOffset;

        [Space]

        /// <summary>
        /// Should this bubble be restricted to the view port.
        /// </summary>
        /// <remarks>
        /// That is to say if the <see cref="Target"/> moves out of frame should the bubble continue to follow or be pinned to the screen edge?
        /// </remarks>
        [SerializeField] private bool constrainToViewPort = true;

        /// <summary>
        /// How much of the view port edges (as a percentage) should the bubbles be constrained within?
        /// </summary>
        /// <remarks>
        /// This reduces the total size of the viewport, as a percentage, by this amount, from the centre outwards.
        /// Does nothing if <see cref="constrainToViewPort"/> is false.
        /// </remarks>
        [Range(0, 25)]
        [ShowIf(nameof(constrainToViewPort))]
        [SerializeField] private int viewPortMarginPercentage = 10;



        [Header("Timing")]

        /// <summary>
        /// An animation curve used to adjust the presentation of the bubble.
        /// </summary>
        /// <remarks>
        /// The values from this are used to drive the localScale of bubble when presenting or hiding the bubble.
        /// The time value for this curve comes from the dialogue view in the form of the <see cref="PresentationFactor"/> value.
        /// </remarks>
        [UnityEngine.Serialization.FormerlySerializedAs("showTimer")]
        [SerializeField] private AnimationCurve presentationCurve = AnimationCurve.Linear(0, 0, 0, 0);

        private Canvas? Canvas
        {
            get
            {
                if (_canvas == null)
                {
                    _canvas = GetComponentInParent<Canvas>();
                }
                return _canvas;
            }
        }

        /// <summary>
        /// Represents the abstract current time for animation purposes.
        /// </summary>
        /// <remarks>
        /// Will be set by the dialogue view during presentation and dismissal moments.
        /// Time is always normalised to be between 0 and 1 regardless of actual clock time taken to move between these values.
        /// </remarks>
        public float PresentationFactor
        {
            get
            {
                return this.State.presentationFactor;

            }
            private set
            {
                if (this.State.presentationFactor == value)
                {
                    // Do nothing if our value hasn't actually changed
                    return;
                }

                this.State.presentationFactor = value;
                ApplyPresentationFactor();
            }
        }

        private void ApplyPresentationFactor()
        {
            if (bubbleContainer == null)
            {
                return;
            }

            float scale = presentationCurve.Evaluate(PresentationFactor);
            bubbleContainer.localScale = Vector3.one * scale;

            if (stem != null)
            {
                stem.UpdatePresentationFactor(this.State.presentationFactor);
            }

            if (content != null)
            {
                content.PresentationFactor = this.State.presentationFactor;
            }
        }

        public bool UsePreviewData
        {
            get
            {
                // We only want to use our preview state if we're in the editor
                // and not currently playing. Otherwise, we want to use the
                // runtime state.
                return Application.isEditor && !Application.isPlaying;
            }
        }

        public PresentationState State
        {
            get
            {
                return this.UsePreviewData ? this.previewState : this.runtimePresentationState;
            }
        }

        void Start()
        {
            // performing some initial hiding of the various bits and pieces
            if (stem != null && Target != null)
            {
                stem.Target = Target.BubblePosition.position;
                stem.edgeFollow = positionRelativeToAnchor;
            }

            if (this.content != null)
            {
                // Clear all content from the bubble
                this.content.SetContentWithoutAnimation(BubbleContent.None);
            }

            // In edit mode, if we've just appeared, default to fully presented.
            if (Application.isEditor && Application.isPlaying == false)
            {
                PresentationFactor = 1f;
            }

            // Ensure that our scale is correct on the first frame we appear at
            ApplyPresentationFactor();
        }

        /// <summary>
        /// Calculates and returns the size the bubble will need to be to display this content.
        /// </summary>
        /// <remarks>
        /// Is a wrapper around <see cref="BubbleContentView.GetContentSize"/>
        /// </remarks>
        /// <param name="line">The line of text soon to be displayed</param>
        /// <param name="isOption">If the upcoming line is part of an option</param>
        /// <returns>The size of the bubble content needed to hold and present this upcoming line</returns>
        public Vector2 GetContentSize(BubbleContent bubbleContent)
        {
            if (content == null)
            {
                return Vector2.zero;
            }
            else
            {
                return content.GetContentSize(bubbleContent);
            }
        }

        /// <summary>
        /// Configures the bubble to be ready to show a line.
        /// </summary>
        /// <remarks>
        /// Is a wrapper around <see cref="BubbleContentView.PrepareForLine"/>
        /// </remarks>
        public void SetStateForLine()
        {
            if (content != null)
            {
                content.PrepareForLine();
            }
        }

        /// <summary>
        /// Configures the bubble to be ready to show an option.
        /// </summary>
        /// <remarks>
        /// Is a wrapper around <see cref="BubbleContentView.PrepareForOptions"/>
        /// </remarks>
        public void SetStateForOptions()
        {
            if (content != null)
            {
                content.PrepareForOptions();
            }
        }

        internal void Update()
        {
            if (TryGetComponent<RectTransform>(out var rect))
            {
                rect.localScale = Vector3.one;
            }

            if (this.UsePreviewData)
            {
                this.UpdateBubbleImmediate(this.previewState.content);
            }

            Vector2 bubblePosition;
            Vector3 targetPositionWorldSpace;
            if (Target != null)
            {
                targetPositionWorldSpace = Target.BubblePosition.position;
            }
            else
            {
                targetPositionWorldSpace = Vector3.zero;
            }

            // the scale of the bubble changes depending on the point in time
            // most of the time this will be 0 or 1 though
            ApplyPresentationFactor();

            bubblePosition = UpdateBubbleContainerPosition(targetPositionWorldSpace);

            UpdateStemPosition(bubblePosition);
        }

        internal void UpdateStemPosition(Vector2 bubblePosition)
        {
            if (stem == null || this.bubbleContainer == null)
            {
                return;
            }
            Vector3 targetWorldPosition;

            if (Target == null)
            {
                targetWorldPosition = this.bubbleContainer.position + (this.stemLength * Vector3.up);
            }
            else
            {
                targetWorldPosition = Target.BubblePosition.position;
            }

            if (Canvas == null)
            {
                Debug.LogError($"Bubble is not in a Canvas!", this);
                return;
            }

            stem.edgeFollow = positionRelativeToAnchor;
            stem.pointInTime = PresentationFactor;
            stem.rectTransform.anchoredPosition = Vector2.zero;
            stem.rectTransform.pivot = Vector2.one * 0.5f;
            stem.rectTransform.anchorMin = Vector2.one * 0.5f;
            stem.rectTransform.anchorMax = Vector2.one * 0.5f;
            stem.rectTransform.localScale = Vector2.one;

            // if we aren't constrained to the view port the stem will always just follow perfectly as is
            // so the different stem edge rules have no impact
            if (!constrainToViewPort)
            {
                stem.Target = WorldToRectangleSpace(targetWorldPosition, bubbleContainer, Camera.main, Canvas);
                return;
            }

            // depending on the stem edge rule and the position of the target we need to change what we tell the stem
            switch (stemEdgeBehaviour)
            {
                // we are clipping the stem
                // so if the target is offscreen we lock the stem to the viewport
                case StemEdgeRule.Clip:
                    {
                        var offScreen = TargetOffScreen(bubblePosition);
                        if (offScreen.Item1)
                        {
                            stem.Target = WorldToRectangleSpace(offScreen.Item2, bubbleContainer, Camera.main, Canvas);
                        }
                        else
                        {
                            stem.Target = WorldToRectangleSpace(targetWorldPosition, bubbleContainer, Camera.main, Canvas);
                        }
                        break;
                    }

                // we are making the stem go away
                // so if the target is offscreen we tell the stem to start shrinking
                // or if it just came back, show itself
                case StemEdgeRule.Disappear:
                    {
                        var offScreen = TargetOffScreen(bubblePosition);
                        if (offScreen.IsOffScreen)
                        {
                            stem.Target = WorldToRectangleSpace(targetWorldPosition, bubbleContainer, Camera.main, Canvas);
                            stem.Hide();
                        }
                        else
                        {
                            stem.Show();
                            stem.Target = WorldToRectangleSpace(targetWorldPosition, bubbleContainer, Camera.main, Canvas);
                        }
                        break;
                    }

                // we are stretching the stem
                // just let it go wild
                case StemEdgeRule.Stretch:
                    {
                        stem.Target = WorldToRectangleSpace(targetWorldPosition, bubbleContainer, Camera.main, Canvas);
                        break;
                    }
            }
        }

        internal Vector2 UpdateBubbleContainerPosition(Vector3 targetPositionWorldSpace)
        {
            if (Canvas == null)
            {
                Debug.LogError($"Bubble is not in a canvas!");
                return Vector2.zero;
            }

            if (bubbleContainer == null)
            {
                Debug.LogError($"{nameof(bubbleContainer)} is null!");
                return Vector2.zero;
            }

            var screenSize = Vector2.zero;
            screenSize.x = Canvas.GetComponent<RectTransform>().rect.width;
            screenSize.y = Canvas.GetComponent<RectTransform>().rect.height;

            // working out the position the bubble will need to be to track the target
            // the magic all happens within the BubbleTargetWorldPositionToRectangleSpace function
            Vector2 position = BubbleTargetWorldPositionToRectangleSpace(
                targetPositionWorldSpace,
                GetComponent<RectTransform>(),
                screenSize,
                (float)viewPortMarginPercentage / 100,
                Canvas
            );

            bubbleContainer.pivot = new Vector2(0.5f, 0.5f);
            bubbleContainer.anchorMin = new Vector2(0.5f, 0.5f);
            bubbleContainer.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleContainer.anchoredPosition = position;

            return screenSize;
        }

        /// <summary>
        /// Determines if the target is offscreen, and if it is which point on the edge is closest to it.
        /// </summary>
        /// <param name="screen">The rectangle that represents the screen</param>
        /// <returns>A tuple with two elements, the first a boolean where true means the target is off the screen.
        /// The second element is the world space position of the screen edge nearest to the target or the target itself if it is within the screen.</returns>
        private (bool IsOffScreen, Vector3 WorldspaceNearestEdgePosition) TargetOffScreen(Vector2 screen)
        {
            if (Target == null)
            {
                // We don't have a target.
                return (true, Vector3.zero);
            }

            // working out by how much of the viewport constraint and fudge do we need to reduce the screen by
            float xFactor = constrainToViewPort ? (float)screenFudge / screen.x : 0;
            float yFactor = constrainToViewPort ? (float)screenFudge / screen.y : 0;
            // the screen space point of the target
            var p = Camera.main.WorldToViewportPoint(Target.BubblePosition.position);

            // if the target is outside of the screen
            if ((p.x < 0 + xFactor || p.x > 1 - xFactor) || (p.y < 0 + yFactor || p.y > 1 - yFactor))
            {
                // we convert the screen clamped point into world point and return it
                p.x = Mathf.Clamp(p.x, 0 + xFactor, 1 - xFactor);
                p.y = Mathf.Clamp(p.y, 0 + yFactor, 1 - yFactor);
                var np = Camera.main.ViewportToWorldPoint(p);

                return (true, np);
            }

            // the target is within the screen, we return false and it's current position
            return (false, Target.BubblePosition.position);
        }

        /// <summary>
        /// Converts a world space point into a rectangle-specific space point.
        /// </summary>
        /// <remarks>
        /// This allows you to get a world position (which is easy) and get it into the same coordinate system and scale as the <paramref name="rect"/>.
        /// Handles all conversions and necessary scaling.
        /// Works for both Screen Space - Overlay and Screen Space - Camera UI.
        /// Does not constrain itself to the view port.
        /// </remarks>
        /// <param name="worldPosition">World position you want converted</param>
        /// <param name="rect">The rectangle holding the element you want to position</param>
        /// <param name="camera">The camera for the UI</param>
        /// <param name="canvas">The canvas holding the rectangle</param>
        /// <returns>The same worldPosition point but now in the rectangle space</returns>
        public Vector2 WorldToRectangleSpace(Vector3 worldPosition, RectTransform rect, Camera? camera, Canvas canvas)
        {
            if (camera == null)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    camera = Camera.main;
                }
                else
                {
                    camera = canvas.worldCamera;
                }
                if (camera == null)
                {
                    throw new NullReferenceException($"Can't calculate position of bubble {name}: no camera is the main camera, and the canvas doesn't have a {nameof(canvas.worldCamera)}");
                }
            }

            // Converting the world point to a screen point (not the same as a local point...)
            Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
            screenPoint.z = 0;

            // overlay canvas have their own quirks, the practical upshot of it we need to remove the camera from the calculation
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                camera = null;
            }

            // finally we can convert the screen point into a local point in rectangle space
            // now it is all within the scale and coordinate system of the rectangle
            Vector2 rectPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, camera, out rectPos);

            // returning our rectangle specific point
            return rectPos;
        }

        /// <summary>
        /// Converts a world position into a screen constrained and offset rectangle space coordinate.
        /// </summary>
        /// <remarks>
        /// Is essentially a variant on <see cref="WorldToRectangleSpace"/> that handles the various
        /// screen edge and bubble alignment issues.
        /// </remarks>
        /// <param name="worldPos">World position you want converted</param>
        /// <param name="parentRect">The RectTransform holding the bubble</param>
        /// <param name="screenSize">The size of the screen</param>
        /// <param name="constrainToViewportMargin">How much the </param>
        /// <returns>The screen constrained and adjusted rectangle space position</returns>
        private Vector3 BubbleTargetWorldPositionToRectangleSpace(Vector3 worldPos, RectTransform parentRect, Vector2 screenSize, float constrainToViewportMargin, Canvas canvas)
        {
            if (bubbleContainer == null)
            {
                // No bubble container, so we can't calculate its position.
                return Vector3.zero;
            }

            // calculate the unconstrained position first
            var rectPos = WorldToRectangleSpace(worldPos, parentRect, Camera.main, canvas);

            // adding in the centre offset
            // this will allow you to offset and shift the apparent position of the rectangle
            rectPos += CentreOffset;

            // now we shift the position by the stem so that the bubble floats "above" the stem
            // left top right bottom
            Vector2[] movementDirection = new Vector2[4]
            {
                new Vector2(-1,0),
                new Vector2(0,1),
                new Vector2(1,0),
                new Vector2(0,-1)
            };

            // move the box out of the way
            Vector2 direction = movementDirection[(int)positionRelativeToAnchor];
            rectPos += direction * (bubbleContainer.rect.size / 2) + (stemLength * direction);

            // are we ok with going off the edge?
            if (!constrainToViewPort)
            {
                // if so just return the position
                return rectPos;
            }

            // calculate "half" values because we are measuring margins based on the center, like a radius
            var halfBubbleWidth = bubbleContainer.rect.width / 2;
            var halfBubbleHeight = bubbleContainer.rect.height / 2;

            // to calculate margin in UI-space pixels, use a % of the smaller screen dimension
            var margin = screenSize.x < screenSize.y ? screenSize.x * constrainToViewportMargin : screenSize.y * constrainToViewportMargin;

            // finally, clamp the position fully within the screen bounds, while accounting for the bubble's anchors
            rectPos.x = Mathf.Clamp(
                rectPos.x,
                margin + halfBubbleWidth - bubbleContainer.anchorMin.x * screenSize.x,
                -(margin + halfBubbleWidth) - bubbleContainer.anchorMax.x * screenSize.x + screenSize.x
            );
            rectPos.y = Mathf.Clamp(
                rectPos.y,
                margin + halfBubbleHeight - bubbleContainer.anchorMin.y * screenSize.y,
                -(margin + halfBubbleHeight) - bubbleContainer.anchorMax.y * screenSize.y + screenSize.y
            );

            // aaaaaaand we can now return the position
            return rectPos;
        }

        DrivenRectTransformTracker tracker;
        private void OnEnable()
        {
            tracker.Clear();

            const DrivenTransformProperties drivenTransformProperties =
                DrivenTransformProperties.AnchoredPosition |
                DrivenTransformProperties.Pivot |
                DrivenTransformProperties.SizeDelta |
                DrivenTransformProperties.Anchors |
                DrivenTransformProperties.Scale;

            tracker.Add(this, bubbleContainer, drivenTransformProperties);

            if (stem != null)
            {
                tracker.Add(this, stem.rectTransform, drivenTransformProperties);
            }

            if (TryGetComponent<RectTransform>(out var rect))
            {
                tracker.Add(this, rect, DrivenTransformProperties.All);
                rect.localPosition = Vector3.zero;
                rect.anchoredPosition = Vector2.zero;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = 0.5f * Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localEulerAngles = Vector3.zero;
            }

            if (UsePreviewData)
            {
                UpdateBubbleImmediate(this.State.content);
                this.PresentationFactor = 1;
            }

        }

        private void OnDisable()
        {
            tracker.Clear();
        }

        internal async YarnTask PresentBubble(BubbleContent content, float duration, System.Threading.CancellationToken cancellationToken)
        {
            if (this.bubbleContainer == null)
            {
                Debug.LogError($"Can't present bubble, because {nameof(bubbleContainer)} is null!", this);
                return;
            }
            if (this.content == null)
            {
                Debug.LogError($"Can't present bubble, because {nameof(Bubble.content)} is null!", this);
                return;
            }

            this.State.content = content;

            this.content.PrepareForContent(content);

            // Calculate the size of the content, and update the bubble's
            // dimensions accordingly.
            var size = this.content.GetContentSize(content);
            this.bubbleContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            this.bubbleContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

            float accumulator;

            accumulator = 0;
            while (accumulator < duration && !cancellationToken.IsCancellationRequested)
            {
                accumulator += Time.deltaTime;

                float t = accumulator / duration;
                this.PresentationFactor = t;
                await YarnTask.Yield();
            }
            this.PresentationFactor = 1;

            if (this.content != null)
            {
                await this.content.PresentContent(content, cancellationToken);
            }
        }

        internal async YarnTask DismissBubble(float duration, System.Threading.CancellationToken token)
        {
            this.State.content = BubbleContent.None;

            if (this.content != null)
            {
                await this.content.DismissContent(token);
            }

            float accumulator = 0;

            while (accumulator < duration && token.IsCancellationRequested == false)
            {
                float hideValue = 1f - (accumulator / duration);
                this.PresentationFactor = hideValue;
                accumulator += Time.deltaTime;
                await YarnTask.Yield();
            }

            this.PresentationFactor = 0;
        }

        internal async YarnTask UpdateBubble(BubbleContent content, float duration, System.Threading.CancellationToken token)
        {
            if (this.bubbleContainer == null)
            {
                Debug.LogError($"Can't update bubble, because {nameof(bubbleContainer)} is null!", this);
                return;
            }

            if (this.content == null)
            {
                Debug.LogError($"Can't update bubble, because {nameof(content)} is null!", this);
                return;
            }

            this.State.content = content;

            // Dismiss any content already present
            await this.content.DismissContent(token);

            this.content.PrepareForContent(content);

            // Remember our current size, and calculate the new size we want to
            // change to
            var startSize = bubbleContainer.rect.size;
            var endSize = this.content.GetContentSize(content);

            float accumulator = 0;

            // Animate from our current size to our new size
            while (accumulator < duration && token.IsCancellationRequested == false)
            {
                // we just lerp between the start and end
                float value = accumulator / duration;
                var width = Mathf.Lerp(startSize.x, endSize.x, value);
                var height = Mathf.Lerp(startSize.y, endSize.y, value);

                // and use the lerp value to change the anchors
                bubbleContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                bubbleContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

                accumulator += Time.deltaTime;

                await YarnTask.Yield();
            }
            bubbleContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, endSize.x);
            bubbleContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, endSize.y);

            // Now that we've finished updating our size, present the new content
            await this.content.PresentContent(content, token);
        }

        internal void UpdateBubbleImmediate(BubbleContent content)
        {
            if (this.bubbleContainer == null)
            {
                Debug.LogError($"Can't update bubble, because {nameof(bubbleContainer)} is null!", this);
                return;
            }

            if (this.content == null)
            {
                Debug.LogError($"Can't update bubble, because {nameof(content)} is null!", this);
                return;
            }

            var endSize = this.content.GetContentSize(content);
            bubbleContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, endSize.x);
            bubbleContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, endSize.y);

            this.content.SetContentWithoutAnimation(content);

        }
    }
}
