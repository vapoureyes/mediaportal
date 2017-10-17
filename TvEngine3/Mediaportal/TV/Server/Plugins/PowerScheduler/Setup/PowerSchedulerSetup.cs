#region Copyright (C) 2005-2013 Team MediaPortal

// Copyright (C) 2005-2013 Team MediaPortal
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
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Mediaportal.TV.Server.Plugins.PowerScheduler.Interfaces;
using TvEngine.PowerScheduler.Interfaces;
#if SERVER
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.TVControl.ServiceAgents;
#endif
#if CLIENT
using MediaPortal.Configuration;
using MediaPortal.Profile;
using MediaPortal.UserInterface.Controls;
#endif

#endregion

namespace PowerScheduler.Setup
{
  /// <summary>
  /// Setup for the PowerScheduler plugin (common code for client and server)
  /// </summary>
#if SERVER
  public partial class PowerSchedulerSetup : SectionSettings
#else
  public partial class PowerSchedulerSetup : MPConfigForm
#endif
  {
    #region Variables

#if SERVER
    private Thread _refreshStatusThread;
    private Thread _setupTvThread;

#endif
#if CLIENT
    private Settings _settings;
    private bool _singleSeat;

#endif
    private const bool AC = true;
    private const bool DC = false;

    private PowerSettingsForm.PowerSettings _defaultSettingsDesktopAC = new PowerSettingsForm.PowerSettings
    {
      allowAwayMode = true,
      requirePassword = false,
      hybridSleep = true,
      allowWakeTimers = true,
      idleTimeout = 30,
      hibernateAfter = 0,
      lidCloseAction = 1,
      powerButtonAction = 1,
      sleepButtonAction = 1,
      whenSharingMedia = 0,
    };

    private PowerSettingsForm.PowerSettings _defaultSettingsDesktopDC = new PowerSettingsForm.PowerSettings
    {
      allowAwayMode = false,
      requirePassword = false,
      hybridSleep = true,
      allowWakeTimers = true,
      idleTimeout = 15,
      hibernateAfter = 0,
      lidCloseAction = 1,
      powerButtonAction = 1,
      sleepButtonAction = 1,
      whenSharingMedia = 0,
    };

    private PowerSettingsForm.PowerSettings _defaultSettingsNotebookAC = new PowerSettingsForm.PowerSettings
    {
      allowAwayMode = true,
      requirePassword = true,
      hybridSleep = false,
      allowWakeTimers = false,
      idleTimeout = 30,
      hibernateAfter = 360,
      lidCloseAction = 1,
      powerButtonAction = 1,
      sleepButtonAction = 1,
      whenSharingMedia = 0,
    };

    private PowerSettingsForm.PowerSettings _defaultSettingsNotebookDC = new PowerSettingsForm.PowerSettings
    {
      allowAwayMode = false,
      requirePassword = true,
      hybridSleep = false,
      allowWakeTimers = false,
      idleTimeout = 15,
      hibernateAfter = 360,
      lidCloseAction = 1,
      powerButtonAction = 1,
      sleepButtonAction = 1,
      whenSharingMedia = 0,
    };

    private PowerSettingsForm.PowerSettings _recommendedSettingsAC;
    private PowerSettingsForm.PowerSettings _recommendedSettingsDC;

    #endregion

    #region Public Methods

    public PowerSchedulerSetup()
#if SERVER
      : base("PowerScheduler")
#endif
    {
      InitializeComponent();
#if SERVER
      // Add server profile, no Client tab
      textBoxProfile.Text +=
        Environment.NewLine + "Server:	    Dedicated server without GUI provides TV and recording services";
      comboBoxProfile.Items.Add("Server");
      tabControl.Controls.Remove(tabPageClient);
#endif
#if CLIENT

      // Change sizes and locations for client GUI
      Size = new Size(508, 372);                       // Original size is 484, 426
      tabControl.Location = new Point(4, 4);           // Move 4 px out of the top left corner
      tabControl.Size = new Size(484, 290);            // Reset to original size (undo autosize)
      buttonApply.Location = new Point(413, 300);      // Move 4 px (original pos is 409, 296)
      buttonApply.Size = new Size(75, 23);             // Reset to original size (undo autosize)

      // Ok-Button, no EPG / Legacy tab, no server only options and no status 
      buttonApply.Text = "Ok";
      tabControl.Controls.Remove(tabPageEPG);
      tabControl.Controls.Remove(tabPageLegacy);
      checkBoxMPClientRunning.Visible = false;
      checkBoxReinitializeController.Visible = false;
      groupBoxStatus.Visible = false;

      // Move command controls to Client tab
      groupBoxAdvanced.Controls.Remove(buttonCommand);
      groupBoxAdvanced.Controls.Remove(textBoxCommand);
      groupBoxAdvanced.Controls.Remove(labelCommand);
      groupBoxClient.Controls.Add(buttonCommand);
      groupBoxClient.Controls.Add(textBoxCommand);
      groupBoxClient.Controls.Add(labelCommand);

#endif

      // For Windows XP change away mode label texts
      if (Environment.OSVersion.Version.Major < 6)
      {
        checkBoxEPGAwayMode.Text = "Prevent the user from putting the computer to sleep";
        checkBoxProcessesAwayMode.Text = "Prevent the user from putting the computer to sleep";
        checkBoxNetworkAwayMode.Text = "Prevent the user from putting the computer to sleep";
        checkBoxSharesAwayMode.Text = "Prevent the user from putting the computer to sleep";
      }
#if CLIENT
      
      LoadSettings();
#endif
    }

#if SERVER
    public override void LoadSettings()
    {
      {
#else
    private void LoadSettings()
    {
      buttonApply.Enabled = false;

      using (_settings = new MPSettings())
      {
        // Detect singleseat/multiseat
        string tvPluginDll;
        string hostName;

        tvPluginDll = Config.GetSubFolder(Config.Dir.Plugins, "windows") + @"\" + "TvPlugin.dll";
        if (File.Exists(tvPluginDll))
        {
          hostName = _settings.GetValueAsString("tvservice", "hostname", String.Empty);
          if (hostName != String.Empty && PowerManager.IsLocal(hostName))
          {
            _singleSeat = true;
            Text = "PowerScheduler Client Plugin (TV-Server on local system)";
          }
          else if (hostName == String.Empty)
          {
            _singleSeat = false;
            Text = "PowerScheduler Client Plugin (No TV-Server configured)";
          }
          else
          {
            _singleSeat = false;
            Text = "PowerScheduler Client Plugin (TV-Server on " + hostName + ")";
          }
        }
        else
        {
          _singleSeat = false;
          Text = "PowerScheduler Client Plugin (No TV-plugin installed)";
        }

        if (_singleSeat)
        {
          textBoxProfile.Text = "Standby / wakeup settings have to be made in the TV-Server Configuration";
          comboBoxProfile.Visible = false;
          flowLayoutPanelIdleTimeout.Visible = false;
          buttonExpertMode.Visible = false;
          labelExpertMode.Visible = false;
          
          tabControl.Controls.Remove(tabPageReboot);
          tabControl.Controls.Remove(tabPageProcesses);
          tabControl.Controls.Remove(tabPageNetwork);
          tabControl.Controls.Remove(tabPageShares);
          tabControl.Controls.Remove(tabPageAdvanced);

          checkBoxHomeOnly.Checked = GetSetting("HomeOnly", false);
          textBoxCommand.Text = GetSetting("Command", string.Empty);
          return;
        }

#endif

        bool buttonApplyEnabled = false;

        // General
        int profile = GetSetting("Profile", -1);
#if SERVER
        if (profile < 0 || profile > 3)
#else
        if (profile < 0 || profile > 2)
#endif
        {
          profile = GetSystemProfile();
          buttonApplyEnabled = true;
        }
        comboBoxProfile.SelectedIndex = profile;

        int idleTimeout = GetSetting("IdleTimeout", -1);
        if (idleTimeout == -1)
        {
          idleTimeout = (int)(PowerManager.RunningOnAC ? _defaultSettingsDesktopAC.idleTimeout : _defaultSettingsDesktopDC.idleTimeout);
          buttonApplyEnabled = true;
        }
        numericUpDownIdleTimeout.Value = idleTimeout;

        labelExpertMode.Text = GetSetting("ExpertMode", false) ? "Expert Mode" : "Plug&&Play Mode";
        if (labelExpertMode.Text == "Expert Mode")
        {
          comboBoxProfile.ForeColor = SystemColors.GrayText;
          buttonExpertMode.Text = "-> Plug&&Play Mode";
        }
        else
        {
          checkBoxEPGPreventStandby.Visible = false;
          checkBoxEPGAwayMode.Visible = false;
          labelEPGCommand.Visible = false;
          textBoxEPGCommand.Visible = false;
          buttonEPGCommand.Visible = false;

          labelRebootCommand.Visible = false;
          textBoxRebootCommand.Visible = false;
          buttonRebootCommand.Visible = false;

          checkBoxAutoPowerSettings.Checked = true;

          tabControl.Controls.Remove(tabPageProcesses);
          tabControl.Controls.Remove(tabPageShares);
          tabControl.Controls.Remove(tabPageNetwork);
          tabControl.Controls.Remove(tabPageAdvanced);
          tabControl.Controls.Remove(tabPageLegacy);

          buttonExpertMode.Text = "-> Expert Mode";
        }

#if CLIENT
        // Client
        checkBoxHomeOnly.Checked = GetSetting("HomeOnly", false);

        textBoxCommand.Text = GetSetting("Command", string.Empty);

#endif
#if SERVER
        // EPG
        {
          EPGWakeupConfig config = new EPGWakeupConfig(GetSetting("EPGWakeupConfig", String.Empty));
          foreach (EPGGrabDays day in config.Days)
          {
            switch (day)
            {
              case EPGGrabDays.Monday:
                checkBoxEPGMonday.Checked = true;
                break;
              case EPGGrabDays.Tuesday:
                checkBoxEPGTuesday.Checked = true;
                break;
              case EPGGrabDays.Wednesday:
                checkBoxEPGWednesday.Checked = true;
                break;
              case EPGGrabDays.Thursday:
                checkBoxEPGThursday.Checked = true;
                break;
              case EPGGrabDays.Friday:
                checkBoxEPGFriday.Checked = true;
                break;
              case EPGGrabDays.Saturday:
                checkBoxEPGSaturday.Checked = true;
                break;
              case EPGGrabDays.Sunday:
                checkBoxEPGSunday.Checked = true;
                break;
            }
          }
          string hFormat, mFormat;
          if (config.Hour < 10)
            hFormat = "0{0}";
          else
            hFormat = "{0}";
          if (config.Minutes < 10)
            mFormat = "0{0}";
          else
            mFormat = "{0}";
          textBoxEPG.Text = String.Format(hFormat, config.Hour) + ":" + String.Format(mFormat, config.Minutes);

          checkBoxEPGPreventStandby.Checked = GetSetting("EPGPreventStandby", false);
          if (!checkBoxEPGPreventStandby.Checked)
            checkBoxEPGAwayMode.Enabled = false;

          checkBoxEPGAwayMode.Checked = GetSetting("EPGAwayMode", false);

          checkBoxEPGWakeup.Checked = GetSetting("EPGWakeup", false);

          textBoxEPGCommand.Text = GetSetting("EPGCommand", String.Empty);
        }

#endif

        // Reboot
        {
          EPGWakeupConfig config = new EPGWakeupConfig(GetSetting("RebootConfig", String.Empty));
          foreach (EPGGrabDays day in config.Days)
          {
            switch (day)
            {
              case EPGGrabDays.Monday:
                checkBoxRebootMonday.Checked = true;
                break;
              case EPGGrabDays.Tuesday:
                checkBoxRebootTuesday.Checked = true;
                break;
              case EPGGrabDays.Wednesday:
                checkBoxRebootWednesday.Checked = true;
                break;
              case EPGGrabDays.Thursday:
                checkBoxRebootThursday.Checked = true;
                break;
              case EPGGrabDays.Friday:
                checkBoxRebootFriday.Checked = true;
                break;
              case EPGGrabDays.Saturday:
                checkBoxRebootSaturday.Checked = true;
                break;
              case EPGGrabDays.Sunday:
                checkBoxRebootSunday.Checked = true;
                break;
            }
          }
          string hFormat, mFormat;
          if (config.Hour < 10)
            hFormat = "0{0}";
          else
            hFormat = "{0}";
          if (config.Minutes < 10)
            mFormat = "0{0}";
          else
            mFormat = "{0}";
          textBoxReboot.Text = String.Format(hFormat, config.Hour) + ":" + String.Format(mFormat, config.Minutes);

          checkBoxRebootWakeup.Checked = GetSetting("RebootWakeup", false);

          textBoxRebootCommand.Text = GetSetting("RebootCommand", String.Empty);
        }

        // Processes
        textBoxProcesses.Text = GetSetting("Processes", String.Empty);
#if SERVER

        checkBoxMPClientRunning.Checked = GetSetting("CheckForMPClientRunning", false);
#endif
        if (!checkBoxMPClientRunning.Checked && textBoxProcesses.Text == String.Empty)
          checkBoxProcessesAwayMode.Enabled = false;

        checkBoxProcessesAwayMode.Checked = GetSetting("ProcessesAwayMode", false);

        // Active Shares
        checkBoxSharesEnabled.Checked = GetSetting("ActiveSharesEnabled", false);
        if (!checkBoxSharesEnabled.Checked)
        {
          checkBoxSharesAwayMode.Enabled = false;
          labelShares.ForeColor = SystemColors.GrayText;
          dataGridShares.Enabled = false;
          dataGridShares.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.GrayText;
          dataGridShares.RowHeadersDefaultCellStyle.ForeColor = SystemColors.GrayText;
          dataGridShares.DefaultCellStyle.ForeColor = SystemColors.GrayText;
          buttonSelectShare.Enabled = false;
        }

        checkBoxSharesAwayMode.Checked = GetSetting("ActiveSharesAwayMode", false);

        dataGridShares.Rows.Clear();
        string[] shares = GetSetting("ActiveShares", String.Empty).Split(';');
        foreach (string share in shares)
        {
          string[] shareItem = share.Split(',');
          if ((shareItem.Length.Equals(3)) &&
             ((shareItem[0].Trim().Length > 0) ||
              (shareItem[1].Trim().Length > 0) ||
              (shareItem[2].Trim().Length > 0)))
          {
            dataGridShares.Rows.Add(shareItem);
          }
        }
        if (dataGridShares.Rows.Count == 1)
        {
          string[] shareItem = { "", "", "" };
          dataGridShares.Rows.Add(shareItem);
        }

        // Network Monitor
        checkBoxNetworkEnabled.Checked = GetSetting("NetworkMonitorEnabled", false);
        if (!checkBoxNetworkEnabled.Checked)
          checkBoxNetworkAwayMode.Enabled = false;

        numericUpDownNetworkIdleLimit.Value = GetSetting("NetworkMonitorIdleLimit", 0);

        checkBoxNetworkAwayMode.Checked = GetSetting("NetworkMonitorAwayMode", false);

        // Advanced
#if SERVER
        checkBoxReinitializeController.Checked = GetSetting("ReinitializeController", false);

        textBoxCommand.Text = GetSetting("Command", string.Empty);

#endif
        checkBoxAutoPowerSettings.Checked = GetSetting("AutoPowerSettings", true);

        checkBoxShutdownEnabled.Checked = GetSetting("ShutdownEnabled", false);
        if (!checkBoxShutdownEnabled.Checked)
        {
          labelShutdownMode.ForeColor = SystemColors.GrayText;
          comboBoxShutdownMode.Enabled = false;
          numericUpDownIdleTimeout.Enabled = checkBoxAutoPowerSettings.Checked;
        }

        int shutdownMode = GetSetting("ShutdownMode", 0);
        if (shutdownMode < 0 || shutdownMode > 3)
          shutdownMode = 0;
        comboBoxShutdownMode.SelectedIndex = shutdownMode;

        // Legacy
        numericUpDownPreWakeupTime.Value = GetSetting("PreWakeupTime", 60);

        numericUpDownPreNoStandbyTime.Value = GetSetting("PreNoStandbyTime", 300);

        numericUpDownStandbyHoursFrom.Value = GetSetting("StandbyHoursFrom", 0);
        numericUpDownStandbyHoursTo.Value = GetSetting("StandbyHoursTo", 24);
        numericUpDownStandbyHoursOnWeekendFrom.Value = GetSetting("StandbyHoursOnWeekendFrom", 0);
        numericUpDownStandbyHoursOnWeekendTo.Value = GetSetting("StandbyHoursOnWeekendTo", 24);

        buttonApply.Enabled = buttonApplyEnabled;

#if SERVER
        // Ping Monitor

        bool _pingMonitorEnabled = GetSetting("PingMonitorEnabled", false);
        checkBoxPingMonitorAwayMode.Checked = GetSetting("PingMonitorAwayMode", false);
        if (_pingMonitorEnabled)
        {
          checkBoxPingMonitorEnable.Checked = true;
          checkBoxPingMonitorAwayMode.Enabled = true;
          buttonAdd.Enabled = true;
          buttonDelete.Enabled = true;
        }
        else
        {
          checkBoxPingMonitorEnable.Checked = false;  
          checkBoxPingMonitorAwayMode.Enabled = false;
          buttonAdd.Enabled = false;
          buttonDelete.Enabled = false;
        }
 
        listBoxHosts.Items.Clear();
        string str = GetSetting("PingMonitorHosts", "");
        if (str != "")
        {
          foreach (string str2 in str.Split(";".ToCharArray()))
          {
            this.listBoxHosts.Items.Add(str2);
          }
        }
#endif

#if CLIENT
      tabControl.Controls.Remove(tabPagePingMonitor);
#endif

      }
#if SERVER

      // Start the RefeshStatusThread responsible for refreshing status information
      _setupTvThread = Thread.CurrentThread;
      _refreshStatusThread = new Thread(RefreshStatusThread);
      _refreshStatusThread.Name = "RefreshStatusThread";
      _refreshStatusThread.IsBackground = true;
      _refreshStatusThread.Start();
#endif
    }

#if SERVER
    public override void SaveSettings()
    {
      {
#else
    private void SaveSettings()
    {
      using (_settings = new MPSettings())
      {
        if (_singleSeat)
        {
          SetSetting("HomeOnly", checkBoxHomeOnly.Checked);
          SetSetting("Command", textBoxCommand.Text);
          return;
        }
#endif
        // General
        SetSetting("Profile", comboBoxProfile.SelectedIndex);
      
        SetSetting("IdleTimeout", (int)numericUpDownIdleTimeout.Value);

        SetSetting("ExpertMode", labelExpertMode.Text == "Expert Mode");

#if CLIENT
        // Client
        SetSetting("HomeOnly", checkBoxHomeOnly.Checked);

        SetSetting("Command", textBoxCommand.Text);

#endif
#if SERVER
        // EPG
        {
          EPGWakeupConfig cfg = new EPGWakeupConfig(GetSetting("EPGWakeupConfig", String.Empty));
          EPGWakeupConfig newcfg = new EPGWakeupConfig();
          newcfg.Hour = cfg.Hour;
          newcfg.Minutes = cfg.Minutes;
          // newcfg.Days = cfg.Days;
          newcfg.LastRun = cfg.LastRun;
          string[] time = textBoxEPG.Text.Split(System.Globalization.DateTimeFormatInfo.CurrentInfo.TimeSeparator[0]);
          newcfg.Hour = Convert.ToInt32(time[0]);
          newcfg.Minutes = Convert.ToInt32(time[1]);
          CheckDay(newcfg, EPGGrabDays.Monday, checkBoxEPGMonday.Checked);
          CheckDay(newcfg, EPGGrabDays.Tuesday, checkBoxEPGTuesday.Checked);
          CheckDay(newcfg, EPGGrabDays.Wednesday, checkBoxEPGWednesday.Checked);
          CheckDay(newcfg, EPGGrabDays.Thursday, checkBoxEPGThursday.Checked);
          CheckDay(newcfg, EPGGrabDays.Friday, checkBoxEPGFriday.Checked);
          CheckDay(newcfg, EPGGrabDays.Saturday, checkBoxEPGSaturday.Checked);
          CheckDay(newcfg, EPGGrabDays.Sunday, checkBoxEPGSunday.Checked);

          if (!cfg.Equals(newcfg))
          {
            SetSetting("EPGWakeupConfig", newcfg.SerializeAsString());
          }
        }

        SetSetting("EPGPreventStandby", checkBoxEPGPreventStandby.Checked);

        SetSetting("EPGAwayMode", checkBoxEPGAwayMode.Checked);

        SetSetting("EPGWakeup", checkBoxEPGWakeup.Checked);

        SetSetting("EPGCommand", textBoxEPGCommand.Text);

#endif
        // Reboot
        {
          EPGWakeupConfig cfg = new EPGWakeupConfig(GetSetting("RebootConfig", String.Empty));
          EPGWakeupConfig newcfg = new EPGWakeupConfig();
          newcfg.Hour = cfg.Hour;
          newcfg.Minutes = cfg.Minutes;
          // newcfg.Days = cfg.Days;
          newcfg.LastRun = cfg.LastRun;
          string[] time = textBoxReboot.Text.Split(System.Globalization.DateTimeFormatInfo.CurrentInfo.TimeSeparator[0]);
          newcfg.Hour = Convert.ToInt32(time[0]);
          newcfg.Minutes = Convert.ToInt32(time[1]);
          CheckDay(newcfg, EPGGrabDays.Monday, checkBoxRebootMonday.Checked);
          CheckDay(newcfg, EPGGrabDays.Tuesday, checkBoxRebootTuesday.Checked);
          CheckDay(newcfg, EPGGrabDays.Wednesday, checkBoxRebootWednesday.Checked);
          CheckDay(newcfg, EPGGrabDays.Thursday, checkBoxRebootThursday.Checked);
          CheckDay(newcfg, EPGGrabDays.Friday, checkBoxRebootFriday.Checked);
          CheckDay(newcfg, EPGGrabDays.Saturday, checkBoxRebootSaturday.Checked);
          CheckDay(newcfg, EPGGrabDays.Sunday, checkBoxRebootSunday.Checked);

          if (!cfg.Equals(newcfg))
          {
            SetSetting("RebootConfig", newcfg.SerializeAsString());
          }
        }

        SetSetting("RebootWakeup", checkBoxRebootWakeup.Checked);

        SetSetting("RebootCommand", textBoxRebootCommand.Text);

        // Processes
        SetSetting("Processes", textBoxProcesses.Text);

        SetSetting("ProcessesAwayMode", checkBoxProcessesAwayMode.Checked);

#if SERVER
        SetSetting("CheckForMPClientRunning", checkBoxMPClientRunning.Checked);

#endif
        // Active Shares
        SetSetting("ActiveSharesEnabled", checkBoxSharesEnabled.Checked);

        SetSetting("ActiveSharesAwayMode", checkBoxSharesAwayMode.Checked);

        StringBuilder shares = new StringBuilder();
        foreach (DataGridViewRow row in dataGridShares.Rows)
        {
          shares.AppendFormat("{0},{1},{2};", row.Cells[0].Value, row.Cells[1].Value, row.Cells[2].Value);
        }
        SetSetting("ActiveShares", shares.ToString());

        // Network Monitor
        SetSetting("NetworkMonitorEnabled", checkBoxNetworkEnabled.Checked);

        SetSetting("NetworkMonitorIdleLimit", (int)numericUpDownNetworkIdleLimit.Value);

        SetSetting("NetworkMonitorAwayMode", checkBoxNetworkAwayMode.Checked);

        // Ping Monitor

        SetSetting("PingMonitorEnabled", checkBoxPingMonitorEnable.Checked);
        SetSetting("PingMonitorAwayMode", checkBoxPingMonitorAwayMode.Checked);

        string str = "";
        for (int i = 0; i < this.listBoxHosts.Items.Count; i++)
        {
          str = str + this.listBoxHosts.Items[i].ToString() + ";";
        }
        str = str.TrimEnd(";".ToCharArray());

        SetSetting("PingMonitorHosts", str);

        // Advanced
#if SERVER
        SetSetting("ReinitializeController", checkBoxReinitializeController.Checked);

        SetSetting("Command", textBoxCommand.Text);

#endif
        SetSetting("AutoPowerSettings", checkBoxAutoPowerSettings.Checked);

        SetSetting("ShutdownEnabled", checkBoxShutdownEnabled.Checked);

        SetSetting("ShutdownMode", comboBoxShutdownMode.SelectedIndex);

        // Legacy
        SetSetting("PreWakeupTime", (int)numericUpDownPreWakeupTime.Value);

        SetSetting("PreNoStandbyTime", (int)numericUpDownPreNoStandbyTime.Value);

        SetSetting("StandbyHoursFrom", (int)numericUpDownStandbyHoursFrom.Value);
        SetSetting("StandbyHoursTo", (int)numericUpDownStandbyHoursTo.Value);
        SetSetting("StandbyHoursOnWeekendFrom", (int)numericUpDownStandbyHoursOnWeekendFrom.Value);
        SetSetting("StandbyHoursOnWeekendTo", (int)numericUpDownStandbyHoursOnWeekendTo.Value);

        // Power settings
        if (checkBoxAutoPowerSettings.Checked)
        {
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.ALLOW_AWAY_MODE, AC, (uint)(_recommendedSettingsAC.allowAwayMode ? 1 : 0));
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.LOCK_CONSOLE_ON_WAKE, AC, (uint)(_recommendedSettingsAC.requirePassword ? 1 : 0));
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.ALLOW_HYBRID_SLEEP, AC, (uint)(_recommendedSettingsAC.hybridSleep ? 1 : 0));
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.ALLOW_RTC_WAKE, AC, (uint)(_recommendedSettingsAC.allowWakeTimers ? 1 : 0));
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.STANDBYIDLE, AC, _recommendedSettingsAC.idleTimeout * 60);
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.HIBERNATE_AFTER, AC, _recommendedSettingsAC.hibernateAfter * 60);
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.LID_CLOSE_ACTION, AC, _recommendedSettingsAC.lidCloseAction);
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.POWER_BUTTON_ACTION, AC, _recommendedSettingsAC.powerButtonAction);
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.SLEEP_BUTTON_ACTION, AC, _recommendedSettingsAC.sleepButtonAction);
          PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.WHEN_SHARING_MEDIA, AC, _recommendedSettingsAC.whenSharingMedia);

          if (PowerManager.HasDCPowerSource)
          {
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.ALLOW_AWAY_MODE, DC, (uint)(_recommendedSettingsDC.allowAwayMode ? 1 : 0));
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.LOCK_CONSOLE_ON_WAKE, DC, (uint)(_recommendedSettingsDC.requirePassword ? 1 : 0));
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.ALLOW_HYBRID_SLEEP, DC, (uint)(_recommendedSettingsDC.hybridSleep ? 1 : 0));
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.ALLOW_RTC_WAKE, DC, (uint)(_recommendedSettingsDC.allowWakeTimers ? 1 : 0));
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.STANDBYIDLE, DC, _recommendedSettingsDC.idleTimeout * 60);
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.HIBERNATE_AFTER, DC, _recommendedSettingsDC.hibernateAfter * 60);
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.LID_CLOSE_ACTION, DC, _recommendedSettingsDC.lidCloseAction);
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.POWER_BUTTON_ACTION, DC, _recommendedSettingsDC.powerButtonAction);
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.SLEEP_BUTTON_ACTION, DC, _recommendedSettingsDC.sleepButtonAction);
            PowerManager.SetPowerSetting(PowerManager.SystemPowerSettingType.WHEN_SHARING_MEDIA, DC, _recommendedSettingsDC.whenSharingMedia);
          }
        }
      }
