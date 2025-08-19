namespace Yarn.Unity.Addons.SpeechBubbles
{
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof(CanvasRenderer))]
    /// <summary>
    /// A graphic that is an oval or ellipse.
    /// </summary>
    /// <remarks>
    /// Inspiration for this shape is any number of comic books
    /// For ovals with a high eccentricity it will rapidly start looking weird,
    /// as such you probably only want to use this only when the semi-major and -minor axes aren't too far apart in length.
    /// </remarks>
    public class OvalBubble : Graphic
    {
        /// <summary>
        /// Represents the number of edges the oval will have.
        /// </summary>
        /// <remarks>
        /// The higher this number the smoother the oval will look.
        /// Very low values will create weird distorted polygons, which may be desirable.
        /// </remarks>
        [SerializeField] private int Density = 20;

        /// <summary>
        /// Represents the semi-major axis of the ellipse, this is the radius of the larger side of the rectangle.
        /// </summary>
        /// <remarks>
        /// We assume that this will be the width of the graphic rectangle.
        /// In most maths contexts this will be referred to as a.
        /// </remarks>
        float majorRadius;

        /// <summary>
        /// Represents the semi-minor axis of the ellipse, this is the radius of the smaller side of the rectangle.
        /// </summary>
        /// <remarks>
        /// We assume that this will be the height of the graphic rectangle.
        /// In most maths contexts this will be referred to as b.
        /// </remarks>
        float minorRadius;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            vh.Clear();

            // adding the centre point in
            var vert = UIVertex.simpleVert;
            var centre = rectTransform.rect.center;

            vert.position = centre;
            vert.color = color;
            vh.AddVert(vert);

            // calculating the radii of the semi-axes
            majorRadius = rectTransform.rect.width / Mathf.Sqrt(2);
            minorRadius = rectTransform.rect.height / Mathf.Sqrt(2);

            // how far do we have to sweep each step of the oval?
            var sweepAngle = Mathf.Deg2Rad * 360f / Density;

            // drawing out each point along the oval
            for (int i = 0; i < Density; i++)
            {
                var x = centre.x + majorRadius * Mathf.Cos(sweepAngle * i);
                var y = centre.y + minorRadius * Mathf.Sin(sweepAngle * i);

                vert.position = new Vector2(x, y);
                vert.color = color;
                vh.AddVert(vert);
            }

            // adding in all the triagles
            // we draw the shape out as a fan of triangles
            // each triangle starts in the center
            for (int i = 0; i < Density; i++)
            {
                // 0, 2, 1
                // 0, 3, 2
                // etc etc
                var a = (i + 1) % Density + 1;
                var b = i + 1;
                vh.AddTriangle(0, a, b);
            }
        }
    }
}
