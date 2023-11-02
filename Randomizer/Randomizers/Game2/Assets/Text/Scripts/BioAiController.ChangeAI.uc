public function ChangeAI(Class<BioAiController> NewAIClass, optional bool bCacheOldController)
{
    local BioAiController NewController;
    
    if (Pawn.Controller.Class == NewAIClass)
    {
        return;
    }
    NewController = Spawn(NewAIClass);
    if (NewController != None)
    {
        Pawn.Controller.PopState(TRUE);
        NewController.Possess(Pawn, FALSE);
        NewController.Initialize();
        if (bCacheOldController)
        {
            NewController.OldController = Self;
        }
        else
        {
            Destroy();
        }
    }
}