#if SERVER

      RefreshStatus();
#endif
    }

    #endregion

    #region Private Methods

    private string GetSetting(string settingName, string settingDefault)
    {
#if SERVER
      return ServiceAgents.Instance.SettingServiceAgent.GetValue("PowerScheduler" + settingName, settingDefault);
#endif
#if CLIENT
      return _settings.GetValueAsString("psclientplugin", settingName, settingDefault);
#endif
    }

    private int GetSetting(string settingName, int settingDefault)
    {
#if SERVER
      return ServiceAgents.Instance.SettingServiceAgent.GetValue("PowerScheduler" + settingName, settingDefault);
#endif
#if CLIENT
      return _settings.GetValueAsInt("psclientplugin", settingName, settingDefault);
#endif
    }

    private bool GetSetting(string settingName, bool settingDefault)
    {
#if SERVER
      return ServiceAgents.Instance.SettingServiceAgent.GetValue("PowerScheduler" + settingName, settingDefault);
#endif
#if CLIENT
      return _settings.GetValueAsBool("psclientplugin", settingName, settingDefault);
#endif
    }

    private void SetSetting(string settingName, string settingValue)
    {
#if SERVER
      ServiceAgents.Instance.SettingServiceAgent.SaveValue("PowerScheduler" + settingName, settingValue);
#endif
#if CLIENT
      _settings.SetValue("psclientplugin", settingName, settingValue);
#endif
    }

    private void SetSetting(string settingName, int settingValue)
    {
#if SERVER
      ServiceAgents.Instance.SettingServiceAgent.SaveValue("PowerScheduler" + settingName, settingValue);
#endif
#if CLIENT
      SetSetting(settingName, settingValue.ToString());
#endif
    }

    private void SetSetting(string settingName, bool settingValue)
    {
#if SERVER
      ServiceAgents.Instance.SettingServiceAgent.SaveValue("PowerScheduler" + settingName, settingValue);
#endif
#if CLIENT
      _settings.SetValueAsBool("psclientplugin", settingName, settingValue);
#endif
    }

    private int GetSystemProfile()
    {
      Microsoft.Win32.RegistryKey rkApp;

#if SERVER
      // See if MediaPortal client is not installed (64 bit or 32 bit)
      rkApp = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MediaPortal", true);
      if (rkApp == null)
      {
        rkApp = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MediaPortal", true);
        if (rkApp == null)
        {
          return 3;     // Server
        }
      }

#endif
      // See if MediaPortal client is configured for autostart
      rkApp = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
      if (rkApp.GetValue("MediaPortal") == null)
      {
        if (PowerManager.HasDCPowerSource)
          return 2;   // Notebook
        return 1;     // Desktop
      }
      return 0;       // HTPC
    }

    private void CheckDay(EPGWakeupConfig cfg, EPGGrabDays day, bool enabled)
    {
      if (enabled)
        cfg.Days.Add(day);
    }
