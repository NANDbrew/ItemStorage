using System;
using UnityModManagerNet;

namespace ItemStorage
{
    public class Utilities
    {
        private static UnityModManager.ModEntry.ModLogger logger = null;

        public enum LogType
        {
            Log,
            Warning,
            Error,
            LogException
        }

        public static void SetLogger(UnityModManager.ModEntry.ModLogger logger)
        {
            Utilities.logger = logger;
        }

        public static void Log(LogType log_type, string format, params object[] items)
        {
            if (logger == null)
                throw new InvalidOperationException("Log functions cannot be used with an unset logger");

            if (log_type == LogType.Log)
                logger.Log(string.Format(format, items));
            if (log_type == LogType.Warning)
                logger.Warning(string.Format(format, items));
            if (log_type == LogType.Error)
                logger.Error(string.Format(format, items));
        }

        public static void Log(string format, params object[] items)
        {
            Log(LogType.Log, format, items);
        }

        public static void LogException(Exception e, string format, params object[] items)
        {
            Log(LogType.Error, format, items);
            logger.LogException(e);
        }
    }
}
