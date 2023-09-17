Class SFXObjectPinner
    abstract;

// Functions
public static function AddPinnedObject(Object Obj)
{
    local SFXEngine Engine;
    
    Engine = Class'SFXEngine'.static.GetEngine();
    Engine.PinnedObjects.AddItem(Obj);
    LogInternal("OBJECT PINNER: Pinned " $ Obj, );
}
public static function Object GetPinnedObject(Class<Object> ClassType)
{
    local SFXEngine Engine;
    local int I;
    
    Engine = Class'SFXEngine'.static.GetEngine();
    for (I = 0; I < Engine.PinnedObjects.Length; I++)
    {
        if (Engine.PinnedObjects[I].Class == ClassType)
        {
            LogInternal("OBJECT PINNER: Retreived pinned object " $ Engine.PinnedObjects[I], );
            return Engine.PinnedObjects[I];
        }
    }
    LogInternal(("OBJECT PINNER: Cannot retreive pinned object; no object of type " $ ClassType) $ " is pinned", );
    return None;
}
public static function bool ReleasePinnedObject(Class<Object> ClassType)
{
    local SFXEngine Engine;
    local int I;
    
    Engine = Class'SFXEngine'.static.GetEngine();
    for (I = 0; I < Engine.PinnedObjects.Length; I++)
    {
        if (Engine.PinnedObjects[I].Class == ClassType)
        {
            LogInternal("OBJECT PINNER: Unpinning " $ Engine.PinnedObjects[I], );
            Engine.PinnedObjects.RemoveItem(Engine.PinnedObjects[I]);
            return TRUE;
        }
    }
    LogInternal(("OBJECT PINNER: Cannot unpin object; no object of type " $ ClassType) $ " is pinned", );
    LogInternal(("OBJECT PINNER: Currently there are " $ Engine.PinnedObjects.Length) $ " pinned objects:", );
    for (I = 0; I < Engine.PinnedObjects.Length; I++)
    {
        LogInternal((("OBJECT PINNER: " $ I) $ " - ") $ Engine.PinnedObjects[I], );
    }
    return FALSE;
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}