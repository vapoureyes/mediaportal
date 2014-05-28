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

#ifndef __LOGGER_DEFINED
#define __LOGGER_DEFINED

#include "ParameterCollection.h"

#define LOGGER_NONE                                                           0
#define LOGGER_ERROR                                                          1
#define LOGGER_WARNING                                                        2
#define LOGGER_INFO                                                           3
#define LOGGER_VERBOSE                                                        4

// logging constants

// methods' names
#define METHOD_CONSTRUCTOR_NAME                                               L"ctor()"
#define METHOD_DESTRUCTOR_NAME                                                L"dtor()"

// methods' common string formats
#define METHOD_START_FORMAT                                                   L"%s: %s: Start"
#define METHOD_CONSTRUCTOR_START_FORMAT                                       L"%s: %s: Start, instance address: 0x%p"
#define METHOD_END_FORMAT                                                     L"%s: %s: End"
#define METHOD_END_HRESULT_FORMAT                                             L"%s: %s: End, result: 0x%08X"
#define METHOD_END_INT_FORMAT                                                 L"%s: %s: End, result: %d"
#define METHOD_END_INT64_FORMAT                                               L"%s: %s: End, result: %lld"
#define METHOD_END_FAIL_FORMAT                                                L"%s: %s: End, Fail"
#define METHOD_END_FAIL_HRESULT_FORMAT                                        L"%s: %s: End, Fail, result: 0x%08X"
#define METHOD_MESSAGE_FORMAT                                                 L"%s: %s: %s"

class CParameterCollection;
class CStaticLogger;
class CStaticLoggerContext;

class CLogger
{
public:
  // initializes a new instance of CLogger class
  // @param staticLogger : the instance of static logger
  // @param configuration : the collection of configuration parameters to initialize logger (verbosity and max log size)
  CLogger(CStaticLogger *staticLogger, CParameterCollection *configuration);

  // initializes a new instance of CLogger class with specified CLogger instance
  // new logger instance have same mutex, verbosity, log file and max log size
  // @param logger : logger instance to initialize new instance
  CLogger(CLogger *logger);

  ~CLogger(void);

  // log message to log file
  // @param logLevel : the log level of message
  // @param format : the formating string
  void Log(unsigned int logLevel, const wchar_t *format, ...);

  void SetParameters(CParameterCollection *configuration);

  // gets logger instance ID
  // @return : logger instance ID
  GUID GetLoggerInstanceId(void);

  // gets logger mutex
  // @return : logger mutex
  HANDLE GetMutex(void);

  // gets static logger context
  // @return : static logger context
  CStaticLoggerContext *GetStaticLoggerContext(void);

protected:
  // mutex for accessing log file
  HANDLE mutex;
  // the logger identifier
  GUID loggerInstance;
  // allowed verbosity (messages with higher verbosity are not logged)
  unsigned int allowedLogVerbosity;
  // holds instance of static logger
  CStaticLogger *staticLogger;

  // get human-readable value of log level
  // @param level : the level of message
  // @return : the human-readable log level
  static const wchar_t *GetLogLevel(unsigned int level);

  wchar_t *GetLogMessage(unsigned int logLevel, const wchar_t *format, va_list vl);

  void LogMessage(unsigned int logLevel, const wchar_t *message);
  void Log(unsigned int logLevel, const wchar_t *format, va_list vl);

private:
};

#endif