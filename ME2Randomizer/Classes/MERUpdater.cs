using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomizerUI.Classes
{
    class MERUpdater
    {
        /// <summary>
        /// Gets the beginning of the applicable asset for updates, in the event there are multiple.
        /// </summary>
        /// <returns></returns>
        public static string GetGithubAssetPrefix()
        {
#if __GAME1__
            return "LE1Randomizer";
#elif __GAME2__
            return "LE2Randomizer";
#elif __GAME3__
            return "LE3Randomizer";
#else
            throw new Exception("GAME PREPROCESSOR DEFINITION NOT SET");
#endif
        }

        /// <summary>
        /// Asset to find in the archive (or the direct download)
        /// </summary>
        /// <returns></returns>
        public static string GetExpectedExeName()
        {
#if __GAME1__
            return "LE1Randomizer.exe";
#elif __GAME2__
            return "LE2Randomizer.exe";
#elif __GAME3__
            return "LE3Randomizer.exe";
#else
            throw new Exception("GAME PREPROCESSOR DEFINITION NOT SET");
#endif
        }

        public static string GetGithubOwner()
        {
            return "ME3Tweaks";
        }

        public static string GetGithubRepoName()
        {
            return "MassEffectLegendaryEditionRandomizer"; 
        }
    }
}
