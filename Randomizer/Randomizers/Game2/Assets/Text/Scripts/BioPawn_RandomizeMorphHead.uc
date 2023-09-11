public static function BioPawn_RandomizeMorphHead(BioPawn BP)
{
    local BioMorphFace BMF;
    
    // Do not randomize character creator
    if (((BP.Tag == 'Iconic_Male_Template' || BP.Tag == 'Iconic_Female_Template') || BP.Tag == 'Custom_Male_Template') || BP.Tag == 'Custom_Female_Template')
    {
        return;
    }
    
    BMF = BP.MorphHead;
    if (BMF == None && BioPawnType(BP.ActorType) != None)
    {
        BMF = BioPawnType(BP.ActorType).m_oMorphFace;
    }
    if ((BMF == None && BP.HeadMesh != None) && BP.HeadMesh.SkeletalMesh != None)
    {
        BMF = new (BP) Class'BioMorphFace';
        BMF.m_oBaseHead = BP.HeadMesh.SkeletalMesh;
        if (BP.m_oHairMesh != None)
        {
            BMF.m_oHairMesh = BP.m_oHairMesh.SkeletalMesh;
        }
        BP.MorphHead = BMF;
    }
    if (BMF != None && Class'MERControlEngine'.static.IsObjectModified(BMF) == FALSE)
    {
        Class'MERBioMorphUtility'.static.RandomizeBioMorphFace(BMF, Class'MERControlEngine'.default.fBioMorphFaceRandomization);
    }
}