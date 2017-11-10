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
using Mediaportal.TV.Server.TVLibrary.Interfaces.Analyzer;

namespace Mediaportal.TV.Server.TVLibrary.Implementations.Scte
{
  /// <summary>
  /// This implementation of <see cref="IGrabberEpgScte"/> only exists because
  /// attempting to cast the interface implementation exposed by TsWriter
  /// (CParserAet) to <see cref="IGrabberEpgAtsc"/> causes TsWriter/COM to
  /// switch to a different implementation (CGrabberEpgAtsc).
  /// </summary>
  internal class GrabberEpgScteWrapper : IGrabberEpgScte
  {
    private IGrabberEpgScte _grabber = null;

    public GrabberEpgScteWrapper(IGrabberEpgScte grabber)
    {
      _grabber = grabber;
    }

    /// <summary>
    /// Set the grabber's call back delegate.
    /// </summary>
    /// <param name="callBack">The delegate.</param>
    public void SetCallBack(ICallBackGrabber callBack)
    {
      _grabber.SetCallBack(callBack);
    }

    /// <summary>
    /// Start the grabber.
    /// </summary>
    public void Start()
    {
      _grabber.Start();
    }

    /// <summary>
    /// Stop the grabber.
    /// </summary>
    public void Stop()
    {
      _grabber.Stop();
    }

    /// <summary>
    /// Check if the grabber has received any sections.
    /// </summary>
    /// <returns><c>true</c> if the grabber has received one or more sections, otherwise <c>false</c></returns>
    public bool IsSeen()
    {
      return _grabber.IsSeen();
    }

    /// <summary>
    /// Check if the grabber has received all available sections.
    /// </summary>
    /// <returns><c>true</c> if the grabber has received all available sections, otherwise <c>false</c></returns>
    public bool IsReady()
    {
      return _grabber.IsReady();
    }

    /// <summary>
    /// Get the number of SCTE events received by the grabber.
    /// </summary>
    /// <returns>the number of SCTE events received by the grabber</returns>
    public uint GetEventCount()
    {
      return _grabber.GetEventCount();
    }

    /// <summary>
    /// Retrieve an SCTE event's details from the grabber.
    /// </summary>
    /// <param name="index">The index of the event to retrieve. Should be in the range 0 to GetEventCount() - 1.</param>
    /// <param name="sourceId">The identifier of the source that the event is associated with.</param>
    /// <param name="eventId">The event's identifier. Only unique when combined with a <paramref name="sourceId">source identifier</paramref>.</param>
    /// <param name="startDateTime">The event's start date/time, encoded as an epoch/Unix/POSIX time-stamp.</param>
    /// <param name="duration">The event's duration in seconds.</param>
    /// <param name="textCount">The number of languages in which the event's text is available.</param>
    /// <param name="audioLanguages">The languages in which the event's audio will be available. The caller must allocate this array.</param>
    /// <param name="audioLanguageCount">As an input, the size of the <paramref name="audioLanguages">audio languages array</paramref>; as an output, the consumed array size.</param>
    /// <param name="captionsLanguages">The languages in which the event's captions will be available. The caller must allocate this array.</param>
    /// <param name="captionsLanguageCount">As an input, the size of the <paramref name="captionsLanguages">captions languages array</paramref>; as an output, the consumed array size.</param>
    /// <param name="genreIds">The identifiers of the genres that the event is associated with. The caller must allocate this array.</param>
    /// <param name="genreIdCount">As an input, the size of the <paramref name="genreIds">genre identifier array</paramref>; as an output, the consumed array size.</param>
    /// <param name="vchipRating">The event's V-CHIP rating. Value is <c>0xff</c> if not available.</param>
    /// <param name="mpaaClassification">The event's MPAA classification. Value is <c>0xff</c> if not available.</param>
    /// <param name="advisories">The event's advisories, encoded as flags.</param>
    /// <returns><c>true</c> if the event's details are successfully retrieved, otherwise <c>false</c></returns>
    public bool GetEvent(uint index,
                          out ushort sourceId,
                          out ushort eventId,
                          out ulong startDateTime,
                          out uint duration,
                          out byte textCount,
                          Iso639Code[] audioLanguages,
                          ref byte audioLanguageCount,
                          Iso639Code[] captionsLanguages,
                          ref byte captionsLanguageCount,
                          byte[] genreIds,
                          ref byte genreIdCount,
                          out byte vchipRating,
                          out byte mpaaClassification,
                          out ushort advisories)
    {
      return _grabber.GetEvent(index,
                                out sourceId,
                                out eventId,
                                out startDateTime,
                                out duration,
                                out textCount,
                                audioLanguages,
                                ref audioLanguageCount,
                                captionsLanguages,
                                ref captionsLanguageCount,
                                genreIds,
                                ref genreIdCount,
                                out vchipRating,
                                out mpaaClassification,
                                out advisories);
    }

    /// <summary>
    /// Retrieve an SCTE event's text from the grabber.
    /// </summary>
    /// <param name="eventIndex">The event's index. Should be in the range 0 to GetEventCount() - 1.</param>
    /// <param name="textIndex">The index of the text to retrieve. Should be in the range 0 to textCount - 1 for the event.</param>
    /// <param name="language">The text's language.</param>
    /// <param name="title">A buffer containing the event's title, encoded as DVB-compatible text. The caller must allocate and free this buffer.</param>
    /// <param name="titleBufferSize">As an input, the size of the <paramref name="title">title buffer</paramref>; as an output, the consumed buffer size.</param>
    /// <param name="text">A buffer containing the event's text (description), encoded as DVB-compatible text. The caller must allocate and free this buffer.</param>
    /// <param name="textBufferSize">As an input, the size of the <paramref name="text"/> buffer; as an output, the consumed buffer size.</param>
    /// <returns><c>true</c> if the event's text is successfully retrieved, otherwise <c>false</c></returns>
    public bool GetEventTextByIndex(uint eventIndex,
                                    byte textIndex,
                                    out Iso639Code language,
                                    IntPtr title,
                                    ref ushort titleBufferSize,
                                    IntPtr text,
                                    ref ushort textBufferSize)
    {
      return _grabber.GetEventTextByIndex(eventIndex,
                                          textIndex,
                                          out language,
                                          title,
                                          ref titleBufferSize,
                                          text,
                                          ref textBufferSize);
    }

    /// <summary>
    /// Retrieve an SCTE event's text from the grabber.
    /// </summary>
    /// <param name="eventIndex">The event's index. Should be in the range 0 to GetEventCount() - 1.</param>
    /// <param name="language">The language of the text to retrieve.</param>
    /// <param name="title">A buffer containing the event's title, encoded as DVB-compatible text. The caller must allocate and free this buffer.</param>
    /// <param name="titleBufferSize">As an input, the size of the <paramref name="title">title buffer</paramref>; as an output, the consumed buffer size.</param>
    /// <param name="text">A buffer containing the event's text (description), encoded as DVB-compatible text. The caller must allocate and free this buffer.</param>
    /// <param name="textBufferSize">As an input, the size of the <paramref name="text"/> buffer; as an output, the consumed buffer size.</param>
    /// <returns><c>true</c> if the event's text is successfully retrieved, otherwise <c>false</c></returns>
    public bool GetEventTextByLanguage(uint eventIndex,
                                        Iso639Code language,
                                        IntPtr title,
                                        ref ushort titleBufferSize,
                                        IntPtr text,
                                        ref ushort textBufferSize)
    {
      return _grabber.GetEventTextByLanguage(eventIndex,
                                              language,
                                              title,
                                              ref titleBufferSize,
                                              text,
                                              ref textBufferSize);
    }
  }
}