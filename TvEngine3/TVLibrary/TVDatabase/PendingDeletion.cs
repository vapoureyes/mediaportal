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
  /// Instances of this class represent the properties and methods of a row in the table <b>PendingDeletion</b>.
  /// </summary>
  [TableName("PendingDeletion")]
  public class PendingDeletion : Persistent
  {
    #region Members

    private bool isChanged;
    [TableColumn("idPendingDeletion", NotNull = true), PrimaryKey(AutoGenerated = true)] private int _idPendingDeletion;
    [TableColumn("fileName", NotNull = true)] private string _fileName;

    #endregion

    #region Constructors

    /// <summary> 
    /// Create a new object by specifying all fields (except the auto-generated primary key field). 
    /// </summary> 
    public PendingDeletion(string fileName)
    {
      isChanged = true;
      this._fileName = fileName;
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
    /// Property relating to database column IdPendingDeletion
    /// </summary>
    public int IdPendingDeletion
    {
      get { return _idPendingDeletion; }
    }

    /// <summary>
    /// Property relating to database column fileName
    /// </summary>
    public string FileName
    {
      get { return _fileName; }
      set
      {
        isChanged |= _fileName != value;
        _fileName = value;
      }
    }

    #endregion

    #region Storage and Retrieval

    /// <summary>
    /// Static method to retrieve all instances that are stored in the database in one call
    /// </summary>
    public static IList<PendingDeletion> ListAll()
    {
      return (List<PendingDeletion>)(Broker.RetrieveList<PendingDeletion>());
    }

    /// <summary>
    /// Retrieves an entity given it's id.
    /// </summary>
    public static PendingDeletion Retrieve(int id)
    {
      // Return null if id is smaller than seed and/or increment for autokey
      if (id < 1)
      {
        return null;
      }
      Key key = new Key(typeof (PendingDeletion), true, "idPendingDeletion", id);

      return Broker.RetrieveInstance<PendingDeletion>(key);
    }

    /// <summary>
    /// Retrieves an entity given it's filename.
    /// </summary>
    public static PendingDeletion Retrieve(string fileName)
    {
      // Return null if id is smaller than seed and/or increment for autokey
      if (string.IsNullOrEmpty(fileName))
      {
        return null;
      }

      SqlBuilder sb = new SqlBuilder(StatementType.Select, typeof (PendingDeletion));
      sb.AddConstraint(Operator.Equals, "fileName", fileName);

      SqlStatement stmt = sb.GetStatement(true);

      // execute the statement/query and create a collection of User instances from the result set
      IList<PendingDeletion> getList = ObjectFactory.GetCollection<PendingDeletion>(stmt.Execute());
      if (getList.Count != 0)
      {
        return getList[0];
      }
      return null;
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
          Log.Error("Exception in PendingDeletion.Persist() with Message {0}", ex.Message);
          return;
        }
        isChanged = false;
      }
    }

    #endregion

    public void Delete()
    {
      Remove();
    }
  }
}