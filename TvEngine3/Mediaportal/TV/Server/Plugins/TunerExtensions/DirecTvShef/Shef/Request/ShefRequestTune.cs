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
using System.Text;
using Mediaportal.TV.Server.Plugins.TunerExtension.DirecTvShef.Shef.Response;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.DirecTvShef.Shef.Request
{
  internal class ShefRequestTune : IShefRequest
  {
    public const int MINOR_CHANNEL_NUMBER_NOT_SET = 65535;

    private int _majorChannelNumber = 1;                              // 1..9999
    private int _minorChannelNumber = MINOR_CHANNEL_NUMBER_NOT_SET;   // 0..999
    private string _clientAddress = null;

    public ShefRequestTune(int majorChannelNumber, int minorChannelNumber = MINOR_CHANNEL_NUMBER_NOT_SET, string clientAddress = null)
    {
      _majorChannelNumber = majorChannelNumber;
      _minorChannelNumber = minorChannelNumber;
      _clientAddress = clientAddress;
    }

    public string GetQueryUri()
    {
      StringBuilder uri = new StringBuilder(string.Format("tv/tune?major={0}", _majorChannelNumber));
      if (_minorChannelNumber != MINOR_CHANNEL_NUMBER_NOT_SET)
      {
        uri.AppendFormat("&minor={0}", _minorChannelNumber);
      }
      if (!string.IsNullOrEmpty(_clientAddress))
      {
        uri.AppendFormat("&clientAddr={0}", _clientAddress);
      }
      return uri.ToString();
    }

    public Type GetResponseType()
    {
      return typeof(ShefResponseTune);
    }
  }
}