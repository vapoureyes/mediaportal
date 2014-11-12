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

#ifndef __RTSP_GET_PARAMETER_REQUEST_DEFINED
#define __RTSP_GET_PARAMETER_REQUEST_DEFINED

#include "RtspRequest.h"

#define RTSP_GET_PARAMETER_METHOD                                     L"GET_PARAMETER"

class CRtspGetParameterRequest : public CRtspRequest
{
public:
  CRtspGetParameterRequest(HRESULT *result);
  virtual ~CRtspGetParameterRequest(void);

  /* get methods */

  // gets RTSP request method
  // @return : RTSP request method
  virtual const wchar_t *GetMethod(void);

  /* set methods */

  /* other methods */

protected:

  /* methods */

  CRtspGetParameterRequest(HRESULT *result, bool createDefaultHeaders);

  // deeply clones current instance to cloned RTSP request
  // @param  clone : cloned RTSP request to hold clone of current instance
  // @return : true if successful, false otherwise
  virtual bool CloneInternal(CRtspRequest *clone);

  // returns new RTSP request object to be used in cloning
  // @return : RTSP request object or NULL if error
  virtual CRtspRequest *CreateRequest(void);
};

#endif