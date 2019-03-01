using System;
using System.Text;
using System.IO;
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
    /// Provides Tools and Utilities for BBB GPIO's
    /// </summary>
    /// <history>
    ///    28 Aug 14  Cynic - Started
    /// </history>
    public static class GpioUtils
    {
        public const string GPIOENUM_PREFIX = "GPIO_";

        // used for external file open calls
        const int O_RDONLY = 0x0;
        const int O_NONBLOCK = 0x0004;

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the name (+full path) to a sysfs event file which matches the input
        /// name. The input name will have been set in the Device Tree when the GPIO 
        /// was configured to present interrupts. There can be multiple GPIOs reporting
        /// back through any one event device. The gpio which triggered the event
        /// can be differentiated by the "linux,code" field set on the gpio in the 
        /// Device Tree. To really understand what is going on here you would be
        /// well advised to read the documenation.
        /// 
        /// Note: a device tree node with a label of BBBCSIO_GPIOs will be named something
        ///       like BBBCSIO_GPIOs.7 in the device. The kernel tacks the .7 (or some other
        ///       digit on at boot time and it refers to the order the driver was loaded.
        ///       Since this digit could change, we strip off everything from the input name
        ///       after the last '.' character. We match based on the label!!!
        /// 
        /// </summary>
        /// <returns>The matching device event filename or null for fail</returns>
        /// <param name="nameToLookFOr">the device tree node label which forms the device name (see Note above)</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public static string FindNamedEventDevice(string nameToLookFor)
        {
            string cleanedName = null;
            string deviceName = null;

            if ((nameToLookFor == null) || (nameToLookFor.Length == 0))    return null;

            // get all the event devices. there will be keyboards and mice etc 
            // in here as well as a gpio events (or maybe more than one)
            List<string> deviceList = GetEventDevices();
            if (deviceList.Count == 0) return null;

            // for each device file
            foreach (string deviceFileName in deviceList)
            {
                // get the name from each event device
                deviceName = GetNameFromDevice(deviceFileName);
                if ((deviceName == null) || (deviceName.Length == 0)) continue;
                // clean off the trailing .<digit>
                int lastDotIndex = deviceName.LastIndexOf (".");
                if (lastDotIndex > 0)
                {
                    cleanedName = deviceName.Substring (0, lastDotIndex);
                } else
                {
                    // not found
                    cleanedName = deviceName;
                }
                // now test
                if (cleanedName == nameToLookFor)
                {
                    // found it, return it
                    return deviceFileName;                        
                }
            }
            return null;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the name from a device. We use an external ioctl() call to do this
        /// </summary>
        /// <param name="deviceFileName">the file name for the device</param>
        /// <returns>>The name of the device or null for not found</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        private static string GetNameFromDevice(string deviceFileName)
        {
            int retVal = -1;
            int fd = -1;

            // this is a code in a specific format required
            // by the event device and is passed through as 
            // by the ioctl call 
            // (2 <<30) | (255<<16) | ('E'<<8) | (6<<0);
            int requestVAL = unchecked((int)0x80ff4506);

            // we get our name back in this
            StringBuilder eventName = new StringBuilder (256);

            // sanity check
            if ((deviceFileName == null) || (deviceFileName.Length == 0)) return null;

            try
            {
                // we open the file. We have to have an open file descriptor
                // note this is an external call. It has to be because the 
                // ioctl needs an open file descriptor it can use
                fd = ExternalFileOpen(deviceFileName, O_RDONLY|O_NONBLOCK);
                if(fd <= 0)
                {
                    throw new Exception("Could not open file:" + deviceFileName);
                }
                // this is an external call to the libc.so.6 library
                retVal = ExternalIoCtl(fd, requestVAL, eventName);
                // did the call succeed?
                if(retVal < 0)
                {
                    // it failed
                    throw new Exception("Fetch of name from device " + deviceFileName + " failed");
                }
                // this is 
                ExternalFileClose(fd);
                // return the event name
                return eventName.ToString();
            }
            finally
            {
                // we have to close the file descriptor
                if(fd > 0) ExternalFileClose(fd);
                fd = -1;
            }

        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets a list of all event devices. These are the device files which
        /// expose sysfs events to userspace. Never returns null.
        /// </summary>
        /// <returns>A list of the event files (full path) or empty list for not found</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public static List<string> GetEventDevices()
        {
            List<string> outList = new List<string>();
            string patternToMatch = BBBDefinitions.SYSFS_EVENTFILEBASENAME+"*";
            foreach (string fileName in Directory.GetFiles(BBBDefinitions.SYSFS_EVENTDIR, patternToMatch))
            {
                outList.Add (fileName);
            }
            return outList;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Converts a GpioID to an integer value. Will not accept a gpioID of 
        /// Gpio.GPIO_NONE or GpioEnum.GPIO_SYSEVENT
        /// </summary>
        /// <param name="gpioIDIn">The gpio we open the port on</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public static int GpioIDToInt(GpioEnum gpioIDIn)
        {
            if (gpioIDIn == GpioEnum.GPIO_NONE) 
            {
                throw new Exception ("Invalid Gpio : " + gpioIDIn.ToString ());
            }

            // return it as an integer
            return Convert.ToInt32(GpioIDToString(gpioIDIn));
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Converts a GpioID to a numeric string value. Will not accept a gpioID of 
        /// Gpio.GPIO_NONE or GpioEnum.GPIO_SYSEVENT
        /// </summary>
        /// <param name="gpioIDIn">The gpio we open the port on</param>
        /// <returns>>the GPIO as a string</returns>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public static string GpioIDToString(GpioEnum gpioIDIn)
        {
            if (gpioIDIn == GpioEnum.GPIO_NONE) 
            {
                throw new Exception ("Invalid Gpio : " + gpioIDIn.ToString ());
            }

            // strip out non digits - want to do this fast so no regex
            // or cycling through the string testing every char.
            return gpioIDIn.ToString().Replace(GPIOENUM_PREFIX, "");
        }                

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets a GpioConfig dictionary which uses the RegisterAddress of the pin
        /// as a key. Note the RegisterAddress is Base+RegisterOffset. Never returns
        /// null.
        /// </summary>
        /// <returns>A dictionary of GpioConfig objects indexed by RegisterAddress</returns>
        /// <history>
        ///    28 Aug 14  Cynic  Originally written
        /// </history>
        public static Dictionary<int, GpioConfig> GetRegisterAddressDictionary()
        {
            // build the dictionary
            Dictionary<int, GpioConfig> outDict = new Dictionary<int, GpioConfig>();

            // get all known pin to Gpio correlations
            List<GpioConfig> pin2GpioList = BuildAllKnownGpioConfigObjects();

            // add to the dictionary
            foreach (GpioConfig pin2GpioObj in pin2GpioList)
            {
                outDict.Add (BBBDefinitions.PINMUX_BASE_ADDRESS + pin2GpioObj.PinmuxRegisterOffset, pin2GpioObj);
            }
            return outDict;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets a GpioConfig object for a given gpio object. NOTE when the 
        /// GpioConfig object is instantiated it acesses system information
        /// for things like mux pin number and usage in the pinmux pins table
        /// </summary>
        /// <returns>>the GpioConfig object or a dummy (GpioEnum.GPIO_NONE) for fail</returns>
        /// <history>
        ///    28 Aug 14  Cynic  Originally written
        /// </history>
        public static GpioConfig GetGpioConfigForGpio(GpioEnum gpioIn)
        {
            switch (gpioIn)
            {
                case GpioEnum.GPIO_38:
                    return new GpioConfig(8, 3, 0x818, 38, GpioEnum.GPIO_38);
                case GpioEnum.GPIO_39:
                    return new GpioConfig(8, 4, 0x81C, 39, GpioEnum.GPIO_39);
                case GpioEnum.GPIO_34:
                    return new GpioConfig(8, 5, 0x808, 34, GpioEnum.GPIO_34);
                case GpioEnum.GPIO_35:
                    return new GpioConfig(8, 6, 0x80C, 35, GpioEnum.GPIO_35);
                case GpioEnum.GPIO_66:
                    return new GpioConfig(8, 7, 0x890, 66, GpioEnum.GPIO_66);
                case GpioEnum.GPIO_67:
                    return new GpioConfig(8, 8, 0x894, 67, GpioEnum.GPIO_67);
                case GpioEnum.GPIO_69:
                    return new GpioConfig(8, 9, 0x89C, 69, GpioEnum.GPIO_69);
                case GpioEnum.GPIO_68:
                    return new GpioConfig(8, 10, 0x898, 68, GpioEnum.GPIO_68);
                case GpioEnum.GPIO_45:
                    return new GpioConfig(8, 11, 0x834, 45, GpioEnum.GPIO_45);
                case GpioEnum.GPIO_44:
                    return new GpioConfig(8, 12, 0x830, 44, GpioEnum.GPIO_44);
                case GpioEnum.GPIO_23:
                    return new GpioConfig(8, 13, 0x824, 23, GpioEnum.GPIO_23);
                case GpioEnum.GPIO_26:
                    return new GpioConfig(8, 14, 0x828, 26, GpioEnum.GPIO_26);
                case GpioEnum.GPIO_47:
                    return new GpioConfig(8, 15, 0x83C, 47, GpioEnum.GPIO_47);
                case GpioEnum.GPIO_46:
                    return new GpioConfig(8, 16, 0x838, 46, GpioEnum.GPIO_46);
                case GpioEnum.GPIO_27:
                    return new GpioConfig(8, 17, 0x82C, 27, GpioEnum.GPIO_27);
                case GpioEnum.GPIO_65:
                    return new GpioConfig(8, 18, 0x88C, 65, GpioEnum.GPIO_65);
                case GpioEnum.GPIO_22:
                    return new GpioConfig(8, 19, 0x820, 22, GpioEnum.GPIO_22);
                case GpioEnum.GPIO_63:
                    return new GpioConfig(8, 20, 0x884, 63, GpioEnum.GPIO_63);
                case GpioEnum.GPIO_62:
                    return new GpioConfig(8, 21, 0x880, 62, GpioEnum.GPIO_62);
                case GpioEnum.GPIO_37:
                    return new GpioConfig(8, 22, 0x814, 37, GpioEnum.GPIO_37);
                case GpioEnum.GPIO_36:
                    return new GpioConfig(8, 23, 0x810, 36, GpioEnum.GPIO_36);
                case GpioEnum.GPIO_33:
                    return new GpioConfig(8, 24, 0x804, 33, GpioEnum.GPIO_33);
                case GpioEnum.GPIO_32:
                    return new GpioConfig(8, 25, 0x800, 32, GpioEnum.GPIO_32);
                case GpioEnum.GPIO_61:
                    return new GpioConfig(8, 26, 0x87C, 61, GpioEnum.GPIO_61);
                case GpioEnum.GPIO_86:
                    return new GpioConfig(8, 27, 0x8E0, 86, GpioEnum.GPIO_86);
                case GpioEnum.GPIO_88:
                    return new GpioConfig(8, 28, 0x8E8, 88, GpioEnum.GPIO_88);
                case GpioEnum.GPIO_87:
                    return new GpioConfig(8, 29, 0x8E4, 87, GpioEnum.GPIO_87);
                case GpioEnum.GPIO_89:
                    return new GpioConfig(8, 30, 0x8EC, 89, GpioEnum.GPIO_89);
                case GpioEnum.GPIO_10:
                    return new GpioConfig(8, 31, 0x8D8, 10, GpioEnum.GPIO_10);
                case GpioEnum.GPIO_11:
                    return new GpioConfig(8, 32, 0x8DC, 11, GpioEnum.GPIO_11);
                case GpioEnum.GPIO_9:
                    return new GpioConfig(8, 33, 0x8D4, 9, GpioEnum.GPIO_9);
                case GpioEnum.GPIO_81:
                    return new GpioConfig(8, 34, 0x8CC, 81, GpioEnum.GPIO_81);
                case GpioEnum.GPIO_8:
                    return new GpioConfig(8, 35, 0x8D0, 8, GpioEnum.GPIO_8);
                case GpioEnum.GPIO_80:
                    return new GpioConfig(8, 36, 0x8C8, 80, GpioEnum.GPIO_80);
                case GpioEnum.GPIO_78:
                    return new GpioConfig(8, 37, 0x8C0, 78, GpioEnum.GPIO_78);
                case GpioEnum.GPIO_79:
                    return new GpioConfig(8, 38, 0x8C4, 79, GpioEnum.GPIO_79);
                case GpioEnum.GPIO_76:
                    return new GpioConfig(8, 39, 0x8B8, 76, GpioEnum.GPIO_76);
                case GpioEnum.GPIO_77:
                    return new GpioConfig(8, 40, 0x8BC, 77, GpioEnum.GPIO_77);
                case GpioEnum.GPIO_74:
                    return new GpioConfig(8, 41, 0x8B0, 74, GpioEnum.GPIO_74);
                case GpioEnum.GPIO_75:
                    return new GpioConfig(8, 42, 0x8B4, 75, GpioEnum.GPIO_75);
                case GpioEnum.GPIO_72:
                    return new GpioConfig(8, 43, 0x8A8, 72, GpioEnum.GPIO_72);
                case GpioEnum.GPIO_73:
                    return new GpioConfig(8, 44, 0x8AC, 73, GpioEnum.GPIO_73);
                case GpioEnum.GPIO_70:
                    return new GpioConfig(8, 45, 0x8A0, 70, GpioEnum.GPIO_70);
                case GpioEnum.GPIO_71:
                    return new GpioConfig(8, 46, 0x8A4, 71, GpioEnum.GPIO_71);
                case GpioEnum.GPIO_30:
                    return new GpioConfig(9, 11, 0x870, 30, GpioEnum.GPIO_30);
                case GpioEnum.GPIO_60:
                    return new GpioConfig(9, 12, 0x878, 60, GpioEnum.GPIO_60);
                case GpioEnum.GPIO_31:
                    return new GpioConfig(9, 13, 0x874, 31, GpioEnum.GPIO_31);
                case GpioEnum.GPIO_50:
                    return new GpioConfig(9, 14, 0x848, 50, GpioEnum.GPIO_50);
                case GpioEnum.GPIO_48:
                    return new GpioConfig(9, 15, 0x840, 48, GpioEnum.GPIO_48);
                case GpioEnum.GPIO_51:
                    return new GpioConfig(9, 16, 0x84C, 51, GpioEnum.GPIO_51);
                case GpioEnum.GPIO_5:
                    return new GpioConfig(9, 17, 0x95C, 5, GpioEnum.GPIO_5);
                case GpioEnum.GPIO_4:
                    return new GpioConfig(9, 18, 0x958, 4, GpioEnum.GPIO_4);
                case GpioEnum.GPIO_13:
                    return new GpioConfig(9, 19, 0x97C, 13, GpioEnum.GPIO_13);
                case GpioEnum.GPIO_12:
                    return new GpioConfig(9, 20, 0x978, 12, GpioEnum.GPIO_12);
                case GpioEnum.GPIO_3:
                    return new GpioConfig(9, 21, 0x954, 3, GpioEnum.GPIO_3);
                case GpioEnum.GPIO_2:
                    return new GpioConfig(9, 22, 0x950, 2, GpioEnum.GPIO_2);
                case GpioEnum.GPIO_49:
                    return new GpioConfig(9, 23, 0x844, 49, GpioEnum.GPIO_49);
                case GpioEnum.GPIO_15:
                    return new GpioConfig(9, 24, 0x984, 15, GpioEnum.GPIO_15);
                case GpioEnum.GPIO_117:
                    return new GpioConfig(9, 25, 0x9AC, 117, GpioEnum.GPIO_117);
                case GpioEnum.GPIO_14:
                    return new GpioConfig(9, 26, 0x980, 14, GpioEnum.GPIO_14);
                case GpioEnum.GPIO_115:
                    return new GpioConfig(9, 27, 0x9A4, 115, GpioEnum.GPIO_115);
                case GpioEnum.GPIO_113:
                    return new GpioConfig(9, 28, 0x99C, 113, GpioEnum.GPIO_113);
                case GpioEnum.GPIO_111:
                    return new GpioConfig(9, 29, 0x994, 111, GpioEnum.GPIO_111);
                case GpioEnum.GPIO_112:
                    return new GpioConfig(9, 30, 0x998, 112, GpioEnum.GPIO_112);
                case GpioEnum.GPIO_110:
                    return new GpioConfig(9, 31, 0x990, 110, GpioEnum.GPIO_110);
                case GpioEnum.GPIO_20:
                    return new GpioConfig(9, 41, 0x9B4, 20, GpioEnum.GPIO_20);
                case GpioEnum.GPIO_7:
                    return new GpioConfig(9, 42, 0x964, 7, GpioEnum.GPIO_7);
                default:
                    return new GpioConfig();
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Get all known GpioConfig objects. These are returned with hard coded 
        /// contents based on the documentation and not fully filled in with 
        /// information derived from the system. Never returns NULL.
        /// </summary>
        /// <returns>>a list of all known GpioConfig objects</returns>
        /// <history>
        ///    28 Aug 14  Cynic  Originally written
        /// </history>
        public static List<GpioConfig> BuildAllKnownGpioConfigObjects()
        {
            List<GpioConfig> outList = new List<GpioConfig> ();

            // run down through the GpioEnum and create one for every
            // value it contains. 
            foreach (GpioEnum gpioVal in (GpioEnum[])Enum.GetValues(typeof(GpioEnum)))
            {
                // never do this one
                if (gpioVal == GpioEnum.GPIO_NONE) continue;
                outList.Add(GetGpioConfigForGpio(gpioVal));
            }
            return outList;

/* slightly less old way
            outList.Add(new GpioConfig(8, 3, 0x818, 38,  GpioEnum.GPIO_38));
            outList.Add(new GpioConfig(8, 4, 0x81C, 39,  GpioEnum.GPIO_39));
            outList.Add(new GpioConfig(8, 5, 0x808, 34,  GpioEnum.GPIO_34));
            outList.Add(new GpioConfig(8, 6, 0x80C, 35,  GpioEnum.GPIO_35));
            outList.Add(new GpioConfig(8, 7, 0x890, 66,  GpioEnum.GPIO_66));
            outList.Add(new GpioConfig(8, 8, 0x894, 67,  GpioEnum.GPIO_67));
            outList.Add(new GpioConfig(8, 9, 0x89C, 69,  GpioEnum.GPIO_69));
            outList.Add(new GpioConfig(8, 10, 0x898, 68,  GpioEnum.GPIO_68));
            outList.Add(new GpioConfig(8, 11, 0x834, 45,  GpioEnum.GPIO_45));
            outList.Add(new GpioConfig(8, 12, 0x830, 44,  GpioEnum.GPIO_44));
            outList.Add(new GpioConfig(8, 13, 0x824, 23,  GpioEnum.GPIO_23));
            outList.Add(new GpioConfig(8, 14, 0x828, 26,  GpioEnum.GPIO_26));
            outList.Add(new GpioConfig(8, 15, 0x83C, 47,  GpioEnum.GPIO_47));
            outList.Add(new GpioConfig(8, 16, 0x838, 46,  GpioEnum.GPIO_46));
            outList.Add(new GpioConfig(8, 17, 0x82C, 27,  GpioEnum.GPIO_27));
            outList.Add(new GpioConfig(8, 18, 0x88C, 65,  GpioEnum.GPIO_65));
            outList.Add(new GpioConfig(8, 19, 0x820, 22,  GpioEnum.GPIO_22));
            outList.Add(new GpioConfig(8, 20, 0x884, 63,  GpioEnum.GPIO_63));
            outList.Add(new GpioConfig(8, 21, 0x880, 62,  GpioEnum.GPIO_62));
            outList.Add(new GpioConfig(8, 22, 0x814, 37,  GpioEnum.GPIO_37));
            outList.Add(new GpioConfig(8, 23, 0x810, 36,  GpioEnum.GPIO_36));
            outList.Add(new GpioConfig(8, 24, 0x804, 33,  GpioEnum.GPIO_33));
            outList.Add(new GpioConfig(8, 25, 0x800, 32,  GpioEnum.GPIO_32));
            outList.Add(new GpioConfig(8, 26, 0x87C, 61,  GpioEnum.GPIO_61));
            outList.Add(new GpioConfig(8, 27, 0x8E0, 86,  GpioEnum.GPIO_86));
            outList.Add(new GpioConfig(8, 28, 0x8E8, 88,  GpioEnum.GPIO_88));
            outList.Add(new GpioConfig(8, 29, 0x8E4, 87,  GpioEnum.GPIO_87));
            outList.Add(new GpioConfig(8, 30, 0x8EC, 89,  GpioEnum.GPIO_89));
            outList.Add(new GpioConfig(8, 31, 0x8D8, 10,  GpioEnum.GPIO_10));
            outList.Add(new GpioConfig(8, 32, 0x8DC, 11,  GpioEnum.GPIO_11));
            outList.Add(new GpioConfig(8, 33, 0x8D4, 9,  GpioEnum.GPIO_9));
            outList.Add(new GpioConfig(8, 34, 0x8CC, 81,  GpioEnum.GPIO_81));
            outList.Add(new GpioConfig(8, 35, 0x8D0, 8,  GpioEnum.GPIO_8));
            outList.Add(new GpioConfig(8, 36, 0x8C8, 80,  GpioEnum.GPIO_80));
            outList.Add(new GpioConfig(8, 37, 0x8C0, 78,  GpioEnum.GPIO_78));
            outList.Add(new GpioConfig(8, 38, 0x8C4, 79,  GpioEnum.GPIO_79));
            outList.Add(new GpioConfig(8, 39, 0x8B8, 76,  GpioEnum.GPIO_76));
            outList.Add(new GpioConfig(8, 40, 0x8BC, 77,  GpioEnum.GPIO_77));
            outList.Add(new GpioConfig(8, 41, 0x8B0, 74,  GpioEnum.GPIO_74));
            outList.Add(new GpioConfig(8, 42, 0x8B4, 75,  GpioEnum.GPIO_75));
            outList.Add(new GpioConfig(8, 43, 0x8A8, 72,  GpioEnum.GPIO_72));
            outList.Add(new GpioConfig(8, 44, 0x8AC, 73,  GpioEnum.GPIO_73));
            outList.Add(new GpioConfig(8, 45, 0x8A0, 70,  GpioEnum.GPIO_70));
            outList.Add(new GpioConfig(8, 46, 0x8A4, 71,  GpioEnum.GPIO_71));
            outList.Add(new GpioConfig(9, 11, 0x870, 30,  GpioEnum.GPIO_30));
            outList.Add(new GpioConfig(9, 12, 0x878, 60,  GpioEnum.GPIO_60));
            outList.Add(new GpioConfig(9, 13, 0x874, 31,  GpioEnum.GPIO_31));
            outList.Add(new GpioConfig(9, 14, 0x848, 50,  GpioEnum.GPIO_50));
            outList.Add(new GpioConfig(9, 15, 0x840, 48,  GpioEnum.GPIO_48));
            outList.Add(new GpioConfig(9, 16, 0x84C, 51,  GpioEnum.GPIO_51));
            outList.Add(new GpioConfig(9, 17, 0x95C, 5,  GpioEnum.GPIO_5));
            outList.Add(new GpioConfig(9, 18, 0x958, 4,  GpioEnum.GPIO_4));
            outList.Add(new GpioConfig(9, 19, 0x97C, 13,  GpioEnum.GPIO_13));
            outList.Add(new GpioConfig(9, 20, 0x978, 12,  GpioEnum.GPIO_12));
            outList.Add(new GpioConfig(9, 21, 0x954, 3,  GpioEnum.GPIO_3));
            outList.Add(new GpioConfig(9, 22, 0x950, 2,  GpioEnum.GPIO_2));
            outList.Add(new GpioConfig(9, 23, 0x844, 49,  GpioEnum.GPIO_49));
            outList.Add(new GpioConfig(9, 24, 0x984, 15,  GpioEnum.GPIO_15));
            outList.Add(new GpioConfig(9, 25, 0x9AC, 117,  GpioEnum.GPIO_117));
            outList.Add(new GpioConfig(9, 26, 0x980, 14,  GpioEnum.GPIO_14));
            outList.Add(new GpioConfig(9, 27, 0x9A4, 115,  GpioEnum.GPIO_115));
            outList.Add(new GpioConfig(9, 28, 0x99C, 113,  GpioEnum.GPIO_113));
            outList.Add(new GpioConfig(9, 29, 0x994, 111,  GpioEnum.GPIO_111));
            outList.Add(new GpioConfig(9, 30, 0x998, 112,  GpioEnum.GPIO_112));
            outList.Add(new GpioConfig(9, 31, 0x990, 110,  GpioEnum.GPIO_110));
            outList.Add(new GpioConfig(9, 41, 0x9B4, 20,  GpioEnum.GPIO_20));
            outList.Add(new GpioConfig(9, 42, 0x964, 7,  GpioEnum.GPIO_7));
 */

/* older method with mode usage labels
            // add the Pins for P8 header
            outList.Add(new GpioConfig(8,3,"GPMC_AD6",0x818,"gpmc_ad6","mmc1_dat6","","","","","","*gpio1_6","emmc"));
            outList.Add(new GpioConfig(8,4,"GPMC_AD7",0x81C,"gpmc_ad7","mmc1_dat7","","","","","","*gpio1_7","emmc"));
            outList.Add(new GpioConfig(8,5,"GPMC_AD2",0x808,"gpmc_ad2","mmc1_dat2","","","","","","*gpio1_2","emmc"));
            outList.Add(new GpioConfig(8,6,"GPMC_AD3",0x80C,"gpmc_ad3","mmc1_dat3","","","","","","*gpio1_3","emmc"));
            outList.Add(new GpioConfig(8,7,"GPMC_ADVn_ALE",0x890,"gpmc_advn_ale","","timer4","","","","","*gpio2_2",""));
            outList.Add(new GpioConfig(8,8,"GPMC_OEn_REn",0x894,"gpmc_oen_ren","","timer7","","","","","*gpio2_3",""));
            outList.Add(new GpioConfig(8,9,"GPMC_BEn0_CLE",0x89C,"gpmc_be0n_cle","","timer5","","","","","*gpio2_5",""));
            outList.Add(new GpioConfig(8,10,"GPMC_WEn",0x898,"gpmc_wen","","timer6","","","","","*gpio2_4",""));
            outList.Add(new GpioConfig(8,11,"GPMC_AD13",0x834,"gpmc_ad13","lcd_data18","mmc1_dat5","mmc2_dat1","eQEP2B_in","pr1_mii0_txd1","pr1_pru0_pru_r30_15","*gpio1_13",""));
            outList.Add(new GpioConfig(8,12,"GPMC_AD12",0x830,"gpmc_ad12","lcd_data19","mmc1_dat4","mmc2_dat0","eQEP2A_in","pr1_mii0_txd2","pr1_pru0_pru_r30_14","*gpio1_12",""));
            outList.Add(new GpioConfig(8,13,"GPMC_AD9",0x824,"gpmc_ad9","lcd_data22","mmc1_dat1","mmc2_dat5","ehrpwm2B","pr1_mii0_col","","*gpio0_23",""));
            outList.Add(new GpioConfig(8,14,"GPMC_AD10",0x828,"gpmc_ad10","lcd_data21","mmc1_dat2","mmc2_dat6","ehrpwm2_tripzone_input","pr1_mii0_txen","","*gpio0_26",""));
            outList.Add(new GpioConfig(8,15,"GPMC_AD15",0x83C,"gpmc_ad15","lcd_data16","mmc1_dat7","mmc2_dat3","eQEP2_strobe","pr1_ecap0_ecap_capin_apwm_o","pr1_pru0_pru_r31_15","*gpio1_15",""));
            outList.Add(new GpioConfig(8,16,"GPMC_AD14",0x838,"gpmc_ad14","lcd_data17","mmc1_dat6","mmc2_dat2","eQEP2_index","pr1_mii0_txd0","pr1_pru0_pru_r31_14","*gpio1_14",""));
            outList.Add(new GpioConfig(8,17,"GPMC_AD11",0x82C,"gpmc_ad11","lcd_data20","mmc1_dat3","mmc2_dat7","ehrpwm0_synco","pr1_mii0_txd3","","*gpio0_27",""));
            outList.Add(new GpioConfig(8,18,"GPMC_CLK",0x88C,"gpmc_clk","lcd_memory_clk","gpmc_wait1","mmc2_clk","pr1_mii1_crs","pr1_mdio_mdclk","mcasp0_fsr","*gpio2_1",""));
            outList.Add(new GpioConfig(8,19,"GPMC_AD8",0x820,"gpmc_ad8","lcd_data23","mmc1_dat0","mmc2_dat4","ehrpwm2A","pr1_mii_mt0_clk","","*gpio0_22",""));
            outList.Add(new GpioConfig(8,20,"GPMC_CSn2",0x884,"gpmc_csn2","gpmc_be1n","mmc1_cmd","pr1_edio_data_in7","pr1_edio_data_out7","pr1_pru1_pru_r30_13","pr1_pru1_pru_r31_13","*gpio1_31","emmc"));
            outList.Add(new GpioConfig(8,21,"GPMC_CSn1",0x880,"gpmc_csn1","gpmc_clk","mmc1_clk","pr1_edio_data_in6","pr1_edio_data_out6","pr1_pru1_pru_r30_12","pr1_pru1_pru_r31_12","*gpio1_30","emmc"));
            outList.Add(new GpioConfig(8,22,"GPMC_AD5",0x814,"gpmc_ad5","mmc1_dat5","","","","","","*gpio1_5","emmc"));
            outList.Add(new GpioConfig(8,23,"GPMC_AD4",0x810,"gpmc_ad4","mmc1_dat4","","","","","","*gpio1_4","emmc"));
            outList.Add(new GpioConfig(8,24,"GPMC_AD1",0x804,"gpmc_ad1","mmc1_dat1","","","","","","*gpio1_1","emmc"));
            outList.Add(new GpioConfig(8,25,"GPMC_AD0",0x800,"gpmc_ad0","mmc1_dat0","","","","","","*gpio1_0","emmc"));
            outList.Add(new GpioConfig(8,26,"GPMC_CSn0",0x87C,"gpmc_csn0","","","","","","","*gpio1_29",""));
            outList.Add(new GpioConfig(8,27,"LCD_VSYNC",0x8E0,"lcd_vsync","gpmc_a8","gpmc_a1","pr1_edio_data_in2","pr1_edio_data_out2","pr1_pru1_pru_r30_8","pr1_pru1_pru_r31_8","*gpio2_22","hdmi"));
            outList.Add(new GpioConfig(8,28,"LCD_PCLK",0x8E8,"lcd_pclk","gpmc_a10","pr1_mii0_crs","pr1_edio_data_in4","pr1_edio_data_out4","pr1_pru1_pru_r30_10","pr1_pru1_pru_r31_10","*gpio2_24","hdmi"));
            outList.Add(new GpioConfig(8,29,"LCD_HSYNC",0x8E4,"lcd_hsync","gpmc_a9","gpmc_a2","pr1_edio_data_in3","pr1_edio_data_out3","pr1_pru1_pru_r30_9","pr1_pru1_pru_r31_9","*gpio2_23","hdmi"));
            outList.Add(new GpioConfig(8,30,"LCD_AC_BIAS_EN",0x8EC,"lcd_ac_bias_en","gpmc_a11","pr1_mii1_crs","pr1_edio_data_in5","pr1_edio_data_out5","pr1_pru1_pru_r30_11","pr1_pru1_pru_r31_11","*gpio2_25","hdmi"));
            outList.Add(new GpioConfig(8,31,"LCD_DATA14",0x8D8,"lcd_data14","gpmc_a18","eQEP1_index","mcasp0_axr1","uart5_rxd","pr1_mii_mr0_clk","uart5_ctsn","*gpio0_10","hdmi"));
            outList.Add(new GpioConfig(8,32,"LCD_DATA15",0x8DC,"lcd_data15","gpmc_a19","eQEP1_strobe","mcasp0_ahclkx","mcasp0_axr3","pr1_mii0_rxdv","uart5_rtsn","*gpio0_11","hdmi"));
            outList.Add(new GpioConfig(8,33,"LCD_DATA13",0x8D4,"lcd_data13","gpmc_a17","eQEP1B_in","mcasp0_fsr","mcasp0_axr3","pr1_mii0_rxer","uart4_rtsn","*gpio0_9","hdmi"));
            outList.Add(new GpioConfig(8,34,"LCD_DATA11",0x8CC,"lcd_data11","gpmc_a15","ehrpwm1B","mcasp0_ahclkr","mcasp0_axr2","pr1_mii0_rxd0","uart3_rtsn","*gpio2_17","hdmi"));
            outList.Add(new GpioConfig(8,35,"LCD_DATA12",0x8D0,"lcd_data12","gpmc_a16","eQEP1A_in","mcasp0_aclkr","mcasp0_axr2","pr1_mii0_rxlink","uart4_ctsn","*gpio0_8","hdmi"));
            outList.Add(new GpioConfig(8,36,"LCD_DATA10",0x8C8,"lcd_data10","gpmc_a14","ehrpwm1A","mcasp0_axr0","","pr1_mii0_rxd1","uart3_ctsn","*gpio2_16","hdmi"));
            outList.Add(new GpioConfig(8,37,"LCD_DATA8",0x8C0,"lcd_data8","gpmc_a12","ehrpwm1_tripzone_input","mcasp0_aclkx","uart5_txd","pr1_mii0_rxd3","uart2_ctsn","*gpio2_14","hdmi"));
            outList.Add(new GpioConfig(8,38,"LCD_DATA9",0x8C4,"lcd_data9","gpmc_a13","ehrpwm0_synco","mcasp0_fsx","uart5_rxd","pr1_mii0_rxd2","uart2_rtsn","*gpio2_15","hdmi"));
            outList.Add(new GpioConfig(8,39,"LCD_DATA6",0x8B8,"lcd_data6","gpmc_a6","pr1_edio_data_in6","eQEP2_index","pr1_edio_data_out6","pr1_pru1_pru_r30_6","pr1_pru1_pru_r31_6","*gpio2_12","hdmi"));
            outList.Add(new GpioConfig(8,40,"LCD_DATA7",0x8BC,"lcd_data7","gpmc_a7","pr1_edio_data_in7","eQEP2_strobe","pr1_edio_data_out7","pr1_pru1_pru_r30_7","pr1_pru1_pru_r31_7","*gpio2_13","hdmi"));
            outList.Add(new GpioConfig(8,41,"LCD_DATA4",0x8B0,"lcd_data4","gpmc_a4","pr1_mii0_txd1","eQEP2A_in","","pr1_pru1_pru_r30_4","pr1_pru1_pru_r31_4","*gpio2_10","hdmi"));
            outList.Add(new GpioConfig(8,42,"LCD_DATA5",0x8B4,"lcd_data5","gpmc_a5","pr1_mii0_txd0","eQEP2B_in","","pr1_pru1_pru_r30_5","pr1_pru1_pru_r31_5","*gpio2_11","hdmi"));
            outList.Add(new GpioConfig(8,43,"LCD_DATA2",0x8A8,"lcd_data2","gpmc_a2","pr1_mii0_txd3","ehrpwm2_tripzone_input","","pr1_pru1_pru_r30_2","pr1_pru1_pru_r31_2","*gpio2_8","hdmi"));
            outList.Add(new GpioConfig(8,44,"LCD_DATA3",0x8AC,"lcd_data3","gpmc_a3","pr1_mii0_txd2","ehrpwm0_synco","","pr1_pru1_pru_r30_3","pr1_pru1_pru_r31_3","*gpio2_9","hdmi"));
            outList.Add(new GpioConfig(8,45,"LCD_DATA0",0x8A0,"lcd_data0","gpmc_a0","pr1_mii_mt0_clk","ehrpwm2A","","pr1_pru1_pru_r30_0","pr1_pru1_pru_r31_0","*gpio2_6","hdmi"));
            outList.Add(new GpioConfig(8,46,"LCD_DATA1",0x8A4,"lcd_data1","gpmc_a1","pr1_mii0_txen","ehrpwm2B","","pr1_pru1_pru_r30_1","pr1_pru1_pru_r31_1","*gpio2_7","hdmi"));

            // add the Pins for P9 header
            outList.Add(new GpioConfig(9,11,"GPMC_WAIT0",0x870,"gpmc_wait0","gmii2_crs","gpmc_csn4","rmii2_crs_dv","mmc1_sdcd","pr1_mii1_col","uart4_rxd","*gpio0_30"));
            outList.Add(new GpioConfig(9,12,"GPMC_BEn1",0x878,"gpmc_be1n","gmii2_col","gpmc_csn6","mmc2_dat3","gpmc_dir","pr1_mii1_rxlink","mcasp0_aclkr","*gpio1_28"));
            outList.Add(new GpioConfig(9,13,"GPMC_WPn",0x874,"gpmc_wpn","gmii2_rxerr","gpmc_csn5","rmii2_rxerr","mmc2_sdcd","pr1_mii1_txen","uart4_txd","*gpio0_31"));
            outList.Add(new GpioConfig(9,14,"GPMC_A2",0x848,"gpmc_a2","gmii2_txd3","rgmii2_td3","mmc2_dat1","gpmc_a18","pr1_mii1_txd2","ehrpwm1A","*gpio1_18"));
            outList.Add(new GpioConfig(9,15,"GPMC_A0",0x840,"gpmc_a0","gmii2_txen","rgmii2_tctl","rmii2_txen","gpmc_a16","pr1_mii_mt1_clk","ehrpwm1_tripzone_input","*gpio1_16"));
            outList.Add(new GpioConfig(9,16,"GPMC_A3",0x84C,"gpmc_a3","gmii2_txd2","rgmii2_td2","mmc2_dat2","gpmc_a19","pr1_mii1_txd1","ehrpwm1B","*gpio1_19"));
            outList.Add(new GpioConfig(9,17,"SPI0_CS0",0x95C,"spi0_cs0","mmc2_sdwp","I2C1_SCL","ehrpwm0_synci","pr1_uart0_txd","pr1_edio_data_in1","pr1_edio_data_out1","*gpio0_5"));
            outList.Add(new GpioConfig(9,18,"SPI0_D1",0x958,"spi0_d1","mmc1_sdwp","I2C1_SDA","ehrpwm0_tripzone_input","pr1_uart0_rxd","pr1_edio_data_in0","pr1_edio_data_out0","*gpio0_4"));
            outList.Add(new GpioConfig(9,19,"UART1_RTSn",0x97C,"uart1_rtsn","timer5","dcan0_rx","I2C2_SCL","spi1_cs1","pr1_uart0_rts_n","pr1_edc_latch1_in","*gpio0_13"));
            outList.Add(new GpioConfig(9,20,"UART1_CTSn",0x978,"uart1_ctsn","timer6","dcan0_tx","I2C2_SDA","spi1_cs0","pr1_uart0_cts_n","pr1_edc_latch0_in","*gpio0_12"));
            outList.Add(new GpioConfig(9,21,"SPI0_D0",0x954,"spi0_d0","uart2_txd","I2C2_SCL","ehrpwm0B","pr1_uart0_rts_n","pr1_edio_latch_in","EMU3","*gpio0_3"));
            outList.Add(new GpioConfig(9,22,"SPI0_SCLK",0x950,"spi0_sclk","uart2_rxd","I2C2_SDA","ehrpwm0A","pr1_uart0_cts_n","pr1_edio_sof","EMU2","*gpio0_2"));
            outList.Add(new GpioConfig(9,23,"GPMC_A1",0x844,"gpmc_a1","gmii2_rxdv","rgmii2_rctl","mmc2_dat0","gpmc_a17","pr1_mii1_txd3","ehrpwm0_synco","*gpio1_17"));
            outList.Add(new GpioConfig(9,24,"UART1_TXD",0x984,"uart1_txd","mmc2_sdwp","dcan1_rx","I2C1_SCL","","pr1_uart0_txd","pr1_pru0_pru_r31_16","*gpio0_15"));
            outList.Add(new GpioConfig(9,25,"MCASP0_AHCLKX",0x9AC,"mcasp0_ahclkx","eQEP0_strobe","mcasp0_axr3","mcasp1_axr1","EMU4","pr1_pru0_pru_r30_7","pr1_pru0_pru_r31_7","*gpio3_21"));
            outList.Add(new GpioConfig(9,26,"UART1_RXD",0x980,"uart1_rxd","mmc1_sdwp","dcan1_tx","I2C1_SDA","","pr1_uart0_rxd","pr1_pru1_pru_r31_16","*gpio0_14"));
            outList.Add(new GpioConfig(9,27,"MCASP0_FSR",0x9A4,"mcasp0_fsr","eQEP0B_in","mcasp0_axr3","mcasp1_fsx","EMU2","pr1_pru0_pru_r30_5","pr1_pru0_pru_r31_5","*gpio3_19"));
            outList.Add(new GpioConfig(9,28,"MCASP0_AHCLKR",0x99C,"mcasp0_ahclkr","ehrpwm0_synci","mcasp0_axr2","spi1_cs0","eCAP2_in_PWM2_out","pr1_pru0_pru_r30_3","pr1_pru0_pru_r31_3","*gpio3_17"));
            outList.Add(new GpioConfig(9,29,"MCASP0_FSX",0x994,"mcasp0_fsx","ehrpwm0B","","spi1_d0","mmc1_sdcd","pr1_pru0_pru_r30_1","pr1_pru0_pru_r31_1","*gpio3_15"));
            outList.Add(new GpioConfig(9,30,"MCASP0_AXR0",0x998,"mcasp0_axr0","ehrpwm0_tripzone_input","","spi1_d1","mmc2_sdcd","pr1_pru0_pru_r30_2","pr1_pru0_pru_r31_2","*gpio3_16"));
            outList.Add(new GpioConfig(9,31,"MCASP0_ACLKX",0x990,"mcasp0_aclkx","ehrpwm0A","","spi1_sclk","mmc0_sdcd","pr1_pru0_pru_r30_0","pr1_pru0_pru_r31_0","*gpio3_14"));
            outList.Add(new GpioConfig(9,41,"XDMA_EVENT_INTR1",0x9B4,"xdma_event_intr1","","tclkin","clkout2","timer7","pr1_pru0_pru_r31_16","EMU3","*gpio0_20"));
            //?    outList.Add(new GpioConfig(9,41.1,"MCASP0_AXR1",0x9A8,"mcasp0_axr1","eQEP0_index","","mcasp1_axr0","EMU3","pr1_pru0_pru_r30_6","pr1_pru0_pru_r31_6","*gpio3_20"));
            outList.Add(new GpioConfig(9,42,"ECAP0_IN_PWM0_OUT",0x964,"eCAP0_in_PWM0_out","uart3_txd","spi1_cs1","pr1_ecap0_ecap_capin_apwm_o","spi1_sclk","mmc0_sdwp","xdma_event_intr2","*gpio0_7"));
            //? outList.Add(new GpioConfig(9,42.1,"MCASP0_ACLKR",0x9A0,"mcasp0_aclkr","eQEP0A_in","mcasp0_axr2","mcasp1_aclkx","mmc0_sdwp","pr1_pru0_pru_r30_4","pr1_pru0_pru_r31_4","*gpio3_18"));
            return outList;
*/
        }

        #region External Library Calls

        // these calls are in the libc.so.6 library. We can just say "libc" and mono
        // will figure out which libc.so is the latest version and use that.

        [DllImport("libc", EntryPoint = "ioctl")]
        static extern int ExternalIoCtl(int fd, int request, StringBuilder outStr);

        [DllImport("libc", EntryPoint = "open")]
        static extern int ExternalFileOpen(string path, int flags);

        [DllImport("libc", EntryPoint = "close")]
        static extern int ExternalFileClose(int fd);

        #endregion

    }
}

