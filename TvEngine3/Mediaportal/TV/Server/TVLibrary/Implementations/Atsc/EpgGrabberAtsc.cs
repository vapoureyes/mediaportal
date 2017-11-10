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
using System.Runtime.InteropServices;
using System.Threading;
using Mediaportal.TV.Server.Common.Types.Enum;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Analyzer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Channel;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channel;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Dvb;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Tuner;

namespace Mediaportal.TV.Server.TVLibrary.Implementations.Atsc
{
  /// <summary>
  /// An implementation of <see cref="IEpgGrabber"/> for electronic programme
  /// guide data formats used by ATSC and SCTE broadcasters.
  /// </summary>
  internal class EpgGrabberAtsc : EpgGrabberBase, ICallBackGrabber
  {
    #region constants

    // Note: the ATSC/SCTE RRT encoding differs from the Dish/BEV encoding.
    private static readonly IDictionary<byte, string> MAPPING_PROGRAM_CLASSIFICATIONS_MPAA = new Dictionary<byte, string>(6)
    {
      { 1, "G" },       // general
      { 2, "PG" },      // parental guidance
      { 3, "PG-13" },   // parental guidance under 13
      { 4, "R" },       // restricted
      { 5, "NC-17" },   // nobody 17 and under
      { 6, "X" },       // explicit
      { 7, "NR" }       // not rated
    };

    private static readonly IDictionary<byte, string> MAPPING_PROGRAM_RATINGS_VCHIP = new Dictionary<byte, string>(6)
    {
      { 1, "TV-Y" },    // all children
      { 2, "TV-Y7" },   // children 7 and older
      { 3, "TV-G" },    // general audience
      { 4, "TV-PG" },   // parental guidance
      { 5, "TV-14" },   // adults 14 and older
      { 6, "TV-MA" }    // mature audience
    };

