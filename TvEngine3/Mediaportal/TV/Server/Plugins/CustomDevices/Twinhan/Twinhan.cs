﻿#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DirectShowLib;
using DirectShowLib.BDA;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Helper;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.Twinhan
{
  /// <summary>
  /// A class for handling conditional access and DiSEqC for Twinhan tuners, including clones from
  /// TerraTec, TechniSat and Digital Rise.
  /// </summary>
  public class Twinhan : BaseCustomDevice, /*ICustomTuner,*/ IPowerDevice, IMpeg2PidFilter, IConditionalAccessProvider, IConditionalAccessMenuActions, IDiseqcDevice
  {
    #region enums

    [Flags]
    private enum TwinhanDeviceType : uint
    {
      // Digital types
      DvbS = 0x0001,
      DvbT = 0x0002,
      DvbC = 0x0004,
      Atsc = 0x0008,
      AnnexC = 0x0010,        // US OpenCable (clear QAM)
      IsdbT = 0x0020,
      IsdbS = 0x0040,

      // Analog types
      Pal = 0x0100,
      Ntsc = 0x0200,
      Secam = 0x0400,
      Svideo = 0x0800,
      Composite = 0x1000,
      Fm = 0x2000,

      RemoteControlSupported = 0x80000000
    }

    private enum TwinhanDeviceSpeed : byte
    {
      Pci = 0xff,             // PCI
      Pcie = 0xfe,            // PCI-express
      UsbLow = 0,             // USB 1.x
      UsbFull = 1,            // USB 1.x
      UsbHigh = 2             // USB 2.0
    }

    private enum TwinhanCiSupport : byte
    {
      Unsupported = 0,
      ApiVersion1,
      ApiVersion2
    }

    [Flags]
    private enum TwinhanSimulatorType : uint
    {
      Physical = 0,   // ie. not a simulator
      VirtualDvbS = 1,
      VirtualDvbT = 2,
      VirtualDvbC = 4,
      VirtualAtsc = 8,
      MceVirtualDvbT = 16
    }

    private enum TwinhanCamType   // CAM_TYPE_ENUM
    {
      Unknown = 0,
      Default = 1,            // Viaccess
      Astoncrypt,
      Conax,
      Cryptoworks
    }

    private enum TwinhanCiState : uint    // CIMessage
    {
      // Old states - CI API v1.
      Empty_Old = 0,          // CI_STATUS_EMPTY_OLD - there is no CAM present
      Cam1Okay_Old,           // CI_STATUS_CAM_OK1_OLD (ME0) - the first slot has a CAM
      Cam2Okay_Old,           // CI_STATUS_CAM_OK2_OLD (ME1) - the second slot has a CAM

      // New states - CI API v2.
      Empty = 10,             // CI_STATUS_EMPTY - there is no CAM present
      CamInserted,            // CI_STATUS_INSERTED - a CAM is present but is still being initialised
      CamOkay,                // CI_STATUS_CAM_OK - a CAM is present, initialised and ready for interaction
      CamUnknown              // CI_STATUS_CAM_UNKNOW
    }

    private enum TwinhanMmiState : uint   // CIMessage
    {
      Idle = 0,               // NON_CI_INFO - there are no new MMI objects available

      // Old states - CI API v1.
      Menu1Okay_Old = 3,      // MMI_STATUS_GET_MENU_OK1_OLD (MMI0) - the CAM in the first slot has an MMI object waiting
      Menu2Okay_Old,          // MMI_STATUS_GET_MENU_OK2_OLD (MMI1) - the CAM in the second slot has an MMI object waiting
      Menu1Close_Old,         // MMI_STATUS_GET_MENU_CLOSE1_OLD (MMI0_ClOSE) - the CAM in the first slot requests that the MMI session be closed
      Menu2Close_Old,         // MMI_STATUS_GET_MENU_CLOSE2_OLD (MMI1_ClOSE) - the CAM in the second slot requests that the MMI session be closed

      // New states - CI API v2.
      SendMenuAnswer = 14,    // MMI_STATUS_ANSWER_SEND 
      MenuOkay,               // MMI_STATUS_GET_MENU_OK - the CAM has an MMI object waiting
      MenuFail,               // MMI_STATUS_GET_MENU_FAIL - the CAM failed to assemble or send and MMI object
      MenuInit,               // MMI_STATUS_GET_MENU_INIT - the CAM is assembling an MMI object
      MenuClose,              // MMI_STATUS_GET_MENU_CLOSE - the CAM requests that the MMI session be closed
      MenuClosed,             // MMI_STATUS_GET_MENU_CLOSED - there is no open MMI session
    }

    private enum TwinhanRawCommandState : uint    // CIMessage
    {
      SendCommand = 30,       // RAW_CMD_STATUS_SEND
      DataOkay                // RAW_CMD_STATUS_GET_DATA_OK
    }

    private enum TwinhanPidFilterMode : byte
    {
      Whitelist = 0,          // PID_FILTER_MODE_PASS - PID filter list contains PIDs that pass through
      Disabled,               // PID_FILTER_MODE_DISABLE - PID filter disabled and all PIDs pass through
      Blacklist               // PID_FILTER_MODE_FILTER - PID filter list contains PIDs that *don't* pass through
    }

    private enum TwinhanToneBurst : byte
    {
      Off = 0,
      ToneBurst,
      DataBurst
    }

    private enum Twinhan22k : byte
    {
      Auto = 0,               // Based on transponder frequency and LNB switch frequency...
      Off,
      On
    }

    private enum TwinhanDiseqcPort : byte
    {
      Null = 0,
      PortA,
      PortB,
      PortC,
      PortD
    }

    #endregion

    #region structs

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DeviceInfo   // DEVICE_INFO
    {
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
      public string Name;                                     // Example: VP1020, VP3020C, VP7045...

      public TwinhanDeviceType Type;                          // Values are bitwise AND'ed together to produce the final device type.
      public TwinhanDeviceSpeed Speed;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
      public byte[] MacAddress;
      public TwinhanCiSupport CiSupport;

      public int TsPacketLength;                              // 188 or 204

      // mm1352000: The following two bytes don't appear to always be set correctly.
      // Maybe these fields are only present for certain tuners or driver versions.
      public byte IsPidFilterPresent;
      public byte IsPidFilterBypassSupported;

      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 190)]
      private byte[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DriverInfo   // DriverInfo
    {
      public byte DriverMajorVersion;                         // BCD encoding eg. 0x32 -> 3.2
      public byte DriverMinorVersion;                         // BCD encoding eg. 0x21 -> 2.1
      public byte FirmwareMajorVersion;                       // BCD encoding eg. 0x10 -> 1.0
      public byte FirmwareMinorVersion;                       // BCD encoding eg. 0x05 -> 0.5  ==> 1.0b05
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 22)]
      public string Date;                                     // Example: "2004-12-20 18:30:00" or  "DEC 20 2004 10:22:10"  with compiler __DATE__ and __TIME__  definition s
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
      public string Company;                                  // Example: "TWINHAN" 
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
      public string HardwareInfo;                             // Example: "PCI DVB CX-878 with MCU series", "PCI ATSC CX-878 with MCU series", "7020/7021 USB-Sat", "7045/7046 USB-Ter" etc.
      public byte CiMmiFlags;                                 // Bit 0 = event mode support (0 => not supported, 1 => supported)
      private byte Reserved;
      public TwinhanSimulatorType SimType;

      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 184)]
      private byte[] Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PidFilterParams    // PID_FILTER_INFO
    {
      public TwinhanPidFilterMode FilterMode;
      public byte MaxPids;                                    // Max number of PIDs supported by the PID filter (HW/FW limit, always <= MAX_PID_FILTER_PIDS).
      private ushort Padding;
      public uint ValidPidMask;                               // A bit mask specifying the current valid PIDs. If the bit is 0 then the PID is ignored. Example: if ValidPidMask = 0x00000005 then there are 2 valid PIDs at indexes 0 and 2.
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_PID_FILTER_PID_COUNT)]
      public ushort[] FilterPids;                             // Filter PID list.
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct LnbParams  // LNB_DATA
    {
      [MarshalAs(UnmanagedType.I1)]
      public bool PowerOn;
      public TwinhanToneBurst ToneBurst;
      private ushort Padding;

      public uint LowBandLof;                               // unit = kHz
      public uint HighBandLof;                              // unit = kHz
      public uint SwitchFrequency;                          // unit = kHz
      public Twinhan22k Tone22k;
      public TwinhanDiseqcPort DiseqcPort;
      private ushort Padding2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DiseqcMessage
    {
      public int MessageLength;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DISEQC_MESSAGE_LENGTH)]
      public byte[] Message;
    }

    // New CI/MMI state info structure - CI API v2.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct CiStateInfo    // THCIState
    {
      public TwinhanCiState CiState;
      public TwinhanMmiState MmiState;
      public uint PmtState;
      public uint EventMessage;                             // Current event status.
      public TwinhanRawCommandState RawCmdState;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
      private uint[] Reserved;
    }

    // Old CI/MMI state info structure - CI API v1.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct CiStateInfoOld   // THCIStateOld
    {
      public TwinhanCiState CiState;
      public TwinhanMmiState MmiState;
    }

    #region MMI data class

    // A private class to help us handle the two MMI data formats cleanly and easily.
    private class MmiData
    {
      public string Title = string.Empty;
      public string SubTitle = string.Empty;
      public string Footer = string.Empty;
      public List<string> Entries = new List<string>();
      public int EntryCount = 0;
      public bool IsEnquiry = false;
      public bool IsBlindAnswer = false;
      public int AnswerLength = 0;
      public string Prompt = string.Empty;
      public int ChoiceIndex = 0;
      public string Answer = string.Empty;
      public int Type = 0;

      public void WriteToBuffer(IntPtr buffer, bool isTerraTecFormat)
      {
        if (isTerraTecFormat)
        {
          TerraTecMmiData mmiData = new TerraTecMmiData();
          mmiData.AnswerLength = AnswerLength;
          mmiData.ChoiceIndex = ChoiceIndex;
          mmiData.Answer = Answer;
          mmiData.Type = Type;
          Marshal.StructureToPtr(mmiData, buffer, true);
        }
        else
        {
          DefaultMmiData mmiData = new DefaultMmiData();
          mmiData.AnswerLength = AnswerLength;
          mmiData.ChoiceIndex = ChoiceIndex;
          mmiData.Answer = Answer;
          mmiData.Type = Type;
          Marshal.StructureToPtr(mmiData, buffer, true);
        }
      }

      public void ReadFromBuffer(IntPtr buffer, bool isTerraTecFormat)
      {
        if (isTerraTecFormat)
        {
          TerraTecMmiData mmiData = (TerraTecMmiData)Marshal.PtrToStructure(buffer, typeof(TerraTecMmiData));
          Title = DvbTextConverter.Convert(mmiData.Title);
          SubTitle = DvbTextConverter.Convert(mmiData.SubTitle);
          Footer = DvbTextConverter.Convert(mmiData.Footer);
          EntryCount = mmiData.EntryCount;
          if (EntryCount > TERRATEC_MAX_CAM_MENU_ENTRIES)
          {
            EntryCount = TERRATEC_MAX_CAM_MENU_ENTRIES;
          }
          foreach (TerraTecMmiMenuEntry entry in mmiData.Entries)
          {
            Entries.Add(DvbTextConverter.Convert(entry.Text));
          }
          IsEnquiry = mmiData.IsEnquiry;
          IsBlindAnswer = mmiData.IsBlindAnswer;
          AnswerLength = mmiData.AnswerLength;
          Prompt = DvbTextConverter.Convert(mmiData.Prompt);
          ChoiceIndex = mmiData.ChoiceIndex;
          Answer = mmiData.Answer;
          Type = mmiData.Type;
        }
        else
        {
          DefaultMmiData mmiData = (DefaultMmiData)Marshal.PtrToStructure(buffer, typeof(DefaultMmiData));
          Title = DvbTextConverter.Convert(mmiData.Title);
          SubTitle = DvbTextConverter.Convert(mmiData.SubTitle);
          Footer = DvbTextConverter.Convert(mmiData.Footer);
          EntryCount = mmiData.EntryCount;
          if (EntryCount > DEFAULT_MAX_CAM_MENU_ENTRIES)
          {
            EntryCount = DEFAULT_MAX_CAM_MENU_ENTRIES;
          }
          foreach (DefaultMmiMenuEntry entry in mmiData.Entries)
          {
            Entries.Add(DvbTextConverter.Convert(entry.Text));
          }
          IsEnquiry = mmiData.IsEnquiry;
          IsBlindAnswer = mmiData.IsBlindAnswer;
          AnswerLength = mmiData.AnswerLength;
          Prompt = DvbTextConverter.Convert(mmiData.Prompt);
          ChoiceIndex = mmiData.ChoiceIndex;
          Answer = mmiData.Answer;
          Type = mmiData.Type;
        }
      }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DefaultMmiMenuEntry
    {
      #pragma warning disable 0649
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 42)]
      public byte[] Text;
      #pragma warning restore 0649
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DefaultMmiData
    {
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
      public byte[] Title;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
      public byte[] SubTitle;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
      public byte[] Footer;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = DEFAULT_MAX_CAM_MENU_ENTRIES)]
      public DefaultMmiMenuEntry[] Entries;
      private ushort Padding1;
      public int EntryCount;

      [MarshalAs(UnmanagedType.Bool)]
      public bool IsEnquiry;

      [MarshalAs(UnmanagedType.Bool)]
      public bool IsBlindAnswer;
      public int AnswerLength;      // enquiry: expected answer length, enquiry answer: actual answer length
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
      public byte[] Prompt;

      public int ChoiceIndex;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
      public string Answer;

      public int Type;              // 1, 2 (menu/list, select entry) or 3 (enquiry, enquiry answer)
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TerraTecMmiMenuEntry
    {
      #pragma warning disable 0649
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
      public byte[] Text;
      #pragma warning restore 0649
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct TerraTecMmiData
    {
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
      public byte[] Title;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
      public byte[] SubTitle;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
      public byte[] Footer;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = TERRATEC_MAX_CAM_MENU_ENTRIES)]
      public TerraTecMmiMenuEntry[] Entries;
      public int EntryCount;

      [MarshalAs(UnmanagedType.Bool)]
      public bool IsEnquiry;

      [MarshalAs(UnmanagedType.Bool)]
      public bool IsBlindAnswer;
      public int AnswerLength;      // enquiry: expected answer length, enquiry answer: actual answer length
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
      public byte[] Prompt;

      public int ChoiceIndex;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
      public string Answer;

      public int Type;              // 1, 2 (menu/list, select entry) or 3 (enquiry, enquiry answer)
    }

    #endregion

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ApplicationInfo    // THAppInfo
    {
      public uint ApplicationType;
      public uint Manufacturer;
      public uint ManufacturerCode;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
      public byte[] RootMenuTitle;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct TuningParams   // TURNER_VALUE
    {
      [FieldOffset(0)]
      public uint Frequency;                                // unit = kHz

      // Note: these two fields are unioned - they are never both required in
      // a single tune request so the bytes are reused.
      [FieldOffset(4)]
      public uint SymbolRate;                               // unit = ks/s
      [FieldOffset(4)]
      public uint Bandwidth;                                // unit = MHz

      [FieldOffset(8)]
      public ModulationType Modulation;
      [FieldOffset(12), MarshalAs(UnmanagedType.I1)]
      public bool LockWaitForResult;
      [FieldOffset(13)]
      private byte Padding1;
      [FieldOffset(14)]
      private ushort Padding2;
    }

    #endregion

    #region Twinhan IO controls

    // Initialise a buffer with a new command, ready to pass to the tuner filter.
    private class TwinhanCommand
    {
      private uint _controlCode;
      private IntPtr _inBuffer;
      private int _inBufferSize;
      private IntPtr _outBuffer;
      private int _outBufferSize;

      public TwinhanCommand(uint controlCode, IntPtr inBuffer, int inBufferSize, IntPtr outBuffer, int outBufferSize)
      {
        _controlCode = controlCode;
        _inBuffer = inBuffer;
        _inBufferSize = inBufferSize;
        _outBuffer = outBuffer;
        _outBufferSize = outBufferSize;
      }

      public int Execute(IKsPropertySet ps, out int returnedByteCount)
      {
        returnedByteCount = 0;
        int hr = (int)HResult.Severity.Error;
        if (ps == null)
        {
          return hr;
        }

        IntPtr instanceBuffer = Marshal.AllocCoTaskMem(INSTANCE_SIZE);
        IntPtr commandBuffer = Marshal.AllocCoTaskMem(COMMAND_SIZE);
        IntPtr returnedByteCountBuffer = Marshal.AllocCoTaskMem(sizeof(int));
        try
        {
          // Clear buffers. This is probably not actually needed, but better
          // to be safe than sorry!
          for (int i = 0; i < INSTANCE_SIZE; i++)
          {
            Marshal.WriteByte(instanceBuffer, i, 0);
          }
          Marshal.WriteInt32(returnedByteCountBuffer, 0);

          // Fill the command buffer.
          byte[] guidAsBytes = COMMAND_GUID.ToByteArray();
          Marshal.Copy(guidAsBytes, 0, commandBuffer, guidAsBytes.Length);
          Marshal.WriteInt32(commandBuffer, 16, (int)_controlCode);
          Marshal.WriteInt32(commandBuffer, 20, _inBuffer.ToInt32());
          Marshal.WriteInt32(commandBuffer, 24, _inBufferSize);
          Marshal.WriteInt32(commandBuffer, 28, _outBuffer.ToInt32());
          Marshal.WriteInt32(commandBuffer, 32, _outBufferSize);
          Marshal.WriteInt32(commandBuffer, 36, returnedByteCountBuffer.ToInt32());

          hr = ps.Set(BDA_EXTENSION_PROPERTY_SET, 0, instanceBuffer, INSTANCE_SIZE, commandBuffer, COMMAND_SIZE);
          if (hr == (int)HResult.Severity.Success)
          {
            returnedByteCount = Marshal.ReadInt32(returnedByteCountBuffer);
          }
        }
        finally
        {
          Marshal.FreeCoTaskMem(instanceBuffer);
          Marshal.FreeCoTaskMem(commandBuffer);
          Marshal.FreeCoTaskMem(returnedByteCountBuffer);
        }
        return hr;
      }
    }

    private const uint THBDA_IO_INDEX = 0xaa00;
    private const uint METHOD_BUFFERED = 0x0000;
    private const uint FILE_ANY_ACCESS = 0x0000;

    // Assemble a control code.
    private static uint CTL_CODE(uint deviceType, uint function, uint method, uint access)
    {
      return ((deviceType) << 16) | ((access) << 14) | ((function) << 2) | (method);
    }

    //*******************************************************************************************************
    //Functionality : Check BDA driver if support IOCTL interface
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CHECK_INTERFACE = CTL_CODE(THBDA_IO_INDEX, 121, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get device info
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct DEVICE_INFO
    //OutBufferSize : sizeof(DEVICE_INFO) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_DEVICE_INFO = CTL_CODE(THBDA_IO_INDEX, 124, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get driver info
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct DriverInfo
    //OutBufferSize : sizeof(DriverInfo) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_DRIVER_INFO = CTL_CODE(THBDA_IO_INDEX, 125, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Reset USB or PCI controller
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_RESET_DEVICE = CTL_CODE(THBDA_IO_INDEX, 120, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #region tuner power

    //*******************************************************************************************************
    //Functionality : Set turner power
    //InBuffer      : Tuner_Power_ON | Tuner_Power_OFF
    //InBufferSize  : 1 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_SET_TUNER_POWER = CTL_CODE(THBDA_IO_INDEX, 100, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get turner power status
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : Tuner_Power_ON | Tuner_Power_OFF
    //OutBufferSize : 1 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_TUNER_POWER = CTL_CODE(THBDA_IO_INDEX, 101, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region tuning

    //*******************************************************************************************************
    //Functionality : Set turner frequency and symbol rate
    //InBuffer      : struct TURNER_VALUE
    //InBufferSize  : sizeof(TURNER_VALUE) bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_LOCK_TUNER = CTL_CODE(THBDA_IO_INDEX, 106, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get turner frequency and symbol rate
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct TURNER_VALUE
    //OutBufferSize : sizeof(TURNER_VALUE) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_TUNER_VALUE = CTL_CODE(THBDA_IO_INDEX, 107, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get signal quality & strength
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct SIGNAL_DATA
    //OutBufferSize : sizeof(SIGNAL_DATA) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_SIGNAL_Q_S = CTL_CODE(THBDA_IO_INDEX, 108, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region DiSEqC and LNB parameters

    //*******************************************************************************************************
    //Functionality : Send DiSEqC command
    //InBuffer      : struct DiSEqC_DATA
    //InBufferSize  : sizeof(DiSEqC_DATA) bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_SET_DiSEqC = CTL_CODE(THBDA_IO_INDEX, 104, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get DiSEqC command
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct DiSEqC_DATA
    //OutBufferSize : sizeof(DiSEqC_DATA) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_DiSEqC = CTL_CODE(THBDA_IO_INDEX, 105, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Set LNB parameters
    //InBuffer      : struct LNB_DATA
    //InBufferSize  : sizeof(LNB_DATA) bytes
    //OutBuffer     : 0
    //OutBufferSize : 0
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_SET_LNB_DATA = CTL_CODE(THBDA_IO_INDEX, 128, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : GET LNB parameters
    //InBuffer      : NULL
    //InBufferSize  : 0
    //OutBuffer     : struct LNB_DATA
    //OutBufferSize : sizeof(LNB_DATA) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_LNB_DATA = CTL_CODE(THBDA_IO_INDEX, 129, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region PID filter

    //*******************************************************************************************************
    //Functionality : Set PID filter mode and Pids to PID filter
    //InBuffer      : struct PID_FILTER_INFO
    //InBufferSize  : sizeof(PID_FILTER_INFO) bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_SET_PID_FILTER_INFO = CTL_CODE(THBDA_IO_INDEX, 113, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get Pids, PLD mode and available max number Pids
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct PID_FILTER_INFO
    //OutBufferSize : sizeof(PID_FILTER_INFO) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_PID_FILTER_INFO = CTL_CODE(THBDA_IO_INDEX, 114, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region CI handling

    #region MMI

    //*******************************************************************************************************
    //Functionality : Get CI state
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct THCIState
    //OutBufferSize : sizeof(THCIState) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_GET_STATE = CTL_CODE(THBDA_IO_INDEX, 200, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get APP info.
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct THAppInfo
    //OutBufferSize : sizeof(THAppInfo) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_GET_APP_INFO = CTL_CODE(THBDA_IO_INDEX, 201, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Init MMI
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_INIT_MMI = CTL_CODE(THBDA_IO_INDEX, 202, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get MMI
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct THMMIInfo
    //OutBufferSize : sizeof(THMMIInfo) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_GET_MMI = CTL_CODE(THBDA_IO_INDEX, 203, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Answer
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct THMMIInfo
    //OutBufferSize : sizeof(THMMIInfo) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_ANSWER = CTL_CODE(THBDA_IO_INDEX, 204, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Close MMI
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_CLOSE_MMI = CTL_CODE(THBDA_IO_INDEX, 205, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region PMT

    //*******************************************************************************************************
    //Functionality : Send PMT
    //InBuffer      : PMT data buffer
    //InBufferSize  : PMT data buffer size bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //Comment       : CA_PMT data format
    //1: ca pmt list management;(8 bit);
    //2: program number (16 bit);
    //3: reserved (2 bit);
    //4: version number (5 bit);
    //5: current next indicator (I bit);
    //6: reserved (4 bit);
    //7: program information length (12 bit);
    //8: if (7!=0)
    //	    ca pmt command id (program level); (8 bit);
    //	    ca descriptor at program level; (n * 8bit);
    //9:  stream type (8 bit);
    //10: reserved (3 bit);
    //11: elementary stream PID (bit 13);
    //12: reserved (4 bit);
    //13: ES information length (12 bit);
    //14: if (ES information length ! =0)
    //       ca pmt command id ( elementary stream level) (8 bit);
    //	     ca descriptor at elementary stream level; ( n * 8bit)
    //* more detail, please refer to EN 50221 (8,4,3,4 CA_PMT); 
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_SEND_PMT = CTL_CODE(THBDA_IO_INDEX, 206, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get PMT Reply
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : PMT Reply Buffer
    //OutBufferSize : sizeof(PMT Reply Buffer) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_GET_PMT_REPLY = CTL_CODE(THBDA_IO_INDEX, 210, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #endregion

    #region not used/implemented

    #region ring buffer TS data capture

    //*******************************************************************************************************
    //Functionality : START TS capture (from Tuner to driver Ring buffer)
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_START_CAPTURE = CTL_CODE(THBDA_IO_INDEX, 109, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Stop TS capture
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_STOP_CAPTURE = CTL_CODE(THBDA_IO_INDEX, 110, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get Driver ring buffer status
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct RING_BUF_STATUS 
    //OutBufferSize : sizeof(RING_BUF_STATUS) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_RINGBUFFER_STATUS = CTL_CODE(THBDA_IO_INDEX, 111, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get TS from driver's ring buffer to local  buffer 
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct CAPTURE_DATA
    //OutBufferSize : sizeof(CAPTURE_DATA) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_CAPTURE_DATA = CTL_CODE(THBDA_IO_INDEX, 112, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region remote control

    //*******************************************************************************************************
    //Functionality : Start RC(Remote Controller receiving) thread
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_START_REMOTE_CONTROL = CTL_CODE(THBDA_IO_INDEX, 115, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Stop RC thread, and remove all RC event
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_STOP_REMOTE_CONTROL = CTL_CODE(THBDA_IO_INDEX, 116, METHOD_BUFFERED, FILE_ANY_ACCESS);


    //*******************************************************************************************************
    //Functionality : Add RC_Event to driver
    //InBuffer      : REMOTE_EVENT
    //InBufferSize  : sizeof(REMOTE_EVENT) bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_ADD_RC_EVENT = CTL_CODE(THBDA_IO_INDEX, 117, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Remove RC_Event 
    //InBuffer      : REMOTE_EVENT
    //InBufferSize  : sizeof(REMOTE_EVENT) bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_REMOVE_RC_EVENT = CTL_CODE(THBDA_IO_INDEX, 118, METHOD_BUFFERED, FILE_ANY_ACCESS);


    //*******************************************************************************************************
    //Functionality : Get Remote Controller key
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : BYTE
    //OutBufferSize : 1 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_REMOTE_CONTROL_VALUE = CTL_CODE(THBDA_IO_INDEX, 119, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Set Remote control,HID function enable or disable
    //InBuffer      : 1 0 for OFF,others for ON.
    //InBufferSize  : 1 bytes
    //OutBuffer     : 0 registers value
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_HID_RC_ENABLE = CTL_CODE(THBDA_IO_INDEX, 152, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region registry parameters

    //*******************************************************************************************************
    //Functionality : Set Twinhan BDA driver configuration
    //InBuffer      : struct THBDAREGPARAMS
    //InBufferSize  : sizeof(THBDAREGPARAMS) bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_SET_REG_PARAMS = CTL_CODE(THBDA_IO_INDEX, 122, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get Twinhan BDA driver configuration
    //InBuffer      : NULL
    //InBufferSize  : 0 bytes
    //OutBuffer     : struct THBDAREGPARAMS
    //OutBufferSize : struct THBDAREGPARAMS
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_REG_PARAMS = CTL_CODE(THBDA_IO_INDEX, 123, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region EEPROM access

    //*******************************************************************************************************
    //Functionality : Write EEPROM value
    //InBuffer      : struct EE_IO_DATA
    //InBufferSize  : sizeof(EE_IO_DATA) bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_SET_EE_VAL = CTL_CODE(THBDA_IO_INDEX, 126, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Read EEPROM value      
    //InBuffer      : struct EE_IO_DATA
    //InBufferSize  : sizeof(EE_IO_DATA) bytes
    //OutBuffer     : struct EE_IO_DATA
    //OutBufferSize : sizeof(EE_IO_DATA) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_EE_VAL = CTL_CODE(THBDA_IO_INDEX, 127, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region virtual DVB-T tuner control

    //*******************************************************************************************************
    //Functionality : Enable virtual DVBT interface for DVB-S card
    //InBuffer      : 1 0 for OFF,others for ON.
    //InBufferSize  : 1 bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_ENABLE_VIRTUAL_DVBT = CTL_CODE(THBDA_IO_INDEX, 300, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Reset (Clear) DVB-S Transponder mapping table entry for virtual DVB-T interface
    //InBuffer      : NULL
    //InBufferSize  : 0
    //OutBuffer     : NULL
    //OutBufferSize : 0
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_RESET_T2S_MAPPING = CTL_CODE(THBDA_IO_INDEX, 301, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Set DVB-S Transponder mapping table entry for virtual DVB-T interface
    //InBuffer      : struct DVB-T2S_MAPPING_ENTRY
    //InBufferSize  : sizeof(struct DVB-T2S_MAPPING_ENTRY) bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_SET_T2S_MAPPING = CTL_CODE(THBDA_IO_INDEX, 302, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : GET DVB-S Transponder mapping table entry
    //InBuffer      : &(Table_Index)
    //InBufferSize  : sizeof(ULONG)
    //OutBuffer     : struct DVB-T2S_MAPPING_ENTRY
    //OutBufferSize : sizeof(struct DVB-T2S_MAPPING_ENTRY) bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_GET_T2S_MAPPING = CTL_CODE(THBDA_IO_INDEX, 303, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region tuner firmware download

    //*******************************************************************************************************
    //Functionality : Download tuner firmware, 704C
    //InBuffer      : 1 byte buffer,  0:Downlaod analog TV firmware, 1:download DVB-T firmware
    //InBufferSize  : 1:byte
    //OutBuffer     :1 byte buffer,  0-99: download percentage, 100:download complete, 255:Fail 
    //OutBufferSize : 1 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_DOWNLOAD_TUNER_FIRMWARE = CTL_CODE(THBDA_IO_INDEX, 400, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get tuner firmware download progress
    //InBuffer      : NULL
    //InBufferSize  : 0:byte
    //OutBuffer     :1 byte buffer,  0-99: download percentage, 100:download complete, 255:Fail 
    //OutBufferSize : 1 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_DOWNLOAD_TUNER_FIRMWARE_STAUS = CTL_CODE(THBDA_IO_INDEX, 401, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region CI event handling

    //*******************************************************************************************************
    //Functionality : Create CI event
    //InBuffer      : hEventHandle (The event handle that is created by AP)
    //InBufferSize  : sizeof(HANDLE)
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_EVENT_CREATE = CTL_CODE(THBDA_IO_INDEX, 208, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Close CI event
    //InBuffer      : hEventHandle (The event handle that is sended by create CI event)
    //InBufferSize  : sizeof(HANDLE)
    //OutBuffer     : NULL
    //OutBufferSize : 0 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_EVENT_CLOSE = CTL_CODE(THBDA_IO_INDEX, 209, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #region CI raw command messaging

    //*******************************************************************************************************
    //Functionality : Send CI raw command
    //InBuffer      : RAW_CMD_INFO
    //InBufferSize  : sizeof(RAW_CMD_INFO) bytes
    //OutBuffer     : NULL
    //OutBufferSize : 0
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_SEND_RAW_CMD = CTL_CODE(THBDA_IO_INDEX, 211, METHOD_BUFFERED, FILE_ANY_ACCESS);

    //*******************************************************************************************************
    //Functionality : Get CI raw command data
    //InBuffer      : NULL
    //InBufferSize  : 0
    //OutBuffer     : Raw command data buffer
    //OutBufferSize : Max 1024 bytes
    //*******************************************************************************************************
    private readonly uint THBDA_IOCTL_CI_GET_RAW_CMD_DATA = CTL_CODE(THBDA_IO_INDEX, 212, METHOD_BUFFERED, FILE_ANY_ACCESS);

    #endregion

    #endregion

    #endregion

    #region constants

    // GUID_THBDA_TUNER
    private static readonly Guid BDA_EXTENSION_PROPERTY_SET = new Guid(0xe5644cc4, 0x17a1, 0x4eed, 0xbd, 0x90, 0x74, 0xfd, 0xa1, 0xd6, 0x54, 0x23);
    // GUID_THBDA_CMD
    private static readonly Guid COMMAND_GUID = new Guid(0x255e0082, 0x2017, 0x4b03, 0x90, 0xf8, 0x85, 0x6a, 0x62, 0xcb, 0x3d, 0x67);

    private const int INSTANCE_SIZE = 32;   // The size of a property instance (KSP_NODE) parameter.
    private const int COMMAND_SIZE = 40;

    private static readonly int DEVICE_INFO_SIZE = Marshal.SizeOf(typeof(DeviceInfo));            // 240
    private static readonly int DRIVER_INFO_SIZE = Marshal.SizeOf(typeof(DriverInfo));            // 256
    private static readonly int PID_FILTER_PARAMS_SIZE = Marshal.SizeOf(typeof(PidFilterParams)); // 72
    private const int MAX_PID_FILTER_PID_COUNT = 32;
    private static readonly int LNB_PARAMS_SIZE = Marshal.SizeOf(typeof(LnbParams));              // 20
    private static readonly int DISEQC_MESSAGE_SIZE = Marshal.SizeOf(typeof(DiseqcMessage));      // 16
    private const int MAX_DISEQC_MESSAGE_LENGTH = 12;
    private static readonly int CI_STATE_INFO_SIZE = Marshal.SizeOf(typeof(CiStateInfo));         // 48
    private static readonly int OLD_CI_STATE_INFO_SIZE = Marshal.SizeOf(typeof(CiStateInfoOld));  // 8
    private static readonly int APPLICATION_INFO_SIZE = Marshal.SizeOf(typeof(ApplicationInfo));  // 76
    private static readonly int TUNING_PARAMS_SIZE = Marshal.SizeOf(typeof(TuningParams));        // 16

    private static readonly int GENERAL_BUFFER_SIZE = new int[]
      {
        DEVICE_INFO_SIZE, DISEQC_MESSAGE_SIZE, DRIVER_INFO_SIZE, LNB_PARAMS_SIZE,
        PID_FILTER_PARAMS_SIZE, TUNING_PARAMS_SIZE
      }.Max();
    private const int MMI_HANDLER_THREAD_WAIT_TIME = 500;     // unit = ms

    // TerraTec have entended the length and number of
    // possible CAM menu entries in the MMI data struct
    // returned by their drivers.
    private const int DEFAULT_MMI_DATA_SIZE = 1684;
    private const int DEFAULT_MAX_CAM_MENU_ENTRIES = 9;

    private const int TERRATEC_MMI_DATA_SIZE = 33944;
    private const int TERRATEC_MAX_CAM_MENU_ENTRIES = 255;

    #endregion

    #region variables

    private bool _isTwinhan = false;
    private bool _isTerraTec = false;
    private bool _isCaInterfaceOpen = false;
    #pragma warning disable 0414
    private bool _isCamPresent = false;
    #pragma warning restore 0414
    private bool _isCamReady = false;

    // PID filter variables.
    private TwinhanDeviceSpeed _connection = TwinhanDeviceSpeed.Pcie;
    private bool _isPidFilterSupported = false;
    private bool _isPidFilterBypassSupported = true;
    private int _maxPidFilterPidCount = MAX_PID_FILTER_PID_COUNT;
    private HashSet<ushort> _pidFilterPids = new HashSet<ushort>();

    // Functions that are called from both the main TV service threads
    // as well as the MMI handler thread use their own local buffer to
    // avoid buffer data corruption. Otherwise functions called exclusively
    // by the MMI handler thread use the MMI buffer and other functions
    // use the general buffer.
    private IntPtr _generalBuffer = IntPtr.Zero;
    private IntPtr _mmiBuffer = IntPtr.Zero;

    private IKsPropertySet _propertySet = null;
    private CardType _tunerType = CardType.Unknown;
    private string _tunerFilterName = string.Empty;

    private TwinhanCiSupport _ciApiVersion = TwinhanCiSupport.Unsupported;
    private int _mmiDataSize = DEFAULT_MMI_DATA_SIZE;

    private Thread _mmiHandlerThread = null;
    private AutoResetEvent _mmiHandlerThreadStopEvent = null;
    private object _mmiLock = new object();
    private IConditionalAccessMenuCallBacks _caMenuCallBacks = null;
    private object _caMenuCallBackLock = new object();

    #endregion

    /// <summary>
    /// Get the conditional access interface status.
    /// </summary>
    /// <param name="ciState">State of the CI slot.</param>
    /// <param name="mmiState">State of the MMI.</param>
    /// <returns>an HRESULT indicating whether the CI status was successfully retrieved</returns>
    private int GetCiStatus(out TwinhanCiState ciState, out TwinhanMmiState mmiState)
    {
      ciState = TwinhanCiState.Empty;
      mmiState = TwinhanMmiState.Idle;
      int bufferSize = CI_STATE_INFO_SIZE;
      if (_ciApiVersion == TwinhanCiSupport.Unsupported)
      {
        return 1; // Fail...
      }
      if (_ciApiVersion == TwinhanCiSupport.ApiVersion1)
      {
        ciState = TwinhanCiState.Empty_Old;
        bufferSize = OLD_CI_STATE_INFO_SIZE;
      }

      // Use local buffers here because this function is called from the MMI polling thread as well as
      // indirectly from the main TV service thread.
      IntPtr responseBuffer = Marshal.AllocCoTaskMem(bufferSize);
      for (int i = 0; i < bufferSize; i++)
      {
        Marshal.WriteByte(responseBuffer, i, 0);
      }
      try
      {
        TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_CI_GET_STATE, IntPtr.Zero, 0, responseBuffer, bufferSize);
        int returnedByteCount;
        int hr = command.Execute(_propertySet, out returnedByteCount);
        if (hr == (int)HResult.Severity.Success)
        {
          ciState = (TwinhanCiState)Marshal.ReadInt32(responseBuffer, 0);
          mmiState = (TwinhanMmiState)Marshal.ReadInt32(responseBuffer, 4);
          //Dump.DumpBinary(responseBuffer, returnedByteCount);
        }
        return hr;
      }
      finally
      {
        Marshal.FreeCoTaskMem(responseBuffer);
        responseBuffer = IntPtr.Zero;
      }
    }

    #region hardware/software information

    /// <summary>
    /// Attempt to read the device information from the tuner.
    /// </summary>
    private void ReadDeviceInfo()
    {
      this.LogDebug("Twinhan: read device information");
      for (int i = 0; i < DEVICE_INFO_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, 0);
      }
      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_GET_DEVICE_INFO, IntPtr.Zero, 0, _generalBuffer, DEVICE_INFO_SIZE);
      int returnedByteCount;
      int hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogWarn("Twinhan: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return;
      }

      //this.LogDebug("Twinhan: number of DeviceInfo bytes returned is {0}", returnedByteCount);
      //Dump.DumpBinary(_generalBuffer, returnedByteCount);
      DeviceInfo deviceInfo = (DeviceInfo)Marshal.PtrToStructure(_generalBuffer, typeof(DeviceInfo));
      this.LogDebug("  name                        = {0}", deviceInfo.Name);
      this.LogDebug("  supported modes             = {0}", deviceInfo.Type.ToString());
      this.LogDebug("  speed/interface             = {0}", deviceInfo.Speed);
      this.LogDebug("  MAC address                 = {0}", BitConverter.ToString(deviceInfo.MacAddress));
      this.LogDebug("  CI support                  = {0}", deviceInfo.CiSupport);
      this.LogDebug("  TS packet length            = {0}", deviceInfo.TsPacketLength);
      // Handle the PID filter paramter bytes carefully - not all drivers actually return
      // meaningful values for them.
      if (deviceInfo.IsPidFilterPresent == 0x01)
      {
        _isPidFilterSupported = true;
        if (deviceInfo.IsPidFilterBypassSupported == 0)
        {
          _isPidFilterBypassSupported = false;
        }
      }
      this.LogDebug("  PID filter supported        = {0}", _isPidFilterSupported);
      this.LogDebug("  PID filter bypass supported = {0}", _isPidFilterBypassSupported);

      _connection = deviceInfo.Speed;
      _ciApiVersion = deviceInfo.CiSupport;
    }

    /// <summary>
    /// Attempt to read the PID filter implementation details from the tuner.
    /// </summary>
    private void ReadPidFilterInfo()
    {
      this.LogDebug("Twinhan: read PID filter information");
      for (int i = 0; i < PID_FILTER_PARAMS_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, 0);
      }
      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_GET_PID_FILTER_INFO, IntPtr.Zero, 0, _generalBuffer, PID_FILTER_PARAMS_SIZE);
      int returnedByteCount;
      int hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogWarn("Twinhan: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return;
      }

      //this.LogDebug("Twinhan: number of PidFilterParams bytes returned is {0}", returnedByteCount);
      //Dump.DumpBinary(_generalBuffer, returnedByteCount);
      PidFilterParams pidFilterInfo = (PidFilterParams)Marshal.PtrToStructure(_generalBuffer, typeof(PidFilterParams));
      this.LogDebug("  current mode                = {0}", pidFilterInfo.FilterMode);
      this.LogDebug("  maximum PIDs                = {0}", pidFilterInfo.MaxPids);
      _maxPidFilterPidCount = pidFilterInfo.MaxPids;
    }

    /// <summary>
    /// Attempt to read the driver information from the tuner.
    /// </summary>
    private void ReadDriverInfo()
    {
      this.LogDebug("Twinhan: read driver information");
      for (int i = 0; i < DRIVER_INFO_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, 0);
      }
      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_GET_DRIVER_INFO, IntPtr.Zero, 0, _generalBuffer, DRIVER_INFO_SIZE);
      int returnedByteCount;
      int hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogWarn("Twinhan: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return;
      }

      //this.LogDebug("Twinhan: number of DriverInfo bytes returned is {0}", returnedByteCount);
      //Dump.DumpBinary(_generalBuffer, returnedByteCount);
      DriverInfo driverInfo = (DriverInfo)Marshal.PtrToStructure(_generalBuffer, typeof(DriverInfo));
      char[] majorVersion = string.Format("{0:x2}", driverInfo.DriverMajorVersion).ToCharArray();
      char[] minorVersion = string.Format("{0:x2}", driverInfo.DriverMinorVersion).ToCharArray();
      this.LogDebug("  driver version              = {0}.{1}.{2}.{3}", majorVersion[0], majorVersion[1], minorVersion[0], minorVersion[1]);
      majorVersion = string.Format("{0:x2}", driverInfo.FirmwareMajorVersion).ToCharArray();
      minorVersion = string.Format("{0:x2}", driverInfo.FirmwareMinorVersion).ToCharArray();
      this.LogDebug("  firmware version            = {0}.{1}.{2}.{3}", majorVersion[0], majorVersion[1], minorVersion[0], minorVersion[1]);
      this.LogDebug("  date                        = {0}", driverInfo.Date);
      this.LogDebug("  company                     = {0}", driverInfo.Company);
      this.LogDebug("  hardware info               = {0}", driverInfo.HardwareInfo);
      this.LogDebug("  CI event mode supported     = {0}", (driverInfo.CiMmiFlags & 0x01) != 0);
      this.LogDebug("  simulator mode              = {0}", driverInfo.SimType);
    }

    #endregion

    #region MMI handler thread

    /// <summary>
    /// Start a thread to receive MMI messages from the CAM.
    /// </summary>
    private void StartMmiHandlerThread()
    {
      // Don't start a thread if the interface has not been opened.
      if (!_isCaInterfaceOpen)
      {
        return;
      }

      lock (_mmiLock)
      {
        // Kill the existing thread if it is in "zombie" state.
        if (_mmiHandlerThread != null && !_mmiHandlerThread.IsAlive)
        {
          StopMmiHandlerThread();
        }

        if (_mmiHandlerThread == null)
        {
          this.LogDebug("Twinhan: starting new MMI handler thread");
          _mmiHandlerThreadStopEvent = new AutoResetEvent(false);
          _mmiHandlerThread = new Thread(new ThreadStart(MmiHandler));
          _mmiHandlerThread.Name = "Twinhan MMI handler";
          _mmiHandlerThread.IsBackground = true;
          _mmiHandlerThread.Priority = ThreadPriority.Lowest;
          _mmiHandlerThread.Start();
        }
      }
    }

    /// <summary>
    /// Stop the thread that receives MMI messages from the CAM.
    /// </summary>
    private void StopMmiHandlerThread()
    {
      lock (_mmiLock)
      {
        if (_mmiHandlerThread != null)
        {
          if (!_mmiHandlerThread.IsAlive)
          {
            this.LogWarn("Twinhan: aborting old MMI handler thread");
            _mmiHandlerThread.Abort();
          }
          else
          {
            _mmiHandlerThreadStopEvent.Set();
            if (!_mmiHandlerThread.Join(MMI_HANDLER_THREAD_WAIT_TIME * 2))
            {
              this.LogWarn("Twinhan: failed to join MMI handler thread, aborting thread");
              _mmiHandlerThread.Abort();
            }
          }
          _mmiHandlerThread = null;
          if (_mmiHandlerThreadStopEvent != null)
          {
            _mmiHandlerThreadStopEvent.Close();
            _mmiHandlerThreadStopEvent = null;
          }
        }
      }
    }

    /// <summary>
    /// Thread function for receiving MMI messages from the CAM.
    /// </summary>
    private void MmiHandler()
    {
      this.LogDebug("Twinhan: MMI handler thread start polling");
      TwinhanCiState ciState = TwinhanCiState.Empty_Old;
      TwinhanMmiState mmiState = TwinhanMmiState.Idle;
      TwinhanCiState prevCiState = TwinhanCiState.Empty_Old;
      TwinhanMmiState prevMmiState = TwinhanMmiState.Idle;
      try
      {
        while (!_mmiHandlerThreadStopEvent.WaitOne(MMI_HANDLER_THREAD_WAIT_TIME))
        {
          int hr = GetCiStatus(out ciState, out mmiState);
          if (hr != (int)HResult.Severity.Success)
          {
            this.LogError("Twinhan: failed to get CI status, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
            continue;
          }

          // Handle CI slot state changes.
          if (ciState != prevCiState)
          {
            this.LogInfo("Twinhan: CI state change, old state = {0}, new state = {1}", prevCiState, ciState);
            prevCiState = ciState;
            if (ciState == TwinhanCiState.CamInserted ||
              ciState == TwinhanCiState.CamUnknown)
            {
              _isCamPresent = true;
              _isCamReady = false;
            }
            else if (ciState == TwinhanCiState.CamOkay ||
              ciState == TwinhanCiState.Cam1Okay_Old ||
              ciState == TwinhanCiState.Cam2Okay_Old)
            {
              _isCamPresent = true;
              _isCamReady = true;
            }
            else
            {
              _isCamPresent = false;
              _isCamReady = false;
            }
          }

          // Log MMI state changes.
          if (mmiState != prevMmiState)
          {
            this.LogInfo("Twinhan: MMI state change, old state = {0}, new state = {1}", prevMmiState, mmiState);
          }

          // If there is no CAM present or the CAM is not ready for interaction
          // then don't attempt to communicate with the CI.
          if (!_isCamReady)
          {
            continue;
          }

          if (
            // CI API v1
            mmiState == TwinhanMmiState.Menu1Okay_Old ||
            mmiState == TwinhanMmiState.Menu2Okay_Old ||
            // CI API v2
            (prevMmiState != mmiState && mmiState == TwinhanMmiState.MenuOkay)
          )
          {
            MmiData mmi;
            if (ReadMmi(out mmi))
            {
              if (mmi.IsEnquiry)
              {
                this.LogDebug("Twinhan: enquiry");
                this.LogDebug("  blind     = {0}", mmi.IsBlindAnswer);
                this.LogDebug("  length    = {0}", mmi.AnswerLength);
                this.LogDebug("  prompt    = {0}", mmi.Prompt);
                this.LogDebug("  type      = {0}", mmi.Type);
                lock (_caMenuCallBackLock)
                {
                  if (_caMenuCallBacks != null)
                  {
                    _caMenuCallBacks.OnCiRequest(mmi.IsBlindAnswer, (uint)mmi.AnswerLength, mmi.Prompt);
                  }
                  else
                  {
                    this.LogDebug("Twinhan: menu call backs are not set");
                  }
                }
              }
              else
              {
                this.LogDebug("Twinhan: menu");

                lock (_caMenuCallBackLock)
                {
                  if (_caMenuCallBacks == null)
                  {
                    this.LogDebug("Twinhan: menu call backs are not set");
                  }

                  this.LogDebug("  title     = {0}", mmi.Title);
                  this.LogDebug("  sub-title = {0}", mmi.SubTitle);
                  this.LogDebug("  footer    = {0}", mmi.Footer);
                  this.LogDebug("  # entries = {0}", mmi.EntryCount);
                  if (_caMenuCallBacks != null)
                  {
                    _caMenuCallBacks.OnCiMenu(mmi.Title, mmi.SubTitle, mmi.Footer, mmi.EntryCount);
                  }
                  for (int i = 0; i < mmi.EntryCount; i++)
                  {
                    this.LogDebug("    {0, -7} = {1}", i + 1, mmi.Entries[i]);
                    if (_caMenuCallBacks != null)
                    {
                      _caMenuCallBacks.OnCiMenuChoice(i, mmi.Entries[i]);
                    }
                  }
                }
                this.LogDebug("  type      = {0}", mmi.Type);
              }
            }
            else
            {
              CloseMenu();
            }
          }
          else if (
            // CI API v1
            mmiState == TwinhanMmiState.Menu1Close_Old ||
            mmiState == TwinhanMmiState.Menu2Close_Old ||
            // CI API v2
            mmiState == TwinhanMmiState.MenuClose)
          {
            this.LogDebug("Twinhan: menu close request");
            lock (_caMenuCallBackLock)
            {
              if (_caMenuCallBacks != null)
              {
                _caMenuCallBacks.OnCiCloseDisplay(0);
              }
              else
              {
                this.LogDebug("Twinhan: menu call backs are not set");
              }
            }
            CloseMenu();
          }
          prevMmiState = mmiState;
        }
      }
      catch (ThreadAbortException)
      {
      }
      catch (Exception ex)
      {
        this.LogError(ex, "Twinhan: MMI handler thread exception");
      }
    }

    /// <summary>
    /// Build and send an MMI response from the user to the CAM.
    /// </summary>
    /// <param name="mmi">The response object from the user.</param>
    /// <returns><c>true</c> if the message was successfully sent, otherwise <c>false</c></returns>
    private bool SendMmi(MmiData mmi)
    {
      this.LogDebug("Twinhan: send MMI message");

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }
      StartMmiHandlerThread();
      if (!_isCamReady)
      {
        this.LogError("Twinhan: the CAM is not ready");
        return false;
      }

      int hr;
      lock (_mmiLock)
      {
        mmi.WriteToBuffer(_mmiBuffer, _isTerraTec);
        //Dump.DumpBinary(_mmiBuffer, _mmiDataSize);
        TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_CI_ANSWER, IntPtr.Zero, 0, _mmiBuffer, _mmiDataSize);
        int returnedByteCount;
        hr = command.Execute(_propertySet, out returnedByteCount);
      }
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
        return true;
      }

      this.LogError("Twinhan: failed to send MMI message, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Read and parse an MMI response from the CAM into an MmiData object.
    /// </summary>
    /// <param name="mmi">The parsed response from the CAM.</param>
    /// <returns><c>true</c> if the response from the CAM was successfully parsed, otherwise <c>false</c></returns>
    private bool ReadMmi(out MmiData mmi)
    {
      this.LogDebug("Twinhan: read MMI response");
      mmi = new MmiData();
      int hr;
      lock (_mmiLock)
      {
        for (int i = 0; i < _mmiDataSize; i++)
        {
          Marshal.WriteByte(_mmiBuffer, i, 0);
        }
        TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_CI_GET_MMI, IntPtr.Zero, 0, _mmiBuffer, _mmiDataSize);
        int returnedByteCount;
        hr = command.Execute(_propertySet, out returnedByteCount);
        if (hr == (int)HResult.Severity.Success && returnedByteCount == _mmiDataSize)
        {
          this.LogDebug("Twinhan: result = success");
          //Dump.DumpBinary(_mmiBuffer, returnedByteCount);
          mmi.ReadFromBuffer(_mmiBuffer, _isTerraTec);
          return true;
        }
      }

      this.LogError("Twinhan: failed to read MMI response, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region ICustomDevice members

    /// <summary>
    /// A human-readable name for the extension. This could be a manufacturer or reseller name, or
    /// even a model name and/or number.
    /// </summary>
    public override string Name
    {
      get
      {
        if (_isTerraTec)
        {
          return "TerraTec";
        }
        return "Twinhan";
      }
    }

    /// <summary>
    /// Attempt to initialise the extension-specific interfaces used by the class. If
    /// initialisation fails, the <see ref="ICustomDevice"/> instance should be disposed
    /// immediately.
    /// </summary>
    /// <param name="tunerExternalIdentifier">The external identifier for the tuner.</param>
    /// <param name="tunerType">The tuner type (eg. DVB-S, DVB-T... etc.).</param>
    /// <param name="context">Context required to initialise the interface.</param>
    /// <returns><c>true</c> if the interfaces are successfully initialised, otherwise <c>false</c></returns>
    public override bool Initialise(string tunerExternalIdentifier, CardType tunerType, object context)
    {
      this.LogDebug("Twinhan: initialising");

      IBaseFilter tunerFilter = context as IBaseFilter;
      if (tunerFilter == null)
      {
        this.LogDebug("Twinhan: tuner filter is null");
        return false;
      }
      if (_isTwinhan)
      {
        this.LogWarn("Twinhan: extension already initialised");
        return true;
      }

      IPin pin = DsFindPin.ByDirection(tunerFilter, PinDirection.Input, 0);
      _propertySet = pin as IKsPropertySet;
      if (_propertySet == null)
      {
        this.LogDebug("Twinhan: pin is not a property set");
        Release.ComObject("Twinhan tuner filter input pin", ref pin);
        return false;
      }

      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_CHECK_INTERFACE, IntPtr.Zero, 0, IntPtr.Zero, 0);
      int returnedByteCount;
      int hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: property set not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      this.LogInfo("Twinhan: extension supported");
      _isTwinhan = true;
      _tunerType = tunerType;

      FilterInfo tunerFilterInfo;
      hr = tunerFilter.QueryFilterInfo(out tunerFilterInfo);
      _tunerFilterName = tunerFilterInfo.achName;
      Release.FilterInfo(ref tunerFilterInfo);

      if (hr != (int)HResult.Severity.Success || _tunerFilterName == null)
      {
        this.LogError("Twinhan: failed to get the tuner filter name, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        _tunerFilterName = string.Empty;
      }
      else
      {
        if (_tunerFilterName.ToLowerInvariant().Contains("terratec") || _tunerFilterName.ToLowerInvariant().Contains("cinergy"))
        {
          this.LogDebug("Twinhan: this tuner is using a TerraTec driver");
          _isTerraTec = true;
        }
      }
      _generalBuffer = Marshal.AllocCoTaskMem(GENERAL_BUFFER_SIZE);

      ReadDeviceInfo();
      if (_isPidFilterSupported)
      {
        ReadPidFilterInfo();
      }
      ReadDriverInfo();
      return true;
    }

    #region device state change call backs

    /// <summary>
    /// This call back is invoked when the tuner has been successfully loaded.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="action">The action to take, if any.</param>
    public override void OnLoaded(ITVCard tuner, out TunerAction action)
    {
      // The TerraTec H7 and TechniSat CableStar Combo HD CI require *very* careful graph management. If the graph
      // is left idle for any length of time (a few minutes) they will fail to (re)start streaming. In addition,
      // they require the graph to be restarted if tuning fails, otherwise they don't seem to behave properly in
      // subsequent tune requests.
      // We start the graph immediately to prevent the graph from being left idle.
      action = TunerAction.Start;
    }

    /// <summary>
    /// This call back is invoked before a tune request is assembled.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="currentChannel">The channel that the tuner is currently tuned to..</param>
    /// <param name="channel">The channel that the tuner will been tuned to.</param>
    /// <param name="action">The action to take, if any.</param>
    public override void OnBeforeTune(ITVCard tuner, IChannel currentChannel, ref IChannel channel, out TunerAction action)
    {
      this.LogDebug("Twinhan: on before tune call back");
      action = TunerAction.Default;

      if (!_isTwinhan)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return;
      }

      // We only need to tweak the modulation for DVB-S2 channels.
      DVBSChannel ch = channel as DVBSChannel;
      if (ch == null)
      {
        return;
      }

      // Modulation for DVB-S2 QPSK should be ModQpsk... which is what we already use to indicate DVB-S2 QPSK
      // in our tuning parameter files.
      if (ch.ModulationType == ModulationType.Mod8Psk)
      {
        ch.ModulationType = ModulationType.Mod8Vsb;
      }
      else if (ch.ModulationType == ModulationType.Mod16Apsk)
      {
        ch.ModulationType = ModulationType.Mod16Vsb;
      }
      else if (ch.ModulationType == ModulationType.Mod32Apsk)
      {
        ch.ModulationType = ModulationType.ModOqpsk;
      }
      this.LogDebug("  modulation = {0}", ch.ModulationType);
    }

    /// <summary>
    /// This call back is invoked after a tune request is submitted, when the tuner is started but
    /// before signal lock is checked.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="currentChannel">The channel that the tuner is tuned to.</param>
    public override void OnStarted(ITVCard tuner, IChannel currentChannel)
    {
      // Ensure the MMI handler thread is always running when the graph is running.
      StartMmiHandlerThread();
    }

    /// <summary>
    /// This call back is invoked before the tuner is stopped.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="action">As an input, the action that TV Server wants to take; as an output, the action to take.</param>
    public override void OnStop(ITVCard tuner, ref TunerAction action)
    {
      // The TerraTec H7 and TechniSat CableStar Combo HD CI require *very* careful graph management. If the graph
      // is left idle for any length of time (a few minutes) they will fail to (re)start streaming. In addition,
      // they require the graph to be restarted if tuning fails, otherwise they don't seem to behave properly in
      // subsequent tune requests.
      if (action == TunerAction.Stop || action == TunerAction.Pause)
      {
        action = TunerAction.Restart;
      }
    }

    #endregion

    #endregion

    #region IPowerDevice member

    /// <summary>
    /// Set the tuner power state.
    /// </summary>
    /// <param name="state">The power state to apply.</param>
    /// <returns><c>true</c> if the power state is set successfully, otherwise <c>false</c></returns>
    public bool SetPowerState(PowerState state)
    {
      this.LogDebug("Twinhan: set power state, state = {0}", state);

      if (!_isTwinhan)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }

      // It is not known for certain whether any Twinhan DVB-T tuners are able to
      // supply power to the aerial, however the FAQs on TerraTec's website suggest
      // that none are able.
      // In practise it seems that there is no problem attempting to enable power
      // for certain DVB-T tuners however attempting to execute this function with
      // a TerraTec H7 causes a hard crash.
      // Unfortunately there is no way to check whether this property is actually
      // supported because of the way the Twinhan API has been implemented (ie. one
      // KsProperty with the actual property codes encoded in the property data).
      // For that reason and for safety's sake we only execute this function for
      // satellite tuners.
      if (_tunerType != CardType.DvbS)
      {
        this.LogDebug("Twinhan: function disabled for safety");
        return true;    // Don't retry...
      }

      if (state == PowerState.On)
      {
        Marshal.WriteByte(_generalBuffer, 0, 0x01);
      }
      else
      {
        Marshal.WriteByte(_generalBuffer, 0, 0x00);
      }
      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_SET_TUNER_POWER, _generalBuffer, 1, IntPtr.Zero, 0);
      int returnedByteCount;
      int hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
        return true;
      }

      this.LogError("Twinhan: failed to set power state, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region ICustomTuner members

    /// <summary>
    /// Check if the extension implements specialised tuning for a given channel.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><c>true</c> if the extension supports specialised tuning for the channel, otherwise <c>false</c></returns>
    public bool CanTuneChannel(IChannel channel)
    {
      // Tuning of DVB-C, DVB-S/2 and DVB-T/2 channels is supported with an appropriate tuner. The interface
      // probably supports ATSC as well, however I'm not sure how to implement that since we tune ATSC by
      // channel number rather than frequency.
      if ((channel is DVBCChannel && _tunerType == CardType.DvbC) ||
        (channel is DVBSChannel && _tunerType == CardType.DvbS) ||
        (channel is DVBTChannel && _tunerType == CardType.DvbT))
      {
        return true;
      }
      return false;
    }

    /// <summary>
    /// Tune to a given channel using the specialised tuning method.
    /// </summary>
    /// <remarks>
    /// This interface has been tested unsuccessfully for satellite and terrestrial tuning with a
    /// DigitalNow Quattro S-T (OEM 6090 2xDVB-S + 2xDVB-T/analog). It returned HRESULT 0x8007001f for the
    /// THBDA_IOCTL_LOCK_TUNER call, probably indicating that the driver does not support does not support
    /// this IOCTL. It is possible that the IOCTLs are only implemented for certain models.
    /// </remarks>
    /// <param name="channel">The channel to tune.</param>
    /// <param name="parameters">Tuning time restriction settings.</param>
    /// <returns><c>true</c> if the channel is successfully tuned, otherwise <c>false</c></returns>
    public bool Tune(IChannel channel)
    {
      this.LogDebug("Twinhan: tune to channel");

      if (!_isTwinhan)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }

      int returnedByteCount;
      int hr;
      TuningParams tuningParams = new TuningParams();
      if (channel is DVBCChannel)
      {
        DVBCChannel ch = channel as DVBCChannel;
        tuningParams.Frequency = (uint)ch.Frequency;
        tuningParams.SymbolRate = (uint)ch.SymbolRate;
        tuningParams.Modulation = ch.ModulationType;
      }
      else if (channel is DVBSChannel)
      {
        DVBSChannel dvbsChannel = channel as DVBSChannel;
        LnbParams lnbParams = new LnbParams();
        lnbParams.PowerOn = true;
        lnbParams.ToneBurst = TwinhanToneBurst.Off;
        lnbParams.LowBandLof = (uint)dvbsChannel.LnbType.LowBandFrequency;
        lnbParams.HighBandLof = (uint)dvbsChannel.LnbType.HighBandFrequency;
        lnbParams.SwitchFrequency = (uint)dvbsChannel.LnbType.SwitchFrequency;
        lnbParams.Tone22k = Twinhan22k.Off;
        if (dvbsChannel.Frequency > dvbsChannel.LnbType.SwitchFrequency)
        {
          lnbParams.Tone22k = Twinhan22k.On;
        }
        lnbParams.DiseqcPort = TwinhanDiseqcPort.PortA;

        Marshal.StructureToPtr(lnbParams, _generalBuffer, true);
        TwinhanCommand lcommand = new TwinhanCommand(THBDA_IOCTL_SET_LNB_DATA, _generalBuffer, LNB_PARAMS_SIZE, IntPtr.Zero, 0);
        hr = lcommand.Execute(_propertySet, out returnedByteCount);
        if (hr == (int)HResult.Severity.Success)
        {
          this.LogDebug("Twinhan: result = success");
        }
        else
        {
          this.LogError("Twinhan: failed to set LNB configuration, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        }

        tuningParams.Frequency = (uint)dvbsChannel.Frequency;
        tuningParams.SymbolRate = (uint)dvbsChannel.SymbolRate;
        tuningParams.Modulation = (ModulationType)1;
      }
      else if (channel is DVBTChannel)
      {
        DVBTChannel ch = channel as DVBTChannel;
        tuningParams.Frequency = (uint)ch.Frequency;
        tuningParams.Bandwidth = (uint)ch.Bandwidth;
        tuningParams.Modulation = 0;  // ???
      }
      else
      {
        this.LogError("Twinhan: tuning not supported");
        return false;
      }
      tuningParams.LockWaitForResult = true;

      Marshal.StructureToPtr(tuningParams, _generalBuffer, true);
      Dump.DumpBinary(_generalBuffer, TUNING_PARAMS_SIZE);

      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_LOCK_TUNER, _generalBuffer, TUNING_PARAMS_SIZE, IntPtr.Zero, 0);
      hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
      }
      else
      {
        this.LogError("Twinhan: failed to tune, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }

      for (int i = 0; i < LNB_PARAMS_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      command = new TwinhanCommand(THBDA_IOCTL_GET_LNB_DATA, IntPtr.Zero, 0, _generalBuffer, LNB_PARAMS_SIZE);
      hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
      }
      else
      {
        this.LogError("Twinhan: failed to get LNB configuration, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }
      Dump.DumpBinary(_generalBuffer, LNB_PARAMS_SIZE);


      Marshal.WriteInt32(_generalBuffer, 0);
      command = new TwinhanCommand(THBDA_IOCTL_GET_SIGNAL_Q_S, IntPtr.Zero, 0, _generalBuffer, sizeof(int));
      hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
        Dump.DumpBinary(_generalBuffer, 4);
      }
      else
      {
        this.LogError("Twinhan: failed to read signal quality/strength information, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }

      return true;
    }

    #endregion

    #region IMpeg2PidFilter member

    /// <summary>
    /// Should the filter be enabled for the current multiplex.
    /// </summary>
    /// <param name="tuningDetail">The current multiplex/transponder tuning parameters.</param>
    /// <returns><c>true</c> if the filter should be enabled, otherwise <c>false</c></returns>
    public bool ShouldEnableFilter(IChannel tuningDetail)
    {
      // As far as I'm aware, PID filtering is only supported by the VP-7021
      // (Starbox) and VP-7041 (Magicbox) models. One or both of these are
      // connected using USB 1.x. The filter should always be enabled for
      // such tuners.
      return _isPidFilterSupported && (!_isPidFilterBypassSupported || _connection == TwinhanDeviceSpeed.UsbLow || _connection == TwinhanDeviceSpeed.UsbFull);
    }

    /// <summary>
    /// Disable the filter.
    /// </summary>
    /// <returns><c>true</c> if the filter is successfully disabled, otherwise <c>false</c></returns>
    public bool DisableFilter()
    {
      this.LogDebug("Twinhan: disable PID filter");
      if (!_isTwinhan)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }
      _pidFilterPids.Clear();

      PidFilterParams pidFilterParams = new PidFilterParams();
      pidFilterParams.FilterPids = new ushort[MAX_PID_FILTER_PID_COUNT];
      pidFilterParams.FilterMode = TwinhanPidFilterMode.Disabled;
      pidFilterParams.ValidPidMask = 0;
      Marshal.StructureToPtr(pidFilterParams, _generalBuffer, true);
      Dump.DumpBinary(_generalBuffer, PID_FILTER_PARAMS_SIZE);
      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_SET_PID_FILTER_INFO, _generalBuffer, PID_FILTER_PARAMS_SIZE, IntPtr.Zero, 0);
      int returnedByteCount;
      int hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
        return true;
      }

      this.LogError("Twinhan: failed to disable PID filter, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Get the maximum number of streams that the filter can allow.
    /// </summary>
    public int MaximumPidCount
    {
      get
      {
        return _maxPidFilterPidCount;
      }
    }

    /// <summary>
    /// Configure the filter to allow one or more streams to pass through the filter.
    /// </summary>
    /// <param name="pids">A collection of stream identifiers.</param>
    /// <returns><c>true</c> if the filter is successfully configured, otherwise <c>false</c></returns>
    public bool AllowStreams(ICollection<ushort> pids)
    {
      _pidFilterPids.UnionWith(pids);
      return true;
    }

    /// <summary>
    /// Configure the filter to stop one or more streams from passing through the filter.
    /// </summary>
    /// <param name="pids">A collection of stream identifiers.</param>
    /// <returns><c>true</c> if the filter is successfully configured, otherwise <c>false</c></returns>
    public bool BlockStreams(ICollection<ushort> pids)
    {
      _pidFilterPids.ExceptWith(pids);
      return true;
    }

    /// <summary>
    /// Apply the current filter configuration.
    /// </summary>
    /// <returns><c>true</c> if the filter configuration is successfully applied, otherwise <c>false</c></returns>
    public bool ApplyFilter()
    {
      this.LogDebug("Twinhan: apply PID filter");
      if (!_isTwinhan)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }

      PidFilterParams pidFilterParams = new PidFilterParams();
      pidFilterParams.FilterPids = new ushort[MAX_PID_FILTER_PID_COUNT];
      pidFilterParams.FilterMode = TwinhanPidFilterMode.Whitelist;
      _pidFilterPids.CopyTo(pidFilterParams.FilterPids, 0, Math.Min(_maxPidFilterPidCount, _pidFilterPids.Count));
      ulong mask = (ulong)(1 << _pidFilterPids.Count) - 1;
      pidFilterParams.ValidPidMask = (uint)mask;
      Marshal.StructureToPtr(pidFilterParams, _generalBuffer, true);
      Dump.DumpBinary(_generalBuffer, PID_FILTER_PARAMS_SIZE);
      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_SET_PID_FILTER_INFO, _generalBuffer, PID_FILTER_PARAMS_SIZE, IntPtr.Zero, 0);
      int returnedByteCount;
      int hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
        return true;
      }

      this.LogError("Twinhan: failed to apply PID filter, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IConditionalAccessProvider members

    /// <summary>
    /// Open the conditional access interface. For the interface to be opened successfully it is expected
    /// that any necessary hardware (such as a CI slot) is connected.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully opened, otherwise <c>false</c></returns>
    public bool OpenInterface()
    {
      this.LogDebug("Twinhan: open conditional access interface");

      if (!_isTwinhan)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }
      if (_isCaInterfaceOpen)
      {
        this.LogWarn("Twinhan: interface is already open");
        return true;
      }

      // We can't tell whether a CI slot is actually connected, but we can tell if this tuner supports
      // a CI slot.
      if (_ciApiVersion == TwinhanCiSupport.Unsupported)
      {
        this.LogDebug("Twinhan: tuner doesn't have a CI slot");
        return false;
      }

      if (_isTerraTec)
      {
        _mmiDataSize = TERRATEC_MMI_DATA_SIZE;
      }
      else
      {
        _mmiDataSize = DEFAULT_MMI_DATA_SIZE;
      }
      _mmiBuffer = Marshal.AllocCoTaskMem(_mmiDataSize);

      this.LogDebug("Twinhan: update CI/CAM state");
      _isCamPresent = false;
      _isCamReady = false;
      TwinhanCiState ciState;
      TwinhanMmiState mmiState;
      int hr = GetCiStatus(out ciState, out mmiState);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogError("Twinhan: failed to get CI status, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }
      else
      {
        if (ciState == TwinhanCiState.CamInserted ||
          ciState == TwinhanCiState.CamUnknown)
        {
          _isCamPresent = true;
          _isCamReady = false;
        }
        else if (ciState == TwinhanCiState.CamOkay ||
          ciState == TwinhanCiState.Cam1Okay_Old ||
          ciState == TwinhanCiState.Cam2Okay_Old)
        {
          _isCamPresent = true;
          _isCamReady = true;
        }
      }

      _isCaInterfaceOpen = true;
      StartMmiHandlerThread();

      this.LogDebug("Twinhan: result = success");
      return true;
    }

    /// <summary>
    /// Close the conditional access interface.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully closed, otherwise <c>false</c></returns>
    public bool CloseInterface()
    {
      this.LogDebug("Twinhan: close conditional access interface");

      StopMmiHandlerThread();

      if (_mmiBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_mmiBuffer);
        _mmiBuffer = IntPtr.Zero;
      }

      _isCamPresent = false;
      _isCamReady = false;
      _isCaInterfaceOpen = false;

      this.LogDebug("Twinhan: result = success");
      return true;
    }

    /// <summary>
    /// Reset the conditional access interface.
    /// </summary>
    /// <param name="resetTuner">This parameter will be set to <c>true</c> if the tuner must be reset
    ///   for the interface to be completely and successfully reset.</param>
    /// <returns><c>true</c> if the interface is successfully reset, otherwise <c>false</c></returns>
    public bool ResetInterface(out bool resetTuner)
    {
      this.LogDebug("Twinhan: reset conditional access interface");
      resetTuner = false;

      if (!_isTwinhan)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }

      CloseInterface();

      // This IOCTL does not seem to be implemented at least for the VP-1041 - the HRESULT returned seems
      // to always be 0x8007001f (ERROR_GEN_FAILURE). Either that or special conditions (graph stopped etc.)
      // are required for the reset to work. Our strategy here is to attempt the reset, and if it fails
      // request a graph rebuild.
      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_RESET_DEVICE, IntPtr.Zero, 0, IntPtr.Zero, 0);
      int returnedByteCount;
      int hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogError("Twinhan: failed to reset device, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        resetTuner = true;
        return true;
      }

      bool result = OpenInterface();
      this.LogDebug("Twinhan: result = {0}", result);
      return result;
    }

    /// <summary>
    /// Determine whether the conditional access interface is ready to receive commands.
    /// </summary>
    /// <returns><c>true</c> if the interface is ready, otherwise <c>false</c></returns>
    public bool IsInterfaceReady()
    {
      this.LogDebug("Twinhan: is conditional access interface ready");
      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }

      TwinhanCiState ciState;
      TwinhanMmiState mmiState;
      int hr = GetCiStatus(out ciState, out mmiState);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogError("Twinhan: failed to get CI status, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }
      this.LogDebug("Twinhan: CI state = {0}, MMI state = {1}", ciState, mmiState);
      bool isCamReady = false;
      if (ciState == TwinhanCiState.Cam1Okay_Old ||
        ciState == TwinhanCiState.Cam2Okay_Old ||
        ciState == TwinhanCiState.CamOkay)
      {
        isCamReady = true;
      }
      this.LogDebug("Twinhan: result = {0}", isCamReady);
      return isCamReady;
    }

    /// <summary>
    /// Send a command to to the conditional access interface.
    /// </summary>
    /// <param name="channel">The channel information associated with the service which the command relates to.</param>
    /// <param name="listAction">It is assumed that the interface may be able to decrypt one or more services
    ///   simultaneously. This parameter gives the interface an indication of the number of services that it
    ///   will be expected to manage.</param>
    /// <param name="command">The type of command.</param>
    /// <param name="pmt">The program map table for the service.</param>
    /// <param name="cat">The conditional access table for the service.</param>
    /// <returns><c>true</c> if the command is successfully sent, otherwise <c>false</c></returns>
    public bool SendCommand(IChannel channel, CaPmtListManagementAction listAction, CaPmtCommand command, Pmt pmt, Cat cat)
    {
      this.LogDebug("Twinhan: send conditional access command, list action = {0}, command = {1}", listAction, command);

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("Twinhan: the CAM is not ready");
        return false;
      }
      if (pmt == null)
      {
        this.LogError("Twinhan: PMT not supplied");
        return true;
      }

      // Twinhan supports the standard CA PMT format.
      byte[] caPmt = pmt.GetCaPmt(listAction, command);

      // Send the data to the CAM. Use local buffers since PMT updates are asynchronous.
      IntPtr pmtBuffer = Marshal.AllocCoTaskMem(caPmt.Length);
      try
      {
        Marshal.Copy(caPmt, 0, pmtBuffer, caPmt.Length);
        //Dump.DumpBinary(pmtBuffer, caPmt.Length);
        TwinhanCommand tcommand = new TwinhanCommand(THBDA_IOCTL_CI_SEND_PMT, pmtBuffer, caPmt.Length, IntPtr.Zero, 0);
        int returnedByteCount;
        int hr = tcommand.Execute(_propertySet, out returnedByteCount);
        if (hr == (int)HResult.Severity.Success)
        {
          this.LogDebug("Twinhan: result = success");
          return true;
        }

        this.LogError("Twinhan: failed to send PMT to CAM, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }
      finally
      {
        Marshal.FreeCoTaskMem(pmtBuffer);
        pmtBuffer = IntPtr.Zero;
      }
    }

    #endregion

    #region IConditionalAccessMenuActions members

    /// <summary>
    /// Set the menu call back delegate.
    /// </summary>
    /// <param name="callBacks">The call back delegate.</param>
    public void SetCallBacks(IConditionalAccessMenuCallBacks callBacks)
    {
      lock (_caMenuCallBackLock)
      {
        _caMenuCallBacks = callBacks;
      }
      StartMmiHandlerThread();
    }

    /// <summary>
    /// Send a request from the user to the CAM to open the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool EnterMenu()
    {
      this.LogDebug("Twinhan: enter menu");

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }
      StartMmiHandlerThread();
      if (!_isCamReady)
      {
        this.LogError("Twinhan: the CAM is not ready");
        return false;
      }

      int hr;
      lock (_mmiLock)
      {
        this.LogDebug("Twinhan: application information");
        for (int i = 0; i < APPLICATION_INFO_SIZE; i++)
        {
          Marshal.WriteByte(_mmiBuffer, i, 0);
        }
        TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_CI_GET_APP_INFO, IntPtr.Zero, 0, _mmiBuffer, APPLICATION_INFO_SIZE);
        int returnedByteCount;
        hr = command.Execute(_propertySet, out returnedByteCount);
        if (hr != (int)HResult.Severity.Success)
        {
          this.LogError("Twinhan: failed to read application information, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          return false;
        }

        ApplicationInfo info = (ApplicationInfo)Marshal.PtrToStructure(_mmiBuffer, typeof(ApplicationInfo));
        this.LogDebug("  type         = {0}", (MmiApplicationType)info.ApplicationType);
        this.LogDebug("  manufacturer = 0x{0:x}", info.Manufacturer);
        this.LogDebug("  code         = 0x{0:x}", info.ManufacturerCode);
        this.LogDebug("  menu title   = {0}", DvbTextConverter.Convert(info.RootMenuTitle));

        command = new TwinhanCommand(THBDA_IOCTL_CI_INIT_MMI, IntPtr.Zero, 0, IntPtr.Zero, 0);
        hr = command.Execute(_propertySet, out returnedByteCount);
      }
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
        return true;
      }

      this.LogError("Twinhan: failed to enter menu, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Send a request from the user to the CAM to close the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool CloseMenu()
    {
      this.LogDebug("Twinhan: close menu");

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }
      StartMmiHandlerThread();
      if (!_isCamReady)
      {
        this.LogError("Twinhan: the CAM is not ready");
        return false;
      }

      int hr;
      lock (_mmiLock)
      {
        TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_CI_CLOSE_MMI, IntPtr.Zero, 0, IntPtr.Zero, 0);
        int returnedByteCount;
        hr = command.Execute(_propertySet, out returnedByteCount);
      }
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
        return true;
      }

      this.LogError("Twinhan: failed to close menu, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Send a menu entry selection from the user to the CAM.
    /// </summary>
    /// <param name="choice">The index of the selection as an unsigned byte value.</param>
    /// <returns><c>true</c> if the selection is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool SelectMenuEntry(byte choice)
    {
      this.LogDebug("Twinhan: select menu entry, choice = {0}", choice);
      MmiData mmi = new MmiData();
      mmi.ChoiceIndex = (int)choice;
      mmi.Type = 1;
      return SendMmi(mmi);
    }

    /// <summary>
    /// Send an answer to an enquiry from the user to the CAM.
    /// </summary>
    /// <param name="cancel"><c>True</c> to cancel the enquiry.</param>
    /// <param name="answer">The user's answer to the enquiry.</param>
    /// <returns><c>true</c> if the answer is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool AnswerEnquiry(bool cancel, string answer)
    {
      if (answer == null)
      {
        answer = string.Empty;
      }
      this.LogDebug("Twinhan: answer enquiry, answer = {0}, cancel = {1}", answer, cancel);

      if (cancel)
      {
        return SelectMenuEntry(0); // 0 means "go back to the previous menu level"
      }

      MmiData mmi = new MmiData();
      mmi.AnswerLength = answer.Length;
      mmi.Answer = answer;
      mmi.Type = 3;
      return SendMmi(mmi);
    }

    #endregion

    #region IDiseqcDevice members

    /// <summary>
    /// Send a tone/data burst command, and then set the 22 kHz continuous tone state.
    /// </summary>
    /// <param name="toneBurstState">The tone/data burst command to send, if any.</param>
    /// <param name="tone22kState">The 22 kHz continuous tone state to set.</param>
    /// <returns><c>true</c> if the tone state is set successfully, otherwise <c>false</c></returns>
    public bool SetToneState(ToneBurst toneBurstState, Tone22k tone22kState)
    {
      // Not implemented.
      // Note: as tempting as it may be, do *not* implement this function using the THBDA_IOCTL_SET_LNB_DATA
      // IOCTL. There is some kind of interaction between the THBDA_IOCTL_SET_LNB_DATA and THBDA_IOCTL_SET_DiSEqC
      // IOCTLs that I was not able to work out fully. The driver seems to get confused about DiSEqC, 22k or LNB
      // settings when both IOCTLs are used.
      return true;
    }

    /// <summary>
    /// Send an arbitrary DiSEqC command.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <returns><c>true</c> if the command is sent successfully, otherwise <c>false</c></returns>
    public bool SendCommand(byte[] command)
    {
      this.LogDebug("Twinhan: send DiSEqC command");

      if (!_isTwinhan)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }
      if (command == null || command.Length == 0)
      {
        this.LogError("Twinhan: command not supplied");
        return true;
      }
      if (command.Length > MAX_DISEQC_MESSAGE_LENGTH)
      {
        this.LogError("Twinhan: command too long, length = {0}", command.Length);
        return false;
      }

      DiseqcMessage message = new DiseqcMessage();
      message.MessageLength = command.Length;
      message.Message = new byte[MAX_DISEQC_MESSAGE_LENGTH];
      Buffer.BlockCopy(command, 0, message.Message, 0, command.Length);

      Marshal.StructureToPtr(message, _generalBuffer, true);
      //Dump.DumpBinary(_generalBuffer, DISEQC_MESSAGE_SIZE);

      TwinhanCommand tcommand = new TwinhanCommand(THBDA_IOCTL_SET_DiSEqC, _generalBuffer, DISEQC_MESSAGE_SIZE, IntPtr.Zero, 0);
      int returnedByteCount;
      int hr = tcommand.Execute(_propertySet, out returnedByteCount);
      /*if (hr != (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }*/

      // The above command seems to return HRESULT 0x8007001f (ERROR_GEN_FAILURE)
      // regardless of whether or not it was actually successful. I tested using
      // a TechniSat SkyStar HD2 (AKA Mantis, VP-1041, Cinergy S2 PCI HD) with
      // driver versions 1.1.1.502 (July 2009) and 1.1.2.700 (July 2010).
      // --mm1352000, 16-12-2011
      this.LogDebug("Twinhan: result = success");
      return true;
    }

    /// <summary>
    /// Retrieve the response to a previously sent DiSEqC command (or alternatively, check for a command
    /// intended for this tuner).
    /// </summary>
    /// <param name="response">The response (or command).</param>
    /// <returns><c>true</c> if the response is read successfully, otherwise <c>false</c></returns>
    public bool ReadResponse(out byte[] response)
    {
      this.LogDebug("Twinhan: read DiSEqC response");
      response = null;

      if (!_isTwinhan)
      {
        this.LogWarn("Twinhan: not initialised or interface not supported");
        return false;
      }

      for (int i = 0; i < DISEQC_MESSAGE_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }

      TwinhanCommand command = new TwinhanCommand(THBDA_IOCTL_GET_DiSEqC, IntPtr.Zero, 0, _generalBuffer, DISEQC_MESSAGE_SIZE);
      int returnedByteCount;
      int hr = command.Execute(_propertySet, out returnedByteCount);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Twinhan: result = success");
        DiseqcMessage message = (DiseqcMessage)Marshal.PtrToStructure(_generalBuffer, typeof(DiseqcMessage));
        response = new byte[message.MessageLength];
        Buffer.BlockCopy(message.Message, 0, response, 0, message.MessageLength);

        Dump.DumpBinary(_generalBuffer, DISEQC_MESSAGE_SIZE);
        return true;
      }

      this.LogError("Twinhan: failed to read DiSEqC response, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      CloseInterface();
      if (_generalBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_generalBuffer);
        _generalBuffer = IntPtr.Zero;
      }
      Release.ComObject("Twinhan property set", ref _propertySet);
      _isTwinhan = false;
    }

    #endregion
  }
}