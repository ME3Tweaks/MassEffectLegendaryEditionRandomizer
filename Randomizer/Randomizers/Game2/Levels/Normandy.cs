﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Utility;
using Randomizer.Shared;

namespace Randomizer.Randomizers.Game2.Levels
{
    public class Normandy
    {
        public static bool PerformRandomization(GameTarget target, RandomizationOption option)
        {
            AddBurgersToCookingQuest(target);
            RandomizeNormandyHolo(target);
            RandomizeWrongWashroomSFX(target);
            RandomizeProbes(target);

            return true;
        }

        class NormandyProbe
        {
            internal string InstancedFullPath { get; set; }
            internal float Scale { get; set; }

            /// <summary>
            /// Installs the dynamic loading mapping data for the normandy probe
            /// </summary>
            public void AddDynamicLoadMapping()
            {
                CoalescedHandler.AddDynamicLoadMappingEntry(new SeekFreeInfo()
                {
                    EntryPath = InstancedFullPath,
                    SeekFreePackage = "MERGameContent_Probes"
                });
            }

            /// <summary>
            /// Installs the probe data to the config file
            /// </summary>
            public void AddToConfig()
            {
                var bioGame = CoalescedHandler.GetIniFile("BioGame.ini");
                var probeConfig = bioGame.GetOrAddSection("MERGameContentKismet.MERSeqAct_RandomizeProbe");
                probeConfig.AddEntry(new CoalesceProperty("MeshOptions", new CoalesceValue($"(ProbeMesh=\"{InstancedFullPath}\", Scale={Scale}f)", CoalesceParseAction.AddUnique)));
            }
        }

        /// <summary>
        /// Gets the defined list of probe options that will be installed into the config files. The assets should be in MERGameContent_Probes.pcc (for LE2R)
        /// </summary>
        /// <returns></returns>
        private static List<NormandyProbe> GetProbeOptions()
        {
            var list = new List<NormandyProbe>();

            // Sovereign
            list.Add(new NormandyProbe()
            {
                InstancedFullPath = "BioApl_Dec_Scaled_Ships01.Meshes.Sov_Min",
                Scale = 0.3f, // Needs verified
            });

            // Mako
            list.Add(new NormandyProbe()
            {
                InstancedFullPath = "BIOG_VEH_ROV_A.StaticMesh.ROV_StaticMesh",
                Scale = 0.1f, // Needs verified
            });

            // Collector ship
            list.Add(new NormandyProbe()
            {
                InstancedFullPath = "biog_veh_collector.Meshes.CollectorShip_Static_Ingame_HiRes",
                Scale = 0.0025f,
            });

            // Add more here

            return list;
        }

        private static void RandomizeProbes(GameTarget target)
        {
            var galaxyMapObjs = MERFileSystem.GetPackageFile(target, "BioD_Nor_103bGalaxyMapObjs.pcc");
            var galaxyMapObjsP = MERFileSystem.OpenMEPackage(galaxyMapObjs);

            var randomizeProbeSeqAct = SequenceObjectCreator.CreateSequenceObject(galaxyMapObjsP, "MERSeqAct_RandomizeProbe");
            var seq4 = galaxyMapObjsP.FindExport("TheWorld.PersistentLevel.Main_Sequence.GalaxyMap.SequenceReference_0.Sequence_4");
            KismetHelper.AddObjectToSequence(randomizeProbeSeqAct, seq4);

            KismetHelper.CreateOutputLink(randomizeProbeSeqAct, "Out", galaxyMapObjsP.FindExport("TheWorld.PersistentLevel.Main_Sequence.GalaxyMap.SequenceReference_0.Sequence_4.BioSeqAct_OrbitalGame_1"));

            // The starting event now goes to our randomize first which then goes to orbital game
            MERSeqTools.ChangeOutlink(galaxyMapObjsP.FindExport("TheWorld.PersistentLevel.Main_Sequence.GalaxyMap.SequenceReference_0.Sequence_4.BioSeqEvt_GalaxyMap_0"),
                0, 0, randomizeProbeSeqAct.UIndex);
            var externProbe = galaxyMapObjsP.FindExport("TheWorld.PersistentLevel.Main_Sequence.GalaxyMap.SequenceReference_0.Sequence_4.SeqVar_External_2");
            KismetHelper.CreateVariableLink(randomizeProbeSeqAct, "Probe", externProbe); // Link the probe to the new kismet object

            // Install the probe asset file
            MEREmbedded.GetEmbeddedPackage(MEGame.LE2, "DynamicLoad.MERGameContent_Probes.pcc").WriteToFile(Path.Combine(MERFileSystem.DLCModCookedPath, "MERGAmeContent_Probes.pcc"));

            // Add the dynamic load and config entries for it to use
            foreach (var v in GetProbeOptions())
            {
                v.AddDynamicLoadMapping();
                v.AddToConfig();
            }

            // Add one more random voice line
            var randSwitch = galaxyMapObjsP.FindExport("TheWorld.PersistentLevel.Main_Sequence.GalaxyMap.SequenceReference_0.Sequence_4.SeqAct_RandomSwitch_1");
            var newLinkNum = MERSeqTools.AddRandomSwitchOutput(randSwitch);

            var delay = MERSeqTools.AddDelay(SeqTools.GetParentSequence(randSwitch), 0.5f);

            var wwisePost = galaxyMapObjsP.FindExport("TheWorld.PersistentLevel.Main_Sequence.GalaxyMap.SequenceReference_0.Sequence_4.SeqAct_WwisePostEvent_3");
            KismetHelper.CreateOutputLink(randSwitch, $"Link {newLinkNum}", delay);
            KismetHelper.CreateOutputLink(delay, "Finished", wwisePost);

            MERFileSystem.SavePackage(galaxyMapObjsP);
        }

