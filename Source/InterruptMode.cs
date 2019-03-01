using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define a type of interrupt
    /// </summary>
    /// <history>
    ///    28 Aug 14  Cynic - Originally written
    /// </history>
    public enum InterruptMode
    {
        InterruptEdgeBoth,      // A value that sets the port so that its interrupt is triggered on both the rising and falling edges.
        InterruptEdgeLevelHigh, // A value that sets the port so that its interrupt is triggered when the input level is high.
        InterruptEdgeLevelLow,  // A value that sets the port so that its interrupt is triggered when the input level is low.
        InterruptNone
        /* Not supported 
            InterruptEdgeHigh,      // A value that sets the port so that its interrupt is triggered on the rising edge.
            InterruptEdgeLow,       // A value that sets the port so that its interrupt is triggered on the falling edge.
             */
    }

}

