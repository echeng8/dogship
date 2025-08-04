using System;

namespace Gravitas
{
    /// <summary>
    /// A set of enum flags to control debug logging of different events.
    /// </summary>
    [Flags]
    [Serializable]
    public enum GravitasDebugLoggingFlags : byte
    {
        None = 0,

        FieldChanging = 2, // Controls debug logging when a subject changes field
        FieldStartScan = 4, // Controls debug logging the results of start scanning fields

        PlayerInteraction = 8, // Controls debug logging all player interaction related events

        SubjectEnterExitVelocity = 16, // Controls logging the subject's entry or exit velocity from changing fields
    }
}
