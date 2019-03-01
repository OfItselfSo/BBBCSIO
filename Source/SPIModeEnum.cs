using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the SPI modes on the Beaglebone Black. See 
    /// http://en.wikipedia.org/wiki/Serial_Peripheral_Interface_Bus
    /// </summary>
    /// <history>
    ///    21 Dec 14  Cynic - Originally written
    /// </history>
    [Flags]
    public enum SPIModeEnum : uint
    {
        SPI_CPHA       = 0x01,                    // clock phase 
        SPI_CPOL       = 0x02,                    // clock polarity 
        SPI_MODE_0     = (0|0),                   // (original MicroWire) 
        SPI_MODE_1     = (0|SPI_CPHA),
        SPI_MODE_2     = (SPI_CPOL|0),
        SPI_MODE_3     = (SPI_CPOL|SPI_CPHA),
        
        /* Not tested. May or may not work on the BBB
        SPI_CS_HIGH    = 0x04,                    // chipselect active high? 
        SPI_LSB_FIRST  = 0x08,                    // per-word bits-on-wire 
        SPI_3WIRE      = 0x10,                    // SI/SO signals shared 
        SPI_LOOP       = 0x20,                    // loopback mode 
        SPI_NO_CS      = 0x40,                    // 1 dev/bus, no chipselect 
        SPI_READY      = 0x80,                    // slave pulls low to pause 
        SPI_TX_DUAL    = 0x100,                   // transmit with 2 wires 
        SPI_TX_QUAD    = 0x200,                   // transmit with 4 wires
        SPI_RX_DUAL    = 0x400,                   // receive with 2 wires 
        SPI_RX_QUAD    = 0x800,                   // receive with 4 wires 
        */
    }
}

