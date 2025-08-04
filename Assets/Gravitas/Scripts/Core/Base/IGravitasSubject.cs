using System;
using UnityEngine;

namespace Gravitas
{
    /// <summary>Represents an implementation of a subject that can enter and exit Gravitas fields.</summary>
    public interface IGravitasSubject
    {
        /// <summary>The GameObject this subject implementation is associated with.</summary>
        public GameObject GameObject { get; }
        /// <summary>The body this subject implementation is associated with.</summary>
        public IGravitasBody GravitasBody { get; }
        /// <summary>The field this subject is currently within.</summary>
        public IGravitasField CurrentField { get; }
        /// <summary>The absolute world space angular velocity of this subject's body.</summary>
        public Vector3 AbsoluteAngularVelocity { get; }
        /// <summary>The absolute world space velocity of this subject's body.</summary>
        public Vector3 AbsoluteVelocity { get; }
        /// <summary>The speed at which this subject should orient itself.</summary>
        public float OrientSpeed { get; }
        /// <summary>Whether or not this subject will orient itself to gravitational force acting on it.</summary>
        public bool AutoOrient { get; }
        /// <summary>Whether or not this subject can change fields at the moment.</summary>
        public bool CanChangeField { get; }
        /// <summary>Whether or not this subject will attempt to right itself when it lands.</summary>
        public bool WillReorient { get; }

        public event Action<IGravitasField>
            OnEnterField,
            OnExitField;

        /// <summary>Adds a request to change field to be processed by this subject.</summary>
        /// <param name="fieldChangeRequest">The request to enqueue.</param>
        void EnqueueFieldChangeRequest(GravitasFieldChangeRequest fieldChangeRequest);

        /// <summary>Called when this subject enters the given field.</summary>
        /// <param name="field">The field this subject is entering.</param>
        void EnterField(IGravitasField field);

        /// <summary>Called when this subject exits the given field.</summary>
        /// <param name="field">The field this subject is exiting.</param>
        void ExitField(IGravitasField field);

        /// <summary>Sets the value of this subject's re-orienting timer.</summary>
        /// <param name="time">The time value to set.</param>
        void SetReorientTimer(float time);
    }
}
