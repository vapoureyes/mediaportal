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
#include <map>
#include <sstream>
#include <streams.h>    // CUnknown, LPUNKNOWN
#include <vector>
#include <WinError.h>   // HRESULT
#include "..\..\shared\ISectionCallback.h"
#include "..\..\shared\SectionDecoder.h"
#include "..\..\shared\TsHeader.h"
#include "CriticalSection.h"
#include "ICallBackGrabber.h"
#include "ICallBackPidConsumer.h"
#include "IDefaultAuthorityProvider.h"
#include "IGrabberEpgDvb.h"
#include "IRecord.h"
#include "RecordStore.h"
#include "Utils.h"

using namespace MediaPortal;
using namespace std;


#define PID_EIT_DVB                 0x12    // DVB standard
#define PID_EIT_VIASAT_SWEDEN       0x39    // Viasat (Sweden satellite); custom PID
#define PID_EIT_DISH                0x300   // DISH Network (USA satellite) 9 day EPG; custom descriptors
#define PID_EIT_MULTICHOICE         0x3fa   // MultiChoice (South Africa satellite); custom PID
#define PID_EIT_BELL_EXPRESSVU      0x441   // Bell ExpressVu (Canada satellite) 9 day EPG; custom descriptors
#define PID_EIT_PREMIERE_SELECT     0xb09   // Germany, now owned by Sky; custom format
#define PID_EIT_PREMIERE_DIREKT     0xb11   // Germany, now owned by Sky; custom format
#define PID_EIT_PREMIERE_SPORT      0xb12   // Germany, now owned by Sky; custom format
#define PID_EIT_DVB_CALL_BACK       PID_EIT_DVB

#define TABLE_ID_EIT_DVB_START      0x4e
#define TABLE_ID_EIT_DVB_END        0x6f
#define TABLE_ID_EIT_DISH_START     0x80
#define TABLE_ID_EIT_DISH_END       0xfe
#define TABLE_ID_EIT_DVB_PF_ACTUAL  0x4e
#define TABLE_ID_EIT_DVB_PF_OTHER   0x4f
#define TABLE_ID_EIT_PREMIERE       0xa0
#define TABLE_ID_EIT_DVB_CALL_BACK  TABLE_ID_EIT_DVB_START

// We don't use this table. It seems to carry duplicate incomplete data.
// Perhaps it is an alternative running status table for accurate recording?
#define TABLE_ID_EIT_FREESAT_PF     0xd1


extern void LogDebug(const wchar_t* fmt, ...);

class CParserEitDvb : public CUnknown, public IGrabberEpgDvb, ISectionCallback
{
  public:
    CParserEitDvb(ICallBackPidConsumer* callBack,
                  IDefaultAuthorityProvider* authorityProvider,
                  LPUNKNOWN unk,
                  HRESULT* hr);
    virtual ~CParserEitDvb(void);

    DECLARE_IUNKNOWN

    STDMETHODIMP NonDelegatingQueryInterface(REFIID iid, void** ppv);

    void SetFreesatPmtPid(unsigned short pid);
    void SetFreesatPids(unsigned short pidBat,
                        unsigned short pidEitPf,
                        unsigned short pidEitSchedule,
                        unsigned short pidNit,
                        unsigned short pidSdt);
    STDMETHODIMP_(void) SetProtocols(bool grabDvbEit,
                                      bool grabBellExpressVu,
                                      bool grabDish,
                                      bool grabFreesat,
                                      bool grabMultiChoice,
                                      bool grabPremiere,
                                      bool grabViasatSweden);
    void Reset(bool enableCrcCheck);
    STDMETHODIMP_(void) SetCallBack(ICallBackGrabber* callBack);
    bool OnTsPacket(CTsHeader& header, unsigned char* tsPacket);
    STDMETHODIMP_(bool) IsSeen();
    STDMETHODIMP_(bool) IsReady();

