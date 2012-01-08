﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Objects;
using System.Data.SqlTypes;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVDatabase.EntityModel.Interfaces;
using Mediaportal.TV.Server.TVDatabase.EntityModel.Repositories;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer.Entities;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVDatabase.EntityModel.ObjContext;
using Channel = Mediaportal.TV.Server.TVDatabase.Entities.Channel;
using Program = Mediaportal.TV.Server.TVDatabase.Entities.Program;


namespace Mediaportal.TV.Server.TVDatabase.TVBusinessLayer
{

  

  public class ProgramManagement
  {
    #region delegates

    private delegate void DeleteProgramsDelegate();
    private delegate void InsertProgramsDelegate(ImportParams aImportParam);

    #endregion
    
    
    private static Thread _insertProgramsThread;
    private static readonly Queue<ImportParams> _programInsertsQueue = new Queue<ImportParams>();
    private static readonly AutoResetEvent _pendingProgramInserts = new AutoResetEvent(false);


    public static IDictionary<int, NowAndNext> GetNowAndNext(List<Channel> channels)
    {
      try
      {
        using (IProgramRepository programRepository = new ProgramRepository())
        {
          IEnumerable<Program> nowAndNextProgramsForChannels =
            programRepository.GetNowAndNextProgramsForChannels(channels);
          IDictionary<int, NowAndNext> nowAndNext = BuildNowNextFromResult(nowAndNextProgramsForChannels.ToList());
          return nowAndNext;
        }
      }
      catch (Exception ex)
      {
        Log.Error("ProgramManagement.GetNowAndNextProgramsForChannels ex={0}", ex);
        throw;
      }
    }

    private static IDictionary<int, NowAndNext> BuildNowNextFromResult(IList<Program> programs)
    {
      IDictionary<int, NowAndNext> progList = new Dictionary<int, NowAndNext>();

      int programsCount = programs.Count;
      var lastChannelIDs = new HashSet<int>();

      // for-loops are faster than foreach-loops
      for (int j = 0; j < programsCount; j++)
      {
        Program nowPrg = programs[j];
        int idChannel = nowPrg.idChannel;
        // Only get the Now-Next-Data _once_ per channel
        if (!lastChannelIDs.Contains(idChannel))
        {
          lastChannelIDs.Add(idChannel);

          int nowidProgram = nowPrg.idProgram;
          DateTime nowStart = nowPrg.startTime;
          DateTime nowEnd = nowPrg.endTime;
          string nowTitle = nowPrg.title;
          string episodeName = nowPrg.episodeName;
          string seriesNum = nowPrg.seriesNum;
          string episodeNum = nowPrg.episodeNum;
          string episodePart = nowPrg.episodePart;
          // if the first entry is not valid for the "Now" entry - use if for "Next" info
          if (nowStart > DateTime.Now)
          {
            var now = new NowAndNext(idChannel, SqlDateTime.MinValue.Value, SqlDateTime.MinValue.Value,
                                          String.Empty, nowTitle, -1, nowidProgram, episodeName, String.Empty,
                                          seriesNum, String.Empty, episodeNum, String.Empty,
                                          episodePart, String.Empty);
            progList[idChannel] = now;
            continue;
          }

          if (j < programsCount - 1)
          {
            // get the the "Next" info if it belongs to the same channel.
            Program nextProgram = programs[j + 1];
            if (idChannel == nextProgram.idChannel)
            {
              int nextidProgram = nextProgram.idProgram;
              string nextTitle = nextProgram.title;
              string nextEpisodeName = nextProgram.episodeName;
              string nextSeriesNum = nextProgram.seriesNum;
              string nextEpisodeNum = nextProgram.episodeNum;
              string nextEpisodePart = nextProgram.episodePart;
              var p = new NowAndNext(idChannel, nowStart, nowEnd, nowTitle, nextTitle, nowidProgram,
                                            nextidProgram, episodeName, nextEpisodeName, seriesNum, nextSeriesNum,
                                            episodeNum, nextEpisodeNum, episodePart, nextEpisodePart);
              progList[idChannel] = p;
            }
            else
            {
              // no "next" info because of holes in EPG data - we want the "now" info nevertheless
              var next = new NowAndNext(idChannel, nowStart, nowEnd, nowTitle, String.Empty, nowidProgram, -1,
                                            String.Empty, String.Empty, String.Empty, String.Empty, String.Empty,
                                            String.Empty, String.Empty, String.Empty);
              progList[idChannel] = next;
            }
          }
        }
      }
      return progList;
    }

    public void InsertPrograms(ImportParams importParams)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        programRepository.UnitOfWork.BeginTransaction();
        switch (importParams.ProgamsToDelete)
        {
          case DeleteBeforeImportOption.OverlappingPrograms:
            IEnumerable<ProgramListPartition> partitions = importParams.ProgramList.GetPartitions();
            DeleteProgramsByPartitions(programRepository, partitions);
            break;
          case DeleteBeforeImportOption.ProgramsOnSameChannel:
            IEnumerable<int> channelIds = importParams.ProgramList.GetChannelIds();
            DeleteProgramsByIds(programRepository, channelIds);
            break;
        }

        foreach (Program program in importParams.ProgramList)
        {
          SynchronizeDateHelpers(program); 
        }        

