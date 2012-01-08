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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MediaPortal.Common.Utils;
using Mediaportal.TV.Server.TVControl;
using Mediaportal.TV.Server.TVControl.Interfaces;
using Mediaportal.TV.Server.TVControl.Interfaces.Services;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVService.CardManagement.CardHandler;
using Mediaportal.TV.Server.TVService.Interfaces.CardHandler;
using Mediaportal.TV.Server.TVService.Interfaces.Enums;
using Mediaportal.TV.Server.TVService.Interfaces.Services;

namespace Mediaportal.TV.Server.TVService.CardManagement.CardAllocation
{
  public class ChannelStates : CardAllocationBase
  {
    public delegate void OnChannelStatesSetDelegate(IUser user);
    public event OnChannelStatesSetDelegate OnChannelStatesSet;

    #region private members   

    private readonly object _lock = new object();
    private readonly object _threadlock = new object();
    private Thread _setChannelStatesThread;

    public ChannelStates()      
    {
      LogEnabled = false;
    }

    private void UpdateChannelStateUsers(IEnumerable<IUser> allUsers, ChannelState chState, int channelId)
    {
      foreach (IUser t in allUsers)
      {
        IUser u = null;
        try
        {
          u = t;
        }
        catch (NullReferenceException) {}

        if (u == null)
          continue;
        if (u.IsAdmin)
          continue; //scheduler users do not need to have their channelstates set.

        try
        {
          UpdateChannelStateUser(u, chState, channelId);
        }
        catch (NullReferenceException) {}
      }
    }

    private static void UpdateChannelStateUser(IUser user, ChannelState chState, int channelId)
    {
      ChannelState currentChState;

      bool stateExists = user.ChannelStates.TryGetValue(channelId, out currentChState);

      if (stateExists)
      {
        if (chState == ChannelState.nottunable)
        {
          return;
        }
        bool recording = (currentChState == ChannelState.recording);
        if (!recording)
        {
          user.ChannelStates[channelId] = chState;
          //add key if does not exist, or update existing one.                            
        }
      }
      else
      {
        user.ChannelStates[channelId] = chState;
        //add key if does not exist, or update existing one.                          
      }
    }

    private static IList<IUser> GetActiveUsers()
    {
      // find all users
      IInternalControllerService tvControllerService = GlobalServiceProvider.Get<IInternalControllerService>();
      IDictionary<int, ITvCardHandler> cards = tvControllerService.CardCollection;
      var allUsers = new List<IUser>();
      try
      {
        ICollection<ITvCardHandler> cardHandlers = cards.Values;
        foreach (ITvCardHandler cardHandler in cardHandlers)
        {
          //get a list of all users for this card
          IDictionary<string, IUser> usersAvail = cardHandler.UserManagement.Users;
          if (usersAvail != null)
          {
            foreach (KeyValuePair<string, IUser> tmpUser in usersAvail.Where(tmpUser => !tmpUser.Value.IsAdmin)) 
            {
                tmpUser.Value.ChannelStates = new Dictionary<int, ChannelState>();
                allUsers.Add(tmpUser.Value);
              }
            }
          }
        }
      catch (InvalidOperationException tex)
      {
        Log.Error("ChannelState: Possible race condition occured when getting users - {0}", tex);
      }

      return allUsers;
    }    