    STDMETHODIMP_(unsigned short) GetServiceCount();
    STDMETHODIMP_(bool) GetService(unsigned short index,
                                    unsigned short* originalNetworkId,
                                    unsigned short* transportStreamId,
                                    unsigned short* serviceId,
                                    unsigned short* eventCount);
    STDMETHODIMP_(bool) GetEvent(unsigned short serviceIndex,
                                  unsigned short eventIndex,
                                  unsigned long long* eventId,
                                  unsigned long long* startDateTime,
                                  unsigned short* duration,
                                  unsigned char* runningStatus,
                                  bool* freeCaMode,
                                  unsigned short* referenceServiceId,
                                  unsigned long long* referenceEventId,
                                  char* seriesId,
                                  unsigned short* seriesIdBufferSize,
                                  char* episodeId,
                                  unsigned short* episodeIdBufferSize,
                                  bool* isHighDefinition,
                                  bool* isStandardDefinition,
                                  bool* isThreeDimensional,
                                  bool* isPreviouslyShown,
                                  unsigned long* audioLanguages,
                                  unsigned char* audioLanguageCount,
                                  unsigned long* subtitlesLanguages,
                                  unsigned char* subtitlesLanguageCount,
                                  unsigned short* dvbContentTypeIds,
                                  unsigned char* dvbContentTypeIdCount,
                                  unsigned long* dvbParentalRatingCountryCodes,
                                  unsigned char* dvbParentalRatings,
                                  unsigned char* dvbParentalRatingCount,
                                  unsigned char* starRating,
                                  unsigned char* mpaaClassification,
                                  unsigned short* dishBevAdvisories,
                                  unsigned char* vchipRating,
                                  unsigned char* textCount);
    STDMETHODIMP_(bool) GetEventText(unsigned short serviceIndex,
                                      unsigned short eventIndex,
                                      unsigned char textIndex,
                                      unsigned long* language,
                                      char* title,
                                      unsigned short* titleBufferSize,
                                      char* shortDescription,
                                      unsigned short* shortDescriptionBufferSize,
                                      char* extendedDescription,
                                      unsigned short* extendedDescriptionBufferSize,
                                      unsigned char* descriptionItemCount);
    STDMETHODIMP_(bool) GetEventDescriptionItem(unsigned short serviceIndex,
                                                unsigned short eventIndex,
                                                unsigned char textIndex,
                                                unsigned char itemIndex,
                                                char* description,
                                                unsigned short* descriptionBufferSize,
                                                char* text,
                                                unsigned short* textBufferSize);

  private:
    class CRecordEitEventDescriptionItem
    {
      public:
        CRecordEitEventDescriptionItem(void)
        {
          DescriptorNumber = 0;
          Index = 0;
          Description = NULL;
          Text = NULL;
        }

        ~CRecordEitEventDescriptionItem(void)
        {
          if (Description != NULL)
          {
            delete[] Description;
            Description = NULL;
          }
          if (Text != NULL)
          {
            delete[] Text;
            Text = NULL;
          }
        }

        bool Equals(const CRecordEitEventDescriptionItem* record) const
        {
          if (
            record == NULL ||
            DescriptorNumber != record->DescriptorNumber ||
            Index != record->Index ||
            !CUtils::CompareStrings(Description, record->Description) ||
            !CUtils::CompareStrings(Text, record->Text)
          )
          {
            return false;
          }
          return true;
        }

        unsigned short GetKey() const
        {
          return (DescriptorNumber << 8) | Index;
        }

        void Debug(const wchar_t* situation) const
        {
          LogDebug(L"EIT DVB: description item %s, descriptor number = %hhu, index = %hhu, description = %S, text = %S",
                    situation, DescriptorNumber, Index,
                    Description == NULL ? "" : Description,
                    Text == NULL ? "" : Text);
        }

        unsigned char DescriptorNumber;
        unsigned char Index;
        char* Description;
        char* Text;
    };

    class CRecordEitEventText
    {
      public:
        CRecordEitEventText(void)
        {
          Language = 0;
          Title = NULL;
          DescriptionShort = NULL;
          DescriptionExtended = NULL;
        }

        ~CRecordEitEventText(void)
        {
          if (Title != NULL)
          {
            delete[] Title;
            Title = NULL;
          }
          if (DescriptionShort != NULL)
          {
            delete[] DescriptionShort;
            DescriptionShort = NULL;
          }
          if (DescriptionExtended != NULL)
          {
            delete[] DescriptionExtended;
            DescriptionExtended = NULL;
          }
          map<unsigned short, CRecordEitEventDescriptionItem*>::iterator it = DescriptionItems.begin();
          for ( ; it != DescriptionItems.end(); it++)
          {
            CRecordEitEventDescriptionItem* item = it->second;
            if (item != NULL)
            {
              delete item;
              it->second = NULL;
            }
          }
          DescriptionItems.clear();
        }

