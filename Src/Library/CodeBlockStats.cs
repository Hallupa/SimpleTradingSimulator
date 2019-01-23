using System;
using System.Threading;

namespace Hallupa.Library
{
    public class CodeBlockStats
    {
        private int _totalRuns = 0;
        private long _totalRunTimeMs = 0;

        public CodeBlockStats(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public int TotalRuns => _totalRuns;

        public long TotalRunTimeMs => _totalRunTimeMs;

        public void Reset()
        {
            _totalRuns = 0;
            _totalRunTimeMs = 0;
        }

        public void AddTime(int tickCount)
        {
            Interlocked.Increment(ref _totalRuns);
            Interlocked.Add(ref _totalRunTimeMs, tickCount);
        }

        public IDisposable Log()
        {
            var start = Environment.TickCount;

            return new DisposableAction(() =>
            {
                var total = Environment.TickCount - start;
                Interlocked.Increment(ref _totalRuns);
                Interlocked.Add(ref _totalRunTimeMs, total);
            });
        }
    }
}