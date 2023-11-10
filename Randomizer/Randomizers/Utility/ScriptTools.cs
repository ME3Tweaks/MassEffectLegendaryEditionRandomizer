using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Shared;

namespace Randomizer.Randomizers.Utility
{
    internal class ScriptTools
    {
        /// <summary>
        /// Installs the specified resource script into the specified package and target name
        /// </summary>
        /// <param name="target"></param>
        /// <param name="packageFile"></param>
        /// <param name="instancedFullPath"></param>
        /// <param name="scriptFilename"></param>
        /// <param name="shared"></param>
        public static IMEPackage InstallScriptToPackage(GameTarget target, IMEPackage pf, string instancedFullPath, string scriptFilename, bool shared, bool saveOnFinish = false, PackageCache cache = null)
        {
            var targetExp = pf.FindExport(instancedFullPath);
            InstallScriptToExport(target, targetExp, scriptFilename, shared, cache);
            if (saveOnFinish)
            {
                MERFileSystem.SavePackage(pf);
            }

            return pf;
        }

        /// <summary>
        /// Installs the specified resource script into the specified package and target name
        /// </summary>
        /// <param name="target"></param>
        /// <param name="packageFile"></param>
        /// <param name="instancedFullPath"></param>
        /// <param name="scriptFilename"></param>
        /// <param name="shared"></param>
        public static IMEPackage InstallScriptToPackage(GameTarget target, string packageFile, string instancedFullPath, string scriptFilename, bool shared, bool saveOnFinish = false, PackageCache cache = null)
        {
            IMEPackage pf;
            if (packageFile == "SFXGame.pcc")
            {
                pf = RSharedSFXGame.GetSFXGame(target);
            }
            else
            {
                pf = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, packageFile));
            }

