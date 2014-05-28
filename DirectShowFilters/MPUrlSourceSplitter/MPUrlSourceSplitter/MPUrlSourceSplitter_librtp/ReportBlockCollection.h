/*
    Copyright (C) 2007-2010 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2.  If not, see <http://www.gnu.org/licenses/>.
*/

#pragma once

#ifndef __REPORT_BLOCK_COLLECTION_DEFINED
#define __REPORT_BLOCK_COLLECTION_DEFINED

#include "ReportBlock.h"
#include "Collection.h"

class CReportBlockCollection : public CCollection<CReportBlock>
{
public:
  CReportBlockCollection(void);
  ~CReportBlockCollection(void);

protected:

  // clones specified item
  // @param item : the item to clone
  // @return : deep clone of item or NULL if not implemented
  CReportBlock *Clone(CReportBlock *item);
};

#endif