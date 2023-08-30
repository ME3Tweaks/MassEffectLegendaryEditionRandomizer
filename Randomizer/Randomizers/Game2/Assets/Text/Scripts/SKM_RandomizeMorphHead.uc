public static function SFXSkeletalMeshActorMAT_RandomizeMorphHead(SFXSkeletalMeshActorMAT SKM){
    local BioMorphFace BMF;

    BMF = SKM.MorphHead;
    if (BMF == None && BioPawnType(SKM.ActorType) != None)
    {
        BMF = BioPawnType(SKM.ActorType).m_oMorphFace;
    }
    LogInternal("SFXSKM Morph head: " $ BMF, );
    if (BMF != None)
    {
        Class'MERBioMorphUtility'.static.RandomizeBioMorphFace(BMF);
    }
}