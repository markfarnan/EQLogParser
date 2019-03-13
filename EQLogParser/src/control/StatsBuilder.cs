﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace EQLogParser
{
  class StatsBuilder
  {
    protected const string RAID_PLAYER = "Totals";
    protected const string TIME_FORMAT = "in {0}s";
    protected const string TOTAL_FORMAT = "{0} @{1}";
    protected const string PLAYER_FORMAT = "{0} = ";
    protected const string PLAYER_RANK_FORMAT = "{0}. {1} = ";

    protected static DictionaryAddHelper<string, int> StringIntAddHelper = new DictionaryAddHelper<string, int>();
    private const int DEATH_TIME_OFFSET = 10; // seconds forward

    internal static List<PlayerStats> GetSelectedPlayerStatsByClass(string classString, ItemCollection items)
    {
      DataManager.SpellClasses type = (DataManager.SpellClasses) Enum.Parse(typeof(DataManager.SpellClasses), classString);
      string className = DataManager.Instance.GetClassName(type);

      List<PlayerStats> selectedStats = new List<PlayerStats>();
      foreach (var item in items)
      {
        PlayerStats stats = item as PlayerStats;
        if (stats.ClassName == className)
        {
          selectedStats.Add(stats);
        }
      }

      return selectedStats;
    }

    protected static PlayerStats CreatePlayerStats(Dictionary<string, PlayerStats> individualStats, string key, string origName = null)
    {
      PlayerStats stats = null;

      lock (individualStats)
      {
        if (!individualStats.ContainsKey(key))
        {
          stats = CreatePlayerStats(key, origName);
          individualStats[key] = stats;
        }
        else
        {
          stats = individualStats[key];
        }
      }

      return stats;
    }

    protected static PlayerStats CreatePlayerStats(string name, string origName = null)
    {
      string className = "";
      origName = origName == null ? name : origName;

      if (!DataManager.Instance.CheckNameForPet(origName))
      {
        className = DataManager.Instance.GetPlayerClass(origName);
      }

      return new PlayerStats()
      {
        Name = name,
        ClassName = className,
        OrigName = origName,
        Percent = 100, // until something says otherwise
        SubStats = new Dictionary<string, PlayerSubStats>(),
        BeginTime = DateTime.MinValue,
        BeginTimes = new List<DateTime>(),
        LastTimes = new List<DateTime>(),
        TimeDiffs = new List<double>()
      };
    }

    protected static PlayerSubStats CreatePlayerSubStats(Dictionary<string, PlayerSubStats> individualStats, string key, string type)
    {
      PlayerSubStats stats = null;

      lock (individualStats)
      {
        if (!individualStats.ContainsKey(key))
        {
          stats = CreatePlayerSubStats(key, type);
          individualStats[key] = stats;
        }
        else
        {
          stats = individualStats[key];
        }
      }

      return stats;
    }

    protected static PlayerSubStats CreatePlayerSubStats(string name, string type)
    {
      return new PlayerSubStats()
      {
        ClassName = "",
        Name = name,
        Type = type,
        CritFreqValues = new Dictionary<long, int>(),
        NonCritFreqValues = new Dictionary<long, int>(),
        BeginTime = DateTime.MinValue,
        BeginTimes = new List<DateTime>(),
        LastTimes = new List<DateTime>(),
        TimeDiffs = new List<double>()
      };
    }

    protected static string FormatTitle(string targetTitle, string timeTitle, string damageTitle = "")
    {
      string result;
      result = targetTitle + " " + timeTitle;
      if (damageTitle != "")
      {
        result += ", " + damageTitle;
      }
      return result;
    }

    protected static void UpdateTimeDiffs(PlayerSubStats subStats, TimedAction action, double offset = 0)
    {
      int currentIndex = subStats.BeginTimes.Count - 1;
      if (currentIndex == -1)
      {
        subStats.BeginTimes.Add(action.BeginTime);
        subStats.LastTimes.Add(action.LastTime.AddSeconds(offset));
        subStats.TimeDiffs.Add(0); // update afterward
        currentIndex = 0;
      }
      else if (subStats.LastTimes[currentIndex] >= action.BeginTime)
      {
        var offsetLastTime = action.LastTime.AddSeconds(offset);
        if (offsetLastTime > subStats.LastTimes[currentIndex])
        {
          subStats.LastTimes[currentIndex] = offsetLastTime;
        }
      }
      else
      {
        subStats.BeginTimes.Add(action.BeginTime);
        subStats.LastTimes.Add(action.LastTime.AddSeconds(offset));
        subStats.TimeDiffs.Add(0); // update afterward
        currentIndex++;
      }

      subStats.TimeDiffs[currentIndex] = subStats.LastTimes[currentIndex].Subtract(subStats.BeginTimes[currentIndex]).TotalSeconds + 1;
    }

    protected static void UpdateStats(PlayerSubStats stats, HitRecord record)
    {
      // record Bane separately in totals
      if (record.Type == Labels.BANE_NAME)
      {
        stats.BaneHits++;
      }
      else
      {
        stats.Total += record.Total;
        stats.Hits += 1;
        stats.Max = (stats.Max < record.Total) ? record.Total : stats.Max;

        if (record.Total > 0 && record.OverTotal > 0)
        {
          stats.Extra += (record.OverTotal - record.Total);
        }

        int crits = stats.CritHits;
        LineModifiersParser.Parse(record, stats);
      }

      if (stats.BeginTime == DateTime.MinValue)
      {
        stats.BeginTime = record.BeginTime;
      }

      stats.LastTime = record.BeginTime;
    }

    protected static void UpdateCalculations(PlayerSubStats stats, PlayerStats raidTotals, Dictionary<string, int> resistCounts = null, PlayerStats superStats = null)
    {
      if (stats.Hits > 0)
      {
        stats.Avg = (long) Math.Round(Convert.ToDecimal(stats.Total) / stats.Hits, 2);
        stats.CritRate = Math.Round(Convert.ToDecimal(stats.CritHits) / stats.Hits * 100, 2);
        stats.LuckRate = Math.Round(Convert.ToDecimal(stats.LuckyHits) / stats.Hits * 100, 2);
      }

      if (stats.Total > 0)
      {
        stats.ExtraRate = Math.Round(Convert.ToDecimal(stats.Extra) / stats.Total * 100, 2);
      }

      if ((stats.CritHits - stats.LuckyHits) > 0)
      {
        stats.AvgCrit = (long) Math.Round(Convert.ToDecimal(stats.TotalCrit) / (stats.CritHits - stats.LuckyHits), 2);
      }

      if (stats.LuckyHits > 0)
      {
        stats.AvgLucky = (long) Math.Round(Convert.ToDecimal(stats.TotalLucky) / stats.LuckyHits, 2);
      }

      // total percents
      if (raidTotals.Total > 0)
      {
        stats.PercentOfRaid = Math.Round((decimal) stats.Total / raidTotals.Total * 100, 2);
      }

      stats.DPS = (long) Math.Round(stats.Total / stats.TotalSeconds, 2);

      if (superStats == null)
      {
        stats.SDPS = (long) Math.Round(stats.Total / raidTotals.TotalSeconds, 2);
      }
      else
      {
        if (superStats.Total > 0)
        {
          stats.Percent = Math.Round((decimal) stats.Total / superStats.Total * 100, 2);
        }

        stats.SDPS = (long) Math.Round(stats.Total / superStats.TotalSeconds, 2);

        if (resistCounts != null && superStats.Name == DataManager.Instance.PlayerName)
        {
          int value;
          if (resistCounts.TryGetValue(stats.Name, out value))
          {
            stats.Resists = value;
            stats.ResistRate = stats.LuckRate = Math.Round(Convert.ToDecimal(stats.Resists) / (stats.Hits + stats.Resists) * 100, 2);
          }
        }
      }

      // handle sub stats
      var playerStats = stats as PlayerStats;

      if (playerStats != null)
      {
        Parallel.ForEach(playerStats.SubStats.Values, subStats => UpdateCalculations(subStats, raidTotals, resistCounts, playerStats));

        // optional stats
        if (playerStats.SubStats2 != null)
        {
          Parallel.ForEach(playerStats.SubStats2.Values, subStats => UpdateCalculations(subStats, raidTotals, resistCounts, playerStats));
        }
      }
    }

    protected static ConcurrentDictionary<string, int> GetPlayerDeaths(PlayerStats raidStats)
    {
      Dictionary<string, int> deathCounts = new Dictionary<string, int>();

      if (raidStats.BeginTimes.Count > 0 && raidStats.LastTimes.Count > 0)
      {
        DateTime beginTime = raidStats.BeginTimes.First();
        DateTime endTime = raidStats.LastTimes.Last().AddSeconds(DEATH_TIME_OFFSET); ;

        Parallel.ForEach(DataManager.Instance.GetPlayerDeathsDuring(beginTime, endTime), timedAction =>
        {
          PlayerDeath death = timedAction as PlayerDeath;
          StringIntAddHelper.Add(deathCounts, death.Player, 1);
        });
      }

      return new ConcurrentDictionary<string, int>(deathCounts);
    }
  }
}
