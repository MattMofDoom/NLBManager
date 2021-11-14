using System;
using System.Configuration;

namespace NLBManager.Classes
{
    public static class Config
    {
        //Defaults for various settings
        private const int LogTimerDefault = 60000;
        private const int LogTimerMin = 30000;
        private const int LogTimerMax = 300000;

        static Config()
        {
            ServiceName = ConfigurationManager.AppSettings["ServiceName"];
            TargetCluster = ConfigurationManager.AppSettings["TargetCluster"];
            ServiceTick = GetTimer(ConfigurationManager.AppSettings["ServiceTick"]);
            ServiceMode = GetServiceMode(ConfigurationManager.AppSettings["ServiceMode"]);
        }

        public static string ServiceName { get; }
        public static string TargetCluster { get; }
        public static int ServiceTick { get; }
        public static ServiceMode ServiceMode { get; }

        /// <summary>
        ///     Translate a <see cref="string" /> setting representing seconds to an <see cref="int" />  timer value representing
        ///     milliseconds
        /// </summary>
        /// <param name="timerSetting">Timer setting in seconds</param>
        /// <returns></returns>
        private static int GetTimer(string timerSetting)
        {
            if (int.TryParse(timerSetting, out var tryTimerTick))
            {
                //Convert to milliseconds
                tryTimerTick *= 1000;

                if ((tryTimerTick > LogTimerMax) | (tryTimerTick < LogTimerMin)) tryTimerTick = LogTimerDefault;
            }
            else
            {
                tryTimerTick = LogTimerDefault;
            }

            return tryTimerTick;
        }

        private static ServiceMode GetServiceMode(string configValue)
        {
            if (string.IsNullOrEmpty(configValue)) return ServiceMode.Default;
            return Enum.TryParse(configValue, true, out ServiceMode serviceMode)
                ? serviceMode
                : ServiceMode.Default;
        }
    }
}