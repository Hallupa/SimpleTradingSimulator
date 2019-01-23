using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;

namespace Hallupa.Library
{
    public static class LogCodeBlockStats
    {
        private static readonly Dictionary<string, CodeBlockStats> _codeBlockStatsLookup = new Dictionary<string, CodeBlockStats>();
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static Timer _timer;

        static LogCodeBlockStats()
        {
            _timer = new Timer(Callback, null, 30000, Timeout.Infinite);

        }

        private static void Callback(object state)
        {
            lock (_codeBlockStatsLookup)
            {
                LogCodeBlockStatsReport(_codeBlockStatsLookup.Values);
            }

            _timer.Change(30000, Timeout.Infinite);
        }

        public static void LogCodeBlockStatsReport(IEnumerable<CodeBlockStats> stats, bool singleLine = true)
        {
            var toLogList = stats.Where(x => x.TotalRuns > 0).OrderByDescending(x => x.TotalRunTimeMs).ToList();

            if (toLogList.Count > 0)
            {
                if (singleLine)
                {
                    Log.Info("CodeBlockStats: " + string.Join(", ", toLogList.Select(x => $"[{x.Name} Runs: {x.TotalRuns} Time: {x.TotalRunTimeMs}ms]")));
                }
                else
                {
                    foreach (var toLog in toLogList)
                    {
                        Log.Info($"## {toLog.Name} Runs: {toLog.TotalRuns} Time: {toLog.TotalRunTimeMs}ms");
                    }
                }

                foreach (var codeBlockStats in toLogList)
                {
                    codeBlockStats.Reset();
                }
            }
        }


        public static CodeBlockStats GetCodeBlockStats(string name)
        {
            lock (_codeBlockStatsLookup)
            {
                if (_codeBlockStatsLookup.TryGetValue(name, out var ret))
                {
                    return ret;
                }

                ret = new CodeBlockStats(name);

                _codeBlockStatsLookup[name] = ret;

                return ret;
            }
        }
    }
}