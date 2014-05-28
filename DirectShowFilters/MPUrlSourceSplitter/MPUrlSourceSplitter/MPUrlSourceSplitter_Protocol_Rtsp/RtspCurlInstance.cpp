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

#include "RtspCurlInstance.h"
#include "InterleavedDataPacket.h"
#include "ControlAttribute.h"
#include "formatUrl.h"
#include "RtpPacket.h"
#include "RtcpPacket.h"
#include "BaseRtpPacketFactory.h"
#include "LockMutex.h"
#include "BufferHelper.h"

#include "UdpServer.h"
#include "TcpServer.h"
#include "NetworkInterface.h"
#include "NetworkInterfaceCollection.h"
#include "UdpSocketContext.h"
#include "TcpSocketContext.h"

#include "RtspOptionsRequest.h"
#include "RtspDescribeRequest.h"
#include "RtspSetupRequest.h"
#include "RtspPlayRequest.h"
#include "RtspTeardownRequest.h"
#include "RtspGetParameterRequest.h"

#include "RtspResponse.h"
#include "RtspPublicResponseHeader.h"
#include "RtspContentTypeResponseHeader.h"
#include "RtspTransportResponseHeader.h"
#include "RtspContentBaseResponseHeader.h"
#include "RtspContentLocationResponseHeader.h"
#include "RtspSessionResponseHeader.h"

#include "SenderReportRtcpPacket.h"
#include "GoodbyeRtcpPacket.h"
#include "ReceiverReportRtcpPacket.h"
#include "SourceDescriptionRtcpPacket.h"

#include "CanonicalEndPointSourceDescriptionItem.h"

#include "RtspSupportedPayloadTypeCollection.h"

#include "hex.h"

#define METHOD_PROCESS_RECEIVED_BASE_RTP_PACKETS_NAME                 L"ProcessReceivedBaseRtpPackets()"

CRtspCurlInstance::CRtspCurlInstance(CLogger *logger, HANDLE mutex, const wchar_t *protocolName, const wchar_t *instanceName)
  : CCurlInstance(logger, mutex, protocolName, instanceName)
{
  this->rtspDownloadRequest = dynamic_cast<CRtspDownloadRequest *>(this->downloadRequest);
  this->rtspDownloadResponse = dynamic_cast<CRtspDownloadResponse *>(this->downloadResponse);

  this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_NONE;
  this->requestCommand = UINT_MAX;
  this->lastSequenceNumber = 1;
  this->sameConnectionTcpPreference = RTSP_SAME_CONNECTION_TCP_PREFERENCE_DEFAULT;
  this->multicastPreference = RTSP_MULTICAST_PREFERENCE_DEFAULT;
  this->udpPreference = RTSP_UDP_PREFERENCE_DEFAULT;
  this->sessionId = NULL;
  this->clientPortMin = RTSP_CLIENT_PORT_MIN_DEFAULT;
  this->clientPortMax = RTSP_CLIENT_PORT_MAX_DEFAULT;
  this->flags = RTSP_CURL_INSTANCE_FLAG_NONE;

  this->SetWriteCallback(CRtspCurlInstance::CurlReceiveDataCallback, this);
}

CRtspCurlInstance::~CRtspCurlInstance(void)
{
  this->StopReceivingData();

  FREE_MEM(this->sessionId);
}

/* get methods */

CRtspDownloadResponse *CRtspCurlInstance::GetRtspDownloadResponse(void)
{
  return this->rtspDownloadResponse;
}

unsigned int CRtspCurlInstance::GetSameConnectionTcpPreference(void)
{
  return this->sameConnectionTcpPreference;
}

unsigned int CRtspCurlInstance::GetMulticastPreference(void)
{
  return this->multicastPreference;
}

unsigned int CRtspCurlInstance::GetUdpPreference(void)
{
  return this->udpPreference;
}

unsigned int CRtspCurlInstance::GetRtspClientPortMin(void)
{
  return this->clientPortMin;
}

unsigned int CRtspCurlInstance::GetRtspClientPortMax(void)
{
  return this->clientPortMax;
}

/* set methods */

void CRtspCurlInstance::SetSameConnectionTcpPreference(unsigned int preference)
{
  this->sameConnectionTcpPreference = preference;
}

void CRtspCurlInstance::SetMulticastPreference(unsigned int preference)
{
  this->multicastPreference = preference;
}

void CRtspCurlInstance::SetUdpPreference(unsigned int preference)
{
  this->udpPreference = preference;
}

void CRtspCurlInstance::SetRtspClientPortMin(unsigned int clientPortMin)
{
  this->clientPortMin = clientPortMin;
}

void CRtspCurlInstance::SetRtspClientPortMax(unsigned int clientPortMax)
{
  this->clientPortMax = clientPortMax;
}

void CRtspCurlInstance::SetIgnoreRtpPayloadType(bool ignoreRtpPayloadType)
{
  this->flags &= ~RTSP_CURL_INSTANCE_FLAG_IGNORE_RTP_PAYLOAD_TYPE;
  this->flags |= (ignoreRtpPayloadType) ? RTSP_CURL_INSTANCE_FLAG_IGNORE_RTP_PAYLOAD_TYPE : RTSP_CURL_INSTANCE_FLAG_NONE;
}

/* other methods */

bool CRtspCurlInstance::IsIgnoreRtpPayloadTypeFlag(void)
{
  return this->IsSetFlags(RTSP_CURL_INSTANCE_FLAG_IGNORE_RTP_PAYLOAD_TYPE);
}

bool CRtspCurlInstance::IsSetFlags(unsigned int flags)
{
  return ((this->flags & flags) == flags);
}

