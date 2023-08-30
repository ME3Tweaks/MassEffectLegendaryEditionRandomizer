using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace Randomizer.Shared
{
    /// <summary>
    /// Tools for interacting with a level
    /// </summary>
    public static class MERLevelTools
    {
        /// <summary>
        /// Adds or returns the LevelStreamingKismet object for the given level name
        /// </summary>
        /// <param name="targetPackage"></param>
        /// <param name="levelName"></param>
        /// <returns></returns>
        public static ExportEntry AddLevelStreamingKismet(IMEPackage targetPackage, NameReference levelName)
        {

            // See if already in file
            ExportEntry lsk = null;
            foreach (var exp in targetPackage.Exports.Where(x =>
                         !x.IsDefaultObject && x.ClassName == "LevelStreamingKismet"))
            {
                var levelNameProp = exp.GetProperty<NameProperty>("PackageName");
                if (levelNameProp == null)
                    continue;
                if (levelNameProp.Value == levelName)
                {
                    lsk = exp;
                    break;
                }
            }

            if (lsk == null)
            {
                var parent = targetPackage.FindExport("TheWorld");
                lsk = ExportCreator.CreateExport(targetPackage, "LevelStreamingKismet", "LevelStreamingKismet", parent);
                lsk.WriteProperty(new NameProperty(levelName));
            }

            var bwi = targetPackage.Exports.FirstOrDefault(x => !x.IsDefaultObject && x.ClassName == "BioWorldInfo");
            if (bwi == null)
            {
                Debugger.Break(); // Stuffs broken
            }

            var streamingLevels = bwi.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels");
            if (streamingLevels == null)
            {
                streamingLevels = new ArrayProperty<ObjectProperty>("StreamingLevels");
            }

            if (streamingLevels.All(x => x.Value != lsk.UIndex))
            {
                streamingLevels.Add(new ObjectProperty(lsk));
            }

            bwi.WriteProperty(streamingLevels);

            return lsk;
        }
    }
}
