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
var config array<string> InvisibleRandomWeaponOptions;
var config array<PowerOption> RandomPowerOptions;
var array<Object> OldReferences; // Used to ensure imports stay resolving

// Power randomizer vars
var bool bPreventPowerRandomization;
var bool bForcePreventPowerRandomization;
var bool bPowerRandomizerHasRunAtLeastOnce;

// Weapon randomzier vars
var bool bPreventWeaponRandomization;
var bool bForcePreventWeaponRandomization;
var bool bWeaponRandomizerHasRunAtLeastOnce;

// Functions
public function Randomize(BioPawn BP, bool willSpawnWeapons)
{
    if (willSpawnWeapons)
    {
        RandomizeWeapons(BP);
    }
    RandomizePowers(BP);
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

    if (bForcePreventWeaponRandomization || !Class'MERControlEngine'.default.bEnemyWeaponRandomizer || RandomWeaponOptions.Length < Weapons.Length)
    {
        return;
    }
    if (bPreventWeaponRandomization && !Class'MERControlEngine'.default.bEnemyWeaponRandomizer_Force){
        return;
    }
    if (bWeaponRandomizerHasRunAtLeastOnce && Class'MERControlEngine'.default.bEnemyWeaponRandomizer_OneTime){
        return;
    }

    ChoiceCount = RandomWeaponOptions.Length;
    if ((SFXPawn(BP) != None && !SFXPawn(BP).bSupportsVisibleWeapons) || Class'MERControlEngine'.default.bEnemyWeaponRandomizer_AllowInvisible) {
        ChoiceCount += InvisibleRandomWeaponOptions.Length;
    }
    
    while (I < Weapons.Length)
    {
        WeaponIDX = Rand(ChoiceCount);
        if (WeaponIDX < RandomWeaponOptions.Length){
            weaponIFP = RandomWeaponOptions[WeaponIDX];
        } else {
            weaponIFP = InvisibleRandomWeaponOptions[WeaponIDX - RandomWeaponOptions.Length];
        }
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
    bWeaponRandomizerHasRunAtLeastOnce = true;
    LogInternal("Randomized loadout weapons", );
}
private final function RandomizePowers(BioPawn BP)
{
    local int existingIndex;
    local int I;
    local PowerOption PowerInfo;
    local Class<SFXPower> NewPower;
    local array<string> AddedBasePowers;
    local array<Name> ValidatedBasePowers;
    
    if (bForcePreventPowerRandomization || !Class'MERControlEngine'.default.bEnemyPowerRandomizer || RandomPowerOptions.Length == 0)
    {
        return;
    }
    if (bPreventPowerRandomization && !Class'MERControlEngine'.default.bEnemyPowerRandomizer_Force) {
        return;
    }
    if (bPowerRandomizerHasRunAtLeastOnce && Class'MERControlEngine'.default.bEnemyPowerRandomizer_OneTime) {
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
        if (ValidatedBasePowers.Find(Powers[I].default.BaseName) >= 0)
        {
            Powers.RemoveItem(Powers[I]);
            I--;
            continue;
        }
        ValidatedBasePowers.AddItem(Powers[I].default.BaseName);
        I--;
    }
    bPowerRandomizerHasRunAtLeastOnce = true;
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}