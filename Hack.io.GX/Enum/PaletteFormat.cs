namespace Hack.io.GX;

/// <summary>
/// Texture Pallete Formats
/// </summary>
public enum GXPaletteFormat
{
    /// <summary>
    /// Greyscale + Alpha - 16 bits/pixel (bpp) | Block Width: 4 | Block height: 4 | Block size: 32 bytes
    /// </summary>
    IA8 = 0x00,
    /// <summary>
    /// Colour - 16 bits/pixel (bpp) | Block Width: 4 | Block height: 4 | Block size: 32 bytes
    /// </summary>
    RGB565 = 0x01,
    /// <summary>
    /// Colour + Alpha - 16 bits/pixel (bpp) | Block Width: 4 | Block height: 4 | Block size: 32 bytes
    /// </summary>
    RGB5A3 = 0x02
}