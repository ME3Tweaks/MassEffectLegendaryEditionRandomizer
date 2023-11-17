public final function SetupGameOver()
{
    local ASParams stParam;
    local array<ASParams> lstParams;
    
    stParam.Type = ASParamTypes.ASParam_String;
    if (Class'MERControlEngine'.default.bGameOverStringRandomizer){
        stParam.sVar = string(class'MERControlEngine'.static.GetRandomGameOverString());
    } else {
        stParam.sVar = string(m_srGameOverString);
    }
    lstParams.AddItem(stParam);
    if (!Class'WorldInfo'.static.IsConsoleBuild())
    {
        oPanel.SetVariableInt("_root.ScreenLayout", int(ScreenLayout));
    }
    oPanel.InvokeMethodArgs("SetGameOverString", lstParams);
}