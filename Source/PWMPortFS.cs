using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
    /// This is the SYSFS version
    /// 
    /// Be aware that you need to ensure the PWM port is configured in the Device
    /// Tree before this code will work. Usually this is done by adding the 
    /// overlay to the uEnv.txt file. The Beaglebone Black and Device Tree Overlays
    /// technical note has more information.
    /// 
    /// http://www.ofitselfso.com/BeagleNotes/Beaglebone_Black_And_Device_Tree_Overlays.php
    ///
    /// </summary>
    /// <history>
    ///    20 Aug 15  Cynic - Originally written
    /// </history>
    public class PWMPortFS : PortFS
    {

        // the PWM port we use
        private PWMPortEnum pwmPort = PWMPortEnum.PWM_NONE;      

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pwmPortIn">The PWM port we use</param>
        /// <history>
        ///    20 Aug 15  Cynic - Originally written
        /// </history>
        public PWMPortFS(PWMPortEnum pwmPortIn) : base(GpioEnum.GPIO_NONE)
        {
            pwmPort = pwmPortIn;
            //Console.WriteLine("PWMPort Starts");
            // open the port
            OpenPort();
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the period of the PWM output square wave form
        /// 
        /// </summary>
        /// <value>the period (1/Freq) in nano seconds</value>
        /// <history>
        ///    20 Aug 15 Cynic - Originally written
        /// </history>
        public uint PeriodNS
        { 
            get
            {
                if(portIsOpen != true) throw new Exception("Port "+ PWMPort.ToString() + " is not open");

                // read the contents of the file
                string pwmFileName = BBBDefinitions.PWM_FILENAME_PERIOD.Replace("%port%", ConvertPWMPortEnumToExportNumber(PWMPort).ToString());
                string outStr = System.IO.File.ReadAllText(pwmFileName);

                // return the contents as a UINT
                return Convert.ToUInt32(outStr);
            }
            set
            {
                //Console.WriteLine("Set PeriodNS : " + value.ToString());
                if(portIsOpen != true) throw new Exception("Port "+ PWMPort.ToString() + " is not open");

                // set the period
                string pwmFileName = BBBDefinitions.PWM_FILENAME_PERIOD.Replace("%port%", ConvertPWMPortEnumToExportNumber(PWMPort).ToString());
                System.IO.File.WriteAllText(pwmFileName, value.ToString());
            }
        }
       
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the frequency of the PWM output square wave form
        /// 
        /// </summary>
        /// <value>frequency in Hz</value>
        /// <history>
        ///    20 Aug 15 Cynic - Originally written
        /// </history>
        public uint FrequencyHz
        { 
            get
            {
                UInt64 freqInHz = 1000000000/PeriodNS;
                return (uint)freqInHz;
            }
            set
            {
                UInt64 periodInNs = 1000000000/value;
                PeriodNS = (uint)periodInNs;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the duty cycle of the PWM output square wave form as a percent
        /// 
        /// </summary>
        /// <value>percentage of the input value. Must be between 0 and 100 </value>
        /// <history>
        ///    20 Aug 15 Cynic - Originally written
        /// </history>
        public double DutyPercent
        { 
            get
            {
                return (( float)DutyNS/(float)PeriodNS)*100;
            }
            set
            {
                DutyNS = (uint)((float)PeriodNS*(value/100));
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the duty cycle of the PWM output square wave form in nanoseconds
        /// 
        /// NOTE: the duty cycle is the part of the wave form devoted to a high state
        /// (if polarity is PWM_POLARITY_NORMAL) or the percent of the wave form devoted to
        /// the low state (if polarity is PWM_POLARITY_INVERTED)
        /// 
        /// </summary>
        /// <value>the duty cycle of the PWM output in nanoseconds. This should always
        /// be less than the PeriodNS</value>
        /// <history>
        ///    20 Aug 15 Cynic - Originally written
        /// </history>
        public uint DutyNS
        { 
            get
            {
                if(portIsOpen != true) throw new Exception("Port "+ PWMPort.ToString() + " is not open");

                // read the contents of the file
                string pwmFileName = BBBDefinitions.PWM_FILENAME_DUTY.Replace("%port%", ConvertPWMPortEnumToExportNumber(PWMPort).ToString());
                string outStr = System.IO.File.ReadAllText(pwmFileName);

                // return the contents as a UINT
                return Convert.ToUInt32(outStr);
            }
            set
            {
                //Console.WriteLine("Set DutyNS : " + value.ToString());
                if(portIsOpen != true) throw new Exception("Port "+ PWMPort.ToString() + " is not open");

                // set the duty
                string pwmFileName = BBBDefinitions.PWM_FILENAME_DUTY.Replace("%port%", ConvertPWMPortEnumToExportNumber(PWMPort).ToString());
                System.IO.File.WriteAllText(pwmFileName, value.ToString());
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the run state of the PWM output square wave
        /// 
        /// </summary>
        /// <value>true - begin running, false - stop running</value>
        /// <history>
        ///    20 Aug 15 Cynic - Originally written
        /// </history>
        public bool RunState
        { 
            get
            {
                if(portIsOpen != true) throw new Exception("Port "+ PWMPort.ToString() + " is not open");

                // read the contents of the file
                string pwmFileName = BBBDefinitions.PWM_FILENAME_RUN.Replace("%port%", ConvertPWMPortEnumToExportNumber(PWMPort).ToString());
                string outStr = System.IO.File.ReadAllText(pwmFileName);
                //Console.WriteLine("Get Run State : outStr=" + outStr);

                // return the contents as a UINT
                if (outStr.Trim() == "1") return true;
                else return false;
            }
            set
            {
                // Console.WriteLine("Set Run State : " + value.ToString());
                if(portIsOpen != true) throw new Exception("Port "+ PWMPort.ToString() + " is not open");

                // set the run state
                string outVal = "0";
                if (value == true) outVal = "1";
                string pwmFileName = BBBDefinitions.PWM_FILENAME_RUN.Replace("%port%", ConvertPWMPortEnumToExportNumber(PWMPort).ToString());
                System.IO.File.WriteAllText(pwmFileName, outVal);
            }
        }            

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Opens the port. Throws an exception on failure
        /// 
        /// </summary>
        /// <history>
        ///    20 Aug 15 Cynic - Originally written
        /// </history>
        protected override void OpenPort()
        { 
            //Console.WriteLine("PWMPort Port Opening: "+ BBBDefinitions.PWM_FILENAME_EXPORT +", " + ConvertPWMPortEnumToExportNumber(PWMPort).ToString());

            // do the export
            System.IO.File.WriteAllText(BBBDefinitions.PWM_FILENAME_EXPORT, ConvertPWMPortEnumToExportNumber(PWMPort).ToString());
            portIsOpen = true;

            // the act of writing the PortExportNumber to the export file
            // will create a directory /sys/class/pwm/pwm<PortExportNumber> 
            // This directory contains files which we can use to enable the PWM
            // and set the pulse widths etc

            //Console.WriteLine("PWMPort Port Opened: "+ PWMPort.ToString());
        }


        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Closes the port. 
        /// 
        /// </summary>
        /// <history>
        ///    20 Aug 15 Cynic - Originally written
        /// </history>
        public override void ClosePort()
        {
            // do the unexport
            System.IO.File.WriteAllText(BBBDefinitions.PWM_FILENAME_UNEXPORT, ConvertPWMPortEnumToExportNumber(PWMPort).ToString());
            portIsOpen = false;

            // the act of writing the PortExportNumber to the unexport file
            // will remove the directory /sys/class/pwm/pwm<PortExportNumber> 
    
            //Console.WriteLine("PWMPort Port Closed: "+ PWMPort.ToString());

        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the PWM Port. There is no Set accessor this is set in the constructor
        /// </summary>
        /// <history>
        ///    20 Aug 15 Cynic - Originally written
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
        ///    20 Aug 15 Cynic - Originally written
        /// </history>
        public override PortDirectionEnum PortDirection()
        {
            return PortDirectionEnum.PORTDIR_OUTPUT;
        }           

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// The sysfs system requires a number to be used in order to 
        /// export and manipulate the PWM device we wish to use. This function 
        /// converts the PWMPortEnum value to that number
        /// </summary>
        /// <param name="pwmPortIn">The PWM port we use</param>
        /// <returns>the export number</returns>
        /// <history>
        ///    20 Aug 15  Cynic - Originally written
        ///    21 Nov 15  Cynic - Converted to new PWM Port Enum names
        /// </history>
        public uint ConvertPWMPortEnumToExportNumber(PWMPortEnum pwmPortIn)
        {
            if (pwmPortIn == PWMPortEnum.PWM_P9_22_or_P9_31) return 0; // 0 - EHRPWM0A
            if (pwmPortIn == PWMPortEnum.PWM_P9_21_or_P9_29) return 1; // 1 - EHRPWM0B    
            // 21 Nov 15        if (pwmPortIn == PWMPortEnum.PWM_P9_42) return 2;          // 2 - ECAPPWM0
            if (pwmPortIn == PWMPortEnum.PWM_P9_14_or_P8_36) return 3; // 3 - EHRPWM1A
            if (pwmPortIn == PWMPortEnum.PWM_P9_16_or_P8_34) return 4; // 4 - EHRPWM1B
            if (pwmPortIn == PWMPortEnum.PWM_P8_19_or_P8_45) return 5; // 5 - EHRPWM2A
            if (pwmPortIn == PWMPortEnum.PWM_P8_13_or_P8_46) return 6; // 6 - EHRPWM2B
            // 21 Nov 15 if (pwmPortIn == PWMPortEnum.PWM_P9_28) return 7;          // 7 - ECAPPWM2
            throw new Exception("Unknown PWM Port: "+ pwmPortIn.ToString());
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
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        protected override void Dispose(bool disposing)
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

                // Clean up our code
                //  Console.WriteLine("Disposing PWMPORT");
         
                // call the base to dispose there
                base.Dispose(disposing);

            }
        }
        #endregion

    }
}

