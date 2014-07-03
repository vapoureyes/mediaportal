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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DirectShowLib;
using DirectShowLib.BDA;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;
using MediaPortal.Common.Utils;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.DvbWorld
{
  /// <summary>
  /// A class for handling DiSEqC and remote controls for DVB World tuners.
  /// </summary>
  public class DvbWorld : BaseCustomDevice, ICustomTuner, IDiseqcDevice, IRemoteControlListener
  {
    #region enums

    private enum DwPolarisation : int
    {
      Vertical = 0,
      Horizontal
    }

    private enum DwDiseqcPort : int
    {
      None = 0,
      PortA,
      PortB,
      PortC,
      PortD
    }

    private enum DwModulation : int
    {
      Dvbs_Qpsk = 1,
      Dvbs2_Qpsk,
      Dvbs2_8Psk
    }

    private enum DwToneBurst : int
    {
      Undefined = 0,
      ToneBurst,    // simple A
      DataBurst     // simple B
    }

    private enum DwTunerType : byte
    {
      DvbS = 1,
      DvbS2
    }

    private enum DwTunerConnection : byte
    {
      Usb1_0 = 0x01,
      Usb2_0,
      Usb3_0,
      Pci2_0 = 0x11,
      Pcie
    }

    #endregion

    #region structs

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DiseqcCommand
    {
      public Guid PropertyGuid;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DISEQC_MESSAGE_LENGTH)]
      public byte[] Command;
      public uint CommandLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TuningParams
    {
      public Guid PropertyGuid;
      public uint SymbolRate;     // unit = ks/s
      public uint Frequency;      // unit = kHz
      public uint LnbLof;         // unit = kHz
      public DwPolarisation Polarisation;
      [MarshalAs(UnmanagedType.Bool)]
      public bool Tone22kEnabled;
      public DwDiseqcPort DiseqcPort;
      public BinaryConvolutionCodeRate FecRate;
      public DwModulation Modulation;
      public DwToneBurst ToneBurst;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TunerInfo
    {
      public uint VendorId;
      public uint ProductId;
      public DwTunerType TunerType;
      public DwTunerConnection Connection;
      private ushort Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct HardwareInfo
    {
      private byte Reserved1;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
      public byte[] SerialNumber;
      public byte CiExtended;
      private byte Reserved2;
    }

    #endregion

    #region constants

    private static readonly Guid BDA_EXTENSION_PROPERTY_SET_DISEQC = new Guid(0x61ae2cdf, 0x87e8, 0x445c, 0x8a, 0x7, 0x35, 0x6e, 0xd2, 0x28, 0xfb, 0x4e);
    private static readonly Guid BDA_EXTENSION_PROPERTY_SET_TUNE = new Guid(0x8bed860a, 0xa7b4, 0x4e90, 0x9d, 0xf4, 0x13, 0x20, 0xc9, 0x49, 0x22, 0x61);
    private static readonly Guid BDA_EXTENSION_PROPERTY_SET_HID = new Guid(0xdc0a8dca, 0x2c9c, 0x45d5, 0x81, 0xf9, 0xdd, 0xb3, 0xf2, 0xba, 0x7e, 0xa6);
    private static readonly Guid BDA_EXTENSION_PROPERTY_SET_TUNER_INFO = new Guid(0x9b56175, 0x4423, 0x47ba, 0x82, 0x85, 0xdd, 0x6a, 0x70, 0xbf, 0x8, 0x4b);
    private static readonly Guid BDA_EXTENSION_PROPERTY_SET_HARDWARE_INFO = new Guid(0x4f52320c, 0x7ff7, 0x422b, 0xbe, 0xfd, 0xdf, 0x96, 0x7b, 0x6c, 0x91, 0x39);
    private static readonly Guid BDA_EXTENSION_PROPERTY_SET_MAC_ADDRESS = new Guid(0x334d58a, 0xc4f, 0x40aa, 0xab, 0x21, 0x44, 0x59, 0xc3, 0xf1, 0x65, 0x5a);

    private static readonly int KS_PROPERTY_SIZE = Marshal.SizeOf(typeof(KsProperty));        // 24
    private static readonly int DISEQC_COMMAND_SIZE = Marshal.SizeOf(typeof(DiseqcCommand));  // 276
    private const int MAX_DISEQC_MESSAGE_LENGTH = 256;
    private static readonly int TUNING_PARAMS_SIZE = Marshal.SizeOf(typeof(TuningParams));    // 52
    private static readonly int TUNER_INFO_SIZE = Marshal.SizeOf(typeof(TunerInfo));          // 12
    private static readonly int HARDWARE_INFO_SIZE = Marshal.SizeOf(typeof(HardwareInfo));    // 8
    private const int MAC_ADDRESS_LENGTH = 6;
    private const int HID_CODE_LENGTH = 4;

    private static readonly int GENERAL_BUFFER_SIZE = new int[]
      {
        DISEQC_COMMAND_SIZE, HARDWARE_INFO_SIZE, MAC_ADDRESS_LENGTH, TUNER_INFO_SIZE, TUNING_PARAMS_SIZE
      }.Max();

    private const int REMOTE_CONTROL_LISTENER_THREAD_WAIT_TIME = 100;     // unit = ms

    #endregion

    #region variables

    private bool _isDvbWorld = false;
    private IntPtr _ksObjectHandle = IntPtr.Zero;
    private IntPtr _generalBuffer = IntPtr.Zero;
    private IBDA_FrequencyFilter _frequencyFilterInterface = null;

    private bool _isRemoteControlInterfaceOpen = false;
    private IntPtr _remoteControlBuffer = IntPtr.Zero;
    private Thread _remoteControlListenerThread = null;
    private AutoResetEvent _remoteControlListenerThreadStopEvent = null;

    #endregion

    /// <summary>
    /// Attempt to read the hardware information from the tuner.
    /// </summary>
    private void ReadTunerInfo()
    {
      this.LogDebug("DVB World: read tuner information");
      for (int i = 0; i < TUNER_INFO_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      int hr = GetIoctl(BDA_EXTENSION_PROPERTY_SET_TUNER_INFO, _generalBuffer, TUNER_INFO_SIZE);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogWarn("DVB World: failed to read tuner information, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }
      else
      {
        TunerInfo info = (TunerInfo)Marshal.PtrToStructure(_generalBuffer, typeof(TunerInfo));
        this.LogDebug("  vendor ID   = 0x{0:x4}", info.VendorId);
        this.LogDebug("  product ID  = 0x{0:x4}", info.ProductId);
        this.LogDebug("  tuner type  = {0}", info.TunerType);
        this.LogDebug("  connection  = {0}", info.Connection);
      }

      this.LogDebug("DVB World: read hardware information");
      for (int i = 0; i < HARDWARE_INFO_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      hr = GetIoctl(BDA_EXTENSION_PROPERTY_SET_HARDWARE_INFO, _generalBuffer, HARDWARE_INFO_SIZE);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogWarn("DVB World: failed to read hardware information, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }
      else
      {
        HardwareInfo info = (HardwareInfo)Marshal.PtrToStructure(_generalBuffer, typeof(HardwareInfo));
        this.LogDebug("  serial #    = {0}", BitConverter.ToString(info.SerialNumber).ToLowerInvariant());
        this.LogDebug("  CI ext.     = {0}", info.CiExtended);
      }

      this.LogDebug("DVB World: read MAC address");
      for (int i = 0; i < MAC_ADDRESS_LENGTH; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      hr = GetIoctl(BDA_EXTENSION_PROPERTY_SET_MAC_ADDRESS, _generalBuffer, MAC_ADDRESS_LENGTH);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogWarn("DVB World: failed to read MAC address, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }
      else
      {
        byte[] address = new byte[MAC_ADDRESS_LENGTH];
        Marshal.Copy(_generalBuffer, address, 0, MAC_ADDRESS_LENGTH);
        this.LogDebug("  MAC address = {0}", BitConverter.ToString(address).ToLowerInvariant());
      }
    }

    /// <summary>
    /// Attempt to find the BDA frequency filter interface.
    /// </summary>
    /// <param name="context">The context supplied to initialise the extension.</param>
    /// <returns>a frequency filter instance if found, otherwise <c>null</c></returns>
    private IBDA_FrequencyFilter FindFrequencyFilter(object context)
    {
      IBDA_Topology topology = context as IBDA_Topology;
      if (topology == null)
      {
        return null;
      }

      int nodeTypeCount;
      int[] nodeTypes = new int[33];
      int hr = topology.GetNodeTypes(out nodeTypeCount, 32, nodeTypes);
      if (hr != (int)HResult.Severity.Success)
      {
        return null;
      }

      Guid[] interfaces = new Guid[33];
      int interfaceCount;
      for (int i = 0; i < nodeTypeCount; ++i)
      {
        hr = topology.GetNodeInterfaces(nodeTypes[i], out interfaceCount, 32, interfaces);
        if (hr != (int)HResult.Severity.Success)
        {
          continue;
        }
        for (int j = 0; j < interfaceCount; j++)
        {
          if (interfaces[j] == typeof(IBDA_FrequencyFilter).GUID)
          {
            object controlNode;
            hr = topology.GetControlNode(0, 1, nodeTypes[i], out controlNode);
            IBDA_FrequencyFilter frequencyFilter = controlNode as IBDA_FrequencyFilter;
            if (frequencyFilter != null)
            {
              this.LogDebug("DVB World: found frequency filter interface");
              return frequencyFilter;
            }
            Release.ComObject("DVB World topology control node", ref controlNode);
          }
        }
      }
      return null;
    }

    #region IOCTL

    private int GetIoctl(Guid propertySet, IntPtr outputBuffer, int outputBufferSize)
    {
      IntPtr inputBuffer = Marshal.AllocCoTaskMem(KS_PROPERTY_SIZE);
      try
      {
        Marshal.StructureToPtr(propertySet, inputBuffer, false);
        Marshal.WriteInt32(inputBuffer, 16, 0);
        Marshal.WriteInt32(inputBuffer, 20, 0);
        uint returnedByteCount;
        return NativeMethods.KsSynchronousDeviceControl(_ksObjectHandle, NativeMethods.IOCTL_KS_PROPERTY, inputBuffer, (uint)KS_PROPERTY_SIZE, outputBuffer, (uint)outputBufferSize, out returnedByteCount);
      }
      finally
      {
        Marshal.FreeCoTaskMem(inputBuffer);
      }
    }

    private int SetIoctl(IntPtr inputBuffer, int inputBufferSize)
    {
      uint returnedByteCount;
      return NativeMethods.KsSynchronousDeviceControl(_ksObjectHandle, NativeMethods.IOCTL_KS_PROPERTY, inputBuffer, (uint)inputBufferSize, IntPtr.Zero, 0, out returnedByteCount);
    }

    #endregion

    #region remote control listener thread

    /// <summary>
    /// Start a thread to listen for remote control commands.
    /// </summary>
    private void StartRemoteControlListenerThread()
    {
      // Don't start a thread if the interface has not been opened.
      if (!_isRemoteControlInterfaceOpen)
      {
        return;
      }

      // Kill the existing thread if it is in "zombie" state.
      if (_remoteControlListenerThread != null && !_remoteControlListenerThread.IsAlive)
      {
        StopRemoteControlListenerThread();
      }
      if (_remoteControlListenerThread == null)
      {
        this.LogDebug("DVB World: starting new remote control listener thread");
        _remoteControlListenerThreadStopEvent = new AutoResetEvent(false);
        _remoteControlListenerThread = new Thread(new ThreadStart(RemoteControlListener));
        _remoteControlListenerThread.Name = "DVB World remote control listener";
        _remoteControlListenerThread.IsBackground = true;
        _remoteControlListenerThread.Priority = ThreadPriority.Lowest;
        _remoteControlListenerThread.Start();
      }
    }

    /// <summary>
    /// Stop the thread that listens for remote control commands.
    /// </summary>
    private void StopRemoteControlListenerThread()
    {
      if (_remoteControlListenerThread != null)
      {
        if (!_remoteControlListenerThread.IsAlive)
        {
          this.LogWarn("DVB World: aborting old remote control listener thread");
          _remoteControlListenerThread.Abort();
        }
        else
        {
          _remoteControlListenerThreadStopEvent.Set();
          if (!_remoteControlListenerThread.Join(REMOTE_CONTROL_LISTENER_THREAD_WAIT_TIME * 2))
          {
            this.LogWarn("DVB World: failed to join remote control listener thread, aborting thread");
            _remoteControlListenerThread.Abort();
          }
        }
        _remoteControlListenerThread = null;
        if (_remoteControlListenerThreadStopEvent != null)
        {
          _remoteControlListenerThreadStopEvent.Close();
          _remoteControlListenerThreadStopEvent = null;
        }
      }
    }

    /// <summary>
    /// Thread function for receiving remote control commands.
    /// </summary>
    private void RemoteControlListener()
    {
      this.LogDebug("DVB World: remote control listener thread start polling");
      int hr;
      try
      {
        while (!_remoteControlListenerThreadStopEvent.WaitOne(REMOTE_CONTROL_LISTENER_THREAD_WAIT_TIME))
        {
          for (int i = 0; i < HID_CODE_LENGTH; i++)
          {
            Marshal.WriteByte(_remoteControlBuffer, i, 0);
          }
          hr = GetIoctl(BDA_EXTENSION_PROPERTY_SET_HID, _remoteControlBuffer, HID_CODE_LENGTH);
          if (hr != (int)HResult.Severity.Success)
          {
            this.LogError("DVB World: failed to read HID code, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          }
          else
          {
            int code = Marshal.ReadInt32(_remoteControlBuffer);
            if (code != 0xff && code != unchecked((int)0xffffffee))
            {
              this.LogDebug("DVB World: remote control keypress, code = {0:x8}", code);
            }
          }
        }
      }
      catch (ThreadAbortException)
      {
      }
      catch (Exception ex)
      {
        this.LogError(ex, "DVB World: remote control listener thread exception");
        return;
      }
      this.LogDebug("DVB World: remote control listener thread stop polling");
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
        return "DVB World";
      }
    }

    /// <summary>
    /// Attempt to initialise the extension-specific interfaces used by the class. If
    /// initialisation fails, the <see ref="ICustomDevice"/> instance should be disposed
    /// immediately.
    /// </summary>
    /// <param name="tunerExternalId">The external identifier for the tuner.</param>
    /// <param name="tunerType">The tuner type (eg. DVB-S, DVB-T... etc.).</param>
    /// <param name="context">Context required to initialise the interface.</param>
    /// <returns><c>true</c> if the interfaces are successfully initialised, otherwise <c>false</c></returns>
    public override bool Initialise(string tunerExternalId, CardType tunerType, object context)
    {
      this.LogDebug("DVB World: initialising");

      if (_isDvbWorld)
      {
        this.LogWarn("DVB World: extension already initialised");
        return true;
      }

      IKsObject ksObject = context as IKsObject;
      if (ksObject == null)
      {
        this.LogDebug("DVB World: tuner filter is not a KS object");
        return false;
      }

      _ksObjectHandle = ksObject.KsGetObjectHandle();
      if (_ksObjectHandle == IntPtr.Zero)
      {
        this.LogDebug("DVB World: KS object handle is not valid");
        return false;
      }

      _generalBuffer = Marshal.AllocCoTaskMem(GENERAL_BUFFER_SIZE);
      for (int i = 0; i < TUNER_INFO_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      int hr = GetIoctl(BDA_EXTENSION_PROPERTY_SET_TUNER_INFO, _generalBuffer, TUNER_INFO_SIZE);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogDebug("DVB World: property set not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      this.LogInfo("DVB World: extension supported");
      _isDvbWorld = true;

      _frequencyFilterInterface = FindFrequencyFilter(context);
      ReadTunerInfo();
      return true;
    }

    #region device state change call backs

    /// <summary>
    /// This call back is invoked before a tune request is assembled.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="currentChannel">The channel that the tuner is currently tuned to..</param>
    /// <param name="channel">The channel that the tuner will been tuned to.</param>
    /// <param name="action">The action to take, if any.</param>
    public override void OnBeforeTune(ITVCard tuner, IChannel currentChannel, ref IChannel channel, out TunerAction action)
    {
      this.LogDebug("DVB World: on before tune call back");
      action = TunerAction.Default;

      if (!_isDvbWorld)
      {
        this.LogWarn("DVB World: not initialised or interface not supported");
        return;
      }

      // We only have work to do if the channel is a DVB-S2 channel.
      DVBSChannel ch = channel as DVBSChannel;
      if (ch == null)
      {
        return;
      }

      if (ch.ModulationType == ModulationType.ModQpsk)
      {
        ch.ModulationType = ModulationType.ModNbcQpsk;
      }
      else if (ch.ModulationType == ModulationType.Mod8Psk)
      {
        ch.ModulationType = ModulationType.ModNbc8Psk;
      }
      this.LogDebug("  modulation = {0}", ch.ModulationType);
    }

    #endregion

    #endregion

    #region ICustomTuner members

    /// <summary>
    /// Check if the extension implements specialised tuning for a given channel.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><c>true</c> if the extension supports specialised tuning for the channel, otherwise <c>false</c></returns>
    public bool CanTuneChannel(IChannel channel)
    {
      return channel is DVBSChannel;
    }

    /// <summary>
    /// Tune to a given channel using the specialised tuning method.
    /// </summary>
    /// <param name="channel">The channel to tune.</param>
    /// <returns><c>true</c> if the channel is successfully tuned, otherwise <c>false</c></returns>
    public bool Tune(IChannel channel)
    {
      this.LogDebug("DVB World: tune to channel");

      if (!_isDvbWorld)
      {
        this.LogWarn("DVB World: not initialised or interface not supported");
        return false;
      }

      DVBSChannel dvbsChannel = channel as DVBSChannel;
      if (dvbsChannel == null)
      {
        this.LogError("DVB World: tuning is not supported for channel");
        return false;
      }

      TuningParams tuningParams = new TuningParams();
      tuningParams.PropertyGuid = BDA_EXTENSION_PROPERTY_SET_TUNE;
      tuningParams.Frequency = (uint)dvbsChannel.Frequency;
      tuningParams.SymbolRate = (uint)dvbsChannel.SymbolRate;
      if (dvbsChannel.Polarisation == Polarisation.LinearV || dvbsChannel.Polarisation == Polarisation.CircularR)
      {
        tuningParams.Polarisation = DwPolarisation.Vertical;
      }
      else
      {
        tuningParams.Polarisation = DwPolarisation.Horizontal;
      }
      // DiSEqC commands are already sent using the raw command interface. No need to resend them and
      // unnecessarily slow down the tune request.
      tuningParams.DiseqcPort = DwDiseqcPort.None;
      tuningParams.FecRate = dvbsChannel.InnerFecRate;

      // Take care! OnBeforeTune() will have modified the modulation.
      if (dvbsChannel.ModulationType == ModulationType.ModNbcQpsk)
      {
        tuningParams.Modulation = DwModulation.Dvbs2_Qpsk;
      }
      else if (dvbsChannel.ModulationType == ModulationType.ModNbc8Psk)
      {
        tuningParams.Modulation = DwModulation.Dvbs2_8Psk;
      }
      else
      {
        tuningParams.Modulation = DwModulation.Dvbs_Qpsk;
      }
      tuningParams.ToneBurst = DwToneBurst.Undefined;

      if (dvbsChannel.Frequency >= dvbsChannel.LnbType.SwitchFrequency)
      {
        tuningParams.LnbLof = (uint)dvbsChannel.LnbType.HighBandFrequency;
        tuningParams.Tone22kEnabled = true;
      }
      else
      {
        tuningParams.LnbLof = (uint)dvbsChannel.LnbType.LowBandFrequency;
        tuningParams.Tone22kEnabled = false;
      }

      Marshal.StructureToPtr(tuningParams, _generalBuffer, false);
      //Dump.DumpBinary(_generalBuffer, TUNING_PARAMS_SIZE);

      int hr = SetIoctl(_generalBuffer, TUNING_PARAMS_SIZE);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("DVB World: result = success");
        return true;
      }

      this.LogError("DVB World: failed to tune, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IDiseqcDevice members

    /// <summary>
    /// Control whether tone/data burst and 22 kHz legacy tone are used.
    /// </summary>
    /// <param name="toneBurstState">The tone/data burst state.</param>
    /// <param name="tone22k">The 22 kHz legacy tone state.</param>
    /// <returns><c>true</c> if the tone state is set successfully, otherwise <c>false</c></returns>
    public bool SetToneState(ToneBurst toneBurstState, Tone22k tone22kState)
    {
      this.LogDebug("DVB World: set tone state, burst = {0}, 22 kHz = {1}", toneBurstState, tone22kState);

      if (!_isDvbWorld)
      {
        this.LogWarn("DVB World: not initialised or interface not supported");
        return false;
      }
      if (_frequencyFilterInterface == null)
      {
        this.LogDebug("DVB World: frequency filter interface not available");
        return false;
      }

      int hr = (int)HResult.Severity.Success;
      if (toneBurstState == ToneBurst.ToneBurst)
      {
        hr = _frequencyFilterInterface.put_FrequencyMultiplier((int)DwToneBurst.ToneBurst);
      }
      else if (toneBurstState == ToneBurst.DataBurst)
      {
        hr = _frequencyFilterInterface.put_FrequencyMultiplier((int)DwToneBurst.DataBurst);
      }
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("DVB World: result = success");
        return true;
      }

      this.LogError("DVB World: failed to set tone state, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Send an arbitrary DiSEqC command.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <returns><c>true</c> if the command is sent successfully, otherwise <c>false</c></returns>
    public bool SendDiseqcCommand(byte[] command)
    {
      this.LogDebug("DVB World: send DiSEqC command");

      if (!_isDvbWorld)
      {
        this.LogWarn("DVB World: not initialised or interface not supported");
        return false;
      }
      if (command == null || command.Length == 0)
      {
        this.LogWarn("DVB World: DiSEqC command not supplied");
        return true;
      }
      if (command.Length > MAX_DISEQC_MESSAGE_LENGTH)
      {
        this.LogError("DVB World: DiSEqC command too long, length = {0}", command.Length);
        return false;
      }

      DiseqcCommand dcommand = new DiseqcCommand();
      dcommand.PropertyGuid = BDA_EXTENSION_PROPERTY_SET_DISEQC;
      dcommand.CommandLength = (uint)command.Length;
      dcommand.Command = new byte[MAX_DISEQC_MESSAGE_LENGTH];
      Buffer.BlockCopy(command, 0, dcommand.Command, 0, command.Length);

      Marshal.StructureToPtr(dcommand, _generalBuffer, false);
      //Dump.DumpBinary(_generalBuffer, DISEQC_COMMAND_SIZE);

      int hr = SetIoctl(_generalBuffer, DISEQC_COMMAND_SIZE);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("DVB World: result = success");
        return true;
      }

      this.LogError("DVB World: failed to send DiSEqC command, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Retrieve the response to a previously sent DiSEqC command (or alternatively, check for a command
    /// intended for this tuner).
    /// </summary>
    /// <param name="response">The response (or command).</param>
    /// <returns><c>true</c> if the response is read successfully, otherwise <c>false</c></returns>
    public bool ReadDiseqcResponse(out byte[] response)
    {
      // Not implemented.
      response = null;
      return false;
    }

    #endregion

    #region IRemoteControlListener members

    /// <summary>
    /// Open the remote control interface and start listening for commands.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully opened, otherwise <c>false</c></returns>
    public bool OpenRemoteControlInterface()
    {
      this.LogDebug("DVB World: open remote control interface");

      if (!_isDvbWorld)
      {
        this.LogWarn("DVB World: not initialised or interface not supported");
        return false;
      }
      if (_isRemoteControlInterfaceOpen)
      {
        this.LogWarn("DVB World: remote control interface is already open");
        return true;
      }

      int hr = GetIoctl(BDA_EXTENSION_PROPERTY_SET_HID, _remoteControlBuffer, HID_CODE_LENGTH);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogDebug("DVB World: property set not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      _remoteControlBuffer = Marshal.AllocCoTaskMem(HID_CODE_LENGTH);
      _isRemoteControlInterfaceOpen = true;
      StartRemoteControlListenerThread();

      this.LogDebug("DVB World: result = success");
      return true;
    }

    /// <summary>
    /// Close the remote control interface and stop listening for commands.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully closed, otherwise <c>false</c></returns>
    public bool CloseRemoteControlInterface()
    {
      this.LogDebug("DVB World: close remote control interface");

      StopRemoteControlListenerThread();
      if (_remoteControlBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_remoteControlBuffer);
        _remoteControlBuffer = IntPtr.Zero;
      }

      _isRemoteControlInterfaceOpen = false;
      this.LogDebug("DVB World: result = success");
      return true;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      if (_isDvbWorld)
      {
        CloseRemoteControlInterface();
      }
      Release.ComObject("DVB World frequency filter interface", ref _frequencyFilterInterface);
      if (_generalBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_generalBuffer);
        _generalBuffer = IntPtr.Zero;
      }
      _isDvbWorld = false;
    }

    #endregion
  }
}