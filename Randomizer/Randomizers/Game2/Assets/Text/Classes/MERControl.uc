﻿Class MERControl extends MERControlEngine
    config(Engine);

// Functions
private static final function array<Name> GetScalarParamsMat(Material M)
{
    local array<Name> Parms;
    local int I;
    
    for (I = 0; I < M.Expressions.Length; I++)
    {
        if (MaterialExpressionScalarParameter(M.Expressions[I]) != None)
        {
            Parms.AddItem(MaterialExpressionScalarParameter(M.Expressions[I]).ParameterName);
        }
    }
    return Parms;
}
private static final function array<Name> GetScalarParamsMIC(MaterialInstanceConstant MIC)
{
    local array<Name> Parms;
    local int I;
    local Material ParentMat;
    
    if (MIC.ScalarParameterValues.Length > 0)
    {
        for (I = 0; I < MIC.ScalarParameterValues.Length; I++)
        {
            Parms.AddItem(MIC.ScalarParameterValues[I].ParameterName);
        }
        return Parms;
    }
    if (MaterialInstanceConstant(MIC.Parent) != None)
    {
        return GetScalarParamsMIC(MaterialInstanceConstant(MIC.Parent));
    }
    if (Material(MIC.Parent) != None)
    {
        return GetScalarParamsMat(Material(MIC.Parent));
    }
    if (RvrEffectsMaterialUser(MIC.Parent) != None)
    {
        ParentMat = Material(RvrEffectsMaterialUser(MIC.Parent).m_pBaseMaterial);
        if (ParentMat != None)
        {
            return GetScalarParamsMat(ParentMat);
        }
    }
    return Parms;
}
private static final function array<Name> GetVectorParamsMat(Material M)
{
    local array<Name> Parms;
    local int I;
    
    for (I = 0; I < M.Expressions.Length; I++)
    {
        if (MaterialExpressionVectorParameter(M.Expressions[I]) != None)
        {
            Parms.AddItem(MaterialExpressionVectorParameter(M.Expressions[I]).ParameterName);
        }
    }
    return Parms;
}
private static final function array<Name> GetVectorParamsMIC(MaterialInstanceConstant MIC)
{
    local array<Name> Parms;
    local int I;
    local Material ParentMat;
    
    if (MIC.VectorParameterValues.Length > 0)
    {
        for (I = 0; I < MIC.VectorParameterValues.Length; I++)
        {
            Parms.AddItem(MIC.VectorParameterValues[I].ParameterName);
        }
        return Parms;
    }
    if (MaterialInstanceConstant(MIC.Parent) != None)
    {
        return GetVectorParamsMIC(MaterialInstanceConstant(MIC.Parent));
    }
    if (Material(MIC.Parent) != None)
    {
        return GetVectorParamsMat(Material(MIC.Parent));
    }
    if (RvrEffectsMaterialUser(MIC.Parent) != None)
    {
        ParentMat = Material(RvrEffectsMaterialUser(MIC.Parent).m_pBaseMaterial);
        if (ParentMat != None)
        {
            return GetVectorParamsMat(ParentMat);
        }
    }
    return Parms;
}
private static final function RandomizeEyeParams(BioMaterialInstanceConstant BioMat)
{
    local MaterialInstanceConstant MatConstParent;
    local int J;
    local Name ParmName;
    local array<Name> ScalarParams;
    local array<Name> VectorParams;
    local float defaultFloat;
    local LinearColor defaultVector;
    
    BioMat.ScalarParameterValues.Length = 0;
    BioMat.VectorParameterValues.Length = 0;
    ScalarParams = GetScalarParamsMIC(BioMat);
    VectorParams = GetVectorParamsMIC(BioMat);
    for (J = 0; J < ScalarParams.Length; J++)
    {
        defaultFloat = GetDefaultScalarMIC(ScalarParams[J], BioMat);
        if (defaultFloat == 0.0)
        {
            defaultFloat = 0.5;
        }
        BioMat.SetScalarParameterValue(ScalarParams[J], RandFloat(0.0, defaultFloat * 2.0));
    }
    for (J = 0; J < VectorParams.Length; J++)
    {
        defaultVector = GetDefaultVectorMIC(ScalarParams[J], BioMat);
        if (defaultVector.R == 0.0)
        {
            defaultVector.R = 0.5;
        }
        if (defaultVector.G == 0.0)
        {
            defaultVector.G = 0.5;
        }
        if (defaultVector.B == 0.0)
        {
            defaultVector.B = 0.5;
        }
        if (defaultVector.A == 0.0)
        {
            defaultVector.A = 0.5;
        }
        BioMat.SetVectorParameterValue(VectorParams[J], RandLinearColor(0.0, defaultVector.R * 2.0, 0.0, defaultVector.G * 2.0, 0.0, defaultVector.B * 2.0, 0.5, defaultVector.A * 2.0));
    }
}
public static function Name GetMatName(Object matObj)
{
    local Name lName;
    local Name cName;
    local Object Parent;
    
    lName = matObj.Name;
    cName = matObj.Class.Name;
    if (BioMaterialInstanceConstant(matObj) != None)
    {
        Parent = BioMaterialInstanceConstant(matObj).Parent;
        lName = BioMaterialInstanceConstant(matObj).Parent.Name;
        cName = BioMaterialInstanceConstant(matObj).Parent.Class.Name;
    }
    else if (MaterialInstanceConstant(matObj) != None)
    {
        Parent = MaterialInstanceConstant(matObj).Parent;
    }
    if (Parent != None && InStr(string(lName), string(cName), , , ) == 0)
    {
        return GetMatName(Parent);
    }
    return matObj.Name;
}
public static function SetEyeParent(MaterialInstanceConstant MIC, MaterialInterface DefaultParent)
{
    local MaterialInterface MI;
    local string ifp;
    
    if (Class'MERControlEngine'.default.MEREyeIFPs.Length > 0)
    {
        ifp = Class'MERControlEngine'.default.MEREyeIFPs[Rand(Class'MERControlEngine'.default.MEREyeIFPs.Length)];
        LogInternal("SeekFreeLoad: " $ ifp, );
        MI = MaterialInterface(Class'SFXEngine'.static.LoadSeekFreeObject(ifp, Class'MaterialInterface'));
        if (MI != None)
        {
            LogInternal("SeekFreeLoad succeeded", );
            MIC.SetParent(MI);
            return;
        }
        LogInternal("SeekFreeLoad failed", );
    }
    MIC.SetParent(DefaultParent);
}
public static function Pawn_RandomizeEyes2(BioPawn BP)
{
    local int I;
    local int J;
    local int K;
    local Name matName;
    local BioMaterialInstanceConstant BioMatConstant;
    local MaterialInstanceConstant MatConstant;
    local Material Mat;
    
    if (!Class'MERControlEngine'.default.bIllusiveEyeRandomizer && !Class'MERControlEngine'.default.bEyeRandomizer)
    {
        return;
    }
    if (BP == None)
    {
        return;
    }
    if (BP.HeadMesh == None)
    {
        return;
    }
    if ((BP.HeadMesh.SkeletalMesh != None && BP.HeadMesh.SkeletalMesh.Name == 'HMM_HED_PROIllusiveMan_MDL') && !Class'MERControlEngine'.default.bIllusiveEyeRandomizer)
    {
        return;
    }
    LogInternal("Eye randomizer on " $ BP.Tag, );
    for (I = 0; I < BP.HeadMesh.Materials.Length; I++)
    {
        if (BP.HeadMesh.Materials[I] == None)
        {
            continue;
        }
        matName = GetMatName(BP.HeadMesh.Materials[I]);
        LogInternal("Mat: " $ matName, );
        if (InStr(string(matName), "EYE", , , ) >= 0)
        {
            LogInternal("Contains EYE", );
            BioMatConstant = BioMaterialInstanceConstant(BP.HeadMesh.Materials[I]);
            if (BioMatConstant != None)
            {
                LogInternal("BMIC", );
                SetEyeParent(BioMatConstant, BioMatConstant.Parent);
                RandomizeEyeParams(BioMatConstant);
                continue;
            }
            MatConstant = MaterialInstanceConstant(BP.HeadMesh.Materials[I]);
            if (MatConstant != None)
            {
                LogInternal("MIC", );
                BioMatConstant = new (BP) Class'BioMaterialInstanceConstant';
                SetEyeParent(BioMatConstant, MatConstant);
                BP.HeadMesh.SetMaterial(I, BioMatConstant);
                RandomizeEyeParams(BioMatConstant);
                BP.HeadMaterialInstances.RemoveItem(MaterialInstance(BP.HeadMesh.Materials[I]));
                BP.HeadMaterialInstances.AddItem(BioMatConstant);
                continue;
            }
            Mat = Material(BP.HeadMesh.Materials[I]);
            if (Mat != None)
            {
                LogInternal("MATERIAL", );
                BioMatConstant = new (BP) Class'BioMaterialInstanceConstant';
                SetEyeParent(BioMatConstant, Mat);
                BP.HeadMesh.SetMaterial(I, BioMatConstant);
                RandomizeEyeParams(BioMatConstant);
                continue;
            }
        }
    }
}
public static function BioPawn_RandomizeSpeed(BioPawn BP)
{
    local SFXMovementData clonedMovementData;
    local bool bAffectPlayer;
    local bool bAffectNonPlayer;
    
    bAffectPlayer = Class'MERControlEngine'.default.bPlayerMovementSpeedRandomizer && SFXPawn_Player(BP) != None;
    bAffectNonPlayer = Class'MERControlEngine'.default.bNPCMovementSpeedRandomizer && SFXPawn_Player(BP) == None;
    if (bAffectPlayer || bAffectNonPlayer)
    {
        clonedMovementData = new (BP.PawnType.m_oAppearance, "MERMovementData") Class'SFXMovementData';
        clonedMovementData.WalkSpeed = BP.PawnType.m_oAppearance.MovementInfo.WalkSpeed;
        clonedMovementData.GroundSpeed = BP.PawnType.m_oAppearance.MovementInfo.GroundSpeed;
        clonedMovementData.TurnSpeed = BP.PawnType.m_oAppearance.MovementInfo.TurnSpeed;
        clonedMovementData.CombatWalkSpeed = BP.PawnType.m_oAppearance.MovementInfo.CombatWalkSpeed;
        clonedMovementData.CombatGroundSpeed = BP.PawnType.m_oAppearance.MovementInfo.CombatGroundSpeed;
        clonedMovementData.CoverGroundSpeed = BP.PawnType.m_oAppearance.MovementInfo.CoverGroundSpeed;
        clonedMovementData.CoverCrouchGroundSpeed = BP.PawnType.m_oAppearance.MovementInfo.CoverCrouchGroundSpeed;
        clonedMovementData.CrouchGroundSpeed = BP.PawnType.m_oAppearance.MovementInfo.CrouchGroundSpeed;
        clonedMovementData.StormTurnSpeed = BP.PawnType.m_oAppearance.MovementInfo.StormTurnSpeed;
        clonedMovementData.AirSpeed = BP.PawnType.m_oAppearance.MovementInfo.AirSpeed;
        clonedMovementData.AccelRate = BP.PawnType.m_oAppearance.MovementInfo.AccelRate;
        clonedMovementData.WalkSpeed = float((Rand(200) + 100)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.GroundSpeed = float((Rand(200) + 500)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.TurnSpeed = float((Rand(200) + 620)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.CombatWalkSpeed = float((Rand(50) + 75)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.CombatGroundSpeed = float((Rand(100) + 150)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.CoverGroundSpeed = float((Rand(100) + 50)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.CoverCrouchGroundSpeed = float((Rand(100) + 50)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.CrouchGroundSpeed = float((Rand(100) + 50)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.StormTurnSpeed = float((Rand(400) + 700)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.AirSpeed = float((Rand(200) + 500)) * (Rand(6) == 0 ? 2.0 : 1.0);
        clonedMovementData.AccelRate = float((Rand(600) + 900)) * (Rand(6) == 0 ? 2.0 : 1.0);
        BP.PawnType.m_oAppearance.MovementInfo = clonedMovementData;
    }
}
public static function BioPawn_RandomizeLookAt(BioPawn BP)
{
    local Bio_Appr_Character ApprChar;
    local BioLookAtDefinition LookAtDef;
    local int I;
    local array<Name> BoneNames;
    local Name BoneName;
    
    if (!Class'MERControlEngine'.default.bPawnLookatRandomizer)
    {
        return;
    }
    ApprChar = BP.PawnType.m_oAppearance;
    if (BP.Mesh != None)
    {
        BP.Mesh.GetBoneNames(BoneNames);
    }
    if (ApprChar != None)
    {
        for (I = 0; I < ApprChar.m_aLookBoneDefs.Length; I++)
        {
            if (Rand(5) == 0 && BoneNames.Length > 0)
            {
                ApprChar.m_aLookBoneDefs[I].m_nBoneName = BoneNames[Rand(BoneNames.Length)];
                continue;
            }
            ApprChar.m_aLookBoneDefs[I].m_fLimit *= RandFloat(0.800000012, 1.20000005);
            ApprChar.m_aLookBoneDefs[I].m_fUpDownLimit *= RandFloat(0.800000012, 1.20000005);
            if (ApprChar.m_aLookBoneDefs[I].m_fDelay == 0.0 && Rand(1) == 0)
            {
                ApprChar.m_aLookBoneDefs[I].m_fDelay = RandFloat(0.0, 2.0);
            }
            else
            {
                ApprChar.m_aLookBoneDefs[I].m_fDelay *= RandFloat(0.800000012, 1.20000005);
            }
            ApprChar.m_aLookBoneDefs[I].m_fSpeedFactor *= RandFloat(0.800000012, 1.20000005);
            ApprChar.m_aLookBoneDefs[I].m_fConversationStrength *= RandFloat(0.800000012, 1.20000005);
        }
        LookAtDef = ApprChar.m_LookAtDefinition;
        if (LookAtDef != None)
        {
            for (I = 0; I < LookAtDef.BoneDefinitions.Length; I++)
            {
                if (Rand(5) == 0 && BoneNames.Length > 0)
                {
                    LookAtDef.BoneDefinitions[I].m_nBoneName = BoneNames[Rand(BoneNames.Length)];
                    continue;
                }
                LookAtDef.BoneDefinitions[I].m_fLimit *= RandFloat(0.800000012, 1.20000005);
                LookAtDef.BoneDefinitions[I].m_fUpDownLimit *= RandFloat(0.800000012, 1.20000005);
                if (LookAtDef.BoneDefinitions[I].m_fDelay == 0.0 && Rand(1) == 0)
                {
                    LookAtDef.BoneDefinitions[I].m_fDelay = RandFloat(0.0, 2.0);
                }
                else
                {
                    LookAtDef.BoneDefinitions[I].m_fDelay *= RandFloat(0.800000012, 1.20000005);
                }
                LookAtDef.BoneDefinitions[I].m_fSpeedFactor *= RandFloat(0.800000012, 1.20000005);
                LookAtDef.BoneDefinitions[I].m_fConversationStrength *= RandFloat(0.800000012, 1.20000005);
            }
        }
    }
    BP.m_fLookAtSpeed *= FRand() * 2.0;
    BP.m_fLookAtMinHoldTime *= FRand() * 2.0;
    BP.m_fLookAtMaxHoldTime *= FRand() * 2.0;
    BP.m_fLookAtMaxAngle *= FRand() * 2.0;
    BP.ViewPitchMin *= FRand() * 2.0;
    BP.ViewPitchMax *= FRand() * 2.0;
    if (BP.m_oLookAtData != None)
    {
        LogInternal("Num look controllers: " $ BP.m_oLookAtData.m_Controllers.Length, );
    }
}
public static function float GetDefaultScalarMIC(Name ParameterName, MaterialInstanceConstant MIC)
{
    if (Material(MIC.Parent) != None)
    {
        return GetDefaultScalarMat(ParameterName, Material(MIC.Parent));
    }
    if (MaterialInstanceConstant(MIC.Parent) != None)
    {
        return GetDefaultScalarMIC(ParameterName, MaterialInstanceConstant(MIC.Parent));
    }
    if (RvrEffectsMaterialUser(MIC.Parent) != None)
    {
        return GetDefaultScalarMat(ParameterName, Material(RvrEffectsMaterialUser(MIC.Parent).m_pBaseMaterial));
    }
    return 0.0;
}
public static function float GetDefaultScalarMat(Name ParameterName, Material Mat)
{
    local int I;
    
    if (Mat == None)
    {
        return 0.0;
    }
    for (I = 0; I < Mat.Expressions.Length; I++)
    {
        if (MaterialExpressionScalarParameter(Mat.Expressions[I]) != None && MaterialExpressionScalarParameter(Mat.Expressions[I]).ParameterName == ParameterName)
        {
            return MaterialExpressionScalarParameter(Mat.Expressions[I]).DefaultValue;
        }
    }
    return 0.0;
}
public static function LinearColor GetDefaultVectorMIC(Name ParameterName, MaterialInstanceConstant MIC)
{
    local LinearColor LC;
    
    if (Material(MIC.Parent) != None)
    {
        return GetDefaultVectorMat(ParameterName, Material(MIC.Parent));
    }
    if (MaterialInstanceConstant(MIC.Parent) != None)
    {
        return GetDefaultVectorMIC(ParameterName, MaterialInstanceConstant(MIC.Parent));
    }
    if (RvrEffectsMaterialUser(MIC.Parent) != None)
    {
        return GetDefaultVectorMat(ParameterName, Material(RvrEffectsMaterialUser(MIC.Parent).m_pBaseMaterial));
    }
    return LC;
}
public static function LinearColor GetDefaultVectorMat(Name ParameterName, Material Mat)
{
    local LinearColor LC;
    local int I;
    
    if (Mat == None)
    {
        return LC;
    }
    for (I = 0; I < Mat.Expressions.Length; I++)
    {
        if (MaterialExpressionVectorParameter(Mat.Expressions[I]) != None && MaterialExpressionVectorParameter(Mat.Expressions[I]).ParameterName == ParameterName)
        {
            return MaterialExpressionVectorParameter(Mat.Expressions[I]).DefaultValue;
        }
    }
    return LC;
}
public static function InitBioPawn(BioPawn BP)
{
    local int I;
    local int J;
    local array<int> buf;
    local BioMorphFace BMF;
    
    LogInternal(("InitBioPawn on: " $ BP.Tag) $ "--------------------------------", );
    BioPawn_RandomizeSpeed(BP);
    BioPawn_RandomizeLookAt(BP);
    Pawn_RandomizeEyes2(BP);
    BioPawn_RandomizeMorphHead(BP);
}
public static function SFXSkeletalMeshActorMAT_RandomizeEyes(SFXSkeletalMeshActorMAT BP)
{
    local int I;
    local int J;
    local int K;
    local Name matName;
    local BioMaterialInstanceConstant BioMatConstant;
    local MaterialInstanceConstant MatConstant;
    local Material Mat;
    
    if (!Class'MERControlEngine'.default.bIllusiveEyeRandomizer && !Class'MERControlEngine'.default.bEyeRandomizer)
    {
        return;
    }
    if (BP == None)
    {
        return;
    }
    if (BP.HeadMesh == None)
    {
        return;
    }
    if ((BP.HeadMesh.SkeletalMesh != None && BP.HeadMesh.SkeletalMesh.Name == 'HMM_HED_PROIllusiveMan_MDL') && !Class'MERControlEngine'.default.bIllusiveEyeRandomizer)
    {
        return;
    }
    LogInternal("Eye randomizer on " $ BP.Tag, );
    for (I = 0; I < BP.HeadMesh.Materials.Length; I++)
    {
        if (BP.HeadMesh.Materials[I] == None)
        {
            continue;
        }
        matName = GetMatName(BP.HeadMesh.Materials[I]);
        LogInternal("Mat: " $ matName, );
        if (InStr(string(matName), "EYE", , , ) >= 0)
        {
            LogInternal("Contains EYE", );
            BioMatConstant = BioMaterialInstanceConstant(BP.HeadMesh.Materials[I]);
            if (BioMatConstant != None)
            {
                LogInternal("BMIC", );
                SetEyeParent(BioMatConstant, BioMatConstant.Parent);
                RandomizeEyeParams(BioMatConstant);
                continue;
            }
            MatConstant = MaterialInstanceConstant(BP.HeadMesh.Materials[I]);
            if (MatConstant != None)
            {
                LogInternal("MIC", );
                BioMatConstant = new (BP) Class'BioMaterialInstanceConstant';
                SetEyeParent(BioMatConstant, MatConstant);
                BP.HeadMesh.SetMaterial(I, BioMatConstant);
                RandomizeEyeParams(BioMatConstant);
                continue;
            }
            Mat = Material(BP.HeadMesh.Materials[I]);
            if (Mat != None)
            {
                LogInternal("MATERIAL", );
                BioMatConstant = new (BP) Class'BioMaterialInstanceConstant';
                SetEyeParent(BioMatConstant, Mat);
                BP.HeadMesh.SetMaterial(I, BioMatConstant);
                RandomizeEyeParams(BioMatConstant);
                continue;
            }
        }
    }
}
public static function InitSFXSkeletalMeshActorMAT(SFXSkeletalMeshActorMAT SKM)
{
    local BioMorphFace BMF;
    
    SFXSkeletalMeshActorMAT_RandomizeEyes(SKM);
    SFXSkeletalMeshActorMAT_RandomizeMorphHead(SKM);
}

public static function SFXSkeletalMeshActorMAT_RandomizeMorphHead(SFXSkeletalMeshActorMAT SKM) {
    // Patched in when feature is chosen
}


public static function BioPawn_RandomizeMorphHead(BioPawn BP) {
    // Patched in when feature is chosen
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}