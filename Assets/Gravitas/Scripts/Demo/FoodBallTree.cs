using UnityEngine;
using Coherence.Toolkit;
using System.Collections.Generic;

namespace Gravitas
{
    /// <summary>
    /// Manages food ball growth and spawning on a tree.
    /// Uses coherence for networking - only authority runs game logic.
    /// </summary>
    public class FoodBallTree : MonoBehaviour
    {
        [Header("Food Ball Tree Settings")]
        [SerializeField] private GameObject foodBallPrefab;
        [SerializeField] private float secondsToGrow = 3f;
        [SerializeField] private float respawnDelay = 5f;

        private CoherenceSync planetSync;
        private IGravitasField gravitasField;
        private FoodBallGrowing[] foodBallGrowingObjects;
        private Dictionary<FoodBallGrowing, float> respawnTimers = new Dictionary<FoodBallGrowing, float>();

        private void Awake()
        {
            // Get planet's CoherenceSync from parent
            planetSync = GetComponentInParent<CoherenceSync>();
            if (planetSync == null)
            {
                Debug.LogError($"FoodBallTree {name} requires a CoherenceSync component on parent planet!");
            }

            // Get the GravitasField from the same GameObject or parent
            gravitasField = GetComponent<IGravitasField>() ?? GetComponentInParent<IGravitasField>();
            if (gravitasField == null)
            {
                Debug.LogError($"FoodBallTree {name} requires a GravitasField component on same GameObject or parent!");
            }            // Get all FoodBallGrowing children
            foodBallGrowingObjects = GetComponentsInChildren<FoodBallGrowing>();

            // Set parent reference for each growing object
            foreach (var growingObj in foodBallGrowingObjects)
            {
                growingObj.parentTree = this;
            }
        }

        private void Start()
        {
            if (HasAuthority())
            {
                Debug.Log($"[FoodBallTree] {name} has authority, starting all growing. Found {foodBallGrowingObjects.Length} growing objects");
                StartAllGrowing();
            }
            else
            {
                Debug.Log($"[FoodBallTree] {name} does not have authority");
            }
        }

        private void Update()
        {
            // Only authority runs game logic
            if (!HasAuthority()) return;

            if (foodBallGrowingObjects == null) return;

            // Update respawn timers
            var keysToUpdate = new List<FoodBallGrowing>(respawnTimers.Keys);
            foreach (var growingObj in keysToUpdate)
            {
                if (respawnTimers[growingObj] > 0f)
                {
                    respawnTimers[growingObj] -= Time.deltaTime;
                    if (respawnTimers[growingObj] <= 0f)
                    {
                        Debug.Log($"[FoodBallTree] Respawn timer finished for {growingObj.name}, starting growth");
                        StartGrowing(growingObj);
                        respawnTimers.Remove(growingObj);
                    }
                }
            }

            // Update growing objects
            foreach (var growingObj in foodBallGrowingObjects)
            {
                if (growingObj != null && !respawnTimers.ContainsKey(growingObj))
                {
                    growingObj.UpdateGrowth(1f / secondsToGrow * Time.deltaTime);
                }
            }
        }

        public void OnFoodBallFullyGrown(FoodBallGrowing growingObj)
        {
            if (!HasAuthority()) return;

            Debug.Log($"[FoodBallTree] {growingObj.name} is fully grown, harvesting");
            HarvestFoodBall(growingObj);
        }

        private void HarvestFoodBall(FoodBallGrowing growingObj)
        {
            if (growingObj == null) return;

            Debug.Log($"[FoodBallTree] Harvesting {growingObj.name} at position {growingObj.transform.position}");

            // Spawn food ball prefab at location
            if (foodBallPrefab != null)
            {
                Vector3 spawnPosition = growingObj.transform.position;
                Quaternion spawnRotation = growingObj.transform.rotation;

                GameObject spawnedFoodBall = Instantiate(foodBallPrefab, spawnPosition, spawnRotation);
                Debug.Log($"[FoodBallTree] Spawned food ball: {spawnedFoodBall.name}");

                // Ensure spawned food ball has coherence sync
                if (spawnedFoodBall.GetComponent<CoherenceSync>() == null)
                {
                    Debug.LogWarning($"Spawned food ball prefab should have CoherenceSync component for networking!");
                }

                // Add the spawned food ball to the same field as the tree
                if (gravitasField != null && spawnedFoodBall.TryGetComponent<IGravitasSubject>(out var foodBallSubject))
                {
                    // Set velocity to match the planet before adding to field
                    IGravitasBody foodBallBody = foodBallSubject.GravitasBody;
                    if (foodBallBody != null)
                    {
                        foodBallBody.AngularVelocity = gravitasField.FieldAngularVelocity;
                        foodBallBody.Velocity = gravitasField.FieldVelocity;
                        Debug.Log($"[FoodBallTree] Set food ball velocity to match field - Linear: {gravitasField.FieldVelocity}, Angular: {gravitasField.FieldAngularVelocity}");
                    }

                    gravitasField.EnqueueSubjectChange(foodBallSubject, true);
                    Debug.Log($"[FoodBallTree] Enqueued food ball {spawnedFoodBall.name} to enter field {gravitasField.GameObject.name}");
                }
                else
                {
                    Debug.LogWarning($"[FoodBallTree] Could not add food ball to field - gravitasField: {gravitasField != null}, hasSubject: {spawnedFoodBall.GetComponent<IGravitasSubject>() != null}");
                }
            }
            else
            {
                Debug.LogWarning($"[FoodBallTree] No food ball prefab assigned!");
            }

            // Hide and reset the growing object
            growingObj.Reset();

            // Set respawn timer
            respawnTimers[growingObj] = respawnDelay;
            Debug.Log($"[FoodBallTree] Set respawn timer for {growingObj.name}: {respawnDelay} seconds");
        }

        private void StartGrowing(FoodBallGrowing growingObj)
        {
            if (growingObj == null) return;

            growingObj.StartGrowing();
        }

        private void StartAllGrowing()
        {
            foreach (var growingObj in foodBallGrowingObjects)
            {
                if (growingObj != null)
                {
                    StartGrowing(growingObj);
                }
            }
        }

        public bool HasAuthority()
        {
            if (planetSync == null) return true;
            if (planetSync.CoherenceBridge == null || !planetSync.CoherenceBridge.IsConnected) return true;
            return planetSync.HasStateAuthority;
        }
    }
}
