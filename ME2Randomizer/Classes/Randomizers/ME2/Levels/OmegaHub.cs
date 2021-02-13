﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME2Randomizer.Classes.Randomizers.ME2.ExportTypes;
using ME2Randomizer.Classes.Randomizers.Utility;
using ME3ExplorerCore.Dialogue;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Kismet;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;

namespace ME2Randomizer.Classes.Randomizers.ME2.Levels
{
    public static class OmegaHub
    {
        private static string[] danceKeywords = new[] { "Dancing", "Dismiss", "Across_The_Throat", "Begging", "Sexy", "ROM", };
        public readonly static string[] notDanceKeywords = new[] { "Idle", "Base", "Standing", "Twitch", };

        private static void RandomizeVIPShepDance()
        {
            var vipLoungeLF = MERFileSystem.GetPackageFile(@"BioD_OmgHub_500DenVIP_LOC_INT.pcc");
            if (vipLoungeLF != null && File.Exists(vipLoungeLF))
            {
                var vipLounge = MEPackageHandler.OpenMEPackage(vipLoungeLF);

                var playerDanceInterpData = vipLounge.GetUExport(547);
                var c = new MERPackageCache();

                InstallShepardDanceGesture(playerDanceInterpData, c); // Paragon
                InstallShepardDanceGesture(vipLounge.GetUExport(559), c); // Stupid shep lol


                // Make able to dance again and again in convo
                var danceTalk = vipLounge.GetUExport(217);
                var bc = new ConversationExtended(danceTalk);
                bc.LoadConversation(null);
                bc.StartingList.Clear();
                bc.StartingList.Add(0, 2);
                bc.SerializeNodes();

                MERFileSystem.SavePackage(vipLounge);
            }

            // make able to always talk to dancer
            var vipLoungeF = MERFileSystem.GetPackageFile(@"BioD_OmgHub_500DenVIP.pcc");
            if (vipLoungeF != null && File.Exists(vipLoungeF))
            {
                var vipLounge = MEPackageHandler.OpenMEPackage(vipLoungeF);
                var selectableBool = vipLounge.GetUExport(8845);
                selectableBool.WriteProperty(new IntProperty(1, "bValue"));
                MERFileSystem.SavePackage(vipLounge);
            }
        }


        private static void RandomizeAfterlifeShepDance()
        {
            var denDanceF = MERFileSystem.GetPackageFile(@"BioD_OmgHub_230DenDance.pcc");
            if (denDanceF != null)
            {
                var loungeP = MEPackageHandler.OpenMEPackage(denDanceF);
                var sequence = loungeP.GetUExport(3924);

                MERPackageCache cache = new MERPackageCache();
                List<InterpTools.InterpData> interpDatas = new List<InterpTools.InterpData>();
                var interp1 = loungeP.GetUExport(3813);

                // Make 2 additional dance options by cloning the interp and the data tree
                var interp2 = SeqTools.CloneBasicSequenceObject(interp1);
                var interp3 = SeqTools.CloneBasicSequenceObject(interp1);


                // Clone the interp data for attaching to 2 and 3
                var interpData1 = loungeP.GetUExport(1174);
                var interpData2 = EntryCloner.CloneTree(interpData1);
                var interpData3 = EntryCloner.CloneTree(interpData2);
                KismetHelper.AddObjectToSequence(interpData2, sequence);
                KismetHelper.AddObjectToSequence(interpData3, sequence);

                // Load ID for randomization
                interpDatas.Add(new InterpTools.InterpData(interpData1));
                interpDatas.Add(new InterpTools.InterpData(interpData2));
                interpDatas.Add(new InterpTools.InterpData(interpData3));


                // Chance the data for interp2/3
                var id2 = SeqTools.GetVariableLinksOfNode(interp2);
                id2[0].LinkedNodes[0] = interpData2;
                SeqTools.WriteVariableLinksToNode(interp2, id2);

                var id3 = SeqTools.GetVariableLinksOfNode(interp3);
                id3[0].LinkedNodes[0] = interpData3;
                SeqTools.WriteVariableLinksToNode(interp3, id3);

                // Add additional finished states for fadetoblack when done
                KismetHelper.CreateOutputLink(loungeP.GetUExport(958), "Finished", interp2, 2);
                KismetHelper.CreateOutputLink(loungeP.GetUExport(958), "Finished", interp3, 2);


                // Link up the random choice it makes
                var randSw = SeqTools.InstallRandomSwitchIntoSequence(sequence, 3);
                KismetHelper.CreateOutputLink(randSw, "Link 1", interp1);
                KismetHelper.CreateOutputLink(randSw, "Link 2", interp2);
                KismetHelper.CreateOutputLink(randSw, "Link 3", interp3);

                // Break the original output to start the interp, repoint it's output to the switch instead
                var sgm = loungeP.GetUExport(1003); //set gesture mode
                KismetHelper.RemoveOutputLinks(sgm);
                KismetHelper.CreateOutputLink(sgm, "Done", loungeP.GetUExport(960));
                KismetHelper.CreateOutputLink(sgm, "Done", randSw);

                // Now install the dances
                foreach (var id in interpDatas)
                {
                    var danceTrack = id.InterpGroups[0].Tracks[0];
                    OmegaHub.InstallShepardDanceGesture(danceTrack.Export, cache);
                }

                MERFileSystem.SavePackage(loungeP);
            }
        }

        public static bool InstallShepardDanceGesture(ExportEntry danceTrackExp, MERPackageCache cache)
        {
            cache ??= new MERPackageCache();

            var danceGestureData = RBioEvtSysTrackGesture.GetGestures(danceTrackExp);
            var newGestures = new List<RBioEvtSysTrackGesture.Gesture>();

            int i = danceGestureData.Count + 1; // The default pose is the +1
            while (i > 0)
            {
                newGestures.Add(RBioEvtSysTrackGesture.InstallRandomFilteredGestureAsset(danceTrackExp.FileRef, 6, danceKeywords, notDanceKeywords, null, true, cache));
                i--;
            }

            for (int k = 0; k < danceGestureData.Count; k++)
            {
                danceGestureData[k] = newGestures.PullFirstItem();
            }
            RBioEvtSysTrackGesture.WriteGestures(danceTrackExp, danceGestureData);

            // Update the default pose
            RBioEvtSysTrackGesture.WriteDefaultPose(danceTrackExp, newGestures.PullFirstItem());

            return true;
        }

        internal static bool PerformRandomization(RandomizationOption notUsed)
        {
            RandomizeVIPShepDance();
            RandomizeAfterlifeShepDance();
            return true;
        }
    }
}