bool CRtspCurlInstance::Initialize(CDownloadRequest *downloadRequest)
{
  unsigned int endTicks = (this->finishTime == FINISH_TIME_NOT_SPECIFIED) ? (GetTickCount() + this->GetReceiveDataTimeout()) : this->finishTime;

  bool result = __super::Initialize(downloadRequest);
  this->state = CURL_STATE_CREATED;

  this->lastSequenceNumber = 1;
  this->flags &= ~RTSP_CURL_INSTANCE_FLAG_REQUEST_COMMAND_FINISHED;
  this->rtspDownloadRequest = dynamic_cast<CRtspDownloadRequest  *>(this->downloadRequest);
  this->rtspDownloadResponse = dynamic_cast<CRtspDownloadResponse *>(this->downloadResponse);
  result &= (this->rtspDownloadRequest != NULL) && (this->rtspDownloadResponse != NULL);
  result &= (this->clientPortMin <= RTSP_CLIENT_PORT_MAX_DEFAULT);
  result &= (this->clientPortMax <= RTSP_CLIENT_PORT_MAX_DEFAULT);
  result &= (this->clientPortMin <= this->clientPortMax);

  if (result)
  {
    result &= (this->rtspDownloadResponse->GetRtspTracks() != NULL);
  }

  if (result)
  {
    // check tranport prefereces
    // they must have different values

    result &= (this->sameConnectionTcpPreference != this->multicastPreference);
    result &= (this->sameConnectionTcpPreference != this->udpPreference);
    result &= (this->multicastPreference != this->udpPreference);
  }

  if (result)
  {
    this->rtspDownloadResponse->GetRtspTracks()->Clear();

    // create receiving data thread
    // this add current CURL handle to multi handle
    // we avoid problem closing opened socket and creating new one
    result &= this->StartReceivingData();
  }

  if (result)
  {
    CURLcode errorCode = CURLE_OK;

    if (errorCode == CURLE_OK)
    {
      // we have own RTSP implementation, using CURL only to connect, send and receive data

      CHECK_CONDITION_EXECUTE_RESULT(errorCode == CURLE_OK, curl_easy_setopt(this->curl, CURLOPT_CONNECT_ONLY, 1L), errorCode);
      CHECK_CONDITION_EXECUTE_RESULT(errorCode == CURLE_OK, curl_easy_perform(this->curl), errorCode);

      // if no error, we successfully connected to remote server

      if (errorCode == CURLE_OK)
      {
        CRtspOptionsRequest *options = new CRtspOptionsRequest();
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (options != NULL) ? errorCode : CURLE_OUT_OF_MEMORY);
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (options->SetUri(this->rtspDownloadRequest->GetUrl())) ? errorCode : CURLE_OUT_OF_MEMORY);
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, options->SetTimeout(endTicks - GetTickCount()));
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, options->SetSequenceNumber(this->lastSequenceNumber++));

        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = this->SendAndReceive(options, errorCode, L"OPTIONS", METHOD_INITIALIZE_NAME));

        CRtspResponse *response = NULL;
        if (errorCode == CURLE_OK)
        {
          // check OPTIONS required methods
          // we need DESCRIBE, SETUP, PLAY and TEARDOWN methods
          
          {
            CLockMutex lock(this->mutex, INFINITE);

            response = this->rtspDownloadResponse->GetRtspResponse()->Clone();
            CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (response != NULL) ? errorCode : CURLE_OUT_OF_MEMORY);
          }

          if (errorCode == CURLE_OK)
          {
            CRtspPublicResponseHeader *publicHeader = (CRtspPublicResponseHeader *)response->GetResponseHeaders()->GetRtspHeader(RTSP_PUBLIC_RESPONSE_HEADER_TYPE);
            if (publicHeader == NULL)
            {
              this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"no PUBLIC RTSP OPTIONS response header");
              errorCode = CURLE_FAILED_INIT;
            }

            if (errorCode == CURLE_OK)
            {
              if (!publicHeader->IsSetFlags(RTSP_PUBLIC_RESPONSE_HEADER_FLAG_METHOD_DESCRIBE | RTSP_PUBLIC_RESPONSE_HEADER_FLAG_METHOD_SETUP | RTSP_PUBLIC_RESPONSE_HEADER_FLAG_METHOD_PLAY | RTSP_PUBLIC_RESPONSE_HEADER_FLAG_METHOD_TEARDOWN))
              {
                this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"not all required methods (DESCRIBE, SETUP, PLAY and TEARDOWN) defined in PUBLIC RTSP OPTIONS response header");
                errorCode = CURLE_FAILED_INIT;
              }

              // test for GET_PARAMETER method, we can use it to maintain connection
              this->flags |= publicHeader->IsSetFlags(RTSP_PUBLIC_RESPONSE_HEADER_FLAG_METHOD_GET_PARAMETER) ? RTSP_CURL_INSTANCE_FLAG_METHOD_GET_PARAMETER_SUPPORTED : RTSP_CURL_INSTANCE_FLAG_NONE;
            }
          }
        }

        FREE_MEM_CLASS(response);
        FREE_MEM_CLASS(options);
      }
      CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_INITIALIZE_NAME, L"error while sending RTSP OPTIONS", errorCode));
    }
    CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_OPTIONS);

    if (errorCode == CURLE_OK)
    {
      if (errorCode == CURLE_OK)
      {
        CRtspDescribeRequest *describe = new CRtspDescribeRequest();
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (describe != NULL) ? errorCode : CURLE_OUT_OF_MEMORY);
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (describe->SetUri(this->rtspDownloadRequest->GetUrl())) ? errorCode : CURLE_OUT_OF_MEMORY);
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, describe->SetTimeout(endTicks - GetTickCount()));
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, describe->SetSequenceNumber(this->lastSequenceNumber++));

        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = this->SendAndReceive(describe, errorCode, L"DESCRIBE", METHOD_INITIALIZE_NAME));

        CRtspResponse *response = NULL;
        if (errorCode == CURLE_OK)
        {
          // check content length
          {
            CLockMutex lock(this->mutex, INFINITE);

            response = this->rtspDownloadResponse->GetRtspResponse()->Clone();
            CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (response != NULL) ? errorCode : CURLE_OUT_OF_MEMORY);
          }

          if (errorCode == CURLE_OK)
          {
            if (response->GetContentLength() == 0)
            {
              this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"RTSP DESCRIBE response content length is zero");
              errorCode = CURLE_FAILED_INIT;
            }

            // check content type
            CRtspResponseHeader *contentTypeHeader = response->GetResponseHeaders()->GetRtspHeader(RTSP_CONTENT_TYPE_RESPONSE_HEADER_TYPE);
            if (contentTypeHeader == NULL)
            {
              this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"RTSP DESCRIBE response content type not found");
              errorCode = CURLE_FAILED_INIT;
            }
            else
            {
              if (wcscmp(RTSP_DESCRIBE_CONTENT_TYPE, contentTypeHeader->GetValue()) != 0)
              {
                this->logger->Log(LOGGER_ERROR, L"%s: %s: content type header '%s' not allowed, required: '%s'", this->protocolName, METHOD_INITIALIZE_NAME, contentTypeHeader->GetValue(), RTSP_DESCRIBE_CONTENT_TYPE);
                errorCode = CURLE_FAILED_INIT;
              }
            }
          }
        }

        if (errorCode == CURLE_OK)
        {
          // check DESCRIBE response, it have to be valid SDP

          unsigned int bufferSize = response->GetContentLength() + 2; // one place for null character, one place for new line character
          ALLOC_MEM_DEFINE_SET(buffer, char, bufferSize, 0);
          errorCode = ((buffer != NULL) && (bufferSize > 2)) ? errorCode : CURLE_OUT_OF_MEMORY;

          if (errorCode == CURLE_OK)
          {
            memcpy(buffer, response->GetContent(), bufferSize - 2);

            // check if we have new line character as last character in response
            // if not, then add new line character

            if ((buffer[bufferSize - 3] != '\r') && (buffer[bufferSize - 3] != '\n'))
            {
              buffer[bufferSize - 2] = '\n';
            }

            // try to log response
            wchar_t *responseW = ConvertToUnicodeA(buffer);
            errorCode = (responseW != NULL) ? errorCode : CURLE_OUT_OF_MEMORY;

            if (errorCode == CURLE_OK)
            {
              this->logger->Log(LOGGER_VERBOSE, L"%s: %s: RTSP DESCRIBE response: %s", this->protocolName, METHOD_INITIALIZE_NAME, responseW);

              errorCode = this->rtspDownloadResponse->SetRawSessionDescription(responseW) ? errorCode : CURLE_FAILED_INIT;
              errorCode = this->rtspDownloadResponse->ParseRawSessionDescription() ? errorCode : CURLE_FAILED_INIT;
            }
            FREE_MEM(responseW);
          }
          FREE_MEM(buffer);
        }

        if (errorCode == CURLE_OK)
        {
          // check content base or content location headers
          CRtspContentBaseResponseHeader *contentBase = (CRtspContentBaseResponseHeader *)response->GetResponseHeaders()->GetRtspHeader(RTSP_CONTENT_BASE_RESPONSE_HEADER_TYPE);
          CRtspContentLocationResponseHeader *contentLocation = (CRtspContentLocationResponseHeader *)response->GetResponseHeaders()->GetRtspHeader(RTSP_CONTENT_LOCATION_RESPONSE_HEADER_TYPE);

          CHECK_CONDITION_NOT_NULL_EXECUTE(contentBase, this->rtspDownloadResponse->SetContentBaseUrl(contentBase->GetUri()));
          CHECK_CONDITION_NOT_NULL_EXECUTE(contentLocation, this->rtspDownloadResponse->SetContentLocationUrl(contentLocation->GetUri()));
        }

        FREE_MEM_CLASS(response);
        FREE_MEM_CLASS(describe);
      }
      CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_INITIALIZE_NAME, L"error while sending RTSP DESCRIBE", errorCode));
    }
    CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_DESCRIBE);

    // clear buffer
    this->rtspDownloadResponse->GetReceivedData()->ClearBuffer();

    // RTSP setup depends on:
    // 1. transport preference (same connection, multicast, udp)
    // 2. SDP session tags (session control, media control, ...)

    unsigned int transportPreference = min(this->sameConnectionTcpPreference, this->multicastPreference);
    transportPreference = min(transportPreference, this->udpPreference);

    bool negotiatedTransport = false;
    CRtspSupportedPayloadTypeCollection *supportedPayloadTypes = new CRtspSupportedPayloadTypeCollection();

    while ((!negotiatedTransport) && (errorCode == CURLE_OK) && (endTicks > GetTickCount()))
    {
      CURLcode error = CURLE_OK;

      if (this->sameConnectionTcpPreference == transportPreference)
      {
        unsigned int interleavedChannel = 0;

        this->logger->Log(LOGGER_VERBOSE, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"trying to negotiate TCP transport on same connection");

        // for each media description create and negotiate TCP transport
        CURLcode error = (this->rtspDownloadResponse->GetSessionDescription()->GetMediaDescriptions()->Count() != 0) ? CURLE_OK : CURLE_FAILED_INIT;

        for (unsigned int i = 0; ((error == CURLE_OK) && (i < this->rtspDownloadResponse->GetSessionDescription()->GetMediaDescriptions()->Count())); i++)
        {
          CMediaDescription *mediaDescription = this->rtspDownloadResponse->GetSessionDescription()->GetMediaDescriptions()->GetItem(i);
          CRtspPayloadType *supportedPayloadType = NULL;

          if (mediaDescription->GetMediaFormats()->Count() == 1)
          {
            // there must be only one media format
            CMediaFormat *mediaFormat = mediaDescription->GetMediaFormats()->GetItem(0);

            for (unsigned int j = 0; ((error == CURLE_OK) && (supportedPayloadType == NULL) && (j < supportedPayloadTypes->Count())); j++)
            {
              CRtspPayloadType *payloadType = supportedPayloadTypes->GetItem(j);

              if ((mediaFormat->GetPayloadType() == payloadType->GetId()) ||
                (CompareWithNull(mediaFormat->GetName(), payloadType->GetEncodingName()) == 0))
              {
                supportedPayloadType = payloadType;
              }
            }

            if (supportedPayloadType == NULL)
            {
              this->logger->Log(LOGGER_WARNING, L"%s: %s: media description format not supported: %d, %s", this->protocolName, METHOD_INITIALIZE_NAME, mediaFormat->GetPayloadType(), (mediaFormat->GetName() == NULL) ? L"unknown" : mediaFormat->GetName());
              continue;
            }
          }
          else
          {
            this->logger->Log(LOGGER_ERROR, L"%s: %s: media description specify more media formats as allowed: '%s'", this->protocolName, METHOD_INITIALIZE_NAME, mediaDescription->GetTagContent());
            continue;
          }

          // find control attribute
          CAttributeCollection *attributes = mediaDescription->GetAttributes();
          CControlAttribute *control = NULL;
          for (unsigned int i = 0; ((control == NULL) && (i < attributes->Count())); i++)
          {
            CAttribute *attribute = attributes->GetItem(i);

            if (attribute->IsInstanceTag(TAG_ATTRIBUTE_INSTANCE_CONTROL))
            {
              control = dynamic_cast<CControlAttribute *>(attribute);
            }
          }

          wchar_t *streamUrl = NULL;
          if (control == NULL)
          {
            this->logger->Log(LOGGER_VERBOSE, L"%s: %s: no control attribute", this->protocolName, METHOD_INITIALIZE_NAME);

            const wchar_t *controlUrl = this->GetBaseUrl();

            if (IsAbsoluteUrl(controlUrl))
            {
              streamUrl = Duplicate(controlUrl);
            }

            error = (streamUrl != NULL) ? error : CURLE_FAILED_INIT;
          }
          else
          {
            // control attribute exists
            // it can be asterisk ('*') - this means stream URI
            // it can be relative or absolute URL

            const wchar_t *controlUrl = control->IsAsterisk() ? this->GetBaseUrl() : control->GetControlUrl();
            // this controlUrl can be relative or absolute
            if (IsAbsoluteUrl(controlUrl))
            {
              streamUrl = Duplicate(controlUrl);
            }
            else
            {
              streamUrl = FormatAbsoluteUrl(this->GetBaseUrl(), control->GetControlUrl());
            }

            error = (streamUrl != NULL) ? error : CURLE_OUT_OF_MEMORY;
          }

          if (error == CURLE_OK)
          {
            CRtspSetupRequest *setup = new CRtspSetupRequest();
            CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (setup != NULL) ? error : CURLE_OUT_OF_MEMORY);
            CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (setup->GetTransportRequestHeader() != NULL) ? error : CURLE_OUT_OF_MEMORY);
            CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (setup->SetUri(streamUrl)) ? error : CURLE_OUT_OF_MEMORY);
            CHECK_CONDITION_EXECUTE(error == CURLE_OK, setup->SetTimeout(endTicks - GetTickCount()));
            CHECK_CONDITION_EXECUTE(error == CURLE_OK, setup->SetSequenceNumber(this->lastSequenceNumber++));
            CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = setup->SetSessionId(this->sessionId) ? error : CURLE_OUT_OF_MEMORY);

            if (errorCode == CURLE_OK)
            {
              // setup transort header
              CRtspTransportRequestHeader *header = setup->GetTransportRequestHeader();

              header->SetTransportProtocol(RTSP_TRANSPORT_REQUEST_HEADER_PROTOCOL_RTP);
              header->SetProfile(RTSP_TRANSPORT_REQUEST_HEADER_PROFILE_AVP);
              header->SetLowerTransport(RTSP_TRANSPORT_REQUEST_HEADER_LOWER_TRANSPORT_TCP);
              header->SetMinInterleavedChannel(interleavedChannel);
              header->SetMaxInterleavedChannel(interleavedChannel + 1);
              header->SetFlags(RTSP_TRANSPORT_REQUEST_HEADER_FLAG_LOWER_TRANSPORT_TCP | RTSP_TRANSPORT_REQUEST_HEADER_FLAG_INTERLEAVED | RTSP_TRANSPORT_REQUEST_HEADER_FLAG_TRANSPORT_PROTOCOL_RTP | RTSP_TRANSPORT_REQUEST_HEADER_FLAG_PROFILE_AVP);
            }

            CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = this->SendAndReceive(setup, error, L"SETUP", METHOD_INITIALIZE_NAME));
            // if no error in SETUP request, we must call TEARDOWN to free resources
            {
              CLockMutex lock(this->mutex, INFINITE);

              // RTSP response can be NULL in case of error (e.g. timeout or wrong parameters)
              CHECK_CONDITION_EXECUTE((this->rtspDownloadResponse->GetRtspResponse() != NULL) && (this->rtspDownloadResponse->GetRtspResponse()->IsSuccess()), this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_SETUP_RESPONSE_NOT_VALID);
            }

            FREE_MEM_CLASS(setup);

            CHECK_CONDITION_EXECUTE(error != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_INITIALIZE_NAME, L"error while sending same connection TCP RTSP SETUP", error));

            CRtspResponse *response = NULL;
            if (error == CURLE_OK)
            {
              {
                CLockMutex lock(this->mutex, INFINITE);

                response = this->rtspDownloadResponse->GetRtspResponse()->Clone();
                CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (response != NULL) ? error : CURLE_OUT_OF_MEMORY);
              }

              if (error == CURLE_OK)
              {
                CRtspTransportResponseHeader *transport = (CRtspTransportResponseHeader *)response->GetResponseHeaders()->GetRtspHeader(RTSP_TRANSPORT_RESPONSE_HEADER_TYPE);

                if (transport == NULL)
                {
                  // no transport header in RTSP response
                  this->logger->Log(LOGGER_ERROR, L"%s: %s: no transport header in response", this->protocolName, METHOD_INITIALIZE_NAME);
                  error = CURLE_FAILED_INIT;
                }

                if ((error == CURLE_OK) && (!transport->IsLowerTransportTCP()))
                {
                  this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, protocolName, METHOD_INITIALIZE_NAME, L"no TCP transport header");
                  error = CURLE_FAILED_INIT;
                }

                if (error == CURLE_OK)
                {
                  this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_SETUP_RESPONSE_VALID;

                  if (!transport->IsInterleaved())
                  {
                    // client and server ports are specified, not interleaved transport
                    // only interleaved transport supported

                    this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, protocolName, METHOD_INITIALIZE_NAME, L"same connection transport not supported by server");
                    error = CURLE_FAILED_INIT;
                  }
                  else if (transport->IsInterleaved() && (!transport->IsClientPort()) && (!transport->IsServerPort()))
                  {
                    // if not set client and server ports, then transport must be interleaved
                  }
                  else
                  {
                    // bad or not implemented transport
                    this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, protocolName, METHOD_INITIALIZE_NAME, L"bad or not implemented transport");
                    error = CURLE_FAILED_INIT;
                  }
                }

                if (error == CURLE_OK)
                {
                  CRtspTrack *track = new CRtspTrack();
                  error = (track != NULL) ? error : CURLE_OUT_OF_MEMORY;

                  CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (track->SetTrackUrl(streamUrl)) ? error : CURLE_OUT_OF_MEMORY);
                  CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (track->SetTransportResponseHeader(transport)) ? error : CURLE_OUT_OF_MEMORY);

                  if (error == CURLE_OK)
                  {
                    long serverPort = 0;
                    long clientPort = 0;

                    CHECK_CONDITION_EXECUTE_RESULT(error == CURLE_OK, curl_easy_getinfo(this->curl, CURLINFO_PRIMARY_PORT, &serverPort), error);
                    CHECK_CONDITION_EXECUTE_RESULT(error == CURLE_OK, curl_easy_getinfo(this->curl, CURLINFO_LOCAL_PORT, &clientPort), error);

                    if (error == CURLE_OK)
                    {
                      track->SetClientDataPort(clientPort);
                      track->SetClientControlPort(clientPort);
                      track->SetServerDataPort(serverPort);
                      track->SetServerControlPort(serverPort);

                      track->SetLastReceiverReportTime(GetTickCount());
                      track->SetReceiverReportInterval(RECEIVER_REPORT_MIN_TIME);

                      // in specifications is not mentioned, which clock frequency have to be used when multiple media formats in SDP are defined
                      if (mediaDescription->GetMediaFormats()->GetItem(0)->GetClockRate() != MEDIA_FORMAT_CLOCK_RATE_UNSPECIFIED)
                      {
                        track->GetStatistics()->SetClockFrequency(mediaDescription->GetMediaFormats()->GetItem(0)->GetClockRate());
                      }

                      // set payload type for RTSP track
                      error = track->GetPayloadType()->CopyFromPayloadType(supportedPayloadType) ? error : CURLE_OUT_OF_MEMORY;
                      CHECK_CONDITION_EXECUTE(CompareWithNull(mediaDescription->GetMediaType(), MEDIA_DESCRIPTION_MEDIA_TYPE_AUDIO) == 0, track->GetPayloadType()->SetMediaType(CPayloadType::Audio));
                      CHECK_CONDITION_EXECUTE(CompareWithNull(mediaDescription->GetMediaType(), MEDIA_DESCRIPTION_MEDIA_TYPE_VIDEO) == 0, track->GetPayloadType()->SetMediaType(CPayloadType::Video));
                      CHECK_CONDITION_EXECUTE(mediaDescription->GetMediaFormats()->GetItem(0)->GetClockRate() != MEDIA_FORMAT_CLOCK_RATE_UNSPECIFIED, track->GetPayloadType()->SetClockRate(mediaDescription->GetMediaFormats()->GetItem(0)->GetClockRate()));
                      CHECK_CONDITION_EXECUTE(mediaDescription->GetMediaFormats()->GetItem(0)->GetChannels() != MEDIA_FORMAT_CHANNELS_UNSPECIFIED, track->GetPayloadType()->SetChannels(mediaDescription->GetMediaFormats()->GetItem(0)->GetChannels()));
                    }
                  }

                  CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = this->rtspDownloadResponse->GetRtspTracks()->Add(track) ? error : CURLE_OUT_OF_MEMORY);
                  CHECK_CONDITION_EXECUTE(error != CURLE_OK, FREE_MEM_CLASS(track));
                }
              }
            }

            FREE_MEM_CLASS(response);
          }

          FREE_MEM(streamUrl);
          interleavedChannel += 2;
        }

        CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (this->rtspDownloadResponse->GetRtspTracks()->Count() != 0) ? error : CURLE_FAILED_INIT);
        CHECK_CONDITION_EXECUTE(error == CURLE_OK, negotiatedTransport = true);
      }
      
      if (this->multicastPreference == transportPreference)
      {
        this->logger->Log(LOGGER_VERBOSE, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"trying to negotiate multicast transport");

        this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"multicast transport not implemented");
      }

      if (this->udpPreference == transportPreference)
      {
        // UDP transport

        this->logger->Log(LOGGER_VERBOSE, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"trying to negotiate udp transport");

        // for each media description create and negotiate UDP transport
        unsigned int clientPort = this->clientPortMin;
        CURLcode error = (this->rtspDownloadResponse->GetSessionDescription()->GetMediaDescriptions()->Count() != 0) ? CURLE_OK : CURLE_FAILED_INIT;

        for (unsigned int i = 0; ((error == CURLE_OK) && (i < this->rtspDownloadResponse->GetSessionDescription()->GetMediaDescriptions()->Count())); i++)
        {
          CMediaDescription *mediaDescription = this->rtspDownloadResponse->GetSessionDescription()->GetMediaDescriptions()->GetItem(i);
          CRtspPayloadType *supportedPayloadType = NULL;

          if (mediaDescription->GetMediaFormats()->Count() == 1)
          {
            // there must be only one media format
            CMediaFormat *mediaFormat = mediaDescription->GetMediaFormats()->GetItem(0);

            for (unsigned int j = 0; ((error == CURLE_OK) && (supportedPayloadType == NULL) && (j < supportedPayloadTypes->Count())); j++)
            {
              CRtspPayloadType *payloadType = supportedPayloadTypes->GetItem(j);

              if ((mediaFormat->GetPayloadType() == payloadType->GetId()) ||
                (CompareWithNull(mediaFormat->GetName(), payloadType->GetEncodingName()) == 0))
              {
                supportedPayloadType = payloadType;
              }
            }

            if (supportedPayloadType == NULL)
            {
              this->logger->Log(LOGGER_WARNING, L"%s: %s: media description format not supported: %d, %s", this->protocolName, METHOD_INITIALIZE_NAME, mediaFormat->GetPayloadType(), (mediaFormat->GetName() == NULL) ? L"unknown" : mediaFormat->GetName());
              continue;
            }
          }
          else
          {
            this->logger->Log(LOGGER_ERROR, L"%s: %s: media description specify more media formats as allowed: '%s'", this->protocolName, METHOD_INITIALIZE_NAME, mediaDescription->GetTagContent());
            continue;
          }

          // find control attribute
          CAttributeCollection *attributes = mediaDescription->GetAttributes();
          CControlAttribute *control = NULL;
          for (unsigned int i = 0; ((control == NULL) && (i < attributes->Count())); i++)
          {
            CAttribute *attribute = attributes->GetItem(i);

            if (attribute->IsInstanceTag(TAG_ATTRIBUTE_INSTANCE_CONTROL))
            {
              control = dynamic_cast<CControlAttribute *>(attribute);
            }
          }
          
          wchar_t *streamUrl = NULL;
          if (control == NULL)
          {
            this->logger->Log(LOGGER_VERBOSE, L"%s: %s: no control attribute", this->protocolName, METHOD_INITIALIZE_NAME);

            const wchar_t *controlUrl = this->GetBaseUrl();

            if (IsAbsoluteUrl(controlUrl))
            {
              streamUrl = Duplicate(controlUrl);
            }

            error = (streamUrl != NULL) ? error : CURLE_FAILED_INIT;
          }
          else
          {
            // control attribute exists
            // it can be asterisk ('*') - this means stream URI
            // it can be relative or absolute URL

            const wchar_t *controlUrl = control->IsAsterisk() ? this->GetBaseUrl() : control->GetControlUrl();
            // this controlUrl can be relative or absolute
            if (IsAbsoluteUrl(controlUrl))
            {
              streamUrl = Duplicate(controlUrl);
            }
            else
            {
              streamUrl = FormatAbsoluteUrl(this->GetBaseUrl(), control->GetControlUrl());
            }

            error = (streamUrl != NULL) ? error : CURLE_OUT_OF_MEMORY;
          }

          if (error == CURLE_OK)
          {
            CSimpleServer *dataServer = NULL;
            CSimpleServer *controlServer = NULL;

            do
            {
              error = CURLE_OK;

              dataServer = new CUdpServer();
              controlServer = new CUdpServer();

              error = ((dataServer != NULL) && (controlServer != NULL)) ? error : CURLE_OUT_OF_MEMORY;

              if (error == CURLE_OK)
              {
                if (error == CURLE_OK)
                {
                  error = SUCCEEDED(dataServer->Initialize(AF_UNSPEC, clientPort, this->networkInterfaces)) ? error : CURLE_FAILED_INIT;
                  error = SUCCEEDED(controlServer->Initialize(AF_UNSPEC, clientPort + 1, this->networkInterfaces)) ? error : CURLE_FAILED_INIT;
                }
              }

              if (error == CURLE_OK)
              {
                error = SUCCEEDED(dataServer->StartListening()) ? error : CURLE_FAILED_INIT;
                error = SUCCEEDED(controlServer->StartListening()) ? error : CURLE_FAILED_INIT;
              }

              if (error != CURLE_OK)
              {
                FREE_MEM_CLASS(dataServer);
                FREE_MEM_CLASS(controlServer);

                clientPort++;
              }
            }
            while ((error != CURLE_OK) && (clientPort <= this->clientPortMax) && (endTicks > GetTickCount()));

            CHECK_CONDITION_EXECUTE((error == CURLE_OK) && (endTicks <= GetTickCount()), error = CURLE_OPERATION_TIMEDOUT);

            if (error == CURLE_OK)
            {
              CRtspSetupRequest *setup = new CRtspSetupRequest();
              CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (setup != NULL) ? error : CURLE_OUT_OF_MEMORY);
              CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (setup->GetTransportRequestHeader() != NULL) ? error : CURLE_OUT_OF_MEMORY);
              CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (setup->SetUri(streamUrl)) ? error : CURLE_OUT_OF_MEMORY);
              CHECK_CONDITION_EXECUTE(error == CURLE_OK, setup->SetTimeout(endTicks - GetTickCount()));
              CHECK_CONDITION_EXECUTE(error == CURLE_OK, setup->SetSequenceNumber(this->lastSequenceNumber++));
              CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = setup->SetSessionId(this->sessionId) ? error : CURLE_OUT_OF_MEMORY);

              if (error == CURLE_OK)
              {
                // setup transort header
                CRtspTransportRequestHeader *header = setup->GetTransportRequestHeader();

                header->SetTransportProtocol(RTSP_TRANSPORT_REQUEST_HEADER_PROTOCOL_RTP);
                header->SetProfile(RTSP_TRANSPORT_REQUEST_HEADER_PROFILE_AVP);
                header->SetMinClientPort(clientPort);
                header->SetMaxClientPort(clientPort + 1);
                header->SetFlags(RTSP_TRANSPORT_REQUEST_HEADER_FLAG_UNICAST | RTSP_TRANSPORT_REQUEST_HEADER_FLAG_TRANSPORT_PROTOCOL_RTP | RTSP_TRANSPORT_REQUEST_HEADER_FLAG_PROFILE_AVP | RTSP_TRANSPORT_REQUEST_HEADER_FLAG_CLIENT_PORT);
              }

              CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = this->SendAndReceive(setup, error, L"SETUP", METHOD_INITIALIZE_NAME));
              // if no error in SETUP request, we must call TEARDOWN to free resources
              {
                CLockMutex lock(this->mutex, INFINITE);

                // RTSP response can be NULL in case of error (e.g. timeout or wrong parameters)
                CHECK_CONDITION_EXECUTE((this->rtspDownloadResponse->GetRtspResponse() != NULL) && (this->rtspDownloadResponse->GetRtspResponse()->IsSuccess()), this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_SETUP_RESPONSE_NOT_VALID);
              }

              FREE_MEM_CLASS(setup);
            }

            CHECK_CONDITION_EXECUTE(error != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_INITIALIZE_NAME, (this->udpPreference == transportPreference) ? L"error while sending UDP RTSP SETUP" : L"error while sending TCP RTSP SETUP", error));

            CRtspResponse *response = NULL;
            if (error == CURLE_OK)
            {
              {
                CLockMutex lock(this->mutex, INFINITE);

                response = this->rtspDownloadResponse->GetRtspResponse()->Clone();
                CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (response != NULL) ? error : CURLE_OUT_OF_MEMORY);
              }

              if (error == CURLE_OK)
              {
                CRtspTransportResponseHeader *transport = (CRtspTransportResponseHeader *)response->GetResponseHeaders()->GetRtspHeader(RTSP_TRANSPORT_RESPONSE_HEADER_TYPE);

                if (transport == NULL)
                {
                  // no transport header in RTSP response
                  this->logger->Log(LOGGER_ERROR, L"%s: %s: no transport header in response", this->protocolName, METHOD_INITIALIZE_NAME);
                  error = CURLE_FAILED_INIT;
                }

                if ((error == CURLE_OK) && (!(transport->IsLowerTransportUDP() || (transport->GetLowerTransport() == NULL))))
                {
                  this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, protocolName, METHOD_INITIALIZE_NAME, L"no UDP transport header");
                  error = CURLE_FAILED_INIT;
                }

                if (error == CURLE_OK)
                {
                  this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_SETUP_RESPONSE_VALID;

                  CRtspTrack *track = new CRtspTrack();
                  error = (track != NULL) ? error : CURLE_OUT_OF_MEMORY;

                  CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (track->SetTrackUrl(streamUrl)) ? error : CURLE_OUT_OF_MEMORY);
                  CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (track->SetTransportResponseHeader(transport)) ? error : CURLE_OUT_OF_MEMORY);

                  if (error == CURLE_OK)
                  {
                    if ((!transport->IsInterleaved()) && transport->IsClientPort() && transport->IsServerPort())
                    {
                      // client and server ports are specified, not interleaved transport

                      if ((transport->GetMinClientPort() == clientPort) && (transport->GetMaxClientPort() == (clientPort + 1)))
                      {
                        track->SetClientDataPort(transport->GetMinClientPort());
                        track->SetClientControlPort(transport->GetMaxClientPort());
                        track->SetServerDataPort(transport->GetMinServerPort());
                        track->SetServerControlPort(transport->GetMaxServerPort());

                        track->SetLastReceiverReportTime(GetTickCount());
                        track->SetReceiverReportInterval(RECEIVER_REPORT_MIN_TIME);

                        // set data and control server to track
                        track->SetDataServer(dataServer);
                        track->SetControlServer(controlServer);

                        // in case of another error will be data server and control server destroyed
                        // we must avoid this, because data server and control server are destroyed with track instance
                        dataServer = NULL;
                        controlServer = NULL;
                      }
                      else
                      {
                        this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, protocolName, METHOD_INITIALIZE_NAME, L"client ports not same as requested");
                        error = CURLE_FAILED_INIT;
                      }
                    }
                    else if (transport->IsInterleaved() && (!transport->IsClientPort()) && (!transport->IsServerPort()))
                    {
                      // if not set client and server ports, then transport must be interleaved
                      long serverPort = 0;
                      long clientPort = 0;

                      CHECK_CONDITION_EXECUTE_RESULT(error == CURLE_OK, curl_easy_getinfo(this->curl, CURLINFO_PRIMARY_PORT, &serverPort), error);
                      CHECK_CONDITION_EXECUTE_RESULT(error == CURLE_OK, curl_easy_getinfo(this->curl, CURLINFO_LOCAL_PORT, &clientPort), error);

                      if (error == CURLE_OK)
                      {
                        track->SetClientDataPort(clientPort);
                        track->SetClientControlPort(clientPort);
                        track->SetServerDataPort(serverPort);
                        track->SetServerControlPort(serverPort);

                        track->SetLastReceiverReportTime(GetTickCount());
                        track->SetReceiverReportInterval(RECEIVER_REPORT_MIN_TIME);
                      }

                      // data and control servers are not needed, destroy them
                      FREE_MEM_CLASS(dataServer);
                      FREE_MEM_CLASS(controlServer);
                    }
                    else
                    {
                      // bad or not implemented transport
                      this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, protocolName, METHOD_INITIALIZE_NAME, L"bad or not implemented transport");
                      error = CURLE_FAILED_INIT;
                    }

                    // in specifications is not mentioned, which clock frequency have to be used when multiple media formats in SDP are defined
                    if (mediaDescription->GetMediaFormats()->GetItem(0)->GetClockRate() != MEDIA_FORMAT_CLOCK_RATE_UNSPECIFIED)
                    {
                      track->GetStatistics()->SetClockFrequency(mediaDescription->GetMediaFormats()->GetItem(0)->GetClockRate());
                    }

                    // set payload type for RTSP track
                    error = track->GetPayloadType()->CopyFromPayloadType(supportedPayloadType) ? error : CURLE_OUT_OF_MEMORY;
                    CHECK_CONDITION_EXECUTE(CompareWithNull(mediaDescription->GetMediaType(), MEDIA_DESCRIPTION_MEDIA_TYPE_AUDIO) == 0, track->GetPayloadType()->SetMediaType(CPayloadType::Audio));
                    CHECK_CONDITION_EXECUTE(CompareWithNull(mediaDescription->GetMediaType(), MEDIA_DESCRIPTION_MEDIA_TYPE_VIDEO) == 0, track->GetPayloadType()->SetMediaType(CPayloadType::Video));
                    CHECK_CONDITION_EXECUTE(mediaDescription->GetMediaFormats()->GetItem(0)->GetClockRate() != MEDIA_FORMAT_CLOCK_RATE_UNSPECIFIED, track->GetPayloadType()->SetClockRate(mediaDescription->GetMediaFormats()->GetItem(0)->GetClockRate()));
                    CHECK_CONDITION_EXECUTE(mediaDescription->GetMediaFormats()->GetItem(0)->GetChannels() != MEDIA_FORMAT_CHANNELS_UNSPECIFIED, track->GetPayloadType()->SetChannels(mediaDescription->GetMediaFormats()->GetItem(0)->GetChannels()));
                  }

                  CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = this->rtspDownloadResponse->GetRtspTracks()->Add(track) ? error : CURLE_OUT_OF_MEMORY);
                  CHECK_CONDITION_EXECUTE(error != CURLE_OK, FREE_MEM_CLASS(track));
                }

                clientPort += 2;
              }
            }

            if (error != CURLE_OK)
            {
              FREE_MEM_CLASS(dataServer);
              FREE_MEM_CLASS(controlServer);
            }

            FREE_MEM_CLASS(response);
          }

          FREE_MEM(streamUrl);
        }

        CHECK_CONDITION_EXECUTE(error == CURLE_OK, error = (this->rtspDownloadResponse->GetRtspTracks()->Count() != 0) ? error : CURLE_FAILED_INIT);
        CHECK_CONDITION_EXECUTE(error == CURLE_OK, negotiatedTransport = true);
      }

      if ((!negotiatedTransport) && (errorCode == CURLE_OK))
      {
        this->rtspDownloadResponse->GetRtspTracks()->Clear();

        // select next transport by preference
        unsigned int temp = UINT_MAX;

        CHECK_CONDITION_EXECUTE_RESULT(this->sameConnectionTcpPreference > transportPreference, min(temp, this->sameConnectionTcpPreference), temp);
        CHECK_CONDITION_EXECUTE_RESULT(this->multicastPreference > transportPreference, min(temp, this->multicastPreference), temp);
        CHECK_CONDITION_EXECUTE_RESULT(this->udpPreference > transportPreference, min(temp, this->udpPreference), temp);

        transportPreference = temp;

        // if transport preference is UINT_MAX, then no other transport is available and no negotiated transport exists
        // in that case we are finished with error
        errorCode = (transportPreference != UINT_MAX) ? errorCode : CURLE_FAILED_INIT;

        if (errorCode != CURLE_OK)
        {
          this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"can't negotiate any transport");
        }
      }

      if ((!negotiatedTransport) && ((this->lastCommand == RTSP_CURL_INSTANCE_COMMAND_SETUP_RESPONSE_NOT_VALID) || (this->lastCommand == RTSP_CURL_INSTANCE_COMMAND_SETUP_RESPONSE_VALID)))
      {
        // we don't have negotiated transport and SETUP request was sent and received correctly
        // we need to call TEARDOWN request to free resources and try something else

        // create and send TEARDOWN request for stream
        errorCode = CURLE_OK;

        CRtspTeardownRequest *teardown = new CRtspTeardownRequest();
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (teardown != NULL) ? errorCode : CURLE_OUT_OF_MEMORY);
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (teardown->SetUri(this->rtspDownloadRequest->GetUrl())) ? errorCode : CURLE_OUT_OF_MEMORY);
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, teardown->SetTimeout(endTicks - GetTickCount()));
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, teardown->SetSequenceNumber(this->lastSequenceNumber++));
        CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = teardown->SetSessionId(this->sessionId) ? errorCode : CURLE_OUT_OF_MEMORY);

        if (errorCode == CURLE_OK)
        {
          // backup session ID, in case of error we need to make TEARDOWN request later
          wchar_t *sessionId = Duplicate(this->sessionId);
          errorCode = TEST_STRING_WITH_NULL(sessionId, this->sessionId) ? errorCode : CURLE_OUT_OF_MEMORY;

          CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = this->SendAndReceive(teardown, errorCode, L"TEARDOWN", METHOD_INITIALIZE_NAME));
          FREE_MEM_CLASS(teardown);

          // session ID is no longer required
          // some RTSP servers send us back session ID, which is no longer valid
          // clear it to avoid error in processing another request
          FREE_MEM(this->sessionId);

          if (errorCode != CURLE_OK)
          {
            // error occured while sending and receiving TEARDOWN request, try it later, but we can't continue now
            this->sessionId = Duplicate(sessionId);
          }
          else
          {
            this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_DESCRIBE;
          }

          FREE_MEM(sessionId);
        }
      }
    }
    FREE_MEM_CLASS(supportedPayloadTypes);
    CHECK_CONDITION_EXECUTE((errorCode == CURLE_OK) && (endTicks <= GetTickCount()), errorCode = CURLE_OPERATION_TIMEDOUT);
    
    if (errorCode == CURLE_OK)
    {
      errorCode = (this->rtspDownloadResponse->GetRtspTracks()->Count() != 0) ? errorCode : CURLE_FAILED_INIT;
      CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_INITIALIZE_NAME, L"no RTSP track to play"));
    }

    if (errorCode == CURLE_OK)
    {
      CRtspPlayRequest *play = new CRtspPlayRequest();
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (play != NULL) ? errorCode : CURLE_OUT_OF_MEMORY);
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (play->SetUri(this->rtspDownloadRequest->GetUrl())) ? errorCode : CURLE_OUT_OF_MEMORY);
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, play->SetTimeout(endTicks - GetTickCount()));
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, play->SetSequenceNumber(this->lastSequenceNumber++));
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = play->SetSessionId(this->sessionId) ? errorCode : CURLE_OUT_OF_MEMORY);
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = play->SetStartTime(this->rtspDownloadRequest->GetStartTime()) ? errorCode : CURLE_FAILED_INIT);

      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = this->SendAndReceive(play, errorCode, L"PLAY", METHOD_INITIALIZE_NAME));

      // if no error in PLAY request, we must call TEARDOWN to free resources
      {
        CLockMutex lock(this->mutex, INFINITE);

        // RTSP response can be NULL in case of error (e.g. timeout or wrong parameters)
        CHECK_CONDITION_EXECUTE((this->rtspDownloadResponse->GetRtspResponse() != NULL) && (this->rtspDownloadResponse->GetRtspResponse()->IsSuccess()), this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_PLAY_RESPONSE_NOT_VALID);

        if ((errorCode == CURLE_OK) && (this->lastCommand == RTSP_CURL_INSTANCE_COMMAND_PLAY_RESPONSE_NOT_VALID))
        {
          CRtspSessionResponseHeader *sessionHeader = (CRtspSessionResponseHeader *)this->rtspDownloadResponse->GetRtspResponse()->GetResponseHeaders()->GetRtspHeader(RTSP_SESSION_RESPONSE_HEADER_TYPE);

          this->rtspDownloadResponse->SetSessionTimeout(((sessionHeader != NULL) ? sessionHeader->GetTimeout() : RTSP_SESSION_RESPONSE_TIMEOUT_DEFAULT) * 1000);
        }

        if ((errorCode == CURLE_OK) && (this->lastCommand == RTSP_CURL_INSTANCE_COMMAND_PLAY_RESPONSE_NOT_VALID))
        {
          this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_PLAY_RESPONSE_VALID;
        }
      }

      FREE_MEM_CLASS(play);
      
      CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_INITIALIZE_NAME, L"error while sending RTSP PLAY", errorCode));
    }

    result &= (errorCode == CURLE_OK);
  }

  this->state = (result) ? CURL_STATE_INITIALIZED : CURL_STATE_CREATED;
  return result;
}