        /// <summary>
        /// Going into male room as female
        /// </summary>
        private static List<(string packageName, string IFP)> MaleWashroomAudioSources = new()
        {
            ("BioD_Nor_CR3_200_LOC_INT.pcc", "ss_collector_general_S.en_us_global_collector_general_ss_collector_general_00332542_m_wav"), // SHEPARD, SUBMIT NOW
            ("BioD_Nor_CR3_200_LOC_INT.pcc", "norcr3_jokershit_a_S.en_us_hench_joker_norcr3_jokershit_a_00312051_m_wav"), // shitshitshitshitshit
            ("BioD_Nor_250Henchmen_LOC_INT.pcc", "nor_cook_a_S.en_us_norcr3_crew_scout_nor_cook_a_00302097_m_wav"), // More food less ass
            ("BioD_TwrAsA_201LowerOffices_LOC_INT.pcc", "twrasa_surprisedguard_d_S.en_us_twrasa_merc_plummets_twrasa_surprisedguard_d_00189094_m_wav"), // merc falling scream
            ("BioD_CitHub_100Dock_LOC_INT.pcc", "cithub_tp_garrus_a_S.en_us_hench_garrus_cithub_tp_garrus_a_00277957_m_wav") // garrus: thought i might come back to see how its changed
        };

        /// <summary>
        /// Going into female room as male
        /// </summary>
        private static List<(string packageName, string IFP)> FemaleWashroomAudioSources = new()
        {
            ("BioD_Nor_CR3_200_LOC_INT.pcc", "ss_collector_general_S.en_us_global_collector_general_ss_collector_general_00332542_m_wav"), // SHEPARD, SUBMIT NOW
            ("BioD_Nor_CR3_200_LOC_INT.pcc", "norcr3_ensign_threesome_a_S.en_us_nor_yeoman_norcr3_ensign_threesome_a_00218997_m_wav"), // AUGH!
            ("BioD_ProCer_300ShuttleBay_LOC_INT.pcc", "procer_vixen_intro_d_S.en_us_hench_leading_procer_vixen_intro_d_00217867_m_wav"), // WHAT THE HELL ARE YOU DOING? (Jacob)!
            ("BioD_ProCer_250ControlRoom_LOC_INT.pcc","procer_wilson_intro_d_S.en_us_procer_wilson_procer_wilson_intro_d_00216755_m_wav"), // OH GOD, THEY FOUND ME
            ("BioD_ProCer_250ControlRoom_LOC_INT.pcc","procer_techpwrs_tutor_a_S.en_us_procer_wilson_procer_techpwrs_tutor_a_00248148_m_wav") // This really isn't the time, jacob
        };

