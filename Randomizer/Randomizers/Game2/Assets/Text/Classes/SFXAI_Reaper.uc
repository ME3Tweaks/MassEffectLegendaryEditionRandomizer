Class SFXAI_Reaper extends SFXAI_Core
    placeable
    config(AI);

// Variables
var bool HasUsedLowHPAttack;

// Functions
public function Vector GetAimLocation(int EnemyIdx, Vector StartLoc)
{
    local Vector AimLoc;
    local BioPawn oEnemyPawn;
    
    AimLoc = Super.GetAimLocation(EnemyIdx, StartLoc);
    if (EnemyIdx >= 0)
    {
        oEnemyPawn = BioPawn(EnemyList[EnemyIdx].Pawn);
    }
    else
    {
        oEnemyPawn = BioPawn(FireTarget);
    }
    if (oEnemyPawn != None)
    {
        AimLoc.Z -= oEnemyPawn.CylinderComponent.CollisionHeight;
    }
    return AimLoc;
}

public function Tick(float DeltaSeconds)
{
    Super(BioAiController).Tick(DeltaSeconds);
    if ((FireTarget != None && SFXPawn_Player(FireTarget) == None) && SFXPawn_Henchman(FireTarget) == None)
    {
        LogInternal(("MER: Invalid target: " $ FireTarget) $ ". Reselecting enemy", );
        SelectTarget();
    }
}
public function FillEnemyList()
{
    local Controller Player;
    
    Super(BioAiController).FillEnemyList();
    LogInternal("See Player", );
    Player = BioWorldInfo(Class'BioWorldInfo'.static.GetWorldInfo()).GetLocalPlayerController();
    NotifyTakeHit(Player, vect(0.0, 0.0, 0.0), 0, None, vect(0.0, 0.0, 0.0));
}
public function FireWeaponAtTarget(Actor oTarget, bool bCheckLOS, optional delegate<FireWeaponDelegate> FireDelegate)
{
    if (MyBP != None && MyBP.Weapon == None)
    {
        AILog_Internal("The Oculus does not have a weapon equipped.  Equipping the Oculus particle beam", 'External');
        MyBP.SetWeaponImmediately(Class'SFXHeavyWeapon_OculusParticleBeam');
    }
    m_bCheckLOS = bCheckLOS;
    FireTarget = oTarget;
    __FireWeaponDelegate__Delegate = FireDelegate;
    PushState('FiringWeapon');
}
public function bool SelectTarget()
{
    local Actor OldTarget;
    local int Idx;
    local int BestIdx;
    local float Rating;
    local float BestRating;
    local float Distance;
    local Pawn EnemyPawn;
    local CoverInfo EnemyCover;
    local bool bResult;
    local Pawn ForcedEnemy;
    
    AILog_Internal("Select Enemy...", 'Enemy');
    if (HasValidTarget() && WorldInfo.TimeSeconds - TargetAcquisitionTime < 0.100000001)
    {
        AILog_Internal("Aborting target selction, already acquired one this frame");
        return TRUE;
    }
    AILog_Internal("Looking for new target, current:" @ FireTarget, 'Enemy');
    OldTarget = FireTarget;
    ForcedEnemy = Pawn(ForcedTarget);
    if (ForcedEnemy != None && ForcedEnemy.IsValidTargetFor(Self))
    {
        AILog_Internal("Selecting forced target:" @ ForcedEnemy);
        AILog_Internal((("SetEnemy " $ Enemy) $ " to ") $ ForcedEnemy, 'Enemy');
        Enemy = ForcedEnemy;
    }
    else
    {
        BestIdx = -1;
        BestRating = -1.0;
        AILog_Internal("Looping enemy list");
        for (Idx = 0; Idx < EnemyList.Length; Idx++)
        {
            EnemyPawn = EnemyList[Idx].Pawn;
            AILog_Internal("Evaluating: " $ EnemyPawn);
            if (EnemyPawn == None || EnemyPawn.IsValidTargetFor(Self) == FALSE)
            {
                AILog_Internal("Warning: Invalid pawn contained in the enemy list", 'Enemy');
                continue;
            }
            if (SFXPawn_Player(EnemyPawn) == None && SFXPawn_Henchman(EnemyPawn) == None)
            {
                AILog_Internal("MER Warning: Reaper is not allowed to target anything but player and party");
                continue;
            }
            AILog_Internal(((("Rate: " $ EnemyList[Idx].Pawn) $ " (") $ EnemyList[Idx].Pawn.Tag) $ ")", 'Enemy');
            if (EnemyPawn.IsInState('Downed', ))
            {
                AILog_Internal("    >>> Enemy is downed", 'Enemy');
                continue;
            }
            if (IgnoredTargets.Find(EnemyPawn) >= 0)
            {
                AILog_Internal("    >>> Enemy is ignored target", 'Enemy');
                continue;
            }
            if ((BioPawn(EnemyPawn) != None && BioPawn(EnemyPawn).Squad != None) && IgnoredSquads.Find(BioPawn(EnemyPawn).Squad) >= 0)
            {
                AILog_Internal("    >>> Enemy is in ignored squad", 'Enemy');
                continue;
            }
            Distance = VSize(GetEnemyLocationByIndex(Idx, 2) - Pawn.Location);
            if (Distance < EnemyDistance_Melee)
            {
                Rating = GetRangeValueByPct(vect2d(8.0, 4.0), Distance / EnemyDistance_Melee);
            }
            else if (Distance < EnemyDistance_Short)
            {
                Rating = GetRangeValueByPct(vect2d(4.0, 2.0), (Distance - EnemyDistance_Melee) / (EnemyDistance_Short - EnemyDistance_Melee));
            }
            else if (Distance < EnemyDistance_Medium)
            {
                Rating = GetRangeValueByPct(vect2d(2.0, 1.0), (Distance - EnemyDistance_Short) / (EnemyDistance_Medium - EnemyDistance_Short));
            }
            else if (Distance < EnemyDistance_Long)
            {
                Rating = GetRangeValueByPct(vect2d(1.0, 0.5), (Distance - EnemyDistance_Long) / (EnemyDistance_Long - EnemyDistance_Medium));
            }
            else
            {
                Rating = 0.349999994;
            }
            AILog_Internal((("    >>> Distance Rating (" $ Distance / float(100)) $ "m) = ") $ Rating, 'Enemy');
            if (Pawn.Weapon != None && !Pawn.Weapon.IsA('SFXHeavyWeapon_ParticleBeam'))
            {
                if (EnemyPawn == OldTarget)
                {
                    if (m_bFailedTicket)
                    {
                        Rating *= 0.600000024;
                        AILog_Internal("    >>> Enemy is current target, but failed last attack = " $ Rating, 'Enemy');
                        m_bFailedTicket = FALSE;
                    }
                    else
                    {
                        Rating *= 1.5;
                        AILog_Internal("    >>> Enemy is current target = " $ Rating, 'Enemy');
                    }
                }
            }
            if (EnemyPawn == PreferredTarget)
            {
                Rating *= 1.89999998;
                AILog_Internal("    >>> Enemy is preferred target = " $ Rating, 'Enemy');
            }
            if (Pawn.Weapon != None && !Pawn.Weapon.IsA('SFXHeavyWeapon_ParticleBeam'))
            {
                if (EnemyPawn.IsHumanControlled())
                {
                    Rating *= 1.5;
                    AILog_Internal("    >>> Enemy is the player = " $ Rating, 'Enemy');
                }
            }
            if (Pawn.LastHitBy == EnemyPawn.Controller && ((Enemy == None || Enemy.Controller == None) || Enemy.Controller != Self))
            {
                Rating *= 1.25;
            }
            if (Pawn.LastHitBy != None)
            {
                AILog_Internal((("    >>> LastHitBy (" $ Pawn.LastHitBy.Pawn) $ ") = ") $ Rating, 'Enemy');
            }
            else
            {
                AILog_Internal("    >>> LastHitBy (None) = " $ Rating, 'Enemy');
            }
            if (Distance > EnemyDistance_Short)
            {
                if (IsEnemyVisibleByIndex(Idx) == FALSE)
                {
                    if (TimeSinceEnemyVisible(Idx) < 5.0)
                    {
                        Rating *= 0.5;
                    }
                    else
                    {
                        Rating *= 0.100000001;
                    }
                }
            }
            AILog_Internal("    >>> Visibility =" @ Rating, 'Enemy');
            if (GetPawnCover(EnemyPawn, EnemyCover) == FALSE)
            {
                Rating *= 1.5;
                AILog_Internal("    >>> Not in cover = " $ Rating, 'Enemy');
            }
            if (FALSE)
            {
                if (Distance > EnemyDistance_Short)
                {
                    AdjustRatingByTickets(Rating, Idx);
                }
            }
            AdjustEnemyRating(EnemyPawn, Rating);
            AILog_Internal("    >>> Final Rating =" @ Rating, 'Enemy');
            if (Rating >= 0.0 && (BestRating < 0.0 || Rating > BestRating))
            {
                BestIdx = Idx;
                BestRating = Rating;
            }
        }
        if (BestIdx >= 0 && BestIdx < EnemyList.Length)
        {
            AILog_Internal(("Selected" @ EnemyList[BestIdx].Pawn) @ BestRating, 'Enemy');
            AILog_Internal((("SetEnemy " $ Enemy) $ " to ") $ EnemyList[BestIdx].Pawn, 'Enemy');
            Enemy = EnemyList[BestIdx].Pawn;
        }
        else
        {
            AILog_Internal(("SetEnemy " $ Enemy) $ " to None", 'Enemy');
            Enemy = None;
        }
    }
    bResult = HasValidEnemy();
    if (bResult)
    {
        FireTarget = Enemy;
    }
    else
    {
        LogInternal("AI does not have a valid enemy.", );
        FireTarget = None;
    }
    if (OldTarget != FireTarget)
    {
        TargetAcquisitionTime = WorldInfo.TimeSeconds;
        ReleaseTicket(OldTarget, 2, TRUE);
        ReleaseTicket(OldTarget, 1);
        AcquireTicket(FireTarget, 1);
        TriggerAttackVocalization();
        OnTargetChanged();
    }
    ShotTarget = Pawn(FireTarget);
    return bResult;
}
public function OnTargetChanged()
{
    AILog_Internal("MER: Target changed - new target is " $ FireTarget, 'Attack');
}

