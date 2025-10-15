namespace Hack.io.GX;

/// <summary>
/// Texture filtering modes used when zooming in and out
/// </summary>
public enum GXFilterMode : byte
{
    Nearest = 0x00,
    Linear = 0x01,
    // Min only
    NearestMipmapNearest = 0x02,
    NearestMipmapLinear = 0x03,
    LinearMipmapNearest = 0x04,
    LinearMipmapLinear = 0x05
}