    private static readonly IDictionary<byte, string> MAPPING_ATSC_GENRES = new Dictionary<byte, string>(256)
    {
      { 0x20, "Education" },
      { 0x21, "Entertainment" },
      { 0x22, "Movie" },
      { 0x23, "News" },
      { 0x24, "Religious" },
      { 0x25, "Sports" },
      { 0x26, "Other" },
      { 0x27, "Action" },
      { 0x28, "Advertisement" },
      { 0x29, "Animated" },
      { 0x2a, "Anthology" },
      { 0x2b, "Automobile" },
      { 0x2c, "Awards" },
      { 0x2d, "Baseball" },
      { 0x2e, "Basketball" },
      { 0x2f, "Bulletin" },
      { 0x30, "Business" },
      { 0x31, "Classical" },
      { 0x32, "College" },
      { 0x33, "Combat" },
      { 0x34, "Comedy" },
      { 0x35, "Commentary" },
      { 0x36, "Concert" },
      { 0x37, "Consumer" },
      { 0x38, "Contemporary" },
      { 0x39, "Crime" },
      { 0x3a, "Dance" },
      { 0x3b, "Documentary" },
      { 0x3c, "Drama" },
      { 0x3d, "Elementary" },
      { 0x3e, "Erotica" },
      { 0x3f, "Exercise" },
      { 0x40, "Fantasy" },
      { 0x41, "Farm" },
      { 0x42, "Fashion" },
      { 0x43, "Fiction" },
      { 0x44, "Food" },
      { 0x45, "Football" },
      { 0x46, "Foreign" },
      { 0x47, "Fund Raiser" },
      { 0x48, "Game/Quiz" },
      { 0x49, "Garden" },
      { 0x4a, "Golf" },
      { 0x4b, "Government" },
      { 0x4c, "Health" },
      { 0x4d, "High School" },
      { 0x4e, "History" },
      { 0x4f, "Hobby" },
      { 0x50, "Hockey" },
      { 0x51, "Home" },
      { 0x52, "Horror" },
      { 0x53, "Information" },
      { 0x54, "Instruction" },
      { 0x55, "International" },
      { 0x56, "Interview" },
      { 0x57, "Language" },
      { 0x58, "Legal" },
      { 0x59, "Live" },
      { 0x5a, "Local" },
      { 0x5b, "Math" },
      { 0x5c, "Medical" },
      { 0x5d, "Meeting" },
      { 0x5e, "Military" },
      { 0x5f, "Miniseries" },
      { 0x60, "Music" },
      { 0x61, "Mystery" },
      { 0x62, "National" },
      { 0x63, "Nature" },
      { 0x64, "Police" },
      { 0x65, "Politics" },
      { 0x66, "Premier" },
      { 0x67, "Prerecorded" },
      { 0x68, "Product" },
      { 0x69, "Professional" },
      { 0x6a, "Public" },
      { 0x6b, "Racing" },
      { 0x6c, "Reading" },
      { 0x6d, "Repair" },
      { 0x6e, "Repeat" },
      { 0x6f, "Review" },
      { 0x70, "Romance" },
      { 0x71, "Science" },
      { 0x72, "Series" },
      { 0x73, "Service" },
      { 0x74, "Shopping" },
      { 0x75, "Soap Opera" },
      { 0x76, "Special" },
      { 0x77, "Suspense" },
      { 0x78, "Talk" },
      { 0x79, "Technical" },
      { 0x7a, "Tennis" },
      { 0x7b, "Travel" },
      { 0x7c, "Variety" },
      { 0x7d, "Video" },
      { 0x7e, "Weather" },
      { 0x7f, "Western" },
      { 0x80, "Art" },
      { 0x81, "Auto Racing" },
      { 0x82, "Aviation" },
      { 0x83, "Biography" },
      { 0x84, "Boating" },
      { 0x85, "Bowling" },
      { 0x86, "Boxing" },
      { 0x87, "Cartoon" },
      { 0x88, "Children" },
      { 0x89, "Classic Film" },
      { 0x8a, "Community" },
      { 0x8b, "Computers" },
      { 0x8c, "Country Music" },
      { 0x8d, "Court" },
      { 0x8e, "Extreme Sports" },
      { 0x8f, "Family" },
      { 0x90, "Financial" },
      { 0x91, "Gymnastics" },
      { 0x92, "Headlines" },
      { 0x93, "Horse Racing" },
      { 0x94, "Hunting/Fishing/Outdoors" },
      { 0x95, "Independent" },
      { 0x96, "Jazz" },
      { 0x97, "Magazine" },
      { 0x98, "Motorcycle Racing" },
      { 0x99, "Music/Film/Books" },
      { 0x9a, "News-International" },
      { 0x9b, "News-Local" },
      { 0x9c, "News-National" },
      { 0x9d, "News-Regional" },
      { 0x9e, "Olympics" },
      { 0x9f, "Original" },
      { 0xa0, "Performing Arts" },
      { 0xa1, "Pets/Animals" },
      { 0xa2, "Pop" },
      { 0xa3, "Rock & Roll" },
      { 0xa4, "Sci-Fi" },
      { 0xa5, "Self Improvement" },
      { 0xa6, "Sitcom" },
      { 0xa7, "Skating" },
      { 0xa8, "Skiing" },
      { 0xa9, "Soccer" },
      { 0xaa, "Track/Field" },
      { 0xab, "True" },
      { 0xac, "Volleyball" },
      { 0xad, "Wrestling" }
    };

    #endregion

    #region variables

    #region ATSC

    /// <summary>
    /// The ATSC EPG grabber.
    /// </summary>
    private IGrabberEpgAtsc _grabberAtsc = null;

    /// <summary>
    /// Indicator: has the grabber seen ATSC EPG data?
    /// </summary>
    private bool _isSeenAtsc = false;

    /// <summary>
    /// Indicator: has the grabber received all ATSC EPG data?
    /// </summary>
    private bool _isCompleteAtsc = false;

    #endregion

    #region SCTE

    /// <summary>
    /// The SCTE EPG grabber.
    /// </summary>
    private IGrabberEpgScte _grabberScte = null;

    /// <summary>
    /// Indicator: has the grabber seen SCTE EPG data?
    /// </summary>
    private bool _isSeenScte = false;

