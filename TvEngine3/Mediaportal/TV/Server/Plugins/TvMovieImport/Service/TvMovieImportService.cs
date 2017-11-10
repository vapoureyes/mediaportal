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
using System.Threading;

namespace Mediaportal.TV.Server.Plugins.TvMovieImport.Service
{
  public class TvMovieImportService : ITvMovieImportService
  {
    private static TvMovieImport _importer = null;

    public static TvMovieImport Importer
    {
      set
      {
        _importer = value;
      }
    }

    public void ImportNow()
    {
      // The function call is a long running process, so execute it in a thread.
      ThreadPool.QueueUserWorkItem(
        delegate
        {
          _importer.ImportData(false, false);
        }
      );
    }

    public void GetImportStatus(out DateTime dateTime, out string status, out string channelCounts, out string programCounts)
    {
      _importer.ReadImportStatus(out dateTime, out status, out channelCounts, out programCounts);
    }

    public IList<string> GetGuideChannelNames()
    {
      // This function can block temporarily if executed while import or
      // scheduled actions are running. We don't execute it in a thread because
      // the results are expected immediately.
      return _importer.ReadChannelsFromTvMovieDatabase();
    }

    public string GetDatabaseFilePath()
    {
      return TvMovieProperty.DatabasePath;
    }
  }
}