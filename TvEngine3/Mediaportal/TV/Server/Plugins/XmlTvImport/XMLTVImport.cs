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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using MediaPortal.Common.Utils;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;

namespace Mediaportal.TV.Server.Plugins.XmlTvImport
{
  internal class XMLTVImport : IComparer
  {


    private readonly IDictionary<string, ProgramCategory> _categories = new ConcurrentDictionary<string, ProgramCategory>();

    private readonly ProgramManagement _programManagement = new ProgramManagement();
    
    public delegate void ShowProgressHandler(Stats stats);

    public event ShowProgressHandler ShowProgress;

    private class ChannelPrograms
    {
      public string Name;
      public string externalId;
      //public ArrayList programs = new ArrayList();
      public readonly ProgramList programs = new ProgramList();
    } ;

    public class Stats
    {
      private string _status = "";
      private int _programs = 0;
      private int _channels = 0;
      private DateTime _startTime = DateTime.Now;
      private DateTime _endTime = DateTime.Now;

      public string Status
      {
        get { return _status; }
        set { _status = value; }
      }

      public int Programs
      {
        get { return _programs; }
        set { _programs = value; }
      }

      public int Channels
      {
        get { return _channels; }
        set { _channels = value; }
      }

      public DateTime startTime
      {
        get { return _startTime; }
        set { _startTime = value; }
      }

      public DateTime endTime
      {
        get { return _endTime; }
        set { _endTime = value; }
      }
    } ;

    private string _errorMessage = "";
    private Stats _status = new Stats();
    private int _backgroundDelay = 0;
    

    private static bool _isImporting = false;

    public XMLTVImport()
      : this(0) {}

    public XMLTVImport(int backgroundDelay)
    {
      IEnumerable<ProgramCategory> categories = ProgramCategoryManagement.ListAllProgramCategories();

      foreach (var programCategory in categories)
      {
        _categories.Add(programCategory.Category, programCategory);
      }

      _backgroundDelay = backgroundDelay;
    }

    public string ErrorMessage
    {
      get { return _errorMessage; }
    }

    public Stats ImportStats
    {
      get { return _status; }
    }

    private int ParseStarRating(string epgRating)
    {
      int Rating = -1;
      try
      {
        // format = 5.2/10
        // check if the epgRating is within a xml tag			
        epgRating = epgRating.Trim();
        if (string.IsNullOrEmpty(epgRating))
          return Rating;

        if (epgRating.StartsWith("<"))
        {
          int endStartTagIdx = epgRating.IndexOf(">") + 1;
          int length = epgRating.IndexOf("</", endStartTagIdx) - endStartTagIdx;
          epgRating = epgRating.Substring(endStartTagIdx, length);
        }
        string strRating = epgRating;
        int slashPos = strRating.IndexOf('/');
        // Some EPG providers only supply the value without n/10
        if (slashPos > 0)
          strRating = strRating.Remove(slashPos);

        decimal tmpRating = -1;
        NumberFormatInfo NFO = NumberFormatInfo.InvariantInfo;
        NumberStyles NStyle = NumberStyles.Float;

        if (Decimal.TryParse(strRating, NStyle, NFO, out tmpRating))
          Rating = Convert.ToInt16(tmpRating);
        else
          Log.Info("XMLTVImport: star-rating could not be used - {0},({1})", epgRating, strRating);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "XMLTVImport: Error parsing star-rating - {0}", epgRating);
      }
      return Rating;
    }

