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

#ifndef __OUTPUT_STREAM_HOSTER_DEFINED
#define __OUTPUT_STREAM_HOSTER_DEFINED

#include "Hoster.h"
#include "IOutputStream.h"

#define MODULE_OUTPUT_STREAM_HOSTER_NAME                                      L"OutputStreamHoster"

class COutputStreamHoster : public CHoster, public IOutputStream
{
public:
  COutputStreamHoster(CLogger *logger, CParameterCollection *configuration, const wchar_t *moduleName, const wchar_t *moduleSearchPattern, IOutputStream *outputStream);
  virtual ~COutputStreamHoster(void);

  // IOutputStream interface

  // notifies output stream about stream count
  // @param streamCount : the stream count
  // @param liveStream : true if stream(s) are live, false otherwise
  // @return : S_OK if successful, false otherwise
  HRESULT SetStreamCount(unsigned int streamCount, bool liveStream);

  // pushes stream received data to filter
  // @param streamId : the stream ID to push stream received data
  // @param streamReceivedData : the stream received data to push to filter
  // @return : S_OK if successful, error code otherwise
  HRESULT PushStreamReceiveData(unsigned int streamId, CStreamReceiveData *streamReceiveData);

protected:
  // reference to output stream methods
  IOutputStream *outputStream;
};

#endif