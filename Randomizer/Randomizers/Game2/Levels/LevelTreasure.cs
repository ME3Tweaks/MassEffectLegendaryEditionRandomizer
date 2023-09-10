using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Levels
{
    internal static class LevelTreasure
    {
        public static bool InstallLevelTreasureRandomizer(GameTarget target, RandomizationOption option)
        {
            MERControl.InstallMERControl(target);
            ScriptTools.InstallScriptToPackage(target, "SFXGame.pcc", "BioSeqAct_AwardTreasure.Activated", "BioSeqAct_AwardTreasure.Activated.uc", false, true, MERCaches.GlobalCommonLookupCache);
            return true;
        }
    }
}
