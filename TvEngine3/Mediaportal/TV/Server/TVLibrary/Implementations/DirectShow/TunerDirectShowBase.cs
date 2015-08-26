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
using System.Runtime.InteropServices.ComTypes;
using DirectShowLib;
using DirectShowLib.BDA;
using Mediaportal.TV.Server.Common.Types.Enum;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVLibrary.Implementations.Dvb;
using Mediaportal.TV.Server.TVLibrary.Implementations.Enum;
using Mediaportal.TV.Server.TVLibrary.Implementations.Helper;
using Mediaportal.TV.Server.TVLibrary.Implementations.Mpeg2Ts;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Analyzer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Exception;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Helper;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Tuner;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;
using MediaPortal.Common.Utils;

namespace Mediaportal.TV.Server.TVLibrary.Implementations.DirectShow
{
  /// <summary>
  /// A base implementation of <see cref="T:TvLibrary.Interfaces.ITVCard"/> for tuners implemented
  /// using a DirectShow graph.
  /// </summary>
  internal abstract class TunerDirectShowBase : TunerBase, IBroadcastEvent, IBroadcastEventEx
  {
    #region variables

    /// <summary>
    /// The DirectShow filter graph.
    /// </summary>
    protected IFilterGraph2 _graph = null;

    /// <summary>
    /// The running object table entry for the graph.
    /// </summary>
    private DsROTEntry _runningObjectTableEntry = null;

    /// <summary>
    /// The main tuner component device.
    /// </summary>
    protected DsDevice _deviceMain = null;

    /// <summary>
    /// The main tuner component filter.
    /// </summary>
    protected IBaseFilter _filterMain = null;

    /// <summary>
    /// The MediaPortal TS writer/analyser filter.
    /// </summary>
    protected IBaseFilter _filterTsWriter = null;

    /// <summary>
    /// The tuner's channel scanning interface.
    /// </summary>
    protected IChannelScannerInternal _channelScanner = null;

    /// <summary>
    /// The tuner's EPG grabbing interface.
    /// </summary>
    protected IEpgGrabber _epgGrabber = null;

    // Graph event registration.
    private IRegisterServiceProvider _registerServiceProvider = null;
    private BroadcastEventService _broadcastEventService = null;
    private int _broadcastEventRegistrationCookie = -1;

    // TsWriter configuration.
    private int _tsWriterInputDumpMask = 0;
    private bool _tsWriterDisableCrcChecking = false;

    #endregion

    #region constructor & finaliser

    /// <summary>
    /// Initialise a new instance of the <see cref="TunerDirectShowBase"/> class.
    /// </summary>
    /// <param name="name">The tuner's name.</param>
    /// <param name="externalId">The external identifier for the tuner.</param>
    /// <param name="tunerInstanceId">The identifier shared by all <see cref="ITuner"/> instances derived from a single tuner.</param>
    /// <param name="productInstanceId">The identifier shared by all <see cref="ITuner"/> instances derived from a single product.</param>
    /// <param name="supportedBroadcastStandards">The broadcast standards supported by the tuner.</param>
    protected TunerDirectShowBase(string name, string externalId, string tunerInstanceId, string productInstanceId, BroadcastStandard supportedBroadcastStandards)
      : base(name, externalId, tunerInstanceId, productInstanceId, supportedBroadcastStandards)
    {
    }

    ~TunerDirectShowBase()
    {
      Dispose(false);
    }

    #endregion

    #region graph building

    /// <summary>
    /// Create and initialise the DirectShow graph.
    /// </summary>
    protected void InitialiseGraph()
    {
      this.LogDebug("DirectShow base: initialise graph");
      _graph = (IFilterGraph2)new FilterGraph();
      _runningObjectTableEntry = new DsROTEntry(_graph);
    }

