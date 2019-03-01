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
    /// Provides the base functionality for all ports
    /// </summary>
    /// <history>
    ///    28 Aug 14  Cynic - Originally written
    /// </history>
    public abstract class Port :  IDisposable
    {
        // Track whether Dispose has been called. 
        private bool disposed = false;

        // defines the GPIO this port references
        GpioConfig gpioCfgObject = null;

        // a flag to indicate if the port is open
        protected bool portIsOpen = false;
        
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gpioIDIn">The gpio we open the port on</param>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        public Port (GpioEnum gpioIDIn)
        {
            // set our GpioConfig object now
            gpioCfgObject = GpioUtils.GetGpioConfigForGpio(gpioIDIn);
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Finalizer
        /// </summary>
        /// <history>
        ///    28 Aug 14  Cynic - Originally written
        /// </history>
        ~Port()
        {
            Dispose(false);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the gpioID. There is no set accessor - this value is set in the constructor
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public GpioEnum GpioID 
        {
            get 
            {
                if (gpioCfgObject == null) return GpioEnum.GPIO_NONE;
                return gpioCfgObject.Gpio;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the gpioCfgObject. There is no set accessor - this value is set in the constructor
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public GpioConfig GpioCfgObject
        {
            get
            {
                return gpioCfgObject;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the portIsOpen flag. There is no set accessor - this value is 
        /// set when the port is opened.
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public bool PortIsOpen
        {
            get
            {
                return portIsOpen;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the PortDirection - derived classes must implement
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public abstract PortDirectionEnum PortDirection();

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Opens the port - derived classes must implement
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        protected abstract void OpenPort();

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Closes the port - derived classes must implement
        /// </summary>
        /// <history>
        ///    28 Aug 14 Cynic - Originally written
        /// </history>
        public abstract void ClosePort();

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
        protected virtual void Dispose(bool disposing)
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

                // Note disposing has been done.
                disposed = true;

            }
        }
        #endregion

    }
}

