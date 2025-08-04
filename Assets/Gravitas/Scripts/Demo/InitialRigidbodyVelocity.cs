using UnityEngine;

namespace Gravitas.Demo
{
    /// <summary>
    /// Utility class for giving a rigidbody or a Gravitas subject an initial starting velocity.
    /// </summary>
    [DefaultExecutionOrder(-1)]
    [DisallowMultipleComponent]
    internal sealed class InitialRigidbodyVelocity : MonoBehaviour
    {
        [SerializeField] private Vector3 initialAngularVelocity;
        [SerializeField] private Vector3 initialVelocity;

        private void Start()
        {
            if (TryGetComponent(out IGravitasBody body)) // Set velocity of subject
            {
                body.AngularVelocity = initialAngularVelocity;
                body.Velocity = initialVelocity;
            }
            else if (TryGetComponent(out Rigidbody rb)) // Set velocity directly to rigidbody
            {
                rb.angularVelocity = initialAngularVelocity;
                rb.velocity = initialVelocity;
            }

            Destroy(this);
        }
    }
}
