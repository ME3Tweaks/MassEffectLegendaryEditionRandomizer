public static function BioPawn_RandomizeMorphHead(BioPawn BP)
{
    local BioMorphFace BMF;
    
    BMF = BP.MorphHead;
    if (BMF == None && BioPawnType(BP.ActorType) != None)
    {
        BMF = BioPawnType(BP.ActorType).m_oMorphFace;
    }
    if ((BMF == None && BP.HeadMesh != None) && BP.HeadMesh.SkeletalMesh != None)
    {
        LogInternal("Making morphhead out of existing headmesh", );
        BMF = new (BP) Class'BioMorphFace';
        BMF.m_oBaseHead = BP.HeadMesh.SkeletalMesh;
        if (BP.m_oHairMesh != None)
        {
            BMF.m_oHairMesh = BP.m_oHairMesh.SkeletalMesh;
        }
        BP.MorphHead = BMF;
    }
    LogInternal("BioPawn Morph head: " $ BMF, );
    if (BMF != None && Class'MERControlEngine'.static.IsObjectModified(BMF) == FALSE)
    {
        Class'MERBioMorphUtility'.static.RandomizeBioMorphFace(BMF);
    }
}