        bool Equals(const CRecordEitEventText* record) const
        {
          if (
            record == NULL ||
            Language != record->Language ||
            !CUtils::CompareStrings(Title, record->Title) ||
            !CUtils::CompareStrings(DescriptionShort, record->DescriptionShort) ||
            !CUtils::CompareStrings(DescriptionExtended, record->DescriptionExtended) ||
            DescriptionItems.size() != record->DescriptionItems.size()
          )
          {
            return false;
          }

          map<unsigned short, CRecordEitEventDescriptionItem*>::const_iterator it1 = DescriptionItems.begin();
          for ( ; it1 != DescriptionItems.end(); it1++)
          {
            if (it1->second == NULL)
            {
              continue;
            }
            map<unsigned short, CRecordEitEventDescriptionItem*>::const_iterator it2 = record->DescriptionItems.find(it1->first);
            if (
              it2 == record->DescriptionItems.end() ||
              !it1->second->Equals(it2->second)
            )
            {
              return false;
            }
          }
          return true;
        }

        unsigned long GetKey() const
        {
          return Language;
        }

        void Debug(const wchar_t* situation) const
        {
          LogDebug(L"EIT DVB: text %s, code = %S, title = %S, short description = %S, extended description = %S, item count = %llu",
                    situation, (char*)&Language, Title == NULL ? "" : Title,
                    DescriptionShort == NULL ? "" : DescriptionShort,
                    DescriptionExtended == NULL ? "" : DescriptionExtended,
                    (unsigned long long)DescriptionItems.size());

          map<unsigned short, CRecordEitEventDescriptionItem*>::const_iterator it = DescriptionItems.begin();
          for ( ; it != DescriptionItems.end(); it++)
          {
            if (it->second != NULL)
            {
              it->second->Debug(situation);
            }
          }
        }

        unsigned long Language;
        char* Title;
        char* DescriptionShort;
        char* DescriptionExtended;
        map<unsigned short, CRecordEitEventDescriptionItem*> DescriptionItems;
    };

    class CRecordEitEvent : public IRecord
    {
      public:
        CRecordEitEvent(void)
        {
          TableId = 0;
          OriginalNetworkId = 0;
          TransportStreamId = 0;
          ServiceId = 0;
          EventId = 0;
          StartDateTime = 0;
          Duration = 0;
          RunningStatus = 0;
          FreeCaMode = false;
          ReferenceServiceId = 0;
          ReferenceEventId = 0;
          AreSeriesAndEpisodeIdsCrids = false;
          SeriesId = NULL;
          EpisodeId = NULL;
          IsHighDefinition = false;
          IsStandardDefinition = false;
          IsThreeDimensional = false;
          IsPreviouslyShown = false;
          StarRating = 0;             // default: [not available]
          MpaaClassification = 0xff;  // default: [not available]
          DishBevAdvisories = 0;      // default: [not available]
          VchipRating = 0xff;         // default: [not available]
        }

        ~CRecordEitEvent(void)
        {
          if (SeriesId != NULL)
          {
            delete[] SeriesId;
            SeriesId = NULL;
          }
          if (EpisodeId != NULL)
          {
            delete[] EpisodeId;
            EpisodeId = NULL;
          }
          map<unsigned long, CRecordEitEventText*>::iterator it = Texts.begin();
          for ( ; it != Texts.end(); it++)
          {
            CRecordEitEventText* text = it->second;
            if (text != NULL)
            {
              delete text;
              it->second = NULL;
            }
          }
          Texts.clear();
        }

