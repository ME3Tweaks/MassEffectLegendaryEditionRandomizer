﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Enemy
{
    public enum EPortablePawnClassification
    {
        Mook,
        Subboss,
        Boss
    }

    public class PortablePawn
    {
        /// <summary>
        /// The filename that contains this pawn
        /// </summary>
        public string PackageFilename { get; set; }
        /// <summary>
        /// The full path to the BioChallengeScaledPawnType object
        /// </summary>
        public string BioPawnTypeIFP { get; set; }
        /// <summary>
        /// The asset to port in. Sometimes you don't wnat the BioChallengeScaledPawnType as it won't include things like models (for example, SFXPawn_Garm)
        /// </summary>
        public string AssetToPortIn { get; set; }
        /// <summary>
        /// Assets for ActorFactory
        /// </summary>
        public string[] AssetPaths { get; set; }
        /// <summary>
        /// If this is a corrected package (stored internal to executable)
        /// </summary>
        public bool IsCorrectedPackage { get; set; }
        /// <summary>
        /// Full path to the class of this pawn
        /// </summary>
        public string PawnClassPath { get; internal set; }
        /// <summary>
        /// How strong this pawn is
        /// </summary>
        public EPortablePawnClassification Classification { get; set; }
        ///// <summary>
        ///// List of textures that should be installed when this pawn is ported in. Key is the full asset path, value is the texture to install
        ///// </summary>
        //public RTexture2D[] TextureUpdates { get; set; }
    }
    public class PawnPorting
    {
        public static PortablePawn GetPortablePawn(string pawnTypeIFP)
        {
            return PortablePawns.FirstOrDefault(x => x.BioPawnTypeIFP == pawnTypeIFP);
        }

        public static PortablePawn[] PortablePawns = new[]
        {
            // Bombinatiton
            new PortablePawn()
            {
                PackageFilename = "SFXPawn_Bombination3.pcc",
                BioPawnTypeIFP = "MERChar_EndGm2.Bomination",
                AssetToPortIn = "MERChar_EndGm2.Bomination",
                AssetPaths = new[] {
                    "BIOG_ZMB_ARM_NKD_R.NKDd.ZMB_ARM_NKDd_MDL",
                    "BIOG_ZMB_ARM_NKD_R.NKDd.ZMB_ARM_NKDd_MAT_1a"
                },
                PawnClassPath = "MERGamePawns.SFXPawn_Bombination",
                Classification = EPortablePawnClassification.Mook,
                IsCorrectedPackage = true
            },
            // Bombinatiton - Suicide version (faster cast on the power to fix flying bug)
            new PortablePawn()
            {
                PackageFilename = "SFXPawn_Bombination3.pcc",
                BioPawnTypeIFP = "MERChar_EndGm2.SuicideBomination",
                AssetToPortIn = "MERChar_EndGm2.SuicideBomination",
                AssetPaths = new[] {
                    "BIOG_ZMB_ARM_NKD_R.NKDd.ZMB_ARM_NKDd_MDL",
                    "BIOG_ZMB_ARM_NKD_R.NKDd.ZMB_ARM_NKDd_MAT_1a"
                },
                PawnClassPath = "MERGamePawns.SFXPawn_BombinationSuicide",
                Classification = EPortablePawnClassification.Mook,
                IsCorrectedPackage = true
            },

            // Husk - Not used
            //new PortablePawn()
            //{
            //    PackageFilename = "BioD_ShpCr2_170HubRoom2.pcc",
            //    BioPawnTypeIFP = "BioChar_Collectors.SWARM_BlueHusk",
            //    AssetToPortIn = "BioChar_Collectors.SWARM_BlueHusk",
            //    AssetPaths = new[] {
            //        "BIOG_ZMB_ARM_NKD_R.NKDa.ZMBLite_ARM_NKDa_MDL",
            //        "BIOG_ZMB_ARM_NKD_R.NKDa.ZMB_ARM_NKDa_MAT_1a"
            //    },
            //    PawnClassPath = "SFXGamePawns.SFXPawn_HuskLite",
            //    Classification = EPortablePawnClassification.Mook,
            //    IsCorrectedPackage = false
            //},

            // Charging husk - charges immediately
            new PortablePawn()
            {
                PackageFilename = "SFXPawn_ChargingHusk.pcc",
                BioPawnTypeIFP = "MERChar_Enemies.ChargingHusk",
                AssetToPortIn = "MERChar_Enemies.ChargingHusk",
                AssetPaths = new[] {
                    "BIOG_ZMB_ARM_NKD_R.NKDa.ZMBLite_ARM_NKDa_MDL",
                    "BIOG_ZMB_ARM_NKD_R.NKDa.ZMB_ARM_NKDa_MAT_1a"
                },
                PawnClassPath = "MERGamePawns.SFXPawn_ChargingHusk",
                Classification = EPortablePawnClassification.Mook,
                IsCorrectedPackage = true
            },

            // Klixen
            new PortablePawn()
            {
                PackageFilename = "SFXPawn_Spider.pcc",
                BioPawnTypeIFP = "BioChar_Animals.Combat.ELT_Spider",
                AssetToPortIn = "BioChar_Animals.Combat.ELT_Spider",
                AssetPaths = new[] {
                    "biog_cbt_rac_nkd_r.NKDa.CBT_RAC_NKDa_MDL",
                    "EffectsMaterials.Users.Creatures.CBT_SPD_NKD_MAT_1a_USER",
                },
                PawnClassPath = "SFXGamePawns.SFXPawn_Spider",
                IsCorrectedPackage = true
            },

            // Scion
            new PortablePawn()
            {
                PackageFilename = "SFXPawn_Scion.pcc",
                BioPawnTypeIFP = "BioChar_Collectors.ELT_Scion",
                AssetToPortIn = "BioChar_Collectors.ELT_Scion",
                AssetPaths = new[] {
                    // I don't think these are really necessary, technically...
                    "BIOG_SCI_ARM_NKD_R.NKDa.SCI_ARM_NKDa_MDL",
                    "BIOG_SCI_ARM_NKD_R.NKDa.SCI_ARM_NKDa_MAT_1a",
                },
                PawnClassPath = "SFXGamePawns.SFXPawn_Scion",
                IsCorrectedPackage = true
            },

            // collector sniper
            new PortablePawn()
            {
                PackageFilename = "SFXPawn_CollectorNeedler.pcc",
                BioPawnTypeIFP = "BioChar_Collectors.Soldiers.ELT_COL_Needler",
                AssetToPortIn = "BioChar_Collectors.Soldiers.ELT_COL_Needler",
                AssetPaths = new string[] {
                    // Collector already has this here
                },
                PawnClassPath = "SFXGamePawns.SFXPawn_CollectorDrone",
                IsCorrectedPackage = true
            },

            // collector flamer
            new PortablePawn()
            {
                PackageFilename = "SFXPawn_CollectorFlamer.pcc",
                BioPawnTypeIFP = "MERChar_Enemies.CollectorFlamerSpawnable",
                AssetToPortIn = "MERChar_Enemies.CollectorFlamerSpawnable",
                AssetPaths = new string[] {
                    // Assets are already referenced by custom pawn
                },
                PawnClassPath = "MERGameContent.SFXPawn_CollectorFlamer",
                IsCorrectedPackage = true
            },

            // Collector Kaidan
            new PortablePawn()
            {
                PackageFilename = "SFXPawn_Kaidan.pcc",
                BioPawnTypeIFP = "MERChar_Enemies.KaidanSpawnable",
                AssetToPortIn = "MERChar_Enemies.KaidanSpawnable",
                AssetPaths = new string[] {
                    // Assets are already referenced by custom pawn
                },
                PawnClassPath = "MERGamePawns.SFXPawn_horcr1_kaidan",
                IsCorrectedPackage = true
            },

            // Collector Ashley
            new PortablePawn()
            {
                PackageFilename = "SFXPawn_Ashley.pcc",
                BioPawnTypeIFP = "MERChar_Enemies.AshleySpawnable",
                AssetToPortIn = "MERChar_Enemies.AshleySpawnable",
                AssetPaths = new string[] {
                    // Assets are already referenced by custom pawn
                },
                PawnClassPath = "MERGamePawns.SFXPawn_horcr1_ashley",
                IsCorrectedPackage = true
            },
        };

        public static void ResetClass()
        {
            //foreach (var pp in PortablePawns)
            //{
            //    if (pp.TextureUpdates != null)
            //    {
            //        foreach (var tu in pp.TextureUpdates)
            //        {
            //            tu.Reset();
            //        }
            //    }
            //}
        }

        internal static void PortHelper(GameTarget target)
        {
#if LEGACY
            // This is ME2R
            var pName = "BioPawn_CollectorAsari_S1.pcc";
            var afUindex = 2914;

            var package = MEPackageHandler.OpenMEPackageFromStream(MEREmbedded.GetEmbeddedPackage(target.Game, "correctedpawns." + pName));
            var af = package.GetUExport(afUindex).GetProperty<ArrayProperty<ObjectProperty>>("ActorResourceCollection");
            foreach (var v in af)
            {
                var resolved = v.ResolveToEntry(package);
                Debug.WriteLine($"\"{resolved.InstancedFullPath}\",");
            }
#endif
        }

        public static bool IsPawnAssetInPackageAlready(PortablePawn pawn, IMEPackage targetPackage)
        {
            return targetPackage.FindExport(pawn.AssetToPortIn) != null;
        }

        public static bool PortPawnIntoPackage(GameTarget target, PortablePawn pawn, IMEPackage targetPackage)
        {
            if (IsPawnAssetInPackageAlready(pawn, targetPackage))
            {
                return true; // Pawn asset to port in already ported in
            }

            IMEPackage pawnPackage = null;
            var pF = MERFileSystem.GetPackageFile(target, pawn.PackageFilename);
            if (pF != null)
            {
                pawnPackage = MERFileSystem.OpenMEPackage(pF);
            }
            else
            {
                Debug.WriteLine($"Pawn package not found: {pawn.PackageFilename}");
                Debugger.Break();
            }

            if (pawnPackage != null)
            {
                var iAsset = pawnPackage.FindExport(pawn.AssetToPortIn);
                if (iAsset == null)
                    Debugger.Break();
                PackageTools.PortExportIntoPackage(target, targetPackage, iAsset, useMemorySafeImport: !pawn.IsCorrectedPackage);

                // Ensure the assets are too as they may not be directly referenced except in the level instance
                foreach (var asset in pawn.AssetPaths)
                {
                    if (targetPackage.FindExport(asset) == null)
                    {
                        PackageTools.PortExportIntoPackage(target, targetPackage, pawnPackage.FindExport(asset), useMemorySafeImport: !pawn.IsCorrectedPackage);
                    }
                }

                //if (pawn.TextureUpdates != null)
                //{
                //    foreach (var tu in pawn.TextureUpdates)
                //    {
                //        var targetTextureExp = targetPackage.FindExport(tu.TextureInstancedFullPath);
                //        TextureHandler.InstallTexture(target, tu, targetTextureExp);
                //    }
                //}

                return true;
            }
            return false;
        }
    }
}
