using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Levels
{
    public static class ArrivalDLC
    {

        public static bool RandomizeParticleSystems(GameTarget gameTarget, ExportEntry exportEntry, RandomizationOption option)
        {
            if (!CanRandomizePS(exportEntry))
                return false;

            var props = exportEntry.GetProperties();
            var emitters = props.GetProp<ArrayProperty<ObjectProperty>>("Emitters");
            foreach (var emitter in emitters.Select(x => x.ResolveToExport(exportEntry.FileRef)))
            {
                var emitterLODs = emitter.GetProperty<ArrayProperty<ObjectProperty>>("LODLevels");
                int lodNumber = 0;
                if (emitterLODs != null)
                {
                    foreach (var lodExport in emitterLODs.Select(x => x.ResolveToExport(exportEntry.FileRef)))
                    {
                        var lodProps = lodExport.GetProperties();
                        #region LOD MODULES?
                        var modules = lodProps.GetProp<ArrayProperty<ObjectProperty>>("Modules");
                        if (modules != null)
                        {
                            foreach (var mod in modules.Select(x => x.ResolveToExport(exportEntry.FileRef)))
                            {
                                RandomizeModule(mod);
                            }
                        }
                        #endregion

                        #region BAKED IN MODULES
                        {
                            var requiredModule = (ExportEntry)lodProps.GetProp<ObjectProperty>("RequiredModule")?.ResolveToEntry(exportEntry.FileRef);
                            if (requiredModule != null)
                            {
                                var rProps = requiredModule.GetProperties();
                                var burstList = rProps.GetProp<ArrayProperty<StructProperty>>("BurstList");
                                if (burstList != null)
                                {
                                    foreach (var bl in burstList)
                                    {
                                        bl.GetProp<IntProperty>("Count").Value = 50;
                                    }
                                }

                                var spawnRate = rProps.GetProp<StructProperty>("SpawnRate");
                                if (spawnRate != null)
                                {
                                    var spawnLookupTable = spawnRate.GetProp<ArrayProperty<FloatProperty>>("LookupTable");
                                    if (spawnLookupTable != null)
                                    {
                                        var val = ThreadSafeRandom.Next(30);
                                        foreach (var f in spawnLookupTable)
                                        {
                                            f.Value = val;
                                        }
                                    }
                                }

                                requiredModule.WriteProperties(rProps);
                            }
                            #endregion
                        }

                        //var typeDataExport = (ExportEntry)lodExport.GetProperty<ObjectProperty>("TypeDataModule")?.ResolveToEntry(exportEntry.FileRef);
                        //if (typeDataExport != null)
                        //{
                        //    var meshes = typeDataExport.GetProperty<ArrayProperty<ObjectProperty>>("m_Meshes");
                        //    if (meshes != null)
                        //    {
                        //        int meshIndex = 0;
                        //        foreach (var mesh in meshes)
                        //        {
                        //            var meshExp = mesh.ResolveToEntry(exportEntry.FileRef);
                        //            if (meshExp != null)
                        //            {
                        //            }

                        //            meshIndex++;
                        //        }
                        //    }
                        //}

                        //var modules = lodExport.GetProperty<ArrayProperty<ObjectProperty>>("Modules");
                        //if (modules != null)
                        //{
                        //    int modIndex = 0;
                        //    foreach (var module in modules)
                        //    {
                        //        var moduleExp = module.ResolveToEntry(exportEntry.FileRef);
                        //        if (moduleExp != null)
                        //        {
                        //            ParticleSystemNode moduleNode = new()
                        //            {
                        //                Entry = moduleExp,
                        //                Header = $"Module {modIndex}: {moduleExp.UIndex} {moduleExp.InstancedFullPath}"
                        //            };

                        //            psLod.Children.Add(moduleNode);
                        //            GenerateNode(moduleNode);
                        //        }

                        //        modIndex++;
                        //    }
                        //}

                        lodNumber++;
                    }
                }
            }


            return true;

            return true;
        }

        private static void RandomizeModule(ExportEntry mod)
        {
            switch (mod.ClassName)
            {
                case "ParticleModuleColorOverLife":
                    RandomizePMCOL(mod);
                    break;
            }
        }

        private static void RandomizePMCOL(ExportEntry mod)
        {
            var colorDist = new BioRawDistributionRwVector3(mod, "ColorOverLifeRw");

            foreach (var v in colorDist.LookupTable)
            {
                v.X = ThreadSafeRandom.NextFloat(1);
                v.Y = ThreadSafeRandom.NextFloat(1);
                v.Z = ThreadSafeRandom.NextFloat(1);
            }

            colorDist.WriteToExport(mod);
        }


        /// <summary>
        /// Helper class for working with vector lookup tables
        /// </summary>
        class BioRawDistributionRwVector3
        {
            public float LookupTableTimeScale { get; set; }
            public List<CFVector3> LookupTable { get; }
            public BioRawDistributionRwVector3(ExportEntry exp, string propertyName)
            {
                PropertyName = propertyName;
                var prop = exp.GetProperty<StructProperty>(propertyName);
                LookupTable = new List<CFVector3>();

                var lookupTable = prop.GetProp<ArrayProperty<StructProperty>>("LookupTable");
                if (lookupTable != null)
                {
                    foreach (var item in lookupTable)
                    {
                        LookupTable.Add(CFVector3.FromStructProperty(item, "X", "Y", "Z"));
                    }
                }

                LookupTableTimeScale = prop.GetProp<FloatProperty>("LookupTableTimeScale")?.Value ?? 1;
            }

            public void WriteToExport(ExportEntry exp)
            {
                PropertyCollection pc = new PropertyCollection();
                pc.AddOrReplaceProp(new FloatProperty(GetMin(), "LookupTableMinOut"));
                pc.AddOrReplaceProp(new FloatProperty(GetMax(), "LookupTableMaxOut"));

                var lookupTable = new ArrayProperty<StructProperty>("LookupTable");
                foreach (var lti in LookupTable)
                {
                    lookupTable.Add(lti.ToStructProperty("X", "Y", "Z"));
                }
                pc.AddOrReplaceProp(lookupTable);
                pc.AddOrReplaceProp(new FloatProperty(0, "LookupTableTimeScale"));

                StructProperty sp = new StructProperty("BioRawDistributionRwVector3", pc, PropertyName);
                exp.WriteProperty(sp);
            }

            private float GetMax()
            {
                float max = float.MinValue;
                foreach (var item in LookupTable)
                {
                    if (item.X > max)
                    {
                        max = item.X;
                    }
                    if (item.Y > max)
                    {
                        max = item.Y;
                    }
                    if (item.Z > max)
                    {
                        max = item.Z;
                    }
                }

                return max;
            }

            private float GetMin()
            {
                float min = float.MaxValue;
                foreach (var item in LookupTable)
                {
                    if (item.X < min)
                    {
                        min = item.X;
                    }
                    if (item.Y < min)
                    {
                        min = item.Y;
                    }
                    if (item.Z < min)
                    {
                        min = item.Z;
                    }
                }

                return min;
            }

            public string PropertyName { get; set; }
        }

        private static bool CanRandomizePS(ExportEntry export)
        {
            return !export.IsDefaultObject && export.ClassName == "ParticleSystem";
        }

        private static void RandomizeAsteroidRelayColor(GameTarget target)
        {
            {
                // Relay at the end of the DLC
                var shuttleFile = MERFileSystem.GetPackageFile(target, @"BioD_ArvLvl5_110_Asteroid.pcc", false);
                var shuttleP = MERFileSystem.OpenMEPackage(shuttleFile);
                var randColorR = ThreadSafeRandom.Next(256);
                var randColorG = ThreadSafeRandom.Next(256);
                var randColorB = ThreadSafeRandom.Next(256);

                var stringsGlowMatInst = shuttleP.FindExport("BioVFX_Cin_MassRelay.Materials.Strings_Glow_INST_Blue");
                var props = stringsGlowMatInst.GetProperties();
                var linearColor = props.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues")[0]
                    .GetProp<StructProperty>("ParameterValue");
                linearColor.GetProp<FloatProperty>("R").Value = randColorR;
                linearColor.GetProp<FloatProperty>("G").Value = randColorG;
                linearColor.GetProp<FloatProperty>("B").Value = randColorB;
                stringsGlowMatInst.WriteProperties(props);

                var lensFlare = shuttleP.FindExport("TheWorld.PersistentLevel.LensFlareSource_1.LensFlareComponent_1");
                var sourceColor = lensFlare.GetProperty<StructProperty>("SourceColor");
                sourceColor.GetProp<FloatProperty>("R").Value = randColorR / 90.0f;
                sourceColor.GetProp<FloatProperty>("G").Value = randColorG / 90.0f;
                sourceColor.GetProp<FloatProperty>("B").Value = randColorB / 90.0f;
                lensFlare.WriteProperty(sourceColor);

                // lighting on the relay
                // ME2: TheWorld.PersistentLevel.PointLight_0.PointLightComponent_28
                var pointlight = shuttleP.FindExport("TheWorld.PersistentLevel.StaticLightCollectionActor_67.PointLight_0_LC");
                var pointlightColor = pointlight.GetProperty<StructProperty>("LightColor");
                var lightR = randColorR / 2 + 170;
                var lightG = randColorG / 2 + 170;
                var lightB = randColorB / 2 + 170;
                pointlightColor.GetProp<ByteProperty>("R").Value = (byte)lightR;
                pointlightColor.GetProp<ByteProperty>("G").Value = (byte)lightG;
                pointlightColor.GetProp<ByteProperty>("B").Value = (byte)lightB;
                pointlight.WriteProperty(pointlightColor);

                //Shield Impact Ring (?)
                // Material changes in LE do not have this data anymore :(
                //var sir = shuttleP.FindExport("BioVFX_Cin_MassRelay.Materials.Shield_Impact_Ring");
                //var data = sir.Data;
                //data.OverwriteRange(0x418, BitConverter.GetBytes(randColorR / 80.0f));
                //data.OverwriteRange(0x41C, BitConverter.GetBytes(randColorG / 80.0f));
                //data.OverwriteRange(0x420, BitConverter.GetBytes(randColorB / 80.0f));
                //sir.Data = data;

                //Particle effect
                var pe = shuttleP.FindExport(
                    "BioVFX_Env_EXP2_Lvl5.Particles.Mass_Relay_Mini.ParticleModuleColorOverLife_5");
                props = pe.GetProperties();
                var lookupTable = props.GetProp<StructProperty>("ColorOverLifeRw")
                    .GetProp<ArrayProperty<StructProperty>>("LookupTable");

                float[] allColors = { randColorR / 255.0f, randColorG / 255.0f, randColorB / 255.0f };
                lookupTable.Clear();

                PropertyCollection lProps = new PropertyCollection();
                lProps.AddOrReplaceProp(new FloatProperty(allColors[0])); // R
                lProps.AddOrReplaceProp(new FloatProperty(allColors[1])); // G
                lProps.AddOrReplaceProp(new FloatProperty(allColors[2])); // B
                lookupTable.Add(new StructProperty("RwVector3", lProps, isImmutable: true));

                lProps.Clear();

                int numToAdd = ThreadSafeRandom.Next(3);

                //flare brightens as it fades out
                lProps.Add(new FloatProperty(allColors[0] + numToAdd == 0 ? .3f : 0f)); //r
                lProps.Add(new FloatProperty(allColors[1] + numToAdd == 1 ? .3f : 0f)); //g
                lProps.Add(new FloatProperty(allColors[2] + numToAdd == 2 ? .3f : 0f)); //b
                lookupTable.Add(new StructProperty("RwVector3", lProps, isImmutable: true));
                pe.WriteProperties(props);

                MERFileSystem.SavePackage(shuttleP);
            }
            return;

            // Relay shown in the shuttle at the end of act 1
            var asteroidf = MERFileSystem.GetPackageFile(target, @"BioD_ArvLvl1_710Shuttle.pcc", false);
            if (asteroidf != null && File.Exists(asteroidf))
            {
                var skhuttleP = MEPackageHandler.OpenMEPackage(asteroidf);
                var randColorR = ThreadSafeRandom.Next(256);
                var randColorG = ThreadSafeRandom.Next(256);
                var randColorB = ThreadSafeRandom.Next(256);

                var stringsGlowMatInst = skhuttleP.GetUExport(4715); //strings glow
                var props = stringsGlowMatInst.GetProperties();
                var linearColor = props.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues")[0].GetProp<StructProperty>("ParameterValue");
                linearColor.GetProp<FloatProperty>("R").Value = randColorR;
                linearColor.GetProp<FloatProperty>("G").Value = randColorG;
                linearColor.GetProp<FloatProperty>("B").Value = randColorB;
                stringsGlowMatInst.WriteProperties(props);

                var lensFlare = skhuttleP.GetUExport(2230);
                var sourceColor = lensFlare.GetProperty<StructProperty>("SourceColor");
                sourceColor.GetProp<FloatProperty>("R").Value = randColorR / 90.0f;
                sourceColor.GetProp<FloatProperty>("G").Value = randColorG / 90.0f;
                sourceColor.GetProp<FloatProperty>("B").Value = randColorB / 90.0f;
                lensFlare.WriteProperty(sourceColor);

                // lighting on the relay
                var pointlight = skhuttleP.GetUExport(8425);
                var pointlightColor = pointlight.GetProperty<StructProperty>("LightColor");
                var lightR = randColorR / 2 + 170;
                var lightG = randColorG / 2 + 170;
                var lightB = randColorB / 2 + 170;
                pointlightColor.GetProp<ByteProperty>("R").Value = (byte)lightR;
                pointlightColor.GetProp<ByteProperty>("G").Value = (byte)lightG;
                pointlightColor.GetProp<ByteProperty>("B").Value = (byte)lightB;
                pointlight.WriteProperty(pointlightColor);

                //Shield Impact Ring (?)
                var sir = skhuttleP.GetUExport(2278);
                var data = sir.Data;
                data.OverwriteRange(0x418, BitConverter.GetBytes(randColorR / 80.0f));
                data.OverwriteRange(0x41C, BitConverter.GetBytes(randColorG / 80.0f));
                data.OverwriteRange(0x420, BitConverter.GetBytes(randColorB / 80.0f));
                sir.Data = data;

                //Particle effect
                var pe = skhuttleP.GetUExport(6078);
                props = pe.GetProperties();
                var lookupTable = props.GetProp<StructProperty>("ColorOverLife").GetProp<ArrayProperty<FloatProperty>>("LookupTable");
                float[] allColors = { randColorR / 255.0f, randColorG / 255.0f, randColorB / 255.0f };
                lookupTable.Clear();
                lookupTable.Add(new FloatProperty(allColors.Min()));
                lookupTable.Add(new FloatProperty(allColors.Max()));

                lookupTable.Add(new FloatProperty(allColors[0])); //r
                lookupTable.Add(new FloatProperty(allColors[1])); //g
                lookupTable.Add(new FloatProperty(allColors[2])); //b

                int numToAdd = ThreadSafeRandom.Next(3);

                //flare brightens as it fades out
                lookupTable.Add(new FloatProperty(allColors[0] + numToAdd == 0 ? .3f : 0f)); //r
                lookupTable.Add(new FloatProperty(allColors[1] + numToAdd == 1 ? .3f : 0f)); //g
                lookupTable.Add(new FloatProperty(allColors[2] + numToAdd == 2 ? .3f : 0f)); //b
                pe.WriteProperties(props);

                MERFileSystem.SavePackage(skhuttleP);
            }
            else
            {
                MERLog.Information(@"BioD_ArvLvl1_710Shuttle not found, skipping...");
            }
        }

        internal static bool PerformRandomization(GameTarget target, RandomizationOption notUsed)
        {
            RandomizeAsteroidRelayColor(target);
            MakeKensonCool(target);
            return true;
        }

        private static void MakeKensonCool(GameTarget target)
        {
            string[] files = new[]
            {
                "BioD_ArvLvl1.pcc",
                "BioD_ArvLvl4_300Entrance.pcc",
            };
            foreach (var f in files)
            {
                var kensonFile = MERFileSystem.GetPackageFile(target, f, false);
                if (kensonFile != null && File.Exists(kensonFile))
                {
                    var kensonP = MEPackageHandler.OpenMEPackage(kensonFile);
                    var ifp = kensonP.FindExport("SFXGameContentKenson.Default__SFXPawn_Kenson_01.BioPawnSkeletalMeshComponent");
                    ifp.RemoveProperty("Materials");
                    ifp.RemoveProperty("SkeletalMesh");

                    // Remove materials used in base as they're wrong
                    kensonP.FindExport("SFXGameContentKenson.Default__SFXPawn_Kenson.BioPawnSkeletalMeshComponent").RemoveProperty("Materials");

                    MERFileSystem.SavePackage(kensonP);
                }
                else
                {
                    MERLog.Information($"Kenson file not found: {f}, skipping...");
                }
            }
        }

        private static int ShiftAmountMax = 1500;
        public static bool RotateMeshes(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            if (!CanRandomizeMeshActor(export))
                return false;

            if (export.ClassName == "StaticMeshActor")
            {
                var existingRotation = export.GetProperty<StructProperty>("Rotation");
                var idx = ThreadSafeRandom.Next(3);

                if (existingRotation == null)
                {
                    CIVector3 civ = new CIVector3();
                    if (idx == 0)
                        civ.X = ThreadSafeRandom.Next(ShiftAmountMax * 2) - ShiftAmountMax;
                    if (idx == 1)
                        civ.Y = ThreadSafeRandom.Next(ShiftAmountMax * 2) - ShiftAmountMax;
                    if (idx == 2)
                        civ.Z = ThreadSafeRandom.Next(ShiftAmountMax * 2) - ShiftAmountMax;
                    existingRotation = civ.ToStructProperty("Pitch", "Yaw", "Roll", "Rotation");
                }
                else
                {
                    CIVector3 civ = CIVector3.FromRotator(existingRotation);
                    if (idx == 0)
                        civ.X += ThreadSafeRandom.Next(ShiftAmountMax * 2) - ShiftAmountMax;
                    if (idx == 1)
                        civ.Y += ThreadSafeRandom.Next(ShiftAmountMax * 2) - ShiftAmountMax;
                    if (idx == 2)
                        civ.Z += ThreadSafeRandom.Next(ShiftAmountMax * 2) - ShiftAmountMax;
                    existingRotation = civ.ToStructProperty("Pitch", "Yaw", "Roll", "Rotation");
                }

                export.WriteProperty(existingRotation);
            }
            else if (export.ClassName == "StaticMeshCollectionActor")
            {
                var bin = ObjectBinary.From<StaticMeshCollectionActor>(export);
                for (int i = 0; i < bin.LocalToWorldTransforms.Count; i++)
                {
                    var actor = bin.LocalToWorldTransforms[i];
                    var parts = actor.UnrealDecompose();
                    var idx = ThreadSafeRandom.Next(3);
                    var pitch = parts.rotation.Pitch;
                    if (idx == 0)
                        pitch += ThreadSafeRandom.Next(ShiftAmountMax * 2) - ShiftAmountMax;
                    var yaw = parts.rotation.Pitch;
                    if (idx == 1)

                        yaw += ThreadSafeRandom.Next(ShiftAmountMax * 2) - ShiftAmountMax;
                    var roll = parts.rotation.Pitch;
                    if (idx == 2)

                        roll += ThreadSafeRandom.Next(ShiftAmountMax * 2) - ShiftAmountMax;

                    Rotator r = new Rotator(pitch, yaw, roll);
                    bin.UpdateTransformationForIndex(i, parts.translation, parts.scale, r);
                }
                export.WriteBinary(bin);
            }

            return true;
        }

        private static bool CanRandomizeMeshActor(ExportEntry export)
        {
            if (!export.IsDefaultObject && export.ClassName is "StaticMeshActor" or "StaticMeshCollectionActor")
                return true;
            return false;
        }
    }
}