    private void DoSetChannelStatesForAllUsers(ICollection<Channel> channels, ICollection<IUser> allUsers)
    {
      IInternalControllerService tvControllerService = GlobalServiceProvider.Get<IInternalControllerService>();
      IDictionary<int, ITvCardHandler> cards = tvControllerService.CardCollection;
      lock (_lock)
      {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
          //construct list of all cards we can use to tune to the new channel
          Log.Debug("Controller: DoSetChannelStatesForAllUsers for {0} channels", channels.Count);

          if (allUsers == null || allUsers.Count == 0)
          {
            return; // no users, no point in continuing.
          }

          IDictionary<int, ChannelState> timeshiftingAndRecordingStates = null;
          ICollection<ITvCardHandler> cardHandlers = cards.Values;
          foreach (Channel ch in channels)
          {
            if (!ch.visibleInGuide)
            {
              UpdateChannelStateUsers(allUsers, ChannelState.nottunable, ch.idChannel);
              continue;
            }

              ICollection<IChannel> tuningDetails = CardAllocationCache.GetTuningDetailsByChannelId(ch);
            bool isValidTuningDetails = IsValidTuningDetails(tuningDetails);
            if (!isValidTuningDetails)
            {
              UpdateChannelStateUsers(allUsers, ChannelState.nottunable, ch.idChannel);
              continue;
            }

            foreach (IChannel tuningDetail in tuningDetails)
            {
              foreach (ITvCardHandler cardHandler in cardHandlers)
              {
                //check if card is enabled
                if (!cardHandler.DataBaseCard.enabled)
                {
                  //not enabled, so skip the card
                  UpdateChannelStateUsers(allUsers, ChannelState.nottunable, ch.idChannel);
                  continue;
                }

                if (!cardHandler.Tuner.CanTune(tuningDetail))
                {
                  //card cannot tune to this channel, so skip it
                  UpdateChannelStateUsers(allUsers, ChannelState.nottunable, ch.idChannel);
                  continue;
                }

                //check if channel is mapped to this card and that the mapping is not for "Epg Only"
                bool isChannelMappedToCard = CardAllocationCache.IsChannelMappedToCard(ch, cardHandler.DataBaseCard);
                if (!isChannelMappedToCard)
                {
                  UpdateChannelStateUsers(allUsers, ChannelState.nottunable, ch.idChannel);
                  continue;
                }

                if (!tuningDetail.FreeToAir && !cardHandler.DataBaseCard.CAM)
                {
                  UpdateChannelStateUsers(allUsers, ChannelState.nottunable, ch.idChannel);
                  continue;
                }

                //ok card could be used to tune to this channel
                //now we check if its free...                              
                CheckTransponderAllUsers(ch, allUsers, cardHandler, tuningDetail);
              } //while card end
            } //foreach tuningdetail end              

            //only query once
              if (timeshiftingAndRecordingStates == null)
            {
                Stopwatch stopwatchTimeshiftingAndRecording = Stopwatch.StartNew();
                timeshiftingAndRecordingStates = tvControllerService.GetAllTimeshiftingAndRecordingChannels();
                stopwatchTimeshiftingAndRecording.Stop();
              Log.Info("ChannelStates.GetAllTimeshiftingAndRecordingChannels took {0} msec",
                         stopwatchTimeshiftingAndRecording.ElapsedMilliseconds);
            }
              UpdateRecOrTSChannelStateForUsers(ch, allUsers, timeshiftingAndRecordingStates);
          }

          RemoveAllTunableChannelStates(allUsers);        
        }
        catch (ThreadAbortException)
        {
          Log.Info("ChannelState.DoSetChannelStatesForAllUsers: thread obsolete and aborted.");
        }
        catch (InvalidOperationException tex)
        {
            Log.Error("ChannelState.DoSetChannelStatesForAllUsers: Possible race condition occured setting channel states - {0}", tex);
        }
        catch (Exception ex)
        {
            Log.Error("ChannelState.DoSetChannelStatesForAllUsers: An unknown error occured while setting channel states - {0}\n{1}", ex.Message,
                      ex);
        }
        finally
        {
          stopwatch.Stop();
          Log.Info("ChannelStates.DoSetChannelStatesForAllUsers took {0} msec", stopwatch.ElapsedMilliseconds);

          if (OnChannelStatesSet != null)
          {
            if (allUsers != null)
            {
              foreach (var user in allUsers)
              {
                Log.Debug("DoSetChannelStatesForAllUsers OnChannelStatesSet user={0}", user.Name);
                OnChannelStatesSet(user);
              } 
            }              
          }
        }
      }
    }

    private static void RemoveAllTunableChannelStates(IEnumerable<IUser> allUsers)
    {
      foreach (IUser user in allUsers)
      {
        var keysToDelete = user.ChannelStates.Where(x => x.Value == ChannelState.tunable).Select(kvp => kvp.Key).ToList();
        foreach (int key in keysToDelete)
        {
          user.ChannelStates.Remove(key);
        }
      }
    }

    private void UpdateRecOrTSChannelStateForUsers(Channel ch, IEnumerable<IUser> allUsers,
                                                          IDictionary<int, ChannelState> TSandRecStates)
    {
      ChannelState cs;
      TSandRecStates.TryGetValue(ch.idChannel, out cs);

      if (cs == ChannelState.recording)
      {
        UpdateChannelStateUsers(allUsers, ChannelState.recording, ch.idChannel);
      }
      else if (cs == ChannelState.timeshifting)
      {
        UpdateChannelStateUsers(allUsers, ChannelState.timeshifting, ch.idChannel);
      }
    }

    private void CheckTransponderAllUsers(Channel ch, IEnumerable<IUser> allUsers, ITvCardHandler tvcard,
                                                 IChannel tuningDetail)
    {
      foreach (IUser user in allUsers) 
      {
        //ignore admin users, like scheduler
        if (!user.IsAdmin)
        {
          bool checkTransponder = CheckTransponder(user, tvcard, tuningDetail);
        if (checkTransponder)
        {
          UpdateChannelStateUser(user, ChannelState.tunable, ch.idChannel);
        }
        else
        {
          UpdateChannelStateUser(user, ChannelState.nottunable, ch.idChannel);
        }
    }
      }
    }

    #endregion

    #region public members

    private void AbortChannelStates()
    {
      lock (_threadlock)
      {
        if (_setChannelStatesThread != null && _setChannelStatesThread.IsAlive)
        {
          _setChannelStatesThread.Abort();
        }
      }
    }

    public void SetChannelStatesForAllUsers(ICollection<Channel> channels)
    {
      if (channels == null)
      {
        return;
      }
      AbortChannelStates();
      //call the real work as a thread in order to avoid slower channel changes.
      // find all users      
      ICollection<IUser> allUsers = GetActiveUsers();
      ThreadStart starter = () => DoSetChannelStatesForAllUsers(channels, allUsers);
      lock (_threadlock)
      {
        _setChannelStatesThread = new Thread(starter)
                                    {
                                      Name = "Channel state thread",
                                      IsBackground = true,
                                      Priority = ThreadPriority.Lowest
                                    };
        _setChannelStatesThread.Start();
    }
    }    

    /// <summary>
    /// Gets a list of all channel states    
    /// </summary>    
    /// <returns>dictionary containing all channel states of the channels supplied</returns>
    public void SetChannelStatesForUser(ICollection<Channel> channels, ref IUser user)
    {            
      if (channels != null)
      {
        var allUsers = new List<IUser> { user };
        DoSetChannelStatesForAllUsers(channels, allUsers);
        if (OnChannelStatesSet != null)
        {
          Log.Debug("SetChannelStatesForUser OnChannelStatesSet user={0}", user.Name);
          OnChannelStatesSet(user);
        }
      }           
    }

    #endregion
  }
}