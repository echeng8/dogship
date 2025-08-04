using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gravitas
{
    /// <summary>
    /// Object representing a physics scene, providing access to the underlying Unity PhysicsScene and update methods.
    /// </summary>
    [AddComponentMenu(null)]
    [DisallowMultipleComponent]
    public class GravitasPhysicsScene : MonoBehaviour
    {
        private Action<IGravitasSubject> subjectRemovalAction;
        private readonly Dictionary<GravitasSubjectProxy, IGravitasSubject> subjectProxies = new();
        private readonly List<Collider> fieldColliders = new List<Collider>();
        private PhysicsScene physicsScene;
        private Transform originTransform;

        public static GravitasPhysicsScene CreateGravitasPhysicsScene(IGravitasField field, bool addChildColliders)
        {
            if (field == null) { return null; }

            GameObject originGO = new GameObject("Origin")
            {
                isStatic = true
            };

            Collider boundaryCollider = field.BoundaryCollider;
            originGO
                .CopyCollider(boundaryCollider, field.PhysicsSceneBoundaryScaleFactor)
                .ScaleColliderSize(boundaryCollider.transform);

            GravitasPhysicsScene gravitasPhysicsScene = originGO.AddComponent<GravitasPhysicsScene>();

            if (addChildColliders) // Creating child colliders in physics scene
            {
                gravitasPhysicsScene.fieldColliders.Clear();
                CreateColliderCopiesRecursively
                (
                    field.GameObject.transform,
                    originGO.transform,
                    gravitasPhysicsScene.fieldColliders
                );
            }

            gravitasPhysicsScene.subjectRemovalAction = (subject) => field.EnqueueSubjectChange(subject, false);
            gravitasPhysicsScene.physicsScene = GravitasSceneManager.CreatePhysicsScene
            (
                gravitasPhysicsScene,
                originGO,
                field.GameObject.name
            );
            gravitasPhysicsScene.originTransform = originGO.transform;

            return gravitasPhysicsScene;
        }

        private static void CreateColliderCopiesRecursively
        (
            in Transform source,
            in Transform parent,
            in List<Collider> fieldColliders
        )
        {
            foreach (Transform child in source)
            {
                if (child.TryGetComponent(out Collider col) && !col.isTrigger)
                {
                    fieldColliders.Add(col);

                    GameObject childColliderGO = new GameObject($"{col.name}")
                    {
                        isStatic = true
                    };

                    childColliderGO.transform.SetParent(parent);
                    childColliderGO.transform.SetLocalPositionAndRotation
                    (
                        col.transform.localPosition,
                        col.transform.localRotation
                    );
                    childColliderGO.transform.localScale = col.transform.lossyScale;

                    childColliderGO.CopyCollider(col);

                    CreateColliderCopiesRecursively(child, childColliderGO.transform, fieldColliders);
                }
                else
                {
                    CreateColliderCopiesRecursively(child, parent, fieldColliders);
                }
            }
        }

        public GravitasSubjectProxy ProxySubjectInPhysicsScene(IGravitasSubject subject)
        {
            IGravitasBody gravitasBody = subject.GravitasBody;
            if (gravitasBody != null)
            {
                GravitasSubjectProxy proxy = GravitasSubjectProxy.CreateProxy(gravitasBody);
                gravitasBody.SetProxy(proxy);

                foreach (Collider bodyCollider in gravitasBody.GetBodyColliders())
                {
                    foreach (Collider fieldCollider in fieldColliders)
                    {
                        Physics.IgnoreCollision(bodyCollider, fieldCollider, true);
                    }
                }

                subjectProxies.Add(proxy, subject);

                GravitasSceneManager.MoveGameObjectToPhysicsScene(proxy.gameObject, this);

                proxy.transform.SetParent(originTransform);

                return proxy;
            }

            return null;
        }

        /// <summary>
        /// Main Physics update loop for this physics scene's simulation.
        /// </summary>
        /// <param name="timeStep">The timestep of the physics update, the rate at which physics has occured</param>
        public void UpdateSimulation(float timeStep)
        {
            if (timeStep >= 0 && physicsScene.IsValid())
                physicsScene.Simulate(timeStep);
        }

        /// <summary>
        /// Performs a raycast into this physics scene.
        /// </summary>
        /// <param name="pos">Origin of the raycast</param>
        /// <param name="dir">Direction of the raycast</param>
        /// <param name="hitInfo">The assigned RaycastHit object, if successful</param>
        /// <param name="distance">Maximum distance of the raycast</param>
        /// <param name="layerMask">LayerMask to be used for the raycast</param>
        /// <param name="queryTriggerInteraction">Raycast interaction with triggers</param>
        /// <returns>bool If the raycast hit something or not</returns>
        public bool PhysicsSceneRaycast
        (
            Vector3 pos,
            Vector3 dir,
            out RaycastHit hitInfo,
            float distance,
            int layerMask,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore
        )
        {
            hitInfo = default;

            return
                physicsScene != null &&
                physicsScene.Raycast
                (
                    pos,
                    dir,
                    out hitInfo,
                    distance,
                    layerMask,
                    queryTriggerInteraction
                );
        }

        void OnTriggerEnter(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;

            if
            (
                rb.TryGetComponentInChildren(out GravitasSubjectProxy proxy) &&
                subjectProxies.ContainsKey(proxy)
            )
            {
                proxy.MarkColliderAsOutOfBounds(other, false);
            }
        }

        void OnTriggerExit(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;

            if
            (
                rb.TryGetComponentInChildren(out GravitasSubjectProxy proxy) &&
                subjectProxies.ContainsKey(proxy)
            )
            {
                proxy.MarkColliderAsOutOfBounds(other, true);

                if (proxy.IsOutOfBounds && subjectProxies.Remove(proxy, out IGravitasSubject subject))
                {
                    foreach (Collider bodyCollider in subject.GravitasBody.GetBodyColliders())
                    {
                        foreach (Collider fieldCollider in fieldColliders)
                        {
                            Physics.IgnoreCollision(bodyCollider, fieldCollider, false);
                        }
                    }
                    subjectRemovalAction?.Invoke(subject);
                }
            }
        }
    }
}
