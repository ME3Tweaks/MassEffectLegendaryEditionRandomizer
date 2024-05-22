using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game1._2DA;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared;
using Randomizer.Randomizers.Utility;
using Serilog;

namespace Randomizer.Randomizers.Game1.GalaxyMap
{
    public static class GalaxyMapRandomizer
    {
        private static readonly int[] GalaxyMapImageIdsThatArePlotReserved = { 1, 7, 8, 116, 117, 118, 119, 120, 121, 122, 123, 124 }; //Plot or Sol planets
        private static readonly int[] GalaxyMapImageIdsThatAreAsteroidReserved = { 70 }; //Asteroids
        private static readonly int[] GalaxyMapImageIdsThatAreFactoryReserved = { 6 }; //Asteroids
        private static readonly int[] GalaxyMapImageIdsThatAreMSVReserved = { 76, 79, 82, 85 }; //MSV Ships
        private static readonly int[] GalaxyMapImageIdsToNeverRandomize = { 127, 128 }; //no idea what these are

        private static void GalaxyMapValidationPass(GameTarget target, RandomizationOption option, Dictionary<int, RandomizedPlanetInfo> rowRPIMapping, Bio2DA planets2DA, Bio2DA galaxyMapImages2DA, IMEPackage galaxyMapImagesPackage)
        {
            option.CurrentOperation = "Running tests on galaxy map images";
            option.ProgressIndeterminate = false;
            option.ProgressMax = rowRPIMapping.Keys.Count;
            option.ProgressValue = 0;

            foreach (int i in rowRPIMapping.Keys)
            {
                option.ProgressValue++;

                //For every row in planets 2DA table
                if (planets2DA[i, "Description"] != null && planets2DA[i, "Description"].DisplayableValue != "-1")
                {
                    int imageRowReference = planets2DA[i, "ImageIndex"].IntValue;
                    if (imageRowReference == -1) continue; //We don't have enough images yet to pass this hurdle
                                                           //Use this value to find value in UI table
                    int rowIndex = galaxyMapImages2DA.GetRowIndexByName(imageRowReference.ToString());
                    string exportName = galaxyMapImages2DA[rowIndex, 0].NameValue;
                    exportName = exportName.Substring(exportName.LastIndexOf('.') + 1);
                    //Use this value to find the export in GUI_SF file
                    var export = galaxyMapImagesPackage.Exports.FirstOrDefault(x => x.ObjectName == exportName);
                    if (export == null)
                    {
                        Debugger.Break();
                    }
                    else
                    {
                        string path = export.GetProperty<StrProperty>("SourceFilePath").Value;
                        path = path.Substring(path.LastIndexOf(' ') + 1);
                        string[] parts = path.Split('.');
                        if (parts.Length == 6)
                        {
                            string swfImageGroup = parts[3];
                            var assignedRPI = rowRPIMapping[i];
                            if (assignedRPI.ImageGroup.ToLower() != swfImageGroup)
                            {
                                Debug.WriteLine("WRONG IMAGEGROUP ASSIGNED!");
                                Debugger.Break();
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Source comment not correct format, might not yet be assigned: " + path);
                        }
                    }

                }
                else
                {
                    //Debugger.Break();
                }
            }
        }

        private static void ReplaceSWFFromResource(ExportEntry exp, string swfResourcePath)
        {
            Debug.WriteLine($"Replacing {Path.GetFileName(exp.FileRef.FilePath)} {exp.UIndex} {exp.ObjectName} SWF with {swfResourcePath}");
            var bytes = MEREmbedded.GetEmbeddedAsset("Binary", swfResourcePath);
            var props = exp.GetProperties();

            props.AddOrReplaceProp(new ImmutableByteArrayProperty(bytes.ReadFully(), "RawData"));

            //Write SWF metadata
            props.AddOrReplaceProp(new StrProperty("MASS EFFECT (LEGENDARY EDITION) RANDOMIZER - " + MEREmbedded.GetFilenameFromAssetName(swfResourcePath), "SourceFilePath"));
            props.AddOrReplaceProp(new StrProperty(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), "SourceFileTimestamp"));
            exp.WriteProperties(props);
        }


        public const string RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION_PLOTPLANET = "RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION_PLOTPLANET";

#if LEGACY
        

