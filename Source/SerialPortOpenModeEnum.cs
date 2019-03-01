using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the possible read modes of a serial port on the 
    /// Beaglebone Black
    /// 
    /// NOTE: Blocking refers to to the way the serial port code deals
    /// with a request to read more data than is present in the input queue. If
    /// the port is in block mode and more data is requested than is present,
    /// then the call will wait until the required amount of data arrives. In
    /// OPEN_NONBLOCK mode the port will just return what it has.
    /// </summary>
    /// <history>
    ///    31 Aug 15  Cynic - Originally written
    /// </history>
    public enum SerialPortOpenModeEnum
    {
        OPEN_BLOCK,       // reads block (wait) 
        OPEN_NONBLOCK     // reads do not block
    }
}

