Class SFXLoadoutDataMER extends SFXLoadoutData
    native
    config(Weapon);

// Types
struct PowerOption 
{
    var string PowerIFP;
    var string BasePowerName;
    var EBioCapabilityTypes CapabilityType;
};

// Variables
var config array<string> RandomWeaponOptions;
var config array<PowerOption> RandomPowerOptions;
var array<Object> OldReferences;
var bool bPreventPowerRandomization;
var bool bForcePreventPowerRandomization;
var bool bPowerRandomizerHasRunAtLeastOnce;
var bool bPreventWeaponRandomization;
var bool bForcePreventWeaponRandomization;
var bool bWeaponRandomizerHasRunAtLeastOnce;
var config int NumPowerRetriesAllowed;

// Functions
public function Randomize(BioPawn BP, bool willSpawnWeapons)
{
    if (willSpawnWeapons)
    {
        RandomizeWeapons(BP);
    }
    RandomizePowers(BP);
}
private final function bool CanRandomizeWeapons(BioPawn BP)
{
    if ((bForcePreventWeaponRandomization || !Class'MERControlEngine'.default.bEnemyWeaponRandomizer) || RandomWeaponOptions.Length < Weapons.Length)
    {
        return FALSE;
    }
    if (bPreventWeaponRandomization && !Class'MERControlEngine'.default.bEnemyWeaponRandomizer_Force)
    {
        return FALSE;
    }
    if (bWeaponRandomizerHasRunAtLeastOnce && Class'MERControlEngine'.default.bEnemyWeaponRandomizer_OneTime)
    {
        return FALSE;
    }
    return TRUE;
}
private final function RandomizeWeapons(BioPawn BP)
{
    local array<string> newWeapons;
    local string weaponIFP;
    local int existingIndex;
    local int I;
    local Class<SFXWeapon> NewWeapon;
    local int WeaponIDX;
    local int ChoiceCount;
    
    if (!CanRandomizeWeapons(BP))
    {
        return;
    }
    ChoiceCount = RandomWeaponOptions.Length;
    while (I < Weapons.Length)
    {
        weaponIFP = RandomWeaponOptions[Rand(ChoiceCount)];
        if (newWeapons.Find(weaponIFP) < 0)
        {
            NewWeapon = Class<SFXWeapon>(Class'SFXEngine'.static.LoadSeekFreeObject(weaponIFP, Class'Class'));
            if (NewWeapon == None)
            {
                LogInternal("Dynamic loading new weapon failed! Weapon: " $ weaponIFP, );
            }
            else
            {
                LogInternal("Set Weapon", );
                OldReferences.AddItem(Weapons[I]);
                Weapons[I] = NewWeapon;
                Class'SFXEngine'.static.ReleaseSeekFreeObject(weaponIFP);
            }
            I++;
        }
        continue;
    }
    bWeaponRandomizerHasRunAtLeastOnce = TRUE;
    LogInternal("Randomized loadout weapons", );
}
private final function bool CanRandomizePowers(BioPawn BP)
{
    local BioAiController Controller;
    
    if ((bForcePreventPowerRandomization || !Class'MERControlEngine'.default.bEnemyPowerRandomizer) || RandomPowerOptions.Length == 0)
    {
        return FALSE;
    }
    if (bPreventPowerRandomization && !Class'MERControlEngine'.default.bEnemyPowerRandomizer_Force)
    {
        return FALSE;
    }
    if (bPowerRandomizerHasRunAtLeastOnce && Class'MERControlEngine'.default.bEnemyPowerRandomizer_OneTime)
    {
        return FALSE;
    }
    if (Class'MERControlEngine'.default.bEnemyPowerRandomizer_EnforceMinPowerCount && RandomPowerOptions.Length >= 5)
    {
        Powers.Length = Clamp(Powers.Length, 2, 5);
    }
    Controller = BioAiController(BP.Controller);
    if (Controller != None)
    {
        if (InStr(string(Controller.Class.Name), "RedHusk", TRUE, , ) >= 0 || InStr(string(Controller.Class.Name), "ination", TRUE, , ) >= 0)
        {
            return FALSE;
        }
    }
    return TRUE;
}
private final function RandomizePowers(BioPawn BP)
{
    local int existingIndex;
    local int I;
    local PowerOption PowerInfo;
    local Class<SFXPower> NewPower;
    local array<string> AddedBasePowers;
    local array<Name> ValidatedBasePowers;
    local bool bHasDeathPower;
    local bool bHasMeleePower;
    local bool isLoadingMeleePower;
    local bool isLoadingDeathPower;
    
    if (!CanRandomizePowers(BP))
    {
        return;
    }
    while (I < Powers.Length)
    {
        if (Powers[I].default.PowerName == 'PraetorianDeathChoir')
        {
            I++;
            continue;
        }
        if (Powers[I].default.PowerName == 'BioticCharge_NPC')
        {
            I++;
            continue;
        }
        if (Powers[I].default.PowerName == 'Maw_Spit')
        {
            I++;
            continue;
        }
        if (Powers[I].default.PowerName == 'TK_Lift_PLC' || Powers[I].default.PowerName == 'KF_Barrier')
        {
            I++;
            continue;
        }
        PowerInfo = RandomPowerOptions[Rand(RandomPowerOptions.Length)];
        if (((BP.Controller != None && BP.Controller.IsA('SFXAI_MechanicalTurret')) && !bHasDeathPower) && PowerInfo.CapabilityType != EBioCapabilityTypes.BioCaps_Death)
        {
            continue;
        }
        if (PowerInfo.CapabilityType == EBioCapabilityTypes.BioCaps_Death)
        {
            if (bHasDeathPower)
            {
                continue;
            }
            if (BP.Controller != None && BP.Controller.IsA('SFXAI_Krogan'))
            {
                continue;
            }
            if (BP.IsA('SFXPawn_Collector'))
            {
                continue;
            }
        }
        isLoadingMeleePower = InStr(PowerInfo.BasePowerName, "Melee", TRUE, , ) >= 0;
        if ((!bHasMeleePower && !isLoadingMeleePower) && BP.IsA('SFXPawn_HuskLite'))
        {
            continue;
        }
        if (AddedBasePowers.Find(PowerInfo.BasePowerName) < 0)
        {
            NewPower = Class<SFXPower>(Class'SFXEngine'.static.LoadSeekFreeObject(PowerInfo.PowerIFP, Class'Class'));
            if (NewPower == None)
            {
                LogInternal("Dynamic loading new power failed! Power: " $ PowerInfo.PowerIFP, );
            }
            else
            {
                if (Powers[I] != None)
                {
                    OldReferences.AddItem(Powers[I]);
                }
                Powers[I] = NewPower;
                Class'SFXEngine'.static.ReleaseSeekFreeObject(PowerInfo.PowerIFP);
                if (PowerInfo.CapabilityType == EBioCapabilityTypes.BioCaps_Death)
                {
                    bHasDeathPower = TRUE;
                }
                if (isLoadingMeleePower)
                {
                    bHasMeleePower = TRUE;
                }
                LogInternal("Set Power to " $ NewPower, );
            }
        }
        I++;
        continue;
    }
    I = Powers.Length - 1;
    while (I >= 0)
    {
        if (ValidatedBasePowers.Find(Powers[I].default.BaseName) >= 0)
        {
            Powers.RemoveItem(Powers[I]);
            I--;
            continue;
        }
        ValidatedBasePowers.AddItem(Powers[I].default.BaseName);
        I--;
    }
    bPowerRandomizerHasRunAtLeastOnce = TRUE;
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
    NumPowerRetriesAllowed = 5
}