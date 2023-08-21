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
        if (PawnType.m_oAppearance != None)
        {
            gv = BioWorldInfo(WorldInfo).GetGlobalVariables();
            bAffectPlayer = gv.GetBool(1350038) && SFXPawn_Player(Self) != None;
            bAffectNonPlayer = gv.GetBool(1350039) && SFXPawn_Player(Self) == None;
            LogInternal((("bAffectPlayer for " $ Self) $ ": ") $ bAffectPlayer, );
            LogInternal((("bAffectNonPlayer for " $ Self) $ ": ") $ bAffectNonPlayer, );
            if (bAffectPlayer || bAffectNonPlayer)
            {
                clonedMovementData = new (PawnType.m_oAppearance, "MERMovementData") Class'SFXMovementData';
                clonedMovementData.WalkSpeed = PawnType.m_oAppearance.MovementInfo.WalkSpeed;
                clonedMovementData.GroundSpeed = PawnType.m_oAppearance.MovementInfo.GroundSpeed;
                clonedMovementData.TurnSpeed = PawnType.m_oAppearance.MovementInfo.TurnSpeed;
                clonedMovementData.CombatWalkSpeed = PawnType.m_oAppearance.MovementInfo.CombatWalkSpeed;
                clonedMovementData.CombatGroundSpeed = PawnType.m_oAppearance.MovementInfo.CombatGroundSpeed;
                clonedMovementData.CoverGroundSpeed = PawnType.m_oAppearance.MovementInfo.CoverGroundSpeed;
                clonedMovementData.CoverCrouchGroundSpeed = PawnType.m_oAppearance.MovementInfo.CoverCrouchGroundSpeed;
                clonedMovementData.CrouchGroundSpeed = PawnType.m_oAppearance.MovementInfo.CrouchGroundSpeed;
                clonedMovementData.StormTurnSpeed = PawnType.m_oAppearance.MovementInfo.StormTurnSpeed;
                clonedMovementData.AirSpeed = PawnType.m_oAppearance.MovementInfo.AirSpeed;
                clonedMovementData.AccelRate = PawnType.m_oAppearance.MovementInfo.AccelRate;
                clonedMovementData.WalkSpeed = float((Rand(200) + 100)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.GroundSpeed = float((Rand(200) + 500)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.TurnSpeed = float((Rand(200) + 620)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.CombatWalkSpeed = float((Rand(50) + 75)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.CombatGroundSpeed = float((Rand(100) + 150)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.CoverGroundSpeed = float((Rand(100) + 50)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.CoverCrouchGroundSpeed = float((Rand(100) + 50)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.CrouchGroundSpeed = float((Rand(100) + 50)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.StormTurnSpeed = float((Rand(400) + 700)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.AirSpeed = float((Rand(200) + 500)) * (Rand(6) == 0 ? 2.0 : 1.0);
                clonedMovementData.AccelRate = float((Rand(600) + 900)) * (Rand(6) == 0 ? 2.0 : 1.0);
                PawnType.m_oAppearance.MovementInfo = clonedMovementData;
            }
        }
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