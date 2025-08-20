using UnityEngine;

namespace Gravitas.Demo
{
    /// <summary>
    /// Simple MonoBehaviour script intended to be used to provide scene access to resetting ship velocity and orientation.
    /// </summary>
    internal sealed class GravitasSpaceshipResetButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private GravitasSpaceshipSubject spaceshipSubject; // Reference to the ship to reset

        public bool CanInteract => spaceshipSubject != null;
        public string InteractionPrompt => "Reset Button";

        /// <summary>
        /// Simple method that resets the assigned spaceship if assigned.
        /// </summary>
        public void ResetSpaceship()
        {
            if (spaceshipSubject != null)
                spaceshipSubject.ResetSpaceship();
        }

        public void Interact(GravitasFirstPersonPlayerSubject player)
        {
#if GRAVITAS_LOGGING
            if (GravitasDebugLogger.CanLog(GravitasDebugLoggingFlags.PlayerInteraction))
                GravitasDebugLogger.Log("Resetting spaceship");
#endif
            ResetSpaceship();
        }

        private void Awake()
        {
            // Warning if reset button does not have required reference
            if (spaceshipSubject == null)
            {
                Debug.LogWarning($@"Gravitas: Spaceship reset button ""{gameObject.name}"" spaceship controller not assigned!");
                Destroy(this);
            }
        }
    }
}
