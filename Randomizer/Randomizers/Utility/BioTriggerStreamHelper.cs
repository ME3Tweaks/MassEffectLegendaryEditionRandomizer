using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace Randomizer.Randomizers.Utility
{
    public static class BioTriggerStreamHelper
    {
        /// <summary>
        /// Ensures the specified state name has the listed level in the streaming state block
        /// </summary>
        /// <param name="triggerStream"></param>
        /// <param name="stateName"></param>
        /// <param name="visibleLevel"></param>
        public static void EnsureVisible(ExportEntry triggerStream, string stateName, NameReference visibleLevel)
        {
            var modified = false;

            var ss = triggerStream.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");
            foreach (var state in ss)
            {
                var tStateName = state.Properties.GetProp<NameProperty>("StateName")?.Value.Name;
                if (tStateName == null)
                    continue; // Something's wrong here...

                if (tStateName != stateName)
                {
                    continue; // Not this one
                }

                var visibleChunks = state.GetProp<ArrayProperty<NameProperty>>("VisibleChunkNames");
                if (visibleChunks.All(x => !x.Value.Instanced.CaseInsensitiveEquals(visibleLevel.Instanced)))
                {
                    visibleChunks.Add(new NameProperty(visibleLevel));
                    triggerStream.WriteProperty(ss);
                }
            }
        }
    }
}