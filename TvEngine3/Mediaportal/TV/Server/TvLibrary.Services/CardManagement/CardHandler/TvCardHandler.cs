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
using System.Threading;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces.Device;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVService.Interfaces.CardHandler;

namespace Mediaportal.TV.Server.TVLibrary.CardManagement.CardHandler
{
  public class TvCardHandler : ITvCardHandler
  {


    #region variables

    private ITVCard _card;
    private Card _dbsCard;
    private readonly UserManagement _userManagement;
    private readonly IParkedUserManagement _parkedUserManagement;

    private readonly DisEqcManagement _disEqcManagement;
    private readonly TeletextManagement _teletext;
    private readonly ChannelScanning _scanner;
    private readonly EpgGrabbing _epgGrabbing;    
    private readonly Recorder _recorder;
    private readonly TimeShifter _timerShifter;
    private readonly CardTuner _tuner;
    private ICiMenuActions _ciMenu;    

    private static ScanParameters _settings;
    private static object _settingsLock = new object();

    #endregion

    #region ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="ITVCard"/> class.
    /// </summary>
    public TvCardHandler(Card dbsCard, ITVCard card)
    {      
      _dbsCard = dbsCard;
      Card = card;
      _userManagement = new UserManagement(this);
      _parkedUserManagement = new ParkedUserManagement(this);

      _disEqcManagement = new DisEqcManagement(this);
      _teletext = new TeletextManagement(this);
      _scanner = new ChannelScanning(this);
      _epgGrabbing = new EpgGrabbing(this);
      _tuner = new CardTuner(this);
      _recorder = new Recorder(this);
      _timerShifter = new TimeShifter(this);      
    }

    #endregion

    #region CI Menu handling

    public bool CiMenuSupported
    {
      get
      {
        ICiMenuActions menuInterface = _card.CaMenuInterface;
        if (menuInterface == null)
        {
          return false;
        }
        IConditionalAccessProvider caProvider = menuInterface as IConditionalAccessProvider;
        if (caProvider.IsInterfaceReady())
        {
          _ciMenu = menuInterface;
          return true;
        }
        return false;
      }
    }

    /// <summary>
    /// Gets the ConditionalAccess handler.
    /// </summary>
    /// <value>ConditionalAccess</value>
    public ICiMenuActions CiMenuActions
    {
      get { return _ciMenu; }
    }

    #endregion


    public IUserManagement UserManagement
    {
      get { return _userManagement; }
    }


    public IParkedUserManagement ParkedUserManagement
    {
      get { return _parkedUserManagement; }
    }    

    /// <summary>
    /// Gets the diseqc handler.
    /// </summary>
    /// <value>The dis eq C.</value>
    public IDisEqcManagement DisEqC
    {
      get { return _disEqcManagement; }
    }

    public ITeletextManagement Teletext
    {
      get { return _teletext; }
    }

    public IChannelScanning Scanner
    {
      get { return _scanner; }
    }

    public IEpgGrabbing Epg
    {
      get { return _epgGrabbing; }
    }

    public IRecorder Recorder
    {
      get { return _recorder; }
    }

    public ITimeShifter TimeShifter
    {
      get { return _timerShifter; }
    }

    public ICardTuner Tuner
    {
      get { return _tuner; }
    }

    /// <summary>
    /// Gets or sets the reference to the ITVCard interface
    /// </summary>
    /// <value>The card.</value>
    public ITVCard Card
    {
      get { return _card; }
      set
      {
        _card = value;
        if (_card.Context == null)
        {
          _card.Context = new TvCardContext();
        }
        SetParameters();
      }
    }

    /// <summary>
    /// Gets a value indicating whether this card supports sub channels.
    /// </summary>
    /// <value><c>true</c> if card supports sub channels; otherwise, <c>false</c>.</value>
    public bool SupportsSubChannels
    {
      get
      {       
        return _card.SupportsSubChannels;
      }
    }

    /// <summary>
    /// Gets or sets the reference the Card database record 
    /// </summary>
    /// <value>The card record from the database.</value>
    public Card DataBaseCard
    {
      get { return _dbsCard; }
      set { _dbsCard = value; }
    }



