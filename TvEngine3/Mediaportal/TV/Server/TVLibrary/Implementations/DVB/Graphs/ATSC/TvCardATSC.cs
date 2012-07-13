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
using System.Linq;
using DirectShowLib;
using DirectShowLib.BDA;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Implementations.Helper;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Epg;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;

namespace Mediaportal.TV.Server.TVLibrary.Implementations.DVB.Graphs.ATSC
{
  /// <summary>
  /// Implementation of <see cref="T:TvLibrary.Interfaces.ITVCard"/> which handles ATSC BDA cards
  /// </summary>
  public class TvCardATSC : TvCardDvbBase
  {
    #region variables

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="TvCardATSC"/> class.
    /// </summary>
    /// <param name="epgEvents">The EPG events interface.</param>
    /// <param name="device">The device.</param>
    public TvCardATSC(IEpgEvents epgEvents, DsDevice device)
      : base(epgEvents, device)
    {
      _cardType = CardType.Atsc;
    }

    #region graphbuilding

    /// <summary>
    /// Builds the graph.
    /// </summary>
    public override void BuildGraph()
    {
      try
      {
        if (_graphState != GraphState.Idle)
        {
          Log.Error("atsc:Graph already built");
          throw new TvException("Graph already built");
        }
        Log.WriteFile("atsc:BuildGraph");
        _graphBuilder = (IFilterGraph2)new FilterGraph();
        _capBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
        _capBuilder.SetFiltergraph(_graphBuilder);
        _rotEntry = new DsROTEntry(_graphBuilder);
        AddNetworkProviderFilter(typeof (ATSCNetworkProvider).GUID);
        AddTsWriterFilterToGraph();
        if (!useInternalNetworkProvider)
        {
          CreateTuningSpace();
          AddMpeg2DemuxerToGraph();
        }
        AddAndConnectBDABoardFilters(_device);
        AddBdaTransportFiltersToGraph();
        string graphName = _device.Name + " - ATSC Graph.grf";
        FilterGraphTools.SaveGraphFile(_graphBuilder, graphName);
        GetTunerSignalStatistics();
        _graphState = GraphState.Created;
      }
      catch (Exception ex)
      {
        Log.Write(ex);
        Dispose();
        _graphState = GraphState.Idle;
        throw new TvExceptionGraphBuildingFailed("Graph building failed", ex);
      }
    }

