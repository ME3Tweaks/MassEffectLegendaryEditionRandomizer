public final function SelectPreviousPregeneratedHead()
{
    local int NumPregeneratedHeads;
    local int nNumCategories;
    local int nNumSliders;
    local int I;
    local int J;
    local int nIndex;
    local int nMax;
    local int nValue;
    local ASParams stParam;
    local array<ASParams> lstParams;
    
    m_sCode = "";
    nIndex = 0;
    nNumCategories = m_oBioMorphFrontEnd.GetNumberOfFeatureCategories();
    for (I = 0; I < nNumCategories; ++I)
    {
        nNumSliders = m_oBioMorphFrontEnd.GetNumSlidersInCategory(I);
        for (J = 0; J < nNumSliders; ++J)
        {
            if (nIndex > 0 && nIndex %  3 == 0)
            {
                m_sCode = m_sCode $ ".";
            }
            nMax = m_oBioMorphFrontEnd.GetSliderMax(I, J);
            nValue = Rand(nMax + 1);
            if (nValue < 9)
            {
                nValue += Asc("1");
            }
            else if (nValue < 34)
            {
                nValue += Asc("A") - 9;
                if (nValue >= Asc("O"))
                {
                    nValue++;
                }
            }
            else
            {
                nValue = Asc("!");
            }
            m_sCode = m_sCode $ Chr(nValue);
            nIndex = nIndex + 1;
        }
    }
    ApplyNewCode(m_sCode);
    SetSliderPositions();
    UpdateCode();
}