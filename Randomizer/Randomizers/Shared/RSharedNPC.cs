using ME3TweaksCore.Targets;
using Randomizer.Randomizers.Game2;
using Randomizer.Randomizers.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Randomizer.Randomizers.Shared
{
    internal class RSharedNPC
    {
        public static bool InstallPawnColorRandomizer(GameTarget target, RandomizationOption option)
        {
            MERControl.InstallBioPawnMERControl(target);
            MERControl.InstallSFXSkeletalMeshActorMATMERControl(target);
            CoalescedHandler.EnableFeatureFlag("bPawnColorsRandomizer");
            return true;
        }
    }
}
