using Randomizer.MER;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Targets;
using Randomizer.Randomizers.Utility;
using Randomizer.Shared;

namespace Randomizer.Randomizers.Game2.Misc
{
    internal static class SharedLE2Fixes
    {
        public static void ResetClass()
        {
            InstalledLegionHereticChessFix = false;
        }

        private const string PowerUsageFixName = "Startup_LE2R_PowerUsageFixes";
        internal static void InstallPowerUsageFixes()
        {
            var startup = MEREmbedded.GetEmbeddedPackage(MEGame.LE2, $@"Powers.{PowerUsageFixName}.pcc");
            MERFileSystem.SaveStreamToDLC(startup, $"{PowerUsageFixName}.pcc");

            // Startup for override
            ThreadSafeDLCStartupPackage.AddStartupPackage(PowerUsageFixName);
        }

        public static bool InstalledLegionHereticChessFix;
        internal static void InstallLegionHereticChessFix(GameTarget target)
        {
            // This block is here in the event that LE2 Unofficial Patch ships this file, we don't want to duplicate it and potentially also break the file
            // This will require update to LE2R
            if (!InstalledLegionHereticChessFix)
                //|| !target.GetInstalledDLC().Contains("DLC_MOD_LE2UnofficialPatch"))
            {
                // Prevent softlock during Legion's Heretic Chess(tm) due to bad spawn logic
                var hereticChessP = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, "BioD_BlbGtl_201CoreRoom.pcc"));
                foreach (var v in hereticChessP.Exports.Where(x => !x.IsDefaultObject && x.ClassName == "SFXSeqAct_AIFactory"))
                {
                    // Do not abort on spawn
                    v.WriteProperty(new BoolProperty(false, "bAbortOnFailSpawn")); // Will fix Wave1 failed and help ensure enemies all spawn as they should
                }

                // Change BioWare's failsafe code to actually work
                var failedWave3 = hereticChessP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Spawn_Sequence.Wave3.SeqEvent_SequenceActivated_1"); // Wave 2 failed
                var failedWave4 = hereticChessP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Spawn_Sequence.wave4.SeqEvent_SequenceActivated_2"); // Wave 3 failed
                var failedWavesDone = hereticChessP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Spawn_Sequence.Waves_Done.SeqEvent_SequenceActivated_3"); // Wave 4 failed

                var failedGate3 = MERSeqTools.CreateGate(SeqTools.GetParentSequence(failedWave3));
                var failedGate4 = MERSeqTools.CreateGate(SeqTools.GetParentSequence(failedWave4));
                var failedGate5 = MERSeqTools.CreateGate(SeqTools.GetParentSequence(failedWavesDone));

                // Failed goes to Failsafe logic instead of skip
                MERSeqTools.ChangeOutlink(failedWave3, 0, 0, failedGate3.UIndex);
                MERSeqTools.ChangeOutlink(failedWave4, 0, 0, failedGate4.UIndex);
                MERSeqTools.ChangeOutlink(failedWavesDone, 0, 0, failedGate5.UIndex);

                // Standard failsafe logic for waves 3 and 4
                KismetHelper.CreateOutputLink(failedGate3, "Out", failedGate3, 2);
                KismetHelper.CreateOutputLink(failedGate3, "Out", hereticChessP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Spawn_Sequence.Wave3.SeqCond_CompareBool_2"));
                KismetHelper.CreateOutputLink(failedGate4, "Out", failedGate4, 2);
                KismetHelper.CreateOutputLink(failedGate4, "Out", hereticChessP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Spawn_Sequence.wave4.SeqCond_CompareBool_4"));

                // WavesDone needs to listen for deaths since it doesn't have a loop
                MERSeqTools.ChangeOutlink(failedWavesDone, 0, 0, failedGate5.UIndex);

                // Do an initial check to see if no-one is alive
                // Turn on death listers so if someone is alive and they die it retries
                KismetHelper.CreateOutputLink(failedGate5, "Out", failedGate5, 2);
                KismetHelper.CreateOutputLink(failedGate5, "Out", hereticChessP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Spawn_Sequence.Waves_Done.SequenceReference_4"));
                KismetHelper.CreateOutputLink(failedGate5, "Out", hereticChessP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Spawn_Sequence.Waves_Done.SeqAct_Toggle_5"));

                MERFileSystem.SavePackage(hereticChessP);
                InstalledLegionHereticChessFix = true;
            }
        }
    }
}
