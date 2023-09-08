
public event function PostBeginPlay()
{
    local LinearColor Col;
    local ActorComponent AC;
    local HeightFogComponent HFC;
    local float ThemeRMin;
    local float ThemeRMax;
    local float ThemeGMin;
    local float ThemeGMax;
    local float ThemeBMin;
    local float ThemeBMax;
    
    Super(Actor).PostBeginPlay();
    bEnabled = Component.bEnabled;

    // MER code begins here
    if (!Class'MERControlEngine'.default.bFogRandomizer)
    {
        return;
    }
    ThemeRMin = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeRMax = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeGMin = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeGMax = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeBMin = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    ThemeBMax = Class'MERControlEngine'.static.RandFloat(0.0, 255.0);
    foreach AllComponents(AC, )
    {
        HFC = HeightFogComponent(AC);
        if (HFC == None)
        {
            continue;
        }
        Col = Class'MERControlEngine'.static.RandLinearColor(ThemeRMin, ThemeRMax, ThemeGMin, ThemeGMax, ThemeBMin, ThemeBMax, 0.0, 1.0);
        HFC.LightColor.R = byte(Col.R);
        HFC.LightColor.G = byte(Col.G);
        HFC.LightColor.B = byte(Col.B);
        if (Rand(4) == 0)
        {
            HFC.Density *= Class'MERControlEngine'.static.RandFloat(1.0, 5.0);
        }
        else
        {
            HFC.Density *= Class'MERControlEngine'.static.RandFloat(0.0, 2.0);
        }
        HFC.LightBrightness *= Class'MERControlEngine'.static.RandFloat(0.0, 2.0);
        HFC.ExtinctionDistance *= Class'MERControlEngine'.static.RandFloat(0.0, 2.0);
        HFC.StartDistance *= Class'MERControlEngine'.static.RandFloat(0.0, 2.0);
        HFC.FalloffStrength *= Class'MERControlEngine'.static.RandFloat(0.0, 2.0);
    }
    ForceUpdateComponents();
}