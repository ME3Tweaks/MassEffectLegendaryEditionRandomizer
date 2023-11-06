using System.IO;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using PropertyChanged;
using Randomizer.MER;
using RandomizerUI.Classes;

namespace RandomizerUI
{
    /// <summary>
    /// Contains code for handling the target randomizer will use
    /// </summary>
    public static class TargetHandler
    {
        public static string PassthroughGamePath;

        internal static bool LoadTargets()
        {
            MERUILog.Information("Loading game target");
            var targetSet = false;
            if (PassthroughGamePath != null)
            {
                targetSet = AttemptSetTarget(PassthroughGamePath);
            }

            if (!targetSet)
            {
                targetSet = AttemptSetTarget(MEDirectories.GetDefaultGamePath(MERFileSystem.Game));
            }

            return targetSet;
        }

        private static bool AttemptSetTarget(string targetPath)
        {
            if (targetPath != null && Directory.Exists(targetPath))
            {
                string exePath = MEDirectories.GetExecutablePath(MERFileSystem.Game, targetPath);

                if (File.Exists(exePath))
                {
                    return internalSetTarget(MERFileSystem.Game, targetPath);
                }
                else
                {
                    MERUILog.Warning($@"Executable not found: {exePath}. This target is not available.");
                }
            }

            return false;
        }

        public static GameTarget Target { get; set; }

        /// <summary>
        /// UI display string of the LE target path. Do not trust this value as a true path, use the target instead.
        /// </summary>
        [DependsOn(nameof(Target))] public static string LEGamePath => Target?.TargetPath ?? "Not installed";

        private static bool internalSetTarget(MEGame game, string path)
        {
            GameTarget gt = new GameTarget(game, path, false);
            var failedValidationReason = gt.ValidateTarget();
            if (failedValidationReason != null)
            {
                MERUILog.Error($@"Game target {path} failed validation: {failedValidationReason}");
                return false;
            }

            if (game.IsLEGame())
            {
                MERUILog.Information($"Using game target {gt.TargetPath}");
                Target = gt;
                return true;
            }

            return false; // DEFAULT
        }

        public static GameTarget GetTarget()
        {
            return Target;
        }

        public static void ReloadTarget()
        {
            var target = GetTarget();
            target?.ReloadGameTarget();
        }

        public static void SetTarget(GameTarget gt)
        {
            if (gt.Game.IsLEGame())
                Target = gt;
        }
    }
}
