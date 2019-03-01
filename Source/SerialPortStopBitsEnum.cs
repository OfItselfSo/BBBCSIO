using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the possible stop bits for the bytes transmitted
    /// via the Serial ports on the Beaglebone Black
    /// </summary>
    /// <history>
    ///    31 Aug 15  Cynic - Originally written
    /// </history>
    public enum SerialPortStopBitsEnum
    {
        STOPBITS_ONE,
        STOPBITS_TWO
    }
}

