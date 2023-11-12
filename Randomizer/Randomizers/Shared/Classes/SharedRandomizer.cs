using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Randomizer.MER;

namespace Randomizer.Randomizers.Shared.Classes
{
    /// <summary>
    /// Shared Randomizer code
    /// </summary>
    internal class SharedRandomizer
    {
        /// <summary>
        /// List of packages that are only on disk for the duration of the randomization, e.g. to port out of, load classes, etc.
        /// </summary>
        public static List<string> InstallTimeOnlyPackages { get; } = new();

        /// <summary>
        /// Loads custom classes from the install session only folder
        /// </summary>
        public static void InventoryCustomKismetClasses()
        {
            foreach (var package in MEREmbedded.ExtractEmbeddedBinaryFolder($"Packages.{MERFileSystem.Game}.InstallSessionOnly"))
            {
                if (!package.RepresentsPackageFilePath())
                {
                    continue;
                }
                var actualPath = Path.Combine(MERFileSystem.DLCModCookedPath, MEREmbedded.GetFilenameFromAssetName(package));
                InstallTimeOnlyPackages.Add(actualPath);

                MERLog.Information($"Inventorying kismet package {actualPath}");
                using var p = MEPackageHandler.OpenMEPackage(actualPath);
                foreach (var ex in p.Exports.Where(x => x.IsClass && x.InheritsFrom("SequenceObject")))
                {
                    var classInfo = GlobalUnrealObjectInfo.generateClassInfo(ex);
                    var defaults = p.GetUExport(ObjectBinary.From<UClass>(ex).Defaults);
                    MERLog.Information($@"Inventorying class {ex.InstancedFullPath}");
                    GlobalUnrealObjectInfo.GenerateSequenceObjectInfoForClassDefaults(defaults);
                    GlobalUnrealObjectInfo.InstallCustomClassInfo(ex.ObjectName, classInfo, ex.Game);
                }
            }
        }

        /// <summary>
        /// Removes Install Session Only files from disk
        /// </summary>
        /// <param name="installTimeOnlyPackages"></param>
        public static void CleanupInstallTimeOnlyFiles()
        {
            foreach (var f in InstallTimeOnlyPackages)
            {
                if (File.Exists(f))
                {
                    File.Delete(f);
                }
            }

            InstallTimeOnlyPackages.Clear();
        }
    }
}
