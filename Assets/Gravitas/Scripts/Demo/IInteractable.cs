using UnityEngine;

namespace Gravitas.Demo
{
    /// <summary>
    /// Interface for all interactable objects in the game.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Whether this object can be interacted with currently.
        /// </summary>
        bool CanInteract { get; }

        /// <summary>
        /// Display name for the interaction prompt.
        /// </summary>
        string InteractionPrompt { get; }

        /// <summary>
        /// Called when the player interacts with this object.
        /// </summary>
        /// <param name="player">The player performing the interaction</param>
        void Interact(GravitasFirstPersonPlayerSubject player);
    }
}
