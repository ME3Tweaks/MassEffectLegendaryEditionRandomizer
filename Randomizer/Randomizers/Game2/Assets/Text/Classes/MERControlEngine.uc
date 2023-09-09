﻿Class MERControlEngine
    abstract
    config(Engine);

// Variables
var config bool bLightRandomizer;
var config bool bFogRandomizer;
var config bool bPlayerMovementSpeedRandomizer;
var config bool bNPCMovementSpeedRandomizer;
var config bool bPawnLookatRandomizer;
var config bool bEyeRandomizer;

var config bool bEnemyWeaponRandomizer;
var config bool bEnemyWeaponRandomizer_Force;
var config bool bEnemyWeaponRandomizer_AllowInvisible;
var config bool bEnemyWeaponRandomizer_OneTime;

var config bool bEnemyPowerRandomizer;
var config bool bEnemyPowerRandomizer_Force;
var config bool bEnemyPowerRandomizer_EnforceMinPowerCount;
var config bool bEnemyPowerRandomizer_OneTime;

var config array<string> MEREyeIFPs;
var config float fBioMorphFaceRandomization;

// Functions
public static function float RandFloat(float minf, float maxf)
{
    return Lerp(minf, maxf, FRand());
}
public static function Vector RandVector(float MinX, float MaxX, float MinY, float MaxY, float maxz, float minz)
{
    local Vector V;
    
    V.X = RandFloat(MinX, MinY);
    V.Y = RandFloat(MinX, MinY);
    V.Z = RandFloat(MinX, MinY);
    return V;
}
public static function LinearColor RandLinearColor(float MinR, float MaxR, float MinG, float MaxG, float MinB, float MaxB, float mina, float maxa)
{
    local LinearColor LC;
    
    LC.R = RandFloat(MinR, MaxR);
    LC.G = RandFloat(MinG, MaxG);
    LC.B = RandFloat(MinB, MaxB);
    LC.A = RandFloat(mina, maxa);
    return LC;
}
public static function MarkObjectModified(Object Obj)
{
    Obj.ObjectFlags.B = Obj.ObjectFlags.B | 8;
}
public static function bool IsObjectModified(Object Obj)
{
    return (Obj.ObjectFlags.B & 8) == 8;
}
public static function bool IsStringCapitalized(string Str)
{
    return "a" > Str;
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}