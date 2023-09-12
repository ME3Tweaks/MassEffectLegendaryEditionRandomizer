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
    if ((ForcedEnemy != None && ForcedEnemy.IsValidTargetFor(Self)) && ForcedEnemy.IsInvisible() == FALSE)
    {
        AILog_Internal("Selecting forced target:" @ ForcedEnemy);
        AILog_Internal((("SetEnemy " $ Enemy) $ " to ") $ ForcedEnemy, 'Enemy');
        Enemy = ForcedEnemy;
    }
    else
    {
        BestIdx = -1;
        BestRating = -1.0;
        for (Idx = 0; Idx < EnemyList.Length; Idx++)
        {
            EnemyPawn = EnemyList[Idx].Pawn;
            if (EnemyPawn == None || EnemyPawn.IsValidTargetFor(Self) == FALSE)
            {
                AILog_Internal("Warning: Invalid pawn contained in the enemy list", 'Enemy');
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
            Distance = VSize(GetEnemyLocationByIndex(Idx) - Pawn.Location);
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
            if (EnemyPawn == PreferredTarget)
            {
                Rating *= 1.89999998;
                AILog_Internal("    >>> Enemy is preferred target = " $ Rating, 'Enemy');
            }
            if (EnemyPawn.IsHumanControlled())
            {
                Rating *= 1.5;
                AILog_Internal("    >>> Enemy is the player = " $ Rating, 'Enemy');
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
            if (Distance > EnemyDistance_Short)
            {
                AdjustRatingByTickets(Rating, Idx);
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