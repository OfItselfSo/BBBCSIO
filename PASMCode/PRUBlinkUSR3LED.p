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
///  PRUBlinkUSR3LED.p  - a PASM assembly language program, intended to run in
///                   the Beaglebone Black PRU and which will blink the USR3 
///                   LED a specified number of times once per second. 
///
///                   This code is intended for demonstration purposes 
///                   and hence has loops which may be confusing to the 
///                   novice unrolled and some code sections purposly 
///                   duplicated to make the structure as simple and linear
///                   as possible. It deliberately uses no include files and 
///                   contains rather more comments than one usually finds in 
///                   assembler source.
///
///                   Compile with the command
///                      pasm -b PRUBlinkUSR3LED.p
/// 
///                   This code was written as part of the BBBCSIO library
///                   which is designed to provide C#/Mono access to the 
///                   OCP Devices and PRU on a Beaglebone Black. Since this
///                   code is in PASM assembler, it is not really related to
///                   C#/Mono and you could compile and then run it 
///                   using any language which can load a binary into the 
///                   Beaglebone Black PRU.
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

// the number of times we blink the LED
#define NUMBER_OF_BLINKS 10

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

        MOV r0, NUMBER_OF_BLINKS            // this is the number of times we wish to blink          

        // there are three loops in this code. The outermost loop defined by the BLINK
        // label executes once for each on/off cycle of the LED. L1 and L2 are half second
        // delay loops

BLINK:  MOV R1, 50000000                    // set up for a half second delay

        // perform a half second delay
L1:     SUB R1, R1, 1                       // subtract 1 from R1
        QBNE L1, R1, 0                      // is R1 == 0? if no, then goto L1

        // now we turn the LED3 on
        MOV R2, GPIO1_LED3BIT               // reload R2 with the USR3 LEDs enable/disable bit
        MOV R3, GPIO_BANK1+GPIO_SETDATAOUT  // load the address to we wish to set. Note that the
                                            // operation GPIO_BANK1+GPIO_SETDATAOUT is performed
                                            // by the assembler at compile time and the resulting  
                                            // constant value is used. The addition is NOT done 
                                            // at runtime by the PRU!
        SBBO R2, R3, 0, 4                   // write the contents of R2 out to the memory address
                                            // contained in R3. Use no offset from that address
                                            // and copy all 4 bytes of R2

        // perform a half second delay. Note the delay works because of the determinate nature
        // of the execution times of PRU instructions. Instructions that do not reference data
        // RAM take 5ns. We have two instructions in our loop (the SUB and the QBNE). If you do the
        // math on that you find that a value of 50000000 gives you a half second delay. Of course
        // we are ignoring the time taken by all the other instructions because the small amount of
        // time they take does not add much error. One can conceive of applications where one might
        // have to take note of such things and compensate.

        MOV R1, 50000000                    // set up for a half second delay
L2:     SUB R1, R1, 1                       // subtract 1 from R1
        QBNE L2, R1, 0                      // is R1 == 0? if no, then goto L2

        // now we turn the LED3 off again
        MOV R2, GPIO1_LED3BIT               // reload R2 with the USR3 LEDs enable/disable bit
        MOV R3, GPIO_BANK1+GPIO_CLEARDATAOUT// load the address we wish to write to. Note that every
                                            // bit that is a 1 will turn off the associated GPIO
                                            // we do NOT write a 0 to turn it off. 0's are simply ignored.
        SBBO R2, R3, 0, 4                   // write the contents of R2 out to the memory address
                                            // contained in R3. Use no offset from that address
                                            // and copy all 4 bytes of R2

        // the bottom of the BLINK loop. Note that R0 contains the number of remaining blinks. It
        // is UP TO YOU to remember that and not use R0 for something else in the code above. There 
        // is no safety net in assembler :-)
        SUB R0, R0, 1
        QBNE BLINK, R0, 0

        // if we get here we have blinked the led as many times as we need to. 

        HALT                                // stop the PRU
