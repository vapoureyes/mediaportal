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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Mediaportal.TV.Server.Common.Types.Enum;
using Mediaportal.TV.Server.TVControl.ServiceAgents;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVDatabase.Entities.Factories;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer.Entities;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.TvPlugin.Helper;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using MediaPortal.Profile;
using MediaPortal.Util;
using Action = MediaPortal.GUI.Library.Action;

namespace Mediaportal.TV.TvPlugin.Search
{
  /// <summary>
  /// </summary>
  public abstract class SearchBase : GUIInternalWindow
  {
    [SkinControl(2)] protected GUISortButtonControl btnSortBy = null;
    [SkinControl(4)] protected GUICheckButton btnSearchByGenre = null;
    [SkinControl(5)] protected GUICheckButton btnSearchByTitle = null;
    [SkinControl(6)] protected GUICheckButton btnSearchByDescription = null;
    [SkinControl(7)] protected GUISelectButtonControl btnLetter = null;
    [SkinControl(19)] protected GUIButtonControl btnSMSInput = null;
    [SkinControl(8)] protected GUISelectButtonControl btnShow = null;
    [SkinControl(9)] protected GUISelectButtonControl btnEpisode = null;
    [SkinControl(10)] protected GUIListControl listView = null;
    [SkinControl(11)] protected GUIListControl titleView = null;
    [SkinControl(12)] protected GUILabelControl lblNumberOfItems = null;
    [SkinControl(13)] protected GUIFadeLabel lblProgramTitle = null;
    [SkinControl(14)] protected GUILabelControl lblProgramTime = null;
    [SkinControl(15)] protected GUITextScrollUpControl lblProgramDescription = null;
    [SkinControl(16)] protected GUILabelControl lblChannel = null;
    [SkinControl(17)] protected GUILabelControl lblProgramGenre = null;
    [SkinControl(18)] protected GUIImage imgChannelLogo = null;
    [SkinControl(20)] protected GUIButtonControl btnViewBy = null; // is replacing btnSearchByTitle, btnSearchByGenre
    [SkinControl(21)] protected GUIButtonControl btnSearchDescription = null; // is replacing btnSearchByDescription 

    private DirectoryHistory history = new DirectoryHistory();

    private enum SearchMode
    {
      Genre,
      Title,
      Description
    }

    private enum SortMethod
    {
      Auto,
      Name,
      Channel,
      Date
    }

    private SortMethod currentSortMethod = SortMethod.Name;
    private SortMethod chosenSortMethod = SortMethod.Auto;
    private bool sortAscending = true;
    private IList<ScheduleBLL> listRecordings;

    private SearchMode currentSearchMode = SearchMode.Title;
    private int currentLevel = 0;
    private string currentGenre = string.Empty;
    private string filterLetter = "A";
    private string filterShow = string.Empty;
    private string filterEpisode = string.Empty;


    private SearchMode prevcurrentSearchMode = SearchMode.Title;
    private int prevcurrentLevel = 0;
    private string prevcurrentGenre = string.Empty;
    private string prevfilterLetter = "a";
    private string prevfilterShow = string.Empty;
    private string prevfilterEpisode = string.Empty;

    #region Serialisation

