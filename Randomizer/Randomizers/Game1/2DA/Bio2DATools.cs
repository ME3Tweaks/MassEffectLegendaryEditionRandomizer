using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Shared;

namespace Randomizer.Randomizers.Game1._2DA
{
    internal class Bio2DATools
    {
        /// <summary>
        /// Returns package files that contain global 2DAs, including DLC.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static List<IMEPackage> GetAll2DAPackages(GameTarget target)
        {
            List<IMEPackage> sourcePackages = new List<IMEPackage>(); // List of packages that contain 2DAs that we are going to change (maybe not save to, but will effectively change)
            sourcePackages.Add(RSharedEngine.GetEngine(target));
            sourcePackages.Add(RSharedSFXGame.GetSFXGame(target)); // A few 2DAs are here.
            ParseAutoloadFor2DA(target, Path.Combine(target.GetCookedPath(), "AutoLoad.ini"), sourcePackages); // Load Bring Down the Sky 2DAs

            // Load all DLC in order of mount priority (lowest to highest) 
            var installedDLC = target.GetInstalledDLCByMountPriority();

            foreach (var id in installedDLC)
            {
                if (id == MERFileSystem.DLCModName)
                    continue; // Ignore this
                var autoloadPath = Path.Combine(target.GetDLCPath(), id, "AutoLoad.ini");
                if (File.Exists(autoloadPath))
                {
                    ParseAutoloadFor2DA(target, autoloadPath, sourcePackages);
                }
            }

            return sourcePackages;
        }

        private static void ParseAutoloadFor2DA(GameTarget target, string autoloadPath, List<IMEPackage> sourcePackages)
        {
            var autoloadIni = new AutoloadIni(autoloadPath);
            foreach (var bio2da in autoloadIni.Bio2DAs)
            {
                var packageFile = MERFileSystem.GetPackageFile(target, $"{bio2da}.pcc");
                if (packageFile != null)
                {
                    sourcePackages.Add(MERFileSystem.OpenMEPackage(packageFile));
                }
            }
        }
    }
}
