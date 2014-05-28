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

#include "StdAfx.h"

#include "RtspContentLocationResponseHeader.h"

CRtspContentLocationResponseHeader::CRtspContentLocationResponseHeader(void)
  : CRtspResponseHeader()
{
  this->uri = NULL;
}

CRtspContentLocationResponseHeader::~CRtspContentLocationResponseHeader(void)
{
  FREE_MEM(this->uri);
}

/* get methods */

const wchar_t *CRtspContentLocationResponseHeader::GetUri(void)
{
  return this->uri;
}

/* set methods */

/* other methods */

CRtspContentLocationResponseHeader *CRtspContentLocationResponseHeader::Clone(void)
{
  return (CRtspContentLocationResponseHeader *)__super::Clone();
}

bool CRtspContentLocationResponseHeader::CloneInternal(CHttpHeader *clonedHeader)
{
  bool result = __super::CloneInternal(clonedHeader);
  CRtspContentLocationResponseHeader *header = dynamic_cast<CRtspContentLocationResponseHeader *>(clonedHeader);
  result &= (header != NULL);

  if (result)
  {
    SET_STRING_RESULT_WITH_NULL(header->uri, this->uri, result);
  }

  return result;
}

CHttpHeader *CRtspContentLocationResponseHeader::GetNewHeader(void)
{
  return new CRtspContentLocationResponseHeader();
}

bool CRtspContentLocationResponseHeader::Parse(const wchar_t *header, unsigned int length)
{
  bool result = __super::Parse(header, length);

  if (result)
  {
    result &= (_wcsicmp(this->name, RTSP_CONTENT_LOCATION_RESPONSE_HEADER_TYPE) == 0);

    if (result)
    {
      this->uri = Duplicate(this->value);
      result &= (this->uri != NULL);
    }
  }

  if (result)
  {
    this->responseHeaderType = Duplicate(RTSP_CONTENT_LOCATION_RESPONSE_HEADER_TYPE);
    result &= (this->responseHeaderType != NULL);
  }

  return result;
}