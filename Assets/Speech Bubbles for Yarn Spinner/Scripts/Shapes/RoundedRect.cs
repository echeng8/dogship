namespace Yarn.Unity.Addons.SpeechBubbles
{
    using System.Linq;
    using UnityEngine;
    using UnityEngine.UI;
    using Yarn.Unity.Attributes;

    [RequireComponent(typeof(CanvasRenderer))]
    /// <summary>
    /// A graphic that is a rectangle with rounded corners.
    /// </summary>
    /// <remarks>
    /// The inspiration for this shape is the speech balloons in A Short Hike.
    /// Note this is not a squircle.
    /// </remarks>
    public class RoundedRect : Graphic
    {
        [System.Serializable]
        public struct Corner
        {
            [Min(0)]
            public float radius;
            public bool clamp;

            [Min(1)]
            public int density;
        }
        [System.Serializable]
        public struct Corners
        {
            public Corner topLeft;
            public Corner topRight;
            public Corner bottomLeft;
            public Corner bottomRight;
        }

        [Space]

        /// <summary>
        /// Setting this to true allows for each corner to have their own radius and density.
        /// </summary>
        /// <remarks>
        /// This ignores the values of <see cref="cornerDensity"/> and <see cref="cornerRadius"/>.
        /// Generally you will want this set to false but using this can create some fascinating shapes.
        /// </remarks>
        [SerializeField] private bool UseIndividualCorners = false;

        /// <summary>
        /// The shared corner radius to be used for each corner.
        /// </summary>
        /// <remarks>
        /// This is an inset-radius, so each corner square will end up being slightly smaller
        /// by approximately ~5.365% smaller area for each corner.
        /// It is unlikely this loss will be noticeable unless you set this very high relative to the size of the rectangle.
        /// </remarks>
        [HideIf(nameof(UseIndividualCorners))]
        [Min(0)]
        [SerializeField] private float cornerRadius = 10f;
        /// <summary>
        /// The shared corner density to be used for each corner.
        /// </summary>
        /// <remarks>
        /// This represents how many vertices each corner will have.
        /// Setting this number higher creates smoother looking circles.
        /// </remarks>

        [HideIf(nameof(UseIndividualCorners))]
        [Min(1)]
        [SerializeField] private int cornerDensity = 10;

        [HideIf(nameof(UseIndividualCorners))]
        [SerializeField] private bool clampOverlap = true;

        /// <summary>
        /// Holds the settings for each of the individual corners.
        /// </summary>
        /// Is only used if <see cref="UseIndividualCorners"/> is set to true.
        /// </remarks>
        [ShowIf(nameof(UseIndividualCorners))]
        [SerializeField] private Corners individualCorners;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            vh.Clear();

            // adding the centre point in
            var vert = UIVertex.simpleVert;
            vert.position = rectTransform.rect.center;
            vert.color = color;
            vh.AddVert(vert);

            // if we are using individual corner radii and density we need to set these values here
            // otherwise we need every corner to have the same radius and density
            if (!UseIndividualCorners)
            {

                individualCorners.topLeft.radius = cornerRadius;
                individualCorners.topRight.radius = cornerRadius;
                individualCorners.bottomLeft.radius = cornerRadius;
                individualCorners.bottomRight.radius = cornerRadius;

                individualCorners.topLeft.clamp = clampOverlap;
                individualCorners.topRight.clamp = clampOverlap;
                individualCorners.bottomLeft.clamp = clampOverlap;
                individualCorners.bottomRight.clamp = clampOverlap;

                individualCorners.topLeft.density = cornerDensity;
                individualCorners.topRight.density = cornerDensity;
                individualCorners.bottomLeft.density = cornerDensity;
                individualCorners.bottomRight.density = cornerDensity;
            }

            Corner[] cornerData = new[] {
                individualCorners.topRight,
                individualCorners.topLeft,
                individualCorners.bottomLeft,
                individualCorners.bottomRight,
            };

            for (var i = 0; i < cornerData.Length; i++)
            {

                if (cornerData[i].clamp)
                {
                    // If the radius is too large for this rectangle and we're
                    // set to clamp, then clamp it
                    var size = this.rectTransform.rect.size;
                    var minDimension = Mathf.Min(size.x, size.y);

                    cornerData[i].radius = Mathf.Min(cornerData[i].radius, minDimension / 2);
                }
            }

            // getting the corners and centre radii for each rounded rect
            // we do this by moving each corner inwards by their radius so we can use it sweep out an arc
            Vector3[] corners = new Vector3[4];
            rectTransform.GetLocalCorners(corners);
            var insetCorners = new Vector2[4] {
                new Vector2(corners[2].x - cornerData[0].radius, corners[2].y - cornerData[0].radius),
                new Vector2(corners[1].x + cornerData[1].radius, corners[1].y - cornerData[1].radius),
                new Vector2(corners[0].x + cornerData[2].radius, corners[0].y + cornerData[2].radius),
                new Vector2(corners[3].x - cornerData[3].radius, corners[3].y + cornerData[3].radius),
            };

            // we draw this out by each corner
            // we could do this in one go but this allows us to turn on and off pieces as needed
            // just keeps the code simpler overall
            for (int i = 0; i < 4; i++)
            {
                var points = cornerData[i].density + 1;
                var sweepAngle = Mathf.Deg2Rad * 90f / cornerData[i].density;

                // this accounts for us moving around the "circle"
                var offsetAngle = Mathf.Deg2Rad * 90f * i;

                var radius = cornerData[i].radius;

                var centre = insetCorners[i];

                // there is no corner density set
                // we just add the corner itself and move onto the next corner
                if (points == 1)
                {
                    vert.position = centre;
                    vert.color = color;
                    vh.AddVert(vert);

                    continue;
                }

                // adding the circle arc points for this corner
                for (int j = 0; j < points; j++)
                {
                    var x = centre.x + radius * Mathf.Cos(sweepAngle * j + offsetAngle);
                    var y = centre.y + radius * Mathf.Sin(sweepAngle * j + offsetAngle);

                    vert.position = new Vector3(x, y, 0);
                    vert.color = color;
                    vh.AddVert(vert);
                }
            }

            // the total number of vertices we drew out
            int cornerVertexCount = 4;
            for (int i = 0; i < 4; i += 1)
            {
                cornerVertexCount += cornerData[i].density;
            }

            // drawing in the triangles
            // we draw the shape out as a fan of triangles
            // each triangle starts in the center
            // this means the bulk of the triangles will be clumped in the corners
            // and each non-corner region will be drawn as one big triangle
            for (int i = 0; i < cornerVertexCount; i++)
            {
                // 0, 2, 1
                // 0, 3, 2
                // etc etc
                var a = (i + 1) % cornerVertexCount + 1;
                var b = i + 1;
                vh.AddTriangle(0, a, b);
            }
        }
    }
}

