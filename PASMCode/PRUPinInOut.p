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

/// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
/// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
///  PRUPinInOut.p  - a PASM assembly language program, intended to run in
///                   the Beaglebone Black PRU0 and which will monitor an input
///                   for a high or low state and will set an output and the
///                   USR3 LED to reflect that state. R31 bit 14 is used as an 
///                   input. This corresponds to header 8 pin 16. R30 bit 15
///                   is used as the output and this corresponds to header 8 pin 11.
///
///                   If H8_16 goes high, R31 bit 14 will go high. This will cause 
///                   the the USR3 LED will be turned on and R30 bit 15
///                   to also be set high. Similarly when R31.t14 goes low then
///                   the USR3 and R30.t15 will go low. Thus the USR3 LED and the 
///                   output on pin H8_11 will follow the high/low input state of 
///                   the pin on header 8 pin 16. 
///
///                   The choice of header 8 pin 16 as an input and header 8 pin
///                   11 as an output is somewhat arbitrary. Note that not every 
///                   header pin has a corresponding mapping to a bit in the PRU 
///                   R31 (output) and R31 (input) registers and the pins that do
///                   have such mappings are specific either to PRU0 or PRU1 and
///                   also can only ever be inputs or outputs but not both. You will
///                   see such pins named like: pr1_pru0_pru_r31_14. The "pru0" means
///                   it is only available in PRU0, the "r31" means it is an input only
///                   ("r30" would be used if it was an output). The "14" means it is 
///                   mapped to bit 14 in R31. You have to look in the various 
///                   documentation to find out that pr1_pru0_pru_r31_14
///                   will actually be accessible on Header 8 pin 16 if the PinMux
///                   mode for offset 0x038 is set to pinmux mode 6.
///                   
///                   Setting the correct pinmux mode is not as hard as it seems. The mode
///                   is not set in the code below (although it could possibly be done by 
///                   fiddling with the bits in the PinMux OCP device). The mux settings 
///                   were done by creating and compiling an overlay. The links below
///                   may provide assistance
///                  
///                   Example Device Tree Overlays used for this code
///                     http://www.ofitselfso.com/BBBCSIO/Help/BBBCSIOHelp_PRUPinInOutExampleDeviceTreeOverlays.html
///                   A discussion of the Beaglebone Black PinMux and its settings
///                     http://www.ofitselfso.com/BeagleNotes/BeagleboneBlackPinMuxModes.php
///                   How to Use Device Trees To Configure PRU IO Pins
///                     http://www.ofitselfso.com/BeagleNotes/UsingDeviceTreesToConfigurePRUIOPins.php
///                   The P8/P9 Header Pins available as PRU inputs
///                     http://www.ofitselfso.com/BeagleNotes/BeagleboneBlackPRUInputPinMuxModes.pdf
///                   The P8/P9 Header Pins available as PRU outputs
///                     http://www.ofitselfso.com/BeagleNotes/BeagleboneBlackPRUOutputPinMuxModes.pdf
///
///                   Try using P8_16, Input, Pulldown, Mode 6 to generate an overlay for the 
///                   pr1_pru0_pru_r31_14 input pin on header 8 pin 16 and a setting of
///                   P8_11, Output, Pulldown, Mode 6 to generate an overlay for the 
///                   pr1_pru0_pru_r30_15 output pin on header 8 pin 11.
///
///                   Warning! Before you make any configuration changes read the discussion on
///                   the page below to ensure that the pins P8_16 and P8_11 are available for use
///                     http://www.ofitselfso.com/BeagleNotes/UsingDeviceTreesToConfigurePRUIOPins.php
///
///                   Compile with the command
///                      pasm -b PRUPinInOut.p
/// 
///                   This code was written as part of the BBBCSIO library
///                   which is designed to provide C#/Mono access to the 
///                   OCP Devices and PRU on a Beaglebone Black. Since this
///                   code is in PASM assembler, it is not really related to
///                   C#/Mono and you could compile and then run it 
///                   using any language which can load a binary into the 
///                   Beaglebone Black PRU0.
///
///               References to the PRM refer to the AM335x PRU-ICSS Reference Guide
///               References to the TRM refer to the AM335x Sitara Processors
///                   Technical Reference Manual
///            
///               History
///                   05 Jun 15  Cynic - Originally Written
///
///               Home Page
///                   http://www.ofitselfso.com/BBBCSIO/BBBCSIO.php
/// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=

.origin 0
.entrypoint START

