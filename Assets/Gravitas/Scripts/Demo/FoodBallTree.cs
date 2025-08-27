using UnityEngine;
using Coherence.Toolkit;
using Coherence;
using System.Collections.Generic;
using Gravitas.Demo;

namespace Gravitas
{
    public enum FoodBallState
    {
        Growing,
        Ripe,
        Empty
    }

    /// <summary>
    /// Manages food ball growth on a tree. Players can harvest ripe food balls.
    /// Uses coherence for networking - only authority runs game logic.
    /// </summary>
    public class FoodBallTree : MonoBehaviour, IInteractable
    {
        [Header("Food Ball Tree Settings")]
        [SerializeField] private GameObject foodBallPrefab;
        [SerializeField] private float secondsToGrow = 3f;
        [SerializeField] private float respawnDelay = 5f;

        private CoherenceSync planetSync;
        private IGravitasField gravitasField;
        private FoodBallGrowing[] foodBallGrowingObjects;
        private Dictionary<FoodBallGrowing, float> respawnTimers = new Dictionary<FoodBallGrowing, float>();
        private Dictionary<FoodBallGrowing, FoodBallState> foodBallStates = new Dictionary<FoodBallGrowing, FoodBallState>();

        // IInteractable implementation
        public bool CanInteract => GetRipeFoodBallCount() > 0;
        public string InteractionPrompt => CanInteract ? $"Harvest Food Balls ({GetRipeFoodBallCount()})" : "Not Harvestable";

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
            }

            // Get all FoodBallGrowing children
            foodBallGrowingObjects = GetComponentsInChildren<FoodBallGrowing>();

            // Set parent reference and initialize states for each growing object
            foreach (var growingObj in foodBallGrowingObjects)
            {
                growingObj.parentTree = this;
                foodBallStates[growingObj] = FoodBallState.Empty;
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

            // Update growing objects - only grow if not ripe
            foreach (var growingObj in foodBallGrowingObjects)
            {
                if (growingObj != null && !respawnTimers.ContainsKey(growingObj) &&
                    foodBallStates[growingObj] == FoodBallState.Growing)
                {
                    growingObj.UpdateGrowth(1f / secondsToGrow * Time.deltaTime);
                }
            }
        }

        public void OnFoodBallFullyGrown(FoodBallGrowing growingObj)
        {
            if (!HasAuthority()) return;

            Debug.Log($"[FoodBallTree] {growingObj.name} is fully grown, setting to ripe state");
            foodBallStates[growingObj] = FoodBallState.Ripe;
            // Don't harvest automatically anymore - wait for player interaction
        }

        public void Interact(GravitasFirstPersonPlayerSubject player)
        {
            if (player != null && planetSync != null && CanInteract)
            {
                // Send command to authority to handle harvesting
                planetSync.SendCommand<FoodBallTree>(
                    nameof(NetworkHarvestFoodBalls),
                    MessageTarget.AuthorityOnly,
                    player.gameObject
                );
            }
        }

        [Command]
        public void NetworkHarvestFoodBalls(GameObject playerGameObject)
        {
            if (!HasAuthority() || !CanInteract) return;

            Debug.Log($"[FoodBallTree] Player {playerGameObject?.name} is harvesting food balls");
            HarvestAllRipeFoodBalls();
        }

        private int GetRipeFoodBallCount()
        {
            int count = 0;
            foreach (var state in foodBallStates.Values)
            {
                if (state == FoodBallState.Ripe)
                    count++;
            }
            return count;
        }

        private void HarvestAllRipeFoodBalls()
        {
            var ripeFoodBalls = new List<FoodBallGrowing>();
            foreach (var kvp in foodBallStates)
            {
                if (kvp.Value == FoodBallState.Ripe)
                {
                    ripeFoodBalls.Add(kvp.Key);
                }
            }

            foreach (var growingObj in ripeFoodBalls)
            {
                HarvestFoodBall(growingObj);
            }
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

                GameObject spawnedFoodBall = gravitasField?.SpawnAndAddToField(foodBallPrefab, spawnPosition, spawnRotation);

                if (spawnedFoodBall != null)
                {
                    Debug.Log($"[FoodBallTree] Spawned and added food ball: {spawnedFoodBall.name}");

                    // Ensure spawned food ball has coherence sync
                    if (spawnedFoodBall.GetComponent<CoherenceSync>() == null)
                    {
                        Debug.LogWarning($"Spawned food ball prefab should have CoherenceSync component for networking!");
                    }
                }
                else
                {
                    Debug.LogWarning($"[FoodBallTree] Failed to spawn food ball - gravitasField: {gravitasField != null}");
                }
            }
            else
            {
                Debug.LogWarning($"[FoodBallTree] No food ball prefab assigned!");
            }

            // Hide and reset the growing object
            growingObj.Reset();

            // Set state to empty and start respawn timer
            foodBallStates[growingObj] = FoodBallState.Empty;
            respawnTimers[growingObj] = respawnDelay;
            Debug.Log($"[FoodBallTree] Set respawn timer for {growingObj.name}: {respawnDelay} seconds");
        }

        private void StartGrowing(FoodBallGrowing growingObj)
        {
            if (growingObj == null) return;

            foodBallStates[growingObj] = FoodBallState.Growing;
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
