using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using Randomizer.Shared;

namespace Randomizer.Randomizers.Shared
{
    internal class RSharedCutscene
    {
        private static bool CanRandomize(ExportEntry export, out string cutsceneName)
        {
            cutsceneName = null;
            if (!export.IsDefaultObject && export.ClassName == "SeqAct_Interp" && SeqTools.GetParentSequence(export)?.ObjectName is { } strp && strp.Instanced.StartsWith("ANIMCUTSCENE_"))
            {
                cutsceneName = strp;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The inputs we filter for randomizing interps with
        /// </summary>
        private static readonly List<int> INTERP_PLAY_INPUT_IDXS = new() { 0 };

        /// <param name="export"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static bool ShuffleCutscenePawns(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            if (!CanRandomize(export, out var cutsceneName)) return false;
            var sequence = SeqTools.GetParentSequence(export);

            // Add our randomizer node
            var randNextNode = MERSeqTools.CreateAndAddToSequence(sequence, @"MERSeqAct_RandomizePawnsInNextNode");

            var sequenceObjects = SeqTools.GetAllSequenceElements(sequence).OfType<ExportEntry>();
            var incomingExports = SeqTools.FindOutboundConnectionsToNode(export, sequenceObjects, INTERP_PLAY_INPUT_IDXS);

            foreach (var incoming in incomingExports)
            {
                // Repoint to our randomization node
                var outboundsFromPrevNode = SeqTools.GetOutboundLinksOfNode(incoming);
                foreach (var outLink in outboundsFromPrevNode)
                {
                    foreach (var linkedNode in outLink)
                    {
                        // Play is Input 0
                        if (linkedNode.InputLinkIdx == 0 && linkedNode.LinkedOp == export)
                        {
                            linkedNode.LinkedOp = randNextNode;
                        }
                    }
                }
                SeqTools.WriteOutboundLinksToNode(incoming, outboundsFromPrevNode);
            }

            KismetHelper.CreateOutputLink(randNextNode, "Randomized", export);
            // Cutscene does not use the Reset Input/output pins
            return true;
        }
    }
}
