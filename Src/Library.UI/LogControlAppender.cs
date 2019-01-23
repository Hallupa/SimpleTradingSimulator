using System.Collections.Generic;
using System.Linq;
using log4net.Appender;
using log4net.Core;

namespace Hallupa.Library.UI
{
    public class LogControlAppender : AppenderSkeleton
    {
        private static List<LoggingEvent> _loggingEvents = new List<LoggingEvent>();

        public static int LoggingEventCount
        {
            get
            {
                lock (_loggingEvents)
                {
                    return _loggingEvents.Count;
                }
            }
        }

        public static List<LoggingEvent> GetLoggingEvents()
        {
            lock (_loggingEvents)
            {
                return _loggingEvents.ToList();
            }
        }

        public LogControlAppender()
        {
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            lock (_loggingEvents)
            {
                _loggingEvents.Add(loggingEvent);
            }
        }
    }
}