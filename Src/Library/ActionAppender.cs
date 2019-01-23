using System;
using log4net.Appender;
using log4net.Core;

namespace Hallupa.Library
{
    public class ActionAppender : AppenderSkeleton
    {
        public static Action<LoggingEvent> LogAction;

        protected override void Append(LoggingEvent loggingEvent)
        {
            LogAction?.Invoke(loggingEvent);
        }
    }
}