        programRepository.AddList(importParams.ProgramList);
        programRepository.UnitOfWork.CommitTransaction();
      }
      //no need to do a manual transaction rollback on UnitOfWork as it does this internally already in case of exceptions
    }

    private void DeleteProgramsByIds(IProgramRepository programRepository, IEnumerable<int> channelIds)
    {             
      programRepository.Delete<Program>(t => channelIds.Any(c => c == t.idChannel));      
    }

    private void DeleteProgramsByPartitions(IProgramRepository programRepository, IEnumerable<ProgramListPartition> deleteProgramRanges)
    {
      /*sqlCmd.CommandText =
      "DELETE FROM Program WHERE idChannel = @idChannel AND ((endTime > @rangeStart AND startTime < @rangeEnd) OR (startTime = endTime AND startTime BETWEEN @rangeStart AND @rangeEnd))";
      */

      
      foreach (ProgramListPartition partition in deleteProgramRanges)
      {
        programRepository.Delete<Program>(
          t =>
          t.idChannel == partition.IdChannel && ((t.endTime > partition.Start && t.startTime < partition.End)) ||
          (t.startTime == t.endTime && t.startTime >= partition.Start && t.startTime <= partition.End));
      }
      
    }    

    public static void DeleteAllPrograms()
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        programRepository.Delete<Program>(p=>p.idProgram > 0);
        programRepository.UnitOfWork.SaveChanges();

        string sql = "Delete FROM programs";
        programRepository.ObjectContext.ExecuteStoreCommand(sql);
        programRepository.ObjectContext.SaveChanges();
      }
    }

    public void PersistProgram(Program prg)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        programRepository.Add(prg);
        programRepository.UnitOfWork.SaveChanges();
      }
    }

    public IList<Program> FindAllProgramsByChannelId(int idChannel)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var query = programRepository.FindAllProgramsByChannelId(idChannel);
        query = programRepository.IncludeAllRelations(query).OrderBy(t => t.startTime);
        return query.ToList();
      }
    }

    public static IList<Program> GetProgramsByChannelAndStartEndTimes(int idChannel, DateTime startTime, DateTime endTime)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var query = programRepository.GetProgramsByStartEndTimes(startTime, endTime);
        query = query.Where(p => p.idChannel == idChannel);
        query = programRepository.IncludeAllRelations(query);
        query = query.OrderBy(p => p.startTime);
        return query.ToList();
      }
    }

    public static IList<Program> GetProgramsByChannelAndTitleAndStartEndTimes(int idChannel, string title, DateTime startTime, DateTime endTime)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var query = programRepository.GetProgramsByStartEndTimes(startTime, endTime);
        query = query.Where(p => p.idChannel == idChannel);
        query = query.Where(p => p.title == title);
        query = programRepository.IncludeAllRelations(query);
        query = query.OrderBy(p => p.startTime);
        return query.ToList();
      }
    }

    public static IList<Program> GetProgramsByTitleAndStartEndTimes(string title, DateTime startTime, DateTime endTime)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var query = programRepository.GetProgramsByStartEndTimes(startTime, endTime);
        query = query.Where(p => p.title == title);
        query = programRepository.IncludeAllRelations(query);
        query = query.OrderBy(p => p.startTime);
        return query.ToList();
      }
    }

    public void DeleteAllProgramsWithChannelId(int idChannel)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        programRepository.DeleteAllProgramsWithChannelId(idChannel);
      }
    }

    public static IList<Program> GetNowAndNextProgramsForChannel(int idChannel)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        return programRepository.GetNowAndNextProgramsForChannel(idChannel).ToList();
      }
    }

    public static Program GetProgramAt(DateTime date, int idChannel)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        return programRepository.GetProgramAt(date, idChannel);
      }
    }

    public static Program GetProgramAt(DateTime date, string title)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        return programRepository.GetProgramAt(date, title);
      }
    }

    public void PersistPrograms(IEnumerable<Program> programs)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        foreach (var program in programs)
        {
          programRepository.Add(program);
        }
        programRepository.UnitOfWork.SaveChanges();
      }
    }

    /// <summary>
    /// Batch inserts programs - intended for faster EPG import. You must make sure before that there are no duplicates 
    /// (e.g. delete all program data of the current channel).
    /// Also you MUST provide a true copy of "aProgramList". If you update it's reference in your own code the values will get overwritten
    /// (possibly before they are written to disk)!
    /// </summary>
    /// <param name="aProgramList">A list of persistable gentle.NET Program objects mapping to the Programs table</param>
    /// <param name="progamsToDelete">Flag specifying which existing programs to delete before the insert</param>
    /// <param name="aThreadPriority">Use "Lowest" for Background imports allowing LiveTV, AboveNormal for full speed</param>
    /// <returns>The record count of programs if successful, 0 on errors</returns>
    /// <remarks><para>Inserts are queued to be performed in the background. Each batch of inserts is executed in a single transaction.
    /// You may also optionally specify to delete either all existing programs in the same channel(s) as the programs to be inserted 
    /// (<see cref="DeleteBeforeImportOption.ProgramsOnSameChannel"/>), or existing programs that would otherwise overlap new programs
    /// (<see cref="DeleteBeforeImportOption.OverlappingPrograms"/>), or none (<see cref="DeleteBeforeImportOption.None"/>).
    /// The deletion is also performed in the same transaction as the inserts so that EPG will not be at any time empty.</para>
    /// <para>After all insert have completed and the background thread is idle for 60 seconds, the program states are
    /// automatically updated to reflect the changes.</para></remarks>
    public int InsertPrograms(List<Program> aProgramList, DeleteBeforeImportOption progamsToDelete, ThreadPriority aThreadPriority)
    {
      try
      {
        int sleepTime = 10;

        switch (aThreadPriority)
        {
          case ThreadPriority.Highest:
          case ThreadPriority.AboveNormal:
            aThreadPriority = ThreadPriority.Normal;
            sleepTime = 0;
            break;
          case ThreadPriority.Normal:
            // this is almost enough on dualcore systems for one cpu to gather epg and the other to insert it
            sleepTime = 10;
            break;
          case ThreadPriority.BelowNormal: // on faster systems this might be enough for background importing
            sleepTime = 20;
            break;
          case ThreadPriority.Lowest: // even a single core system is enough to use MP while importing.
            sleepTime = 40;
            break;
        }

        ImportParams param = new ImportParams();
        param.ProgramList = new ProgramList(aProgramList);
        param.ProgamsToDelete = progamsToDelete;
        param.SleepTime = sleepTime;
        param.Priority = aThreadPriority;

        lock (_programInsertsQueue)
        {
          _programInsertsQueue.Enqueue(param);
          _pendingProgramInserts.Set();

          if (_insertProgramsThread == null)
          {
            _insertProgramsThread = new Thread(InsertProgramsThreadStart)
            {
              Priority = ThreadPriority.Lowest,
              Name = "SQL EPG importer",
              IsBackground = true
            };
            _insertProgramsThread.Start();
          }
        }

        return aProgramList.Count;
      }
      catch (Exception ex)
      {
        Log.Error("BusinessLayer: InsertPrograms error - {0}, {1}", ex.Message, ex.StackTrace);
        return 0;
      }
    }    

    public static void InitiateInsertPrograms()
    {
      Thread currentInsertThread = _insertProgramsThread;
      if (currentInsertThread != null &&
          (currentInsertThread.ThreadState & ThreadState.Unstarted) != ThreadState.Unstarted)
        currentInsertThread.Join();
    }

    /// <summary>
    /// Batch inserts programs - intended for faster EPG import. You must make sure before that there are no duplicates 
    /// (e.g. delete all program data of the current channel).
    /// Also you MUST provide a true copy of "aProgramList". If you update it's reference in your own code the values will get overwritten
    /// (possibly before they are written to disk)!
    /// </summary>
    /// <param name="aProgramList">A list of persistable gentle.NET Program objects mapping to the Programs table</param>
    /// <param name="aThreadPriority">Use "Lowest" for Background imports allowing LiveTV, AboveNormal for full speed</param>
    /// <returns>The record count of programs if successful, 0 on errors</returns>
    public int InsertPrograms(List<Program> aProgramList, ThreadPriority aThreadPriority)
    {
      return InsertPrograms(aProgramList, DeleteBeforeImportOption.None, aThreadPriority);
    }

    private void InsertProgramsThreadStart()
    {
      try
      {
        Log.Debug("BusinessLayer: InsertProgramsThread started");
        DateTime lastImport = DateTime.Now;
        while (true)
        {
          if (lastImport.AddSeconds(60) < DateTime.Now)
          {
            // Done importing and 60 seconds since last import
            // Remove old programs
            
            // Let's update states            
            using (IProgramRepository programRepository = new ProgramRepository())
            {
              SynchProgramStatesForAllSchedules(programRepository.GetAll<Schedule>());
            }
            // and exit
            lock (ProgramManagement._programInsertsQueue)
            {
              //  Has new work been queued in the meantime?
              if (ProgramManagement._programInsertsQueue.Count == 0)
              {
                Log.Debug("BusinessLayer: InsertProgramsThread exiting");
                ProgramManagement._insertProgramsThread = null;
                break;
              }
            }
          }

          ProgramManagement._pendingProgramInserts.WaitOne(10000); // Check every 10 secs
          while (ProgramManagement._programInsertsQueue.Count > 0)
          {
            try
            {
              ImportParams importParams;
              lock (ProgramManagement._programInsertsQueue)
              {
                importParams = ProgramManagement._programInsertsQueue.Dequeue();
              }
              Thread.CurrentThread.Priority = importParams.Priority;
              InsertPrograms(importParams);
              Log.Debug("BusinessLayer: Inserted {0} programs to the database", importParams.ProgramList.Count);
              lastImport = DateTime.Now;
              Thread.CurrentThread.Priority = ThreadPriority.Lowest;
            }
            catch (Exception ex)
            {
              Log.Error("BusinessLayer: InsertMySQL/InsertMSSQL caused an exception:");
              Log.Write(ex);
            }
          }          
        }
      }
      catch (Exception ex)
      {
        Log.Error("BusinessLayer: InsertProgramsThread error - {0}, {1}", ex.Message, ex.StackTrace);
      }
    }

    public static void SynchProgramStatesForAllSchedules(IEnumerable<Schedule> schedules)
    {    
      Log.Info("SynchProgramStatesForAllSchedules");
      
      if (schedules != null)
      {
        foreach (TVDatabase.Entities.Schedule schedule in schedules)
        {          
          SynchProgramStates(new ScheduleBLL(schedule));
        }
      }
    }

    public void InitiateInsertPrograms(int millisecondsTimeout)
    {
      Thread currentInsertThread = ProgramManagement._insertProgramsThread;
      if (currentInsertThread != null &&
          (currentInsertThread.ThreadState & ThreadState.Unstarted) != ThreadState.Unstarted)
        currentInsertThread.Join(millisecondsTimeout);
    }


    public static Program RetrieveByTitleTimesAndChannel(string programName, DateTime startTime, DateTime endTime, int idChannel)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var query = programRepository.GetQuery<Program>(
          p => p.title == programName && p.startTime == startTime && p.endTime == endTime && p.idChannel == idChannel);
        query = programRepository.IncludeAllRelations(query);
        return query.FirstOrDefault();
      }
    }

    public static IList<Program> RetrieveDaily(DateTime startTime, DateTime endTime, int idChannel)
    {
      return RetrieveDaily(startTime, endTime, idChannel, -1);
    }

    public static IList<Program> RetrieveDaily(DateTime startTime, DateTime endTime, int channelId, int maxDays)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        DateTime now = DateTime.Now;
        var query = programRepository.GetQuery<Program>(p => p.endTime >= now && p.idChannel == channelId);
        if (maxDays > 0)
        {
          query = query.Where(p => p.startTime < now.AddDays(maxDays));
        }        
        query = AddTimeRangeConstraint(query, startTime, endTime);
        query = programRepository.IncludeAllRelations(query);
        return query.ToList();
      }     
    }

    /*private static IQueryable<Program> AddTimeRangeConstraint(IQueryable<Program> query, DateTime startTime, DateTime endTime)
    {      
      TimeSpan startOffset = startTime.TimeOfDay;
      TimeSpan endOffset = endTime - startTime.Date;
      if (startOffset > endOffset)
      {
        endOffset = endOffset.Add(new TimeSpan(1,0,0,0)); //AddDays(1);
      }
      query = query.Where(p =>
         p.startTime < p.startTime.Subtract(p.startTime.TimeOfDay).Add(endOffset) 
          && p.endTime > p.startTime.Subtract(p.startTime.TimeOfDay).Add(startOffset)
          ||
           p.startTime < p.startTime.Subtract(p.startTime.TimeOfDay).Add(endOffset.Add(new TimeSpan(-1, 0, 0, 0)))
           && p.endTime > p.startTime.Subtract(p.startTime.TimeOfDay).Add(startOffset.Add(new TimeSpan(-1, 0, 0, 0)))
         ||
                p.startTime < p.startTime.Subtract(p.startTime.TimeOfDay).Add(endOffset.Add(new TimeSpan(1, 0, 0, 0)))
            && p.endTime > p.startTime.Subtract(p.startTime.TimeOfDay).Add(startOffset.Add(new TimeSpan(1, 0, 0, 0)))
          );
      return query;
    }*/

    // TODO workaround/hack for Entity Framework not currently (as of 21-11-2011) supporting DayOfWeek and other similar date functions.
    // once this gets sorted, if ever, this code should be refactored. The db helper fields should be deleted as they have no purpose.
    #region hack
    private static IQueryable<Program> AddTimeRangeConstraint(IQueryable<Program> query, DateTime startTime, DateTime endTime)
    {
      TimeSpan startOffset = startTime.TimeOfDay;
      TimeSpan endOffset = endTime - startTime.Date;
      if (startOffset > endOffset)
      {
        endOffset = endOffset.Add(new TimeSpan(1, 0, 0, 0));
      }

      DateTime endDateTimeOffset1 = CreateDateTimeFromTimeSpan(endOffset.Add(new TimeSpan(-1, 0, 0, 0)));
      DateTime endDateTimeOffset2 = CreateDateTimeFromTimeSpan(endOffset.Add(new TimeSpan(1, 0, 0, 0)));
      DateTime endDateTimeOffset = CreateDateTimeFromTimeSpan(endOffset);
      DateTime startDateTimeOffset1 = CreateDateTimeFromTimeSpan(startOffset.Add(new TimeSpan(-1, 0, 0, 0)));
      DateTime startDateTimeOffset2 = CreateDateTimeFromTimeSpan(startOffset.Add(new TimeSpan(1, 0, 0, 0)));
      DateTime startDateTimeOffset = CreateDateTimeFromTimeSpan(startOffset);

      query = query.Where(p =>
           p.startTimeOffset < endDateTimeOffset
           && p.endTimeOffset > startDateTimeOffset
         ||
           p.startTimeOffset < endDateTimeOffset1
           && p.endTimeOffset > startDateTimeOffset1
         ||
           p.startTimeOffset < endDateTimeOffset2
           && p.endTimeOffset > startDateTimeOffset2
          );
      return query;
    } 
    #endregion

    public static IList<Program> RetrieveEveryTimeOnEveryChannel(string title)
    {
      IList<Program> retrieveByTitleAndTimesInterval = RetrieveByTitleAndTimesInterval(title, DateTime.Now, DateTime.MaxValue);
      return retrieveByTitleAndTimesInterval;
    }

    public static IList<Program> RetrieveByTitleAndTimesInterval(string title, DateTime startTime, DateTime endTime)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var query =
          programRepository.GetQuery<Program>(p => p.title == title && p.endTime >= startTime && p.startTime <= endTime);
        query = programRepository.IncludeAllRelations(query);
        return query.ToList();
      }
    }

    public static IList<Program> RetrieveEveryTimeOnThisChannel(string title, int channelId)
    {
      return RetrieveEveryTimeOnThisChannel(title, channelId, -1);
    }

    public static IList<Program> RetrieveEveryTimeOnThisChannel(string title, int channelId, int maxDays)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        DateTime startTime = DateTime.Now;
        DateTime endTime = maxDays > 0 ? startTime.AddDays(maxDays) : DateTime.MaxValue;
        var query = programRepository.GetQuery<Program>(
          p => p.title == title && p.endTime >= startTime && p.endTime < endTime && p.idChannel == channelId);
        query = programRepository.IncludeAllRelations(query);
        return query.ToList();
      }
    }

    public static IList<Program> RetrieveWeeklyEveryTimeOnThisChannel(DateTime startTime, string title,
                                                                      int channelId)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var query =
          programRepository.GetQuery<Program>(
            p =>
            p.title == title && p.idChannel == channelId && p.startTime >= DateTime.Now &&
            p.endTime <= DateTime.MaxValue);        
        query = AddWeekdayConstraint(query, startTime.DayOfWeek);
        query = programRepository.IncludeAllRelations(query);        
        return query.ToList();
      }
    }

    private static IQueryable<Program>AddWeekdayConstraint(IQueryable<Program> query, DayOfWeek dayOfWeek)
    {      
      // TODO workaround/hack for Entity Framework not currently (as of 21-11-2011) supporting DayOfWeek and other similar date functions.
      // once this gets sorted, if ever, this code should be refactored. The db helper fields should be deleted as they have no purpose.
      #region hack
      query = query.Where(p => p.startTimeDayOfWeek == (int)dayOfWeek);
      #endregion

      //query = query.Where(p => p.startTime.DayOfWeek == dayOfWeek);
      
      return query;
    }

    public static IList<Program> RetrieveWeekends(DateTime startTime, DateTime endTime, int channelId)
    {
      return RetrieveWeekends(startTime, endTime, channelId, -1);
    }

    public static IList<Program> RetrieveWeekends(DateTime startTime, DateTime endTime, int channelId, int maxDays)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        DateTime now = DateTime.Now;
        var query = programRepository.GetQuery<Program>(p => p.endTime >= now && p.idChannel == channelId);
        if (maxDays > 0)
        {
          query = query.Where(p => p.startTime < now.AddDays(maxDays));
        }
        query = AddWeekendsConstraint(query);
        query = AddTimeRangeConstraint(query, startTime, endTime);
        query = programRepository.IncludeAllRelations(query);
        return query.ToList();
      }          
    }

    private static IQueryable<Program> AddWeekendsConstraint(IQueryable<Program> query)
    {
      DayOfWeek firstWeekendDay = WeekEndTool.FirstWeekendDay;
      DayOfWeek secondWeekendDay = WeekEndTool.SecondWeekendDay;      

      // TODO workaround/hack for Entity Framework not currently (as of 21-11-2011) supporting DayOfWeek and other similar date functions.
      // once this gets sorted, if ever, this code should be refactored. The db helper fields should be deleted as they have no purpose.
      #region hack
      query = query.Where(p => p.startTimeDayOfWeek == (int)firstWeekendDay || p.startTimeDayOfWeek == (int)secondWeekendDay);
      #endregion

      //query = query.Where(p => p.startTime.DayOfWeek == firstWeekendDay || p.startTime.DayOfWeek == secondWeekendDay);

      return query;
    }

    public static IList<Program> RetrieveWeekly(DateTime startTime, DateTime endTime, int channelId)
    {
      return RetrieveWeekly(startTime, endTime, channelId, -1);
    }

    public static IList<Program> RetrieveWeekly(DateTime startTime, DateTime endTime, int channelId, int maxDays)
    {     
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        DateTime now = DateTime.Now;
        var query = programRepository.GetQuery<Program>(p => p.endTime >= now && p.idChannel == channelId);
        if (maxDays > 0)
        {
          query = query.Where(p => p.startTime < now.AddDays(maxDays));
        }        
        query = AddWeekdayConstraint(query, startTime.DayOfWeek);
        query = AddTimeRangeConstraint(query, startTime, endTime);
        query = programRepository.IncludeAllRelations(query);
        return query.ToList();
      }     
    }

    public static IList<Program> RetrieveWorkingDays(DateTime startTime, DateTime endTime, int channelId)
    {
      return RetrieveWorkingDays(startTime, endTime, channelId, -1);
    }

    public static IList<Program> RetrieveWorkingDays(DateTime startTime, DateTime endTime, int channelId, int maxDays)
    {

      using (IProgramRepository programRepository = new ProgramRepository())
      {
        DateTime now = DateTime.Now;
        var query = programRepository.GetQuery<Program>(p => p.endTime >= now && p.idChannel == channelId);
        if (maxDays > 0)
        {
          query = query.Where(p => p.startTime < now.AddDays(maxDays));
        }
        query = AddWorkingDaysConstraint(query);
        query = AddTimeRangeConstraint(query, startTime, endTime);
        query = programRepository.IncludeAllRelations(query);
        return query.ToList();
      }     
    }

    private static IQueryable<Program> AddWorkingDaysConstraint(IQueryable<Program> query)
    {
      var firstWeekendDay = WeekEndTool.FirstWeekendDay;
      var secondWeekendDay = WeekEndTool.SecondWeekendDay;

      // TODO workaround/hack for Entity Framework not currently (as of 21-11-2011) supporting DayOfWeek and other similar date functions.
      // once this gets sorted, if ever, this code should be refactored. The db helper fields should be deleted as they have no purpose.
      #region hack
      query = query.Where(p => p.startTimeDayOfWeek != (int)firstWeekendDay && p.startTimeDayOfWeek != (int)secondWeekendDay);
      #endregion
      //query = query.Where(p => p.startTime.DayOfWeek != firstWeekendDay && p.startTime.DayOfWeek != secondWeekendDay);
      return query;    
    }

    public static Program SaveProgram(Program program)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {

        SynchronizeDateHelpers(program);

        programRepository.AttachEntityIfChangeTrackingDisabled(programRepository.ObjectContext.Programs, program);        
        programRepository.ApplyChanges(programRepository.ObjectContext.Programs, program);
        programRepository.UnitOfWork.SaveChanges();
        program.AcceptChanges();
        return program;
      }
    }

    private static DateTime CreateDateTimeFromTimeSpan(TimeSpan timeSpan)
    {
      var date = new DateTime(2000, 1, 1).Add(timeSpan);
      return date;
    }    

    private static void SynchronizeDateHelpers(Program program)
    {
      // TODO workaround/hack for Entity Framework not currently (as of 21-11-2011) supporting DayOfWeek and other similar date functions.
      // once this gets sorted, if ever, this code should be refactored. The db helper fields should be deleted as they have no purpose.

      #region hack

      DateTime startDateTime = program.startTime;
      DateTime endDateTime = program.endTime;
      program.startTimeDayOfWeek = (short) startDateTime.DayOfWeek;
      program.endTimeDayOfWeek = (short) endDateTime.DayOfWeek;
      program.startTimeOffset = CreateDateTimeFromTimeSpan(startDateTime.TimeOfDay);
      program.endTimeOffset = CreateDateTimeFromTimeSpan(endDateTime.Subtract(startDateTime.Date));

      #endregion
    }


    public static IList<Program> GetProgramsByTitleAndTimesInterval(string title, DateTime startTime, DateTime endTime)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var programsByTitleAndTimesInterval =
          programRepository.GetQuery<Program>(
            p => p.title == title && p.endTime >= startTime && p.startTime <= startTime).Include(p => p.ProgramCategory);
        return programsByTitleAndTimesInterval.ToList();
      }
    }

    public static Program GetProgramsByTitleTimesAndChannel(string programName, DateTime startTime, DateTime endTime, int idChannel)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var programsByTitleTimesAndChannel = programRepository.GetQuery<Program>(
          p => p.title == programName && p.startTime == startTime && p.endTime == endTime && p.idChannel == idChannel).
          Include(p => p.ProgramCategory).FirstOrDefault();
        return programsByTitleTimesAndChannel;
      }
    }

    public static IList<Program> GetProgramsByState(ProgramState state)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var programsByState =
          programRepository.GetQuery<Program>(p => (p.state & (int) state) == (int) state).Include(
            p => p.ProgramCategory);
        return programsByState.ToList();
      }
    }

    public static Program GetProgramByTitleAndTimes(string programName, DateTime startTime, DateTime endTime)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var programByTitleAndTimes = programRepository.GetQuery<Program>(
          p => p.title == programName && p.startTime == startTime && p.endTime == endTime).Include(
            p => p.ProgramCategory).FirstOrDefault();
        return programByTitleAndTimes;
      }
    }

    public static IList<Program> GetProgramsByDescription(string searchCriteria, MediaTypeEnum mediaType, StringComparisonEnum stringComparison)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        IQueryable<Program> query = programRepository.GetQuery<Program>();
        query = programRepository.GetProgramsByDescription(query, searchCriteria, stringComparison);
        query = query.Where(p => p.Channel.mediaType == (int) mediaType);
        query = programRepository.IncludeAllRelations(query);
        return query.ToList();
      }
    }

    public static IList<Program> GetProgramsByDescription(string searchCriteria, StringComparisonEnum stringComparison)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        //todo gibman: check if those stringComparison that contains reg. expr. are indeed working.. client calls with reg exp.
        IQueryable<Program> programsByDescription = programRepository.GetQuery<Program>();
        programsByDescription = programRepository.GetProgramsByDescription(programsByDescription, searchCriteria,
                                                                            stringComparison);
        programsByDescription = programRepository.IncludeAllRelations(programsByDescription);
        return programsByDescription.ToList();
      }
    }

    public static IList<Program> GetProgramsByTitle(string searchCriteria, StringComparisonEnum stringComparison)
    {
      //todo gibman: check if those stringComparison that contains reg. expr. are indeed working.. client calls with reg exp.
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        IQueryable<Program> programsByTitle = programRepository.GetQuery<Program>();
        programsByTitle = programRepository.GetProgramsByTitle(programsByTitle, searchCriteria, stringComparison);
        programsByTitle = programRepository.IncludeAllRelations(programsByTitle);
        return programsByTitle.ToList();
      }
    }

    public IList<Program> GetProgramsByCategory(string searchCriteria, StringComparisonEnum stringComparison)
    {
      //todo gibman: check if those stringComparison that contains reg. expr. are indeed working.. client calls with reg exp.
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        IQueryable<Program> programsByCategory = programRepository.GetQuery<Program>();
        programsByCategory = programRepository.GetProgramsByCategory(programsByCategory, searchCriteria,
                                                                      stringComparison);
        programsByCategory = programRepository.IncludeAllRelations(programsByCategory);
        return programsByCategory.ToList();
      }
    }

    public static IList<Program> GetProgramsByTitle(string searchCriteria, MediaTypeEnum mediaType, StringComparisonEnum stringComparisonEnum)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        IQueryable<Program> programsByTitle = programRepository.GetQuery<Program>();
        programsByTitle = programRepository.GetProgramsByTitle(programsByTitle, searchCriteria, stringComparisonEnum);
        programsByTitle = programsByTitle.Where(p => p.Channel.mediaType == (int) mediaType);
        programsByTitle = programRepository.IncludeAllRelations(programsByTitle);
        return programsByTitle.ToList();
      }
    }

    public static Program GetProgram(int idProgram)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var findOne =
          programRepository.GetQuery<Program>(p => p.idProgram == idProgram).Include(p => p.ProgramCategory).
            FirstOrDefault();
        return findOne;
      }
    }

    public static void SetSingleStateSeriesPending(DateTime startTime, int idChannel, string programName)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        Program program = programRepository.FindOne<Program>(
          p => p.title == programName && p.startTime == startTime && p.idChannel == idChannel);

        var programBll = new ProgramBLL(program)
                                  {IsRecordingOncePending = false, IsRecordingSeriesPending = true};
        programRepository.Update(programBll.Entity);
        programRepository.UnitOfWork.SaveChanges();
      }
    }

    public static IList<Program> GetProgramsForAllChannels(IEnumerable<Channel> channels)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        IList<int> channelIds = channels.Select(channel => channel.idChannel).ToList();
        var buildContainsExpression = programRepository.BuildContainsExpression<Channel, int>(e => e.idChannel,
                                                                                              channelIds);

        IQueryable<Program> query = programRepository.GetQuery<Program>(
          p => (
               programRepository.ObjectContext.Channels.Where(buildContainsExpression).Any(
                 c => c.idChannel == p.idChannel)));

        return query.ToList();
      }
    }

    public static IDictionary<int, IList<Program>> GetProgramsForAllChannels(DateTime startTime, DateTime endTime, IEnumerable<Channel> channels)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        IList<int> channelIds = channels.Select(channel => channel.idChannel).ToList();
        var buildContainsExpression = programRepository.BuildContainsExpression<Channel, int>(e => e.idChannel,
                                                                                              channelIds);

        IQueryable<Program> query = programRepository.GetQuery<Program>(
          p => (p.endTime > startTime && p.endTime < endTime) ||
               (p.startTime >= startTime && p.startTime <= endTime) ||
               (p.startTime <= startTime && p.endTime >= endTime)
               &&
               programRepository.ObjectContext.Channels.Where(buildContainsExpression).Any(
                 c => c.idChannel == p.idChannel)).OrderBy(p => p.startTime).Include(p => p.ProgramCategory).Include(
                   p => p.Channel);

        IDictionary<int, IList<Program>> maps = new Dictionary<int, IList<Program>>();

        foreach (Program program in query)
        {
          if (!maps.ContainsKey(program.idChannel))
          {
            maps[program.idChannel] = new List<Program>();
          }
          maps[program.idChannel].Add(program);
        }
        return maps;
      }
    }

    public static IList<Program> GetProgramsByTitleAndCategoryAndMediaType(string categoryCriteriea, string titleCriteria, MediaTypeEnum mediaType, StringComparisonEnum stringComparisonCategory, StringComparisonEnum stringComparisonTitle)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        IQueryable<Program> query = programRepository.GetQuery<Program>();
        query = programRepository.GetProgramsByTitle(query, titleCriteria, stringComparisonTitle);
        query = programRepository.GetProgramsByCategory(query, categoryCriteriea, stringComparisonCategory);
        query = query.Where(p => p.Channel.mediaType == (int) mediaType);
        query = programRepository.IncludeAllRelations(query);
        return query.ToList();
      }
    }

    public static IList<Program> GetProgramsByTimesInterval(DateTime startTime, DateTime endTime)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var programsByTimesInterval = programRepository.GetProgramsByTimesInterval(startTime, endTime);
        return programsByTimesInterval.ToList();
      }
    }

    public static void SynchProgramStates(ScheduleBLL schedule)
    {      
      if (schedule == null)
      {
        return;
      }

      IEnumerable<Program> programs = GetProgramsForSchedule(schedule.Entity);
      
      foreach (var prog in programs)
      {
        var programBll = new ProgramBLL(prog);
        if(schedule.IsSerieIsCanceled(schedule.GetSchedStartTimeForProg(prog)))
        {
          // program has been cancelled so reset any pending recording flags          
          ResetPendingState(programBll);
        }
        else
        {
          bool isPartialRecording = schedule.IsPartialRecording(prog);
          if (schedule.Entity.scheduleType == (int)ScheduleRecordingType.Once)
          {
            // is one off recording that is still active so set pending flags accordingly
            programBll.IsRecordingOncePending = true;
            programBll.IsRecordingSeriesPending = false;
            programBll.IsPartialRecordingSeriesPending = false;
            SaveProgram(programBll.Entity);
          }
          else if (isPartialRecording)
          {
            // is part of a series recording but is a time based schedule and program times do not
            // match up with schedule times so flag as partial recording
            programBll.IsRecordingOncePending = false;
            programBll.IsRecordingSeriesPending = false;
            programBll.IsPartialRecordingSeriesPending = true;
            SaveProgram(programBll.Entity);
          }
          else
          {
            // is part of a series recording but is not a partial recording
            programBll.IsRecordingOncePending = false;
            programBll.IsRecordingSeriesPending = true;
            programBll.IsPartialRecordingSeriesPending = false;
            SaveProgram(programBll.Entity);
          }
        }
      }
    }

    private static void ResetPendingState(ProgramBLL prog)
    {
      if (prog != null)
      {
        prog.IsRecordingOncePending = false;
        prog.IsRecordingSeriesPending = false;
        prog.IsPartialRecordingSeriesPending = false;

        SaveProgram(prog.Entity);
      }
    }
    public static IList<Program> GetProgramsForSchedule(TVDatabase.Entities.Schedule schedule)
    {      
      IList<Program> progsEntities = new List<Program>();
      switch (schedule.scheduleType)
      {
        case (int)ScheduleRecordingType.Once:
          var prgOnce = RetrieveByTitleTimesAndChannel(schedule.programName, schedule.startTime, schedule.endTime,
                                                        schedule.idChannel);
          if (prgOnce != null)
          {
            progsEntities.Add(prgOnce);
          }
          return progsEntities;

        case (int)ScheduleRecordingType.Daily:
          progsEntities = RetrieveDaily(schedule.startTime, schedule.endTime, schedule.idChannel);
          break;

        case (int)ScheduleRecordingType.EveryTimeOnEveryChannel:
          progsEntities = RetrieveEveryTimeOnEveryChannel(schedule.programName).ToList();
          return progsEntities;

        case (int)ScheduleRecordingType.EveryTimeOnThisChannel:
          progsEntities = RetrieveEveryTimeOnThisChannel(schedule.programName, schedule.idChannel).ToList();
          return progsEntities;

        case (int)ScheduleRecordingType.WeeklyEveryTimeOnThisChannel:
          progsEntities = RetrieveWeeklyEveryTimeOnThisChannel(schedule.startTime, schedule.programName, schedule.idChannel).ToList();
          return progsEntities;

        case (int)ScheduleRecordingType.Weekends:
          progsEntities = RetrieveWeekends(schedule.startTime, schedule.endTime, schedule.idChannel);          
          break;

        case (int)ScheduleRecordingType.Weekly:
          progsEntities = RetrieveWeekly(schedule.startTime, schedule.endTime, schedule.idChannel);
          break;

        case (int)ScheduleRecordingType.WorkingDays:
          progsEntities = RetrieveWorkingDays(schedule.startTime, schedule.endTime, schedule.idChannel);
          break;
      }
      return progsEntities;
    }

    /*public static IList<Program> ConvertGentleToEntities(IEnumerable<Gentle.Program> progs)
    {
      IList<Program> prgEntities = new List<Program>();
      foreach (var program in progs)
      {
        Program entity = new Program();
        entity.classification = program.Classification;
        entity.description = program.Description;
        entity.endTime = program.endTime;
        entity.startTime = program.startTime;
        entity.title = program.Title;
        entity.starRating = program.StarRating;
        entity.idChannel = program.idChannel;
        entity.idProgram = program.IdProgram;
        entity.episodeName = program.EpisodeName;
        entity.episodeNum = program.EpisodeNum;
        entity.episodePart = program.EpisodePart;
        entity.originalAirDate = program.OriginalAirDate;
        entity.parentalRating = program.ParentalRating;
        prgEntities.Add(entity);
      }
      return prgEntities;
    }*/

    public static void ResetAllStates()
    {
      //string sql = "Update Programs set state=0 where state<>0;";
      //SqlStatement stmt = new SqlStatement(StatementType.Update, Broker.Provider.GetCommand(), sql);
      //stmt.Execute();
      //CacheManager.ClearQueryResultsByType(typeof(Program));
      
      //TODO implement Future recording as discussed
    }

    public static IList<Program> RetrieveCurrentRunningByTitle(string programName, int preRecordInterval, int postRecordInterval)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        DateTime now = DateTime.Now;
        return programRepository.GetQuery<Program>(p => p.title == programName && p.startTime <= now.AddMinutes(preRecordInterval) && p.endTime > now).ToList();
      }
    }

    public static ProgramCategory GetProgramCategoryByName(string category)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        return programRepository.FindOne<ProgramCategory>(p => p.category == category);
      }
    }

    public static IList<string> ListAllDistinctCreditRoles()
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {        
        return programRepository.GetQuery<ProgramCredit>().Select(p=>p.role).Distinct().ToList();
      }
    }

    public static IList<ProgramCategory> ListAllCategories()
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        return programRepository.GetAll<ProgramCategory>().ToList();
      }
    }

    public static IList<ProgramCredit> ListAllCredits()
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        return programRepository.GetAll<ProgramCredit>().ToList();
      }
    }

    public static IList<Program> RetrieveByChannelAndTimesInterval(int channelId, DateTime startTime, DateTime endTime)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var query =
          programRepository.GetQuery<Program>(p => p.idChannel == channelId && p.startTime >= startTime && p.endTime <= endTime);
        query = programRepository.IncludeAllRelations(query);
        return query.ToList();
      }
    }

    public static void DeleteProgram(int idProgram)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        programRepository.Delete<Program>(p => p.idProgram == idProgram);
        programRepository.UnitOfWork.SaveChanges();
      }
    }

    public static void DeleteOldPrograms()
    {
      DateTime dtYesterday = DateTime.Now.AddHours(-SettingsManagement.EpgKeepDuration);
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        programRepository.Delete<Program>(p => p.endTime < dtYesterday);
        programRepository.UnitOfWork.SaveChanges();
      }
    }

    public static void DeleteOldPrograms(int idChannel)
    {      
      DateTime dtYesterday = DateTime.Now.AddHours(-SettingsManagement.EpgKeepDuration);
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        programRepository.Delete<Program>(p => p.endTime < dtYesterday && p.idChannel == idChannel);
        programRepository.UnitOfWork.SaveChanges();
      }
    }

    public static IList<Program> GetPrograms(int idChannel, DateTime startTime)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        return programRepository.GetQuery<Program>(p => p.idChannel == idChannel && p.startTime >= startTime).OrderBy(p => p.startTime).ToList();
      }
    }

    public static DateTime GetNewestProgramForChannel(int idChannel)
    {
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        Program program = programRepository.GetQuery<Program>(p => p.idChannel == idChannel).OrderByDescending(p => p.startTime).FirstOrDefault();
        if (program != null)
        {
          return program.startTime;
        }
        else
        {
          return DateTime.MinValue;
        }
      }
    }

    public static IList<Program> GetProgramExists(int idChannel, DateTime startTime, DateTime endTime)
    {      
      /*
         string sub1 =
        String.Format("( (StartTime >= '{0}' and StartTime < '{1}') or ( EndTime > '{0}' and EndTime <= '{1}' ) )",
                      startTime.ToString(GetDateTimeString(), mmddFormat),
                      endTime.ToString(GetDateTimeString(), mmddFormat));
      string sub2 = String.Format("(StartTime < '{0}' and EndTime > '{1}')",
                                  startTime.ToString(GetDateTimeString(), mmddFormat),
                                  endTime.ToString(GetDateTimeString(), mmddFormat));

      sb.AddConstraint(Operator.Equals, "idChannel", channel.IdChannel);
      sb.AddConstraint(string.Format("({0} or {1}) ", sub1, sub2));
      sb.AddOrderByField(true, "starttime");
      */
      using (IProgramRepository programRepository = new ProgramRepository())
      {
        var query = programRepository.GetProgramsByTimesInterval(startTime, endTime);
        query = query.Where(p => p.idChannel == idChannel);
        return query.ToList();
      }
    }
  }
 }