using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gravitas
{
    /// <summary>Implementation of a subject that can enter and exit Gravitas fields.</summary>
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

        private readonly HashSet<GravitasFieldChangeRequest> fieldChangeRequests = new HashSet<GravitasFieldChangeRequest>();
        [SerializeField] private float
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

            OnEnterField?.Invoke(field);

            isChangingField = false;
            fieldChangeTimer = 0;
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

            if (!isChangingField)
            {
                if (!CanChangeField) // Updating field change timer
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
                if (newField != null && (CurrentField == null || newField.Priority > CurrentField.Priority))
                {
                    newField.EnqueueSubjectChange(this, true);

                    isChangingField = true;
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
