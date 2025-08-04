using UnityEngine;

namespace Gravitas
{
    /// <summary>Represents an implementation of a Gravitas body handling physics operations and proxying.</summary>
    public interface IGravitasBody
    {
        /// <summary>The rigidbody currently being used by this body. Either the body's own or a proxy's.</summary>
        public Rigidbody CurrentRigidbody { get; }
        /// <summary>The rigidbody belonging to this body.</summary>
        public Rigidbody Rigidbody { get; }
        /// <summary>The transform currently being used by this body. Either the body's own or a proxy's.</summary>
        public Transform CurrentTransform { get; }
        /// <summary>Gets or sets the angular velocity of the currently used rigidbody.</summary>
        public Vector3 AngularVelocity
        {
            get
            {
                Rigidbody rb = CurrentRigidbody;
                if (rb)
                    return rb.angularVelocity;

                return Vector3.zero;
            }
            set
            {
                Rigidbody rb = CurrentRigidbody;
                if (rb)
                    rb.angularVelocity = value;
            }
        }
        /// <summary>The local position of this body's proxy, if available.</summary>
        public Vector3 ProxyPosition { get; }
        /// <summary>Gets or sets the angular velocity of the currently used rigidbody.</summary>
        public Vector3 Velocity
        {
            get
            {
                Rigidbody rb = CurrentRigidbody;
                if (rb)
                    return rb.velocity;

                return Vector3.zero;
            }
            set
            {
                Rigidbody rb = CurrentRigidbody;
                if (rb)
                    rb.velocity = value;
            }
        }
        /// <summary>Whether or not this body is landed on solid ground.</summary>
        public bool IsLanded { get; set; }
        /// <summary>Whether or not this body currently has a proxy assigned.</summary>
        public bool IsProxied { get; }

        /// <summary>Adds a force to this body, sent to either this body's rigidbody or a proxy's.</summary>
        /// <param name="force">The force to apply to this body.</param>
        /// <param name="forceMode">The type of force to apply.</param>
        void AddForce(Vector3 force, ForceMode forceMode)
        {
            Rigidbody rb = CurrentRigidbody;
            if (rb)
                rb.AddForce(force, forceMode);
        }

        /// <summary>Adds a torque to this body, sent to either this body's rigidbody or a proxy's.</summary>
        /// <param name="torque">The torque to apply to this body.</param>
        /// <param name="forceMode">The type of torque force to apply.</param>
        void AddTorque(Vector3 torque, ForceMode forceMode)
        {
            Rigidbody rb = CurrentRigidbody;
            if (rb)
                rb.AddTorque(torque, forceMode);
        }

        /// <summary>Assigns the given proxy to this body.</summary>
        /// <param name="proxy">The proxy to assign to this body.</param>
        void SetProxy(GravitasSubjectProxy proxy);

        /// <summary>Destroys this body's current proxy.</summary>
        void DestroyProxy();

        /// <summary>Orients the body or its proxy to the given up direction at the given orientation speed.</summary>
        /// <param name="up">The up direction to orient to.</param>
        /// <param name="orientSpeed">The speed at which to orient at.</param>
        void Orient(Vector3 up, float orientSpeed);

        /// <summary>Updates this body's world position to match the proxy's position within the given field transform.</summary>
        /// <param name="fieldTransform">The transform of the field the body's proxy is in.</param>
        void UpdatePosition(Transform fieldTransform);

        /// <summary>Returns a collection of colliders used by this body.</summary>
        /// <returns><c>Collider[]</c> The colliders used by this body</returns>
        Collider[] GetBodyColliders();
    }
}
