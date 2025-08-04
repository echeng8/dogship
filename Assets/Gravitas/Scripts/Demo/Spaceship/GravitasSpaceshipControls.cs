using UnityEngine;

namespace Gravitas.Demo
{
    internal class GravitasSpaceshipControls : MonoBehaviour
    {
        public string SpaceshipName => name;
        public bool CanActivate => spaceshipSubject && !spaceshipSubject.PlayerSubject && activeTimer >= ACTIVE_TIME;

        private const float ACTIVE_TIME = 0.5f;

        [SerializeField] private GravitasSpaceshipSubject spaceshipSubject;
        private float activeTimer = ACTIVE_TIME; // A time tracking if the spaceship controls can be activated yet

        /// <summary>
        /// Point of entry for spaceship controlling, as activated by a player controller.
        /// </summary>
        /// <param name="player">The player interacting with the spaceship controls</param>
        public void InteractWithSpaceshipControls(GravitasFirstPersonPlayerSubject player)
        {
            if (CanActivate && spaceshipSubject)
            {
                spaceshipSubject.SetPlayerController(player, transform.localPosition);
            }
        }

        private void LateUpdate()
        {
            if (spaceshipSubject.PlayerSubject) // If spaceship is controlled
            {
                activeTimer = 0;
            }
            else if (!CanActivate) // Updating activation cooldown timer
            {
                activeTimer += Time.deltaTime;
            }
        }
    }
}
