#region Copyright (C) 2005-2010 Team MediaPortal

// Copyright (C) 2005-2010 Team MediaPortal
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

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Mediaportal.TV.Server.TVControl.Events
{  
  public static class TvServerEventArgsTranslator
  {
    public static byte[] ToBinary(TvServerEventArgs item)
    {
      BinaryFormatter format = new BinaryFormatter();
      using (MemoryStream str = new MemoryStream())
      {        
        format.Serialize(str, item);
        return str.ToArray();
      }
    }

    public static TvServerEventArgs FromBinary(byte[] data)
    {
      BinaryFormatter format = new BinaryFormatter();
      using (MemoryStream str = new MemoryStream(data))
      {
        str.Position = 0;
        return (TvServerEventArgs)format.Deserialize(str);
      }
    }
  }
}