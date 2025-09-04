using System;
using System.Collections.Generic;
using UnityEngine;
using Gravitas.Demo;

namespace Gravitas
{
    /// <summary>Implementation of a field that can accept and simulate subjects in a local physics scene.</summary>
    [AddComponentMenu("Gravitas/Gravitas Field")]
    [DisallowMultipleComponent]
    public class GravitasField : MonoBehaviour, IGravitasField
    {
        public Collider BoundaryCollider => boundaryCollider;
        public GameObject GameObject => gameObject;
        public FixedDirection FixedDirection
        {
            get => fixedDirection;
            set => fixedDirection = value;
        }
        public Vector3 FieldAbsoluteAngularVelocity => fieldSubject?.AbsoluteAngularVelocity ?? Vector3.zero;
        public Vector3 FieldAbsoluteVelocity => fieldSubject?.AbsoluteVelocity ?? Vector3.zero;
        public Vector3 FieldAngularVelocity => fieldSubject?.GravitasBody?.AngularVelocity ?? Vector3.zero;
        public Vector3 FieldVelocity => fieldSubject?.GravitasBody?.Velocity ?? Vector3.zero;
        public Vector3 LocalFieldCenter
        {
            get
            {
                Vector3 pos = boundaryCollider ? boundaryCollider.bounds.center : Vector3.zero;

                return transform.InverseTransformPoint(pos);
            }
        }
        public float Acceleration => acceleration;
        public float PhysicsSceneBoundaryScaleFactor => physicsSceneBoundaryScaleFactor;
        public int Priority => priority;

        public event Action<IGravitasSubject>
            OnSubjectAdded,
            OnSubjectRemoved;

        private readonly List<IGravitasSubject> subjects = new List<IGravitasSubject>();
        private readonly Queue<(IGravitasSubject, bool)> subjectChangeQueue = new Queue<(IGravitasSubject, bool)>();
        [SerializeField] private AnimationCurve distanceFalloffCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [SerializeField] private Collider boundaryCollider;
        [SerializeField] private FixedDirection fixedDirection = FixedDirection.None;
        private GravitasPhysicsScene gravitasPhysicsScene;
        [SerializeField] private LayerMask fieldLayerMask = Physics.DefaultRaycastLayers;
        private IGravitasSubject fieldSubject;
        [SerializeField] private float acceleration;
        [SerializeField] private float physicsSceneBoundaryScaleFactor = 1.1f;
        [SerializeField] private bool addChildColliders = true;
        [SerializeField] private int priority;

        public virtual void AddSubjectToField(IGravitasSubject subject)
        {
            if (subject != null && (subject as UnityEngine.Object) && !ContainsSubject(subject))
            {
                if (!gravitasPhysicsScene)
                    gravitasPhysicsScene = GravitasPhysicsScene.CreateGravitasPhysicsScene(this, addChildColliders);

                if (gravitasPhysicsScene)
                {
                    subject.CurrentField?.DestroySubjectFromField(subject);

                    Transform
                        fieldTransform = transform,
                        subjectTransform = subject.GameObject.transform;
                    Vector3
                        angularVelocity = subject.AbsoluteAngularVelocity,
                        fieldAngularVelocity = FieldAbsoluteAngularVelocity,
                        fieldVelocity = FieldAbsoluteVelocity,
                        velocity = subject.AbsoluteVelocity;

                    GravitasSubjectProxy proxy = gravitasPhysicsScene.ProxySubjectInPhysicsScene(subject);

                    proxy.transform.SetLocalPositionAndRotation
                    (
                        fieldTransform.InverseTransformPointUnscaled(subjectTransform.position),
                        Quaternion.LookRotation
                        (
                            fieldTransform.InverseTransformDirection(subjectTransform.forward),
                            fieldTransform.InverseTransformDirection(subjectTransform.up)
                        )
                    );

                    Rigidbody proxyRb = proxy.ProxyRigidbody;
                    proxyRb.angularVelocity = angularVelocity - fieldAngularVelocity;
                    proxyRb.linearVelocity = transform.InverseTransformDirection(velocity - fieldVelocity);

                    subjects.Add(subject);
                    OnSubjectAdded?.Invoke(subject);

                    subject.EnterField(this);

#if GRAVITAS_LOGGING
                    if (GravitasDebugLogger.CanLog(GravitasDebugLoggingFlags.FieldChanging))
                        GravitasDebugLogger.Log($"Added subject \"{subject.GameObject.name}\" to field \"{name}\"");
#endif
                }
            }
        }

