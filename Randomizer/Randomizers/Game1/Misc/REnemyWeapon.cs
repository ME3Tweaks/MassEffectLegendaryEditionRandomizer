using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Targets;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared;

namespace Randomizer.Randomizers.Game1.Misc
{
    internal class REnemyWeapon
    {
        public const string OPTIONKEY_EnemyWeaponsRandomizer = "bEnemyWeaponRandomizer";
        public const string OPTIONKEY_EnemyWeaponModsRandomizer = "bEnemyWeaponModsRandomizer";

        public static bool InstallWeaponRandomizer(GameTarget target, RandomizationOption option)
        {
            RSharedMERControl.InstallBioPawnMERControl(target);
            CoalescedHandler.EnableFeatureFlag(OPTIONKEY_EnemyWeaponsRandomizer);
            return true;
        }

        public static bool InstallWeaponModsRandomizer(GameTarget target, RandomizationOption option)
        {
            RSharedMERControl.InstallBioPawnMERControl(target);
            CoalescedHandler.EnableFeatureFlag(OPTIONKEY_EnemyWeaponModsRandomizer);
            return true;
        }
    }
}
