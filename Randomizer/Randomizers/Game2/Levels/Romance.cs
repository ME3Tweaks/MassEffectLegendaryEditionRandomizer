using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Shared;

namespace Randomizer.Randomizers.Game2.Levels
{
    class Romance
    {
        public static bool PerformRandomization(GameTarget target, RandomizationOption option)
        {
            RandomizeRomance(target);
            return true;
        }


        /// <summary>
        /// Technically this is not part of Nor (It's EndGm). But it takes place on normandy so users
        /// will think it is part of the normandy.
        /// </summary>
        /// <param name="random"></param>
        private static void RandomizeRomance(GameTarget target)
        {

            // Romance is 2 pass: 

            // Pass 1: The initial chances that are not ME1 or Miranda
            {
                var romChooserPackage = MEPackageHandler.OpenMEPackage(MERFileSystem.GetPackageFile(target, "BioD_EndGm1_110Romance.pcc"));
                var romSeq = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Romance_Culminations.Load_and_Activate_Romance_Content");
                var outToRepoint = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Romance_Culminations.Load_and_Activate_Romance_Content.SeqAct_Log_6"); //repoint to our switch

                // Install random switch and point it at the romance log culminations for each
                // Miranda gets 2 as she has a 50/50 of miranda or lonely shep.
                var randomSwitch = MERSeqTools.CreateRandSwitch(romSeq, 7);
                var outLinks = KismetHelper.GetOutputLinksOfNode(randomSwitch);

                outLinks[0].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Romance_Culminations.Load_and_Activate_Romance_Content.BioSeqAct_SetStreamingState_7") }); // JACOB
                outLinks[1].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Romance_Culminations.Load_and_Activate_Romance_Content.BioSeqAct_SetStreamingState_10") }); // GARRUS
                outLinks[2].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Romance_Culminations.Load_and_Activate_Romance_Content.BioSeqAct_SetStreamingState_9") }); // TALI
                outLinks[3].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Romance_Culminations.Load_and_Activate_Romance_Content.BioSeqAct_SetStreamingState_8") }); // THANE
                outLinks[4].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Romance_Culminations.Load_and_Activate_Romance_Content.BioSeqAct_SetStreamingState_6") }); // JACK
                outLinks[5].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Romance_Culminations.Load_and_Activate_Romance_Content.SeqAct_Delay_4") }); // MIRANDA--| -> Delay into teleport
                outLinks[6].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Romance_Culminations.Load_and_Activate_Romance_Content.SeqAct_Delay_4") }); // ME1------| -> Delay into teleport

                KismetHelper.WriteOutputLinksToNode(randomSwitch, outLinks);

                // Repoint to our randomswitch
                var penultimateOutbound = KismetHelper.GetOutputLinksOfNode(outToRepoint);
                penultimateOutbound[0].Clear();
                penultimateOutbound[0].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = randomSwitch });
 
                // DEBUG ONLY: FORCE LINK
                //penultimateOutbound[0].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.GetUExport(27) });
                KismetHelper.WriteOutputLinksToNode(outToRepoint, penultimateOutbound);

                MERFileSystem.SavePackage(romChooserPackage);
            }

            // Pass 2: ME1 or Miranda if Pass 1 fell through at runtime
            {
                var romChooserPackage = MEPackageHandler.OpenMEPackage(MERFileSystem.GetPackageFile(target, "BioD_EndGm1_110ROMMirranda.pcc"));
                var romSeq = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Introduction.Intro_Cutscene_Bridge");
                var outToRepoint = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Introduction.Intro_Cutscene_Bridge.BioSeqAct_ModifyPropertyPawn_0"); //repoint to our switch

                // Install random switch and point it at the romance log culminations for each
                // Miranda gets 2 as she has a 50/50 of miranda or lonely shep.
                var randomSwitch = MERSeqTools.CreateRandSwitch(romSeq, 2);
                var outLinks = KismetHelper.GetOutputLinksOfNode(randomSwitch);

                outLinks[0].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Introduction.Intro_Cutscene_Bridge.SeqAct_Log_0") }); // MIRANDA
                outLinks[1].Add(new OutputLink() { InputLinkIdx = 0, LinkedOp = romChooserPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.SEQ_Endgame_Introduction.Intro_Cutscene_Bridge.SeqAct_Delay_1") }); // ME1

                KismetHelper.WriteOutputLinksToNode(randomSwitch, outLinks);

                // Repoint to our randomswitch
                var penultimateOutbound = KismetHelper.GetOutputLinksOfNode(outToRepoint);
                penultimateOutbound[0].Clear();
                penultimateOutbound[0].Add(new OutputLink() {InputLinkIdx = 0, LinkedOp = randomSwitch});
                KismetHelper.WriteOutputLinksToNode(outToRepoint, penultimateOutbound);

                MERFileSystem.SavePackage(romChooserPackage);
            }
        }
    }
}
