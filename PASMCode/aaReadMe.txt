About the PASM Code Files

 This directory contains sample PASM source code and compiled binaries which demonstrate various techniques of interacting  
 with the Beaglebone Black Programable Realtime Units (PRUs). Although this code is intended to interact with the PRU_Driver 
 class of the BBBCSIO library, much of this code is generic and could reasonably be launched and used by any PRU capable 
 driver software.
 
 The example files are written in the free and open source PASM Assembler and are extensively commented. 
 
 More discussion and examples can be found at 
    http://www.ofitselfso.com/BBBCSIO/Help/BBBCSIOHelp_PRUDriverExamples.html
	http://www.ofitselfso.com/BBBCSIO/BBBCSIO.php
	
 PRUBlinkUSR3LED.p 
    A simple example which blinks the USR3 LED. 

 PRUDataInOut.p
    An example of how to transfer data from a user space program to the PRU for processing and how to receive the returned 
	  data. 

 PRUInterruptHost.p
	An example of how to trigger interrupts in a PRU to send signals to a user space program. The PRU can send trigger 
	  interrupts to send signals to user space programs.  Note that the routing of interrupts from the PRU requires 
	  considerable configuration of internal PRU registers and that different drivers do this in different ways. This 
	  version is structured to work with the BBBCSIO PRU_Driver class.

 PRUPinInOut.p
    An example of how to use the PRU for direct I/O. The PRU can directly read and write the state of certain pins on the 
	  Beaglebone Black's P8 and P9 headers. This example code demonstrates this process. 

  