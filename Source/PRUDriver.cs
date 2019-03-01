using System;
using System.IO;
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
    /// Provides the functionality to interact with a PRU on the Beaglebone Black
    /// </summary>
    /// <history>
    ///    05 Jun 15  Cynic - Originally written
    /// </history>
    public class PRUDriver :  IDisposable
    {
        // for substitutions
        private const string PRUNUM = "%PRUNUM%";
        private const string PRUUIONUM = "%PRUUIONUM%";

        private const uint PRU_UIO_PAGE_SIZE = 4096;
        private const uint PRU_UIO_MMAP_BASE = 0 * PRU_UIO_PAGE_SIZE;
        // the size of the PRU's memory mapped into user space by the UIO routines
        private const uint PRU_UIO_PHYSMAPSIZE = 0x00080000;
        private const string PRU_UIODEVICE_NAME = "/dev/uio%PRUUIONUM%";
        private const string PRU_UIO_BASE_PATH = "/sys/class/uio/uio%PRUUIONUM%/maps/map0/addr";
        private const string PRU_UIO_SIZE_PATH = "/sys/class/uio/uio%PRUUIONUM%/maps/map0/size";

        // not needed
        //#define PRUSS_UIO_MAP_OFFSET_EXTRAM 1*PAGE_SIZE
        //#define PRUSS_UIO_DRV_EXTRAM_BASE "/sys/class/uio/uio0/maps/map1/addr"
        //#define PRUSS_UIO_DRV_EXTRAM_SIZE "/sys/class/uio/uio0/maps/map1/size"

        // the PRU we are using
        private PRUEnum pruID = PRUEnum.PRU_NONE;

        // this contains the file descriptors of events which are enabled and which
        // we are monitoring. The index into the dictionary is the event enum, the 
        // value associated with it is the file descriptor of the enabled event uio file
        Dictionary<PRUEventEnum, PRUEventHandler> enabledEvents = new Dictionary<PRUEventEnum, PRUEventHandler>();

        // ###
        // ### runtime discovered or calculated values
        // ###
        // the user space base address of the PRU system as mapped in by uio_pruss
        private uint pruPhysBaseAddr = 0;
        // the user space base address range of the PRU system as mapped in by uio_pruss
        private uint pruPhysMapSize = 0;

        // ###
        // ### Constants
        // ###
        private const uint PRUOFFSET_CONTROL_PRU0 = 0x00022000;
        private const uint PRUOFFSET_CONTROL_PRU1 = 0x00024000;
        private const uint PRUOFFSET_IRAM_PRU0 = 0x00034000;
        private const uint PRUOFFSET_IRAM_PRU1 = 0x00038000;
        private const uint PRUOFFSET_DRAM_PRU0 = 0x00000000;
        private const uint PRUOFFSET_DRAM_PRU1 = 0x00002000;
        private const uint PRUOFFSET_INTC_PRU0 = 0x00020000; // yes, PRU0 and PRU1 are the same offset
        private const uint PRUOFFSET_INTC_PRU1 = 0x00020000; //   they share an interrupt controller

        public const uint PRU_IRAM_SIZE = 0x2000;  // PRU has 8Kb of instruction RAM
        public const uint PRU_DRAM_SIZE = 0x2000;  // PRU has 8Kb of primary data RAM

        // some specific named offsets into the PRU INTC area
        private const uint PRUOFFSET_INTC_SIPR0 = 0xD00;
        private const uint PRUOFFSET_INTC_SIPR1 = 0xD04;

        private const uint PRUOFFSET_INTC_SITR0 = 0xD80;
        private const uint PRUOFFSET_INTC_SITR1 = 0xD84;

        private const uint PRUOFFSET_INTC_ESR0 = 0x300;
        private const uint PRUOFFSET_INTC_ESR1 = 0x304;

        private const uint PRUOFFSET_INTC_ECR0 = 0x380;
        private const uint PRUOFFSET_INTC_ECR1 = 0x384;

        private const uint PRUOFFSET_INTC_SECR0 = 0x280;
        private const uint PRUOFFSET_INTC_SECR1 = 0x284;

        private const uint PRUOFFSET_INTC_CMR00 = 0x400;
        private const uint PRUOFFSET_INTC_CMR01 = 0x404;
        private const uint PRUOFFSET_INTC_CMR02 = 0x408;
        private const uint PRUOFFSET_INTC_CMR03 = 0x40C;
        private const uint PRUOFFSET_INTC_CMR04 = 0x410;
        private const uint PRUOFFSET_INTC_CMR05 = 0x414;
        private const uint PRUOFFSET_INTC_CMR06 = 0x418;
        private const uint PRUOFFSET_INTC_CMR07 = 0x41C;
        private const uint PRUOFFSET_INTC_CMR08 = 0x420;
        private const uint PRUOFFSET_INTC_CMR09 = 0x424;
        private const uint PRUOFFSET_INTC_CMR10 = 0x428;
        private const uint PRUOFFSET_INTC_CMR11 = 0x42C;
        private const uint PRUOFFSET_INTC_CMR12 = 0x430;
        private const uint PRUOFFSET_INTC_CMR13 = 0x434;
        private const uint PRUOFFSET_INTC_CMR14 = 0x438;
        private const uint PRUOFFSET_INTC_CMR15 = 0x43C;

        private const uint PRUOFFSET_INTC_HMR0 = 0x800;
        private const uint PRUOFFSET_INTC_HMR1 = 0x804;
        private const uint PRUOFFSET_INTC_HMR2 = 0x808;

        private const uint PRUOFFSET_INTC_SICR = 0x024;
        private const uint PRUOFFSET_INTC_EISR = 0x028;
        private const uint PRUOFFSET_INTC_EICR = 0x02C;

        private const uint PRUOFFSET_INTC_HIEISR = 0x034;
        private const uint PRUOFFSET_INTC_HIDISR = 0x038;
        private const uint PRUOFFSET_INTC_GER = 0x010;
        private const uint PRUOFFSET_INTC_CR = 0x004;

        // there are only 10 HOST Interrupts, HostInt0,1 are for PRU to PRU
        // communications and HOSTInt2,9 are for interacting with C#
        // user space programs. The UIO subsystem interacts with these and
        // is hard coded so that HOSTINT2 appears on UIO0, HOSTINT3 on UIO1 etc.
        private const uint MIN_HOSTINT = 0;
        private const uint MAX_HOSTINT = 9;

        // there are only 10 interrupt Channels, We, in our standard
        // configuration, map these 1 to 1 onto the Host Interrupts
        // it does not have to be this way but it keeps things simple
        private const uint MIN_INTCHAN = 0;
        private const uint MAX_INTCHAN = 9;

        // there are 64 interrupts. Most of these are used by the 
        // PRU's peripherals (UART, Ethercat etc). SysInts 16-31
        // can be used to raise events in user space programs
        private const uint MIN_SYSINT = 0;
        private const uint MAX_SYSINT = 61;

        // these are the system interrupts the PRU itself can raise
        // by writing to bits R31[3:0] 
        private const uint MIN_PRUSYSINT = 16;
        private const uint MAX_PRUSYSINT = 31;

        // pointers into the pruMapped Memory
        private IntPtr pruMappedMem = IntPtr.Zero;

        // Track whether Dispose has been called. 
        private bool disposed = false;

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pruIDIn">The pru we interact with</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public PRUDriver (PRUEnum pruIDIn)
        {
            // Console.WriteLine("Building PRUDriver for " + pruIDIn.ToString()); 
            // set some values
            pruID = pruIDIn;

            // set up the PRU
            SetupPRU();
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Finalizer
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        ~PRUDriver()
        {
            Dispose(false);
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Loads a compiled binary into a PRU and runs it.
        /// </summary>
        /// <param name="binaryToRun">The binary file to run. Cannot be NULL</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void ExecutePRUProgram(string binaryToRun)
        {
            ExecutePRUProgram(binaryToRun, null);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Loads a compiled binary into a PRU and runs it.
        /// </summary>
        /// <param name="binaryToRun">The filename and path to the 
        ///    binary file to run. Cannot be NULL</param>
        /// <param name="dataBytes">The databyes to load into the PRU dataRAM. The
        /// offset is assumed to be 0. This is intended for data which should be
        /// in place and which the PRU should find when it starts. Can be NULL</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void ExecutePRUProgram(string binaryToRun, byte[] dataBytes)
        {
            byte[] fileBytes = null;
            FileStream fs = null;

            if ((binaryToRun == null) || (binaryToRun.Length == 0))
            {
                throw new Exception("Null or zero length binary file name specified");
            }

            try
            {
                fs = new FileStream(binaryToRun, FileMode.Open,FileAccess.Read);
                if ((fs == null) || (fs.Length == 0))
                {
                    throw new Exception("The binary file stream: " + binaryToRun + " is null or zero bytes");
                }
                fileBytes = new byte[fs.Length];
                fs.Read(fileBytes,0,System.Convert.ToInt32(fs.Length));
            }
            finally
            {
                fs.Close();
                fs.Dispose();
            }         
            if ((fileBytes == null) || (fileBytes.Length == 0))
            {
                throw new Exception("The binary file: " + binaryToRun + " returned null or zero bytes on read");
            }
            if (fileBytes.Length > PRU_IRAM_SIZE)
            {
                throw new Exception("The binary file: " + binaryToRun + " contains more bytes (" + fileBytes.Length.ToString() + ") than the PRU instruction RAM can hold (" + PRU_IRAM_SIZE.ToString() + ")");
            }

            // Console.WriteLine("The binary file: " + binaryToRun + " has been read");

            // We disable the PRU first to reset it
            DisablePRU();

            // if we have databytes we load those
            if ((dataBytes != null) && (dataBytes.Length > 0))
            {
                // we load the data into the PRU local data memory with 0 offset
                WritePRUData(dataBytes, 0);
            }

            // we load the code into the PRU local instruction memory
            LoadPRUCode(ref fileBytes);

            // enable the PRU
            EnableAndExecutePRU(0);

        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Stop the PRU.
        /// 
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void PRUStop()
        {
            // get a pointer to the control register of the PRU as mapped by the UIO file
            IntPtr ctrlPtr = PRUUIOMappedControlBase;
            if (ctrlPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU control register");
            }
            // this does it all
            DisablePRU();
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Loads the contents of a specified byte array into the PRU
        /// local instruction memory.
        /// 
        /// NOTE: the max size here is PRU_IRAM_SIZE
        /// </summary>
        /// <param name="codeBytes">The bytes of the compiled binary the 
        /// PRU is to run</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private void LoadPRUCode(ref byte[] codeBytes)
        {

            // Console.WriteLine("LoadPRUCode called for " + PRUID.ToString());
            if ((codeBytes == null) || (codeBytes.Length == 0))
            {
                throw new Exception("The code bytes to load was null or zero length");
            }
            if (codeBytes.Length > PRU_IRAM_SIZE)
            {
                throw new Exception("The " + codeBytes.Length.ToString() + " code bytes to load are too long. Max is " + PRU_IRAM_SIZE.ToString());
            }
                
            // Console.WriteLine("LoadPRUCode IRAM Base is " + PRUUIOMappedIRAMBase.ToString("X8"));

            // get a pointer to the iram base register of the PRU as mapped by the UIO file
            IntPtr iramPtr = PRUUIOMappedIRAMBase;
            if (iramPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU iram area");
            }
                
            // copy the data from the codeBytes to our instruction ram pointer               
            Marshal.Copy(codeBytes, 0, iramPtr, codeBytes.Length);
            // Console.WriteLine("LoadPRUCode complete for " + PRUID.ToString());
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Writes the contents of a specified byte into the PRU
        /// data memory.
        /// 
        /// NOTE: the max size here is PRU_DRAM_SIZE
        /// </summary>
        /// <param name="byteVal">The byte to write</param>
        /// <param name="offset">The offset into the PRU DataRAM we write the byte
        /// into. Note that the sizeof(byte) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void WritePRUDataByte(byte byteVal, uint offset)
        {
            byte[] dataBytes = new byte[sizeof(byte)];
            dataBytes[0] = byteVal;
            WritePRUData(dataBytes, offset);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Writes the contents of a specified byte array into the PRU
        /// data memory.
        /// 
        /// </summary>
        /// <param name="byteArray">The byte array to write</param>
        /// <param name="offset">The offset into the PRU DataRAM we write the byte
        /// array into. Note that the byteArray.Length + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void WritePRUDataByteArray(byte[] byteArray, uint offset)
        {
            WritePRUData(byteArray, offset);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Writes the contents of a specified UInt16 into the PRU
        /// data memory.
        /// 
        /// NOTE: the max size here is PRU_DRAM_SIZE
        /// </summary>
        /// <param name="dataVal">The UInt16 to write</param>
        /// <param name="offset">The offset into the PRU DataRAM we write the UInt16
        /// into. Note that the sizeof(UInt16) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void WritePRUDataUInt16(UInt16 dataVal, uint offset)
        {
            byte[] dataBytes = new byte[sizeof(UInt16)];
            BitConverter.GetBytes(dataVal).CopyTo(dataBytes,0);
            WritePRUData(dataBytes, offset);           
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Writes the contents of a specified Int16 into the PRU
        /// data memory.
        /// 
        /// NOTE: the max size here is PRU_DRAM_SIZE
        /// </summary>
        /// <param name="dataVal">The Int16 to write</param>
        /// <param name="offset">The offset into the PRU DataRAM we write the Int16
        /// into. Note that the sizeof(Int16) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void WritePRUDataInt16(Int16 dataVal, uint offset)
        {
            byte[] dataBytes = new byte[sizeof(Int16)];
            BitConverter.GetBytes(dataVal).CopyTo(dataBytes,0);
            WritePRUData(dataBytes, offset);           
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Writes the contents of a specified UInt32 into the PRU
        /// data memory.
        /// 
        /// NOTE: the max size here is PRU_DRAM_SIZE
        /// </summary>
        /// <param name="dataVal">The UInt32 to write</param>
        /// <param name="offset">The offset into the PRU DataRAM we write the UInt32
        /// into. Note that the sizeof(uint) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void WritePRUDataUInt32(UInt32 dataVal, uint offset)
        {
            byte[] dataBytes = new byte[sizeof(UInt32)];
            BitConverter.GetBytes(dataVal).CopyTo(dataBytes,0);
            WritePRUData(dataBytes, offset);           
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Writes the contents of a specified Int32 into the PRU
        /// data memory.
        /// 
        /// NOTE: the max size here is PRU_DRAM_SIZE
        /// </summary>
        /// <param name="dataVal">The Int32 to write</param>
        /// <param name="offset">The offset into the PRU DataRAM we write the Int32
        /// into. Note that the sizeof(Int32) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void WritePRUDataInt32(Int32 dataVal, uint offset)
        {
            byte[] dataBytes = new byte[sizeof(Int32)];
            BitConverter.GetBytes(dataVal).CopyTo(dataBytes,0);
            WritePRUData(dataBytes, offset);           
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Writes the contents of a specified UInt64 into the PRU
        /// data memory.
        /// 
        /// NOTE: the max size here is PRU_DRAM_SIZE
        /// </summary>
        /// <param name="dataVal">The UInt64 to write</param>
        /// <param name="offset">The offset into the PRU DataRAM we write the UInt64
        /// into. Note that the sizeof(UInt64) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void WritePRUDataUInt64(UInt64 dataVal, uint offset)
        {
            byte[] dataBytes = new byte[sizeof(UInt64)];
            BitConverter.GetBytes(dataVal).CopyTo(dataBytes,0);
            WritePRUData(dataBytes, offset);           
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Writes the contents of a specified Int64 into the PRU
        /// data memory.
        /// 
        /// NOTE: the max size here is PRU_DRAM_SIZE
        /// </summary>
        /// <param name="dataVal">The Int64 to write</param>
        /// <param name="offset">The offset into the PRU DataRAM we write the Int64
        /// into. Note that the sizeof(Int64) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void WritePRUDataInt64(Int64 dataVal, uint offset)
        {
            byte[] dataBytes = new byte[sizeof(Int64)];
            BitConverter.GetBytes(dataVal).CopyTo(dataBytes,0);
            WritePRUData(dataBytes, offset);           
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// 
        /// Reads a byte from the PRU data memory
        /// 
        /// </summary>
        /// <param name="offset">The offset into the PRU DataRAM we read the databytes
        /// from. Note that the sizeof(byte) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <returns>
        ///   a byte read from the PRU Data Ram at the offset
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public byte ReadPRUDataByte(uint offset)
        {
            byte[] outBytes = ReadPRUData(offset, sizeof(byte));
            return outBytes[0];
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// 
        /// Reads a byte array from the PRU data memory
        /// 
        /// </summary>
        /// <param name="offset">The offset into the PRU DataRAM we read the databytes
        /// from. Note that the length + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <param name="length">the number of bytes to read</param>
        /// <returns>
        ///   a byte array read from the PRU Data Ram at the offset
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public byte[] ReadPRUDataByteArray(uint offset, uint length)
        {
            return ReadPRUData(offset, length);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Reads the PRU data memory and returns it as a UInt16
        /// 
        /// </summary>
        /// <param name="offset">The offset into the PRU DataRAM we read the databytes
        /// from. Note that the sizeof(UInt16) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <returns>
        ///   a uint read from the PRU Data Ram at the offset
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public UInt16 ReadPRUDataUInt16(uint offset)
        {
            byte[] outBytes = ReadPRUData(offset, sizeof(UInt16));
            return BitConverter.ToUInt16(outBytes, 0);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Reads the PRU data memory and returns it as a Int16
        /// 
        /// </summary>
        /// <param name="offset">The offset into the PRU DataRAM we read the databytes
        /// from. Note that the sizeof(Int16) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <returns>
        ///   an Int16 read from the PRU Data Ram at the offset
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public Int16 ReadPRUDataInt16(uint offset)
        {
            byte[] outBytes = ReadPRUData(offset, sizeof(Int16));
            return BitConverter.ToInt16(outBytes, 0);
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Reads the PRU data memory and returns it as a UInt32
        /// 
        /// </summary>
        /// <param name="offset">The offset into the PRU DataRAM we read the databytes
        /// from. Note that the sizeof(UInt32) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <returns>
        ///   a uint read from the PRU Data Ram at the offset
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public UInt32 ReadPRUDataUInt32(uint offset)
        {
            byte[] outBytes = ReadPRUData(offset, sizeof(UInt32));
            return BitConverter.ToUInt32(outBytes, 0);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Reads the PRU data memory and returns it as a Int32
        /// 
        /// </summary>
        /// <param name="offset">The offset into the PRU DataRAM we read the databytes
        /// from. Note that the sizeof(Int32) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <returns>
        ///   an Int32 read from the PRU Data Ram at the offset
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public Int32 ReadPRUDataInt32(uint offset)
        {
            byte[] outBytes = ReadPRUData(offset, sizeof(Int32));
            return BitConverter.ToInt32(outBytes, 0);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Reads the PRU data memory and returns it as a UInt64
        /// 
        /// </summary>
        /// <param name="offset">The offset into the PRU DataRAM we read the databytes
        /// from. Note that the sizeof(UInt64) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <returns>
        ///   a uint read from the PRU Data Ram at the offset
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public UInt64 ReadPRUDataUInt64(uint offset)
        {
            byte[] outBytes = ReadPRUData(offset, sizeof(UInt64));
            return BitConverter.ToUInt64(outBytes, 0);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Reads the PRU data memory and returns it as a Int64
        /// 
        /// </summary>
        /// <param name="offset">The offset into the PRU DataRAM we read the databytes
        /// from. Note that the sizeof(Int64) + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <returns>
        ///   a int read from the PRU Data Ram at the offset
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public Int64 ReadPRUDataInt64(uint offset)
        {
            byte[] outBytes = ReadPRUData(offset, sizeof(Int64));
            return BitConverter.ToInt64(outBytes, 0);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Reads the PRU data memory and returns it as a byte array
        /// 
        /// NOTE: the max size here is PRU_DRAM_SIZE
        /// </summary>
        /// <param name="offset">The offset into the PRU DataRAM we read the databytes
        /// from. Note that the length + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <param name="length">the number of bytes to read</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private byte[] ReadPRUData(uint offset, uint length)
        {
            byte[] outBytes = null;

            //Console.WriteLine("ReadPRUData called for " + PRUID.ToString());
   
            // do we have an offset?
            if (offset > 0)
            {
                // yes, we do. Perform the test this way
                if ((length + offset) > PRU_DRAM_SIZE)
                {
                    throw new Exception("The " + length.ToString() + " length to read and offset of " + offset.ToString() + " are too long. Max is " + PRU_DRAM_SIZE.ToString());
                }
            }
            else
            {
                // no, we do not. Perform the test this way
                if (length > PRU_DRAM_SIZE)
                {
                    throw new Exception("The " + length.ToString() + " length to read is too long. Max is " + PRU_DRAM_SIZE.ToString());
                }
            }

            // create our output dataBytes array
            outBytes = new byte[length];

            // get a pointer to the data register of the PRU as mapped by the UIO file
            // this assumes an offset of 0
            IntPtr dramPtr = PRUUIOMappedDRAMBase;
            if (dramPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU DRAM area");
            }
            // do we need to adjust for a user specified offset?
            if (offset != 0)
            {
                dramPtr = new IntPtr(dramPtr.ToInt64() + (long)offset);
            }

            // copy the data from our data ram pointer to the output array            
            Marshal.Copy(dramPtr, outBytes, 0, (Int32)length);
            return outBytes;
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Loads the contents of a specified byte array into the PRU
        /// data memory.
        /// 
        /// NOTE: the max size here is PRU_DRAM_SIZE
        /// </summary>
        /// <param name="dataBytes">The bytes of the data to load</param>
        /// <param name="offset">The offset into the PRU DataRAM we load the databytes
        /// into. Note that the databytes.Length + offset cannot be more than the
        /// PRU_DRAM_SIZE</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private void WritePRUData(byte[] dataBytes, uint offset)
        {
            //Console.WriteLine("WritePRUData called for " + PRUID.ToString());
            if ((dataBytes == null) || (dataBytes.Length == 0))
            {
                throw new Exception("The data bytes to load was null or zero length");
            }

            // do we have an offset?
            if (offset > 0)
            {
                // yes, we do. Perform the test this way
                if ((dataBytes.Length + offset) > PRU_DRAM_SIZE)
                {
                    throw new Exception("The " + dataBytes.Length.ToString() + " data bytes to load and offset of " + offset.ToString() + " are too long. Max is " + PRU_DRAM_SIZE.ToString());
                }
            }
            else
            {
                // no, we do not. Perform the test this way
                if (dataBytes.Length > PRU_DRAM_SIZE)
                {
                    throw new Exception("The " + dataBytes.Length.ToString() + " data bytes to load are too long. Max is " + PRU_DRAM_SIZE.ToString());
                }
            }

            // Console.WriteLine("WritePRUData DRAM Base is " + PRUUIOMappedDRAMBase.ToString("X8"));

            // get a pointer to the data register of the PRU as mapped by the UIO file
            // this assumes an offset of 0
            IntPtr dramPtr = PRUUIOMappedDRAMBase;
            if (dramPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU DRAM area");
            }
            // do we need to adjust for a user specified offset?
            if (offset != 0)
            {
                dramPtr = new IntPtr(dramPtr.ToInt64() + (long)offset);
            }

            // copy the data from the dataBytes to our data ram pointer               
            Marshal.Copy(dataBytes, 0, dramPtr, dataBytes.Length);
            // Console.WriteLine("WritePRUData complete for " + PRUID.ToString());
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Disables and resets the PRU
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private void DisablePRU()
        {

            // Console.WriteLine("DisablePRU called for " + PRUID.ToString());
            // Console.WriteLine("DisablePRU Control Offset is " + PRUUIOMappedControlBase.ToString("X8"));

            // get a pointer to the control register of the PRU as mapped by the UIO file
            IntPtr ctrlPtr = PRUUIOMappedControlBase;
            if (ctrlPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU control register");
            }
            // explicitly build the 32 bit data we write in order to disable and reset
            // the PRU. This is being done in a long winded manner so you can see 
            // exactly what is being done and why. Really we could just set it 
            // to 0x00000001 and be done with it
            uint ctrlRegisterValue = 0;
            // clear the two high bytes these are the starting address
            ctrlRegisterValue = ctrlRegisterValue & 0x0000FFFF;
            // clear bit 8 this turns off SINGLE_STEP this also turns off 
            // bits 15-9 which are read-only or undefined
            ctrlRegisterValue = ctrlRegisterValue & 0xFFFF00FF;
            // clear bits 7-1, a 0 in bit 3 turns off COUNTER_ENABLE
            // a 0 in bit 2 turns off SLEEPING
            // a 0 in bit 1 turns off ENABLE (ie the PRU is disabled)
            ctrlRegisterValue = ctrlRegisterValue & 0xFFFF0000;
            // a 1 in bit 0 forces a SOFT_RESET when this bit is set
            //     back to 0 in a later EnablePRU() call
            ctrlRegisterValue = ctrlRegisterValue | 0x00000001;
            // Console.WriteLine("DisablePRU Control ctrlRegisterValue is " + ctrlRegisterValue.ToString("X8"));

            // write the data to the control register
            Marshal.WriteInt32(ctrlPtr, (int)ctrlRegisterValue);
            // Console.WriteLine("DisablePRU complete for " + PRUID.ToString());
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Enables the PRU, and runs the binary code loaded into its instruction
        /// memory.
        /// 
        /// NOTE: the PRU has to start its code on a 32 bit boundary. This means
        /// that the starting address in bytes will be divided by four before
        /// being set in the Control register. Your actual start point in the 
        /// binary file should be at a 32 bit address.
        /// 
        /// </summary>
        /// <param name="startOffsetInBytes">The starting address (in bytes) at
        /// which the PRU should begin executing.</param>
        /// code in the 
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private void EnableAndExecutePRU(uint startOffsetInBytes)
        {

            // Console.WriteLine("EnablePRU called for " + PRUID.ToString());
            // Console.WriteLine("EnablePRU Controll Offset is " + PRUUIOMappedControlBase.ToString("X8"));

            // get a pointer to the control register of the PRU as mapped by the UIO file
            IntPtr ctrlPtr = PRUUIOMappedControlBase;
            if (ctrlPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU control register");
            }

            // explicitly build the 32 bit data we write in order to enable and
            // run the code in the PRU. This is being done in a long winded manner 
            // so you can see  exactly what is being done and why Really we could 
            // just divide the incoming offset by 4, place it in the top two bytes
            // and set the low word to 0x02 and be done with it
            uint ctrlRegisterValue = 0;
            // divide the incoming byte offset by 4 so it is an instruction start
            // shift it up in into the top two bytes and set it as the starting address
            ctrlRegisterValue = ctrlRegisterValue | ((startOffsetInBytes/4) << 16);
            // clear bit 8 this turns off SINGLE_STEP this also turns off 
            // bits 15-9 which are read-only or undefined
            ctrlRegisterValue = ctrlRegisterValue & 0xFFFF00FF;
            // clear bits 7-1, a 0 in bit 3 turns off COUNTER_ENABLE
            // a 0 in bit 2 turns off SLEEPING
            ctrlRegisterValue = ctrlRegisterValue & 0xFFFF0000;
            // a 1 in bit 1 turns on ENABLE (ie the PRU is enabled)
            ctrlRegisterValue = ctrlRegisterValue | 0x00000002;
            // a 0 in bit 0 forces a SOFT_RESET if this bit was set
            //     to 1 in a earlier DisablePRU() call
            ctrlRegisterValue = ctrlRegisterValue & 0xFFFFFFF6;
            // Console.WriteLine("EnablePRU Controll ctrlRegisterValue is " + ctrlRegisterValue.ToString("X8"));

            // write the data
            Marshal.WriteInt32(ctrlPtr, (int)ctrlRegisterValue);
            // Console.WriteLine("EnablePRU complete for " + PRUID.ToString());
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Sets up the pru. Must be done prior to loading and executing a binary
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private void SetupPRU()
        {
            int pruMMap_fd = -1;

            try
            {
                // Now we want to get the memory map for the PRU. To do this we need a UIO device
                // Since we close the device after we set up the memory map it doesn't really
                // matter which one we use. We alway just use PRUUIODeviceEnum.PRUUIODEVICE_0

                // get the filename for the pru uio device. If this does not exist then 
                // the modprobe(uio_pruss) has not been issued or the BB-BONE-PRU-01 overlay
                // has not been setup in the device tree
                // We expect to see that the files /dev/uio[0-7] exist
                string pruUIOFile = PRU_UIODEVICE_NAME.Replace(PRUUIONUM,GetPruUIODeviceAsNumber(PRUUIODeviceEnum.PRUUIODEVICE_0).ToString());
                // Console.WriteLine("pruUIOFile=" +pruUIOFile); 

                // open the pru uio device it is a device file that maps the pru address space 
                // into user space memory. This file is also used to send and receive interrupt
                // events to and from the PRU. However, this function does not care about that.
                pruMMap_fd = Syscall.open(pruUIOFile, OpenFlags.O_RDWR|OpenFlags.O_SYNC);
                if (pruMMap_fd == -1) throw new Exception ("Could not open UIO device file: " + pruUIOFile);

                /* NOT NECESSARY? the prussdrv.c code does this but I do not know why
                 * it does not appear to be used.
                // read the line from the UIO base file. This will tell us the base address of
                // the pru subsystem, we can use it to figure out specific values for PRU0 and PRU1
                string pruUIOBaseFile = PRU_UIO_BASE_PATH.Replace(PRUUIONUM,GetPruUIODeviceAsNumber().ToString());
                string[] baseLines = System.IO.File.ReadAllLines(pruUIOBaseFile);
                if ((baseLines == null) || (baseLines.Length == 0)) throw new Exception("Error opening and reading pruUIOBaseFile: " + pruUIOBaseFile);
                // convert to a UINT
                pruPhysBaseAddr = Convert.ToUInt32(baseLines[0], 16);
      
                // read the line from the UIO size file. This will tell us the size of the PRU
                // subsystem mapped into user space
                string pruUIOSizeFile = PRU_UIO_SIZE_PATH.Replace(PRUUIONUM,GetPruUIODeviceAsNumber().ToString());
                string[] sizeLines = System.IO.File.ReadAllLines(pruUIOSizeFile);
                if ((sizeLines == null) || (sizeLines.Length == 0)) throw new Exception("Error opening and reading pruUIOSizeFile: " + pruUIOSizeFile);
                // convert to a UINT
                pruPhysMapSize = Convert.ToUInt32(sizeLines[0], 16);
*/
                pruPhysMapSize = PRU_UIO_PHYSMAPSIZE;
                // Console.WriteLine("pruPhysBaseAddr " + pruPhysBaseAddr.ToString("X8")); 
                // Console.WriteLine("pruPhysMapSize " + pruPhysMapSize.ToString("X8")); 

                // map in the PRU Device File into memory 
                pruMappedMem = Syscall.mmap (IntPtr.Zero, pruPhysMapSize, MmapProts.PROT_WRITE | MmapProts.PROT_READ, MmapFlags.MAP_SHARED, pruMMap_fd, PRU_UIO_MMAP_BASE);
                if (pruMappedMem == (IntPtr)(-1))
                {
                    throw new IOException ("mmap failed for pru device file " + "(" + pruPhysBaseAddr.ToString("X8") + ", " + pruPhysMapSize.ToString("X8") + ")");
                }
                // Console.WriteLine("PRU Memory Now Mapped pruMappedMem="+pruMappedMem.ToString("X8")); 

            }
            finally
            {
                // once the memory is mapped the file can be closed.
                if (pruMMap_fd > 0)
                {
                    Syscall.close(pruMMap_fd);
                    pruMMap_fd = -1;
                }
            }
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Resets and initializes the interrupts and events for all PRU's (PRU0 and PRU1).
        /// 
        /// The PRU's share a interrupt controller subsystem so calling this 
        /// function once is sufficient for both PRUs.
        /// 
        /// Note: if you set up events for one PRU then call this to init
        ///       the events for the other PRU you will clear the event
        ///       configuration for the first PRU. Call this function 
        ///       once!
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void InitInterruptsForAllPRUs()
        {
            // reset everything, get it all back to square 1
            ResetInterruptsForAllPRUs();
            // set up the standard mapping from which the user can
            // enable or disable specific interrupts
            SetupStandardHostAndChannelMappingForAllPRUs();
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// The system interrupts, channels, hosts and corresponding events
        /// and interrupts can be configured in a near infinite number of ways.
        /// 
        /// In order to keep things simple a standard, documented, arrangement
        /// is configured and the user can then enable specific interrupts to
        /// perform the desired actions. This does mean that many of the PRU
        /// system interrupts are left unconnected. This setup is intended
        /// primarily to let the PRU trigger a host interrupt which will propagate
        /// down to user space and be visible on one of the uio* files
        /// 
        /// The PRU's share a interrupt controller subsystem so calling this 
        /// function sets the standard configuration for both PRUs
        /// 
        /// NOTE: a call to ResetInterruptsForAllPRUs() should be made to reset
        /// things before calling this function
        /// 
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void SetupStandardHostAndChannelMappingForAllPRUs()
        {
            byte[] fourDataBytes = new byte[sizeof(UInt32)];

            if (pruMappedMem == (IntPtr)(-1))
            {
                throw new Exception ("Could not map standard PRU events, device file failed to open.");
            }  

            // get a pointer to the interrupt controller of the PRU as mapped by the UIO file
            IntPtr intcPtr = PRUUIOMappedINTCBase;
            if (intcPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU INTC area");
            }

            // ########
            // ######## CMR[00-15]: Setup the mapping of the system interrupts to the channels
            // ########

            // The 16 Channel Map Registers specify the channel for the system interrupts 0 to 63. 
            // There is one register per 4 system interrupts. We are only interested in SysInts 16-31
            // here since these are the only ones the PRU can raise. The others are all raised by
            // various peripherals such as the UART or EtherCat. All interrupts are mapped onto
            // channel 0 (they have to go somewhere) and SysInts 16-23 are mapped onto Channels 2-9
            // SysInts  24-29 onto Channel 9, SysInt 30 onto Channel 0 and SysInt 31 onto Channel 1

            // our standard mapping is...
            //   systemInterrupt0 -> channel0
            //   systemInterrupt1 -> channel0
            //   ...
            //   systemInterrupt15-> channel0
            //   systemInterrupt16->  channel2
            //   systemInterrupt17->  channel3
            //   ...
            //   systemInterrupt23 -> channel9
            //   systemInterrupt24->  channel9
            //   systemInterrupt25->  channel9
            //   ...
            //   systemInterrupt29 -> channel9
            //   systemInterrupt30->  channel0
            //   systemInterrupt31->  channel1
            //   systemInterrupt32 -> channel0
            //   ...
            //   systemInterrupt62 -> channel0
            //   systemInterrupt63 -> channel0

            // these CMRxx registers work by setting the channel number for the system interrupt
            // in the bottom four bits of each byte. For example CMR00 maps sysInts0,1,2,3
            // so if we write a byte like 0x00030501 to CMR00 it means that system interrupt
            // 0 is mapped to channel 1, sysInt 2 to channel 5, sysint 3 to channel 3 and 
            // sysInt 4 is mapped to channel 0

            // map sysInts 0-15 to channel 0
            UInt32 cmrSetVal = 0x00000000;
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            IntPtr cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR00);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR01);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR02);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR03);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            // map sysInts 16-19 to channels 2-5
            cmrSetVal = 0x05040302;
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR04);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            // map sysInts 20-23 to channels 6-9
            cmrSetVal = 0x09080706;
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR05);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            // map sysInts 24-27 to channel 9
            cmrSetVal = 0x09090909;
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR06);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            // map sysInts 28-29 to channel 9 and sysInts 30-31 to channels 0 and 1
            cmrSetVal = 0x01000909;
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR07);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            // map sysInts 32-61 to channel 0
            cmrSetVal = 0x00000000;
            BitConverter.GetBytes(cmrSetVal).CopyTo(fourDataBytes,0);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR08);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR09);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR10);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR11);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR12);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR13);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR14);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR15);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);

            // ########
            // ######## HMR[0-2]: set the mapping of the channels to the Host Interrupts
            // ########

            // The Host Interrupt Map Registers define the host interrupts for channels. 
            // There is one register per 4 channels. We simply map channel0 to host 0
            // channel 1 to host 1 and so on

            // map channel 0-3 to hosts 0-3
            UInt32 hmrSetVal = 0x03020100;
            BitConverter.GetBytes(hmrSetVal).CopyTo(fourDataBytes,0);
            IntPtr hmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_HMR0);     
            Marshal.Copy(fourDataBytes, 0, hmrPtr, fourDataBytes.Length);
            // map channel 4-7 to hosts 4-7
            hmrSetVal = 0x07060504;
            BitConverter.GetBytes(hmrSetVal).CopyTo(fourDataBytes,0);
            hmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_HMR1);     
            Marshal.Copy(fourDataBytes, 0, hmrPtr, fourDataBytes.Length);
            // map channel 8-9 to hosts 8-9
            hmrSetVal = 0x00000908;
            BitConverter.GetBytes(hmrSetVal).CopyTo(fourDataBytes,0);
            hmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_HMR2);     
            Marshal.Copy(fourDataBytes, 0, hmrPtr, fourDataBytes.Length);

            // ########
            // ######## ESR[0-1]: Enable system interrupts mapping to channels
            // ########

            // The System Interrupt Enable Set Registers enable system interrupts to trigger outputs. System
            // interrupts that are not enabled do not interrupt the host. There is a bit per system interrupt.

            // set up a suitable data structure
            UInt32 esrResetVal = 0x00ff0000; // ie  0000 0000 1111 1111 0000 0000 0000 0000
            BitConverter.GetBytes(esrResetVal).CopyTo(fourDataBytes,0);
            // get a pointer to ECR0, and copy the data
            IntPtr esrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_ESR0);     
            Marshal.Copy(fourDataBytes, 0, esrPtr, fourDataBytes.Length);
            esrResetVal = 0x00000000;   // ie enable nothing
            BitConverter.GetBytes(esrResetVal).CopyTo(fourDataBytes,0);
            // get a pointer to ECR1, and copy the data
            esrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_ESR1);     
            Marshal.Copy(fourDataBytes, 0, esrPtr, fourDataBytes.Length);

            // at this point we have setup the standard mapping. So activating
            // systemInterrupt16 will propagate through channel2 to host interrupt2
            // similarly systemInterrupt17 will propagate through channel3 to host interrupt3

            // hostInterrupts0 and 1 are left by the UIO subsystem for signalling between
            // the two PRUs because the functionality to support this is hard coded into 
            // the PRU INTC system. Host Interrupts 2 to 9 are hard coded by the uio subsystem
            // to appear in userspace via the uio device files /dev/uio0 to /dev/uio7. 

            // This means that if you trigger PRU systemInterrupt16 you will see it appear
            // on /dev/uio0. Similarly, triggering PRU systemInterrupt17 will cause it to
            // appear on /dev/uio1 The UIO file number is always TWO less than the 
            // system interrupt. NOTE: the only reason this works like that is because the 
            // above standard setup routes sysInt2 through channel2 through host2 and
            // host2 is tied to uio0 by the UIO subsystem. 

            // If the above mapping seems complicated to you that is because
            // it is designed to be easy to conceptualize on the input and output. Basically
            // writing a 0 (sysint16) to R31 bits 3-0 makes the interrupt happen on uio0 in user space.
            // Similarly writing a 1 (sysint17) makes the event happen on uio1. The two PRU-to-PRU 
            // interrupts (sysint30 and sysint31) are a bit different. Writing a 14 (sysint30) 
            // makes the PRU activate the interrupt flag on R31 bit 30 and writing a 15 (sysInt31)
            // activates the interrupt flag on R31 bit 31. Thus the input and output are 
            // roughly correlated in a hopefully memorable way.

            // another way to think if it is that things have been arranged so that the 
            // bottom 4 bits of the System Interrupt represent the uio file number on
            // which the event appears and also are equal to the PRUEventEnum number
            // Similarly, if you write a value of 14 (bottom 4 bits of 30) to R31 you trigger 
            // sysInt30 which appears on R31 bit 30 when it is read and so on.

            // NOTE that only the mapping has happened here. None of the system interrupts
            // or host interrupts or the global interrupts are enabled yet. That is done
            // by the userspace program via the EnablePRUEvent() call


        }
                   
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Enables a PRU System Event at the Host level. Our standard mapping has ensured 
        /// that PRU systemInterrupts 16-23 are mapped to host Interrupts 2-9 and all
        /// we need to do is enable that if we want to use the functionality.
        /// 
        /// NOTE: hostInterrupts0 and 1 are left by the UIO subsystem for signalling between
        /// the two PRUs because the functionality to support this is hard coded into 
        /// the PRU INTC system. Host Interrupts 2 to 9 appear in userspace via the 
        /// uio device files /dev/uio0 to /dev/uio7. 
        ///
        /// This means that if you trigger PRU systemInterrupt16 you will see it appear
        /// on /dev/uio0. Similarly triggering PRU systemInterrupt17 will cause it to
        /// appear on /dev/uio1 The UIO file number which receives the event 
        /// is always the low four bits of the system interrupt. The only reason this 
        /// works like that is because the standard setup routes sysInt16 through channel2 
        /// through host2 and host2 is tied to uio0 by the UIO subsystem.
        ///
        /// </summary>
        /// <param name="pruEventIn">the host event.
        /// This must have been opened with a call to OpenPRUEvent</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void EnablePRUHostEvent(PRUEventEnum pruEventIn)
        {
            byte[] fourDataBytes = new byte[sizeof(UInt32)];

            if (pruMappedMem == (IntPtr)(-1))
            {
                throw new Exception ("Could not enable PRU event " + pruEventIn.ToString() + " device file failed to open.");
            }

            // open up the event uio file
            OpenPRUHostEvent(pruEventIn);

            // check we have a key
            if (enabledEvents.ContainsKey(pruEventIn) == false)
            {
                throw new Exception ("Could not enable PRU event " + pruEventIn.ToString() + " host event is not present");
            }  
            if ((enabledEvents[pruEventIn] == null) || (enabledEvents[pruEventIn].EventFD == PRUEventHandler.NO_FD))
            {
                // should never happen
                throw new Exception ("Could not enable PRU event " + pruEventIn.ToString() + " host event is not open");
            }  

            // get a pointer to the interrupt controller of the PRU as mapped by the UIO file
            IntPtr intcPtr = PRUUIOMappedINTCBase;
            if (intcPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU INTC area");
            }

            // ########
            // ######## SECR0: Be sure to remove and clear any interrupts which may be pending
            // ########

            // The System Interrupt Status Enabled/Clear Registers show the pending 
            // enabled status of the system interrupts. We write a 1 to the register
            // bit which represents the system interrupt. 

            // Note our standard configuration only uses System interrupts 16-31 so we only
            // need to deal with SECR0

            // put a 1 in position 0
            UInt32 secrResetVal = 0x00000001;
            // shift it up into position according to the system interrupt number, the
            // host event to system event correlation is entirely dependent on the 
            // standard configuration we use
            secrResetVal = secrResetVal << GetSystemEventNumberFromPruEvent(pruEventIn);
            BitConverter.GetBytes(secrResetVal).CopyTo(fourDataBytes,0);

            // get a pointer to SECR0, and copy the data
            IntPtr secrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_SECR0);     
            Marshal.Copy(fourDataBytes, 0, secrPtr, fourDataBytes.Length);
            // we can ignore SECR1, since we are ignoring any system interrupts it configures

            // ########
            // ######## HIEISR: Enable the triggering of the host interrupts
            // ########

            // these are mapped onto the UIO files as part of the standard setup
            // HostEvents 0,1 are reserved for PRU to PRU interrupts. Host Event 2 
            // goes to UIO0, Host Event 1 to UIO1 etc

            // The Host Interrupt Enable Indexed Set Register enables a host interrupt output. 
            // The host 0-9 interrupt to enable is the value written. Only the bottom 10 bits are used

            // get the host interrupt which corresponts to the pruEvent
            UInt32 hieisrSetVal = GetHostEventFromPruEvent(pruEventIn);
            BitConverter.GetBytes(hieisrSetVal).CopyTo(fourDataBytes,0);

            // get a pointer to HIEISR, and copy the data
            IntPtr hieisrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_HIEISR);     
            Marshal.Copy(fourDataBytes, 0, hieisrPtr, fourDataBytes.Length);

            // ########
            // ######## GER: Enable all interrupts - host or PRU system
            // ########

            // The Global Host Interrupt Enable Register enables all the host interrupts. 
            // Individual host interrupts are still enabled or disabled from their individual 
            // enables and are not overridden by the global enable. 

            // this is just set to 0x01 to enable and 0x00 to disable 

            // set up a suitable data structure
            UInt32 gerResetVal = 0x00000001;
            BitConverter.GetBytes(gerResetVal).CopyTo(fourDataBytes,0);

            // get a pointer to GER, and copy the data
            IntPtr gerPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_GER);     
            Marshal.Copy(fourDataBytes, 0, gerPtr, fourDataBytes.Length);

        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Opens up a PRU Host Event. These correspond one-to-one to a uio file
        /// </summary>
        /// <param name="pruEventIn">the host event we open</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private void OpenPRUHostEvent(PRUEventEnum pruEventIn)
        {

            if (pruMappedMem == (IntPtr)(-1))
            {
                throw new Exception ("Could not open PRU event " + pruEventIn.ToString() + " device file failed to open.");
            }

            // check the event record
            if (enabledEvents.ContainsKey(pruEventIn) == true)
            {
                // we have an event, but is it open?
                if (enabledEvents[pruEventIn].EventFD != PRUEventHandler.NO_FD)
                {
                    throw new Exception("Could not open PRU event " + pruEventIn.ToString() + " event is already open");
                }
            }
            else
            {
                // add the event record now
                enabledEvents.Add(pruEventIn, new PRUEventHandler(pruEventIn, this));
            }

            // the events (interrupts) are sent and received by interacting
            // the apropriate UIO device - we must open this device.
            // the later configuration of the event will enable the 
            // pre-configured system interrupts to appear on this device file

            // convert the event to a device
            PRUUIODeviceEnum eventUIODevice = GetPruUIODeviceFromPRUEvent(pruEventIn);

            try
            {
                // get the filename for the pru uio device. 
                string pruUIOFile = PRU_UIODEVICE_NAME.Replace(PRUUIONUM,GetPruUIODeviceAsNumber(eventUIODevice).ToString());
 
                // open the pru uio device. Besides the fact that it is a device file that maps 
                // the pru address space into user space memory it also can send and receive 
                // events from the PRU by being written to and read from in a certain way. Because
                // we wish to have the capability of enabling multiple events and the event
                // number is tied to the file, we do not use the memory map uio file for the 
                // uio event file - even though we could if we only had one event. It is better
                // to just open another copy and use that.
                enabledEvents[pruEventIn].EventFD = Syscall.open(pruUIOFile, OpenFlags.O_RDWR|OpenFlags.O_SYNC);
                if (enabledEvents[pruEventIn].EventFD == PRUEventHandler.NO_FD) throw new Exception ("Could not open UIO device event file: " + pruUIOFile);

                // Console.WriteLine("PRU Event opened "+pruEventIn.ToString()); 
                // Console.WriteLine("PRU Event pruUIOFile  "+pruUIOFile); 
            }
            finally
            {
            }
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Waits for the specified event. Will stall with a blocking read
        /// until the event is triggered. It also possible to set up an
        /// asynchronous event via the PRUEventMonitorOn/Off call and
        /// receive the events via a handler function while the main
        /// thread is doing something else
        /// </summary>
        /// <param name="pruEventIn">the host event we wait for</param>
        /// <returns>
        ///   the total number of events processed by the uio file (from any
        ///   source) since boot up.
        /// </returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public UInt32 WaitForEvent(PRUEventEnum pruEventIn)
        {
            if (enabledEvents[pruEventIn].EventFD == PRUEventHandler.NO_FD)
            {
                throw new Exception("Host Event Not Open");
            }
                
            // call the wait function in the PRUEventHandler
            return enabledEvents[pruEventIn].WaitForEvent();
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Begins Monitoring a PRU event. This allows us to process asynchronous
        /// event actions. The pruEventHander will get the event information when
        /// the event is triggered.
        /// </summary>
        /// <param name="pruEventIn">the host event we begin monitoring</param>
        /// <param name="pruEventHander">the event data handler. Cannot be NULL</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void PRUEventMonitorOn(PRUEventEnum pruEventIn, PRUInterruptEventHandlerUIO pruEventHander)
        {
            if (enabledEvents[pruEventIn].EventFD == PRUEventHandler.NO_FD)
            {
                throw new Exception("Host Event Not Open");
            }
            if (pruEventHander == null)
            {
                throw new Exception("No Event Handler Supplied");
            }
            // set the handler now
            enabledEvents[pruEventIn].OnInterrupt += pruEventHander;
            // start the monitoring
            enabledEvents[pruEventIn].StartPRUEventMonitor();
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Stop monitoring a PRU event. 
        /// </summary>
        /// <param name="pruEventIn">the host event we stop monitoring</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void PRUEventMonitorOff(PRUEventEnum pruEventIn)
        {
            if (enabledEvents[pruEventIn].EventFD == PRUEventHandler.NO_FD)
            {
                throw new Exception("Host Event Not Open");
            }
            // start the monitoring
            enabledEvents[pruEventIn].StopPRUEventMonitor();
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Clears the specified event - this should be called after every event
        /// (synchronous or asynchronous) and must be done or the event cannot
        /// be re-triggered
        /// </summary>
        /// <param name="pruEventIn">the host event we clear</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void ClearEvent(PRUEventEnum pruEventIn)
        {
            byte[] fourDataBytes = new byte[sizeof(UInt32)];

            if (pruMappedMem == (IntPtr)(-1))
            {
                throw new Exception ("Could not reset PRU events, device file failed to open.");
            }              

            // get a pointer to the interrupt controller of the PRU as mapped by the UIO file
            IntPtr intcPtr = PRUUIOMappedINTCBase;
            if (intcPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU INTC area");
            }

            // ########
            // ######## SECR0: Remove and clear the interrupt
            // ########

            // The System Interrupt Status Enabled/Clear Registers show the pending 
            // enabled status of the system interrupts. We write a 1 to the register
            // bit which represents the system interrupt. 

            // Note our standard configuration only uses System interrupts 16-31 so we only
            // need to deal with SECR0

            // put a 1 in position 0
            UInt32 secrResetVal = 0x00000001;
            // shift it up into position according to the system interrupt number, the
            // host event to system event correlation is entirely dependent on the 
            // standard configuration we use
            secrResetVal = secrResetVal << GetSystemEventNumberFromPruEvent(pruEventIn);
            BitConverter.GetBytes(secrResetVal).CopyTo(fourDataBytes,0);
            secrResetVal = 0xffffffff;
            // get a pointer to SECR0, and copy the data
            IntPtr secrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_SECR0);     
            Marshal.Copy(fourDataBytes, 0, secrPtr, fourDataBytes.Length);
            // we can ignore SECR1, since we are ignoring any system interrupts it configures
 
            // ########
            // ######## HIEISR
            // ########

            // get the number host interrupt which corresponts to the pruEvent
            // writing that number to the HIEISR will re-enable the interrupt
            // we have to do this after clearing the interrupt or it will just
            // cause the old interrupt to be re-triggered.
            UInt32 hieisrSetVal = GetHostEventFromPruEvent(pruEventIn);
            BitConverter.GetBytes(hieisrSetVal).CopyTo(fourDataBytes,0);
            IntPtr hieisrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_HIEISR);     
            Marshal.Copy(fourDataBytes, 0, hieisrPtr, fourDataBytes.Length);

        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Resets the events for all PRU's (PRU0 and PRU1).
        /// 
        /// The PRU's share a interrupt controller subsystem so calling this 
        /// function resets the interrupts and events for both PRUs
        /// 
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void ResetInterruptsForAllPRUs()
        {
            byte[] fourDataBytes = new byte[sizeof(UInt32)];

            if (pruMappedMem == (IntPtr)(-1))
            {
                throw new Exception ("Could not reset PRU events, device file failed to open.");
            }              

            // get a pointer to the interrupt controller of the PRU as mapped by the UIO file
            IntPtr intcPtr = PRUUIOMappedINTCBase;
            if (intcPtr == (IntPtr)(-1))
            {
                throw new IOException ("Failed to get pointer to PRU INTC area");
            }

            // NOTE: Since this setup code is not particularly performance critical 
            // and I wish to illustrate what is going on. I am explicitly coding the setup 
            // for the registers rather than just using one call to update all of them or
            // using some sort of clever loop which builds the offsets

            // begin the disable. The code starts by disabling at the most global
            // level and gets more and more specific on the reset in order to 
            // prevent a reset inadvertently triggering an interrupt

            // ########
            // ######## GER: Disable all interrupts - host or PRU system
            // ########

            // The Global Host Interrupt Enable Register enables all the host interrupts. 
            // Individual host interrupts are still enabled or disabled from their individual 
            // enables and are not overridden by the global enable.

            // this is just set to 0x01 to enable and 0x00 to disable

            // set up a suitable data structure
            UInt32 gerResetVal = 0x00000000;
            BitConverter.GetBytes(gerResetVal).CopyTo(fourDataBytes,0);
            // get a pointer to GER, and copy the data
            IntPtr gerPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_GER);     
            Marshal.Copy(fourDataBytes, 0, gerPtr, fourDataBytes.Length);

            // ########
            // ######## HIDSR: Prevent host interrupts from triggering
            // ########

            // The Host Interrupt Enable Indexed Clear Register disables a host interrupt output. 
            // The host 0-9 interrupt to disable is the bit position written. This disables the host 
            // interrupt output. Only the bottom 10 bits are used

            // since we are resetting, we wish to disable all host interrupts, so we write a
            // a series of values 0 to 10 here

             // get a pointer to HIDISR, and copy the data
            // NOTE one wonders why it is not called the HIEICR register to remain consistent
            //      with the other naming conventions. The HIEISR register is used to re-enable
            IntPtr hidisrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_HIDISR);     
            for (UInt32 hidisrResetVal = MIN_HOSTINT; hidisrResetVal <= MAX_HOSTINT; hidisrResetVal++)
            {
                BitConverter.GetBytes(hidisrResetVal).CopyTo(fourDataBytes, 0);
                Marshal.Copy(fourDataBytes, 0, hidisrPtr, fourDataBytes.Length);
            }

            // ########
            // ######## EICR: Prevent system interrupts from triggering
            // ########

            // The System Interrupt Enable Indexed Clear Register allows disabling a system interrupt. 
            // This the index value written. This clears the Enable Register bit of the given index.
            // Only the bottom 10 bits are used

            // since we are resetting, we wish to disable all host interrupts, so we write a
            // a series of values 0 to 61 here

            // get a pointer to EICR
            IntPtr eicrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_EICR);     
            for (UInt32 eicrResetVal = MIN_SYSINT; eicrResetVal <= MAX_SYSINT; eicrResetVal++)
            {
                BitConverter.GetBytes(eicrResetVal).CopyTo(fourDataBytes, 0);
                Marshal.Copy(fourDataBytes, 0, eicrPtr, fourDataBytes.Length);
            }

            // ########
            // ######## ECR: Prevent system interrupts from mapping to channels
            // ########

            // The System Interrupt Enable Clear Registers disable system interrupts mapping to 
            // channels. System interrupts that have a disabled channel mapping do not interrupt 
            // the host. There is a bit per system interrupt. We disable all system interrupt 
            // mappings by writing a 1 in each bit position

            // set up a suitable data structure
            UInt32 ecrResetVal = 0xffffffff;
            BitConverter.GetBytes(ecrResetVal).CopyTo(fourDataBytes,0);

            // get a pointer to ECR0, and copy the data
            IntPtr ecrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_ECR0);     
            Marshal.Copy(fourDataBytes, 0, ecrPtr, fourDataBytes.Length);
            // get a pointer to ECR1, and copy the data
            ecrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_ECR1);     
            Marshal.Copy(fourDataBytes, 0, ecrPtr, fourDataBytes.Length);

            // ########
            // ######## SECR: remove and clear any interrupts which may be pending
            // ########

            // The System Interrupt Status Enabled/Clear Registers show the pending 
            // enabled status of the system interrupts. There is 1 bit per system interrupt
            // We write a 1 to each bit of these registers so that we clear all system interrupts 

            // set up a suitable data structure
            UInt32 secrResetVal = 0xffffffff;
            BitConverter.GetBytes(secrResetVal).CopyTo(fourDataBytes,0);

            // get a pointer to SECR0, and copy the data
            IntPtr secrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_SECR0);     
            Marshal.Copy(fourDataBytes, 0, secrPtr, fourDataBytes.Length);
            // get a pointer to SECR1, and copy the data
            secrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_SECR1);     
            Marshal.Copy(fourDataBytes, 0, secrPtr, fourDataBytes.Length);
 
            // ########
            // ######## HMR[0-2]: reset the mapping of the channels to the Host Interrupts
            // ########

            // The Host Interrupt Map Registers define the host interrupts for channels. 
            // There is one register per 4 channels. We init these all to 0 now which means
            // all channels are mapped to a host interrupt 0. A more appropriate mapping is
            // performed later in our standard setup.  

            UInt32 hmrResetVal = 0x00000000;
            BitConverter.GetBytes(hmrResetVal).CopyTo(fourDataBytes,0);
            // get a pointer to the HMR regs, and copy the data
            IntPtr hmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_HMR0);     
            Marshal.Copy(fourDataBytes, 0, hmrPtr, fourDataBytes.Length);
            hmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_HMR1);     
            Marshal.Copy(fourDataBytes, 0, hmrPtr, fourDataBytes.Length);
            hmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_HMR2);     
            Marshal.Copy(fourDataBytes, 0, hmrPtr, fourDataBytes.Length);

            // ########
            // ######## CMR[00-63]: Reset the mapping of the system interrupts to the channels
            // ########

            // The 16 Channel Map Registers specify the channel for the system interrupts 0 to 63. 
            // There is one register per 4 system interrupts.

            // all we are doing here is resetting all 16 CMR regs to 0). This means
            // all system interrupts are mapped to a channel 0. A more appropriate mapping is
            // performed later in our standard setup.  

            UInt32 cmrResetVal = 0x00000000;
            BitConverter.GetBytes(cmrResetVal).CopyTo(fourDataBytes,0);
            // get a pointer to the CMR regs, and copy the data
            IntPtr cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR00);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR01);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR02);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR03);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR04);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR05);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR06);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR07);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR08);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR09);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR10);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR11);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR12);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR13);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR14);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);
            cmrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CMR15);     
            Marshal.Copy(fourDataBytes, 0, cmrPtr, fourDataBytes.Length);

            // ########
            // ######## SIPR[0-1]: Reset the interrupt polarity settings
            // ######## 

            // The System Interrupt Polarity Registers define the polarity of the system interrupts
            // We always want active high interrupts so we write a 1 to each bit in both SIPR registers.

            // set up a suitable data structure
            UInt32 siprResetVal = 0xffffffff;
            BitConverter.GetBytes(siprResetVal).CopyTo(fourDataBytes,0);

            // get a pointer to SIPR0, and copy the data
            IntPtr siprPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_SIPR0);     
            Marshal.Copy(fourDataBytes, 0, siprPtr, fourDataBytes.Length);
            // get a pointer to SIPR1, and copy the data
            siprPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_SIPR1);     
            Marshal.Copy(fourDataBytes, 0, siprPtr, fourDataBytes.Length);

            // ########
            // ######## SITR[0-1]: Reset the interrupt type settings
            // ######## 

            // The System Interrupt Type Registers define the type of the system interrupts. 
            // The type of all system interrupts must be pulse. We always write 0 to the bits 
            // of these registers. 

            // set up a suitable data structure
            UInt32 sitrResetVal = 0x00000000;
            BitConverter.GetBytes(sitrResetVal).CopyTo(fourDataBytes,0);

            // get a pointer to SITR0, and copy the data
            IntPtr sitrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_SITR0);     
            Marshal.Copy(fourDataBytes, 0, sitrPtr, fourDataBytes.Length);
            // get a pointer to SITR1, and copy the data
            sitrPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_SITR1);     
            Marshal.Copy(fourDataBytes, 0, sitrPtr, fourDataBytes.Length);


            // ########
            // ######## CR: Disable all nesting
            // ########

            // The Global Host Interrupt Enable Register enables all the host interrupts. 
            // Individual host interrupts are still enabled or disabled from their individual 
            // enables and are not overridden by the global enable.

            // this is just set to 0x01 to enable and 0x00 to disable

            // set up a suitable data structure
            UInt32 crResetVal = 0x00000000;
            BitConverter.GetBytes(crResetVal).CopyTo(fourDataBytes,0);
            // get a pointer to CR, and copy the data
            IntPtr crPtr = new IntPtr(intcPtr.ToInt64() + (long)PRUOFFSET_INTC_CR);     
            Marshal.Copy(fourDataBytes, 0, crPtr, fourDataBytes.Length);
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// The address of the PRUs (0 or 1) control register as mapped
        /// into the main user space memory by the UIO file we opened
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private IntPtr PRUUIOMappedControlBase
        {
            get
            {
                if (PRUID == PRUEnum.PRU_0)
                {
                    return GetPRUUIOMappedPointer(PRUOFFSET_CONTROL_PRU0);
                }
                else if (PRUID == PRUEnum.PRU_1)
                {
                    return GetPRUUIOMappedPointer(PRUOFFSET_CONTROL_PRU1);
                }
                else
                {
                    throw new Exception("Unknown PRU: "+ PRUID.ToString());
                }                    
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// The address of the PRUs (0 or 1) Data RAM as mapped
        /// into the main user space memory by the UIO file we opened
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private IntPtr PRUUIOMappedDRAMBase
        {
            get
            {
                if (PRUID == PRUEnum.PRU_0)
                {
                    return GetPRUUIOMappedPointer(PRUOFFSET_DRAM_PRU0);
                }
                else if (PRUID == PRUEnum.PRU_1)
                {
                    return GetPRUUIOMappedPointer(PRUOFFSET_DRAM_PRU1);
                }
                else
                {
                    throw new Exception("Unknown PRU: "+ PRUID.ToString());
                }                    
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// The address of the PRUs (0 or 1) Interrupt Controller (INTC) as mapped
        /// into the main user space memory by the UIO file we opened. The two
        /// PRUs use the same INTC so this value will be the same for both
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private IntPtr PRUUIOMappedINTCBase
        {
            get
            {
                if (PRUID == PRUEnum.PRU_0)
                {
                    return GetPRUUIOMappedPointer(PRUOFFSET_INTC_PRU0);
                }
                else if (PRUID == PRUEnum.PRU_1)
                {
                    return GetPRUUIOMappedPointer(PRUOFFSET_INTC_PRU1);
                }
                else
                {
                    throw new Exception("Unknown PRU: "+ PRUID.ToString());
                }                    
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// The address of the PRUs (0 or 1) Instruction RAM as mapped
        /// into the main user space memory by the UIO file we opened
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private IntPtr PRUUIOMappedIRAMBase
        {
            get
            {
                if (PRUID == PRUEnum.PRU_0)
                {
                    return GetPRUUIOMappedPointer(PRUOFFSET_IRAM_PRU0);
                }
                else if (PRUID == PRUEnum.PRU_1)
                {
                    return GetPRUUIOMappedPointer(PRUOFFSET_IRAM_PRU1);
                }
                else
                {
                    throw new Exception("Unknown PRU: "+ PRUID.ToString());
                }                    
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the pointer for a memory mapped UIO file
        /// </summary>
        /// <param name="offsetIntoMap">the offset into the UIO memory mapped
        /// file to return
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private IntPtr GetPRUUIOMappedPointer(long offsetIntoMap)
        {
            if (pruMappedMem == (IntPtr)(-1))
            {
                throw new IOException ("no mmap for pru device file");
            }

            // build a pointer to the mapped file
            return new IntPtr(pruMappedMem.ToInt64() + offsetIntoMap);     
        }           

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Close the memory maps. 
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private void CloseMemoryMaps()
        {
            try
            {
                //Console.WriteLine("Closing PRU Memory Maps"); 
                // clean up the pru map
                if (pruMappedMem != IntPtr.Zero) Syscall.munmap(pruMappedMem, pruPhysMapSize);
                pruMappedMem = IntPtr.Zero;
            }
            catch {}
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Closes all events
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void CloseAllEvents()
        {
            foreach (KeyValuePair<PRUEventEnum, PRUEventHandler> kvPairObj in enabledEvents)
            {
                // close the event, will ignore it if already closed
                CloseEvent(kvPairObj.Key);
            }
        }
         
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Closes an event
        /// </summary>
        /// <param name="pruEventIn">the host event we close</param>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public void CloseEvent(PRUEventEnum pruEventIn)
        {
            if (enabledEvents.ContainsKey(pruEventIn) == false) return;
            // stop the worker thread - if there is one
            enabledEvents[pruEventIn].StopPRUEventMonitor();
            // if we have an open event file then close it
            if (enabledEvents[pruEventIn].EventFD != PRUEventHandler.NO_FD)
            {
                Syscall.close(enabledEvents[pruEventIn].EventFD);
                enabledEvents[pruEventIn].EventFD = PRUEventHandler.NO_FD;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the PRUUIODevice as a number
        /// </summary>
        /// <param name="eventIn">The event we convert</param>
        /// <returns>>the uio device enum or exception for fail</returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private PRUUIODeviceEnum GetPruUIODeviceFromPRUEvent(PRUEventEnum eventIn)
        {
            switch (eventIn)
            {
            case PRUEventEnum.PRU_EVENT_0:
                return PRUUIODeviceEnum.PRUUIODEVICE_0;
            case PRUEventEnum.PRU_EVENT_1:
                return PRUUIODeviceEnum.PRUUIODEVICE_1;
            case PRUEventEnum.PRU_EVENT_2:
                return PRUUIODeviceEnum.PRUUIODEVICE_2;
            case PRUEventEnum.PRU_EVENT_3:
                return PRUUIODeviceEnum.PRUUIODEVICE_3;
            case PRUEventEnum.PRU_EVENT_4:
                return PRUUIODeviceEnum.PRUUIODEVICE_4;
            case PRUEventEnum.PRU_EVENT_5:
                return PRUUIODeviceEnum.PRUUIODEVICE_5;
            case PRUEventEnum.PRU_EVENT_6:
                return PRUUIODeviceEnum.PRUUIODEVICE_6;
            case PRUEventEnum.PRU_EVENT_7:
                return PRUUIODeviceEnum.PRUUIODEVICE_7;
            default:
                {
                    throw new Exception("no such PRU Event: " + eventIn.ToString());
                }
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets a Host Event number from the PRU Event
        /// </summary>
        /// <param name="eventIn">The event we convert</param>
        /// <returns>>the event as a number or exception for fail</returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private uint GetHostEventFromPruEvent(PRUEventEnum eventIn)
        {
            switch (eventIn)
            {
            // host events 0 and 1 do not exist to user space programs. They are used for 
            // PRU to PRU interrupts
            case PRUEventEnum.PRU_EVENT_0:
                return 2;
            case PRUEventEnum.PRU_EVENT_1:
                return 3;
            case PRUEventEnum.PRU_EVENT_2:
                return 4;
            case PRUEventEnum.PRU_EVENT_3:
                return 5;
            case PRUEventEnum.PRU_EVENT_4:
                return 6;
            case PRUEventEnum.PRU_EVENT_5:
                return 7;
            default:
                {
                    throw new Exception("no such PRU Event: " + eventIn.ToString());
                }
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets a system event number as a number. Note this correlation
        /// is only valid if we are using the standard configuration setup.
        /// </summary>
        /// <param name="eventIn">The event we convert</param>
        /// <returns>>the system event number as a number or exception for fail</returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private int GetSystemEventNumberFromPruEvent(PRUEventEnum eventIn)
        {
            switch (eventIn)
            {
            case PRUEventEnum.PRU_EVENT_0:
                return 16;
            case PRUEventEnum.PRU_EVENT_1:
                return 17;
            case PRUEventEnum.PRU_EVENT_2:
                return 18;
            case PRUEventEnum.PRU_EVENT_3:
                return 18;
            case PRUEventEnum.PRU_EVENT_4:
                return 20;
            case PRUEventEnum.PRU_EVENT_5:
                return 21;
            case PRUEventEnum.PRU_EVENT_6:
                return 22;
            case PRUEventEnum.PRU_EVENT_7:
                return 23;
            default:
                {
                    throw new Exception("no such PRU Event: " + eventIn.ToString());
                }
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the PRUUIODevice as a number
        /// </summary>
        /// <param name="eventIn">The event we convert</param>
        /// <returns>>the uio device number</returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private int GetPruUIODeviceAsNumber(PRUEventEnum eventIn)
        {
            return GetPruUIODeviceAsNumber(GetPruUIODeviceFromPRUEvent(eventIn));
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the PRUUIODevice as a number
        /// </summary>
        /// <param name="pruUIODeviceIn">the device we find the number for</param>
        /// <returns>>the device number or exception for fail</returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private int GetPruUIODeviceAsNumber(PRUUIODeviceEnum pruUIODeviceIn)
        {
            switch (pruUIODeviceIn)
            {
            case PRUUIODeviceEnum.PRUUIODEVICE_0:
                return 0;
            case PRUUIODeviceEnum.PRUUIODEVICE_1:
                return 1;
            case PRUUIODeviceEnum.PRUUIODEVICE_2:
                return 2;
            case PRUUIODeviceEnum.PRUUIODEVICE_3:
                return 3;
            case PRUUIODeviceEnum.PRUUIODEVICE_4:
                return 4;
            case PRUUIODeviceEnum.PRUUIODEVICE_5:
                return 5;
            case PRUUIODeviceEnum.PRUUIODEVICE_6:
                return 6;
            case PRUUIODeviceEnum.PRUUIODEVICE_7:
                return 7;
            default:
                {
                    throw new Exception("no such PRU UIO Device: " + PRUUIODeviceEnum.PRUUIODEVICE_0.ToString());
                }
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the ID of a pru as a number
        /// </summary>
        /// <returns>>the pru id or exception for fail</returns>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        private int GetPruIDAsNumber()
        {
            switch (pruID)
            {
            case PRUEnum.PRU_0:
                return 0;
            case PRUEnum.PRU_1:
                return 1;
            default:
                {
                    throw new Exception("no such PRU ID: " + pruID.ToString());
                }
            }
        }
            
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Gets the ID of the PRU we are using
        /// </summary>
        /// <history>
        ///    05 Jun 15  Cynic - Originally written
        /// </history>
        public PRUEnum PRUID
        {
            get
            {
                return pruID;
            }
        }
            
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
        ///    05 Jun 15 Cynic - Originally written
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
        ///    05 Jun 15 Cynic - Originally written
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
        ///    05 Jun 15 Cynic - Originally written
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
                CloseAllEvents();
                CloseMemoryMaps();

                // Note disposing has been done.
                disposed = true;

            }
        }
        #endregion

    }
}

