﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Enemy
{
    public static class EnemyPowerChanger
    {
        /// <summary>
        /// Gets a property on an object, looking at archetype first, then up the class chain if its not defined locally.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="exp"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static T GetInstanceProperty<T>(this ExportEntry exp, string propertyName, PackageCache globalcache = null, PackageCache localcache = null) where T : Property
        {
            var prop = exp.GetProperty<T>(propertyName);
            if (prop != null) return prop;

            if (exp.Archetype is ExportEntry aExp)
            {
                prop = aExp.GetInstanceProperty<T>(propertyName, globalcache, localcache);
                if (prop != null) return prop;
            }
            else if (exp.Archetype is ImportEntry aImp && EntryImporter.TryResolveImport(aImp, out var resAExp, localcache, globalcache))
            {
                prop = resAExp.GetInstanceProperty<T>(propertyName, globalcache, localcache);
                if (prop != null) return prop;
            }


            if (exp.SuperClass is ExportEntry sExp)
            {
                prop = sExp.GetDefaults().GetInstanceProperty<T>(propertyName);
                if (prop != null) return prop;
            }
            else if (exp.SuperClass is ImportEntry scImp && EntryImporter.TryResolveImport(scImp, out var resSCExp, localcache, globalcache))
            {
                prop = resSCExp.GetInstanceProperty<T>(propertyName, globalcache, localcache);
                if (prop != null) return prop;
            }

            return null; // Could not find instanced property
        }

        public const string SUBOPTIONKEY_ENEMYPOWERS_FORCERANDOMIZER = "SUBOPTIONKEY_ENEMYPOWERS_FORCERANDOMIZER";
        public const string SUBOPTIONKEY_ENEMYPOWERS_ENFORCEMINIMUM = "SUBOPTIONKEY_ENEMYPOWERS_ENFORCEMINIMUM";
        public const string SUBOPTIONKEY_ENEMYPOWERS_ONETIMERANDOMIZE = "SUBOPTIONKEY_ENEMYPOWERS_ONETIMERANDOMIZE";

#if LEGACY
        // ME2R - we have to put this into class changer code for LE2R
        private static string[] PowersToNotSwap = new[]
        {
            // Collector powers, used by it's AI
            // Patched via startup
            //"SFXPower_CollectorWarp", //Used by Combat_Collector_Possessed
            //"SFXPower_Singularity_NPC", // Used by Combat_Collector_Possessed
            //"SFXPower_Collector_Pulse", // Used by Combat_Collector_Possessed


            "SFXPower_HuskMelee_Right", //Used by SwipeAttack() in SFXAI_Husk
            "SFXPower_HuskMelee_Left",

            "SFXPower_BioticChargeLong_NPC", // Used by the asari in LOTSB
            "SFXPower_Shockwave_NPC", //Used by the asari in LOTSB

            "SFXPower_Geth_Supercharge", // Used by SFXAI_GethTrooper Combat_Geth_Berserk
            "SFXPower_KroganCharge", // Krogan charge, used by it's AI
            "SFXPower_CombatDrone_Death", // Used by combat drone

            "SFXPower_PraetorianDeathChoir", // Used by Praetorian, otherwise softlocks on HorCR1

            // Vasir in LOTSB
            "SFXPower_BioticCharge_NPC",
            "SFXPower_BioticChargeLong_NPC",
            "SFXPower_BioticChargeLong_AsariSpectre",
        };
#endif

        internal static void PatchUsePowerOn(GameTarget target, RandomizationOption option)
        {
            // UsePowerOn uses names. We update these ones to use a random power instead since with power randomizer on they will be unlikely to work
            var patches = new[]
            {
                // Gunship changes don't seem to work
                ("BioD_Exp1Lvl1_400Office.pcc", "TheWorld.PersistentLevel.Main_Sequence.Baria_Frontiers.Combat.Upstairs.SFXSeqAct_UsePowerOn_1"), // FlashBang, Vasir Fight
                //("BioD_OmgGrA_203Wave3.pcc","TheWorld.PersistentLevel.Main_Sequence.Gunship.Gunship_Matinees.Rocket_Strafe_Front_Window_0.SFXSeqAct_UsePowerOn_1"), // Garrus gunship - fire at player
                //("BioD_OmgGrA_203Wave3.pcc","TheWorld.PersistentLevel.Main_Sequence.Gunship.Gunship_Matinees.Rocket_Strafe_Front_Window_0.SFXSeqAct_UsePowerOn_0"), // Garrus gunship - fire at target
                //("BioD_OmgGrA_203Wave3.pcc","TheWorld.PersistentLevel.Main_Sequence.Gunship.Gunship_Matinees.Rocket_Strafe_Side_Window.SFXSeqAct_UsePowerOn_1"), // Garrus gunship - fire at target
                //("BioD_OmgGrA_203Wave3.pcc","TheWorld.PersistentLevel.Main_Sequence.Gunship.Gunship_Matinees.Rocket_Strafe_Side_Window.SFXSeqAct_UsePowerOn_0"), // Garrus gunship - fire at target
                ("BioD_ProCer_200Catwalk.pcc","TheWorld.PersistentLevel.Main_Sequence.Ambient.SFXSeqAct_UsePowerOn_1"), // Shoot rocket at person
                //("BioD_PtyMtL_410Gunship.pcc", "TheWorld.PersistentLevel.Main_Sequence.Combat.Weapon_Control.SFXSeqAct_UsePowerOn_2"), // Kasumi Gunship
                //("BioD_PtyMtL_410Gunship.pcc", "TheWorld.PersistentLevel.Main_Sequence.Combat.Weapon_Control.SFXSeqAct_UsePowerOn_3"), // Kasumi Gunship
                ("BioD_SunTlA_205Colossus.pcc", "TheWorld.PersistentLevel.Main_Sequence.Combat.Colossus_Cover_System.Colossus_scripted_attack.SFXSeqAct_UsePowerOn_1"), // Fire at player, Tali mission
                ("BioD_SunTlA_205Colossus.pcc", "TheWorld.PersistentLevel.Main_Sequence.Combat.Colossus_Cover_System.Colossus_scripted_attack.SFXSeqAct_UsePowerOn_0"), // Fire at Reegar, Tali mission
                ("BioD_TwrMwA_201HangarAFight1.pcc", "TheWorld.PersistentLevel.Main_Sequence.Combat.SFXSeqAct_UsePowerOn_0"), // Warp NPC on interpactor?
                //("BioD_TwrMwA_301HangarBFight.pcc", null) // Patch everything in here
            };

            MERPackageCache cache = new MERPackageCache(target, null, false);
            foreach (var p in patches)
            {
                var package = cache.GetCachedPackage(p.Item1);
                var merSeqActUseRandPower = EntryImporter.EnsureClassIsInFile(package, "MERSeqAct_UsePowerOn", new RelinkerOptionsPackage(), gamePathOverride: target.TargetPath);
                if (p.Item2 != null)
                {
                    var exp = package.FindExport(p.Item2);
                    exp.Class = merSeqActUseRandPower;
                    exp.ObjectName = new NameReference("MERSeqAct_UsePowerOn", exp.ObjectName.Number);
                }
                else
                {
                    foreach (var exp in package.Exports.Where(x => !x.IsDefaultObject && x.ClassName == "SFXSeqAct_UsePowerOn"))
                    {
                        exp.Class = merSeqActUseRandPower;
                        exp.ObjectName = new NameReference("MERSeqAct_UsePowerOn", exp.ObjectName.Number);
                    }
                }
            }

            foreach (var package in cache.GetPackages())
            {
                MERFileSystem.SavePackage(package);
            }
            cache.Dispose();
        }

        internal class PowerInfo2
        {
            public string PowerIFP;
            public string BasePowerName;
            public string CapabilityType;

            public string ToConfigValue()
            {
                Dictionary<string, string> dict = new Dictionary<string, string>();
                dict[@"PowerIFP"] = PowerIFP;
                if (BasePowerName != null)
                {
                    dict[@"BasePowerName"] = BasePowerName;
                }
                dict[@"CapabilityType"] = CapabilityType;

                return StringStructParser.BuildCommaSeparatedSplitValueList(dict, @"PowerIFP", @"BasePowerName");
            }
        }

        private static string GetBaseName(ExportEntry exp, MERPackageCache cache)
        {
            var baseName = exp.GetInstanceProperty<NameProperty>("BaseName");
            if (baseName != null)
            {
                return baseName.Value.ToString();
            }

            return exp.GetInstanceProperty<NameProperty>("PowerName")?.Value.ToString() ?? null;
        }

        public static bool InitLE2R(GameTarget target, RandomizationOption option)
        {
            MERFileSystem.InstallAlways("PowerBank");
            MERFileSystem.SavePackage(SFXGame.CreateAndSetupMERLoadoutClass(target));
            SharedLE2Fixes.InstallPowerUsageFixes(); // Fixes collector ai

            using var pb = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, "MERPowersBank.pcc"));

            var bioWeapon = CoalescedHandler.GetIniFile("BioWeapon.ini");
            var section = bioWeapon.GetOrAddSection("SFXGame.SFXLoadoutDataMER");

            var ePowersList = PackageTools.GetExportList(pb, "EnemyPowersReferencer");
            foreach (var powExp in ePowersList)
            {
                var powInfo = new PowerInfo2()
                {
                    PowerIFP = powExp.InstancedFullPath,
                    BasePowerName = GetBaseName(powExp.GetDefaults(), MERCaches.GlobalCommonLookupCache),
                    CapabilityType = powExp.GetDefaults().GetInstanceProperty<EnumProperty>("CapabilityType")?.Value ?? "BioCaps_AllTypes"
                };

                CoalescedHandler.AddDynamicLoadMappingEntry(new SeekFreeInfo(powExp));
                section.AddEntry(new CoalesceProperty("RandomPowerOptions", new CoalesceValue(powInfo.ToConfigValue(), CoalesceParseAction.AddUnique)));
            }

            // Set runtime feature flags
            CoalescedHandler.EnableFeatureFlag("bEnemyPowerRandomizer");
            CoalescedHandler.EnableFeatureFlag("bEnemyPowerRandomizer_Force", option.HasSubOptionSelected(SUBOPTIONKEY_ENEMYPOWERS_FORCERANDOMIZER));
            CoalescedHandler.EnableFeatureFlag("bEnemyPowerRandomizer_EnforceMinPowerCount", option.HasSubOptionSelected(SUBOPTIONKEY_ENEMYPOWERS_ENFORCEMINIMUM));
            CoalescedHandler.EnableFeatureFlag("bEnemyPowerRandomizer_OneTime", option.HasSubOptionSelected(SUBOPTIONKEY_ENEMYPOWERS_ONETIMERANDOMIZE));

            // Needs more work
            // PatchUsePowerOn(target, option);

            return true;
        }


        // This can probably be changed later
        private static bool CanRandomize2(ExportEntry export) => !export.IsDefaultObject && export.ClassName == "SFXLoadoutData"
            && !export.ObjectName.Name.Contains("Drone") // We don't modify drone powers
            && !export.ObjectName.Name.Contains("NonCombat") // Non combat enemies won't use powers so this is just a waste of time
            && export.ObjectName.Name != "Loadout_None" // Loadout_None has nothing, don't bother giving it anything
            && Path.GetFileName(export.FileRef.FilePath).StartsWith("Bio"); // Must be part of a level, not a squadmate, player, etc

        internal static bool RandomizeExport2(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            if (!CanRandomize2(export)) return false;
#if DEBUG
            //if (!export.ObjectName.Name.Contains("HeavyWeaponMech"))
            //    return false;
#endif

            // Set to class that will be randomized
            SharedLoadout.ConfigureLoadoutForRandomization(target, export);
            return true;
        }

