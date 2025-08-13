using UnityEngine;
using Coherence.Toolkit;

namespace Gravitas.Demo
{
    public class GravitasSpaceshipControls : MonoBehaviour
    {
        public string SpaceshipName => name;
        public bool CanActivate => spaceshipSubject && !spaceshipSubject.IsControlled && activeTimer >= ACTIVE_TIME;

        private const float ACTIVE_TIME = 0.5f;

        [SerializeField] private GravitasSpaceshipSubject spaceshipSubject;
        private float activeTimer = ACTIVE_TIME;
        private CoherenceSync _sync;

        private void Awake()
        {
            _sync = GetComponentInParent<CoherenceSync>();
        }

        /// <summary>
        /// Point of entry for spaceship controlling, as activated by a player controller.
        /// </summary>
        /// <param name="player">The player interacting with the spaceship controls</param>
        public void InteractWithSpaceshipControls(GravitasFirstPersonPlayerSubject player)
        {
            Debug.Log($"InteractWithSpaceshipControls called. CanActivate: {CanActivate}");

            if (CanActivate && spaceshipSubject && player)
            {
                var playerSync = player.GetComponent<CoherenceSync>();
                Debug.Log($"Player sync found: {playerSync != null}, Has input authority: {playerSync?.HasInputAuthority}");

                if (playerSync && playerSync.HasInputAuthority)
                {
                    Debug.Log("Calling SetPlayerController on spaceship");
                    spaceshipSubject.SetPlayerController(player, transform.localPosition);
                }
            }
            else
            {
                Debug.Log($"Cannot activate spaceship. CanActivate: {CanActivate}, spaceshipSubject: {spaceshipSubject != null}, player: {player != null}");
            }
        }

        private void LateUpdate()
        {
            if (spaceshipSubject.IsControlled) // If spaceship is controlled
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
