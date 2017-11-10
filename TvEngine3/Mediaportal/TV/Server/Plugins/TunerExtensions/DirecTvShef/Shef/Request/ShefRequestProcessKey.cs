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
  internal class ShefRequestProcessKey : IShefRequest
  {
    private ShefRemoteKey _key = null;
    private ShefRemoteKeyPress _hold = null;
    private string _clientAddress = null;

    public ShefRequestProcessKey(ShefRemoteKey key, ShefRemoteKeyPress hold = null, string clientAddress = null)
    {
      _key = key;
      if (hold == null)
      {
        _hold = ShefRemoteKeyPress.Press;
      }
      else
      {
        _hold = hold;
      }
      _clientAddress = clientAddress;
    }

    public string GetQueryUri()
    {
      StringBuilder uri = new StringBuilder(string.Format("remote/processKey?key={0}&hold={1}", _key.ToString(), _hold.ToString()));
      if (!string.IsNullOrEmpty(_clientAddress))
      {
        uri.AppendFormat("&clientAddr={0}", _clientAddress);
      }
      return uri.ToString();
    }

    public Type GetResponseType()
    {
      return typeof(ShefResponseProcessKey);
    }
  }
}