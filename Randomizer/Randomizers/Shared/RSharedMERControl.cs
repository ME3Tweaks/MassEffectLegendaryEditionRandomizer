using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Shared
{
    internal class RSharedMERControl
    {

        private static bool InstalledBioPawnMERControl = false;
        /// <summary>
        /// Installs code into BioPawn.PostBeginPlay for MER
        /// </summary>
        /// <param name="target"></param>
        public static void InstallBioPawnMERControl(GameTarget target)
        {
            if (!InstalledBioPawnMERControl)
            {
                InstallMERControl(target); // This is a prerequesite
                var sfxgame = RSharedSFXGame.GetSFXGame(target);
                ScriptTools.InstallScriptToExport(target, sfxgame.FindExport("BioPawn.PostBeginPlay"),
                    "BioPawn.PostBeginPlay.uc");
                MERFileSystem.SavePackage(sfxgame);
                InstalledBioPawnMERControl = true;
            }
        }


        private static bool InstalledMERControl = false;
        /// <summary>
        /// Installs scaffolding code used by many other randomizers
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static bool InstallMERControl(GameTarget target)
        {
            if (!InstalledMERControl)
            {
                // Engine class
                var engine = RSharedEngine.GetEngine(target);
                ScriptTools.InstallClassToPackageFromEmbedded(target, engine, "MERControlEngine", useCache: true);
                MERFileSystem.SavePackage(engine);

                var sfxgame = RSharedSFXGame.GetSFXGame(target);
                ScriptTools.InstallClassToPackageFromEmbedded(target, sfxgame, "MERControl");
                MERFileSystem.SavePackage(sfxgame);

                InstalledMERControl = true;
            }

            return true;
        }

        public static void ResetClass()
        {
            InstalledMERControl = false;
            InstalledBioPawnMERControl = false;
        }

        public static bool InstallNPCMovementRandomizer(GameTarget target, RandomizationOption option)
        {
            InstallBioPawnMERControl(target);
            CoalescedHandler.EnableFeatureFlag("bNPCMovementSpeedRandomizer");
            return true;
        }
    }
}
