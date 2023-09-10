public function Activated()
{
    local BioWorldInfo oWorldInfo;
    local SFXPlotTreasure oTreasure;
    local Bio2DA Current2DA;
    local int RowCount;
    local int RandPoolRowNum;
    local array<Name> RowPool;
    local bool bAwardedLoot;
    
    Super(SequenceOp).Activated();
    oWorldInfo = BioWorldInfo(GetWorldInfo());
    oTreasure = oWorldInfo.m_oTreasure;
    if (Class'MERControlEngine'.default.bLootRandomizer)
    {
        // Randomize loot pickups - does not work on research, maybe stores as well?
        Current2DA = Bio2DANumberedRows(oTreasure.oPlotTreasureTreasure2DA);
        RowCount = Current2DA.GetNumRows();
        for (RandPoolRowNum = 0; RandPoolRowNum < RowCount; RandPoolRowNum++)
        {
            RowPool.AddItem(Current2DA.GetRowName(RandPoolRowNum));
        }
        while (RowPool.Length > 0)
        {
            RandPoolRowNum = Rand(RowPool.Length);
            RowPool.Remove(RandPoolRowNum, 1);
            nState = Current2DA.GetRowNumber(RandPoolRowNum);
            bAwardedLoot = oTreasure.AwardTreasure(nState, bDiscount);
            if (bAwardedLoot)
            {
                break;
            }
        }
    }
    else
    {
        bAwardedLoot = oTreasure.AwardTreasure(nState, bDiscount);
    }
    OutputLinks[0].bHasImpulse = bAwardedLoot;
    OutputLinks[1].bHasImpulse = !OutputLinks[0].bHasImpulse;
}