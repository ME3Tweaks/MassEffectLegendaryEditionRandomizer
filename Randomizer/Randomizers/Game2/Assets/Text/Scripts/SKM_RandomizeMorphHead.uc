public static function SFXSkeletalMeshActorMAT_RandomizeMorphHead(SFXSkeletalMeshActorMAT SKM)
{
    local BioMorphFace BMF;
    
    BMF = SKM.MorphHead;
    if (BMF == None && BioPawnType(SKM.ActorType) != None)
    {
        BMF = BioPawnType(SKM.ActorType).m_oMorphFace;
    }
    if ((BMF == None && SKM.HeadMesh != None) && SKM.HeadMesh.SkeletalMesh != None)
    {
        LogInternal("Making morphhead out of existing headmesh", );
        BMF = new (SKM) Class'BioMorphFace';
        BMF.m_oBaseHead = SKM.HeadMesh.SkeletalMesh;
        if (SKM.HairMesh != None)
        {
            BMF.m_oHairMesh = SKM.HairMesh.SkeletalMesh;
        }
        SKM.MorphHead = BMF;
    }
    LogInternal("SFXSKM Morph head: " $ BMF, );
    if (BMF != None && Class'MERControlEngine'.static.IsObjectModified(BMF) == FALSE)
    {
        Class'MERBioMorphUtility'.static.RandomizeBioMorphFace(BMF);
    }
}