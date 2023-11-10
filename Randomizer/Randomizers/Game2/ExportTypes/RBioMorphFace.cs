using System.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.ExportTypes
{
    class RBioMorphFace
    {
        public static bool RandomizeBioMorphFace2(GameTarget target, RandomizationOption option)
        {
            MERControl.InstallBioPawnMERControl(target);
            MERControl.InstallSFXSkeletalMeshActorMATMERControl(target);
            MERControl.InstallBioMorphFaceRandomizerClasses(target);

            // Patch in the stubs (I miss you Stubby Mo)
            var sfxgame = RSharedSFXGame.GetSFXGame(target);
            ScriptTools.AddToClassInPackageFromEmbedded(target, sfxgame, "SKM_RandomizeMorphHead", "MERControl");
            ScriptTools.AddToClassInPackageFromEmbedded(target, sfxgame, "BioPawn_RandomizeMorphHead", "MERControl");
            MERFileSystem.SavePackage(sfxgame);

            MERControl.SetVariable("fBioMorphFaceRandomization", option.SliderValue, CoalesceParseAction.Add);
            return true;
        }


        private static RandomizationOption henchFaceOption = new RandomizationOption() { SliderValue = .3f };

        private static string[] SquadmateMorphHeadPaths =
        {
            "BIOG_Hench_FAC.HMM.hench_wilson",
            "BIOG_Hench_FAC.HMM.hench_leadingman"
        };

        public static bool RandomizeSquadmateFaces(GameTarget target, RandomizationOption option)
        {
            var henchFiles = MERFileSystem.LoadedFiles.Where(x => x.Key.StartsWith("BioH_")
                                                                  || x.Key.StartsWith("BioP_ProCer")
                                                                  || x.Key.StartsWith("BioD_ProCer")
                                                                  || x.Key == "BioD_EndGm1_110ROMJacob.pcc");
            foreach (var h in henchFiles)
            {
                var hPackage = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, h.Key));
                foreach (var smhp in SquadmateMorphHeadPaths)
                {
                    var mf = hPackage.FindExport(smhp);
                    if (mf != null)
                    {
                        RSharedBioMorphHead.RandomizeInternal(mf, henchFaceOption);

                    }
                }
                MERFileSystem.SavePackage(hPackage);
            }
            return true;
        }

        private static bool CanRandomizeNonHench(ExportEntry export) => !export.IsDefaultObject
                                                                && export.ClassName == @"BioMorphFace"
                                                                && !export.ObjectName.Name.Contains("hench_leadingman")
                                                                && !export.ObjectName.Name.Contains("hench_wilson");
        public static bool RandomizeExportNonHench(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            if (!CanRandomizeNonHench(export)) return false;
            RSharedBioMorphHead.RandomizeInternal(export, option);
            return true;
        }
    }
}
