using UnityEngine;
using IngameDebugConsole;

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
    }
}
