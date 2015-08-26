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
using Mediaportal.TV.Server.Common.Types.Enum;
using Mediaportal.TV.Server.Plugins.Base.Interfaces;
using Mediaportal.TV.Server.Plugins.TunerExtension.DirecTvShef.Config;
using Mediaportal.TV.Server.Plugins.TunerExtension.DirecTvShef.Service;
using Mediaportal.TV.Server.Plugins.TunerExtension.DirecTvShef.Shef;
using Mediaportal.TV.Server.Plugins.TunerExtension.DirecTvShef.Shef.Request;
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.TVControl.Interfaces.Services;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Channel;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channel;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Tuner;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension.Enum;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.DirecTvShef
{
  /// <summary>
  /// A class for handling DirecTV set top box control using the Set top box
  /// HTTP Exported Functionality (SHEF) protocol.
  /// </summary>
  public class DirecTvShef : BaseTunerExtension, IDisposable, IPowerDevice, ITvServerPlugin, ITvServerPluginCommunication
  {
    #region variables

    private static bool _isPluginEnabled = false;
    private static DirecTvShefConfigService _service = new DirecTvShefConfigService();

    private bool _isDirecTvShef = false;
    private string _tunerExternalId = null;

    private object _configLock = new object();
    private ShefClient _shefClient = null;
    private SetTopBoxConfig _config = null;

    #endregion

    private void OnSetTopBoxConfigChange(SetTopBoxConfig config)
    {
      if (string.Equals(_tunerExternalId, config.TunerExternalId))
      {
        this.LogDebug("DirecTV SHEF: config change, tuner external ID = {0}", _tunerExternalId);
        UpdateConfig(config);
      }
    }

    private void UpdateConfig(SetTopBoxConfig config)
    {
      lock (_configLock)
      {
        if (string.IsNullOrEmpty(config.IpAddress))
        {
          _shefClient = null;
        }
        else
        {
          _shefClient = new ShefClient(config.IpAddress);
        }
        _config = config;
      }
      this.LogDebug("  IP address    = {0}", config.IpAddress ?? "[null]");
      this.LogDebug("  power control = {0}", config.EnablePowerControl);
    }

    #region ITunerExtension members

    /// <summary>
    /// A human-readable name for the extension.
    /// </summary>
    public override string Name
    {
      get
      {
        return "DirecTV SHEF";
      }
    }

    /// <summary>
    /// Does the extension control some part of the tuner hardware?
    /// </summary>
    public override bool ControlsTunerHardware
    {
      get
      {
        return false;
      }
    }

    /// <summary>
    /// Attempt to initialise the interfaces used by the extension.
    /// </summary>
    /// <param name="tunerExternalId">The external identifier for the tuner.</param>
    /// <param name="tunerSupportedBroadcastStandards">The broadcast standards supported by the tuner (eg. DVB-T, DVB-T2... etc.).</param>
    /// <param name="context">Context required to initialise the interfaces.</param>
    /// <returns><c>true</c> if the interfaces are successfully initialised, otherwise <c>false</c></returns>
    public override bool Initialise(string tunerExternalId, BroadcastStandard tunerSupportedBroadcastStandards, object context)
    {
      this.LogDebug("DirecTV SHEF: initialising");

      if (_isDirecTvShef)
      {
        this.LogWarn("DirecTV SHEF: extension already initialised");
        return true;
      }
      if (string.IsNullOrEmpty(tunerExternalId))
      {
        this.LogDebug("DirecTV SHEF: tuner external identifier is not set");
        return false;
      }
      if (!tunerSupportedBroadcastStandards.HasFlag(BroadcastStandard.AnalogTelevision) && !tunerSupportedBroadcastStandards.HasFlag(BroadcastStandard.ExternalInput))
      {
        this.LogDebug("DirecTV SHEF: tuner type not supported");
        return false;
      }

      this.LogInfo("DirecTV SHEF: extension supported");
      _isDirecTvShef = true;
      _tunerExternalId = tunerExternalId;
      UpdateConfig(SetTopBoxConfig.LoadSettings(_tunerExternalId));
      _service.OnConfigChange += OnSetTopBoxConfigChange;
      return true;
    }

    #region tuner state change call backs

    /// <summary>
    /// This call back is invoked before a tune request is assembled.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="currentChannel">The channel that the tuner is currently tuned to..</param>
    /// <param name="channel">The channel that the tuner will been tuned to.</param>
    /// <param name="action">The action to take, if any.</param>
    public override void OnBeforeTune(ITuner tuner, IChannel currentChannel, ref IChannel channel, out TunerAction action)
    {
      this.LogDebug("DirecTV SHEF: on before tune call back");
      action = TunerAction.Default;

      if (!_isDirecTvShef)
      {
        this.LogWarn("DirecTV SHEF: not initialised or interface not supported");
        return;
      }
      if (!_isPluginEnabled)
      {
        return;
      }
      if (!(channel is ChannelAnalogTv) && !(channel is ChannelCapture))
      {
        this.LogDebug("DirecTV SHEF: not tuning a capture channel");
        return;
      }
      int logicalChannelNumber;
      if (!int.TryParse(channel.LogicalChannelNumber, out logicalChannelNumber) || logicalChannelNumber < 1)
      {
        this.LogError("DirecTV SHEF: invalid channel number, channel = {0}, number = {1}", channel.Name, channel.LogicalChannelNumber);
        return;
      }

      lock (_configLock)
      {
        if (_shefClient == null)
        {
          this.LogDebug("DirecTV SHEF: set top box IP address not set");
          return;
        }

        this.LogDebug("DirecTV SHEF: change channel, set top box = {0}, channel = {1}", _config.IpAddress, logicalChannelNumber);
        if (!_shefClient.SendRequest(new ShefRequestTune(logicalChannelNumber)))
        {
          this.LogError("DirecTV SHEF: failed to change channel, set top box = {0}, channel = {1}", _config.IpAddress, logicalChannelNumber);
        }
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
      this.LogDebug("DirecTV SHEF: set power state, state = {0}", state);

      if (!_isDirecTvShef)
      {
        this.LogWarn("DirecTV SHEF: not initialised or interface not supported");
        return false;
      }
      if (!_isPluginEnabled)
      {
        return true;
      }

      lock (_configLock)
      {
        if (_shefClient == null)
        {
          this.LogDebug("DirecTV SHEF: set top box IP address not set");
          return true;
        }
        if (!_config.EnablePowerControl)
        {
          this.LogDebug("DirecTV SHEF: power control disabled");
          return true;
        }

        ShefRemoteKey key;
        if (state == PowerState.On)
        {
          key = ShefRemoteKey.PowerOn;
        }
        else
        {
          key = ShefRemoteKey.PowerOff;
        }
        if (!_shefClient.SendRequest(new ShefRequestProcessKey(key)))
        {
          this.LogError("DirecTV SHEF: failed to set power state, set top box = {0}", _config.IpAddress);
          return false;
        }

        this.LogDebug("DirecTV SHEF: result = success");
        return true;
      }
    }

    #endregion

    #region ITvServerPlugin members

    /// <summary>
    /// The version of this TV Server plugin.
    /// </summary>
    public string Version
    {
      get
      {
        return "1.0.0.0";
      }
    }

    /// <summary>
    /// The author of this TV Server plugin.
    /// </summary>
    public string Author
    {
      get
      {
        return "mm1352000";
      }
    }

    /// <summary>
    /// Get an instance of the configuration section for use in TV Server configuration (SetupTv).
    /// </summary>
    public SectionSettings Setup
    {
      get
      {
        return new DirecTvShefConfig();
      }
    }

    /// <summary>
    /// Start this TV Server plugin.
    /// </summary>
    public void Start(IInternalControllerService controllerService)
    {
      this.LogDebug("DirecTV SHEF: plugin enabled");
      _isPluginEnabled = true;
    }

    /// <summary>
    /// Stop this TV Server plugin.
    /// </summary>
    public void Stop()
    {
      this.LogDebug("DirecTV SHEF: plugin disabled");
      _isPluginEnabled = false;
    }

    #endregion

    #region ITvServerPluginCommunication members

    /// <summary>
    /// Supply a service class implementation for client-server plugin communication.
    /// </summary>
    public object GetServiceInstance
    {
      get
      {
        return _service;
      }
    }

    /// <summary>
    /// Supply a service class interface for client-server plugin communication.
    /// </summary>
    public Type GetServiceInterfaceForContractType
    {
      get
      {
        return typeof(IDirecTvShefConfigService);
      }
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    ~DirecTvShef()
    {
      Dispose(false);
    }

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    /// <param name="isDisposing"><c>True</c> if the instance is being disposed.</param>
    private void Dispose(bool isDisposing)
    {
      if (isDisposing && _isDirecTvShef)
      {
        _service.OnConfigChange -= OnSetTopBoxConfigChange;
        _isDirecTvShef = false;
      }
    }

    #endregion
  }
}