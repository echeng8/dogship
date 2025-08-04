using UnityEngine;

namespace Gravitas
{
    /// <summary>
    /// Utility class defining useful extension methods for Gravitas operations.
    /// </summary>
    public static class GravitasExtensions
    {
        public static void ScaleColliderSize(this Collider col, Transform t)
        {
            Vector3 scale = t.localScale;

            switch (col)
            {
                case BoxCollider boxCollider:
                    Vector3 size = boxCollider.size;
                    Matrix4x4 scaleMatrix = Matrix4x4.Scale(scale);

                    boxCollider.center = scaleMatrix.MultiplyPoint3x4(boxCollider.center);
                    boxCollider.size = new Vector3
                    (
                        size.x * scale.x,
                        size.y * scale.y,
                        size.z * scale.z
                    );

                    break;

                case CapsuleCollider capsuleCollider:
                    capsuleCollider.height = capsuleCollider.direction switch
                    {
                        0 => capsuleCollider.height * t.localScale.x,
                        1 => capsuleCollider.height * t.localScale.y,
                        2 => capsuleCollider.height * t.localScale.z,

                        _ => capsuleCollider.height
                    };

                    float radius = capsuleCollider.radius;
                    capsuleCollider.radius = radius * Mathf.Max(scale.x, scale.y, scale.z);

                    break;

                case SphereCollider sphereCollider:
                    radius = sphereCollider.radius;
                    sphereCollider.radius = radius * Mathf.Max(scale.x, scale.y, scale.z);

                    break;
            }
        }

        /// <summary>
        /// Creates a new Collider component on the given GameObject, copying relevant settings over.
        /// </summary>
        /// <param name="go">The GameObject target</param>
        /// <param name="col">The Collider to copy type and settings from</param>
        /// <returns>Collider The created collider copy </returns>
        public static Collider CopyCollider(this GameObject go, Collider col, float colliderScale = 1.0f)
        {
            switch (col)
            {
                case BoxCollider boxCollider:
                    BoxCollider newBoxCollidier = go.AddComponent<BoxCollider>();

                    newBoxCollidier.center = boxCollider.center;
                    newBoxCollidier.contactOffset = boxCollider.contactOffset;
                    newBoxCollidier.isTrigger = boxCollider.isTrigger;
                    newBoxCollidier.sharedMaterial = boxCollider.sharedMaterial;
                    newBoxCollidier.size = boxCollider.size * colliderScale;

                    return newBoxCollidier;

                case CapsuleCollider capsuleCollider:
                    CapsuleCollider newCapsuleCollider = go.AddComponent<CapsuleCollider>();

                    newCapsuleCollider.center = capsuleCollider.center;
                    newCapsuleCollider.contactOffset = capsuleCollider.contactOffset;
                    newCapsuleCollider.direction = capsuleCollider.direction;
                    newCapsuleCollider.height = capsuleCollider.height * colliderScale;
                    newCapsuleCollider.isTrigger = capsuleCollider.isTrigger;
                    newCapsuleCollider.sharedMaterial = capsuleCollider.sharedMaterial;
                    newCapsuleCollider.radius = capsuleCollider.radius * colliderScale;

                    return newCapsuleCollider;

                case MeshCollider meshCollider:
                    MeshCollider newMeshCollider = go.AddComponent<MeshCollider>();

                    newMeshCollider.contactOffset = meshCollider.contactOffset;
                    newMeshCollider.convex = meshCollider.convex;
                    newMeshCollider.isTrigger = meshCollider.isTrigger;
                    newMeshCollider.sharedMaterial = meshCollider.sharedMaterial;
                    newMeshCollider.sharedMesh = meshCollider.sharedMesh;

                    return newMeshCollider;

                case SphereCollider sphereCollider:
                    SphereCollider newSphereCollider = (SphereCollider)go.AddComponent(typeof(SphereCollider));

                    newSphereCollider.center = sphereCollider.center;
                    newSphereCollider.contactOffset = sphereCollider.contactOffset;
                    newSphereCollider.isTrigger = sphereCollider.isTrigger;
                    newSphereCollider.sharedMaterial = sphereCollider.sharedMaterial;
                    newSphereCollider.radius = sphereCollider.radius * colliderScale;

                    return newSphereCollider;

                default: return null;
            }
        }

        /// <summary>
        /// Creates a new Rigidbody component on the given GameObject, copying relevant settings over.
        /// </summary>
        /// <param name="go">The GameObject target</param>
        /// <param name="rb">The Rigidbody to copy settings from</param>
        /// <param name="isKinematic">Optional force isKinematic setting on created Rigidbody, defaults to true</param>
        /// <param name="useGravity">Optional force useGravity setting on created Rigidbody, defaults to true</param>
        /// <returns>Rigidbody The created rigidbody copy</returns>
        public static Rigidbody CopyRigidbody(this GameObject go, Rigidbody rb, bool isKinematic = true, bool useGravity = true)
        {
            if (!go.TryGetComponent(out Rigidbody newRb))
                newRb = go.AddComponent<Rigidbody>();

            newRb.angularDrag = rb.angularDrag;
            newRb.collisionDetectionMode = rb.collisionDetectionMode;
            newRb.constraints = rb.constraints;
            newRb.drag = rb.drag;
            newRb.isKinematic = isKinematic && rb.isKinematic;
            newRb.mass = rb.mass;
            newRb.useGravity = useGravity && rb.useGravity;

            return newRb;
        }

        /// <summary>
        /// Returns the corresponding vector representation of the fixed direction type
        /// </summary>
        /// <param name="fixedDirection">The FixedDirection value to convert</param>
        /// <returns>Vector3 The corresponding vector</returns>
        public static Vector3 AsVector(this FixedDirection fixedDirection) => fixedDirection switch
        {
            FixedDirection.PositiveX => Vector3.right,
            FixedDirection.NegativeX => Vector3.left,

            FixedDirection.PositiveY => Vector3.up,
            FixedDirection.NegativeY => Vector3.down,

            FixedDirection.PositiveZ => Vector3.forward,
            FixedDirection.NegativeZ => Vector3.back,

            _ => Vector3.zero
        };

        public static Vector3 TransformPointUnscaled(this Transform t, Vector3 worldPos)
        {
            if (t == null) { return Vector3.zero; }

            Matrix4x4 m = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);

            return m.MultiplyPoint3x4(worldPos);
        }

        public static Vector3 TransformVectorUnscaled(this Transform t, Vector3 v)
        {
            if (t == null) { return Vector3.zero; }

            Matrix4x4 m = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);

            return m.MultiplyVector(v);
        }

        public static Vector3 InverseTransformPointUnscaled(this Transform t, Vector3 localPos)
        {
            if (t == null) { return Vector3.zero; }

            Matrix4x4 m = Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;

            return m.MultiplyPoint3x4(localPos);
        }

        public static Vector3 InverseTransformVectorUnscaled(this Transform t, Vector3 v)
        {
            if (t == null) { return Vector3.zero; }

            Matrix4x4 m = Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;

            return m.MultiplyVector(v);
        }

        public static bool HasLayer(this GameObject go, LayerMask layerMask)
        {
            return (1 << go.layer & layerMask) != 0;
        }

        public static bool TryGetComponentInChildren<T>(this Component c, out T t) where T : Component
        {
            if (c)
                t = c.GetComponentInChildren<T>();
            else
                t = null;

            return (bool)t;
        }
    }
}
