using System.Numerics;
using Color = Hack.io.Class.Color<byte>;

namespace Hack.io.GX;

public class GXPrimitive
{
    protected GXPrimitiveType mType;

    // These vectors contain one additional value for use with the "Mtx" versions of attributes. Anything below 0 is treated as "not existing".
    // The only one that doesn't get a Mtx version is the Normals and Colors
    public Vector4[]? Positions;
    public Vector3[]? Normals;
    public Color[]? Color0;
    public Color[]? Color1;
    public Vector3[]? TexCoord0;
    public Vector3[]? TexCoord1;
    public Vector3[]? TexCoord2;
    public Vector3[]? TexCoord3;
    public Vector3[]? TexCoord4;
    public Vector3[]? TexCoord5;
    public Vector3[]? TexCoord6;
    public Vector3[]? TexCoord7;
}
