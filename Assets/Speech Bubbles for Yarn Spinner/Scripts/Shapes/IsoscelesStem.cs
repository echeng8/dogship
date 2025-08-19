namespace Yarn.Unity.Addons.SpeechBubbles
{
    using UnityEngine;
    using UnityEngine.UI;

#nullable enable

    /// <summary>
    /// This class draws the small callout stem that is drawn coming out of one edge of the speech bubble.
    /// </summary>
    /// <remarks>
    /// Will draw as a small isosceles triangle (although sometimes it will be an equilateral), center aligned along the edge.
    /// This class will also handle the situations of correctly drawing the stem when close to an edge
    /// as well as hiding and showing the edge as needed.
    /// </remarks>
    [RequireComponent(typeof(CanvasRenderer))]
    public class IsoscelesStem : Graphic
    {
        /// <summary>
        /// The different states the stem can be in.
        /// </summary>
        /// <remarks>
        /// Most of the time it will be <see cref="Shown"/> or <see cref="Hidden"/>.
        /// </remarks>
        enum StemState
        {
            /// <summary>
            /// stem is fully drawn and at it's maximum size.
            /// </summary>
            Shown,
            /// <summary>
            /// stem is transitioning to it's fully <see cref="Shown"/> state from <see cref="Hidden"/>.
            /// </summary>
            Growing,
            /// <summary>
            /// stem is transitioning from it's fully <see cref="Shown"/> state to <see cref="Hidden"/>
            /// </summary>
            Shrinking,
            /// <summary>
            /// stem is fully hidden and won't be visible.
            /// </summary>
            Hidden,
        }

        /// <summary>
        /// The state of the stem, only exists so that we know when to animate the stem growing and shrinking.
        /// </summary>
        private StemState state = StemState.Shown;

        /// <summary>
        /// Creates a factor by how we hide and show the stem.
        /// </summary>
        /// <remarks>
        /// At zero is fully hidden, at one fully shown.
        /// We need this to be able to blend between the literal edge case of when showing or hiding the stem
        /// but also are at the very edge of the screen.
        /// </remarks>
        private float hideDistance = 1;

        /// <summary>
        /// How long (in seconds) does the stem take to transition between hiding and showing?
        /// </summary>
        float hideShowDuration = 0.25f;

        /// <summary>
        /// a time counter used during animations.
        /// </summary>
        private float offscreenAccumulator = 0;

        /// <summary>
        /// The position of the target the stem needs to reach towards.
        /// This is given to the stem by the <see cref="Bubble"/>.
        /// </summary>
        public Vector3 Target { get; set; }

        /// <summary>
        /// the width of the stem, in canvas units
        /// </summary>
        public int width = 5;

        /// <summary>
        /// The edge the stem needs to come out of.
        /// </summary>
        /// <remarks>
        /// Is given to the stem by the <see cref="Bubble"/>.
        /// </remarks>
        public EdgePositioning edgeFollow = EdgePositioning.Below;

        /// <summary>
        /// Represents the presentation factor for animation purposes - that is,
        /// how "much" has the bubble appeared?
        /// </summary>
        /// <remarks>
        /// Will be set by the <see cref="Bubble"/> during showing and hiding
        /// animations. 
        /// </remarks>
        [Range(0f, 1f)]
        public float pointInTime = 0f;

        /// <summary>
        /// An animation curve used to adjust the presentation of the stem while hiding and showing.
        /// </summary>
        /// <remarks>
        /// The values for this are used to drive the distance of the stem as it heads towards or away from the <see cref="Target"/> position.
        /// It's very important that no matter the curve shape that <c>curve.Evaluate(0) == 0</c> and <c>curve.Evaluate(1) == 1</c> be true.
        /// This is because it's used to control the relative positioning of the target distance during animation, not the animation itself.
        /// </remarks>
        [SerializeField] private AnimationCurve curve = AnimationCurve.Constant(0, 1, 1);

        /// <summary>
        /// The <see cref="Bubble"/> that is the parent for this stem.
        /// </summary>
        /// <remarks>
        /// Used to get access to tracking dependant positional information
        /// which upon reflection should just be set direction on <see cref="Target"/> instead.
        /// </remarks>
        private Bubble? parent;

        void Update()
        {
            SetAllDirty();
        }

        protected override void Start()
        {
            base.Start();
            parent = GetComponentInParent<Bubble>();
        }
        protected override void OnEnable()
        {
            base.OnEnable();

            SetAllDirty();
            UpdateGeometry();
        }

        /// <summary>
        /// Hides the stem.
        /// </summary>
        /// <remarks>
        /// This animates the stem away from the <see cref="Target"/> towards the rectangle center.
        /// Is called by the <see cref="Bubble"/> when the target is off the screen edge.
        /// </remarks>
        public void Hide()
        {
            switch (state)
            {
                case StemState.Shown:
                    {
                        // we are fully shown and asked to hide
                        // so we start shrinking
                        state = StemState.Shrinking;
                        offscreenAccumulator = 0;
                        break;
                    }
                case StemState.Growing:
                    {
                        // we were expanded but then the target went off screen
                        // need to start shrinking
                        state = StemState.Shrinking;
                        offscreenAccumulator = 0;
                        break;
                    }
                case StemState.Hidden:
                    {
                        // we are already hidden, no need to do anything
                        break;
                    }
                case StemState.Shrinking:
                    {
                        // we are already in the process of shrinking
                        // do nothing
                        break;
                    }
            }
        }

        /// <summary>
        /// Shows the stem.
        /// </summary>
        /// <remarks>
        /// This animates the stem towards the target from the rectangle center.
        /// Is called by the <see cref="Bubble"/> when the target moves back into frame from off-screen.
        /// </remarks>
        public void Show()
        {
            switch (state)
            {
                // we are already expanding/expanded
                // so do nothing
                case StemState.Growing:
                    {
                        break;
                    }
                case StemState.Shown:
                    {
                        break;
                    }

                // we are hidden or in the process of hiding
                // need to change state
                case StemState.Hidden:
                    {
                        offscreenAccumulator = 0;
                        state = StemState.Growing;
                        break;
                    }
                case StemState.Shrinking:
                    {
                        state = StemState.Growing;
                        offscreenAccumulator = 0;
                        break;
                    }
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            vh.Clear();

            var rect = GetComponent<RectTransform>().rect;

            // the triangle is drawn literally as two points along the centre parallel to the edge being followed
            // and then a third point at the target
            Vector2 centreA;
            Vector2 centreB;

            // determining the positioning of the two centre aligned points
            // these change depnding on which edge is being followed
            switch (edgeFollow)
            {
                case EdgePositioning.Left:
                    {
                        centreA = new Vector2(rect.center.x - rect.width / 4, rect.center.y - width);
                        centreB = new Vector2(rect.center.x - rect.width / 4, rect.center.y + width);
                        break;
                    }
                case EdgePositioning.Right:
                    {
                        centreA = new Vector2(rect.center.x + rect.width / 4, rect.center.y - width);
                        centreB = new Vector2(rect.center.x + rect.width / 4, rect.center.y + width);
                        break;
                    }
                case EdgePositioning.Below:
                    {
                        centreA = new Vector2(rect.center.x - width, rect.center.y + rect.height / 4);
                        centreB = new Vector2(rect.center.x + width, rect.center.y + rect.height / 4);
                        break;
                    }
                default:
                    {
                        centreA = new Vector2(rect.center.x - width, rect.center.y - rect.height / 4);
                        centreB = new Vector2(rect.center.x + width, rect.center.y - rect.height / 4);
                        break;
                    }
            }

            // adding the two centre aligned points
            UIVertex vert = UIVertex.simpleVert;
            vert.position = centreA;
            vert.color = color;
            vh.AddVert(vert);

            vert.position = centreB;
            vert.color = color;
            vh.AddVert(vert);

            // the final point in the triangle, essentially the target, changes its position depending on the state of the stem
            // there are two virtual positions which represent the stem being fully hidden or fully shown
            // these are then blended through the animation curve to give us a distance factor of how close the stem based on its state should be to the target
            // this is then used to lerp towards/away from the target position
            // this is weird but handles the edge cases of needing to animate in/out based on being presented as well as when the target has moved in/out of frame

            // determining the hideDistance value
            // for hidden will be 0, for shown 1
            // for growning and shrinking will be somewhere in-between based on time
            switch (state)
            {
                case StemState.Hidden:
                    {
                        hideDistance = 0;
                        break;
                    }
                case StemState.Shown:
                    {
                        hideDistance = 1;
                        break;
                    }
                case StemState.Growing:
                    {
                        offscreenAccumulator += Time.deltaTime;

                        if (offscreenAccumulator >= hideShowDuration)
                        {
                            state = StemState.Shown;
                            hideDistance = 1;
                        }
                        else
                        {
                            hideDistance = Mathf.Lerp(0, 1, offscreenAccumulator / hideShowDuration);
                        }
                        break;
                    }
                case StemState.Shrinking:
                    {
                        offscreenAccumulator += Time.deltaTime;

                        if (offscreenAccumulator >= hideShowDuration)
                        {
                            state = StemState.Hidden;
                            hideDistance = 0;
                        }
                        else
                        {
                            hideDistance = Mathf.Lerp(1, 0, offscreenAccumulator / hideShowDuration);
                        }
                        break;
                    }
            }

            // the hideDistance is then used to determine the distanceFactor
            // this is the relative distance the final triangle point should be to the target position.
            // This relies on two elements, the hideDistance calculated above
            // and the value of the pointInTime as evaluated through the animation curve
            // we want the minumum of these as it covers the main extremes:
            // 1: the target is off-screen but the bubble is presenting so wants to animate out the stem
            // 2: the target is on-screen but the bubble is leaving so wants to animate inwards the stem
            // so in the first case the hideDistance will prevent the stem animating out
            // and in the second the curve value will prevent the stem from following the target
            var distanceFactor = Mathf.Min(curve.Evaluate(pointInTime), hideDistance);

            // If distance factor is very small, and if the containing rect
            // transform has been scaled to a small but non-zero value, then the
            // conversion from world space to rectangle space can encounter
            // precision errors. Clamp distanceFactor to zero to avoid these
            // problems.
            if (distanceFactor < 0.02)
            {
                distanceFactor = 0;
            }

            // converting the the target point into a canvas point
            var endPoint = Target;
            // moving the endpoint out along the line between the rectangle centre and the target based on the above calculated distance factor
            endPoint = Vector3.Lerp(rect.center, endPoint, distanceFactor);

            // now that we *finally* have the final triangle point we can add that vertex
            vert.position = endPoint;
            vert.color = color;
            vh.AddVert(vert);

            // drawing the triangle
            vh.AddTriangle(0, 1, 2);
        }

        internal void UpdatePresentationFactor(float factor)
        {
            this.pointInTime = factor;
            this.SetAllDirty();
        }
    }
}
