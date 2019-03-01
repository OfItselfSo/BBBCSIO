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
    /// Provides the Analog to Digital Port functionality for a BeagleBone Black
    /// This is the SYSFS version
    /// 
    /// Be aware that you need to ensure the A2D port is configured in the Device
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
    public class A2DPortFS : PortFS
    {

        // the A2D port we use
        private A2DPortEnum a2dPort = A2DPortEnum.AIN_NONE;      

        // the stream descriptor for the open a2d file
        Stream a2dStream = null;
        StreamReader a2dReader = null;

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="a2dPortIn">The A2D port we use</param>
        /// <history>
        ///    20 Aug 15  Cynic - Originally written
        /// </history>
        public A2DPortFS(A2DPortEnum a2dPortIn) : base(GpioEnum.GPIO_NONE)
        {
            a2dPort = a2dPortIn;
            //Console.WriteLine("A2DPort Starts");
            // open the port
            OpenPort();
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Reads the current value in from an A2D Device. 
        /// 
        /// NOTE: 
        ///   Tests indicate that the maximum possible speed here is about 
        ///   1000 reads/second
        ///   
        /// </summary>
        /// <history>
        ///    20 Aug 15  Cynic - Originally written
        /// </history>
        public uint Read()
        {
            string line;

            if (a2dStream == null)
            {
                throw new Exception ("A2D port is not open, a2dStream=null");
            }
            if (PortIsOpen == false)
            {
                throw new Exception ("A2D port is not open");
            }

            // reset our location
            a2dStream.Seek(0, SeekOrigin.Begin);
            line = a2dReader.ReadLine();
            //Console.WriteLine("A2DPort read returned "+ line);
            return Convert.ToUInt32(line);
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
            string deviceFileName;
            // set up now
            deviceFileName = BBBDefinitions.A2DIIO_FILENAME;

            // set up the a2d port number
            if (A2DPort == A2DPortEnum.AIN_0)
            {
                deviceFileName = deviceFileName.Replace("%port%", "0");
            }
            else if (A2DPort == A2DPortEnum.AIN_1)
            {
                deviceFileName = deviceFileName.Replace("%port%", "1");
            }
            else if (A2DPort == A2DPortEnum.AIN_2)
            {
                deviceFileName = deviceFileName.Replace("%port%", "2");
            }
            else if (A2DPort == A2DPortEnum.AIN_3)
            {
                deviceFileName = deviceFileName.Replace("%port%", "3");
            }
            else if (A2DPort == A2DPortEnum.AIN_4)
            {
                deviceFileName = deviceFileName.Replace("%port%", "4");
            }
            else if (A2DPort == A2DPortEnum.AIN_5)
            {
                deviceFileName = deviceFileName.Replace("%port%", "5");
            }
            else if (A2DPort == A2DPortEnum.AIN_6)
            {
                deviceFileName = deviceFileName.Replace("%port%", "6");
            }
            else
            {
                // should never happen
                throw new Exception ("Unknown A2D Port:" + A2DPort.ToString());
            }
                
            // we open the file.
            a2dStream = File.Open(deviceFileName, FileMode.Open);
            if(a2dStream == null)
            {
                throw new Exception("Could not open a2d device file:" + deviceFileName);
            }
            // we open the reader
            a2dReader = new StreamReader(a2dStream);
            portIsOpen = true;

            // Console.WriteLine("A2DPort Port Device Enabled: "+ deviceFileName);
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
            //Console.WriteLine("A2DPort Closing");
            if (a2dReader != null)
            {
                // do a close
                a2dReader.Close();
            }
            if (a2dStream != null)
            {
                // do a close
                a2dStream.Close();
            }
            portIsOpen = false;
            a2dStream = null;
            a2dReader = null;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the A2D Port. There is no Set accessor this is set in the constructor
        /// </summary>
        /// <history>
        ///    20 Aug 15 Cynic - Originally written
        /// </history>
        public A2DPortEnum A2DPort
        {
            get
            {
                return a2dPort;
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
            return PortDirectionEnum.PORTDIR_INPUT;
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
                //  Console.WriteLine("Disposing A2DPORT");
         
                // call the base to dispose there
                base.Dispose(disposing);

            }
        }
        #endregion

    }
}

