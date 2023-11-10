using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using Newtonsoft.Json;
using Randomizer.MER;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Shared.Classes
{
    public class SourceTexture
    {
        private string FindFile()
        {
            var fPath = $@"G:\My Drive\Mass Effect Legendary Modding\LERandomizer\{MERFileSystem.Game}\Images";
            foreach (var f in Directory.GetFiles(fPath, @"*.*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(f).CaseInsensitiveEquals(Filename))
                {
                    return f;
                }
            }

            return null;
        }

        /// <summary>
        /// The filename on disk. Will be enumerated for, names must be unique
        /// </summary>
        [JsonIgnore]
        public string Filename { get; set; }

        /// <summary>
        /// The ID of the texture
        /// </summary>
        [JsonProperty(@"id")]
        public string Id { get; set; }

        /// <summary>
        /// Tee name of a package that contains the desired destination texture - doesn't have to be exact, but must match dest type (DXT, etc)
        /// </summary>
        [JsonIgnore]
        public string ContainingPackageName { get; set; }

        /// <summary>
        /// List of IPs in ContainingPackageName that can be used to trigger the texture replacement
        /// </summary>
        [JsonIgnore]
        public string[] IFPsToBuildOff { get; set; }

        /// <summary>
        /// Installed by specific randomizer, not general texture randomizer
        /// </summary>
        [JsonIgnore]
        public bool SpecialUseOnly { get; set; }

        public void StoreTexture(IMEPackage premadePackage)
        {
            Debug.WriteLine($"Storing texture {Id}");
            var sourceFile = FindFile();
            if (sourceFile == null)
            {
                throw new Exception($"Source file not found: {Filename}");
            }

            using var sourceFileData = File.OpenRead(sourceFile);
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(MERFileSystem.Game);
            var packageF = loadedFiles[ContainingPackageName];
            using var package = MEPackageHandler.OpenMEPackage(packageF);
            var i = 0;
            var stored = false;
            while (i < IFPsToBuildOff.Length)
            {
                var sourceTex = package.FindExport(IFPsToBuildOff[i]);
                if (sourceTex == null)
                {
                    i++;
                    continue;
                }

                TextureTools.ReplaceTexture(sourceTex, sourceFileData, false, out var loadedImage);
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, sourceTex, premadePackage, null, true, new RelinkerOptionsPackage(), out var newEntry);
                newEntry.ObjectName = Id;
                stored = true;
                break;
            }

            if (!stored)
            {
                Debug.WriteLine($@"Failed to store texture: {stored}");
                Debugger.Break();
            }
        }
    }
}
