﻿using System.IO;
using System.Reflection;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Randomizer.MER;

namespace Randomizer.Randomizers.Game2.ExportTypes
{
    class RTextureMovie
    {
        //private static bool CanRandomize(ExportEntry export) => !export.IsDefaultObject && export.ClassName == @"TextureMovie" && ExportNameToAssetMapping.ContainsKey(export.ObjectName.Name);
        //public static bool RandomizeExport(ExportEntry export, RandomizationOption option)
        //{
        //    if (!CanRandomize(export)) return false;
        //    var assets = ExportNameToAssetMapping[export.ObjectName.Name];
        //    byte[] tmAsset = GetTextureMovieAssetBinary(assets[ThreadSafeRandom.Next(assets.Count)]);
        //    var tm = ObjectBinary.From<TextureMovie>(export);
        //    tm.EmbeddedData = tmAsset;
        //    tm.DataSize = tmAsset.Length;
        //    export.WriteBinary(tm);
        //    return true;
        //}

        // ME2 only has few texture movies so these are used
        public static bool RandomizeExportDirect(ExportEntry export, RandomizationOption option, byte[] tmAsset)
        {
            var tm = ObjectBinary.From<TextureMovie>(export);
            tm.EmbeddedData = tmAsset;
            tm.DataSize = tmAsset.Length;
            export.WriteBinary(tm);
            return true;
        }

        //        private static Dictionary<string, List<string>> ExportNameToAssetMapping;

        //        public static void SetupOptions()
        //        {
        //#if __ME2__
        //            ExportNameToAssetMapping = new Dictionary<string, List<string>>()
        //            {
        //                { "ProFre_501_VeetorFootage", new List<string>()
        //                    {
        //                        "Veetor.size_mer.bik"
        //                    }
        //                }
        //            };
        //#elif __ME3__

        //#endif

        /// <summary>
        /// Gets binary data for a file in the TextureMovie asset folder
        /// </summary>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public static byte[] GetTextureMovieAssetBinary(string assetName)
        {
            var item = MEREmbedded.GetEmbeddedAsset("TextureMovie", assetName);
            byte[] ba = new byte[item.Length];
            item.Read(ba, 0, ba.Length);
            return ba;
        }
    }
}