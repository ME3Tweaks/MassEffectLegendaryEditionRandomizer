﻿using System.Linq;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Targets;

namespace Randomizer.Randomizers.Game2.Misc
{
    class SkipMiniGames
    {
        private static bool CanApplySkip(ExportEntry export, out EMinigameSkipType skipType)
        {
            skipType = EMinigameSkipType.Invalid;
            return !export.IsDefaultObject && IsMiniGameRef(export, out skipType);
        }

        private static bool IsMiniGameRef(ExportEntry export, out EMinigameSkipType skipType)
        {
            skipType = EMinigameSkipType.Invalid;

            if (export.ClassName == "BioSeqAct_SkillGame_Decryption" || export.ClassName == "BioSeqAct_SkillGame_Bypass")
            {
                skipType = EMinigameSkipType.SeqAct;
                return true;
            }

            if (export.ClassName == "SequenceReference")
            {
                var sRef = export.GetProperty<ObjectProperty>("oSequenceReference");
                if (sRef != null && export.FileRef.TryGetUExport(sRef.Value, out var referencedItem))
                {
                    var objName = referencedItem.GetProperty<StrProperty>("ObjName");
                    if (objName != null)
                    {
                        skipType = EMinigameSkipType.SeqRef;
                        if (objName == "REF_SkillGame_Bypass") return true;
                        if (objName == "REF_SkillGame_Decryption") return true;
                        if (objName == "REF_SkillGame_Hack") return true;
                    }
                }
            }

            return false;
        }

        private enum EMinigameSkipType
        {
            Invalid,
            SeqRef,
            SeqAct
        }

        public static bool DetectAndSkipMiniGameSeqRefs(GameTarget target, ExportEntry exp, RandomizationOption option)
        {
            if (!CanApplySkip(exp, out var miniGameType)) return false;

            if (miniGameType == EMinigameSkipType.SeqRef)
            {
                // Update the credits
                var minigameVarLinks = KismetHelper.GetVariableLinksOfNode(exp);
                // Update the Out: Value Remaining to something random.
                var ovrNode = minigameVarLinks.FirstOrDefault(x => x.LinkDesc == "OUT: Value Remaining")?.LinkedNodes.FirstOrDefault();
                if (ovrNode is ExportEntry ovr)
                {
                    ovr.WriteProperty(new IntProperty(ThreadSafeRandom.Next(1, 2400), "IntValue"));
                }
            }
            else if (miniGameType == EMinigameSkipType.SeqAct)
            {
                // Update the credits
                var minigameVarLinks = KismetHelper.GetVariableLinksOfNode(exp);
                // Update the Remaining Remaining to something random.
                var ovrNode = minigameVarLinks.FirstOrDefault(x => x.LinkDesc == "Remaining Resources")?.LinkedNodes.FirstOrDefault();
                if (ovrNode is ExportEntry ovr)
                {
                    ovr.WriteProperty(new IntProperty(ThreadSafeRandom.Next(1, 2400), "IntValue"));
                }
            }

            KismetHelper.SkipSequenceElement(exp, "Success"); //Success is link 0
            return true;
        }
    }
}
