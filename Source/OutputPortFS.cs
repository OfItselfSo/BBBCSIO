using System;

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
    /// Provides the Output Port functionality for a BeagleBone Black
    /// (SYSFS Version)
    /// </summary>
    /// <history>
    ///    28 Aug 14  Cynic - Originally written
    /// </history>
    public class OutputPortFS : PortFS
    {

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gpioIDIn">The gpio we open the port on</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public OutputPortFS (GpioEnum gpioIDIn) : base(gpioIDIn)
        {
            // open the port
            OpenPort ();
            // set the direction to output
            SetSysFsDirection();
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gpioIDIn">The gpio we open the port on</param>
        /// <param name="initialState">The initial state, true or false, of the port
        /// after we open it.
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public OutputPortFS (GpioEnum gpioIDIn, bool initialState) : base(gpioIDIn)
        {
            // open the port
            OpenPort ();
            // set the direction to output
            SetSysFsDirection();
            // set the initial value
            Write(initialState);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the port value
        /// 
        /// This is really just doing the equivalent of a shell command 
        ///    echo <value_as_string> > /sys/class/gpio/gpio<gpioID>/value
        /// 
        /// </summary>
        /// <returns>true or false - the ports value</returns>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public bool Read()
        {
            string outStr = System.IO.File.ReadAllText(BBBDefinitions.SYSFS_GPIODIR+BBBDefinitions.SYSFS_GPIODIRNAMEBASE+GpioUtils.GpioIDToString(GpioID)+"/"+BBBDefinitions.SYSFS_GPIOVALUE);

            if(outStr.Trim() == "0") return false;
            else return true;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Sets the port value
        /// 
        /// This is really just doing the equivalent of a shell command 
        ///    echo <value_as_string> > /sys/class/gpio/gpio<gpioID>/value
        /// 
        /// </summary>
        /// <param name="valueToSet>the value to set/param>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public void Write(bool valueToSet)
        {
            string valueStr;
            if (valueToSet == true)
            {
                valueStr = "1";
            } 
            else 
            {
                valueStr = "0";
            }
            // set the value now        
            System.IO.File.WriteAllText(BBBDefinitions.SYSFS_GPIODIR+BBBDefinitions.SYSFS_GPIODIRNAMEBASE+GpioUtils.GpioIDToString(GpioID)+"/"+BBBDefinitions.SYSFS_GPIOVALUE, valueStr);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the PortDirection
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public override PortDirectionEnum PortDirection()
        {
            return PortDirectionEnum.PORTDIR_OUTPUT;
        }

    }
}

