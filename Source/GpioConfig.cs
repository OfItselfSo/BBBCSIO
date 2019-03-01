using System;
using System.IO;

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
    /// Provides a container to correlate HeaderPinNumber, PinMuxRegisterOffset, 
    /// the GPIO and also contain the configuration state of the GPIO
    /// </summary>
    /// <history>
    ///    28 Aug 14  Cynic - Started
    /// </history>
    public class GpioConfig
    {
        public const int NO_GPIOBANK = -1;
        public const int NO_GPIOBIT = -1;
        public const int NO_GPIOMASK = 0;
        public const int NO_HEADER = -1;
        public const int NO_GPIO = -1;
        public const int NO_HEADERPIN = -1;
        public const int NO_MUXPIN = -1;
        public const int NO_PINMUXREGISTEROFFSET = -1;
        public const int NO_GPIOSETTING = 0;
        public const string MUX_UNCLAIMED = "UNCLAIMED";
        public const string GPIO_UNCLAIMED = "UNCLAIMED";

        private GpioEnum gpio = GpioEnum.GPIO_NONE;
        private int gpioNum = NO_GPIO;
        private int headerNum = NO_HEADER;
        private int headerPin = NO_HEADERPIN;
        private int muxPin = NO_MUXPIN;
        private int pinmuxRegisterOffset = NO_PINMUXREGISTEROFFSET;
        private int gpioBank = NO_GPIOBANK;
        private int gpioBit = NO_GPIOBIT;
        private int gpioMask = NO_GPIOMASK;

        private int gpioSetting = NO_GPIOSETTING;
        private string muxOwner="";
        private string gpioOwner="";

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor - only used to set up a dummy default GpioConfig object
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public GpioConfig()
        {
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="headerNumIn">the number of the header (8 or 9)</param>
        /// <param name="headerPinIn">the number of the header pin
        /// <param name="pinmuxRegisterOffsetIn">the pinmux register offset</param>
        /// <param name="gpioNumIn">The gpio as a number</param>
        /// <param name="gpioIn">The gpio as an enum</param>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public GpioConfig(int headerNumIn, int headerPinIn, int pinmuxRegisterOffsetIn, int gpioNumIn, GpioEnum gpioIn)
        {
            // set some values, ignore others - they are for future use
            headerNum = headerNumIn;
            headerPin = headerPinIn;
            pinmuxRegisterOffset = pinmuxRegisterOffsetIn;
            gpioNum = gpioNumIn;
            gpio = gpioIn;

            // these values are pre-calculated. We use them a lot
            gpioBank = GpioNum / 32; // the bank number in the pinmux is the GPIO/32
            gpioBit = GpioNum % 32; // the bit number in the pinmux is the remainder of GPIO/32
            gpioMask = 0x01 << gpioBit; // the mask is a 0x01 shifted left by gpioBits

            // get the information from the system so that we are fully built
            UpdateConfigurationWithPinsFileInfo();
            UpdateConfigurationWithPinmuxPinsFileInfo ();
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the gpioBank
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int GpioBank
        {
            get
            {
                return gpioBank;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the gpioBit
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int GpioBit
        {
            get
            {
                return gpioBit;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the gpioMask
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int GpioMask
        {
            get
            {
                return gpioMask;
            }
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the configuration of a GPIO from the PINUMUX_PINSFILE which provides
        /// us with the muxPin and the gpioMode
        /// </summary>
        /// <returns>z success, nz fail</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public int UpdateConfigurationWithPinsFileInfo()
        {
            char[] splitter = { ' ', '\t', '\n' };
            string[] splitArr = null;

            string hexAddress = PinmuxRegisterAddressAsHexString;

            // read every line in the pins file
            foreach (string line in File.ReadLines(BBBDefinitions.PINUMUX_PINSFILE))
            {
                if (line.Contains(hexAddress))
                {
                    splitArr = line.Split (splitter, StringSplitOptions.RemoveEmptyEntries);
                    if (splitArr.Length == 5)
                    {
                        // set the mux pin
                        muxPin = Convert.ToInt32(splitArr[1]);
                        // set the gpio mode
                        gpioSetting = Convert.ToInt32(splitArr[3], 16);
                    }
                }
            }
            return 0;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the configuration of a GPIO from the PINUMUX_PINSFILE which provides
        /// us with the muxOwner and the gpioOwner
        /// </summary>
        /// <returns>z success, nz fail</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public int UpdateConfigurationWithPinmuxPinsFileInfo()
        {
            char[] splitter = { ' ', '\r', '\t' };
            string[] splitArr = null;

            string hexAddress = PinmuxRegisterAddressAsHexString;

            // read every line in the pins file
            foreach (string line in File.ReadLines(BBBDefinitions.PINUMUX_PINUMUXPINSFILE))
            {
                if (line.Contains(hexAddress))
                {
                    // process the line to remove unfortunate spaces
                    string processedLine = line.Replace ("(MUX UNCLAIMED)", MUX_UNCLAIMED);
                    processedLine = processedLine.Replace ("(GPIO UNCLAIMED)", GPIO_UNCLAIMED);
                    splitArr = processedLine.Split (splitter, StringSplitOptions.RemoveEmptyEntries);
                    if (splitArr.Length >= 5)
                    {
                        // set the mux owner
                        muxOwner = splitArr[3].Trim();
                        // set the gpio owner
                        gpioOwner = splitArr[4].Trim();
                    }
                }
            }
            return 0;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the Gpio Settings as a human readable string
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public string GpioSettingsAsString()
        {
            string pullMode = "";
            string recModeStr = "";
            if (MuxMode == 7) 
            {
                // really the concept of input or output only exists in mode 7 (GPIO Mode)
                if (ReceiverMode != 0) recModeStr = ", Input";
                else recModeStr = ", Output";
                // similarly pullup or pulldown only exists in mode 7 as well
                if (PullupIsActive == true)    pullMode = ", Pullup";
                if (PulldownIsActive == true)    pullMode = ", Pulldown";
            }

            return HeaderNum.ToString()+"_"+
                HeaderPin.ToString() + " " + 
                Gpio.ToString() + 
                " " + EventBank.ToString() + "[" + EventBit.ToString() + "](0x" + EventBit.ToString("x2")+ ")" +
                ", PinMuxAddr=" + PinmuxRegisterAddressAsHexString + 
                " (" + PinmuxRegisterOffsetAsHexString + 
                "/" + PinmuxRegisterDTIndexAsHexString + "), " + 
                "Mux=" + MuxOwner + ", " + 
                "Gpio=" + GpioOwner + ", " + 
                "GpioSet=" + GpioSetting.ToString("x4") + ", " + 
                "MuxMode=" + MuxMode.ToString() + 
                pullMode + recModeStr;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the headerNum
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int HeaderNum
        {
            get
            {
                return headerNum;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the headerPin
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int HeaderPin
        {
            get
            {
                return headerPin;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the muxPin
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int MuxPin
        {
            get
            {
                return muxPin;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pinmuxRegisterOffset
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int PinmuxRegisterOffset
        {
            get
            {
                return pinmuxRegisterOffset;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pinmuxRegisterDTIndex. This is the pinmuxRegisterOffset-0x0800
        ///  the Device Tree Compiler uses this format for its addressing
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int PinmuxRegisterDTIndex
        {
            get
            {
                return pinmuxRegisterOffset-BBBDefinitions.PINMUX_TO_DT_ADDRDIFF;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pinmuxRegisterAddress this is the base+offset
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int PinmuxRegisterAddress
        {
            get
            {
                return BBBDefinitions.PINMUX_BASE_ADDRESS+PinmuxRegisterOffset;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pinmuxRegister address as hex string
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public string PinmuxRegisterAddressAsHexString
        {
            get
            {
                return PinmuxRegisterAddress.ToString("x8");
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pinmuxRegister offset as hex string
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public string PinmuxRegisterOffsetAsHexString
        {
            get
            {
                return PinmuxRegisterOffset.ToString("x3");
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pinmuxRegister offset as hex string
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public string PinmuxRegisterDTIndexAsHexString
        {
            get
            {
                return PinmuxRegisterDTIndex.ToString("x3");
            }
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the muxOwner. Never returns null. May return empty
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public string MuxOwner
        {
            get
            {
                if (muxOwner == null) muxOwner = "";
                return muxOwner;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the gpioOwner. Never returns null. May return empty
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public string GpioOwner
        {
            get
            {
                if (gpioOwner == null)    gpioOwner = "";
                return gpioOwner;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the gpioNum
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int GpioNum
        {
            get
            {
                return gpioNum;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the event bank number. This is related to the GPIO. you will some
        /// times see a gpio listed in the documentation like gpio1_23 or gpio1[23]
        /// the gpio number will be (1*32) + 23 hence GPIO_55. This function
        /// just recovers the bank (the 1 in the above example) from the calculated 
        /// GPIO number. It is needed when configuring certain things (like events) 
        /// in the device tree
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int EventBank
        {
            get
            {
                // the bank number in the pinmux is the GPIO/32
                return GpioNum / 32;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the event bit number. This is related to the GPIO. you will some
        /// times see a gpio listed in the documentation like gpio1_23 or gpio1[23]
        /// the gpio number will be (1*32) + 23 hence GPIO_55. This function
        /// just recovers the number of the bit (the 23 in the above example) from 
        /// the calculated GPIO number. 
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int EventBit
        {
            get
            {
                // the bit number in the pinmux is the remainder of GPIO/32
                return GpioNum % 32;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the Gpio
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public GpioEnum Gpio
        {
            get
            {
                return gpio;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the GpioSettings. This is the raw hex bits that determine the various
        /// mode settings
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int GpioSetting
        {
            get
            {
                return gpioSetting;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the MuxMode. These are the lsb three bits of the gpioSetting. 
        /// Mode 7 111b enables GPIO's
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int MuxMode
        {
            get
            {
                return gpioSetting & 0x07;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Detects if we are GPIO mode (mode 7) enabled
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public bool GpioModeEnabled
        {
            get
            {
                if (MuxMode == 7) return true;
                else return false;    
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Detects if pullups/pulldowns are enabled
        /// </summary>
        /// <returns>true they are enabled, false they are not</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public bool PullupsPulldownsEnabled
        {
            get
            {
                // a 0 at bit 3 means "enabled"
                if ((gpioSetting & 0x08) == 0) return true;
                else return false;    
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Detects if the gpio is in pull up or pull down mode. Note the actual
        /// pull up or pull down is only truely operational if PullupsPulldownsEnabled
        /// is true
        /// </summary>
        /// <returns>1 pullup mode active, 0 pull down mode active</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int PullupPulldownMode
        {
            get
            {
                if ((gpioSetting & 0x10) != 0)
                    return 1;
                else
                    return 0;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Quick way to run through sequence of tests to see if we actually
        /// have an active pullup resistor on the port
        /// </summary>
        /// <returns>true - pullup mode active, false - it is not active</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public bool PullupIsActive
        {
            get
            {
                if (GpioModeEnabled == false)
                    return false;
                if (PullupsPulldownsEnabled == false)
                    return false;
                if (PullupPulldownMode == 0)
                    return false;
                return true;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Quick way to run through sequence of tests to see if we actually
        /// have an active pulldown resistor on the port
        /// </summary>
        /// <returns>true - pulldown mode active, false - it is not active</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public bool PulldownIsActive
        {
            get
            {
                if (GpioModeEnabled == false)
                    return false;
                if (PullupsPulldownsEnabled == false)
                    return false;
                if (PullupPulldownMode == 1)
                    return false;
                return true;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Detects if the gpio receiver mode is active
        /// is true
        /// </summary>
        /// <returns>1 receiver mode active, 0 receiver mode inactive</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int ReceiverMode
        {
            get
            {
                if ((gpioSetting & 0x20) != 0)
                    return 1;
                else
                    return 0;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Detects if the slew control state
        /// is true
        /// </summary>
        /// <returns>1 slew control fast, 0 slew control slow</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Started
        /// </history>
        public int SlewControl
        {
            get
            {
                if ((gpioSetting & 0x40) != 0)
                    return 1;
                else
                    return 0;
            }
        }

    }
}

