public function Activated()
{
    local MassEffectGuiManager oManager;
    local BioSFHandler_NewCharacter oNCHandler;
    local array<Name> PawnTagsToRandomize;
    local Name Tag;
    local BioMorphFace BMF;
    local BioPawn BP;
    
    oManager = MassEffectGuiManager(BioWorldInfo(GetWorldInfo()).GetLocalPlayerController().GetScaleFormManager());
    bAborted = FALSE;
    m_bIsFinished = FALSE;
    if (MaleDataSource != None && FemaleDataSource != None)
    {
        oNCHandler = oManager.LaunchCharacterCreation(lstClassAnimSetRefs, lstCharacterClasses, MaleDataSource, FemaleDataSource);
        if (oNCHandler != None)
        {
            oNCHandler.SetClassVFXTemplates(FullBioticEffectTemplate, HalfBioticEffectTemplate, TechToolEffectTemplate);
            oNCHandler.SetSchematicResources(SchematicEffectTemplate, SchematicAnimSet);
            BioWorldInfo(GetWorldInfo()).m_UIWorld.SetAnimSet(oNCHandler.lstTemplates[int(oNCHandler.m_nCurrentTemplate)], SchematicAnimSet);
            oNCHandler.SetBackgroundMaterial(BackgroundMaterial);
            oNCHandler.AddKismetNamedObject('SchematicIdle', SchematicIdleEffectTemplate);
            oNCHandler.AddKismetNamedObject('SchematicEffect', SchematicEffectTemplate);
            oNCHandler.AddKismetNamedObject('SchematicReverseEffect', SchematicReverseEffectTemplate);
            oNCHandler.AddKismetNamedObject('MaleBackgroundSchematicMaterial', SchematicMaleBackgroundMaterial);
            oNCHandler.AddKismetNamedObject('FemaleBackgroundSchematicMaterial', SchematicFemaleBackgroundMaterial);
            oNCHandler.SetOnCloseCallback(onScreenClosed);
            if (Class'MERControlEngine'.default.bIconicRandomizer)
            {
                Class'MERIRFemshepFixer'.static.FixFemShepForIR('Iconic_Female_Template');
                Class'MERIRFemshepFixer'.static.FixFemShepForIR('Custom_Female_Template');
                PawnTagsToRandomize.AddItem('Iconic_Male_Template');
                PawnTagsToRandomize.AddItem('Custom_Male_Template');
                PawnTagsToRandomize.AddItem('Iconic_Female_Template');
                PawnTagsToRandomize.AddItem('Custom_Female_Template');
                foreach PawnTagsToRandomize(Tag, )
                {
                    BP = BioPawn(Class'MERControlEngine'.static.FindActorByTag(Tag));
                    if (BP == None)
                    {
                        LogInternal("Did not find pawn: " $ Tag, );
                        continue;
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
                        Class'MERBioMorphUtility'.static.RandomizeBioMorphFace(BMF, Class'MERControlEngine'.default.fIconicFaceRandomization);
                    }
                }
            }
        }
        else
        {
            bAborted = TRUE;
            m_bIsFinished = TRUE;
        }
    }
    else
    {
        WarnInternal("PlayCharacterCreation( AnimSets, CharacterClasses ) should be used instead of PlayCharacterCreation()");
        oNCHandler = oManager.PlayCharacterCreation();
        oNCHandler.SetOnCloseCallback(onScreenClosed);
    }
}