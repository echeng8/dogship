using System.Collections;
using UnityEngine;

#nullable enable

namespace Yarn.Unity.Addons.SpeechBubbles
{
    /// <summary>
    /// Jitters the characters in a <see cref="TMPro.TMP_Text"/> component over
    /// time.
    /// </summary>
    [RequireComponent(typeof(TMPro.TMP_Text))]
    public class JitterText : MonoBehaviour
    {
        /// <summary>
        /// The number of times per second the text will jitter.
        /// </summary>
        /// <remarks>
        /// This value must be greater than zero.
        /// </remarks>
        [Min(0.001f)]
        [SerializeField] float jitterRate = 2;

        /// <summary>
        /// The distance, in local space, that each vertex will be offset by.
        /// </summary>
        [Min(0)]
        [SerializeField] float jitterAmount = 0.5f;

        /// <summary>
        /// The text object we're managing.
        /// </summary>
        TMPro.TMP_Text? text;

        public void OnEnable()
        {
            // Get a reference to our text.
            text = GetComponent<TMPro.TMP_Text?>();

            if (text == null)
            {
                Debug.LogError($"{nameof(JitterText)} can't start, because no text object was found!", this);
                return;
            }

            // Attach the pre-render call to the text object.
            text.OnPreRenderText += OnPreRenderText;

            // Start requesting text updates at the desired rate
            StartCoroutine(UpdateTextOverTime());
        }

        public void OnDisable()
        {
            if (text != null)
            {
                // Remove the pre-render call when we're on the way out.
                text.OnPreRenderText -= OnPreRenderText;
            }
        }

        /// <summary>
        /// Continuously updates <see cref="text"/>'s mesh at a fixed framerate.
        /// </summary>
        IEnumerator UpdateTextOverTime()
        {
            // Continuously request a text mesh rebuild at the requested number
            // of times per second.
            while (true)
            {
                if (jitterRate <= 0)
                {
                    throw new System.InvalidOperationException($"{nameof(jitterRate)} value is invalid ({jitterRate}; must be above zero)");
                }

                if (text != null)
                {
                    // Request a mesh rebuild.
                    text.ForceMeshUpdate();
                }

                // Wait until the next rebuild.            
                yield return new WaitForSeconds(1f / jitterRate);
            }
        }

        /// <summary>
        /// Calculates the fractional part of a number by subtracting its floor
        /// value from itself.
        /// </summary>
        /// <param name="x">The input number whose fractional part needs to be
        /// calculated.</param>
        /// <returns>The fractional part of <paramref name="x"/>.</returns>
        static float Frac(float x)
        {
            return x - Mathf.Floor(x);
        }

        /// <summary>
        /// Snaps a number to the nearest multiple of a given snap value.
        /// </summary>
        /// <param name="x">The number to be snapped.</param>
        /// <param name="snap">The value to which <paramref name="x"/> will be
        /// snapped.</param>
        /// <returns><paramref name="x"/>, snapped to <paramref
        /// name="snap"/>.</returns>
        static float Snap(float x, float snap)
        {
            return snap * Mathf.Round(x / snap);
        }

        /// <summary>
        /// Generates a pseudo-random <see cref="Vector2"/> based on the
        /// provided vector's components.
        /// </summary>
        /// <param name="c">The input vector whose components will be used to
        /// generate the pseudo-random value.</param>
        /// <returns>A <see cref="Vector2"/> containing two pseudo-random values
        /// between 0 and 1.</returns>
        static Vector2 Random2(Vector2 c)
        {
            float x = Frac(Mathf.Sin(c.x) * 43758.5453123f);
            float y = Frac(Mathf.Sin(c.y * x) * 43758.5453123f);
            return new Vector2(x, y);
        }

        /// <summary>
        /// Called by TextMeshPro when <see cref="text"/> needs to update its
        /// mesh.
        /// </summary>
        /// <param name="textInfo">Contains information describing the text
        /// mesh.</param>
        void OnPreRenderText(TMPro.TMP_TextInfo textInfo)
        {
            // We only want to update the jitter effect at jitterRate times per
            // second, even if this function is being called more often than
            // that (for example, another effect is running that modifies the
            // mesh, like Yarn Spinner's typewriter effect.)
            //
            // To ensure that the effect doesn't change too often, we snap the
            // value of Time.time based on jitterRate, and use that in
            // combination with each vertex's position to get the final amount
            // of random offset.

            var snappedTime = Snap(Time.time, 1f / jitterRate);

            // Each material slot in TextMeshPro is a different mesh, so we have
            // materialCount meshes to update.
            for (int mat = 0; mat < textInfo.materialCount; mat++)
            {
                var meshInfo = textInfo.meshInfo[mat];

                // Each character knows about the index of its first vertex in
                // the mesh, and each character is always 4 vertices. We'll
                // iterate through each character, and then iterate through each
                // of its vertices.

                for (int v = 0; v < meshInfo.vertexCount; v++)
                {
                    // Calculate a random Vector2 based on the vertex's
                    // position and our snapped time. The result will be a
                    // random position between (-1,-1) and (1,1).
                    var jitter = Random2(meshInfo.vertices[v] + Vector3.one * snappedTime);
                    // Offset our vertices, scaled by the defined amount
                    meshInfo.vertices[v] += new Vector3(
                        jitter.x * jitterAmount,
                        jitter.y * jitterAmount,
                        0
                    );
                }

                // Apply the updated mesh.
                textInfo.meshInfo[mat] = meshInfo;
            }
        }
    }
}