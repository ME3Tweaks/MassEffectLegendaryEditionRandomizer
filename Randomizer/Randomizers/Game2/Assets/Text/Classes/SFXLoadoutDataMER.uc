Class SFXLoadoutDataMER extends SFXLoadoutData
    native
    config(Weapon);

// Types
struct PowerOption 
{
    var string PowerIFP;
    var string BasePowerName;
};

// Variables
var config array<string> RandomWeaponOptions;
var config array<PowerOption> RandomPowerOptions;
var array<Object> OldReferences;
var bool bPreventPowerRandomization;
var bool bPreventWeaponRandomization;
var bool bForcePreventPowerRandomization;
var bool bForcePreventWeaponRandomization;

// Functions
public function Randomize(bool willSpawnWeapons)
{
    if (willSpawnWeapons)
    {
        RandomizeWeapons();
    }
    RandomizePowers();
}
private final function RandomizeWeapons()
{
    local array<string> newWeapons;
    local string weaponIFP;
    local int existingIndex;
    local int I;
    local Class<SFXWeapon> NewWeapon;
    
    if (!Class'MERControlEngine'.default.bEnemyWeaponRandomizer || RandomWeaponOptions.Length < Weapons.Length)
    {
        return;
    }
    if (bPreventWeaponRandomization || bForcePreventWeaponRandomization){
        return;
    }
    
    while (I < Weapons.Length)
    {
        weaponIFP = RandomWeaponOptions[Rand(RandomWeaponOptions.Length)];
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
            }
            I++;
        }
        continue;
    }
    LogInternal("Randomized loadout weapons", );
}
private final function RandomizePowers()
{
    local int existingIndex;
    local int I;
    local PowerOption PowerInfo;
    local Class<SFXPower> NewPower;
    local array<string> AddedBasePowers;
    local array<Name> ValidatedBasePowers;
    
    if (!Class'MERControlEngine'.default.bEnemyPowerRandomizer || RandomPowerOptions.Length == 0)
    {
        return;
    }
    if (bForcePreventPowerRandomization){
        return;
    } 
    if (bPreventPowerRandomization && !Class'MERControlEngine'.default.bForceEnemyPowerRandomizer) {
        return;
    }
    if (Class'MERControlEngine'.default.bEnemyPowerRandomizer_EnforceMinPowerCount && RandomPowerOptions.Length >= 5)
    {
        Powers.Length = Clamp(Powers.Length, 2, 5);
    }

    
    while (I < Powers.Length)
    {
        PowerInfo = RandomPowerOptions[Rand(RandomPowerOptions.Length)];
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
                LogInternal("Set Power to " $ NewPower, );
            }
        }
        I++;
        continue;
    }
    I = Powers.Length - 1;
    while (I >= 0)
    {
        LogInternal("Validating power index " $ I, );
        if (ValidatedBasePowers.Find(Powers[I].default.BaseName) >= 0)
        {
            Powers.RemoveItem(Powers[I]);
            I--;
            continue;
        }
        ValidatedBasePowers.AddItem(Powers[I].default.BaseName);
        I--;
    }
    LogInternal("Randomized loadout", );
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}