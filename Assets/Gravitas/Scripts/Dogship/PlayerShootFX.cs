using UnityEngine;
using System.Collections;
using Coherence.Toolkit;

namespace Gravitas
{
    /// <summary>
    /// Handles visual effects for player shooting including bullet trails, muzzle flash, and hit effects.
    /// Hooks into PlayerShoot UnityEvents for clean separation of concerns.
    /// 
    /// Network Behavior:
    /// - Responds to [Command] methods from PlayerShoot
    /// - Commands automatically route to local calls when offline
    /// - Works seamlessly in both online and offline modes
    /// </summary>
    public class PlayerShootFX : MonoBehaviour
    {
        #region SerializeField Variables
        [Header("Bullet Trail")]
        [SerializeField] private LineRenderer bulletTrail;
        [SerializeField] private float trailDuration = 0.1f;
        [SerializeField] private AnimationCurve trailWidthCurve = AnimationCurve.Linear(0, 0.05f, 1, 0f);
        [SerializeField] private Color trailColor = Color.yellow;

        [Header("Muzzle Flash")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private Light muzzleLight;
        [SerializeField] private float muzzleLightDuration = 0.05f;
        [SerializeField] private float muzzleLightIntensity = 2f;

        [Header("Hit Effects")]
        [SerializeField] private ParticleSystem hitSparks;
        [SerializeField] private ParticleSystem missEffect;
        [SerializeField] private GameObject hitImpactPrefab;
        [SerializeField] private float impactLifetime = 5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip shootSound;
        [SerializeField] private AudioClip[] hitSounds;
        [SerializeField] private AudioClip failSound;
        #endregion

        #region Private Variables
        private PlayerShoot playerShoot;
        private Camera playerCamera;
        private CoherenceSync _sync;
        #endregion

        #region Unity Lifecycle Methods
        void Start()
        {
            // Get required components
            playerShoot = GetComponent<PlayerShoot>();
            if (playerShoot == null)
            {
                Debug.LogError($"PlayerShootFX on {name} requires a PlayerShoot component!");
                enabled = false;
                return;
            }

            // Get CoherenceSync component (optional for networking)
            _sync = GetComponent<CoherenceSync>();

            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            // Set up audio source
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            // Set up line renderer if not assigned
            if (bulletTrail == null)
            {
                SetupBulletTrail();
            }
            else
            {
                ConfigureBulletTrail();
            }

            // Hook into PlayerShoot events - these are now networked
            HookIntoShootEvents();
        }
        #endregion

        #region Setup Methods
        private void SetupBulletTrail()
        {
            GameObject trailObject = new GameObject("BulletTrail");
            trailObject.transform.SetParent(transform);

            bulletTrail = trailObject.AddComponent<LineRenderer>();
            ConfigureBulletTrail();
        }

        private void ConfigureBulletTrail()
        {
            if (bulletTrail == null) return;

            bulletTrail.material = CreateTrailMaterial();
            bulletTrail.widthMultiplier = 0.05f;
            bulletTrail.widthCurve = trailWidthCurve;
            bulletTrail.startColor = trailColor;
            bulletTrail.endColor = trailColor;
            bulletTrail.positionCount = 2;
            bulletTrail.useWorldSpace = false;
            bulletTrail.enabled = false;
        }

        private Material CreateTrailMaterial()
        {
            // Create a simple unlit material for the trail
            Material trailMat = new Material(Shader.Find("Sprites/Default"));
            trailMat.color = trailColor;
            return trailMat;
        }

        private void HookIntoShootEvents()
        {
            // Hook into all the PlayerShoot events
            playerShoot.OnShootStart.AddListener(OnShootStart);
            playerShoot.OnHitTarget.AddListener(OnHitTarget);
            playerShoot.OnShootMiss.AddListener(OnShootMiss);
            playerShoot.OnShootFailed.AddListener(OnShootFailed);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Called when shooting starts - triggers muzzle flash and sound.
        /// Networked via [Command] when online, local call when offline.
        /// </summary>
        public void OnShootStart()
        {
            PlayMuzzleFlash();
            PlayShootSound();
        }

        /// <summary>
        /// Called when a target is hit - shows bullet trail to hit point and impact effects.
        /// Networked via [Command] when online, local call when offline.
        /// </summary>
        /// <param name="startPos">Where the shot started from</param>
        /// <param name="endPos">Where the shot hit</param>
        public void OnHitTarget(Vector3 startPos, Vector3 endPos)
        {
            StartCoroutine(ShowBulletTrail(startPos, endPos));

            // Calculate hit normal for effects
            Vector3 hitNormal = (startPos - endPos).normalized;
            ShowHitEffect(endPos, hitNormal);
            PlayHitSound();
        }

        /// <summary>
        /// Called when shot misses - shows bullet trail to max range.
        /// Networked via [Command] when online, local call when offline.
        /// </summary>
        /// <param name="startPos">Where the shot started from</param>
        /// <param name="endPos">Where the shot ended (max range)</param>
        public void OnShootMiss(Vector3 startPos, Vector3 endPos)
        {
            StartCoroutine(ShowBulletTrail(startPos, endPos));
            ShowMissEffect(endPos);
        }

        /// <summary>
        /// Called when shooting fails (no stamina) - plays fail sound.
        /// Networked via [Command] when online, local call when offline.
        /// </summary>
        public void OnShootFailed()
        {
            PlayFailSound();
        }
        #endregion

        #region Effect Methods
        private void PlayMuzzleFlash()
        {
            if (muzzleFlash != null)
            {
                muzzleFlash.Play();
            }

            if (muzzleLight != null)
            {
                StartCoroutine(MuzzleLightFlash());
            }
        }

        private IEnumerator MuzzleLightFlash()
        {
            muzzleLight.intensity = muzzleLightIntensity;
            muzzleLight.enabled = true;

            yield return new WaitForSeconds(muzzleLightDuration);

            muzzleLight.enabled = false;
        }

        private IEnumerator ShowBulletTrail(Vector3 startPos, Vector3 endPos)
        {
            if (bulletTrail == null) yield break;

            // Convert world positions to local positions relative to the player
            Vector3 localStart = transform.InverseTransformPoint(startPos);
            Vector3 localEnd = transform.InverseTransformPoint(endPos);

            bulletTrail.enabled = true;
            bulletTrail.SetPosition(0, localStart);
            bulletTrail.SetPosition(1, localEnd);

            yield return new WaitForSeconds(trailDuration);

            bulletTrail.enabled = false;
        }

        private void ShowHitEffect(Vector3 hitPosition, Vector3 hitNormal)
        {
            if (hitSparks != null)
            {
                hitSparks.transform.position = hitPosition;
                hitSparks.transform.rotation = Quaternion.LookRotation(hitNormal);
                hitSparks.Play();
            }

            if (hitImpactPrefab != null)
            {
                GameObject impact = Instantiate(hitImpactPrefab, hitPosition, Quaternion.LookRotation(hitNormal));
                Destroy(impact, impactLifetime);
            }
        }

        private void ShowMissEffect(Vector3 missPosition)
        {
            if (missEffect != null)
            {
                missEffect.transform.position = missPosition;
                missEffect.Play();
            }
        }
        #endregion

        #region Audio Methods
        private void PlayShootSound()
        {
            if (audioSource != null && shootSound != null)
            {
                audioSource.PlayOneShot(shootSound);
            }
        }

        private void PlayHitSound()
        {
            if (audioSource != null && hitSounds != null && hitSounds.Length > 0)
            {
                AudioClip randomHitSound = hitSounds[Random.Range(0, hitSounds.Length)];
                audioSource.PlayOneShot(randomHitSound);
            }
        }

        private void PlayFailSound()
        {
            if (audioSource != null && failSound != null)
            {
                audioSource.PlayOneShot(failSound);
            }
        }
        #endregion

        #region Cleanup
        void OnDestroy()
        {
            // Unhook events to prevent memory leaks
            if (playerShoot != null)
            {
                playerShoot.OnShootStart.RemoveListener(OnShootStart);
                playerShoot.OnHitTarget.RemoveListener(OnHitTarget);
                playerShoot.OnShootMiss.RemoveListener(OnShootMiss);
                playerShoot.OnShootFailed.RemoveListener(OnShootFailed);
            }
        }
        #endregion
    }
}