    /// <summary>
    /// Load the <see cref="ITunerExtension">extensions</see> for this tuner.
    /// </summary>
    /// <remarks>
    /// It is expected that this function will be called at some stage during tuner loading.
    /// This function may update the lastFilter reference parameter to insert filters for
    /// <see cref="IDirectShowAddOnDevice"/> extensions.
    /// </remarks>
    /// <param name="context">Any context required to initialise supported extensions.</param>
    /// <param name="lastFilter">The source filter (usually either a tuner or capture/receiver
    ///   filter) to connect the [first] extension filter to.</param>
    /// <returns>the set of extensions loaded for the tuner, in priority order</returns>
    protected IList<ITunerExtension> LoadExtensions(object context, ref IBaseFilter lastFilter)
    {
      this.LogDebug("DirectShow base: load tuner extensions");
      TunerExtensionLoader loader = new TunerExtensionLoader();
      IList<ITunerExtension> extensions = loader.Load(this, context);

      if (lastFilter != null)
      {
        List<IDirectShowAddOnDevice> addOnsToDispose = new List<IDirectShowAddOnDevice>();
        foreach (ITunerExtension e in extensions)
        {
          IDirectShowAddOnDevice addOn = e as IDirectShowAddOnDevice;
          if (addOn != null)
          {
            this.LogDebug("DirectShow base: add-on \"{0}\" found", e.Name);
            if (!addOn.AddToGraph(_graph, ref lastFilter))
            {
              this.LogDebug("DirectShow base: failed to add to graph");
              addOnsToDispose.Add(addOn);
              continue;
            }
          }
        }
        foreach (IDirectShowAddOnDevice addOn in addOnsToDispose)
        {
          IDisposable d = addOn as IDisposable;
          if (d != null)
          {
            d.Dispose();
          }
          extensions.Remove(addOn);
        }
      }
      return extensions;
    }

    /// <summary>
    /// Complete the DirectShow graph.
    /// </summary>
    protected void CompleteGraph()
    {
      this.LogDebug("DirectShow base: complete graph");

      // For some reason this fails for graphs containing a stream source
      // filter. In any case we should never allow failure to prevent using the
      // tuner.
      try
      {
        FilterGraphTools.SaveGraphFile(_graph, Name + " - " + SupportedBroadcastStandards + " Graph.grf");
      }
      catch
      {
      }
      _channelScanner = new ChannelScannerDirectShowBase(this, new ChannelScannerHelperDvb(), _filterTsWriter as ITsChannelScan);
      _epgGrabber = new EpgGrabberDvb(_filterTsWriter as IGrabberEpgDvb, _filterTsWriter as IGrabberEpgMhw, _filterTsWriter as IGrabberEpgOpenTv);

      RegisterForEvents();
    }

    /// <summary>
    /// Add the filter for the main tuner component to the DirectShow graph.
    /// </summary>
    protected void AddMainComponentFilterToGraph()
    {
      this.LogDebug("DirectShow base: add main component filter");
      if (!DevicesInUse.Instance.Add(_deviceMain))
      {
        throw new TvException("Main DirectShow tuner component is in use.");
      }
      try
      {
        _filterMain = FilterGraphTools.AddFilterFromDevice(_graph, _deviceMain);
      }
      catch (Exception ex)
      {
        DevicesInUse.Instance.Remove(_deviceMain);
        throw new TvException(ex, "Failed to add filter for DirectShow tuner main component to graph.");
      }
    }

    /// <summary>
    /// Add and connect the TS writer/analyser filter into the DirectShow graph.
    /// ...[upstream filter]->[TS writer/analyser]
    /// </summary>
    /// <param name="upstreamFilter">The filter to connect to the TS writer/analyser filter.</param>
    protected void AddAndConnectTsWriterIntoGraph(IBaseFilter upstreamFilter)
    {
      this.LogDebug("DirectShow base: add TS writer/analyser filter");
      _filterTsWriter = ComHelper.LoadComObjectFromFile("TsWriter.ax", typeof(MediaPortalTsWriter).GUID, typeof(IBaseFilter).GUID, true) as IBaseFilter;
      FilterGraphTools.AddAndConnectFilterIntoGraph(_graph, _filterTsWriter, "MediaPortal TS Analyser", upstreamFilter);
      ApplyTsWriterConfig();
    }

    private void ApplyTsWriterConfig()
    {
      ITsWriter tsWriter = _filterTsWriter as ITsWriter;
      if (tsWriter != null)
      {
        tsWriter.DumpInput((_tsWriterInputDumpMask & 1) != 0, (_tsWriterInputDumpMask & 2) != 0);
        tsWriter.CheckSectionCrcs(!_tsWriterDisableCrcChecking);
      }
    }

    #region events

