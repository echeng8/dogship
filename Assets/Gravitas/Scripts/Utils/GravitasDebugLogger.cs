using System;
using UnityEngine;

namespace Gravitas
{
    public class GravitasDebugLogger : MonoBehaviour
    {
        public Action<string> LogAction { private get; set; }

        private static GravitasDebugLogger instance;

        [SerializeField] private GravitasDebugLoggingFlags debugLoggingFlags;

        public static void Log(string message)
        {
            if (instance)
                instance.LogAction?.Invoke($"[Gravitas]: {message}");
        }

        public static bool CanLog(GravitasDebugLoggingFlags debugLoggingFlags)
        {
            return
                instance &&
                (instance.debugLoggingFlags & debugLoggingFlags) != 0;
        }

        void Awake()
        {
            if (instance == null)
            {
                instance = this;

                #if UNITY_EDITOR
                LogAction = Debug.Log;
                #endif
            }
            else
            {
                Destroy(this);
            }
        }
    }
}
