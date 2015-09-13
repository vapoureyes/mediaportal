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

#include "StreamAvailableLength.h"

CStreamAvailableLength::CStreamAvailableLength(void)
{
  this->availableLength = 0;
  this->streamId = 0;
}

CStreamAvailableLength::~CStreamAvailableLength(void)
{
}

/* get methods */

LONGLONG CStreamAvailableLength::GetAvailableLength(void)
{
  return this->availableLength;
}

unsigned int CStreamAvailableLength::GetStreamId(void)
{
  return this->streamId;
}

/* set methods */

void CStreamAvailableLength::SetAvailableLength(LONGLONG availableLength)
{
  this->availableLength = availableLength;
}

void CStreamAvailableLength::SetStreamId(unsigned int streamId)
{
  this->streamId = streamId;
}

/* other methods */