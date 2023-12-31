﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using ME3TweaksCore.Targets;
using Newtonsoft.Json;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Enemy
{
    class EnemyWeaponChanger
    {
        public const string SUBOPTIONKEY_ENEMYWEAPONS_FORCERANDOMIZER = "SUBOPTIONKEY_ENEMYWEAPONS_FORCERANDOMIZER";
        public const string SUBOPTIONKEY_ENEMYWEAPONS_ALLOWINVISIBLE = "SUBOPTIONKEY_ENEMYWEAPONS_ALLOWINVISIBLE";
        public const string SUBOPTIONKEY_ENEMYWEAPONS_ONETIMERANDOMIZE = "SUBOPTIONKEY_ENEMYWEAPONS_ONETIMERANDOMIZE";

        // Animation info
        private static ArrayProperty<StructProperty> WeaponAnimationsArrayProp;
        private static IMEPackage WeaponAnimsPackage;

        // Weapon Info
        private static ConcurrentDictionary<string, bool> LoadoutSupportsVisibleMapping;
        public static List<GunInfo> AllAvailableWeapons;
        private static List<GunInfo> VisibleAvailableWeapons;

        public static void LoadGuns(GameTarget target)
        {
            if (AllAvailableWeapons == null)
            {
                string fileContents = MEREmbedded.GetEmbeddedTextAsset("weaponloadoutrules.json");
                LoadoutSupportsVisibleMapping = JsonConvert.DeserializeObject<ConcurrentDictionary<string, bool>>(fileContents);

                fileContents = MEREmbedded.GetEmbeddedTextAsset("weaponlistme2.json");
                var allGuns = JsonConvert.DeserializeObject<List<GunInfo>>(fileContents).ToList();
                AllAvailableWeapons = new List<GunInfo>();
                VisibleAvailableWeapons = new List<GunInfo>();
                foreach (var g in allGuns)
                {
                    var gf = MERFileSystem.GetPackageFile(target, g.PackageFileName, false);
                    if (g.IsCorrectedPackage || (gf != null && File.Exists(gf)))
                    {
                        MERLog.Information($@"Adding {g.GunName} to weapon selection pools");
                        AllAvailableWeapons.Add(g);
                        if (g.HasGunMesh)
                        {
                            VisibleAvailableWeapons.Add(g);
                        }
                    }

                    if (!g.IsCorrectedPackage && gf == null)
                    {
                        MERLog.Information($@"{g.GunName} package file not found ({g.PackageFileName}), weapon not added to weapon pools");
                    }
                }
                Debug.WriteLine($"Number of available weapons for randomization: {AllAvailableWeapons.Count}");
                Debug.WriteLine($"Number of visible weapons for randomization: {VisibleAvailableWeapons.Count}");
            }
        }



        /// <summary>
        /// Initializes weapon randomizer, including installing required changes to SFXGame.pcc
        /// </summary>
        /// <param name="option"></param>
        /// <returns></returns>
        public static bool Init(GameTarget target, RandomizationOption option)
        {
            MERLog.Information(@"Preloading weapon data");
            LoadGuns(target);

            MERFileSystem.SavePackage(SFXGame.CreateAndSetupMERLoadoutClass(target));

            var bioWeapon = CoalescedHandler.GetIniFile(@"BioWeapon.ini");
            var sfxLoadoutDataMER = bioWeapon.GetOrAddSection(@"SFXGame.SFXLoadoutDataMER");
            var enemyWeaponsList = MEREmbedded.GetEmbeddedTextAsset(@"enemyweaponifps.txt")
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var ew in enemyWeaponsList)
            {
                // Lines starting with // are 'commented' out - mainly so during dev
                // we can force specific lines
                if (!ew.StartsWith("//"))
                {
                    sfxLoadoutDataMER.AddEntryIfUnique(new CoalesceProperty(@"RandomWeaponOptions", new CoalesceValue(ew, CoalesceParseAction.AddUnique)));
                }
            }

            // Extract all weapon randomizer packages
            MEREmbedded.ExtractEmbeddedBinaryFolder(@"Packages.LE2.Weapons");

            // Add corrected weapons here for dynamic load
            CoalescedHandler.AddDynamicLoadMappingEntry(new SeekFreeInfo() { EntryPath = @"SFXGameContentMER.SFXHeavyWeapon_Blackstorm_MER", SeekFreePackage = @"SFXHeavyWeapon_Blackstorm_MER" });
            CoalescedHandler.AddDynamicLoadMappingEntry(new SeekFreeInfo() { EntryPath = @"SFXGameContent_Inventory.SFXWeapon_GethMiniGun", SeekFreePackage = @"SFXWeapon_GethMiniGun" });
            CoalescedHandler.AddDynamicLoadMappingEntry(new SeekFreeInfo() { EntryPath = @"SFXGameContent_Inventory.SFXHeavyWeapon_FlameThrower", SeekFreePackage = @"SFXHeavyWeapon_FlameThrower" });
            CoalescedHandler.AddDynamicLoadMappingEntry(new SeekFreeInfo() { EntryPath = @"SFXGameContent_Inventory.SFXHeavyWeapon_ScionGun", SeekFreePackage = @"SFXHeavyWeapon_ScionGun" });
            CoalescedHandler.AddDynamicLoadMappingEntry(new SeekFreeInfo() { EntryPath = @"SFXGameContent_Inventory.SFXHeavyWeapon_ReaperMIRV", SeekFreePackage = @"SFXHeavyWeapon_ReaperMIRV" });
            CoalescedHandler.AddDynamicLoadMappingEntry(new SeekFreeInfo() { EntryPath = @"SFXGameContent_Inventory.SFXHeavyWeapon_OculusParticleBeam", SeekFreePackage = @"SFXHeavyWeapon_OculusParticleBeam" });

            // Add animations
            MERLog.Information(@"Installing weapon animations startup package");
            WeaponAnimsPackage = MEPackageHandler.OpenMEPackageFromStream(MEREmbedded.GetEmbeddedPackage(MEGame.LE2, @"Weapons.Startup_LE2R_WeaponAnims.pcc"), @"Startup_LE2R_WeaponAnims.pcc");

            // Add animation to startup
            ThreadSafeDLCStartupPackage.AddStartupPackage(@"Startup_LE2R_WeaponAnims"); // Make it referencable via imports

            // Set runtime feature flags
            CoalescedHandler.EnableFeatureFlag("bEnemyWeaponRandomizer");
            CoalescedHandler.EnableFeatureFlag("bEnemyWeaponRandomizer_Force", option.HasSubOptionSelected(SUBOPTIONKEY_ENEMYWEAPONS_FORCERANDOMIZER));
            CoalescedHandler.EnableFeatureFlag("bEnemyWeaponRandomizer_OneTime", option.HasSubOptionSelected(SUBOPTIONKEY_ENEMYWEAPONS_ONETIMERANDOMIZE));

            WeaponAnimationsArrayProp = WeaponAnimsPackage.FindExport("WeaponAnimData").GetProperty<ArrayProperty<StructProperty>>("WeaponAnimSpecs");
            return true;
        }

        /// <summary>
        /// Only use for debug mode!
        /// </summary>
        public static void Preboot(GameTarget target)
        {
            LoadGuns(target);
        }

        /// <summary>
        /// Converts an AnimSet export to an import (from a startup package)
        /// </summary>
        /// <param name="origEntry"></param>
        /// <param name="targetPackage"></param>
        /// <returns></returns>
        private static IEntry ConvertAnimSetExportToImport(IEntry origEntry, IMEPackage targetPackage)
        {
            // Check if this item is available already
            var found = targetPackage.FindEntry(origEntry.InstancedFullPath);
            if (found != null)
                return found;

            // Setup the link for our import
            var parentPackage = targetPackage.FindEntry(origEntry.Parent.InstancedFullPath);
            if (parentPackage == null)
            {
                // We must add a package import
                parentPackage = new ImportEntry(targetPackage)
                {
                    idxLink = 0,
                    ClassName = "Package",
                    ObjectName = origEntry.Parent.ObjectName,
                    PackageFile = "Core"
                };
                targetPackage.AddImport((ImportEntry)parentPackage);
            }

            // Install the import
            ImportEntry imp = new ImportEntry(targetPackage)
            {
                ClassName = origEntry.ClassName,
                idxLink = parentPackage.UIndex,
                ObjectName = origEntry.ObjectName,
                PackageFile = "Engine"
            };
            targetPackage.AddImport(imp);

            return imp;
        }

        [DebuggerDisplay("GunInfo for {GunName} in {PackageFileName}")]
        internal class GunInfo
        {
            public enum EWeaponClassification
            {
                AssaultRifle,
                Pistol,
                SMG,
                Shotgun,
                SniperRifle,
                HeavyWeapon
            }
            /// <summary>
            /// The parent package export of this export
            /// </summary>
            [JsonProperty("packagename")]
            public string PackageName { get; set; } = "SFXGameContent_Inventory";
            /// <summary>
            /// Package file that contains this class export
            /// </summary>
            [JsonProperty("packagefilename")]
            public string PackageFileName { get; set; }
            [JsonProperty("sourceuindex")]

            public int SourceUIndex { get; set; }
            /// <summary>
            /// Weapon selection weighting
            /// </summary>
            [JsonIgnore]
            public float Weight { get; set; } = 1.0f;
            [JsonProperty("weaponclassification")]
            public EWeaponClassification WeaponClassification { get; set; }

            /// <summary>
            /// If gun has a mesh. If it doesn't, it can only be used by pawns that support hidden mesh guns
            /// </summary>
            [JsonProperty("hasgunmesh")]
            public bool HasGunMesh { get; set; }
            /// <summary>
            /// Object Name
            /// </summary>
            [JsonProperty("gunname")]
            public string GunName { get; set; }
            /// <summary>
            /// If this gun can only be used via imports - this is for DLC weapons that are for some reason loaded in a startup file and thus will always be in memory
            /// </summary>
            [JsonProperty("importonly")]
            public bool ImportOnly { get; set; }

            [JsonIgnore]
            public bool IsUsable { get; set; } = true;
            [JsonIgnore]
            public long PackageFileSize { get; set; }
            /// <summary>
            /// If this is a DLC weapon that must be loaded into memory in order to be used (due to immovable shader cache)
            /// </summary>
            [JsonProperty("requiresstartuppackage")]
            public bool RequiresStartupPackage { get; set; }

            public GunInfo() { }
            public GunInfo(ExportEntry export, bool isCorrected)
            {
                ParseGun(export);
                GunName = export.ObjectName;
                if (GunName == "SFXHeavyWeapon_BlackStorm")
                {
                    // We do not allow player blackstorm. It must be embedded patched version
                    IsUsable = false;
                    return;
                }
                PackageFileName = Path.GetFileName(export.FileRef.FilePath);
                PackageName = export.ParentName;
                SourceUIndex = export.UIndex;
                PackageFileSize = isCorrected ? 0 : new FileInfo(export.FileRef.FilePath).Length;
                IsCorrectedPackage = isCorrected;
            }

            /// <summary>
            /// If the file this is sourced from is stored in the randomizer executable and not the game
            /// </summary>
            [JsonProperty("iscorrectedpackage")]
            public bool IsCorrectedPackage { get; set; }

            private void ParseGun(ExportEntry classExport)
            {
                var uClass = ObjectBinary.From<UClass>(classExport);
                var defaults = classExport.FileRef.GetUExport(uClass.Defaults);
                var props = defaults.GetProperties();

                var mesh = props.GetProp<ObjectProperty>("Mesh");
                if (mesh?.ResolveToEntry(classExport.FileRef) is ExportEntry meshExp)
                {
                    var meshProp = meshExp.GetProperty<ObjectProperty>("SkeletalMesh");
                    HasGunMesh = meshProp != null;
                }

                if (classExport.InheritsFrom("SFXHeavyWeapon"))
                {
                    WeaponClassification = EWeaponClassification.HeavyWeapon;
                }
                else if (classExport.InheritsFrom("SFXWeapon_AssaultRifle"))
                {
                    WeaponClassification = EWeaponClassification.AssaultRifle;
                }
                else if (classExport.InheritsFrom("SFXWeapon_HeavyPistol"))
                {
                    WeaponClassification = EWeaponClassification.Pistol;
                }
                else if (classExport.InheritsFrom("SFXWeapon_AutoPistol"))
                {
                    WeaponClassification = EWeaponClassification.SMG;
                }
                else if (classExport.InheritsFrom("SFXWeapon_Shotgun"))
                {
                    WeaponClassification = EWeaponClassification.Shotgun;
                }
                else if (classExport.InheritsFrom("SFXWeapon_SniperRifle"))
                {
                    WeaponClassification = EWeaponClassification.SniperRifle;
                }
                else
                {
                    Debugger.Break();
                }

                var hasShaderCache = classExport.FileRef.FindExport("SeekFreeShaderCache") != null;
                RequiresStartupPackage = hasShaderCache && !classExport.FileRef.FilePath.Contains("Startup", StringComparison.InvariantCultureIgnoreCase);
                ImportOnly = hasShaderCache;
            }
        }

        public static List<GunInfo> GetAllowedWeaponsForLoadout(ExportEntry export)
        {
            List<GunInfo> guns = new();
            if (LoadoutSupportsVisibleMapping.TryGetValue(export.FullPath, out var supportsVisibleWeapons))
            {
                // We use FullPath instead of instanced as the loadout number may change across randomization
                if (supportsVisibleWeapons)
                {
                    guns.AddRange(VisibleAvailableWeapons);
                }
                else
                {
                    guns.AddRange(AllAvailableWeapons);
                }
            }
            else
            {
                // Only allow visible guns
                guns.AddRange(VisibleAvailableWeapons);
            }

            return guns;
        }

        public static IEntry PortWeaponIntoPackage(GameTarget target, IMEPackage targetPackage, GunInfo gunInfo)
        {
            IMEPackage sourcePackage;
            if (gunInfo.IsCorrectedPackage)
            {
                var sourceData = MEREmbedded.GetEmbeddedPackage(target.Game, "correctedloadouts.weapons." + gunInfo.PackageFileName);
                sourcePackage = MEPackageHandler.OpenMEPackageFromStream(sourceData);

                if (gunInfo.ImportOnly)
                {
                    // We need to install this file
                    var outfname = Path.Combine(MERFileSystem.DLCModCookedPath, gunInfo.PackageFileName);
                    if (!File.Exists(outfname))
                    {
                        sourcePackage.Save(outfname, true);
                        ThreadSafeDLCStartupPackage.AddStartupPackage(Path.GetFileNameWithoutExtension(gunInfo.PackageFileName));
                    }
                }
            }
            else
            {
                sourcePackage = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, gunInfo.PackageFileName));
            }

            if (sourcePackage != null)
            {
                var sourceExport = sourcePackage.GetUExport(gunInfo.SourceUIndex);

                if (!sourceExport.InheritsFrom("SFXWeapon") || sourceExport.IsDefaultObject)
                {
                    throw new Exception("Wrong setup!");
                }

                if (sourceExport.Parent != null && sourceExport.Parent.ClassName != "Package")
                {
                    throw new Exception("Cannot port weapon - parent object is not Package!");
                }

                // 1. Setup the link that will be used.
                //var newParent = EntryExporter.PortParents(sourceExport, targetPackage);
                var newParent = EntryExporter.PortParents(sourceExport, targetPackage, gunInfo.ImportOnly);

                void errorOccuredCB(string s)
                {
                    Debugger.Break();
                }

                IEntry newEntry = null;
                if (gunInfo.ImportOnly)
                {
                    Debug.WriteLine($"Gun ImportOnly in file {targetPackage.FilePath}");
                    if (gunInfo.RequiresStartupPackage)
                    {
                        ThreadSafeDLCStartupPackage.AddStartupPackage(Path.GetFileNameWithoutExtension(gunInfo.PackageFileName));
                    }

                    newEntry = PackageTools.CreateImportForClass(sourceExport, targetPackage, newParent);
                }
                else
                {
                    List<EntryStringPair> relinkResults = null;
                    if (gunInfo.IsCorrectedPackage || (PackageTools.IsPersistentPackage(gunInfo.PackageFileName) && MERFileSystem.GetPackageFile(target, gunInfo.PackageFileName.ToLocalizedFilename()) == null))
                    {
                        // Faster this way, without having to check imports
                        relinkResults = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExport, targetPackage,
                            newParent, true, new RelinkerOptionsPackage(), out newEntry); // TODO: CACHE?
                    }
                    else
                    {
                        // MEMORY SAFE (resolve imports to exports)
                        MERPackageCache cache = new MERPackageCache(target, MERCaches.GlobalCommonLookupCache, true);
                        relinkResults = EntryExporter.ExportExportToPackage(sourceExport, targetPackage, out newEntry, cache);
                    }

                    if (relinkResults.Any())
                    {
                        Debugger.Break();
                    }
                }

                return newEntry;
            }
            else
            {
                Debug.WriteLine($"Package for gun porting not found: {gunInfo.PackageFileName}");
            }
            return null; // No package was found
        }

        private enum EWRType
        {
            Invalid,
            Loadout,
            ApprBody,
            SetWeapon
        }

        /// <summary>
        /// When gun randomizer is active, we must also update weapon animations
        /// unfortunately due to the complexity there's no way this could be done on the fly
        /// so we're just gonna update all of them
        /// </summary>
        /// <param name="export"></param>
        /// <param name="wrtype"></param>
        /// <returns></returns>
        private static bool CanRandomize(ExportEntry export, out EWRType wrtype)
        {
            wrtype = EWRType.Invalid;
            if (export.IsDefaultObject) return false;
            if (export.ClassName == "Bio_Appr_Character_Body")
            {
                var fname = Path.GetFileName(export.FileRef.FilePath);
                if ((!fname.StartsWith("BioD") && !fname.StartsWith("BioP")) || fname == "BioP_Global.pcc")
                    return false; // Only modify design files
                wrtype = EWRType.ApprBody;
                return true;
            }
            else if (export.ClassName == "SFXLoadoutData"
                     //&& !export.ObjectName.Name.Contains("HeavyWeaponMech") // Not actually sure we can't randomize this one
                     && !export.ObjectName.Name.Contains("BOS_Reaper") // Don't randomize the final boss cause it'd really make him stupid
                     && export.GetProperty<ArrayProperty<ObjectProperty>>("Weapons") != null)
            {
                wrtype = EWRType.Loadout;
                return true;
            }
            else if (export.ClassName == "BioSeqAct_SetWeapon")
            {
                wrtype = EWRType.SetWeapon;
                return true;
            }

            return false;

        }

        internal static bool RandomizeExport(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            if (!CanRandomize(export, out var wrtype)) return false;
            if (wrtype == EWRType.Loadout)
                return RandomizeWeaponLoadout(target, export, option);
            else if (wrtype == EWRType.ApprBody)
                return InstallWeaponAnims(export, option);
            // This seems kind of pointless so we're not going to enable it
            //else if (wrtype == EWRType.SetWeapon)
            //    return SetWeaponSeqAct(export, option);
            return false;
        }

        private static bool SetWeaponSeqAct(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            if (ThreadSafeRandom.Next(1) == 0)
            {
                var cWeapon = export.GetProperty<ObjectProperty>("cWeapon");
                if (cWeapon != null && cWeapon.Value != 0)
                {
                    var randGun = VisibleAvailableWeapons.RandomElement();
                    Debug.WriteLine($"Changing SetWeapon from {cWeapon.ResolveToEntry(export.FileRef).ObjectName} to {randGun.GunName}");
                    cWeapon.Value = PortWeaponIntoPackage(target, export.FileRef, randGun).UIndex;
                    export.WriteProperty(cWeapon);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets up weapon animations for appearances that don't support it. In case the type receives a heavy weapon for example
        /// </summary>
        /// <param name="export"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        private static bool InstallWeaponAnims(ExportEntry export, RandomizationOption option)
        {
            var weaponAnimSpecs = export.GetProperty<ArrayProperty<StructProperty>>("WeaponAnimSpecs");
            if (weaponAnimSpecs != null && weaponAnimSpecs.Count == 12)
            {
                for (int i = 0; i < 12; i++)
                {
                    var localAnimSpecs = weaponAnimSpecs[i];
                    var masterAnimSpecs = WeaponAnimationsArrayProp[i];
                    var drawAnimSet = localAnimSpecs.GetProp<ObjectProperty>("m_drawAnimSet");
                    if (drawAnimSet.Value == 0)
                    {
                        // This set is not populated
                        var masterDrawAnimSpecExp = masterAnimSpecs.GetProp<ObjectProperty>("m_drawAnimSet").ResolveToEntry(WeaponAnimsPackage) as ExportEntry;
                        drawAnimSet.Value = ConvertAnimSetExportToImport(masterDrawAnimSpecExp, export.FileRef).UIndex;
                    }

                    var animSets = localAnimSpecs.GetProp<ArrayProperty<ObjectProperty>>("m_animSets");
                    var masterAnimSets = masterAnimSpecs.GetProp<ArrayProperty<ObjectProperty>>("m_animSets");
                    foreach (var mas in masterAnimSets.Where(x => x.Value != 0))
                    {
                        var masterEntry = mas.ResolveToEntry(WeaponAnimsPackage);
                        var newObj = ConvertAnimSetExportToImport(masterEntry, export.FileRef);
                        // Prevents duplicates. It seems some pawns don't have a draw but have animsets for the gun
                        if (animSets.All(x => x.Value != newObj.UIndex))
                        {
                            animSets.Add(new ObjectProperty(newObj));
                        }
                    }

                    //if (animSets.Count != masterAnimSets.Count)
                    //    Debugger.Break();

                }
                export.WriteProperty(weaponAnimSpecs);
                return true;
            }
            return false;
        }

        /*private static bool InstallGlobalWeaponAnims(RandomizationOption option)
        {
            var objsToPort = WeaponAnimsPackage.Exports.Where(x => x.ClassName == "AnimSet").ToList();
            //var persistentPackages = MERFileSystem.LoadedFiles.Keys.Where(x => x.StartsWith("BioP_")
            //                                                                   && x != "BioP_Global.pcc"
            //                                                                   && x != "BioP_Char.pcc"
            //                                                                   && x != "BioP_EndGm_StuntHench.pcc"
            //                                                                   && !x.Contains("_LOC_")).ToList();
            var persistentPackages = new List<string>(new[] { "BioP_Global.pcc" }); // I think this package is always loaded in SP in ME2 so we can probably get away with just using it... i hope
            Parallel.ForEach(persistentPackages, pp =>
            {
                MERLog.Information($"Installing persistent weapon animations into {pp}");
                var package = MEPackageHandler.OpenMEPackage(MERFileSystem.GetPackageFile(pp));
                var originalExportCount = package.ExportCount;
                // Install all weapon animations
                var newMemoryReferences = new List<ExportEntry>();
                foreach (var v in objsToPort)
                {
                    newMemoryReferences.Add(PackageTools.PortExportIntoPackage(package, v));
                }

                // Add world reference to force it to persist in memory
                var world = package.FindExport("TheWorld");
                var worldBin = ObjectBinary.From<World>(world);
                var extraRefs = worldBin.ExtraReferencedObjects.ToList();
                extraRefs.AddRange(newMemoryReferences.Select(x => new UIndex(x.UIndex)));
                worldBin.ExtraReferencedObjects = extraRefs.Distinct().ToArray(); // Filter out duplicates that may have already been in package
                world.WriteBinary(worldBin);

                MERFileSystem.SavePackage(package);
            });

            return true;
        }*/

        private static bool tried = false;


        private static bool RandomizeWeaponLoadout(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            // Check for blacklisted changes
            // HACK FOR NOW until we have better solution in place
            if (export.ObjectName.Name.Contains("HammerHead", StringComparison.InvariantCultureIgnoreCase))
            {
                return false; // Do not randomize hammerhead
            }

            var guns = export.GetProperty<ArrayProperty<ObjectProperty>>("Weapons");
            if (guns.Count == 1) //Randomizing multiple guns could be difficult and I'm not sure enemies ever change their weapons.
            {
                var gun = guns[0];
                if (gun.Value == 0) return false; // Null entry in weapons list

                // Set to class that will be randomized
                SharedLoadout.ConfigureLoadoutForRandomization(target, export);

                //var pName = Path.GetFileName(export.FileRef.FilePath);
                //var isPersistentPackage = PackageTools.IsPersistentPackage(pName);

                // Ensure unique loadout object
                //export = EntryCloner.CloneEntry(export); // Clone it to make it memory unique for randomization

                //var allowedGuns = GetAllowedWeaponsForLoadout(export);
                //if (allowedGuns.Any())
                //{
                //    var randomNewGun = allowedGuns.RandomElementByWeight(x => x.Weight);
                //    if (option.HasSliderOption && option.SliderValue >= 0)
                //    {
                //        randomNewGun = AllAvailableWeapons[(int)option.SliderValue];
                //    }
                //    //if (ThreadSafeRandom.Next(1) == 0)
                //    //{
                //    //    randomNewGun = allowedGuns.FirstOrDefault(x => x.GunName.Contains("GrenadeLauncher"));
                //    //}

                // var originalGun = gun.ResolveToEntry(export.FileRef);
                //    if (randomNewGun.GunName != originalGun.ObjectName)
                //    {
                //        var gunInfo = randomNewGun;
                //        MERLog.Information($@"Changing gun {export.ObjectName} => {randomNewGun.GunName}");
                //        // It's a different gun.

                //        // See if we need to port this in
                //        var fullName = gunInfo.PackageName + "." + randomNewGun.GunName;
                //        var repoint = export.FileRef.FindEntry(fullName);

                //        if (repoint != null)
                //        {
                //            // Gun does not need ported in
                //            gun.Value = repoint.UIndex;
                //        }
                //        else
                //        {
                //            // Gun needs ported in
                //            var newEntry = PortWeaponIntoPackage(target, export.FileRef, randomNewGun);
                //            gun.Value = newEntry.UIndex;
                //        }

                //        //if (!tried)
                //        export.WriteProperty(guns);

                // If this is not a master or localization file (which are often used for imports) 
                // Change the number around so it will work across packages.
                // May need disabled if game becomes unstable.

                // We check if less than 10 as it's very unlikely there will be more than 10 loadouts in a non-persistent package
                // if it's > 10 it's likely already a memory-changed item by MER
                //if (export.indexValue < 10 && !isPersistentPackage && !PackageTools.IsLocalizationPackage(pName))
                //{
                //    export.ObjectName = new NameReference(export.ObjectName, ThreadSafeRandom.Next(4000) + 10);
                //}
                //else if (isPersistentPackage && originalGun.UIndex > 0)
                //{
                //    // Make sure we add the original gun to the list of referenced memory objects
                //    // so subfiles that depend on this gun existing don't crash the game!
                //    var world = export.FileRef.FindExport("TheWorld");
                //    var worldBin = ObjectBinary.From<World>(world);
                //    var extraRefs = worldBin.ExtraReferencedObjects.ToList();
                //    extraRefs.Add(originalGun.UIndex);
                //    worldBin.ExtraReferencedObjects = extraRefs.Distinct().ToArray(); // Filter out duplicates that may have already been in package
                //    world.WriteBinary(worldBin);
                //}

                return true;
                //        tried = true;
                //    }
            }
            return false;
        }
    }
}
