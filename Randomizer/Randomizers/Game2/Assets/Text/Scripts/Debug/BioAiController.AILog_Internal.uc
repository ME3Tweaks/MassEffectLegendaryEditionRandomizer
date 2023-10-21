public final event function AILog_Internal(coerce string LogText, optional Name LogCategory, optional bool bForce)
{
    local int Idx;
    
    if (!bForce && !bAILogging)
    {
        return;
    }
    //if (!bForce)
    //{
    //    for (Idx = 0; Idx < AILogFilter.Length; Idx++)
    //    {
    //        if (AILogFilter[Idx] == LogCategory)
    //        {
    //            return;
    //        }
    //    }
    //}
    if (AILogFile == None)
    {
        AILogFile = Spawn(Class'FileLog');
        AILogFile.bKillDuringLevelTransition = TRUE;
        AILogFile.OpenLog(string(Self), ".ailog");
    }
    AILogFile.Logf((((((Pawn @ "[") $ WorldInfo.TimeSeconds) $ "]") @ GetStateName()) $ ":") @ LogText);
    if (bAILogToWindow)
    {
        LogInternal((((((Pawn @ "[") $ WorldInfo.TimeSeconds) $ "]") @ GetStateName()) $ ":") @ LogText, );
    }
}