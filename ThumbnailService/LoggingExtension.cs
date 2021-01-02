using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThumbnailService
{
    public static class LoggingExtension
    {

        public static void LogException(this EventLog eventLog, Exception ex)
        {
            eventLog.WriteEntry(ex.Message, EventLogEntryType.Information);
        }

        public static void LogMessage(this EventLog eventLog, string message, EventLogEntryType entryType = EventLogEntryType.Information)
        {
            eventLog.WriteEntry(message, entryType);
        }
    }
}
