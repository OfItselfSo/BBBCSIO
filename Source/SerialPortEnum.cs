using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the possible Serial ports on the Beaglebone Black
    /// </summary>
    /// <history>
    ///    31 Aug 15  Cynic - Originally written
    /// </history>
    public enum SerialPortEnum
    {
        UART_NONE,
        // UART_0,  serial console
        UART_1,          
        UART_2,          
        // UART_3,  not exposed on headers         
        UART_4,          
        UART_5         
    }
}

