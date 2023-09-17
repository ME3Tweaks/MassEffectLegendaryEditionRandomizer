using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.ExportTypes;
using Randomizer.Randomizers.Utility;
using Randomizer.Shared;

namespace Randomizer.Randomizers.Game2.Misc
{
    class SizeSixteens
    {

        public static bool InstallSSChanges(GameTarget target, RandomizationOption option)
        {
            // Freedoms progress
            SetVeetorFootage(target);
            return true;
        }

        private static void SetVeetorFootage(GameTarget target)
        {
            var moviedata = RTextureMovie.GetTextureMovieAssetBinary("Veetor.size_mer.bk2");
            var veetorFiles = MERFileSystem.LoadedFiles.Keys.Where(x => x.StartsWith("BioD_ProFre_501Veetor")).ToList();
            var propertyAssets = MEREmbedded.ListEmbeddedAssets("Text", "Properties.SSVeetor")
                .Select(x => Path.GetFileNameWithoutExtension(x.Substring(x.IndexOf(@"Properties.") + "Properties.".Length))).ToArray();
            var trashItems = new[]
            {
                "profre_veetor_door_d_d.Node_Data_Sequence.SeqAct_ControlMovieTexture_0",
                "profre_veetor_door_d_d.Node_Data_Sequence.SeqAct_ControlMovieTexture_3"
            };
            foreach (var v in veetorFiles)
            {
                MERLog.Information($@"Setting veetor footage in {v}");
                var mpackage = MERFileSystem.GetPackageFile(target, v);
                var package = MEPackageHandler.OpenMEPackage(mpackage);
                var veetorExport = package.FindExport("BioVFX_Env_Hologram.ProFre_501_VeetorFootage");
                if (veetorExport != null)
                {
                    RTextureMovie.RandomizeExportDirect(veetorExport, null, moviedata);
                }

                if (package.Localization != MELocalization.None)
                {
                    // Update the interps to look better
                    foreach (var p in propertyAssets)
                    {
                        ScriptTools.CompileEmbeddedPropertiesToPackage(target, package, p);
                    }

                    foreach (var i in trashItems)
                    {
                        MERSeqTools.RemoveLinksTo(package.FindExport(i));
                    }
                }

                MERFileSystem.SavePackage(package);
            }
        }
    }
}
