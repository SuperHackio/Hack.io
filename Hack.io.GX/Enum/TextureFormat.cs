namespace Hack.io.GX;

/// <summary>
/// The Wii's imaging formats
/// </summary>
public enum GXTextureFormat
{
    /// <summary>
    /// Greyscale - 4 bits/pixel (bpp) | Block Width: 8 | Block height: 8 | Block size: 32 bytes
    /// </summary>
    I4 = 0x00,
    /// <summary>
    /// Greyscale - 8 bits/pixel (bpp) | Block Width: 8 | Block height: 4 | Block size: 32 bytes
    /// </summary>
    I8 = 0x01,
    /// <summary>
    /// Greyscale + Alpha - 8 bits/pixel (bpp) | Block Width: 8 | Block height: 4 | Block size: 32 bytes
    /// </summary>
    IA4 = 0x02,
    /// <summary>
    /// Greyscale + Alpha - 16 bits/pixel (bpp) | Block Width: 4 | Block height: 4 | Block size: 32 bytes
    /// </summary>
    IA8 = 0x03,
    /// <summary>
    /// Colour - 16 bits/pixel (bpp) | Block Width: 4 | Block height: 4 | Block size: 32 bytes
    /// </summary>
    RGB565 = 0x04,
    /// <summary>
    /// Colour + Alpha - 16 bits/pixel (bpp) | Block Width: 4 | Block height: 4 | Block size: 32 bytes
    /// </summary>
    RGB5A3 = 0x05,
    /// <summary>
    /// Colour + Alpha - 32 bits/pixel (bpp) | Block Width: 4 | Block height: 4 | Block size: 64 bytes
    /// </summary>
    RGBA8 = 0x06,
    /// <summary>
    /// Palette - 4 bits/pixel (bpp) | Block Width: 8 | Block Height: 8 | Block size: 32 bytes
    /// </summary>
    C4 = 0x08,
    /// <summary>
    /// Palette - 8 bits/pixel (bpp) | Block Width: 8 | Block Height: 4 | Block size: 32 bytes
    /// </summary>
    C8 = 0x09,
    /// <summary>
    /// Palette - 14 bits/pixel (bpp) | Block Width: 4 | Block Height: 4 | Block size: 32 bytes
    /// </summary>
    C14X2 = 0x0A,
    /// <summary>
    /// Colour + Alpha (1 bit) - 4 bits/pixel (bpp) | Block Width: 8 | Block height: 8 | Block size: 32 bytes
    /// </summary>
    CMPR = 0x0E
}

[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class GXTextureFormatUtil
{
    public static int GetMaxColors(this GXTextureFormat Format) => Format switch
    {
        GXTextureFormat.C4 => 1 << 4,//16
        GXTextureFormat.C8 => 1 << 8,//256
        GXTextureFormat.C14X2 => 1 << 14,//16384
        _ => throw new Exception("Not a Palette format!"),
    };

    public static bool IsPaletteFormat(this GXTextureFormat Format) => Format is GXTextureFormat.C4 or GXTextureFormat.C8 or GXTextureFormat.C14X2;

    public static int GetBlockSize(this GXTextureFormat Format) => Format is GXTextureFormat.RGBA8 ? 64 : 32;
}