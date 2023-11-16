using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game1.ExportTypes
{
    class RBioWaypointSet
    {

        public static bool InstallDynamicBioWayPointSetRandomizer(GameTarget target, RandomizationOption option)
        {
            RSharedMERControl.InstallMERControl(target);
            var sfxGame = RSharedSFXGame.GetSFXGame(target);
            ScriptTools.AddToClassInPackageFromEmbedded(target, sfxGame, "BioWayPointSet.PostBeginPlay", "BioWayPointSet");
            MERFileSystem.SavePackage(sfxGame);
            CoalescedHandler.EnableFeatureFlag("bBioWayPointSetRandomizer");
            return true;
        }

        private static bool CanRandomize(ExportEntry export) => !export.IsDefaultObject && export.ClassName == @"BioWaypointSet";
        public static bool RandomizeExport(GameTarget target, ExportEntry export, RandomizationOption option)
        {
            if (!CanRandomize(export)) return false;
            RandomizeInternal(export, option);
            return true;
        }

        private static void RandomizeInternal(ExportEntry export, RandomizationOption option)
        {
            MERLog.Information($"Randomizing BioWaypointSet {export.UIndex} in {Path.GetFileName(export.FileRef.FilePath)}");
            var waypointReferences = export.GetProperty<ArrayProperty<StructProperty>>("WaypointReferences");
            if (waypointReferences != null)
            {
                //Get list of valid targets
                var pcc = export.FileRef;
                var waypoints = pcc.Exports.Where(x => x.ClassName == "BioPathPoint" || x.ClassName == "PathNode").ToList();
                waypoints.Shuffle();

                foreach (var waypoint in waypointReferences)
                {
                    var nav = waypoint.GetProp<ObjectProperty>("Nav");
                    if (nav != null && nav.Value > 0)
                    {
                        ExportEntry currentPoint = nav.ResolveToEntry(export.FileRef) as ExportEntry;
                        if (currentPoint != null)
                        {
                            if (currentPoint.ClassName == "BioPathPoint" || currentPoint.ClassName == "PathNode")
                            {
                                nav.Value = waypoints[0].UIndex;
                                waypoints.RemoveAt(0);
                            }
                            else
                            {
                                Debug.WriteLine("SKIPPING NODE TYPE " + currentPoint.ClassName);
                            }
                        }
                    }
                }
            }
            export.WriteProperty(waypointReferences);

        }
    }
}