bool CRtspCurlInstance::StopReceivingData(void)
{
  if ((this->lastCommand == RTSP_CURL_INSTANCE_COMMAND_SETUP_RESPONSE_NOT_VALID) || (this->lastCommand == RTSP_CURL_INSTANCE_COMMAND_SETUP_RESPONSE_VALID) ||
    (this->lastCommand == RTSP_CURL_INSTANCE_COMMAND_PLAY_RESPONSE_NOT_VALID) || (this->lastCommand == RTSP_CURL_INSTANCE_COMMAND_PLAY_RESPONSE_VALID))
  {
    // create and send TEARDOWN request for stream
    CURLcode errorCode = CURLE_OK;

    CRtspTeardownRequest *teardown = new CRtspTeardownRequest();
    CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (teardown != NULL) ? errorCode : CURLE_OUT_OF_MEMORY);
    CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (teardown->SetUri(this->rtspDownloadRequest->GetUrl())) ? errorCode : CURLE_OUT_OF_MEMORY);
    CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, teardown->SetTimeout(this->GetReceiveDataTimeout()));
    CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, teardown->SetSequenceNumber(this->lastSequenceNumber++));
    CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = teardown->SetSessionId(this->sessionId) ? errorCode : CURLE_OUT_OF_MEMORY);

    CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = this->SendAndReceive(teardown, errorCode, L"TEARDOWN", L"StopReceivingData()"));
    FREE_MEM_CLASS(teardown);

    this->lastCommand = RTSP_CURL_INSTANCE_COMMAND_TEARDOWN;

    // session ID is no longer required
    // some RTSP servers send us back session ID, which is no longer valid
    // clear it to avoid error in processing another request
    FREE_MEM(this->sessionId);
  }

  return __super::StopReceivingData();
}

