﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using ME3TweaksCore.Targets;
using Randomizer.MER;

namespace Randomizer.Randomizers.Utility
{
    public static class PackageTools
    {
        private static Regex isLevelPersistentPackage = new Regex("Bio([ADPS]|Snd)_[A-Za-z0-9]+.pcc", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex isSublevelPackage = new Regex("Bio([ADPS]|Snd)_[A-Za-z0-9]+_[A-Za-z0-9_]+.pcc", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Is this a localization file?
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static bool IsLocalizationPackage(string pName)
        {
            return pName.Contains("_LOC_");
        }

        /// <summary>
        /// Is this a top level master file (BioP/S/D/A ?)
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static bool IsPersistentPackage(string pName)
        {
            return isLevelPersistentPackage.IsMatch(pName);
        }

        public static bool IsLevelSubfile(string pName)
        {
            return isSublevelPackage.IsMatch(pName);
        }

        /// <summary>
        /// Adds the specified entry to the object referencer in the package. If there is no object referencer already added then this does nothing.
        /// </summary>
        /// <param name="entry">The entry to add. It is not checked if it is already in the list</param>
        /// <returns>If object reference was added</returns>
        public static bool AddToObjectReferencer(IEntry entry)
        {
            var referencer = entry.FileRef.Exports.FirstOrDefault(x => x.ClassName == @"ObjectReferencer");
            if (referencer == null) return false;
            var refs = referencer.GetProperty<ArrayProperty<ObjectProperty>>(@"ReferencedObjects") ?? new ArrayProperty<ObjectProperty>(@"ReferencedObjects");
            refs.Add(new ObjectProperty(entry));
            referencer.WriteProperty(refs);
            return true;
        }

        public static List<IEntry> ReadObjectReferencer(this IMEPackage package)
        {
            var objReferencer = package.Exports.FirstOrDefault(x => x.idxLink == 0 && x.ObjectName == "ObjectReferencer");
            if (objReferencer != null)
            {
                var results = new List<IEntry>();
                var refs = objReferencer.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedObjects");
                foreach (var refX in refs)
                {
                    var eRef = refX.ResolveToEntry(package);
                    if (eRef != null)
                    {
                        results.Add(eRef);
                    }
                }

                return results;
            }

            return null;
        }

        /// <summary>
        /// Ports an export into a package. Checks if the export already exists, and if it does, returns that instead.
        /// </summary>
        /// <param name="targetPackage">The target package to port into.</param>
        /// <param name="sourceExport">The source export to port over, including all dependencies and references.</param>
        /// <param name="targetLink">The target link UIndex. Only used if createParentPackages is false.</param>
        /// <param name="createParentPackages">If the export should be ported in the same way as it was cooked into the package natively, e.g. create the parent package paths. The export must directly sit under a Package or an exception will be thrown.</param>
        /// <param name="ensureMemoryUniqueness">If this object is an instance, such as a sequence object, and should be made memory-unique so it is properly used</param>
        /// <returns></returns>
        public static ExportEntry PortExportIntoPackage(GameTarget target, IMEPackage targetPackage, ExportEntry sourceExport, int targetLink = 0, bool createParentPackages = true, bool ensureMemoryUniqueness = false, bool useMemorySafeImport = false, PackageCache cache = null)
        {
#if DEBUG
            // in preprocessor to prevent this from running in release mode
            if (sourceExport.FileRef.FilePath != null && targetPackage.FilePath != null)
            {
                Debug.WriteLine($"Porting {sourceExport.InstancedFullPath} from {Path.GetFileName(sourceExport.FileRef.FilePath)} into {Path.GetFileName(targetPackage.FilePath)}");
            }
#endif
            var existing = targetPackage.FindExport(sourceExport.InstancedFullPath);
            if (existing != null)
                return existing;

            // Create parent hierarchy
            IEntry newParent = null;
            if (createParentPackages)
            {
                List<IEntry> parents = new List<IEntry>();
                var parent = sourceExport.Parent;
                while (parent != null)
                {
                    if (parent.ClassName != "Package")
                        throw new Exception("Parent is not package!");
                    parents.Add(parent);
                    parent = parent.Parent;
                }

                // Create the parents
                parents.Reverse();
                foreach (var p in parents)
                {
                    var sourceFullPath = p.InstancedFullPath;
                    var matchingParent = targetPackage.FindEntry(sourceFullPath);

                    if (matchingParent != null)
                    {
                        newParent = matchingParent;
                        continue;
                    }

                    newParent = ExportCreator.CreatePackageExport(targetPackage, p.ObjectName, newParent);
                }
            }
            else
            {
                newParent = targetPackage.GetEntry(targetLink);
            }


            IEntry newEntry;
            if (!useMemorySafeImport)
            {
                var relinkResults = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExport, targetPackage,
                    newParent, true, new RelinkerOptionsPackage(), out newEntry); // TODO: CACHE?
                if (relinkResults.Any())
                {
                    Debugger.Break();
                }
            }
            else
            {
                // Memory safe, fixes upstream
                var relinkedResults = EntryExporter.ExportExportToPackage(sourceExport, targetPackage, out newEntry, MERCaches.GlobalCommonLookupCache, cache);
                if (relinkedResults.Any())
                {
#if __GAME2__
                    if (relinkedResults.Count == 1 && relinkedResults[0].Entry.ObjectNameString.Contains("OVRD"))
                    {
                        // Do nothing, this is a false positive from the way METR startup file has some extra imports
                        // from things like BIOG_HMM_HED files. IDK why it's like this.
                    }
                    else
                    {
                        Debugger.Break();
                    }
#else
 Debugger.Break();
#endif
                }
            }
#if DEBUG
            //(sourceExport.FileRef as MEPackage).CompareToPackageDetailed(targetPackage);
#endif

            // Helps ensure we don't have memory duplicates
            if (ensureMemoryUniqueness)
            {
                newEntry.ObjectName = targetPackage.GetNextIndexedName(newEntry.ObjectName);
            }

            return newEntry as ExportEntry;
        }

        /// <summary>
        /// Creates an ImportEntry that references the listed ExportEntry. Ensure the item will be in memory or this will crash the game!
        /// </summary>
        /// <param name="sourceExport"></param>
        /// <param name="targetPackage"></param>
        /// <returns></returns>
        public static ImportEntry CreateImportForClass(ExportEntry sourceExport, IMEPackage targetPackage, IEntry parentObject = null)
        {
            if (sourceExport.ClassName != "Class")
            {
                throw new Exception("Cannot reliably create import for non-class object!");
            }

            var existingImport = targetPackage.FindImport(sourceExport.InstancedFullPath);
            if (existingImport != null)
            {
                return existingImport;
            }

            ImportEntry imp = new ImportEntry(targetPackage)
            {
                ObjectName = sourceExport.ObjectName,
                PackageFile = "Core", //Risky...
                ClassName = sourceExport.ClassName,
                idxLink = parentObject?.UIndex ?? 0,
            };
            targetPackage.AddImport(imp);
            return imp;
        }

        /// <summary>
        /// Creates an ImportEntry
        /// </summary>
        /// <param name="sourceExport"></param>
        /// <param name="targetPackage"></param>
        /// <returns></returns>
        public static ImportEntry CreateImport(IMEPackage targetPackage, NameReference objectName, string className, string packageFile, IEntry parentObject = null)
        {
            ImportEntry imp = new ImportEntry(targetPackage)
            {
                ObjectName = objectName,
                PackageFile = packageFile,
                ClassName = className,
                idxLink = parentObject?.UIndex ?? 0,
            };
            targetPackage.AddImport(imp);
            return imp;
        }

        public static void AddReferencesToWorld(IMEPackage package, IEnumerable<ExportEntry> newRefs)
        {
            var theWorld = package.FindExport("TheWorld");
            var world = ObjectBinary.From<World>(theWorld);
            var extarRefs = world.ExtraReferencedObjects.ToList();
            extarRefs.AddRange(newRefs.Where(x => x != null).Select(x => x.UIndex));
            world.ExtraReferencedObjects = extarRefs.Distinct().ToArray();
            theWorld.WriteBinary(world);
        }

        /// <summary>
        /// Creates an empty ObjectReferencer
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public static ExportEntry CreateObjectReferencer(IMEPackage package)
        {
            var rop = new RelinkerOptionsPackage() { Cache = new PackageCache() };
            var referencer = new ExportEntry(package, 0, package.GetNextIndexedName("ObjectReferencer"), properties: new PropertyCollection() { new ArrayProperty<ObjectProperty>("ReferencedObjects") })
            {
                Class = EntryImporter.EnsureClassIsInFile(package, "ObjectReferencer", rop)
            };
            package.AddExport(referencer);
            return referencer;
        }

        public static void AddObjectReferencerReference(IEntry reference, ExportEntry referencer)
        {
            var objRef = referencer.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedObjects");
            objRef.Add(new ObjectProperty(reference));
            referencer.WriteProperty(objRef);
        }

        /// <summary>
        /// Ports the specified LEXOpenable into the listed package.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="targetPackage"></param>
        /// <param name="underBellyDp"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static ExportEntry PortExportIntoPackage(GameTarget target, LEXOpenable sourceItem, IMEPackage package, MERPackageCache sourceCache = null)
        {
            sourceCache ??= new MERPackageCache(target, MERCaches.GlobalCommonLookupCache, true);
            var sourcePackage = sourceCache.GetCachedPackage(MERFileSystem.GetPackageFile(target, sourceItem.FilePath));
            return PortExportIntoPackage(target, package, sourcePackage.FindExport(sourceItem.EntryPath));
        }

        /// <summary>
        /// Attempts to create a new class
        /// </summary>
        /// <param name="Pcc"></param>
        /// <param name="className"></param>
        /// <param name="classText"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static ExportEntry CreateNewClass(IMEPackage Pcc, string className, string classText, ExportEntry parent = null)
        {
            var fileLib = new FileLib(Pcc);
            if (!fileLib.Initialize())
            {
                MERLog.Error(@"Error initializing filelib for new class: ");
                foreach (var l in fileLib.InitializationLog.AllErrors)
                {
                    MERLog.Error(l.Message);
                }
                return null;
            }


            (ASTNode node, MessageLog log) = UnrealScriptCompiler.CompileClass(Pcc, classText, fileLib, parent: parent);
            if (log.HasErrors)
            {
                MERLog.Error(@"Error creating new class: ");
                foreach (var l in log.AllErrors)
                {
                    MERLog.Error(l.Message);
                }
                return null;
            }
            string fullPath = parent is null ? className : $"{parent.InstancedFullPath}.{className}";
            return Pcc.FindExport(fullPath);
        }

        /// <summary>
        /// Returns the list of objects in the object referencer with the given name. Only returns exports
        /// </summary>
        /// <param name="package"></param>
        /// <param name="referencerIFP"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static List<ExportEntry> GetExportList(IMEPackage package, string referencerIFP)
        {
            var exp = package.FindExport(referencerIFP);
            return exp.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedObjects").Where(x => x.Value > 0)
                .Select(x => x.ResolveToExport(package)).ToList();
        }
    }
}
