#region Copyright (C) 2005-2010 Team MediaPortal

// Copyright (C) 2005-2010 Team MediaPortal
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MediaPortal.GUI.Library;
using MediaPortal.Profile;
using Mediaportal.TV.Server.TVControl;
using Mediaportal.TV.Server.TVControl.Events;
using Mediaportal.TV.Server.TVControl.Interfaces.Events;
using Mediaportal.TV.Server.TVControl.Interfaces.Services;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer.Entities;
using Mediaportal.TV.Server.TVService.ServiceAgents;
using Mediaportal.TV.TvPlugin.Helper;
using Channel = Mediaportal.TV.Server.TVDatabase.Entities.Channel;
using Recording = Mediaportal.TV.Server.TVDatabase.Entities.Recording;

namespace Mediaportal.TV.TvPlugin
{
  public class TvNotifyManager : ITvServerEventCallback
  {
    private readonly Timer _timer;
    // flag indicating that notifies have been added/changed/removed
    private readonly IRecordingService _recordingServiceAgent = ServiceAgents.Instance.RecordingServiceAgent;
    private static bool _notifiesListChanged;
    private static bool _enableRecNotification;
    private static bool _busy;
    private readonly int _preNotifyConfig;

    //list of all notifies (alert me n minutes before program starts)
    private readonly IList<ProgramBLL> _notifiesList = new List<ProgramBLL>();

    public TvNotifyManager()
    {
      using (Settings xmlreader = new MPSettings())
      {
        _enableRecNotification = xmlreader.GetValueAsBool("mytv", "enableRecNotifier", false);
        _preNotifyConfig = xmlreader.GetValueAsInt("mytv", "notifyTVBefore", 300);
      }
      
      _busy = false;
      _timer = new Timer();
      _timer.Stop();
      // check every 15 seconds for notifies
      new User {IsAdmin = false, Name = "Free channel checker"};
      _timer.Interval = 15000;
      _timer.Enabled = true;
      _timer.Tick += new EventHandler(_timer_Tick);
    }

    private void OnRecordingFailed(int idSchedule)
    {
      Schedule failedSchedule = ServiceAgents.Instance.ScheduleServiceAgent.GetSchedule(idSchedule);
      if (failedSchedule != null)
      {
          Log.Debug("TVPlugIn: No free card available for {0}. Notifying user.", failedSchedule.programName);

          Notify(GUILocalizeStrings.Get(1004),
                 String.Format("{0}. {1}", failedSchedule.programName, GUILocalizeStrings.Get(200055)),
                 TVHome.Navigator.Channel.Entity);
      }
    }    

    private void OnRecordingStarted(int idRecording)
    {

      Server.TVDatabase.Entities.Recording startedRec = _recordingServiceAgent.GetRecording(idRecording);
      if (startedRec != null)
      {
        Server.TVDatabase.Entities.Schedule parentSchedule = startedRec.Schedule;
        if (parentSchedule != null && parentSchedule.id_Schedule > 0)
        {
          string endTime = string.Empty;
          endTime = parentSchedule.endTime.AddMinutes(parentSchedule.postRecordInterval).ToString("t",
                                                                                                  CultureInfo.
                                                                                                    CurrentCulture.
                                                                                                    DateTimeFormat);
          string text = String.Format("{0} {1}-{2}",
                                      startedRec.title,
                                      startedRec.startTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                      endTime);
          //Recording started                            
          Notify(GUILocalizeStrings.Get(1446), text, startedRec.Channel);
        }
      }
    }