    public bool Import(string fileName, bool deleteBeforeImport, bool showProgress)
    {
      //System.Diagnostics.Debugger.Launch();
      _errorMessage = "";
      if (_isImporting == true)
      {
        _errorMessage = "already importing...";
        return false;
      }
      _isImporting = true;

      bool result = false;
      XmlTextReader xmlReader = null;


      // remove old programs
      _status.Status = "Removing old programs";
      _status.Channels = 0;
      _status.Programs = 0;
      _status.startTime = DateTime.Now;
      _status.endTime = new DateTime(1971, 11, 6);
      if (showProgress && ShowProgress != null) ShowProgress(_status);

      bool useTimeZone = SettingsManagement.GetSetting("xmlTvUseTimeZone", "false").Value == "true";
      int hours = Int32.Parse(SettingsManagement.GetSetting("xmlTvTimeZoneHours", "0").Value);
      int mins = Int32.Parse(SettingsManagement.GetSetting("xmlTvTimeZoneMins", "0").Value);
      int timeZoneCorrection = hours * 60 + mins;

      var Programs = new ArrayList();
      var dChannelPrograms = new Dictionary<int, ChannelPrograms>();
      try
      {
        //layer.RemoveOldPrograms();        
        ProgramManagement.DeleteOldPrograms();        
        Log.Debug("xmltv import {0}", fileName);        
        //
        // Make sure the file exists before we try to do any processing
        //
        if (File.Exists(fileName))
        {
          _status.Status = "Loading channel list";
          _status.Channels = 0;
          _status.Programs = 0;
          _status.startTime = DateTime.Now;
          _status.endTime = new DateTime(1971, 11, 6);
          if (showProgress && ShowProgress != null) ShowProgress(_status);

          var guideChannels = new Dictionary<int, Channel>();
          IList<Channel> allChannels = ChannelManagement.ListAllChannels().ToList();

          int iChannel = 0;

          xmlReader = new XmlTextReader(fileName);

          #region import non-mapped channels by their display-name

          if (xmlReader.ReadToDescendant("tv"))
          {
            // get the first channel
            if (xmlReader.ReadToDescendant("channel"))
            {
              do
              {
                String id = xmlReader.GetAttribute("id");
                if (id == null || id.Length == 0)
                {
                  Log.Error("  channel#{0} doesnt contain an id", iChannel);
                }
                else
                {
                  String displayName = null;

                  XmlReader xmlChannel = xmlReader.ReadSubtree();
                  xmlChannel.ReadStartElement(); // read channel
                  // now, xmlChannel is positioned on the first sub-element of <channel>
                  while (!xmlChannel.EOF)
                  {
                    if (xmlChannel.NodeType == XmlNodeType.Element)
                    {
                      switch (xmlChannel.Name)
                      {
                        case "display-name":
                        case "Display-Name":
                          if (displayName == null) displayName = xmlChannel.ReadString();
                          else xmlChannel.Skip();
                          break;
                          // could read more stuff here, like icon...
                        default:
                          // unknown, skip entire node
                          xmlChannel.Skip();
                          break;
                      }
                    }
                    else
                      xmlChannel.Read();
                  }
                  if (xmlChannel != null)
                  {
                    xmlChannel.Close();
                    xmlChannel = null;
                  }

                  if (displayName == null || displayName.Length == 0)
                  {
                    Log.Error("  channel#{0} xmlid:{1} doesnt contain an displayname", iChannel, id);
                  }
                  else
                  {
                    Channel chan = null;

                    // a guide channel can be mapped to multiple tvchannels
                    foreach (Channel ch in allChannels)
                    {
                      if (ch.ExternalId == id)
                      {
                        chan = ch;
                        chan.ExternalId = id;
                      }

                      if (chan == null)
                      {
                        // no mapping found, ignore channel
                        continue;
                      }

                      ChannelPrograms newProgChan = new ChannelPrograms();
                      newProgChan.Name = chan.DisplayName;
                      newProgChan.externalId = chan.ExternalId;
                      Programs.Add(newProgChan);

                      Log.Debug("  channel#{0} xmlid:{1} name:{2} dbsid:{3}", iChannel, chan.ExternalId,
                                    chan.DisplayName, chan.IdChannel);
                      if (!guideChannels.ContainsKey(chan.IdChannel))
                      {
                        guideChannels.Add(chan.IdChannel, chan);
                        dChannelPrograms.Add(chan.IdChannel, newProgChan);
                      }
                    }

                    _status.Channels++;
                    if (showProgress && ShowProgress != null) ShowProgress(_status);
                  }
                }
                iChannel++;
                // get the next channel
              } while (xmlReader.ReadToNextSibling("channel"));
            }
          }

          //xmlReader.Close();

          #endregion

          allChannels = ChannelManagement.GetAllChannelsWithExternalId().ToList();          
          if (allChannels.Count == 0)
          {
            _isImporting = false;
            if (xmlReader != null)
            {
              xmlReader.Close();
              xmlReader = null;
            }

            return true;
          }

          ///////////////////////////////////////////////////////////////////////////
          /*  design:
           * 1. create a Dictionary<string,Channel> using the externalid as the key,
           *    add all channels to this Dictionary 
           *    Note: channel -> guidechannel is a one-to-many relationship. 
           * 2. Read all programs from the xml file
           * 3. Create a program for each mapped channel
           */
          ///////////////////////////////////////////////////////////////////////////
          Dictionary<string, List<Channel>> allChannelMappingsByexternalId = new Dictionary<string, List<Channel>>();

          string previousexternalId = null;
          // one-to-many so we need a collection of channels for each externalId
          List<Channel> eidMappedChannels = new List<Channel>();

          for (int i = 0; i < allChannels.Count; i++)
          {
            Channel ch = allChannels[i];

            if (previousexternalId == null)
            {
              eidMappedChannels.Add(ch);
              previousexternalId = ch.ExternalId;
            }
            else if (ch.ExternalId == previousexternalId)
            {
              eidMappedChannels.Add(ch);
            }
            else
            {
              // got all channels for this externalId. Add the mappings
              allChannelMappingsByexternalId.Add(previousexternalId, eidMappedChannels);
              // new externalid, create a new List & add the channel to the new List
              eidMappedChannels = new List<Channel>();
              eidMappedChannels.Add(ch);
              previousexternalId = ch.ExternalId;
            }

            if (i == allChannels.Count - 1)
            {
              allChannelMappingsByexternalId.Add(previousexternalId, eidMappedChannels);
            }
          }

          int programIndex = 0;
          _status.Status = "Loading TV programs";
          if (showProgress && ShowProgress != null) ShowProgress(_status);

          Log.Debug("xmltvimport: Reading TV programs");
          if (xmlReader != null)
          {
            xmlReader.Close();
            xmlReader = null;
          }
          xmlReader = new XmlTextReader(fileName);
          if (xmlReader.ReadToDescendant("tv"))
          {
            // get the first programme
            if (xmlReader.ReadToDescendant("programme"))
            {
              #region read programme node

              do
              {
                ChannelPrograms channelPrograms = new ChannelPrograms();

                String nodeStart = xmlReader.GetAttribute("start");
                String nodeStop = xmlReader.GetAttribute("stop");
                String nodeChannel = xmlReader.GetAttribute("channel");

                List<ProgramCredit> credits = null;
                XmlReader nodeCredits = null;
                String nodeTitle = null;
                String nodeCategory = null;
                String nodeDescription = null;
                String nodeEpisode = null;
                String nodeRepeat = null;
                String nodeEpisodeNum = null;
                String nodeEpisodeNumSystem = null;
                String nodeDate = null;
                String nodeStarRating = null;
                String nodeClassification = null;

                XmlReader xmlProg = xmlReader.ReadSubtree();
                xmlProg.ReadStartElement(); // read programme
                // now, xmlProg is positioned on the first sub-element of <programme>
                while (!xmlProg.EOF)
                {
                  if (xmlProg.NodeType == XmlNodeType.Element)
                  {
                    switch (xmlProg.Name)
                    {
                      case "title":
                        if (nodeTitle == null) nodeTitle = xmlProg.ReadString();
                        else xmlProg.Skip();
                        break;
                      case "category":
                        if (nodeCategory == null) nodeCategory = xmlProg.ReadString();
                        else xmlProg.Skip();
                        break;
                      case "credits":
                        if (nodeCredits == null)
                        {
                          nodeCredits = xmlProg.ReadSubtree();
                          if (nodeCredits != null)
                          {
                            credits = ParseCredits(nodeCredits);
                          }
                        }
                        else xmlProg.Skip();
                        break;
                      case "desc":
                        if (nodeDescription == null) nodeDescription = xmlProg.ReadString();
                        else xmlProg.Skip();
                        break;
                      case "sub-title":
                        if (nodeEpisode == null) nodeEpisode = xmlProg.ReadString();
                        else xmlProg.Skip();
                        break;
                      case "previously-shown":
                        if (nodeRepeat == null) nodeRepeat = xmlProg.ReadString();
                        else xmlProg.Skip();
                        break;
                      case "episode-num":
                        if (nodeEpisodeNum == null)
                        {
                          nodeEpisodeNumSystem = xmlProg.GetAttribute("system");
                          nodeEpisodeNum = xmlProg.ReadString();
                        }
                        else xmlProg.Skip();
                        break;
                      case "date":
                        if (nodeDate == null) nodeDate = xmlProg.ReadString();
                        else xmlProg.Skip();
                        break;
                      case "star-rating":
                        if (nodeStarRating == null) nodeStarRating = xmlProg.ReadInnerXml();
                        else xmlProg.Skip();
                        break;
                      case "rating":
                        if (nodeClassification == null) nodeClassification = xmlProg.ReadInnerXml();
                        else xmlProg.Skip();
                        break;
                      default:
                        // unknown, skip entire node
                        xmlProg.Skip();
                        break;
                    }
                  }
                  else
                    xmlProg.Read();
                }
                if (xmlProg != null)
                {
                  xmlProg.Close();
                  xmlProg = null;
                }

                #endregion

                #region verify/convert values (programme)

                if (nodeStart != null && nodeChannel != null && nodeTitle != null &&
                    nodeStart.Length > 0 && nodeChannel.Length > 0 && nodeTitle.Length > 0)
                {
                  
                  string description = "";
                  string category = "-";
                  string serEpNum = "";
                  string date = "";
                  string seriesNum = "";
                  string episodeNum = "";
                  string episodeName = "";
                  string episodePart = "";
                  int starRating = -1;
                  string classification = "";
                  bool repeat = false;

                  string title = ConvertHTMLToAnsi(nodeTitle);

                  long startDate = 0;
                  if (nodeStart.Length >= 14)
                  {
                    if (Char.IsDigit(nodeStart[12]) && Char.IsDigit(nodeStart[13]))
                      startDate = Int64.Parse(nodeStart.Substring(0, 14)); //20040331222000
                    else
                      startDate = 100 * Int64.Parse(nodeStart.Substring(0, 12)); //200403312220
                  }
                  else if (nodeStart.Length >= 12)
                  {
                    startDate = 100 * Int64.Parse(nodeStart.Substring(0, 12)); //200403312220
                  }

                  long stopDate = startDate;
                  if (nodeStop != null)
                  {
                    if (nodeStop.Length >= 14)
                    {
                      if (Char.IsDigit(nodeStop[12]) && Char.IsDigit(nodeStop[13]))
                        stopDate = Int64.Parse(nodeStop.Substring(0, 14)); //20040331222000
                      else
                        stopDate = 100 * Int64.Parse(nodeStop.Substring(0, 12)); //200403312220
                    }
                    else if (nodeStop.Length >= 12)
                    {
                      stopDate = 100 * Int64.Parse(nodeStop.Substring(0, 12)); //200403312220
                    }
                  }

                  startDate = CorrectIllegalDateTime(startDate);
                  stopDate = CorrectIllegalDateTime(stopDate);
                  string timeZoneStart = "";
                  string timeZoneEnd = "";
                  if (nodeStart.Length > 14)
                  {
                    timeZoneStart = nodeStart.Substring(14);
                    timeZoneStart = timeZoneStart.Trim();
                    timeZoneEnd = timeZoneStart;
                  }
                  if (nodeStop != null)
                  {
                    if (nodeStop.Length > 14)
                    {
                      timeZoneEnd = nodeStop.Substring(14);
                      timeZoneEnd = timeZoneEnd.Trim();
                    }
                  }

                  //
                  // add time correction
                  //

                  // correct program starttime
                  DateTime dateTimeStart = longtodate(startDate);
                  dateTimeStart = dateTimeStart.AddMinutes(timeZoneCorrection);

                  if (useTimeZone)
                  {
                    int off = GetTimeOffset(timeZoneStart);
                    int h = off / 100; // 220 -> 2,  -220 -> -2
                    int m = off - (h * 100); // 220 -> 20, -220 -> -20

                    dateTimeStart = dateTimeStart.AddHours(-h);
                    dateTimeStart = dateTimeStart.AddMinutes(-m);
                  }
                  startDate = datetolong(dateTimeStart);

                  if (nodeStop != null)
                  {
                    // correct program endtime
                    DateTime dateTimeEnd = longtodate(stopDate);
                    dateTimeEnd = dateTimeEnd.AddMinutes(timeZoneCorrection);

                    if (useTimeZone)
                    {
                      int off = GetTimeOffset(timeZoneEnd);
                      int h = off / 100; // 220 -> 2,  -220 -> -2
                      int m = off - (h * 100); // 220 -> 20, -220 -> -20

                      dateTimeEnd = dateTimeEnd.AddHours(-h);
                      dateTimeEnd = dateTimeEnd.AddMinutes(-m);
                    }
                    stopDate = datetolong(dateTimeEnd);
                  }
                  else stopDate = startDate;

                  //int channelId = -1;
                  //string channelName = "";

                  if (nodeCategory != null)
                    category = nodeCategory;

                  if (nodeDescription != null)
                  {
                    description = ConvertHTMLToAnsi(nodeDescription);
                  }
                  if (nodeEpisode != null)
                  {
                    episodeName = ConvertHTMLToAnsi(nodeEpisode);
                    if (title.Length == 0)
                      title = nodeEpisode;
                  }

                  if (nodeEpisodeNum != null)
                  {
                    if (nodeEpisodeNumSystem != null)
                    {
                      // http://xml.coverpages.org/XMLTV-DTD-20021210.html
                      if (nodeEpisodeNumSystem == "xmltv_ns")
                      {
                        serEpNum = ConvertHTMLToAnsi(nodeEpisodeNum.Replace(" ", ""));
                        int dot1 = serEpNum.IndexOf(".", 0);
                        int dot2 = serEpNum.IndexOf(".", dot1 + 1);
                        seriesNum = serEpNum.Substring(0, dot1);
                        episodeNum = serEpNum.Substring(dot1 + 1, dot2 - (dot1 + 1));
                        episodePart = serEpNum.Substring(dot2 + 1, serEpNum.Length - (dot2 + 1));
                        //xmltv_ns is theorically zero-based number will be increased by one
                        seriesNum = CorrectEpisodeNum(seriesNum, 1);
                        episodeNum = CorrectEpisodeNum(episodeNum, 1);
                        episodePart = CorrectEpisodeNum(episodePart, 1);
                      }
                      else if (nodeEpisodeNumSystem == "onscreen")
                      {
                        // example: 'Episode #FFEE' 
                        serEpNum = ConvertHTMLToAnsi(nodeEpisodeNum);
                        int num1 = serEpNum.IndexOf("#", 0);
                        if (num1 < 0) num1 = 0;
                        episodeNum = CorrectEpisodeNum(serEpNum.Substring(num1, serEpNum.Length - num1), 0);
                      }
                    }
                    else
                      // fixing mantis bug 1486: XMLTV import doesn't take episode number from TVGuide.xml made by WebEPG 
                    {
                      // example: '5' like WebEPG is creating
                      serEpNum = ConvertHTMLToAnsi(nodeEpisodeNum.Replace(" ", ""));
                      episodeNum = CorrectEpisodeNum(serEpNum, 0);
                    }
                  }

                  if (nodeDate != null)
                  {
                    date = nodeDate;
                  }

                  repeat = (nodeRepeat != null);                  

                  if (nodeStarRating != null)
                  {
                    starRating = ParseStarRating(nodeStarRating);
                  }

                  if (nodeClassification != null)
                  {
                    classification = nodeClassification;
                  }

                  if (showProgress && ShowProgress != null && (_status.Programs % 100) == 0) ShowProgress(_status);

                  #endregion

                  #region create a program for every mapped channel

                  List<Channel> mappedChannels;
                  //var programs = new List<TVDatabase.Entities.Program>();

                  if (allChannelMappingsByexternalId.ContainsKey(nodeChannel))
                  {
                    mappedChannels = allChannelMappingsByexternalId[nodeChannel];
                    if (mappedChannels != null && mappedChannels.Count > 0)
                    {
                      foreach (Channel chan in mappedChannels)
                      {
                        // get the channel program
                        channelPrograms = dChannelPrograms[chan.IdChannel];

                        if (chan.IdChannel < 0)
                        {
                          continue;
                        }

                        title = title.Replace("\r\n", " ");
                        title = title.Replace("\n\r", " ");
                        title = title.Replace("\r", " ");
                        title = title.Replace("\n", " ");
                        title = title.Replace("  ", " ");

                        description = description.Replace("\r\n", " ");
                        description = description.Replace("\n\r", " ");
                        description = description.Replace("\r", " ");
                        description = description.Replace("\n", " ");
                        description = description.Replace("  ", " ");

                        episodeName = episodeName.Replace("\r\n", " ");
                        episodeName = episodeName.Replace("\n\r", " ");
                        episodeName = episodeName.Replace("\r", " ");
                        episodeName = episodeName.Replace("\n", " ");
                        episodeName = episodeName.Replace("  ", " ");
                    
                        var prg = new Program();
                        prg.IdChannel = chan.IdChannel;
                        prg.StartTime = longtodate(startDate);
                        prg.EndTime = longtodate(stopDate);
                        prg.Title = title;
                        prg.Description = description;                        
                        prg.State = (int)ProgramState.None;
                        prg.OriginalAirDate = System.Data.SqlTypes.SqlDateTime.MinValue.Value;
                        prg.SeriesNum = seriesNum;
                        prg.EpisodeNum = episodeNum;
                        prg.EpisodeName = episodeName;
                        prg.EpisodePart = episodePart;
                        prg.StarRating = starRating;
                        prg.Classification = classification;
                        prg.ParentalRating = -1;
                        prg.PreviouslyShown = repeat;
                        if (credits != null && credits.Count > 0)
                        {
                          foreach (var credit in credits)
                          {
                            prg.ProgramCredits.Add(credit);                            
                          }                          
                        }                        

                        ProgramCategory programCategory;
                        bool hasCategory = _categories.TryGetValue(category, out programCategory);
                        if (!hasCategory)
                        {
                          programCategory = new ProgramCategory {Category = category};
                          ProgramCategoryManagement.AddCategory(programCategory);
                          _categories[category] = programCategory;
                        }
                        prg.IdProgramCategory = programCategory.IdProgramCategory;
                        channelPrograms.programs.Add(prg);
                        
                        //programs.Add(prg);                                              
                        programIndex++;                      
                        _status.Programs++;
                      }                      
                    }
                  }
                }
                // get the next programme
              } while (xmlReader.ReadToNextSibling("programme"));

              #endregion

              #region sort & remove invalid programs. Save all valid programs

              Log.Debug("xmltvimport: Sorting TV programs");

              _status.Programs = 0;
              _status.Status = "Sorting TV programs";
              if (showProgress && ShowProgress != null) ShowProgress(_status);
              DateTime dtStartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0, 0);
              //dtStartDate=dtStartDate.AddDays(-4);

              foreach (ChannelPrograms progChan in Programs)
              {
                // empty, skip it
                if (progChan.programs.Count == 0) continue;

                progChan.programs.Sort();
                progChan.programs.AlreadySorted = true;
                progChan.programs.FixEndTimes();
                progChan.programs.RemoveOverlappingPrograms(); // be sure that we do not have any overlapping

                // get the id of the channel, just get the idChannel of the first program
                int idChannel = progChan.programs[0].IdChannel;

                if (!deleteBeforeImport)
                {                  
                  _programManagement.DeleteAllProgramsWithChannelId(idChannel);
                  List<Program> programs = _programManagement.FindAllProgramsByChannelId(idChannel).ToList();
                  progChan.programs.RemoveOverlappingPrograms(programs, true);
                }

                for (int i = 0; i < progChan.programs.Count; ++i)
                {
                  var prog = progChan.programs[i];
                  // don't import programs which have already ended...
                  if (prog.EndTime <= dtStartDate)
                  {
                    progChan.programs.RemoveAt(i);
                    i--;
                    continue;
                  }
                  DateTime airDate = System.Data.SqlTypes.SqlDateTime.MinValue.Value;
                  try
                  {
                    airDate = prog.OriginalAirDate.GetValueOrDefault(DateTime.MinValue);
                    if (airDate > System.Data.SqlTypes.SqlDateTime.MinValue.Value &&
                        airDate < System.Data.SqlTypes.SqlDateTime.MaxValue.Value)
                      prog.OriginalAirDate = airDate;
                  }
                  catch (Exception)
                  {
                    Log.Info("XMLTVImport: Invalid year for OnAirDate - {0}", prog.OriginalAirDate);
                  }

                  if (prog.StartTime < _status.startTime)
                    _status.startTime = prog.StartTime;
                  if (prog.EndTime > _status.endTime)
                    _status.endTime = prog.EndTime;
                  _status.Programs++;
                  if (showProgress && ShowProgress != null && (_status.Programs % 100) == 0) ShowProgress(_status);
                }
                Log.Info("XMLTVImport: Inserting {0} programs for {1}", progChan.programs.Count.ToString(),
                         progChan.Name);
                _programManagement.InsertPrograms(progChan.programs,
                                     deleteBeforeImport
                                       ? DeleteBeforeImportOption.OverlappingPrograms
                                       : DeleteBeforeImportOption.None, ThreadPriority.BelowNormal);
              }
            }

            #endregion

            //TVDatabase.RemoveOverlappingPrograms();

            //TVDatabase.SupressEvents = false;
            if (programIndex > 0)
            {
              _errorMessage = "File imported successfully";
              result = true;
            }
            else
              _errorMessage = "No programs found";
          }
        }
        else
        {
          _errorMessage = "No xmltv file found";
          _status.Status = _errorMessage;
          Log.Error("xmltv data file was not found");
        }
      }
      catch (Exception ex)
      {
        _errorMessage = String.Format("Invalid XML file:{0}", ex.Message);
        _status.Status = String.Format("invalid XML file:{0}", ex.Message);
        Log.Error(ex, "XML tv import error loading {0}", fileName);

        //TVDatabase.RollbackTransaction();
      }
      finally
      {
        _isImporting = false;        
      }

