using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// The event handler delegate for interrupts from the PRU to the Host 
    /// send via the UIO subsystem
    /// </summary>
    /// <param name="evPRUEvent">The PRU Event the event is configured on</param>
    /// <param name="evCount">The count of event</param>
    /// <param name="evDateTime">The date time of the event</param>
    /// <param name="evPRUData">The PRUEventHandler data structure</param>
    /// <history>
    ///    05 Jun 15  Cynic - Originally written
    /// </history>
    public delegate void PRUInterruptEventHandlerUIO(PRUEventEnum evPRUEvent, uint evCount, DateTime evDateTime, PRUEventHandler evPRUData);
}

