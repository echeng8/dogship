using System;

namespace Gravitas
{
    /// <summary>A simple struct encapsulating a request to a subject to enter a field.</summary>
    public readonly struct GravitasFieldChangeRequest : IEquatable<GravitasFieldChangeRequest>
    {
        /// <summary>The priority of the assigned field.</summary>
        public readonly int FieldPriority => field?.Priority ?? int.MinValue;

        /// <summary>The field requesting the change.</summary>
        public readonly IGravitasField field;

        public GravitasFieldChangeRequest(IGravitasField _field)
        {
            field = _field;
        }

        public bool Equals(GravitasFieldChangeRequest other)
        {
            return ReferenceEquals(other.field, field);
        }
        public override bool Equals(object obj)
        {
            return
                obj is GravitasFieldChangeRequest gravitasFieldChangeRequest &&
                Equals(gravitasFieldChangeRequest);
        }

        public override int GetHashCode()
        {
            return field.GetHashCode();
        }
    }
}
