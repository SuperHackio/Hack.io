namespace Hack.io.GX;

/// <summary>
/// Texture Wrap modes
/// </summary>
public enum GXWrapMode
{
    /// <summary>
    /// Do not tile the texture in any way
    /// </summary>
    CLAMP = 0x00,
    /// <summary>
    /// Tile Texture
    /// </summary>
    REPEAT = 0x01,
    /// <summary>
    /// Tile Texture while mirroring every other tile
    /// </summary>
    MIRROR = 0x02
}