        /// <summary>
        /// Removes a subject from the field and cleans up its physics proxy.
        /// </summary>
        /// <param name="subject">The subject to remove.</param>
        public virtual void DestroySubjectFromField(IGravitasSubject subject)
        {
            if (subject != null && ContainsSubject(subject))
            {
                subjects.Remove(subject);
                OnSubjectRemoved?.Invoke(subject);

#if GRAVITAS_LOGGING
                if (GravitasDebugLogger.CanLog(GravitasDebugLoggingFlags.FieldChanging))
                    GravitasDebugLogger.Log($"Removing subject \"{subject.GameObject.name}\" from field \"{name}\"");
#endif

                Rigidbody
                    bodyRb = subject.GravitasBody.Rigidbody,
                    proxyRb = subject.GravitasBody.CurrentRigidbody;

                // Store velocities before destroying proxy
                Vector3 finalAngularVelocity = FieldAbsoluteAngularVelocity + proxyRb.angularVelocity;
                Vector3 finalLinearVelocity = FieldAbsoluteVelocity + transform.TransformDirection(proxyRb.linearVelocity);

                subject.GravitasBody.DestroyProxy();

                // Apply velocities after proxy destruction (when rigidbody is no longer kinematic)
                bodyRb.WakeUp();
                bodyRb.angularVelocity = finalAngularVelocity;
                bodyRb.linearVelocity = finalLinearVelocity;

                subject.ExitField(this);

                if (subjects.Count == 0) { GravitasManager.QueueFieldForUpdateRemoval(this); }
            }
        }

        public virtual void EnqueueSubjectChange(IGravitasSubject subject, bool add)
        {
            // Check if the subject is valid using Unity's null check
            if (subject != null && (subject as UnityEngine.Object))
            {
                subjectChangeQueue.Enqueue((subject, add));

                if (add)
                    GravitasManager.RegisterFieldForUpdates(this);
            }
        }

        public virtual void FlushSubjectChanges()
        {
            while (subjectChangeQueue.TryDequeue(out (IGravitasSubject subject, bool add) result))
            {
                // Check if the subject is destroyed using Unity's null check for MonoBehaviour
                if (result.subject == null || !(result.subject as UnityEngine.Object))
                {
                    continue;
                }

                if (result.add)
                    AddSubjectToField(result.subject);
                else
                    DestroySubjectFromField(result.subject);
            }
        }

        public void StartScan()
        {
            if (!this || !boundaryCollider) { return; }

            Bounds bounds = boundaryCollider.bounds;
            int startAddedSubjects = 0;
            // Overlap checking for subjects
            foreach (Collider col in Physics.OverlapBox
            (
                bounds.center,
                bounds.extents,
                boundaryCollider.transform.rotation,
                fieldLayerMask,
                QueryTriggerInteraction.Collide
            ))
            {
                if
                (
                    col.gameObject == gameObject ||
                    col.transform.IsChildOf(transform) ||
                    transform.IsChildOf(col.transform)
                )
                {
                    continue;
                }

                // Add subject to field immediately
                if
                (
                    col.TryGetComponent(out IGravitasSubject subject) &&
                    (
                        subject.CurrentField == null ||
                        subject.CurrentField.Priority < priority
                    )
                )
                {
                    IGravitasBody subjectBody = subject.GravitasBody;
                    subjectBody.AngularVelocity += FieldAngularVelocity;
                    subjectBody.Velocity += FieldVelocity;

                    AddSubjectToField(subject);

                    startAddedSubjects++;
                }
            }

#if GRAVITAS_LOGGING
            if (startAddedSubjects > 0 && GravitasDebugLogger.CanLog(GravitasDebugLoggingFlags.FieldStartScan))
                GravitasDebugLogger.Log($"Field \"{name}\"  added {startAddedSubjects} subjects on startup");
#endif
        }

