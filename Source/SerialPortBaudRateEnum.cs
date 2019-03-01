using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the possible Baud rates for the bytes transmitted
    /// via the Serial ports on the Beaglebone Black
    /// </summary>
    /// <history>
    ///    31 Aug 15  Cynic - Originally written
    /// </history>
    public enum SerialPortBaudRateEnum
    {
        BAUDRATE_0,
        BAUDRATE_50,
        BAUDRATE_75,
        BAUDRATE_110,
        BAUDRATE_134,
        BAUDRATE_150,
        BAUDRATE_200,
        BAUDRATE_300,
        BAUDRATE_600,
        BAUDRATE_1200,
        BAUDRATE_1800,
        BAUDRATE_2400,
        BAUDRATE_4800,
        BAUDRATE_9600,
        BAUDRATE_19200,
        BAUDRATE_38400,
        BAUDRATE_57600,
        BAUDRATE_115200,
        BAUDRATE_230400,
        BAUDRATE_460800,
    }
}

