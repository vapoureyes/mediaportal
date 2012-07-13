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
using System.Linq;
using System.Windows.Forms;
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.SetupControls.UserInterfaceControls;
using Mediaportal.TV.Server.TVControl;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Simmetrics;
using Mediaportal.TV.Server.TVService.ServiceAgents;


namespace Mediaportal.TV.Server.SetupTV.Sections
{
  public partial class ChannelCombinations : SectionSettings
  {
    public class CardInfo
    {
      protected Card _card;

      public Card Card
      {
        get { return _card; }
      }

      public CardInfo(Card card)
      {
        _card = card;
      }

      public override string ToString()
      {
        return _card.name;
      }
    }

    private readonly MPListViewStringColumnSorter lvwColumnSorter2;
    private readonly MPListViewStringColumnSorter lvwColumnSorter3;

    private Dictionary<int, CardType> cards;
    private MediaTypeEnum _mediaTypeEnum;

    public ChannelCombinations(string name, MediaTypeEnum mediaType)
      : base(name)
    {
      _mediaTypeEnum = mediaType;
      InitializeComponent();

      lvwColumnSorter2 = new MPListViewStringColumnSorter();
      lvwColumnSorter3 = new MPListViewStringColumnSorter();
      lvwColumnSorter3.Order = SortOrder.Ascending;
      lvwColumnSorter2.Order = SortOrder.Descending;
      lvwColumnSorter2.OrderType = MPListViewStringColumnSorter.OrderTypes.AsValue;
      mpListViewMapped.ListViewItemSorter = lvwColumnSorter2;
      mpListViewChannels.ListViewItemSorter = lvwColumnSorter3;
    }

    public MediaTypeEnum MediaTypeEnum
    {
      get { return _mediaTypeEnum; }
      set { _mediaTypeEnum = value; }
    }

    public override void OnSectionDeActivated()
    {
      //DatabaseManager.Instance.SaveChanges();
      ServiceAgents.Instance.ControllerServiceAgent.OnNewSchedule();
      base.OnSectionDeActivated();
    }

    public override void OnSectionActivated()
    {
      cards = new Dictionary<int, CardType>();
      IList<Card> dbsCards = ServiceAgents.Instance.CardServiceAgent.ListAllCards(CardIncludeRelationEnum.None);
      mpComboBoxCard.Items.Clear();
      foreach (Card card in dbsCards)
      {
        if (card.enabled == false)
          continue;
        if (!ServiceAgents.Instance.ControllerServiceAgent.IsCardPresent(card.idCard))
          continue;
        cards[card.idCard] = ServiceAgents.Instance.ControllerServiceAgent.Type(card.idCard);
        mpComboBoxCard.Items.Add(new CardInfo(card));
      }
      mpComboBoxCard.SelectedIndex = 0;
      base.OnSectionActivated();
    }


    private void TvCombinations_Load(object sender, EventArgs e) {}

    private void mpComboBoxCard_SelectedIndexChanged(object sender, EventArgs e)
    {
      mpListViewChannels.BeginUpdate();
      try
      {
        mpListViewChannels.Items.Clear();
        mpListViewMapped.Items.Clear();

       
        CardInfo cardInfo = (CardInfo)mpComboBoxCard.SelectedItem;

        Card card = cardInfo.Card;
        CardType cardType = cards[card.idCard];
        IList<ChannelMap> maps = card.ChannelMaps;


        List<ListViewItem> items = new List<ListViewItem>();
        foreach (ChannelMap map in maps)
        {
          Channel channel = null;
          try
          {
            channel = map.Channel;
          }
          catch (Exception) {}
          if (channel == null)
            continue;
          if (channel.mediaType != (decimal) _mediaTypeEnum)
            continue;          
          bool enableDVBS2 = (ServiceAgents.Instance.SettingServiceAgent.GetSettingWithDefaultValue("dvbs" + card.idCard + "enabledvbs2", "false").value == "true");
          List<TuningDetail> tuningDetails = GetTuningDetailsByCardType(channel, cardType, enableDVBS2);
          int imageIndex = GetImageIndex(tuningDetails);
          ListViewItem item = new ListViewItem(channel.displayName, imageIndex);
          item.Tag = channel;
          items.Add(item);
        }
        mpListViewChannels.Items.AddRange(items.ToArray());
        mpListViewChannels.Sort();
      }
      finally
      {
        mpListViewChannels.EndUpdate();
      }
    }

