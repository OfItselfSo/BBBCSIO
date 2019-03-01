using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the possible parity bit for the bytes transmitted
    /// via the Serial ports on the Beaglebone Black
    /// </summary>
    /// <history>
    ///    31 Aug 15  Cynic - Originally written
    /// </history>
    public enum SerialPortParityEnum
    {
        PARITY_NONE,
        PARITY_ODD,
        PARITY_EVEN
    }
}

