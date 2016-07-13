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
        #region Nested Types
        /// <summary>
        /// This Type is used to Retrieve Current TraceEvent Context Information such as,
        /// MachineName, AppDomainName, ThreadName etc.
        /// </summary>
        /// <seealso cref="System.Diagnostics.TraceEventCache"/>
        public class TraceEventContext : TraceEventCache
        {
            private TraceEventCache _eventCache;

            static string processName = Process.GetCurrentProcess().ProcessName;
            static string machineName = Environment.MachineName;

            public TraceEventContext(TraceEventCache eventCache)
            {
                _eventCache = eventCache;
            }

            public string MachineName
            {
                get { return machineName; }
            }

            public string AppDomainName
            {
                get { return AppDomain.CurrentDomain.FriendlyName; }
            }

            public string ProcessName
            {
                get { return processName; }
            }

            public string ThreadName
            {
                get { return Thread.CurrentThread.Name; }
            }

            public new string Callstack
            {
                get { return _eventCache.Callstack; }
            }

            public new DateTime DateTime
            {
                get { return _eventCache.DateTime; }
            }

            public new System.Collections.Stack LogicalOperationStack
            {
                get { return _eventCache.LogicalOperationStack; }
            }

            public new int ProcessId
            {
                get { return _eventCache.ProcessId; }
            }

            public new string ThreadId
            {
                get { return _eventCache.ThreadId; }
            }

            public new long Timestamp
            {
                get { return _eventCache.Timestamp; }
            }
        }

        #endregion

        private static BooleanSwitch _booleanSwitch;
        private static TraceSwitch _traceSwitch;
        private static int MAXLOGSIZE = 32766;

        #region System.Diagnostics.Trace Methods

        /// <summary>
        /// Gets the boolean switch.
        /// </summary>
        /// <value>The boolean switch.</value>
        public static BooleanSwitch BooleanSwitch
        {
            get
            {
                if (_booleanSwitch == null)
                {
                    _booleanSwitch = new BooleanSwitch("BooleanSwitch", "General Boolean Switch");
                }
                return _booleanSwitch;
            }
        }
        private static string LimitLogSize(string format, params object[] args)
        {
            string message = string.Format(format, args);
            return LimitLogSize(message);
        }

        private static string LimitLogSize(string message)
        {
            if (message.Length > MAXLOGSIZE)
                return message.Substring(0, MAXLOGSIZE);
            else
                return message;
        }

        /// <summary>
        /// Gets the trace switch.
        /// </summary>
        /// <value>The trace switch.</value>
        public static TraceSwitch TraceSwitch
        {
            get
            {
                if (_traceSwitch == null)
                {
                    _traceSwitch = new TraceSwitch("TraceLevelSwitch", "General Trace Switch");
                }
                return _traceSwitch;
            }
        }

        public static void TraceError(string message)
        {
            if (TraceSwitch.TraceError)
                Trace.TraceError(LimitLogSize(message));
        }

        public static void TraceError(string format, params object[] args)
        {
            if (TraceSwitch.TraceError)
                Trace.TraceError(LimitLogSize(format, args));
        }

        public static void TraceWarning(string message)
        {
            if (TraceSwitch.TraceWarning)
                Trace.TraceWarning(LimitLogSize(message));
        }

        public static void TraceWarning(string format, params object[] args)
        {
            if (TraceSwitch.TraceWarning)
                Trace.TraceWarning(LimitLogSize(format, args));
        }

        public static void TraceInformation(string message)
        {
            if (TraceSwitch.TraceInfo)
                Trace.TraceInformation(LimitLogSize(message));
        }

        public static void TraceInformation(string format, params object[] args)
        {
            if (TraceSwitch.TraceInfo)
                Trace.TraceInformation(LimitLogSize(format, args));
        }

        public static void WriteDiagnostic(string message)
        {
            if (TraceSwitch.TraceVerbose)
                Trace.Write(LimitLogSize(message));
        }

        public static void WriteDiagnostic(string format, params object[] args)
        {
            if (TraceSwitch.TraceVerbose)
                Trace.Write(LimitLogSize(format, args));
        }

        #endregion

        #region System.Diagnostics.TraceSource Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="enumType"></param>
        /// <returns></returns>
        public static TraceSource GetTraceSource(int value, Type enumType)
        {
            return GetTraceSource(value, enumType, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="enumType"></param>
        /// <param name="listeners"></param>
        /// <returns></returns>
        public static TraceSource GetTraceSource(int value, Type enumType, TraceListener[] listeners)
        {
            return GetTraceSource(value, enumType,
                SourceLevels.Information | SourceLevels.ActivityTracing, listeners);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="enumType"></param>
        /// <param name="defaultLevel"></param>
        /// <param name="listeners"></param>
        /// <returns></returns>
        public static TraceSource GetTraceSource(int value, Type enumType, SourceLevels defaultLevel, TraceListener[] listeners)
        {
            if (!enumType.BaseType.Equals(typeof(Enum)))
                throw new ArgumentException(string.Format("Unexpected argument type '{0}'. 'Enum' was expected.", enumType.FullName), "enumType");

            TraceSource traceSource = new TraceSource(Enum.GetName(enumType, value), defaultLevel);

            if (traceSource.Listeners.Count < 1 ||
                    (traceSource.Listeners.Count == 1 &&
                traceSource.Listeners[0].GetType().Equals(typeof(DefaultTraceListener)))) // No Listeners in '*.Config'
            {
                if (listeners != null && listeners.Length > 0)
                {
                    traceSource.Listeners.AddRange(listeners);  // Use the 'listeners' argument
                }
                // Error condition - Listeners neither in Configuration nor in the 'listeners' Argument
                else if (traceSource.Listeners.Count < 1)
                {
                    throw new TypeInitializationException(traceSource.GetType().FullName,
                            new ArgumentException("Can not create a TraceSource with no trace listeners defined.", "listeners"));
                }
                else if (traceSource.Listeners.Count == 1 &&
                    traceSource.Listeners[0].GetType().Equals(typeof(DefaultTraceListener)))
                {
                    throw new TypeInitializationException(traceSource.GetType().FullName,
                            new ArgumentException("Can not create a TraceSource with no trace listeners defined other then 'DefaultTraceListener' attached.", "listeners"));
                }
            }

            return traceSource;
        }

        /// <summary>
        /// Writes the Event information and a string message using supplied 'traceSource'.
        /// </summary>
        /// <param name="traceSource">'TraceSource' <see cref="System.Diagnostics.TraceSource"/> object that should receive the Event.</param>
        /// <param name="eventType">The type of event. <see cref="System.Diagnostics.TraceEventType"/></param>
        /// <param name="message">The string message to trace.</param>
        public static void TraceEvent(TraceSource traceSource, TraceEventType eventType, string message)
        {
            TraceEvent(traceSource, eventType, message, 0);
        }

        /// <summary>
        /// Writes the Event information and a string message using supplied 'traceSource'.
        /// </summary>
        /// <param name="traceSource">'TraceSource' <see cref="System.Diagnostics.TraceSource"/> object that should receive the Event.</param>
        /// <param name="eventType">The type of event. <see cref="System.Diagnostics.TraceEventType"/></param>
        /// <param name="format">The format of a string message to be traced.</param>
        /// <param name="args">Array of arguments for the message format.</param>
        public static void TraceEvent(TraceSource traceSource, TraceEventType eventType, string format, params object[] args)
        {
            TraceEvent(traceSource, eventType, string.Format(format, args));
        }

        /// <summary>
        /// Writes the Event information and a string message using supplied 'traceSource'.
        /// </summary>
        /// <param name="traceSource">'TraceSource' <see cref="System.Diagnostics.TraceSource"/> object that should receive the Event.</param>
        /// <param name="eventType">The type of event. <see cref="System.Diagnostics.TraceEventType"/></param>
        /// <param name="message">The string message to trace.</param>
        /// <param name="eventId">The id of the event.</param>
        public static void TraceEvent(TraceSource traceSource, TraceEventType eventType, string message, int eventId)
        {
            if (traceSource == null)
                throw new ArgumentNullException("traceSource");

            traceSource.TraceEvent(eventType, eventId, message);
        }

        #endregion

    }
}