        bool Equals(const IRecord* record) const
        {
          const CRecordEitEvent* recordEvent = dynamic_cast<const CRecordEitEvent*>(record);
          if (
            recordEvent == NULL ||
            TableId != recordEvent->TableId ||
            EventId != recordEvent->EventId ||
            StartDateTime != recordEvent->StartDateTime ||
            Duration != recordEvent->Duration ||
            RunningStatus != recordEvent->RunningStatus ||
            FreeCaMode != recordEvent->FreeCaMode ||
            ReferenceServiceId != recordEvent->ReferenceServiceId ||
            ReferenceEventId != recordEvent->ReferenceEventId ||
            !CUtils::CompareStrings(SeriesId, recordEvent->SeriesId) ||
            !CUtils::CompareStrings(EpisodeId, recordEvent->EpisodeId) ||
            IsHighDefinition != recordEvent->IsHighDefinition ||
            IsStandardDefinition != recordEvent->IsStandardDefinition ||
            IsThreeDimensional != recordEvent->IsThreeDimensional ||
            IsPreviouslyShown != recordEvent->IsPreviouslyShown ||
            !CUtils::CompareVectors(AudioLanguages, recordEvent->AudioLanguages) ||
            !CUtils::CompareVectors(SubtitlesLanguages, recordEvent->SubtitlesLanguages) ||
            !CUtils::CompareVectors(DvbContentTypeIds, recordEvent->DvbContentTypeIds) ||
            DvbParentalRatings.size() != recordEvent->DvbParentalRatings.size() ||
            StarRating != recordEvent->StarRating ||
            MpaaClassification != recordEvent->MpaaClassification ||
            DishBevAdvisories != recordEvent->DishBevAdvisories ||
            VchipRating != recordEvent->VchipRating ||
            Texts.size() != recordEvent->Texts.size()
          )
          {
            return false;
          }

          map<unsigned long, unsigned char>::const_iterator prIt = DvbParentalRatings.begin();
          for ( ; prIt != DvbParentalRatings.end(); prIt++)
          {
            if (recordEvent->DvbParentalRatings.find(prIt->first) == recordEvent->DvbParentalRatings.end())
            {
              return false;
            }
          }

          map<unsigned long, CRecordEitEventText*>::const_iterator textIt1 = Texts.begin();
          for ( ; textIt1 != Texts.end(); textIt1++)
          {
            if (textIt1->second == NULL)
            {
              continue;
            }
            map<unsigned long, CRecordEitEventText*>::const_iterator textIt2 = recordEvent->Texts.find(textIt1->first);
            if (
              textIt2 == recordEvent->Texts.end() ||
              !textIt1->second->Equals(textIt2->second)
            )
            {
              return false;
            }
          }
          return true;
        }

        unsigned long long GetKey() const
        {
          // Ideally we wouldn't have to include the table ID in the key.
          // However, some providers include the same event in multiple tables
          // at the same time (eg. present/following and schedule). Therefore
          // we must include the table ID to avoid spurious duplicate detection
          // hits.
          return ((unsigned long long)TableId << 48) | EventId;
        }

        unsigned long long GetExpiryKey() const
        {
          return TableId;
        }

        void Debug(const wchar_t* situation) const
        {
          LogDebug(L"EIT DVB: event %s, table ID = 0x%hhx, ONID = %hu, TSID = %hu, service ID = %hu, event ID = %llu, start date/time = %llu, duration = %hu m, running status = %hhu, free CA mode = %d, reference service ID = %hu, reference event ID = %llu, series ID = %S, episode ID = %S, is HD = %d, is SD = %d, is 3D = %d, is previously shown = %d, audio language count = %llu, subtitles language count = %llu, DVB content type count = %llu, DVB parental rating count = %llu, star rating = %hhu, MPAA classification = %hhu, Dish/BEV advisories = %hu, V-CHIP rating = %hhu, text count = %llu",
                    situation, TableId, OriginalNetworkId, TransportStreamId,
                    ServiceId, EventId, StartDateTime, Duration, RunningStatus,
                    FreeCaMode, ReferenceServiceId, ReferenceEventId,
                    SeriesId == NULL ? "" : SeriesId,
                    EpisodeId == NULL ? "" : EpisodeId, IsHighDefinition,
                    IsStandardDefinition, IsThreeDimensional,
                    IsPreviouslyShown,
                    (unsigned long long)AudioLanguages.size(),
                    (unsigned long long)SubtitlesLanguages.size(),
                    (unsigned long long)DvbContentTypeIds.size(),
                    (unsigned long long)DvbParentalRatings.size(), StarRating,
                    MpaaClassification, DishBevAdvisories, VchipRating,
                    (unsigned long long)Texts.size());

          CUtils::DebugVector(AudioLanguages, L"audio language(s)", true);
          CUtils::DebugVector(SubtitlesLanguages, L"subtitles language(s)", true);
          CUtils::DebugVector(DvbContentTypeIds, L"DVB content type ID(s)", false);

          if (DvbParentalRatings.size() > 0)
          {
            wstringstream temp(ios_base::out | ios_base::ate);
            temp.str(L"  DVB parental rating(s) = ");
            bool isFirst = true;
            map<unsigned long, unsigned char>::const_iterator prIt = DvbParentalRatings.begin();
            for ( ; prIt != DvbParentalRatings.end(); prIt++)
            {
              if (!isFirst)
              {
                temp << L", ";
              }
              temp << prIt->second << L" (" << (char*)(&(prIt->first)) << ")";
              isFirst = false;
            }
            LogDebug(temp.str().c_str());
          }

          map<unsigned long, CRecordEitEventText*>::const_iterator textIt = Texts.begin();
          for ( ; textIt != Texts.end(); textIt++)
          {
            if (textIt->second != NULL)
            {
              textIt->second->Debug(situation);
            }
          }
        }

