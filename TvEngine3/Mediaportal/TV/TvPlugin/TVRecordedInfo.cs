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
using System.Globalization;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using MediaPortal.Util;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVService.ServiceAgents;
using Mediaportal.TV.TvPlugin.Helper;
using Action = MediaPortal.GUI.Library.Action;

namespace Mediaportal.TV.TvPlugin
{
  /// <summary>
  /// Summary description for TvRecordedInfo.
  /// </summary>
  public class TvRecordedInfo : GUIInternalWindow
  {
    [SkinControl(17)] protected GUILabelControl lblProgramGenre = null;
    [SkinControl(15)] protected GUITextScrollUpControl lblProgramDescription = null;
    [SkinControl(14)] protected GUILabelControl lblProgramTime = null;
    [SkinControl(13)] protected GUIFadeLabel lblProgramTitle = null;
    [SkinControl(2)] protected GUIButtonControl btnKeep = null;

    private static Recording currentProgram = null;

    public TvRecordedInfo()
    {
      GetID = (int)Window.WINDOW_TV_RECORDED_INFO; //759
    }

    public override bool IsTv
    {
      get { return true; }
    }

    public override bool Init()
    {
      bool bResult = Load(GUIGraphicsContext.Skin + @"\mytvRecordedInfo.xml");
      return bResult;
    }

    protected override void OnPageLoad()
    {
      base.OnPageLoad();
      Update();
    }

    public static Recording CurrentProgram
    {
      get { return currentProgram; }
      set { currentProgram = value; }
    }

    private void Update()
    {
      if (currentProgram == null)
      {
        return;
      }

      string strTime = String.Format("{0} {1} - {2}",
                                     Utils.GetShortDayString(currentProgram.startTime),
                                     currentProgram.startTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                     currentProgram.endTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));      
      lblProgramGenre.Label = TVUtil.GetCategory(currentProgram.ProgramCategory);
      lblProgramTime.Label = strTime;
      lblProgramDescription.Label = currentProgram.description;
      lblProgramTitle.Label = currentProgram.title;
    }

    protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
    {
      if (control == btnKeep)
      {
        OnKeep();
      }
      base.OnClicked(controlId, control, actionType);
    }

    private void OnKeep()
    {
      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
      if (dlg == null)
      {
        return;
      }
      dlg.Reset();
      dlg.SetHeading(1042);
      dlg.AddLocalizedString(1043); //Until watched
      dlg.AddLocalizedString(1044); //Until space needed
      dlg.AddLocalizedString(1045); //Until date
      dlg.AddLocalizedString(1046); //Always
      switch ((KeepMethodType)currentProgram.keepUntil)
      {
        case KeepMethodType.UntilWatched:
          dlg.SelectedLabel = 0;
          break;
        case KeepMethodType.UntilSpaceNeeded:
          dlg.SelectedLabel = 1;
          break;
        case KeepMethodType.TillDate:
          dlg.SelectedLabel = 2;
          break;
        case KeepMethodType.Always:
          dlg.SelectedLabel = 3;
          break;
      }
      dlg.DoModal(GetID);
      if (dlg.SelectedLabel == -1)
      {
        return;
      }
      switch (dlg.SelectedId)
      {
        case 1043:
          currentProgram.keepUntil = (int)KeepMethodType.UntilWatched;
          break;
        case 1044:
          currentProgram.keepUntil = (int)KeepMethodType.UntilSpaceNeeded;

          break;
        case 1045:
          currentProgram.keepUntil = (int)KeepMethodType.TillDate;
          dlg.Reset();
          dlg.ShowQuickNumbers = false;
          dlg.SetHeading(1045);
          for (int iDay = 1; iDay <= 100; iDay++)
          {
            DateTime dt = currentProgram.startTime.AddDays(iDay);
            if (currentProgram.startTime < DateTime.Now)
            {
              dt = DateTime.Now.AddDays(iDay);
            }

            dlg.Add(dt.ToLongDateString());
          }
          TimeSpan ts = (currentProgram.keepUntilDate.GetValueOrDefault(DateTime.MinValue) - currentProgram.startTime);
          if (currentProgram.startTime < DateTime.Now)
          {
            ts = (currentProgram.keepUntilDate.GetValueOrDefault(DateTime.MinValue) - DateTime.Now);
          }
          int days = (int)ts.TotalDays;
          if (days >= 100)
          {
            days = 30;
          }
          dlg.SelectedLabel = days - 1;
          dlg.DoModal(GetID);
          if (dlg.SelectedLabel < 0)
          {
            return;
          }
          if (currentProgram.startTime < DateTime.Now)
          {
            currentProgram.keepUntilDate = DateTime.Now.AddDays(dlg.SelectedLabel + 1);
          }
          else
          {
            currentProgram.keepUntilDate = currentProgram.startTime.AddDays(dlg.SelectedLabel + 1);
          }
          break;
        case 1046:
          currentProgram.keepUntil = (int)KeepMethodType.Always;
          break;
      }      
      ServiceAgents.Instance.RecordingServiceAgent.SaveRecording(currentProgram);
    }

    public override void Process()
    {
      TVHome.UpdateProgressPercentageBar();
    }
  }
}