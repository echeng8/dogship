using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gravitas
{
    /// <summary>
    /// Main management class for controlling Gravitas physics simulation and behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    public class GravitasManager : MonoBehaviour
    {
        public static int FieldsLayerMask
        {
            get
            {
                if (instance) { return 1 << instance.fieldsLayer; }

                return 0;
            }
        }

        // A list of currently active fields to update
        private static readonly IComparer<IGravitasField> fieldPriorityComparer = Comparer<IGravitasField>.Create
        (
            (a, b) => (b?.Priority ?? 0).CompareTo(a?.Priority ?? 0)
        );
        private static readonly List<IGravitasField> fieldsToUpdate = new List<IGravitasField>();
        // A queue for removing fields to avoid modifying the main fields collection
        private static readonly Queue<IGravitasField> fieldRemovalQueue = new Queue<IGravitasField>();
        private static GravitasManager instance; // Static instance, will belong to the scene

        [SerializeField]
        [Tooltip("Controls the rate of time when simulating physics scenes, don't usually need to change this unless you have a use for it")]
        private float physicsSceneTimescale = 1f; // The scale at which the Gravitas physics simulation updates
        [SerializeField] private int fieldsLayer = 7;

        /// <summary>
        /// Registers the given field to have its physics simulation updated.
        /// </summary>
        /// <param name="field">The field to register for updates</param>
        public static void RegisterFieldForUpdates(IGravitasField field)
        {
            if (!fieldsToUpdate.Contains(field))
            {
                fieldsToUpdate.Add(field);
                fieldsToUpdate.Sort(fieldPriorityComparer);
            }
        }

        /// <summary>
        /// Add the given field to the removal queue, removing on the next frame.
        /// </summary>
        /// <param name="field">The field to be removed</param>
        public static void QueueFieldForUpdateRemoval(IGravitasField field)
        {
            if (fieldsToUpdate.Contains(field) && !fieldRemovalQueue.Contains(field))
                fieldRemovalQueue.Enqueue(field);
        }

        /// <summary>
        /// Unload every field and scene registered. Used when unloading scene.
        /// </summary>
        public static void UnloadAllFields()
        {
            if (fieldsToUpdate != null)
            {
                foreach (IGravitasField field in fieldsToUpdate)
                {
                    field?.UnloadPhysicsScene();
                }

                fieldsToUpdate.Clear();
            }

            fieldRemovalQueue?.Clear();
        }

        void Awake()
        {
            // Assigning the one manager instance
            if (!instance) { instance = this; }
            else
            {
                Destroy(this);

                return;
            }

            UnloadAllFields();
        }

        void Start()
        {
            GravitasField[] fields = FindObjectsOfType<GravitasField>(false);
            Array.Sort(fields, fieldPriorityComparer);
            // Forcing registry of all active and visible fields on startup to avoid missing any
            foreach (GravitasField field in fields)
            {
                if (field)
                {
                    fieldsToUpdate.Add(field);

                    #if GRAVITAS_LOGGING
                    if (GravitasDebugLogger.CanLog(GravitasDebugLoggingFlags.FieldStartScan))
                        GravitasDebugLogger.Log($"Start scanning field \"{field.name}\"...");
                    #endif

                    field.StartScan();
                }
            }
        }

        void Update()
        {
            UnloadFields(); // Queued field removal

            static void UnloadFields()
            {
                while (fieldRemovalQueue.TryDequeue(out IGravitasField field))
                {
                    fieldsToUpdate.Remove(field);
                    field.UnloadPhysicsScene();
                }
            }
        }

        void LateUpdate()
        {
            // Position updating of subjects, performed every frame after main update loop
            for (int i = fieldsToUpdate.Count - 1; i >= 0; i--)
            {
                fieldsToUpdate[i]?.UpdateSubjectPositions();
            }
        }

        void FixedUpdate()
        {
            // Proxy force updating and then physics simulation of each field
            float timeStep = Time.fixedDeltaTime * physicsSceneTimescale;

            if (fieldsToUpdate != null)
            {
                for (int i = 0; i < fieldsToUpdate.Count; i++)
                {
                    IGravitasField field = fieldsToUpdate[i];
                    if (field != null)
                    {
                        field.UpdatePhysicsSceneProxies();
                        field.UpdatePhysicsSceneSimulation(timeStep);

                        field.FlushSubjectChanges();
                    }
                }
            }
        }
    }
}
