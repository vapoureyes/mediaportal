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

#ifndef __RTSP_TRANSPORT_REQUEST_HEADER_DEFINED
#define __RTSP_TRANSPORT_REQUEST_HEADER_DEFINED

#include "RtspRequestHeader.h"

#define RTSP_TRANSPORT_REQUEST_HEADER_NAME                                L"Transport"

#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_NONE                           0x00000000
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_UNICAST                        0x00000001
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_MULTICAST                      0x00000002
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_INTERLEAVED                    0x00000004
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_TRANSPORT_PROTOCOL_RTP         0x00000008
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_PROFILE_AVP                    0x00000010
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_LOWER_TRANSPORT_TCP            0x00000020
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_LOWER_TRANSPORT_UDP            0x00000040
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_APPEND                         0x00000080
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_TIME_TO_LIVE                   0x00000100
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_LAYERS                         0x00000200
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_PORT                           0x00000400
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_CLIENT_PORT                    0x00000800
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_SERVER_PORT                    0x00001000
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_SSRC                           0x00002000
#define RTSP_TRANSPORT_REQUEST_HEADER_FLAG_MODE                           0x00004000

#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_UNICAST                   L"unicast"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_MULTICAST                 L"multicast"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_DESTINATION               L"destination"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_INTERLEAVED               L"interleaved"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_APPEND                    L"append"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_TIME_TO_LIVE              L"ttl"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_LAYERS                    L"layers"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_PORT                      L"port"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_CLIENT_PORT               L"client_port"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_SERVER_PORT               L"server_port"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_SSRC                      L"ssrc"
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_MODE                      L"mode"

#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_UNICAST_LENGTH            7
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_MULTICAST_LENGTH          9
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_DESTINATION_LENGTH        11
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_INTERLEAVED_LENGTH        11
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_APPEND_LENGTH             6
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_TIME_TO_LIVE_LENGTH       3
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_LAYERS_LENGTH             6
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_PORT_LENGTH               4
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_CLIENT_PORT_LENGTH        11
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_SERVER_PORT_LENGTH        11
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_SSRC_LENGTH               4
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_MODE_LENGTH               4

#define RTSP_TRANSPORT_REQUEST_HEADER_SEPARATOR                           L";"
#define RTSP_TRANSPORT_REQUEST_HEADER_SEPARATOR_LENGTH                    1

#define RTSP_TRANSPORT_REQUEST_HEADER_PROTOCOL_SEPARATOR                  L"/"
#define RTSP_TRANSPORT_REQUEST_HEADER_PROTOCOL_SEPARATOR_LENGTH           1

#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_VALUE_SEPARATOR           L"="
#define RTSP_TRANSPORT_REQUEST_HEADER_PARAMETER_VALUE_SEPARATOR_LENGTH    1

#define RTSP_TRANSPORT_REQUEST_HEADER_RANGE_SEPARATOR                     L"-"
#define RTSP_TRANSPORT_REQUEST_HEADER_RANGE_SEPARATOR_LENGTH              1

#define RTSP_TRANSPORT_REQUEST_HEADER_PROTOCOL_RTP                        L"RTP"
#define RTSP_TRANSPORT_REQUEST_HEADER_PROFILE_AVP                         L"AVP"
#define RTSP_TRANSPORT_REQUEST_HEADER_LOWER_TRANSPORT_TCP                 L"TCP"
#define RTSP_TRANSPORT_REQUEST_HEADER_LOWER_TRANSPORT_UDP                 L"UDP"

class CRtspTransportRequestHeader : public CRtspRequestHeader
{
public:
  CRtspTransportRequestHeader(void);
  virtual ~CRtspTransportRequestHeader(void);

  /* get methods */

  // gets RTSP header name
  // @return : RTSP header name
  virtual const wchar_t *GetName(void);

  // gets RTSP header value
  // @return : RTSP header value
  virtual const wchar_t *GetValue(void);

  // gets transport protocol
  // @return : transport protoco or NULL if not specified
  virtual const wchar_t *GetTransportProtocol(void);

  // gets profile
  // @return : profile or NULL if not specified
  virtual const wchar_t *GetProfile(void);

