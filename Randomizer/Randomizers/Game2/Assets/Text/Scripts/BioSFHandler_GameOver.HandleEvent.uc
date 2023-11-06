public function HandleEvent(byte nCommand, const out array<string> lstArguments)
{
    local ASParams stParam;
    local array<ASParams> lstParams;
    local MassEffectGuiManager GuiMan;
    local BioPlayerController PC;
    local SFXSFHandler_Load Handler;
    local BioSFPanel NewPanel;
    
    GuiMan = MassEffectGuiManager(oPanel.oParentManager);
    switch (nCommand)
    {
        case 1:
            stParam.Type = ASParamTypes.ASParam_Integer;
            stParam.nVar = int(ScreenLayout);
            lstParams.AddItem(stParam);
            if (GameOverStringOverride != 0)
            {
                stParam.Type = ASParamTypes.ASParam_String;
                stParam.sVar = Class'SFXGame'.static.GetSimpleString(GameOverStringOverride);
                lstParams.AddItem(stParam);
            }
            else
            {
                stParam.Type = ASParamTypes.ASParam_String;
                // Set custom MER string
                stParam.sVar = Class'SFXGame'.static.GetSimpleString(class'MERControlEngine'.static.GetRandomGameOverString());
                lstParams.AddItem(stParam);
            }
            oPanel.InvokeMethodArgs("Initialize", lstParams);
            break;
        case 3:
            PC = GuiMan.GetBioPlayerController();
            if (PC != None && GuiMan.Player != None)
            {
                bWaitingOnLoad = TRUE;
                oPanel.SetInputDisabled(TRUE);
                if (Class'WorldInfo'.static.IsConsoleBuild())
                {
                    GuiMan.GetSaveLoadWidget().ShowLoadingMessage();
                }
                PC.ResumeGame(GameOverResume_Callback);
                PlayGuiSound('GameOverResume');
            }
            else
            {
                DisplayResumeFailure();
            }
            break;
        case 4:
            if (!bWaitingOnLoad)
            {
                PlayGuiSound('GameOverMainMenu');
                GotoMainMenu();
            }
            break;
        case 5:
            if (!bWaitingOnLoad)
            {
                PlayGuiSound('GameOverLoadScreen');
                GuiMan.ClearAll();
                GuiMan.SetupBackground();
                NewPanel = GuiMan.CreatePanel('Load', TRUE);
                NewPanel.bFullScreen = TRUE;
                Handler = SFXSFHandler_Load(NewPanel.GetDefaultHandler());
                Handler.GuiMode = ESaveGuiMode.SaveGuiMode_GameOver;
                if (oPanel != None)
                {
                    GuiMan.RemovePanel(oPanel, TRUE);
                }
            }
            break;
        default:
    }
}