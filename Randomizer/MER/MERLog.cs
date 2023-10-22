using System;
using System.IO;
using System.Text;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using Serilog;
using Serilog.Sinks.File;
using static Randomizer.MER.MERLog;

namespace Randomizer.MER
{
    /// <summary>
    /// Hook used to capture what log is currently being used
    /// </summary>
    public class CaptureFilePathHook : FileLifecycleHooks
    {
        public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
        {
            MERLog.CurrentLogFilePath = path;
            return base.OnFileOpened(path, underlyingStream, encoding);
        }
    }

    /// <summary>
    /// Interposer used to prefix MERLog messages with their source component. Call only from MER code
    /// </summary>
    public static class MERLog
    {
#if __GAME1__
        private const string Prefix = "LE1R";
#elif __GAME2__
        private const string Prefix = "LE2R";
#elif __GAME3__
        private const string Prefix = "LE3R";
#endif

        /// <summary>
        /// The path of the current log file
        /// </summary>
        public static string CurrentLogFilePath { get; internal set; }

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

        /// <summary>
        /// Creates an ILogger for ME3Tweaks Mod Manager. This does NOT assign it to the Log.Logger instance.
        /// </summary>
        /// <returns></returns>
        public static ILogger CreateLogger()
        {
            return new LoggerConfiguration().WriteTo
                .File(Path.Combine(MCoreFilesystem.GetLogDir(), $@"{Prefix}-.txt"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: FileSize.MebiByte * 10, // 10 MB
                                                                // shared: true, // Allow us to read log without closing it // doesn't work in shared mode
                    hooks: new CaptureFilePathHook()) // Allow us to capture current log path 
#if DEBUG
                .WriteTo.Debug()
#endif
                .CreateLogger();
        }
    }
}