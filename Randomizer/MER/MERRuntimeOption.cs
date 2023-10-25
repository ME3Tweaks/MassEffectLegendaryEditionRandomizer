using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Helpers;

namespace Randomizer.MER
{
    /// <summary>
    /// Used to describe runtime options. This is technically only used by the UI, but it must exist here so the consts are accessible to the main Randomizer
    /// </summary>
    public class MERRuntimeOption
    {
#if __GAME2__
        public const string RTO_TITLE_ICONICFACES = "Iconic faces";
        public const string RTO_TITLE_LOOKAT_SYSTEM = "LookAt system";
        public const string RTO_TITLE_NPCCOLORS = "NPC colors";
        public const string RTO_TITLE_ROMANCE = "Romance";
        public const string RTO_TITLE_ENEMYPOWERS = "Enemy Powers";
        public const string RTO_TITLE_ENEMYWEAPONS = "Enemy weapons";
        public const string RTO_TITLE_EYES = "Eyes";
        public const string RTO_TITLE_NPCFACES = "NPC faces";
        public const string RTO_TITLE_FOGCOLORS = "Fog colors";
        public const string RTO_TITLE_LIGHTCOLORS = "Light colors";
        public const string RTO_TITLE_NPCMOVEMENT = "NPC movement speeds";
        public const string RTO_TITLE_PLAYERMOVEMENT = "Player movement speeds";
        public const string RTO_TITLE_WEAPONSTATS = "Weapon stats";

        public const string RTO_TITLE_FORCERANDOMIZATION = "Force Randomization";
        public const string RTO_TITLE_RANDOMIZEONCEPERLOADOUT = "Randomize once per loadout";
        public const string RTO_TITLE_GIVEPOWERSTOALLENEMIES = "Give powers to all enemies";

        public const string RTO_TITLE_ICONICFACE_AMOUNT = "Iconic faces (amount)";
        public const string RTO_TITLE_NPCFACES_AMOUNT = "NPC faces (amount)";


        public const string RTO_TITLE_ICONICPERSISTENT = "Keep face persistent";
        public const string RTO_TITLE_SUICIDEMISSION = "Suicide Mission";
        public const string RTO_TITLE_USENEWFINALBOSSMUSIC = "Use new final boss music";

#endif

        public MERRuntimeOption(string controlEngineProperty, CoalesceProperty val)
        {
            PropertyName = controlEngineProperty;
            switch (controlEngineProperty)
            {
#if __GAME2__
                case "fBioMorphFaceRandomization":
                case "fIconicFaceRandomization":
                    IsFloatProperty = true;
                    if (float.TryParse(val[0].Value, out var f))
                    {
                        FloatValue = f;
                    }
                    else
                    {
                        MERLog.Error($"Invalid property value for float property {PropertyName}: {val[0].Value}");
                    }
                    break;
                case "bSuicideMissionRandomizationInstalled":
                    CanBeModified = false;
                    IsBoolProperty = true;
                    IsSelected = val[0].Value.CaseInsensitiveEquals("TRUE"); // This technically should never be changed by users...
                    break;
#endif
                default:
                    IsBoolProperty = true;
                    IsSelected = val[0].Value.CaseInsensitiveEquals("TRUE");
                    break;
            }
            SetUIText();
        }

        private void SetUIText()
        {
            switch (PropertyName)
            {
#if __GAME2__
                case "fBioMorphFaceRandomization":
                    DisplayString = RTO_TITLE_NPCFACES_AMOUNT;
                    break;
                case "fIconicFaceRandomization":
                    DisplayString = RTO_TITLE_ICONICFACE_AMOUNT;
                    break;
                case "bEnemyPowerRandomizer":
                    DisplayString = RTO_TITLE_ENEMYPOWERS;
                    break;
                case "bEnemyPowerRandomizer_Force":
                    DisplayString = $"{RTO_TITLE_ENEMYPOWERS}: {RTO_TITLE_FORCERANDOMIZATION}";
                    break;
                case "bEnemyPowerRandomizer_EnforceMinPowerCount":
                    DisplayString = $"{RTO_TITLE_ENEMYPOWERS}: {RTO_TITLE_GIVEPOWERSTOALLENEMIES}";
                    break;
                case "bEnemyPowerRandomizer_OneTime":
                    DisplayString = $"{RTO_TITLE_ENEMYPOWERS}: {RTO_TITLE_RANDOMIZEONCEPERLOADOUT}";
                    break;
                case "bEnemyWeaponRandomizer":
                    DisplayString = RTO_TITLE_ENEMYWEAPONS;
                    break;
                case "bEnemyWeaponRandomizer_Force":
                    DisplayString = $"{RTO_TITLE_ENEMYWEAPONS}: {RTO_TITLE_FORCERANDOMIZATION}";
                    break;
                case "bEnemyWeaponRandomizer_OneTime":
                    DisplayString = $"{RTO_TITLE_ENEMYWEAPONS}: {RTO_TITLE_RANDOMIZEONCEPERLOADOUT}";
                    break;
                case "bIconicRandomizer":
                    DisplayString = RTO_TITLE_ICONICFACES;
                    break;
                case "bIconicRandomizer_Persistent":
                    DisplayString = RTO_TITLE_ICONICPERSISTENT;
                    break;
                case "bUseNewFinalBossMusic":
                    DisplayString = $"{RTO_TITLE_SUICIDEMISSION}: {RTO_TITLE_USENEWFINALBOSSMUSIC}";
                    break;
                case "bNPCMovementSpeedRandomizer":
                    DisplayString = RTO_TITLE_NPCMOVEMENT;
                    break;
                case "bPlayerMovementSpeedRandomizer":
                    DisplayString = RTO_TITLE_PLAYERMOVEMENT;
                    break;
                case "bPawnLookatRandomizer":
                    DisplayString = RTO_TITLE_LOOKAT_SYSTEM;
                    break;
                case "bEyeRandomizer":
                    DisplayString = RTO_TITLE_EYES;
                    break;
                case "bFogRandomizer":
                    DisplayString = RTO_TITLE_FOGCOLORS;
                    break;
                case "bLightRandomizer":
                    DisplayString = RTO_TITLE_LIGHTCOLORS;
                    break;
                case "bPawnColorsRandomizer":
                    DisplayString = RTO_TITLE_NPCCOLORS;
                    break;
#endif
                default:
                    DisplayString = $"Unknown property: {PropertyName}";
                    DescriptionString = "This property is not defined in Option Toggler";
                    break;
            }
        }


        /// <summary>
        /// If this value is editable
        /// </summary>
        public bool CanBeModified { get; set; } = true;

        /// <summary>
        /// What to display
        /// </summary>
        public string DisplayString { get; set; }

        /// <summary>
        /// Extra information to show
        /// </summary>
        public string DescriptionString { get; set; }

        /// <summary>
        /// The name of the property
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Value for Bool property
        /// </summary>
        public bool IsSelected { get; set; }
        public bool IsBoolProperty { get; set; }

        /// <summary>
        /// Value for Int Property
        /// </summary>
        public int IntValue { get; set; }
        public bool IsIntProperty { get; set; }

        /// <summary>
        /// Value for FloatProperty
        /// </summary>
        public float FloatValue { get; set; }
        public bool IsFloatProperty { get; set; }
    }


}
