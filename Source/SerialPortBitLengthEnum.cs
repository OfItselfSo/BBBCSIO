using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the possible Bit Length for the bytes transmitted
    /// via the Serial ports on the Beaglebone Black
    /// </summary>
    /// <history>
    ///    31 Aug 15  Cynic - Originally written
    /// </history>
    public enum SerialPortBitLengthEnum
    {
        BITLENGTH_NONE,
        BITLENGTH_5,
        BITLENGTH_6,
        BITLENGTH_7,
        BITLENGTH_8,
    }
}

