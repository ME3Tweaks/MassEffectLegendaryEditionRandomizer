﻿public static final function BioMorphFace LoadMorphHead(out PlayerSaveRecord ThePlayerRecord)
{
    local BioMorphFace MorphHead;
    local MorphHeadSaveRecord Record;
    local string PackageName;
    local int Idx;
    local MorphFeature Feature;
    local OffsetBonePos Offset;
    local ScalarParameter ScalarParam;
    local ColorParameter ColorParam;
    local TextureParameter TextureParam;
    local array<int> BuffersToRefresh;
    local Package OuterPackage;
    
    if (ThePlayerRecord.Appearance.bHasMorphHead)
    {
        Record = ThePlayerRecord.Appearance.MorphHead;
        PackageName = Left(ThePlayerRecord.bIsFemale ? "BIOG_MORPH_FACE.Player_Base_Female" : "BIOG_MORPH_FACE.Player_Base_Male", InStr(ThePlayerRecord.bIsFemale ? "BIOG_MORPH_FACE.Player_Base_Female" : "BIOG_MORPH_FACE.Player_Base_Male", ".", FALSE, , ));
        Class'SFXGame'.static.LoadPackage(PackageName);
        MorphHead = BioMorphFace(FindObject(ThePlayerRecord.bIsFemale ? "BIOG_MORPH_FACE.Player_Base_Female" : "BIOG_MORPH_FACE.Player_Base_Male", Class'BioMorphFace'));
        if (MorphHead != None)
        {
            if (ThePlayerRecord.FaceCode == "LE2RICONIC")
            {
                LogInternal("Swapping base-head for LE2R iconic-head base", );
                if (ThePlayerRecord.bIsFemale)
                {
                    PackageName = "BIOG_HMF_HED_PROMorph_R";
                    Class'SFXGame'.static.LoadPackage("BIOG_Female_Player_C");
                    OuterPackage = Package(FindObject(PackageName, Class'Package'));
                    MorphHead = new (MorphHead.Outer) Class'BioMorphFace';
                    MorphHead.m_oBaseHead = SkeletalMesh(DynamicLoadObject("BIOG_HMF_HED_PROMorph_R.PROSheppard.HMF_HED_PROSheppard_MDL", Class'SkeletalMesh'));
                    MorphHead.m_oHairMesh = SkeletalMesh(DynamicLoadObject("BIOG_HMF_HIR_PRO.Hair_PROShepard.HMF_HIR_PROShepard_MDL", Class'SkeletalMesh'));
                }
                else
                {
                    MorphHead = new (MorphHead.Outer) Class'BioMorphFace';
                    MorphHead.m_oBaseHead = SkeletalMesh(FindObject("BIOG_HMM_HED_PROMorph.Sheppard.HMM_HED_PROSheppard_MDL", Class'SkeletalMesh'));
                }
                if (MorphHead.m_oBaseHead == None)
                {
                    WarnInternal("Error: Failed to iconic morph head base!");
                }
            }
            if (PathName(MorphHead.m_oHairMesh) != string(Record.HairMesh))
            {
                MorphHead.m_oHairMesh = SkeletalMesh(DynamicLoadObject(string(Record.HairMesh), Class'SkeletalMesh'));
            }
            if (MorphHead.m_oOtherMeshes.Length != Record.AccessoryMeshes.Length)
            {
                MorphHead.m_oOtherMeshes.Length = Record.AccessoryMeshes.Length;
                for (Idx = 0; Idx < Record.AccessoryMeshes.Length; Idx++)
                {
                    MorphHead.m_oOtherMeshes[Idx] = SkeletalMesh(DynamicLoadObject(string(Record.AccessoryMeshes[Idx]), Class'SkeletalMesh'));
                }
            }
            MorphHead.m_aMorphFeatures.Length = Record.MorphFeatures.Length;
            for (Idx = 0; Idx < Record.MorphFeatures.Length; Idx++)
            {
                Feature.sFeatureName = Record.MorphFeatures[Idx].Feature;
                Feature.Offset = Record.MorphFeatures[Idx].Offset;
                MorphHead.m_aMorphFeatures[Idx] = Feature;
            }
            if (ThePlayerRecord.FaceCode != "LE2RICONIC")
            {
                MorphHead.m_aFinalSkeleton.Length = Record.OffsetBones.Length;
                for (Idx = 0; Idx < Record.OffsetBones.Length; Idx++)
                {
                    Offset.nName = Record.OffsetBones[Idx].Name;
                    Offset.vPos = Record.OffsetBones[Idx].Offset;
                    MorphHead.m_aFinalSkeleton[Idx] = Offset;
                }
                for (Idx = 0; Idx < Record.LOD0Vertices.Length; Idx++)
                {
                    MorphHead.SetPosition(0, Idx, Record.LOD0Vertices[Idx]);
                }
                BuffersToRefresh.AddItem(0);
                MorphHead.RefreshBuffers(BuffersToRefresh);
            }
            else if (Class'MERControlEngine'.default.bIconicRandomizer_Persistent)
            {
                Class'MorphTargetUtility'.static.UpdateMeshAndSkeleton(MorphHead, Class'MERBioMorphUtility'.static.GetMorphTargetSet(MorphHead));
            }
            else
            {
                Class'MERBioMorphUtility'.static.RandomizeBioMorphFace(MorphHead, Class'MERControlEngine'.default.fIconicFaceRandomization);
            }
            MorphHead.m_oMaterialOverrides.m_aScalarOverrides.Length = Record.ScalarParameters.Length;
            for (Idx = 0; Idx < Record.ScalarParameters.Length; Idx++)
            {
                ScalarParam.nName = Record.ScalarParameters[Idx].Name;
                ScalarParam.sValue = Record.ScalarParameters[Idx].Value;
                MorphHead.m_oMaterialOverrides.m_aScalarOverrides[Idx] = ScalarParam;
            }
            MorphHead.m_oMaterialOverrides.m_aColorOverrides.Length = Record.VectorParameters.Length;
            for (Idx = 0; Idx < Record.VectorParameters.Length; Idx++)
            {
                ColorParam.nName = Record.VectorParameters[Idx].Name;
                ColorParam.cValue = Record.VectorParameters[Idx].Value;
                MorphHead.m_oMaterialOverrides.m_aColorOverrides[Idx] = ColorParam;
            }
            MorphHead.m_oMaterialOverrides.m_aTextureOverrides.Length = Record.TextureParameters.Length;
            for (Idx = 0; Idx < Record.TextureParameters.Length; Idx++)
            {
                TextureParam.nName = Record.TextureParameters[Idx].Name;
                TextureParam.m_pTexture = Texture2D(DynamicLoadObject(string(Record.TextureParameters[Idx].Texture), Class'Texture2D'));
                MorphHead.m_oMaterialOverrides.m_aTextureOverrides[Idx] = TextureParam;
            }
            return MorphHead;
        }
    }
    return None;
}