        unsigned char TableId;
        unsigned short OriginalNetworkId;
        unsigned short TransportStreamId;
        unsigned short ServiceId;
        unsigned long long EventId;
        unsigned long long StartDateTime; // unit = UTC epoch
        unsigned short Duration;          // unit = minutes
        unsigned char RunningStatus;
        bool FreeCaMode;
        unsigned short ReferenceServiceId;
        unsigned long long ReferenceEventId;
        bool AreSeriesAndEpisodeIdsCrids;
        char* SeriesId;
        char* EpisodeId;
        bool IsHighDefinition;
        bool IsStandardDefinition;
        bool IsThreeDimensional;
        bool IsPreviouslyShown;
        vector<unsigned long> AudioLanguages;
        vector<unsigned long> SubtitlesLanguages;
        vector<unsigned short> DvbContentTypeIds;
        map<unsigned long, unsigned char> DvbParentalRatings;   // country code => rating
        unsigned char StarRating;
        unsigned char MpaaClassification;
        unsigned short DishBevAdvisories;
        unsigned char VchipRating;
        map<unsigned long, CRecordEitEventText*> Texts;
    };

    class CRecordEitService
    {
      public:
        CRecordEitService(void) : Events(600000)
        {
          IsPremiereService = false;
          OriginalNetworkId = 0;
          TransportStreamId = 0;
          ServiceId = 0;
        }

        ~CRecordEitService(void)
        {
        }

        bool IsPremiereService;
        unsigned short OriginalNetworkId;
        unsigned short TransportStreamId;
        unsigned short ServiceId;
        CRecordStore Events;

        vector<unsigned char> SeenTables;
        vector<unsigned char> UnseenTables;
        vector<unsigned long> SeenSegments;
        vector<unsigned long> UnseenSegments;
        vector<unsigned long> SeenSections;
        vector<unsigned long> UnseenSections;
    };

    bool SelectServiceRecordByIndex(unsigned short index);
    bool SelectEventRecordByIndex(unsigned short index);
    bool SelectTextRecordByIndex(unsigned char index);

    void OnNewSection(int pid, int tableId, CSection& section);

    void PrivateReset(bool removeFreesatDecoders);
    bool AddOrResetDecoder(unsigned short pid, bool enableCrcCheck);
    void ResetFreesatGrabState();
    template<class T> static void RemoveExpiredEntries(vector<T>& set,
                                                        bool isTableIdSet,
                                                        unsigned char sectionTableId,
                                                        unsigned char sectionVersionNumber,
                                                        bool isTableFromGroup,
                                                        unsigned char lastValidTableId,
                                                        vector<unsigned char>& erasedTableIds);
    void CreatePremiereEvents(CRecordEitEvent& eventTemplate,
                              map<unsigned long long,
                              vector<unsigned long long>*>& premiereShowings);
    CRecordEitService* GetOrCreateService(bool isPremiereService,
                                          unsigned short originalNetworkId,
                                          unsigned short transportStreamId,
                                          unsigned short serviceId,
                                          bool doNotCreate);
    static CRecordEitEventText* GetOrCreateText(CRecordEitEvent& event, unsigned long language);
    static void CreateDescriptionItem(CRecordEitEvent& event,
                                      unsigned long language,
                                      unsigned char index,
                                      const char* description,
                                      char* text);
    static void CopyString(const char* input, char** output, wchar_t* debug);

    static bool DecodeEventRecord(unsigned char* sectionData,
                                  unsigned short& pointer,
                                  unsigned short endOfSection,
                                  CRecordEitEvent& event,
                                  map<unsigned long long, vector<unsigned long long>*>& premiereShowings);
    static bool DecodeEventDescriptors(unsigned char* sectionData,
                                        unsigned short& pointer,
                                        unsigned short endOfDescriptorLoop,
                                        CRecordEitEvent& event,
                                        map<unsigned long long, vector<unsigned long long>*>& premiereShowings);