        private static void RandomizeWrongWashroomSFX(GameTarget target)
        {
            // Yeah I went to canada
            // they call them washrooms
            var henchmenLOCInt250 = MERFileSystem.GetPackageFile(target, "BioD_Nor_250Henchmen_LOC_INT.pcc");
            if (henchmenLOCInt250 != null && File.Exists(henchmenLOCInt250))
            {
                var washroomP = MEPackageHandler.OpenMEPackage(henchmenLOCInt250);
                using MERPackageCache pc = new MERPackageCache(target, MERCaches.GlobalCommonLookupCache, true);
                var randomMale = MaleWashroomAudioSources.RandomElement();
                var randomFemale = FemaleWashroomAudioSources.RandomElement();

                var mPackage = pc.GetCachedPackage(randomMale.packageName);
                var fPackage = pc.GetCachedPackage(randomFemale.packageName);
                WwiseTools.RepointWwiseStream(mPackage.FindExport(randomMale.IFP), washroomP.FindExport("nor_ai_restroom_a_S.en_us_hench_ai_nor_ai_restroom_a_00325703_m_wav")); //male into female
                WwiseTools.RepointWwiseStream(fPackage.FindExport(randomFemale.IFP), washroomP.FindExport("nor_ai_restroom_a_S.en_us_hench_ai_nor_ai_restroom_a_00325702_m_wav")); //female into male

                MERFileSystem.SavePackage(washroomP);
            }
        }

        private static (Vector3 loc, CIVector3 rot)[] BurgerLocations = new[]
        {
            (new Vector3(-517,2498,-459), new CIVector3(0,ThreadSafeRandom.Next(65535),0)), // The one near the cook

            // On the tables
            (new Vector3(79,3265,-479), new CIVector3(0,ThreadSafeRandom.Next(65535),0)),
            (new Vector3(-94,3225,-479), new CIVector3(0,ThreadSafeRandom.Next(65535),0)),
            (new Vector3(160,3823,-479), new CIVector3(0,ThreadSafeRandom.Next(65535),0)),
        };