/* protected methods */

CDownloadResponse *CRtspCurlInstance::GetNewDownloadResponse(void)
{
  return new CRtspDownloadResponse();
}

bool CRtspCurlInstance::ProcessReceivedBaseRtpPackets(CRtspTrack *track, unsigned int clientPort, CBaseRtpPacketCollection *packets)
{
  // only one thread can work with RTSP tracks and responses in one time
  CLockMutex lock(this->mutex, INFINITE);

  bool result = ((track != NULL) && (packets != NULL) && (this->rtspDownloadResponse != NULL));

  if (result)
  {
    result &= (packets->Count() != 0);
    result &= (this->rtspDownloadResponse->GetRtspTracks()->Count() != 0);
  }

  if (result)
  {
    bool isDataPort = track->IsClientDataPort(clientPort);
    bool isControlPort = track->IsClientControlPort(clientPort);

    result &= (isDataPort || isControlPort);

    for (unsigned int i = 0; (result && (i < packets->Count())); i++)
    {
      CBaseRtpPacket *packet = packets->GetItem(i);
      CRtpPacket *rtpPacket = dynamic_cast<CRtpPacket *>(packet);
      CRtcpPacket *rtcpPacket = dynamic_cast<CRtcpPacket *>(packet);

      result &= ((rtpPacket != NULL) || (rtcpPacket != NULL));
      result &= ((isDataPort && (rtpPacket != NULL)) || (rtpPacket == NULL));
      result &= ((isControlPort && (rtcpPacket != NULL)) || (rtcpPacket == NULL));

      if (rtcpPacket != NULL)
      {
        // handle control packet

        CSenderReportRtcpPacket *senderReport = dynamic_cast<CSenderReportRtcpPacket *>(rtcpPacket);
        CGoodbyeRtcpPacket *goodBye = dynamic_cast<CGoodbyeRtcpPacket *>(rtcpPacket);

        if (senderReport != NULL)
        {
          track->SetSenderSynchronizationSourceIdentifier(senderReport->GetSenderSynchronizationSourceIdentifier());
          track->GetStatistics()->AdjustLastSenderReportTimestamp(senderReport->GetNtpTimestamp(), GetTickCount());
        }

        if (goodBye != NULL)
        {
          // we received good bye RTCP packet
          // stream is finished
          
          this->logger->Log(LOGGER_INFO, L"%s: %s: received GOOD BYE RTCP packet, track '%s', reason: '%s'", this->protocolName, METHOD_PROCESS_RECEIVED_BASE_RTP_PACKETS_NAME, track->GetTrackUrl(), (goodBye->GetReason() == NULL) ? L"" : goodBye->GetReason());
          track->SetEndOfStream(true);
        }
      }
      else
      {
        // check if RTP packet is for this track
        // in another case ignore RTP packet

        if ((rtpPacket->GetPayloadType() == track->GetPayloadType()->GetId()) ||
            (this->IsIgnoreRtpPayloadTypeFlag()))
        {
          // in case of RTP packet
          track->SetSenderSynchronizationSourceIdentifier(rtpPacket->GetSynchronizationSourceIdentifier());

          // adjust RTSP track statistics
          track->GetStatistics()->AdjustJitter(GetTickCount(), rtpPacket->GetTimestamp());
          track->GetStatistics()->AdjustExpectedAndLostPacketCount(rtpPacket->GetSequenceNumber());

          // add RTP packet to track RTP packets

          CRtpPacket *clone = rtpPacket->Clone();
          result &= (clone != NULL);

          CHECK_CONDITION_EXECUTE(result, result &= track->GetRtpPackets()->Add(clone));
          CHECK_CONDITION_EXECUTE(!result, FREE_MEM_CLASS(clone));
        }
      }
    }
  }

  return result;
}

