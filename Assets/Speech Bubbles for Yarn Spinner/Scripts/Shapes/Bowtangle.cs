namespace Yarn.Unity.Addons.SpeechBubbles
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;

#nullable enable

    [RequireComponent(typeof(CanvasRenderer))]
    /// <summary>
    /// A graphic that is a rectangle with bowed out edges.
    /// </summary>
    /// <remarks>
    /// The inspiration for this shape is the speech balloons in Night in the Woods
    /// </remarks>
    public class Bowtangle : Graphic
    {
        /// <summary>
        /// The number of edges used in the horizontal bowed out region.
        /// </summary>
        /// <remarks>
        /// The greater the number the smoother the circle segment looks.
        /// Can be set very low to create some very unusual looking shapes.
        /// We find that 20 is a decent default for a smoothish looking segment.
        /// </remarks>
        [SerializeField] private int horizontalEdgesDensity = 20;
        /// <summary>
        /// The number of edges used in the vertical bowed out region.
        /// </summary>
        /// <remarks>
        /// The greater the number the smoother the circle segment looks.
        /// Can be set very low to create some very unusual looking shapes.
        /// We find that 20 is a decent default for a smoothish looking segment.
        /// </remarks>
        [SerializeField] private int verticalEdgesDensity = 20;

        /// <summary>
        /// The percentage of the horizontal edge as a chord relative to the diameter.
        /// </summary>
        /// <remarks>
        /// The bowtangle works by assuming the edge is a chord and the bowed out part is the circle segment of that chord.
        /// This value controls how long this chord-edge is as a percentage of the diameter.
        /// That is to say that setting this to 100% is the same as saying the edge is the diameter of the circle
        /// resulting in a semi-circle being drawn.
        /// We use a percentage here so that as the rectangle is changed the relative amount of bowing looks the same.
        /// </remarks>
        [Range(-100f, 100)]
        [SerializeField] private float horizontalChordPercentage = 100f;
        /// <summary>
        /// The percentage of the vertical edge as a chord relative to the diameter.
        /// </summary>
        /// <remarks>
        /// The bowtangle works by assuming the edge is a chord and the bowed out part is the circle segment of that chord.
        /// This value controls how long this chord-edge is as a percentage of the diameter.
        /// That is to say that setting this to 100% is the same as saying the edge is the diameter of the circle
        /// resulting in a semi-circle being drawn.
        /// We use a percentage here so that as the rectangle is changed the relative amount of bowing looks the same.
        /// </remarks>
        [Range(-100f, 100)]
        [SerializeField] private float verticalChordPercentage = 100f;

        /// <summary>
        /// Controls if the bowtangle will lightly deform, or wibble.
        /// </summary>
        /// <remarks>
        /// This exists to give the bubble a little bit of life when otherwise text is just being shown.
        /// </remarks>
        [SerializeField] private bool wibble = true;
        /// <summary>
        /// The frequency in hertz (cycles per second), that the bowtangle will wibble at.
        /// </summary>
        /// <remarks>
        /// Higher numbers will make it deform more often.
        /// Represents ω in the <c>A * sin(ωt + φ)</c> formula used to deform the bowtangle.
        /// </remarks>
        [SerializeField] private float wibbleFrequency = 1f;
        /// <summary>
        /// The amplitude in canvas positional units (akin to pixels or points) that the bowtangle will deform by.
        /// </summary>
        /// <remarks>
        /// Higher numbers will make the deformation distance more extreme in both highs and lows.
        /// Represents A in the <c>A * sin(ωt + φ)</c> formula used to deform the bowtangle.
        /// </remarks>
        [SerializeField] private float wibbleAmplitude = 2.5f;
        /// <summary>
        /// The phase in canvas position units (akin to pixels of points) that the bowtangle will be shifted by.
        /// </summary>
        /// <remarks>
        /// Higher numbers will result in more peaks and trough along the edge of the bowtangle.
        /// Cranking this and <see cref="wibbleAmplitude"/> high can result in spiky looking shapes.
        /// Setting this to 0 creates a rather unsettling breathing appearance across the bowtangle, highly recommend.
        /// Represents φ in the <c>A * sin(ωt + φ)</c> formula used to deform the bowtangle.
        /// </remarks>
        [SerializeField] private int wibblePhase = 1;
        /// <summary>
        /// The framerate (in deformations per second) at which the wibbling effect will occur.
        /// </summary>
        /// <remarks>
        /// Will have no effect if <see cref="unconstrainedWibbleTiming"/> is set to true.
        /// </remarks>
        [SerializeField] private int wibbleFramerate = 8;
        /// <summary>
        /// Controls if the framerate of the wibble effect will be unconstrained or not.
        /// </summary>
        /// <remarks>
        /// If set to true the value in <see cref="wibbleFramerate"/> has no effect and the 
        /// wibble effect will run at the frame rate of the game.
        /// </remarks>
        [SerializeField] private bool unconstrainedWibbleTiming = true;

        private float accumulator = 0f;
        private Coroutine? constrainedWibble;

        void Update()
        {
            if (wibble)
            {
                if (unconstrainedWibbleTiming)
                {
                    if (constrainedWibble != null)
                    {
                        StopCoroutine(constrainedWibble);
                        constrainedWibble = null;
                    }
                    accumulator += Time.deltaTime;
                    SetAllDirty();
                }
                else
                {
                    if (constrainedWibble == null)
                    {
                        constrainedWibble = StartCoroutine(DoWibble());
                    }
                }
            }
        }

        /// <summary>
        /// The coroutine used to distort the bowtangle
        /// </summary>
        /// <returns>The coroutine that does the wibbling</returns>
        private IEnumerator DoWibble()
        {
            while (true)
            {
                if (wibbleFramerate == 0)
                {
                    continue;
                }
                accumulator += 1f / wibbleFramerate;
                SetAllDirty();
                yield return new WaitForSeconds(1f / wibbleFramerate);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            vh.Clear();

            var width = rectTransform.rect.width;
            var height = rectTransform.rect.height;

            if (width == 0 || height == 0)
            {
                // Prevent a division-by-zero if we have no area.
                return;
            }

            // adding in the centre of the shape
            // which is the centre of the rect
            var vert = UIVertex.simpleVert;
            vert.position = rectTransform.rect.center;
            vert.color = color;
            vh.AddVert(vert);

            // Avoid a divide-by-zero when horizontal or vertical chord percentage is 0
            float horizontalChord = (horizontalChordPercentage != 0) ? horizontalChordPercentage / 100f : 0.0001f;
            float verticalChord = (verticalChordPercentage != 0) ? verticalChordPercentage / 100f : 0.0001f;

            // the radius of the circle segment used to bow out the edge
            var horizontalRadius = width / horizontalChord / 2f;
            // the angle of the chord that the edge represents on the circle with the above radius
            var horizontalTheta = 2f * Mathf.Asin(width / (2 * horizontalRadius));
            // the "height" of the chord from the centre of the circle with the above radius
            var horizontalApothem = 0.5f * width * (1f / Mathf.Tan(0.5f * horizontalTheta));

            // same as the above three (radius, angle, height) but for the vertical sides instead of the top and bottom
            var verticalRadius = height / verticalChord / 2f;
            var verticalTheta = 2f * Mathf.Asin(height / (2 * verticalRadius));
            var verticalApothem = 0.5f * height * (1f / Mathf.Tan(0.5f * verticalTheta));

            // bl tl tr br
            Vector3[] corners = new Vector3[4];
            rectTransform.GetLocalCorners(corners);

            // working out the circle "centres" we use to draw the segment
            var horizontalCentre = (corners[1].x + corners[2].x) / 2;
            var verticalCentre = (corners[0].y + corners[1].y) / 2;
            // top left bottom right
            var centres = new Vector3[4] {
            new Vector3(horizontalCentre, corners[2].y - horizontalApothem, 0),
            new Vector3(corners[0].x + verticalApothem, verticalCentre, 0),
            new Vector3(horizontalCentre, corners[0].y + horizontalApothem),
            new Vector3(corners[2].x - verticalApothem, verticalCentre, 0),
        };

            // just so that we can have every corner in the same order as how we go around the circle
            // tr tl bl br
            var cs = new Vector3[4] {
            corners[2],
            corners[1],
            corners[0],
            corners[3],
        };

            var vertices = new List<Vector2>();

            // sweep out the various circle segments
            // starting with top and going counter clockwise
            // we do it like this so that we can more easily see where we are
            // and can turn pieces on/off during testing
            // could make this one big loop if we want
            for (int i = 0; i < 4; i++)
            {
                var centre = centres[i];

                var radius = i % 2 == 0 ? horizontalRadius : verticalRadius;

                var density = i % 2 == 0 ? horizontalEdgesDensity : verticalEdgesDensity;

                var theta = i % 2 == 0 ? horizontalTheta : verticalTheta;
                var sweepAngle = theta / density;

                // this accounts for us moving around the "circle"
                var offsetAngle = (Mathf.Deg2Rad * 90f * (i + 1)) - theta / 2f;

                // we are adding in the starting corner for this circle segment
                // starting in the top-right and swinging around counter-clockwise
                Vector2 p = new Vector2(cs[i].x, cs[i].y);
                vertices.Add(p);

                // sweeping around the points of the circle segment OTHER than the corners
                // we add the corners individually above
                for (int j = 1; j < density; j++)
                {
                    var x = centre.x + radius * Mathf.Cos(sweepAngle * j + offsetAngle);
                    var y = centre.y + radius * Mathf.Sin(sweepAngle * j + offsetAngle);

                    vertices.Add(new Vector2(x, y));
                }
            }

            // adding every vertex into the vertex helper
            // and offsetting them as needed by the wibble settings
            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];

                // if we are set to wibble we need to offset the position of each vertex a bit
                // here is where you change how you calculate the offset if you want a different effect
                if (wibble)
                {
                    // here we are offsetting vertex along the direction to the centre
                    // this means each one will move in and out along this line
                    var direction = (vertex - rectTransform.rect.center).normalized;

                    // the offset value calculated from a sine wave
                    // using the phase to move each along a little bit relative to their neighbours
                    // but each vertex phase is STILL within a cycle of the wave
                    // this prevents weird mismatched edges between the first and last vertex
                    var frequency = wibbleFrequency * Mathf.PI * 2;
                    var phase = Mathf.Lerp(0, 2 * Mathf.PI, ((float)i / vertices.Count));

                    // this is essentially the same as the standard A * sin(ω * t + φ) sine wave formula
                    // where A is the wibbleAmplitude
                    // ω is frequency
                    // accumulator is t
                    // and the combination of phase * wibblePhase is φ
                    // and then we move along the direction vector by the result of the above
                    vertex += direction * wibbleAmplitude * Mathf.Sin(frequency * accumulator + phase * wibblePhase);
                }

                vert.position = vertex;
                vert.color = color;
                vh.AddVert(vert);
            }

            // drawing in the triangles
            // we draw the shape out as a fan of triangles
            // each triangle starts in the center
            int k = 2 * horizontalEdgesDensity + 2 * verticalEdgesDensity;
            for (int i = 0; i < k; i++)
            {
                // 0, 2, 1
                // 0, 3, 2
                // etc etc
                var a = (i + 1) % k + 1;
                var b = i + 1;
                vh.AddTriangle(0, a, b);
            }
        }
    }
}