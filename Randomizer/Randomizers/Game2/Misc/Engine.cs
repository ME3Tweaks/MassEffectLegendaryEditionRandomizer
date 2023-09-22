﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Misc
{
    public class Engine
    {
        public static IMEPackage GetEngine(GameTarget target)
        {
            return MERCaches.GlobalCommonLookupCache.GetCachedPackage("Engine.pcc");
        }
    }
}
