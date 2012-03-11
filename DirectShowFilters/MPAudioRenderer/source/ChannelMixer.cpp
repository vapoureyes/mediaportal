// Copyright (C) 2005-2012 Team MediaPortal
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

#include "stdafx.h"
#include "Globals.h"
#include "ChannelMixer.h"
#include "..\AE_mixer\AEChannelInfo.h"

#include "alloctracing.h"

CChannelMixer::CChannelMixer(AudioRendererSettings* pSettings, IRenderFilter* pRenderer) :
  CBaseAudioSink(true),
  m_pRenderer(pRenderer),
  m_bPassThrough(false),
  m_rtInSampleTime(0),
  m_pSettings(pSettings),
  m_rtNextIncomingSampleTime(0)
{
  m_pRemap = new CAERemap();
}

CChannelMixer::~CChannelMixer(void)
{
  delete m_pRemap;
}

HRESULT CChannelMixer::Init()
{
  HRESULT hr = InitAllocator();
  if (FAILED(hr))
    return hr;

  return CBaseAudioSink::Init();
}

HRESULT CChannelMixer::Cleanup()
{
  return CBaseAudioSink::Cleanup();
}

HRESULT CChannelMixer::NegotiateFormat(const WAVEFORMATEXTENSIBLE* pwfx, int nApplyChangesDepth)
{
  if (!pwfx)
    return VFW_E_TYPE_NOT_ACCEPTED;

  if (FormatsEqual(pwfx, m_pInputFormat))
    return S_OK;

  if (!m_pNextSink)
    return VFW_E_TYPE_NOT_ACCEPTED;

  bool bApplyChanges = (nApplyChangesDepth != 0);
  if (nApplyChangesDepth != INFINITE && nApplyChangesDepth > 0)
    nApplyChangesDepth--;

  // try passthrough
  /*
  HRESULT hr = m_pNextSink->NegotiateFormat(pwfx, nApplyChangesDepth);
  if (SUCCEEDED(hr))
  {
    if (bApplyChanges)
    {
      m_bPassThrough = true;
      SetInputFormat(pwfx);
      SetOutputFormat(pwfx);
    }
    return hr;
  }
  */

  if (pwfx->SubFormat != KSDATAFORMAT_SUBTYPE_IEEE_FLOAT)
    return VFW_E_TYPE_NOT_ACCEPTED;

  WAVEFORMATEXTENSIBLE* pOutWfx;
  CopyWaveFormatEx(&pOutWfx, pwfx);

  pOutWfx->dwChannelMask = m_pSettings->m_lSpeakerConfig;
  pOutWfx->Format.nChannels = m_pSettings->m_lSpeakerCount;
  pOutWfx->SubFormat = KSDATAFORMAT_SUBTYPE_IEEE_FLOAT;
  pOutWfx->Format.nBlockAlign = pOutWfx->Format.wBitsPerSample / 8 * pOutWfx->Format.nChannels;
  pOutWfx->Format.nAvgBytesPerSec = pOutWfx->Format.nBlockAlign * pOutWfx->Format.nSamplesPerSec;
  
  HRESULT hr = m_pNextSink->NegotiateFormat(pOutWfx, nApplyChangesDepth);

  if (FAILED(hr))
  {
    SAFE_DELETE_WAVEFORMATEX(pOutWfx);
    return hr;
  }

  if (bApplyChanges)
  {
    m_bPassThrough = false;
    SetInputFormat(pwfx);
    SetOutputFormat(pOutWfx, true);
    hr = SetupConversion();
  }
  else
    SAFE_DELETE_WAVEFORMATEX(pOutWfx);

  return hr;
}

