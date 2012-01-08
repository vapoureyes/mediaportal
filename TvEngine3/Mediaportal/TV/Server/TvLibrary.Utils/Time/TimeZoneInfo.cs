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

namespace Mediaportal.TV.Server.TvLibrary.Utils.Time
{
  /// <summary>
  /// A World Time Zone
  /// </summary>
  public struct TimeZoneInfo
  {
    //public int Index;
    public string Display;
    public string StdName;
    public string DltName;

    public Int32 Offset;
    public Int32 StdOffset;
    public Int32 DltOffset;

    public TimeZoneDate StdDate;
    public TimeZoneDate DltDate;
  }
}