using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the possible SPI Slave devices on the Beaglebone Black
    /// </summary>
    /// <history>
    ///    21 Dec 14  Cynic - Originally written
    /// </history>
    public enum SPISlaveDeviceEnum
    {
        SPI_SLAVEDEVICE_NONE,
        SPI_SLAVEDEVICE_CS0,   // the slave device on the CS0 pin
        SPI_SLAVEDEVICE_CS1,   // the slave device on the CS1 pin
        SPI_SLAVEDEVICE_GPIO   // the slave uses a GPIO pin
    }
}

