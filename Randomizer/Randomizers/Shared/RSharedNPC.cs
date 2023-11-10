using ME3TweaksCore.Targets;
using Randomizer.Randomizers.Handlers;

namespace Randomizer.Randomizers.Shared
{
    internal class RSharedNPC
    {
        public static bool InstallPawnColorRandomizer(GameTarget target, RandomizationOption option)
        {
            RSharedMERControl.InstallBioPawnMERControl(target);
#if __GAME2__
            // LE2 has SFXSkeletalMeshActorMAT
            Randomizer.Randomizers.Game2.MERControl.InstallSFXSkeletalMeshActorMATMERControl(target);
#elif __GAME3__
            // StuntActor?
#endif
            CoalescedHandler.EnableFeatureFlag("bPawnColorsRandomizer");
            return true;
        }
    }
}