  // gets lower transport
  // @return : lower transport or NULL if not specified
  virtual const wchar_t *GetLowerTransport(void);

  // gets destination
  // @return : destination or NULL if not specified
  virtual const wchar_t *GetDestination(void);

  // gets min interleaved channel
  // @return : min interleaved channel
  virtual unsigned int GetMinInterleavedChannel(void);

  // gets max interleaved channel
  // @return : max interleaved channel
  virtual unsigned int GetMaxInterleavedChannel(void);

  // gets multicast time-to-live
  // @return : multicast time-to-live
  virtual unsigned int GetTimeToLive(void);

  // gets number of multicast layers to be used for this media stream
  // @return : number of multicast layers to be used for this media stream
  virtual unsigned int GetLayers(void);

  // gets multicast session min port
  // @return : multicast session min port
  virtual unsigned int GetMinPort(void);

  // gets multicast session max port
  // @return : multicast session max port
  virtual unsigned int GetMaxPort(void);

  // gets min client port
  // @return : min client port
  virtual unsigned int GetMinClientPort(void);

  // gets max client port
  // @return : max client port
  virtual unsigned int GetMaxClientPort(void);

  // gets min server port
  // @return : min server port
  virtual unsigned int GetMinServerPort(void);

  // gets max server port
  // @return : max server port
  virtual unsigned int GetMaxServerPort(void);

  // gets mode
  // @return : mode or NULL if not specified
  virtual const wchar_t *GetMode(void);

  // gets RTP synchronization source identifier
  // @return : RTP synchronization source identifier
  virtual unsigned int GetSynchronizationSourceIdentifier(void);

  // gets all flags
  // @return : flags
  virtual unsigned int GetFlags(void);

  /* set methods */

  // sets RTSP header name
  // @param name : RTSP header name to set
  // @return : true if successful, false otherwise
  virtual bool SetName(const wchar_t *name);

  // sets RTSP header value
  // @param value : RTSP header value to set
  // @return : true if successful, false otherwise
  virtual bool SetValue(const wchar_t *value);

  // sets transport protocol
  // @param transportProtocol : transport protocol to set
  // @return : true if successful, false otherwise
  virtual bool SetTransportProtocol(const wchar_t *transportProtocol);

  // sets profile
  // @param profile : profile to set
  // @return : true if successful, false otherwise
  virtual bool SetProfile(const wchar_t *profile);

  // sets lower transport
  // @param lowerTransport : lower transport to set
  // @return : true if successful, false otherwise
  virtual bool SetLowerTransport(const wchar_t *lowerTransport);

  // sets destination
  // @param destination : destination to set
  // @return : true if successful, false otherwise
  virtual bool SetDestination(const wchar_t *destination);

  // sets min interleaved channel
  // @param minInterleavedChannel : min interleaved channel to set
  virtual void SetMinInterleavedChannel(unsigned int minInterleavedChannel);

  // sets max interleaved channel
  // @param maxInterleavedChannel : max interleaved channel to set
  virtual void SetMaxInterleavedChannel(unsigned int maxInterleavedChannel);

  // sets multicast time-to-live
  // @param timeToLive : multicast time-to-live to set
  virtual void SetTimeToLive(unsigned int timeToLive);

  // sets number of multicast layers to be used for this media stream
  // @param layers : number of multicast layers to set
  virtual void SetLayers(unsigned int layers);

  // sets multicast session min port
  // @param minPort : multicast session min port
  virtual void SetMinPort(unsigned int minPort);

  // sets multicast session max port
  // @param maxPort : multicast session max port to set
  virtual void SetMaxPort(unsigned int maxPort);

  // sets min client port
  // @param minClientPort : min client port to set
  virtual void SetMinClientPort(unsigned int minClientPort);

  // sets max client port
  // @param maxClientPort : max client port to set
  virtual void SetMaxClientPort(unsigned int maxClientPort);

  // sets min server port
  // @param minServerPort : min server port
  virtual void SetMinServerPort(unsigned int minServerPort);

  // sets max server port
  // @param maxServerPort : max server port to set
  virtual void SetMaxServerPort(unsigned int maxServerPort);

  // sets mode
  // @param mode : mode to set
  // @return : true if successful, false otherwise
  virtual bool SetMode(const wchar_t *mode);

