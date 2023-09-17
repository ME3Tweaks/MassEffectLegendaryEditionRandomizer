using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Shared
{
    /// <summary>
    /// Handles randomizing things such as PointLight, Spotlight, etc
    /// </summary>
    class RSharedLighting
    {
        public static bool InstallDynamicLightingRandomizer(GameTarget target, RandomizationOption option)
        {
            var engine = Engine.GetEngine(target);
            ScriptTools.AddToClassInPackageFromEmbedded(target, engine, "Light.PostBeginPlay", "Light");
            MERFileSystem.SavePackage(engine);
            CoalescedHandler.EnableFeatureFlag("bLightRandomizer");
            return true;
        }

        private static bool CanRandomize(ExportEntry export) => !export.IsDefaultObject &&
            (export.ClassName == @"SpotLightComponent" ||
             export.ClassName == @"PointLightComponent" ||
             export.ClassName == @"DirectionalLightComponent" ||
             export.ClassName == @"SkyLightComponent");

        public static bool RandomizeExport(GameTarget target, ExportEntry export,RandomizationOption option)
        {
            if (!CanRandomize(export)) return false;
            //Log.Information($@"Randomizing light {export.UIndex}");
            var lc = export.GetProperty<StructProperty>("LightColor");
            if (lc == null)
            {
                // create
                var pc = new PropertyCollection();
                pc.Add(new ByteProperty(255, "B"));
                pc.Add(new ByteProperty(255, "G"));
                pc.Add(new ByteProperty(255, "R"));
                pc.Add(new ByteProperty(0, "A"));

                lc = new StructProperty("Color", pc, "LightColor", true);
            }

            StructTools.RandomizeColor( lc, false);
            export.WriteProperty(lc);
            return true;
        }
    }
}
