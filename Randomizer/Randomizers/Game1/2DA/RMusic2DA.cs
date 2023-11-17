using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksCore.Targets;
using Randomizer.MER;

namespace Randomizer.Randomizers.Game1._2DA
{
    /// <summary>
    /// Randomizes Music 2DA tables
    /// </summary>
    class RMusic2DA
    {
        private static bool CanRandomize(ExportEntry export) => !export.IsDefaultObject && export.ClassName == @"Bio2DA" && export.ObjectName.Name.StartsWith("Music_Music");

        private static List<string> AllMusicCues = new();

        public static void ResetClass()
        {
            AllMusicCues = new();
        }


        /// <summary>
        /// Randomizes the sound cues in the music table.
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        public static bool RandomizeMusic2DAs(GameTarget target, RandomizationOption option)
        {
            // Get list of all music options
            var allMusicPackages = MERFileSystem.LoadedFiles.Where(x => Path.GetExtension(x.Key) == ".pcc" && x.Key.StartsWith("Music"));
            List<string> soundCueIFPs = new List<string>(); // Pool of music sound cues

            foreach (var file in allMusicPackages)
            {
                var p = MERFileSystem.OpenMEPackage(file.Value);
                foreach (var exp in p.Exports.Where(x =>
                             !x.IsDefaultObject && x.ClassName == "SoundCue" &&
                             x.GetProperty<NameProperty>("SoundGroup")?.Value.Name == "Music"))
                {
                    soundCueIFPs.Add($"{p.FileNameNoExtension}.{exp.InstancedFullPath}");
                }
            }

            // Load all music 2DAs and change them
            // MusicResource is for GUIMusic table.
            string[] colsToRandomize = { "MusicResource", "SoundCue", "1", "2", "3", "4", "5", "6", "7", "8" };

            var all2DAPackages = Bio2DATools.GetAll2DAPackages(target);
            foreach (var package in all2DAPackages)
            {
                var music2DAExps = package.Exports.Where(x => !x.IsDefaultObject && x.ClassName == "Bio2DA" && x.ObjectName.Name.Contains("Music_Music") ||
                                                                      x.ObjectName.Name.Contains("UISounds_GuiMusic"));
                foreach(var music2DAExp in music2DAExps)
                {
                    Bio2DA music2da = new Bio2DA(music2DAExp);
                    for (int row = 0; row < music2da.RowNames.Count; row++)
                    {
                        foreach (var col in colsToRandomize)
                        {
                            if (music2da.TryGetColumnIndexByName(col, out var colIndex))
                            {
                                if (music2da[row, colIndex].Type != Bio2DACell.Bio2DADataType.TYPE_NULL && music2da[row, colIndex].NameValue != "MUSIC_NONE")
                                {
                                    music2da[row, colIndex].NameValue = soundCueIFPs.RandomElement();
                                }
                            }
                        }
                    }

                    music2da.Write2DAToExport();
                }
             
                // Won't save if not modified
                MERFileSystem.SavePackage(package);
            }

            return true;
        }
    }
}