    /// <summary>
    /// Gets a value indicating whether this instance is idle.
    /// </summary>
    /// <value><c>true</c> if this instance is idle; otherwise, <c>false</c>.</value>
    public bool IsIdle
    {
      get
      {
        if (UserManagement.UsersCount() == 0)
        {
          return true;
        }
        return false;
      }
    }

    /// <summary>
    /// Does the device support conditional access?
    /// </summary>
    /// <value><c>true</c> if the device supports conditional access, otherwise <c>false</c></value>
    public bool IsConditionalAccessSupported
    {
      get
      {       
        return _card.IsConditionalAccessSupported;
      }
    }

    /// <summary>
    /// Returns the number of channels the card is currently decrypting
    /// </summary>
    /// <value>The number of channels decrypting.</value>
    public int NumberOfChannelsDecrypting
    {
      get
      {       
        return _card.NumberOfChannelsDecrypting;
      }
    }


    /// <summary>
    /// Gets the type of card.
    /// </summary>
    /// <value>cardtype (Analog,DvbS,DvbT,DvbC,Atsc,WebStream)</value>
    public CardType Type
    {
      get
      {
        try
        {         
          return _card.CardType;
        }
        catch (ThreadAbortException)
        {
          return CardType.Analog;
        }
        catch (Exception ex)
        {
          this.LogError(ex);
          return CardType.Analog;
        }
      }
    }

    /// <summary>
    /// Gets the name for a card.
    /// </summary>
    /// <returns>name of card</returns>
    public string CardName
    {
      get
      {
        try
        {          
          return _card.Name;
        }
        catch (ThreadAbortException)
        {
          return "";
        }
        catch (Exception ex)
        {
          this.LogError(ex);
          return "";
        }
      }
    }

    /// <summary>
    /// Gets the name for a card.
    /// </summary>
    /// <returns>device of card</returns>
    public string CardDevice()
    {
      try
      {      
        return _dbsCard.DevicePath;
      }
      catch (ThreadAbortException)
      {
        return "";
      }
      catch (Exception ex)
      {
        this.LogError(ex);
        return "";
      }
    }


    /// <summary>
    /// Returns if the tuner is locked onto a signal or not
    /// </summary>
    /// <returns>true when tuner is locked to a signal otherwise false</returns>
    public bool TunerLocked
    {
      get
      {
        try
        {
          if (_dbsCard.Enabled == false)
          {
            return false;
          }
          return _card.IsTunerLocked;
        }
        catch (ThreadAbortException)
        {
          return false;
        }
        catch (Exception ex)
        {
          this.LogError(ex);
          return false;
        }
      }
    }

    /// <summary>
    /// Returns the signal quality for a card
    /// </summary>
    /// <returns>signal quality (0-100)</returns>
    public int SignalQuality
    {
      get
      {
        try
        {
          if (_dbsCard.Enabled == false)
          {
            return 0;
          }
          return _card.SignalQuality;
        }
        catch (ThreadAbortException)
        {
          return 0;
        }
        catch (Exception ex)
        {
          this.LogError(ex);
          return 0;
        }
      }
    }

    /// <summary>
    /// Returns the signal level for a card.
    /// </summary>
    /// <returns>signal level (0-100)</returns>
    public int SignalLevel
    {
      get
      {
        try
        {
          if (_dbsCard.Enabled == false)
          {
            return 0;
          }         
          return _card.SignalLevel;
        }
        catch (ThreadAbortException)
        {
          return 0;
        }
        catch (Exception ex)
        {
          this.LogError(ex);
          return 0;
        }
      }
    }

    /// <summary>
    /// Updates the signal state for a card.
    /// </summary>
    public void UpdateSignalSate()
    {
      try
      {
        if (_dbsCard.Enabled == false)
        {
          return;
        }       
        _card.ResetSignalUpdate();
      }
      catch (ThreadAbortException)
      {       
      }
      catch (Exception ex)
      {
        this.LogError(ex);
      }
    }


