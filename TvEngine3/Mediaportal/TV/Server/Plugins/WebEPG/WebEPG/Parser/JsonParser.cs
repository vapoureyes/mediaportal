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
using System.Collections.Generic;
using Mediaportal.TV.Server.TvLibrary.Utils.Web.http;
using Mediaportal.TV.Server.TvLibrary.Utils.Web.Parser;
using WebEPG.config.Grabber;

namespace WebEPG.Parser
{
  /// <summary>
  /// Parser for EPG in JSON format
  /// </summary>
  public class JsonParser : IParser
  {
    #region Variables

    private JsonParserTemplate _data;
    private List<JsonNode> _nodeList;
    private HTTPRequest _page;
    private string _source;
    private string _channelName;
    private Type _dataType;

    #endregion

    #region Constructors/Destructors

    public JsonParser(JsonParserTemplate data)
    {
      _page = null;
      _data = data;
      _dataType = typeof(ProgramData);
    }

    #endregion

    #region Public Methods

    public void SetChannel(string name)
    {
      _channelName = name;
    }

    #endregion

    #region IParser Implementations

    public int ParseUrl(HTTPRequest page)
    {
      int count = 0;

      if (_page != page)
      {
        HTMLPage webPage = new HTMLPage(page);
        _source = webPage.GetPage();
        _page = new HTTPRequest(page);
      }

      JsonNode root = JsonNode.LoadJson(_source);
      try
      {
        _nodeList = root.GetNodes(_data.XPath, _data.ChannelFilter);
      }
      catch (Exception) // ex)
      {
        //Log.Error("WebEPG: JSON failed");
        return count;
      }

      if (_nodeList != null)
      {
        count = _nodeList.Count;
      }

      return count;
    }

    public IParserData GetData(int index)
    {
      IParserData jSONData = (IParserData)Activator.CreateInstance(_dataType);

      JsonNode progNode = _nodeList[index];
      if (progNode != null)
      {
        for (int i = 0; i < _data.Fields.Count; i++)
        {
          JsonField field = _data.Fields[i];
          List<JsonNode> subNode = progNode.GetNodes(field.JsonName,null);
          if (subNode.Count>0)
          jSONData.SetElement(field.FieldName, subNode[0].GetValue(""));
        }
      }

      return jSONData;
    }

    #endregion
  }
}