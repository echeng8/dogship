using System;
using System.Collections.Generic;
using UnityEngine;
using Coherence.Toolkit;

namespace Gravitas
{
    /// <summary>
    /// Implementation of a subject that can enter and exit Gravitas fields.
    /// 
    /// NETWORKING:
    /// This class supports Coherence networking to synchronize the current field across all clients.
    /// - The client with authority over the subject controls which field it's in
    /// - Field changes are synchronized via the [Sync] networkedCurrentFieldTransform property
    /// - Non-authoritative clients will automatically match their subject's field to the networked field
    /// - Requires a CoherenceSync component on the GameObject for networking support
    /// </summary>
    [AddComponentMenu("Gravitas/Gravitas Subject")]
    [DisallowMultipleComponent]
    public class GravitasSubject : MonoBehaviour, IGravitasSubject
    {
        public virtual IGravitasBody GravitasBody => gravitasBody;
        public virtual IGravitasField CurrentField { get; private set; }
        public virtual GameObject GameObject => gameObject;
        public Vector3 AbsoluteAngularVelocity
            => GravitasBody.AngularVelocity + (CurrentField?.FieldAngularVelocity ?? Vector3.zero);
        public Vector3 AbsoluteVelocity
            => GravitasBody.Velocity + (CurrentField?.FieldVelocity ?? Vector3.zero);
        public float OrientSpeed => orientSpeed;
        public bool AutoOrient => autoOrient;
        public virtual bool CanChangeField => !isChangingField && fieldChangeTimer >= fieldChangeDelay;
        public bool WillReorient => willReorient;

        public event Action<IGravitasField>
            OnEnterField,
            OnExitField;

        protected IGravitasBody gravitasBody;

        // Networking: Coherence sync for current field
        private CoherenceSync coherenceSync;
        [Sync] public Transform networkedCurrentFieldTransform;

        private readonly HashSet<GravitasFieldChangeRequest> fieldChangeRequests = new HashSet<GravitasFieldChangeRequest>();
        [SerializeField]
        private float
            fieldChangeDelay = 0.5f,
            orientSpeed = 80f,
            reorientDelay = 2f;
        private float
            fieldChangeTimer,
            reorientTimer;
        [SerializeField] private bool autoOrient;
        [SerializeField] private bool willReorient;
        private bool isChangingField;

        public void EnqueueFieldChangeRequest(GravitasFieldChangeRequest fieldChangeRequest)
        {
            if (!fieldChangeRequests.Contains(fieldChangeRequest))
            {
                fieldChangeRequests.Add(fieldChangeRequest);
            }
        }

        public virtual void EnterField(IGravitasField field)
        {
            if (field == null) { return; }

#if GRAVITAS_LOGGING
            if (GravitasDebugLogger.CanLog(GravitasDebugLoggingFlags.SubjectEnterExitVelocity))
                GravitasDebugLogger.Log($"\"{name}\" Enter Velocity: {gravitasBody.Velocity} Enter Angular Velocity: {gravitasBody.AngularVelocity}");
#endif

            CurrentField = field;
            willReorient = autoOrient && CurrentField.FixedDirection != FixedDirection.None;

            OnEnterField?.Invoke(field);

            isChangingField = false;
            fieldChangeTimer = 0;

            // Update networked field if we have authority
            if (HasAuthorityOverSubject())
            {
                UpdateNetworkedField();
            }
        }

        public virtual void ExitField(IGravitasField field)
        {
            if (field == null) { return; }

#if GRAVITAS_LOGGING
            if (GravitasDebugLogger.CanLog(GravitasDebugLoggingFlags.SubjectEnterExitVelocity))
                GravitasDebugLogger.Log($"\"{name}\" Exit Velocity: {gravitasBody.Velocity} Exit Angular Velocity: {gravitasBody.AngularVelocity}");
#endif

            bool foundNewField = CheckPositionForNewField(out IGravitasField newField);

            CurrentField = null;

            OnExitField?.Invoke(field);

            isChangingField = false;
            fieldChangeTimer = 0;

            // Update networked field if we have authority
            if (HasAuthorityOverSubject())
            {
                UpdateNetworkedField();
            }

            // Attempt move to different field
            if (foundNewField && !fieldChangeRequests.Contains(new GravitasFieldChangeRequest(newField)))
            {
                newField.EnqueueSubjectChange(this, true);
            }
        }

        public void SetReorientTimer(float time)
            => reorientTimer = time;

        protected virtual void OnSubjectAwake()
        {
            if (!TryGetComponent(out gravitasBody))
            {
                Debug.LogWarning($@"Gravitas: No GravitasBody able to be located on subject ""{gameObject.name}""");
                DestroyImmediate(this);

                return;
            }

            // Get CoherenceSync component for networking
            if (!TryGetComponent(out coherenceSync))
            {
                Debug.LogWarning($@"Gravitas: No CoherenceSync component found on subject ""{gameObject.name}"". Field changes will not be networked.");
            }
        }

