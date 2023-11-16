public function PostBeginPlay()
{
    local ActorReference WP;
    local int I;
    local NavigationPoint NP;
    local array<NavigationPoint> NPs;
    local array<NavigationPoint> AddedNPs;
    
    Super.PostBeginPlay();

    if (!class'MERControlEngine'.default.bBioWayPointSetRandomizer)
    {
        return;
    }
    
    // Add 8 random points to travel to.
    WaypointReferences.Length = 0;
    foreach AllActors(Class'NavigationPoint', NP, )
    {
        NPs.AddItem(NP);
    }
    for (; I < 8; I++)
    {
        NP = NPs[Rand(NPs.Length)];
        if (AddedNPs.Find(NP) == -1)
        {
            WP.Actor = NP;
            WaypointReferences.AddItem(WP);
            AddedNPs.AddItem(NP);
        }
    }
}