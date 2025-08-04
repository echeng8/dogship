using UnityEngine;

namespace Gravitas.Demo
{
    /// <summary>
    /// A simple utility class to change the fixed direction of a Gravitas field during runtime.
    /// </summary>
    internal class GravitasFieldDirectionControl : MonoBehaviour
    {
        // The name of the chosen fixed direction, provided as a property for convenient UI display
        public string DirectionName
            => fixedDirection.ToString();

        [SerializeField] private FixedDirection fixedDirection; // The chosen fixed direction to change to
        [SerializeField] private GravitasField fieldTarget; // The targeted field to switch the direction of

        /// <summary>
        /// Switches the assigned field's direction to the assigned fixed direction.
        /// </summary>
        public void SwitchGravity()
        {
            if (fieldTarget != null && fixedDirection != FixedDirection.None)
                fieldTarget.FixedDirection = fixedDirection;
        }

        private void Awake()
        {
            // Warning if this script does not have the required values
            if (fieldTarget == null)
            {
                Debug.LogWarning($@"Gravitas: Field Direction Control ""{gameObject.name}"" field target not assigned!");
                Destroy(this);
            }
        }
    }
}