      Programs.Clear();
      Programs = null;
      
      //      TVDatabase.SupressEvents = false;
      if (xmlReader != null)
      {
        xmlReader.Close();
        xmlReader = null;
      }
      return result;
    }

    private static List<ProgramCredit> ParseCredits(XmlReader nodeCredits)
    {
      var programcredits = new List<ProgramCredit>();

      using (nodeCredits)
      {
        nodeCredits.ReadStartElement();
        while (!nodeCredits.EOF)
        {
          if (nodeCredits.NodeType == XmlNodeType.Element)
          {
            string creditRole = nodeCredits.Name;
            if (creditRole.Length > 50)
            {
              creditRole = creditRole.Substring(0, 50);  
            }
            string creditPerson = nodeCredits.ReadString();
            if (creditPerson.Length > 200)
            {
              creditPerson = creditPerson.Substring(0, 200);
            }
            var credit = new ProgramCredit {Role = creditRole, Person = creditPerson};
            programcredits.Add(credit);
          }
          nodeCredits.Read();
        }
      }
      return programcredits;
    }

    /// <summary>
    /// Parse and correct ep. # in the episode string
    /// </summary>
    /// <param name="episodenum"></param>
    /// <param name="nodeEpisodeNumSystemBase">int to add to the parsed episode num (depends on 0-based or not xmltv files)</param>
    /// <returns></returns>
    private string CorrectEpisodeNum(string episodenum, int nodeEpisodeNumSystemBase)
    {
      if (episodenum == "")
        return episodenum;

      // Find format of the episode number
      int slashpos = episodenum.IndexOf("/", 0);
      if (slashpos == -1)
      {
        // No slash found => assume it's just a plain number
        try
        {
          int epnum = Convert.ToInt32(episodenum);
          return Convert.ToString(epnum + nodeEpisodeNumSystemBase);
        }
        catch (Exception)
        {
          Log.Debug("XMLTVImport::CorrectEpisodeNum, could not parse '{0}' as plain number", episodenum);
        }
      }
      else
      {
        try
        {
          // Slash found -> assume it's formatted as <episode number>/<episodes>
          int epnum = Convert.ToInt32(episodenum.Substring(0, slashpos));
          int epcount = Convert.ToInt32(episodenum.Substring(slashpos + 1));
          return Convert.ToString(epnum + nodeEpisodeNumSystemBase) + "/" + Convert.ToString(epcount);
        }
        catch (Exception)
        {
          Log.Debug("XMLTVImport::CorrectEpisodeNum, could not parse '{0}' as episode/episodes", episodenum);
        }
      }
      return "";
    }

    private int GetTimeOffset(string timeZone)
    {
      // timezone can b in format:
      // GMT +0100 or GMT -0500
      // or just +0300
      if (timeZone.Length == 0) return 0;
      timeZone = timeZone.ToLowerInvariant();

      // just ignore GMT offsets, since we're calculating everything from GMT anyway
      if (timeZone.IndexOf("gmt") >= 0)
      {
        int ipos = timeZone.IndexOf("gmt");
        timeZone = timeZone.Substring(ipos + "GMT".Length);
      }

      timeZone = timeZone.Trim();
      if (timeZone[0] == '+' || timeZone[0] == '-')
      {
        string strOff = timeZone.Substring(1);
        try
        {
          int iOff = Int32.Parse(strOff);
          if (timeZone[0] == '-') return -iOff;
          else return iOff;
        }
        catch (Exception) {}
      }
      return 0;
    }

    private long CorrectIllegalDateTime(long datetime)
    {
      //format : 20050710245500
      long orgDateTime = datetime;
      long sec = datetime % 100;
      datetime /= 100;
      long min = datetime % 100;
      datetime /= 100;
      long hour = datetime % 100;
      datetime /= 100;
      long day = datetime % 100;
      datetime /= 100;
      long month = datetime % 100;
      datetime /= 100;
      long year = datetime;
      DateTime dt = new DateTime((int)year, (int)month, (int)day, 0, 0, 0);
      dt = dt.AddHours(hour);
      dt = dt.AddMinutes(min);
      dt = dt.AddSeconds(sec);


      long newDateTime = datetolong(dt);
      if (sec < 0 || sec > 59 ||
          min < 0 || min > 59 ||
          hour < 0 || hour >= 24 ||
          day < 0 || day > 31 ||
          month < 0 || month > 12)
      {
        //Log.Debug(LogType.EPG, true, "epg-import:tvguide.xml contains invalid date/time :{0} converted it to:{1}",
        //              orgDateTime, newDateTime);
      }

      return newDateTime;
    }

    public void RemoveOverlappingPrograms(ref ArrayList Programs)
    {
      try
      {
        if (Programs.Count == 0) return;
        Programs.Sort(this);
        Program prevProg = (Program)Programs[0];
        for (int i = 1; i < Programs.Count; i++)
        {
          Program newProg = (Program)Programs[i];
          if (newProg.StartTime < prevProg.EndTime) // we have an overlap here
          {
            // let us find out which one is the correct one
            if (newProg.StartTime > prevProg.StartTime) // newProg will create hole -> delete it
            {
              Programs.Remove(newProg);
              i--; // stay at the same position
              continue;
            }

            List<Program> prevList = new List<Program>();
            List<Program> newList = new List<Program>();
            prevList.Add(prevProg);
            newList.Add(newProg);
            Program syncPrev = prevProg;
            Program syncProg = newProg;
            for (int j = i + 1; j < Programs.Count; j++)
            {
              Program syncNew = (Program)Programs[j];
              if (syncPrev.EndTime == syncNew.StartTime)
              {
                prevList.Add(syncNew);
                syncPrev = syncNew;
                if (syncNew.StartTime > syncProg.EndTime)
                {
                  // stop point reached => delete Programs in newList
                  foreach (Program Prog in newList) Programs.Remove(Prog);
                  i = j - 1;
                  prevProg = syncPrev;
                  newList.Clear();
                  prevList.Clear();
                  break;
                }
              }
              else if (syncProg.EndTime == syncNew.StartTime)
              {
                newList.Add(syncNew);
                syncProg = syncNew;
                if (syncNew.StartTime > syncPrev.EndTime)
                {
                  // stop point reached => delete Programs in prevList
                  foreach (Program Prog in prevList) Programs.Remove(Prog);
                  i = j - 1;
                  prevProg = syncProg;
                  newList.Clear();
                  prevList.Clear();
                  break;
                }
              }
            }
            // check if a stop point was reached => if not delete newList
            if (newList.Count > 0)
            {
              foreach (Program Prog in prevList) Programs.Remove(Prog);
              i = Programs.Count;
              break;
            }
          }
          prevProg = newProg;
        }
      }
      catch (Exception ex)
      {
        Log.Error("XML tv import error:{1} \n {2} ", ex.Message, ex.StackTrace);
      }
    }

    public void FillInMissingDataFromDB(ref ArrayList Programs, ArrayList dbEPG)
    {
      Programs.Sort(this);
      dbEPG.Sort(this);
      Program prevProg = (Program)Programs[0];
      for (int i = 1; i < Programs.Count; i++)
      {
        Program newProg = (Program)Programs[i];
        if (newProg.StartTime > prevProg.EndTime) // we have a gab here
        {
          // try to find data in the database
          foreach (Program dbProg in dbEPG)
          {
            if ((dbProg.StartTime >= prevProg.EndTime) && (dbProg.EndTime <= newProg.StartTime))
            {
              Programs.Insert(i, dbProg.Clone());
              i++;
              prevProg = dbProg;
            }
            if (dbProg.StartTime >= newProg.EndTime) break; // no more data available
          }
        }
        prevProg = newProg;
      }
    }

    public long datetolong(DateTime dt)
    {
      try
      {
        long iSec = 0; //(long)dt.Second;
        long iMin = (long)dt.Minute;
        long iHour = (long)dt.Hour;
        long iDay = (long)dt.Day;
        long iMonth = (long)dt.Month;
        long iYear = (long)dt.Year;

        long lRet = (iYear);
        lRet = lRet * 100L + iMonth;
        lRet = lRet * 100L + iDay;
        lRet = lRet * 100L + iHour;
        lRet = lRet * 100L + iMin;
        lRet = lRet * 100L + iSec;
        return lRet;
      }
      catch (Exception) {}
      return 0;
    }

    public DateTime longtodate(long ldate)
    {
      try
      {
        if (ldate < 0) return DateTime.MinValue;
        int year, month, day, hour, minute, sec;
        sec = (int)(ldate % 100L);
        ldate /= 100L;
        minute = (int)(ldate % 100L);
        ldate /= 100L;
        hour = (int)(ldate % 100L);
        ldate /= 100L;
        day = (int)(ldate % 100L);
        ldate /= 100L;
        month = (int)(ldate % 100L);
        ldate /= 100L;
        year = (int)ldate;
        DateTime dt = new DateTime(year, month, day, hour, minute, 0, 0);
        return dt;
      }
      catch (Exception) {}
      return DateTime.Now;
    }

    public string ConvertHTMLToAnsi(string html)
    {
      string strippedHtml = String.Empty;
      ConvertHTMLToAnsi(html, out strippedHtml);
      return strippedHtml;
    }

    public void ConvertHTMLToAnsi(string html, out string strippedHtml)
    {
      strippedHtml = "";
      //	    int i=0; 
      if (html.Length == 0)
      {
        strippedHtml = "";
        return;
      }
      //int iAnsiPos=0;
      using (var writer = new StringWriter())
      {

        System.Web.HttpUtility.HtmlDecode(html, writer);
        String DecodedString = writer.ToString();
        strippedHtml = DecodedString.Replace("<br>", "\n");
      }            
    }

    #region Sort Members

    public int Compare(object x, object y)
    {
      if (x == y) return 0;
      Program item1 = (Program)x;
      Program item2 = (Program)y;
      if (item1 == null) return -1;
      if (item2 == null) return -1;

      if (item1.IdChannel != item2.IdChannel)
      {
        return String.Compare(item1.Channel.DisplayName, item2.Channel.DisplayName, true);
      }
      if (item1.StartTime > item2.StartTime) return 1;
      if (item1.StartTime < item2.StartTime) return -1;
      return 0;
    }

    #endregion
  }
}