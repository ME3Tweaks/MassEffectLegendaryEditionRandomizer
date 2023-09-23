public function PostBeginPlay()
{
    Super(BioPawn).PostBeginPlay();
    if (Mesh.GetSocketByName('eyeRight_VFX') != None)
    {
        Mesh.AttachComponentToSocket(RightEye, 'eyeRight_VFX');
    }
    if (Mesh.GetSocketByName('eyeLeft_VFX') != None)
    {
        Mesh.AttachComponentToSocket(LeftEye, 'eyeLeft_VFX');
    }
    if (Mesh.GetSocketByName('eyeLeft2_VFX') != None)
    {
        Mesh.AttachComponentToSocket(LeftEyes, 'eyeLeft2_VFX');
    }
    if (Mesh.GetSocketByName('chestdisc_VFX') != None)
    {
        Mesh.AttachComponentToSocket(Chest, 'chestdisc_VFX');
    }
}