    /// <summary>
    /// Creates the tuning space.
    /// </summary>
    protected void CreateTuningSpace()
    {
      Log.WriteFile("atsc:CreateTuningSpace()");
      ITuner tuner = (ITuner)_filterNetworkProvider;
      SystemTuningSpaces systemTuningSpaces = new SystemTuningSpaces();
      ITuningSpaceContainer container = systemTuningSpaces as ITuningSpaceContainer;
      if (container == null)
      {
        Log.Error("CreateTuningSpace() Failed to get ITuningSpaceContainer");
        return;
      }
      IEnumTuningSpaces enumTuning;
      ITuningSpace[] spaces = new ITuningSpace[2];
      ITuneRequest request;
      container.get_EnumTuningSpaces(out enumTuning);
      while (true)
      {
        int fetched;
        enumTuning.Next(1, spaces, out fetched);
        if (fetched != 1)
          break;
        string name;
        spaces[0].get_UniqueName(out name);
        if (name == "MediaPortal ATSC TuningSpace")
        {
          Log.WriteFile("atsc:found correct tuningspace {0}", name);
          _tuningSpace = (IATSCTuningSpace)spaces[0];
          tuner.put_TuningSpace(_tuningSpace);
          _tuningSpace.CreateTuneRequest(out request);
          _tuneRequest = (IATSCChannelTuneRequest)request;
          return;
        }
        Release.ComObject("ITuningSpace", spaces[0]);
      }
      Release.ComObject("IEnumTuningSpaces", enumTuning);
      Log.WriteFile("atsc:Create new tuningspace");
      _tuningSpace = (IATSCTuningSpace)new ATSCTuningSpace();
      IATSCTuningSpace tuningSpace = (IATSCTuningSpace)_tuningSpace;

      tuningSpace.put_UniqueName("MediaPortal ATSC TuningSpace");
      tuningSpace.put_FriendlyName("MediaPortal ATSC TuningSpace");
      tuningSpace.put__NetworkType(typeof (ATSCNetworkProvider).GUID);
      tuningSpace.put_CountryCode(0);
      tuningSpace.put_InputType(TunerInputType.Antenna);
      tuningSpace.put_MaxMinorChannel(999); //minor channels per major
      tuningSpace.put_MaxPhysicalChannel(158); //69 for OTA 158 for QAM
      tuningSpace.put_MaxChannel(99); //major channels
      tuningSpace.put_MinMinorChannel(0);
      tuningSpace.put_MinPhysicalChannel(1); //OTA 1, QAM 2
      tuningSpace.put_MinChannel(1);

      IATSCLocator locator = (IATSCLocator)new ATSCLocator();
      locator.put_CarrierFrequency(-1);
      locator.put_InnerFEC(FECMethod.MethodNotSet);
      locator.put_InnerFECRate(BinaryConvolutionCodeRate.RateNotSet);
      locator.put_Modulation(ModulationType.Mod8Vsb); //OTA modultation, QAM = .Mod256Qam
      locator.put_OuterFEC(FECMethod.MethodNotSet);
      locator.put_OuterFECRate(BinaryConvolutionCodeRate.RateNotSet);
      locator.put_PhysicalChannel(-1);
      locator.put_SymbolRate(-1);
      locator.put_TSID(-1);
      object newIndex;
      _tuningSpace.put_DefaultLocator(locator);
      container.Add(_tuningSpace, out newIndex);
      tuner.put_TuningSpace(_tuningSpace);
      Release.ComObject("TuningSpaceContainer", container);
      _tuningSpace.CreateTuneRequest(out request);
      _tuneRequest = (IATSCChannelTuneRequest)request;
    }

    #endregion

    #region tuning & recording

    /// <summary>
    /// Scans the specified channel.
    /// </summary>
    /// <param name="subChannelId">The sub channel id.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>true if succeeded else false</returns>
    public override ITvSubChannel Scan(int subChannelId, IChannel channel)
    {
      Log.WriteFile("atsc:Tune:{0} ", channel);
      try
      {
        if (!BeforeTune(channel))
        {
          return null;
        }
        ITvSubChannel ch = base.Scan(subChannelId, channel);
        return ch;
      }
      catch (TvExceptionNoSignal)
      {
        throw;
      }
      catch (TvExceptionNoPMT)
      {
        throw;
      }
      catch (TvExceptionTuneCancelled)
      {
        throw;
      }
      catch (Exception ex)
      {
        Log.Write(ex);
        throw;
      }
    }

    /// <summary>
    /// Tunes the specified channel.
    /// </summary>
    /// <param name="subChannelId">The sub channel id.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>true if succeeded else false</returns>
    public override ITvSubChannel Tune(int subChannelId, IChannel channel)
    {
      Log.WriteFile("atsc:Tune:{0} ", channel);
      try
      {
        if (!BeforeTune(channel))
        {
          return null;
        }
        ITvSubChannel ch = base.Tune(subChannelId, channel);
        return ch;
      }
      catch (TvExceptionTuneCancelled)
      {
        throw;
      }
      catch (TvExceptionNoSignal)
      {
        throw;
      }
      catch (TvExceptionNoPMT)
      {
        throw;
      }
      catch (Exception ex)
      {
        Log.Write(ex);
        throw;
      }
    }

