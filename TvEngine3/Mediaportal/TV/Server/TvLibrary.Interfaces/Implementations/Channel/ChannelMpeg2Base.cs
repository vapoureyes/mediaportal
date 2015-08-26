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

using System.Runtime.Serialization;

namespace Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channel
{
  /// <summary>
  /// A base class for MPEG 2 transport stream <see cref="T:IChannel"/> implementations.
  /// </summary>
  [DataContract]
  [KnownType(typeof(ChannelAtsc))]
  [KnownType(typeof(ChannelDigiCipher2))]
  [KnownType(typeof(ChannelDvbC))]
  [KnownType(typeof(ChannelDvbC2))]
  [KnownType(typeof(ChannelDvbS))]
  [KnownType(typeof(ChannelDvbS2))]
  [KnownType(typeof(ChannelDvbT))]
  [KnownType(typeof(ChannelDvbT2))]
  [KnownType(typeof(ChannelSatelliteTurboFec))]
  [KnownType(typeof(ChannelScte))]
  [KnownType(typeof(ChannelStream))]
  public abstract class ChannelMpeg2Base : ChannelBase
  {
    #region variables

    [DataMember]
    protected int _transportStreamId = -1;

    [DataMember]
    protected int _programNumber = -1;

    [DataMember]
    protected int _pmtPid = -1;

    #endregion

    #region properties

    /// <summary>
    /// Get/set the channel's MPEG 2 transport stream identifier.
    /// </summary>
    public int TransportStreamId
    {
      get
      {
        return _transportStreamId;
      }
      set
      {
        _transportStreamId = value;
      }
    }

    /// <summary>
    /// Get/set the channel's MPEG 2 program number.
    /// </summary>
    public int ProgramNumber
    {
      get
      {
        return _programNumber;
      }
      set
      {
        _programNumber = value;
      }
    }

    /// <summary>
    /// Get/set the channel's MPEG 2 program map table packet identifier.
    /// </summary>
    public int PmtPid
    {
      get
      {
        return _pmtPid;
      }
      set
      {
        _pmtPid = value;
      }
    }

    #endregion

    #region object overrides

    /// <summary>
    /// Determine whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
    /// </summary>
    /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>.</param>
    /// <returns><c>true</c> if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>, otherwise <c>false</c></returns>
    public override bool Equals(object obj)
    {
      ChannelMpeg2Base channel = obj as ChannelMpeg2Base;
      if (
        channel == null ||
        !base.Equals(obj) ||
        TransportStreamId != channel.TransportStreamId ||
        ProgramNumber != channel.ProgramNumber ||
        PmtPid != channel.PmtPid
      )
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// A hash function for this type.
    /// </summary>
    /// <returns>a hash code for the current <see cref="T:System.Object"/></returns>
    public override int GetHashCode()
    {
      return base.GetHashCode() ^ TransportStreamId.GetHashCode() ^
              ProgramNumber.GetHashCode() ^ PmtPid.GetHashCode();
    }

    /// <summary>
    /// Get a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
    /// </summary>
    /// <returns>a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/></returns>
    public override string ToString()
    {
      return string.Format("{0}, TSID = {1}, program number = {2}, PMT PID = {3}",
                            base.ToString(), TransportStreamId, ProgramNumber,
                            PmtPid);
    }

    #endregion
  }
}