    private void LoadSettings()
    {
      using (Settings xmlreader = new MPSettings())
      {
        currentSortMethod = (SortMethod)Enum.Parse(typeof(SortMethod), xmlreader.GetValueAsString(SettingsSection, "cursortmethod", "Name"), true);
        chosenSortMethod = (SortMethod)Enum.Parse(typeof(SortMethod), xmlreader.GetValueAsString(SettingsSection, "chosortmethod", "Auto"), true);
        currentSearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), xmlreader.GetValueAsString(SettingsSection, "searchmode", "Title"), true);
      }
    }

    private void SaveSettings()
    {
      using (Settings xmlwriter = new MPSettings())
      {
        xmlwriter.SetValue(SettingsSection, "cursortmethod", currentSortMethod.ToString());
        xmlwriter.SetValue(SettingsSection, "chosortmethod", chosenSortMethod.ToString());
        xmlwriter.SetValue(SettingsSection, "searchmode", currentSearchMode.ToString());
      }
    }

    #endregion

    #region abstract properties

    protected abstract MediaType MediaType
    {
      get;
    }

    protected abstract string ThumbsType
    {
      get;
    }

    protected abstract string DefaultLogo
    {
      get;
    }

    protected abstract string SettingsSection
    {
      get;
    }

    protected abstract string SkinFileName
    {
      get;
    }

    protected abstract string SkinPropertyPrefix
    {
      get;
    }

    #endregion

    public override bool Init()
    {
      bool bResult = Load(GUIGraphicsContext.GetThemedSkinFile(SkinFileName));
      LoadSettings();
      return bResult;
    }
    public override void DeInit()
    {
      SaveSettings();
    }
 
    public override void OnAction(Action action)
    {
      if (action.wID == Action.ActionType.ACTION_PREVIOUS_MENU)
      {
        if (listView.Focus || titleView.Focus)
        {
          GUIListItem item = GetItem(0);
          if (item != null)
          {
            if (item.IsFolder && item.Label == "..")
            {
              OnClick(0);
              return;
            }
          }
        }
      }

      base.OnAction(action);
    }

    protected override void OnPageDestroy(int newWindowId)
    {
      base.OnPageDestroy(newWindowId);
      if (TVHome.Connected)
      {
        listRecordings.Clear();
        listRecordings = null;
      }
    }

    protected override void OnPageLoad()
    {
      TVHome.ShowTvEngineSettingsUIIfConnectionDown();

      base.OnPageLoad();
      LoadSchedules();

      if (btnShow != null)
      {
        btnShow.RestoreSelection = false;
        btnShow.Clear();
      }
      if (btnEpisode != null)
      {
        btnEpisode.RestoreSelection = false;
        btnEpisode.Clear();
      }
      if (btnLetter != null)
      {
        btnLetter.RestoreSelection = false;
        for (char k = 'A'; k <= 'Z'; k++)
        {
          btnLetter.AddSubItem(k.ToString());
        }
        btnLetter.AddSubItem("#");
      }
      Update();

      btnSortBy.SortChanged += new SortEventHandler(SortChanged);
      if (btnSearchByDescription != null)
      {
        btnSearchByDescription.Disabled = true;
      }
    }

    private void LoadSchedules()
    {
      listRecordings = new List<ScheduleBLL>();
      foreach (var schedule in ServiceAgents.Instance.ScheduleServiceAgent.ListAllSchedules())
      {
        listRecordings.Add(new ScheduleBLL(schedule));
      }
    }

    protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
    {
      base.OnClicked(controlId, control, actionType);
      if ((control == btnSearchByGenre) || ((control == btnViewBy) && (currentSearchMode != SearchMode.Genre)))
      {
        if (btnShow != null)
        {
          btnShow.Clear();
        }
        if (btnEpisode != null)
        {
          btnEpisode.Clear();
        }
        currentSearchMode = SearchMode.Genre;
        currentLevel = 0;
        filterEpisode = string.Empty;
        filterLetter = string.Empty;
        filterShow = string.Empty;
        Update();
      }
      else if ((control == btnSearchByTitle) || ((control == btnViewBy) && (currentSearchMode == SearchMode.Genre)))
      {
        if (btnShow != null)
        {
          btnShow.Clear();
        }
        if (btnEpisode != null)
        {
          btnEpisode.Clear();
        }
        filterEpisode = string.Empty;
        filterShow = string.Empty;
        currentSearchMode = SearchMode.Title;
        currentLevel = 0;
        filterLetter = "A";
        Update();
      }
      else if (control == btnSearchDescription)
      {
        if (btnShow != null)
        {
          btnShow.Clear();
        }
        if (btnEpisode != null)
        {
          btnEpisode.Clear();
        }
        VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow((int)Window.WINDOW_VIRTUAL_KEYBOARD);
        if (null == keyboard)
        {
          return;
        }
        String searchterm = string.Empty;
        keyboard.Reset();

        String tmpFilterLetter = filterLetter;
        if (tmpFilterLetter.StartsWith("%"))
        {
          tmpFilterLetter = tmpFilterLetter.Substring(1); // cut of leading % for display in dialog
        }
        keyboard.Text = tmpFilterLetter;
        keyboard.DoModal(GetID); // show it...

        if (keyboard.IsConfirmed)
        {
          if (keyboard.Text.Length > 0)
          {
            currentSearchMode = SearchMode.Description;
            filterLetter = "%" + keyboard.Text; // re-add % to perform fulltext search
            currentLevel = 0; // only search on root level
            filterEpisode = string.Empty;
            filterShow = string.Empty;
            Update();
          }
        }
      }
      else if (control == btnSMSInput)
      {
        VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow((int)Window.WINDOW_VIRTUAL_KEYBOARD);
        if (null == keyboard)
        {
          return;
        }
        String searchterm = string.Empty;
        keyboard.Reset();

        String tmpFilterLetter = filterLetter;
        if (tmpFilterLetter.StartsWith("%"))
        {
          tmpFilterLetter = tmpFilterLetter.Substring(1); // cut of leading % for display in dialog
        }
        keyboard.Text = tmpFilterLetter;
        keyboard.DoModal(GetID); // show it...

        if (keyboard.IsConfirmed)
        {
          currentSearchMode = SearchMode.Title;
          currentLevel = 0; // only search on root level
          filterShow = string.Empty;
          filterEpisode = string.Empty;
          if (keyboard.Text.Length > 0)
          {
            filterLetter = "%" + keyboard.Text; // re-add % to perform fulltext search
            Update();
          }
          else
          {
            filterLetter = "A"; // do a [Starts with] search
            Update();
          }
        }
      }
      else if (control == btnSortBy)
      {
        GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
        if (dlg == null)
        {
          return;
        }
        dlg.Reset();
        dlg.SetHeading(495); //Sort Options
        dlg.AddLocalizedString(1202); //auto
        dlg.AddLocalizedString(622); //name
        dlg.AddLocalizedString(620); //channel
        dlg.AddLocalizedString(621); //date
        

        // set the focus to currently used sort method
        dlg.SelectedLabel = (int)chosenSortMethod;

        // show dialog and wait for result
        dlg.DoModal(GetID);
        if (dlg.SelectedId == -1)
        {
          return;
        }

        chosenSortMethod = (SortMethod)dlg.SelectedLabel;
        Update();
      }
      else if (control == listView || control == titleView)
      {
        GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_ITEM_SELECTED, GetID, 0, control.GetID, 0, 0,
                                        null);
        OnMessage(msg);
        int iItem = (int)msg.Param1;
        if (actionType == Action.ActionType.ACTION_SELECT_ITEM)
        {
          OnClick(iItem);
        }
      }
      else if (control == btnLetter)
      {
        currentSearchMode = SearchMode.Title;
        GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_ITEM_SELECTED, GetID, 0, controlId, 0, 0, null);
        OnMessage(msg);
        filterLetter = msg.Label;
        filterShow = string.Empty;
        filterEpisode = string.Empty;
        Update();
      }
      else if (control == btnShow)
      {
        GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_ITEM_SELECTED, GetID, 0, controlId, 0, 0, null);
        OnMessage(msg);
        filterShow = msg.Label;
        filterEpisode = string.Empty;
        Update();
      }
      else if (control == btnEpisode)
      {
        GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_ITEM_SELECTED, GetID, 0, controlId, 0, 0, null);
        OnMessage(msg);
        filterEpisode = msg.Label;
        Update();
      }
      else if (control == btnSearchDescription)
      {
        VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow((int)Window.WINDOW_VIRTUAL_KEYBOARD);
        if (null == keyboard)
        {
          return;
        }
        String searchterm = string.Empty;
        keyboard.Reset();

        String tmpFilterLetter = filterLetter;
        if (tmpFilterLetter.StartsWith("%"))
        {
          tmpFilterLetter = tmpFilterLetter.Substring(1); // cut of leading % for display in dialog
        }
        keyboard.Text = tmpFilterLetter;
        keyboard.DoModal(GetID); // show it...

        if (keyboard.IsConfirmed)
        {
          filterLetter = "%" + keyboard.Text; // re-add % to perform fulltext search
          Update();
        }
      }
      GUIControl.FocusControl(GetID, control.GetID);
    }

    public override bool OnMessage(GUIMessage message)
    {
      switch (message.Message)
      {
        case GUIMessage.MessageType.GUI_MSG_ITEM_FOCUS_CHANGED:
          UpdateDescription();
          break;
      }
      return base.OnMessage(message);
    }

    private void Update()
    {
      SetHistory();
      int currentItemId = 0;
      listView.Clear();
      titleView.Clear();
      Dictionary<int, Channel> channelMap = GetChannelMap();
      if (chosenSortMethod == SortMethod.Auto)
      {
        if (filterShow == string.Empty)
        {
          currentSortMethod = SortMethod.Name;
        }
        else
        {
          currentSortMethod = SortMethod.Date;
        }
      }
      else
      {
        currentSortMethod = chosenSortMethod;
      }

      if (currentLevel == 0 && currentSearchMode == SearchMode.Genre)
      {
        listView.IsVisible = true;
        titleView.IsVisible = false;
        GUIControl.FocusControl(GetID, listView.GetID);
        if (btnEpisode != null)
        {
          btnEpisode.Disabled = true;
        }
        if (btnLetter != null)
        {
          btnLetter.Disabled = true;
        }
        if (btnSMSInput != null)
        {
          btnSMSInput.Disabled = true;
        }
        if (btnShow != null)
        {
          btnShow.Disabled = true;
        }
        if (btnSearchDescription != null)
        {
          btnSearchDescription.Disabled = true;
        }

        listView.Height = lblProgramDescription.YPosition - listView.YPosition;
        lblNumberOfItems.YPosition = listView.SpinY;
      }
      else
      {
        listView.IsVisible = false;
        titleView.IsVisible = true;
        GUIControl.FocusControl(GetID, titleView.GetID);

        if (filterShow == String.Empty)
        {
          if (imgChannelLogo != null)
          {
            imgChannelLogo.IsVisible = false;
          }
          if (titleView.SubItemCount == 2)
          {
            string subItem = (string)titleView.GetSubItem(1);
            int h = Int32.Parse(subItem.Substring(1));
            GUIGraphicsContext.ScaleVertical(ref h);
            titleView.Height = h;
            h = Int32.Parse(subItem.Substring(1));
            h -= 55;
            GUIGraphicsContext.ScaleVertical(ref h);
            titleView.SpinY = titleView.YPosition + h;
            titleView.Dispose();
            titleView.AllocResources();
          }
        }
        else
        {
          if (imgChannelLogo != null)
          {
            imgChannelLogo.IsVisible = true;
          }
          if (titleView.SubItemCount == 2)
          {
            string subItem = (string)titleView.GetSubItem(0);
            int h = Int32.Parse(subItem.Substring(1));
            GUIGraphicsContext.ScaleVertical(ref h);
            titleView.Height = h;

            h = Int32.Parse(subItem.Substring(1));
            h -= 50;
            GUIGraphicsContext.ScaleVertical(ref h);
            titleView.SpinY = titleView.YPosition + h;

            titleView.Dispose();
            titleView.AllocResources();
          }
          lblNumberOfItems.YPosition = titleView.SpinY;
        }

        if (currentSearchMode != SearchMode.Genre)
        {
          if (btnEpisode != null)
          {
            btnEpisode.Disabled = false;
          }
          if (btnLetter != null)
          {
            btnLetter.Disabled = false;
          }
          if (btnSMSInput != null)
          {
            btnSMSInput.Disabled = false;
          }
          if (btnShow != null)
          {
            btnShow.Disabled = false;
          }
          if (btnSearchDescription != null)
          {
            btnSearchDescription.Disabled = false;
          }
        }
        lblNumberOfItems.YPosition = listView.SpinY;
      }

      List<Program> programs = new List<Program>();
      List<Program> episodes = new List<Program>();
      int itemCount = 0;
      switch (currentSearchMode)
      {
        case SearchMode.Genre:
          if (currentLevel == 0)
          {
            IEnumerable<ProgramCategory> genres = ServiceAgents.Instance.ProgramCategoryServiceAgent.ListAllProgramCategories();
            foreach (ProgramCategory genre in genres)
            {
              GUIListItem item = new GUIListItem
                                   {
                                     IsFolder = true,
                                     Label = genre.Category,
                                     Path = genre.Category,
                                     ItemId = currentItemId
                                   };
              currentItemId++;
              Utils.SetDefaultIcons(item);
              listView.Add(item);
              itemCount++;
            }
          }
          else
          {
            listView.Clear();
            titleView.Clear();
            GUIListItem item = new GUIListItem();
            item.IsFolder = true;
            item.Label = "..";
            item.Label2 = string.Empty;
            item.Path = string.Empty;
            Utils.SetDefaultIcons(item);
            listView.Add(item);
            titleView.Add(item);
            
            StringComparisonEnum stringComparisonCategory = StringComparisonEnum.StartsWith;
            stringComparisonCategory |= StringComparisonEnum.EndsWith;
            IEnumerable<Program> titles = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByTitleAndCategoryAndMediaType(currentGenre,
                                                                                                             filterShow,
                                                                                                             MediaType, stringComparisonCategory, StringComparisonEnum.StartsWith);            
            //titles = new List<Program>();
            foreach (Program program in titles)
            {
              //dont show programs which have ended
              if (program.EndTime < DateTime.Now)
              {
                continue;
              }
              bool add = true;
              foreach (Program prog in programs)
              {
                if (prog.Title == program.Title)
                {
                  add = false;
                }
              }
              if (!add && filterShow == string.Empty)
              {
                continue;
              }
              if (add)
              {
                programs.Add(program);
              }

              if (filterShow != string.Empty)
              {
                if (program.Title == filterShow)
                {
                  episodes.Add(program);
                }
              }

              if (filterShow != string.Empty && program.Title != filterShow)
              {
                continue;
              }

              string strTime = string.Format("{0} {1}",
                                             Utils.GetShortDayString(program.StartTime),
                                             program.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));
              if (filterEpisode != string.Empty && strTime != filterEpisode)
              {
                continue;
              }

              strTime = string.Format("{0} {1} - {2}",
                                      Utils.GetShortDayString(program.StartTime),
                                      program.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                      program.EndTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));

              item = new GUIListItem();
              
              //check if we are filtering for specific show or just letter
              if (filterShow == string.Empty)
              {
                //not searching for episode data so show just title
                item.Label = program.Title;
                item.Label2 = string.Empty;
                item.IsFolder = true;
              }
              else
              {
                //searching for specific show so add episode data to display
                item.Label = TVUtil.GetDisplayTitle(program);
                item.Label2 = strTime;
                item.IsFolder = false;
              }
              item.Path = program.Title;
              item.TVTag = program;
              item.ItemId = currentItemId;
              currentItemId++;
              bool isSerie;
              if (IsRecording(program, out isSerie))
              {
                if (isSerie)
                {
                  item.PinImage = Thumbs.TvRecordingSeriesIcon;
                }
                else
                {
                  item.PinImage = Thumbs.TvRecordingIcon;
                }
              }
              Utils.SetDefaultIcons(item);
              SetChannelLogo(program, ref item, channelMap);
              listView.Add(item);
              titleView.Add(item);
              itemCount++;
            }
          }
          break;
        case SearchMode.Title:
          {
            if (filterShow != string.Empty)
            {
              GUIListItem item = new GUIListItem();
              item.IsFolder = true;
              item.Label = "..";
              item.Label2 = string.Empty;
              item.Path = string.Empty;
              Utils.SetDefaultIcons(item);
              //item.IconImage = "defaultFolderBig.png";
              //item.IconImageBig = "defaultFolderBig.png";
              listView.Add(item);
              titleView.Add(item);
            }
            IEnumerable<Program> titles = new List<Program>();            
            StringComparisonEnum stringComparison = StringComparisonEnum.StartsWith;
            stringComparison |= StringComparisonEnum.EndsWith;
            if (filterLetter == "#")
            {              
              if (filterShow == string.Empty)
              {                       
                titles = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByTitleAndMediaType("[0-9]", MediaType, stringComparison);
              }
              else
              {
                titles = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByTitleAndMediaType("%" + filterShow, MediaType, stringComparison);
              }
            }
            else
            {
              if (filterShow == string.Empty)
              {
                titles = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByTitleAndMediaType(filterLetter, MediaType, stringComparison);
              }
              else
              {
                titles = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByTitleAndMediaType("%" + filterShow, MediaType, stringComparison);
              }
            }
            foreach (Program program in titles)
            {
              bool add = true;
              foreach (Program prog in programs)
              {
                if (prog.Title == program.Title)
                {
                  add = false;
                }
              }
              if (!add && filterShow == string.Empty)
              {
                continue;
              }
              if (add)
              {
                programs.Add(program);
              }

              if (filterShow != string.Empty)
              {
                if (program.Title == filterShow)
                {
                  episodes.Add(program);
                }
              }

              if (filterShow != string.Empty && program.Title != filterShow)
              {
                continue;
              }

              string strTime = string.Format("{0} {1} - {2}",
                                             Utils.GetShortDayString(program.StartTime),
                                             program.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                             program.EndTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));

              GUIListItem item = new GUIListItem();
              

              //check if we are filtering for specific show or just letter
              if (filterShow == string.Empty)
              {
                //not searching for episode data so show just title
                item.Label = program.Title;
                item.Label2 = string.Empty;
                item.IsFolder = true;
              }
              else
              {
                //searching for specific show so add episode data to display
                item.Label = TVUtil.GetDisplayTitle(program);
                item.IsFolder = false;
                //moved this if statement but can not see it is doing anything?
                //if (program.startTime > DateTime.MinValue)
                //{
                    item.Label2 = strTime;
                //}
              }

              item.Path = program.Title;
              item.TVTag = program;
              item.ItemId = currentItemId;
              currentItemId++;
              bool isSerie;
              if (IsRecording(program, out isSerie))
              {
                if (isSerie)
                {
                  item.PinImage = Thumbs.TvRecordingSeriesIcon;
                }
                else
                {
                  item.PinImage = Thumbs.TvRecordingIcon;
                }
              }
              Utils.SetDefaultIcons(item);
              SetChannelLogo(program, ref item, channelMap);
              listView.Add(item);
              titleView.Add(item);
              itemCount++;
            }
          }
          break;
        case SearchMode.Description:
          {
            IEnumerable<Program> titles = new List<Program>();
            if (filterLetter == "0")
            {
              if (filterShow == string.Empty)
              {
                titles = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByDescriptionAndMediaType(string.Empty, MediaType, StringComparisonEnum.StartsWith);
              }
              else
              {
                titles = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByDescriptionAndMediaType(filterShow, MediaType, StringComparisonEnum.StartsWith);
              }
            }
            else
            {
              if (filterShow == string.Empty)
              {
                titles = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByDescriptionAndMediaType(filterLetter, MediaType, StringComparisonEnum.StartsWith);
              }
              else
              {
                titles = ServiceAgents.Instance.ProgramServiceAgent.GetProgramsByDescriptionAndMediaType(filterShow, MediaType, StringComparisonEnum.StartsWith);
              }
            }
            foreach (Program program in titles)
            {
              if (program.Description.Length == 0)
              {
                continue;
              }

              programs.Add(program);

              if (filterShow != string.Empty)
              {
                if (program.Title == filterShow)
                {
                  episodes.Add(program);
                }
              }

              if (filterShow != string.Empty && program.Title != filterShow)
              {
                continue;
              }

              string strTime = string.Format("{0} {1}",
                                             Utils.GetShortDayString(program.StartTime),
                                             program.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));
              if (filterEpisode != string.Empty && strTime != filterEpisode)
              {
                continue;
              }

              strTime = string.Format("{0} {1} - {2}",
                                      Utils.GetShortDayString(program.StartTime),
                                      program.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                      program.EndTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));

              GUIListItem item = new GUIListItem();
              item.IsFolder = false;
              item.Label = TVUtil.GetDisplayTitle(program);
              item.Label2 = strTime;
              

              item.Path = program.Title;
              item.TVTag = program;
              item.ItemId = currentItemId;
              currentItemId++;
              bool isSerie;
              if (IsRecording(program, out isSerie))
              {
                if (isSerie)
                {
                  item.PinImage = Thumbs.TvRecordingSeriesIcon;
                }
                else
                {
                  item.PinImage = Thumbs.TvRecordingIcon;
                }
              }
              Utils.SetDefaultIcons(item);
              SetChannelLogo(program, ref item, channelMap);
              listView.Add(item);
              titleView.Add(item);
              itemCount++;
            }
          }
          break;
      }

      //set object count label
      GUIPropertyManager.SetProperty("#itemcount", Utils.GetObjectCountLabel(itemCount));

      if (btnShow != null) btnShow.Clear();
      try
      {
        programs.Sort();
      }
      catch (Exception)
      {
      }
      int selItem = 0;
      int count = 0;
      if (btnShow != null)
      {
        foreach (Program prog in programs)
        {
          btnShow.Add(prog.Title.ToString());
          if (filterShow == prog.Title)
          {
            selItem = count;
          }
          count++;
        }
        GUIControl.SelectItemControl(GetID, btnShow.GetID, selItem);
      }

      selItem = 0;
      count = 0;
      if (btnEpisode != null)
      {
        btnEpisode.Clear();

        foreach (Program prog in episodes)
        {
          string strTime = string.Format("{0} {1}",
                                         Utils.GetShortDayString(prog.StartTime),
                                         prog.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));
          btnEpisode.Add(strTime.ToString());
          if (filterEpisode == strTime)
          {
            selItem = count;
          }
          count++;
        }
        GUIControl.SelectItemControl(GetID, btnEpisode.GetID, selItem);
      }
      OnSort();

      string strLine = string.Empty;
      switch (chosenSortMethod)
      {
        case SortMethod.Auto:
          strLine = GUILocalizeStrings.Get(1202);
          break;
        case SortMethod.Name:
          strLine = GUILocalizeStrings.Get(622);
          break;
        case SortMethod.Channel:
          strLine = GUILocalizeStrings.Get(620);
          break;
        case SortMethod.Date:
          strLine = GUILocalizeStrings.Get(621);
          break;
      }
      btnSortBy.Label = strLine;
      btnSortBy.IsAscending = sortAscending;

      UpdateButtonStates();
      RestoreHistory();
    }


    private void SetHistory()
    {
      GUIListItem item = GetSelectedItem();
      if (item == null)
      {
        return;
      }
      string currentFolder = string.Format("{0}.{1}.{2}.{3}.{4}.{5}",
                                           (int)prevcurrentSearchMode, prevcurrentLevel, prevcurrentGenre,
                                           prevfilterLetter, prevfilterShow, prevfilterEpisode);
      prevcurrentSearchMode = currentSearchMode;
      prevcurrentLevel = currentLevel;
      prevcurrentGenre = currentGenre;
      prevfilterLetter = filterLetter;
      prevfilterShow = filterShow;
      prevfilterEpisode = filterEpisode;
      if (item.Label == "..")
      {
        return;
      }

      history.Set(item.ItemId.ToString(), currentFolder);
      //this.LogInfo("history.Set({0},{1}", item.ItemId.ToString(), currentFolder);
    }

    private void RestoreHistory()
    {
      string currentFolder = string.Format("{0}.{1}.{2}.{3}.{4}.{5}",
                                           (int)currentSearchMode, currentLevel, currentGenre,
                                           filterLetter, filterShow, filterEpisode);
      //this.LogInfo("history.Get({0})", currentFolder);
      string selectedItemLabel = history.Get(currentFolder);
      if (selectedItemLabel == null)
      {
        return;
      }
      if (selectedItemLabel.Length == 0)
      {
        return;
      }
      for (int i = 0; i < listView.Count; ++i)
      {
        GUIListItem item = listView[i];
        //if (item.Label == selectedItemLabel)
        this.LogInfo(item.ItemId.ToString() + "==" + selectedItemLabel);
        if (item.ItemId.ToString() == selectedItemLabel)
        {
          listView.SelectedListItemIndex = i;
          titleView.SelectedListItemIndex = i;
          break;
        }
      }
    }

    #region Sort Members

    private void OnSort()
    {
      Comparer c = new Comparer(currentSortMethod, sortAscending);
      listView.Sort(c);
      titleView.Sort(c);
    }

    #endregion

    private GUIListItem GetItem(int iItem)
    {
      if (currentLevel != 0)
      {
        if (iItem >= titleView.Count || iItem < 0)
        {
          return null;
        }
        return titleView[iItem];
      }
      if (iItem >= listView.Count || iItem < 0)
      {
        return null;
      }
      return listView[iItem];
    }

    private void OnClick(int iItem)
    {
      GUIListItem item = GetItem(iItem);
      if (item == null)
      {
        return;
      }
      switch (currentSearchMode)
      {
        case SearchMode.Genre:
          if (currentLevel == 0)
          {
            filterLetter = "0";
            filterShow = string.Empty;
            filterEpisode = string.Empty;
            currentGenre = item.Label;
            currentLevel++;
            Update();
          }
          else
          {
            Program program = item.TVTag as Program;
            if (filterShow == string.Empty)
            {
              if (item.Label == "..")
              {
                currentLevel = 0;
                currentGenre = string.Empty;
              }
              else
              {
                filterShow = program.Title;
              }
              Update();
            }
            else
            {
              if (item.Label == "..")
              {
                filterShow = string.Empty;
                Update();
              }
              else
              {
                OnRecord(program);
              }
            }
          }
          break;
        case SearchMode.Title:
          {
            if (item.Label == ".." && item.IsFolder)
            {
              filterShow = string.Empty;
              currentLevel = 0;
              Update();
              return;
            }
            Program program = item.TVTag as Program;
            if (filterShow == string.Empty)
            {
              filterShow = program.Title;
              Update();
              return;
            }
            OnRecord(program);
          }
          break;
        case SearchMode.Description:
          {
            Program program = item.TVTag as Program;
            /*if (filterShow == string.Empty)
            {
              filterShow = program.title;
              Update();
              return;
            }*/
            OnRecord(program);
          }
          break;
      }
    }

    private void SetChannelLogo(Program prog, ref GUIListItem item, Dictionary<int, Channel> channelMap)
    {
      string strLogo = string.Empty;

      if (filterShow == string.Empty)
      {
        strLogo = Utils.GetCoverArt(Thumbs.TVShows, prog.Title);
      }
      if (string.IsNullOrEmpty(strLogo) || !File.Exists(strLogo))
      {
        Channel channel = channelMap[prog.IdChannel];
        strLogo = Utils.GetCoverArt(Thumbs.Radio, channel.Name);
      }

      if (string.IsNullOrEmpty(strLogo) || !File.Exists(strLogo))
      {
        strLogo = DefaultLogo;
      }

      item.ThumbnailImage = strLogo;
      item.IconImageBig = strLogo;
      item.IconImage = strLogo;
    }

    private void OnRecord(Program program)
    {
      if (program == null)
      {
        return;
      }
      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
      if (dlg != null)
      {
        dlg.Reset();
        dlg.SetHeading(GUILocalizeStrings.Get(616)); //616=Select Recording type

        //610=None
        //611=Record once
        //612=Record everytime on this channel
        //613=Record everytime on every channel
        //614=Record every week at this time
        //615=Record every day at this time
        for (int i = 610; i <= 615; ++i)
        {
          dlg.Add(GUILocalizeStrings.Get(i));
        }
        dlg.Add(GUILocalizeStrings.Get(WeekEndTool.GetText(DayType.Record_WorkingDays)));
        dlg.Add(GUILocalizeStrings.Get(WeekEndTool.GetText(DayType.Record_WeekendDays)));
        dlg.Add(GUILocalizeStrings.Get(990000));  // 990000=Weekly everytime on this channel

        dlg.DoModal(GetID);
        if (dlg.SelectedLabel == -1)
        {
          return;
        }
        Schedule rec = ScheduleFactory.CreateSchedule(program.IdChannel, program.Title, program.StartTime, program.EndTime);

        switch (dlg.SelectedLabel)
        {
          case 0: //none
            foreach (ScheduleBLL rec1 in listRecordings)
            {
              if (rec1.IsRecordingProgram(program, true))
              {
                if (rec1.Entity.ScheduleType != (int)ScheduleRecordingType.Once)
                {
                  //delete specific series
                  Schedule sched = ServiceAgents.Instance.ScheduleServiceAgent.GetSchedule(rec1.Entity.IdSchedule);
                  TVUtil.DeleteRecAndSchedWithPrompt(sched);
                }
                else
                {
                  //cancel recording
                  ServiceAgents.Instance.ControllerServiceAgent.StopRecordingSchedule(rec1.Entity.IdSchedule);                  
                  ServiceAgents.Instance.ScheduleServiceAgent.DeleteSchedule(rec1.Entity.IdSchedule);
                  ServiceAgents.Instance.ControllerServiceAgent.OnNewSchedule();
                }
              }
            }
            LoadSchedules();
            Update();
            return;
          case 1: //once
            rec.ScheduleType = (int)ScheduleRecordingType.Once;
            break;
          case 2: //everytime, this channel
            rec.ScheduleType = (int)ScheduleRecordingType.EveryTimeOnThisChannel;
            break;
          case 3: //everytime, all channels
            rec.ScheduleType = (int)ScheduleRecordingType.EveryTimeOnEveryChannel;
            break;
          case 4: //weekly
            rec.ScheduleType = (int)ScheduleRecordingType.Weekly;
            break;
          case 5: //daily
            rec.ScheduleType = (int)ScheduleRecordingType.Daily;
            break;
          case 6: //WorkingDays
            rec.ScheduleType = (int)ScheduleRecordingType.WorkingDays;
            break;
          case 7: //Weekends
            rec.ScheduleType = (int)ScheduleRecordingType.Weekends;
            break;
          case 8://Weekly everytime, this channel
            rec.ScheduleType = (int)ScheduleRecordingType.WeeklyEveryTimeOnThisChannel;
            break;
        }        
        ServiceAgents.Instance.ScheduleServiceAgent.SaveSchedule(rec);
        ServiceAgents.Instance.ControllerServiceAgent.OnNewSchedule();
        LoadSchedules();
        Update();
      }
    }

    private bool IsRecording(Program program, out bool isSerie)
    {
      bool isRecording = false;
      isSerie = false;
      foreach (ScheduleBLL record in listRecordings)
      {
        if (record.IsRecordingProgram(program, true))
        {
          if (record.Entity.ScheduleType != (int)ScheduleRecordingType.Once)
          {
            isSerie = true;
          }
          isRecording = true;
          break;
        }
      }
      return isRecording;
    }


    private GUIListItem GetSelectedItem()
    {
      if (titleView.Focus)
      {
        return titleView.SelectedListItem;
      }
      if (listView.Focus)
      {
        return listView.SelectedListItem;
      }
      return null;
    }

    private void UpdateDescription()
    {
      // have commented out setting lblProgramTitle.Label
      // this is because this label was never actually set in 
      // previous versions of code (Skins are using #TV.Search.title)
      // also there is a bug with FadeLabels being set to string.Empty
      // which leads to this label not being updated when it should be
      GUIListItem item = GetSelectedItem();
      Program prog = null;
      if (item != null)
      {
        prog = item.TVTag as Program;
      }
      
      if (item == null || item.Label == ".." || item.IsFolder || prog == null)
      {
        lblProgramTime.Label = string.Empty;
        lblProgramDescription.Label = string.Empty;
        lblChannel.Label = string.Empty;
        GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Time", string.Empty);
        GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Description", string.Empty);
        GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.thumb", string.Empty);
        GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Channel", string.Empty);
        if (prog == null)
        {
          // see comment at top of method
          //lblProgramTitle.Label = string.Empty;
          lblProgramGenre.Label = string.Empty;
          GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Title", string.Empty);
          GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Genre", string.Empty);
        }
        else
        {
          // see comment at top of method
          //lblProgramTitle.Label = prog.title;
          lblProgramGenre.Label = TVUtil.GetCategory(prog.ProgramCategory);
          GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Title", prog.Title);
          GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Genre", TVUtil.GetCategory(prog.ProgramCategory));
        }
        return;
      }
      
      string strTime = string.Format("{0} {1} - {2}",
                                     Utils.GetShortDayString(prog.StartTime),
                                     prog.StartTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat),
                                     prog.EndTime.ToString("t", CultureInfo.CurrentCulture.DateTimeFormat));

      GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Title", TVUtil.GetDisplayTitle(prog));
      GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Time", strTime);
      GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Description", prog.Description);
      GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Genre", TVUtil.GetCategory(prog.ProgramCategory));
      GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.Channel", prog.Channel.Name);

      // see comment at top of method
      //lblProgramTitle.Label = TVUtil.GetDisplayTitle(prog);
      lblProgramTime.Label = strTime;
      lblProgramDescription.Label = prog.Description;
      lblProgramGenre.Label = TVUtil.GetCategory(prog.ProgramCategory);

      if (lblChannel != null)
      {
        lblChannel.Label = prog.Channel.Name;
      }

      string strLogo = null;
      strLogo = Utils.GetCoverArt(ThumbsType, prog.Channel.Name);
      if (string.IsNullOrEmpty(strLogo) || !File.Exists(strLogo))
      {
        strLogo = DefaultLogo;
      }
      GUIPropertyManager.SetProperty(SkinPropertyPrefix + ".Search.thumb", strLogo);
    }

    private void UpdateButtonStates()
    {
      if (btnSearchByDescription != null)
      {
        btnSearchByDescription.Selected = false;
      }
      if (btnSearchDescription != null)
      {
        btnSearchDescription.Selected = false;
      }
      if (btnSearchByTitle != null)
      {
        btnSearchByTitle.Selected = false;
      }
      if (btnSearchByGenre != null)
      {
        btnSearchByGenre.Selected = false;
      }

      if (currentSearchMode == SearchMode.Title)
      {
        if (btnSearchByTitle != null)
        {
          btnSearchByTitle.Selected = true;
        }
        if (btnViewBy != null)
        {
          btnViewBy.Label = GUILocalizeStrings.Get(1521);
        }
      }
      if (currentSearchMode == SearchMode.Genre)
      {
        if (btnSearchByGenre != null)
        {
          btnSearchByGenre.Selected = true;
        }
        if (btnViewBy != null)
        {
          btnViewBy.Label = GUILocalizeStrings.Get(1522);
        }
      }
      if (currentSearchMode == SearchMode.Description)
      {
        if (btnSearchDescription != null)
        {
          btnSearchDescription.Selected = true;
        }
        if (btnViewBy != null)
        {
          btnViewBy.Label = GUILocalizeStrings.Get(1521);
        }
      }
    }

    private void SortChanged(object sender, SortEventArgs e)
    {
      sortAscending = e.Order != SortOrder.Descending;

      Update();
      GUIControl.FocusControl(GetID, ((GUIControl)sender).GetID);
    }

    public override void Process()
    {
      TVHome.UpdateProgressPercentageBar();
    }

    private static Dictionary<int, Channel> GetChannelMap()
    {
      IEnumerable<Channel> channels = ServiceAgents.Instance.ChannelServiceAgent.ListAllChannels(ChannelRelation.None);
      return channels.ToDictionary(channel => channel.IdChannel);
    }

    private class Comparer : IComparer<GUIListItem>
    {
      private Dictionary<int, Channel> channelMap;
      private SortMethod currentSortMethod;
      private bool sortAscending;
      public Comparer(SortMethod currentSortMethod, bool sortAscending)
      {
        channelMap = new Dictionary<int, Channel>();
        this.currentSortMethod = currentSortMethod;
        this.sortAscending = sortAscending;
        if (currentSortMethod == SortMethod.Channel)
        {
          channelMap = GetChannelMap();
        }
      }
      public int Compare(GUIListItem item1, GUIListItem item2)
      {
        if (item1 == item2)
        {
          return 0;
        }
        if (item1 == null)
        {
          return -1;
        }
        if (item2 == null)
        {
          return -1;
        }
        if (item1.IsFolder && item1.Label == "..")
        {
          return -1;
        }
        if (item2.IsFolder && item2.Label == "..")
        {
          return 1;
        }

        Program prog1 = item1.TVTag as Program;
        Program prog2 = item2.TVTag as Program;

        int iComp = 0;
        switch (currentSortMethod)
        {
          case SortMethod.Name:
            if (sortAscending)
            {
              iComp = string.Compare(item1.Label, item2.Label, true);
            }
            else
            {
              iComp = string.Compare(item2.Label, item1.Label, true);
            }
            return iComp;

          case SortMethod.Channel:
            if (prog1 != null && prog2 != null)
            {
              Channel ch1 = channelMap[prog1.IdChannel];
              Channel ch2 = channelMap[prog2.IdChannel];
              if (sortAscending)
              {
                iComp = string.Compare(ch1.Name, ch2.Name, true);
              }
              else
              {
                iComp = string.Compare(ch2.Name, ch1.Name, true);
              }
              return iComp;
            }
            return 0;

          case SortMethod.Date:
            if (prog1 != null && prog2 != null)
            {
              if (sortAscending)
              {
                iComp = prog1.StartTime.CompareTo(prog2.StartTime);
              }
              else
              {
                iComp = prog2.StartTime.CompareTo(prog1.StartTime);
              }
              return iComp;
            }
            return 0;
        }
        return iComp;
      }
    }
  }
}