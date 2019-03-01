using System;

namespace BBBCSIO
{
    /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
    /// <summary>
    /// An enum to define the possible PWM ports on the Beaglebone Black.
    /// 
    /// NOTE: often two pins on the P8 or P9 Headers share a PWM output. 
    /// 
    ///    PWM Outputs that share the same PWM OCP device MUST have the same
    ///    frequency but can have independent pulse widths. The trigger
    ///    timing of the start of the high part of pulse waveform is 
    ///    simultaneous.
    /// 
    ///    PWM Name         H8/H9 Pins          PWMDevice_Output
    ///    PWM0_A,        PWM_P9_22_or_P9_31     (EHRPWM0_A)
    ///    PWM0_B,        PWM_P9_21_or_P9_29     (EHRPWM0_B)    
    ///                 
    ///    PWM1_A,        PWM_P9_14_or_P8_36     (EHRPWM1_A)
    ///    PWM1_B,        PWM_P9_16_or_P8_34     (EHRPWM1_B)
    /// 
    ///    PWM2_A,        PWM_P8_19_or_P8_45     (EHRPWM2_A)
    ///    PWM2_B,        PWM_P8_13_or_P8_46     (EHRPWM2_B)
    /// 
    ///    This is IMPORTANT so I will say it again. If you configure 
    ///    two PWM outputs in the same device (PWM1_A, PWM1_B for example) 
    ///    then they MUST be configured with the same frequency. 
    /// 
    ///    They will use the same frequency anyways and if
    ///    you change one, you change the other. Changing the frequency on the
    ///    B output will instantly change the frequency on the A output and
    ///    will really mess up any pulse widths/duty cycles the A output is using
    /// 
    ///    Always set the frequency first then the Pulse Width/duty cycle. The pulse 
    ///    width is calculated from whatever frequency is currently set it is
    ///    not adjusted if the frequency is later changed.
    /// 
    /// </summary>
    /// <history>
    ///    20 Aug 15  Cynic - Originally written
    ///    21 Nov 15  Cynic - revised, removed the ECAPP modules
    ///                       revised, renamed to make it more obvious which modules
    ///                       were being used
    /// </history>
    public enum PWMPortEnum
    {
        PWM_NONE,
        PWM0_A,     // PWM_P9_22_or_P9_31 (EHRPWM0A)
        PWM0_B,     // PWM_P9_21_or_P9_29 (EHRPWM0B)                 
        PWM1_A,     // PWM_P9_14_or_P8_36 (EHRPWM1A)
        PWM1_B,     // PWM_P9_16_or_P8_34 (EHRPWM1B)
        PWM2_A,     // PWM_P8_19_or_P8_45 (EHRPWM2A)
        PWM2_B,     // PWM_P8_13_or_P8_46 (EHRPWM2B)
        // PWM_P9_42,             // ECAPPWM0
        // PWM_P9_28,             // ECAPPWM2
    }
}

