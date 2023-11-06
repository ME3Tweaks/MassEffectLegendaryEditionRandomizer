Class MERControl extends MERControlEngine
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
    local float NewFloat;
    local LinearColor defaultVector;
    local LinearColor NewValue;
    
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
        if (ScalarParams[J] == 'Emis_Scalar')
        {
            NewFloat = RandFloat(0.25, 2.0);
            NewFloat = 2.0;
        }
        else
        {
            NewFloat = RandFloat(0.0, defaultFloat * 2.0);
        }
        BioMat.SetScalarParameterValue(ScalarParams[J], NewFloat);
        LogInternal((("Setting parameter: " $ ScalarParams[J]) $ " to ") $ NewFloat, );
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
        NewValue = RandLinearColor(0.0, defaultVector.R * 2.0, 0.0, defaultVector.G * 2.0, 0.0, defaultVector.B * 2.0, 0.5, defaultVector.A * 2.0);
        NewValue.A = 1.0;
        LogInternal((((((((("Setting parameter: " $ VectorParams[J]) $ " to ") $ NewValue.R) $ ",") $ NewValue.B) $ ",") $ NewValue.B) $ ",") $ NewValue.A, );
        BioMat.SetVectorParameterValue(VectorParams[J], NewValue);
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
public static function Name GetMatName2(Object matObj)
{
    local Object Parent;
    
    if (matObj == None)
    {
        return 'None';
    }
    if (MaterialInstanceConstant(matObj) != None)
    {
        Parent = MaterialInstanceConstant(matObj).Parent;
        if (Parent != None)
        {
            return GetMatName2(Parent);
        }
        return 'None';
    }
    else if (RvrEffectsMaterialUser(matObj) != None)
    {
        return GetMatName2(RvrEffectsMaterialUser(matObj).m_pBaseMaterial);
    }
    else if (Material(matObj) != None)
    {
        return matObj.Name;
    }
    LogInternal((("Unhandled case for object " $ matObj) $ " with class ") $ matObj.Class, );
    return 'None';
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
public static function BioPawn_RandomizeColors(BioPawn BP)
{
    local int I;
    local SkeletalMeshComponent MeshComp;
    local array<SkeletalMeshComponent> MeshComps;
    local array<Name> ColorMatNames;
    local array<Name> LocalMatNames;
    local Name ExprName;
    local BioMaterialInstanceConstant BioMatConstant;
    local MaterialInstanceConstant MatConstant;
    local Material Mat;
    local Color newcol;
    local LinearColor currentval;
    local bool Next;
    
    if (!Class'MERControlEngine'.default.bPawnColorsRandomizer)
    {
        return;
    }
    if (BP == None)
    {
        return;
    }
    MeshComps.AddItem(BP.HeadMesh);
    MeshComps.AddItem(BP.Mesh);
    MeshComps.AddItem(BP.m_oHairMesh);
    MeshComps.AddItem(BP.m_oHeadGearMesh);
    MeshComps.AddItem(BP.m_oHairMesh);
    MeshComps.AddItem(BP.m_oVisorMesh);
    MeshComps.AddItem(BP.m_oFacePlateMesh);
    foreach MeshComps(MeshComp, )
    {
        if (MeshComp == None)
        {
            continue;
        }
        for (I = 0; I < MeshComp.Materials.Length; I++)
        {
            if (MeshComp.Materials[I] == None)
            {
                continue;
            }
            BioMatConstant = BioMaterialInstanceConstant(MeshComp.Materials[I]);
            if (BioMatConstant != None)
            {
                LocalMatNames = GetVectorParamsMIC(BioMatConstant);
                foreach LocalMatNames(ExprName, )
                {
                    LogInternal(string(ExprName), );
                    if (ColorMatNames.Find(ExprName) == -1)
                    {
                        ColorMatNames.AddItem(ExprName);
                    }
                }
                continue;
            }
            MatConstant = MaterialInstanceConstant(MeshComp.Materials[I]);
            if (MatConstant != None)
            {
                BioMatConstant = new (BP) Class'BioMaterialInstanceConstant';
                BioMatConstant.SetParent(MatConstant);
                MeshComp.SetMaterial(I, BioMatConstant);
                LocalMatNames = GetVectorParamsMIC(BioMatConstant);
                foreach LocalMatNames(ExprName, )
                {
                    LogInternal(string(ExprName), );
                    if (ColorMatNames.Find(ExprName) == -1)
                    {
                        ColorMatNames.AddItem(ExprName);
                    }
                }
                continue;
            }
            Mat = Material(MeshComp.Materials[I]);
            if (Mat != None)
            {
                BioMatConstant = new (BP) Class'BioMaterialInstanceConstant';
                BioMatConstant.SetParent(Mat);
                MeshComp.SetMaterial(I, BioMatConstant);
                LocalMatNames = GetVectorParamsMIC(BioMatConstant);
                foreach LocalMatNames(ExprName, )
                {
                    LogInternal(string(ExprName), );
                    if (ColorMatNames.Find(ExprName) == -1)
                    {
                        ColorMatNames.AddItem(ExprName);
                    }
                }
                continue;
            }
        }
    }
    foreach ColorMatNames(ExprName, )
    {
        if (SFXPawn_Player(BP) != None && ExprName == 'Skin_Tone'){
            continue;
        }

        foreach MeshComps(MeshComp, )
        {
            Next = FALSE;
            for (I = 0; I < MeshComp.Materials.Length; I++)
            {
                if (MeshComp.Materials[I] == None)
                {
                    continue;
                }
                if (MeshComp.Materials[I].GetVectorParameterValue(ExprName, currentval))
                {
                    newcol = Class'MERControlEngine'.static.RandColor(0, int(currentval.R * 255.0), 0, int(currentval.G * 255.0), 0, int(currentval.B * 255.0));
                    BP.SetVectorParameterValue(ExprName, newcol);
                    Next = TRUE;
                    break;
                }
            }
            if (Next)
            {
                break;
            }
        }
    }
}
public static function BioPawn_RandomizeEyes(BioPawn BP)
{
    local int I;
    local int J;
    local int K;
    local Name matName;
    local BioMaterialInstanceConstant BioMatConstant;
    local MaterialInstanceConstant MatConstant;
    local Material Mat;
    
    if (!Class'MERControlEngine'.default.bEyeRandomizer)
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
    LogInternal("Eye randomizer on " $ BP.Tag, );
    for (I = 0; I < BP.HeadMesh.Materials.Length; I++)
    {
        if (BP.HeadMesh.Materials[I] == None)
        {
            continue;
        }
        matName = GetMatName2(BP.HeadMesh.Materials[I]);
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
    
    if ((BP.PawnType == None || BP.PawnType.m_oAppearance == None) || BP.PawnType.m_oAppearance.MovementInfo == None)
    {
        return;
    }
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
    if (BP.PawnType == None)
    {
        return;
    }
    ApprChar = BP.PawnType.m_oAppearance;
    if (ApprChar != None)
    {
        if (BP.Mesh != None)
        {
            BP.Mesh.GetBoneNames(BoneNames);
        }
        if (BoneNames.Length == 0)
        {
            return;
        }
        for (I = 0; I < ApprChar.m_aLookBoneDefs.Length; I++)
        {
            if (Rand(5) == 0 && BoneNames.Length > 0)
            {
                BoneName = BoneNames[Rand(BoneNames.Length)];
                // LogInternal((((("Setting ApprChar " $ BP) $ " lookat bone ") $ I) $ " to ") $ BoneName, );
                ApprChar.m_aLookBoneDefs[I].m_nBoneName = BoneName;
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
                    BoneName = BoneNames[Rand(BoneNames.Length)];
                    // LogInternal((((("Setting ApprChar LookAtDef " $ BP) $ " lookat bone ") $ I) $ " to ") $ BoneName, );
                    LookAtDef.BoneDefinitions[I].m_nBoneName = BoneName;
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
    if (SFXPawn_Player(BP) == None)
    {
        BP.ViewPitchMin *= FRand() * 2.0;
        BP.ViewPitchMax *= FRand() * 2.0;
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
    BioPawn_RandomizeEyes(BP);
    if (SFXPawn_Player(BP) == None){
        // These only run on non-player pawns
       BioPawn_RandomizeMorphHead(BP); // Controlled elsewhere
    }
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
    
    if (!Class'MERControlEngine'.default.bEyeRandomizer)
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
    LogInternal("Eye randomizer on " $ BP.Tag, );
    for (I = 0; I < BP.HeadMesh.Materials.Length; I++)
    {
        if (BP.HeadMesh.Materials[I] == None)
        {
            continue;
        }
        matName = GetMatName2(BP.HeadMesh.Materials[I]);
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
    SFXSkeletalMeshActorMAT_RandomizeEyes(SKM);
    SFXSkeletalMeshActorMAT_RandomizeMorphHead(SKM);
}
public static function SFXSkeletalMeshActorMAT_RandomizeMorphHead(SFXSkeletalMeshActorMAT SKM)
{
}
public static function BioPawn_RandomizeMorphHead(BioPawn BP)
{
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}