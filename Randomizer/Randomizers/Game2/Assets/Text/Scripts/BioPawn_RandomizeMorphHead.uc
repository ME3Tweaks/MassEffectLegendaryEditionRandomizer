public static function BioPawn_RandomizeMorphHead(BioPawn BP){
    local BioMorphFace BMF;

    BMF = BP.MorphHead;
    if (BMF == None && BioPawnType(BP.ActorType) != None)
    {
        BMF = BioPawnType(BP.ActorType).m_oMorphFace;
    }
    LogInternal("BioPawn Morph head: " $ BMF, );
    if (BMF != None && class'MERControlEngine'.static.IsObjectModified(BMF) == FALSE)
    {
        Class'MERBioMorphUtility'.static.RandomizeBioMorphFace(BMF);
    }
}