// this is the address of the BBB GPIO Bank1 Register. We set bits in special locations in offsets
// here to put a GPIO high or low. It so happens that the BBB USR LED3 is tied to the 24th bit.
// Note GPIO's still have to be enabled in the PINMUX if you want to see the signal on the 
// BBB P8 and P9 Headers. The USR LED3 is usually mux'ed in by default so it is a good one to
// use as an example.
#define GPIO_BANK1 0x4804c000

// set up a value that has a 1 in bit 24. We will later use this to toggle the state of LED3
#define GPIO1_LED3BIT 1<<24

// at this offset various GPIOs are associated with a bit position. Writing a 32 bit value to 
// this offset enables them (sets them high) if there is a 1 in a corresponding bit. A zero 
// in a bit position here is ignored - it does NOT turn the associated GPIO off.
#define GPIO_SETDATAOUT 0x194

// you may think that you turn off (set low) a GPIO by writing a 0 somewhere. After all, you set it
// high with a 1 so why not set it low with a 0. That is NOT the way it works. 
// We set a GPIO low by writing to this offset. In the 32 bit value we write, if a bit is 1 the 
// GPIO goes low. If a bit is 0 it is ignored. Thus we can write the same value (GPIO1_LED3BIT 
// in this example) to two different registers in order to turn it on and then off again.
#define GPIO_CLEARDATAOUT 0x190

// this defines a memory location inside the PRU's address space which we can use to enable
// the PRU to see the BBB's memory as if it were the PRU's memory (and other things too).
#define REGISTER_PRUCFG 0x00026000

        // this label is where the code execution starts
START:

        // Enable the OCP master port, this is what allows the PRU to read/write to the 
        // BBB's main memory (rather than the PRU's own memory). The BBB main memory
        // will be mapped into the PRU address space and you can use it in the PRU as
        // if it belonged to the PRU. Note that the code below will only permit the 
        // PRU to access BBB memory addresses above 0x0008_0000 unless you go to a bit 
        // of extra trouble. This is because below 0x0008_0000 you are hitting the 
        // PRU's own memory regions. See the PRM page 19 Section 3.1.1

        MOV       r3, REGISTER_PRUCFG       // mov the address of the PRUs CFG register into R3
        LBBO      r0, r3, 4, 4              // use R3 to get the contents of the PRUCFG register into R0
        CLR       r0, r0, 4                 // Clear bit 4. This will enable the OCP master port
                                            //     see the PRM page 272 section 10.1.2
        SBBO      r0, r3, 4, 4              // write the modified contents back out

        // Now monitor the state of R31 bit 14, this corresponds to header 8 pin 16
L1:
        QBBS      LED_HIGH, R31.t14         // if the bit is high, jump to LED_HIGH
        QBBC      LED_LOW, R31.t14          // if the bit is low, jump to LED_LOW
        JMP       L1                        // we should never get here unless the bit
                                            // changes between tests

LED_HIGH:
        // turn the LED3 on
        MOV R2, GPIO1_LED3BIT               // reload R2 with the USR3 LEDs enable/disable bit
        MOV R3, GPIO_BANK1+GPIO_SETDATAOUT  // load the address to we wish to set. Note that the
                                            // operation GPIO_BANK1+GPIO_SETDATAOUT is performed
                                            // by the assembler at compile time and the resulting  
                                            // constant value is used. The addition is NOT done 
                                            // at runtime by the PRU!
        SBBO R2, R3, 0, 4                   // write the contents of R2 out to the memory address
                                            // contained in R3. Use no offset from that address
                                            // and copy all 4 bytes of R2
        SET r30.t15                         // R30 bit 15 (header 8 pin 11) also goes high
        JMP  L1                             // test again

LED_LOW:
        // turn the LED3 off
        MOV R2, GPIO1_LED3BIT               // reload R2 with the USR3 LEDs enable/disable bit
        MOV R3, GPIO_BANK1+GPIO_CLEARDATAOUT// load the address we wish to write to. Note that every
                                            // bit that is a 1 will turn off the associated GPIO
                                            // we do NOT write a 0 to turn it off. 0's are simply ignored.
        SBBO R2, R3, 0, 4                   // write the contents of R2 out to the memory address
                                            // contained in R3. Use no offset from that address
                                            // and copy all 4 bytes of R2
        CLR r30.t15                         // R30 bit 15 (header 8 pin 11) also goes low
        JMP       L1                        // test again                                            
