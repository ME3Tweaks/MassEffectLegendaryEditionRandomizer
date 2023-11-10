using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Targets;
using Randomizer.MER;

namespace Randomizer.Randomizers.Shared
{
    internal class RSharedSFXGame
    {
        public static IMEPackage GetSFXGame(GameTarget target)
        {
            return MERCaches.GlobalCommonLookupCache.GetCachedPackage("SFXGame.pcc");
        }
    }
}
