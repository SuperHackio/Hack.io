namespace Hack.io.GX;

public enum GXPrimitiveType
{
    None = 0,

    Quads = 0x80,
    Triangles = 0x90,
    TriangleStrips = 0x98,
    TriangleFan = 0xA0,
    Lines = 0xA8,
    LineStrips = 0xB0,
    Points = 0xB8
}
