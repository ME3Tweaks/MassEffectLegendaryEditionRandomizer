public function HandleEvent(byte nCommand, const out array<string> lstArguments)
{
    local BioSFPanel oNewPanel;
    local Name nmNewClass;
    local PlayerController PC;
    
    LogInternal("HandleEvent: " $ nCommand, );
    switch (nCommand)
    {
        case 2:
            HandleSliderChange(int(lstArguments[0]), int(lstArguments[1]), int(lstArguments[2]));
            UpdateCode();
            break;
        case 9:
            if (false){
                Update3DModelByClass(Name(lstArguments[0]), lstTemplates[int(m_nCurrentTemplate)], m_nCurrentTemplate, FALSE);
            }
            break;
        case 17:
            nmNewClass = lstCurrentClass[int(m_nCurrentTemplate)];
            if (bZoomedInOnFace)
            {
                BioWorldInfo(oWorldInfo).m_UIWorld.TriggerEvent('CharCreateLookReset', oWorldInfo);
            }
            Update3DModelState(byte(int(lstArguments[0])));
            if (FALSE)
            {
                Update3DModelByClass(nmNewClass, lstTemplates[int(lstArguments[0])], byte(int(lstArguments[0])), FALSE, FALSE, !bZoomedInOnFace);
            }
            break;
        case 1:
            SelectNextPregeneratedHead();
            UpdateCode();
            break;
        case 16:
            DoCategoryReset(int(lstArguments[0]));
            UpdateCode();
            break;
        case 3:
            StartGameWithIconicCharacter(lstArguments);
            m_oBioMorphFrontEnd.Cleanup();
            StartNewGame();
            break;
        case 5:
            StartGameWithCustomCharacter(lstArguments);
            m_oBioMorphFrontEnd.Cleanup();
            StartNewGame();
            break;
        case 8:
            ConfirmComplete();
            break;
        case 14:
            bZoomedInOnFace = TRUE;
            BioWorldInfo(oWorldInfo).m_UIWorld.TriggerEvent('CharCreateZoomIn', oWorldInfo);
            break;
        case 15:
            bZoomedInOnFace = FALSE;
            BioWorldInfo(oWorldInfo).m_UIWorld.TriggerEvent('CharCreateZoomOut', oWorldInfo);
            ResetLookAt();
            break;
        case 11:
            BioWorldInfo(oWorldInfo).m_UIWorld.HidePawn(lstTemplates[int(m_nCurrentTemplate)]);
            break;
        case 13:
            BioWorldInfo(oWorldInfo).m_UIWorld.HidePawn(lstTemplates[int(m_nCurrentTemplate)], FALSE);
            break;
        case 10:
            oNewPanel = oPanel.oParentManager.CreatePanel('ReplayCharacterSelect', TRUE);
            oNewPanel.bFullScreen = TRUE;
            oPanel.oParentManager.RemovePanel(oPanel, TRUE);
            break;
        case 18:
            UpdateBonusTalentList();
            break;
        case 4:
            m_oBioMorphFrontEnd = new Class'BioMorphFaceFrontEnd';
            BioWorldInfo(oWorldInfo).m_UIWorld.TriggerEvent('CharCreateInitCamera', oWorldInfo);
            ProcessExternalStates();
            SetCustomName(m_sMaleName, m_sFemaleName);
            UpdateCustomClassList();
            SetupSummary();
            SetUIPawnCasual(TRUE);
            BioWorldInfo(oWorldInfo).m_UIWorld.SetObjectVariable('ZoomInCompleteEffect', SchematicVisualEffect);
            BioWorldInfo(oWorldInfo).m_UIWorld.HidePawn(lstTemplates[int(m_nCurrentTemplate)], FALSE);
            Update3DModelByClass('Soldier', lstTemplates[int(m_nCurrentTemplate)], m_nCurrentTemplate, TRUE, TRUE);
            bZoomedInOnFace = TRUE;
            BioWorldInfo(oWorldInfo).m_UIWorld.TriggerEvent('CharCreateForcedZoomIn', oWorldInfo);
            break;
        case 12:
            m_oBioMorphFrontEnd.SetPlayerName(m_bMaleSelected ? m_sMaleName : m_sFemaleName);
            SetCustomModel();
            PopulateCustomFaceList();
            SetSliderPositions();
            UpdateCode();
            break;
        case 6:
            SetBackgroundMaterial(None);
            oNewPanel = oPanel.oParentManager.CreatePanel(bOpenedFromMainMenu ? 'MainMenu' : 'SelectCharacter', TRUE);
            oNewPanel.bFullScreen = TRUE;
            StopGuiVoice();
            oPanel.oParentManager.RemovePanel(oPanel, TRUE);
            break;
        case 7:
            m_bEnteringCode = FALSE;
            oKeyboard = BioSFHandler_Keyboard(oPanel.AttachHandler(Class'BioSFHandler_Keyboard'));
            oKeyboard.DisplayKeyboard(srNameTitle, $0, 0, nMaxNameLength, m_bMaleSelected ? m_sMaleName : m_sFemaleName);
            PlayGuiVoice(m_nmAllianceComputerPleaseLogin);
            break;
        case 20:
            if (Class'WorldInfo'.static.IsConsoleBuild(0))
            {
                m_bEnteringCode = TRUE;
                oKeyboard = BioSFHandler_Keyboard(oPanel.AttachHandler(Class'BioSFHandler_Keyboard'));
                oKeyboard.DisplayKeyboard(srCodeTitle, $0, 3, nMaxCodeLength, m_sCode);
            }
            break;
        case 21:
            if (lstArguments.Length > 0)
            {
                ApplyNewCode(lstArguments[0]);
                SetSliderPositions();
            }
            UpdateCode();
            break;
        case 24:
            if (Class'WorldInfo'.static.IsConsoleBuild(0))
            {
                PC = GetPlayerController();
                if (Class'WorldInfo'.static.IsConsoleBuild(0) && PC != None)
                {
                    if (Class'UIInteraction'.static.IsLoggedIn(LocalPlayer(PC.Player).ControllerId))
                    {
                        bImportingCharacter = TRUE;
                        ChooseStorageDevice();
                    }
                }
            }
            break;
        default:
    }
}