#if SERVER

    private void RefreshStatusThread()
    {
      while (_setupTvThread.IsAlive)
      {
        RefreshStatus();
        Thread.Sleep(5000);
      }
    }

    private void RefreshStatus()
    {
      bool unattended, disAllowShutdown = false;
      DateTime nextWakeupTime = DateTime.MaxValue;
      string disAllowShutdownHandler = "";
      string nextWakeupHandler = "";

      lock (this)
      {
        // Connect to the local TVserver's IPowerController instance
        if (RemotePowerControl.Instance != null && RemotePowerControl.Instance.IsConnected())
          RemotePowerControl.Instance.GetCurrentState(true, out unattended, out disAllowShutdown, out disAllowShutdownHandler, out nextWakeupTime, out nextWakeupHandler);
      }

      labelWakeupHandler.Text = nextWakeupHandler;
      if (nextWakeupHandler != String.Empty)
        labelWakeupTimeValue.Text = nextWakeupTime.ToString();
      else
        labelWakeupTimeValue.Text = "";

      if (GetSetting("ShutdownEnabled", false))
      {
        labelStandbyStatus.Text = "Standby is handled by PowerScheduler";
        textBoxStandbyHandler.Text = disAllowShutdownHandler;
      }
      else
      {
        labelStandbyStatus.Text = "Standby is handled by Windows";
        if (string.IsNullOrEmpty(disAllowShutdownHandler))
        {
          textBoxStandbyHandler.Text = PowerManager.GetPowerCfgRequests(true);   // show tvservice.exe
        }
        else
        {
          string requests = PowerManager.GetPowerCfgRequests(false);   // do not show tvservice.exe
          if (string.IsNullOrEmpty(requests))
            textBoxStandbyHandler.Text = disAllowShutdownHandler;
          else
            textBoxStandbyHandler.Text = disAllowShutdownHandler + Environment.NewLine + requests;
        }
      }
    }