#if FALSE
        #region OLD ME2R code

        /// <summary>
        /// List of loadouts that have all their powers locked for randomization due to their AI. Add more powers so their AI behaves differently.
        /// </summary>
        public static string[] LoadoutsToAddPowersTo = new[]
        {
            "SUB_COL_Possessed",
        };

        public static List<PowerInfo> Powers;

        /// <summary>
        /// Bank package that contains all powers, this suppresses a lot of I/O and memory allocations and uses fixed size memory
        /// </summary>
        private static IMEPackage PowerBank;

        public static void LoadPowers(GameTarget target)
        {
            if (Powers == null)
            {
                // Load the power bank
                PowerBank = MEPackageHandler.OpenMEPackageFromStream(
                    MEREmbedded.GetEmbeddedPackage(MEGame.LE2, @"Powers.EnemyPowersBank.pcc"), @"EnemyPowersBank.pcc");
                string fileContents = MEREmbedded.GetEmbeddedTextAsset("powerlistle2.txt");
                var whitelistedPowers = fileContents.Split(
                    new string[] { "\r\n", "\r", "\n" },
                    StringSplitOptions.None
                ).ToList();

                // Inventory the powerlist for use
                Powers = new List<PowerInfo>();
                foreach (var exp in whitelistedPowers)
                {
                    var pExp = PowerBank.FindExport(exp);
                    if (pExp != null)
                    {
                        Powers.Add(new PowerInfo(pExp, false));
                    }
                    else
                    {
                        Debug.WriteLine($"POWER ExP NOT FOUND: {exp}");
                    }
                }
            }
        }

        /// <summary>
        /// Hack to force power lists to load with only a single check
        /// </summary>
        /// <param name="option"></param>
        /// <returns></returns>
        public static bool Init(GameTarget target, RandomizationOption option)
        {
            MERLog.Information(@"Preloading power data");
            LoadPowers(target);
            return true;
        }
        public class PowerInfo
        {
            /// <summary>
            ///  Name of the power
            /// </summary>
            [JsonProperty("powername")]
            public string PowerName { get; set; }

            [JsonProperty("instancedfullpath")]
            public string InstancedFullPath { get; set; }

            [JsonIgnore]
            public ExportEntry PowerExport { get; set; }

            [JsonProperty("type")]
            internal EPowerCapabilityType Type { get; set; }

            /// <summary>
            /// Maps the power type to the power type enum for categorization
            /// </summary>
            /// <param name="classExport"></param>
            /// <returns></returns>
            private bool MapPowerType()
            {
                var uClass = ObjectBinary.From<UClass>(PowerExport);
                var defaults = PowerExport.FileRef.GetUExport(uClass.Defaults);
                var bct = defaults.GetProperty<EnumProperty>("CapabilityType");
                while (bct == null)
                {
                    defaults = defaults.Archetype as ExportEntry;
                    if (defaults == null)
                        return false; // We cannot look up the capability type
                    bct = defaults.GetProperty<EnumProperty>("CapabilityType"); // Look in archetype (superclass of defaults)
                }
                switch (bct.Value.Name)
                {
                    case "BioCaps_AllTypes":
                        Debugger.Break();
                        //Type = ;
                        return true;
                    case "BioCaps_SingleTargetAttack":
                        Type = EPowerCapabilityType.Attack;
                        return true;
                    case "BioCaps_AreaAttack":
                        Type = EPowerCapabilityType.Attack;
                        return true;
                    case "BioCaps_Disable":
                        Type = EPowerCapabilityType.Debuff;
                        return true;
                    case "BioCaps_Debuff":
                        Type = EPowerCapabilityType.Debuff;
                        return true;
                    case "BioCaps_Defense":
                        Type = EPowerCapabilityType.Defense;
                        return true;
                    case "BioCaps_Heal":
                        Type = EPowerCapabilityType.Heal;
                        return true;
                    case "BioCaps_Buff":
                        Type = EPowerCapabilityType.Buff;
                        return true;
                    case "BioCaps_Suicide":
                        Type = EPowerCapabilityType.Suicide;
                        return true;
                    case "BioCaps_Death":
                        Type = EPowerCapabilityType.Death;
                        return true;
                    default:
                        Debugger.Break();
                        return true;
                }
            }

            private static bool IsWhitelistedPower(ExportEntry export)
            {
                return IsWhitelistedPower(export.ObjectName);
            }

            private static bool IsWhitelistedPower(string powername)
            {
                if (powername == "SFXPower_Flashbang_NPC") return true;
                if (powername == "SFXPower_ZaeedUnique_Player") return true;
                //if (powername == "SFXPower_StasisNew") return true; //Doesn't work on player and player squad. It's otherwise identical to other powers so no real point, plus it has lots of embedded pawns
                return false;
            }

            public PowerInfo() { }
            public PowerInfo(ExportEntry export, bool isCorrectedPackage)
            {
                PowerName = export.ObjectName;
                PowerExport = export;
                InstancedFullPath = export.InstancedFullPath;
                // PackageFileName = Path.GetFileName(export.FileRef.FilePath);

                if (!MapPowerType() && !IsWhitelistedPower(export))
                {
                    // Whitelisted powers bypass this check
                    // Powers that do not list a capability type are subclasses. We will not support using these
                    IsUsable = false;
                    return;
                }
                if (!IsWhitelistedPower(PowerName) &&
                    // Forced blacklist after whitelist
                    (
                        PowerName.Contains("Ammo")
                        || PowerName.Contains("Base")
                        || PowerName.Contains("FirstAid")
                        || PowerName.Contains("Player")
                        || PowerName.Contains("GunshipRocket")
                        || (PowerName.Contains("NPC") && PowerName != "SFXPower_CombatDrone_NPC") // this technically should be used, but too lazy to write algo to figure it out
                        || PowerName.Contains("Player")
                        || PowerName.Contains("Zaeed") // Only use player version. The normal one doesn't throw the grenade
                        || PowerName.Contains("HuskTesla")
                        || PowerName.Contains("Kasumi") // Depends on her AI
                        || PowerName.Contains("CombatDroneDeath") // Crashes the game
                        || PowerName.Contains("DeathChoir") // Buggy on non-praetorian, maybe crashes game?
                        || PowerName.Contains("Varren") // Don't use this
                        || PowerName.Contains("Lift_TwrMwA") // Not sure what this does, but culling itCrashes the game, maybe
                        || PowerName.Contains("Crush") // Don't let enemies use this, it won't do anything useful for the most part
                        || PowerName == "SFXPower_MechDog" // dunno what this is
                        || PowerName == "SFXPower_CombatDroneAttack" // Combat drone only
                        || PowerName == "SFXPower_HeavyMechExplosion" // This is not actually used and doesn't seem to work but is on some pawns
                        || PowerName == "SFXPower_CombatDrone" // Player version is way too OP. Enforce NPC version
                        || PowerName.Contains("Dominate") // This is pointless against player squad // But is it REALLY?
                    )
                    )
                {
                    IsUsable = false;
                    return; // Do not use ammo or base powers as they're player only in the usable code
                }


                IsCorrectedPackage = isCorrectedPackage;
                SetupDependencies(export);
            }

            private void SetupDependencies(ExportEntry export)
            {
                // Not necessary in LE
                //switch (export.ObjectName)
                //{
                //    // Check for 01's as basegame has stub 00 versions
                //    case "SFXPower_Flashbang_NPC":
                //        FileDependency = "BioH_Thief_01.pcc"; // Test for Kasumi DLC
                //        break;
                //    case "SFXPower_ZaeedUnique_Player":
                //        FileDependency = "BioH_Veteran_01.pcc"; // Test for Kasumi DLC
                //        break;
                //}
            }

            /// <summary>
            /// If this power file is stored in the executable as it required manual corrections to work
            /// </summary>
            [JsonProperty("iscorrectedpackage")]
            public bool IsCorrectedPackage { get; set; }

            [JsonIgnore]
            public bool IsUsable { get; set; } = true;

            /// <summary>
            /// If this power can be used as an import (it's in a startup file)
            /// </summary>
            [JsonProperty("importonly")]
            public bool ImportOnly { get; set; }

            /// <summary>
            /// If this power requires the package that contains it to be added to the startup list. Used for DLC powers
            /// </summary>
            //[JsonProperty("requiresstartuppackage")]
            //public bool RequiresStartupPackage { get; set; }

            /// <summary>
            /// A list of additional related powers that are required for this power to work, for example Shadow Strike requires teleport and assasination abilities. Full asset paths to power class
            /// </summary>
            [JsonProperty("additionalrequiredpowers")]
            public string[] AdditionalRequiredPowers { get; set; } = new string[] { };
        }

        internal enum EPowerCapabilityType
        {
            Attack,
            Disable,
            Debuff,
            Defense,
            Heal,
            Buff,
            Suicide,
            Death
        }

        /// <summary>
        /// Ports a power into a package
        /// </summary>
        /// <param name="targetPackage"></param>
        /// <param name="powerInfo"></param>
        /// <param name="additionalPowers">A list of additioanl powers that are referenced when this powerinfo is an import only power (prevent re-opening package)</param>
        /// <returns></returns>
        public static IEntry PortPowerIntoPackage(GameTarget target, IMEPackage targetPackage, PowerInfo powerInfo)
        {
            //if (powerInfo.IsCorrectedPackage)
            //{
            //    var sourceData = MEREmbedded.GetEmbeddedPackage(target.Game, "correctedloadouts.powers." + powerInfo.PackageFileName);
            //    sourcePackage = MEPackageHandler.OpenMEPackageFromStream(sourceData);
            //}
            //else
            //{
            //    sourcePackage = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, powerInfo.PackageFileName));
            //}

            //if (sourcePackage != null)
            //{
            var sourceExport = powerInfo.PowerExport;
            if (!sourceExport.InheritsFrom("SFXPower") || sourceExport.IsDefaultObject)
            {
                throw new Exception("Wrong setup!");
            }
            if (sourceExport.Parent != null && sourceExport.Parent.ClassName != "Package")
            {
                throw new Exception("Cannot port power - parent object is not Package!");
            }

            var newParent = EntryExporter.PortParents(sourceExport, targetPackage);
            IEntry newEntry;
#if DEBUG
            // DEBUG ONLY-----------------------------------
            //var defaults = sourceExport.GetDefaults();
            //defaults.RemoveProperty("VFX");
            //var vfx = defaults.GetProperty<ObjectProperty>("VFX").ResolveToEntry(sourcePackage) as ExportEntry;
            //vxx.RemoveProperty("PlayerCrust");
            //vfx.FileRef.GetUExport(1544).RemoveProperty("oPrefab");

            ////vfx = defaults.FileRef.GetUExport(6211); // Prefab
            ////vfx.RemoveProperty("WorldImpactVisualEffect");
            //MERPackageCache cached = new MERPackageCache();
            //EntryExporter.ExportExportToPackage(vfx, targetPackage, out newEntry, cached);
            //PackageTools.AddReferencesToWorld(targetPackage, new [] {newEntry as ExportEntry});

            //return null;


            // END DEBUG ONLY--------------------------------
#endif
            List<EntryStringPair> relinkResults = null;
            //if ((powerInfo.IsCorrectedPackage || (PackageTools.IsPersistentPackage(powerInfo.PackageFileName) && MERFileSystem.GetPackageFile(target, powerInfo.PackageFileName.ToLocalizedFilename()) == null)))
            //{
            // Faster this way, without having to check imports
            relinkResults = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExport, targetPackage,
                newParent, true, new RelinkerOptionsPackage(), out newEntry); // TODO: CACHE?
                                                                              //}
                                                                              //else
                                                                              //{
                                                                              //    // MEMORY SAFE (resolve imports to exports)
                                                                              //    MERPackageCache cache = new MERPackageCache(target, MERCaches.GlobalCommonLookupCache, true);
                                                                              //    relinkResults = EntryExporter.ExportExportToPackage(sourceExport, targetPackage, out newEntry, cache);
                                                                              //}

            if (relinkResults.Any())
            {
                Debugger.Break();
            }

            return newEntry;
            //}
            //return null; // No package was found
        }

        // This can probably be changed later
        private static bool CanRandomize(ExportEntry export) => !export.IsDefaultObject && export.ClassName == "SFXLoadoutData"
                                                                                        && !export.ObjectName.Name.Contains("Drone") // We don't modify drone powers
                                                                                        && !export.ObjectName.Name.Contains("NonCombat") // Non combat enemies won't use powers so this is just a waste of time
                                                                                        && export.ObjectName.Name != "Loadout_None" // Loadout_None has nothing, don't bother giving it anything
                                                                && Path.GetFileName(export.FileRef.FilePath).StartsWith("Bio"); // Must be part of a level, not a squadmate, player, etc

        internal static bool RandomizeExport(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            if (!CanRandomize(export)) return false;
#if DEBUG
            //if (!export.ObjectName.Name.Contains("HeavyWeaponMech"))
            //    return false;
#endif

            // Set to class that will be randomized
            SharedLoadout.ConfigureLoadoutForRandomization(target, export);
            return true;

            // old ME2R compile time randomizer

            var powers = export.GetProperty<ArrayProperty<ObjectProperty>>("Powers");

            if (powers == null)
            {
                // This loadout has no powers!
                // Randomly give them some powers.
                if (ThreadSafeRandom.Next(1) == 0) // Be sure to change this for release build, as this is always true
                {
                    // unlimited power
                    List<ObjectProperty> blankPows = new List<ObjectProperty>();
                    // Add two blanks. We'll strip blanks before writing it
                    blankPows.Add(new ObjectProperty(int.MinValue));
                    blankPows.Add(new ObjectProperty(int.MinValue));
                    powers = new ArrayProperty<ObjectProperty>(blankPows, "Powers");
                }
                else
                {
                    // Sorry mate no powers for you
                    return false;
                }
            }

            var originalPowerUIndexes = powers.Where(x => x.Value > 0).Select(x => x.Value).ToList();

            foreach (var power in powers.ToList())
            {
                if (power.Value == 0) return false; // Null entry in weapons list


                // We must first perform a bunch of checks to make sure this power is OK to randomize
                // as a lot of AI depends on specific powers; we will not change those ones
                #region CHECK FOR UNRANDOMIZABLE POWERS ON LOADOUT
                IEntry existingPowerEntry = null;
                if (power.Value != int.MinValue) // MinValue is MER's version of a 'blank' since we cannot use 0
                {
                    // Husk AI kinda depends on melee or they just kinda breath on you all creepy like
                    // We'll give them a chance to change it up though
                    existingPowerEntry = power.ResolveToEntry(export.FileRef);
                    if (existingPowerEntry.ObjectName.Name.Contains("Melee", StringComparison.InvariantCultureIgnoreCase) && ThreadSafeRandom.Next(2) == 0)
                    {
                        MERLog.Information($"Not changing melee power {existingPowerEntry.ObjectName.Name}");
                        continue; // Don't randomize power
                    }
                    if (PowersToNotSwap.Contains(existingPowerEntry.ObjectName.Name))
                    {
                        MERLog.Information($"Not changing power {existingPowerEntry.ObjectName.Name}");
                        continue; // Do not change this power
                    }
                }

                // DEBUG
                PowerInfo randomNewPower = Powers.RandomElement();
                //if (option.SliderValue < 0) // This lets us select a specific power
                //{
                //    randomNewPower = Powers.RandomElement();
                //}
                //else
                //{
                //    randomNewPower = Powers[(int)option.SliderValue];
                //}


                // Prevent krogan from getting a death power
                while (export.ObjectName.Name.Contains("Krogan", StringComparison.InvariantCultureIgnoreCase) && randomNewPower.Type == EPowerCapabilityType.Death)
                {
                    MERLog.Information(@"Re-roll no-death-power on krogan");
                    // Reroll. Krogan AI has something weird about it
                    randomNewPower = Powers.RandomElement();
                }

                // Prevent powerful enemies from getting super stat boosters
                while (randomNewPower.Type == EPowerCapabilityType.Buff && (
                        export.ObjectName.Name.Contains("Praetorian", StringComparison.InvariantCultureIgnoreCase)
                        || export.ObjectName.Name.Contains("ShadowBroker", StringComparison.InvariantCultureIgnoreCase)))
                {
                    MERLog.Information(@"Re-roll no-buffs for powerful enemy");
                    randomNewPower = Powers.RandomElement();
                }

                #region YMIR MECH fixes
                if (export.ObjectName.Name.Contains("HeavyWeaponMech"))
                {
                    // Heavy weapon mech chooses named death powers so we cannot change these
                    // HeavyMechDeathExplosion is checked for existence. NormalExplosion for some reason isn't
                    if ((existingPowerEntry.ObjectName.Name == "SFXPower_HeavyMechNormalExplosion"))
                    {
                        MERLog.Information($@"YMIR mech power HeavyMechNormalExplosion cannot be randomized, skipping");
                        continue;
                    }

                    // Do not add buff powers to YMIR
                    while (randomNewPower.Type == EPowerCapabilityType.Buff)
                    {
                        MERLog.Information($@"Re-roll YMIR mech power to prevent potential enemy too difficult to kill softlock. Incompatible power: {randomNewPower.PowerName}");
                        randomNewPower = Powers.RandomElement();
                    }
                }
                #endregion
                #endregion

                // CHANGE THE POWER
                if (existingPowerEntry == null || randomNewPower.PowerName != existingPowerEntry.ObjectName)
                {
                    if (powers.Any(x => power.Value != int.MinValue && power.ResolveToEntry(export.FileRef).ObjectName == randomNewPower.PowerName))
                        continue; // Duplicate powers crash the game. It seems this code is not bulletproof here and needs changed a bit...


                    MERLog.Information($@"Changing power {export.ObjectName} {existingPowerEntry?.ObjectName ?? "(+New Power)"} => {randomNewPower.PowerName}");
                    // It's a different power.

                    // See if we need to port this in
                    var existingVersionOfPower = export.FileRef.FindEntry(randomNewPower.InstancedFullPath);

                    if (existingVersionOfPower != null)
                    {
                        // Power does not need ported in, already in package
                        power.Value = existingVersionOfPower.UIndex;
                    }
                    else
                    {
                        // Power needs ported in
                        power.Value = PortPowerIntoPackage(target, export.FileRef, randomNewPower)?.UIndex ?? int.MinValue;
                    }

                    if (existingPowerEntry != null && existingPowerEntry.UIndex > 0 && PackageTools.IsPersistentPackage(export.FileRef.FilePath))
                    {
                        // Make sure we add the original power to the list of referenced memory objects
                        // so subfiles that depend on this power existing don't crash the game!
                        PackageTools.AddReferencesToWorld(export.FileRef, new[] { existingPowerEntry as ExportEntry });
                    }

                    foreach (var addlPow in randomNewPower.AdditionalRequiredPowers)
                    {
                        var existingPow = export.FileRef.FindEntry(addlPow);
                        //if (existingPow == null && randomNewPower.ImportOnly && sourcePackage != null)
                        //{
                        //    existingPow = PackageTools.CreateImportForClass(sourcePackage.FindExport(randomNewPower.PackageName + "." + randomNewPower.PowerName), export.FileRef);
                        //}

                        if (existingPow == null)
                        {
                            Debugger.Break();
                        }
                        powers.Add(new ObjectProperty(existingPow));
                    }
                }
            }

            // Strip any blank powers we might have added, remove any duplicates
            powers.RemoveAll(x => x.Value == int.MinValue);
            powers.ReplaceAll(powers.ToList().Distinct()); //tolist prevents concurrent modification in nested linq

            // DEBUG
#if DEBUG
            var duplicates = powers
                .GroupBy(i => i.Value)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key).ToList();
            if (duplicates.Any())
            {
                foreach (var dup in duplicates)
                {
                    Debug.WriteLine($"DUPLICATE POWER IN LOADOUT {export.FileRef.GetEntry(dup).ObjectName}");
                }
                Debugger.Break();
            }
