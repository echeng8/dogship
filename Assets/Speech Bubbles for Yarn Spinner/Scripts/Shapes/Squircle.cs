namespace Yarn.Unity.Addons.SpeechBubbles
{
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof(CanvasRenderer))]
    /// <summary>
    /// A graphic that is a superellipse or squircle.
    /// </summary>
    /// <remarks>
    /// inspired by most articles that get mad at rounded rectangles being a bit of hack.
    /// This provides a fancy way to make various ellipsoid shapes, intended for squircles though.
    /// This allows you to blend between various ellipse shapes depending on the ellipse constant.
    /// </remarks>
    public class Squircle : Graphic
    {
        /// <summary>
        /// Represents the number of edges the squircle will have.
        /// </summary>
        /// <remarks>
        /// The higher this number the smoother the squircle will look.
        /// Very low values will create weird distorted polygons, which may be desirable.
        /// In general this will need to be higher than you'd think compared to a rounded rect
        /// </remarks>
        [SerializeField] private int Density = 48;

        /// <summary>
        /// Represent the ellipse constant which controls the distortion of the shape.
        /// </summary>
        /// <remarks>
        /// This refers to n, p, or s in most formula (cmon maths).
        /// Some common values for this:
        /// <list type="bullet">
        /// <item><description>2 will give you a regular ellipse/circle</description></item>
        /// <item><description>4 will give you the most common squircle</description></item>
        /// <item><description>infinity (or very large values) will give you a square/rectangle</description></item>
        /// </list>
        /// Values between 0 and 2 give very unusual inwards and outwards distorted parallelograms
        /// </remarks>
        [Header("Shape Control")]
        [SerializeField] private float shapeConstant = 4;

        /// <summary>
        /// Adjusts the imagined size of the rectangle the Squircle lives in.
        /// </summary>
        /// <remarks>
        /// Because the shape is just a weird circle, by default it won't properly fill the rectangle.
        /// So you can solve this via margins on the text field or by adjusting this value.
        /// This will make the rectangle the squircle thinks it needs to draw slightly bigger.
        /// Which makes the squircle larger than it should be, giving the text some space.
        /// <para>
        /// This is multiplied into the size of the rectangle, so a value of 0 means it will be the same size as the bubble rectangle.
        /// A value of 1 will mean the squircle will be drawing into an imagined rectangle twice as large as the bubbles.
        /// The default value of 0.15 was decided by us eyeballing the squircle in the editor until we liked it.
        /// Your idealised size factor will differ based on the needs of your game.
        /// </para>
        /// </remarks>
        [SerializeField][Range(0, 1)] private float sizeAdjustmentFactor = 0.15f;

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
            var majorRadius = rectTransform.rect.width / 2 * (1f + sizeAdjustmentFactor);
            var minorRadius = rectTransform.rect.height / 2 * (1f + sizeAdjustmentFactor);

            // how far do we have to sweep each step of the squircle?
            var sweepAngle = Mathf.Deg2Rad * 360f / Density;

            // convenience function that does value ^ power but keeps the sign of value
            // we do it like to to avoid having to worry about negative square roots
            // you can think of this as how distorted from a circle it is
            float SignedPower(float value, float exponent)
            {
                var pow = Mathf.Pow(Mathf.Abs(value), exponent);

                // why does this function not exist..?
                // return System.Math.CopySign(b, value);
                if (Mathf.Sign(pow) == Mathf.Sign(value))
                {
                    return pow;
                }
                else
                {
                    return pow * -1;
                }
            }

            // the distortion factor
            float power = 2f / shapeConstant;

            // drawing out each point along the squircle
            for (int i = 0; i < Density; i++)
            {
                var theta = sweepAngle * i;


                var x = centre.x + majorRadius * SignedPower(Mathf.Cos(theta), power);
                var y = centre.y + minorRadius * SignedPower(Mathf.Sin(theta), power);

                vert.position = new Vector2(x, y);
                vert.color = color;
                vh.AddVert(vert);
            }

            // adding in all the triangles
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