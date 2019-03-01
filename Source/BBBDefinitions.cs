using System;

/// +------------------------------------------------------------------------------------------------------------------------------+
/// |                                                   TERMS OF USE: MIT License                                                  |
/// +------------------------------------------------------------------------------------------------------------------------------|
/// |Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation    |
/// |files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy,    |
/// |modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software|
/// |is furnished to do so, subject to the following conditions:                                                                   |
/// |                                                                                                                              |
/// |The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.|
/// |                                                                                                                              |
/// |THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE          |
/// |WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR         |
/// |COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,   |
/// |ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.                         |
/// +------------------------------------------------------------------------------------------------------------------------------+

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// Provides a central location to define various system values 
    /// for the Beaglebone Black
    /// </summary>
    /// <history>
    ///    28 Aug 14  Cynic - Originally written
    /// </history>
    public static class BBBDefinitions
    {
        // the memory mapped device file on the BBB
        public const string DEVMEM_FILE = @"/dev/mem";
        // the base address of the PCRM config memory
        public const long PCRM_BASE_ADDRESS = 0x44e00000;
        // the base address of the GPMC memory
        public const long GPMC_BASE_ADDRESS = 0x44e10000;
        // the base address of the GPIO PINMUX config registers
        public const long GPIOPINMUX_BASE_ADDRESS = 0x44e10800;
        // the difference between what the gpios in the pinmux 
        // use as an offset and the general GPMC starting address
        public const long GPIOPINMUX_ADDRDIFF = GPIOPINMUX_BASE_ADDRESS-GPMC_BASE_ADDRESS;

        // the base addresses of the ocp GPIO banks
        public const long GPIO0_BASE_ADDRESS = 0x44e07000;
        public const long GPIO1_BASE_ADDRESS = 0x4804c000;
        public const long GPIO2_BASE_ADDRESS = 0x481ac000;
        public const long GPIO3_BASE_ADDRESS = 0x481ae000;
        // the size of memory we map into from the DEVMEM_FILE
        public const long GPIO_MAPSIZE = 0xfff;
        public const long PCRM_MAPSIZE = 0xfff;
        public const long GPIOPINMUX_MAPSIZE = 0xfff;

        public const long GPIO_OE = 0x134;

        // these are the offset within the ocp GPIO registers
        // for the data in and out functionality 
        public const long GPIO_DATAIN = 0x138;
        public const long GPIO_DATAOUT = 0x13C;
        public const long GPIO_SETDATAOUT = 0x194;
        public const long GPIO_CLEARDATAOUT = 0x190;

        // these are the offsets with the PCRM register
        // which control the CM_PER_GPIO?_CLKCTRLs
        public const long PCRM_GPIO1_CLKCTRL = 0xac;
        public const long PCRM_GPIO2_CLKCTRL = 0xb0;
        public const long PCRM_GPIO3_CLKCTRL = 0xb4;

        // the base address of the pinmux.
        public const int PINMUX_BASE_ADDRESS = 0x44e10000;
        // the difference between what the pinmux uses as an offset
        // and what the device tree expects to use
        public const int PINMUX_TO_DT_ADDRDIFF = 0x800;
        // the sysfs base GPIODIR
        public const string SYSFS_GPIODIR = "/sys/class/gpio/";
        // the sysfs export file/device
        public const string SYSFS_GPIOEXPORT = "export";
        // the sysfs unexport file/device
        public const string SYSFS_GPIOUNEXPORT = "unexport";
        // the sysfs direction file/device
        public const string SYSFS_GPIODIRECTION = "direction";
        // the sysfs value file/device
        public const string SYSFS_GPIOVALUE = "value";
        // the base part of the gpio directory created in the
        // SYSFS_GPIODIR once we export the gpio
        public const string SYSFS_GPIODIRNAMEBASE = "gpio";
        // the directory which contains the event devices
        public const string SYSFS_EVENTDIR = "/dev/input/";
        // the name of the files which present events to userspace via sysfs
        public const string SYSFS_EVENTFILEBASENAME = "event";

        // the file which contains the pinmux pins in use
        public const string PINUMUX_PINUMUXPINSFILE = "/sys/kernel/debug/pinctrl/44e10800.pinmux/pinmux-pins";
        // the file which contains the pins information
        public const string PINUMUX_PINSFILE = "/sys/kernel/debug/pinctrl/44e10800.pinmux/pins";

        // the template for the SPIDEV device file
        public const string SPIDEV_FILENAME = "/dev/spidev%device%.%slave%";

        // the template for the I2CDEV device file
        public const string I2CDEV_FILENAME = "/dev/i2c-%port%";

        // the template for the TTY device file
        public const string TTYDEV_FILENAME = "/dev/ttyO%tty%";

        // the template for the IIO A2D converter device file
        public const string A2DIIO_FILENAME = "/sys/bus/iio/devices/iio:device0/in_voltage%port%_raw";

        // the templates for the PWM device files
        public const string PWM_FILENAME_EXPORT = "/sys/class/pwm/export";
        public const string PWM_FILENAME_UNEXPORT = "/sys/class/pwm/unexport";
        public const string PWM_FILENAME_DUTY = "/sys/class/pwm/pwm%port%/duty_ns";
        public const string PWM_FILENAME_PERIOD = "/sys/class/pwm/pwm%port%/period_ns";
        public const string PWM_FILENAME_RUN = "/sys/class/pwm/pwm%port%/run";
        public const string PWM_FILENAME_POLARITY = "/sys/class/pwm/pwm%port%/polarity";

    }
}

