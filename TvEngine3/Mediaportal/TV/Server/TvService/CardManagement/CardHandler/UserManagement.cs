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
using System.Threading;
using Mediaportal.TV.Server.TVControl;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVService.Interfaces;
using Mediaportal.TV.Server.TVService.Interfaces.CardHandler;
using Mediaportal.TV.Server.TVService.Interfaces.Enums;
using Mediaportal.TV.Server.TVService.Interfaces.Services;

namespace Mediaportal.TV.Server.TVService.CardManagement.CardHandler
{
  public class UserManagement : IUserManagement
  {
    private readonly ITvCardHandler _cardHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagement"/> class.
    /// </summary>
    /// <param name="cardHandler">The card handler.</param>
    public UserManagement(ITvCardHandler cardHandler)
    {
      _cardHandler = cardHandler;
    }

    /// <summary>
    /// Locks the card to the user specified
    /// </summary>
    /// <param name="user">The user.</param>
    public void Lock(IUser user)
    {
      if (_cardHandler.Card != null)
      {
        var context = _cardHandler.Card.Context as ITvCardContext;
        context.Lock(user);
      }
    }

    /// <summary>
    /// Unlocks this card.
    /// </summary>
    /// <param name="user">The user.</param>
    /// 
    public void Unlock(IUser user)
    {
      if (_cardHandler.Card != null)
      {
        var context = _cardHandler.Card.Context as ITvCardContext;
        context.Remove(user);
      }
    }

    /// <summary>
    /// Determines whether the specified user is owner of this card
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>
    /// 	<c>true</c> if the specified user is owner; otherwise, <c>false</c>.
    /// </returns>
    public bool IsOwner(IUser user)
    {
      var context = _cardHandler.Card.Context as ITvCardContext;
      return context.IsOwner(user);
    }

    /// <summary>
    /// Removes the user from this card
    /// </summary>
    /// <param name="user">The user.</param>
    public void RemoveUser(IUser user)
    {
      var context = _cardHandler.Card.Context as ITvCardContext;
      if (context == null)
        return;
      if (!context.DoesExists(user))
        return;
      context.GetUser(ref user, _cardHandler.DataBaseCard.idCard);

      Log.Debug("usermanagement.RemoveUser: {0}, subch: {1} of {2}, card: {3}", user.Name, user.SubChannel, _cardHandler.Card.SubChannels.Length, _cardHandler.DataBaseCard.idCard);                  
      context.Remove(user);
      if (!context.ContainsUsersForSubchannel(user.SubChannel))
      {
        //only remove subchannel if it exists.
        if (_cardHandler.Card.GetSubChannel(user.SubChannel) != null)
        {
          int usedSubChannel = user.SubChannel;
          // Before we remove the subchannel we have to stop it
          ITvSubChannel subChannel = _cardHandler.Card.GetSubChannel(user.SubChannel);
          if (subChannel.IsTimeShifting)
          {
            subChannel.StopTimeShifting();
          }
          else if (subChannel.IsRecording)
          {
            subChannel.StopRecording();
          }
          _cardHandler.Card.FreeSubChannel(user.SubChannel);
          CleanTimeshiftFilesThread cleanTimeshiftFilesThread =
            new CleanTimeshiftFilesThread(_cardHandler.DataBaseCard.timeshiftingFolder,
                                          String.Format("live{0}-{1}.ts", _cardHandler.DataBaseCard.idCard,
                                                        usedSubChannel));
          Thread cleanupThread = new Thread(cleanTimeshiftFilesThread.CleanTimeshiftFiles);
          cleanupThread.IsBackground = true;
          cleanupThread.Name = "TS_File_Cleanup";
          cleanupThread.Priority = ThreadPriority.Lowest;
          cleanupThread.Start();
        }
      }
      if (_cardHandler.IsIdle)
      {
        if (_cardHandler.Card.SupportsPauseGraph)
        {
          _cardHandler.Card.PauseGraph();
        }
        else
        {
          _cardHandler.Card.StopGraph();
        }
      }
    }

    public TvStoppedReason GetTvStoppedReason(IUser user)
    {
      var context = _cardHandler.Card.Context as ITvCardContext;
      if (context == null)
        return TvStoppedReason.UnknownReason;

      return context.GetTimeshiftStoppedReason(user);
    }

    public void SetTvStoppedReason(IUser user, TvStoppedReason reason)
    {
      var context = _cardHandler.Card.Context as ITvCardContext;
      if (context == null)
        return;

      context.SetTimeshiftStoppedReason(user, reason);
    }

    /// <summary>
    /// Determines whether the card is locked and ifso returns by which user
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>
    /// 	<c>true</c> if the specified card is locked; otherwise, <c>false</c>.
    /// </returns>
    public bool IsLocked(out IUser user)
    {
      user = null;
      if (_cardHandler.Card != null)
      {
        var context = _cardHandler.Card.Context as ITvCardContext;
        context.IsLocked(out user);
        return (user != null);
      }
      return false;
    }

    /// <summary>
    /// Gets the users for this card.
    /// </summary>
    /// <returns></returns>
    public IDictionary<string,IUser> Users
    {
      get
      {
        var context = _cardHandler.Card.Context as ITvCardContext;
        if (context == null)
        {
          return new Dictionary<string, IUser>();
        }
        return context.Users; 
      }      
    }

    public bool HasEqualOrHigherPriority(IUser user)
    {
      bool hasEqualOrHigherPriority = false;
      var context = _cardHandler.Card.Context as ITvCardContext;
      if (context != null)
      {
        hasEqualOrHigherPriority = context.HasUserEqualOrHigherPriority(user);
  }
      return hasEqualOrHigherPriority;
    }

    public bool HasHighestPriority(IUser user)
    {
      bool hasHighestPriority = false;
      var context = _cardHandler.Card.Context as ITvCardContext;
      if (context != null)
      {
        hasHighestPriority = context.HasUserHighestPriority(user);
      }
      return hasHighestPriority;         
    }

  }
}