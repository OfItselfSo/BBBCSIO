# BBBCSIO

BBBCSIO is a free and open source .NET v4 library which provides a comprehensive C# input/output solution for the Beaglebone Black Mono environment. Using BBBCSIO, you can easily read and write to the GPIO pins (and trigger interrupt events from state changes), launch and interact with PRU programs and control SPI, I2C, PWM, UART and A2D devices. BBBCSIO is intended to be a comprehensive solution for I/O on the Beaglebone Black.

At the current time, BBBCSIO has only been tested on the Beaglebone Black. Most things will probably work on later Beaglebone versions - but it has not been verified and the functionality on earlier versions is unknown.

## Capabilities

- Provides simple and transparent read/write access (including the triggering of events) to the GPIO pins of a Beaglebone Black. A maximum output frequency of about 1.8MHz is possible when using a memory mapped port class and about 1.2Khz when using the SYSFS class.
- The SPI ports are fully supported. It is possible to use GPIO ports to provide a large number of SPI device select lines.
- The I2C ports are fully supported.
- The Serial/UART ports are fully supported.
- The Pulse Width Modulation (PWM) devices are fully supported. The PWM duty cycle can be specified in nano-seconds or as a percentage of the base frequency.
- The Analog to Digital Conversion (A2D) devices are fully supported. A sample rate of about 1000 samples/sec has been observed.
- Full PRU support is included. The BBBCSIO PRU_Driver class can launch PRU binary code and interact with running PRU programs to exchange data or receive interrupt events etc. The PRU can read from or write to I/O lines at speeds of up to 50Mhz.
- Developed on  Debian 9.5 2018-10-07 image freely available from the Beaglebone website.
- Tested with the Mono JIT compiler version 4.6.2 but is probably also compatible with other versions.
- The software is written in C# and a .NET project is included with the source code.

The BBBCSIO Project is open source and released under the MIT License. The home page for this project can be found at [http://www.OfItselfSo.com/BBBCSIO](http://www.OfItselfSo.com/BBBCSIO) and contains a zip file with the compiled dll, help files, manual, sample code and other useful advice and assistance.