    static bool DecodeShortEventDescriptor(unsigned char* data,
                                            unsigned char dataLength,
                                            unsigned long& language,
                                            char** eventName,
                                            char** text);
    static bool DecodeExtendedEventDescriptor(unsigned char* data,
                                              unsigned char dataLength,
                                              unsigned long& language,
                                              vector<CRecordEitEventDescriptionItem*>& items,
                                              char** text);
    static bool DecodeTimeShiftedEventDescriptor(unsigned char* data,
                                                  unsigned char dataLength,
                                                  unsigned short& referenceServiceId,
                                                  unsigned short& referenceEventId);
    static bool DecodeComponentDescriptor(unsigned char* data,
                                          unsigned char dataLength,
                                          bool& isAudio,
                                          bool& isSubtitles,
                                          bool& isHighDefinition,
                                          bool& isStandardDefinition,
                                          bool& isThreeDimensional,
                                          unsigned long& language);
    static bool DecodeContentDescriptor(unsigned char* data,
                                        unsigned char dataLength,
                                        vector<unsigned short>& contentTypeIds);
    static bool DecodeParentalRatingDescriptor(unsigned char* data,
                                                unsigned char dataLength,
                                                map<unsigned long, unsigned char>& ratings);
    static bool DecodePrivateDataSpecifierDescriptor(unsigned char* data,
                                                      unsigned char dataLength,
                                                      unsigned long& privateDataSpecifier);
    static bool DecodeContentIdentifierDescriptor(unsigned char* data,
                                                  unsigned char dataLength,
                                                  map<unsigned char, char*>& crids);
    static bool DecodeDishBevRatingDescriptor(unsigned char* data,
                                              unsigned char dataLength,
                                              unsigned char& starRating,
                                              unsigned char& mpaaClassification,
                                              unsigned short& advisories);
    static bool DecodeDishTextDescriptor(unsigned char* data,
                                          unsigned char dataLength,
                                          unsigned char tableId,
                                          char** text);
    static bool DecodeDishEpisodeInformationDescriptor(unsigned char* data,
                                                        unsigned char dataLength,
                                                        unsigned char tableId,
                                                        char** information);
    static bool DecodeDishVchipDescriptor(unsigned char* data,
                                          unsigned char dataLength,
                                          unsigned char& vchipRating,
                                          unsigned short& advisories);
    static bool DecodeDishBevSeriesDescriptor(unsigned char* data,
                                              unsigned char dataLength,
                                              char** seriesId,
                                              char** episodeId,
                                              bool& isPreviouslyShown);
    static bool DecodePremiereOrderInformationDescriptor(unsigned char* data,
                                                          unsigned char dataLength,
                                                          char** orderNumber,
                                                          char** price,
                                                          char** phoneNumber,
                                                          char** smsNumber,
                                                          char** url);
    static bool DecodePremiereParentInformationDescriptor(unsigned char* data,
                                                          unsigned char dataLength,
                                                          unsigned char& rating,
                                                          char** text);
    static bool DecodePremiereContentTransmissionDescriptor(unsigned char* data,
                                                            unsigned char dataLength,
                                                            unsigned short& originalNetworkId,
                                                            unsigned short& transportStreamId,
                                                            unsigned short& serviceId,
                                                            vector<unsigned long long>& showings);

    static unsigned long long DecodeDateTime(unsigned short dateMjd, unsigned long timeBcd);

    CCriticalSection m_section;
    map<unsigned short, bool> m_grabPids;
    bool m_grabFreesat;
    unsigned short m_freesatPidBat;
    unsigned short m_freesatPidEitPf;
    unsigned short m_freesatPidEitSchedule;
    unsigned short m_freesatPidNit;
    unsigned short m_freesatPidPmt;
    unsigned short m_freesatPidSdt;
    bool m_isSeen;
    bool m_isReady;
    clock_t m_completeTime;
    unsigned long m_unseenTableCount;
    unsigned long m_unseenSegmentCount;
    unsigned long m_unseenSectionCount;
    ICallBackGrabber* m_callBackGrabber;
    ICallBackPidConsumer* m_callBackPidConsumer;
    IDefaultAuthorityProvider* m_defaultAuthorityProvider;
    map<unsigned short, CSectionDecoder*> m_decoders;
    map<unsigned long long, CRecordEitService*> m_services;
    bool m_enableCrcCheck;

    CRecordEitService* m_currentService;
    unsigned short m_currentServiceIndex;
    CRecordEitEvent* m_currentEvent;
    unsigned short m_currentEventIndex;
    CRecordEitEventText* m_currentEventText;
    unsigned char m_currentEventTextIndex;
    CRecordEitEvent* m_referenceEvent;
};