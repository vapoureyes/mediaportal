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

using System.Collections.ObjectModel;

namespace Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces
{
  /// <summary>
  /// This interface links related tuner instances.
  /// </summary>
  public interface ITunerGroup
  {
    /// <summary>
    /// Get the tuner group's identifier.
    /// </summary>
    int TunerGroupId
    {
      get;
    }

    /// <summary>
    /// Get the tuner group's name.
    /// </summary>
    string Name
    {
      get;
    }

    /// <summary>
    /// Get the tuner group's product instance identifier.
    /// </summary>
    /// <remarks>
    /// The product instance identifier relates to a single instance of a tuner
    /// product. A product may have one or more physical tuners, and a computer
    /// may contain one or more instances of a product.
    /// </remarks>
    string ProductInstanceId
    {
      get;
    }

    /// <summary>
    /// Get the tuner group's tuner instance identifier.
    /// </summary>
    /// <remarks>
    /// The tuner instance identifier relates to a single physical tuner. A
    /// product may have one or more physical tuners.
    /// </remarks>
    string TunerInstanceId
    {
      get;
    }

    /// <summary>
    /// Get the tuner group's members.
    /// </summary>
    ReadOnlyCollection<ITVCard> Tuners
    {
      get;
    }
  }
}