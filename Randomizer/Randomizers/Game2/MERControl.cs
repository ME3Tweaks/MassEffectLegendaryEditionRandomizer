using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2
{
    /// <summary>
    /// Handler for MERControl enabled modifications
    /// </summary>
    public class MERControl
    {
        private static bool InstalledBioPawnMERControl = false;
        public static void InstallBioPawnMERControl(GameTarget target)
        {
            if (!InstalledBioPawnMERControl)
            {
                var sfxgame = SFXGame.GetSFXGame(target);
                ScriptTools.InstallScriptToExport(sfxgame.FindExport("BioPawn.PostBeginPlay"),
                    "BioPawn.PostBeginPlay.uc");
                MERFileSystem.SavePackage(sfxgame);
                InstalledBioPawnMERControl = true;
            }
        }

        private static bool InstalledSFXSkeletalMeshActorMATMERControl = false;
        public static void InstallSFXSkeletalMeshActorMATMERControl(GameTarget target)
        {
            if (!InstalledSFXSkeletalMeshActorMATMERControl)
            {
                var sfxgame = SFXGame.GetSFXGame(target);
                ScriptTools.AddToClassInPackage(target, sfxgame, "SFXSkeletalMeshActorMAT.PostBeginPlay",
                    "SFXSkeletalMeshActorMAT");
                MERFileSystem.SavePackage(sfxgame);
                InstalledSFXSkeletalMeshActorMATMERControl = true;
            }
        }

        private static bool InstalledBioMorphFaceClass = false;

        public static void InstallBioMorphFaceRandomizerClasses(GameTarget target)
        {
            if (!InstalledBioMorphFaceClass)
            {
                var sfxgame = SFXGame.GetSFXGame(target);

                // Instead headmorph utility classes
                using var morphUtilP = MEPackageHandler.OpenMEPackageFromStream(MEREmbedded.GetEmbeddedPackage(target.Game, "Headmorph.MERHeadmorphUtility.pcc"), "MERHeadmorphUtility.pcc");

                var meshTools = sfxgame.FindEntry("MeshTools") ?? ExportCreator.CreatePackageExport(sfxgame, "MeshTools");
                foreach (var classx in morphUtilP.Exports.Where(x => x.IsClass))
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, classx, sfxgame, classx.idxLink != 0 ? meshTools : null, true, new RelinkerOptionsPackage(), out _);
                }

                // Install the utility class
                ScriptTools.InstallClassToPackage(target, sfxgame, "MERBioMorphUtility");

                // Patch in the stubs
                ScriptTools.AddToClassInPackage(target, sfxgame, "SKM_RandomizeMorphHead", "MERControl");
                ScriptTools.AddToClassInPackage(target, sfxgame, "BioPawn_RandomizeMorphHead", "MERControl");

                MERFileSystem.SavePackage(sfxgame);
                InstalledBioMorphFaceClass = true;
            }
        }

        public static bool InstallMERControl(GameTarget target)
        {
            // Engine class
            var engine = Engine.GetEngine(target);
            ScriptTools.InstallClassToPackage(target, engine, "MERControlEngine");
            MERFileSystem.SavePackage(engine);

            var sfxgame = SFXGame.GetSFXGame(target);
            ScriptTools.InstallClassToPackage(target, sfxgame, "MERControl");
            MERFileSystem.SavePackage(sfxgame);
            return true;
        }

        public static void ResetClass()
        {
            InstalledBioPawnMERControl = false;
            InstalledBioMorphFaceClass = false;
            InstalledSFXSkeletalMeshActorMATMERControl = false;
        }
    }
}
