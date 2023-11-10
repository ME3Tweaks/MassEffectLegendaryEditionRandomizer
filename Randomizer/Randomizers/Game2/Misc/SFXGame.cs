using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared;
using Randomizer.Randomizers.Shared.Classes;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Misc
{
    public class SFXGame
    {
        public static bool MakeShepardRagdollable(GameTarget target, RandomizationOption option)
        {
            var sfxgame = RSharedSFXGame.GetSFXGame(target);

            // Add ragdoll power to shep
            var sfxplayercontrollerDefaults = sfxgame.FindExport(@"Default__SFXPlayerController");
            var cac = sfxplayercontrollerDefaults.GetProperty<ArrayProperty<ObjectProperty>>("CustomActionClasses");
            cac[5].Value = sfxgame.FindExport(@"SFXCustomAction_Ragdoll").UIndex; //SFXCustomAction_Ragdoll in this slot
            sfxplayercontrollerDefaults.WriteProperty(cac);

            // Update power script design and patch out player physics level
            var sd = sfxgame.FindExport(@"BioPowerScriptDesign.GetPhysicsLevel");
            ScriptTools.InstallScriptToExport(target, sd, "GetPhysicsLevel.uc", false, null);

            MERFileSystem.SavePackage(sfxgame);
            return true;
        }

        public static bool TurnOnFriendlyFire(GameTarget target, RandomizationOption option)
        {
            // Remove the friendly pawn check
            var sfxgame = ScriptTools.InstallScriptToPackage(target, "SFXGame.pcc", "SFXGame.ModifyDamage", "SFXGame.ModifyDamage.uc", false);
            if (option.HasSubOptionSelected(SUBOPTIONKEY_CARELESSFF))
            {
                // Remove the friendly fire check
                ScriptTools.InstallScriptToPackage(target, sfxgame, "BioAiController.IsFriendlyBlockingFireLine", "IsFriendlyBlockingFireLine.uc", false);
            }
            MERFileSystem.SavePackage(sfxgame);
            return true;
        }

        public static bool RandomizeGameOverString(GameTarget target, RandomizationOption option)
        {
            // Install rotation code for strref
            option.ProgressIndeterminate = true;
            MERControl.InstallMERControl(target);
            var sfxgame = RSharedSFXGame.GetSFXGame(target);
            ScriptTools.InstallScriptToPackage(target, sfxgame, "BioSFHandler_GameOver.HandleEvent", "BioSFHandler_GameOver.HandleEvent.uc", false);
            MERFileSystem.SavePackage(sfxgame);

            // Install TLK strings
            var tlkData = MEREmbedded.GetEmbeddedAsset("Binary", "TLK.GameOverStrings_INT.tlk");
            var tlk = new ME2ME3TalkFile(tlkData);
            foreach (var str in tlk.StringRefs)
            {
                CoalescedHandler.SetProperty(new CoalesceProperty("srGameOverOptions", new CoalesceValue(str.StringID.ToString(), CoalesceParseAction.AddUnique)));
            }

            return true;
        }


        public const string SUBOPTIONKEY_CARELESSFF = "CarelessMode";

        public static bool RandomizeWwiseEvents(GameTarget target, RandomizationOption option)
        {
            var sfxgame = RSharedSFXGame.GetSFXGame(target);
            List<ExportEntry> referencedWwiseEvents = new List<ExportEntry>();

            var f = sfxgame.FindExport("BioSFResources.GUI_Sound_Mappings").GetProperties().GetAllProperties();
            // Get all resolved values
            foreach (var exp in sfxgame.Exports)
            {
                var objProps = exp.GetProperties().GetAllProperties().OfType<ObjectProperty>();
                foreach (var op in objProps)
                {
                    var resolvedValue = op.ResolveToExport(exp.FileRef);
                    if (resolvedValue != null && resolvedValue.ClassName == @"WwiseEvent")
                    {
                        referencedWwiseEvents.Add(resolvedValue);
                    }
                }
            }

            referencedWwiseEvents.Shuffle();

            // Write them back
            foreach (var exp in sfxgame.Exports)
            {
                var propertyCollection = exp.GetProperties();
                bool modified = false;
                var objProps = propertyCollection.GetAllProperties().OfType<ObjectProperty>();
                foreach (var op in objProps)
                {
                    var resolvedValue = op.ResolveToExport(exp.FileRef);
                    if (resolvedValue != null && resolvedValue.ClassName == @"WwiseEvent")
                    {
                        op.Value = referencedWwiseEvents.PullFirstItem().UIndex;
                        modified = true;
                    }
                }

                if (modified)
                    exp.WriteProperties(propertyCollection);
            }


            MERFileSystem.SavePackage(sfxgame);
            return true;
        }

        /// <summary>
        /// Creates the loadoutdatamer class, and returns the unsaved package
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static IMEPackage CreateAndSetupMERLoadoutClass(GameTarget target)
        {
            var sfxGame = RSharedSFXGame.GetSFXGame(target);

            if (sfxGame.FindExport("SFXLoadoutDataMER") == null)
            {
                // Add SFXLoadoutDataMER subclass
                var classText = MEREmbedded.GetEmbeddedTextAsset(@"Classes.SFXLoadoutDataMER.uc");
                PackageTools.CreateNewClass(sfxGame, @"SFXLoadoutDataMER", classText);

                // Patch the loadout generation method
                ScriptTools.InstallScriptToExport(target, sfxGame.FindExport("BioPawn.GenerateInventoryFromLoadout"), "GenerateInventoryFromLoadout.uc");
            }

            return sfxGame;
        }
    }
}
