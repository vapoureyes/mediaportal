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

using System.Runtime.InteropServices;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.SmarDtvUsbCi.Product.Struct
{
  [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
  internal struct VersionInfo
  {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
    public string PluginVersion;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
    public string BdaVersion;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
    public string UsbVersion;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
    public string FirmwareVersion;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
    public string FpgaVersion;
  }
}