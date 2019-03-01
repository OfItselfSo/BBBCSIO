using System;
﻿using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Mono.Unix;
using Mono.Unix.Native;

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
    /// Memory maps the BBB device memory into our program address space
    /// so we can access it programmatically from C#
    /// 
    /// NOTE: the MemoryMapDevMem class is a singleton class. This means that 
    /// program wide there can ever only be one instantiation of these. If you 
    /// don't know what a singleton class is you should look it up. Otherwise 
    /// you will never really understand how this code works.
    /// 
    /// NOTE: you will notice there are no locks in here. This is deliberate
    ///       for speed. If you hit this code from multiple threads you might
    ///       find odd things happen. Probably best to route all access 
    ///       via a single thread. 
    /// 
    /// </summary>
    /// <history>
    ///    28 Aug 14  Cynic - Originally written
    /// </history>
    public class MemoryMapDevMem :  IDisposable
    {
        // Track whether Dispose has been called. 
        private bool disposed = false;

        // pointers into the gpio banks
        private IntPtr gpio0MappedMem = IntPtr.Zero;
        private IntPtr gpio1MappedMem = IntPtr.Zero;
        private IntPtr gpio2MappedMem = IntPtr.Zero;
        private IntPtr gpio3MappedMem = IntPtr.Zero;

        // threads we use to simulate interrupts
        private Thread gpio0EventThread = null;
        private Thread gpio1EventThread = null;
        private Thread gpio2EventThread = null;
        private Thread gpio3EventThread = null;

        private const int MAX_MSEC_TO_WAIT_FOR_THREADSTART = 1000;
        private const int MAX_MSEC_TO_WAIT_FOR_THREADSTOP = 500;

        // maximum number of interrupt ports we an handle on 
        // any one gpio bank. This is hard coded for now but there
        // is no reason why it couldn't be made dynamic
        private const int MAX_GPIO0BANK_INTERRUPTPORTS = 16;
        private const int MAX_GPIO1BANK_INTERRUPTPORTS = 16;
        private const int MAX_GPIO2BANK_INTERRUPTPORTS = 16;
        private const int MAX_GPIO3BANK_INTERRUPTPORTS = 16;

        // arrays we use to associate multiple interrupt ports
        // with a gpio bank and event thread. Normally one would
        // use a List<> here but we are looking for speed!
        private InterruptPortMM[] gpio0BankInterruptPorts = new InterruptPortMM[MAX_GPIO0BANK_INTERRUPTPORTS];
        private InterruptPortMM[] gpio1BankInterruptPorts = new InterruptPortMM[MAX_GPIO1BANK_INTERRUPTPORTS];
        private InterruptPortMM[] gpio2BankInterruptPorts = new InterruptPortMM[MAX_GPIO2BANK_INTERRUPTPORTS];
        private InterruptPortMM[] gpio3BankInterruptPorts = new InterruptPortMM[MAX_GPIO3BANK_INTERRUPTPORTS];
       
        // flags to enable and disable processing of interrupts on a bank. We
        // mostly use these to disable processing when we are updating the 
        // array of InterruptPorts active on the bank.
        private bool gpio0BankEnabled = true;
        private bool gpio1BankEnabled = true;
        private bool gpio2BankEnabled = true;
        private bool gpio3BankEnabled = true;

        // flags used to close down a GPIO thread gently
        private bool gpio0BankEventThreadMustExit = false;
        private bool gpio1BankEventThreadMustExit = false;
        private bool gpio2BankEventThreadMustExit = false;
        private bool gpio3BankEventThreadMustExit = false;

        // pointers into the pcrm bank
        private IntPtr pcrmMappedMem = IntPtr.Zero;
        // pointers into the gpmc pinmux bank
        private IntPtr pinmuxMappedMem = IntPtr.Zero;

        private static readonly MemoryMapDevMem instance = new MemoryMapDevMem();

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Static Constructor for Singleton
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        static MemoryMapDevMem()
        {
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        private MemoryMapDevMem()
        {
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Finalizer
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        ~MemoryMapDevMem()
        {
             Dispose(false);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the one and only copy of the memory mapped singleton
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public static MemoryMapDevMem Instance
        {
            get
            {
                return instance;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Detects if the memory maps are open. 
        /// </summary>
        /// <returns>True - maps open, False - maps closed</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public bool MemoryMapsAreOpen
        {
            get
            {
                // just check the first one.
                if (gpio0MappedMem != IntPtr.Zero) return true;
                return false;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Opens the memory maps. 
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public void OpenMemoryMaps()
        {
            int fd = -1;

            // we do not re-open the maps if they are already operational
            if (MemoryMapsAreOpen == true) return;

            try
            {
                // open the device memory file 
                fd = Syscall.open (BBBDefinitions.DEVMEM_FILE, OpenFlags.O_RDWR, FilePermissions.DEFFILEMODE);
                if (fd == -1) 
                {
                    throw new Exception ("DEVMEM_FILE did not open");
                }

                // map in the PRCRM memory 
                pcrmMappedMem = Syscall.mmap (IntPtr.Zero, BBBDefinitions.PCRM_MAPSIZE, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, fd, BBBDefinitions.PCRM_BASE_ADDRESS);
                if (pcrmMappedMem == (IntPtr)(-1))
                {
                    throw new IOException ("mmap failed for PCRM Bank " + "(" + BBBDefinitions.PCRM_BASE_ADDRESS + ", " + BBBDefinitions.PCRM_MAPSIZE + ")");
                }

                // map in the pinmux memory 
                pinmuxMappedMem = Syscall.mmap (IntPtr.Zero, BBBDefinitions.GPIOPINMUX_MAPSIZE, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, fd, BBBDefinitions.GPMC_BASE_ADDRESS);
                if (pinmuxMappedMem == (IntPtr)(-1))
                {
                    throw new IOException ("mmap failed for GPIOPINMUX Bank " + "(" + BBBDefinitions.GPIOPINMUX_BASE_ADDRESS + ", " + BBBDefinitions.GPIOPINMUX_MAPSIZE + ")");
                }
                    
                // map in GPIO Bank 0 
                gpio0MappedMem = Syscall.mmap (IntPtr.Zero, BBBDefinitions.GPIO_MAPSIZE, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, fd, BBBDefinitions.GPIO0_BASE_ADDRESS);
                if (gpio0MappedMem == (IntPtr)(-1))
                {
                    throw new IOException ("mmap failed for GPIO Bank 0 " + "(" + BBBDefinitions.GPIO0_BASE_ADDRESS + ", " + BBBDefinitions.GPIO_MAPSIZE + ")");
                }
                // map in GPIO Bank 1 
                gpio1MappedMem = Syscall.mmap (IntPtr.Zero, BBBDefinitions.GPIO_MAPSIZE, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, fd, BBBDefinitions.GPIO1_BASE_ADDRESS);
                if (gpio1MappedMem == (IntPtr)(-1))
                {
                    throw new IOException ("mmap failed for GPIO Bank 1 " + "(" + BBBDefinitions.GPIO1_BASE_ADDRESS + ", " + BBBDefinitions.GPIO_MAPSIZE + ")");
                }
                // map in GPIO Bank 2 
                gpio2MappedMem = Syscall.mmap (IntPtr.Zero, BBBDefinitions.GPIO_MAPSIZE, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, fd, BBBDefinitions.GPIO2_BASE_ADDRESS);
                if (gpio2MappedMem == (IntPtr)(-2))
                {
                    throw new IOException ("mmap failed for GPIO Bank 2 " + "(" + BBBDefinitions.GPIO2_BASE_ADDRESS + ", " + BBBDefinitions.GPIO_MAPSIZE + ")");
                }
                // map in GPIO Bank 3 
                gpio3MappedMem = Syscall.mmap (IntPtr.Zero, BBBDefinitions.GPIO_MAPSIZE, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, fd, BBBDefinitions.GPIO3_BASE_ADDRESS);
                if (gpio3MappedMem == (IntPtr)(-3))
                {
                    throw new IOException ("mmap failed for GPIO Bank 3 " + "(" + BBBDefinitions.GPIO3_BASE_ADDRESS + ", " + BBBDefinitions.GPIO_MAPSIZE + ")");
                }

                // once the memory is mapped the file can be closed.
                if(fd > 0) 
                {
                    Syscall.close(fd);
                    fd = -1;
                }
            }
            catch (Exception ex) 
            {
                // clean up the GPIO maps
                if (gpio0MappedMem != IntPtr.Zero) Syscall.munmap(gpio0MappedMem, BBBDefinitions.GPIO_MAPSIZE);
                gpio0MappedMem = IntPtr.Zero;    
                if (gpio1MappedMem != IntPtr.Zero) Syscall.munmap(gpio1MappedMem, BBBDefinitions.GPIO_MAPSIZE);
                gpio1MappedMem = IntPtr.Zero;
                if (gpio2MappedMem != IntPtr.Zero) Syscall.munmap(gpio2MappedMem, BBBDefinitions.GPIO_MAPSIZE);
                gpio2MappedMem = IntPtr.Zero;
                if (gpio3MappedMem != IntPtr.Zero) Syscall.munmap(gpio3MappedMem, BBBDefinitions.GPIO_MAPSIZE);
                gpio3MappedMem = IntPtr.Zero;

                // clean up the pcrm map
                if (pcrmMappedMem != IntPtr.Zero) Syscall.munmap(pcrmMappedMem, BBBDefinitions.PCRM_MAPSIZE);
                pcrmMappedMem = IntPtr.Zero;

                // clean up the pinmux map
                if (pinmuxMappedMem != IntPtr.Zero) Syscall.munmap(pinmuxMappedMem, BBBDefinitions.GPIOPINMUX_MAPSIZE);
                pinmuxMappedMem = IntPtr.Zero;

                if (fd > 0) Syscall.close(fd);
                throw ex;
            }                
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Close the memory maps. 
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public void CloseMemoryMaps()
        {
            try
            {
                // Close the GPIO banks
                if (gpio0MappedMem != IntPtr.Zero) Syscall.munmap(gpio0MappedMem, BBBDefinitions.GPIO_MAPSIZE);
                gpio0MappedMem = IntPtr.Zero;    
                if (gpio1MappedMem != IntPtr.Zero) Syscall.munmap(gpio1MappedMem, BBBDefinitions.GPIO_MAPSIZE);
                gpio1MappedMem = IntPtr.Zero;
                if (gpio2MappedMem != IntPtr.Zero) Syscall.munmap(gpio2MappedMem, BBBDefinitions.GPIO_MAPSIZE);
                gpio2MappedMem = IntPtr.Zero;
                if (gpio3MappedMem != IntPtr.Zero) Syscall.munmap(gpio3MappedMem, BBBDefinitions.GPIO_MAPSIZE);
                gpio3MappedMem = IntPtr.Zero;

                // clean up the pcrm map
                if (pcrmMappedMem != IntPtr.Zero) Syscall.munmap(pcrmMappedMem, BBBDefinitions.PCRM_MAPSIZE);
                pcrmMappedMem = IntPtr.Zero;

                // clean up the pinmux map
                if (pinmuxMappedMem != IntPtr.Zero) Syscall.munmap(pinmuxMappedMem, BBBDefinitions.GPIOPINMUX_MAPSIZE);
                pinmuxMappedMem = IntPtr.Zero;

            }
            catch {}
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pointer for a memory mapped GPIO bank
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <param name="offsetIntoBank">the offset into the bank to fetch
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        private IntPtr GetMappedGpioBankPointer(GpioConfig gpioCfg, long offsetIntoBank)
        {
            if (gpioCfg == null)
            {
                throw new Exception("GpioConfig is null on call to GetMappedGpioBankPointer");
            }
            // just call this
            return GetMappedGpioBankPointer(gpioCfg.GpioBank, offsetIntoBank);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pointer for a memory mapped GPIO bank
        /// </summary>
        /// <param name="gpioBank">A number 0 to 3 representing the GPIO bank</param>
        /// <param name="offsetIntoBank">the offset into the bank to fetch
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        private IntPtr GetMappedGpioBankPointer(int gpioBank, long offsetIntoBank)
        {
            // operate on the appropriate bank
            if (gpioBank == 0)
            {
                // build a pointer to the mapped bank
                return new IntPtr(gpio0MappedMem.ToInt64() + offsetIntoBank);
            }
            else if (gpioBank == 1)
            {
                // build a pointer to the mapped bank
                return new IntPtr(gpio1MappedMem.ToInt64() + offsetIntoBank);
            }
            else if (gpioBank == 2)
            {
                // build a pointer to the mapped bank
                return new IntPtr(gpio2MappedMem.ToInt64() + offsetIntoBank);
            }
            else if (gpioBank == 3)
            {
                // build a pointer to the mapped bank
                return new IntPtr(gpio3MappedMem.ToInt64() + offsetIntoBank);
            }

            // should never happen
            throw new Exception("Unknown event bank " + gpioBank.ToString() + " on call to GetMappedGpioBankPointer");
        }           

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Sets the direction state (Input, Output) for the GPIO in the banks
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <param name="portDirectionIn">the port direction</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        ///    21 Dec 14  Cynic - Now test for all possibilities of PortDirectionEnum
        /// </history>
        public void SetGpioDirection(GpioConfig gpioCfg, PortDirectionEnum portDirectionIn)
        {
            // build a pointer to the mapped bank
            IntPtr bankPtr = GetMappedGpioBankPointer(gpioCfg, BBBDefinitions.GPIO_OE);
            // get the state now
            int regVal = Marshal.ReadInt32(bankPtr);
            // figure out what we want to do
            if (portDirectionIn == PortDirectionEnum.PORTDIR_OUTPUT)
            {
                // we must clear this GPIO's bit to configure it as output
                regVal &= ~(gpioCfg.GpioMask);
            }
            else if (portDirectionIn == PortDirectionEnum.PORTDIR_INPUT)
            {
                // we must set this GPIO's bit to configure it as an input
                regVal |= gpioCfg.GpioMask;
            }
            else
            {
                // 21 Dec 14 Added, should never happen
                throw new Exception ("unknown port direction:" + portDirectionIn.ToString ());
            }

            // put the register state with the new bit state back
            Marshal.WriteInt32(bankPtr,regVal );
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Sets the state on GPIO
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <param name="pinState">the pin state to set true=high, false = low</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        ///    02 Mar 15  Cynic - fixed bug, harded coded reference to gpio1MappedMem
        ///                       as found by Danny S.
        /// </history>
        public unsafe void WriteGPIOPin(GpioConfig gpioCfg, bool pinState)
        {
            if (gpioCfg == null)
            {
                throw new Exception("GpioConfig is null on call to ReadGPIOPin");
            }

            // switch based on what the user wants to do - set or clear
            if (pinState == false)
            {
                // build a pointer to the mapped bank
                // 02 Mar 15 IntPtr bankPtr = new IntPtr(gpio1MappedMem.ToInt64() + (BBBDefinitions.GPIO_CLEARDATAOUT));
                IntPtr bankPtr = GetMappedGpioBankPointer(gpioCfg ,BBBDefinitions.GPIO_CLEARDATAOUT);
                Marshal.WriteInt32(bankPtr, gpioCfg.GpioMask);
            }
            else
            {
                // build a pointer to the mapped bank
                // 02 Mar 15 IntPtr bankPtr = new IntPtr(gpio1MappedMem.ToInt64() + (BBBDefinitions.GPIO_SETDATAOUT));
                IntPtr bankPtr = GetMappedGpioBankPointer(gpioCfg ,BBBDefinitions.GPIO_SETDATAOUT);
                Marshal.WriteInt32(bankPtr, gpioCfg.GpioMask);
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Reads the state of a GPIO
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <returns>true - the pin is high, false - the pin is low</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public unsafe bool ReadGPIOPin(GpioConfig gpioCfg)
        {
            if (gpioCfg == null)
            {
                throw new Exception("GpioConfig is null on call to ReadGPIOPin");
            }

            // build a pointer to the mapped bank
            IntPtr bankPtr = GetMappedGpioBankPointer(gpioCfg, BBBDefinitions.GPIO_DATAIN);
            // get the register at that pointer
            int regVal = Marshal.ReadInt32(bankPtr);
            // the regVal contains the state of all pins in that bank, extract the GPIO state
            return ((regVal & gpioCfg.GpioMask) != 0);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Detects if a GPIO bank is enabled. Access to an unenabled bank will
        /// throw and exception. Basically the CM_PER_GPIO?_CLKCTRLs must be enabled
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <returns>true - the bank is enabled, false - the bank is not enabled</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public unsafe bool GPIOBankIsEnabled(GpioConfig gpioCfg)
        {
            if (gpioCfg == null)
            {
                throw new Exception("GpioConfig is null on call to GPIOBankIsEnabled");
            }

            long offsetIntoBank = 0;

            // operate on the appropriate bank
            if (gpioCfg.GpioBank == 1)
            {
                offsetIntoBank = BBBDefinitions.PCRM_GPIO1_CLKCTRL;
            }
            else if (gpioCfg.GpioBank == 2)
            {
                offsetIntoBank = BBBDefinitions.PCRM_GPIO2_CLKCTRL;
            }
            else if (gpioCfg.GpioBank == 3)
            {
                offsetIntoBank = BBBDefinitions.PCRM_GPIO3_CLKCTRL;
            }
            else
            {
                // GPIO bank 0 is always enabled.
                return true;
            }
                
            // build a pointer to the mapped bank
            IntPtr bankPtr = new IntPtr(pcrmMappedMem.ToInt64() + offsetIntoBank);
            // get the register at that pointer
            int regVal = Marshal.ReadInt32(bankPtr);
            // the regVal contains the configuration, extract the CLKCTRL state
            // it is in the bottom two bits, 0x02 means clock enabled
            return ((regVal & 0x03) != 0);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Enables a GPIO bank. Access to an unenabled bank will
        /// throw and exception. Basically the CM_PER_GPIO?_CLKCTRLs must be enabled
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public unsafe void EnableGPIOBank(GpioConfig gpioCfg)
        {
            if (gpioCfg == null)
            {
                throw new Exception("GpioConfig is null on call to EnableGPIOBank");
            }

            long offsetIntoBank = 0;

            // operate on the appropriate bank
            if (gpioCfg.GpioBank == 1)
            {
                offsetIntoBank = BBBDefinitions.PCRM_GPIO1_CLKCTRL;
            }
            else if (gpioCfg.GpioBank == 2)
            {
                offsetIntoBank = BBBDefinitions.PCRM_GPIO2_CLKCTRL;
            }
            else if (gpioCfg.GpioBank == 3)
            {
                offsetIntoBank = BBBDefinitions.PCRM_GPIO3_CLKCTRL;
            }
            else
            {
                // GPIO bank 0 is always enabled.
                return;
            }

            // build a pointer to the mapped bank
            IntPtr bankPtr = new IntPtr(pcrmMappedMem.ToInt64() + offsetIntoBank);
            // set the register at that pointer
            Marshal.WriteInt32(bankPtr, 0x02);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pinmux state for a GPIO by reading the appropriate memory
        /// location at the GPIO Pinmux offset
        /// 
        /// Note: there is no SetGPIOPinMuxState. This is because it is not
        ///       possible to set the pinmux in User Mode. The registers
        ///       just ignore any update where the process is not in Kernel mode
        /// 
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <returns>The pinmux setting for that gpio</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public unsafe int GetGPIOPinMuxState(GpioConfig gpioCfg)
        {
            if (gpioCfg == null)
            {
                throw new Exception("GpioConfig is null on call to GetGPIOPinMuxSetting");
            }

            // build a pointer to the mapped bank
            IntPtr bankPtr = new IntPtr(pinmuxMappedMem.ToInt64() + gpioCfg.PinmuxRegisterOffset);
            // get the register at that pointer
            int regVal = Marshal.ReadInt32(bankPtr);
            return regVal;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Ensures the interrupt event thread for an interrupt port is started.
        /// 
        /// </summary>
        /// <param name="intPort">the interrupt port</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public void StartEventThreadForInterruptPort(InterruptPortMM intPort)
        {
            // sanity checks
            if (intPort == null)
            {
                throw new Exception ("Error 10, StartEventThreadForInterruptPort intPort == null");
            }
            GpioConfig gpioCfg = intPort.GpioCfgObject;
            if (gpioCfg == null)
            {
                throw new Exception ("Error 20, StartEventThreadForInterruptPort bad gpio input value");
            }

            // find the proper bank. We have only one event thread per bank
            int gpioBank = gpioCfg.GpioBank;
            if (gpioBank == 0)
            {
                StartEventThreadOnBank(gpioCfg, ref gpio0BankEventThreadMustExit, ref gpio0EventThread, Gpio0EventWorker);
            }
            else if (gpioBank == 1)
            {
                StartEventThreadOnBank(gpioCfg, ref gpio1BankEventThreadMustExit, ref gpio1EventThread, Gpio1EventWorker);
            }
            else if (gpioBank == 2)
            {
                StartEventThreadOnBank(gpioCfg, ref gpio2BankEventThreadMustExit, ref gpio2EventThread, Gpio2EventWorker);
            }
            else if (gpioBank == 3)
            {
                StartEventThreadOnBank(gpioCfg, ref gpio3BankEventThreadMustExit, ref gpio3EventThread, Gpio3EventWorker);
            }
            else
            {
                throw new Exception ("Error 44, StartEventThreadForInterruptPort " + gpioCfg.Gpio.ToString() + ", unknown gpio bank "+ gpioBank.ToString());
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Starts the interrupt event thread for a gpio bank. There is only
        /// one thread per bank. It would be too inefficient to have a separate
        /// thread for each interrupt.
        /// 
        /// If interrupt events are already being processed for this bank (ie another
        /// InterruptPort activated them) this function will just return quietly.
        /// 
        /// NOTE: this uses an action to pass the thread worker function into this
        ///       procedure. If you do not know what the system "Action" or "Func" 
        ///       delegate types are then you should probably look them up.
        /// 
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <param name="mustExitFlag">A reference to the flag that causes the thread worker to gently exit</param>
        /// <param name="eventThread">The event thread variable</param>
        /// <param name="action">The event worker function passed as an Action delegate</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        private void StartEventThreadOnBank(GpioConfig gpioCfg, ref bool mustExitFlag, ref Thread eventThread, Action action)
        {
            if (gpioCfg == null)
            {
                throw new Exception ("Error 10, StartEventThreadOnBank, gpioCfg == null");
            }
            if (action == null)
            {
                throw new Exception ("Error 20, StartEventThreadOnBank, gaction == null");
            }
            // is the event already running? If so, quietly leave
            if ((eventThread != null) && (eventThread.IsAlive == true)) return;
            // we must reset this
            mustExitFlag = false;
            // create the event thread
            eventThread = new Thread(new ThreadStart(action));
            if (eventThread == null)
            {
                throw new Exception ("Error 30, StartEventThreadOnBank " + gpioCfg.Gpio.ToString() + ", failed to create worker");
            }

            // start the thread
            eventThread.Start();
            // wait for it to start
            for(int i=0; i<MAX_MSEC_TO_WAIT_FOR_THREADSTART; i++)
            {
                // if our worker is alive we are good
                if (eventThread.IsAlive == true) break;
                // sleep for a millisecond
                Thread.Sleep (1);
            }
            // test it
            if (eventThread.IsAlive == false)
            {
                throw new Exception ("Error 55, StartEventThreadOnBank " + gpioCfg.Gpio.ToString() + ", failed to start worker");
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Stops the interrupt event thread for a gpio bank. 
        /// 
        /// If interrupt events are being processed for other ports on this bank (ie another
        /// InterruptPort activated them) this function will just leave quietly.
        /// 
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <param name=">mustExitFlag</param>">A reference to a flag that causes the worker
        /// thread to exit</param>
        /// <param name="eventThread">a reference to the event thread variable</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public void StopEventThreadForGPIOBank(GpioConfig gpioCfg, ref bool mustExitFlag, ref Thread eventThread)
        {
            try
            {
                // set the flag
                mustExitFlag = true;
                // wait for it to start
                for(int i=0; i<MAX_MSEC_TO_WAIT_FOR_THREADSTOP; i++)
                {
                    // if our worker is dead we are good
                    if (eventThread.IsAlive == false) return;
                    // sleep for a millisecond
                    Thread.Sleep (1);
                }
                // did not stop nicely when asked, go nuclear on it
                eventThread.Abort();
                eventThread = null;
            }
            catch 
            {
                eventThread = null;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Activates the events monitoring for an interrupt port and makes it operational
        /// </summary>
        /// <param name="intPort">The interrupt port we are activating interrupt events
        /// for</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public void ActivateInterrupt(InterruptPortMM intPort)
        {
            // this code does not need to be particularly fast - so it isn't

            // sanity check
            if (intPort == null)
            {
                throw new Exception ("Error 10, ActivateInterrupt bad InterruptPort input");
            }
            if (intPort.GpioCfgObject == null)
            {
                throw new Exception ("Error 15, ActivateInterrupt bad gpio input value");
            }

            // set the interrupt in the proper bank. We have only one event thread per bank
            int gpioBank = intPort.GpioCfgObject.GpioBank;

            // feed in appropriate ref variables to configure the port on the correct bank
            if (gpioBank == 0)
            {
                ActivateInterruptOnBank(intPort, ref gpio0BankEnabled, ref gpio0BankInterruptPorts);
            }
            else if (gpioBank == 1)
            {
                ActivateInterruptOnBank(intPort, ref gpio1BankEnabled, ref gpio1BankInterruptPorts);
            }
            else if (gpioBank == 2)
            {
                ActivateInterruptOnBank(intPort, ref gpio2BankEnabled, ref gpio2BankInterruptPorts);
            }
            else if (gpioBank == 3)
            {
                ActivateInterruptOnBank(intPort, ref gpio3BankEnabled, ref gpio3BankInterruptPorts);
            }
            else
            {
                throw new Exception ("Error 20, ActivateInterrupt " + intPort.GpioCfgObject.Gpio.ToString() + ", unknown gpio bank "+ gpioBank.ToString());
            }                
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Activates an interrupt port on GPIO bank and makes it operational
        /// </summary>
        /// <param name="intPort">The interrupt port we are activating interrupt events for</param>
        /// <param name="bankEnabledFlag">A reference to the flag which enables and disables interrupts
        /// on that bank</param>
        /// <param name="interruptPortArray">A reference to the interruptPort array for that bank.
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        private void ActivateInterruptOnBank(InterruptPortMM intPort, ref bool bankEnabledFlag, ref InterruptPortMM[] interruptPortArray)
        {
            try
            {
                // stop processing temporarily while we set things
                bankEnabledFlag = false;

                List<InterruptPortMM> portList = new List<InterruptPortMM>();

                // get all existing InterruptPorts into a list
                for(int i=0; i < interruptPortArray.Length; i++)
                {
                    InterruptPortMM tmpPort = interruptPortArray[i];
                    // this can be null - just means empty slot
                    if(tmpPort!=null)
                    {
                        // is the GPIO for this port already registered 
                        // we cannot be having that!
                        if(intPort.GpioID == tmpPort.GpioID)
                        {
                            throw new Exception("ActivateInterruptOnBank intPort.GpioID " + intPort.GpioID.ToString() +" is already registered");
                        }
                        portList.Add(tmpPort);
                    }
                }

                // add the input interrupt port to the list
                portList.Add(intPort);

                // check to see if we have enough slots
                if(portList.Count > interruptPortArray.Length)
                {
                    throw new Exception("ActivateInterruptOnBank intPort.GpioID " + intPort.GpioID.ToString() +" cannot add. Max slots of "+ interruptPortArray.Length.ToString() + " exceeded");
                }

                // we can add, clear all interruptPortArray Slots
                for(int i=0; i < interruptPortArray.Length; i++)
                {
                   interruptPortArray[i] = null;
                }

                // sort the list, highest priority first
                portList.Sort();

                // place the ordered ports back into the array
                for(int i=0; i < interruptPortArray.Length; i++)
                {
                    if(i >= portList.Count) break;
                    interruptPortArray[i] = portList[i];
                }
                    
                // build a pointer to the mapped bank
                IntPtr bankPtr = GetMappedGpioBankPointer(intPort.GpioCfgObject, BBBDefinitions.GPIO_DATAIN);
                // get the register at that pointer - this is the current state of the GPIO
                // we will trigger on any changes to this 
                intPort.LastInterruptRegisterState = Marshal.ReadInt32(bankPtr);
            }
            finally
            {
                // processing finished, the polling can proceed
                bankEnabledFlag = true;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Deactivates the event monitoring for an interrupt port
        /// </summary>
        /// <param name="gpioCfg">A configuration object for the gpio</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public void DeactivateInterrupt(InterruptPortMM intPort)
        {
            // this code does not need to be particularly fast - so it isn't

            // sanity check
            if (intPort == null)
            {
                throw new Exception ("Error 10, DeactivateInterrupt bad InterruptPort input");
            }
            if (intPort.GpioCfgObject == null)
            {
                throw new Exception ("Error 15, DeactivateInterrupt bad gpio input value");
            }

            // set the interrupt in the proper bank. We have only one event thread per bank
            int gpioBank = intPort.GpioCfgObject.GpioBank;

            // feed in appropriate ref variables to configure the port on the correct bank
            if (gpioBank == 0)
            {
                DeactivateInterruptOnBank(intPort, ref gpio0BankEventThreadMustExit, ref gpio0BankEnabled, ref gpio0BankInterruptPorts, ref gpio0EventThread);
            }
            else if (gpioBank == 1)
            {
                DeactivateInterruptOnBank(intPort, ref gpio1BankEventThreadMustExit, ref gpio1BankEnabled, ref gpio1BankInterruptPorts, ref gpio1EventThread);
            }
            else if (gpioBank == 2)
            {
                DeactivateInterruptOnBank(intPort, ref gpio2BankEventThreadMustExit, ref gpio2BankEnabled, ref gpio2BankInterruptPorts, ref gpio2EventThread);
            }
            else if (gpioBank == 3)
            {
                DeactivateInterruptOnBank(intPort, ref gpio3BankEventThreadMustExit, ref gpio3BankEnabled, ref gpio3BankInterruptPorts, ref gpio3EventThread);
            }
            else
            {
                throw new Exception ("Error 20, DeactivateInterrupt " + intPort.GpioCfgObject.Gpio.ToString() + ", unknown gpio bank "+ gpioBank.ToString());
            }                
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Deactivates an interrupt port on GPIO bank 
        /// </summary>
        /// <param name="intPort">The interrupt port we are deactivating interrupt events for</param>
        /// <param name="bankEnabledFlag">A reference to the flag which enables and disables interrupts
        /// on that bank</param>
        /// <param name="interruptPortArray">A reference to the interruptPort array for that bank.
        /// <param name=">mustExitFlag</param>">A reference to a flag that causes the worker
        /// thread to exit</param>
        /// <param name="eventThread">The event thread variable</param>
            /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        private void DeactivateInterruptOnBank(InterruptPortMM intPort, ref bool mustExitFlag, ref bool bankEnabledFlag, ref InterruptPortMM[] interruptPortArray, ref Thread eventThread)
        {
            try
            {
                // stop processing temporarily while we set things
                bankEnabledFlag = false;

                List<InterruptPortMM> portList = new List<InterruptPortMM>();

                // get all existing InterruptPorts into a list
                for(int i=0; i < interruptPortArray.Length; i++)
                {
                    // usually if we hit a null the rest of the array
                    // is empty. However, it will not hurt to continue here
                    if(interruptPortArray[i] == null) continue;

                    // is it the port we wish to remove?
                    if(intPort.GpioID == interruptPortArray[i].GpioID)
                    {
                        // yes it is, do not add it to the port list
                    }
                    else
                    {
                        // no it is not, add it to the port list
                        portList.Add(interruptPortArray[i]);
                    }
                }

                // clear all interruptPortArray Slots
                for(int i=0; i < interruptPortArray.Length; i++)
                {
                    interruptPortArray[i] = null;
                }

                // sort the list, highest priority first
                portList.Sort();

                // place the ordered ports back into the array, the one we wish to 
                // de-activate will not be in the list
                for(int i=0; i < interruptPortArray.Length; i++)
                {
                    if(i >= portList.Count) break;
                    interruptPortArray[i] = portList[i];
                }

                // if there are no other InterruptPorts listening then we 
                // stop the thread
                StopEventThreadForGPIOBank(intPort.GpioCfgObject, ref mustExitFlag, ref eventThread);

            }
            finally
            {
                // processing finished, the polling can proceed
                bankEnabledFlag = true;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// The event worker threads for banks 0,1,2,3. 
        /// 
        ///   This thread will spin continuously and read the data. It will detect
        ///   a change in the appropriate bit position (which corresponds to a gpio
        ///   pin). If there is an InterruptPort registered on that GPIO then the
        ///   SendInterruptEvent() function of that object will be called to 
        ///   process the event and send it on to any listeners.
        /// 
        /// NOTE: these workers are hard coded for each bank. Normally this is bad
        ///   practice and I avoid it. However, speed is important here and I do not
        ///   want to incur the overhead of a function call on each interrupt so the
        ///   common code which could be factored out remains in place.
        /// 
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public void Gpio0EventWorker()
        {
            int count = 0;
            // build a pointer to the mapped bank, do this once
            IntPtr bankPtr = GetMappedGpioBankPointer(0, BBBDefinitions.GPIO_DATAIN);

            // we are continuously computable - we'll be swapped in and
            // out by the kernel
            while (true)
            {
                // have we got a thread termination signal
                if (gpio0BankEventThreadMustExit == true) break;
                // we do not process if this flag is false
                if (gpio0BankEnabled == false) continue;

                // get the GPIO DATAIN register at that pointer
                int currentRegisterState = Marshal.ReadInt32(bankPtr);

                // test each possible interrupt. The array is setup
                // so that a null terminates the testing and the interrupt
                // ports are also ordered in terms of priority when the 
                // array is set (highest priority first)
                for (int i = 0; i < MAX_GPIO0BANK_INTERRUPTPORTS; i++)
                {
                    if (gpio0BankInterruptPorts[i] == null) break;

                    // are we interested in this? the XOR tells us if anything changed from
                    // the last read, the subsequent AND masks out any other GPIOs changes
                    if (((currentRegisterState ^ gpio0BankInterruptPorts[i].LastInterruptRegisterState) & gpio0BankInterruptPorts[i].InterruptMask) != 0)
                    {
                        // yes we are, the state of our register has changed from the last read
                        count++;

                        // check our triggers 
                        if ((gpio0BankInterruptPorts[i].ActiveHighMask & currentRegisterState) != 0)
                        {
                            // we triggered on the changed bit because it is now high. The ActiveHighMask 
                            // will have a bit set in the appropriate position to go non zero on the 
                            // current register state
                            gpio0BankInterruptPorts[i].SendInterruptEvent(1);
                        }
                        else if ((gpio0BankInterruptPorts[i].ActiveLowMask & ~currentRegisterState) != 0)
                        {
                            // we triggered on the changed bit because it is low. The ActiveLowMask 
                            // will have a bit set in the appropriate position to go non zero on the NOTed 
                            // current register state
                            gpio0BankInterruptPorts[i].SendInterruptEvent(0);
                        }

                        // set this now so next time through we know what the last state was
                        gpio0BankInterruptPorts[i].LastInterruptRegisterState = currentRegisterState;

                        // once we have processed an interrupt we do not 
                        // process any more. We exit the for{} loop and try again
                        // the interrupt port array is ordered by priority on setup
                        // exiting now means that the high priority interrupts always 
                        // get cleared before any lower priority interrupt can be 
                        // process. The break below ensures this.
                        break;

                    }
                } // bottom of for (int i = 0; i < MAX_GPIO0BANK_INTERRUPTPORTS; i++)
            } // bottom of while (true)
        }
        public void Gpio1EventWorker()
        {
            int count = 0;
            // build a pointer to the mapped bank, do this once
            IntPtr bankPtr = GetMappedGpioBankPointer(1, BBBDefinitions.GPIO_DATAIN);

            // we are continuously computable - we'll be swapped in and
            // out by the kernel
            while (true)
            {
                // have we got a thread termination signal
                if (gpio1BankEventThreadMustExit == true) break;
                // we do not process if this flag is false
                if (gpio1BankEnabled == false) continue;

                // get the GPIO DATAIN register at that pointer
                int currentRegisterState = Marshal.ReadInt32(bankPtr);

                // test each possible interrupt. The array is setup
                // so that a null terminates the testing and the interrupt
                // ports are also ordered in terms of priority when the 
                // array is set (highest priority first)
                for (int i = 0; i < MAX_GPIO1BANK_INTERRUPTPORTS; i++)
                {
                    if (gpio1BankInterruptPorts[i] == null) break;

                    // are we interested in this? the XOR tells us if anything changed from
                    // the last read, the subsequent AND masks out any other GPIOs changes
                    if (((currentRegisterState ^ gpio1BankInterruptPorts[i].LastInterruptRegisterState) & gpio1BankInterruptPorts[i].InterruptMask) != 0)
                    {
                        // yes we are, the state of our register has changed from the last read
                        count++;

                        // check our triggers 
                        if ((gpio1BankInterruptPorts[i].ActiveHighMask & currentRegisterState) != 0)
                        {
                            // we triggered on the changed bit because it is now high. The ActiveHighMask 
                            // will have a bit set in the appropriate position to go non zero on the 
                            // current register state
                            gpio1BankInterruptPorts[i].SendInterruptEvent(1);
                        }
                        else if ((gpio1BankInterruptPorts[i].ActiveLowMask & ~currentRegisterState) != 0)
                        {
                            // we triggered on the changed bit because it is low. The ActiveLowMask 
                            // will have a bit set in the appropriate position to go non zero on the NOTed 
                            // current register state
                            gpio1BankInterruptPorts[i].SendInterruptEvent(0);
                        }

                        // set this now so next time through we know what the last state was
                        gpio1BankInterruptPorts[i].LastInterruptRegisterState = currentRegisterState;

                        // once we have processed an interrupt we do not 
                        // process any more. We exit the for{} loop and try again
                        // the interrupt port array is ordered by priority on setup
                        // exiting now means that the high priority interrupts always 
                        // get cleared before any lower priority interrupt can be 
                        // process. The break below ensures this.
                        break;

                    }
                } // bottom of for (int i = 0; i < MAX_GPIO1BANK_INTERRUPTPORTS; i++)
            } // bottom of while (true)
        }
        public void Gpio2EventWorker()
        {
            int count = 0;
            // build a pointer to the mapped bank, do this once
            IntPtr bankPtr = GetMappedGpioBankPointer(2, BBBDefinitions.GPIO_DATAIN);

            // we are continuously computable - we'll be swapped in and
            // out by the kernel
            while (true)
            {
                // have we got a thread termination signal
                if (gpio2BankEventThreadMustExit == true) break;
                // we do not process if this flag is false
                if (gpio2BankEnabled == false) continue;

                // get the GPIO DATAIN register at that pointer
                int currentRegisterState = Marshal.ReadInt32(bankPtr);

                // test each possible interrupt. The array is setup
                // so that a null terminates the testing and the interrupt
                // ports are also ordered in terms of priority when the 
                // array is set (highest priority first)
                for (int i = 0; i < MAX_GPIO2BANK_INTERRUPTPORTS; i++)
                {
                    if (gpio2BankInterruptPorts[i] == null) break;

                    // are we interested in this? the XOR tells us if anything changed from
                    // the last read, the subsequent AND masks out any other GPIOs changes
                    if (((currentRegisterState ^ gpio2BankInterruptPorts[i].LastInterruptRegisterState) & gpio2BankInterruptPorts[i].InterruptMask) != 0)
                    {
                        // yes we are, the state of our register has changed from the last read
                        count++;

                        // check our triggers 
                        if ((gpio2BankInterruptPorts[i].ActiveHighMask & currentRegisterState) != 0)
                        {
                            // we triggered on the changed bit because it is now high. The ActiveHighMask 
                            // will have a bit set in the appropriate position to go non zero on the 
                            // current register state
                            gpio2BankInterruptPorts[i].SendInterruptEvent(1);
                        }
                        else if ((gpio2BankInterruptPorts[i].ActiveLowMask & ~currentRegisterState) != 0)
                        {
                            // we triggered on the changed bit because it is low. The ActiveLowMask 
                            // will have a bit set in the appropriate position to go non zero on the NOTed 
                            // current register state
                            gpio2BankInterruptPorts[i].SendInterruptEvent(0);
                        }

                        // set this now so next time through we know what the last state was
                        gpio2BankInterruptPorts[i].LastInterruptRegisterState = currentRegisterState;

                        // once we have processed an interrupt we do not 
                        // process any more. We exit the for{} loop and try again
                        // the interrupt port array is ordered by priority on setup
                        // exiting now means that the high priority interrupts always 
                        // get cleared before any lower priority interrupt can be 
                        // process. The break below ensures this.
                        break;

                    }
                } // bottom of for (int i = 0; i < MAX_GPIO2BANK_INTERRUPTPORTS; i++)
            } // bottom of while (true)
        }
        public void Gpio3EventWorker()
        {
            int count = 0;
            // build a pointer to the mapped bank, do this once
            IntPtr bankPtr = GetMappedGpioBankPointer(3, BBBDefinitions.GPIO_DATAIN);

            // we are continuously computable - we'll be swapped in and
            // out by the kernel
            while (true)
            {
                // have we got a thread termination signal
                if (gpio3BankEventThreadMustExit == true) break;
                // we do not process if this flag is false
                if (gpio3BankEnabled == false) continue;

                // get the GPIO DATAIN register at that pointer
                int currentRegisterState = Marshal.ReadInt32(bankPtr);

                // test each possible interrupt. The array is setup
                // so that a null terminates the testing and the interrupt
                // ports are also ordered in terms of priority when the 
                // array is set (highest priority first)
                for (int i = 0; i < MAX_GPIO3BANK_INTERRUPTPORTS; i++)
                {
                    if (gpio3BankInterruptPorts[i] == null) break;

                    // are we interested in this? the XOR tells us if anything changed from
                    // the last read, the subsequent AND masks out any other GPIOs changes
                    if (((currentRegisterState ^ gpio3BankInterruptPorts[i].LastInterruptRegisterState) & gpio3BankInterruptPorts[i].InterruptMask) != 0)
                    {
                        // yes we are, the state of our register has changed from the last read
                        count++;

                        // check our triggers 
                        if ((gpio3BankInterruptPorts[i].ActiveHighMask & currentRegisterState) != 0)
                        {
                            // we triggered on the changed bit because it is now high. The ActiveHighMask 
                            // will have a bit set in the appropriate position to go non zero on the 
                            // current register state
                            gpio3BankInterruptPorts[i].SendInterruptEvent(1);
                        }
                        else if ((gpio3BankInterruptPorts[i].ActiveLowMask & ~currentRegisterState) != 0)
                        {
                            // we triggered on the changed bit because it is low. The ActiveLowMask 
                            // will have a bit set in the appropriate position to go non zero on the NOTed 
                            // current register state
                            gpio3BankInterruptPorts[i].SendInterruptEvent(0);
                        }

                        // set this now so next time through we know what the last state was
                        gpio3BankInterruptPorts[i].LastInterruptRegisterState = currentRegisterState;

                        // once we have processed an interrupt we do not 
                        // process any more. We exit the for{} loop and try again
                        // the interrupt port array is ordered by priority on setup
                        // exiting now means that the high priority interrupts always 
                        // get cleared before any lower priority interrupt can be 
                        // process. The break below ensures this.
                        break;

                    }
                } // bottom of for (int i = 0; i < MAX_GPIO3BANK_INTERRUPTPORTS; i++)
            } // bottom of while (true)
        }
               
        // #########################################################################
        // ### Dispose Code
        // #########################################################################
        #region

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the disposed state. There is no setter - this is done inside the 
        /// Dispose() call.
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public bool Disposed
        {
            get
            {
                return disposed;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Implement IDisposable. 
        ///     Do not make this method virtual. 
        ///     A derived class should not be able to override this method. 
        ///  see: http://msdn.microsoft.com/en-us/library/system.idisposable.dispose%28v=vs.110%29.aspx
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public void Dispose()
        {
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Implement IDisposable. 
        /// Dispose(bool disposing) executes in two distinct scenarios. 
        /// 
        ///    If disposing equals true, the method has been called directly 
        ///    or indirectly by a user's code. Managed and unmanaged resources 
        ///    can be disposed.
        ///  
        ///    If disposing equals false, the method has been called by the 
        ///    runtime from inside the finalizer and you should not reference 
        ///    other objects. Only unmanaged resources can be disposed. 
        /// 
        ///  see: http://msdn.microsoft.com/en-us/library/system.idisposable.dispose%28v=vs.110%29.aspx
        /// 
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called. 
            if(Disposed==false)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources. 
                if(disposing==true)
                {
                    // Dispose managed resources.
                }

                // Call the appropriate methods to clean up 
                // unmanaged resources here. If disposing is false, 
                // only the following code is executed.
                CloseMemoryMaps();

                // Note disposing has been done.
                disposed = true;

            }
        }
        #endregion

    }
}

