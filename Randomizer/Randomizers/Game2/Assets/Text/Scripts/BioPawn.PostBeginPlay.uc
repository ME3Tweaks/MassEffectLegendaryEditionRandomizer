public function PostBeginPlay()
{
    local SFXMovementData clonedMovementData;
    local BioGlobalVariableTable gv;
    local bool bAffectPlayer;
    local bool bAffectNonPlayer;
    
    PawnType = BioPawnType(ActorType);
    if (m_oBehavior == None)
    {
        m_oBehavior = BioPawnBehavior(oBioComponent);
    }
    Super.PostBeginPlay();
    m_oBehavior.PostBeginPlay();
    if (Squad == None)
    {
        Squad = m_oBehavior.Squad;
    }
    if (!bFullyInitialized)
    {
        bFullyInitialized = TRUE;
        Class'MERControl'.static.InitBioPawn(Self);
        InitializeSpeeds();
        AddDefaultInventory();
        if (Squad != None)
        {
            Squad.AddMember(Self, FALSE);
        }
        if (PawnType.m_oAppearance != None && PawnType.m_oAppearance.LifetimeCrust != None)
        {
            m_LifeCrust = Class'BioVisualEffect'.static.CreateVFXOnMesh(PawnType.m_oAppearance.LifetimeCrust, Self, 'None', , , FALSE);
            if (m_LifeCrust != None)
            {
                m_LifeCrust.SetPaused(TRUE);
            }
        }
        else if (PawnType.EliteCrust != None)
        {
            m_LifeCrust = Class'BioVisualEffect'.static.CreateVFXOnMesh(PawnType.EliteCrust, Self, 'None', , , FALSE);
            if (m_LifeCrust != None)
            {
                m_LifeCrust.SetPaused(TRUE);
            }
        }
        if (BioWorldInfo(WorldInfo).SelectableActors.Find(Self) == -1)
        {
            BioWorldInfo(WorldInfo).SelectableActors.AddItem(Self);
        }
        CreateSelection();
    }
}