    private void OnRecordingEnded(int idRecording)
    {
      Recording stoppedRec = _recordingServiceAgent.GetRecording(idRecording);
      if (stoppedRec != null)
      {
        string textPrg;
        IList<Program> prgs = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByTitleAndTimesInterval(stoppedRec.title, stoppedRec.startTime,
                                                                      stoppedRec.endTime).ToList();        
        Program prg = null;
        if (prgs != null && prgs.Count > 0)
        {
          prg = prgs[0];
          }
        if (prg != null)
        {
          textPrg = String.Format("{0} {1}-{2}",
                                  prg.title,
                                  prg.startTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                  prg.endTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));
        }
        else
        {          
          textPrg = String.Format("{0} {1}-{2}",
                                  stoppedRec.title,
                                  stoppedRec.startTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                  DateTime.Now.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));
        }
        //Recording stopped:                                    
        Notify(GUILocalizeStrings.Get(1447), textPrg, stoppedRec.Channel);
      }
    }

    public void Start()
      {
      Log.Info("TvNotify: start");

      if (_enableRecNotification)
      {
        EventServiceAgent.RegisterTvServerEventCallbacks(this);        
      }
      _timer.Start();
    }    

    public void Stop()
    {
      Log.Info("TvNotify: stop");

      if (_enableRecNotification)
      {
        EventServiceAgent.UnRegisterTvServerEventCallbacks(this);
      }
      _timer.Stop();
    }

    public static bool RecordingNotificationEnabled
      {
      get { return _enableRecNotification; }
        }


    public static void OnNotifiesChanged()
    {
      Log.Info("TvNotify:OnNotifiesChanged");
      _notifiesListChanged = true;
    }

    private void LoadNotifies()
    {
      try
      {
        Log.Info("TvNotify:LoadNotifies");
        IEnumerable<Program> prgs = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByState(ProgramState.Notify);
        _notifiesList.Clear();
        foreach (var program in prgs)
        {
          _notifiesList.Add(new ProgramBLL(program));
        }

        if (_notifiesList != null)
        {
          Log.Info("TvNotify: {0} notifies", _notifiesList.Count);
        }

      }
      catch (Exception e)
      {
        Log.Error("TvNotify:LoadNotifies exception : {0}", e.Message);
      }
    }

    private void Notify(string heading, string mainMsg, Channel channel)
    {
      Log.Info("send rec notify");
      var msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_NOTIFY_REC, 0, 0, 0, 0, 0, null)
                  {Label = heading, Label2 = mainMsg, Object = channel};
      GUIGraphicsContext.SendMessage(msg);
      Log.Info("send rec notify done");
    }

    private void ProcessNotifies(DateTime preNotifySecs)
    {
      if (_notifiesListChanged)
      {
        LoadNotifies();
        _notifiesListChanged = false;
      }
      if (_notifiesList != null && _notifiesList.Count > 0)
      {
        foreach (ProgramBLL program in _notifiesList)
        {
          if (preNotifySecs > program.Entity.startTime)
          {
            Log.Info("Notify {0} on {1} start {2}", program.Entity.title, program.Entity.Channel.displayName,
                     program.Entity.startTime);
            program.Notify = false;
            ServiceAgents.Instance.ProgramServiceAgent.SaveProgram(program.Entity);
            var tvProg = new TVProgramDescription
                           {
                             Channel = program.Entity.Channel,
                             Title = program.Entity.title,
                             Description = program.Entity.description,
                             Genre = TVUtil.GetCategory(program.Entity.ProgramCategory),
                             StartTime = program.Entity.startTime,
                             EndTime = program.Entity.endTime
                           };

            _notifiesList.Remove(program);
            Log.Info("send notify");
            GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_NOTIFY_TV_PROGRAM, 0, 0, 0, 0, 0, null);
            msg.Object = tvProg;
            GUIGraphicsContext.SendMessage(msg);
            msg = null;
            Log.Info("send notify done");
            return;
          }
        }
      }
    }

    private void _timer_Tick(object sender, EventArgs e)
    {
      try
      {
        if (!TVHome.Connected)
        {
          return;
        }
        
        if (_busy)
        {
          return;
        }

        _busy = true;

        if (!TVHome.Connected)
        {
          return;
        }

      
        DateTime preNotifySecs = DateTime.Now.AddSeconds(_preNotifyConfig);
        ProcessNotifies(preNotifySecs);
      }
      catch (Exception ex)
      {        
        Log.Error("Tv NotifyManager: Exception at timer_tick {0} st : {1}", ex.ToString(), Environment.StackTrace);
      }
      finally
      {
        _busy = false;
      }
    }

    #region Implementation of ITvServerEventEventCallbacks

    public void CallbackTvServerEvent(TvServerEventArgs eventArgs)
    {
      switch (eventArgs.EventType)
      {
        case TvServerEventType.RecordingEnded:
          OnRecordingEnded(eventArgs.Recording);
          break;

        case TvServerEventType.RecordingStarted:
          OnRecordingStarted(eventArgs.Recording);
          break;

        case TvServerEventType.RecordingFailed:
          OnRecordingFailed(eventArgs.Schedule);
          break;
      }     
    }

    #endregion
  }
}