// States
state Combat_Reaper extends Combat 
{
    // State Functions
    
    // State code
Begin:
    while (TRUE)
    {
        while (SelectTarget() == FALSE)
        {
            Sleep(1.0);
        }
        if (Enemy != None && Pawn != None)
        {
            Focus = Enemy;
        }
        Attack();
        Sleep(0.200000003);
    }
    stop;
};

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
    Begin Template Class=Goal_AtCover Name=AtCov_EvalAggressive
        Begin Template Class=CovGoal_Enemies Name=CovGoal_Enemies0
        End Template
        Begin Template Class=CovGoal_MovementDistance Name=CovGoal_MovDistAggressive
        End Template
        Begin Template Class=CovGoal_TeammateProximity Name=CovGoal_TeamProx0
        End Template
        Begin Template Class=CovGoal_WeaponRange Name=CovGoal_WeaponRange0
        End Template
        CoverGoalConstraints = (CovGoal_WeaponRange0, CovGoal_Enemies0, CovGoal_TeamProx0, CovGoal_MovDistAggressive)
    End Template
    Begin Template Class=Goal_AtCover Name=AtCov_EvalDefensive
        Begin Template Class=CovGoal_Enemies Name=CovGoal_Enemies0
        End Template
        Begin Template Class=CovGoal_MovementDistance Name=CovGoal_MovDistDefensive
        End Template
        Begin Template Class=CovGoal_TeammateProximity Name=CovGoal_TeamProx0
        End Template
        CoverGoalConstraints = (CovGoal_Enemies0, CovGoal_MovDistDefensive, CovGoal_TeamProx0)
    End Template
    Begin Template Class=Goal_AtCover Name=AtCov_EvalNearGoal
        Begin Template Class=CovGoal_Enemies Name=CovGoal_Enemies0
        End Template
        Begin Template Class=CovGoal_GoalProximity Name=CovGoal_NearMoveGoal
        End Template
        CoverGoalConstraints = (CovGoal_NearMoveGoal, CovGoal_Enemies0)
    End Template
    Begin Template Class=Goal_AtCover Name=AtCov_EvalWeaponRange
        Begin Template Class=CovGoal_Enemies Name=CovGoal_Enemies0
        End Template
        Begin Template Class=CovGoal_MovementDistance Name=CovGoal_MovDistWeapon
        End Template
        Begin Template Class=CovGoal_TeammateProximity Name=CovGoal_TeamProx0
        End Template
        Begin Template Class=CovGoal_WeaponRange Name=CovGoal_WeaponRange0
        End Template
        CoverGoalConstraints = (CovGoal_WeaponRange0, CovGoal_Enemies0, CovGoal_TeamProx0, CovGoal_MovDistWeapon)
    End Template
    Begin Template Class=Goal_AtGoHereCover Name=AtCov_EvalGoHere
        Begin Template Class=CovGoal_Enemies Name=CovGoal_Enemies0
        End Template
        Begin Template Class=CovGoal_GoalProximity Name=CovGoal_NearGoHereGoal
        End Template
        Begin Template Class=CovGoal_MovementDistance Name=CovGoal_MovDistGoHere
        End Template
        CoverGoalConstraints = (CovGoal_Enemies0, CovGoal_MovDistGoHere, CovGoal_NearGoHereGoal)
    End Template
    BehaviourList = ('Combat_Reaper')
    m_nmCurrentBehavior = 'Combat_Reaper'
    AtCover_WeaponRange = AtCov_EvalWeaponRange
    AtCover_Defensive = AtCov_EvalDefensive
    AtCover_Aggressive = AtCov_EvalAggressive
    AtCover_NearMoveGoal = AtCov_EvalNearGoal
    AtCover_AIGoHere = AtCov_EvalGoHere
    m_fFiringArcAngle = -1.0
    bUseTicketing = FALSE
    m_nFireLineObstructionFrequency = 1000000000
    EnemyDistance_Long = 6800.0
    EnemyDistance_Medium = 6000.0
    EnemyDistance_Short = 5500.0
}