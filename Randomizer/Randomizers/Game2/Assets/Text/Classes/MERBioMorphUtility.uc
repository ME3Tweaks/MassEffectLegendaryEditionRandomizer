Class MERBioMorphUtility
    config(Engine);

// Types
struct BMFFeatureRandomization 
{
    var string Feature;
    var float Min;
    var float Max;
    var EBMFFeatureMergeMode MergeMode;
    var bool AddIfNotFound;
};
struct MorphRandomizationAlgorithm 
{
    var string AlgoName;
    var array<BMFFeatureRandomization> Randomizations;
};
enum EBMFFeatureMergeMode
{
    Multiplicative,
    Additive,
    Exact,
};

// Variables
var config array<MorphRandomizationAlgorithm> HMMMorphAlgorithms;
var config array<MorphRandomizationAlgorithm> HMFMorphAlgorithms;
var config array<MorphRandomizationAlgorithm> ASAMorphAlgorithms;
var config array<MorphRandomizationAlgorithm> KROMorphAlgorithms;
var config array<MorphRandomizationAlgorithm> SALMorphAlgorithms;
var config array<MorphRandomizationAlgorithm> TURMorphAlgorithms;
var config array<MorphRandomizationAlgorithm> VORMorphAlgorithms;
var config array<MorphRandomizationAlgorithm> SharedMorphAlgorithms;

// Functions
private static final function bool GetRandomAlgorithm(Class<CustomMorphTargetSet> targetSet, out MorphRandomizationAlgorithm algorithm)
{
    local MorphRandomizationAlgorithm generatedAlgo;
    local int I;
    local int NumToDo;
    
    generatedAlgo.AlgoName = "Generated";
    generatedAlgo.Randomizations.Length = targetSet.default.BaseMorphTargets.Length;
    for (I = 0; I < targetSet.default.BaseMorphTargets.Length; I++)
    {
        generatedAlgo.Randomizations[I].Feature = string(targetSet.default.BaseMorphTargets[I].TargetName);
        generatedAlgo.Randomizations[I].Min = -Class'MERControlEngine'.default.fBioMorphFaceRandomization;
        generatedAlgo.Randomizations[I].Max = Class'MERControlEngine'.default.fBioMorphFaceRandomization;
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
    LogInternal("The following is currently unused", );
    if (targetSet == Class'HMM_BaseMorphSet' && default.HMMMorphAlgorithms.Length > 0)
    {
        algorithm = default.HMMMorphAlgorithms[Rand(default.HMMMorphAlgorithms.Length)];
        return TRUE;
    }
    if (targetSet == Class'HMF_BaseMorphSet' && default.HMFMorphAlgorithms.Length > 0)
    {
        algorithm = default.HMFMorphAlgorithms[Rand(default.HMFMorphAlgorithms.Length)];
        return TRUE;
    }
    if (targetSet == Class'ASA_BaseMorphSet' && default.ASAMorphAlgorithms.Length > 0)
    {
        algorithm = default.ASAMorphAlgorithms[Rand(default.ASAMorphAlgorithms.Length)];
        return TRUE;
    }
    if (targetSet == Class'KRO_baseMorphSet' && default.KROMorphAlgorithms.Length > 0)
    {
        algorithm = default.KROMorphAlgorithms[Rand(default.KROMorphAlgorithms.Length)];
        return TRUE;
    }
    if (targetSet == Class'SAL_BaseMorphSet' && default.SALMorphAlgorithms.Length > 0)
    {
        algorithm = default.SALMorphAlgorithms[Rand(default.SALMorphAlgorithms.Length)];
        return TRUE;
    }
    return FALSE;
}
public static function RandomizeBioMorphFace(BioMorphFace BMF)
{
    local int I;
    local int J;
    local MorphRandomizationAlgorithm algorithm;
    local bool modified;
    local bool found;
    local bool hasAlgorithm;
    local Class<CustomMorphTargetSet> MorphTargetSet;
    
    MorphTargetSet = GetMorphTargetSet(BMF);
    if (MorphTargetSet == None)
    {
        LogInternal("COULD NOT FIND MORPH TARGET SET FOR: " $ BMF.m_oBaseHead, );
        return;
    }
    hasAlgorithm = GetRandomAlgorithm(MorphTargetSet, algorithm);
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
private static final function Class<CustomMorphTargetSet> GetMorphTargetSet(BioMorphFace BMF)
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
        // Drells are all male
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

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
    HMMMorphAlgorithms = ({
                           AlgoName = "Anderson", 
                           Randomizations = ({Feature = "Anderson", Min = 0.5, Max = 3.5, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }, 
                          {
                           AlgoName = "Joker", 
                           Randomizations = ({Feature = "Anderson", Min = 0.5, Max = 3.5, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }, 
                          {
                           AlgoName = "SadFaceMaybe", 
                           Randomizations = ({Feature = "Anderson", Min = 0.5, Max = 3.5, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }, 
                          {
                           AlgoName = "BigHair", 
                           Randomizations = ({Feature = "Afro", Min = 0.0, Max = 4.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }, 
                          {
                           AlgoName = "AngryMan1", 
                           Randomizations = ({Feature = "Eastwood", Min = 5.0, Max = 10.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}, 
                                             {Feature = "eyes_BallBack", Min = 0.899999976, Max = 1.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}, 
                                             {Feature = "eyes_RotateIn", Min = 0.0, Max = 1.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}, 
                                             {Feature = "mouth_Down", Min = -3.0, Max = 1.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}, 
                                             {Feature = "mouth_Wide", Min = -1.0, Max = 5.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}, 
                                             {Feature = "nose_BendRight", Min = -3.0, Max = 3.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }, 
                          {
                           AlgoName = "Jacob", 
                           Randomizations = ({Feature = "Jacob", Min = 0.0, Max = 2.5, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }, 
                          {
                           AlgoName = "Kaidan", 
                           Randomizations = ({Feature = "Kaidan", Min = 0.0, Max = 3.75, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }, 
                          {
                           AlgoName = "SmolEyes", 
                           Randomizations = ({Feature = "eyes_Big", Min = -40.0, Max = 0.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }, 
                          {
                           AlgoName = "ThiccEyes", 
                           Randomizations = ({Feature = "eyes_Big", Min = 3.0, Max = 5.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }
                         )
    HMFMorphAlgorithms = ({
                           AlgoName = "TallEyes", 
                           Randomizations = ({Feature = "eyes_posup", Min = 0.5, Max = 4.0, MergeMode = EBMFFeatureMergeMode.Multiplicative, AddIfNotFound = TRUE}
                                            )
                          }, 
                          {
                           AlgoName = "EyesPoppinOut", 
                           Randomizations = ({Feature = "eyes_ballforward", Min = -1.5, Max = 4.69999981, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }
                         )
    ASAMorphAlgorithms = ({
                           AlgoName = "LargeTentacles", 
                           Randomizations = ({Feature = "tentacle_Large", Min = -2.0, Max = 3.0, MergeMode = EBMFFeatureMergeMode.Exact, AddIfNotFound = TRUE}
                                            )
                          }
                         )
}