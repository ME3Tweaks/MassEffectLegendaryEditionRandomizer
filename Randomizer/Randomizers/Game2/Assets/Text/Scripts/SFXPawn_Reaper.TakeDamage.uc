public event function TakeDamage(float DamageAmount, Controller EventInstigator, Vector HitLocation, Vector Momentum, Class<DamageType> DamageType, optional TraceHitInfo HitInfo, optional Actor DamageCauser)
{
    // Makes reaper not hurt himself as much
    // LogInternal(((((">>>>>>>>>>>>>>>>> Reaper took damage " $ DamageAmount) $ " from ") $ EventInstigator) $ ", ") $ DamageCauser, );
    if (BioPawn(EventInstigator.Pawn).FactionClass == FactionClass)
    {
        //LogInternal("HIT SAME TEAM!!!", );
        return;
    }
    Super(BioPawn).TakeDamage(DamageAmount, EventInstigator, HitLocation, Momentum, DamageType, HitInfo, DamageCauser);
}