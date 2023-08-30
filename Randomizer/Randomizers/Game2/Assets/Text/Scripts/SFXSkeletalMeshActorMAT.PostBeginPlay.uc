public function PostBeginPlay()
{
    Super(SkeletalMeshActor).PostBeginPlay();
    Class'MERControl'.static.InitSFXSkeletalMeshActorMAT(Self);
}