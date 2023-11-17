using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.TLK.ME2ME3;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game1.Misc
{
    class SFXGame
    {
        /// <summary>
        /// Turns on friendly collateral
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static bool TurnOnFriendlyFire(GameTarget target, RandomizationOption option)
        {
            var biogameIni = CoalescedHandler.GetIniFile("BioGame.ini");
            biogameIni.GetOrAddSection("SFXGame.BioActorBehavior").SetSingleEntry("AllowFriendlyCollateral", "TRUE");

            // Old code.
            /*
            var sfxgame = MEPackageHandler.OpenMEPackage(MERFileSystem.GetPackageFile(target, "SFXGame"));
            
            // Disable config reading of the property
            var friendlyCollateralProp = sfxgame.FindExport("BioActorBehavior.AllowFriendlyCollateral");
            var propFlags = friendlyCollateralProp.GetPropertyFlags();
            if (propFlags != null)
            {
                propFlags &= ~UnrealFlags.EPropertyFlags.Config;
                friendlyCollateralProp.SetPropertyFlags(propFlags.Value);
            }

            // Set property to true
            var bioActorBehavior = sfxgame.FindExport("Default__BioActorBehavior");
            bioActorBehavior.WriteProperty(new BoolProperty(true, "AllowFriendlyCollateral"));

            MERFileSystem.SavePackage(sfxgame);
            */
            return true;
        }

        public static bool RandomizeGameOverString(GameTarget target, RandomizationOption option)
        {
            // Install rotation code for strref
            option.ProgressIndeterminate = true;
            RSharedMERControl.InstallMERControl(target);
            var sfxgame = RSharedSFXGame.GetSFXGame(target);
            ScriptTools.InstallScriptToPackage(target, sfxgame, "BioSFHandler_GameOver.SetupGameOver", "BioSFHandler_GameOver.SetupGameOver.uc", false);
            MERFileSystem.SavePackage(sfxgame);

            // Install TLK strings
            // These will already be merged by Randomizer
            var tlkData = MEREmbedded.GetEmbeddedAsset("Binary", "TLK.GameOverStrings_INT.tlk", true);
            var tlk = new ME2ME3TalkFile(tlkData); // This is the same asset as LE2R so we use ME2ME3TalkFile.
            foreach (var str in tlk.StringRefs)
            {
                CoalescedHandler.SetProperty(new CoalesceProperty("srGameOverOptions", new CoalesceValue(str.StringID.ToString(), CoalesceParseAction.AddUnique)));
            }

            CoalescedHandler.EnableFeatureFlag("bGameOverStringRandomizer");
            return true;
        }
    }
}
