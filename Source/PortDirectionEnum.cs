using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the direction of the port. This is only
    /// meaningful to ports.
    /// </summary>
    /// <history>
    ///    28 Aug 14  Cynic - Originally written
    ///    21 Dec 14  Cynic - added PORTDIR_INPUTOUTPUT
    /// </history>
    public enum PortDirectionEnum
    {
        PORTDIR_INPUT,
        PORTDIR_OUTPUT,
        PORTDIR_INPUTOUTPUT
    }
}