        public void UnloadPhysicsScene()
        {
            if (gravitasPhysicsScene)
            {
                GravitasSceneManager.UnloadGravitasPhysicsScene(gravitasPhysicsScene);
                gravitasPhysicsScene = null;
            }
        }

        public void UpdatePhysicsSceneProxies()
        {
            if (gravitasPhysicsScene && subjects != null)
            {
                FixedDirection fixedDirection = FixedDirection;
                Vector3 localFieldCenter = LocalFieldCenter;
                float acceleration = Acceleration;

                // Use reverse iteration to safely remove destroyed subjects during iteration
                for (int i = subjects.Count - 1; i >= 0; i--)
                {
                    IGravitasSubject subject = subjects[i];

                    // Remove destroyed subjects from the list
                    if (subject == null || !(subject as UnityEngine.Object))
                    {
                        subjects.RemoveAt(i);
                        continue;
                    }

                    IGravitasBody subjectBody = subject.GravitasBody;
                    if (subjectBody == null) { continue; }

                    float distanceMultiplier = GetDistanceMultiplier(subjectBody.ProxyPosition);

                    // Force calculation
                    Vector3 dir = localFieldCenter - subjectBody.ProxyPosition;
                    Vector3 force = distanceMultiplier * acceleration * dir.normalized;

                    // Fixed direction force replacement, if applicable
                    if (fixedDirection != FixedDirection.None)
                    {
                        force = distanceMultiplier * acceleration * fixedDirection.AsVector();
                    }
                    // Direction to center raycast checking
                    else if
                    (
                        gravitasPhysicsScene.PhysicsSceneRaycast
                        (
                            subjectBody.ProxyPosition,
                            dir,
                            out RaycastHit dirHit,
                            dir.magnitude,
                            Physics.DefaultRaycastLayers
                        )
                    )
                    {
                        force = -dirHit.normal * acceleration;
                    }

                    bool isLanded =
                        gravitasPhysicsScene.PhysicsSceneRaycast
                        (
                            subjectBody.ProxyPosition,
                            force,
                            out RaycastHit hitInfo,
                            1.25f,
                            Physics.DefaultRaycastLayers
                        ) &&
                        (
                            !hitInfo.rigidbody ||
                            !ReferenceEquals(hitInfo.rigidbody, subjectBody.CurrentRigidbody)
                        );

                    // Skip gravity force if subject is a dashing player
                    bool skipGravity = subject.GameObject.TryGetComponent<GravitasFirstPersonPlayerSubject>(out var player) && player.IsDashing;

                    if (!skipGravity)
                    {
                        subjectBody.AddForce(force, ForceMode.Acceleration);
                    }

                    //Debug.Log($"Object: {subject.GameObject.name}, IsLanded: {isLanded}, WillReorient: {subject.WillReorient}, AutoOrient: {subject.AutoOrient}");

                    // Orientation processing
                    if (isLanded)
                    {
                        //Debug.Log($"GameObject '{subject.GameObject.name}' landed on '{hitInfo.collider.gameObject.name}' - normal: {hitInfo.normal}");
                        if (subject.WillReorient) // Orient to surface normal if re-orientable and landed
                        {
                            subjectBody.Orient(hitInfo.normal, subject.OrientSpeed);
                            subject.SetReorientTimer(0);
                        }

                        subject.GravitasBody.AngularVelocity = Vector3.zero;
                    }
                    else if (subject.AutoOrient)
                    {
                        //Debug.Log($"GameObject '{subject.GameObject.name}' auto-orienting to force direction: {-force.normalized}");
                        subjectBody.Orient(-force.normalized, subject.OrientSpeed);
                        subject.SetReorientTimer(0);
                    }

                    subjectBody.IsLanded = isLanded;
                }
            }
        }

