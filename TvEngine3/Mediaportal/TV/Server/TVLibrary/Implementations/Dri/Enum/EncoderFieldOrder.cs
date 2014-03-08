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

namespace Mediaportal.TV.Server.TVLibrary.Implementations.Dri.Enum
{
  public sealed class EncoderFieldOrder
  {
    private readonly string _name;
    private static readonly IDictionary<string, EncoderFieldOrder> _values = new Dictionary<string, EncoderFieldOrder>();

    public static readonly EncoderFieldOrder Lower = new EncoderFieldOrder("Lower");
    public static readonly EncoderFieldOrder Higher = new EncoderFieldOrder("Higher");

    private EncoderFieldOrder(string name)
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
      EncoderFieldOrder fieldOrder = obj as EncoderFieldOrder;
      if (fieldOrder != null && this == fieldOrder)
      {
        return true;
      }
      return false;
    }

    public static ICollection<EncoderFieldOrder> Values
    {
      get { return _values.Values; }
    }

    public static explicit operator EncoderFieldOrder(string name)
    {
      EncoderFieldOrder value = null;
      if (!_values.TryGetValue(name, out value))
      {
        return null;
      }
      return value;
    }

    public static implicit operator string(EncoderFieldOrder fieldOrder)
    {
      return fieldOrder._name;
    }
  }
}