  // sets RTP synchronization source identifier
  // @param synchronizationSourceIdentifier : RTP synchronization source identifier to set
  virtual void SetSynchronizationSourceIdentifier(unsigned int synchronizationSourceIdentifier);

  // sets flags for RTSP transport header request
  // @param flags : the combination of FLAG_RTSP_TRANSPORT_REQUEST_HEADER flags to set
  virtual void SetFlags(unsigned int flags);

  /* other methods */

  // tests if unicast is set
  // @return : true if unicast is set, false otherwise
  virtual bool IsUnicast(void);

  // tests if multicast is set
  // @return : true if multiast is set, false otherwise
  virtual bool IsMulticast(void);

  // tests if interleaved is set
  // @return : true if interleaved is set, false otherwise
  virtual bool IsInterleaved(void);

  // tests if transport protocol is RTP
  // @return : true if transport protocol is RTP, false otherwise
  virtual bool IsTransportProtocolRTP(void);

  // tests if profile is AVP
  // @return : true if profile is AVP, false otherwise
  virtual bool IsProfileAVP(void);

  // tests if lower transport is TCP
  // @return : true if lower transport is TCP, false otherwise
  virtual bool IsLowerTransportTCP(void);

  // tests if lower transport is UDP
  // @return : true if lower transport is UDP, false otherwise
  virtual bool IsLowerTransportUDP(void);

  // tests if append is set
  // @return : true if append is set, false otherwise
  virtual bool IsAppend(void);

  // tests if time-to-live is set
  // @return : true if time-to-live is set, false otherwise
  virtual bool IsTimeToLive(void);

  // tests if layers is set
  // @return : true if layers is set, false otherwise
  virtual bool IsLayers(void);

  // tests if port is set
  // @return : true if port is set, false otherwise
  virtual bool IsPort(void);

  // tests if client port is set
  // @return : true if client port is set, false otherwise
  virtual bool IsClientPort(void);

  // tests if server port is set
  // @return : true if server port is set, false otherwise
  virtual bool IsServerPort(void);

  // tests if synchronization source identifier is set
  // @return : true if synchronization source identifier is set, false otherwise
  virtual bool IsSynchronizationSourceIdentifier(void);

  // tests if flag is set
  // @param flag : the flag to test
  // @return : true if flag is set, false otherwise
  virtual bool IsSetFlag(unsigned int flag);

  // deep clones of current instance
  // @return : deep clone of current instance or NULL if error
  virtual CRtspTransportRequestHeader *Clone(void);

protected:

  // holds various flags
  unsigned int flags;

  // holds transport protocol (it should be RTP)
  wchar_t *transportProtocol;

  // holds profile (it should be AVP)
  wchar_t *profile;

  // holds lower transport (it should be TCP or UDP)
  wchar_t *lowerTransport;

  // holds destination
  wchar_t *destination;

  // holds min and max interleaved channel number (if specified)
  unsigned int minInterleaved;
  unsigned int maxInterleaved;

  // holds multicast time-to-live
  unsigned int timeToLive;

  // holds the number of multicast layers to be used for this media stream
  unsigned int layers;

  // holds pair for a multicast session, it is specified as a range, e.g., port=3456-3457
  unsigned int minPort;
  unsigned int maxPort;

  // holds pair on which the client has chosen to receive media data and control information
  // it is specified as a range, e.g., client_port=3456-3457.
  unsigned int minClientPort;
  unsigned int maxClientPort;

  // holds pair on which the server has chosen to receive media data and control information
  // it is specified as a range, e.g., server_port=3456-3457. 
  unsigned int minServerPort;
  unsigned int maxServerPort;

  // holds mode
  wchar_t *mode;

  // holds ssrc (synchronization source identifier)
  unsigned int synchronizationSourceIdentifier;

  // deeply clones current instance to cloned header
  // @param  clonedHeader : cloned header to hold clone of current instance
  // @return : true if successful, false otherwise
  virtual bool CloneInternal(CHttpHeader *clonedHeader);

  // returns new RTSP request header object to be used in cloning
  // @return : RTSP request header object or NULL if error
  virtual CHttpHeader *GetNewHeader(void);
};

#endif