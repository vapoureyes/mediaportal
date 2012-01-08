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

#region usings 

using System.Collections.Generic;
using System.Linq;
using Mediaportal.TV.Server.TVControl;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVService.CardManagement.CardHandler;
using Mediaportal.TV.Server.TVService.Interfaces.CardHandler;
using Mediaportal.TV.Server.TVService.Interfaces.Services;

#endregion

namespace Mediaportal.TV.Server.TVService.CardManagement.CardAllocation
{
  public abstract class CardAllocationBase
  {
    private bool _logEnabled = true;

    protected bool LogEnabled
    {
      get { return _logEnabled; }
      set { _logEnabled = value; }
    }    

    protected CardAllocationBase()
    {           
    }

    #region protected members            

    private bool IsCamAbleToDecryptChannel(IUser user, ITvCardHandler tvcard, IChannel tuningDetail, int decryptLimit)
    {
      if (!tuningDetail.FreeToAir)
      {
        bool isCamAbleToDecryptChannel = true;
        if (decryptLimit > 0)
        {
          int camDecrypting = NumberOfChannelsDecrypting(tvcard);

          //Check if the user is currently occupying a decoding slot and subtract that from the number of channels it is decoding.
          if (user.CardId == tvcard.DataBaseCard.idCard)
          {
            bool isFreeToAir = IsFreeToAir(tvcard, ref user);
            if (!isFreeToAir)
            {
              int numberOfUsersOnCurrentChannel = GetNumberOfUsersOnCurrentChannel(tvcard, user);

              //Only subtract the slot the user is currently occupying if he is the only user occupying the slot.
              if (numberOfUsersOnCurrentChannel == 1)
              {
                --camDecrypting;
              }
            }
          }
          //check if cam is capable of descrambling an extra channel
          isCamAbleToDecryptChannel = (camDecrypting < decryptLimit);
        }

        return isCamAbleToDecryptChannel;
      }
      return true;
    }

    protected virtual int GetNumberOfUsersOnCurrentChannel(ITvCardHandler tvcard, IUser user) 
    {
      int currentChannelId = tvcard.CurrentDbChannel(ref user);
      return tvcard.UserManagement.Users.Count(aUser => aUser.Value.IdChannel == currentChannelId);
    }

    protected virtual bool IsFreeToAir(ITvCardHandler tvcard, ref IUser user)
    {      
      IChannel currentUserCh = tvcard.CurrentChannel(ref user);
      return (currentUserCh != null && currentUserCh.FreeToAir);
    }

    protected virtual int NumberOfChannelsDecrypting(ITvCardHandler tvcard)
    {
      return tvcard.NumberOfChannelsDecrypting;
    }

    protected virtual bool IsCamAlreadyDecodingChannel(ITvCardHandler tvcard, IChannel tuningDetail)
    {
      bool isCamAlreadyDecodingChannel = false;
      IDictionary<string, IUser> currentUsers = tvcard.UserManagement.Users;
      if (currentUsers != null)
      {
        foreach (IUser user in currentUsers.Values)        
        {
          IUser userCopy = (IUser)user.Clone();
          IChannel currentChannel = tvcard.CurrentChannel(ref userCopy);
          if (currentChannel != null && currentChannel.Equals(tuningDetail))          
          {
            //yes, cam already is descrambling this channel
            isCamAlreadyDecodingChannel = true;
            break;
          }
        }
      }
      return isCamAlreadyDecodingChannel;
    }



    protected static bool IsValidTuningDetails(ICollection<IChannel> tuningDetails)
    {
      bool isValid = (tuningDetails != null && tuningDetails.Count > 0);
      return isValid;
    }

