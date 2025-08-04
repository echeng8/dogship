using UnityEngine;

namespace Gravitas.UI
{
    /// <summary>
    /// MonoBehaviour UI class used to update a NavBall GameObject to display how the target is oriented
    /// </summary>
    public sealed class NavBall : MonoBehaviour
    {
        [SerializeField] private GameObject velocityArrow; // Reference to the child arrow representing the target's velocity
        [SerializeField] private GravitasBody bodyTarget; // Reference to the target to match orientation and show velocity of

        public void SetBodyTarget(GravitasBody body)
        {
            if (!body) { return; }

            bodyTarget = body;
        }

        private void Start()
        {
            // Warnings if any of the required fields aren't assigned
            if (velocityArrow == null)
            {
                Debug.LogWarning($@"Gravitas: NavBall ""{gameObject.name}"" velocity arrow GameObject is not assigned!");
                Destroy(this);
            }
            else if (bodyTarget == null)
            {
                Debug.LogWarning($@"Gravitas: NavBall ""{gameObject.name}"" subject target is not assigned!");
                Destroy(this);
            }
        }

        private void Update()
        {
            // Assigning the opposite of the target's rotation
            transform.rotation = Quaternion.Inverse(bodyTarget.CurrentTransform.rotation);

            // Velocity display
            bool showVelocityArrow = bodyTarget.Velocity.sqrMagnitude > 0.1f;
            velocityArrow.SetActive(showVelocityArrow);
            if (showVelocityArrow)
            {
                velocityArrow.transform.localRotation =
                    Quaternion.LookRotation(bodyTarget.Velocity, Vector3.up);
            }
        }
    }
}
