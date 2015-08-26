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
#include <ctime>
#include <DShow.h>    // (media types), IMediaSample, IPin, REFERENCE_TIME
#include <streams.h>  // CAutoLock, CBaseFilter, CCritSec, CMediaType, CRenderedInputPin
#include <WinError.h> // HRESULT
#include "..\..\shared\FileWriter.h"
#include "..\..\shared\PacketSync.h"
#include "ITsAnalyser.h"

using namespace std;


#define NOT_RECEIVING -1

const AMOVIESETUP_MEDIATYPE INPUT_MEDIA_TYPES_TS[] =
{
  { &MEDIATYPE_Stream, &MEDIASUBTYPE_MPEG2_TRANSPORT }
};
const unsigned char INPUT_MEDIA_TYPE_COUNT_TS = 1;


class CInputPinTs : public CRenderedInputPin, CPacketSync
{
  public:
    CInputPinTs(ITsAnalyser* analyser,
                CBaseFilter* filter,
                CCritSec* filterLock,
                CCritSec& receiveLock,
                HRESULT* hr);
    virtual ~CInputPinTs(void);

    HRESULT BreakConnect();
    HRESULT CheckMediaType(const CMediaType* mediaType);
    HRESULT CompleteConnect(IPin* receivePin);
    HRESULT GetMediaType(int position, CMediaType* mediaType);
    STDMETHODIMP Receive(IMediaSample* sample);
    STDMETHODIMP ReceiveCanBlock();
    HRESULT Run(REFERENCE_TIME startTime);

    clock_t GetReceiveTime();
    HRESULT StartDumping(const wchar_t* fileName);
    HRESULT StopDumping();

  private:
    void OnTsPacket(unsigned char* tsPacket);

    ITsAnalyser* m_analyser;
    clock_t m_receiveTime;
    CCritSec& m_receiveLock;

    bool m_isDumpEnabled;
    FileWriter m_dumpFileWriter;
    CCritSec m_dumpLock;
};