    /// <summary>
    /// returns the min/max channel numbers for analog cards
    /// </summary>
    public int MinChannel
    {
      get
      {
        try
        {
          if (_dbsCard.Enabled == false)
          {
            return 0;
          }
          
          return _card.MinChannel;
        }
        catch (ThreadAbortException)
        {
          return 0;
        }
        catch (Exception ex)
        {
          this.LogError(ex);
          return 0;
        }
      }
    }

    /// <summary>
    /// Gets the max channel to which we can tune.
    /// </summary>
    /// <value>The max channel.</value>
    public int MaxChannel
    {
      get
      {
        try
        {
          if (_dbsCard.Enabled == false)
          {
            return 0;
          }
          return _card.MaxChannel;
        }
        catch (ThreadAbortException)
        {
          return 0;
        }
        catch (Exception ex)
        {
          this.LogError(ex);
          return 0;
        }
      }
    }

    private static ScanParameters Settings
    {
      get
      {
        lock (_settingsLock)
        {
          if (_settings == null)
          {
            _settings = new ScanParameters();
            Common.Utils.ThreadHelper.ParallelInvoke(
            () => _settings.TimeOutTune = SettingsManagement.GetValue("timeoutTune", 2),
            () => _settings.TimeOutPAT = SettingsManagement.GetValue("timeoutPAT", 5),
            () => _settings.TimeOutCAT = SettingsManagement.GetValue("timeoutCAT", 5),
            () => _settings.TimeOutPMT = SettingsManagement.GetValue("timeoutPMT", 10),
            () => _settings.TimeOutSDT = SettingsManagement.GetValue("timeoutSDT", 20),
            () => _settings.TimeOutAnalog = SettingsManagement.GetValue("timeoutAnalog", 20),
            () => _settings.UseDefaultLnbFrequencies = SettingsManagement.GetValue("lnbDefault", true),
            () => _settings.LnbLowFrequency = SettingsManagement.GetValue("LnbLowFrequency", 0),
            () => _settings.LnbHighFrequency = SettingsManagement.GetValue("LnbHighFrequency", 0),
            () => _settings.LnbSwitchFrequency = SettingsManagement.GetValue("LnbSwitchFrequency", 0),
            () => _settings.MinimumFiles = SettingsManagement.GetValue("timeshiftMinFiles", 6),
            () => _settings.MaximumFiles = SettingsManagement.GetValue("timeshiftMaxFiles", 20),
            () => _settings.MaximumFileSize = (uint)SettingsManagement.GetValue("timeshiftMaxFileSize", 256)
            );
            _settings.MaximumFileSize *= 1000;
            _settings.MaximumFileSize *= 1000;                                                       
          }
        }        
        return _settings;
      }
    }


    /// <summary>
    /// Disposes this instance.
    /// </summary>
    public void Dispose()
    {
      _card.Dispose();
    }
    
    private void SetParameters()
    {
      if (_card == null)
      {
        return;
      }      
      _card.Parameters = Settings;
    }



    public long CurrentMux()
    {
      ITvSubChannel subchannel = _card.GetFirstSubChannel();
      long frequency = -1;

      IChannel tuningDetail = subchannel.CurrentChannel;

      var dvbTuningDetail = tuningDetail as DVBBaseChannel;
      if (dvbTuningDetail != null)
      {
        frequency = dvbTuningDetail.Frequency;
      }
      else
      {
        var analogTuningDetail = tuningDetail as AnalogChannel;
        if (analogTuningDetail != null)
        {
          frequency = analogTuningDetail.Frequency; 
        }        
      }

      return frequency;      
    }


    /// <summary>
    /// Gets the current channel.
    /// </summary>
    /// <param name="userName"> </param>
    /// <param name="idChannel"> </param>
    /// <returns>channel</returns>
    public IChannel CurrentChannel(string userName, int idChannel)
    {
      try
      {
        if (_dbsCard.Enabled == false)
        {
          return null;
        }
               
        if (Context == null)
        {
          return null;
        }
        
        ITvSubChannel subchannel = _card.GetSubChannel(_userManagement.GetSubChannelIdByChannelId(userName, idChannel));
        if (subchannel == null)
        {
          return null;
        }
        return subchannel.CurrentChannel;
      }
      catch(ThreadAbortException)
      {
        return null;
      }
      catch (Exception ex)
      {
        this.LogError(ex);
        return null;
      }
    }

