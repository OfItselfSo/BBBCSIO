using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the possible A2D ports on the Beaglebone Black
    /// </summary>
    /// <history>
    ///    20 Aug 15  Cynic - Originally written
    /// </history>
    public enum A2DPortEnum
    {
        AIN_NONE,
        AIN_0,
        AIN_1,          
        AIN_2,
        AIN_3,
        AIN_4,
        AIN_5,
        AIN_6,
        // AIN_7, not present on BBB
    }
}

