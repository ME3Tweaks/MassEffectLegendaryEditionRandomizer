Class MERControl extends MERControlEngine
    config(Engine);

// Types
struct MERPowerMapping 
{
    var Name powerName;
    var int HasPowerTalentId;
};

// Variables
var config array<MERPowerMapping> PowerMappings;

// Functions
public static function RandomizeBioPawn(BioPawn BP)
{
    BioPawn_RandomizePowers(BP);
    BioPawn_BattleRoyalify(BP);
    BioPawn_RandomizeSpeed(BP);
}

private static final function BioPawn_RandomizePowers(BioPawn BP)
{
    local BioTalent Talent;
    local BioGameProperty Property;
    local MERPowerMapping PM;
    local int I;
    local int Id;
    local BioPower bioPow;
    local array<MERPowerMapping> AddedPowers;
    
    if (Class'MERControlEngine'.default.bEnemyPowerRandomizer)
    {
        Talent = new (None) Class'BioTalent';
        Talent.m_nRank = 1;
        Talent.m_nID = 1000;
        for (I = 0; I < 3; I++)
        {
            PM = default.PowerMappings[Rand(default.PowerMappings.Length)];
            Property = Class'BioGameProperty'.static.CreateInstant();
            Class'BioGameEffectPowerGive'.static.Create(Property, PM.powerName);
            Talent.AddGameProperty(Property);
            AddedPowers.AddItem(PM);
            LogInternal(BP $ " adding power " $ PM.powerName $ " with talent " $ PM.HasPowerTalentId, );
        }
    }
    BP.m_oBehavior.m_Talents.AddSimpleTalent(Talent);
    BP.m_oBehavior.RecomputeCapabilities();
    for (I = 0; I < AddedPowers.Length; I++)
    {
        PM = AddedPowers[I];
        BP.m_oBehavior.m_Talents.AddSimpleTalent(Class'BioTalentImporter'.static.LoadTalent(BP.m_oBehavior, PM.HasPowerTalentId, 1));
    }
    BP.m_oBehavior.RecomputeCapabilities();
}

