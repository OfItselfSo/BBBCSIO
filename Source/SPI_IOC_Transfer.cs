﻿using System;
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
    /// Defines the Ioctl transfer structure used by the SPIDEV version of
    /// the SPIPort (SPIPortFS)
    /// 
    /// </summary>
    /// <history>
    ///    21 Dec 14  Cynic - Originally written
    /// </history>
    [StructLayout(LayoutKind.Explicit)]
    public struct spi_ioc_transfer
    {
        [MarshalAs(UnmanagedType.LPStr)]
        [FieldOffset(0)]
        public IntPtr  tx_buf;        // 8 bytes

        [MarshalAs(UnmanagedType.LPStr)]
        [FieldOffset(8)]
        public IntPtr rx_buf;         // 8 bytes

        [FieldOffset(16)]
        public UInt32 len;            // 4 bytes

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(20)]
        public UInt32 speed_hz;       // 4 bytes

        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(24)]
        public UInt16 delay_usecs;    // 2 bytes

        [MarshalAs(UnmanagedType.U1)]
        [FieldOffset(26)]
        public byte bits_per_word;  // 1 byte

        [MarshalAs(UnmanagedType.U1)]
        [FieldOffset(27)]
        public byte cs_change;      // 1 byte

        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(30)]
        public Int32 pad;            // 2 bytes
    }
}

