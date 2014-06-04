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

using System.Collections.Generic;

namespace Mediaportal.TV.Server.TVLibrary.Implementations.Rtsp
{
  /// <summary>
  /// Standard RTSP request methods.
  /// </summary>
  internal sealed class RtspMethod
  {
    private readonly string _name;
    private static readonly IDictionary<string, RtspMethod> _values = new Dictionary<string, RtspMethod>();

    public static readonly RtspMethod Describe = new RtspMethod("DESCRIBE");
    public static readonly RtspMethod Announce = new RtspMethod("ANNOUNCE");
    public static readonly RtspMethod GetParameter = new RtspMethod("GET_PARAMETER");
    public static readonly RtspMethod Options = new RtspMethod("OPTIONS");
    public static readonly RtspMethod Pause = new RtspMethod("PAUSE");
    public static readonly RtspMethod Play = new RtspMethod("PLAY");
    public static readonly RtspMethod Record = new RtspMethod("RECORD");
    public static readonly RtspMethod Redirect = new RtspMethod("REDIRECT");
    public static readonly RtspMethod Setup = new RtspMethod("SETUP");
    public static readonly RtspMethod SetParameter = new RtspMethod("SET_PARAMETER");
    public static readonly RtspMethod Teardown = new RtspMethod("TEARDOWN");

    private RtspMethod(string name)
    {
      _name = name;
      _values.Add(name, this);
    }

    public override string ToString()
    {
      return _name;
    }

    public override bool Equals(object obj)
    {
      RtspMethod method = obj as RtspMethod;
      if (method != null && this == method)
      {
        return true;
      }
      return false;
    }

    public override int GetHashCode()
    {
      return _name.GetHashCode();
    }

    public static ICollection<RtspMethod> Values
    {
      get
      {
        return _values.Values;
      }
    }

    public static explicit operator RtspMethod(string name)
    {
      RtspMethod value = null;
      if (!_values.TryGetValue(name, out value))
      {
        return null;
      }
      return value;
    }

    public static implicit operator string(RtspMethod method)
    {
      return method._name;
    }
  }
}