    /// <summary>
    /// Indicator: has the grabber received all SCTE EPG data?
    /// </summary>
    private bool _isCompleteScte = false;

    #endregion

    #endregion

    /// <summary>
    /// Initialise a new instance of the <see cref="EpgGrabberAtsc"/> class.
    /// </summary>
    /// <param name="controller">The controller for a tuner's EPG grabber.</param>
    /// <param name="grabberAtsc">The ATSC EPG grabber, if available/supported.</param>
    /// <param name="grabberScte">The SCTE EPG grabber, if available/supported.</param>
    public EpgGrabberAtsc(IEpgGrabberController controller, IGrabberEpgAtsc grabberAtsc, IGrabberEpgScte grabberScte)
      : base(controller)
    {
      _grabberAtsc = grabberAtsc;
      if (_grabberAtsc != null)
      {
        _grabberAtsc.SetCallBack(this);
      }

      _grabberScte = grabberScte;
      if (_grabberScte != null)
      {
        _grabberScte.SetCallBack(this);
      }
    }

    #region data collection

    private IList<Tuple<IChannel, IList<EpgProgram>>> CollectData(IChannel currentTuningDetail, IGrabberEpgAtsc grabber)
    {
      uint eventCount = grabber.GetEventCount();
      this.LogDebug("EPG ATSC: initial event count = {0}", eventCount);
      IDictionary<ushort, List<EpgProgram>> tempData = new Dictionary<ushort, List<EpgProgram>>(100);

      const byte ARRAY_SIZE_AUDIO_LANGUAGES = 20;
      const byte ARRAY_SIZE_CAPTIONS_LANGUAGES = 20;
      const byte ARRAY_SIZE_GENRE_IDS = 20;
      const ushort BUFFER_SIZE_TITLE = 300;
      IntPtr bufferTitle = Marshal.AllocCoTaskMem(BUFFER_SIZE_TITLE);
      const ushort BUFFER_SIZE_DESCRIPTION = 1000;
      IntPtr bufferDescription = Marshal.AllocCoTaskMem(BUFFER_SIZE_DESCRIPTION);
      try
      {
        ushort sourceId;
        ushort eventId;
        ulong startDateTimeEpoch;
        uint duration;
        byte textCount;
        Iso639Code[] audioLanguages = new Iso639Code[ARRAY_SIZE_AUDIO_LANGUAGES];
        Iso639Code[] captionsLanguages = new Iso639Code[ARRAY_SIZE_CAPTIONS_LANGUAGES];
        byte[] genreIds = new byte[ARRAY_SIZE_GENRE_IDS];
        byte vchipRating;
        byte mpaaClassification;
        ushort advisories;
        Iso639Code language;
        for (uint i = 0; i < eventCount; i++)
        {
          byte countAudioLanguages = ARRAY_SIZE_AUDIO_LANGUAGES;
          byte countCaptionsLanguages = ARRAY_SIZE_CAPTIONS_LANGUAGES;
          byte countGenreIds = ARRAY_SIZE_GENRE_IDS;
          bool result = grabber.GetEvent(i,
                                          out sourceId,
                                          out eventId,
                                          out startDateTimeEpoch,
                                          out duration,
                                          out textCount,
                                          audioLanguages,
                                          ref countAudioLanguages,
                                          captionsLanguages,
                                          ref countCaptionsLanguages,
                                          genreIds,
                                          ref countGenreIds,
                                          out vchipRating,
                                          out mpaaClassification,
                                          out advisories);
          if (!result)
          {
            this.LogWarn("EPG ATSC: failed to get event, event index = {0}, event count = {1}", i, eventCount);
            continue;
          }

          DateTime programStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
          programStartTime = programStartTime.AddSeconds(startDateTimeEpoch);
          EpgProgram program = new EpgProgram(programStartTime, programStartTime.AddSeconds(duration));

          bool isPlaceholderOrDummyEvent = false;
          for (byte j = 0; j < textCount; j++)
          {
            ushort bufferSizeTitle = BUFFER_SIZE_TITLE;
            ushort bufferSizeDescription = BUFFER_SIZE_DESCRIPTION;
            result = grabber.GetEventTextByIndex(i, j,
                                                  out language,
                                                  bufferTitle,
                                                  ref bufferSizeTitle,
                                                  bufferDescription,
                                                  ref bufferSizeDescription);
            if (!result)
            {
              this.LogWarn("EPG ATSC: failed to get event text, event index = {0}, event count = {1}, text index = {2}, text count = {3}", i, eventCount, j, textCount);
              continue;
            }

            string title = TidyString(DvbTextConverter.Convert(bufferTitle, bufferSizeTitle));
            if (string.IsNullOrEmpty(title))
            {
              isPlaceholderOrDummyEvent = true;
              continue;
            }
            program.Titles.Add(language.Code, title);

            string description = TidyString(DvbTextConverter.Convert(bufferDescription, bufferSizeDescription));
            if (!string.IsNullOrEmpty(description))
            {
              program.Descriptions.Add(language.Code, description);
            }
          }

          if (isPlaceholderOrDummyEvent)
          {
            continue;
          }

          for (byte x = 0; x < countAudioLanguages; x++)
          {
            program.AudioLanguages.Add(audioLanguages[x].Code);
          }
          for (byte x = 0; x < countCaptionsLanguages; x++)
          {
            program.SubtitlesLanguages.Add(captionsLanguages[x].Code);
          }
          for (byte x = 0; x < countGenreIds; x++)
          {
            string genreDescription;
            if (MAPPING_ATSC_GENRES.TryGetValue(genreIds[x], out genreDescription))
            {
              program.Categories.Add(genreDescription);
            }
          }
          string mpaaClassificationDescription;
          if (MAPPING_PROGRAM_CLASSIFICATIONS_MPAA.TryGetValue(mpaaClassification, out mpaaClassificationDescription))
          {
            program.Classifications.Add("MPAA", mpaaClassificationDescription);
          }
          program.Advisories = GetContentAdvisories(advisories);
          string vchipRatingDescription;
          if (MAPPING_PROGRAM_RATINGS_VCHIP.TryGetValue(vchipRating, out vchipRatingDescription))
          {
            program.Classifications.Add("V-Chip", vchipRatingDescription);
          }

          List<EpgProgram> programs;
          if (!tempData.TryGetValue(sourceId, out programs))
          {
            programs = new List<EpgProgram>(100);
            tempData.Add(sourceId, programs);
          }
          programs.Add(program);
        }

        IList<Tuple<IChannel, IList<EpgProgram>>> data = new List<Tuple<IChannel, IList<EpgProgram>>>(tempData.Count);
        int validEventCount = 0;
        foreach (var channelData in tempData)
        {
          IChannel atscScteChannel = currentTuningDetail.Clone() as IChannel;
          ChannelAtsc atscChannel = atscScteChannel as ChannelAtsc;
          if (atscChannel != null)
          {
            atscChannel.SourceId = channelData.Key;
          }
          else
          {
            ChannelScte scteChannel = atscScteChannel as ChannelScte;
            if (scteChannel != null)
            {
              scteChannel.SourceId = channelData.Key;
            }
            else
            {
              this.LogWarn("EPG ATSC: the tuned channel is not an ATSC or SCTE channel");
              continue;
            }
          }
          channelData.Value.Sort();
          data.Add(new Tuple<IChannel, IList<EpgProgram>>(atscScteChannel, channelData.Value));
          validEventCount += channelData.Value.Count;
        }

        this.LogDebug("EPG ATSC: channel count = {0}, event count = {1}", tempData.Count, validEventCount);
        return data;
      }
      finally
      {
        if (bufferTitle != IntPtr.Zero)
        {
          Marshal.FreeCoTaskMem(bufferTitle);
        }
        if (bufferDescription != IntPtr.Zero)
        {
          Marshal.FreeCoTaskMem(bufferDescription);
        }
      }
    }

