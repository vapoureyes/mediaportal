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
using Gentle.Framework;
using TvLibrary.Log;

namespace TvDatabase
{
  /// <summary>
  /// Instances of this class represent the properties and methods of a row in the table <b>CardGroupMap</b>.
  /// </summary>
  [TableName("CardGroup")]
  public class CardGroup : Persistent
  {
    #region Members

    private bool isChanged;
    [TableColumn("idCardGroup", NotNull = true), PrimaryKey(AutoGenerated = true)] private int idCardGroup;
    [TableColumn("name", NotNull = true)] private string name;

    #endregion

    #region Constructors

    /// <summary> 
    /// Create a new object by specifying all fields (except the auto-generated primary key field). 
    /// </summary> 
    public CardGroup(string name)
    {
      isChanged = true;
      this.name = name;
    }

    /// <summary> 
    /// Create an object from an existing row of data. This will be used by Gentle to 
    /// construct objects from retrieved rows. 
    /// </summary> 
    public CardGroup(int idCardGroup, string name)
    {
      this.idCardGroup = idCardGroup;
      this.name = name;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Indicates whether the entity is changed and requires saving or not.
    /// </summary>
    public bool IsChanged
    {
      get { return isChanged; }
    }

    /// <summary>
    /// Property relating to database column idCardGroup
    /// </summary>
    public int IdCardGroup
    {
      get { return idCardGroup; }
    }

    /// <summary>
    /// Property relating to database column name
    /// </summary>
    public string Name
    {
      get { return name; }
      set
      {
        isChanged |= name != value;
        name = value;
      }
    }

    #endregion

    #region Storage and Retrieval

    /// <summary>
    /// Static method to retrieve all instances that are stored in the database in one call
    /// </summary>
    public static IList<CardGroup> ListAll()
    {
      return Broker.RetrieveList<CardGroup>();
    }

    /// <summary>
    /// Retrieves an entity given it's id.
    /// </summary>
    public static CardGroup Retrieve(int id)
    {
      // Return null if id is smaller than seed and/or increment for autokey
      if (id < 1)
      {
        return null;
      }
      Key key = new Key(typeof (CardGroup), true, "idCardGroup", id);
      return Broker.RetrieveInstance<CardGroup>(key);
    }

    /// <summary>
    /// Retrieves an entity given it's id, using Gentle.Framework.Key class.
    /// This allows retrieval based on multi-column keys.
    /// </summary>
    public static CardGroup Retrieve(Key key)
    {
      return Broker.RetrieveInstance<CardGroup>(key);
    }

    /// <summary>
    /// Persists the entity if it was never persisted or was changed.
    /// </summary>
    public override void Persist()
    {
      if (IsChanged || !IsPersisted)
      {
        try
        {
          base.Persist();
        }
        catch (Exception ex)
        {
          Log.Error("Exception in CardGroup.Persist() with Message {0}", ex.Message);
          return;
        }
        isChanged = false;
      }
    }

    #endregion

    #region Relations

    /// <summary>
    /// Get a list of Card referring to the current entity.
    /// </summary>
    public IList<CardGroupMap> CardGroupMaps()
    {
      //select * from 'foreigntable'
      SqlBuilder sb = new SqlBuilder(StatementType.Select, typeof (CardGroupMap));

      // where foreigntable.foreignkey = ourprimarykey
      sb.AddConstraint(Operator.Equals, "idCardGroup", idCardGroup);

      // passing true indicates that we'd like a list of elements, i.e. that no primary key
      // constraints from the type being retrieved should be added to the statement
      SqlStatement stmt = sb.GetStatement(true);

      // execute the statement/query and create a collection of User instances from the result set
      return ObjectFactory.GetCollection<CardGroupMap>(stmt.Execute());

      // TODO In the end, a GentleList should be returned instead of an arraylist
      //return new GentleList( typeof(Card), this );
    }

    #endregion

    public void Delete()
    {
      foreach (CardGroupMap group in CardGroupMaps())
      {
        group.Remove();
      }
      Remove();
    }
  }
}