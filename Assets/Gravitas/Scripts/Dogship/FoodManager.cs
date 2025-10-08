using UnityEngine;
using System.Collections.Generic;
using Coherence.Toolkit;
using Coherence;

namespace Gravitas
{
    /// <summary>
    /// Manages eaten food balls and tracks win condition.
    /// Win condition: At least one Yellow and one Green food ball must be eaten.
    /// Networked via Coherence - authority client modifies state.
    /// </summary>
    public class FoodManager : MonoBehaviour
    {
        private static FoodManager _instance;
        public static FoodManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<FoodManager>();
                }
                return _instance;
            }
        }

        [Header("Networked State")]
        private CoherenceSync _sync;

        [Header("Eaten Food Balls Tracking")]
        private HashSet<FoodBallType> eatenFoodBallTypes = new HashSet<FoodBallType>();

        [Sync]
        public int totalEatenCount = 0;

        [Sync]
        public bool hasYellowBeenEaten = false;

        [Sync]
        public bool hasGreenBeenEaten = false;

        [Header("Win Condition")]
        [Sync]
        public bool hasWon = false;

        // Events
        public event System.Action<FoodBallType, int> OnFoodBallEaten; // type, total count
        public event System.Action OnWinConditionMet;

        // Track previous values to detect changes from network sync
        private bool _previousHasWon = false;
        private int _previousTotalCount = 0;
        private bool _previousHasYellow = false;
        private bool _previousHasGreen = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _sync = GetComponent<CoherenceSync>();
            if (_sync == null)
            {
                Debug.LogError("FoodManager requires a CoherenceSync component for networking!");
            }
        }

        private void Start()
        {
            // Initialize the HashSet from synced variables
            UpdateHashSetFromSyncedState();

            // Initialize previous values
            _previousHasWon = hasWon;
            _previousTotalCount = totalEatenCount;
            _previousHasYellow = hasYellowBeenEaten;
            _previousHasGreen = hasGreenBeenEaten;
        }

        private void Update()
        {
            // Detect changes from network sync on non-authority clients
            // and trigger local events accordingly
            if (_sync != null && !_sync.HasStateAuthority)
            {
                // Check if total count changed
                if (totalEatenCount != _previousTotalCount)
                {
                    // Determine which type was added
                    if (hasYellowBeenEaten && !_previousHasYellow)
                    {
                        OnFoodBallEaten?.Invoke(FoodBallType.Yellow, totalEatenCount);
                        UpdateHashSetFromSyncedState();
                    }
                    else if (hasGreenBeenEaten && !_previousHasGreen)
                    {
                        OnFoodBallEaten?.Invoke(FoodBallType.Green, totalEatenCount);
                        UpdateHashSetFromSyncedState();
                    }

                    _previousTotalCount = totalEatenCount;
                    _previousHasYellow = hasYellowBeenEaten;
                    _previousHasGreen = hasGreenBeenEaten;
                }

                // Check if win condition changed
                if (hasWon && !_previousHasWon)
                {
                    Debug.Log("WIN CONDITION RECEIVED from network sync!");
                    OnWinConditionMet?.Invoke();
                    _previousHasWon = hasWon;
                }
            }
        }

        /// <summary>
        /// Updates the local HashSet based on synced boolean variables.
        /// Called on Start and whenever synced state changes.
        /// </summary>
        private void UpdateHashSetFromSyncedState()
        {
            eatenFoodBallTypes.Clear();
            if (hasYellowBeenEaten)
                eatenFoodBallTypes.Add(FoodBallType.Yellow);
            if (hasGreenBeenEaten)
                eatenFoodBallTypes.Add(FoodBallType.Green);
        }

        /// <summary>
        /// Records that a food ball of the specified type has been eaten.
        /// Checks win condition after tracking.
        /// Sends command to authority if not authority.
        /// </summary>
        /// <param name="type">The type of food ball that was eaten</param>
        public void RecordFoodBallEaten(FoodBallType type)
        {
            if (_sync != null && _sync.HasStateAuthority)
            {
                // We have authority, apply the change directly
                NetworkRecordFoodBallEaten((int)type);
            }
            else if (_sync != null)
            {
                // Send command to authority (convert enum to int for network compatibility)
                _sync.SendCommand<FoodManager>(
                    nameof(NetworkRecordFoodBallEaten),
                    MessageTarget.AuthorityOnly,
                    (int)type
                );
            }
            else
            {
                // No networking, apply directly (fallback for single player)
                NetworkRecordFoodBallEaten((int)type);
            }
        }

        /// <summary>
        /// Network command to record a food ball eaten. Only executed by authority.
        /// </summary>
        /// <param name="typeInt">The type of food ball that was eaten (as int)</param>
        [Command]
        public void NetworkRecordFoodBallEaten(int typeInt)
        {
            FoodBallType type = (FoodBallType)typeInt;

            // Update synced variables (these will automatically sync to all clients)
            totalEatenCount++;

            if (type == FoodBallType.Yellow)
                hasYellowBeenEaten = true;
            else if (type == FoodBallType.Green)
                hasGreenBeenEaten = true;

            // Update local HashSet
            eatenFoodBallTypes.Add(type);

            Debug.Log($"Food ball eaten! Type: {type}, Total eaten: {totalEatenCount}, Unique types: {eatenFoodBallTypes.Count}");

            OnFoodBallEaten?.Invoke(type, totalEatenCount);

            CheckWinCondition();
        }

        /// <summary>
        /// Checks if the win condition has been met.
        /// Win condition: At least one Yellow and one Green food ball eaten.
        /// </summary>
        private void CheckWinCondition()
        {
            if (hasWon)
                return;

            // Check synced variables directly
            if (hasYellowBeenEaten && hasGreenBeenEaten)
            {
                hasWon = true; // This will sync to all clients automatically
                Debug.Log("WIN CONDITION MET! Player has eaten both Yellow and Green food balls!");
                OnWinConditionMet?.Invoke();
            }
        }

        /// <summary>
        /// Gets whether the win condition has been met.
        /// </summary>
        public bool HasWon => hasWon;

        /// <summary>
        /// Gets the total number of food balls eaten.
        /// </summary>
        public int TotalEatenCount => totalEatenCount;

        /// <summary>
        /// Gets the number of unique food ball types eaten.
        /// </summary>
        public int UniqueTypesEaten => eatenFoodBallTypes.Count;

        /// <summary>
        /// Checks if a specific type has been eaten.
        /// </summary>
        public bool HasEatenType(FoodBallType type)
        {
            return eatenFoodBallTypes.Contains(type);
        }

        /// <summary>
        /// Resets the food manager (for testing or restarting the game).
        /// Sends command to authority if not authority.
        /// </summary>
        public void Reset()
        {
            if (_sync != null && _sync.HasStateAuthority)
            {
                // We have authority, apply the change directly
                NetworkReset();
            }
            else if (_sync != null)
            {
                // Send command to authority
                _sync.SendCommand<FoodManager>(
                    nameof(NetworkReset),
                    MessageTarget.AuthorityOnly
                );
            }
            else
            {
                // No networking, apply directly (fallback for single player)
                NetworkReset();
            }
        }

        /// <summary>
        /// Network command to reset the manager. Only executed by authority.
        /// </summary>
        [Command]
        public void NetworkReset()
        {
            // Reset synced variables (will automatically sync to all clients)
            totalEatenCount = 0;
            hasWon = false;
            hasYellowBeenEaten = false;
            hasGreenBeenEaten = false;

            // Reset local HashSet
            eatenFoodBallTypes.Clear();

            Debug.Log("FoodManager reset.");
        }
    }

    /// <summary>
    /// Enum representing different types of food balls.
    /// </summary>
    public enum FoodBallType
    {
        Yellow,
        Green
    }
}