            return InstallScriptToPackage(target, pf, instancedFullPath, scriptFilename, shared, saveOnFinish, cache);
        }

        public static void InstallScriptToExport(GameTarget target, ExportEntry targetExport, string scriptFilename, bool shared = false, PackageCache cache = null)
        {
            MERLog.Information($@"Installing script {scriptFilename} to export {targetExport.InstancedFullPath}");
            string scriptText = MEREmbedded.GetEmbeddedTextAsset($"Scripts.{scriptFilename}", shared);
            InstallScriptTextToExport(target, targetExport, scriptText, scriptFilename, cache);
        }

        /// <summary>
        /// Opens a package and installs a script to the specified path, then saves the package.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="packageFile"></param>
        /// <param name="instancedFullPath"></param>
        /// <param name="scriptText"></param>
        /// <param name="scriptFileNameForLogging"></param>
        /// <param name="cache"></param>
        public static IMEPackage InstallScriptTextToPackage(GameTarget target, string packageFile, string instancedFullPath, string scriptText, string scriptFileNameForLogging, bool saveOnFinish = false, PackageCache cache = null)
        {
            var pf = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, packageFile));
            var export = pf.FindExport(instancedFullPath);
            InstallScriptTextToExport(target, export, scriptText, scriptFileNameForLogging, cache);
            if (saveOnFinish)
            {
                MERFileSystem.SavePackage(pf);
            }

            return pf;
        }

        public static void InstallScriptTextToExport(GameTarget target, ExportEntry targetExport, string scriptText, string scriptFileNameForLogging, PackageCache cache)
        {
            var fl = new FileLib(targetExport.FileRef);
            bool initialized = fl.Initialize(cache, gameRootPath: target.TargetPath);
            if (!initialized)
            {
                MERLog.Error($@"FileLib loading failed for package {targetExport.InstancedFullPath} ({targetExport.FileRef.FilePath}):");
                foreach (var v in fl.InitializationLog.AllErrors)
                {
                    MERLog.Error(v.Message);
                }

                throw new Exception($"Failed to initialize FileLib for package {targetExport.FileRef.FilePath}");
            }

            MessageLog log;
            switch (targetExport.ClassName)
            {
                case "Function":
                    (_, log) = UnrealScriptCompiler.CompileFunction(targetExport, scriptText, fl);
                    break;
                case "State":
                    (_, log) = UnrealScriptCompiler.CompileState(targetExport, scriptText, fl);
                    break;
                case "Class":
                    (_, log) = UnrealScriptCompiler.CompileClass(targetExport.FileRef, scriptText, fl, export: targetExport);
                    break;
                default:
                    throw new Exception("Can't compile to this type yet!");
            }

            if (log.AllErrors.Any())
            {
                MERLog.Error($@"Error compiling {targetExport.ClassName} {targetExport.InstancedFullPath} from filename {scriptFileNameForLogging}:");
                foreach (var l in log.AllErrors)
                {
                    MERLog.Error(l.Message);
                }

                throw new Exception($"Error compiling {targetExport.ClassName} {targetExport.InstancedFullPath} from file {scriptFileNameForLogging}: {string.Join(Environment.NewLine, log.AllErrors)}");
            }

        }

        /// <summary>
        /// Installs a class into the listed package, under the named package, or at the root if null. This loads text from the specified embedded file. The package is not saved
        /// </summary>
        /// <param name="target">The game target that this script can compile against</param>
        /// <param name="packageToInstallTo">The package to install the class into</param>
        /// <param name="embeddedClassName">The name of the embedded class file - the class name must match the filename. DO NOT PUT .uc here, it will be added automatically</param>
        /// <param name="rootPackageName">The IFP of the root package, null if you want the class created at the root of the file</param>
        /// <param name="useCache">If the MERCommonCache should be used. If you are adding things to basegame files you probably don't want to use this as it will be a mix of stale data and current until the cache is refreshed</param>
        public static ExportEntry InstallClassToPackageFromEmbedded(GameTarget target, IMEPackage packageToInstallTo, string embeddedClassName, string rootPackageName = null, bool useCache = true)
        {
            var classText = MEREmbedded.GetEmbeddedTextAsset($"Classes.{embeddedClassName}.uc");
            return InstallClassToPackage(target, packageToInstallTo, embeddedClassName, classText, rootPackageName, useCache);
        }

        /// <summary>
        /// Installs a new class to the specified package, using the provided class text.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="packageToInstallTo"></param>
        /// <param name="classText"></param>
        /// <param name="rootPackageName"></param>
        /// <param name="useCache"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static ExportEntry InstallClassToPackage(GameTarget target, IMEPackage packageToInstallTo, string className, string classText, string rootPackageName = null, bool useCache = true)
        {
            var fl = new FileLib(packageToInstallTo);
            bool initialized = fl.Initialize(gameRootPath: target.TargetPath, packageCache: useCache ? MERCaches.GlobalCommonLookupCache : null);
            if (!initialized)
            {
                MERLog.Error($@"FileLib loading failed for package {packageToInstallTo.FileNameNoExtension}:");
                foreach (var v in fl.InitializationLog.AllErrors)
                {
                    MERLog.Error(v.Message);
                }

                throw new Exception($"Failed to initialize FileLib for package {packageToInstallTo.FilePath}");
            }

            ExportEntry parentExport = null;
            if (rootPackageName != null)
            {
                parentExport = packageToInstallTo.FindExport(rootPackageName);
                if (parentExport == null)
                {
                    // Create the root package we will install the class under
                    parentExport = ExportCreator.CreatePackageExport(packageToInstallTo, rootPackageName);
                }
            }

            MessageLog log;
            (_, log) = UnrealScriptCompiler.CompileClass(packageToInstallTo, classText, fl, export: packageToInstallTo.FindExport(rootPackageName != null ? $"{rootPackageName}.{className}" : className), parent: parentExport);

            if (log.AllErrors.Any())
            {
                MERLog.Error($@"Error compiling class {className}:");
                foreach (var l in log.AllErrors)
                {
                    MERLog.Error(l.Message);
                }

                throw new Exception($"Error compiling {className}: {string.Join(Environment.NewLine, log.AllErrors)}");
            }

            return parentExport != null
                ? packageToInstallTo.FindExport($"{parentExport.InstancedFullPath}.{className}")
                : packageToInstallTo.FindExport(className);
        }

        /// <summary>
        /// AddToOrReplace implementation, reading file from embedded. Does not save the package
        /// </summary>
        /// <param name="target"></param>
        /// <param name="packageToInstallTo"></param>
        /// <param name="scriptName">Do not include .uc</param>
        /// <param name="classIFP"></param>
        /// <exception cref="Exception"></exception>
        public static void AddToClassInPackageFromEmbedded(GameTarget target, IMEPackage packageToInstallTo, string scriptName, string classIFP)
        {
            var scriptText = MEREmbedded.GetEmbeddedTextAsset($"Scripts.{scriptName}.uc");
            AddToClassInPackage(target, packageToInstallTo, scriptText, classIFP);
        }

        public static void AddToClassInPackage(GameTarget target, IMEPackage packageToInstallTo, string scriptText, string classIFP)
        {
            var classExp = packageToInstallTo.FindExport(classIFP);
            var fl = new FileLib(packageToInstallTo);
            bool initialized = fl.Initialize(gameRootPath: target.TargetPath, packageCache: MERCaches.GlobalCommonLookupCache);
            if (!initialized)
            {
                MERLog.Error($@"FileLib loading failed for package {packageToInstallTo.FileNameNoExtension}:");
                foreach (var v in fl.InitializationLog.AllErrors)
                {
                    MERLog.Error(v.Message);
                }

                throw new Exception($"Failed to initialize FileLib for package {packageToInstallTo.FilePath}");
            }
            MessageLog log = UnrealScriptCompiler.AddOrReplaceInClass(classExp, scriptText, fl);
            if (log.HasErrors)
            {
                Debugger.Break();
            }
        }
        public static void CompileEmbeddedPropertiesToPackage(GameTarget target, IMEPackage packageToInstallTo, string textAssetName)
        {
            var propertiesText = MEREmbedded.GetEmbeddedTextAsset($"Properties.{textAssetName}.uc");
            var ifp = propertiesText.SplitToLines().First().TrimStart('/');
            var targetExport = packageToInstallTo.FindExport(ifp);

            var fl = new FileLib(packageToInstallTo);
            bool initialized = fl.Initialize(gameRootPath: target.TargetPath, packageCache: MERCaches.GlobalCommonLookupCache);
            if (!initialized)
            {
                MERLog.Error($@"FileLib loading failed for package {packageToInstallTo.FileNameNoExtension}:");
                foreach (var v in fl.InitializationLog.AllErrors)
                {
                    MERLog.Error(v.Message);
                }

                throw new Exception($"Failed to initialize FileLib for package {packageToInstallTo.FilePath}");
            }
            var log = UnrealScriptCompiler.CompileDefaultProperties(targetExport, propertiesText, fl);
            if (log.log.HasErrors)
            {
                Debugger.Break();
            }
        }
    }
}
