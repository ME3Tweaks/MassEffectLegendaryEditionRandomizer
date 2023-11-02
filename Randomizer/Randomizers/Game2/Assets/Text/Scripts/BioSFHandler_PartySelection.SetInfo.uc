public function SetInfo(Name nmTag, SFXPlayerSquadLoadoutData oLoadout)
{
    local int nChar;
    local int nSlot;
    local int nPower;
    local int nLoadoutPower;
    local int nExistingPower;
    local Class<SFXPower> cPower;
    local Class<SFXPower> cEvolvedPower;
    local BioPlayerController PC;
    local SFXEngine Engine;
    local int Idx;
    local HenchmanSaveRecord oHenchData;
    local bool addDefaultPowerText;
    
    nChar = lstMemberInfo.Find('Tag', nmTag);
    if (nChar == -1)
    {
        LogInternal("Couldn't find henchman called " $ nmTag, );
        return;
    }
    if (lstDynamicInfo.Length < lstMemberInfo.Length)
    {
        lstDynamicInfo.Add(lstMemberInfo.Length - lstDynamicInfo.Length);
    }
    PC = BioPlayerController(GetPlayerController());
    if (PC != None)
    {
        Engine = SFXEngine(PC.Player.Outer);
        if (Engine != None)
        {
            Idx = Engine.HenchmanRecords.Find('Tag', nmTag);
            if (Idx >= 0)
            {
                oHenchData = Engine.HenchmanRecords[Idx];
            }
        }
    }
    nSlot = 0;
    if (oLoadout != None)
    {
        for (nLoadoutPower = 0; nLoadoutPower < oLoadout.Powers.Length; nLoadoutPower++)
        {
            if (nSlot >= 5)
            {
                break;
            }
            cPower = oLoadout.Powers[nLoadoutPower];
            if (cPower != None)
            {
                if (!cPower.default.DisplayInHUD)
                {
                    continue;
                }
                addDefaultPowerText = TRUE;
                for (nPower = 0; nPower < oHenchData.Powers.Length; nPower++)
                {
                    if (cPower.default.PowerName == oHenchData.Powers[nPower].PowerName)
                    {
                        lstDynamicInfo[nChar].Abilities[nSlot] = int(cPower.default.DisplayName);
                        lstDynamicInfo[nChar].Ranks[nSlot] = int(oHenchData.Powers[nPower].CurrentRank);
                        nSlot++;
                        addDefaultPowerText = FALSE;
                    }
                    cEvolvedPower = Class<SFXPower>(cPower.default.EvolvedPowerClass1);
                    if (cEvolvedPower == None || cEvolvedPower.default.PowerName != oHenchData.Powers[nPower].PowerName)
                    {
                        cEvolvedPower = Class<SFXPower>(cPower.default.EvolvedPowerClass2);
                        if (cEvolvedPower.default.PowerName != oHenchData.Powers[nPower].PowerName)
                        {
                            cEvolvedPower = None;
                        }
                    }
                    if (cEvolvedPower != None)
                    {
                        lstDynamicInfo[nChar].Abilities[nSlot] = int(cEvolvedPower.default.DisplayName);
                        lstDynamicInfo[nChar].Ranks[nSlot] = int(oHenchData.Powers[nPower].CurrentRank);
                        nSlot++;
                        addDefaultPowerText = FALSE;
                        for (nExistingPower = 0; nExistingPower < nSlot; nExistingPower++)
                        {
                            if (lstDynamicInfo[nChar].Abilities[nExistingPower] == int(cPower.default.DisplayName))
                            {
                                for (nExistingPower = nExistingPower; nExistingPower < nSlot; nExistingPower++)
                                {
                                    lstDynamicInfo[nChar].Abilities[nExistingPower] = lstDynamicInfo[nChar].Abilities[nExistingPower + 1];
                                    lstDynamicInfo[nChar].Ranks[nExistingPower] = lstDynamicInfo[nChar].Ranks[nExistingPower + 1];
                                }
                                nSlot--;
                                break;
                            }
                        }
                        break;
                    }
                }
                if (addDefaultPowerText)
                {
                    lstDynamicInfo[nChar].Abilities[nSlot] = int(cPower.default.DisplayName);
                    lstDynamicInfo[nChar].Ranks[nSlot] = int(cPower.default.Rank);
                    nSlot++;
                }
            }
        }
        for (nSlot = nSlot; nSlot < 5; nSlot++)
        {
            lstDynamicInfo[nChar].Abilities[nSlot] = 0;
            lstDynamicInfo[nChar].Ranks[nSlot] = 0;
        }
        nSlot = 0;
        for (nLoadoutPower = 0; nLoadoutPower <= 4; nLoadoutPower++)
        {
            if (nSlot >= 3)
            {
                break;
            }
            if (Class'SFXPlayerSquadLoadoutData'.static.CanHenchmanUseWeaponGroup(nmTag, byte(nLoadoutPower)))
            {
                lstDynamicInfo[nChar].Weapons[nSlot] = Class'SFXPlayerSquadLoadoutData'.static.GetPluralPrettyName(nLoadoutPower);
                LogInternal("Can use weapon type" @ nLoadoutPower, );
                nSlot++;
                continue;
            }
            lstDynamicInfo[nChar].Weapons[nSlot] = 0;
        }
        if (m_bSFScreenInitialized)
        {
            SendInfo(nChar);
        }
    }
}