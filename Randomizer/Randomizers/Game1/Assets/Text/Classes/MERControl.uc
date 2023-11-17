class MERControl extends MERControlEngine;


public static function RandomizeBioPawn(BioPawn BP){
    BioPawn_BattleRoyalify(BP);
    BioPawn_RandomizeSpeed(BP);
}

private static function BioPawn_BattleRoyalify(BioPawn BP)
{
    local Class<BioBaseSquad> SquadClass;
    local BioAiController oController;
    local BioWeaponRanged weap;
    local Name RandomWeaponManf;
    
    if (Class'MERControlEngine'.default.bBattleRoyaleMode)
    {
        // Enable Battle Royale Mode
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