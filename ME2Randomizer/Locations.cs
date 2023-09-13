using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows.Documents;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using PropertyChanged;
using Randomizer.MER;

namespace RandomizerUI
{
    /// <summary>
    /// Contains the targets for OT and LE games.
    /// </summary>
    public static class Locations
    {
#if __GAME1__
        public static readonly MEGame[] SupportedGames = new[] { MEGame.LE1};
#elif __GAME2__
        public static readonly MEGame[] SupportedGames = new[] { MEGame.LE2 };
#elif __GAME3__
        public static readonly MEGame[] SupportedGames = new[] { MEGame.LE3};
#endif
        internal static void LoadTargets()
        {
            MERLog.Information("Loading game targets");
            LoadGamePaths();
        }
        public static GameTarget LegendaryEditionTarget { get; set; }

        /// <summary>
        /// UI display string of the LE target path. Do not trust this value as a true path, use the target instead.
        /// </summary>
        [DependsOn(nameof(LegendaryEditionTarget))] public static string LEGamePath => LegendaryEditionTarget?.TargetPath ?? "Not installed";

        private static void LoadGamePaths()
        {
            foreach (var game in SupportedGames)
            {
                var path = MEDirectories.GetDefaultGamePath(game);
                if (path != null && Directory.Exists(path))
                {
                    string exePath = MEDirectories.GetExecutablePath(game, path);

                    if (File.Exists(exePath))
                    {
                        internalSetTarget(game, path);
                    }
                    else
                    {
                        MERLog.Warning($@"Executable not found: {exePath}. This target is not available.");
                    }
                }
            }
        }

        private static bool internalSetTarget(MEGame game, string path)
        {
            GameTarget gt = new GameTarget(game, path, false);
            var failedValidationReason = gt.ValidateTarget();
            if (failedValidationReason != null)
            {
                MERLog.Error($@"Game target {path} failed validation: {failedValidationReason}");
                return false;
            }

            if (game.IsLEGame())
            {
                LegendaryEditionTarget = gt;
                return true;
            }

            return false; // DEFAULT
        }

        public static GameTarget GetTarget()
        {
            return LegendaryEditionTarget;
        }

        public static void ReloadTarget()
        {
            var target = GetTarget();
            target?.ReloadGameTarget();
        }

        public static void SetTarget(GameTarget gt)
        {
            if (gt.Game.IsLEGame())
                LegendaryEditionTarget = gt;
        }
    }
}
