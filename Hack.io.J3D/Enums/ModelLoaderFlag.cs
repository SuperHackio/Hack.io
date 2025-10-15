namespace Hack.io.J3D;

[Flags]
public enum ModelLoaderFlag
{
    None = 0x00000000,
    MtxSoftImageCalc = 0x00000001,
    MtxMayaCalc = 0x00000002,
    _03 = 0x00000004,
    _04 = 0x00000008,
    MtxTypeMask = MtxSoftImageCalc | MtxMayaCalc | _03 | _04,  // 0 - 2 (0 = Basic, 1 = SoftImage, 2 = Maya)
    UseImmediateMtx = 0x00000010,
    UsePostTexMtx = 0x00000020,
    _07 = 0x00000040,
    _08 = 0x00000080,
    NoMatrixTransform = 0x00000100,
    _10 = 0x00000200,
    _11 = 0x00000400,
    _12 = 0x00000800,
    _13 = 0x00001000,
    DoBdlMaterialCalc = 0x00002000,
    NoBdlMaterialPatch = 0x00004000,
    _16 = 0x00008000,
}