        private static void AddBurgersToCookingQuest(GameTarget target)
        {
            var cookingAreaF = MERFileSystem.GetPackageFile(target, "BioD_Nor_250Henchmen.pcc");
            if (cookingAreaF != null && File.Exists(cookingAreaF))
            {
                var nor250Henchmen = MEPackageHandler.OpenMEPackage(cookingAreaF);

                var packageBin = MEREmbedded.GetEmbeddedPackage(target.Game, "Delux2go_EdmontonBurger.pcc");
                var burgerPackage = MEPackageHandler.OpenMEPackageFromStream(packageBin);

                List<ExportEntry> addedBurgers = new List<ExportEntry>();

                // 1. Add the burger package by porting in the first skeletal mesh.
                var world = nor250Henchmen.FindExport("TheWorld.PersistentLevel");
                var firstBurgerSKM = PackageTools.PortExportIntoPackage(target, nor250Henchmen, burgerPackage.FindExport("BurgerSKMA"), world.UIndex, false);

                // Setup the object for cloning, add to list of new actors
                firstBurgerSKM.WriteProperty(new BoolProperty(true, "bHidden")); // Make burger hidden by default. It's unhidden in kismet
                addedBurgers.Add(firstBurgerSKM);

                // 1.5 Shrink the burger
                // Look, everyone likes a thicc burger but this thing is the size
                // of a barbie 4 wheeler
                // I think it could use a small taking down a notch
                var ds = firstBurgerSKM.GetProperty<FloatProperty>("DrawScale");
                ds.Value = 0.0005f;
                firstBurgerSKM.WriteProperty(ds);

                // 2. Link up the textures
                // ME2R OT/LE build now does this as package stored to make this simpler.
                //TextureHandler.RandomizeExport(target, nor250Henchmen.FindExport("Edmonton_Burger_Delux2go.Textures.Burger_Diff"), null);
                //TextureHandler.RandomizeExport(target, nor250Henchmen.FindExport("Edmonton_Burger_Delux2go.Textures.Burger_Norm"), null);
                //TextureHandler.RandomizeExport(target, nor250Henchmen.FindExport("Edmonton_Burger_Delux2go.Textures.Burger_Spec"), null);

                // 3. Clone 3 more burgers
                addedBurgers.Add(EntryCloner.CloneTree(firstBurgerSKM));
                addedBurgers.Add(EntryCloner.CloneTree(firstBurgerSKM));
                addedBurgers.Add(EntryCloner.CloneTree(firstBurgerSKM));

                // 4. Port in plates for the ones people are eating to sit on
                var plateFile = MERFileSystem.GetPackageFile(target, "BioA_OmgHub800_Marketplace.pcc");
                var platePackage = MEPackageHandler.OpenMEPackage(plateFile);
                List<ExportEntry> plateExports = new List<ExportEntry>();
                var plateSMA1 = PackageTools.PortExportIntoPackage(target, nor250Henchmen, platePackage.GetUExport(3280), world.UIndex, false);
                plateSMA1.WriteProperty(new BoolProperty(true, "bHidden")); //make hidden by default

                // Update the textures to remove that god awful original design and use something less ugly
                var pNorm = nor250Henchmen.FindExport("BioAPL_Dec_PlatesCup_Ceramic.Materials.Plates_Norm");
                var pDiff = nor250Henchmen.FindExport("BioAPL_Dec_PlatesCup_Ceramic.Materials.Plates_Diff");
                pNorm.ObjectName = "Plates_NotUgly_Norm";
                pDiff.ObjectName = "Plates_NotUgly_Diff";
                TextureHandler.RandomizeExport(target, pDiff, null);
                TextureHandler.RandomizeExport(target, pNorm, null);
                
                // We need to make dynamic lit - values are copied from another SMA in BioA_Nor_200. I don't know what the channels mean
                var skmc = plateSMA1.GetProperty<ObjectProperty>("StaticMeshComponent").ResolveToEntry(world.FileRef) as ExportEntry;
                var lc = skmc.GetProperty<StructProperty>("LightingChannels");
                lc.Properties.Clear();
                lc.Properties.Add(new BoolProperty(true, "bInitialized"));
                lc.Properties.Add(new BoolProperty(true, new NameReference("Unnamed", 5))); //??
                lc.Properties.Add(new BoolProperty(true, new NameReference("Unnamed", 7))); //??
                skmc.WriteProperty(lc);
                skmc.WriteProperty(new BoolProperty(false, "bForceDirectLightMap"));

                plateExports.Add(plateSMA1);
                plateExports.Add(EntryCloner.CloneTree(plateSMA1));
                plateExports.Add(EntryCloner.CloneTree(plateSMA1));

                // 4. Setup the locations and rotations, setup the sequence object info
                var toggleHiddenUnhide = nor250Henchmen.GetUExport(5734);
                for (int i = 0; i < addedBurgers.Count; i++)
                {
                    var burger = addedBurgers[i];
                    var lp = BurgerLocations[i];
                    burger.WriteProperty(lp.loc.ToVectorStructProperty("Location"));
                    burger.WriteProperty(lp.rot.ToRotatorStructProperty("Rotation"));

                    // Add burger object to kismet unhide
                    var cookSeq = nor250Henchmen.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Cook");


                    var clonedSeqObj = SequenceObjectCreator.CreateSequenceObject(nor250Henchmen, "SeqVar_Object");
                    clonedSeqObj.WriteProperty(new ObjectProperty(burger.UIndex, "ObjValue"));
                    KismetHelper.CreateVariableLink(toggleHiddenUnhide, "Target", clonedSeqObj);

                    if (i >= 1)
                    {
                        var plate = plateExports[i - 1];
                        // Put the burger on the plate
                        var pLoc = lp.loc;
                        pLoc.Z -= 15; // Plate under burger
                        plate.WriteProperty(lp.loc.ToVectorStructProperty("Location"));

                        // Add plate object to kismet unhide
                        var clonedSeqObjPlate = MERSeqTools.CloneBasicSequenceObject(nor250Henchmen.GetUExport(5970));
                        clonedSeqObjPlate.WriteProperty(new ObjectProperty(plate.UIndex, "ObjValue"));
                        KismetHelper.CreateVariableLink(toggleHiddenUnhide, "Target", clonedSeqObjPlate);
                    }
                }

                // Add burgers to level
                addedBurgers.AddRange(plateExports);
                nor250Henchmen.AddToLevelActorsIfNotThere(addedBurgers.ToArray());

                MERFileSystem.SavePackage(nor250Henchmen);
            }
        }

        private static void RandomizeTrashCompactor(GameTarget target)
        {
            var packageF = MERFileSystem.GetPackageFile(target, "BioA_Nor_310.pcc");
            if (packageF != null && File.Exists(packageF))
            {
                var package = MEPackageHandler.OpenMEPackage(packageF);
                if (package.TryGetUExport(2176, out var junkCube) && junkCube.ClassName == "SkeletalMesh")
                {
                    // We had to check the class name of the object cause it might be the vanilla version and not the HEN_VT one


                    MERFileSystem.SavePackage(package);
                }
            }

        }