private static final function BioPawn_BattleRoyalify(BioPawn BP)
{
    local Class<BioBaseSquad> SquadClass;
    local BioAiController oController;
    local BioWeaponRanged weap;
    local Name RandomWeaponManf;
    
    if (Class'MERControlEngine'.default.bBattleRoyaleMode)
    {
        LogInternal("PostBeginPlay() on " $ BP, );
        if (SFXPawn_Player(BP) == None && BP.m_oBehavior != None)
        {
            BP.bAmbientCreature = FALSE;
            SquadClass = Class<BioBaseSquad>(FindObject("SFXStrategicAI.BioSquadCombat", Class'Class'));
            if (SquadClass != None)
            {
                LogInternal("Setting squad on " $ BP, );
                BP.m_oBehavior.Squad = BP.Spawn(SquadClass, BP);
                oController = BioAiController(BP.Controller);
                BP.m_oBehavior.m_bTargetable = TRUE;
                BP.m_oBehavior.m_bTargetableOverride = TRUE;
                BP.m_oBehavior.AddWeapon("NihlusShotgun", 1);
                BP.m_oBehavior.m_Talents.AddBonusTalents(154);
                if (oController != None)
                {
                    oController.ChangeAI(Class'BioAI_Charge', FALSE);
                }
            }
            else
            {
                LogInternal("Could not find object... on " $ BP, );
            }
        }
    }
}
public static function BioPawn_RandomizeSpeed(BioPawn BP)
{
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
    PowerMappings = ({powerName = 'TK_Throw', HasPowerTalentId = 288}, 
                     {powerName = 'TK_Lift', HasPowerTalentId = 50}, 
                     {powerName = 'SD_Warp', HasPowerTalentId = 56}, 
                     {powerName = 'SD_Singularity', HasPowerTalentId = 57}, 
                     {powerName = 'KF_Barrier', HasPowerTalentId = 63}, 
                     {powerName = 'KF_Stasis', HasPowerTalentId = 64}, 
                     {powerName = 'MD_Heal', HasPowerTalentId = 98},  // First Aid
                     {powerName = 'MD_Shock', HasPowerTalentId = 157}, // Medicine 
                     {powerName = 'EL_EMP_Delivery', HasPowerTalentId = 84}, 
                     //{powerName = 'EL_EMP_Activation', HasPowerTalentId = -1}, 
                     {powerName = 'EL_Damping_Delivery', HasPowerTalentId = 86}, 
                     //{powerName = 'EL_Damping_Activation', HasPowerTalentId = -1}, 
                     {powerName = 'DC_Sabotage_Delivery', HasPowerTalentId = 93}, 
                     //{powerName = 'DC_Sabotage_Activation', HasPowerTalentId = -1}, 
                     {powerName = 'DC_Hacking', HasPowerTalentId = 91}, 
                     {powerName = 'AW_Suppression', HasPowerTalentId = 7}, 
                     {powerName = 'LW_Marksman', HasPowerTalentId = 0}, // This is technical Pistols. Marksman does not have single power 
                     // {powerName = 'BW_CarnageSetup', HasPowerTalentId = -1}, 
                     // {powerName = 'BW_Carnage', HasPowerTalentId = -1}, 
                     {powerName = 'BW_Carnage_NPC', HasPowerTalentId = 14}, 
                     {powerName = 'SW_Critical', HasPowerTalentId = 21}, // Sniper Rifles 
                     {powerName = 'AR_ShieldBoost', HasPowerTalentId = 28}, // Light Armor 
                     {powerName = 'TC_TakeDown', HasPowerTalentId = 259},  // Spectre Training
                     {powerName = 'FT_Adrenaline', HasPowerTalentId = 35}, // Assault Rifles 
                     {powerName = 'AT_Immunity', HasPowerTalentId = 52}, // Fitness
                     {powerName = 'MD_SquadHeal', HasPowerTalentId = 325}, // Player Only. Or is it? 
                     //{powerName = 'Rifle_Butt', HasPowerTalentId = -1}, // Not in talent effect levels
                     //{powerName = 'Pistol_Whip', HasPowerTalentId = -1}, // Not in talent effect levels
                     //{powerName = 'ArmorEmitter', HasPowerTalentId = -1}, // Only on SuperSoldier. Unknown
                    {PowerName='GETH_HexBarrier', HasPowerTalentId=168},
                    {PowerName='GETH_DampingBeam', HasPowerTalentId=169},
                    {PowerName='GETH_AntiTank', HasPowerTalentId=167},
                    {PowerName='GETH_AntiTank_LR', HasPowerTalentId=296}, // This also covers AntiTank
                    {PowerName='GETH_Carnage', HasPowerTalentId=205},
                    {PowerName='GETH_Carnage_ShotgunOnly', HasPowerTalentId=-331},
                    {PowerName='GETH_SiegePulse', HasPowerTalentId=171},
                    {PowerName='GETH_SiegePulse_LR', HasPowerTalentId=171},
                    {PowerName='GETH_SabotageBeam', HasPowerTalentId=170},
                    {PowerName='GETH_OverloadBeam', HasPowerTalentId=291},
                    {PowerName='GETH_SniperBeam', HasPowerTalentId=292},
                    //{PowerName='GETH_SniperPulse', HasPowerTalentId=-1}, // Not sure
                    //{PowerName='DRONE_Entrench', HasPowerTalentId=-1}, // Not in talent effects
                    {PowerName='ZOMBIE_SmashR', HasPowerTalentId=193},
                    {PowerName='ZOMBIE_SmashL', HasPowerTalentId=193},
                    {PowerName='ZOMBIE_AcidSpit', HasPowerTalentId=194},
                    {PowerName='RACHNI_LanceArmsR', HasPowerTalentId=196},
                    {PowerName='RACHNI_LanceArmsL', HasPowerTalentId=196},
                    {PowerName='RACHNI_AcidSpit', HasPowerTalentId=195},
                    // {PowerName='RACHNI_AcidGlob', HasPowerTalentId=-1}, // Not in talent effects, unknown
                    {PowerName='RACHNI_Explode', HasPowerTalentId=199},
                    {PowerName='ZOMBIE_Explode', HasPowerTalentId=201},
                    {PowerName='KRO_Regen', HasPowerTalentId=198},
                    {PowerName='GETH_CombatVI', HasPowerTalentId=206},
                    {PowerName='HUSK_SmashR', HasPowerTalentId=208},
                    {PowerName='HUSK_SmashL', HasPowerTalentId=208},
                    {PowerName='MAW_Smash', HasPowerTalentId=210},
                    {PowerName='MAW_Spit', HasPowerTalentId=213},
                    {PowerName='TEN_Smash', HasPowerTalentId=212},
                    {PowerName='VAR_Rake', HasPowerTalentId=240},
                    {PowerName='TURRET_AntiTank', HasPowerTalentId=271},
                    {PowerName='TURRET_AntiTank_LR', HasPowerTalentId=271},
                    // {PowerName='VehicleRepair', HasPowerTalentId=-1}, // Enemies are not vehicles
                    {PowerName='MD_HealFree', HasPowerTalentId=274},
                    {PowerName='MD_HealFree_NoAnim', HasPowerTalentId=297}, // Rachni heal
                    // {PowerName='MD_RepairFree_NoAnim', HasPowerTalentId=-1},
                    {PowerName='TK_Lift_PLC', HasPowerTalentId=276},
                    //{PowerName='KRO_Rifle_Butt', HasPowerTalentId=-1},
                    //{PowerName='KRO_Pistol_Whip', HasPowerTalentId=-1},
                    {PowerName='TK_Throw_NPC', HasPowerTalentId=288},
                    {PowerName='SD_Warp_NPC', HasPowerTalentId=289},
                    {PowerName='KF_Stasis_NPC', HasPowerTalentId=277},
                    {PowerName='SD_Warp_RAC', HasPowerTalentId=298},
                    {PowerName='KF_Stasis_RAC', HasPowerTalentId=299}
                    //{PowerName='SAREN_Carnage', HasPowerTalentId=286},
                    //{PowerName='ASARI_ThrowWarp', HasPowerTalentId=-1},
                    //{PowerName='SAREN_Clear', HasPowerTalentId=-1}
                    )
}