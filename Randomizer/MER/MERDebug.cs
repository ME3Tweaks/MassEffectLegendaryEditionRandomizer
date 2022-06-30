﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using ME3TweaksCore.Targets;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Utility;

namespace Randomizer.MER
{
    public class MERDebug
    {
        public static void InstallDebugScript(GameTarget target, string packagename, string scriptName)
        {
#if DEBUG
            Debug.WriteLine($"Installing debug script {scriptName}");
            ScriptTools.InstallScriptToPackage(target, packagename, scriptName, "Debug." + scriptName + ".uc", false,
                true);
#endif
        }

        public static void InstallDebugScript(IMEPackage package, string scriptName, bool saveOnFinish)
        {
#if DEBUG
            Debug.WriteLine($"Installing debug script {scriptName}");
            ScriptTools.InstallScriptToPackage(package, scriptName, "Debug." + scriptName + ".uc", false, saveOnFinish);
#endif
        }

        public static void DebugPrintActorNames(object sender, RunWorkerCompletedEventArgs e)
        {
#if DEBUG
            var game = MEGame.LE3;
            var files = MELoadedFiles.GetFilesLoadedInGame(game, true, false).Values
                //.Where(x =>
                //                    !x.Contains("_LOC_")
                //&& x.Contains(@"CitHub", StringComparison.InvariantCultureIgnoreCase)
                //)
                //.OrderBy(x => x.Contains("_LOC_"))
                .ToList();

            // PackageName -> GesturePackage
            int i = 0;
            SortedSet<string> actorTypeNames = new SortedSet<string>();
            TLKBuilder.StartHandler(new GameTarget(game, MEDirectories.GetDefaultGamePath(game), false));
            foreach (var f in files)
            {
                i++;
                var p = MEPackageHandler.UnsafePartialLoad(f,
                    x => !x.IsDefaultObject &&
                         (x.ClassName == "SFXSimpleUseModule" || x.ClassName == "SFXModule_AimAssistTarget"));
                foreach (var exp in p.Exports.Where(x =>
                             !x.IsDefaultObject && (x.ClassName == "SFXSimpleUseModule" ||
                                                    x.ClassName == "SFXModule_AimAssistTarget")))
                {
                    if (exp.Parent.ClassName == "SFXPointOfInterest")
                        continue; // 
                    var displayNameVal = exp.GetProperty<StringRefProperty>("m_SrGameName");
                    if (displayNameVal != null)
                    {
                        var displayName = TLKBuilder.TLKLookupByLang(displayNameVal.Value, MELocalization.INT);
                        actorTypeNames.Add($"{displayNameVal.Value}: {displayName}");
                    }
                    else
                    {
                        // actorTypeNames.Add(exp.ObjectName.Instanced);
                    }
                }

            }

            foreach (var atn in actorTypeNames)
            {
                Debug.WriteLine(atn);
            }
        }
#endif


        /// <summary>
        /// Attempts to build full packages (still ForcedExport unfortunately)
        /// </summary>
        /// <param name="target"></param>
        public static void DecookGame(object sender, DoWorkEventArgs doWorkEventArgs)
        {
#if DEBUG
            var game = MEGame.LE3;
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(game);
            var outputDirectory = @"B:\DecookedGames\LE3";

            // Step 1: Find all top level package exports
            SortedSet<string> topLevelPackages = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, IMEPackage> fileToTablesOnlyPackage = new Dictionary<string, IMEPackage>();
            foreach (var f in loadedFiles)
            {
                if (f.Key.StartsWith("BIOG_"))
                    continue; // These are already cooked seek free

                var file = MERFileSystem.OpenMEPackageTablesOnly(f.Value);
                foreach (var exp in file.Exports.Where(x => !x.IsDefaultObject && x.ClassName == "Package" && x.idxLink == 0))
                {
                    // Don't add blanks
                    if (file.Exports.Any(x => x.idxLink == exp.UIndex))
                    {
                        topLevelPackages.Add(exp.ObjectName);
                    }
                }

                fileToTablesOnlyPackage[f.Key] = file;
            }

            //File.WriteAllLines(@"B:\DecookedGames\LE3TopLevelPackages.txt", topLevelPackages.ToList());
            //foreach (var v in topLevelPackages)
            //{
            //    Debug.WriteLine(v);
            //}

            Debug.WriteLine("Beginning decook");

            // var decookPackagesList = File.ReadAllLines(@"B:\DecookedGames\LE3TopLevelPackages.txt");

            using var globalCache = MERFileSystem.GetGlobalCache(new GameTarget(game, MEDirectories.GetDefaultGamePath(game), true));

            Parallel.ForEach(topLevelPackages, new ParallelOptions() { MaxDegreeOfParallelism = 6 }, tlp =>
            {
                Debug.WriteLine($"Decooking {tlp}");
                if (tlp.Contains("..") || tlp.Contains('\\'))
                {
                    Debug.WriteLine($"Skipping path-named package {tlp}");
                    return;
                }

                var topLevelNameBase = $"{tlp}."; // Add . to denote separator
                var decookedPackagePath = Path.Combine(outputDirectory, tlp + ".pcc");
                MEPackageHandler.CreateAndSavePackage(decookedPackagePath, game);
                var decookedPackage = MEPackageHandler.OpenMEPackage(decookedPackagePath);
                foreach (var tableOnlyPackage in fileToTablesOnlyPackage)
                {
                    if (tableOnlyPackage.Value.Exports.Any(x => x.InstancedFullPath.StartsWith(topLevelNameBase, StringComparison.InvariantCultureIgnoreCase) && decookedPackage.FindEntry(x.InstancedFullPath) == null))
                    {
                        var package = MEPackageHandler.OpenMEPackage(tableOnlyPackage.Value.FilePath);
                        using PackageCache localCache = new PackageCache();
                        foreach (var itemToPort in package.Exports.Where(x => x.InstancedFullPath.StartsWith(topLevelNameBase, StringComparison.InvariantCultureIgnoreCase)).ToList())
                        {
                            EntryExporter.ExportExportToPackage(itemToPort, decookedPackage, out var _, globalCache, localCache);
                        }
                    }
                }
                decookedPackage.Save();
            });
            fileToTablesOnlyPackage = null;
#endif
        }
    }
}