        private static void RandomizeNormandyHolo(GameTarget target)
        {
            string[] packages = { "BioD_Nor_104Comm.pcc", "BioA_Nor_110.pcc" };
            foreach (var packagef in packages)
            {
                var package = MEPackageHandler.OpenMEPackage(MERFileSystem.GetPackageFile(target, packagef));

                //WIREFRAME COLOR
                var wireColorR = ThreadSafeRandom.NextFloat(0.01, 2);
                var wireColorG = ThreadSafeRandom.NextFloat(0.01, 2);
                var wireColorB = ThreadSafeRandom.NextFloat(0.01, 2);

                var wireframeMaterial = package.FindExport("BioVFX_Env_Hologram.Material.Wireframe_mat_Master");
                var matConst = MERMaterials.GenerateMaterialInstanceConstantFromMaterial(wireframeMaterial);
                MERMaterials.SetMatConstVectorParam(matConst, "Color", wireColorR, wireColorG, wireColorB);

                // Now make it use the mat const
                var exports = new List<string>();
                if (packagef == "BioD_Nor_104Comm.pcc")
                {
                    exports.Add("TheWorld.PersistentLevel.SkeletalMeshActor_2.SkeletalMeshComponent_37"); // Small normandy in comm room
                }
                else
                {
                    exports.Add("BioVFX_Env_Galaxymap.Prefabs.Nor2_Deck_Hologram_wUpgrades_Prefab.Nor2_Deck_Hologram_wUpgrades_Prefab_Arc16.SkeletalMeshComponent0"); // CIC normandy
                }

                foreach (var expIFP in exports)
                {
                    var exp = package.FindExport(expIFP);
                    if (exp != null)
                    {
                        var mats = exp.GetProperty<ArrayProperty<ObjectProperty>>("Materials");
                        foreach (var mat in mats)
                        {
                            mat.Value = matConst.UIndex; // Repoint material, both are the same here on the wireframe mat refs
                        }

                        exp.WriteProperty(mats);
                    }
                }




                //data.OverwriteRange(0x33C, BitConverter.GetBytes(wireColorR)); //R
                //data.OverwriteRange(0x340, BitConverter.GetBytes(wireColorG)); //G
                //data.OverwriteRange(0x344, BitConverter.GetBytes(wireColorB)); //B
                //wireframeMaterial.Data = data;

                //INTERNAL HOLO
                var norHoloLargeMat = package.FindExport("BioVFX_Env_Galaxymap.Materials.Nor_Hologram_Large_METR"); // LE2 uses METR

                float holoR = 0, holoG = 0, holoB = 0;
                holoR = wireColorR * 5;
                holoG = wireColorG * 5;
                holoB = wireColorB * 5;

                matConst = MERMaterials.GenerateMaterialInstanceConstantFromMaterial(norHoloLargeMat);
                MERMaterials.SetMatConstVectorParam(matConst, "Color", holoR, holoG, holoB);

                // Now make it use the mat const
                exports = new List<string>();
                if (packagef == "BioD_Nor_104Comm.pcc")
                {
                    exports.Add("TheWorld.PersistentLevel.SkeletalMeshActor_1.SkeletalMeshComponent_36"); // The main mesh?
                }
                else
                {
                    exports.Add("TheWorld.PersistentLevel.SkeletalMeshActor_2.SkeletalMeshComponent_1"); // CIC normandy
                }

                // Fix the colors
                foreach (var exp in exports)
                {
                    var skm1 = package.FindExport(exp);
                    if (skm1 != null)
                    {
                        var mats = skm1.GetProperty<ArrayProperty<ObjectProperty>>("Materials");
                        mats[0].Value = matConst.UIndex; // First material in list is METR Nor Holo
                        skm1.WriteProperty(mats);
                    }
                }






                if (packagef == "BioA_Nor_110.pcc")
                {
                    //need to also adjust the glow under the CIC. It's controlled by a interp apparently
                    var lightColorInterp =
                        package.FindExport(
                            "TheWorld.PersistentLevel.Main_Sequence.InterpData_3.InterpGroup_0.InterpTrackColorProp_1");
                    var vectorTrack = lightColorInterp.GetProperty<StructProperty>("VectorTrack");
                    var blueToOrangePoints = vectorTrack.GetProp<ArrayProperty<StructProperty>>("Points");
                    //var maxColor = allColors.Max();
                    blueToOrangePoints[1].GetProp<StructProperty>("OutVal").GetProp<FloatProperty>("X").Value =
                        wireColorR;
                    blueToOrangePoints[1].GetProp<StructProperty>("OutVal").GetProp<FloatProperty>("Y").Value =
                        wireColorG;
                    blueToOrangePoints[1].GetProp<StructProperty>("OutVal").GetProp<FloatProperty>("Z").Value =
                        wireColorB;
                    lightColorInterp.WriteProperty(vectorTrack);
                }

                MERFileSystem.SavePackage(package);
            }
        }
    }
}