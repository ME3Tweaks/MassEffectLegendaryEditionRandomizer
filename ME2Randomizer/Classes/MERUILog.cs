using System;
using System.IO;
using System.Text;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using Serilog;
using Serilog.Sinks.File;

namespace RandomizerUI.Classes
{
    /// <summary>
    /// Interposer used to prefix the UI logger
    /// </summary>
    public static class MERUILog
    {
        private const string Prefix = "MERUI";

        public static void Exception(Exception exception, string preMessage, bool fatal = false, bool condition = true)
        {
            MLog.Exception(exception, preMessage, fatal, Prefix);
        }

        public static void Information(string message, bool condition = true)
        {
            MLog.Information(message, condition, Prefix);
        }

        public static void Warning(string message, bool condition = true)
        {
            MLog.Warning(message, condition, Prefix);
        }

        public static void Error(string message, bool condition = true)
        {
            MLog.Error(message, condition, Prefix);
        }

        public static void Fatal(string message, bool condition = true)
        {
            MLog.Fatal(message, condition, Prefix);
        }

        public static void Debug(string message, bool condition = true)
        {
            MLog.Debug(message, condition, Prefix);
        }

        public static void LogSessionStart()
        {
            MLog.LogSessionStart(Prefix);
        }
    }
}