        public void UpdatePhysicsSceneSimulation(float timeStep)
        {
            if (gravitasPhysicsScene)
                gravitasPhysicsScene.UpdateSimulation(timeStep);
        }

        public void UpdateSubjectPositions()
        {
            if (subjects == null) return;

            // Use reverse iteration to safely remove destroyed subjects during iteration
            for (int i = subjects.Count - 1; i >= 0; i--)
            {
                IGravitasSubject subject = subjects[i];

                // Remove destroyed subjects from the list
                if (subject == null || !(subject as UnityEngine.Object))
                {
                    subjects.RemoveAt(i);
                    continue;
                }

                subject.GravitasBody?.UpdatePosition(transform);
            }
        }

        public virtual Collider[] GetFieldColliders()
        {
            return GetComponentsInChildren<Collider>(false);
        }

        public float GetDistanceMultiplier(Vector3 localPos)
        {
            if (fixedDirection != FixedDirection.None)
                return 1;

            return Vector3.Distance(LocalFieldCenter, localPos) / boundaryCollider.bounds.extents.magnitude;
        }
        public float GetDistanceMultiplier(float normalizedDistance)
        {
            if (fixedDirection == FixedDirection.None && distanceFalloffCurve != null)
                return distanceFalloffCurve.Evaluate(normalizedDistance);

            return 1;
        }

        public bool ContainsSubject(IGravitasSubject subject)
        {
            return
                subject != null &&
                (subject as UnityEngine.Object) &&
                subjects != null &&
                subjects.Contains(subject);
        }

        public GameObject SpawnAndAddToField(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            GameObject spawnedObject = Instantiate(prefab, position, rotation);

            if (spawnedObject.TryGetComponent<IGravitasSubject>(out var subject))
            {
                // Set velocity to match the field before adding to field
                IGravitasBody subjectBody = subject.GravitasBody;
                if (subjectBody != null)
                {
                    subjectBody.AngularVelocity = FieldAngularVelocity;
                    subjectBody.Velocity = FieldVelocity;
                }

                EnqueueSubjectChange(subject, true);
                return spawnedObject;
            }

            return spawnedObject;
        }

#if UNITY_EDITOR
        /// <summary>Checks and reports any issues with this field for display in the inspector.</summary>
        /// <param name="errorMessage">The error message result.</param>
        /// <returns><c>int</c> The returned error code, indicating the error severity. </returns>
        public int CheckFieldErrors(out string errorMessage)
        {
            // 0 = No error
            // 1 = Warning
            // 2 = Error

            errorMessage = null;

            if (!boundaryCollider)
            {
                errorMessage = "No boundary collider assigned for field!";

                return 2;
            }
            else if (!Application.isPlaying && !boundaryCollider.isTrigger)
            {
                errorMessage = $"Boundary Collider '{boundaryCollider.name}' is not a trigger. Subjects will be unable to enter field!";

                return 2;
            }


            return 0;
        }
#endif

        protected void Awake()
        {
            // Warnings about unassigned fields required for field to operate
            if (boundaryCollider == null && !TryGetComponent(out boundaryCollider))
            {
                Debug.LogWarning($@"Gravitas: Field ""{gameObject.name}"" has no boundary collider! Cannot operate!");
                DestroyImmediate(this);

                return;
            }
            else if (!boundaryCollider.isTrigger)
            {
                Debug.LogWarning($@"Gravitas: Field ""{gameObject.name}"" boundary collider is not a trigger. Is this intentional?");
                boundaryCollider.isTrigger = true;
            }

            TryGetComponent(out fieldSubject);
        }

        void OnTriggerEnter(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;

            if
            (
                rb &&
                rb.gameObject.HasLayer(fieldLayerMask) &&
                rb.TryGetComponent(out IGravitasSubject subject) &&
                !subjects.Contains(subject)
            )
            {
                subject.EnqueueFieldChangeRequest(new GravitasFieldChangeRequest(this));
            }
        }
    }
}