// Processing
HRESULT CChannelMixer::PutSample(IMediaSample *pSample)
{
  if (!pSample)
    return S_OK;

  AM_MEDIA_TYPE *pmt = NULL;
  bool bFormatChanged = false;
  
  HRESULT hr = S_OK;

  if (SUCCEEDED(pSample->GetMediaType(&pmt)) && pmt != NULL)
    bFormatChanged = !FormatsEqual((WAVEFORMATEXTENSIBLE*)pmt->pbFormat, m_pInputFormat);

  if (pSample->IsDiscontinuity() == S_OK)
    m_bDiscontinuity = true;

  CAutoLock lock (&m_csOutputSample);
  if (m_bFlushing)
    return S_OK;

  if (bFormatChanged)
  {
    // Process any remaining input
    if (!m_bPassThrough)
      hr = ProcessData(NULL, 0, NULL);
    // Apply format change locally, 
    // next filter will evaluate the format change when it receives the sample
    Log("CChannelMixer::PutSample: Processing format change");
    hr = NegotiateFormat((WAVEFORMATEXTENSIBLE*)pmt->pbFormat, 1);
    if (FAILED(hr))
    {
      DeleteMediaType(pmt);
      Log("SampleRateConverter: PutSample failed to change format: 0x%08x", hr);
      return hr;
    }
  }

  if (pmt)
    DeleteMediaType(pmt);

  if (m_bPassThrough)
  {
    if (m_pNextSink)
      return m_pNextSink->PutSample(pSample);
    return S_OK; // perhaps we should return S_FALSE to indicate sample was dropped
  }

  long nOffset = 0;
  long cbSampleData = pSample->GetActualDataLength();
  BYTE *pData = NULL;
  REFERENCE_TIME rtStop = 0;
  REFERENCE_TIME rtStart = 0;
  pSample->GetTime(&rtStart, &rtStop);

  // Detect discontinuity in stream timeline
  if ((abs(m_rtNextIncomingSampleTime - rtStart) > MAX_SAMPLE_TIME_ERROR))
  {
    Log("CChannelMixer - stream discontinuity: %6.3f", (rtStart - m_rtNextIncomingSampleTime) / 10000000.0);

    m_rtInSampleTime = rtStart;

    if (m_nSampleNum > 0)
    {
      Log("CChannelMixer - using buffered sample data");
      FlushStream();
    }
    else
      Log("CChannelMixer - discarding buffered sample data");
  }

  if (m_nSampleNum == 0)
    m_rtInSampleTime = rtStart;

  UINT nFrames = cbSampleData / m_pInputFormat->Format.nBlockAlign;
  REFERENCE_TIME duration = nFrames * UNITS / m_pInputFormat->Format.nSamplesPerSec;

  m_rtNextIncomingSampleTime = rtStart + duration;
  m_nSampleNum++;

  hr = pSample->GetPointer(&pData);
  ASSERT(pData);
  if (FAILED(hr))
  {
    Log("CChannelMixer::PutSample - failed to get sample's data pointer: 0x%08x", hr);
    return hr;
  }

  while (nOffset < cbSampleData && SUCCEEDED(hr))
  {
    long cbProcessed = 0;
    hr = ProcessData(pData + nOffset, cbSampleData - nOffset, &cbProcessed);
    nOffset += cbProcessed;
  }
  return hr;
}

HRESULT CChannelMixer::EndOfStream()
{
  if (!m_bPassThrough)
    FlushStream();

  return CBaseAudioSink::EndOfStream();  
}

HRESULT CChannelMixer::OnInitAllocatorProperties(ALLOCATOR_PROPERTIES* properties)
{
  properties->cBuffers = 4;
  properties->cbBuffer = (0x10000);
  properties->cbPrefix = 0;
  properties->cbAlign = 8;

  return S_OK;
}

HRESULT CChannelMixer::SetupConversion()
{
  m_nInFrameSize = m_pInputFormat->Format.nBlockAlign;
  m_nOutFrameSize = m_pOutputFormat->Format.nBlockAlign;

  bool ac3Encoded = m_pRenderer->RenderFormat()->SubFormat == KSDATAFORMAT_SUBTYPE_IEC61937_DOLBY_DIGITAL;

  CAEChannelInfo input;

  switch (m_pInputFormat->Format.nChannels)
  {
    case 1:
      input = AE_CH_LAYOUT_1_0;
      break;

    case 2:
      input = AE_CH_LAYOUT_2_0;
      break;

    case 3:
      if (m_pOutputFormat->dwChannelMask & SPEAKER_LOW_FREQUENCY)
        input = AE_CH_LAYOUT_2_1;
      else
        input = AE_CH_LAYOUT_3_0;
      
      break;

    case 4:
      if (m_pOutputFormat->dwChannelMask & SPEAKER_LOW_FREQUENCY)
        input = AE_CH_LAYOUT_3_1;
      else
        input = AE_CH_LAYOUT_4_0;
      
      break;

    case 5:
      if (m_pOutputFormat->dwChannelMask & SPEAKER_LOW_FREQUENCY)
        input = AE_CH_LAYOUT_4_1;
      else
        input = AE_CH_LAYOUT_5_0;
      
      break;

    case 6:
      input = AE_CH_LAYOUT_5_1;
      break;

    case 7:
      input = AE_CH_LAYOUT_7_0;
      break;
    
    case 8:
      input = AE_CH_LAYOUT_7_1;
      break;
    
    default:
      ASSERT(false);
  }

  //CAEChannelInfo input(AE_CH_LAYOUT_5_1);
  CAEChannelInfo output(AE_CH_LAYOUT_2_0);
  
  // AC3 encoder channel mapping correction

  //CAEChannelInfo output(AE_CH_LAYOUT_5_1);
  /*output += AE_CH_FL;
  output += AE_CH_FC;
  output += AE_CH_FR;
  output += AE_CH_BL;
  output += AE_CH_BR;
  output += AE_CH_LFE;*/

  if (!m_pRemap->Initialize(input, output, false, false, AE_CH_LAYOUT_5_1))
  {
    Log("CChannelMixer::SetupConversion - failed to initialize channel remapper");
    ASSERT(false);
  }

  return S_OK;
}

