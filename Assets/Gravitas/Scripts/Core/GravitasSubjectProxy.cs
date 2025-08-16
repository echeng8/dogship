using System.Collections.Generic;
using UnityEngine;

namespace Gravitas
{
    /// <summary>A physics proxy of a subject.</summary>
    [AddComponentMenu(null)]
    [DisallowMultipleComponent]
    public sealed class GravitasSubjectProxy : MonoBehaviour
    {
        /// <summary>Gets or sets the proxy's currently used rigidbody.</summary>
        public Rigidbody ProxyRigidbody
        {
            get => proxyRigidbody;
            set
            {
                proxyRigidbody = value;

                if (proxyRigidbody)
                {
                    foreach (Collider collider in proxyRigidbody.GetComponentsInChildren<Collider>(false))
                    {
                        outOfBoundsMap[collider] = false;
                    }
                }
            }
        }
        /// <summary>Whether or not this subject has left the bounds of the field it is in.</summary>
        public bool IsOutOfBounds
        {
            get
            {
                foreach (bool value in outOfBoundsMap.Values)
                {
                    if (!value) { return false; }
                }

                return true;
            }
        }

        private readonly Dictionary<Collider, bool> outOfBoundsMap = new Dictionary<Collider, bool>();
        private Rigidbody proxyRigidbody;

        /// <summary>Creates and returns a proxy of the given body.</summary>
        /// <param name="gravitasBody">The body to proxy.</param>
        /// <returns><c>GravitasSubjectProxy</c> The created proxy.</returns>
        public static GravitasSubjectProxy CreateProxy(in IGravitasBody gravitasBody)
        {
            if (gravitasBody == null) { return null; }

            // Creating the initial proxy gameobject
            GameObject
                bodyGO = gravitasBody.Rigidbody.gameObject,
                proxyGO = new GameObject(bodyGO.name)
                {
                    layer = bodyGO.layer
                };
            proxyGO.transform.localScale = bodyGO.transform.localScale;

            Collider[] bodyColliders = gravitasBody.GetBodyColliders();
            if (bodyColliders != null)
            {
                foreach (Collider col in bodyColliders) // Creating all subject colliders for proxy
                {
                    if (col != null && col.gameObject.activeInHierarchy && col.enabled && !col.isTrigger)
                    {
                        if (!col.gameObject.Equals(bodyGO)) // Create child collider 
                        {
                            GameObject childInstance = new GameObject()
                            {
                                layer = col.gameObject.layer,
                                name = col.name,
                                tag = col.gameObject.tag
                            };

                            childInstance.transform.SetParent(proxyGO.transform);
                            childInstance.transform.SetLocalPositionAndRotation
                            (
                                bodyGO.transform.InverseTransformPoint(col.transform.position),
                                Quaternion.Inverse(bodyGO.transform.rotation) * col.transform.rotation
                            );
                            childInstance.transform.localScale = bodyGO.transform.InverseTransformVector
                            (
                                col.transform.localToWorldMatrix * col.transform.localScale
                            );

                            childInstance.CopyCollider(col);
                        }
                        else // Create on root gameobject
                        {
                            proxyGO.CopyCollider(col);
                        }
                    }
                }
            }

            // Creating a copy of the subject's rigidbody on the proxy
            Rigidbody proxyRb = proxyGO.CopyRigidbody(gravitasBody.Rigidbody, false, false);
            proxyRb.centerOfMass = proxyGO.transform.InverseTransformPoint(proxyGO.transform.position);

            GravitasSubjectProxy proxy = proxyGO.AddComponent<GravitasSubjectProxy>();
            proxy.ProxyRigidbody = proxyRb;

            return proxy;
        }

        /// <summary>Marks the given collider of this subject as in or out of bounds of the field it is in.</summary>
        /// <param name="col">The collider to mark.</param>
        /// <param name="isOutOfBounds">Whether or not this collider is out of bounds.</param>
        public void MarkColliderAsOutOfBounds(Collider col, bool isOutOfBounds)
        {
            if (outOfBoundsMap.ContainsKey(col))
                outOfBoundsMap[col] = isOutOfBounds;
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (ProxyRigidbody)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transform.position, transform.position + ProxyRigidbody.linearVelocity);
            }
        }
        #endif
    }
}