using System;
using UnityEngine;

namespace Gravitas
{
    /// <summary>Represents an implementation of a gravity field that manages and simulates subjects.</summary>
    public interface IGravitasField
    {
        /// <summary>The trigger collider defining where this field starts and ends.</summary>
        public Collider BoundaryCollider { get; }
        /// <summary>Gets or sets the fixed gravitational acceleration direction of this field.</summary>
        public FixedDirection FixedDirection { get; set; }
        /// <summary>The GameObject this field implementation is associated with.</summary>
        public GameObject GameObject { get; }
        /// <summary>The absolute world space angular velocity of this field.</summary>
        public Vector3 FieldAbsoluteAngularVelocity { get; }
        /// <summary>The absolute world space velocity of this field.</summary>
        public Vector3 FieldAbsoluteVelocity { get; }
        /// <summary>The angular velocity of the field relative to its frame of reference.</summary>
        public Vector3 FieldAngularVelocity { get; }
        /// <summary>The velocity of the field relative to its frame of reference.</summary>
        public Vector3 FieldVelocity { get; }
        /// <summary>The center of this gravity field.</summary>
        public Vector3 LocalFieldCenter { get; }
        /// <summary>The magnitude of acceleration this field exerts on subjects.</summary>
        public float Acceleration { get; }
        /// <summary>The factor to scale the exit boundary collider by in a local physics scene.</summary>
        public float PhysicsSceneBoundaryScaleFactor { get; }
        /// <summary>The priority this field should be considered with.</summary>
        public int Priority { get; }

        public event Action<IGravitasSubject>
            OnSubjectAdded,
            OnSubjectRemoved;

        /// <summary>Proxies the given subject in this field.</summary>
        /// <param name="subject">The subject to add.</param>
        void AddSubjectToField(IGravitasSubject subject);

        /// <summary>Destroys the given subject's proxy from this field.</summary>
        /// <param name="subject">The subject to destroy.</param>
        void DestroySubjectFromField(IGravitasSubject subject);

        /// <summary>Enqueues a change to the field's subject to be processed at the next field update.</summary>
        /// <param name="subject">The subject to add or remove.</param>
        /// <param name="add">Whether to add or remove the subject.</param>
        void EnqueueSubjectChange(IGravitasSubject subject, bool add);

        /// <summary>Processes and performs all subject changes for this field.</summary>
        void FlushSubjectChanges();

        /// <summary>Performs the startup scan for this field.</summary>
        void StartScan();

        /// <summary>Unloads this field's physics scene from memory.</summary>
        void UnloadPhysicsScene();

        /// <summary>Apply gravitational forces to the subjects in this field.</summary>
        void UpdatePhysicsSceneProxies();

        /// <summary>Updates the simulation of this field's physics scene by the given time step.</summary>
        /// <param name="timeStep">The amount of time to simulate.</param>
        void UpdatePhysicsSceneSimulation(float timeStep);

        /// <summary>Sync positions between all subjects and their proxies.</summary>
        void UpdateSubjectPositions();

        /// <summary>Gets a collection of colliders used by this field.</summary>
        /// <returns><c>Collider[]</c> The colliders used by this field.</returns>
        Collider[] GetFieldColliders();

        /// <summary>Gets the distance multiplier based on a given local position.</summary>
        /// <param name="localPos">The position relative to the field center.</param>
        /// <returns><c>float</c> The distance multiplier.</returns>
        float GetDistanceMultiplier(Vector3 localPos);
        /// <summary>Gets the distance multiplier based on the given normalized distance from the center.</summary>
        /// <param name="normalizedDistance">The normalized distance representing distance from the field center.</param>
        /// <returns><c>float</c> The distance multiplier.</returns>
        float GetDistanceMultiplier(float normalizedDistance);

        /// <summary>Returns whether or not this field is simulating the given subject.</summary>
        /// <param name="subject">The subject to check.</param>
        /// <returns><c>bool</c> Whether or not this field is simulating the given subject.</returns>
        bool ContainsSubject(IGravitasSubject subject);

        /// <summary>Spawns an object and adds it to this field with synchronized velocity.</summary>
        /// <param name="prefab">The prefab to spawn.</param>
        /// <param name="position">The spawn position.</param>
        /// <param name="rotation">The spawn rotation.</param>
        /// <returns>The spawned GameObject, or null if spawning failed.</returns>
        GameObject SpawnAndAddToField(GameObject prefab, Vector3 position, Quaternion rotation);
    }
}
