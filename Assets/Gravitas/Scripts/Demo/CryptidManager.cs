using UnityEngine;
using Coherence.Toolkit;
using Coherence;

namespace Gravitas
{
    public class CryptidManager : MonoBehaviour
    {
        [Header("Cryptid Settings")]
        [SerializeField] private GameObject cryptidPrefab;
        [SerializeField] private float spawnDistance = 3f;

        private CoherenceSync _sync;

        void Start()
        {
            _sync = GetComponent<CoherenceSync>();
        }

        void Update()
        {

        }

        /// <summary>
        /// Get the cryptid prefab for spawning
        /// </summary>
        public GameObject GetCryptidPrefab()
        {
            return cryptidPrefab;
        }

        /// <summary>
        /// Spawn a cryptid near the specified transform
        /// </summary>
        /// <param name="spawnTransform">The transform to spawn near</param>
        public void SpawnCryptid(Transform spawnTransform)
        {
            if (_sync != null && _sync.HasStateAuthority)
            {
                // We have authority, apply the change directly
                NetworkSpawnCryptid(spawnTransform);
            }
            else if (_sync != null)
            {
                // Send command to authority
                _sync.SendCommand<CryptidManager>(
                    nameof(NetworkSpawnCryptid),
                    MessageTarget.AuthorityOnly,
                    spawnTransform
                );
            }
            else
            {
                // No networking, apply directly (fallback for single player)
                NetworkSpawnCryptid(spawnTransform);
            }
        }

        /// <summary>
        /// Network command to spawn cryptid. Only executed by authority.
        /// </summary>
        [Command]
        public void NetworkSpawnCryptid(Transform spawnTransform)
        {
            if (cryptidPrefab == null)
            {
                Debug.LogWarning("Cannot spawn cryptid: cryptidPrefab is null");
                return;
            }

            SpawnCryptidInternal(spawnTransform);
        }

        /// <summary>
        /// Internal method to spawn the cryptid
        /// </summary>
        private void SpawnCryptidInternal(Transform spawnTransform)
        {
            // Calculate spawn position in front of the transform
            Vector3 spawnPosition = spawnTransform.position + spawnTransform.forward * spawnDistance;
            Quaternion spawnRotation = Quaternion.LookRotation(spawnTransform.forward);

            // Find the GravitasField that contains the spawn transform
            IGravitasField currentField = null;
            GravitasSubject gravitasSubject = spawnTransform.GetComponent<GravitasSubject>();
            if (gravitasSubject != null)
            {
                currentField = gravitasSubject.CurrentField;
            }

            // Spawn cryptid using gravitas field if available
            GameObject spawnedCryptid = null;
            if (currentField != null)
            {
                print("Spawning cryptid in gravitas field");
                spawnedCryptid = currentField.SpawnAndAddToField(cryptidPrefab, spawnPosition, spawnRotation);
            }
            else
            {
                print("Spawning cryptid outside of gravitas field");
                spawnedCryptid = Instantiate(cryptidPrefab, spawnPosition, spawnRotation);
            }

            if (spawnedCryptid != null)
            {
                // Ensure the cryptid has the Cryptid tag
                spawnedCryptid.tag = "Cryptid";

                // Ensure the cryptid has CryptidController
                CryptidController controller = spawnedCryptid.GetComponent<CryptidController>();
                if (controller == null)
                {
                    controller = spawnedCryptid.AddComponent<CryptidController>();
                }

                Debug.Log($"Spawned cryptid: {spawnedCryptid.name}");
            }
        }
    }
}