HRESULT CChannelMixer::ProcessData(const BYTE* pData, long cbData, long* pcbDataProcessed)
{
  HRESULT hr = S_OK;

  long bytesOutput = 0;

  CAutoLock lock (&m_csOutputSample);

  while (cbData)
  {
    if (m_pNextOutSample)
    {
      // If there is not enough space in output sample, flush it
      long nOffset = m_pNextOutSample->GetActualDataLength();
      long nSize = m_pNextOutSample->GetSize();

      if (nOffset + m_nOutFrameSize > nSize)
      {
        hr = OutputNextSample();

        UINT nFrames = nOffset / m_pOutputFormat->Format.nBlockAlign;
        m_rtInSampleTime += nFrames * UNITS / m_pOutputFormat->Format.nSamplesPerSec;      

        if (FAILED(hr))
        {
          Log("CChannelMixer::ProcessData OutputNextSample failed with: 0x%08x", hr);
          return hr;
        }
      }
    }

    // try to get an output buffer if none available
    if (!m_pNextOutSample && FAILED(hr = RequestNextOutBuffer(m_rtInSampleTime)))
    {
      if (pcbDataProcessed)
        *pcbDataProcessed = bytesOutput + cbData; // we can't realy process the data, lie about it!

      return hr;
    }

    long nOffset = m_pNextOutSample->GetActualDataLength();
    long nSize = m_pNextOutSample->GetSize();
    BYTE* pOutData = NULL;

    if (FAILED(hr = m_pNextOutSample->GetPointer(&pOutData)))
    {
      Log("CChannelMixer: Failed to get output buffer pointer: 0x%08x", hr);
      return hr;
    }
    ASSERT(pOutData);
    pOutData += nOffset;

    // TODO: process sample data
    int framesToConvert = min(cbData / m_nInFrameSize, (nSize - nOffset) / m_nOutFrameSize);

    m_pRemap->Remap((float*)pData, (float*)pOutData, framesToConvert);

    pData += framesToConvert * m_nInFrameSize;
    bytesOutput += framesToConvert * m_nInFrameSize;
    cbData -= framesToConvert * m_nInFrameSize; 
    nOffset += framesToConvert * m_nOutFrameSize;
    m_pNextOutSample->SetActualDataLength(nOffset);

    m_pNextOutSample->SetActualDataLength(nOffset);
    if (nOffset + m_nOutFrameSize > nSize)
    {
      hr = OutputNextSample();
      
      UINT nFrames = nOffset / m_pOutputFormat->Format.nBlockAlign;
      m_rtInSampleTime += nFrames * UNITS / m_pOutputFormat->Format.nSamplesPerSec;

      if (FAILED(hr))
      {
        Log("CChannelMixer::ProcessData OutputNextSample failed with: 0x%08x", hr);
        return hr;
      }
    }

    // all samples should contain an integral number of frames
    ASSERT(cbData == 0 || cbData >= m_nInFrameSize);
  }
  
  if (pcbDataProcessed)
    *pcbDataProcessed = bytesOutput;

  return hr;
}

HRESULT CChannelMixer::FlushStream()
{
  HRESULT hr = S_OK;

  CAutoLock lock (&m_csOutputSample);
  if (m_pNextOutSample)
  {
    hr = OutputNextSample();
    if (FAILED(hr))
      Log("CChannelMixer::FlushStream OutputNextSample failed with: 0x%08x", hr);
  }

  return hr;
}