    private static string GetAtscGenreDescription(byte genreId)
    {
      string description;
      if (!MAPPING_ATSC_GENRES.TryGetValue(genreId, out description))
      {
        description = string.Format("User Defined {0}", genreId);
      }
      return description;
    }

    private static ContentAdvisory GetContentAdvisories(ushort advisories)
    {
      ContentAdvisory advisoryFlags = ContentAdvisory.None;
      if ((advisories & 0x01) != 0)
      {
        advisoryFlags |= ContentAdvisory.SexualSituations;
      }
      if ((advisories & 0x02) != 0)
      {
        advisoryFlags |= ContentAdvisory.CourseOrCrudeLanguage;
      }
      if ((advisories & 0x04) != 0)
      {
        advisoryFlags |= ContentAdvisory.MildSensuality;
      }
      if ((advisories & 0x08) != 0)
      {
        advisoryFlags |= ContentAdvisory.FantasyViolence;
      }
      if ((advisories & 0x10) != 0)
      {
        advisoryFlags |= ContentAdvisory.Violence;
      }
      if ((advisories & 0x20) != 0)
      {
        advisoryFlags |= ContentAdvisory.MildPeril;
      }
      if ((advisories & 0x40) != 0)
      {
        advisoryFlags |= ContentAdvisory.Nudity;
      }
      if ((advisories & 0x8000) != 0)
      {
        advisoryFlags |= ContentAdvisory.SuggestiveDialogue;
      }
      return advisoryFlags;
    }

