public function PostBeginPlay()
{
    local Class<BioBaseSquad> SquadClass;
    local BioAiController oController;
    local BioWeaponRanged weap;
    local Name RandomWeaponManf;
    
    Super.PostBeginPlay();
    m_oBehavior.PostBeginPlay();
    SetBaseEyeheight();
    CylinderComponent.SetTraceBlocking(default.CylinderComponent.BlockZeroExtent, default.CylinderComponent.BlockNonZeroExtent);
    m_oCustomCollision.SetTraceBlocking(default.m_oCustomCollision.BlockZeroExtent, default.m_oCustomCollision.BlockNonZeroExtent);
    Mesh.SetTraceBlocking(default.Mesh.BlockZeroExtent, default.Mesh.BlockNonZeroExtent);
    Mesh.SetActorCollision(default.Mesh.CollideActors, default.Mesh.BlockActors, default.Mesh.AlwaysCheckCollision);
    LogInternal("PostBeginPlay() on " $ Self, );
    if (SFXPawn_Player(Self) == None && m_oBehavior != None)
    {
        bAmbientCreature = FALSE;
        SquadClass = Class<BioBaseSquad>(FindObject("SFXStrategicAI.BioSquadCombat", Class'Class'));
        if (SquadClass != None)
        {
            LogInternal("Setting squad on " $ Self, );
            m_oBehavior.Squad = Spawn(SquadClass, Self);
            oController = BioAiController(Controller);
            m_oBehavior.m_bTargetable = TRUE;
            m_oBehavior.m_bTargetableOverride = TRUE;
            m_oBehavior.AddWeapon("NihlusShotgun", 1);
            m_oBehavior.m_Talents.AddBonusTalents(154);
            if (oController != None)
            {
                oController.ChangeAI(Class'BioAI_Charge', FALSE);
            }
        }
        else
        {
            LogInternal("Could not find object... on " $ Self, );
        }
    }
}