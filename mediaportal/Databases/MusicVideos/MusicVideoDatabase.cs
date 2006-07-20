#region Copyright (C) 2006 Team MediaPortal

/* 
 *	Copyright (C) 2005-2006 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#endregion

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using MediaPortal.GUI.Library;
using MediaPortal.Database;
using SQLite.NET;
using System.Xml;
using System.IO;
using MediaPortal.MusicVideos;

namespace MediaPortal.MusicVideos.Database
{
  public class MusicVideoDatabase
  {
    public enum MusicVideoTypes : int
    {
      TOP100, NEW, GENRE, FAVORITE,
    };
    private SQLiteClient m_db;

    private static MusicVideoDatabase Instance;

    private MusicVideoDatabase()
    {
      bool dbExists;
      try
      {
        // Open database
        try
        {
          System.IO.Directory.CreateDirectory("database");
        }
        catch (Exception) { }
        dbExists = System.IO.File.Exists(@"database\MusicVideoDatabaseV1.db3");
        m_db = new SQLiteClient(@"database\MusicVideoDatabaseV1.db3");

        MediaPortal.Database.DatabaseUtility.SetPragmas(m_db);
        if (!dbExists)
        {
          CreateTables();
        }
      }
      catch (SQLiteException ex)
      {
        Console.Write("Recipedatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
      }
    }

    public static MusicVideoDatabase getInstance()
    {
      if (Instance == null)
      {
        Instance = new MusicVideoDatabase();
      }
      return Instance;
    }

    private void CreateTables()
    {
      if (m_db == null)
      {
        return;
      }
      try
      {
        m_db.Execute("CREATE TABLE FAVORITE_VIDEOS(SONG_NM text,SONG_ID text,ARTIST_NM text,ARTIST_ID text,COUNTRY text,FAVORITE_ID integer)\n");
        m_db.Execute("CREATE TABLE FAVORITE(FAVORITE_ID integer primary key,FAVORITE_NM text)\n");
        //m_db.Execute("CREATE TABLE MUSIC_VDO_CAT(MUSIC_VDO_CAT_ID integer primary key,MUSIC_VDO_CAT_NM text,CTRY_NM text,MUSIC_VDO_TYP_ID integer)\n");
        //m_db.Execute("CREATE TABLE MUSIC_VDO_TYP(MUSIC_VDO_TYP_ID integer primary key,MUSIC_VDO_TYP_NM text)\n");
        //m_db.Execute("CREATE TABLE MUSIC_VDO(MUSIC_VDO_ID integer primary key,MUSIC_VDO_SONG_NM text,MUSIC_VDO_SONG_ID text,MUSIC_VDO_ARTIST_NM text,MUSIC_VDO_ARTIST_ID text,MUSIC_VDO_CAT_ID integer)\n");
        //addVideoType("TOP100");
        //addVideoType("NEW");
        //addVideoType("GENRE");
        //addVideoType("FAVORITE");
        //createVideoCat("Default", MusicVideoTypes.FAVORITE, "");
        List<YahooVideo> loFavoriteVideos = parseOldFavoriteFile();
        createFavorite("Default");
        if (loFavoriteVideos.Count > 0)
        {

          foreach (YahooVideo loVideo in loFavoriteVideos)
          {
            addFavoriteVideo("Default", loVideo);
          }
        }
      }
      catch (Exception e)
      {
        Log.Write(e);
      }
    }

    public bool createFavorite(string fsName)
    {
      string lsSQL = String.Format("insert into FAVORITE(FAVORITE_NM) VALUES('{0}')", fsName.Replace("'", "''"));
      m_db.Execute(lsSQL);
      if (m_db.ChangedRows() > 0)
      {
        return true;
      }
      else
      {
        return false;
      }
    }

    public bool DeleteFavorite(string fsName)
    {
      string lsSQL = String.Format(" delete from FAVORITE where FAVORITE_NM='{0}'", fsName.Replace("'", "''"));
      m_db.Execute(lsSQL);
      if (m_db.ChangedRows() > 0)
      {
        return true;
      }
      else
      {
        return false;
      }

    }

    public bool updateFavorite(string fsOldName, String fsNewName)
    {
      string lsSQL = String.Format("update FAVORITE set FAVORITE_NM = '{0}' where FAVORITE_NM= '{1}'", fsNewName.Replace("'", "''"), fsOldName.Replace("'", "''"));
      m_db.Execute(lsSQL);
      if (m_db.ChangedRows() > 0)
      {
        return true;
      }
      else
      {
        return false;
      }
    }

    public bool addFavoriteVideo(string fsFavoriteNm, YahooVideo foVideo)
    {
      //get the favorite id
      string lsSQL = String.Format("select FAVORITE_ID from FAVORITE where FAVORITE_NM='{0}'", fsFavoriteNm.Replace("'", "''"));
      SQLiteResultSet loResultSet = m_db.Execute(lsSQL);

      string lsFavID = (String)loResultSet.GetColumn(0)[0];

      lsSQL = string.Format("insert into FAVORITE_VIDEOS(SONG_NM,SONG_ID,ARTIST_NM,ARTIST_ID,COUNTRY,FAVORITE_ID)VALUES('{0}','{1}','{2}','{3}','{4}','{5}')", foVideo._yahooVideoSongName.Replace("'", "''"), foVideo._yahooVideoSongId, foVideo._yahooVideoArtistName.Replace("'", "''"), foVideo._yahooVideoArtistId, foVideo._yahooVideoCountryId, lsFavID);
      m_db.Execute(lsSQL);
      if (m_db.ChangedRows() > 0)
      {
        return true;
      }
      else
      {
        return false;
      }

    }

    public bool removeFavoriteVideo(YahooVideo foVideo, string fsFavoriteNm)
    {
      string lsSQL = String.Format("select FAVORITE_ID from FAVORITE where FAVORITE_NM='{0}'", fsFavoriteNm.Replace("'", "''"));
      SQLiteResultSet loResultSet = m_db.Execute(lsSQL);

      string lsFavID = (String)loResultSet.GetColumn(0)[0];
      //Log.Write("fav id = {0}",lsFavID);
      //Log.Write("song id = {0}", foVideo._yahooVideoSongId);
      lsSQL = string.Format("delete from FAVORITE_VIDEOS where SONG_ID='{0}' and FAVORITE_ID = {1}", foVideo._yahooVideoSongId, lsFavID);
      m_db.Execute(lsSQL);
      if (m_db.ChangedRows() > 0)
      {
        return true;
      }
      else
      {
        return false;
      }

    }

    public ArrayList getFavorites()
    {
      //createFavorite("Default2");
      string lsSQL = string.Format("select favorite_nm from favorite");
      SQLiteResultSet loResultSet = m_db.Execute(lsSQL);
      return loResultSet.GetColumn(0);
    }

    public List<YahooVideo> getFavoriteVideos(string fsFavoriteNm)
    {
      List<YahooVideo> loFavoriteList = new List<YahooVideo>();
      string lsSQL = String.Format("select FAVORITE_ID from FAVORITE where FAVORITE_NM='{0}'", fsFavoriteNm.Replace("'", "''"));
      SQLiteResultSet loResultSet = m_db.Execute(lsSQL);

      string lsFavID = (String)loResultSet.GetColumn(0)[0];
      lsSQL = string.Format("select SONG_NM,SONG_ID,ARTIST_NM,ARTIST_ID,COUNTRY from FAVORITE_VIDEOS where FAVORITE_ID={0}", lsFavID);
      loResultSet = m_db.Execute(lsSQL);

      foreach (ArrayList loRow in loResultSet.RowsList)
      {
        YahooVideo loVideo = new YahooVideo();
        IEnumerator en = loRow.GetEnumerator();
        en.MoveNext();
        loVideo._yahooVideoSongName = (String)en.Current;
        en.MoveNext();
        loVideo._yahooVideoSongId = (String)en.Current;
        en.MoveNext();
        loVideo._yahooVideoArtistName = (String)en.Current;
        en.MoveNext();
        loVideo._yahooVideoArtistId = (String)en.Current;
        en.MoveNext();
        loVideo._yahooVideoCountryId = (String)en.Current;
        loFavoriteList.Add(loVideo);

      }

      return loFavoriteList;
    }

    private List<YahooVideo> parseOldFavoriteFile()
    {
      XmlTextReader loXmlreader = null;
      List<YahooVideo> loFavoriteList = new List<YahooVideo>();
      //moFavoriteTable = new Dictionary<string,List<YahooVideo>>();
      //string lsCurrentName = msDefaultFavoriteName;
      try
      {
        loXmlreader = new XmlTextReader("MusicVideoFavorites.xml");
        YahooVideo loVideo;

        while (loXmlreader.Read())
        {
          if (loXmlreader.NodeType == XmlNodeType.Element && loXmlreader.Name == "SONG")
          {
            loVideo = new YahooVideo();
            loVideo._yahooVideoArtistId = loXmlreader.GetAttribute("ArtistId");
            loVideo._yahooVideoArtistName = loXmlreader.GetAttribute("Artist");
            loVideo._yahooVideoSongId = loXmlreader.GetAttribute("SongId");
            loVideo._yahooVideoSongName = loXmlreader.GetAttribute("SongTitle");
            loVideo._yahooVideoCountryId = loXmlreader.GetAttribute("CtryId");
            Log.Write("found favorite:{0}", loVideo.ToString());
            loFavoriteList.Add(loVideo);
          }
        }
        loXmlreader.Close();
      }
      catch (Exception e) { Log.Write(e); }
      finally
      {
        loXmlreader.Close();
        Log.Write("old parse closed.");
      }
      return loFavoriteList;
    }
  }
}
