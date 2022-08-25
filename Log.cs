using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace NvkCommon
{
    public class Log
    {
        private static TextWriterTraceListener textWriterTraceListener;
        private static FileStream logFileWriter;

        public static string LogFilePath
        {
            get
            {
                return logFileWriter?.Name;
            }
        }

        public static bool LogToFile
        {
            get
            {
                return textWriterTraceListener != null && logFileWriter != null;
            }
            set
            {
                if (textWriterTraceListener != null)
                {
                    Trace.Listeners.Remove(textWriterTraceListener);
                    textWriterTraceListener.Close();
                    textWriterTraceListener = null;
                    logFileWriter.Close();
                    logFileWriter = null;
                }

                if (value)
                {
                    var logFilePath = $"{Utils.ApplicationExecutableName}.log";
                    try
                    {
                        // Can throw an exception if another instance of the app is running
                        logFileWriter = new FileStream(logFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                        textWriterTraceListener = new TextWriterTraceListener(logFileWriter);
                        Trace.Listeners.Add(textWriterTraceListener);
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine($"LogToFile: Failed to open log file '{logFilePath}': {e.Message}");
                    }
                }
            }
        }

        public enum LogLevel
        {
            Fatal = 1,
            Error = 2,
            Warning = 4,
            Information = 8,
            Debug = 16,
            Verbose = 32,
        }

        private static string GetShortClassName(string className)
        {
            if (string.IsNullOrEmpty(className))
            {
                return "null";
            }
            return className.Substring(className.LastIndexOf('.') + 1);
        }

        private static string GetShortClassName(Object o)
        {
            return GetShortClassName(o?.GetType());
        }

        private static string GetShortClassName(Type c)
        {
            return GetShortClassName(c?.Name);
        }

        public static string TAG(object o)
        {
            return TAG(o?.GetType());
        }

        public static string TAG(Type c)
        {
            return GetShortClassName(c);
        }

        public static void PrintLine(string tag, LogLevel level, string format)
        {
            PrintLine(tag, level, format, null as Exception);
        }

        public static void PrintLine(string tag, LogLevel level, string format, params object[] args)
        {
            PrintLine(tag, level, string.Format(format, args));
        }

        public static void PrintLine(string tag, LogLevel level, string message, Exception e)
        {
            DateTime dt = DateTime.Now;
#if SILVERLIGHT
            // NOTE: Process ID is not available in Silverlight
            int pid = -1;
#else
            int pid = Process.GetCurrentProcess().Id;
#endif
            int tid = Thread.CurrentThread.ManagedThreadId;

            StringBuilder sb = new StringBuilder()
                        .Append(dt.ToString("yy/MM/dd HH:mm:ss.fff")) //
                        .Append(' ').Append(level.ToString()[0]) //
                        .Append(" P").Append(pid.ToString("X4")) //
                        .Append(" T").Append(tid.ToString("X4")) //
                        .Append(' ').Append(tag) //
                        .Append(' ').Append(message);
            if (e != null)
            {
                sb.Append(": exception=").Append(e.ToString()).Append('\n').Append(e.Message);
            }

            Trace.WriteLine(sb.ToString());
            Trace.Flush();
        }
    }
}
