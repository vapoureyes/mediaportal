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
using System.Runtime.InteropServices;
using DirectShowLib;
using DirectShowLib.BDA;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.MicrosoftBda
{
  /// <summary>
  /// This class provides a base implementation of PID filtering, DiSEqC and clear QAM tuning
  /// support for tuners that support Microsoft BDA interfaces and de-facto standards.
  /// </summary>
  public class MicrosoftBda : BaseCustomDevice, IMpeg2PidFilter, IDiseqcDevice
  {
    #region constants

    private const int INSTANCE_SIZE = 32;   // The size of a property instance (KSP_NODE) parameter.
    private static readonly int BDA_DISEQC_MESSAGE_SIZE = Marshal.SizeOf(typeof(BdaDiseqcMessage));   // 16
    private const int MAX_DISEQC_MESSAGE_LENGTH = 8;
    private static readonly int PARAM_BUFFER_SIZE = BDA_DISEQC_MESSAGE_SIZE;

    #endregion

    #region variables

    private bool _isMicrosoftBda = false;

    // DiSEqC
    private IKsPropertySet _diseqcPropertySet = null;           // IBDA_DiseqCommand
    private uint _requestId = 1;                                // Unique request ID for raw DiSEqC commands.
    private IBDA_FrequencyFilter _oldDiseqcInterface = null;    // IBDA_FrequencyFilter
    private IBDA_DeviceControl _deviceControl = null;
    private List<byte[]> _commands = new List<byte[]>();        // A cache of commands.
    private bool _useToneBurst = false;

    // Annex C QAM (North American cable)
    private IKsPropertySet _qamPropertySet = null;

    // PID filter
    private IMPEG2PIDMap _pidFilterInterface = null;
    private HashSet<ushort> _pidFilterPids = new HashSet<ushort>();

    private IntPtr _instanceBuffer = IntPtr.Zero;
    private IntPtr _paramBuffer = IntPtr.Zero;

    #endregion

    /// <summary>
    /// The class or property set that provides access to the tuner modulation parameter.
    /// </summary>
    protected virtual Guid ModulationPropertyClass
    {
      get
      {
        return typeof(IBDA_DigitalDemodulator).GUID;
      }
    }

    /// <summary>
    /// Determine if a filter supports the IBDA_DiseqCommand interface.
    /// </summary>
    /// <remarks>
    /// The IBDA_DiseqCommand was introduced in Windows 7. It is only supported by some tuners. We prefer to use
    /// this interface [over IBDA_FrequencyFilter.put_Range()] if it is available because it has capability to
    /// support sending and receiving raw messages.
    /// </remarks>
    /// <param name="filter">The filter to check.</param>
    /// <returns>a property set that supports the IBDA_DiseqCommand interface if successful, otherwise <c>null</c></returns>
    private IKsPropertySet CheckBdaDiseqcSupport(IBaseFilter filter)
    {
      this.LogDebug("Microsoft BDA: check for IBDA_DiseqCommand DiSEqC support");

      IPin pin = DsFindPin.ByDirection(filter, PinDirection.Input, 0);
      if (pin == null)
      {
        this.LogError("Microsoft BDA: failed to find input pin");
        return null;
      }

      IKsPropertySet ps = pin as IKsPropertySet;
      if (ps == null)
      {
        this.LogDebug("Microsoft BDA: input pin is not a property set");
        Release.ComObject("Microsoft DiSEqC filter input pin", ref pin);
        return null;
      }

      KSPropertySupport support;
      int hr = ps.QuerySupported(typeof(IBDA_DiseqCommand).GUID, (int)BdaDiseqcProperty.LnbSource, out support);
      if (hr != (int)HResult.Severity.Success || !support.HasFlag(KSPropertySupport.Set))
      {
        this.LogDebug("Microsoft BDA: property set not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        Release.ComObject("Microsoft DiSEqC property set", ref ps);
        pin = null;
        return null;
      }

      return ps;
    }

    /// <summary>
    /// Determine if a filter supports the IBDA_FrequencyFilter interface.
    /// </summary>
    /// <remarks>
    /// The IBDA_FrequencyFilter.put_Range() function was the de-facto "BDA" standard for DiSEqC 1.0 prior
    /// to the introduction of IBDA_DiseqCommand in Windows 7.
    /// </remarks>
    /// <param name="filter">The filter to check.</param>
    /// <returns>a control node that supports the IBDA_FrequencyFilter interface if successful, otherwise <c>null</c></returns>
    private IBDA_FrequencyFilter CheckPutRangeDiseqcSupport(IBaseFilter filter)
    {
      this.LogDebug("Microsoft BDA: check for IBDA_FrequencyFilter.put_Range() DiSEqC 1.0 support");

      IBDA_Topology topology = filter as IBDA_Topology;
      if (topology == null)
      {
        this.LogDebug("Microsoft BDA: filter is not a topology");
        return null;
      }

      object controlNode;
      int hr = topology.GetControlNode(0, 1, 0, out controlNode);
      IBDA_FrequencyFilter frequencyFilterInterface = controlNode as IBDA_FrequencyFilter;
      if (hr == (int)HResult.Severity.Success && frequencyFilterInterface != null)
      {
        return frequencyFilterInterface;
      }

      this.LogDebug("Microsoft BDA: failed to get the control interface, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      Release.ComObject("Microsoft DiSEqC control node", ref controlNode);
      return null;
    }

    /// <summary>
    /// Determine if a filter supports tuning annex C QAM (North American cable). This requires the ability to
    /// manually set the modulation type for the demodulator.
    /// </summary>
    /// <remarks>
    /// We need to be able to set the modulation manually to support QAM tuning on [at least] Windows XP.
    /// </remarks>
    /// <param name="filter">The filter to check.</param>
    /// <returns>a property set that supports a modulation property if successful, otherwise <c>null</c></returns>
    private IKsPropertySet CheckQamTuningSupport(IBaseFilter filter)
    {
      this.LogDebug("Microsoft BDA: check for QAM tuning support");

      IPin pin = DsFindPin.ByDirection(filter, PinDirection.Output, 0);
      if (pin == null)
      {
        this.LogError("Microsoft BDA: failed to find output pin");
        return null;
      }

      IKsPropertySet ps = pin as IKsPropertySet;
      if (ps == null)
      {
        this.LogDebug("Microsoft BDA: output pin is not a property set");
        Release.ComObject("Microsoft QAM filter output pin", ref pin);
        return null;
      }
      // Note: the below code could be problematic for single tuner/capture filter implementations. Some drivers
      // will not report whether a property set is supported unless the pin is connected. It is okay when we're
      // checking a tuner filter which has a capture filter connected, but if the tuner filter is also the capture
      // filter then the output pin(s) won't be connected yet.
      KSPropertySupport support;
      int hr = ps.QuerySupported(ModulationPropertyClass, (int)BdaDemodulatorProperty.ModulationType, out support);
      if (hr != (int)HResult.Severity.Success || !support.HasFlag(KSPropertySupport.Set))
      {
        this.LogDebug("Microsoft BDA: property set not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        Release.ComObject("Microsoft QAM property set", ref pin);
        return null;
      }

      return ps;
    }

    /// <summary>
    /// Determine if a filter supports PID filtering.
    /// </summary>
    /// <param name="filter">The filter to check.</param>
    /// <returns>an implementation of the IMPEG2PIDMap interface if successful, otherwise <c>null</c></returns>
    private IMPEG2PIDMap CheckBdaPidFilterSupport(IBaseFilter filter)
    {
      this.LogDebug("Microsoft BDA: check for IMPEG2PIDMap PID filtering support");

      IMPEG2PIDMap pidFilterInterface = filter as IMPEG2PIDMap;
      if (pidFilterInterface != null)
      {
        return pidFilterInterface;
      }

      this.LogDebug("Microsoft BDA: tuner does not implement the interface");
      return null;
    }

    #region ICustomDevice members

    /// <summary>
    /// The loading priority for this extension.
    /// </summary>
    public override byte Priority
    {
      get
      {
        // This is the most generic <see cref="ICustomDevice"/> implementation. It should only be
        // used as a last resort when more specialised interfaces are not suitable.
        return 1;
      }
    }

    /// <summary>
    /// A human-readable name for the extension. This could be a manufacturer or reseller name, or
    /// even a model name and/or number.
    /// </summary>
    public override string Name
    {
      get
      {
        if (_diseqcPropertySet != null)
        {
          return "Microsoft (BDA DiSEqC)";
        }
        if (_oldDiseqcInterface != null)
        {
          return "Microsoft (generic DiSEqC)";
        }
        if (_pidFilterInterface != null)
        {
          return "Microsoft (PID filter)";
        }
        if (_qamPropertySet != null)
        {
          return "Microsoft (generic ATSC/QAM)";
        }
        return base.Name;
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
      this.LogDebug("Microsoft BDA: initialising");

      if (_isMicrosoftBda)
      {
        this.LogWarn("Microsoft BDA: extension already initialised");
        return true;
      }
      IBaseFilter tunerFilter = context as IBaseFilter;
      if (tunerFilter == null)
      {
        this.LogDebug("Microsoft BDA: tuner filter is null");
        return false;
      }

      // First, checks for DVB-S tuners: does the tuner support sending DiSEqC commands?
      if (tunerType == CardType.DvbS)
      {
        // We prefer the IBDA_DiseqCommand interface because it has the potential to support raw commands.
        _diseqcPropertySet = CheckBdaDiseqcSupport(tunerFilter);
        if (_diseqcPropertySet != null)
        {
          this.LogInfo("Microsoft BDA: extension supported (IBDA_DiseqCommand DiSEqC)");
          _isMicrosoftBda = true;
        }
        else
        {
          // Fallback to IBDA_FrequencyFilter.put_Range().
          _oldDiseqcInterface = (IBDA_FrequencyFilter)CheckPutRangeDiseqcSupport(tunerFilter);
          if (_oldDiseqcInterface != null)
          {
            this.LogInfo("Microsoft BDA: extension supported (IBDA_FrequencyFilter.put_Range() DiSEqC)");
            _isMicrosoftBda = true;
          }
        }
      }
      // For ATSC tuners: check if clear QAM tuning is supported.
      else if (tunerType == CardType.Atsc)
      {
        _qamPropertySet = CheckQamTuningSupport(tunerFilter);
        if (_qamPropertySet != null)
        {
          this.LogInfo("Microsoft BDA: extension supported (QAM tuning)");
          _isMicrosoftBda = true;
        }
      }

      // Any type of tuner can support PID filtering.
      _pidFilterInterface = CheckBdaPidFilterSupport(tunerFilter);
      if (_pidFilterInterface != null)
      {
        this.LogInfo("Microsoft BDA: extension supported (PID filtering)");
        _isMicrosoftBda = true;
      }

      if (!_isMicrosoftBda)
      {
        this.LogDebug("Microsoft BDA: no interfaces supported");
        return false;
      }

      _deviceControl = tunerFilter as IBDA_DeviceControl;
      _paramBuffer = Marshal.AllocCoTaskMem(PARAM_BUFFER_SIZE);
      _instanceBuffer = Marshal.AllocCoTaskMem(INSTANCE_SIZE);
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
      this.LogDebug("Microsoft BDA: on before tune call back");
      action = TunerAction.Default;

      if (!_isMicrosoftBda)
      {
        this.LogWarn("Microsoft BDA: not initialised or interface not supported");
        return;
      }

      // When tuning a DVB-S channel, we need to translate the modulation value.
      DVBSChannel dvbsChannel = channel as DVBSChannel;
      if (dvbsChannel != null)
      {
        if (dvbsChannel.ModulationType == ModulationType.ModQpsk)
        {
          dvbsChannel.ModulationType = ModulationType.ModNbcQpsk;
        }
        else if (dvbsChannel.ModulationType == ModulationType.Mod8Psk)
        {
          dvbsChannel.ModulationType = ModulationType.ModNbc8Psk;
        }
        else if (dvbsChannel.ModulationType == ModulationType.ModNotSet)
        {
          dvbsChannel.ModulationType = ModulationType.ModQpsk;
        }
        this.LogDebug("  modulation = {0}", dvbsChannel.ModulationType);
      }

      // When tuning a clear QAM channel, we need to set the modulation directly for compatibility with Windows XP.
      ATSCChannel atscChannel = channel as ATSCChannel;
      if (atscChannel != null && _qamPropertySet != null)
      {
        if (atscChannel.ModulationType == ModulationType.Mod64Qam || atscChannel.ModulationType == ModulationType.Mod256Qam)
        {
          Marshal.WriteInt32(_paramBuffer, (int)atscChannel.ModulationType);
          int hr = _qamPropertySet.Set(ModulationPropertyClass, (int)BdaDemodulatorProperty.ModulationType, _instanceBuffer, INSTANCE_SIZE, _paramBuffer, sizeof(int));
          if (hr != (int)HResult.Severity.Success)
          {
            this.LogError("Microsoft BDA: failed to set QAM modulation, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          }
          else
          {
            this.LogDebug("  modulation = {0}", atscChannel.ModulationType);
          }
        }
      }
    }

    #endregion

    #endregion

    #region IMpeg2PidFilter member

    /// <summary>
    /// Should the filter be enabled for the current multiplex.
    /// </summary>
    /// <param name="tuningDetail">The current multiplex/transponder tuning parameters.</param>
    /// <returns><c>true</c> if the filter should be enabled, otherwise <c>false</c></returns>
    public bool ShouldEnableFilter(IChannel tuningDetail)
    {
      // If a tuner supports a PID filter then assume it is desirable to enable it.
      return true;
    }

    /// <summary>
    /// Disable the filter.
    /// </summary>
    /// <returns><c>true</c> if the filter is successfully disabled, otherwise <c>false</c></returns>
    public bool DisableFilter()
    {
      this.LogDebug("Microsoft BDA: disable PID filter");

      int hr = (int)HResult.Severity.Success;
      if (_pidFilterPids.Count > 0)
      {
        int[] pids = new int[_pidFilterPids.Count];
        int i = 0;
        foreach (ushort pid in _pidFilterPids)
        {
          pids[i++] = pid;
        }
        hr = _pidFilterInterface.UnmapPID(pids.Length, pids);
      }
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Microsoft BDA: result = success");
        _pidFilterPids.Clear();
        return true;
      }

      this.LogError("Microsoft BDA: failed to disable PID filter, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Get the maximum number of streams that the filter can allow.
    /// </summary>
    public int MaximumPidCount
    {
      get
      {
        return -1;  // maximum not known
      }
    }

    /// <summary>
    /// Configure the filter to allow one or more streams to pass through the filter.
    /// </summary>
    /// <param name="pids">A collection of stream identifiers.</param>
    /// <returns><c>true</c> if the filter is successfully configured, otherwise <c>false</c></returns>
    public bool AllowStreams(ICollection<ushort> pids)
    {
      this.LogDebug("Microsoft BDA: allow streams through PID filter");
      int[] pidArray = new int[pids.Count];
      int i = 0;
      foreach (ushort pid in pids)
      {
        pidArray[i++] = pid;
      }
      int hr = _pidFilterInterface.MapPID(pidArray.Length, pidArray, MediaSampleContent.ElementaryStream);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Microsoft BDA: result = success");
        _pidFilterPids.UnionWith(pids);
        return true;
      }

      this.LogError("Microsoft BDA: failed to allow streams through filter, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Configure the filter to stop one or more streams from passing through the filter.
    /// </summary>
    /// <param name="pids">A collection of stream identifiers.</param>
    /// <returns><c>true</c> if the filter is successfully configured, otherwise <c>false</c></returns>
    public bool BlockStreams(ICollection<ushort> pids)
    {
      this.LogDebug("Microsoft BDA: block streams with PID filter");
      int[] pidArray = new int[pids.Count];
      int i = 0;
      foreach (ushort pid in pids)
      {
        pidArray[i++] = pid;
      }
      int hr = _pidFilterInterface.UnmapPID(pidArray.Length, pidArray);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Microsoft BDA: result = success");
        _pidFilterPids.ExceptWith(pids);
        return true;
      }

      this.LogError("Microsoft BDA: failed to block streams with filter, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Apply the current filter configuration.
    /// </summary>
    /// <returns><c>true</c> if the filter configuration is successfully applied, otherwise <c>false</c></returns>
    public bool ApplyFilter()
    {
      // Nothing to do here.
      return true;
    }

    #endregion

    #region IDiseqcDevice members

    /// <summary>
    /// Send a tone/data burst command, and then set the 22 kHz continuous tone state.
    /// </summary>
    /// <remarks>
    /// The Microsoft interface does not support directly setting the 22 kHz tone state. The tuning
    /// request LNB frequency parameters can be used to manipulate the tone state appropriately.
    /// </remarks>
    /// <param name="toneBurstState">The tone/data burst command to send, if any.</param>
    /// <param name="tone22kState">The 22 kHz continuous tone state to set.</param>
    /// <returns><c>true</c> if the tone state is set successfully, otherwise <c>false</c></returns>
    public bool SetToneState(ToneBurst toneBurstState, Tone22k tone22kState)
    {
      this.LogDebug("Microsoft BDA: set tone state, burst = {0}, 22 kHz = {1}", toneBurstState, tone22kState);

      if (!_isMicrosoftBda)
      {
        this.LogWarn("Microsoft BDA: not initialised or interface not supported");
        return false;
      }
      if (_diseqcPropertySet == null)
      {
        this.LogDebug("Microsoft BDA: the interface does not support setting the tone state");
        return false;
      }

      _useToneBurst = toneBurstState != ToneBurst.None;
      this.LogDebug("Microsoft BDA: result = success");
      return true;
    }

    /// <summary>
    /// Send an arbitrary DiSEqC command.
    /// </summary>
    /// <remarks>
    /// Drivers don't all behave the same. There are notes about MS network providers messing up the put_Range()
    /// method when attempting to send commands before the tune request (http://www.dvbdream.org/forum/viewtopic.php?f=1&t=608&start=15).
    /// In practise, I have observed the following behaviour with the tuners that I have tested:
    ///
    /// Must send commands before tuning
    ///----------------------------------
    /// - Anysee (E7 S2 - IBDA_DiseqCommand)
    /// - Pinnacle (PCTV 7010ix - IBDA_DiseqCommand)
    /// - TechniSat SkyStar HD2 (IBDA_DiseqCommand)
    /// - AVerMedia (Satellite Trinity - IBDA_DiseqCommand)
    ///
    /// Must send commands after tuning
    ///---------------------------------
    /// - TBS (5980 CI - IBDA_DiseqCommand)
    ///
    /// Doesn't matter
    ///----------------
    /// - Hauppauge (HVR-4400 - IBDA_DiseqCommand)
    /// - TechniSat SkyStar 2 r2.6d BDA driver (IBDA_DiseqCommand)
    /// - TeVii (S480 - IBDA_DiseqCommand)
    /// - Digital Everywhere (FloppyDTV S2 - IBDA_DiseqCommand)
    /// - TechnoTrend (Budget S2-3200 - IBDA_FrequencyFilter)
    /// 
    /// Since the list for "before" is longer than the list for "after" (and because we have specific DiSEqC
    /// support for Turbosight but not for AVerMedia and Pinnacle), we send commands before the tune request.
    /// </remarks>
    /// <param name="command">The command to send.</param>
    /// <returns><c>true</c> if the command is sent successfully, otherwise <c>false</c></returns>
    public bool SendCommand(byte[] command)
    {
      this.LogDebug("Microsoft BDA: send DiSEqC command");

      if (!_isMicrosoftBda || _deviceControl == null || (_diseqcPropertySet == null && _oldDiseqcInterface == null))
      {
        this.LogWarn("Microsoft BDA: not initialised or interface not supported");
        return false;
      }
      if (command == null || command.Length == 0)
      {
        this.LogError("Microsoft BDA: command not supplied");
        return true;
      }
      if (command.Length > MAX_DISEQC_MESSAGE_LENGTH)
      {
        this.LogError("Microsoft BDA: command too long, length = {0}", command.Length);
        return false;
      }

      // Attempt to translate the raw command back into a DiSEqC 1.0 command. The old interface only supports
      // DiSEqC 1.0 switch commands, and some drivers don't implement support for raw commands using the
      // IBDA_DiseqCommand interface (so we want to use the simpler LNB source property if possible).
      int portNumber = -1;
      if (command.Length == 4 &&
        (command[0] == (byte)DiseqcFrame.CommandFirstTransmissionNoReply ||
        command[0] == (byte)DiseqcFrame.CommandRepeatTransmissionNoReply) &&
        command[1] == (byte)DiseqcAddress.AnySwitch &&
        command[2] == (byte)DiseqcCommand.WriteN0)
      {
        portNumber = (command[3] & 0xc) >> 2;
        this.LogDebug("Microsoft BDA: DiSEqC 1.0 command recognised for port {0}", portNumber);
      }
      if (_oldDiseqcInterface != null && portNumber == -1)
      {
        this.LogError("Microsoft BDA: command not supported");
        return false;
      }

      // If we get to here, then we're going to attempt to send a command.
      bool success = true;
      int hr = _deviceControl.StartChanges();
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogError("Microsoft BDA: failed to start device control changes, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        success = false;
      }

      // IBDA_DiseqCommand interface
      if (_diseqcPropertySet != null && success)
      {
        // This property has to be set for each command sent for some tuners (eg. TBS).
        Marshal.WriteInt32(_paramBuffer, 0, 1);
        hr = _diseqcPropertySet.Set(typeof(IBDA_DiseqCommand).GUID, (int)BdaDiseqcProperty.Enable, _instanceBuffer, INSTANCE_SIZE, _paramBuffer, sizeof(int));
        if (hr != (int)HResult.Severity.Success)
        {
          this.LogError("Microsoft BDA: failed to enable DiSEqC commands, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          success = false;
        }

        // Disable command repeats for optimal performance. We set this for each command for "safety",
        // assuming that if DiSEqC must be enabled for each command then the same may apply to repeats.
        if (success)
        {
          Marshal.WriteInt32(_paramBuffer, 0, 0);
          hr = _diseqcPropertySet.Set(typeof(IBDA_DiseqCommand).GUID, (int)BdaDiseqcProperty.Repeats, _instanceBuffer, INSTANCE_SIZE, _paramBuffer, sizeof(int));
          if (hr != (int)HResult.Severity.Success)
          {
            this.LogError("Microsoft BDA: failed to disable DiSEqC command repeats, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
            success = false;
          }
        }

        // Disable tone burst messages - it seems that many drivers don't support them, and setting the correct
        // tone state is inconvenient with the IBDA_DiseqCommand implementation.
        if (success)
        {
          Marshal.WriteInt32(_paramBuffer, 0, 0);
          hr = _diseqcPropertySet.Set(typeof(IBDA_DiseqCommand).GUID, (int)BdaDiseqcProperty.UseToneBurst, _instanceBuffer, INSTANCE_SIZE, _paramBuffer, sizeof(int));
          if (hr != (int)HResult.Severity.Success)
          {
            this.LogError("Microsoft BDA: failed to disable tone burst commands, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
            success = false;
          }
        }

        if (success)
        {
          LNB_Source lnbSource = (LNB_Source)portNumber++;
          if (lnbSource != LNB_Source.NOT_SET)
          {
            Marshal.WriteInt32(_paramBuffer, 0, (int)lnbSource);
            hr = _diseqcPropertySet.Set(typeof(IBDA_DiseqCommand).GUID, (int)BdaDiseqcProperty.LnbSource, _instanceBuffer, INSTANCE_SIZE, _paramBuffer, sizeof(int));
            if (hr != (int)HResult.Severity.Success)
            {
              this.LogError("Microsoft BDA: failed to set LNB source, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
              success = false;
            }
          }
          else
          {
            BdaDiseqcMessage message = new BdaDiseqcMessage();
            message.RequestId = _requestId++;
            message.PacketLength = (uint)command.Length;
            message.PacketData = new byte[MAX_DISEQC_MESSAGE_LENGTH];
            Buffer.BlockCopy(command, 0, message.PacketData, 0, command.Length);
            Marshal.StructureToPtr(message, _paramBuffer, true);
            //Dump.DumpBinary(_paramBuffer, BDA_DISEQC_MESSAGE_SIZE);
            hr = _diseqcPropertySet.Set(typeof(IBDA_DiseqCommand).GUID, (int)BdaDiseqcProperty.Send, _instanceBuffer, INSTANCE_SIZE, _paramBuffer, BDA_DISEQC_MESSAGE_SIZE);
            if (hr != (int)HResult.Severity.Success)
            {
              this.LogError("Microsoft BDA: failed to send DiSEqC command, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
              success = false;
            }
          }
        }
      }
      // IBDA_FrequencyFilter interface
      else if (_oldDiseqcInterface != null && success)
      {
        // The two rightmost bytes encode option and position respectively.
        if (portNumber > 1)
        {
          portNumber -= 2;
          portNumber |= 0x100;
        }
        this.LogDebug("Microsoft BDA: range = 0x{0:x4}", portNumber);
        hr = _oldDiseqcInterface.put_Range((ulong)portNumber);
        if (hr != (int)HResult.Severity.Success)
        {
          this.LogError("Microsoft BDA: failed to put range, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          success = false;
        }
      }

      // Finalise (send) the command.
      if (success)
      {
        hr = _deviceControl.CheckChanges();
        if (hr != (int)HResult.Severity.Success)
        {
          this.LogError("Microsoft BDA: failed to check device control changes, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          success = false;
        }
      }
      if (success)
      {
        hr = _deviceControl.CommitChanges();
        if (hr != (int)HResult.Severity.Success)
        {
          this.LogError("Microsoft BDA: failed to commit device control changes, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          success = false;
        }
      }

      this.LogDebug("Microsoft BDA: result = {0}", success);
      return success;
    }

    /// <summary>
    /// Retrieve the response to a previously sent DiSEqC command (or alternatively, check for a command
    /// intended for this tuner).
    /// </summary>
    /// <param name="response">The response (or command).</param>
    /// <returns><c>true</c> if the response is read successfully, otherwise <c>false</c></returns>
    public bool ReadResponse(out byte[] response)
    {
      this.LogDebug("Microsoft BDA: read DiSEqC response");
      response = null;

      if (!_isMicrosoftBda)
      {
        this.LogWarn("Microsoft BDA: not initialised or interface not supported");
        return false;
      }
      if (_diseqcPropertySet == null)
      {
        this.LogError("Microsoft BDA: the interface does not support reading DiSEqC responses");
        return false;
      }

      for (int i = 0; i < BDA_DISEQC_MESSAGE_SIZE; i++)
      {
        Marshal.WriteInt32(_paramBuffer, 0, 0);
      }
      int returnedByteCount;
      int hr = _diseqcPropertySet.Get(typeof(IBDA_DiseqCommand).GUID, (int)BdaDiseqcProperty.Response, _paramBuffer, INSTANCE_SIZE, _paramBuffer, BDA_DISEQC_MESSAGE_SIZE, out returnedByteCount);
      if (hr == (int)HResult.Severity.Success && returnedByteCount == BDA_DISEQC_MESSAGE_SIZE)
      {
        // Copy the response into the return array.
        BdaDiseqcMessage message = (BdaDiseqcMessage)Marshal.PtrToStructure(_paramBuffer, typeof(BdaDiseqcMessage));
        if (message.PacketLength > MAX_DISEQC_MESSAGE_LENGTH)
        {
          this.LogError("Microsoft BDA: response length is out of bounds, response length = {0}", message.PacketLength);
          return false;
        }
        this.LogDebug("Microsoft BDA: result = success");
        response = new byte[message.PacketLength];
        Buffer.BlockCopy(message.PacketData, 0, response, 0, (int)message.PacketLength);
        return true;
      }

      this.LogError("Microsoft BDA: failed to read DiSEqC response, response length = {0}, hr = 0x{1:x} ({2})", returnedByteCount, hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      Release.ComObject("Microsoft DiSEqC property set", ref _diseqcPropertySet);
      Release.ComObject("Microsoft old DiSEqC interface", ref _oldDiseqcInterface);
      _deviceControl = null;
      Release.ComObject("Microsoft QAM property set", ref _qamPropertySet);
      if (_instanceBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_instanceBuffer);
        _instanceBuffer = IntPtr.Zero;
      }
      if (_paramBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_paramBuffer);
        _paramBuffer = IntPtr.Zero;
      }
      _isMicrosoftBda = false;
    }

    #endregion
  }
}