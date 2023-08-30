public function PostBeginPlay()
{
    local LinearColor Col;
    local ActorComponent AC;
    local LightComponent LC;
    local float ThemeRMin;
    local float ThemeRMax;
    local float ThemeGMin;
    local float ThemeGMax;
    local float ThemeBMin;
    local float ThemeBMax;
    
    if (!Class'MERControlEngine'.default.bLightRandomizer)
    {
        return;
    }
    //bStatic = FALSE;
    ThemeRMin = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeRMax = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeGMin = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeGMax = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeBMin = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeBMax = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    //LogInternal((("UPDATING LIGHTING ON " $ Self) $ ": ") $ AllComponents.Length, );
    foreach AllComponents(AC, )
    {
        //LogInternal("running on " $ AC, );
        LC = LightComponent(AC);
        if (LC == None)
        {
            continue;
        }
        Col = Class'MERControlEngine'.static.RandLinearColor(ThemeRMin, ThemeRMax, ThemeGMin, ThemeGMax, ThemeBMin, ThemeBMax, 0.0, 1.0);
        LC.LightColor.R = byte(Col.R);
        LC.LightColor.G = byte(Col.G);
        LC.LightColor.B = byte(Col.B);
        LC.Brightness *= Class'MERControlEngine'.static.RandFloat(0.5, 1.5);
        //LogInternal((((((((("Light update for " $ LC) $ ": RGBA: ") $ LC.LightColor.R) $ ",") $ LC.LightColor.G) $ ",") $ LC.LightColor.B) $ ",") $ LC.LightColor.A, );
        LC.UpdateColorAndBrightness();
    }
    ForceUpdateComponents();
}