    #endregion

    #region EpgGrabberBase overrides

    protected override void OnStart(TunerEpgGrabberProtocol? newProtocols = null)
    {
      if (_grabberAtsc != null)
      {
        if (!newProtocols.HasValue && _protocols.HasFlag(TunerEpgGrabberProtocol.AtscEit))
        {
          this.LogDebug("EPG ATSC: starting ATSC grabber");
          _grabberAtsc.Start();
        }
        else if (newProtocols.HasValue && (newProtocols.Value & TunerEpgGrabberProtocol.AtscEit) != (_protocols & TunerEpgGrabberProtocol.AtscEit))
        {
          this.LogDebug("EPG ATSC: ATSC protocol configuration changed");
          if (newProtocols.Value.HasFlag(TunerEpgGrabberProtocol.AtscEit))
          {
            _grabberAtsc.Start();
          }
          else
          {
            _grabberAtsc.Stop();
          }
        }
      }

      if (_grabberScte != null)
      {
        if (!newProtocols.HasValue && _protocols.HasFlag(TunerEpgGrabberProtocol.ScteAeit))
        {
          this.LogDebug("EPG ATSC: starting SCTE grabber");
          _grabberScte.Start();
        }
        else if (newProtocols.HasValue && (newProtocols.Value & TunerEpgGrabberProtocol.ScteAeit) != (_protocols & TunerEpgGrabberProtocol.ScteAeit))
        {
          this.LogDebug("EPG ATSC: SCTE protocol configuration changed");
          if (newProtocols.Value.HasFlag(TunerEpgGrabberProtocol.ScteAeit))
          {
            _grabberScte.Start();
          }
          else
          {
            _grabberScte.Stop();
          }
        }
      }
    }

    protected override void OnStop()
    {
      if (_grabberAtsc != null && _protocols.HasFlag(TunerEpgGrabberProtocol.AtscEit))
      {
        this.LogDebug("EPG ATSC: stopping ATSC grabber");
        _grabberAtsc.Stop();
      }
      if (_grabberScte != null && _protocols.HasFlag(TunerEpgGrabberProtocol.ScteAeit))
      {
        this.LogDebug("EPG ATSC: stopping SCTE grabber");
        _grabberScte.Stop();
      }
    }

    #endregion

    #region ICallBackGrabber members

    /// <summary>
    /// This function is invoked when the first section from a table is received.
    /// </summary>
    /// <param name="pid">The PID that the table section was recevied from.</param>
    /// <param name="tableId">The identifier of the table that was received.</param>
    public void OnTableSeen(ushort pid, byte tableId)
    {
      this.LogDebug("EPG ATSC: on table seen, PID = {0}, table ID = 0x{1:x}", pid, tableId);
      if (pid == 0xcb)
      {
        _isSeenAtsc = true;
        _isCompleteAtsc = false;
      }
      else if (pid == 0xd6)
      {
        _isSeenScte = true;
        _isCompleteScte = false;
      }
    }