const wchar_t *CRtspCurlInstance::GetBaseUrl(void)
{
  // Implementations should look for a base URL in the following order:
  // 1.     The RTSP Content-Base field
  // 2.     The RTSP Content-Location field
  // 3.     The RTSP request URL 

  const wchar_t *result = NULL;

  if ((result == NULL) && (this->rtspDownloadResponse != NULL) && (this->rtspDownloadResponse->GetContentBaseUrl() != NULL))
  {
    result = this->rtspDownloadResponse->GetContentBaseUrl();
  }

  if ((result == NULL) && (this->rtspDownloadResponse != NULL) && (this->rtspDownloadResponse->GetContentLocationUrl() != NULL))
  {
    result = this->rtspDownloadResponse->GetContentLocationUrl();
  }

  if ((result == NULL) && (this->rtspDownloadRequest != NULL))
  {
    result = this->rtspDownloadRequest->GetUrl();
  }

  return result;
}

unsigned int CRtspCurlInstance::CurlWorker(void)
{
  this->logger->Log(LOGGER_INFO, L"%s: %s: Start, url: '%s'", this->protocolName, METHOD_CURL_WORKER_NAME, this->downloadRequest->GetUrl());
  this->startReceivingTicks = GetTickCount();
  this->stopReceivingTicks = 0;
  this->totalReceivedBytes = 0;

  CURLcode errorCode = CURLE_OK;
  ALLOC_MEM_DEFINE_SET(buffer, unsigned char, BUFFER_LENGTH_DEFAULT, 0);
  CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (buffer != NULL) ? errorCode : CURLE_OUT_OF_MEMORY);
  int readData = 0;

  DWORD rtspRequestStartTicks = 0;
  DWORD rtspRequestEndTicks = 0;
  CURLcode rtspRequestErrorCode = CURLE_AGAIN;
  HRESULT rtspResponseParseResult = S_OK;
  bool externalRequest = false;

  // RTSP request and response
  CRtspRequest *rtspRequest = NULL;
  CRtspResponse *rtspResponse = NULL;

  unsigned int nextMaintainConnectionRequest = GetTickCount();

  while (!this->curlWorkerShouldExit)
  {
    // RTSP response must be NULL to start with next request/response pair
    if (rtspResponse == NULL)
    {
      CLockMutex lock(this->mutex, 10);

      if (lock.IsLocked())
      {
        if ((rtspRequest == NULL) && (this->rtspDownloadResponse->GetRtspRequest() != NULL) && (this->rtspDownloadResponse->GetRtspResponse() == NULL) && (this->rtspDownloadResponse->GetResultCode() == CURLE_AGAIN))
        {
          // it is prepared new RTSP request
          // we must send it to remote server and create new response

          rtspRequest = this->rtspDownloadResponse->GetRtspRequest()->Clone();
          CHECK_CONDITION_EXECUTE(rtspRequest == NULL, rtspRequestErrorCode = CURLE_OUT_OF_MEMORY);

          // RTSP request come from code out of this thread
          externalRequest = true;
        }

        if ((rtspRequestErrorCode == CURLE_AGAIN) && (rtspRequest != NULL))
        {
          rtspResponse = new CRtspResponse();
        
          rtspRequestStartTicks = GetTickCount();
          rtspRequestEndTicks = rtspRequestStartTicks + rtspRequest->GetTimeout();

          CHECK_CONDITION_EXECUTE(rtspResponse == NULL, rtspRequestErrorCode = CURLE_OUT_OF_MEMORY);

          if (rtspRequestErrorCode == CURLE_AGAIN)
          {
            // everything is OK, we can continue

            const wchar_t *requestW = rtspRequest->GetRequest();
            CHECK_CONDITION_EXECUTE(requestW != NULL, this->logger->Log(LOGGER_VERBOSE, L"%s: %s: RTSP request:\n%s", protocolName, METHOD_CURL_WORKER_NAME, requestW));

            char *requestA = ConvertToMultiByte(requestW);
            unsigned int requestLength = (requestA != NULL) ? strlen(requestA) : 0;
            CHECK_CONDITION_EXECUTE(requestA == NULL, rtspRequestErrorCode = CURLE_OUT_OF_MEMORY);

            if (rtspRequestErrorCode == CURLE_AGAIN)
            {
              // send data
              unsigned int receivedData = 0;
              CURLcode error = curl_easy_perform(this->curl);
              CHECK_CONDITION_EXECUTE_RESULT(error == CURLE_OK, curl_easy_send(this->curl, requestA, requestLength, &receivedData), error);

              rtspRequestErrorCode = (error == CURLE_OK) ? CURLE_AGAIN : error;
              FREE_MEM(requestA);
            }
          }
        }
      }
    }

    if ((rtspRequestStartTicks != 0) && (rtspRequestEndTicks != 0) && (GetTickCount() > rtspRequestEndTicks))
    {
      // timeout occured
      rtspRequestErrorCode = CURLE_OPERATION_TIMEDOUT;
    }

    int readData = 0;

    do
    {
      readData = this->ReadData(buffer, BUFFER_LENGTH_DEFAULT);

      if (readData < 0)
      {
        // error occured
        rtspRequestErrorCode = (CURLcode)(-readData);
      }
      else if (readData >= 0)
      {
        CLockMutex lock(this->mutex, INFINITE);
        CURLcode error = CURLE_OK;

        if (readData > 0)
        {
          error = (this->rtspDownloadResponse->GetReceivedData()->AddToBufferWithResize(buffer, readData, this->rtspDownloadResponse->GetReceivedData()->GetBufferSize() * 2) == readData) ? CURLE_OK : CURLE_OUT_OF_MEMORY;
          CHECK_CONDITION_EXECUTE(error != CURLE_OK, this->rtspDownloadResponse->SetResultCode(error));
        }

        bool possibleInterleavedPacket = false;

        if ((rtspRequest != NULL) && (rtspResponse != NULL))
        {
          if ((error == CURLE_OK) && (rtspRequestErrorCode == CURLE_AGAIN))
          {
            // we have pending RTSP request
            // response can be for that RTSP request or data can be interleaved packet

            unsigned int bufferSize = this->rtspDownloadResponse->GetReceivedData()->GetBufferOccupiedSpace();

            if (bufferSize > 0)
            {
              ALLOC_MEM_DEFINE_SET(temp, unsigned char, (bufferSize + 1), 0);

              if (temp == NULL)
              {
                error = CURLE_OUT_OF_MEMORY;
                rtspRequestErrorCode = error;
              }

              if (error == CURLE_OK)
              {
                this->rtspDownloadResponse->GetReceivedData()->CopyFromBuffer(temp, bufferSize);
                HRESULT parseResult = rtspResponse->Parse(temp, bufferSize);

                if (parseResult > 0)
                {
                  // received valid RTSP response
                  char *responseA = SubstringA((char *)temp, 0, parseResult);
                  if (responseA != NULL)
                  {
                    wchar_t *responseW = ConvertToUnicodeA(responseA);
                    if (responseW != NULL)
                    {
                      this->logger->Log(LOGGER_VERBOSE, L"%s: %s: RTSP response:\n%s", protocolName, METHOD_CURL_WORKER_NAME, responseW);
                    }
                    FREE_MEM(responseW);
                  }
                  FREE_MEM(responseA);

                  rtspRequestErrorCode = CURLE_OK;
                  this->rtspDownloadResponse->GetReceivedData()->RemoveFromBufferAndMove(parseResult);
                }
                else if (parseResult == 0)
                {
                  // no RTSP response in buffer => possible interleaved packet
                  possibleInterleavedPacket = true;
                }
                else if (parseResult == HRESULT_FROM_WIN32(ERROR_MORE_DATA))
                {
                  // wait for more data
                }
                else if (FAILED(parseResult))
                {
                  // error

                  wchar_t *responseW = ConvertToUnicodeA((char *)temp);
                  if (responseW != NULL)
                  {
                    this->logger->Log(LOGGER_WARNING, L"%s: %s: RTSP response:\n%s", protocolName, METHOD_CURL_WORKER_NAME, responseW);
                  }
                  FREE_MEM(responseW);

                  rtspRequestErrorCode = CURLE_RECV_ERROR;
                  rtspResponseParseResult = parseResult;
                }
              }
              FREE_MEM(temp);
            }
          }
        }

        if ((errorCode == CURLE_OK) && (possibleInterleavedPacket || ((rtspRequest == NULL) && (rtspResponse == NULL))))
        {
          CInterleavedDataPacket *packet = new CInterleavedDataPacket();
          CHECK_CONDITION_EXECUTE(packet == NULL, errorCode = CURLE_OUT_OF_MEMORY);

          unsigned int bufferLength = this->rtspDownloadResponse->GetReceivedData()->GetBufferOccupiedSpace();
          ALLOC_MEM_DEFINE_SET(temp, unsigned char, bufferLength, 0);
          CHECK_CONDITION_EXECUTE(temp == NULL, errorCode = CURLE_OUT_OF_MEMORY);

          if (errorCode == CURLE_OK)
          {
            this->rtspDownloadResponse->GetReceivedData()->CopyFromBuffer(temp, bufferLength);

            unsigned int processed = 0;

            while ((errorCode == CURLE_OK) && (processed < bufferLength))
            {
              HRESULT parseResult = packet->Parse(temp + processed, bufferLength - processed);

              if (parseResult > 0)
              {
                CRtspTrack *track = NULL;

                // for current case we need to search for RTSP track by channel identifier
                for (unsigned int i = 0; i < this->rtspDownloadResponse->GetRtspTracks()->Count(); i++)
                {
                  CRtspTrack *tempTrack = this->rtspDownloadResponse->GetRtspTracks()->GetItem(i);

                  if ((tempTrack->GetTransportResponseHeader()->GetMinInterleavedChannel() == packet->GetChannelIdentifier()) ||
                    (tempTrack->GetTransportResponseHeader()->GetMaxInterleavedChannel() == packet->GetChannelIdentifier()))
                  {
                    track = tempTrack;
                    break;
                  }
                }

                if (track != NULL)
                {
                  // process received interleaved packet
                  errorCode = this->ProcessReceivedBaseRtpPackets(track, track->GetClientControlPort(), packet->GetBaseRtpPackets()) ? errorCode : CURLE_RECV_ERROR;
                }

                this->rtspDownloadResponse->GetReceivedData()->RemoveFromBufferAndMove(parseResult);
                processed += parseResult;
              }
              else if ((parseResult == 0) && (processed == 0))
              {
                // if processed is not zero, then we processed some packet(s)
                // next data can be RTSP response

                // not interleaved packet and also not valid RTSP response
                this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, protocolName, METHOD_CURL_WORKER_NAME, L"not interleaved packet and also not valid RTSP response");
                errorCode = CURLE_RECV_ERROR;
              }
              else if ((parseResult == 0) && (processed != 0))
              {
                // next data can be RTSP response
                processed = bufferLength;
              }
              else if (parseResult == HRESULT_FROM_WIN32(ERROR_MORE_DATA))
              {
                // wait for more data
                processed = bufferLength;
              }
              else if (FAILED(parseResult))
              {
                // error
                this->logger->Log(LOGGER_ERROR, L"%s: %s: error while parsing interleaved packet data: 0x%08X", protocolName, METHOD_CURL_WORKER_NAME, parseResult);
                errorCode = CURLE_RECV_ERROR;
              }
            }
          }

          FREE_MEM(temp);
          FREE_MEM_CLASS(packet);
        }
      }
    }
    while ((errorCode == CURLE_OK) && (readData > 0));
    // repeat until error or until we are receiving data

    if (rtspRequestErrorCode != CURLE_AGAIN)
    {
      // request finished (successfully or not, doesn't matter)

      {
        CLockMutex lock(this->mutex, INFINITE);

        if (externalRequest)
        {
          // RTSP request was generated by external code
          // report result

          if (rtspResponse != NULL)
          {
            this->rtspDownloadResponse->SetRtspResponse(rtspResponse);
            this->rtspDownloadResponse->SetResponseCode(SUCCEEDED(rtspResponseParseResult) ? rtspResponse->GetStatusCode() : rtspResponseParseResult);
          }

          this->rtspDownloadResponse->SetResultCode(rtspRequestErrorCode);
        }
      }

      externalRequest = false;
      rtspResponseParseResult = S_OK;
      rtspRequestStartTicks = 0;
      rtspRequestEndTicks = 0;
      rtspRequestErrorCode = CURLE_AGAIN;
      FREE_MEM_CLASS(rtspRequest);
      FREE_MEM_CLASS(rtspResponse);
    }

    if ((errorCode == CURLE_OK) && (this->IsSetFlags(RTSP_CURL_INSTANCE_FLAG_METHOD_GET_PARAMETER_SUPPORTED)) && (this->lastCommand == RTSP_CURL_INSTANCE_COMMAND_PLAY_RESPONSE_VALID) && (rtspRequest == NULL) && (GetTickCount() > nextMaintainConnectionRequest))
    {
      // create GET_PARAMETER request to maintain connection alive
      CRtspGetParameterRequest *getParameter = new CRtspGetParameterRequest();
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (getParameter != NULL) ? errorCode : CURLE_OUT_OF_MEMORY);
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = (getParameter->SetUri(this->rtspDownloadRequest->GetUrl())) ? errorCode : CURLE_OUT_OF_MEMORY);
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, getParameter->SetTimeout(this->receiveDataTimeout));
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, getParameter->SetSequenceNumber(this->lastSequenceNumber++));
      CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = getParameter->SetSessionId(this->sessionId) ? errorCode : CURLE_OUT_OF_MEMORY);

      nextMaintainConnectionRequest = GetTickCount() + this->rtspDownloadResponse->GetSessionTimeout() / 2;
      
      CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, FREE_MEM_CLASS(getParameter));
      rtspRequest = getParameter;
    }

    if (errorCode == CURLE_OK)
    {
      // only one thread can work with RTSP tracks and responses in one time
      CLockMutex lock(this->mutex, INFINITE);

      for (unsigned int i = 0; ((errorCode == CURLE_OK) && (i < this->rtspDownloadResponse->GetRtspTracks()->Count())); i++)
      {
        CRtspTrack *track = this->rtspDownloadResponse->GetRtspTracks()->GetItem(i);

        CSimpleServer *dataServer = track->GetDataServer(); 
        CSimpleServer *controlServer = track->GetControlServer(); 

        if ((dataServer != NULL) && (controlServer != NULL))
        {
          if (dataServer->IsServerType(SERVER_TYPE_UDP) && controlServer->IsServerType(SERVER_TYPE_UDP))
          {
            for (unsigned int k = 0; k < 2; k++)
            {
              CUdpServer *server = (k == 0) ? (CUdpServer *)dataServer : (CUdpServer *)controlServer;

              // check only data servers, check only in case of not consolidated servers (not consolidated servers have more contexts)
              if ((k == 0) && (dataServer->GetServers()->Count() != 1))
              {
                for (unsigned int j = 0; ((errorCode == CURLE_OK) && (j < dataServer->GetServers()->Count())); j++)
                {
                  CUdpSocketContext *udpContext = (CUdpSocketContext *)(dataServer->GetServers()->GetItem(j));
                  unsigned int pendingIncomingDataLength = 0;

                  if (SUCCEEDED(udpContext->GetPendingIncomingDataLength(&pendingIncomingDataLength)))
                  {
                    if (pendingIncomingDataLength != 0)
                    {
                      // found data server context with some pending incoming data
                      // consolidate data and control server contexts

                      // remove all data and control server contexts except 'j'
                      // server contexts are created in same order for data and control servers
                      unsigned int count = dataServer->GetServers()->Count() - 1;
                      for (unsigned int m = 0; m < (count - j); m++)
                      {
                        dataServer->GetServers()->Remove(dataServer->GetServers()->Count() - 1);
                        controlServer->GetServers()->Remove(controlServer->GetServers()->Count() - 1);
                      }
                      for (unsigned int m = 0; m < j; m++)
                      {
                        dataServer->GetServers()->Remove(0);
                        controlServer->GetServers()->Remove(0);
                      }
                      break;
                    }
                  }
                }
              }

              for (unsigned int j = 0; ((errorCode == CURLE_OK) && (j < server->GetServers()->Count())); j++)
              {
                CUdpSocketContext *udpContext = (CUdpSocketContext *)(server->GetServers()->GetItem(j));

                unsigned int pendingIncomingDataLength = 0;
                unsigned int interleavedPacketLength = 0;
                HRESULT result = S_OK;

                do
                {
                  result = udpContext->GetPendingIncomingDataLength(&pendingIncomingDataLength);

                  if (SUCCEEDED(result) && (pendingIncomingDataLength != 0))
                  {
                    // allocate buffer and receive data
                    // increase pending incoming data by 4 (interleaved packet header)
                    interleavedPacketLength = pendingIncomingDataLength + 4;
                    CHECK_CONDITION_EXECUTE(interleavedPacketLength > INTERLEAVED_MAX_PACKET_LENGTH, result = E_OUTOFMEMORY);

                    if (SUCCEEDED(result))
                    {
                      ALLOC_MEM_DEFINE_SET(buffer, unsigned char, interleavedPacketLength, 0);
                      CHECK_POINTER_HRESULT(result, buffer, result, E_OUTOFMEMORY);

                      unsigned int receivedLength = 0;
                      CIpAddress *sender = NULL;

                      CHECK_CONDITION_EXECUTE(SUCCEEDED(result), result = udpContext->Receive((char *)(buffer + 4), pendingIncomingDataLength, &receivedLength, &sender));
                      CHECK_CONDITION_EXECUTE(SUCCEEDED(result), result = (pendingIncomingDataLength == receivedLength) ? result : E_NOT_VALID_STATE);
                      CHECK_POINTER_HRESULT(result, sender, result, E_OUTOFMEMORY);

                      // for created server it can't be interleaved transport
                      // create interleaved packet, parse it and process base RTP packet collection

                      if (SUCCEEDED(result))
                      {
                        buffer[0] = INTERLEAVED_PACKET_HEADER_IDENTIFIER;   // interleaved packet identifier
                        buffer[1] = 0x00;                                   // interleaved packet channnel (value doesn't matter)
                        WBE16(buffer, 2, pendingIncomingDataLength);        // interleaved packet length (without header length)
                      }

                      CInterleavedDataPacket *packet = new CInterleavedDataPacket();
                      CHECK_POINTER_HRESULT(result, packet, result, E_OUTOFMEMORY);

                      if (SUCCEEDED(result))
                      {
                        CHECK_CONDITION_EXECUTE(SUCCEEDED(result), result = packet->Parse(buffer, interleavedPacketLength) ? result : E_FAIL);
                        CHECK_CONDITION_EXECUTE(SUCCEEDED(result), result = this->ProcessReceivedBaseRtpPackets(track, udpContext->GetIpAddress()->GetPort(), packet->GetBaseRtpPackets()) ? result : E_FAIL);

                        if (FAILED(result))
                        {
                          // error while processing received packet, dump it for further analysis

                          // every byte is in HEX encoding plus space
                          // every 32 bytes is new line
                          // add one character for null terminating character
                          unsigned int dumpPacketLength = interleavedPacketLength * 3 + ((interleavedPacketLength / 32) + 1) * 2 + 1;
                          ALLOC_MEM_DEFINE_SET(dumpPacket, wchar_t, dumpPacketLength, 0);
                          
                          if (dumpPacket != NULL)
                          {
                            unsigned int outputPosition = 0;
                            for (unsigned int i = 0; i < interleavedPacketLength; i++)
                            {
                              dumpPacket[outputPosition++] = get_charW(buffer[i] >> 4);
                              dumpPacket[outputPosition++] = get_charW(buffer[i] & 0x0F);
                              dumpPacket[outputPosition++] = L' ';
                              
                              if ((i % 32) == 0x1F)
                              {
                                dumpPacket[outputPosition++] = L'\r';
                                dumpPacket[outputPosition++] = L'\n';
                              }
                            }
                          }

                          this->logger->Log(LOGGER_ERROR, L"%s: %s: error while processing packet, track url: '%s', IP: %s, server IP: %s, length: %u\n%s",
                            protocolName, METHOD_CURL_WORKER_NAME,
                            track->GetTrackUrl(),
                            udpContext->GetIpAddress()->GetAddressString(),
                            sender->GetAddressString(),
                            interleavedPacketLength,
                            dumpPacket);

                          FREE_MEM(dumpPacket);
                        }
                      }

                      FREE_MEM_CLASS(packet);
                      FREE_MEM(buffer);
                      FREE_MEM_CLASS(sender);
                    }
                  }

                  if (result == HRESULT_FROM_WIN32(WSAECONNRESET))
                  {
                    // this is special case, it means that we are sending mostly RTCP packets to remote server, but remote server isn't listening
                    // we need to read data from UDP context to release error

                    ALLOC_MEM_DEFINE_SET(buffer, unsigned char, 1024, 0);
                    CHECK_POINTER_HRESULT(result, buffer, result, E_OUTOFMEMORY);

                    // ignore return code, it's always error
                    unsigned int readData = 0;
                    udpContext->Receive((char *)buffer, 1024, &readData);

                    FREE_MEM(buffer);
                    result = S_OK;
                  }
                }
                while (SUCCEEDED(result) && (pendingIncomingDataLength != 0));

                if (FAILED(result))
                {
                  this->logger->Log(LOGGER_ERROR, L"%s: %s: error while receiving data: 0x%08X", this->protocolName, METHOD_CURL_WORKER_NAME, result);
                  errorCode = CURLE_READ_ERROR;
                }
              }
            }
          }
        }
      }
    }

    if (errorCode == CURLE_OK)
    {
      // only one thread can work with RTSP tracks and responses in one time
      CLockMutex lock(this->mutex, INFINITE);

      bool endOfStreamReached = (this->rtspDownloadResponse->GetRtspTracks()->Count() != 0);
      for (unsigned int i = 0; ((errorCode == CURLE_OK) && (i < this->rtspDownloadResponse->GetRtspTracks()->Count())); i++)
      {
        CRtspTrack *track = this->rtspDownloadResponse->GetRtspTracks()->GetItem(i);

        // check end of stream state by checking previous and last received packet count
        if ((!track->IsEndOfStream()) && ((GetTickCount() - track->GetLastReceiverReportTime()) > track->GetReceiverReportInterval()))
        {
          if (track->GetStatistics()->IsSetSequenceNumber() && 
            (track->GetStatistics()->GetLastReceivedPacketCount() == track->GetStatistics()->GetPreviousLastReceivedPacketCount()) &&
            (track->GetStatistics()->GetLastReceivedPacketCount() != 0))
          {
            // previous and last state are same, probably end of stream
            track->SetEndOfStream(true);

            this->logger->Log(LOGGER_INFO, L"%s: %s: track '%s', no data received between receiver reports", this->protocolName, METHOD_CURL_WORKER_NAME, track->GetTrackUrl());
          }
        }

        // check last receiver report time and interval
        // if needed, send receiver report for track and set last receiver report
        // there's no need to send receiver report if end of stream is reached
        if ((!track->IsEndOfStream()) && ((GetTickCount() - track->GetLastReceiverReportTime()) > track->GetReceiverReportInterval()))
        {
          CReceiverReportRtcpPacket *receiverReport = new CReceiverReportRtcpPacket();
          CSourceDescriptionRtcpPacket *sourceDescription = new CSourceDescriptionRtcpPacket();

          errorCode = (receiverReport != NULL) ? errorCode : CURLE_OUT_OF_MEMORY;
          errorCode = (sourceDescription != NULL) ? errorCode : CURLE_OUT_OF_MEMORY;
          // we must have sender SSRC to send report
          errorCode = track->IsSetSenderSynchronizationSourceIdentifier() ? errorCode : CURLE_SEND_ERROR;

          CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_CURL_WORKER_NAME, L"error while initializing receiver report and source description RTCP packets", errorCode));

          if (errorCode == CURLE_OK)
          {
            // fill receiver report RTCP packet with data
            receiverReport->SetSenderSynchronizationSourceIdentifier(track->GetSynchronizationSourceIdentifier());

            CReportBlock *reportBlock = new CReportBlock();
            errorCode = (receiverReport != NULL) ? errorCode : CURLE_OUT_OF_MEMORY;

            if (errorCode == CURLE_OK)
            {
              // set SSRC of server track
              reportBlock->SetSynchronizationSourceIdentifier(track->GetSenderSynchronizationSourceIdentifier());

              // set statistics of track
              reportBlock->SetFractionLost(track->GetStatistics()->GetFractionLost());
              reportBlock->SetCumulativeNumberOfPacketsLost(track->GetStatistics()->GetCumulativePacketLostCount());
              reportBlock->SetExtendedHighestSequenceNumberReceived(track->GetStatistics()->GetExtendedHighestSequenceNumberReceived());
              reportBlock->SetInterarrivalJitter(track->GetStatistics()->GetJitter());
              reportBlock->SetLastSenderReport(track->GetStatistics()->GetLastSenderReportTimestamp());
              reportBlock->SetDelaySinceLastSenderReport(track->GetStatistics()->GetDelaySinceLastSenderReport(GetTickCount()));

              errorCode = receiverReport->GetReportBlocks()->Add(reportBlock) ? errorCode : CURLE_OUT_OF_MEMORY;
            }

            if (errorCode != CURLE_OK)
            {
              FREE_MEM_CLASS(reportBlock);
              this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_CURL_WORKER_NAME, L"error while setting properties of receiver report RTCP packet", errorCode);
            }
          }

          if (errorCode == CURLE_OK)
          {
            // fill source description RTCP packet with data
            CSourceDescriptionChunk *chunk = new CSourceDescriptionChunk();
            errorCode = (chunk != NULL) ? errorCode : CURLE_OUT_OF_MEMORY;

            if (errorCode == CURLE_OK)
            {
              chunk->SetIdentifier(track->GetSynchronizationSourceIdentifier());

              CCanonicalEndPointSourceDescriptionItem *endPoint = new CCanonicalEndPointSourceDescriptionItem();
              errorCode = (chunk != NULL) ? errorCode : CURLE_OUT_OF_MEMORY;

              if (errorCode == CURLE_OK)
              {
                ALLOC_MEM_DEFINE_SET(computerName, wchar_t, (MAX_COMPUTERNAME_LENGTH + 1), 0);
                errorCode = (computerName != NULL) ? errorCode : CURLE_OUT_OF_MEMORY;

                if (errorCode == CURLE_OK)
                {
                  DWORD length = MAX_COMPUTERNAME_LENGTH + 1;
                  errorCode = GetComputerName(computerName, &length) ? errorCode : CURLE_OUT_OF_MEMORY;

                  if (errorCode == CURLE_OK)
                  {
                    errorCode = endPoint->SetEndPointName(computerName) ? errorCode : CURLE_OUT_OF_MEMORY;
                    errorCode = chunk->GetItems()->Add(endPoint) ? errorCode : CURLE_OUT_OF_MEMORY;
                  }
                }
                FREE_MEM(computerName);
              }

              CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, FREE_MEM_CLASS(endPoint));

              errorCode = sourceDescription->GetChunks()->Add(chunk) ? errorCode : CURLE_OUT_OF_MEMORY;
            }

            if (errorCode != CURLE_OK)
            {
              FREE_MEM_CLASS(chunk);
              this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_CURL_WORKER_NAME, L"error while setting properties of source description RTCP packet", errorCode);
            }
          }

          if (errorCode == CURLE_OK)
          {
            unsigned int receiverReportSize = receiverReport->GetSize();
            unsigned int sourceDescriptionSize = sourceDescription->GetSize();

            // allocate four more bytes - in case that we need to create interlaved packet
            ALLOC_MEM_DEFINE_SET(reportBuffer, unsigned char, (receiverReportSize + sourceDescriptionSize + 4), 0);
            errorCode = (reportBuffer != NULL) ? errorCode : CURLE_OUT_OF_MEMORY;

            CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = receiverReport->GetPacket(reportBuffer + 4, receiverReportSize) ? errorCode : CURLE_SEND_ERROR);
            CHECK_CONDITION_EXECUTE(errorCode == CURLE_OK, errorCode = sourceDescription->GetPacket(reportBuffer + 4 + receiverReportSize, sourceDescriptionSize) ? errorCode : CURLE_SEND_ERROR);

            CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_CURL_WORKER_NAME, L"error while serializing receiver report and source description RTCP packets", errorCode));

            if (errorCode == CURLE_OK)
            {
              // send report buffer to remote server
              CSimpleServer *controlServer = track->GetControlServer(); 

              if (controlServer != NULL)
              {
                // track has control server

                CUdpServer *udpControlServer = dynamic_cast<CUdpServer *>(controlServer);

                if (udpControlServer != NULL)
                {
                  if (udpControlServer->GetServers()->Count() != 0)
                  {
                    CUdpSocketContext *udpContext = NULL;

                    // get control UDP socket context with set last sender IP address
                    for (unsigned int i = 0; i < udpControlServer->GetServers()->Count(); i++)
                    {
                      CUdpSocketContext *server = (CUdpSocketContext *)udpControlServer->GetServers()->GetItem(i);

                      if (server->GetLastSenderIpAddress() != NULL)
                      {
                        udpContext = server;
                        break;
                      }
                    }

                    if (udpContext == NULL)
                    {
                      this->logger->Log(LOGGER_VERBOSE, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_CURL_WORKER_NAME, L"no RTCP control packets received");

                      // it's bad, but not critical
                      // we don't received any RTCP packet on control server
                      // so we don't know on which interface and to which client we have to send our report

                      // we need to find data server, which receives data and with its information we send our report

                      CUdpServer *udpDataServer = dynamic_cast<CUdpServer *>(track->GetDataServer());

                      if (udpDataServer != NULL)
                      {
                        CUdpSocketContext *udpDataServerContext = NULL;

                        for (unsigned int i = 0; i < udpDataServer->GetServers()->Count(); i++)
                        {
                          CUdpSocketContext *server = (CUdpSocketContext *)udpDataServer->GetServers()->GetItem(i);

                          if (server->GetLastSenderIpAddress() != NULL)
                          {
                            udpDataServerContext = server;
                            break;
                          }
                        }

                        CHECK_CONDITION_EXECUTE(udpDataServerContext == NULL, this->logger->Log(LOGGER_WARNING, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_CURL_WORKER_NAME, L"no RTP data packets received"));

                        if (udpDataServerContext != NULL)
                        {
                          // we have network interface with last sender
                          // we need to use same network interface and same last sender

                          CIpAddress *ipAddress = udpDataServerContext->GetIpAddress()->Clone();
                          CHECK_CONDITION_EXECUTE(ipAddress == NULL, this->logger->Log(LOGGER_WARNING, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_CURL_WORKER_NAME, L"cannot get IP address from data server"));
                          if (ipAddress != NULL)
                          {
                            // set port to control port
                            ipAddress->SetPort(udpControlServer->GetServers()->GetItem(0)->GetIpAddress()->GetPort());

                            // get control UDP socket context with same IP address and port
                            for (unsigned int i = 0; i < udpControlServer->GetServers()->Count(); i++)
                            {
                              CUdpSocketContext *server = (CUdpSocketContext *)udpControlServer->GetServers()->GetItem(i);

                              if (server->GetIpAddress()->GetAddressLength() == ipAddress->GetAddressLength())
                              {
                                if (memcmp(server->GetIpAddress()->GetAddress(), ipAddress->GetAddress(), ipAddress->GetAddressLength()) == 0)
                                {
                                  // IP addresses are same
                                  udpContext = server;
                                  break;
                                }
                              }
                            }

                            CHECK_CONDITION_EXECUTE(udpContext == NULL, this->logger->Log(LOGGER_WARNING, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_CURL_WORKER_NAME, L"cannot find control server with same IP address as data server"));

                            if (udpContext != NULL)
                            {
                              // get last sender IP address and change its port to sender control port
                              CIpAddress *lastSenderIpAddress = udpDataServerContext->GetLastSenderIpAddress()->Clone();

                              if (lastSenderIpAddress != NULL)
                              {
                                lastSenderIpAddress->SetPort(track->GetServerControlPort());

                                // set last sender IP address to control server
                                udpContext = udpContext->SetLastSenderIpAddress(lastSenderIpAddress) ? udpContext : NULL;

                                CHECK_CONDITION_EXECUTE(udpContext == NULL, this->logger->Log(LOGGER_WARNING, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_CURL_WORKER_NAME, L"cannot set control server last sender IP address"));
                              }

                              FREE_MEM_CLASS(lastSenderIpAddress);
                            }
                          }

                          FREE_MEM_CLASS(ipAddress);
                        }
                      }
                    }

                    errorCode = (udpContext != NULL) ? errorCode : CURLE_SEND_ERROR;
                    CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->logger->Log(LOGGER_ERROR, METHOD_MESSAGE_FORMAT, this->protocolName, METHOD_CURL_WORKER_NAME, L"no endpoint found"));

                    if (errorCode == CURLE_OK)
                    {
                      // for sure, set server control port, some servers sent data/control packets on another ports as they negotiated
                      udpContext->GetLastSenderIpAddress()->SetPort(track->GetServerControlPort());

                      unsigned int sentLength = 0;
                      errorCode = SUCCEEDED(udpContext->Send((const char *)(reportBuffer + 4), receiverReportSize + sourceDescriptionSize, &sentLength)) ? errorCode : CURLE_SEND_ERROR;
                      errorCode = (sentLength == (receiverReportSize + sourceDescriptionSize)) ? errorCode : CURLE_SEND_ERROR;

                      CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_CURL_WORKER_NAME, L"error while sending RTCP packets", errorCode));
                    }
                  }
                }
              }
              else
              {
                // track hasn't control server = it's interleaved transport
                // in that case we send receiver report packet through CURL instance

                // but first we need to create interleaved packet
                reportBuffer[0] = INTERLEAVED_PACKET_HEADER_IDENTIFIER;                             // interleaved packet identifier
                reportBuffer[1] = track->GetTransportResponseHeader()->GetMaxInterleavedChannel();  // interleaved packet channnel (specified in transport header)
                WBE16(reportBuffer, 2, (receiverReportSize + sourceDescriptionSize));               // interleaved packet length (without header length)

                errorCode = this->SendData(reportBuffer, receiverReportSize + sourceDescriptionSize + 4, this->GetReceiveDataTimeout() * 1000);
                CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_CURL_WORKER_NAME, L"error while sending interleaved RTCP packets", errorCode));
              }

              track->SetLastReceiverReportTime(GetTickCount());
            }

            FREE_MEM(reportBuffer);
          }

          FREE_MEM_CLASS(receiverReport);
          FREE_MEM_CLASS(sourceDescription);
        }

        endOfStreamReached &= track->IsEndOfStream();
      }

      if (endOfStreamReached && (this->state != CURL_STATE_RECEIVED_ALL_DATA) && (rtspRequest == NULL))
      {
        // we have end of stream on all tracks, we can't do more
        // report error code and wait for destroying CURL instance

        CLockMutex lock(this->mutex, INFINITE);

        this->rtspDownloadResponse->SetResultCode(errorCode);
        this->state = CURL_STATE_RECEIVED_ALL_DATA;
      }
    }

    if ((errorCode != CURLE_OK) && (this->state != CURL_STATE_RECEIVED_ALL_DATA))
    {
      // we have some error, we can't do more
      // report error code and wait for destroying CURL instance

      CLockMutex lock(this->mutex, INFINITE);

      this->rtspDownloadResponse->SetResultCode(errorCode);
      this->state = CURL_STATE_RECEIVED_ALL_DATA;
    }

    Sleep(1);
  }

  FREE_MEM(buffer);

  CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, METHOD_CURL_WORKER_NAME, L"error while sending, receiving or processing data", errorCode));
  this->rtspDownloadResponse->SetResultCode(errorCode);

  {
    CLockMutex lock(this->mutex, INFINITE);

    for (unsigned int i = 0; (i < this->rtspDownloadResponse->GetRtspTracks()->Count()); i++)
    {
      CRtspTrack *track = this->rtspDownloadResponse->GetRtspTracks()->GetItem(i);

      CSimpleServer *dataServer = track->GetDataServer(); 
      CSimpleServer *controlServer = track->GetControlServer(); 

      if ((dataServer != NULL) && (controlServer != NULL))
      {
        for (unsigned int k = 0; k < 2; k++)
        {
          CSimpleServer *server = (k == 0) ? dataServer : controlServer;

          for (unsigned int j = 0; (j < server->GetServers()->Count()); j++)
          {
            CSocketContext *context = server->GetServers()->GetItem(j);

            this->logger->Log(LOGGER_VERBOSE, L"%s: %s: address: %s, received bytes: %lld, sent bytes: %lld", this->protocolName, METHOD_CURL_WORKER_NAME, (context->GetIpAddress()->GetAddressString() == NULL) ? L"unknown" : context->GetIpAddress()->GetAddressString(), context->GetReceivedDataLength(), context->GetSentDataLength());
          }
        }
      }
    }
  }

  this->state = CURL_STATE_RECEIVED_ALL_DATA;
  this->stopReceivingTicks = GetTickCount();

  this->logger->Log(LOGGER_INFO, METHOD_END_FORMAT, this->protocolName, METHOD_CURL_WORKER_NAME);
  return S_OK;
}

