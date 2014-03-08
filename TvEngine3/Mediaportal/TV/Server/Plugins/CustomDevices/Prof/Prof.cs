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
using System.Runtime.InteropServices;
using DirectShowLib;
using DirectShowLib.BDA;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.Prof
{
  /// <summary>
  /// A class for handling DiSEqC for Prof tuners, including clones from Satrade and Omicom. The
  /// interface was originally a customised Conexant interface created by Turbosight, however
  /// Turbosight have implemented a new unified interface for their products.
  /// </summary>
  public class Prof : BaseCustomDevice, IPowerDevice, IDiseqcDevice
  {
    #region enums

    private enum BdaExtensionProperty
    {
      DiseqcMessage = 0,    // For DiSEqC messaging.
      DiseqcInit,           // For intialising DiSEqC.
      ScanFrequency,        // (Not supported...)
      ChannelChange,        // For changing channel.
      DemodInfo,            // For returning demodulator firmware state and version.
      EffectiveFrequency,   // (Not supported...)
      SignalStatus,         // For retrieving signal quality, strength, BER and other attributes.
      LockStatus,           // For retrieving demodulator lock indicators.
      ErrorControl,         // For controlling error correction and BER window.
      ChannelInfo,          // For retrieving the locked values of frequency, symbol rate etc. after corrections and adjustments.
      NbcParams             // For setting DVB-S2 parameters that could not initially be set through BDA interfaces.
    }

    private enum BdaExtensionCommand : uint
    {
      LnbPower = 0,
      Motor,
      Tone,
      Diseqc
    }

    /// <summary>
    /// Enum listing all possible 22 kHz oscillator states.
    /// </summary>
    protected enum Prof22k : byte
    {
      /// <summary>
      /// Oscillator off.
      /// </summary>
      Off = 0,
      /// <summary>
      /// Oscillator on.
      /// </summary>
      On
    }

    /// <summary>
    /// Enum listing all possible tone burst (simple DiSEqC) messages.
    /// </summary>
    protected enum ProfToneBurst : byte
    {
      /// <summary>
      /// Tone burst (simple A).
      /// </summary>
      ToneBurst = 0,
      /// <summary>
      /// Data burst (simple B).
      /// </summary>
      DataBurst,
      /// <summary>
      /// Off (no message).
      /// </summary>
      Off
    }

    private enum ProfToneModulation : uint
    {
      Undefined = 0,        // (Results in an error - *do not use*!)
      Modulated,
      Unmodulated
    }

    private enum ProfDiseqcReceiveMode : uint
    {
      Interrogation = 0,    // Expecting multiple devices attached.
      QuickReply,           // Expecting one response (receiving is suspended after first response).
      NoReply,              // Expecting no response(s).
    }

    private enum ProfPilot : uint
    {
      Off = 0,
      On,
      Unknown               // (Not used...)
    }

    private enum ProfRollOff : uint
    {
      Undefined = 0xff,
      Twenty = 0,           // 0.2
      TwentyFive,           // 0.25
      ThirtyFive            // 0.35
    }

    private enum ProfDvbsStandard : uint
    {
      Auto = 0,
      Dvbs,
      Dvbs2
    }

    private enum ProfLnbPower : uint
    {
      Off = 0,
      On
    }

    private enum ProfIrProperty
    {
      Keystrokes = 0,
      Command
    }

    #endregion

    #region structs

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BdaExtensionParams
    {
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DISEQC_TX_MESSAGE_LENGTH)]
      public byte[] DiseqcTransmitMessage;
      public byte DiseqcTransmitMessageLength;

      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DISEQC_RX_MESSAGE_LENGTH)]
      public byte[] DiseqcReceiveMessage;
      public byte DiseqcReceiveMessageLength;
      private ushort Padding;

      public ProfToneModulation ToneModulation;
      public ProfDiseqcReceiveMode ReceiveMode;

      public BdaExtensionCommand Command;
      public Prof22k Tone22k;
      public ProfToneBurst ToneBurst;
      public byte MicroControllerParityErrors;        // Parity errors: 0 indicates no errors, binary 1 indicates an error.
      public byte MicroControllerReplyErrors;         // 1 in bit i indicates error in byte i.

      [MarshalAs(UnmanagedType.Bool)]
      public bool IsLastMessage;
      public ProfLnbPower LnbPower;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct NbcTuningParams
    {
      public ProfRollOff RollOff;
      public ProfPilot Pilot;
      public ProfDvbsStandard DvbsStandard;
      public BinaryConvolutionCodeRate InnerFecRate;
      public ModulationType ModulationType;
    }

    #endregion

    #region constants

    private static readonly Guid BDA_EXTENSION_PROPERTY_SET = new Guid(0xfaa8f3e5, 0x31d4, 0x4e41, 0x88, 0xef, 0xd9, 0xeb, 0x71, 0x6f, 0x6e, 0xc9);

    private static readonly int BDA_EXTENSION_PARAMS_SIZE = Marshal.SizeOf(typeof(BdaExtensionParams));   // 188
    private static readonly int NBC_TUNING_PARAMS_SIZE = Marshal.SizeOf(typeof(NbcTuningParams));         // 20

    private const byte MAX_DISEQC_TX_MESSAGE_LENGTH = 151;  // 3 bytes per message * 50 messages
    private const byte MAX_DISEQC_RX_MESSAGE_LENGTH = 9;    // reply fifo size, do not increase (hardware limitation)

    private static readonly int GENERAL_BUFFER_SIZE = Math.Max(BDA_EXTENSION_PARAMS_SIZE, NBC_TUNING_PARAMS_SIZE);

    #endregion

    #region variables

    private bool _isProf = false;
    private IKsPropertySet _propertySet = null;
    private IntPtr _generalBuffer = IntPtr.Zero;

    #endregion

    #region ICustomDevice members

    /// <summary>
    /// The loading priority for this extension.
    /// </summary>
    public override byte Priority
    {
      get
      {
        return 60;
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
      this.LogDebug("Prof: initialising");

      IBaseFilter tunerFilter = context as IBaseFilter;
      if (tunerFilter == null)
      {
        this.LogDebug("Prof: tuner filter is null");
        return false;
      }
      if (_isProf)
      {
        this.LogWarn("Prof: extension already initialised");
        return true;
      }

      IPin pin = DsFindPin.ByDirection(tunerFilter, PinDirection.Input, 0);
      _propertySet = pin as IKsPropertySet;
      if (_propertySet == null)
      {
        this.LogDebug("Prof: pin is not a property set");
        return false;
      }

      KSPropertySupport support;
      int hr = _propertySet.QuerySupported(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.DiseqcMessage, out support);
      // The original Conexant interface uses the set method; this interface uses the get method.
      if (hr != (int)HResult.Severity.Success || !support.HasFlag(KSPropertySupport.Get))
      {
        this.LogDebug("Prof: property set not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      this.LogInfo("Prof: extension supported");
      _isProf = true;
      _generalBuffer = Marshal.AllocCoTaskMem(GENERAL_BUFFER_SIZE);
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
      this.LogDebug("Prof: on before tune call back");
      action = TunerAction.Default;

      if (!_isProf)
      {
        this.LogWarn("Prof: not initialised or interface not supported");
        return;
      }

      DVBSChannel ch = channel as DVBSChannel;
      if (ch == null)
      {
        return;
      }

      NbcTuningParams command = new NbcTuningParams();
      // Default: tuning with "auto" is slower, so avoid it if possible.
      command.DvbsStandard = ProfDvbsStandard.Auto;

      // FEC rate
      command.InnerFecRate = ch.InnerFecRate;
      this.LogDebug("  inner FEC rate = {0}", command.InnerFecRate);

      // Modulation
      if (ch.ModulationType == ModulationType.ModNotSet)
      {
        ch.ModulationType = ModulationType.ModQpsk;
        command.DvbsStandard = ProfDvbsStandard.Dvbs;
      }
      else if (ch.ModulationType == ModulationType.ModQpsk)
      {
        ch.ModulationType = ModulationType.ModNbcQpsk;
        command.DvbsStandard = ProfDvbsStandard.Dvbs2;
      }
      else if (ch.ModulationType == ModulationType.Mod8Psk)
      {
        ch.ModulationType = ModulationType.ModNbc8Psk;
        command.DvbsStandard = ProfDvbsStandard.Dvbs2;
      }
      command.ModulationType = ch.ModulationType;
      this.LogDebug("  modulation     = {0}", ch.ModulationType);

      // Pilot
      command.Pilot = ProfPilot.Off;
      if (ch.Pilot == Pilot.On)
      {
        command.Pilot = ProfPilot.On;
      }
      this.LogDebug("  pilot          = {0}", command.Pilot);

      // Roll-off
      if (ch.RollOff == RollOff.Twenty)
      {
        command.RollOff = ProfRollOff.Twenty;
      }
      else if (ch.RollOff == RollOff.TwentyFive)
      {
        command.RollOff = ProfRollOff.TwentyFive;
      }
      else if (ch.RollOff == RollOff.ThirtyFive)
      {
        command.RollOff = ProfRollOff.ThirtyFive;
      }
      else
      {
        command.RollOff = ProfRollOff.Undefined;
      }
      this.LogDebug("  roll-off       = {0}", command.RollOff);

      KSPropertySupport support;
      int hr = _propertySet.QuerySupported(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.NbcParams, out support);
      if (hr != (int)HResult.Severity.Success || !support.HasFlag(KSPropertySupport.Set))
      {
        this.LogDebug("Prof: NBC tuning parameter property not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return;
      }

      Marshal.StructureToPtr(command, _generalBuffer, true);
      //Dump.DumpBinary(_generalBuffer, NBC_TUNING_PARAMS_SIZE);

      hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.NbcParams,
        _generalBuffer, NBC_TUNING_PARAMS_SIZE,
        _generalBuffer, NBC_TUNING_PARAMS_SIZE
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Prof: result = success");
      }
      else
      {
        this.LogError("Prof: failed to set NBC tuning parameters, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
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
    public virtual bool SetPowerState(PowerState state)
    {
      this.LogDebug("Prof: set power state, state = {0}", state);

      if (!_isProf)
      {
        this.LogWarn("Prof: not initialised or interface not supported");
        return false;
      }

      BdaExtensionParams command = new BdaExtensionParams();
      command.Command = BdaExtensionCommand.LnbPower;
      command.LnbPower = ProfLnbPower.Off;
      if (state == PowerState.On)
      {
        command.LnbPower = ProfLnbPower.On;
      }

      Marshal.StructureToPtr(command, _generalBuffer, true);
      //Dump.DumpBinary(_generalBuffer, BDA_EXTENSION_PARAMS_SIZE);

      int returnedByteCount = 0;
      int hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.DiseqcMessage,
         _generalBuffer, BDA_EXTENSION_PARAMS_SIZE,
         _generalBuffer, BDA_EXTENSION_PARAMS_SIZE,
         out returnedByteCount
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Prof: result = success");
        return true;
      }

      this.LogError("Prof: failed to set power state, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IDiseqcDevice members

    /// <summary>
    /// Control whether tone/data burst and 22 kHz legacy tone are used.
    /// </summary>
    /// <param name="toneBurstState">The tone/data burst state.</param>
    /// <param name="tone22kState">The 22 kHz legacy tone state.</param>
    /// <returns><c>true</c> if the tone state is set successfully, otherwise <c>false</c></returns>
    public virtual bool SetToneState(ToneBurst toneBurstState, Tone22k tone22kState)
    {
      this.LogDebug("Prof: set tone state, burst = {0}, 22 kHz = {1}", toneBurstState, tone22kState);

      if (!_isProf)
      {
        this.LogWarn("Prof: not initialised or interface not supported");
        return false;
      }

      BdaExtensionParams command = new BdaExtensionParams();
      command.Command = BdaExtensionCommand.Tone;
      command.ToneBurst = ProfToneBurst.Off;
      command.ToneModulation = ProfToneModulation.Unmodulated;   // Can't use undefined, so use simple A instead.
      if (toneBurstState == ToneBurst.ToneBurst)
      {
        command.ToneBurst = ProfToneBurst.ToneBurst;
      }
      else if (toneBurstState == ToneBurst.DataBurst)
      {
        command.ToneBurst = ProfToneBurst.DataBurst;
        command.ToneModulation = ProfToneModulation.Modulated;
      }

      command.Tone22k = Prof22k.Off;
      if (tone22kState == Tone22k.On)
      {
        command.Tone22k = Prof22k.On;
      }

      Marshal.StructureToPtr(command, _generalBuffer, true);
      //Dump.DumpBinary(_generalBuffer, BDA_EXTENSION_PARAMS_SIZE);

      int returnedByteCount = 0;
      int hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.DiseqcMessage,
        _generalBuffer, BDA_EXTENSION_PARAMS_SIZE,
        _generalBuffer, BDA_EXTENSION_PARAMS_SIZE,
        out returnedByteCount
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Prof: result = success");
        return true;
      }

      this.LogError("Prof: failed to set tone state, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Send an arbitrary DiSEqC command.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <returns><c>true</c> if the command is sent successfully, otherwise <c>false</c></returns>
    public virtual bool SendCommand(byte[] command)
    {
      this.LogDebug("Prof: send DiSEqC command");

      if (!_isProf)
      {
        this.LogWarn("Prof: not initialised or interface not supported");
        return false;
      }
      if (command == null || command.Length == 0)
      {
        this.LogError("Prof: command not supplied");
        return true;
      }
      if (command.Length > MAX_DISEQC_TX_MESSAGE_LENGTH)
      {
        this.LogError("Prof: command too long, length = {0}", command.Length);
        return false;
      }

      BdaExtensionParams propertyParams = new BdaExtensionParams();
      propertyParams.DiseqcTransmitMessage = new byte[MAX_DISEQC_TX_MESSAGE_LENGTH];
      Buffer.BlockCopy(command, 0, propertyParams.DiseqcTransmitMessage, 0, command.Length);
      propertyParams.DiseqcTransmitMessageLength = (byte)command.Length;
      propertyParams.ReceiveMode = ProfDiseqcReceiveMode.NoReply;
      propertyParams.Command = BdaExtensionCommand.Diseqc;
      propertyParams.IsLastMessage = true;
      propertyParams.LnbPower = ProfLnbPower.On;

      Marshal.StructureToPtr(propertyParams, _generalBuffer, true);
      //Dump.DumpBinary(_generalBuffer, BDA_EXTENSION_PARAMS_SIZE);

      int returnedByteCount = 0;
      int hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.DiseqcMessage,
        _generalBuffer, BDA_EXTENSION_PARAMS_SIZE,
        _generalBuffer, BDA_EXTENSION_PARAMS_SIZE,
        out returnedByteCount
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Prof: result = success");
        return true;
      }

      this.LogError("Prof: failed to send DiSEqC command, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Retrieve the response to a previously sent DiSEqC command (or alternatively, check for a command
    /// intended for this tuner).
    /// </summary>
    /// <param name="response">The response (or command).</param>
    /// <returns><c>true</c> if the response is read successfully, otherwise <c>false</c></returns>
    public bool ReadResponse(out byte[] response)
    {
      // Not implemented.
      response = null;
      return false;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      if (_generalBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_generalBuffer);
        _generalBuffer = IntPtr.Zero;
      }
      Release.ComObject("Prof property set", ref _propertySet);
      _isProf = false;
    }

    #endregion
  }
}