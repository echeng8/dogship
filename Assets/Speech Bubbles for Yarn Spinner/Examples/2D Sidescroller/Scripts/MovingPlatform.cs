namespace Yarn.Unity.Addons.SpeechBubbles
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class MovingPlatform : MonoBehaviour
    {
        public Transform[] points;

        public float speed = 10;

        private int currentPointIndex = 0;
        private float accumulator = 0;

        const float closenessThreshold = 0.01f;

        void Update()
        {
            var point = points[currentPointIndex].position;

            // are we at that point?
            if (Vector3.Distance(point, this.transform.position) < closenessThreshold)
            {
                var o = currentPointIndex;
                currentPointIndex = (currentPointIndex + 1) % points.Length;
                accumulator = 0;
                return;
            }

            accumulator = Mathf.Clamp(accumulator + Time.deltaTime, 0, speed);
            var pos = Vector3.Lerp(this.transform.position, point, accumulator / speed);
            this.transform.position = pos;
        }

        void OnDrawGizmos()
        {
            if (points.Length < 2) { return; }

            Gizmos.color = Color.white;

            for (int i = 0; i < points.Length; i++)
            {
                var current = points[i];
                var next = points[(i + 1) % points.Length];
                Gizmos.DrawLine(current.position, next.position);
            }
        }
    }
}
