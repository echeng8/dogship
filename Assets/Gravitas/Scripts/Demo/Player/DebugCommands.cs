using UnityEngine;
using IngameDebugConsole;
using Coherence.Toolkit;
using Gravitas.Demo;

namespace Gravitas
{
    public class DebugCommands : MonoBehaviour
    {
        [Header("Debug References")]
        [SerializeField] private PlayerStats targetPlayer;

        void Start()
        {
            // Register debug commands
            DebugLogConsole.AddCommand("poop", "Trigger poop for the target player", TriggerPoop);
            DebugLogConsole.AddCommand("spawn-cryptid", "Spawn a cryptid near the target player", TriggerSpawnCryptid);
            DebugLogConsole.AddCommand("cryptid-chase-me", "Make the nearest cryptid chase the target player", TriggerCryptidChase);
            DebugLogConsole.AddCommand("tp-ship", "Teleport the target player to the ship", TriggerTeleportToShip);
            DebugLogConsole.AddCommand("tp-shearth", "Teleport the ship to the earth position", TriggerTeleportShipToEarth);
        }

        void Update()
        {

        }

        [ConsoleMethod("poop", "Triggers poop for the specified player")]
        public static void TriggerPoop()
        {
            // Find the DebugCommands instance
            DebugCommands instance = FindFirstObjectByType<DebugCommands>();
            if (instance == null)
            {
                Debug.LogError("DebugCommands instance not found!");
                return;
            }

            if (instance.targetPlayer == null)
            {
                Debug.LogError("Target player not set in DebugCommands!");
                return;
            }

            instance.targetPlayer.Poop();
            Debug.Log($"Triggered poop for {instance.targetPlayer.name}");
        }

        [ConsoleMethod("spawn-cryptid", "Spawns a cryptid near the target player")]
        public static void TriggerSpawnCryptid()
        {
            // Find the DebugCommands instance
            DebugCommands instance = FindFirstObjectByType<DebugCommands>();
            if (instance == null)
            {
                Debug.LogError("DebugCommands instance not found!");
                return;
            }

            if (instance.targetPlayer == null)
            {
                Debug.LogError("Target player not set in DebugCommands!");
                return;
            }

            instance.targetPlayer.SpawnCryptid();
            Debug.Log($"Triggered cryptid spawn for {instance.targetPlayer.name}");
        }

        [ConsoleMethod("cryptid-chase-me", "Makes the nearest cryptid chase the target player")]
        public static void TriggerCryptidChase()
        {
            // Find the DebugCommands instance
            DebugCommands instance = FindFirstObjectByType<DebugCommands>();
            if (instance == null)
            {
                Debug.LogError("DebugCommands instance not found!");
                return;
            }

            if (instance.targetPlayer == null)
            {
                Debug.LogError("Target player not set in DebugCommands!");
                return;
            }

            // Find the nearest cryptid
            GameObject[] cryptids = GameObject.FindGameObjectsWithTag("Cryptid");
            if (cryptids.Length == 0)
            {
                Debug.LogError("No cryptids found in the scene!");
                return;
            }

            GameObject nearestCryptid = null;
            float nearestDistance = float.MaxValue;
            Vector3 playerPosition = instance.targetPlayer.transform.position;

            foreach (GameObject cryptid in cryptids)
            {
                float distance = Vector3.Distance(playerPosition, cryptid.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCryptid = cryptid;
                }
            }

            if (nearestCryptid != null)
            {
                // Make the cryptid chase the player
                CryptidController cryptidController = nearestCryptid.GetComponent<CryptidController>();
                if (cryptidController != null)
                {
                    cryptidController.StartChasing(instance.targetPlayer.transform);
                    Debug.Log($"Made cryptid chase {instance.targetPlayer.name}");
                }
                else
                {
                    Debug.LogError("Cryptid does not have CryptidController component!");
                }
            }
        }

        [ConsoleMethod("tp-ship", "Teleports the target player to the ship")]
        public static void TriggerTeleportToShip()
        {
            // Find the DebugCommands instance
            DebugCommands instance = FindFirstObjectByType<DebugCommands>();
            if (instance == null)
            {
                Debug.LogError("DebugCommands instance not found!");
                return;
            }

            if (instance.targetPlayer == null)
            {
                Debug.LogError("Target player not set in DebugCommands!");
                return;
            }

            // Find the ship by tag
            GameObject ship = GameObject.FindGameObjectWithTag("Ship");
            if (ship == null)
            {
                Debug.LogError("No ship found in the scene with tag 'Ship'!");
                return;
            }

            instance.targetPlayer.TeleportToShip(ship.transform);
            Debug.Log($"Teleported {instance.targetPlayer.name} to ship at {ship.transform.position}");
        }

        [ConsoleMethod("tp-shearth", "Teleports the ship to the earth position")]
        public static void TriggerTeleportShipToEarth()
        {
            // Find the ship by tag
            GameObject ship = GameObject.FindGameObjectWithTag("Ship");
            if (ship == null)
            {
                Debug.LogError("No ship found in the scene with tag 'Ship'!");
                return;
            }

            // Get the ship's GravitasSpaceshipSubject component
            GravitasSpaceshipSubject shipSubject = ship.GetComponent<GravitasSpaceshipSubject>();
            if (shipSubject == null)
            {
                Debug.LogError("Ship does not have a GravitasSpaceshipSubject component!");
                return;
            }

            // Use the network-aware teleport method
            shipSubject.TeleportToEarth();
            Debug.Log("Ship teleport to earth command sent");
        }
    }
}