CURLcode CRtspCurlInstance::SendAndReceive(CRtspRequest *request, CURLcode errorCode, const wchar_t *rtspMethodName, const wchar_t *functionName)
{
  if (errorCode == CURLE_OK)
  {
    // check sequence number
    unsigned int requestSequenceNumber = request->GetSequenceNumber();

    if ((errorCode == CURLE_OK) && (requestSequenceNumber == RTSP_SEQUENCE_NUMBER_UNSPECIFIED))
    {
      // bad sequence number
      this->logger->Log(LOGGER_ERROR, L"%s: %s: not specified request sequence number", this->protocolName, functionName);
      errorCode = CURLE_RTSP_CSEQ_ERROR;
    }
  }

  if (errorCode == CURLE_OK)
  {
    if (errorCode == CURLE_OK)
    {
      // send RTSP request
      {
        CLockMutex lock(this->mutex, INFINITE);

        this->rtspDownloadResponse->ClearRtspRequestAndResponse();
        this->rtspDownloadResponse->SetRtspRequest(request);
        this->rtspDownloadResponse->SetResponseCode(RTSP_STATUS_CODE_UNSPECIFIED);
        this->rtspDownloadResponse->SetResultCode(CURLE_AGAIN);
      }
      errorCode = CURLE_AGAIN;
    }

    // wait for response
    while (errorCode == CURLE_AGAIN)
    {
      // give chance other threads to do something useful
      Sleep(1);

      {
        CLockMutex lock(this->mutex, INFINITE);

        errorCode = this->rtspDownloadResponse->GetResultCode();
      }
    }

    if (errorCode != CURLE_OK)
    {
      wchar_t *error = FormatString(L"error while sending RTSP %s", rtspMethodName);
      if (error != NULL)
      {
        this->ReportCurlErrorMessage(LOGGER_ERROR, this->protocolName, functionName, error, errorCode);
      }
      this->logger->Log(LOGGER_ERROR, L"%s: %s: error while sending RTSP %s: 0x%08X", this->protocolName, functionName, rtspMethodName, this->rtspDownloadResponse->GetResponseCode());
      FREE_MEM(error);
    }

    {
      CLockMutex lock(this->mutex, INFINITE);

      if ((errorCode == CURLE_OK) && (this->rtspDownloadResponse->GetRtspResponse()->IsEmpty()))
      {
        // this should not happen, we should always have response or error (maybe timeout which is also error)
        this->logger->Log(LOGGER_ERROR, L"%s: %s: no response for RTSP %s request", this->protocolName, functionName, rtspMethodName);
        errorCode = CURLE_RECV_ERROR;
      }

      if (errorCode == CURLE_OK)
      {
        // check existence of session ID or check session ID value (if exists)

        if (this->sessionId != NULL)
        {
          // valid RTSP response can have session ID, but it is not mandatory
          if (this->rtspDownloadResponse->GetRtspResponse()->GetSessionId() != NULL)
          {
            // check session ID if same
            errorCode = (CompareWithNull(this->sessionId, this->rtspDownloadResponse->GetRtspResponse()->GetSessionId()) == 0) ? errorCode : CURLE_RECV_ERROR;

            CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->logger->Log(LOGGER_ERROR, L"%s: %s: bad session ID for RTSP %s request", this->protocolName, functionName, rtspMethodName));
          }
        }
        else if (this->rtspDownloadResponse->GetRtspResponse()->GetSessionId() != NULL)
        {
          // new session ID, our session ID is NULL
          this->sessionId = Duplicate(this->rtspDownloadResponse->GetRtspResponse()->GetSessionId());
          errorCode = (this->sessionId != NULL) ? errorCode : CURLE_OUT_OF_MEMORY;

          CHECK_CONDITION_EXECUTE(errorCode != CURLE_OK, this->logger->Log(LOGGER_ERROR, L"%s: %s: cannot store session ID for RTSP %s request", this->protocolName, functionName, rtspMethodName));
        }
      }

      if (errorCode == CURLE_OK)
      {
        // check sequence number
        unsigned int requestSequenceNumber = request->GetSequenceNumber();
        unsigned int responseSequenceNumber = this->rtspDownloadResponse->GetRtspResponse()->GetSequenceNumber();

        if ((errorCode == CURLE_OK) && (responseSequenceNumber == RTSP_SEQUENCE_NUMBER_UNSPECIFIED))
        {
          // bad sequence number
          this->logger->Log(LOGGER_ERROR, L"%s: %s: not specified response sequence number", this->protocolName, functionName);
          errorCode = CURLE_RTSP_CSEQ_ERROR;
        }

        if ((errorCode == CURLE_OK) && (requestSequenceNumber != responseSequenceNumber))
        {
          // sequence numbers not equal
          this->logger->Log(LOGGER_ERROR, L"%s: %s: request (%u) and response (%u) sequence numbers not equal", this->protocolName, functionName, requestSequenceNumber, responseSequenceNumber);
          errorCode = CURLE_RTSP_CSEQ_ERROR;
        }
      }

      if ((errorCode == CURLE_OK) && (!this->rtspDownloadResponse->GetRtspResponse()->IsSuccess()))
      {
        CRtspResponse *response = this->rtspDownloadResponse->GetRtspResponse();
        this->logger->Log(LOGGER_ERROR, L"%s: %s: RTSP %s response status code not success: %u (%s)", this->protocolName, functionName, rtspMethodName, response->GetStatusCode(), (response->GetStatusReason() != NULL) ? response->GetStatusReason() : L"");
        errorCode = CURLE_RECV_ERROR;
      }
    }
  }

  return errorCode;
}