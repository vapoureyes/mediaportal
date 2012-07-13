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

using System.Collections;

namespace Mediaportal.TV.Server.TvLibrary.Utils.Web.html
{
  public class MatchTagCollection : CollectionBase
  {
    #region Properties

    public virtual MatchTag this[int Index]
    {
      get { return (MatchTag)this.List[Index]; }
    }

    #endregion

    #region Public Methods

    public virtual void Add(MatchTag value)
    {
      this.List.Add(value);
    }

    #endregion
  }
}