    private void mpListViewChannels_SelectedIndexChanged(object sender, EventArgs e)
    {
      mpListViewMapped.BeginUpdate();
      try
      {
        mpListViewMapped.Items.Clear();
        if (mpListViewChannels.SelectedIndices == null)
          return;
        if (mpListViewChannels.SelectedIndices.Count != 1)
          return;
        Card card = ((CardInfo)mpComboBoxCard.SelectedItem).Card;
        ListViewItem selectedItem = mpListViewChannels.Items[mpListViewChannels.SelectedIndices[0]];
        Channel selectedChannel = (Channel)selectedItem.Tag;
        IList<Channel> allChannels = ServiceAgents.Instance.ChannelServiceAgent.ListAllChannels();
        List<ListViewItem> items = new List<ListViewItem>();
        NotifyForm dlg = new NotifyForm("Searching for Similar Channels...",
                                        "This can take some time\n\nPlease be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();
        foreach (Channel channel in allChannels)
        {
          if (channel.mediaType != (decimal)_mediaTypeEnum)
            continue;

          IList<ChannelMap> list = channel.ChannelMaps;
          bool isMapped = list.Any(map => map.idCard == card.idCard);
          if (isMapped)
            continue;

          Levenstein comparer = new Levenstein();
          float result = comparer.getSimilarity(selectedChannel.displayName, channel.displayName);

          IList<TuningDetail> details = channel.TuningDetails;
          int imageIndex = GetImageIndex(details);
          ListViewItem item = new ListViewItem((result * 100f).ToString("f2") + "%", imageIndex);
          item.Tag = channel;
          item.SubItems.Add(channel.displayName);
          items.Add(item);
        }
        mpListViewMapped.Items.AddRange(items.ToArray());
        mpListViewMapped.Sort();
        dlg.Close();
      }
      finally
      {
        mpListViewMapped.EndUpdate();
      }
    }

    private void btnCombine_Click(object sender, EventArgs e)
    {
      if (mpListViewChannels.SelectedIndices == null)
        return;
      if (mpListViewChannels.SelectedIndices.Count != 1)
        return;
      if (mpListViewMapped.SelectedIndices == null)
        return;

      ListViewItem selectedItem = mpListViewChannels.Items[mpListViewChannels.SelectedIndices[0]];
      Channel selectedChannel = (Channel)selectedItem.Tag;

      ListView.SelectedListViewItemCollection selectedItemsToCombine = mpListViewMapped.SelectedItems;

      foreach (ListViewItem listViewItem in selectedItemsToCombine)
      {
        Channel selectedChannel2 = (Channel)listViewItem.Tag;
        NotifyForm dlg = new NotifyForm("Combining Channels...", "Updating TuningDetail Table\n\nPlease be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();
        foreach (TuningDetail detail in selectedChannel2.TuningDetails)
        {
          detail.idChannel = selectedChannel.idChannel;
          ServiceAgents.Instance.ChannelServiceAgent.SaveTuningDetail(detail);
        }
        dlg.Close();
        dlg = new NotifyForm("Combining Channels...", "Updating ChannelMap Table\n\nPlease be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();
        foreach (ChannelMap map in selectedChannel2.ChannelMaps)
        {
          map.idChannel = selectedChannel.idChannel;
          ServiceAgents.Instance.ChannelServiceAgent.SaveChannelMap(map);
        }
        dlg.Close();
        dlg = new NotifyForm("Combining Channels...", "Updating GroupMap Table\n\nPlease be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();
        foreach (GroupMap groupMap in selectedChannel2.GroupMaps)
        {
          bool alreadyExistsInGroup = false;
          foreach (GroupMap groupMapMaster in selectedChannel.GroupMaps)
          {
            if (groupMapMaster.idGroup == groupMap.idGroup)
            {
              ServiceAgents.Instance.CardServiceAgent.DeleteGroupMap(groupMap.idMap);              
              alreadyExistsInGroup = true;
              continue;
            }
          }
          if (!alreadyExistsInGroup)
          {
            groupMap.idChannel = selectedChannel.idChannel;
            ServiceAgents.Instance.ChannelServiceAgent.SaveChannelGroupMap(groupMap);

          }
        }
        dlg.Close();
        dlg = new NotifyForm("Combining Channels...", "Updating Program Table\n\nPlease be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();
        foreach (Program program in selectedChannel2.Programs)
        {
          program.idChannel = selectedChannel.idChannel;
          ServiceAgents.Instance.ProgramServiceAgent.SaveProgram(program);
        }
        dlg.Close();
        dlg = new NotifyForm("Combining Channels...", "Updating Recording Table\n\nPlease be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();
        foreach (Recording recording in selectedChannel2.Recordings)
        {
          recording.idChannel = selectedChannel.idChannel;
          ServiceAgents.Instance.RecordingServiceAgent.SaveRecording(recording);
        }
        dlg.Close();
        dlg = new NotifyForm("Combining Channels...", "Updating Schedule Table\n\nPlease be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();
        foreach (Schedule schedule in selectedChannel2.Schedules)
        {
          schedule.idChannel = selectedChannel.idChannel;          
          ServiceAgents.Instance.ScheduleServiceAgent.SaveSchedule(schedule);
        }
        dlg.Close();
        ServiceAgents.Instance.ChannelServiceAgent.DeleteChannel(selectedChannel2.idChannel);
        mpListViewMapped.Items.Remove(listViewItem);
      }
    }

    private void mpListViewChannels_ColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == lvwColumnSorter3.SortColumn)
      {
        // Reverse the current sort direction for this column.
        lvwColumnSorter3.Order = lvwColumnSorter3.Order == SortOrder.Ascending
                                   ? SortOrder.Descending
                                   : SortOrder.Ascending;
      }
      else
      {
        // Set the column number that is to be sorted; default to ascending.
        lvwColumnSorter3.SortColumn = e.Column;
        lvwColumnSorter3.Order = SortOrder.Ascending;
      }

      // Perform the sort with these new sort options.
      mpListViewChannels.Sort();
    }

    private void mpListViewMapped_ColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == lvwColumnSorter2.SortColumn)
      {
        // Reverse the current sort direction for this column.
        lvwColumnSorter2.Order = lvwColumnSorter2.Order == SortOrder.Ascending
                                   ? SortOrder.Descending
                                   : SortOrder.Ascending;
      }
      else
      {
        // Set the column number that is to be sorted; default to ascending.
        lvwColumnSorter2.SortColumn = e.Column;
        lvwColumnSorter2.Order = SortOrder.Ascending;
      }

      // Perform the sort with these new sort options.
      mpListViewMapped.Sort();
    }

    private static List<TuningDetail> GetTuningDetailsByCardType(Channel channel, CardType cardType, bool enableDVBS2)
    {
      List<TuningDetail> result = new List<TuningDetail>();
      foreach (TuningDetail tDetail in channel.TuningDetails)
      {
        switch (cardType)
        {
          case CardType.Analog:
            if (tDetail.channelType == 0)
              result.Add(tDetail);
            break;
          case CardType.Atsc:
            if (tDetail.channelType == 1)
              result.Add(tDetail);
            break;
          case CardType.DvbC:
            if (tDetail.channelType == 2)
              result.Add(tDetail);
            break;
          case CardType.DvbS:
            if (tDetail.channelType == 3)
            {
              if (!enableDVBS2 && (tDetail.pilot > -1 || tDetail.rollOff > -1))
              {
                Log.Debug(String.Format(
                  "Imported channel {0} detected as DVB-S2. Skipped! \n Enable \"DVB-S2 tuning\" option in your TV-Card properties to be able to combine these channels.",
                  tDetail.name));
              }
              else
              {
                result.Add(tDetail);
              }
            }
            break;
          case CardType.DvbT:
            if (tDetail.channelType == 4)
              result.Add(tDetail);
            break;
          case CardType.DvbIP:
            if (tDetail.channelType == 7)
              result.Add(tDetail);
            break;
          case CardType.RadioWebStream:
            if (tDetail.channelType == 5)
              result.Add(tDetail);
            break;
          default:
            break;
        }
      }
      return result;
    }

    private static int GetImageIndex(IList<TuningDetail> tuningDetails)
    {
      bool hasFta = false;
      bool hasScrambled = false;
      foreach (TuningDetail detail in tuningDetails)
      {
        if (detail.freeToAir)
        {
          hasFta = true;
        }
        if (!detail.freeToAir)
        {
          hasScrambled = true;
        }
      }

      int imageIndex;
      if (hasFta && hasScrambled)
      {
        imageIndex = 5;
      }
      else if (hasScrambled)
      {
        imageIndex = 4;
      }
      else
      {
        imageIndex = 3;
      }
      return imageIndex;
    }
  }
}