#endif
            export.WriteProperty(powers);

            // Our precalculated map should have accounted for imports already, so we don't need to worry about missing imports upstream
            // If this is not a master or localization file (which are often used for imports) 
            // Change the number around so it will work across packages.
            // May need disabled if game becomes unstable.

            // We check if less than 10 as it's very unlikely there will be more than 10 loadouts in a non-persistent package
            // if it's > 10 it's likely already a memory-changed item by MER
            var pName = Path.GetFileName(export.FileRef.FilePath);
            if (export.indexValue < 10 && !PackageTools.IsPersistentPackage(pName) && !PackageTools.IsLocalizationPackage(pName))
            {
                export.ObjectName = new NameReference(export.ObjectName, ThreadSafeRandom.Next(2000));
            }

            if (originalPowerUIndexes.Any())
            {
                // We should ensure the original objects are still referenced so shared objects they have (vfx?) are kept in memory
                // Dunno if this actually fixes the problems...
                PackageTools.AddReferencesToWorld(export.FileRef, originalPowerUIndexes.Select(x => export.FileRef.GetUExport(x)));
            }

            return true;
        }

#endregion
#endif
    }

    internal static class SharedLoadout
    {
        public static void ConfigureLoadoutForRandomization(GameTarget target, ExportEntry export)
        {
            IEntry classRef = export.FileRef.FindImport("SFXGame.SFXLoadoutDataMER");
            if (classRef == null)
            {
                classRef = EntryImporter.EnsureClassIsInFile(export.FileRef, "SFXLoadoutDataMER", new RelinkerOptionsPackage(), target.TargetPath);
            }

            export.Class = classRef;

            // Some pawns should not be randomized
            var objName = export.ObjectName.Name;
            if (objName.Contains("RedHusk", StringComparison.InvariantCultureIgnoreCase) // Husks don't have AI to use powers
                || objName.Contains("omination", StringComparison.InvariantCultureIgnoreCase)) // LE2R abominations (suicide + cover jumping) - YES ITS omination, not abomination, as other loadouts are named bomination
            {
                export.WriteProperty(new BoolProperty(true, "bPreventPowerRandomization"));
                export.WriteProperty(new BoolProperty(true, "bPreventWeaponRandomization"));
                export.WriteProperty(new BoolProperty(true, "bIgnorePowerOverride")); // We do not allow MER override flags to work for these
                export.WriteProperty(new BoolProperty(true, "bIgnoreWeaponOverride")); // We do not allow MER override flags to work for these
                return;
            }

            // LE2R specific enemies
            if (objName.Contains("Kaidan", StringComparison.InvariantCultureIgnoreCase)
                || objName.Contains("Ashley", StringComparison.InvariantCultureIgnoreCase))
            {
                export.WriteProperty(new BoolProperty(true, "bPreventPowerRandomization"));
                export.WriteProperty(new BoolProperty(true, "bPreventWeaponRandomization"));
                // You can override these but must opt in to do so
                return;
            }
        }
    }
}