    /// <summary>
    /// Gets the current channel.
    /// </summary>
    /// <param name="userName"> </param>
    /// <returns>id of database channel</returns>
    public int CurrentDbChannel(string userName)
    {
      try
      {
        if (_dbsCard.Enabled == false)
        {
          return -1;
        }
        
        return _userManagement.GetTimeshiftingChannelId(userName);
      }
      catch (ThreadAbortException)
      {
        return -1;
      }
      catch (Exception ex)
      {
        this.LogError(ex);
        return -1;
      }
    }

    private ITvCardContext Context
    {
      get { return _card.Context as ITvCardContext; }
    }

    /// <summary>
    /// Gets the current channel name.
    /// </summary>
    /// <param name="userName"> </param>
    /// <param name="idChannel"> </param>
    /// <returns>channel</returns>
    public string CurrentChannelName(string userName, int idChannel)
    {
      try
      {
        if (_dbsCard.Enabled == false)
        {
          return "";
        }
        
        if (Context == null)
        {
          return "";
        }
        
        ITvSubChannel subchannel = _card.GetSubChannel(_userManagement.GetSubChannelIdByChannelId(userName, idChannel));
        if (subchannel == null)
        {
          return "";
        }
        if (subchannel.CurrentChannel == null)
        {
          return "";
        }
        return subchannel.CurrentChannel.Name;
      }
      catch (ThreadAbortException)
      {
        return "";
      }
      catch (Exception ex)
      {
        this.LogError(ex);
        return "";
      }
    }

    /// <summary>
    /// Returns whether the channel to which the card is tuned is
    /// scrambled or not.
    /// </summary>
    /// <returns>yes if channel is scrambled and CI/CAM cannot decode it, otherwise false</returns>
    public bool IsScrambled(string userName)
    {
      try
      {
        if (_dbsCard.Enabled == false)
        {
          return true;
        }               
        if (Context == null)
        {
          return false;
        }        
        ITvSubChannel subchannel = _card.GetSubChannel(_userManagement.GetTimeshiftingSubChannel(userName));
        if (subchannel == null)
        {
          return false;
        }
        return (!subchannel.IsReceivingAudioVideo);
      }
      catch (ThreadAbortException)
      {
        return false;
      }
      catch (Exception ex)
      {
        this.LogError(ex);
        return false;
      }
    }

    public bool IsScrambled(int subchannelId)
    {
      try
      {
        if (_dbsCard.Enabled == false)
        {
          return true;
        }
        
        if (Context == null)
          return false;

        ITvSubChannel subchannel = _card.GetSubChannel(subchannelId);
        if (subchannel == null)
          return false;
        return (false == subchannel.IsReceivingAudioVideo);
      }
      catch (ThreadAbortException)
      {
        return false;
      }
      catch (Exception ex)
      {
        this.LogError(ex);
        return false;
      }
    }


    

   

    /// <summary>
    /// Stops the card.
    /// </summary>
    public void StopCard()
    {
      try
      {
        if (!_dbsCard.Enabled)
        {
          return;
        }

        if (_parkedUserManagement.HasAnyParkedUsers())
        {
          this.LogInfo("unable to Stopcard since there are parked channels");
          return;
        }
      
        this.LogInfo("Stopcard");

        //remove all subchannels, except for this user...
        FreeAllSubChannels();        

        
        
        _userManagement.Clear();
        
        _card.Stop();
      }
      catch (ThreadAbortException)
      {       
      }
      catch (Exception ex)
      {
        this.LogError(ex);
      }
    }

    private void FreeAllSubChannels()
    {
      ITvSubChannel[] channels = _card.SubChannels;
      foreach (ITvSubChannel tvSubChannel in channels)
      {
        _card.FreeSubChannel(tvSubChannel.SubChannelId);
      }

    }
  
  }
}