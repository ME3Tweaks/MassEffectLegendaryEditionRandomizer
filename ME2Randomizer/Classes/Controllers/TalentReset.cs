﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME2Randomizer.Classes.ME2SaveEdit.FileFormats;
using ME3ExplorerCore.Misc;

namespace ME2Randomizer.Classes.Controllers
{
    class Career
    {
        public ObservableCollectionExtended<SaveFile> SaveFiles { get; } = new ObservableCollectionExtended<SaveFile>();
    }

    class TalentReset
    {
        public static void GetSaveFiles()
        {
            var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BioWare", "Mass Effect 2", "Save");

            var saveDirs = Directory.GetDirectories(savePath);
            Dictionary<string, List<SaveFile>> charNameCareers = new Dictionary<string, List<SaveFile>>();
            foreach (var saveDir in saveDirs)
            {
                foreach (var sf in Directory.GetFiles(saveDir, "*.pcsav"))
                {
                    using var saveFileS = File.OpenRead(sf);
                    var saveFile = SaveFile.Load(saveFileS);
                    if (!charNameCareers.TryGetValue(saveFile.PlayerRecord.FirstName, out var list))
                    {
                        list = new List<SaveFile>();
                        charNameCareers[saveFile.PlayerRecord.FirstName] = list;
                    }

                    list.Add(saveFile);
                }
            }

            Debug.WriteLine("OK");
            //                saveFile.HenchmanRecords.Clear(); // Get rid of henchmen records... will this affect their loadout?
            /*            var playerLevel = saveFile.PlayerRecord.Level;


                        // CLEAR PLAYER POWERS
                        foreach (var pp in saveFile.PlayerRecord.Powers)
                        {
                            // Need to figure out a way to only remove powers and leave things like first aid.
                        }


                        // CLEAR SQUADMATE POWERS
                        foreach (var hm in saveFile.HenchmanRecords)
                        {
                            var numTalentPoints = GetNumTalentPoints(playerLevel, false, true, hm.Tag == "hench_vixen" || hm.Tag == "hench_leading");
                            hm.TalentPoints = numTalentPoints;
                            hm.Powers.Clear(); // Wipe out the talents list so game has to rebuild it on load
                        }*/
        }

        /// <summary>
        /// Calculates the number of spendable points that should be assigned as if no points were allocated
        /// </summary>
        /// <param name="playerLevel"></param>
        /// <param name="isPlayer"></param>
        /// <returns></returns>
        private static int GetNumTalentPoints(int playerLevel, bool isPlayer, bool asIfWiped, bool isBoostedSquadmate = false)
        {
            // Level 30:
            // Player: 48 (+2 assigned at game start, +1 for bonus power)
            // Squadmates: 29 (+1 assigned at start, +2 for jacob/miranda)

            int numPoints = 0;

            // Set as if all powers except bonus was wiped out, so we get full refund
            if (asIfWiped)
            {
                if (isPlayer)
                {
                    numPoints = 2; // Bonus power is not wiped
                }
                else if (isBoostedSquadmate)
                {
                    numPoints = 2;
                }
                else
                {
                    numPoints = 1;
                }
            }

            for (int i = 1; i < 30; i++) // i = 1 at start. This way it lines up with level up which only starts at 2
            {
                if (isPlayer)
                {
                    numPoints += i < 21 ? 2 : 1;
                }
                else
                {
                    if (i < 6)
                    {
                        numPoints += 2;
                    }
                    else if (i < 21)
                    {
                        if (i % 2 == 1)
                        {
                            numPoints += 2; // every other level
                        }
                    }
                    else if (i < 30)
                    {
                        if (i % 2 == 1)
                        {
                            numPoints += 1;
                        }
                    }
                    else if (i == 30)
                    {
                        numPoints += 1;
                    }
                }
            }
            return numPoints;
        }
    }
}