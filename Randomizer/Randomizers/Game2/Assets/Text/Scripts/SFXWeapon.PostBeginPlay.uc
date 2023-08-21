public simulated function PostBeginPlay()
{
    local float RangeMult;
    
    Super(Actor).PostBeginPlay();
    ScaleWeapon(0);
    if (TRUE)
    {
        RangeMult = Rand(24) == 0 ? 8.0 : 2.0;
        Damage.X *= FRand() * RangeMult;
        Damage.Y *= FRand() * RangeMult;
        MagSize.X *= FRand() * RangeMult;
        MagSize.Y *= FRand() * RangeMult;
        MagSize.X = float(Class'Object'.static.FCeil(MagSize.X));
        MagSize.Y = float(Class'Object'.static.FCeil(MagSize.Y));
        MaxSpareAmmo.X *= FRand() * RangeMult;
        MaxSpareAmmo.Y *= FRand() * RangeMult;
        MaxSpareAmmo.X = float(Class'Object'.static.FCeil(MaxSpareAmmo.X));
        MaxSpareAmmo.Y = float(Class'Object'.static.FCeil(MaxSpareAmmo.Y));
        MinAimError.X *= FRand() * RangeMult;
        MinAimError.Y *= FRand() * RangeMult;
        MaxAimError.X *= FRand() * RangeMult;
        MaxAimError.Y *= FRand() * RangeMult;
        MinZoomAimError.X *= FRand() * RangeMult;
        MinZoomAimError.Y *= FRand() * RangeMult;
        MaxZoomAimError.X *= FRand() * RangeMult;
        MaxZoomAimError.Y *= FRand() * RangeMult;
        RateOfFire.X *= FRand() * RangeMult;
        RateOfFire.Y *= FRand() * RangeMult;
        RateOfFire.X = float(Class'Object'.static.FCeil(RateOfFire.X));
        RateOfFire.Y = float(Class'Object'.static.FCeil(RateOfFire.Y));
        Recoil.X *= FRand() * RangeMult;
        Recoil.Y *= FRand() * RangeMult;
        ZoomRecoil.X *= FRand() * RangeMult;
        ZoomRecoil.Y *= FRand() * RangeMult;
        AI_AccCone_Min.X *= FRand() * RangeMult;
        AI_AccCone_Min.Y *= FRand() * RangeMult;
        AI_AccCone_Max.X *= FRand() * RangeMult;
        AI_AccCone_Max.Y *= FRand() * RangeMult;
        AI_BurstFireCount.X *= FRand() * RangeMult;
        AI_BurstFireCount.Y *= FRand() * RangeMult;
        if (Rand(10) == 0)
        {
            AI_BurstFireCount.X *= FRand() * 5.0;
            AI_BurstFireCount.Y *= FRand() * 5.0;
        }
        AI_BurstFireCount.X = float(Max(Class'Object'.static.Round(AI_BurstFireCount.X), 1));
        AI_BurstFireCount.Y = float(Max(Class'Object'.static.Round(AI_BurstFireCount.Y), 1));
        AI_HenchBurstFireMultiplier.X *= FRand() * RangeMult;
        AI_HenchBurstFireMultiplier.Y *= FRand() * RangeMult;
        AI_AimDelay.X *= FRand() * RangeMult;
        AI_AimDelay.Y *= FRand() * RangeMult;
        CrosshairRange.X *= FRand() * RangeMult;
        CrosshairRange.Y *= FRand() * RangeMult;
        ZoomCrosshairRange.X *= FRand() * RangeMult;
        ZoomCrosshairRange.Y *= FRand() * RangeMult;
        FrictionMultiplierRange.X *= FRand() * RangeMult;
        FrictionMultiplierRange.Y *= FRand() * RangeMult;
        AdhesionRot.X *= FRand() * RangeMult;
        AdhesionRot.Y *= FRand() * RangeMult;
        ZoomFOV *= FRand() * RangeMult;
        DamageAI *= FRand() * RangeMult;
        DamageHench *= FRand() * RangeMult;
        RateOfFireAI *= FRand() * RangeMult;
        AmmoPerShot *= FRand() * RangeMult;
        AmmoPerShot = float(Max(Class'Object'.static.FFloor(AmmoPerShot), 1));
        if (BurstRounds == 1.0 && Rand(5) == 0)
        {
            BurstRounds += 1.0;
        }
        BurstRounds *= FRand() * RangeMult;
        BurstRounds = float(Max(Class'Object'.static.Round(BurstRounds), 1));
        RecoilInterpSpeed *= FRand() * RangeMult;
        RecoilFadeSpeed *= FRand() * RangeMult;
        RecoilZoomFadeSpeed *= FRand() * RangeMult;
        RecoilCap *= FRand() * RangeMult;
        ZoomRecoilCap *= FRand() * RangeMult;
        RecoilYawScale *= FRand() * RangeMult;
        RecoilYawFrequency *= FRand() * RangeMult;
        TraceRange *= FRand() * RangeMult;
        AccFirePenalty *= FRand() * RangeMult;
        AccFireInterpSpeed *= FRand() * RangeMult;
        ZoomAccFirePenalty *= FRand() * RangeMult;
        ZoomAccFireInterpSpeed *= FRand() * RangeMult;
        MagneticCorrectionThresholdAngle *= FRand() * RangeMult;
        MaxMagneticCorrectionAngle *= FRand() * RangeMult;
        Range_Melee *= FRand() * RangeMult;
        Range_Short *= FRand() * RangeMult;
        Range_Medium *= FRand() * RangeMult;
        Range_Long *= FRand() * RangeMult;
        DamageMod_MeleeRange *= FRand() * RangeMult;
        DamageMod_ShortRange *= FRand() * RangeMult;
        DamageMod_MediumRange *= FRand() * RangeMult;
        DamageMod_LongRange *= FRand() * RangeMult;
        MinAdhesionDistance *= FRand() * RangeMult;
        MaxAdhesionDistance *= FRand() * RangeMult;
        MinAdhesionVelocity *= FRand() * RangeMult;
        CamInputAdhesionDamping *= FRand() * RangeMult;
        MaxLateralAdhesionDist *= FRand() * RangeMult;
        MinZoomSnapDistance *= FRand() * RangeMult;
        MaxZoomSnapDistance *= FRand() * RangeMult;
        ZoomSnapDuration *= FRand() * RangeMult;
    }
    InitializeAmmo();
    InitDefaultDecalProperties();
    CoverLeanPositions.Length = 0;
}