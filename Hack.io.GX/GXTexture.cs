
namespace Hack.io.GX;

public class GXTexture
{
    public GXTextureFormat TextureFormat => mTextureFormat;
    public GXPaletteFormat PaletteFormat => mPaletteFormat;
    public GXWrapMode WrapS => mWrapS;
    public GXWrapMode WrapT => mWrapT;
    public GXFilterMode MagnificationFilter => mMagnificationFilter;
    public GXFilterMode MinificationFilter => mMinificationFilter;
    public float MinLOD => mMinLOD;
    public float MaxLOD => mMaxLOD;
    public float LODBias => mLODBias;
    public bool EnableEdgeLOD => mEnableEdgeLOD;
    public int Width => mWidth;
    public int Height => mHeight;
    public int TextureCount => mTextureCount;
    public int? PaletteCount => mPaletteData is not null ? mPaletteCount : null;
    public ReadOnlySpan<byte> TextureData => mTextureData;
    public ReadOnlySpan<byte> PaletteData => mPaletteData;

    protected GXTextureFormat mTextureFormat;
    protected GXPaletteFormat mPaletteFormat;
    protected GXWrapMode mWrapS;
    protected GXWrapMode mWrapT;
    protected GXFilterMode mMagnificationFilter;
    protected GXFilterMode mMinificationFilter;
    protected float mMinLOD;
    protected float mMaxLOD;
    protected float mLODBias;
    protected bool mEnableEdgeLOD;
    protected int mWidth;
    protected int mHeight;
    protected int mTextureCount;
    protected int mPaletteCount;
    protected byte[] mTextureData = [];
    protected byte[]? mPaletteData = null;


    protected virtual void ReadTexture(Stream Strm, long TextureDataPos, long? PaletteDataPos)
    {
        if (PaletteDataPos.HasValue)
        {
            Strm.Position = PaletteDataPos.Value;
            mPaletteData = new byte[mPaletteCount * 2];
            _ = Strm.Read(mPaletteData);
        }

        int DataLength = Utility.CalculateTextureDataSize(mTextureFormat, mWidth, mHeight, mTextureCount);
        mTextureData = new byte[DataLength];
        Strm.Position = TextureDataPos;
        Strm.Read(mTextureData);
    }

    protected virtual void WriteTexture(Stream Strm, long TextureDataPos, long? PaletteDataPos)
    {
        Strm.Position = TextureDataPos;
        Strm.Write(mTextureData);
        if (PaletteDataPos.HasValue && mPaletteData is not null)
        {
            Strm.Position = PaletteDataPos.Value;
            Strm.Write(mPaletteData);
        }
    }

    public override bool Equals(object? obj)
        => obj is GXTexture texture &&
            mTextureFormat == texture.mTextureFormat &&
            mPaletteFormat == texture.mPaletteFormat &&
            mWrapS == texture.mWrapS &&
            mWrapT == texture.mWrapT &&
            mMagnificationFilter == texture.mMagnificationFilter &&
            mMinificationFilter == texture.mMinificationFilter &&
            mMinLOD == texture.mMinLOD &&
            mMaxLOD == texture.mMaxLOD &&
            mLODBias == texture.mLODBias &&
            mEnableEdgeLOD == texture.mEnableEdgeLOD &&
            mWidth == texture.mWidth &&
            mHeight == texture.mHeight &&
            mTextureCount == texture.mTextureCount &&
            mPaletteCount == texture.mPaletteCount &&
        mTextureData.SequenceEqual(texture.mTextureData) &&
        (mPaletteData?.SequenceEqual(texture.mPaletteData) ?? texture.mPaletteData is null);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(mTextureFormat);
        hash.Add(mPaletteFormat);
        hash.Add(mWrapS);
        hash.Add(mWrapT);
        hash.Add(mMagnificationFilter);
        hash.Add(mMinificationFilter);
        hash.Add(mMinLOD);
        hash.Add(mMaxLOD);
        hash.Add(mLODBias);
        hash.Add(mEnableEdgeLOD);
        hash.Add(mWidth);
        hash.Add(mHeight);
        hash.Add(mTextureCount);
        hash.Add(mPaletteCount);
        hash.Add(mTextureData);
        hash.Add(mPaletteData);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Decodes texture data from the Wii's Graphics Format
/// </summary>
/// <typeparam name="T">The resulting Data Type that the function will convert into</typeparam>
/// <param name="TexData">Wii Graphics texture byte data</param>
/// <param name="PalData">Wii Graphics palette byte data</param>
/// <param name="PalFormat">Wii Graphics palette format</param>
/// <param name="Width">Width of the texture</param>
/// <param name="Height">Height of the texture</param>
/// <param name="Levels">Number of image levels<para/>Should always be greater than 0</param>
/// <returns>A data type representing the decoded image data</returns>
public delegate T GXTextureDecoder<T>(ReadOnlySpan<byte> TexData, ReadOnlySpan<byte> PalData, GXPaletteFormat PalFormat, int Width, int Height, int Levels);

public delegate (byte[] TexData, byte[]? PalData) GXTextureEncoder<T>(T Source);