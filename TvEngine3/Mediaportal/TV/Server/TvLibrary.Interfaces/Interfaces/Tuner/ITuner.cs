#region Copyright (C) 2005-2011 Team MediaPortal

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
using Mediaportal.TV.Server.Common.Types.Enum;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Channel;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Tuner.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.TVLibrary.Interfaces.Tuner
{
  #region event delegates

  /// <summary>
  /// Delegate for the new sub-channel event.
  /// </summary>
  /// <param name="subChannelId">The ID of the new sub-channel.</param>
  public delegate void OnNewSubChannelDelegate(int subChannelId);

  /// <summary>
  /// Delegate for the after tune event.
  /// </summary>
  public delegate void OnAfterTuneDelegate();

  #endregion

  /// <summary>
  /// interface for a tv card
  /// </summary>
  public interface ITuner : IDisposable
  {
    #region events

    /// <summary>
    /// Set the tuner's new sub-channel event handler.
    /// </summary>
    event OnNewSubChannelDelegate OnNewSubChannelEvent;

    /// <summary>
    /// Set the tuner's after tune event handler.
    /// </summary>
    event OnAfterTuneDelegate OnAfterTuneEvent;

    #endregion

    #region properties

    /// <summary>
    /// Get the tuner's name.
    /// </summary>
    string Name
    {
      get;
    }

    /// <summary>
    /// Get the tuner's group.
    /// </summary>
    ITunerGroup Group
    {
      get;
    }

    /// <summary>
    /// Get the tuner's unique identifier.
    /// </summary>
    /// <remarks>
    /// This is the TV Engine's unique internal identifier.
    /// </remarks>
    int TunerId
    {
      get;
    }

    /// <summary>
    /// Get the tuner's unique external identifier.
    /// </summary>		
    string ExternalId
    {
      get;
    }

    /// <summary>
    /// Get the tuner's product instance identifier.
    /// </summary>
    /// <remarks>
    /// The product instance identifier is a shared identifier for all tuner instances derived from a [multi-tuner] product.
    /// </remarks>
    string ProductInstanceId
    {
      get;
    }

    /// <summary>
    /// Get the tuner's instance identifier.
    /// </summary>
    /// <remarks>
    /// The tuner instance identifier is a shared identifier for all tuner instances derived from a single physical tuner.
    /// </remarks>
    string TunerInstanceId
    {
      get;
    }

    /// <summary>
    /// Get the state of the tuner's enable/disable setting.
    /// </summary>
    bool IsEnabled
    {
      get;
    }

    /// <summary>
    /// Get the set of conditional access providers that the tuner's
    /// conditional access interface is able to decrypt.
    /// </summary>
    ICollection<string> ConditionalAccessProviders
    {
      get;
    }

    /// <summary>
    /// Get the type of conditional access module available to the tuner's
    /// conditional access interface.
    /// </summary>
    CamType CamType
    {
      get;
    }

    /// <summary>
    /// Get the broadcast standards supported by the tuner hardware.
    /// </summary>
    /// <remarks>
    /// This property is configurable.
    /// </remarks>
    BroadcastStandard SupportedBroadcastStandards
    {
      get;
    }

    /// <summary>
    /// Get the broadcast standards supported by the tuner code/class/type implementation.
    /// </summary>
    /// <remarks>
    /// This property is based on detected limitations and hard code capabilities.
    /// </remarks>
    BroadcastStandard PossibleBroadcastStandards
    {
      get;
    }

    /// <summary>
    /// Does the tuner support conditional access?
    /// </summary>
    /// <value><c>true</c> if the tuner supports conditional access, otherwise <c>false</c></value>
    bool IsConditionalAccessSupported
    {
      get;
    }

    /// <summary>
    /// Get a count of the number of services that the tuner is currently decrypting.
    /// </summary>
    /// <value>The number of services currently being decrypted.</value>
    int NumberOfChannelsDecrypting { get; }

    /// <summary>
    /// Get the tuner's conditional access interface decrypt limit.
    /// </summary>
    /// <remarks>
    /// This is the number of channels that the interface is able to decrypt
    /// simultaneously.
    /// </remarks>
    int DecryptLimit
    {
      get;
    }

    /// <summary>
    /// Get or set external context to associate with the tuner.
    /// </summary>
    object Context
    {
      get;
      set;
    }

    /// <summary>
    /// Get the tuning parameters that have been applied to the hardware.
    /// </summary>
    /// <remarks>
    /// This property returns null when the device is not in use.
    /// </remarks>
    IChannel CurrentTuningDetail
    {
      get;
    }

    /// <summary>
    /// Does configuration allow the tuner to be used for EPG grabbing?
    /// </summary>
    /// <value><c>true</c> if the tuner configuration allows EPG grabbing, otherwise <c>false</c></value>
    bool IsEpgGrabbingAllowed
    {
      get;
    }

    #endregion

    #region interfaces

    /// <summary>
    /// Get the tuner's channel linkage scanning interface.
    /// </summary>
    /// <remarks>
    /// This interface is only applicable for DVB tuners.
    /// </remarks>
    /// <value><c>null</c> if the tuner does not support channel linkage scanning</value>
    IChannelLinkageScanner ChannelLinkageScanningInterface
    {
      get;
    }

    /// <summary>
    /// Get the tuner's channel scanning interface.
    /// </summary>
    IChannelScanner ChannelScanningInterface
    {
      get;
    }

    /// <summary>
    /// Get the tuner's DiSEqC control interface.
    /// </summary>
    /// <remarks>
    /// This interface is only applicable for satellite tuners. It is used for
    /// controlling switch, positioner and LNB settings.
    /// </remarks>
    /// <value><c>null</c> if the tuner is not a satellite tuner or the tuner does not support sending/receiving DiSEqC commands</value>
    IDiseqcController DiseqcController
    {
      get;
    }

    /// <summary>
    /// Get the tuner's electronic programme guide data grabbing interface.
    /// </summary>
    /// <value><c>null</c> if the tuner does not support EPG grabbing</value>
    IEpgGrabber EpgGrabberInterface
    {
      get;
    }

    /// <summary>
    /// Get the tuner's conditional access menu interaction interface.
    /// </summary>
    /// <remarks>
    /// This interface is only applicable if conditional access is supported.
    /// </remarks>
    /// <value><c>null</c> if the tuner does not support conditional access menu interaction</value>
    IConditionalAccessMenuActions CaMenuInterface
    {
      get;
    }

    /// <summary>
    /// Get the tuner's quality control (encoder) interface.
    /// </summary>
    /// <value><c>null</c> if the tuner does not support quality control</value>
    IQuality QualityControlInterface
    {
      get;
    }

    #endregion

    #region tuning

    /// <summary>
    /// Check if the tuner can tune to a specific channel.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><c>true</c> if the tuner can tune to the channel, otherwise <c>false</c></returns>
    bool CanTune(IChannel channel);

    /// <summary>
    /// Tune to a specific channel.
    /// </summary>
    /// <param name="subChannelId">The ID of the sub-channel associated with the channel that is being tuned.</param>
    /// <param name="channel">The channel to tune to.</param>
    /// <returns>the sub-channel associated with the tuned channel</returns>
    ISubChannel Tune(int subChannelId, IChannel channel);

    /// <summary>
    /// Cancel the current tuning process.
    /// </summary>
    /// <param name="subChannelId">The ID of the sub-channel associated with the channel that is being cancelled.</param>
    void CancelTune(int subChannelId);

    #endregion

    #region sub-channels

    /// <summary>
    /// Gets the sub-channel.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns></returns>
    ISubChannel GetSubChannel(int id);

    /// <summary>
    /// Frees the sub-channel.
    /// </summary>
    /// <param name="id">The id.</param>
    void FreeSubChannel(int id);

    /// <summary>
    /// Gets the sub-channels.
    /// </summary>
    /// <value>The sub-channels.</value>
    ISubChannel[] SubChannels { get; }

    #endregion

    /// <summary>
    /// Reload the tuner's configuration.
    /// </summary>
    void ReloadConfiguration();

    /// <summary>
    /// Stop the tuner.
    /// </summary>
    /// <remarks>
    /// The actual result of this function depends on tuner configuration.
    /// </remarks>
    [Obsolete("This function should not be used. Instead use FreeSubChannel() to free each remaining sub-channel. The tuner will be stopped after the last sub-channel is freed... but that is implementation detail which you should not have to care about.")]
    void Stop();

    /// <summary>
    /// Get the tuner's signal status information.
    /// </summary>
    /// <param name="forceUpdate"><c>True</c> to force the signal status to be updated, and not use cached information.</param>
    /// <param name="isLocked"><c>True</c> if the tuner has locked onto signal.</param>
    /// <param name="isPresent"><c>True</c> if the tuner has detected signal.</param>
    /// <param name="strength">An indication of signal strength. Range: 0 to 100.</param>
    /// <param name="quality">An indication of signal quality. Range: 0 to 100.</param>
    void GetSignalStatus(bool forceUpdate, out bool isLocked, out bool isPresent, out int strength, out int quality);
  }
}