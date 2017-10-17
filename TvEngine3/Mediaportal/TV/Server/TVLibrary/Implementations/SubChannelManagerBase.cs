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
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Channel;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Exception;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Tuner;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.TVLibrary.Implementations
{
  internal abstract class SubChannelManagerBase : ISubChannelManager
  {
    #region variables

    /// <summary>
    /// The manager's sub-channels.
    /// </summary>
    private Dictionary<int, ISubChannelInternal> _subChannels = new Dictionary<int, ISubChannelInternal>();

    /// <summary>
    /// The maximum time to wait for implementation-dependent stream
    /// information (eg. PAT, PMT and CAT) to be received during tuning.
    /// </summary>
    private TimeSpan _timeLimitReceiveStreamInfo = new TimeSpan(0, 0, 5);

    /// <summary>
    /// The tuner's quality control interface.
    /// </summary>
    private IQualityControlInternal _qualityControlInterface = null;

    /// <summary>
    /// Is electronic programme guide data grabbing enabled?
    /// </summary>
    private bool _isEpgGrabbingEnabled = false;

    /// <summary>
    /// Should the current tuning process be aborted immediately?
    /// </summary>
    private volatile bool _cancelTune = false;

    #endregion

    #region ISubChannelManager members

    /// <summary>
    /// Reload the manager's configuration.
    /// </summary>
    /// <param name="configuration">The tuner's configuration.</param>
    public virtual void ReloadConfiguration(Tuner configuration)
    {
      _timeLimitReceiveStreamInfo = new TimeSpan(0, 0, 0, 0, SettingsManagement.GetValue("timeLimitReceiveStreamInfo", 5000));
      if (_qualityControlInterface != null)
      {
        _qualityControlInterface.ReloadConfiguration(configuration);
      }
    }

    /// <summary>
    /// Set the manager's extensions.
    /// </summary>
    /// <param name="extensions">A list of the tuner's extensions, in priority order.</param>
    public virtual void SetExtensions(IList<ITunerExtension> extensions)
    {
      List<ITunerExtension> encoders = new List<ITunerExtension>(extensions.Count);
      foreach (ITunerExtension extension in extensions)
      {
        IEncoder encoder = extension as IEncoder;
        if (encoder != null)
        {
          this.LogDebug("sub-channel manager base: found encoder control interface \"{0}\"", extension.Name);
          encoders.Add(encoder);
        }
      }

      if (encoders.Count > 0)
      {
        _qualityControlInterface = new EncoderController(encoders);
      }
    }

    /// <summary>
    /// Get the manager's quality control interface.
    /// </summary>
    public virtual IQualityControlInternal QualityControlInterface
    {
      get
      {
        return _qualityControlInterface;
      }
    }

    /// <summary>
    /// Enable or disable electronic programme guide data grabbing.
    /// </summary>
    public virtual bool IsEpgGrabbingEnabled
    {
      get
      {
        return _isEpgGrabbingEnabled;
      }
      set
      {
        _isEpgGrabbingEnabled = value;
      }
    }

    /// <summary>
    /// Decompose the sub-channel manager.
    /// </summary>
    public void Decompose()
    {
      this.LogDebug("sub-channel manager base: decompose, sub-channel count = {0}", _subChannels.Count);
      foreach (var subChannel in _subChannels.Values)
      {
        subChannel.Decompose();
      }
      _subChannels.Clear();

      OnDecompose();
    }

    #region tuning

    /// <summary>
    /// This function should be called before the tuner is tuned to a new
    /// transmitter.
    /// </summary>
    public virtual void OnBeforeTune()
    {
      if (_subChannels.Count > 1)
      {
        throw new TvException("Tune attempt with more than one active sub-channel.");
      }
      foreach (ISubChannelInternal subChannel in _subChannels.Values)
      {
        subChannel.OnBeforeTune();
      }
    }

    /// <summary>
    /// Tune a sub-channel.
    /// </summary>
    /// <param name="id">The sub-channel's identifier.</param>
    /// <param name="channel">The channel to tune to.</param>
    /// <returns>the sub-channel</returns>
    public ISubChannel Tune(int id, IChannel channel)
    {
      _cancelTune = false;
      bool isNew = false;
      ISubChannelInternal subChannel = null;
      if (_subChannels.TryGetValue(id, out subChannel) && subChannel != null)
      {
        this.LogInfo("sub-channel manager base: using existing sub-channel, ID = {0}, count = {1}", id, _subChannels.Count);
      }
      else
      {
        this.LogInfo("sub-channel manager base: create new sub-channel, ID = {0}, count = {1}", id, _subChannels.Count);
        isNew = true;
      }

      subChannel = OnTune(id, channel, _timeLimitReceiveStreamInfo);
      if (isNew && subChannel != null)
      {
        subChannel.QualityControlInterface = QualityControlInterface;
        _subChannels[id] = subChannel;
      }
      return subChannel;
    }

    /// <summary>
    /// Cancel the current tuning process.
    /// </summary>
    /// <param name="id">The identifier of the sub-channel associated with the tuning process that is being cancelled.</param>
    public void CancelTune(int id)
    {
      _cancelTune = true;
      ISubChannelInternal subChannel;
      if (_subChannels.TryGetValue(id, out subChannel) && subChannel != null)
      {
        subChannel.CancelTune();
      }
    }

    #endregion

    #region sub-channels

    /// <summary>
    /// Get a sub-channel.
    /// </summary>
    /// <param name="id">The sub-channel's identifier.</param>
    /// <returns>the sub-channel if it exists, otherwise <c>null</c></returns>
    public ISubChannel GetSubChannel(int id)
    {
      ISubChannelInternal subChannel = null;
      _subChannels.TryGetValue(id, out subChannel);
      return subChannel;
    }

    /// <summary>
    /// Free a sub-channel.
    /// </summary>
    /// <param name="id">The sub-channel's identifier.</param>
    public void FreeSubChannel(int id)
    {
      this.LogDebug("sub-channel manager base: free sub-channel, ID = {0}, count = {1}", id, _subChannels.Count);
      ISubChannelInternal subChannel;
      if (!_subChannels.TryGetValue(id, out subChannel))
      {
        this.LogWarn("sub-channel manager base: sub-channel to free not found, ID = {0}", id);
        return;
      }

      if (subChannel.IsTimeShifting)
      {
        throw new TvException("Asked to free sub-channel {0}, still time-shifting.", id);
      }
      if (subChannel.IsRecording)
      {
        throw new TvException("Asked to free sub-channel {0}, still recording.", id);
      }

      OnFreeSubChannel(id);
      subChannel.Decompose();
      _subChannels.Remove(id);
    }

    /// <summary>
    /// Get the count of sub-channels.
    /// </summary>
    public int SubChannelCount
    {
      get
      {
        return _subChannels.Count;
      }
    }

    /// <summary>
    /// Get the set of sub-channel identifiers for each channel the tuner is
    /// currently decrypting.
    /// </summary>
    /// <returns>a collection of sub-channel identifier lists</returns>
    public abstract ICollection<IList<int>> GetDecryptedSubChannelDetails();

    /// <summary>
    /// Determine whether a sub-channel is being decrypted.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><c>true</c> if the sub-channel is being decrypted, otherwise <c>false</c></returns>
    public abstract bool IsDecrypting(IChannel channel);

    #endregion

    #endregion

    #region protected members

    #region abstract members

    /// <summary>
    /// Tune a sub-channel.
    /// </summary>
    /// <param name="id">The sub-channel's identifier.</param>
    /// <param name="channel">The channel to tune to.</param>
    /// <param name="timeLimitReceiveStreamInfo">The maximum time to wait for required implementation-dependent stream information during tuning.</param>
    /// <returns>the sub-channel</returns>
    protected abstract ISubChannelInternal OnTune(int id, IChannel channel, TimeSpan timeLimitReceiveStreamInfo);

    /// <summary>
    /// Free a sub-channel.
    /// </summary>
    /// <param name="id">The sub-channel's identifier.</param>
    protected abstract void OnFreeSubChannel(int id);

    /// <summary>
    /// Decompose the sub-channel manager.
    /// </summary>
    protected abstract void OnDecompose();

    #endregion

    /// <summary>
    /// Check if the current tuning process has been cancelled and throw an
    /// exception if it has.
    /// </summary>
    protected void ThrowExceptionIfTuneCancelled()
    {
      if (_cancelTune)
      {
        throw new TvExceptionTuneCancelled();
      }
    }

    #endregion
  }
}