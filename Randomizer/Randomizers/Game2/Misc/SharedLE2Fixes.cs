using Randomizer.MER;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Misc
{
    internal static class SharedLE2Fixes
    {
        private const string PowerUsageFixName = "Startup_LE2R_PowerUsageFixes";
        internal static void InstallPowerUsageFixes()
        {
            var startup = MEREmbedded.GetEmbeddedPackage(MEGame.LE2, $@"Powers.{PowerUsageFixName}.pcc");
            MERFileSystem.SaveStreamToDLC(startup, $"{PowerUsageFixName}.pcc");

            // Startup for override
            ThreadSafeDLCStartupPackage.AddStartupPackage(PowerUsageFixName);
        }
    }
}
