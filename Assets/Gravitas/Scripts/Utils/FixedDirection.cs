using System;

namespace Gravitas
{
    /// <summary>
    /// Enum used to define the fixed direction of a gravity field, influencing the direction of the force it applies.
    /// </summary>
    [Serializable]
    public enum FixedDirection
    {
        None,

        PositiveX,
        NegativeX,

        PositiveY,
        NegativeY,

        PositiveZ,
        NegativeZ
    }
}