        private static void RandomizePlanetNameDescriptions(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            option.CurrentOperation = "Applying entropy to galaxy map";
            string fileContents = MEREmbedded.GetEmbeddedTextAsset("planetinfo.xml");

            XElement rootElement = XElement.Parse(fileContents);
            var allMapRandomizationInfo = (from e in rootElement.Elements("RandomizedPlanetInfo")
                                           select new RandomizedPlanetInfo
                                           {
                                               PlanetName = (string)e.Element("PlanetName"),
                                               PlanetName2 = (string)e.Element("PlanetName2"), //Original name (plot planets only)
                                               PlanetDescription = (string)e.Element("PlanetDescription"),
                                               IsMSV = (bool)e.Element("IsMSV"),
                                               IsAsteroidBelt = (bool)e.Element("IsAsteroidBelt"),
                                               IsAsteroid = e.Element("IsAsteroid") != null && (bool)e.Element("IsAsteroid"),
                                               PreventShuffle = (bool)e.Element("PreventShuffle"),
                                               RowID = (int)e.Element("RowID"),
                                               MapBaseNames = e.Elements("MapBaseNames")
                                                   .Select(r => r.Value).ToList(),
                                               DLC = e.Element("DLC")?.Value,
                                               ImageGroup = e.Element("ImageGroup")?.Value ?? "Generic", //TODO: TURN THIS OFF FOR RELEASE BUILD AND DEBUG ONCE FULLY IMPLEMENTED
                                               ButtonLabel = e.Element("ButtonLabel")?.Value,
                                               Playable = !(e.Element("NotPlayable") != null && (bool)e.Element("NotPlayable")),
                                           }).ToList();

            fileContents = MEREmbedded.GetEmbeddedTextAsset("galaxymapclusters.xml");
            rootElement = XElement.Parse(fileContents);
            var suffixedClusterNames = rootElement.Elements("suffixedclustername").Select(x => x.Value).ToList(); //Used for assignments
            var suffixedClusterNamesForPreviousLookup = rootElement.Elements("suffixedclustername").Select(x => x.Value).ToList(); //Used to lookup previous assignments 
            VanillaSuffixedClusterNames = rootElement.Elements("originalsuffixedname").Select(x => x.Value).ToList();
            var nonSuffixedClusterNames = rootElement.Elements("nonsuffixedclustername").Select(x => x.Value).ToList();
            suffixedClusterNames.Shuffle();
            nonSuffixedClusterNames.Shuffle();

            fileContents = MEREmbedded.GetEmbeddedTextAsset("galaxymapsystems.xml");
            rootElement = XElement.Parse(fileContents);
            var shuffledSystemNames = rootElement.Elements("systemname").Select(x => x.Value).ToList();
            shuffledSystemNames.Shuffle();


            var everything = new List<string>();
            everything.AddRange(suffixedClusterNames);
            everything.AddRange(allMapRandomizationInfo.Select(x => x.PlanetName));
            everything.AddRange(allMapRandomizationInfo.Where(x => x.PlanetName2 != null).Select(x => x.PlanetName2));
            everything.AddRange(shuffledSystemNames);
            everything.AddRange(nonSuffixedClusterNames);

            //Subset checking
            //foreach (var name1 in everything)
            //{
            //    foreach (var name2 in everything)
            //    {
            //        if (name1.Contains(name2) && name1 != name2)
            //        {
            //            //Debugger.Break();
            //        }
            //    }
            //}

            var msvInfos = allMapRandomizationInfo.Where(x => x.IsMSV).ToList();
            var asteroidInfos = allMapRandomizationInfo.Where(x => x.IsAsteroid).ToList();
            var asteroidBeltInfos = allMapRandomizationInfo.Where(x => x.IsAsteroidBelt).ToList();
            var planetInfos = allMapRandomizationInfo.Where(x => !x.IsAsteroidBelt && !x.IsAsteroid && !x.IsMSV && !x.PreventShuffle).ToList();

            msvInfos.Shuffle();
            asteroidInfos.Shuffle();
            planetInfos.Shuffle();

            List<int> rowsToNotRandomlyReassign = new List<int>();

            ExportEntry systemsExport = export.FileRef.Exports.First(x => x.ObjectName == "GalaxyMap_System");
            ExportEntry clustersExport = export.FileRef.Exports.First(x => x.ObjectName == "GalaxyMap_Cluster");
            ExportEntry areaMapExport = export.FileRef.Exports.First(x => x.ObjectName == "AreaMap_AreaMap");
            ExportEntry plotPlanetExport = export.FileRef.Exports.First(x => x.ObjectName == "GalaxyMap_PlotPlanet");
            ExportEntry mapExport = export.FileRef.Exports.First(x => x.ObjectName == "GalaxyMap_Map");

            Bio2DA systems2DA = new Bio2DA(systemsExport);
            Bio2DA clusters2DA = new Bio2DA(clustersExport);
            Bio2DA planets2DA = new Bio2DA(export);
            Bio2DA areaMap2DA = new Bio2DA(areaMapExport);
            Bio2DA plotPlanet2DA = new Bio2DA(plotPlanetExport);
            Bio2DA levelMap2DA = new Bio2DA(mapExport);

            //These dictionaries hold the mappings between the old names and new names and will be used in the 
            //map file pass as references to these are also contained in the localized map TLKs.
            systemNameMapping = new Dictionary<string, string>();
            clusterNameMapping = new Dictionary<string, SuffixedCluster>();
            planetNameMapping = new Dictionary<string, string>();


            //Cluster Names
            int nameColumnClusters = clusters2DA.GetColumnIndexByName("Name");
            //Used for resolving %SYSTEMNAME% in planet description and localization VO text
            Dictionary<int, SuffixedCluster> clusterIdToClusterNameMap = new Dictionary<int, SuffixedCluster>();

            for (int i = 0; i < clusters2DA.RowNames.Count; i++)
            {
                int tlkRef = clusters2DA[i, nameColumnClusters].IntValue;

                string oldClusterName = "";
                oldClusterName = TLKBuilder.TLKLookupByLang(tlkRef, MELocalization.INT);
                if (oldClusterName != "No Data")
                {
                    SuffixedCluster suffixedCluster = null;
                    if (VanillaSuffixedClusterNames.Contains(oldClusterName) || suffixedClusterNamesForPreviousLookup.Contains(oldClusterName))
                    {
                        suffixedClusterNamesForPreviousLookup.Remove(oldClusterName);
                        suffixedCluster = new SuffixedCluster(suffixedClusterNames[0], true);
                        suffixedClusterNames.RemoveAt(0);
                    }
                    else
                    {
                        suffixedCluster = new SuffixedCluster(nonSuffixedClusterNames[0], false);
                        nonSuffixedClusterNames.RemoveAt(0);
                    }

                    clusterNameMapping[oldClusterName] = suffixedCluster;
                    clusterIdToClusterNameMap[int.Parse(clusters2DA.RowNames[i])] = suffixedCluster;
                    break;
                }
            }

            //SYSTEMS
            //Used for resolving %SYSTEMNAME% in planet description and localization VO text
            Dictionary<int, (SuffixedCluster clustername, string systemname)> systemIdToSystemNameMap = new Dictionary<int, (SuffixedCluster clustername, string systemname)>();


            BuildSystemClusterMap(target, option, systems2DA, systemIdToSystemNameMap, clusterIdToClusterNameMap, shuffledSystemNames);


            //BRING DOWN THE SKY (UNC) SYSTEM===================
            if (File.Exists(MERFileSystem.GetPackageFile(target, @"BIOG_2DA_UNC_GalaxyMap_X")))
            {
                var bdtsGalaxyMapX = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"BIOG_2DA_UNC_GalaxyMap_X"));
                Bio2DA bdtsGalMapX_Systems2DA = new Bio2DA(bdtsGalaxyMapX.GetUExport(6));
                var bdtstalkfile = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"DLC_UNC_GlobalTlk"));
                var bdtsTlks = bdtstalkfile.Exports.Where(x => x.ClassName == "BioTlkFile").Select(x => new ME1TalkFile(x)).ToList();
                BuildSystemClusterMap(target, option, bdtsGalMapX_Systems2DA, systemIdToSystemNameMap, clusterIdToClusterNameMap, shuffledSystemNames);
            }
            //END BRING DOWN THE SKY=====================

            //PLANETS
            //option.ProgressValue = 0;37
            //option.ProgressMax = planets2DA.RowCount;
            //option.ProgressIndeterminate = false;

            Dictionary<string, List<string>> galaxyMapGroupResources = new Dictionary<string, List<string>>();
            var resourceItems = Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(x => x.StartsWith("MassEffectRandomizer.staticfiles.galaxymapimages.")).ToList();
            var uniqueNames = new SortedSet<string>();

            //Get unique image group categories
            foreach (string str in resourceItems)
            {
                string[] parts = str.Split('.');
                if (parts.Length == 6)
                {
                    uniqueNames.Add(parts[3]);
                }
            }

            //Build group lists
            foreach (string groupname in uniqueNames)
            {
                // NEEDS UPDATED
                galaxyMapGroupResources[groupname] = resourceItems.Where(x => x.StartsWith("MassEffectRandomizer.staticfiles.galaxymapimages." + groupname)).ToList();
                galaxyMapGroupResources[groupname].Shuffle();
            }

            //BASEGAME===================================
            var rowRPIMap = new Dictionary<int, RandomizedPlanetInfo>();
            var AlreadyAssignedMustBePlayableRows = new List<int>();
            for (int i = 0; i < planets2DA.RowCount; i++)
            {
                Bio2DACell mapCell = planets2DA[i, "Map"];
                if (mapCell.IntValue > 0)
                {
                    //must be playable
                    RandomizePlanetText(target, option, planets2DA, i, "", systemIdToSystemNameMap, allMapRandomizationInfo, rowRPIMap, planetInfos, msvInfos, asteroidInfos, asteroidBeltInfos, mustBePlayable: true);
                    AlreadyAssignedMustBePlayableRows.Add(i);
                }
            }

            for (int i = 0; i < planets2DA.RowCount; i++)
            {
                if (AlreadyAssignedMustBePlayableRows.Contains(i)) continue;
                RandomizePlanetText(target, option, planets2DA, i, "", systemIdToSystemNameMap, allMapRandomizationInfo, rowRPIMap, planetInfos, msvInfos, asteroidInfos, asteroidBeltInfos);
            }
            var galaxyMapImagesBasegame = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"GUI_SF_GalaxyMap")); //lol demiurge, what were you doing?
            var ui2DAPackage = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"BIOG_2DA_UI_X")); //lol demiurge, what were you doing?
            ExportEntry galaxyMapImages2DAExport = ui2DAPackage.GetUExport(8);
            RandomizePlanetImages(target, option, rowRPIMap, planets2DA, galaxyMapImagesBasegame, galaxyMapImages2DAExport, galaxyMapGroupResources);
            UpdateGalaxyMapReferencesForTLKs(target, option, true, true); //Update TLKs.
            planets2DA.Write2DAToExport();
            //END BASEGAME===============================

            //BRING DOWN THE SKY (UNC)===================
            if (File.Exists(MERFileSystem.GetPackageFile(target, @"BIOG_2DA_UNC_GalaxyMap_X")))
            {
                var bdtsplanets = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"BIOG_2DA_UNC_GalaxyMap_X"));
                var bdtstalkfile = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"DLC_UNC_GlobalTlk"));

                Bio2DA bdtsGalMapX_Planets2DA = new Bio2DA(bdtsplanets.GetUExport(3));
                var rowRPIMapBdts = new Dictionary<int, RandomizedPlanetInfo>();
                var bdtsTlks = bdtstalkfile.Exports.Where(x => x.ClassName == "BioTlkFile").Select(x => new ME1TalkFile(x)).ToList();

                for (int i = 0; i < bdtsGalMapX_Planets2DA.RowCount; i++)
                {
                    RandomizePlanetText(target, option, bdtsGalMapX_Planets2DA, i, "UNC", systemIdToSystemNameMap, allMapRandomizationInfo, rowRPIMapBdts, planetInfos, msvInfos, asteroidInfos, asteroidBeltInfos);
                }
                var galaxyMapImagesBdts = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"GUI_SF_DLC_GalaxyMap"));
                ui2DAPackage = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"BIOG_2DA_UNC_UI_X"));
                galaxyMapImages2DAExport = ui2DAPackage.GetUExport(2);
                RandomizePlanetImages(target, option, rowRPIMapBdts, bdtsGalMapX_Planets2DA, galaxyMapImagesBdts, galaxyMapImages2DAExport, galaxyMapGroupResources);
                MERFileSystem.SavePackage(bdtsplanets);
                UpdateGalaxyMapReferencesForTLKs(target, option, true, false); //Update TLKs
                //bdtsTlks.ForEach(x => x.saveToExport(x.E)); // TODO: REIMPLEMENT
                MERFileSystem.SavePackage(bdtstalkfile);
                GalaxyMapValidationPass(target, option, rowRPIMapBdts, bdtsGalMapX_Planets2DA, new Bio2DA(galaxyMapImages2DAExport), galaxyMapImagesBdts);
            }
            //END BRING DOWN THE SKY=====================

            //PINNACE STATION (VEGAS)====================
            if (File.Exists(MERFileSystem.GetPackageFile(target, @"BIOG_2DA_Vegas_GalaxyMap_X")))
            {
                var vegasplanets = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"BIOG_2DA_Vegas_GalaxyMap_X"));
                var vegastalkfile = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"DLC_Vegas_GlobalTlk"));

                Bio2DA vegasGalMapX_Planets2DA = new Bio2DA(vegasplanets.GetUExport(2));
                var rowRPIMapVegas = new Dictionary<int, RandomizedPlanetInfo>();
                var vegasTlks = vegastalkfile.Exports.Where(x => x.ClassName == "BioTlkFile").Select(x => new ME1TalkFile(x)).ToList();

                for (int i = 0; i < vegasGalMapX_Planets2DA.RowCount; i++)
                {
                    RandomizePlanetText(target, option, vegasGalMapX_Planets2DA, i, "Vegas", systemIdToSystemNameMap, allMapRandomizationInfo, rowRPIMapVegas, planetInfos, msvInfos, asteroidInfos, asteroidBeltInfos);
                }

                var galaxyMapImagesVegas = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"GUI_SF_PRC2_GalaxyMap"));
                ui2DAPackage = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, @"BIOG_2DA_Vegas_UI_X"));
                galaxyMapImages2DAExport = ui2DAPackage.GetUExport(2);
                RandomizePlanetImages(target, option, rowRPIMapVegas, vegasGalMapX_Planets2DA, galaxyMapImagesVegas, galaxyMapImages2DAExport, galaxyMapGroupResources);
                MERFileSystem.SavePackage(vegasplanets);
                UpdateGalaxyMapReferencesForTLKs(target, option, true, false); //Update TLKs.
                //vegasTlks.ForEach(x => x.saveToExport()); //todo: renable
                MERFileSystem.SavePackage(vegastalkfile);
            }
            //END PINNACLE STATION=======================
        }

