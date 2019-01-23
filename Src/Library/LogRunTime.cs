using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using log4net;

namespace Hallupa.Library
{
    public class LogRunTime : IDisposable
    {
        private readonly string _name;
        private Stopwatch _counter;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public LogRunTime([CallerMemberName]string name = null)
        {
            _name = name;
            _counter = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _counter.Stop();
            Log.Debug($"LogRunTime: {_name} took {_counter.ElapsedMilliseconds}ms");
        }
    }
}