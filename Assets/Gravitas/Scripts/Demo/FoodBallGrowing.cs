using UnityEngine;
using Coherence.Toolkit;

namespace Gravitas
{
    /// <summary>
    /// Represents a growing food ball that scales up over time.
    /// Should be networked using CoherenceSync for multiplayer.
    /// </summary>
    public class FoodBallGrowing : MonoBehaviour
    {
        [Header("Growing Settings")]
        [SerializeField] private Vector3 targetScale = Vector3.one;

        [Header("Tree Reference")]
        public FoodBallTree parentTree;

        [Header("Debug Info (Read Only)")]
        [SerializeField] private Vector3 debugWorldScale;
        [SerializeField] private Vector3 debugLocalScale;
        [SerializeField] private Vector3 debugCurrentScale;
        [SerializeField] private bool debugIsGrowing;

        private Vector3 initialScale;
        private bool isGrowing = false;
        private MeshRenderer meshRenderer;

        // Sync through parent tree's planet CoherenceSync
        public bool IsVisible { get; set; } = true;
        public Vector3 CurrentScale { get; set; }

        private void Awake()
        {
            // Store initial world scale, not local scale
            initialScale = transform.lossyScale;
            CurrentScale = initialScale;
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Start()
        {
            UpdateVisibility();
        }

        private void Update()
        {
            // Update debug info for inspector
            debugWorldScale = transform.lossyScale;
            debugLocalScale = transform.localScale;
            debugCurrentScale = CurrentScale;
            debugIsGrowing = isGrowing;

            // Convert world scale to local scale to maintain uniform world appearance
            if (transform.parent != null)
            {
                Vector3 parentLossyScale = transform.parent.lossyScale;
                Vector3 worldScale = CurrentScale;
                Vector3 localScale = new Vector3(
                    worldScale.x / parentLossyScale.x,
                    worldScale.y / parentLossyScale.y,
                    worldScale.z / parentLossyScale.z
                );
                transform.localScale = localScale;
            }
            else
            {
                transform.localScale = CurrentScale;
            }

            UpdateVisibility();
        }
        public void StartGrowing()
        {
            Debug.Log($"[FoodBallGrowing] {name} StartGrowing called " + HasAuthority());
            if (HasAuthority())
            {
                isGrowing = true;
                IsVisible = true;
                CurrentScale = initialScale;
                Debug.Log($"[FoodBallGrowing] {name} started growing. Initial: {initialScale}, Target: {targetScale}");
            }
        }

        public void UpdateGrowth(float deltaTime)
        {
            if (!HasAuthority() || !isGrowing) return;

            Vector3 oldScale = CurrentScale;
            CurrentScale = Vector3.MoveTowards(CurrentScale, targetScale, deltaTime);

            Debug.Log($"[FoodBallGrowing] {name} growing: {oldScale} -> {CurrentScale} (delta: {deltaTime})");

            if (Vector3.Distance(CurrentScale, targetScale) < 0.01f)
            {
                CurrentScale = targetScale;
                isGrowing = false;
                Debug.Log($"[FoodBallGrowing] {name} fully grown! Notifying tree.");
                NotifyFullyGrown();
            }
        }

        public bool IsFullyGrown => Vector3.Distance(CurrentScale, targetScale) < 0.01f;

        public void Reset()
        {
            if (HasAuthority())
            {
                CurrentScale = initialScale;
                IsVisible = false;
                isGrowing = false;
                Debug.Log($"[FoodBallGrowing] {name} reset and hidden");
            }
        }

        public void NotifyFullyGrown()
        {
            if (parentTree != null)
            {
                parentTree.OnFoodBallFullyGrown(this);
            }
        }

        private void UpdateVisibility()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = IsVisible;
            }
        }

        private bool HasAuthority()
        {
            if (parentTree == null) return true;
            return parentTree.HasAuthority();
        }
    }
}