#endif

    #endregion

    #region Forms Control Events

    #region MasterSetup

    private void buttonApply_Click(object sender, EventArgs e)
    {
      buttonApply.Enabled = false;
      SaveSettings();
#if CLIENT
      Close();
#endif
    }

    private void buttonApply_Enable(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
    }

    private void buttonApply_Enable(object sender, DataGridViewCellEventArgs e)
    {
      buttonApply.Enabled = true;
    }

    #endregion

    #region General tab

    private void comboBoxProfile_SelectedIndexChanged(object sender, EventArgs e)
    {
      // General
      comboBoxProfile.ForeColor = SystemColors.WindowText;
      buttonApply.Enabled = true;

      // EPG
      checkBoxEPGAwayMode.Checked = false;
      textBoxEPGCommand.Text = "";

      // Reboot
      textBoxRebootCommand.Text = "";

      // Processes
      textBoxProcesses.Text = "";
      checkBoxMPClientRunning.Checked = false;
      checkBoxProcessesAwayMode.Checked = false;

      // Shares
      checkBoxSharesEnabled.Checked = false;
      checkBoxSharesAwayMode.Checked = false;

      // Network Monitor
      checkBoxNetworkEnabled.Checked = false;
      checkBoxNetworkAwayMode.Checked = false;

      // Advanced
      checkBoxReinitializeController.Checked = false;
      textBoxCommand.Text = "";
      checkBoxAutoPowerSettings.Checked = true;
      checkBoxShutdownEnabled.Checked = false;

      // Legacy
      numericUpDownPreWakeupTime.Value = 60;
      numericUpDownPreNoStandbyTime.Value = 300;
      
      // Power Settings
      _recommendedSettingsAC = _defaultSettingsDesktopAC;
      _recommendedSettingsDC = _defaultSettingsDesktopDC;

      // Profile specific
      switch (comboBoxProfile.SelectedIndex)
      {
        // HTPC (single-seat)
        case 0:
          // EPG
          checkBoxEPGWakeup.Checked = true;
          checkBoxEPGPreventStandby.Checked = true;
          // Reboot
          checkBoxRebootWakeup.Checked = true;
          // Power Settings
          _recommendedSettingsAC.hybridSleep = false;
          _recommendedSettingsDC.hybridSleep = false;
          break;

        // Desktop
        case 1:
          // EPG
          checkBoxEPGWakeup.Checked = false;
          checkBoxEPGPreventStandby.Checked = false;
          // Reboot
          checkBoxRebootWakeup.Checked = false;
          break;

        // Notebook
        case 2:
          // EPG
          checkBoxEPGWakeup.Checked = false;
          checkBoxEPGPreventStandby.Checked = false;
          // Reboot
          checkBoxRebootWakeup.Checked = false;
          // Power settings
          _recommendedSettingsAC = _defaultSettingsNotebookAC;
          _recommendedSettingsDC = _defaultSettingsNotebookDC;
          break;
#if SERVER

        // Server
        case 3:
          // EPG
          checkBoxEPGWakeup.Checked = true;
          checkBoxEPGPreventStandby.Checked = true;
          // Reboot
          checkBoxRebootWakeup.Checked = true;
          break;
#endif
      }

      // Idle timeout
      if (PowerManager.RunningOnAC)
        _recommendedSettingsAC.idleTimeout = (uint)numericUpDownIdleTimeout.Value;
      else
        _recommendedSettingsAC.idleTimeout = (uint)numericUpDownIdleTimeout.Value * 2;
      _recommendedSettingsDC.idleTimeout = _recommendedSettingsAC.idleTimeout / 2;
    }

    private void numericUpDownIdleTimeout_ValueChanged(object sender, EventArgs e)
    {
      if (numericUpDownIdleTimeout.Enabled)
        buttonApply.Enabled = true;

      if (PowerManager.RunningOnAC)
        _recommendedSettingsAC.idleTimeout = (uint)numericUpDownIdleTimeout.Value;
      else
        _recommendedSettingsAC.idleTimeout = (uint)numericUpDownIdleTimeout.Value * 2;
      _recommendedSettingsDC.idleTimeout = _recommendedSettingsAC.idleTimeout / 2;
    }

    private void numericUpDownIdleTimeout_EnabledChanged(object sender, EventArgs e)
    {
      if (numericUpDownIdleTimeout.Enabled)
      {
        labelIdleTimeout1.ForeColor = SystemColors.ControlText;
        labelIdleTimeout2.ForeColor = SystemColors.ControlText;
        toolTip.SetToolTip(groupBoxGeneral, "");
        toolTip.SetToolTip(flowLayoutPanelIdleTimeout, "");
      }
      else
      {
        labelIdleTimeout1.ForeColor = SystemColors.GrayText;
        labelIdleTimeout2.ForeColor = SystemColors.GrayText;
        toolTip.SetToolTip(groupBoxGeneral,
          "Adjust the time after which the system goes to standby" + Environment.NewLine +
          "by configuring the Windows Power Settings manually." + Environment.NewLine +
          "(Switch to \"Expert Mode\" and open the \"Advanced\" tab.)");
        toolTip.SetToolTip(flowLayoutPanelIdleTimeout,
          "Adjust the time after which the system goes to standby" + Environment.NewLine +
          "by configuring the Windows Power Settings manually." + Environment.NewLine +
          "(Switch to \"Expert Mode\" and open the \"Advanced\" tab.)");
      }
    }

    private void buttonExpertMode_Click(object sender, EventArgs e)
    {
      if (labelExpertMode.Text == "Expert Mode")
      {
        labelExpertMode.Text = "Plug&&Play Mode";
        buttonExpertMode.Text = "-> Expert Mode";
        buttonApply.Enabled = true;

        // Hide expert settings
        checkBoxEPGPreventStandby.Visible = false;
        checkBoxEPGAwayMode.Visible = false;
        labelEPGCommand.Visible = false;
        textBoxEPGCommand.Visible = false;
        buttonEPGCommand.Visible = false;

        labelRebootCommand.Visible = false;
        textBoxRebootCommand.Visible = false;
        buttonRebootCommand.Visible = false;

        tabControl.Controls.Remove(tabPageProcesses);
        tabControl.Controls.Remove(tabPageShares);
        tabControl.Controls.Remove(tabPageNetwork);
        tabControl.Controls.Remove(tabPageAdvanced);
        tabControl.Controls.Remove(tabPageLegacy);

        // Set default values
        comboBoxProfile_SelectedIndexChanged(null, (EventArgs)null);
      }
      else
      {
        labelExpertMode.Text = "Expert Mode";
        buttonExpertMode.Text = "-> Plug&&Play Mode";

        // Show expert settings
        comboBoxProfile.ForeColor = SystemColors.GrayText;

        checkBoxEPGPreventStandby.Visible = true;
        checkBoxEPGAwayMode.Visible = true;
        labelEPGCommand.Visible = true;
        textBoxEPGCommand.Visible = true;
        buttonEPGCommand.Visible = true;

        labelRebootCommand.Visible = true;
        textBoxRebootCommand.Visible = true;
        buttonRebootCommand.Visible = true;

        tabControl.Controls.Add(tabPageProcesses);
        tabControl.Controls.Add(tabPageShares);
        tabControl.Controls.Add(tabPageNetwork);
        tabControl.Controls.Add(tabPageAdvanced);
        tabControl.Controls.Add(tabPageLegacy);
      }
    }

    #endregion

    #region EPG tab

    private void checkBoxEPGPreventStandby_CheckedChanged(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
      if (checkBoxEPGPreventStandby.Checked)
      {
        checkBoxEPGAwayMode.Enabled = true;
      }
      else
      {
        checkBoxEPGAwayMode.Checked = false;
        checkBoxEPGAwayMode.Enabled = false;
      }
    }

    private void buttonEPGCommand_Click(object sender, EventArgs e)
    {
      DialogResult r = openFileDialog.ShowDialog();
      if (r == DialogResult.OK)
      {
        textBoxEPGCommand.Text = openFileDialog.FileName;
      }
    }

    #endregion

    #region Reboot tab

    private void buttonRebootCommand_Click(object sender, EventArgs e)
    {
      DialogResult r = openFileDialog.ShowDialog();
      if (r == DialogResult.OK)
      {
        textBoxRebootCommand.Text = openFileDialog.FileName;
      }
    }

    #endregion

    #region Processes tab

    private void textBoxProcesses_TextChanged(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
      if (checkBoxMPClientRunning.Checked || textBoxProcesses.Text != String.Empty)
      {
        checkBoxProcessesAwayMode.Enabled = true;
      }
      else
      {
        checkBoxProcessesAwayMode.Checked = false;
        checkBoxProcessesAwayMode.Enabled = false;
      }
    }

    private void buttonSelectProcess_Click(object sender, EventArgs e)
    {
      SelectProcessForm spf = new SelectProcessForm();
      DialogResult dr = spf.ShowDialog();
      if (DialogResult.OK == dr)
      {
        if (!spf.SelectedProcess.Equals(""))
        {
          if (textBoxProcesses.Text.Equals(""))
          {
            textBoxProcesses.Text = spf.SelectedProcess;
          }
          else
          {
            textBoxProcesses.Text = String.Format("{0}, {1}", textBoxProcesses.Text, spf.SelectedProcess);
          }
        }
      }
    }

    private void checkBoxMPClientRunning_CheckedChanged(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
      if (checkBoxMPClientRunning.Checked || textBoxProcesses.Text != String.Empty)
      {
        checkBoxProcessesAwayMode.Enabled = true;
      }
      else
      {
        checkBoxProcessesAwayMode.Checked = false;
        checkBoxProcessesAwayMode.Enabled = false;
      }
    }

    #endregion

    #region Shares tab

    private void checkBoxSharesEnabled_CheckedChanged(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
      if (checkBoxSharesEnabled.Checked)
      {
        checkBoxSharesAwayMode.Enabled = true;
        labelShares.ForeColor = SystemColors.ControlText;
        dataGridShares.Enabled = true;
        dataGridShares.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
        dataGridShares.RowHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
        dataGridShares.DefaultCellStyle.ForeColor = SystemColors.WindowText;
        buttonSelectShare.Enabled = true;
      }
      else
      {
        checkBoxSharesAwayMode.Checked = false;
        checkBoxSharesAwayMode.Enabled = false;
        labelShares.ForeColor = SystemColors.GrayText;
        dataGridShares.Enabled = false;
        dataGridShares.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.GrayText;
        dataGridShares.RowHeadersDefaultCellStyle.ForeColor = SystemColors.GrayText;
        dataGridShares.DefaultCellStyle.ForeColor = SystemColors.GrayText;
        buttonSelectShare.Enabled = false;
      }
    }

    private void buttonSelectShare_Click(object sender, EventArgs e)
    {
      SelectShareForm ssf = new SelectShareForm();
      DialogResult dr = ssf.ShowDialog();
      if (DialogResult.OK == dr)
      {
        if (!ssf.SelectedShare.Equals(""))
        {
          string[] shareItem = ssf.SelectedShare.Split(',');
          dataGridShares.Rows.Add(shareItem);
        }
      }
    }

    #endregion

    #region Network tab

    private void checkBoxNetworkEnabled_CheckedChanged(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
      if (checkBoxNetworkEnabled.Checked)
      {
        labelNetwork.ForeColor = SystemColors.ControlText;
        numericUpDownNetworkIdleLimit.Enabled = true;
        checkBoxNetworkAwayMode.Enabled = true;
      }
      else
      {
        labelNetwork.ForeColor = SystemColors.GrayText;
        numericUpDownNetworkIdleLimit.Enabled = false;
        checkBoxNetworkAwayMode.Checked = false;
        checkBoxNetworkAwayMode.Enabled = false;
      }
    }

    #endregion

    #region Advanced tab

    private void buttonStandbyWakeupCommand_Click(object sender, EventArgs e)
    {
      DialogResult r = openFileDialog.ShowDialog();
      if (r == DialogResult.OK)
      {
        textBoxCommand.Text = openFileDialog.FileName;
      }
    }

    private void checkBoxAutoPowerSettings_CheckedChanged(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
      if (checkBoxAutoPowerSettings.Checked)
      {
        buttonPowerSettings.Enabled = false;
        numericUpDownIdleTimeout.Enabled = true;
      }
      else
      {
        buttonPowerSettings.Enabled = true;
        numericUpDownIdleTimeout.Enabled = checkBoxShutdownEnabled.Checked;
      }
    }

    private void buttonPowerSettings_Click(object sender, EventArgs e)
    {
      PowerSettingsForm psf = new PowerSettingsForm(_recommendedSettingsAC, _recommendedSettingsDC);
      DialogResult result = psf.ShowDialog();
      if (result == DialogResult.OK)
        numericUpDownIdleTimeout.Value = (int)PowerManager.GetActivePowerSetting(PowerManager.SystemPowerSettingType.STANDBYIDLE) / 60;
    }

    private void checkBoxShutdownEnabled_CheckedChanged(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
      if (checkBoxShutdownEnabled.Checked)
      {
        if (numericUpDownIdleTimeout.Value < 2)
          numericUpDownIdleTimeout.Value = 2;
        numericUpDownIdleTimeout.Minimum = 2;
        toolTip.SetToolTip(numericUpDownIdleTimeout,
          "Adjust the time after which the system goes to standby when idle.");

        labelShutdownMode.ForeColor = SystemColors.ControlText;
        comboBoxShutdownMode.Enabled = true;
        numericUpDownIdleTimeout.Enabled = true;
      }
      else
      {
        numericUpDownIdleTimeout.Minimum = 0;
        toolTip.SetToolTip(numericUpDownIdleTimeout,
          "Adjust the time after which the system goes to standby when idle" + Environment.NewLine +
          "(\"0\" means \"never\").");

        labelShutdownMode.ForeColor = SystemColors.GrayText;
        comboBoxShutdownMode.Enabled = false;
        numericUpDownIdleTimeout.Enabled = checkBoxAutoPowerSettings.Checked;
      }
    }

    private void comboBoxShutdownMode_SelectedIndexChanged(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
      if (!PowerManager.CanHibernate && comboBoxShutdownMode.SelectedIndex == 1)
        comboBoxShutdownMode.SelectedIndex = 0;
    }

    #endregion

    # region Ping Monitor tab

    private void checkBoxPingMonitorEnable_CheckedChanged(object sender, EventArgs e)
    {
      buttonApply.Enabled = true;
      if (checkBoxPingMonitorEnable.Checked)
      {
        checkBoxPingMonitorAwayMode.Enabled = true;
        buttonAdd.Enabled = true;
        buttonDelete.Enabled = true;
      }
      else
      {
        checkBoxPingMonitorAwayMode.Enabled = false;
        buttonAdd.Enabled = false;
        buttonDelete.Enabled = false;
      }
    }
    
    private void buttonAdd_Click(object sender, EventArgs e)
    {
      if (textBoxEditHost.Text == "")
      {
        MessageBox.Show("No Hostname entered");
      }
      else
      {
        for (int i = 0; i < listBoxHosts.Items.Count; i++)
        {
          if (listBoxHosts.Items[i].ToString().ToLower() == textBoxEditHost.Text.ToLower())
          {
            MessageBox.Show("Host already in List");
            return;
          }
        }
        listBoxHosts.Items.Add(textBoxEditHost.Text);
        textBoxEditHost.Text = "";
        buttonApply.Enabled = true;
      }
    }

    private void buttonDelete_Click(object sender, EventArgs e)
    {
      listBoxHosts.Items.Remove(listBoxHosts.SelectedItem);
      buttonApply.Enabled = true;
    }

    private void checkBoxPingMonitorAwayMode_CheckedChanged(object sender, EventArgs e)
    {

    }
    #endregion

    #endregion
  }
}