    /// <summary>
    /// This function is invoked after the last section from a table is received.
    /// </summary>
    /// <param name="pid">The PID that the table section was recevied from.</param>
    /// <param name="tableId">The identifier of the table that was completed.</param>
    public void OnTableComplete(ushort pid, byte tableId)
    {
      this.LogDebug("EPG ATSC: on table complete, PID = {0}, table ID = 0x{1:x}", pid, tableId);
      if (pid == 0xcb)
      {
        _isCompleteAtsc = true;
      }
      else if (pid == 0xd6)
      {
        _isCompleteScte = true;
      }

      if (
        (_isCompleteAtsc || _isCompleteScte) &&
        (!_isSeenAtsc || _isCompleteAtsc) &&
        (!_isSeenScte || _isCompleteScte)
      )
      {
        this.LogDebug("EPG ATSC: EPG complete");

        // Use a thread to notify about data readiness. Expect that data may be
        // collected in the call-back thread. If we collect from this thread it
        // can cause stuttering and deadlocks.
        Thread collector = new Thread(_callBack.OnEpgDataReady);
        collector.IsBackground = true;
        collector.Priority = ThreadPriority.Lowest;
        collector.Name = "EPG collector";
        collector.Start();
      }
    }

    /// <summary>
    /// This function is invoked after any section from a table changes.
    /// </summary>
    /// <param name="pid">The PID that the table section was recevied from.</param>
    /// <param name="tableId">The identifier of the table that changed.</param>
    public void OnTableChange(ushort pid, byte tableId)
    {
      OnTableSeen(pid, tableId);
    }

    /// <summary>
    /// This function is invoked after the grabber is reset.
    /// </summary>
    /// <param name="pid">The PID that is associated with the grabber.</param>
    public void OnReset(ushort pid)
    {
      this.LogDebug("EPG ATSC: on reset, PID = {0}", pid);
      if (pid == 0xcb)
      {
        _isSeenAtsc = false;
        _isCompleteAtsc = false;
      }
      else if (pid == 0xd6)
      {
        _isSeenScte = false;
        _isCompleteScte = false;
      }
    }

    #endregion

    #region IEpgGrabber members

    /// <summary>
    /// Get the EPG data protocols supported by the grabber code/class/type implementation.
    /// </summary>
    public override TunerEpgGrabberProtocol PossibleProtocols
    {
      get
      {
        TunerEpgGrabberProtocol protocols = TunerEpgGrabberProtocol.None;
        if (_grabberAtsc != null)
        {
          protocols |= TunerEpgGrabberProtocol.AtscEit;
        }
        if (_grabberScte != null)
        {
          protocols |= TunerEpgGrabberProtocol.ScteAeit;
        }
        return protocols;
      }
    }

    /// <summary>
    /// Get all available EPG data.
    /// </summary>
    /// <returns>the data, grouped by channel</returns>
    public override IList<Tuple<IChannel, IList<EpgProgram>>> GetData()
    {
      this.LogInfo("EPG ATSC: get data, ATSC = {0} / {1}, SCTE = {2} / {3}",
                    _isSeenAtsc, _isCompleteAtsc, _isSeenScte, _isCompleteScte);
      List<Tuple<IChannel, IList<EpgProgram>>> data = new List<Tuple<IChannel, IList<EpgProgram>>>();
      try
      {
        if (_isSeenAtsc && _grabberAtsc != null)
        {
          data.AddRange(CollectData(_tuningDetail, _grabberAtsc));
        }

        if (_isSeenScte && _grabberScte != null)
        {
          data.AddRange(CollectData(_tuningDetail, _grabberScte));
        }
      }
      catch (Exception ex)
      {
        this.LogError(ex, "EPG ATSC: failed to collect data");
      }
      return data;
    }

    #endregion
  }
}