    /// <summary>
    /// Register to receive events from the graph.
    /// </summary>
    /// <remarks>
    /// http://msdn.microsoft.com/en-us/library/windows/desktop/dd693480%28v=vs.85%29.aspx
    /// </remarks>
    private void RegisterForEvents()
    {
      this.LogDebug("DirectShow base: register for events");

      DirectShowLib.IServiceProvider serviceProvider = _graph as DirectShowLib.IServiceProvider;
      if (serviceProvider == null)
      {
        this.LogWarn("DirectShow base: failed to register for events, graph is not a service provider");
        return;
      }

      int hr = (int)NativeMethods.HResult.E_FAIL;
      object obj = null;
      try
      {
        hr = serviceProvider.QueryService(typeof(BroadcastEventService).GUID, typeof(IBroadcastEvent).GUID, out obj);
        if (hr == (int)NativeMethods.HResult.S_OK)
        {
          _broadcastEventService = obj as BroadcastEventService;
          if (_broadcastEventService == null)
          {
            throw new Exception();
          }
          this.LogDebug("DirectShow base: broadcast event service already registered");
        }
        else
        {
          throw new Exception();
        }
      }
      catch
      {
        // Invalid cast exception thrown when service not registered.
        this.LogDebug("DirectShow base: register broadcast event service, hr = 0x{0:x}", hr);
        _registerServiceProvider = _graph as IRegisterServiceProvider;
        if (_registerServiceProvider == null)
        {
          this.LogWarn("DirectShow base: failed to register for events, graph cannot register service providers");
          return;
        }

        // The event service should be available, even on XP.
        try
        {
          _broadcastEventService = new BroadcastEventService();
        }
        catch
        {
        }
        if (_broadcastEventService == null)
        {
          this.LogWarn("DirectShow base: failed to register for events, broadcast event service not supported/available");
          return;
        }

        hr = _registerServiceProvider.RegisterService(typeof(BroadcastEventService).GUID, _broadcastEventService);
        if (hr != (int)NativeMethods.HResult.S_OK)
        {
          this.LogWarn("DirectShow base: failed to register broadcast event service, hr = 0x{0:x}", hr);
          return;
        }
      }

      try
      {
        IConnectionPoint connectionPoint = _broadcastEventService as IConnectionPoint;
        if (Environment.OSVersion.Version.Major >= 6) // Vista or later
        {
          connectionPoint.Advise((IBroadcastEventEx)this, out _broadcastEventRegistrationCookie);
        }
        else
        {
          connectionPoint.Advise((IBroadcastEvent)this, out _broadcastEventRegistrationCookie);
        }
      }
      catch (Exception ex)
      {
        this.LogWarn(ex, "DirectShow base: failed to register for events, advise failed");
      }
    }

    private void UnregisterForEvents()
    {
      this.LogDebug("DirectShow base: unregister for events");

      if (_broadcastEventService != null)
      {
        if (_broadcastEventRegistrationCookie != -1)
        {
          IConnectionPoint connectionPoint = _broadcastEventService as IConnectionPoint;
          connectionPoint.Unadvise(_broadcastEventRegistrationCookie);
          _broadcastEventRegistrationCookie = -1;
        }

        if (_registerServiceProvider != null)
        {
          _registerServiceProvider.RegisterService(typeof(BroadcastEventService).GUID, null);
          _registerServiceProvider = null;
        }

        Release.ComObject("DirectShow tuner broadcast event service", ref _broadcastEventService);
      }
    }

    #endregion

    /// <summary>
    /// Remove and release DirectShow main component and graph components.
    /// </summary>
    /// <param name="isFinalising"><c>True</c> if the tuner is being finalised.</param>
    protected void CleanUpGraph(bool isFinalising = false)
    {
      this.LogDebug("DirectShow base: clean up graph");
      if (isFinalising)
      {
        return;
      }

      UnregisterForEvents();

      if (_runningObjectTableEntry != null)
      {
        _runningObjectTableEntry.Dispose();
        _runningObjectTableEntry = null;
      }

      if (_graph != null)
      {
        // First remove the filters that we inserted.
        _graph.RemoveFilter(_filterTsWriter);
        if (_filterMain != null)
        {
          _graph.RemoveFilter(_filterMain);
          Release.ComObject("DirectShow tuner main component filter", ref _filterMain);

          DevicesInUse.Instance.Remove(_deviceMain);
          // Do ***NOT*** dispose or release _deviceMain here. This would cause
          // reloading to fail. Deal with this in Dispose().
        }

        // Now check if there are any filters remaining in the graph.
        // Presence of such filters indicate there are badly behaved
        // classes or extensions present.
        IEnumFilters enumFilters = null;
        int hr = _graph.EnumFilters(out enumFilters);
        if (hr != (int)NativeMethods.HResult.S_OK)
        {
          this.LogWarn("DirectShow base: failed to enumerate remaining filters in graph");
        }
        else
        {
          try
          {
            IBaseFilter[] filters = new IBaseFilter[1];
            int filterCount;
            while (enumFilters.Next(filters.Length, filters, out filterCount) == (int)NativeMethods.HResult.S_OK && filterCount == 1)
            {
              IBaseFilter filter = filters[0];
              this.LogWarn("DirectShow base: removing and releasing orphaned filter {0}", FilterGraphTools.GetFilterName(filter));
              _graph.RemoveFilter(filter);
              Release.ComObjectAllRefs("DirectShow tuner orphaned filter", ref filter);
            }
          }
          finally
          {
            Release.ComObject("DirectShow tuner remaining filter enumerator", ref enumFilters);
          }
        }
        Release.ComObject("DirectShow tuner graph", ref _graph);
      }
      _channelScanner = null;
      _epgGrabber = null;
      Release.ComObject("TS writer/analyser filter", ref _filterTsWriter);
    }

