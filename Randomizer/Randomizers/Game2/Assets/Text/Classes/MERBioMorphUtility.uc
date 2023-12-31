﻿Class MERBioMorphUtility
    config(Engine);

// Functions
private static final function bool GetRandomAlgorithm(float randomizationLimit, Class<CustomMorphTargetSet> targetSet, out MorphRandomizationAlgorithm algorithm)
{
    local MorphRandomizationAlgorithm generatedAlgo;
    local int I;
    local int NumToDo;
    
    generatedAlgo.AlgoName = "Generated";
    generatedAlgo.Randomizations.Length = targetSet.default.BaseMorphTargets.Length;
    for (I = 0; I < targetSet.default.BaseMorphTargets.Length; I++)
    {
        generatedAlgo.Randomizations[I].Feature = string(targetSet.default.BaseMorphTargets[I].TargetName);
        generatedAlgo.Randomizations[I].Min = -randomizationLimit;
        generatedAlgo.Randomizations[I].Max = randomizationLimit;
        generatedAlgo.Randomizations[I].MergeMode = Rand(5) == 0 ? EBMFFeatureMergeMode.Multiplicative : EBMFFeatureMergeMode.Exact;
        if (FALSE)
        {
            LogInternal((((((("Add feature to algo: " $ targetSet.default.BaseMorphTargets[I].TargetName) $ "(") $ generatedAlgo.Randomizations[I].Min) $ ",") $ generatedAlgo.Randomizations[I].Max) $ ") for ") $ targetSet, );
        }
        if (targetSet == Class'HMM_BaseMorphSet')
        {
            generatedAlgo.Randomizations[I].AddIfNotFound = Class'MERControlEngine'.static.IsStringCapitalized(generatedAlgo.Randomizations[I].Feature) ? Rand(4) == 0 : TRUE;
            continue;
        }
        if (targetSet == Class'HMF_BaseMorphSet')
        {
            generatedAlgo.Randomizations[I].AddIfNotFound = Class'MERControlEngine'.static.IsStringCapitalized(generatedAlgo.Randomizations[I].Feature) ? Rand(3) > 0 : TRUE;
            continue;
        }
        generatedAlgo.Randomizations[I].AddIfNotFound = TRUE;
    }
    algorithm = generatedAlgo;
    return TRUE;
}
public static function RandomizeBioMorphFace(BioMorphFace BMF, float randomizationLimit)
{
    local int I;
    local int J;
    local bool modified;
    local bool found;
    local MorphRandomizationAlgorithm algorithm;
    local bool hasAlgorithm;
    local Class<CustomMorphTargetSet> MorphTargetSet;
    
    MorphTargetSet = GetMorphTargetSet(BMF);
    if (MorphTargetSet == None)
    {
        LogInternal("COULD NOT FIND MORPH TARGET SET FOR: " $ BMF.m_oBaseHead, );
        return;
    }
    hasAlgorithm = GetRandomAlgorithm(randomizationLimit, MorphTargetSet, algorithm);
    if (!hasAlgorithm)
    {
        LogInternal("Could not find algorithm for " $ MorphTargetSet, );
        return;
    }
    LogInternal("Algorithm: " $ algorithm.Randomizations.Length, );
    for (J = 0; J < algorithm.Randomizations.Length; J++)
    {
        found = FALSE;
        for (I = 0; I < BMF.m_aMorphFeatures.Length; I++)
        {
            if (InStr(string(BMF.m_aMorphFeatures[I].sFeatureName), algorithm.Randomizations[J].Feature, , TRUE, ) >= 0)
            {
                if (FALSE)
                {
                    LogInternal("Updating morph feature: " $ BMF.m_aMorphFeatures[I].sFeatureName, );
                }
                BMF.m_aMorphFeatures[I] = RandomizeMorphTarget(BMF.m_aMorphFeatures[I], algorithm.Randomizations[J]);
                modified = TRUE;
                found = TRUE;
                break;
            }
        }
        if (!found && algorithm.Randomizations[J].AddIfNotFound)
        {
            if (FALSE)
            {
                LogInternal("Adding morph feature: " $ algorithm.Randomizations[J].Feature, );
            }
            BMF.m_aMorphFeatures[BMF.m_aMorphFeatures.Length] = MakeNewMorphFeature(algorithm.Randomizations[J]);
            modified = TRUE;
        }
    }
    if (modified)
    {
        Class'MorphTargetUtility'.static.UpdateMeshAndSkeleton(BMF, MorphTargetSet);
        Class'MERControlEngine'.static.MarkObjectModified(BMF);
    }
    else
    {
        LogInternal("Not modified: " $ BMF, );
    }
}
private static final function MorphFeature RandomizeMorphTarget(MorphFeature MT, BMFFeatureRandomization falgorithm)
{
    if (falgorithm.MergeMode == EBMFFeatureMergeMode.Additive)
    {
        MT.Offset += Class'MERControlEngine'.static.RandFloat(falgorithm.Min, falgorithm.Max);
    }
    else if (falgorithm.MergeMode == EBMFFeatureMergeMode.Multiplicative)
    {
        MT.Offset *= Class'MERControlEngine'.static.RandFloat(falgorithm.Min, falgorithm.Max);
    }
    else
    {
        MT.Offset = Class'MERControlEngine'.static.RandFloat(falgorithm.Min, falgorithm.Max);
    }
    return MT;
}
public static final function Class<CustomMorphTargetSet> GetMorphTargetSet(BioMorphFace BMF)
{
    local Name BaseName;
    
    BaseName = BMF.m_oBaseHead.Name;
    if (InStr(string(BaseName), "hmf_", , TRUE, ) >= 0)
    {
        return Class'HMF_BaseMorphSet';
    }
    if (InStr(string(BaseName), "hmm_", , TRUE, ) >= 0)
    {
        return Class'HMM_BaseMorphSet';
    }
    if (InStr(string(BaseName), "kro_", , TRUE, ) >= 0)
    {
        return Class'KRO_baseMorphSet';
    }
    if (InStr(string(BaseName), "asa_", , TRUE, ) >= 0)
    {
        return Class'ASA_BaseMorphSet';
    }
    if (InStr(string(BaseName), "sal_", , TRUE, ) >= 0)
    {
        return Class'SAL_BaseMorphSet';
    }
    if (InStr(string(BaseName), "tur_", , TRUE, ) >= 0)
    {
        return Class'TUR_BaseMorphSet';
    }
    if (InStr(string(BaseName), "bat_", , TRUE, ) >= 0)
    {
        return Class'BAT_BaseMorphSet';
    }
    if (InStr(string(BaseName), "aln_", , TRUE, ) >= 0)
    {
        LogInternal("not implemented", );
    }
    if (InStr(string(BaseName), "drl_", , TRUE, ) >= 0)
    {
        return Class'HMM_BaseMorphSet';
    }
    return None;
}
private static final function MorphFeature MakeNewMorphFeature(BMFFeatureRandomization Feature)
{
    local MorphFeature MF;
    
    MF.sFeatureName = Name(Feature.Feature);
    MF.Offset = Class'MERControlEngine'.static.RandFloat(Feature.Min, Feature.Max);
    return MF;
}
public static function RandomizeCCMorphFace(BioMorphFace BMF, float randomizationLimit, SFXMorphFaceFrontEndDataSource dataSource)
{
    local int I;
    local int J;
    local bool modified;
    local bool found;
    local bool hasAlgorithm;
    local MorphRandomizationAlgorithm algorithm;
    local Class<CustomMorphTargetSet> MorphTargetSet;
    
    MorphTargetSet = GetMorphTargetSet(BMF);
    if (MorphTargetSet == None)
    {
        LogInternal("COULD NOT FIND MORPH TARGET SET FOR: " $ BMF.m_oBaseHead, );
        return;
    }
    hasAlgorithm = GetCCAlgorithm(dataSource, randomizationLimit, MorphTargetSet, algorithm);
    if (!hasAlgorithm)
    {
        LogInternal("Could not generate algorithm for " $ MorphTargetSet, );
        return;
    }
    BMF.m_aMorphFeatures.Length = 0;
    for (J = 0; J < algorithm.Randomizations.Length; J++)
    {
        found = FALSE;
        for (I = 0; I < BMF.m_aMorphFeatures.Length; I++)
        {
            if (InStr(string(BMF.m_aMorphFeatures[I].sFeatureName), algorithm.Randomizations[J].Feature, , TRUE, ) >= 0)
            {
                if (FALSE)
                {
                    LogInternal("Updating morph feature: " $ BMF.m_aMorphFeatures[I].sFeatureName, );
                }
                BMF.m_aMorphFeatures[I] = RandomizeMorphTarget(BMF.m_aMorphFeatures[I], algorithm.Randomizations[J]);
                modified = TRUE;
                found = TRUE;
                break;
            }
        }
        if (!found && algorithm.Randomizations[J].AddIfNotFound)
        {
            if (FALSE)
            {
                LogInternal("Adding morph feature: " $ algorithm.Randomizations[J].Feature, );
            }
            BMF.m_aMorphFeatures[BMF.m_aMorphFeatures.Length] = MakeNewMorphFeature(algorithm.Randomizations[J]);
            modified = TRUE;
        }
    }
    if (modified)
    {
        Class'MorphTargetUtility'.static.UpdateMeshAndSkeleton(BMF, MorphTargetSet);
        Class'MERControlEngine'.static.MarkObjectModified(BMF);
    }
    else
    {
        LogInternal("Not modified: " $ BMF, );
    }
}
private static final function bool GetCCAlgorithm(SFXMorphFaceFrontEndDataSource dataSource, float randomizationLimit, Class<CustomMorphTargetSet> targetSet, out MorphRandomizationAlgorithm algorithm)
{
    local MorphRandomizationAlgorithm generatedAlgo;
    local int I;
    local int J;
    local int K;
    local array<string> MorphFeatures;
    local Category Category;
    local Slider Slider;
    local BioMorphFaceFESliderBase sliderData;
    local BioMorphFaceFESliderMorph SliderMorph;
    local CCAlgorithm cachedAlgo;
    
    generatedAlgo.AlgoName = "CCGenerated";
    for (I = 0; I < dataSource.MorphCategories.Length; I++)
    {
        Category = dataSource.MorphCategories[I];
        for (J = 0; J < Category.m_aoSliders.Length; J++)
        {
            Slider = Category.m_aoSliders[J];
            for (K = 0; K < Slider.m_aoSliderData.Length; K++)
            {
                SliderMorph = BioMorphFaceFESliderMorph(Slider.m_aoSliderData[K]);
                if (SliderMorph != None)
                {
                    if (Rand(2) == 0)
                    {
                        MorphFeatures.AddItem(SliderMorph.m_sMorph_Positive);
                        continue;
                    }
                    MorphFeatures.AddItem(SliderMorph.m_sMorph_Negative);
                }
            }
        }
    }
    generatedAlgo.Randomizations.Length = MorphFeatures.Length;
    for (I = 0; I < MorphFeatures.Length; I++)
    {
        generatedAlgo.Randomizations[I].Feature = MorphFeatures[I];
        generatedAlgo.Randomizations[I].Min = -randomizationLimit;
        generatedAlgo.Randomizations[I].Max = randomizationLimit;
        generatedAlgo.Randomizations[I].MergeMode = EBMFFeatureMergeMode.Exact;
        if (targetSet == Class'HMM_BaseMorphSet')
        {
            generatedAlgo.Randomizations[I].AddIfNotFound = Class'MERControlEngine'.static.IsStringCapitalized(generatedAlgo.Randomizations[I].Feature) ? Rand(4) == 0 : TRUE;
            continue;
        }
        if (targetSet == Class'HMF_BaseMorphSet')
        {
            generatedAlgo.Randomizations[I].AddIfNotFound = Class'MERControlEngine'.static.IsStringCapitalized(generatedAlgo.Randomizations[I].Feature) ? Rand(3) > 0 : TRUE;
            continue;
        }
        generatedAlgo.Randomizations[I].AddIfNotFound = TRUE;
    }
    algorithm = generatedAlgo;
    return TRUE;
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}