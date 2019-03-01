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
///  PRUDataInOut.p - a PASM assembly language program, intended to run in
///                   the Beaglebone Black PRU and which will accept an
///                   integer, double it, and then return it. It implements
///                   a simple semaphore system to coordinate actions with
///                   the user space program. 
///
///                   This code is intended to demonstrate the use of
///                   the PRU data RAM as a means of passing data
///                   between the PRU and a user space program and so
///                   this is probably not the most optimal or practical 
///                   bit of PASM assembly code you will ever see.
///
///                   Compile with the command
///                      pasm -b PRUDataInOut.p
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
///               References to the TRM refer to the AM335x SitaraTM Processors
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

// this defines the data RAM memory location inside the PRUs address space. 
#define PRU_DATARAM 0    // yep, starts at address zero no matter which PRU you are using

        // this label is where the code execution starts
START:

        // Note we do NOT need to enable the OCP master port as was done in the
        // previous example PRUBlinkUSR3LED.p This is because this code does not write
        // directly out to the BBB's userspace memory. It writes to its own data
        // RAM and this is presented to userspace as a MemoryMapped file by the UIO driver.

M1:     MOV       R3, PRU_DATARAM           // put the address of our 8Kb DataRAM space in R3
        MOV       R0, 0                     // clear R0
        LBBO      R0.b3, R3, 4, 1           // our flag byte is at offset 4, we read in that 
        QBEQ      M1, R0, 0                 // if R0 = 0 no data is ready
                                           
        // the flag is not 0, the data is ready from the user space program                                     

        LBBO      r0, r3, 0, 4              // read in the 4 bytes at that address into R0
                                            //   these are the bytes of the integer carefully
                                            //   placed there by the user space program and they
                                            //   should be in the correct order to represent an 
                                            //   integer when read sequentially into the 4
                                            //   bytes of R0. Fortunately the storage order of
                                            //   the bytes of an integer in user space is the 
                                            //   same as the storage order used in the PRU  
        LSL       R0, R0, 1                 // multiply the value in R0 by two and put it back  
        SBBO      r0, r3, 0, 4              // store the newly multiplied integer in R0 back in
                                            //   the memory location pointed at by R3
        MOV       R0, 0                     // reset R0
        SBBO      r0.b3, r3, 4, 1           // store a 0 back in our flag to signal the 
                                            //   operation is complete


        JMP M1                              // loop again
                                           