    private bool BeforeTune(IChannel channel)
    {
      ATSCChannel atscChannel = channel as ATSCChannel;
      if (atscChannel == null)
      {
        Log.WriteFile("atsc:Channel is not a ATSC channel!!! {0}", channel.GetType().ToString());
        return false;
      }
      if (_graphState == GraphState.Idle)
      {
        BuildGraph();
      }
      if (useInternalNetworkProvider)
      {
        return true;
      }
      if (_previousChannel == null || _previousChannel.IsDifferentTransponder(atscChannel))
      {
        Log.WriteFile("atsc:using new channel tuning settings");
        ITuneRequest request;
        int hr = _tuningSpace.CreateTuneRequest(out request);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - CreateTuneRequest");
        _tuneRequest = request;
        IATSCChannelTuneRequest tuneRequest = (IATSCChannelTuneRequest)_tuneRequest;
        ILocator locator;
        hr = _tuningSpace.get_DefaultLocator(out locator);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - get_DefaultLocator");
        IATSCLocator atscLocator = (IATSCLocator)locator;
        hr = atscLocator.put_SymbolRate(-1);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - put_SymbolRate");
        hr = atscLocator.put_TSID(-1);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - put_TSID");
        hr = atscLocator.put_CarrierFrequency((int)atscChannel.Frequency);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - put_CarrierFrequency");
        hr = atscLocator.put_Modulation(atscChannel.ModulationType);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - put_Modulation");
        hr = tuneRequest.put_Channel(atscChannel.MajorChannel);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - put_Channel");
        hr = tuneRequest.put_MinorChannel(atscChannel.MinorChannel);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - put_MinorChannel");
        hr = atscLocator.put_PhysicalChannel(atscChannel.PhysicalChannel);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - put_PhysicalChannel");
        hr = _tuneRequest.put_Locator(locator);
        if (hr != 0)
          Log.WriteFile("atsc: Failed - put_Locator");
        //set QAM paramters if necessary...
        _conditionalAccess.CheckATSCQAM(atscChannel);
      }
      else
      {
        Log.WriteFile("atsc:using previous channel tuning settings");
      }
      return true;
    }

    #endregion

    #region epg & scanning

    /// <summary>
    /// checks if a received EPGChannel should be filtered from the resultlist
    /// </summary>
    /// <value></value>
    protected override bool FilterOutEPGChannel(EpgChannel epgChannel)
    {      
      if (SettingsManagement.GetSetting("generalGrapOnlyForSameTransponder", "no").value == "yes")
      {
        DVBBaseChannel chan = epgChannel.Channel as DVBBaseChannel;
        Channel dbchannel =  ChannelManagement.GetChannelByTuningDetail(chan.NetworkId, chan.TransportId, chan.ServiceId);
        ATSCChannel atscchannel = new ATSCChannel();
        if (dbchannel == null)
        {
          return false;
        }
        foreach (TuningDetail detail in dbchannel.TuningDetails.Where(detail => detail.channelType == 1))
        {
          atscchannel.MajorChannel = detail.majorChannel;
          atscchannel.MinorChannel = detail.minorChannel;
          atscchannel.PhysicalChannel = detail.channelNumber;
        }
        return this.CurrentChannel.IsDifferentTransponder(atscchannel);
      }
      return false;
    }

    /// <summary>
    /// returns the ITVScanning interface used for scanning channels
    /// </summary>
    /// <value></value>
    public override ITVScanning ScanningInterface
    {
      get { return new ATSCScanning(this); }
    }

    #endregion

    /// <summary>
    /// Returns a <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    /// </summary>
    /// <returns>
    /// A <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    /// </returns>
    public override string ToString()
    {
      return _name;
    }

    /// <summary>
    /// Method to check if card can tune to the channel specified
    /// </summary>
    /// <param name="channel"></param>
    /// <returns>
    /// true if card can tune to the channel otherwise false
    /// </returns>
    public override bool CanTune(IChannel channel)
    {
      if ((channel as ATSCChannel) == null)
        return false;
      return true;
    }

    protected override DVBBaseChannel CreateChannel(int networkid, int transportid, int serviceid, string name)
    {
      ATSCChannel channel = new ATSCChannel();
      channel.NetworkId = networkid;
      channel.TransportId = transportid;
      channel.ServiceId = serviceid;
      channel.Name = name;
      return channel;
    }
  }
}