#endif

        /// <summary>
        /// Randomizes the planet-level galaxy map view. All values in this table that are float types will be randomized 
        /// </summary>
        private static void RandomizePlanetLooks(Bio2DA planet2da)
        {
            var columnNames = new[]
            {
                "Scale",
                "RingColor",
                // "OrbitRing",
                "Horizon_Atmosphere_Intensity",
                "Horizon_Atmosphere_Falloff",
                "Bump_Amount",
                "Atmosphere_Min",
                "Atmosphere_Tile_U",
                "Atmosphere_Tile_V",
                "Atmosphere_Pan_Multiplier",
                "Emissive_Twinkle_Multiplier",
                "Normal_Map_Tile",
                "City_Emissive_Tile",
                "Atmosphere_ColorR",
                "Atmosphere_ColorG",
                "Atmosphere_ColorB",
                "Atmosphere_ColorA",
                "Atmosphere_MixerR",
                "Atmosphere_MixerG",
                "Atmosphere_MixerB",
                "Atmosphere_MixerA",
                "Beach_ColorR",
                "Beach_ColorG",
                "Beach_ColorB",
                "Beach_ColorA",
                "City_Emissive_ColorR",
                "City_Emissive_ColorG",
                "City_Emissive_ColorB",
                "City_Emissive_ColorA",
                "City_Emissive_MixerR",
                "City_Emissive_MixerG",
                "City_Emissive_MixerB",
                "City_Emissive_MixerA",
                "Continent_ColorR",
                "Continent_ColorG",
                "Continent_ColorB",
                "Continent_ColorA",
                "Continent_Color_AltR",
                "Continent_Color_AltG",
                "Continent_Color_AltB",
                "Continent_Color_AltA",
                "Continent_Mask_MixerR",
                "Continent_Mask_MixerG",
                "Continent_Mask_MixerB",
                "Continent_Mask_MixerA",
                "Continent_Mask_Mixer02R",
                "Continent_Mask_Mixer02G",
                "Continent_Mask_Mixer02B",
                "Continent_Mask_Mixer02A",
                "Continent_Texture_MixerR",
                "Continent_Texture_MixerG",
                "Continent_Texture_MixerB",
                "Continent_Texture_MixerA",
                "Horizon_Atmosphere_ColorR",
                "Horizon_Atmosphere_ColorG",
                "Horizon_Atmosphere_ColorB",
                "Horizon_Atmosphere_ColorA",
                "Landmass_MixerR",
                "Landmass_MixerG",
                "Landmass_MixerB",
                "Landmass_MixerA",
                "Ocean_ColorR",
                "Ocean_ColorG",
                "Ocean_ColorB",
                "Ocean_ColorA",
                "Ocean_Color_AltR",
                "Ocean_Color_AltG",
                "Ocean_Color_AltB",
                "Ocean_Color_AltA",
                "Ocean_Texture_MixerR",
                "Ocean_Texture_MixerG",
                "Ocean_Texture_MixerB",
                "Ocean_Texture_MixerA",
                "Silt_ColorR",
                "Silt_ColorG",
                "Silt_ColorB",
                "Silt_ColorA",
                "SunColor0",
                "SunColor1",
                "SunColor2",
                "Brightness0",
                "Brightness1",
                "Brightness2",
                "Fringe_Bloom",
                "Opacity",
                "Corona_ColorR",
                "Corona_ColorG",
                "Corona_ColorB",
                "Corona_ColorA",
            };

            // Calculate the minimum and maximum values
            Dictionary<string, (float min, float max)> floatMinMax = new Dictionary<string, (float, float)>();
            Dictionary<string, (int min, int max)> intMinMax = new Dictionary<string, (int, int)>();
            for (int row = 0; row < planet2da.RowNames.Count(); row++)
            {
                foreach (var columnName in columnNames)
                {
                    // Randomize float value
                    if (planet2da[row, columnName] != null)
                    {
                        if (planet2da[row, columnName].Type == Bio2DACell.Bio2DADataType.TYPE_FLOAT)
                        {
                            var value = planet2da[row, columnName].FloatValue;
                            if (!floatMinMax.TryGetValue(columnName, out var minMax))
                            {
                                floatMinMax[columnName] = (value, value);
                            }
                            else if (minMax.min > value)
                            {
                                // Min is above our value
                                floatMinMax[columnName] = (value, minMax.max);
                            }
                            else if (minMax.max < value)
                            {
                                // Max is below our value
                                floatMinMax[columnName] = (minMax.min, value);
                            }
                        }
                        else if (planet2da[row, columnName].Type == Bio2DACell.Bio2DADataType.TYPE_INT)
                        {
                            var value = planet2da[row, columnName].IntValue;
                            if (!intMinMax.TryGetValue(columnName, out var minMax))
                            {
                                intMinMax[columnName] = (value, value);
                            }
                            else if (minMax.min > value)
                            {
                                // Min is above our value
                                intMinMax[columnName] = (value, minMax.max);
                            }
                            else if (minMax.max < value)
                            {
                                // Max is below our value
                                intMinMax[columnName] = (minMax.min, value);
                            }
                        }
                    }
                }
            }

            // Perform randomize
            for (int row = 0; row < planet2da.RowNames.Count(); row++)
            {
                foreach (var columnName in columnNames)
                {
                    // Randomize float value
                    if (planet2da[row, columnName] != null && planet2da[row, columnName].Type == Bio2DACell.Bio2DADataType.TYPE_FLOAT)
                    {
                        MERLog.Debug($"[{row}][{columnName}]({columnName}) value is {planet2da[row, columnName].FloatValue}");
                        var minMax = floatMinMax[columnName];
                        float randvalue = ThreadSafeRandom.NextFloat(minMax.min, minMax.max);
                        if (minMax.max <= 1.0)
                        {
                            // give me something different.
                            randvalue = ThreadSafeRandom.NextFloat(minMax.min, 2);
                        }
                        MERLog.Debug($"[{row}][{columnName}]({columnName}) value is {planet2da[row, columnName].FloatValue}");
                        planet2da[row, columnName].FloatValue = randvalue;
                    }
                    else if (planet2da[row, columnName] != null && planet2da[row, columnName].Type == Bio2DACell.Bio2DADataType.TYPE_INT)
                    {
                        //MERLog.Debug($"[{row}][{columnName}]({columnName}) value is {planet2da[row, columnName].FloatValue}");
                        //float randvalue = ThreadSafeRandom.NextFloat(0, 1);
                        //MERLog.Debug($"[{row}][{columnName}]({columnName}) value is {planet2da[row, columnName].FloatValue}");
                        //planet2da[row, columnName].FloatValue = randvalue;
                    }
                }
            }
        }

        public const string RANDSETTING_KEEP_PLOT_PLANET_NAMES = "RANDSETTING_KEEP_PLOT_PLANET_NAMES";
        public const string MAP_SWF_PREFIX = "galMapLE1R_";

        public static bool RewriteGalaxyMap(GameTarget target, RandomizationOption option)
        {
            var all2DAPackages = Bio2DATools.GetAll2DAPackages(target);

            // Create our overriding 2DA package
            var twoDAPath = Path.Combine(MERFileSystem.DLCModCookedPath, "BIOG_2DA_LE1R_GalaxyMap.pcc");
            var finalPackage = MEPackageHandler.CreateAndOpenPackage(twoDAPath, MEGame.LE1);

            // Setup final destination exports
            var finalGalaxyMap_Planet_Export = ExportCreator.CreateExport(finalPackage, "GalaxyMap_Planet_part", "Bio2DANumberedRows");
            var finalGalaxyMap_Cluster_Export = ExportCreator.CreateExport(finalPackage, "GalaxyMap_Cluster_part", "Bio2DANumberedRows");
            var finalGalaxyMap_System_Export = ExportCreator.CreateExport(finalPackage, "GalaxyMap_System_part", "Bio2DANumberedRows");
            var finalGalaxyMapImages_Export = ExportCreator.CreateExport(finalPackage, "Images_GalaxyMapImages_part", "Bio2DANumberedRows");

            // Load blank initials that will be merged into
            Bio2DA galaxyMap_Planet = new Bio2DA() { IsIndexed = true };
            Bio2DA galaxyMap_Cluster = new Bio2DA() { IsIndexed = true };
            Bio2DA galaxyMap_System = new Bio2DA() { IsIndexed = true };
            Bio2DA galaxyMap_Images = new Bio2DA() { IsIndexed = false }; // Check if this is accurate.

            // Merge 2DAs
            foreach (var sp in all2DAPackages)
            {
                var galMapPlanet = sp.Exports.FirstOrDefault(x => !x.IsDefaultObject && x.ClassName == "Bio2DANumberedRows" && x.ObjectName.Name.Contains("GalaxyMap_Planet"));
                if (galMapPlanet != null)
                {
                    var planet2DA = new Bio2DA(galMapPlanet);
                    planet2DA.MergeInto(galaxyMap_Planet, out _);
                }

                var galMapCluster = sp.Exports.FirstOrDefault(x => !x.IsDefaultObject && x.ClassName == "Bio2DANumberedRows" && x.ObjectName.Name.Contains("GalaxyMap_Cluster"));
                if (galMapCluster != null)
                {
                    var cluster2DA = new Bio2DA(galMapCluster);
                    cluster2DA.MergeInto(galaxyMap_Cluster, out _);
                }

                var galMapSystem = sp.Exports.FirstOrDefault(x => !x.IsDefaultObject && x.ClassName == "Bio2DANumberedRows" && x.ObjectName.Name.Contains("GalaxyMap_System"));
                if (galMapSystem != null)
                {
                    var system2DA = new Bio2DA(galMapSystem);
                    system2DA.MergeInto(galaxyMap_System, out _);
                }

                var galMapImages = sp.Exports.FirstOrDefault(x => !x.IsDefaultObject && x.ClassName == "Bio2DANumberedRows" && x.ObjectName.Name.Contains("Images_GalaxyMapImages"));
                if (galMapImages != null)
                {
                    var galaxyMapImages = new Bio2DA(galMapImages);
                    galaxyMapImages.MergeInto(galaxyMap_Images, out _);
                }
            }

            if (option.HasSubOptionSelected(RANDSETTING_KEEP_PLOT_PLANET_NAMES))
            {
                GalaxyMapRewriter = new GalaxyMapRewrite(target, option, galaxyMap_Planet, galaxyMap_Cluster, galaxyMap_System, galaxyMap_Images); // Initialize the rewrite module
            }

            // Run updates
            RandomizePlanetLooks(galaxyMap_Planet);

            GalaxyMapRewriter?.Rewrite2DAs(target);

#if HAVE_MERGE_ASIS
            // Write out the results to the destination exports
            galaxyMap_Planet.Write2DAToExport(finalGalaxyMap_Planet_Export);
            galaxyMap_Cluster.Write2DAToExport(finalGalaxyMap_Cluster_Export);
            galaxyMap_System.Write2DAToExport(finalGalaxyMap_System_Export);
            galaxyMap_Images.Write2DAToExport(finalGalaxyMapImages_Export);

            // Save the override package
            MERFileSystem.SavePackage(finalPackage);
#endif
            // Write back the 2DA data. This process is not reverseable for basegame files!!

            List<int> mergedPlanetRows = new List<int>();
            List<int> mergedSystemRows = new List<int>();
            List<int> mergedClusterRows = new List<int>();
            List<int> mergedImageRows = new List<int>();

            foreach (var sp in all2DAPackages)
            {
                // In each source package, load the original 2DA we loaded from
                // and then 
                var galMapPlanet = sp.Exports.FirstOrDefault(x => !x.IsDefaultObject && x.ClassName == "Bio2DANumberedRows" && x.ObjectName.Name.Contains("GalaxyMap_Planet"));
                if (galMapPlanet != null)
                {
                    var planet2DA = new Bio2DA(galMapPlanet);
                    mergedPlanetRows.AddRange(galaxyMap_Planet.MergeInto(planet2DA, out _, false));
                    planet2DA.Write2DAToExport();
                }

                var galMapSystem = sp.Exports.FirstOrDefault(x => !x.IsDefaultObject && x.ClassName == "Bio2DANumberedRows" && x.ObjectName.Name.Contains("GalaxyMap_System"));
                if (galMapSystem != null)
                {
                    var system2DA = new Bio2DA(galMapSystem);
                    mergedSystemRows.AddRange(galaxyMap_System.MergeInto(system2DA, out _, false));
                    system2DA.Write2DAToExport();
                }

                var galMapCluster = sp.Exports.FirstOrDefault(x => !x.IsDefaultObject && x.ClassName == "Bio2DANumberedRows" && x.ObjectName.Name.Contains("GalaxyMap_Cluster"));
                if (galMapCluster != null)
                {
                    var cluster2DA = new Bio2DA(galMapCluster);
                    mergedClusterRows.AddRange(galaxyMap_Cluster.MergeInto(cluster2DA, out _, false));
                    cluster2DA.Write2DAToExport();
                }

                var galMapImages = sp.Exports.FirstOrDefault(x => !x.IsDefaultObject && x.ClassName == "Bio2DANumberedRows" && x.ObjectName.Name.Contains("Images_GalaxyMapImages"));
                if (galMapImages != null)
                {
                    var galaxyMapImages = new Bio2DA(galMapImages);
                    mergedImageRows.AddRange(galaxyMap_Images.MergeInto(galaxyMapImages, out _, false));
                    galaxyMapImages.Write2DAToExport();
                }

                // Move package to our DLC folder
                // Disable if we ever get proper working ASI merge
                MERFileSystem.SavePackage(sp);
            }

            // Add unmerged items as new 2DAs as they are new rows - we don't want to have duplicates with Autoload.ini merge if DLC mods add stuff.
            AddUnmerged2DA(finalGalaxyMap_Planet_Export, galaxyMap_Planet, mergedPlanetRows);
            AddUnmerged2DA(finalGalaxyMap_System_Export, galaxyMap_System, mergedSystemRows);
            AddUnmerged2DA(finalGalaxyMap_Cluster_Export, galaxyMap_Cluster, mergedClusterRows);
            AddUnmerged2DA(finalGalaxyMapImages_Export, galaxyMap_Images, mergedImageRows);

            MERFileSystem.SavePackage(finalPackage);

            // Re-enable if we get proper merge working via ASI. Disable above block of code if we do.
            //CoalescedHandler.Add2DAPackage(finalGalaxyMap_Planet_Export);
            //CoalescedHandler.Add2DAPackage(finalGalaxyMap_Cluster_Export);
            //CoalescedHandler.Add2DAPackage(finalGalaxyMap_System_Export);
            //CoalescedHandler.Add2DAPackage(finalGalaxyMapImages_Export);

            return true;
        }

        private static void AddUnmerged2DA(ExportEntry targetExport, Bio2DA source2DA, List<int> rowsIndicesToNotToAdd)
        {
            var dest2DA = new Bio2DA() { IsIndexed = source2DA.IsIndexed };

            // Add columns
            foreach (var col in source2DA.ColumnNames)
            {
                dest2DA.AddColumn(col);
            }

            // Add rows
            for (int i = 0; i < source2DA.RowCount; i++)
            {
                if (rowsIndicesToNotToAdd.Contains(i))
                    continue;

                // Add the row with the original name
                var rowIdx = dest2DA.AddRow(source2DA.RowNames[i]);

                for (int colIdx = 0; colIdx < source2DA.ColumnCount; colIdx++)
                {
                    dest2DA.Cells[rowIdx, colIdx] = source2DA[i, colIdx];
                }
            }

            // Must write all empty 2DAs out to ensure binary data is set correctly.
            dest2DA.Write2DAToExport(targetExport);
            CoalescedHandler.Add2DAPackage(targetExport);
        }

        public class GalaxyMapRewrite
        {
            // LE1R
            private RandomizationOption MERRandOption;

            // 2DAs
            private Bio2DA Planets2DA;
            private Bio2DA Clusters2DA;
            private Bio2DA Systems2DA;
            private Bio2DA UI2DA;

            // Cluster name suffixing
            public List<string> SuffixedClusterNamesForPreviousLookup;
            public List<string> NonSuffixedClusterNames;
            public List<string> SuffixedClusterNames;

            // Name mapping
            private Dictionary<string, string> SystemNameMapping = new();
            private Dictionary<string, SuffixedCluster> ClusterNameMapping = new();
            private Dictionary<string, string> PlanetNameMapping = new();
            private List<string> VanillaSuffixedClusterNames;

            // Information about planet-level information
            private List<RandomizedPlanetInfo> MSVInfos = new();
            private List<RandomizedPlanetInfo> AsteroidInfos = new();
            private List<RandomizedPlanetInfo> PlanetInfos = new();
            ///
            private Dictionary<string, List<string>> GalaxyMapImageGroupResources;

            /// <summary>
            /// These IDs should not be randomized by another text randomizer
            /// </summary>
            private List<int> NoTextRandomizationTlkIds = new List<int>();


            // Randomization variables
            /// <summary>
            /// Contains the mapping of the row to its new info after assignment.
            /// </summary>
            private Dictionary<int, RandomizedPlanetInfo> PlanetRowToNewPlanetInfoMap = new Dictionary<int, RandomizedPlanetInfo>();
            /// <summary>
            /// Rows that are marked as requiring to be playable. So no touching down on super-death planets.
            /// </summary>
            private List<int> AlreadyAssignedMustBePlayableRows = new List<int>();
            /// <summary>
            /// Maps the id of a system to its new name.
            /// </summary>
            private Dictionary<int, (SuffixedCluster clustername, string systemname)> SystemIdToSystemNameMap = new();

            /// <summary>
            /// Maps the id of a cluster to its new suffixed name.
            /// </summary>
            private Dictionary<int, SuffixedCluster> ClusterIdToClusterNameMap = new();

            /// <summary>
            /// All randomized planet information.
            /// </summary>
            private List<RandomizedPlanetInfo> AllMapRandomizationInfo = new();
            /// <summary>
            /// List of all available system names for shuffling
            /// </summary>
            private List<string> ShuffledSystemNames;

            /// <summary>
            /// Initializes the Galaxy Map Rewriter class
            /// </summary>
            /// <param name="planet2DA"></param>
            /// <param name="cluster2DA"></param>
            /// <param name="system2DA"></param>
            public GalaxyMapRewrite(GameTarget target, RandomizationOption option, Bio2DA planet2DA, Bio2DA cluster2DA, Bio2DA system2DA, Bio2DA ui2DA)
            {
                MERRandOption = option;

                Planets2DA = planet2DA;
                Clusters2DA = cluster2DA;
                Systems2DA = system2DA;
                UI2DA = ui2DA;

                LoadData();
                BuildSystemAndClusterNames();
                LoadImageData(target);
            }



            public void Rewrite2DAs(GameTarget target)
            {
                // Maps a row index to its new randomized planet info. First, we run on all planets that must be considered 'playable'.
                for (int i = 0; i < Planets2DA.RowCount; i++)
                {
                    Bio2DACell mapCell = Planets2DA[i, "Map"];
                    if (mapCell.IntValue > 0)
                    {
                        //must be playable
                        RandomizePlanetText(i, mustBePlayable: true);
                        AlreadyAssignedMustBePlayableRows.Add(i);
                    }
                }

                // Now we randomize and map planets that are not considered playable - they have no map associated with them
                for (int i = 0; i < Planets2DA.RowCount; i++)
                {
                    if (AlreadyAssignedMustBePlayableRows.Contains(i)) continue;
                    RandomizePlanetText(i);
                }

                // Using results from planet text randomization we now assign map images from the image pool
                RandomizePlanetImages();

                UpdateGalaxyMapReferencesForTLKs(this, TLKBuilder.GetOfficialTLKs().ToList(), MERRandOption, true); //Update TLKs.
            }

            private void LoadImageData(GameTarget target)
            {
                MERFileSystem.InstallAlways("GalaxyMap");

                var galaxyMapPackage = MERFileSystem.OpenMEPackageTablesOnly(MERFileSystem.GetPackageFile(target, GALAXY_MAP_IMAGES_PACKAGENAME + ".pcc"));

                var allResources = galaxyMapPackage.Exports.Where(x => x.Parent == null && x.ClassName == "GFxMovieInfo");
                var set = new SortedSet<string>();
                foreach (var x in allResources)
                {
                    // Add all group names based on the syntax [groupname]_imagename
                    set.Add(x.InstancedFullPath.Substring(0, x.InstancedFullPath.IndexOf("_")));
                }


                GalaxyMapImageGroupResources = new CaseInsensitiveDictionary<List<string>>();
                //Build group lists
                foreach (var groupName in set.GetEnumerator())
                {
                    GalaxyMapImageGroupResources[groupName] = galaxyMapPackage.Exports.Where(x => x.ClassName == "GFxMovieInfo" && x.InstancedFullPath.StartsWith($"{groupName}_"))
                        .Select(x => $"{GALAXY_MAP_IMAGES_PACKAGENAME}.{x.InstancedFullPath}").ToList();
                    GalaxyMapImageGroupResources[groupName].Shuffle();
                }
            }

            private void LoadData()
            {
                string fileContents = MEREmbedded.GetEmbeddedTextAsset("GalaxyMap.PlanetInfo.xml");
                XElement rootElement = XElement.Parse(fileContents);
                AllMapRandomizationInfo = (from e in rootElement.Elements("RandomizedPlanetInfo")
                                           select new RandomizedPlanetInfo
                                           {
                                               PlanetName = (string)e.Element("PlanetName"),
                                               PlanetName2 = (string)e.Element("PlanetName2"), //Original name (plot planets only)
                                               PlanetDescription = (string)e.Element("PlanetDescription"),
                                               IsMSV = (bool)e.Element("IsMSV"),
                                               IsAsteroidBelt = (bool)e.Element("IsAsteroidBelt"),
                                               IsAsteroid = e.Element("IsAsteroid") != null && (bool)e.Element("IsAsteroid"),
                                               PreventShuffle = (bool)e.Element("PreventShuffle"),
                                               RowID = (int)e.Element("RowID"),
                                               MapBaseNames = e.Elements("MapBaseNames")
                                                   .Select(r => r.Value).ToList(),
                                               // DLC = e.Element("DLC")?.Value,
                                               ImageGroup = e.Element("ImageGroup")?.Value ?? "Generic", //TODO: TURN THIS OFF FOR RELEASE BUILD AND DEBUG ONCE FULLY IMPLEMENTED
                                               ButtonLabel = e.Element("ButtonLabel")?.Value,
                                               Playable = !(e.Element("NotPlayable") != null && (bool)e.Element("NotPlayable")),
                                           }).ToList();

                fileContents = MEREmbedded.GetEmbeddedTextAsset("GalaxyMap.ClusterInfo.xml");
                rootElement = XElement.Parse(fileContents);
                SuffixedClusterNames = rootElement.Elements("suffixedclustername").Select(x => x.Value).ToList(); //Used for assignments
                SuffixedClusterNamesForPreviousLookup = rootElement.Elements("suffixedclustername").Select(x => x.Value).ToList(); //Used to lookup previous assignments 
                VanillaSuffixedClusterNames = rootElement.Elements("originalsuffixedname").Select(x => x.Value).ToList();
                NonSuffixedClusterNames = rootElement.Elements("nonsuffixedclustername").Select(x => x.Value).ToList();
                SuffixedClusterNames.Shuffle();
                NonSuffixedClusterNames.Shuffle();

                fileContents = MEREmbedded.GetEmbeddedTextAsset("GalaxyMap.SystemInfo.xml");
                rootElement = XElement.Parse(fileContents);
                ShuffledSystemNames = rootElement.Elements("systemname").Select(x => x.Value).ToList();
                ShuffledSystemNames.Shuffle();


                var everything = new List<string>();
                everything.AddRange(SuffixedClusterNames);
                everything.AddRange(AllMapRandomizationInfo.Select(x => x.PlanetName));
                everything.AddRange(AllMapRandomizationInfo.Where(x => x.PlanetName2 != null).Select(x => x.PlanetName2));
                everything.AddRange(ShuffledSystemNames);
                everything.AddRange(NonSuffixedClusterNames);

                MSVInfos = AllMapRandomizationInfo.Where(x => x.IsMSV).ToList();
                AsteroidInfos = AllMapRandomizationInfo.Where(x => x.IsAsteroid).ToList();
                // var asteroidBeltInfos = AllMapRandomizationInfo.Where(x => x.IsAsteroidBelt).ToList();
                PlanetInfos = AllMapRandomizationInfo.Where(x => !x.IsAsteroidBelt && !x.IsAsteroid && !x.IsMSV && !x.PreventShuffle).ToList();

                MSVInfos.Shuffle();
                AsteroidInfos.Shuffle();
                PlanetInfos.Shuffle();
            }

            private void RandomizePlanetText(int tableRow, bool mustBePlayable = false)
            {
                //option.ProgressValue = i;
                int systemId = Planets2DA[tableRow, 1].IntValue;
                (SuffixedCluster clusterName, string systemName) systemClusterName = SystemIdToSystemNameMap[systemId];

                Bio2DACell descriptionRefCell = Planets2DA[tableRow, "Description"];
                int descriptionReference = descriptionRefCell?.IntValue ?? 0;

                //var rowIndex = int.Parse(planets2DA.RowNames[i]);
                var info = AllMapRandomizationInfo.FirstOrDefault(x => x.RowID == tableRow);
                if (info != null)
                {
                    if (info.IsAsteroidBelt)
                    {
                        return; //we don't care.
                    }
                    //found original info
                    RandomizedPlanetInfo rpi = null;
                    if (info.PreventShuffle)
                    {
                        //Shuffle with items of same rowindex.
                        //Todo post launch.
                        rpi = info;
                        //Do not use shuffled

                    }
                    else
                    {
                        if (info.IsMSV)
                        {
                            rpi = MSVInfos.PullFirstItem();
                        }
                        else if (info.IsAsteroid)
                        {
                            rpi = AsteroidInfos.PullFirstItem();
                        }
                        else
                        {

                            int indexPick = 0;
                            rpi = PlanetInfos[indexPick];
                            Debug.WriteLine("Assigning MustBePlayable: " + rpi.PlanetName);
                            while (!rpi.Playable && mustBePlayable) //this could error out but since we do things in a specific order it shouldn't
                            {
                                indexPick++;
                                //We need to fetch another RPI
                                rpi = PlanetInfos[indexPick];
                            }

                            PlanetInfos.RemoveAt(indexPick);
                            //if (isMap)
                            //{
                            //    Debug.WriteLine("IsMapAssigned: " + rpi.PlanetName);
                            //    numRequiredLandablePlanets--;
                            //    if (remainingLandablePlanets < numRequiredLandablePlanets)
                            //    {
                            //        Debugger.Break(); //we're gonna have a bad time
                            //    }
                            //}
                            //Debug.WriteLine("Assigning planet from pool, is playable: " + rpi.Playable);

                        }
                    }


                    PlanetRowToNewPlanetInfoMap[tableRow] = rpi; //Map row in this table to the assigned RPI
                    string newPlanetName = rpi.PlanetName;
                    if (MERRandOption.HasSubOptionSelected(RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION_PLOTPLANET) && rpi.PlanetName2 != null)
                    {
                        newPlanetName = rpi.PlanetName2;
                    }

                    //if (rename plot missions) planetName = rpi.PlanetName2
                    var description = rpi.PlanetDescription;
                    if (description != null)
                    {
                        SuffixedCluster clusterName = systemClusterName.clusterName;
                        string clusterString = systemClusterName.clusterName.ClusterName;
                        if (!clusterName.Suffixed)
                        {
                            clusterString += " cluster";
                        }
                        description = description.Replace("%CLUSTERNAME%", clusterString).Replace("%SYSTEMNAME%", systemClusterName.systemName).Replace("%PLANETNAME%", newPlanetName).TrimLines();
                    }

                    //var landableMapID = planets2DA[i, planets2DA.GetColumnIndexByName("Map")].IntValue;
                    int planetNameTlkId = Planets2DA[tableRow, "Name"].IntValue;

                    //Replace planet description here, as it won't be replaced in the overall pass
                    foreach (var tf in TLKBuilder.GetOfficialTLKs())
                    {
                        //Debug.WriteLine("Setting planet name on row index (not rowname!) " + i + " to " + newPlanetName);
                        string originalPlanetName = tf.FindDataById(planetNameTlkId, returnNullIfNotFound: true);

                        if (originalPlanetName == null)
                        {
                            continue;
                        }

                        if (!info.IsAsteroid)
                        {
                            //We don't want to do a planet mapping as this might overwrite existing text somewhere, and nothing mentions an asteroid directly.
                            PlanetNameMapping[originalPlanetName] = newPlanetName;
                        }

                        //if (originalPlanetName == "Ilos") Debugger.Break();
                        if (descriptionReference != 0 && description != null)
                        {
                            TLKBuilder.AddUpdatedTlkId(descriptionReference);
                            //Log.Information($"New planet: {newPlanetName}");
                            //if (descriptionReference == 138077)
                            //{
                            //    Debug.WriteLine($"------------SUBSTITUTING----{tf.export.ObjectName}------------------");
                            //    Debug.WriteLine($"{originalPlanetName} -> {newPlanetName}");
                            //    Debug.WriteLine("New description:\n" + description);
                            //    Debug.WriteLine("----------------------------------");
                            //    Debugger.Break(); //Xawin
                            //}
                            TLKBuilder.ReplaceString(descriptionReference, description);

                            if (rpi.ButtonLabel != null)
                            {
                                Bio2DACell actionLabelCell = Planets2DA[tableRow, "ButtonLabel"];
                                if (actionLabelCell != null)
                                {
                                    var currentTlkId = actionLabelCell.IntValue;
                                    if (tf.FindDataById(currentTlkId) != rpi.ButtonLabel)
                                    {
                                        //Value is different
                                        //try to find existing value first
                                        var tlkref = tf.FindIdByData(rpi.ButtonLabel);
                                        if (tlkref >= 0)
                                        {
                                            //We found result
                                            actionLabelCell.IntValue = tlkref;
                                        }
                                        else
                                        {
                                            // Did not find a result. Add a new string
                                            int newID = TLKBuilder.GetNewTLKID(); // WE PROBABLY NEED TO CHANGE THIS FOR DLC SYSTEM IN GAME 1.......
                                            if (newID == -1) Debugger.Break(); //hopefully we never see this, but if user runs it enough, i guess you could.
                                            tf.ReplaceString(newID, rpi.ButtonLabel);
                                            actionLabelCell.DisplayableValue = newID.ToString(); //Assign cell to new TLK ref
                                        }
                                    }
                                }
                            }
                        }

                        if (info.IsAsteroid)
                        {
                            //Since some asteroid names change and/or are shared amongst themselves, we have to add names if they don't exist.
                            if (originalPlanetName != rpi.PlanetName)
                            {
                                var newTlkValue = tf.FindIdByData(rpi.PlanetName);
                                if (newTlkValue == -1)
                                {
                                    //Doesn't exist
                                    int newId = TLKBuilder.GetNewTLKID(); // WE PROBABLY NEED TO CHANGE THIS FOR DLC SYSTEM IN GAME 1.......
                                    tf.ReplaceString(newId, rpi.PlanetName);
                                    Planets2DA[tableRow, "Name"].IntValue = newId;
                                    MERLog.Information("Assigned asteroid new TLK ID: " + newId);
                                }
                                else
                                {
                                    //Exists - repoint to that TLK value
                                    Planets2DA[tableRow, "Name"].IntValue = newTlkValue;
                                    MERLog.Information("Repointed asteroid new existing string ID: " + newTlkValue);
                                }
                            }
                        }
                    }
                }
                else
                {
                    MERLog.Error("No randomization data for galaxy map planet 2da, row id " + tableRow);
                }
            }

            private static void UpdateGalaxyMapReferencesForTLKs(GalaxyMapRewrite rewriter, List<ITalkFile> tlks, RandomizationOption option, bool updateProgressbar)
            {
                int currentTlkIndex = 0;
                // For each passed in TLK (could be multiple per package, for example)
                foreach (var tf in tlks)
                {
                    currentTlkIndex++;
                    int current = 0;
                    if (updateProgressbar)
                    {
                        option.CurrentOperation = $"Updating Galaxy Map [{currentTlkIndex}/{tlks.Count}]";
                        option.ProgressMax = tf.StringRefs.Count;
                        option.ProgressIndeterminate = false;

                        //this will only be fired on the basegame tlk's since they're the only ones that update the progerssbar.

                        //text fixes - must be done before we do a lookup of everything for replacement.
                        //TODO: CHECK IF ORIGINAL VALUE IS BIOWARE - IF IT ISN'T ITS ALREADY BEEN UPDATED.
                        string testStr = tf.FindDataById(179694);
                        if (testStr == "") // Investigate why this was written this way
                        {
                            tf.ReplaceString(179694, "Head to the Armstrong Nebula to investigate what the geth are up to."); //Remove cluster after Nebula to ensure the text pass works without cluster cluster.

                        }
                        //testStr = tf.FindDataById(156006);
                        //testStr = tf.FindDataById(136011);

                        tf.ReplaceString(156006, "Go to the Newton System in the Kepler Verge and find the one remaining scientist assigned to the secret project.");
                        tf.ReplaceString(136011, "The geth have begun setting up a number of small outposts in the Armstrong Nebula of the Skyllian Verge. You must eliminate these outposts before the incursion becomes a full-scale invasion.");
                    }

                    // Update the string data
                    //This is inefficient but not much I can do it about it.
                    foreach (var sref in tf.StringRefs)
                    {
                        current++;
                        if (TLKBuilder.UpdatedTlkStrings.Contains(sref.StringID)) continue; //This string has already been updated and should not be modified.
                        if (updateProgressbar)
                        {
                            option.ProgressValue = current;
                        }

                        if (!string.IsNullOrWhiteSpace(sref.Data))
                        {
                            string originalString = sref.Data;
                            string newString = sref.Data;
                            foreach (var planetMapping in rewriter.PlanetNameMapping)
                            {

                                //Update TLK references to this planet.
                                bool originalPlanetNameIsSingleWord = !planetMapping.Key.Contains(" ");

                                if (originalPlanetNameIsSingleWord)
                                {
                                    //This is to filter out things like Inti resulting in Intimidate
                                    if (originalString.ContainsWord(planetMapping.Key) /*&& newString.ContainsWord(planetMapping.Key)*/) //second statement is disabled as its the same at this point in execution.
                                    {
                                        //Do a replace if the whole word is matched only (no partial matches on words).
                                        newString = newString.Replace(planetMapping.Key, planetMapping.Value);
                                    }
                                }
                                else
                                {
                                    //Planets with spaces in the names won't (hopefully) match on Contains.
                                    if (originalString.Contains(planetMapping.Key) && newString.Contains(planetMapping.Key))
                                    {
                                        newString = newString.Replace(planetMapping.Key, planetMapping.Value);
                                    }
                                }
                            }


                            foreach (var systemMapping in rewriter.SystemNameMapping)
                            {
                                //Update TLK references to this system.
                                bool originalSystemNameIsSingleWord = !systemMapping.Key.Contains(" ");
                                if (originalSystemNameIsSingleWord)
                                {
                                    //This is to filter out things like Inti resulting in Intimidate
                                    if (originalString.ContainsWord(systemMapping.Key) && newString.ContainsWord(systemMapping.Key))
                                    {
                                        //Do a replace if the whole word is matched only (no partial matches on words).
                                        newString = newString.Replace(systemMapping.Key, systemMapping.Value);
                                    }
                                }
                                else
                                {
                                    //System with spaces in the names won't (hopefully) match on Contains.
                                    if (originalString.Contains(systemMapping.Key) && newString.Contains(systemMapping.Key))
                                    {
                                        newString = newString.Replace(systemMapping.Key, systemMapping.Value);
                                    }
                                }
                            }



                            //string test1 = "The geth must be stopped. Go to the Kepler Verge and stop them!";
                            //string test2 = "Protect the heart of the Artemis Tau cluster!";

                            // >> test1 Detect types that end with Verge or Nebula, or types that end with an adjective.
                            // >> >> Determine if new name ends with Verge or Nebula or other terms that have a specific ending type that is an adjective of the area. (Castle for example)
                            // >> >> >> True: Do an exact replacement
                            // >> >> >> False: Check if the match is 100% matching on the whole thing. If it is, just replace the string. If it is not, replace the string but append the word "cluster".

                            // >> test 2 Determine if cluster follows the name of the item being replaced.
                            // >> >> Scan for the original key + cluster appended.
                            // >> >> >> True: If the new item includes an ending adjective, replace the whold thing with the word cluster included.
                            // >> >> >> False: If the new item doesn't end with an adjective, replace only the exact original key.

                            foreach (var clusterMapping in rewriter.ClusterNameMapping)
                            {
                                //Update TLK references to this cluster.
                                bool originalclusterNameIsSingleWord = !clusterMapping.Key.Contains(" ");
                                if (originalclusterNameIsSingleWord)
                                {
                                    //Go to the Kepler Verge and end the threat.
                                    //Old = Kepler Verge, New = Zoltan Homeworlds
                                    if (originalString.ContainsWord(clusterMapping.Key) && newString.ContainsWord(clusterMapping.Key)) //
                                    {
                                        //Terribly inefficent
                                        if (originalString.Contains("I'm asking you because the Normandy can get on-site quickly and quietly."))
                                            Debugger.Break();
                                        if (clusterMapping.Value.SuffixedWithCluster && !clusterMapping.Value.Suffixed)
                                        {
                                            //Replacing string like Local Cluster
                                            newString = newString.ReplaceInsensitive(clusterMapping.Key + " Cluster", clusterMapping.Value.ClusterName); //Go to the Voyager Cluster and... 
                                        }
                                        else
                                        {
                                            //Replacing string like Artemis Tau
                                            newString = newString.ReplaceInsensitive(clusterMapping.Key + " Cluster", clusterMapping.Value.ClusterName + " cluster"); //Go to the Voyager Cluster and... 
                                        }

                                        newString = newString.Replace(clusterMapping.Key, clusterMapping.Value.ClusterName); //catch the rest of the items.
                                        Debug.WriteLine(newString);
                                    }
                                }
                                else
                                {
                                    if (newString.Contains(clusterMapping.Key, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        //Terribly inefficent

                                        if (clusterMapping.Value.SuffixedWithCluster || clusterMapping.Value.Suffixed)
                                        {
                                            //Local Cluster
                                            if (rewriter.VanillaSuffixedClusterNames.Contains(clusterMapping.Key, StringComparer.InvariantCultureIgnoreCase))
                                            {
                                                newString = newString.ReplaceInsensitive(clusterMapping.Key, clusterMapping.Value.ClusterName); //Go to the Voyager Cluster and... 
                                            }
                                            else
                                            {
                                                newString = newString.ReplaceInsensitive(clusterMapping.Key + " Cluster", clusterMapping.Value.ClusterName); //Go to the Voyager Cluster and... 
                                            }
                                        }
                                        else
                                        {
                                            //Artemis Tau
                                            if (rewriter.VanillaSuffixedClusterNames.Contains(clusterMapping.Key.ToLower(), StringComparer.InvariantCultureIgnoreCase))
                                            {
                                                newString = newString.ReplaceInsensitive(clusterMapping.Key, clusterMapping.Value.ClusterName + " cluster"); //Go to the Voyager Cluster and... 
                                            }
                                            else
                                            {
                                                newString = newString.ReplaceInsensitive(clusterMapping.Key + " Cluster", clusterMapping.Value.ClusterName + " cluster"); //Go to the Voyager Cluster and... 
                                            }
                                        }

                                        newString = newString.ReplaceInsensitive(clusterMapping.Key, clusterMapping.Value.ClusterName); //catch the rest of the items.
                                        Debug.WriteLine(newString);
                                    }
                                }
                            }

                            if (originalString != newString)
                            {
                                TLKBuilder.ReplaceString(sref.StringID, newString);
                                TLKBuilder.AddUpdatedTlkId(sref.StringID); // Prevents updating for the other gender language.
                            }
                        }
                    }
                }
            }

            private void BuildSystemAndClusterNames()
            {
                // Cluster Names
                int nameColumnClusters = Clusters2DA.GetColumnIndexByName("Name");
                //Used for resolving %SYSTEMNAME% in planet description and localization VO text

                for (int i = 0; i < Clusters2DA.RowNames.Count; i++)
                {
                    int tlkRef = Clusters2DA[i, nameColumnClusters].IntValue;

                    string oldClusterName;
                    foreach (ITalkFile tf in TLKBuilder.GetAllTLKs())
                    {
                        oldClusterName = tf.FindDataById(tlkRef, returnNullIfNotFound: true, noQuotes: true);
                        if (oldClusterName != null)
                        {
                            SuffixedCluster suffixedCluster = null;
                            if (VanillaSuffixedClusterNames.Contains(oldClusterName) || SuffixedClusterNamesForPreviousLookup.Contains(oldClusterName))
                            {
                                SuffixedClusterNamesForPreviousLookup.Remove(oldClusterName);
                                suffixedCluster = new SuffixedCluster(SuffixedClusterNames.PullFirstItem(), true);
                            }
                            else
                            {
                                suffixedCluster = new SuffixedCluster(NonSuffixedClusterNames.PullFirstItem(), false);
                            }

                            ClusterNameMapping[oldClusterName] = suffixedCluster;
                            ClusterIdToClusterNameMap[int.Parse(Clusters2DA.RowNames[i])] = suffixedCluster;
                            break;
                        }
                    }
                }

                //SYSTEMS
                //Used for resolving %SYSTEMNAME% in planet description and localization VO text
                BuildSystemClusterMap();

            }

            private void BuildSystemClusterMap()
            {
                int nameColumnSystems = Systems2DA.GetColumnIndexByName("Name");
                int clusterColumnSystems = Systems2DA.GetColumnIndexByName("Cluster");
                for (int i = 0; i < Systems2DA.RowNames.Count; i++)
                {

                    string newSystemName = ShuffledSystemNames.PullFirstItem();
                    int tlkRef = Systems2DA[i, nameColumnSystems].IntValue;
                    int clusterTableRow = Systems2DA[i, clusterColumnSystems].IntValue;


                    string oldSystemName;
                    foreach (var tf in TLKBuilder.GetOfficialTLKs())
                    {
                        oldSystemName = tf.FindDataById(tlkRef, returnNullIfNotFound: true);
                        if (oldSystemName != null)
                        {
                            TLKBuilder.ReplaceString(tlkRef, newSystemName);
                            SystemNameMapping[oldSystemName] = newSystemName;
                            SystemIdToSystemNameMap[int.Parse(Systems2DA.RowNames[i])] = (ClusterIdToClusterNameMap[clusterTableRow], newSystemName);
                            break;
                        }
                    }
                }
            }


            private const string GALAXY_MAP_IMAGES_PACKAGENAME = "GUI_SF_GalaxyMapImages_LE1R";



            private void RandomizePlanetImages()
            {
                MERRandOption.CurrentOperation = "Updating galaxy map images";
                MERRandOption.ProgressIndeterminate = false;


                //Get all exports for images
                var planet2daRowsWithImages = new List<int>();
                int imageIndexCol = Planets2DA.GetColumnIndexByName("ImageIndex");
                int descriptionCol = Planets2DA.GetColumnIndexByName("Description");
                int nextAddedImageIndex = int.Parse(Planets2DA.RowNames.Last()) + 100; // We increment new rows starting from here

                //var mappedRPIs = planetsRowToRPIMapping.Values.ToList();

                MERRandOption.ProgressMax = Planets2DA.RowCount;

                Debug.WriteLine("----------------DICTIONARY OF PLANET INFO MAPPINGS:============");
                foreach (var kvp in PlanetRowToNewPlanetInfoMap)
                {
                    //textBox3.Text += ("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
                    Debug.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value.PlanetName + (kvp.Value.PlanetName2 != null ? $" ({kvp.Value.PlanetName2})" : ""));
                }
                Debug.WriteLine("----------------------------------------------------------------");
                List<int> assignedImageIndexes = new List<int>(); //This is used to generate new indexes for items that vanilla share values with (like MSV ships)
                for (int i = 0; i < Planets2DA.RowCount; i++)
                {
                    MERRandOption.ProgressValue = i;
                    if (Planets2DA[i, descriptionCol] == null || Planets2DA[i, descriptionCol].IntValue == 0)
                    {
                        Debug.WriteLine("Skipping tlk -1 or blank row: (0-indexed) " + i);
                        continue; //Skip this row, its an asteroid belt (or liara's dig site)
                    }

                    //var assignedRPI = mappedRPIs[i];
                    int rowName = i;
                    //int.Parse(Planets2DA.RowNames[i]);
                    //Debug.WriteLine("Getting RPI via row #: " + Planets2DA.RowNames[i] + ", using dictionary key " + rowName);

                    if (PlanetRowToNewPlanetInfoMap.TryGetValue(rowName, out RandomizedPlanetInfo assignedRPI))
                    {

                        var hasImageResource = GalaxyMapImageGroupResources.TryGetValue(assignedRPI.ImageGroup.ToLower(), out var newImagePool);
                        if (!hasImageResource)
                        {
                            hasImageResource = GalaxyMapImageGroupResources.TryGetValue("generic", out newImagePool); //DEBUG ONLY! KIND OF?
                            MERLog.Warning("WARNING: NO IMAGEGROUP FOR GROUP " + assignedRPI.ImageGroup);
                        }
                        if (hasImageResource)
                        {
                            string imageInstancedFullPath = null;
                            if (newImagePool.Count > 0)
                            {
                                imageInstancedFullPath = newImagePool[0];
                                if (assignedRPI.ImageGroup.ToLower() != "error")
                                {
                                    //We can use error multiple times.
                                    newImagePool.RemoveAt(0);
                                }
                            }
                            else
                            {
                                Debug.WriteLine("Not enough images in group " + assignedRPI.ImageGroup + " to continue randomization. Skipping row " + rowName);
                                continue;
                            }

                            Bio2DACell imageIndexCell = Planets2DA[i, imageIndexCol];
                            bool didntIncrementNextImageIndex = false;
                            if (imageIndexCell.Type == Bio2DACell.Bio2DADataType.TYPE_NULL)
                            {
                                //Generating new cell that used to be blank - not sure if we should do this.
                                imageIndexCell.IntValue = ++nextAddedImageIndex;
                            }
                            else if (imageIndexCell.IntValue < 0 || assignedImageIndexes.Contains(imageIndexCell.IntValue))
                            {
                                //Generating new image value
                                imageIndexCell.IntValue = ++nextAddedImageIndex;
                            }
                            else
                            {
                                didntIncrementNextImageIndex = true;
                            }

                            assignedImageIndexes.Add(imageIndexCell.IntValue);
                            int uiTableRowName = imageIndexCell.IntValue;


                            //galaxyMapImagesPackage.AddExport(targetSwfExport);
                            //MERLog.Information("Cloning galaxy map SWF export. New export " + targetSwfExport.InstancedFullPath);
                            if (!UI2DA.TryGetRowIndexByName(uiTableRowName.ToString(), out var rowIndex))
                            {
                                rowIndex = UI2DA.AddRow(nextAddedImageIndex.ToString());
                            }

                            UI2DA[rowIndex, "imageResource"].NameValue = imageInstancedFullPath;
                            if (didntIncrementNextImageIndex)
                            {
                                Debug.WriteLine("Unused image? Row specified but doesn't exist in this table. Repointing to new image row for image value " + nextAddedImageIndex);
                                imageIndexCell.DisplayableValue = nextAddedImageIndex.ToString(); //assign the image cell to point to this export row
                                nextAddedImageIndex++; //next image index was not incremented, but we had to create a new export anyways. Increment the counter.
                            }
                            else
                            {
                                // Change nothing...?
                                // Need to figure out what I'm supposed to do here.
                                //Debugger.Break();

                                //imageIndexCell.DisplayableValue = nextAddedImageIndex.ToString(); // Is this right? //assign the image cell to point to this export row

                                // Old code below.
                                // It exists in the table already
                                //var swfImageExportObjectName = UI2DA[rowIndex, "imageResource"].NameValue.Name;
                                //get object name of export inside of GUI_SF_GalaxyMap
                                //swfImageExportObjectName = swfImageExportObjectName.Substring(swfImageExportObjectName.IndexOf('.') + 1); //TODO: Need to deal with name instances for Pinnacle Station DLC. Because it's too hard for them to type a new name.
                                //Fetch export

                                // Need to figure out what this was doing
                                //matchingExport = mapImageExports.FirstOrDefault(x => x.ObjectName == swfImageExportObjectName);
                            }
                        }
                        else
                        {
                            string nameTextForRow = Planets2DA[i, 5].DisplayableValue;
                            Debug.WriteLine("Skipped row: " + rowName + ", " + nameTextForRow + ", could not find imagegroup " + assignedRPI.ImageGroup);
                        }
                    }
                    else
                    {
                        string nameTextForRow = Planets2DA[i, 5].DisplayableValue;
                        Debug.WriteLine("Skipped row: " + rowName + ", " + nameTextForRow + " due to no RPI for this row.");
                    }
                }
            }
        }

        /// <summary>
        /// The current rewriter. If null, it's not in use.
        /// </summary>
        public static GalaxyMapRewrite GalaxyMapRewriter;

        public static void ResetClass()
        {
            GalaxyMapRewriter = null;
        }
    }
}