    public virtual bool CheckTransponder(IUser user, ITvCardHandler tvcard, IChannel tuningDetail)
    {
      int decryptLimit = tvcard.DataBaseCard.decryptLimit;
      int cardId = tvcard.DataBaseCard.idCard;      

      bool checkTransponder = true;
      bool isOwnerOfCard = IsOwnerOfCard(tvcard, user);

      //TODO: Being card owner you can do whatever you want, but in case of decryptlimit that could mean kicking users. This is not handled in the code.
      if (isOwnerOfCard)
      {
        if (LogEnabled)
        {
          Log.Info("Controller:    card:{0} type:{1} is available", cardId, tvcard.Type);
        }
      }
      else
      {
        bool isSameTransponder = IsSameTransponder(tvcard, tuningDetail);
        if (isSameTransponder)
        {
          //card is in use, but it is tuned to the same transponder.
          //meaning.. we can use it.          
          //if the channel is encrypted check cam decrypt limit.
          if (!tuningDetail.FreeToAir)
          {
            //but we must check if cam can decode the extra channel as well
            //first check if cam is already decrypting this channel          
            bool canDecrypt = false;
            bool isCamAlreadyDecodingChannel = IsCamAlreadyDecodingChannel(tvcard, tuningDetail);

            if (!isCamAlreadyDecodingChannel)
            {
              //check if cam is capable of descrambling an extra channel                            
              bool isCamAbleToDecrypChannel = IsCamAbleToDecryptChannel(user, tvcard, tuningDetail, decryptLimit);
              if (isCamAbleToDecrypChannel)
              {
                canDecrypt = true;
              }
            }
            else
            {
              canDecrypt = true;
            }

            if (canDecrypt)
            {
              if (decryptLimit > 0)
              {
                if (LogEnabled)
                {
                  Log.Info(
                    "Controller:    card:{0} type:{1} is available, tuned to same transponder decrypting {2}/{3} channels",
                    cardId, tvcard.Type, NumberOfChannelsDecrypting(tvcard), decryptLimit);
                }
              }
              else
              {
                if (LogEnabled)
                {
                  Log.Info(
                    "Controller:    card:{0} type:{1} is available, tuned to same transponder",
                    cardId, tvcard.Type);
                }
              }
            }
            else
            {
              //it is not, skip this card
              if (LogEnabled)
              {
                Log.Info(
                  "Controller:    card:{0} type:{1} is not available, tuned to same transponder decrypting {2}/{3} channels (cam limit reached)",
                  cardId, tvcard.Type, NumberOfChannelsDecrypting(tvcard), decryptLimit);
              }
              checkTransponder = false;
            }
          }
        }
        else
        {
          if (LogEnabled)
          {
            Log.Info("Controller:    card:{0} type:{1} is not available, tuned to different transponder",
                     cardId, tvcard.Type);
          }
          checkTransponder = false;
        }
      }
      return checkTransponder;
    }

    private static bool HasEqualOrHigherPriority(ITvCardHandler tvcard, IUser user)
    {
      return tvcard.UserManagement.HasEqualOrHigherPriority(user);
    }

    private static bool HasHighestPriority(ITvCardHandler tvcard, IUser user)
    {
      return tvcard.UserManagement.HasHighestPriority(user);
    }

    protected virtual bool IsOwnerOfCard(ITvCardHandler tvcard, IUser user)
    {      
      bool hasHighestPriority = HasHighestPriority(tvcard, user);
      bool isOwnerOfCard = false;

      if (hasHighestPriority)
      {
        isOwnerOfCard = true;
      }
      else
      {
        bool hasEqualOrHigherPriority = HasEqualOrHigherPriority(tvcard, user);
        if (hasEqualOrHigherPriority)
        {
          isOwnerOfCard = tvcard.UserManagement.IsOwner(user);
        }
      }

      return isOwnerOfCard;
    }

    protected virtual bool IsSameTransponder(ITvCardHandler tvcard, IChannel tuningDetail)
    {
      return tvcard.Tuner.IsTunedToTransponder(tuningDetail) &&
             tvcard.SupportsSubChannels;
    }

    #endregion
  }
}