    #endregion

    #region ITunerInternal members

    #region configuration

    /// <summary>
    /// Reload the tuner's configuration.
    /// </summary>
    /// <param name="configuration">The tuner's configuration.</param>
    public override void ReloadConfiguration(Tuner configuration)
    {
      this.LogDebug("DirectShow base: reload configuration");
      _tsWriterInputDumpMask = configuration.TsWriterInputDumpMask;
      _tsWriterDisableCrcChecking = configuration.DisableTsWriterCrcChecking;

      this.LogDebug("  TsWriter input dump mask = 0x{0:x}", _tsWriterInputDumpMask);
      this.LogDebug("  TsWriter CRC check?      = {0}", !_tsWriterDisableCrcChecking);

      ApplyTsWriterConfig();
    }

    #endregion

    #region state control

    /// <summary>
    /// Actually set the state of the tuner.
    /// </summary>
    /// <param name="state">The state to apply to the tuner.</param>
    /// <param name="isFinalising"><c>True</c> if the tuner is being finalised.</param>
    public override void PerformSetTunerState(TunerState state, bool isFinalising = false)
    {
      this.LogDebug("DirectShow base: perform set tuner state");
      if (isFinalising)
      {
        return;
      }

      if (_graph == null)
      {
        throw new TvException("DirectShow graph is null, can't set tuner state.");
      }

      // Note pause and run can return S_FALSE in certain non-error conditions.
      // Refer to the MSDN documentation for IMediaControl.
      int hr = (int)NativeMethods.HResult.S_OK;
      if (state == TunerState.Stopped)
      {
        hr = (_graph as IMediaControl).Stop();
      }
      else if (state == TunerState.Paused)
      {
        hr = (_graph as IMediaControl).Pause();
        if (hr == (int)NativeMethods.HResult.S_FALSE)
        {
          hr = (int)NativeMethods.HResult.S_OK;
        }
      }
      else if (state == TunerState.Started)
      {
        hr = (_graph as IMediaControl).Run();
        if (hr == (int)NativeMethods.HResult.S_FALSE)
        {
          hr = (int)NativeMethods.HResult.S_OK;
        }
      }
      else
      {
        hr = (int)NativeMethods.HResult.E_FAIL;
      }
      TvExceptionDirectShowError.Throw(hr, "Failed to change tuner state to {0}.", state);
    }

    #endregion

    #region tuning

    /// <summary>
    /// Allocate a new sub-channel instance.
    /// </summary>
    /// <param name="id">The identifier for the sub-channel.</param>
    /// <returns>the new sub-channel instance</returns>
    public override ISubChannelInternal CreateNewSubChannel(int id)
    {
      return new SubChannelMpeg2Ts(id, _filterTsWriter as ITsFilter);
    }

    #endregion

    #region interfaces

    /// <summary>
    /// Get the tuner's channel linkage scanning interface.
    /// </summary>
    public override IChannelLinkageScanner InternalChannelLinkageScanningInterface
    {
      get
      {
        return null;  // not required => no longer supported by TsWriter
      }
    }

    /// <summary>
    /// Get the tuner's channel scanning interface.
    /// </summary>
    public override IChannelScannerInternal InternalChannelScanningInterface
    {
      get
      {
        return _channelScanner;
      }
    }

    /// <summary>
    /// Get the tuner's electronic programme guide data grabbing interface.
    /// </summary>
    public override IEpgGrabber InternalEpgGrabberInterface
    {
      get
      {
        return _epgGrabber;
      }
    }

    #endregion

    #endregion

    #region IBroadcastEvent member

    public int Fire(Guid eventId)
    {
      this.LogDebug("DirectShow base: received broadcast event, ID = {0}", eventId);
      return 0;
    }

    #endregion

    #region IBroadcastEventEx member

    public int FireEx(Guid eventId, int param1, int param2, int param3, int param4)
    {
      this.LogDebug("DirectShow base: received extended broadcast event, ID = {0}, param 1 = {1}, param 2 = {2}, param 3 = {3}, param 4 = {4}", eventId, param1, param2, param3, param4);
      return 0;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    /// <param name="isDisposing"><c>True</c> if the tuner is being disposed.</param>
    protected override void Dispose(bool isDisposing)
    {
      base.Dispose(isDisposing);
      if (isDisposing && _deviceMain != null)
      {
        _deviceMain.Dispose();
        _deviceMain = null;
      }
    }

    #endregion
  }
}