        protected virtual void OnSubjectUpdate()
        {
            if (willReorient && reorientTimer < reorientDelay)
            {
                reorientTimer += Time.deltaTime;
            }
            else if (willReorient && reorientTimer >= reorientDelay && Vector3.Angle(transform.up, Vector3.up) > 0.1f)
            {
                gravitasBody.Orient(Vector3.up, orientSpeed);
            }

            // Update networked field if we have authority and field has changed
            UpdateNetworkedField();

            if (!isChangingField)
            {
                if (!CanChangeField)
                {
                    fieldChangeTimer += Time.deltaTime;
                }
                else
                {
                    ProcessFieldChangeRequests(fieldChangeRequests);
                    fieldChangeRequests.Clear();
                }
            }
        }

        protected virtual void OnSubjectFixedUpdate() { }

        /// <summary>
        /// Updates the networked field Transform if this client has authority over the subject
        /// </summary>
        protected virtual void UpdateNetworkedField()
        {
            // Only the client with authority can update the field
            if (coherenceSync != null && HasAuthorityOverSubject())
            {
                Transform currentFieldTransform = GetCurrentFieldTransform();
                if (networkedCurrentFieldTransform != currentFieldTransform)
                {
                    networkedCurrentFieldTransform = currentFieldTransform;
                }
            }
            else
            {
                // If we don't have authority, apply the networked field from the authoritative client
                ApplyNetworkedField();
            }
        }

        /// <summary>
        /// Checks if this client has authority over the subject
        /// </summary>
        protected virtual bool HasAuthorityOverSubject()
        {
            if (coherenceSync == null) return true; // No networking, assume local authority

            // If not connected, assume local authority
            if (!coherenceSync.CoherenceBridge.IsConnected)
                return true;

            // In coherence, check if we have state authority over this entity
            return coherenceSync.HasStateAuthority;
        }

        /// <summary>
        /// Gets the Transform of the current field, or null if no field
        /// </summary>
        protected virtual Transform GetCurrentFieldTransform()
        {
            return CurrentField?.GameObject?.transform;
        }

        /// <summary>
        /// Applies the field from the network if we don't have authority
        /// </summary>
        protected virtual void ApplyNetworkedField()
        {
            if (networkedCurrentFieldTransform == null)
            {
                // No field set over network
                if (CurrentField != null)
                {
                    // Exit current field
                    CurrentField.DestroySubjectFromField(this);
                }
                return;
            }

            // Find the field with the networked Transform
            IGravitasField targetField = FindFieldByTransform(networkedCurrentFieldTransform);

            if (targetField != null && targetField != CurrentField)
            {
                // Switch to the networked field
                targetField.EnqueueSubjectChange(this, true);
            }
        }

        /// <summary>
        /// Finds a field by its Transform
        /// </summary>
        protected virtual IGravitasField FindFieldByTransform(Transform fieldTransform)
        {
            if (fieldTransform == null) return null;

            // Try to get the GravitasField component directly from the Transform
            if (fieldTransform.TryGetComponent<GravitasField>(out var field))
            {
                return field;
            }

            // Search all active GravitasField components for matching Transform
            foreach (var gravField in FindObjectsOfType<GravitasField>())
            {
                if (gravField.GameObject.transform == fieldTransform)
                {
                    return gravField;
                }
            }

            Debug.LogError($"No field found with Transform: {fieldTransform.name}");
            return null;
        }

        protected virtual void ProcessFieldChangeRequests(in HashSet<GravitasFieldChangeRequest> requests)
        {
            if (requests.Count > 0)
            {
                GravitasFieldChangeRequest selectedRequest = default;
                foreach (GravitasFieldChangeRequest request in requests)
                {
                    if
                    (
                        request.field != null &&
                        !request.field.ContainsSubject(this) &&
                        request.FieldPriority >= selectedRequest.FieldPriority
                    )
                    {
                        selectedRequest = request;
                    }
                }

                IGravitasField newField = selectedRequest.field;

                // Only process field changes if we have authority
                if (newField != null && (CurrentField == null || newField.Priority > CurrentField.Priority))
                {
                    // Check authority before changing fields
                    if (HasAuthorityOverSubject())
                    {
                        newField.EnqueueSubjectChange(this, true);
                        isChangingField = true;
                    }
                    else
                    {
                        // If we don't have authority, don't change fields locally
                        // The authoritative client will handle the change and sync it
#if GRAVITAS_LOGGING
                        if (GravitasDebugLogger.CanLog(GravitasDebugLoggingFlags.FieldChanging))
                            GravitasDebugLogger.Log($"Subject \"{gameObject.name}\" attempted field change without authority - ignoring");
#endif
                    }
                }
            }
        }

        protected void Awake()
        {
            fieldChangeTimer = fieldChangeDelay;

            OnSubjectAwake();
        }

        protected void Update()
        {
            OnSubjectUpdate();
        }

        protected void FixedUpdate()
        {
            OnSubjectFixedUpdate();
        }

        protected bool CheckPositionForNewField(out IGravitasField field)
        {
            field = null;

            if (!this) { return false; }

            foreach (Collider col in Physics.OverlapBox
            (
                transform.position,
                Vector3.one * 2f,
                transform.rotation,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide
            ))
            {
                if
                (
                    !col.transform.IsChildOf(transform) &&
                    col.TryGetComponent(out IGravitasField newField) &&
                    !ReferenceEquals(newField, CurrentField) &&
                    (field == null || newField.Priority > field.Priority)
                )
                {
                    field = newField;
                }
            }

            return field != null;
        }
    }
}
