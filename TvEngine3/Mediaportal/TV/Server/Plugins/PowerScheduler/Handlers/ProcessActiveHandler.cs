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

#region Usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mediaportal.TV.Server.Plugins.PowerScheduler.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using MediaPortal.Common.Utils;
using TvEngine.PowerScheduler.Interfaces;

#endregion

namespace Mediaportal.TV.Server.Plugins.PowerScheduler.Handlers
{
  /// <summary>
  /// Prevents standby when the given process is active
  /// </summary>
  public class ProcessActiveHandler : IStandbyHandler
  {
    #region Variables

    private List<string> _processes = new List<string>();
    private List<string> _preventers = new List<string>();

    #endregion

    #region Constructor

    public ProcessActiveHandler()
    {
      if (GlobalServiceProvider.Instance.IsRegistered<IPowerScheduler>())
        GlobalServiceProvider.Instance.Get<IPowerScheduler>().OnPowerSchedulerEvent +=
          new PowerSchedulerEventHandler(ProcessActiveHandler_OnPowerSchedulerEvent);
    }

    #endregion

    #region Private methods

    private void ProcessActiveHandler_OnPowerSchedulerEvent(PowerSchedulerEventArgs args)
    {
      switch (args.EventType)
      {
        case PowerSchedulerEventType.Started:
        case PowerSchedulerEventType.Elapsed:
          IPowerScheduler ps = GlobalServiceProvider.Instance.Get<IPowerScheduler>();
          if (ps == null)
            return;
          PowerSetting setting = ps.Settings.GetSetting("Processes");
          
          string processString = SettingsManagement.GetSetting("PowerSchedulerProcesses").value;
          List<string> processes = new List<string>();
          foreach (string process in processString.Split(','))
            processes.Add(process.Trim());
          if (!IsEqual(processes, setting.Get<List<string>>()))
          {
            setting.Set<List<string>>(processes);
            _processes = processes;
            foreach (string process in processes)
              Log.Debug("PowerScheduler: preventing standby for process: {0}", process);
          }
          break;
      }
    }

    private bool IsEqual(List<string> oldConfig, List<string> newConfig)
    {
      if (oldConfig != null && newConfig == null)
        return false;
      if (oldConfig == null && newConfig != null)
        return false;
      foreach (string s in oldConfig)
        if (!newConfig.Contains(s))
          return false;
      foreach (string s in newConfig)
        if (!oldConfig.Contains(s))
          return false;
      return true;
    }

    #endregion

    #region IStandbyHandler implementation

    public bool DisAllowShutdown
    {
      get
      {
        // dont use GetProcessesByName, it leaks
        _preventers.Clear();
        Process[] runningProcesses = Process.GetProcesses();

        foreach (Process p1 in runningProcesses)
        {
          foreach (string p2 in _processes)
          {
            if (string.Equals(p1.ProcessName, p2, StringComparison.OrdinalIgnoreCase))
            {
              _preventers.Add(p2);
              break;
            }
          }
          p1.Dispose();
        }
        return (_preventers.Count > 0);
      }
    }

    public void UserShutdownNow() {}

    public string HandlerName
    {
      get
      {
        string preventers = String.Empty;
        foreach (string preventer in _preventers)
          preventers += String.Format(" {0}", preventer);
        return String.Format("ProcessActiveHandler:{0}", preventers);
      }
    }

    #endregion
  }
}