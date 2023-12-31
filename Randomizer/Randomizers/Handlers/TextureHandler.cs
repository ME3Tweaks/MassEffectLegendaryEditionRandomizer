﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Shared.Classes;

namespace Randomizer.Randomizers.Handlers
{
    class TextureHandler
    {
        /// <summary>
        /// List of all texture randomizations
        /// </summary>
        public static List<SourceTexture> TextureRandomizations { get; private set; }

        /// <summary>
        /// This is here for optimization since we will enumerate a lot of exports
        /// </summary>
        private static List<string> GeneralRandomizerIFPs { get; set; }

        /// <summary>
        /// Contains data to copy into packages
        /// </summary>
        private static IMEPackage PremadeTexturePackage { get; set; }

        /// <summary>
        /// Opens a new TFC file for writing
        /// </summary>
        /// <param name="randomizations"></param>
        /// <param name="dlcTfcName"></param>
        public static void StartHandler(GameTarget target, List<SourceTexture> randomizations)
        {
            TextureRandomizations = randomizations;
            GeneralRandomizerIFPs = randomizations.Where(x=>!x.SpecialUseOnly).SelectMany(x => x.IFPsToBuildOff).Distinct().ToList(); // To avoid a lot of enumeration
            // PremadeTFCName: CHANGE FOR OTHER GAMES
#if __GAME1__
            //var tfcStream = MEREmbedded.GetEmbeddedAsset("Binary", $"Textures.{Randomizer.Randomizers.Game1.TextureAssets.LE1.LE1Textures.PremadeTFCName}.tfc");
            //tfcStream.WriteToFile(Path.Combine(MERFileSystem.DLCModCookedPath, $"{Randomizer.Randomizers.Game1.TextureAssets.LE1.LE1Textures.PremadeTFCName}.tfc")); // Write the embedded TFC out to the DLC folder

#elif __GAME2__
            var tfcStream = MEREmbedded.GetEmbeddedAsset("Binary", $"Textures.{Randomizer.Randomizers.Game2.TextureAssets.LE2.LE2Textures.PremadeTFCName}.tfc");
            tfcStream.WriteToFile(Path.Combine(MERFileSystem.DLCModCookedPath, $"{Randomizer.Randomizers.Game2.TextureAssets.LE2.LE2Textures.PremadeTFCName}.tfc")); // Write the embedded TFC out to the DLC folder
#elif __GAME3__
            throw new Exception("NOT IMPLEMENTED");
            var tfcStream = MEREmbedded.GetEmbeddedAsset("Binary", $"Textures.{Randomizer.Randomizers.Game2.TextureAssets.LE2.LE2Textures.PremadeTFCName}.tfc");
            tfcStream.WriteToFile(Path.Combine(MERFileSystem.DLCModCookedPath, $"{Randomizer.Randomizers.Game2.TextureAssets.LE2.LE2Textures.PremadeTFCName}.tfc")); // Write the embedded TFC out to the DLC folder

#endif
            PremadeTexturePackage = MEPackageHandler.OpenMEPackageFromStream(MEREmbedded.GetEmbeddedPackage(MERFileSystem.Game, @"Textures.PremadeImages.pcc"), @"PremadeImages.pcc");
        }

        /// <summary>
        /// Can this texture be randomized?
        /// </summary>
        /// <param name="export"></param>
        /// <returns></returns>
        private static bool CanRandomize(ExportEntry export, out string instancedFullPath)
        {
            instancedFullPath = null;
            if (export.IsDefaultObject || !export.IsTexture()) return false;
            instancedFullPath = export.InstancedFullPath;
            return GeneralRandomizerIFPs.Contains(instancedFullPath, StringComparer.InvariantCultureIgnoreCase);
        }

        public static bool RandomizeExport(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            if (!CanRandomize(export, out var instancedFullPath)) return false;
            InstallTexture(target, GetRandomTexture(instancedFullPath), export);
            return true;
        }

        /// <summary>
        /// Gets a random texture for the given IFP - cannot 
        /// </summary>
        /// <param name="instancedFullPath"></param>
        /// <returns></returns>
        private static SourceTexture GetRandomTexture(string instancedFullPath, bool allowSpecialUse = false)
        {
            var options = TextureRandomizations.Where(x => (allowSpecialUse || !x.SpecialUseOnly) && x.IFPsToBuildOff.Contains(instancedFullPath, StringComparer.InvariantCultureIgnoreCase)).ToList();
            return options.RandomElement();
        }

        /// <summary>
        /// Installs the specified r2d to the specified export. Specify an id name if you wish to use a specific id in the RTexture2D object.
        /// </summary>
        /// <param name="r2d"></param>
        /// <param name="export"></param>
        /// <param name="id"></param>
        public static void InstallTexture(GameTarget target, SourceTexture r2d, ExportEntry export)
        {
            var sourceTexToCopy = PremadeTexturePackage.FindExport(r2d.Id); // ID is the export name
#if DEBUG
            if (sourceTexToCopy == null)
            {
                Debugger.Break();
            }
#endif
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, sourceTexToCopy, export.FileRef, export, true, new RelinkerOptionsPackage(), out _);
        }

        /// <summary>
        /// Installs a new premade texture export to the specified package
        /// </summary>
        public static ExportEntry InstallNewTexture(IMEPackage package, string textureName, IEntry parent = null)
        {
            var sourceTexToCopy = PremadeTexturePackage.FindExport(textureName);
#if DEBUG
            if (sourceTexToCopy == null)
            {
                Debugger.Break();
            }
#endif
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, sourceTexToCopy, package, parent, true, new RelinkerOptionsPackage(), out var newEntry);
            return newEntry as ExportEntry;
        }

        public static void EndHandler(GameTarget target)
        {
            PremadeTexturePackage = null; // Lose reference
        }
    }

    //public class RTexture2D
    //{
    //    /// <summary>
    //    /// The full path of the memory instance. Textures that have this match will have their texture reference updated to one of the random allowed id names.
    //    /// </summary>
    //    public string TextureInstancedFullPath { get; set; }

    //    /// <summary>
    //    /// List of assets that can be used for this texture (path in exe)
    //    /// </summary>
    //    public List<string> AllowedTextureIds { get; set; }

    //    /// <summary>
    //    /// Indicates that this texture must be stored in the basegame, as it will be cached into memory before DLC mount. Use this for textures in things like Startup and SFXGame
    //    /// </summary>
    //    public bool PreMountTexture { get; set; }

    //    /// <summary>
    //    /// Cached mip storage data - only populated on first install a texture
    //    /// </summary>
    //    // public ConcurrentDictionary<string, List<MipStorage>> StoredMips { get; set; }

    //    ///// <summary>
    //    ///// Mapping of an id to it's instantiated/used data that should be placed into a usage of the texture
    //    ///// </summary>
    //    //public ConcurrentDictionary<string, (PropertyCollection props, UTexture2D texData)> InstantiatedItems = new ConcurrentDictionary<string, (PropertyCollection props, UTexture2D texData)>();

    //    public string FetchRandomTextureId()
    //    {
    //        return AllowedTextureIds[ThreadSafeRandom.Next(AllowedTextureIds.Count)];
    //    }

    //    /// <summary>
    //    /// Resets this RTexture2D, dropping the instantiatetd list
    //    /// </summary>
    //    public void Reset()
    //    {
    //        TextureRan
    //    }
    //}
}
