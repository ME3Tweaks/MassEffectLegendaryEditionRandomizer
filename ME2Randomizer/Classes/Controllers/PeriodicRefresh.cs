using System;
using System.Diagnostics;
using System.Timers;
using ME3TweaksCore.Services.Backup;
using Randomizer.MER;

namespace RandomizerUI.Classes.Controllers
{
    /// <summary>
    /// Periodically refreshes various variables such as backup status
    /// </summary>
    public class PeriodicRefresh
    {
        private static Timer periodicTimer;
        private static PeriodicRefresh pr;
        public static void StartPeriodicRefresh()
        {
            if (periodicTimer != null)
            {
                periodicTimer.Stop();
                periodicTimer.Elapsed -= periodicRefresh;
                periodicTimer.Close();
            }

            periodicTimer = new Timer(60000)
            {
                AutoReset = true
            };
            periodicTimer.Elapsed += periodicRefresh;
            periodicTimer.Start();
            pr = new PeriodicRefresh();
        }

        private static void periodicRefresh(object sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("Periodic refresh");
            BackupService.RefreshBackupStatus(game: MERFileSystem.Game, log: false);
            OnPeriodicRefresh?.Invoke(null, null);
        }

        /// <summary>
        /// Invoked when a periodic refresh occurs, which happens every 60 seconds. The parameters from the call are always null.
        /// </summary>
        public static event EventHandler OnPeriodicRefresh;
    }
}