public function PostBeginPlay()
{
    local Class<BioBaseSquad> SquadClass;
    local BioAiController oController;
    local BioWeaponRanged weap;
    local Name RandomWeaponManf;
    
    Super.PostBeginPlay();
    m_oBehavior.PostBeginPlay();
    SetBaseEyeheight();
    CylinderComponent.SetTraceBlocking(default.CylinderComponent.BlockZeroExtent, default.CylinderComponent.BlockNonZeroExtent);
    m_oCustomCollision.SetTraceBlocking(default.m_oCustomCollision.BlockZeroExtent, default.m_oCustomCollision.BlockNonZeroExtent);
    Mesh.SetTraceBlocking(default.Mesh.BlockZeroExtent, default.Mesh.BlockNonZeroExtent);
    Mesh.SetActorCollision(default.Mesh.CollideActors, default.Mesh.BlockActors, default.Mesh.AlwaysCheckCollision);

    Class'MERControl'.static.RandomizeBioPawn(Self);

}