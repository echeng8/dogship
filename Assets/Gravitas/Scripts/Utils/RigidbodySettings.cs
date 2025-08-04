using UnityEngine;

namespace Gravitas
{
    public class RigidbodySettings
    {
        private readonly CollisionDetectionMode collisionDetectionMode;
        private readonly RigidbodyConstraints constraints;
        private readonly RigidbodyInterpolation interpolation;
        private readonly float
            angularDrag,
            drag,
            mass,
            maxAngularVelocity,
            maxDepenetrationVelocity,
            sleepThreshold;
        private readonly bool
            detectCollisions,
            freezeRotation,
            isKinematic,
            useGravity;
        private readonly int
            solverIterations,
            solverVelocityIterations;

        public RigidbodySettings(in Rigidbody rigidbody)
        {
            if (!rigidbody) { return; }

            angularDrag = rigidbody.angularDrag;
            collisionDetectionMode = rigidbody.collisionDetectionMode;
            constraints = rigidbody.constraints;
            detectCollisions = rigidbody.detectCollisions;
            drag = rigidbody.drag;
            freezeRotation = rigidbody.freezeRotation;
            interpolation = rigidbody.interpolation;
            isKinematic = rigidbody.isKinematic;
            mass = rigidbody.mass;
            maxAngularVelocity = rigidbody.maxAngularVelocity;
            maxDepenetrationVelocity = rigidbody.maxDepenetrationVelocity;
            sleepThreshold = rigidbody.sleepThreshold;
            solverIterations = rigidbody.solverIterations;
            solverVelocityIterations = rigidbody.solverVelocityIterations;
            useGravity = rigidbody.useGravity;
        }

        public void AssignTo(ref Rigidbody rigidbody)
        {
            if (!rigidbody) { return; }

            rigidbody.isKinematic = isKinematic;

            rigidbody.angularDrag = angularDrag;
            rigidbody.collisionDetectionMode = collisionDetectionMode;
            rigidbody.detectCollisions = detectCollisions;
            rigidbody.drag = drag;
            rigidbody.freezeRotation = freezeRotation;
            rigidbody.interpolation = interpolation;
            rigidbody.mass = mass;
            rigidbody.maxAngularVelocity = maxAngularVelocity;
            rigidbody.maxDepenetrationVelocity = maxDepenetrationVelocity;
            rigidbody.sleepThreshold = sleepThreshold;
            rigidbody.solverIterations = solverIterations;
            rigidbody.solverVelocityIterations = solverVelocityIterations;
            rigidbody.useGravity = useGravity;

            rigidbody.constraints = constraints;
        }
    }
}
