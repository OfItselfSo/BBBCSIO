using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
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
    /// Provides the storage and handling of the various items associated with 
    /// a PRU Event
    /// </summary>
    /// <history>
    ///    05 Jun 15  Cynic - Originally written
    /// </history>
    public class PRUEventHandler
    {
        public const int NO_FD = -1;

        // the event we represent
        private PRUEventEnum pruEvent = PRUEventEnum.PRU_EVENT_NONE;
        // the PRUDriver object which created this event data
        private PRUDriver pruDriver = null;
        // the file descriptor of the event (the open uio file).
        private int eventFD = NO_FD;

        // the thread we use to simulate interrupts
        private Thread pruEventThread = null;
        // some values 
        private const int MAX_MSEC_TO_WAIT_FOR_THREADSTART = 1000;

        private int interruptEventsReceived = 0;
        private int interruptEventsSent = 0;

        // this is where we send the interrupt events
        public event PRUInterruptEventHandlerUIO OnInterrupt;

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pruEventIn">The event we represent</param>
        /// <param name="pruDriverIn">The PRUDriver object which created this event
        ///    data object</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public PRUEventHandler(PRUEventEnum pruEventIn, PRUDriver pruDriverIn)
        {
            pruEvent = pruEventIn;
            pruDriver = pruDriverIn;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// gets the PRU event. There is no set accessor - this value is fixed
        /// when this object is created.
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public PRUEventEnum PRUEvent
        {
            get
            {
                return pruEvent;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// gets the PRUDriver that created this PRU event. There is no set 
        /// accessor - this value is fixed when this object is created.
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public PRUDriver PRUEventDriver
        {
            get
            {
                return pruDriver;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// gets/sets the PRU event file descriptor. THis is the file descriptor of
        /// the open UIO file.
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public int EventFD
        {
            get
            {
                return eventFD;
            }
            set
            {
                eventFD = value;
            }
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// 
        /// Gets/Sets the count of events received
        /// 
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public int InterruptEventsReceived
        {
            get
            {
                return interruptEventsReceived;
            }
            set
            {
                interruptEventsReceived = value;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets/Sets the count of events sent. 
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public int InterruptEventsSent
        {
            get
            {
                return interruptEventsSent;
            }
            set
            {
                interruptEventsSent = value;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Starts the interrupt event thread for a PRU event. There is one
        /// thread per interrupt event.
        /// 
        /// Basically what is happening here is that we are starting a separate thread
        /// which will immediately try to read the UIO file and will block on it 
        /// until an event happens. Once the event happens, a user specified handler is 
        /// called to process the interrupt data and clear the interrupt 
        /// 
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void StartPRUEventMonitor()
        {
            // is the event already running? If so, quietly leave
            if ((pruEventThread != null) && (pruEventThread.IsAlive == true)) return;

            // create the event thread
            pruEventThread = new Thread(new ThreadStart(PRUEventWorker));
            if (pruEventThread == null)
            {
                throw new Exception ("Error 30, StartPRUEventMonitor " + PRUEvent.ToString() + ", failed to create worker");
            }

            // start the thread
            pruEventThread.Start();
            // wait for it to start
            for(int i=0; i<MAX_MSEC_TO_WAIT_FOR_THREADSTART; i++)
            {
                // if our worker is alive we are good
                if (pruEventThread.IsAlive == true) break;
                // sleep for a millisecond
                Thread.Sleep (1);
            }
            // test it
            if (pruEventThread.IsAlive == false)
            {
                throw new Exception ("Error 55, StartPRUEventMonitor " + PRUEvent.ToString() + ", failed to start worker");
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Stops the pru event monitor thread. Since the worker thread immediately 
        /// enters into a blocking read we just hard kill the thread.
        /// 
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void StopPRUEventMonitor()
        {
            try
            {
                // is it already stopped
                if(pruEventThread==null) return;

                if (pruEventThread.IsAlive == true)
                {
                    // we are almost certainly hanging on a 
                    // blocking read. Just whack the thread
                    pruEventThread.Abort();
                    pruEventThread = null;
                }
            }
            catch 
            {
                pruEventThread = null;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// The PRU event worker thread
        /// 
        ///   This thread will attempt to read data from the UIO file descriptor
        ///   when it gets a result the SendInterruptEvent() function will be called to 
        ///   process the event and send it on to any listeners.
        /// 
        /// </summary>
        /// <history>
        ///    05 Jun 15 Cynic - Originally written
        /// </history>
        private void PRUEventWorker()
        {
            // we are continuously computable - we'll be swapped in and
            // out by the kernel
            while (true)
            {
                // wait for the event, this call will not return
                //  until the event happens. NOTE that the output here
                //  is a number indicating the cumulative number of times the interrupt
                //  event has been activated on the UIO file since boot time
                //  this is NOT usually the same as the number of events the 
                //  processed by the event worker.
                uint outInt = WaitForEvent();
                // the event has happened, Send the event now
                // note that the event handler MUST clear the event
                SendInterruptEvent(outInt);
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// A function to send an interrupt event. This function expects to be called
        /// from within our interrupt event monitoring thread. It fills in all the
        /// required parts and sends event off to the registered subscribers.
        /// 
        /// NOTE: you are NOT in the main form thread here. You are in the thread
        ///       which processes the interrupts
        /// 
        /// </summary>
        /// <param name="evValue">The current count from the UIO file</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private void SendInterruptEvent(uint evValue)
        {
            // count it
            interruptEventsReceived++;

            // send on the data 
            if (OnInterrupt == null) return;

            // send the data 
            OnInterrupt(PRUEvent, evValue, DateTime.Now, this);
            // count it
            interruptEventsSent++;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Waits for the specified event. Will stall with a blocking read
        /// until the event is triggered.
        /// </summary>
        /// <returns>
        ///   the total number of events processed by the uio file (from any
        ///   source) since boot up.
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public UInt32 WaitForEvent()
        {
            if (EventFD == PRUEventHandler.NO_FD)
            {
                throw new Exception("Host Event Not Open");
            }
            UInt32 outVal = 0;       // 4 bytes
            byte[] fourDataBytes = new byte[sizeof(UInt32)];

            // now we read the event file, the file descriptor of which represents 
            // one of the previously opened /dev/uio[0-7] files. The read() call will block and wait
            // until the interrupt is triggered by the PRU program. At this point 4 bytes (a uint)
            // will be available to be read and the Syscall.read() call will return. The uint
            // itself represents the number of interrupts (from any source) processed by the
            // uio file since boot. 

            // an explaination of the code below is in order. You have to actually read the bytes  
            // from the uio file and these bytes need to be marshalled out - you cannot fudge this. 
            // If you execute a simple bit of code like...
            //
            //     Syscall.read(pruEvent_fd, new IntPtr(outVal), sizeof(UInt32));
            //
            // the Syscall.read() call will block as expected and return when the interrupt is
            // triggered. However because the bytes did not get properly read or marshalled the
            // next time you call WaitForEvent() you will return immediately. This has nothing
            // to do with properly Clear()ing the previous event in the PRU registers. If the four 
            // bytes in the uio file are not properly read, then the UIO file thinks it still 
            // has an unprocessed interrupt to send and will return immediately

            // wait for the interrupt event and read the event data indicating the
            // number of interrupts which have occurred. If you do not know what the keyword "unsafe"
            // means in this context you should look it up now. I think you will find it means 
            // something a bit different than what you are probably assuming it means.
            unsafe
            {
                // set up a fixed pointer. 
                fixed (byte *fdbPtr=fourDataBytes) 
                {
                    // get an IntPtr to our fixed pointer
                    IntPtr bufIntPtr= (IntPtr)fdbPtr;
                    // do the read, will stall till data becomes available
                    Syscall.read(EventFD, bufIntPtr, sizeof(UInt32));
                    // the data is now in our fourDataBytes buffer, convert it so 
                    // it can be returned.
                    outVal = (uint)BitConverter.ToInt32(fourDataBytes, 0);
                }
            }

            return outVal;
        }
            
    }
}

