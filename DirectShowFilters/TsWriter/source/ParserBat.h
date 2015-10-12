/*
 *  Copyright (C) 2006-2008 Team MediaPortal
 *  http://www.team-mediaportal.com
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
#pragma once
#include "ParserNitDvb.h"


#define PID_BAT 0x11
#define TABLE_ID_BAT 0x4a


class CParserBat : public CParserNitDvb
{
  public:
    CParserBat(void);
    virtual ~CParserBat(void);

    void SetPid(unsigned short pid);
    bool IsSeen() const;
    bool IsReady() const;

    bool GetService(unsigned short originalNetworkId,
                    unsigned short transportStreamId,
                    unsigned short serviceId,
                    unsigned short preferredLogicalChannelNumberBouquetId,
                    unsigned short preferredLogicalChannelNumberRegionId,
                    unsigned short& freesatChannelId,
                    unsigned short& openTvChannelId,
                    unsigned short& logicalChannelNumber,
                    bool& visibleInGuide,
                    unsigned short* bouquetIds,
                    unsigned char& bouquetIdCount,
                    unsigned long* availableInCells,
                    unsigned char& availableInCellCount,
                    unsigned long long* targetRegionIds,
                    unsigned char& targetRegionIdCount,
                    unsigned short* freesatRegionIds,
                    unsigned char& freesatRegionIdCount,
                    unsigned short* openTvRegionIds,
                    unsigned char& openTvRegionIdCount,
                    unsigned short* freesatChannelCategoryIds,
                    unsigned char& freesatChannelCategoryIdCount,
                    unsigned char* norDigChannelListIds,
                    unsigned char& norDigChannelListIdCount,
                    unsigned long* availableInCountries,
                    unsigned char& availableInCountryCount,
                    unsigned long* unavailableInCountries,
                    unsigned char& unavailableInCountryCount) const;

    unsigned char GetBouquetNameCount(unsigned short bouquetId) const;
    bool GetBouquetNameByIndex(unsigned short bouquetId,
                                unsigned char index,
                                unsigned long& language,
                                char* name,
                                unsigned short nameBufferSize) const;
    bool GetBouquetNameByLanguage(unsigned short bouquetId,
                                  unsigned long language,
                                  char* name,
                                  unsigned short nameBufferSize) const;
};