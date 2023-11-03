using System.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Handlers;
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
                ScriptTools.InstallScriptToExport(target, sfxgame.FindExport("BioPawn.PostBeginPlay"),
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
                ScriptTools.AddToClassInPackageFromEmbedded(target, sfxgame, "SFXSkeletalMeshActorMAT.PostBeginPlay",
                    "SFXSkeletalMeshActorMAT");
                MERFileSystem.SavePackage(sfxgame);
                InstalledSFXSkeletalMeshActorMATMERControl = true;
            }
        }

        private static bool InstalledBioMorphFaceClass = false;

        /// <summary>
        /// Installs the tools needed to randomize morphs. Returns SFXGame if installed; null if already installed
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static IMEPackage InstallBioMorphFaceRandomizerClasses(GameTarget target)
        {
            IMEPackage sfxgame = null;
            if (!InstalledBioMorphFaceClass)
            {
                sfxgame = SFXGame.GetSFXGame(target);

                // Instead headmorph utility classes
                using var morphUtilP = MEPackageHandler.OpenMEPackageFromStream(MEREmbedded.GetEmbeddedPackage(target.Game, "Headmorph.MERHeadmorphUtility.pcc"), "MERHeadmorphUtility.pcc");

                var meshTools = sfxgame.FindEntry("MeshTools") ?? ExportCreator.CreatePackageExport(sfxgame, "MeshTools");
                foreach (var classx in morphUtilP.Exports.Where(x => x.IsClass))
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, classx, sfxgame, classx.idxLink != 0 ? meshTools : null, true, new RelinkerOptionsPackage(), out _);
                }

                // Install dependencies of utility class
                ScriptTools.InstallClassToPackageFromEmbedded(target, sfxgame, "MERMorphStructs");
                ScriptTools.InstallClassToPackage(target, sfxgame, "CCAlgorithm", @"Class CCAlgorithm; var MorphRandomizationAlgorithm algorithm; defaultproperties{}");

                // Install the utility class
                ScriptTools.InstallClassToPackageFromEmbedded(target, sfxgame, "MERBioMorphUtility");

                MERFileSystem.SavePackage(sfxgame);
                InstalledBioMorphFaceClass = true;
                MERCaches.ReInit(target);
            }

            return sfxgame;
        }

        /// <summary>
        /// Installs the object pinning system into SFXEngine, which can be used to store object references, differentiating via class types.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sfxgame"></param>
        public static void InstallObjectPinSystem(GameTarget target, IMEPackage sfxgame)
        {
            if (sfxgame.FindExport("SFXEngine.PinnedObjects") == null)
            {
                ScriptTools.AddToClassInPackage(target, sfxgame, @"var array<Object> PinnedObjects;", "SFXEngine");
                ScriptTools.InstallClassToPackageFromEmbedded(target, sfxgame, "SFXObjectPinner");
            }
        }

        public static bool InstallMERControl(GameTarget target)
        {
            // Engine class
            var engine = Engine.GetEngine(target);
            ScriptTools.InstallClassToPackageFromEmbedded(target, engine, "MERControlEngine", useCache: true);
            MERFileSystem.SavePackage(engine);

            var sfxgame = SFXGame.GetSFXGame(target);
            ScriptTools.InstallClassToPackageFromEmbedded(target, sfxgame, "MERControl");
            // MERFileSystem.SavePackage(sfxgame);
            return true;
        }

        public static void ResetClass()
        {
            InstalledBioPawnMERControl = false;
            InstalledBioMorphFaceClass = false;
            InstalledSFXSkeletalMeshActorMATMERControl = false;
        }

        public static void SetVariable(string key, object value, CoalesceParseAction parseAction = CoalesceParseAction.Add)
        {
            var bioEngine = CoalescedHandler.GetIniFile("BIOEngine.ini");
            var section = bioEngine.GetOrAddSection("Engine.MERControlEngine");
            section.AddEntryIfUnique(new CoalesceProperty(key, new CoalesceValue(value.ToString(), parseAction)));
        }
    }
}
