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
    /// Provides the Pulse Width Modulation Port functionality for a BeagleBone Black
    /// This is the Memory Mapped version
    /// 
    /// Be aware that you need to ensure the PWM port is configured in the Device
    /// Tree before this code will work. Usually this is done by adding the 
    /// overlay to the uEnv.txt file. The Beaglebone Black and Device Tree Overlays
    /// technical note has more information.
    /// 
    /// http://www.ofitselfso.com/BeagleNotes/Beaglebone_Black_And_Device_Tree_Overlays.php
    ///
    /// NOTE: The Pulse Width Modulation Subsystem (PWMSS) of the Beaglebone Black 
    ///       contains other things than pulse width modulation (PWM), there is also
    ///       a  Enhanced Capture (eCAP), and Encoded Pulse (eQEP) module. The actual
    ///       PWM is in the Enhanced High Resolution Pulse Width Modulator (eHRPWM)
    /// 
    ///       As a naming convention, in the code below a variable with 'PWMSS' refers
    ///       to something in the PWMSS system itself and things with names like EHRPWM refer
    ///       to the PWM module of that system.
    /// 
    ///       Furthermore, there are three PWMSS subsystems on the BBB (0,1,2). Each of
    ///       these has a eHRPWM, eQEP and eCap module. Each eHRPWM module has two PWM
    ///       outputs (A and B). This gives a total of six PWM outputs for the BBB.
    ///
    ///       Some things like the PWM frequency are set at the PWMSS level, and other things
    ///       such as the duty cycle are set individually at the eHRPWM level on each PWM
    ///       output (A or B). This means that, for any one PWMSS subsystem, both PWM outputs
    ///       have to use the same frequency but that they can have independently settable
    ///       duty cycles.
    /// 
    ///       This is IMPORTANT so I will say it again. If you configure 
    ///       two PWM outputs in the same device (PWM1_A, PWM1_B for example) 
    ///       then they MUST be configured with the same frequency. 
    /// 
    ///       They will use the same frequency anyways and if
    ///       you change one you change the other. Changing the frequency on the
    ///       B output will instantly change the frequency on the A output and
    ///       will really mess up any pulse widths/duty cycles the A output is using
    /// 
    ///       Always set the frequency first then the Pulse Width/duty cycle. The pulse 
    ///       width is calculated from whatever frequency is currently set and it is
    ///       not adjusted if the frequency is later changed.
    /// 
    /// In writing this class much reference was made to the BBBIOlib open source C code 
    /// which implements a Memory Mapped PWM module. The BBBIOlib code can be found here:
    ///    https://github.com/VegetableAvenger/BBBIOlib
    /// thank you very much Shabaz, VegetableAvenger
    /// 
    /// </summary>
    /// <history>
    ///    21 Nov 15  Cynic - Originally written
    /// </history>
    public class PWMPortMM : PortMM
    {
        // some constants
        private const int BBB_CONTROL_MODULE = 0x44e10000;
        private const int BBB_CONTROL_LEN = 0x2000;
        private const int BBB_CONTROL_PWMSS_CTRLREG = 0x664;
        private const int BBB_CLOCKMODULE_PERIPHERAL_ADDR = 0x44e00000;
        private const int BBB_CLOCKMODULE_PERIPHERAL_LEN = 0x4000;
        private const int BBB_CLOCKMODULE_PERIPHERAL_EPWMSS0_CLKCTRL = 0xD4; // yes! these are out of order
        private const int BBB_CLOCKMODULE_PERIPHERAL_EPWMSS1_CLKCTRL = 0xCC; // yes! these are out of order
        private const int BBB_CLOCKMODULE_PERIPHERAL_EPWMSS2_CLKCTRL = 0xD8; // yes! these are out of order
        private const int PWMSS0_MODULE_ADDR = 0x48300000;
        private const int PWMSS1_MODULE_ADDR = 0x48302000;
        private const int PWMSS2_MODULE_ADDR = 0x48304000;
        private const int PWMSS_MMAP_LEN = 0x1000;
        private const int PWMSS_MODULE_ECAP_OFFSET = 0x100;
        private const int PWMSS_MODULE_EQEP_OFFSET = 0x180;
        private const int PWMSS_MODULE_EPWM_OFFSET = 0x200;
        private const int PWMSS_MODULE_EPWM_AQCTLA = 0x16;
        private const int PWMSS_MODULE_EPWM_AQCTLB = 0x18;
        private const int PWMSS_MODULE_EPWM_TBCNT = 0x8;
        private const int PWMSS_MODULE_EPWM_TBCTL = 0x0;
        private const int PWMSS_MODULE_EPWM_TBPRD = 0xA;
        private const int PWMSS_MODULE_EPWM_CMPA = 0x12;
        private const int PWMSS_MODULE_EPWM_CMPB = 0x14;
        // set ZRO to 0x02 and CAU to 0x03, everthing else 0
        private const short AQCTLA_SETTING_ON = 0x001A;
        // set ZRO to 0x02 and CBU to 0x03, everthing else 0
        private const short AQCTLB_SETTING_ON = 0x010A;
        // set ZRO to 0x01 - force it low, everthing else 0
        private const short AQCTLA_SETTING_OFF = 0x1;
        private const short AQCTLB_SETTING_OFF = 0x1;

        // the PWM port we use
        private PWMPortEnum pwmPort = PWMPortEnum.PWM_NONE; 
        // memory mapped pointers
        private IntPtr bbbControlModule_ptr = IntPtr.Zero;
        private IntPtr bbbClockModulePeripheral_ptr = IntPtr.Zero;
        private IntPtr pwmssModule_ptr = IntPtr.Zero;
        // calculated by adding offsets to the memory mapped pointers
        // these are the same for either the A or B PWM Output
        private IntPtr pwmssModuleEpwm_ptr = IntPtr.Zero;
        private IntPtr pwmssModuleEpwmTBCNT_ptr  = IntPtr.Zero;
        private IntPtr pwmssModuleEpwmTBCTL_ptr = IntPtr.Zero;
        private IntPtr pwmssModuleEpwmTBPRD_ptr = IntPtr.Zero;
        // these are specific to the A or B PWM Output
        private IntPtr pwmssModuleEpwmAQCTLA_ptr = IntPtr.Zero;
        private IntPtr pwmssModuleEpwmAQCTLB_ptr = IntPtr.Zero;
        private IntPtr pwmssModuleEpwmCMP_ptr = IntPtr.Zero;
           
        private float[] clkdivArr = {1.0f, 2.0f, 4.0f, 8.0f, 16.0f, 32.0f, 64.0f, 128.0f};
        private float[] hspclkdivArr = {1.0f, 2.0f, 4.0f, 6.0f, 8.0f, 10.0f, 12.0f, 14.0f};
        // the TBCLK base frequency
        private const float BASE_FREQUENCY = 100000000.0f;
        // the maximum we can count up to
        private const float MAX_TBPRD_COUNT = 0xFFFF;

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pwmPortIn">The PWM port we use</param>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        public PWMPortMM(PWMPortEnum pwmPortIn) : base(GpioEnum.GPIO_NONE)
        {
            pwmPort = pwmPortIn;

            //            Console.WriteLine("PWMPort Starts");
            OpenPort();
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Opens the port. Throws an exception on failure
        /// 
        /// </summary>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        protected override void OpenPort()
        { 
            int fdDevMem = -1;
            bool retBool = false;

            //Console.WriteLine("PWMPort Port Opening: "+ PWMPort.ToString());
            // some tests
            if (PWMPort == PWMPortEnum.PWM_NONE) 
            {
                throw new Exception ("Cannot open PWM port. Invalid port: " + PWMPort.ToString ());
            }

            // open the device memory file 
            fdDevMem = Syscall.open (BBBDefinitions.DEVMEM_FILE, OpenFlags.O_RDWR, FilePermissions.DEFFILEMODE);
            if (fdDevMem == -1) 
            {
                throw new Exception ("DEVMEM_FILE did not open");
            }
            //Console.WriteLine("  device memory file opened");

            // ########
            // ######## Create memory maps to memory areas we are interested in
            // ########

            // map in the Control Module Register, we will need to check
            // the pwmss_ctrl register to make sure the timebase clock is enabled
            bbbControlModule_ptr = Syscall.mmap (IntPtr.Zero, BBB_CONTROL_LEN, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, fdDevMem, BBB_CONTROL_MODULE);
            if (bbbControlModule_ptr == (IntPtr)(-1))
            {
                throw new IOException ("mmap failed for CRM " + "(" + BBB_CONTROL_MODULE + ", " + BBB_CONTROL_LEN + ")");
            }
            //Console.WriteLine("  bbbControlModule_ptr=0x"+bbbControlModule_ptr.ToString("X8"));

            // map in the Clock Module Peripheral Register 
            bbbClockModulePeripheral_ptr = Syscall.mmap (IntPtr.Zero, BBB_CLOCKMODULE_PERIPHERAL_LEN, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, fdDevMem, BBB_CLOCKMODULE_PERIPHERAL_ADDR);
            if (bbbClockModulePeripheral_ptr == (IntPtr)(-1))
            {
                throw new IOException ("mmap failed for PCRM " + "(" + BBB_CLOCKMODULE_PERIPHERAL_ADDR + ", " + BBB_CLOCKMODULE_PERIPHERAL_LEN + ")");
            }
            //Console.WriteLine("  bbbClockModulePeripheral_ptr=0x"+bbbClockModulePeripheral_ptr.ToString("X8"));

            // create memory map for the PWMSS module
            int pwmssModuleBaseAddress = GetPWMSSBaseAddressFromPWMPortEnum(PWMPort);
            pwmssModule_ptr = Syscall.mmap (IntPtr.Zero, PWMSS_MMAP_LEN, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, fdDevMem, pwmssModuleBaseAddress);
            if (pwmssModule_ptr == (IntPtr)(-1))
            {
                throw new IOException ("mmap failed for CRM " + "(" + PWMSS1_MODULE_ADDR + ", " + PWMSS_MMAP_LEN + ")");
            }
            //Console.WriteLine("  pwmssModuleBaseAddress=0x"+pwmssModuleBaseAddress.ToString("X8"));

            // ########
            // ######## Now set up everything so we can run when required
            // ########

            retBool = IsControlModuleClockEnabled();
            if (retBool == false)
            {
                // NOTE: I do not think it is possible to set the Control Module from 
                //       a userspace program - even one running as root. This sort of 
                //       thing is typically done from the Device Tree
                //Console.WriteLine("  Control Module Clock Not Enabled");
                throw new Exception("Control Module Clock Not Enabled - Is Device Tree Set?");
            }
            else
            {
                //Console.WriteLine("  Control Module Clock Is Enabled");
            }

            retBool = IsClockModulePeripheralClockEnabled();
            if (retBool == false)
            {
                //Console.WriteLine("  Clock Module Peripheral Clock Not Enabled");
                EnableClockModulePeripheralClock();
            }
            else
            {
                //Console.WriteLine("  Clock Module Peripheral Clock Already Enabled");
            }

            retBool = IsClockModulePeripheralClockEnabled();
            if (retBool == false)
            {
                //Console.WriteLine("  Clock Module Peripheral Clock Not Enabled");
            }
            else
            {
                //Console.WriteLine("  Clock Module Peripheral Clock Already Enabled");
            }

            // this points at the EPWM sub module of the PWMSS module, remember we also
            // have the EQEP and ECAP modules in there as well
            pwmssModuleEpwm_ptr = new IntPtr(pwmssModule_ptr.ToInt64() + PWMSS_MODULE_EPWM_OFFSET );
            //Console.WriteLine("  pwmssModuleEpwm_ptr=0x"+pwmssModuleEpwm_ptr.ToString("X8"));

            // these are all based off the pwmssModuleEpwm_ptr and will be used by other
            // functions to set the frequency, duty cycle and enable/disable the PWM
            pwmssModuleEpwmTBCNT_ptr = new IntPtr(pwmssModuleEpwm_ptr.ToInt64() + PWMSS_MODULE_EPWM_TBCNT);
            pwmssModuleEpwmTBCTL_ptr = new IntPtr(pwmssModuleEpwm_ptr.ToInt64() + PWMSS_MODULE_EPWM_TBCTL);
            pwmssModuleEpwmTBPRD_ptr = new IntPtr(pwmssModuleEpwm_ptr.ToInt64() + PWMSS_MODULE_EPWM_TBPRD);
            // we need both of these
            pwmssModuleEpwmAQCTLA_ptr = new IntPtr(pwmssModuleEpwm_ptr.ToInt64() + PWMSS_MODULE_EPWM_AQCTLA);
            pwmssModuleEpwmAQCTLB_ptr = new IntPtr(pwmssModuleEpwm_ptr.ToInt64() + PWMSS_MODULE_EPWM_AQCTLB);

            // these are specific to the A or B PWM output
            if (IsThisPortForPWMOutputA(PWMPort) == true)
            {
                // set up for the A output
                pwmssModuleEpwmCMP_ptr = new IntPtr(pwmssModuleEpwm_ptr.ToInt64() + PWMSS_MODULE_EPWM_CMPA);
            }
            else
            {
                // set up for the B output
                pwmssModuleEpwmAQCTLB_ptr = new IntPtr(pwmssModuleEpwm_ptr.ToInt64() + PWMSS_MODULE_EPWM_AQCTLB);
                pwmssModuleEpwmCMP_ptr = new IntPtr(pwmssModuleEpwm_ptr.ToInt64() + PWMSS_MODULE_EPWM_CMPB);
            }
                
            // set this flag
            portIsOpen = true;

            //    Console.WriteLine("PWMPort Port Opened: "+ PWMPort.ToString());

            // once the memory is mapped the device file can be closed.
            if (fdDevMem > 0)
            {
                Syscall.close(fdDevMem);
                fdDevMem = -1;
            }

        }
                      
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Closes the port. 
        /// 
        /// </summary>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        public override void ClosePort()
        {
            //  Console.WriteLine("ClosePort() called");
            if (portIsOpen == true)
            {
                //Console.WriteLine("portIsOpen == true");
                RunState = false;           
                portIsOpen = false;
            }                
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the period of the PWM output square wave form
        /// 
        /// Note both outputs from the same PWM module will use the same period
        ///      See the notes in this codes header. Always set the period/frequency 
        ///      before setting the pulse width/duty cycle
        /// 
        /// </summary>
        /// <value>the period (1/Freq) in nano seconds</value>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        public uint PeriodNS
        { 
            get
            {
                uint freqInHz = (uint)GetBaseFrequency();
                if (freqInHz == 0)
                {
                    throw new Exception("Invalid base frequency of " + freqInHz.ToString()); 
                }
                return 1000000000/freqInHz;
            }
            set
            {
                uint freqInHz = 1000000000/value;
                SetBaseFrequency((int)freqInHz);
            }
        }
       
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the frequency of the PWM output square wave form
        /// 
        /// Note both outputs from the same PWM module will use the same frequency
        ///      See the notes in this codes header. Always set the period/frequency 
        ///      before setting the pulse width/duty cycle
        /// 
        /// </summary>
        /// <value>frequency in Hz</value>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        public uint FrequencyHz
        { 
            get
            {
                return (uint)GetBaseFrequency();
            }
            set
            {
                SetBaseFrequency((int)value);
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the duty cycle of the PWM output square wave form as a percent
        /// 
        /// Note both outputs from the same PWM module will use the same frequency
        ///      but can have different duty cycles. Always set the period/frequency
        ///      before setting the dutyNs/duty cycle
        /// 
        /// </summary>
        /// <value>percentage of the input value. Must be between 0 and 100 </value>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        public float DutyPercent
        { 
            get
            {
                return (float)GetDutyCycle();
            }
            set
            {
                SetDutyCycle((float)value);
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the duty cycle of the PWM output square wave form in nanoseconds
        /// 
        /// Note both outputs from the same PWM module will use the same frequency
        ///      but can have different duty cycles. Always set the period/frequency
        ///      before setting the dutyNs/duty cycle
        /// 
        /// </summary>
        /// <value>the duty cycle of the PWM output in nanoseconds. This should always
        /// be less than the PeriodNS</value>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        public uint DutyNS
        { 
            get
            {
                uint periodNS = PeriodNS;
                return (uint)((float)periodNS * (float)DutyPercent) / 100;
            }
            set
            {
                 DutyPercent = (((float)value * 100)/ (float)PeriodNS) ;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the run state of the PWM output square wave
        /// 
        /// </summary>
        /// <value>true - begin running, false - stop running</value>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        public bool RunState
        { 
            get
            {
                short out16Val=0;

                if (portIsOpen == false) return false;

                // check TBCTL 
                out16Val = Marshal.ReadInt16(pwmssModuleEpwmTBCTL_ptr);
                if ((out16Val & 0x03) != 0x00)
                {
                    // neither PWM output configured 
                    return false;
                }

                if (IsThisPortForPWMOutputA(PWMPort) == true)
                {
                    // read the A side AQCTLA register
                    out16Val = Marshal.ReadInt16(pwmssModuleEpwmAQCTLA_ptr);
                    if (out16Val == AQCTLA_SETTING_ON) return true;
                }
                else
                {
                    // read the B side AQCTLB register
                    out16Val = Marshal.ReadInt16(pwmssModuleEpwmAQCTLB_ptr);
                    if (out16Val == AQCTLB_SETTING_ON) return true;
                }
                return false;
            }
            set
            {
                short out16Val=0;
               
                //           Console.WriteLine("Set Run State : " + value.ToString());
                if (portIsOpen == false) return;

                // set the run state
                if (value == true)
                {
                    // enable
                    if (IsThisPortForPWMOutputA(PWMPort) == true)
                    {
                        out16Val = AQCTLA_SETTING_ON;
                        Marshal.WriteInt16(pwmssModuleEpwmAQCTLA_ptr, out16Val);
                    }
                    else
                    {
                        out16Val = AQCTLB_SETTING_ON;
                        Marshal.WriteInt16(pwmssModuleEpwmAQCTLB_ptr, out16Val);
                    }

                    // test to see if the other PWM port is active
                    if (IsTheOtherPWMOutputActive() == false)
                    {
                        // not active, start counting from 0
                        out16Val = 0;
                        Marshal.WriteInt16(pwmssModuleEpwmTBCNT_ptr, out16Val);
                    }

                    // set the TBCTL going - if it is not already, both PWM
                    // outputs use this so we have to be careful here
                    out16Val = Marshal.ReadInt16(pwmssModuleEpwmTBCTL_ptr);
                    if ((out16Val & 0x03) != 0x00)
                    {
                        // not configured, clear the bottom two bits to 
                        // start it running in increment counter mode
                        out16Val &= ~0x03;
                        Marshal.WriteInt16(pwmssModuleEpwmTBCTL_ptr, out16Val);
                    }
                }
                else
                {
                    // disable

                    if (IsThisPortForPWMOutputA(PWMPort) == true)
                    {
                        // just force the output low on the next cycle, and configure it
                        // to never change
                        out16Val = AQCTLA_SETTING_OFF;
                        Marshal.WriteInt16(pwmssModuleEpwmAQCTLA_ptr, out16Val);
                    }
                    else
                    {
                        // just force the output low on the next cycle, and configure it
                        // to never change
                        out16Val = AQCTLB_SETTING_OFF;
                        Marshal.WriteInt16(pwmssModuleEpwmAQCTLB_ptr, out16Val);
                    }

                    // test to see if the other PWM port is active
                    if(IsTheOtherPWMOutputActive()==false)
                    {
                        // no, it is not active. We can disable the TBCTL
                        // setting the bottom two bits high (0x03), stops the counter for
                        // both PWM ports
                        out16Val = Marshal.ReadInt16(pwmssModuleEpwmTBCTL_ptr);
                        out16Val|=0x3;
                        Marshal.WriteInt16(pwmssModuleEpwmTBCTL_ptr, out16Val );
                    }

                    // test to see if the other PWM port is active
                    if (IsTheOtherPWMOutputActive() == false)
                    {
                        // reset the counter. it is shared
                        out16Val = 0;
                        Marshal.WriteInt16(pwmssModuleEpwmTBCNT_ptr, out16Val);
                    }
                }
            }
        }            
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the PWM Port. There is no Set accessor this is set in the constructor
        /// </summary>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        public PWMPortEnum PWMPort
        {
            get
            {
                return pwmPort;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the PortDirection
        /// </summary>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        public override PortDirectionEnum PortDirection()
        {
            return PortDirectionEnum.PORTDIR_OUTPUT;
        }   

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Sets the DutyCycle - 
        /// 
        /// NOTE this code expects the base frequency to have been set. It uses that
        ///      value to calculate the UP and DOWN pulse timing
        /// 
        /// </summary>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        private void SetDutyCycle(float dutyCyclePercent)
        {
            if ((dutyCyclePercent < 0) || (dutyCyclePercent > 100))
            {
                throw new Exception("The percentage duty cycle of " + dutyCyclePercent.ToString() + " is not valid");
            }
            short tbprd16Val=0;
            short cmpReg16Val=0;

            //  Console.WriteLine("  SDS: dutyCyclePercent=" + dutyCyclePercent.ToString());

            // we read the contents of the TBPRD to get the current pulse count
            tbprd16Val = Marshal.ReadInt16(pwmssModuleEpwmTBPRD_ptr);
            if (dutyCyclePercent == 0)
            {
                // 0% duty cycle is this count
                cmpReg16Val = 0;
            }
            else if (dutyCyclePercent == 100)
            {
                // 100% duty cycle is this count
                cmpReg16Val = tbprd16Val;
            }
            else
            {
                // calc the total high count based on a percentage of the total pulse width
                ushort ushort16Val = (ushort)tbprd16Val;
                float tmpVal = ((float)ushort16Val * dutyCyclePercent);
                cmpReg16Val = (short)( tmpVal/ 100);
                //            Console.WriteLine("  SDS setting: tmpVal="+tmpVal.ToString()); 
            }
            // at this point we have the count for the cmpReg16Val to be calulated
            // we write it back out to CMPA or CMPB depending on the output we 
            // are using. The pwmssModuleEpwmCMP_ptr points appropriately
            Marshal.WriteInt16(pwmssModuleEpwmCMP_ptr, cmpReg16Val);
            //        Console.WriteLine("  SDS setting: CMP val="+cmpReg16Val.ToString()); 
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the DutyCycle 
        /// 
        /// </summary>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        private float GetDutyCycle()
        {
            short tbprd16Val=0;
            short cmpReg16Val=0;

            // we read the contents of the TBPRD to get the current pulse count
            tbprd16Val = Marshal.ReadInt16(pwmssModuleEpwmTBPRD_ptr);
            //Console.WriteLine("GDS: TBPRD=" + tbprd16Val.ToString());

            // we read the contents of the TBPRD to get the current pulse count
            cmpReg16Val = Marshal.ReadInt16(pwmssModuleEpwmCMP_ptr);
            //Console.WriteLine("GDS: TBPRD=" + tbprd16Val.ToString());

            if (tbprd16Val==0)
            {
                throw new Exception("Invalid TBPRD count of " + tbprd16Val.ToString());
            }
            if ((ushort)cmpReg16Val > (ushort)tbprd16Val)
            {
                throw new Exception("cmpReg16Val > tbprd16Val (" + cmpReg16Val.ToString() + "," + tbprd16Val.ToString()+")");
            }

            return ((float)((ushort)cmpReg16Val)*100)/(float)((ushort)tbprd16Val);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Recalculates the current base frequency from the TBCTL divisors
        /// 
        /// </summary>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        private int GetBaseFrequency()
        {
            // read the TBCTL
            short out16Val = Marshal.ReadInt16(pwmssModuleEpwmTBCTL_ptr);
            //Console.WriteLine("  GBF: TBCTL val="+out16Val.ToString("X4")); 
            int workingCLKDIV = (int)((out16Val & 0x1C00) >> 10);
            int workingHSPCLKDIV = (int)((out16Val & 0x0380) >> 7);
            if ((workingCLKDIV < 0) || (workingCLKDIV > clkdivArr.Length))
            {
                throw new Exception("invalid CLKDIV=" + workingCLKDIV.ToString());
            }
            if ((workingHSPCLKDIV < 0) || (workingHSPCLKDIV > hspclkdivArr.Length))
            {
                throw new Exception("invalid HSPCLKDIV=" + workingHSPCLKDIV.ToString());
            }

            // read the TBPRD - we have to know how many clocks we are counting
            out16Val = Marshal.ReadInt16(pwmssModuleEpwmTBPRD_ptr);
            if (out16Val == 0)
            {
                throw new Exception("invalid count in TBPRD =" + out16Val.ToString());
            }
            float tbPRDCount = (float)((ushort)(out16Val));
            //Console.WriteLine("  GBF: tbPRDCount="+tbPRDCount.ToString()); 

            //Console.WriteLine("  workingCLKDIV="+workingCLKDIV.ToString()); 
            //Console.WriteLine("  workingHSPCLKDIV="+workingHSPCLKDIV.ToString()); 
            //Console.WriteLine("  clkdivArr[workingCLKDIV]="+clkdivArr[workingCLKDIV].ToString()); 
            //Console.WriteLine("  hspclkdivArr[workingHSPCLKDIV]="+hspclkdivArr[workingHSPCLKDIV].ToString()); 
            return (int)((BASE_FREQUENCY / (clkdivArr[workingCLKDIV] *  hspclkdivArr[workingHSPCLKDIV]))/tbPRDCount);           
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Sets the TBPRD count and the TBCTL divisors based on the specified input
        /// frequency.
        /// 
        /// NOTE:
        /// The grid-it-out brute force method of finding the best divisors was 
        /// inspired by the similar method used in the BBBIOlib open source C code.
        /// Thank you very much Shabaz, VegetableAvenger, the BBBIOlib code can be 
        /// found here:
        ///    https://github.com/VegetableAvenger/BBBIOlib
        /// 
        /// Note both outputs from the same PWM module will use the same frequency
        ///      See the notes in this codes header. Always set the period/frequency 
        ///      before setting the pulse width/duty cycle
        /// 
        /// </summary>
        /// <param name="frequencyInHz">The frequency in Hz</param>
        /// <history>
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        private void SetBaseFrequency(int frequencyInHz)
        {
            int bestCLKDIV = -1;
            int bestHSPCLKDIV = -1;
            float bestDividedClocksPerPulse = 0;
            short out16Val=0;

            if (frequencyInHz == 0)
            {
                throw new Exception("Zero is not a valid base frequency");

            }
            // set this now
            float numberUndividedClocksPerPulse = BASE_FREQUENCY / (float)frequencyInHz;

            // now figure out a good set of high and low divisors. The maximum we pulses
            // we can count for any total pulse width is MAXCOUNT
            for (int i = 0; i < clkdivArr.Length; i++)
            {
                for (int j = 0; j < hspclkdivArr.Length; j++)
                {
                    float workingDivisor = (clkdivArr[i] * hspclkdivArr[j]);
                    float numberDividedClocksPerPulse = numberUndividedClocksPerPulse / workingDivisor;
                    // now some tests, we cannot use a count greater than MAX_TBPRD_COUNT
                    if (numberDividedClocksPerPulse > MAX_TBPRD_COUNT) continue;
                    // ideally we want the highest number of pulses possible for best accuracy
                    // especially when we split it down for the duty cycle part
                    if (numberDividedClocksPerPulse > bestDividedClocksPerPulse)
                    {
                        // we want this one
                        bestCLKDIV = i;
                        bestHSPCLKDIV = j;
                        bestDividedClocksPerPulse = numberDividedClocksPerPulse;
                    }                        
                }
            }
            // sanity check
            if ((bestCLKDIV < 0) || (bestHSPCLKDIV<0))
            {
                throw new Exception("The base frequency "+ frequencyInHz.ToString() +" cannot be output");

            }

            //Console.WriteLine("    numberDividedClocksPerPulse=" + (numberUndividedClocksPerPulse/(clkdivArr[bestCLKDIV] * hspclkdivArr[bestHSPCLKDIV])).ToString());
            //Console.WriteLine("    bestCLKDIV=" + bestCLKDIV.ToString());
            //Console.WriteLine("   bestHSPCLKDIV=" + bestHSPCLKDIV.ToString());
            //Console.WriteLine("   divisor=" + (clkdivArr[bestCLKDIV] * hspclkdivArr[bestHSPCLKDIV]).ToString());

            // set the TBCTL register now
            out16Val = (short)((int)0x03 | (bestCLKDIV << 10) | (bestHSPCLKDIV << 7));
            Marshal.WriteInt16(pwmssModuleEpwmTBCTL_ptr, out16Val);
            //Console.WriteLine("  settings: TBCTL val="+out16Val.ToString("X8")); 

            // set the period
            out16Val=unchecked((short)(numberUndividedClocksPerPulse/(clkdivArr[bestCLKDIV] * hspclkdivArr[bestHSPCLKDIV])));
            Marshal.WriteInt16(pwmssModuleEpwmTBPRD_ptr, out16Val);
            //Console.WriteLine("  settings: TBPRD val="+out16Val.ToString("X8")); 

            // reset the clock counter
            out16Val=0x0000;
            Marshal.WriteInt16(pwmssModuleEpwmTBCNT_ptr, out16Val);

        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the PWMSSBase address given a PWMPortEnum
        /// </summary>
        /// <param name="pwmPortIn">The PWM port we use</param>
        /// <returns>the PMSS module base address</returns>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        private int GetPWMSSBaseAddressFromPWMPortEnum(PWMPortEnum pwmPortIn)
        {
            uint pwmssModuleNumber = ConvertPWMPortEnumToPWMSSModuleNumber(pwmPortIn);
            if (pwmssModuleNumber == 0) return PWMSS0_MODULE_ADDR;
            if (pwmssModuleNumber == 1) return PWMSS1_MODULE_ADDR;
            if (pwmssModuleNumber == 2) return PWMSS2_MODULE_ADDR;
            throw new Exception("Unknown PWM module number: "+ pwmssModuleNumber.ToString());
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Converts the PWMPort Enum to a PWMSS module number
        /// </summary>
        /// <param name="pwmPortIn">The PWM port we use</param>
        /// <returns>the PMSS module number</returns>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        private uint ConvertPWMPortEnumToPWMSSModuleNumber(PWMPortEnum pwmPortIn)
        {
            if (pwmPortIn == PWMPortEnum.PWM0_A) return 0; // EHRPWM0A
            if (pwmPortIn == PWMPortEnum.PWM0_B) return 0; // EHRPWM0B    
            if (pwmPortIn == PWMPortEnum.PWM1_A) return 1; // EHRPWM1A
            if (pwmPortIn == PWMPortEnum.PWM1_B) return 1; // EHRPWM1B
            if (pwmPortIn == PWMPortEnum.PWM2_A) return 2; // EHRPWM2A
            if (pwmPortIn == PWMPortEnum.PWM2_B) return 2; // EHRPWM2B
            // if (pwmPortIn == PWMPortEnum.PWM_P9_28) return 7;          // 7 - ECAPPWM2
            // if (pwmPortIn == PWMPortEnum.PWM_P9_42) return 2;          // 2 - ECAPPWM0
            throw new Exception("Unknown PWM Port: "+ pwmPortIn.ToString());
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Detects if this is the A or B PWM output for this EHPWMSS subsystem
        /// </summary>
        /// <param name="pwmPortIn">The PWM port we use</param>
        /// <returns>true - this is the A output, false - this is the B output</returns>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        private bool IsThisPortForPWMOutputA(PWMPortEnum pwmPortIn)
        {
            if (pwmPortIn == PWMPortEnum.PWM0_A) return true; // EHRPWM0A
            if (pwmPortIn == PWMPortEnum.PWM0_B) return false; // EHRPWM0B    
            if (pwmPortIn == PWMPortEnum.PWM1_A) return true; // EHRPWM1A
            if (pwmPortIn == PWMPortEnum.PWM1_B) return false; // EHRPWM1B
            if (pwmPortIn == PWMPortEnum.PWM2_A) return true; // EHRPWM2A
            if (pwmPortIn == PWMPortEnum.PWM2_B) return false; // EHRPWM2B
            // if (pwmPortIn == PWMPortEnum.PWM_P9_28) return 7;          // 7 - ECAPPWM2
            // if (pwmPortIn == PWMPortEnum.PWM_P9_42) return 2;          // 2 - ECAPPWM0
            throw new Exception("Unknown PWM Port: "+ pwmPortIn.ToString());
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Checks to see if the other PWM output in the PWMSS module is active
        /// We check the AQCTL register for this since these are the only registers
        /// we independently configure for the A and B side
        /// </summary>
        /// <returns>true - is enabled, false - is not enabled</returns>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        private bool IsTheOtherPWMOutputActive()
        {
            short out16Val=0;
            if (IsThisPortForPWMOutputA(PWMPort) == true)
            {
                // read the B side AQCTLB register
                out16Val = Marshal.ReadInt16(pwmssModuleEpwmAQCTLB_ptr);
                if (out16Val == AQCTLB_SETTING_ON) return true;
                else return false;
            }
            else
            {
                // read the A side AQCTLA register
                out16Val = Marshal.ReadInt16(pwmssModuleEpwmAQCTLA_ptr);
                if (out16Val == AQCTLA_SETTING_ON) return true;
                else return false;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Checks to see if the PWMSS Module Clock is Enabled in the Control Module
        /// </summary>
        /// <returns>true - is enabled, false - is not enabled</returns>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        private bool IsControlModuleClockEnabled()
        {            
            //Console.WriteLine("IsControlModuleClockEnabled");

            // get an integer 0,1,2 which represents the PWMSS module
            uint pwmssModuleNumber = ConvertPWMPortEnumToPWMSSModuleNumber(PWMPort);

            // set up our pointer
            IntPtr cmClockCheck_ptr = new IntPtr(bbbControlModule_ptr.ToInt64() + BBB_CONTROL_PWMSS_CTRLREG);
            // read in our value from the register
            int regVal = Marshal.ReadInt32(cmClockCheck_ptr);
            // mask off the bits we are interested in. 
            int outVal =(regVal & (1 << (int)pwmssModuleNumber));

            //Console.WriteLine("  cmClockCheck_ptr=0x"+cmClockCheck_ptr.ToString("X8"));
            //Console.WriteLine("  regVal=0x" + regVal.ToString("X8"));
            //Console.WriteLine("  outVal=0x" + outVal.ToString("X8"));

            // return the enabled state
            if (outVal != 0) return true;
            return false;
 
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Checks to see if the Clock Module Peripheral Clock is enabled
        /// </summary>
        /// <returns>true - is enabled, false - is not enabled</returns>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        private bool IsClockModulePeripheralClockEnabled()
        {            
            //Console.WriteLine("IsClockModulePeripheralClockEnabled");

            // get an integer 0,1,2 which represents the PWMSS module
            uint pwmssModuleNumber = ConvertPWMPortEnumToPWMSSModuleNumber(PWMPort);

            // get our offset into the Clock Module Peripheral register
            int clkctrlRegOffset = 0;
            if (pwmssModuleNumber == 0) clkctrlRegOffset = BBB_CLOCKMODULE_PERIPHERAL_EPWMSS0_CLKCTRL;
            else if (pwmssModuleNumber == 1) clkctrlRegOffset = BBB_CLOCKMODULE_PERIPHERAL_EPWMSS1_CLKCTRL;
            else if (pwmssModuleNumber == 2) clkctrlRegOffset = BBB_CLOCKMODULE_PERIPHERAL_EPWMSS2_CLKCTRL;
            else
            {
                throw new Exception("Unknown pwmss Module Number: "+ pwmssModuleNumber.ToString());
            }
            // set up our pointer
            IntPtr cmPerClk_ptr = new IntPtr(bbbClockModulePeripheral_ptr.ToInt64() + clkctrlRegOffset);
            // read in our value from the register
            int regVal = Marshal.ReadInt32(cmPerClk_ptr);
            // mask off the bits we are not interested in. 
            int outVal =(regVal & 3);
            // Console.WriteLine("  regVal=0x" + regVal.ToString("X8"));
            // Console.WriteLine("  outVal=0x" + outVal.ToString("X8"));

            // return the enabled state, 2=enabled
            if (outVal == 2) return true;
            return false;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Enables the Clock Module Peripheral register Clock
        /// </summary>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        private void EnableClockModulePeripheralClock()
        {
            //Console.WriteLine("EnableClockModulePeripheralClock");

            // get an integer 0,1,2 which represents the PWMSS module
            uint pwmssModuleNumber = ConvertPWMPortEnumToPWMSSModuleNumber(PWMPort);

            // get our offset into the Clock Module Peripheral register
            int clkctrlRegOffset = 0;
            if (pwmssModuleNumber == 0) clkctrlRegOffset = BBB_CLOCKMODULE_PERIPHERAL_EPWMSS0_CLKCTRL;
            else if (pwmssModuleNumber == 1) clkctrlRegOffset = BBB_CLOCKMODULE_PERIPHERAL_EPWMSS1_CLKCTRL;
            else if (pwmssModuleNumber == 2) clkctrlRegOffset = BBB_CLOCKMODULE_PERIPHERAL_EPWMSS2_CLKCTRL;
            else
            {
                throw new Exception("Unknown pwmss Module Number: "+ pwmssModuleNumber.ToString());
            }

            // build a pointer to appropriate memory location
            IntPtr cmPerClk_ptr = new IntPtr(bbbClockModulePeripheral_ptr.ToInt64() + clkctrlRegOffset);
            // set the appropriate bits - 2=enable
            Marshal.WriteInt32(cmPerClk_ptr, 2);
            //Console.WriteLine("  cmPerClk_ptr=0x"+cmPerClk_ptr.ToString("X8"));
            //Console.WriteLine("  clkctrlRegOffset=0x" + clkctrlRegOffset.ToString("X8"));
 
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Disables the Clock Module Peripheral register Clock. Be aware that 
        /// two PWM's share a PWMSS module. Disabling this disables it for both
        /// </summary>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        private void DisableClockModulePeripheralClock()
        {
            //Console.WriteLine("DisableClockModulePeripheralClock");

            // get an integer 0,1,2 which represents the PWMSS module
            uint pwmssModuleNumber = ConvertPWMPortEnumToPWMSSModuleNumber(PWMPort);

            // get our offset into the Clock Module Peripheral register
            int clkctrlRegOffset = 0;
            if (pwmssModuleNumber == 0) clkctrlRegOffset = BBB_CLOCKMODULE_PERIPHERAL_EPWMSS0_CLKCTRL;
            else if (pwmssModuleNumber == 1) clkctrlRegOffset = BBB_CLOCKMODULE_PERIPHERAL_EPWMSS1_CLKCTRL;
            else if (pwmssModuleNumber == 2) clkctrlRegOffset = BBB_CLOCKMODULE_PERIPHERAL_EPWMSS2_CLKCTRL;
            else
            {
                throw new Exception("Unknown pwmss Module Number: "+ pwmssModuleNumber.ToString());
            }

            // build a pointer to appropriate memory location
            IntPtr cmPerClk_ptr = new IntPtr(bbbClockModulePeripheral_ptr.ToInt64() + clkctrlRegOffset);
            // set the appropriate bits - 0=disable
            Marshal.WriteInt32(cmPerClk_ptr, 0);
            //Console.WriteLine("  cmPerClk_ptr=0x"+cmPerClk_ptr.ToString("X8"));
            //Console.WriteLine("  clkctrlRegOffset=0x" + clkctrlRegOffset.ToString("X8"));

        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Useful diagnostic function. 
        /// </summary>
        /// <history>
        ///    21 Nov 15  Cynic - Originally written
        /// </history>
        public void DumpRegs()
        {
            short out16Val = Marshal.ReadInt16(pwmssModuleEpwmTBCTL_ptr);
            Console.WriteLine("y: TBCTL=0x" + out16Val.ToString("X4"));
            out16Val = Marshal.ReadInt16(pwmssModuleEpwmCMP_ptr);
            Console.WriteLine("y: CMP=0x" + out16Val.ToString("X4"));
            out16Val = Marshal.ReadInt16(pwmssModuleEpwmTBPRD_ptr);
            Console.WriteLine("y: TBPRD=0x" + out16Val.ToString("X4"));
            out16Val = Marshal.ReadInt16(pwmssModuleEpwmAQCTLA_ptr);
            Console.WriteLine("y: AQCTLA=0x" + out16Val.ToString("X4"));
            out16Val = Marshal.ReadInt16(pwmssModuleEpwmAQCTLB_ptr);
            Console.WriteLine("y: AQCTLB=0x" + out16Val.ToString("X4"));
        }

        // #########################################################################
        // ### Dispose Code
        // #########################################################################
        #region

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
        ///    21 Nov 15 Cynic - Originally written
        /// </history>
        protected override void Dispose(bool disposing)
        {
            //Console.WriteLine("Disposed called");
            // Check to see if Dispose has already been called. 
            if(Disposed==false)
            {
                //Console.WriteLine("  Disposed called Disposed==false");
                // If disposing equals true, dispose all managed 
                // and unmanaged resources. 
                if(disposing==true)
                {
                    //Console.WriteLine("  Disposed called disposing==true");
                    if (PortIsOpen == true)
                    {
                        ClosePort();
                    }
                    // Dispose managed resources.
                    if (bbbControlModule_ptr != IntPtr.Zero) Syscall.munmap(bbbControlModule_ptr, BBB_CONTROL_LEN);
                    bbbControlModule_ptr = IntPtr.Zero;

                    if (bbbClockModulePeripheral_ptr != IntPtr.Zero) Syscall.munmap(bbbClockModulePeripheral_ptr, BBB_CLOCKMODULE_PERIPHERAL_LEN);
                    bbbClockModulePeripheral_ptr = IntPtr.Zero;

                    if (pwmssModule_ptr != IntPtr.Zero) Syscall.munmap(pwmssModule_ptr, PWMSS_MMAP_LEN);
                    pwmssModule_ptr = IntPtr.Zero;

                }

                // Call the appropriate methods to clean up 
                // unmanaged resources here. If disposing is false, 
                // only the following code is executed.

                // Clean up our code
                //  Console.WriteLine("Disposing PWMPORT");
         
                //Console.WriteLine("  base dispose being called");
                // call the base to dispose there
                base.Dispose(disposing);

            }
        }
        #endregion

    }
}

