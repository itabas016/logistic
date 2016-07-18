using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    /// <summary>
    /// Diagnostics
    /// </summary>
    public static class Diagnostics
    {
        public static void Info(string comMessage)
        {
            Info(comMessage, null);
        }

        public static void Error(string comMessage)
        {
            Error(comMessage, null);
        }

        public static void Warning(string comMessage)
        {
            Warning(comMessage, null);
        }

        public static void Trace(string comMessage)
        {
            Trace(comMessage, null);
        }

        public static void Info(string format, params object[] args)
        {
            WriteLog("Audit", TraceEventType.Information, format, args);
        }

        public static void Error(string format, params object[] args)
        {
            WriteLog("Exception", TraceEventType.Error, format, args);
        }

        public static void Warning(string format, params object[] args)
        {
            WriteLog("Audit", TraceEventType.Warning, format, args);
        }

        public static void Trace(string format, params object[] args)
        {
            WriteLog("Trace", TraceEventType.Information, format, args);
        }

        private static void WriteLog(string category, TraceEventType severity, string format, params object[] args)
        {
            if (PayMedia.Logging.Logger.ShouldLog(category, severity))
                PayMedia.Logging.Logger.Write(category, (args == null) ? format : string.Format(format, args), severity, null);
        }

    }
}
