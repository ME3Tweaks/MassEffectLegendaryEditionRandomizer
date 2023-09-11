public final function StartGameWithCustomCharacter(const out array<string> playerSettings)
{
    local BioPawn Pawn;
    local SFXPawn_Player PlayerPawn;
    local BioMorphFace MorphHead;
    local SFXEngine Engine;
    
    Engine = Class'SFXEngine'.static.GetEngine();
    if (m_nCurrentTemplate == BioNewCharacterTemplate.BNCT_CUSTOM)
    {
        Pawn = lstTemplates[1];
        MorphHead = Pawn.MorphHead;
        Engine.NewPlayer.FaceCode = m_sCode;
        Class'SFXTelemetry'.static.SendCustomizationStr("CharCreate", "custom", "template");
    }
    else if (m_nCurrentTemplate == BioNewCharacterTemplate.BNCT_IMPORTED)
    {
        Pawn = lstTemplates[2];
        MorphHead = Pawn.MorphHead;
        PlayerPawn = SFXPawn_Player(Pawn);
        if (PlayerPawn != None)
        {
            Engine.NewPlayer.FaceCode = PlayerPawn.FaceCode;
        }
        Class'SFXTelemetry'.static.SendCustomizationStr("CharCreate", "imported", "template");
    }
    else
    {
        Pawn = lstTemplates[0];
        MorphHead = Pawn.MorphHead;
        Engine.NewPlayer.FaceCode = "LE2RICONIC";
        Class'SFXTelemetry'.static.SendCustomizationStr("CharCreate", "iconic", "template");
    }
    Engine.bNewPlayer = TRUE;
    Engine.NewPlayer.CharacterClass = SFXGame(oWorldInfo.Game).GetCharacterClassByName(playerSettings[0]);
    Engine.NewPlayer.bIsFemale = !m_bMaleSelected;
    Engine.NewPlayer.FirstName = m_bMaleSelected ? m_sMaleName : m_sFemaleName;
    Engine.NewPlayer.Origin = byte(int(playerSettings[2]));
    Engine.NewPlayer.Notoriety = byte(int(playerSettings[3]));
    Engine.NewPlayer.MorphHead = MorphHead;
    if (int(playerSettings[4]) > -1 && int(playerSettings[4]) < UnlockedBonusTalents.Length)
    {
        Engine.NewPlayer.BonusTalentClass = UnlockedBonusTalents[int(playerSettings[4])].PowerClassName;
    }
    else
    {
        Engine.NewPlayer.BonusTalentClass = 'None';
    }
}