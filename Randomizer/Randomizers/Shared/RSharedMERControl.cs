using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Targets;

namespace Randomizer.Randomizers.Shared
{
    internal class RSharedMERControl
    {
        public static void InstallBioPawnMERControl(GameTarget target)
        {
#if __GAME1__
            Randomizer.Randomizers.Game1.MERControl.InstallBioPawnMERControl(target);
#elif __GAME2__
            Randomizer.Randomizers.Game2.MERControl.InstallBioPawnMERControl(target);
#elif __GAME3__
            Randomizer.Randomizers.Game3.MERControl.InstallBioPawnMERControl(target);
#endif
        }

        public static void InstallMERControl(GameTarget target)
        {
#if __GAME1__
            Randomizer.Randomizers.Game1.MERControl.InstallMERControl(target);
#elif __GAME2__
            Randomizer.Randomizers.Game2.MERControl.InstallBioPawnMERControl(target);
#elif __GAME3__
            Randomizer.Randomizers.Game3.MERControl.InstallBioPawnMERControl(target);
#endif
        }
    }
}
