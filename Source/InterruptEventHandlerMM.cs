using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// The event handler delegate 
    /// </summary>
    /// <param name="evGpio">The gpio the event is configured on</param>
    /// <param name="evState">The event pin state (true or false)</param>
    /// <param name="evData">The event data structure</param>
    /// <param name="evDateTime">The date time of the event</param>
    /// <history>
    ///    28 Aug 14  Cynic - Originally written
    /// </history>
    public delegate void InterruptEventHandlerMM(GpioEnum evGpio, bool evState, DateTime evDateTime, EventData evData);
}

