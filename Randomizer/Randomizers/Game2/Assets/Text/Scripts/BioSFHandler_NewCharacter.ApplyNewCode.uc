public function ApplyNewCode(string sInputCode)
{
    local int nNumCategories;
    local int nNumSliders;
    local int I;
    local int J;
    local int nIndex;
    local int nValue;
    local BioWorldInfo oBWI;
    local BioPawn BP;
    
    BP = lstTemplates[int(m_nCurrentTemplate)];
    if (BP == None)
    {
        return;
    }
    Class'MERBioMorphUtility'.static.RandomizeCCMorphFace(BP.MorphHead, Class'MERControlEngine'.default.fCCBioMorphFaceRandomization, m_bMaleSelected ? MaleDataSource : FemaleDataSource);
    Class'MERBioMorphUtility'.static.RandomizeCCMorphFace(BioWorldInfo(oWorldInfo).m_UIWorld.GetSpawnedPawn(lstTemplates[int(m_nCurrentTemplate)]).MorphHead, Class'MERControlEngine'.default.fCCBioMorphFaceRandomization, m_bMaleSelected ? MaleDataSource : FemaleDataSource);
}