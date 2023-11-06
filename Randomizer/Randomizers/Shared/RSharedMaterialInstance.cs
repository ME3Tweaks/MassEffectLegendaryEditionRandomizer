using System;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Randomizer.Randomizers.Utility;
using WinCopies.Util;

namespace Randomizer.Randomizers.Shared
{
    class RSharedMaterialInstance
    {
        public static bool CanRandomize(ExportEntry export) => export.IsA(@"MaterialInstanceConstant");

        public static bool RandomizeExport(ExportEntry material, RandomizationOption option, string[] noRandomizeParameterNames = null)
        {
            if (!CanRandomize(material)) return false;
            var props = material.GetProperties();

            {
                var vectors = props.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues");
                if (vectors != null)
                {
                    foreach (var vector in vectors)
                    {
                        if (noRandomizeParameterNames != null && noRandomizeParameterNames.Contains(vector.GetProp<NameProperty>("ParameterName").Value.Name, StringComparer.InvariantCultureIgnoreCase))
                        {
                            continue; // Do not randomize
                        }
                        var pc = vector.GetProp<StructProperty>("ParameterValue");
                        if (pc != null)
                        {
                            StructTools.RandomizeTint(pc, false);
                        }
                    }
                }

                var scalars = props.GetProp<ArrayProperty<StructProperty>>("ScalarParameterValues");
                if (scalars != null)
                {
                    for (int i = 0; i < scalars.Count; i++)
                    {
                        var scalar = scalars[i];
                        if (noRandomizeParameterNames != null && noRandomizeParameterNames.Contains(scalar.GetProp<NameProperty>("ParameterName").Value.Name, StringComparer.InvariantCultureIgnoreCase))
                        {
                            continue; // Do not randomize
                        }
                        var currentValue = scalar.GetProp<FloatProperty>("ParameterValue");
                        if (currentValue > 1)
                        {
                            scalar.GetProp<FloatProperty>("ParameterValue").Value = ThreadSafeRandom.NextFloat(0, currentValue * 1.3);
                        }
                        else
                        {
                            //Debug.WriteLine("Randomizing parameter " + scalar.GetProp<NameProperty>("ParameterName"));
                            scalar.GetProp<FloatProperty>("ParameterValue").Value = ThreadSafeRandom.NextFloat(0, 1);
                        }
                    }

                    //foreach (var scalar in vectors)
                    //{
                    //    var paramValue = vector.GetProp<StructProperty>("ParameterValue");
                    //    RandomizeTint( paramValue, false);
                    //}
                }
            }
            material.WriteProperties(props);
            return true;
        }
    }
}
