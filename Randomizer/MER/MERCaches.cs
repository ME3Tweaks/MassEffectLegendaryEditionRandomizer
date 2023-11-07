using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using ME3TweaksCore.Targets;
using Randomizer.Randomizers.Game2.Misc;

namespace Randomizer.MER
{
    internal class MERCaches
    {
        public static void Init(GameTarget target)
        {
            MERLog.Information(@"Loading global package cache");
            _globalCommonLookupCache = new MERPackageCache(target, null, false);
            foreach (var fullySafeFile in EntryImporter.FilesSafeToImportFrom(target.Game))
            {
                MERLog.Information($"Caching {fullySafeFile} into memory");
                _globalCommonLookupCache.GetCachedPackage(fullySafeFile);
            }
        }

        /// <summary>
        /// Disposes of packages in the global common lookup cache
        /// </summary>
        public static void Cleanup()
        {
            MERFileSystem.sfxgameGuid = default;
            _globalCommonLookupCache?.Dispose();
            _globalCommonLookupCache = null;
        }

        /// <summary>
        /// Re-initializes the cache - can be used if you add things to Engine, SFXGame, etc
        /// </summary>
        /// <param name="target"></param>
        public static void ReInit(GameTarget target)
        {
            Cleanup();
            Init(target);
        }

        private static MERPackageCache _globalCommonLookupCache;
        /// <summary>
        /// Cache used for things such as resolving imports. For things like SFXGame, Engine, that will commonly be opened
        /// </summary>
        public static MERPackageCache GlobalCommonLookupCache
        {
            get
            {
                if (_globalCommonLookupCache == null)
                {
#if DEBUG
                    Debug.WriteLine(@"WARNING: Loading a default global lookup cache. This will fail in release builds!");
                    Init(new GameTarget(MERFileSystem.Game, MEDirectories.GetDefaultGamePath(MERFileSystem.Game), true));
#else
                    throw new Exception("Cannot access a null GlobalCommonLookupCache! It must be initialized first");
#endif
                }

                return _globalCommonLookupCache;
            }
        }
    }
}
