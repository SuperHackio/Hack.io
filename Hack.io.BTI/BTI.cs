using Hack.io.GX;
using Hack.io.Interface;
using Hack.io.Utility;

namespace Hack.io.BTI;

public class BTI : GXTexture, ILoadSaveFile
{
    public JUTTransparency AlphaSetting => mAlphaSetting;
    public bool ClampLODBias => mClampLODBias;
    public byte MaxAnisotropy => mMaxAnisotropy;
    public bool EnableMipmaps => mEnableMipmaps;

    protected JUTTransparency mAlphaSetting;
    protected bool mClampLODBias;
    protected byte mMaxAnisotropy;
    protected bool mEnableMipmaps;


    public void Load(Stream Strm)
    {
        long HeaderStart = Strm.Position;
        mTextureFormat = Strm.ReadEnum<GXTextureFormat, byte>(StreamUtil.ReadUInt8);
        mAlphaSetting = Strm.ReadEnum<JUTTransparency, byte>(StreamUtil.ReadUInt8);
        mWidth = Strm.ReadUInt16();
        mHeight = Strm.ReadUInt16();
        mWrapS = Strm.ReadEnum<GXWrapMode, byte>(StreamUtil.ReadUInt8);
        mWrapT = Strm.ReadEnum<GXWrapMode, byte>(StreamUtil.ReadUInt8);
        bool UsePalette = Strm.ReadByte() != 0;
        long? PaletteDataAddress = null;
        if (UsePalette)
        {
            mPaletteFormat = Strm.ReadEnum<GXPaletteFormat, byte>(StreamUtil.ReadUInt8);
            mPaletteCount = Strm.ReadUInt16();
            PaletteDataAddress = HeaderStart + Strm.ReadUInt32();
        }
        else
            Strm.Position += 7;
        mEnableMipmaps = Strm.ReadByte() != 0;
        mEnableEdgeLOD = Strm.ReadByte() != 0;
        mClampLODBias = Strm.ReadByte() != 0;
        mMaxAnisotropy = Strm.ReadUInt8();
        mMinificationFilter = Strm.ReadEnum<GXFilterMode, byte>(StreamUtil.ReadUInt8);
        mMagnificationFilter = Strm.ReadEnum<GXFilterMode, byte>(StreamUtil.ReadUInt8);
        mMinLOD = Strm.ReadInt8() / 8.0f;
        mMaxLOD = Strm.ReadInt8() / 8.0f;

        mTextureCount = Strm.ReadByte();
        if (mTextureCount == 0)
            mTextureCount = (int)MaxLOD;
        if (mTextureCount == 0)
            mTextureCount = 1;

        Strm.Position++; //Padding?
        mLODBias = Strm.ReadInt16() / 100.0f;
        long ImageDataAddress = HeaderStart + Strm.ReadUInt32();

        ReadTexture(Strm, ImageDataAddress, PaletteDataAddress);
    }

    public void Save(Stream Strm)
    {
        Save(Strm, (uint)Strm.Position + 0x20, 0x00);
    }

    public void Save(Stream Strm, uint DataPos, uint Start)
    {
        WriteHeader(Strm, Start);
        Strm.Position = DataPos;
        WriteTexture(Strm);
    }

    public void WriteHeader(Stream Strm, uint DataPos)
    {
        bool isNoPalette = mPaletteData is null;
        uint PaletteOffset = (uint)(DataPos + (isNoPalette ? 0 : mTextureData.Length));
        long HeaderStart = Strm.Position;
        Strm.WriteEnum<GXTextureFormat, byte>(mTextureFormat, StreamUtil.WriteUInt8);
        Strm.WriteEnum<JUTTransparency, byte>(mAlphaSetting, StreamUtil.WriteUInt8);
        Strm.WriteUInt16((ushort)mWidth);
        Strm.WriteUInt16((ushort)mHeight);
        Strm.WriteEnum<GXWrapMode, byte>(mWrapS, StreamUtil.WriteUInt8);
        Strm.WriteEnum<GXWrapMode, byte>(mWrapT, StreamUtil.WriteUInt8);
        Strm.WriteByte((byte)(isNoPalette ? 0 : 1));
        if (!isNoPalette)
        {
            Strm.WriteEnum<GXPaletteFormat, byte>(mPaletteFormat, StreamUtil.WriteUInt8);
            Strm.WriteUInt16((ushort)mPaletteCount);
            Strm.WriteUInt32(PaletteOffset);
        }
        else
        {
            Strm.WriteEnum<GXPaletteFormat, byte>(GXPaletteFormat.IA8, StreamUtil.WriteUInt8);
            Strm.WriteUInt16(0);
            Strm.WriteUInt32(PaletteOffset);
        }
        Strm.WriteByte((byte)(mEnableMipmaps ? 1 : 0));
        Strm.WriteByte((byte)(mEnableEdgeLOD ? 1 : 0));
        Strm.WriteByte((byte)(mClampLODBias ? 1 : 0));
        Strm.WriteByte(mMaxAnisotropy);
        Strm.WriteEnum<GXFilterMode, byte>(mMinificationFilter, StreamUtil.WriteUInt8);
        Strm.WriteEnum<GXFilterMode, byte>(mMagnificationFilter, StreamUtil.WriteUInt8);
        Strm.WriteInt8((sbyte)(mMinLOD * 8.0f));
        Strm.WriteInt8((sbyte)(mMaxLOD * 8.0f));
        Strm.WriteByte((byte)mTextureCount);
        Strm.WriteByte(0x00); //Padding?
        Strm.WriteInt16((short)(mLODBias * 100.0f));
        Strm.WriteUInt32(DataPos);
    }

    public void WriteTexture(Stream Strm)
    {
        //Palette Data comes after texture data
        WriteTexture(Strm, Strm.Position, mPaletteData is null ? null : Strm.Position + mTextureData.Length);
    }

    public override bool Equals(object? obj)
        => obj is BTI bTI &&
            base.Equals(obj) &&
            mAlphaSetting == bTI.mAlphaSetting &&
            mClampLODBias == bTI.mClampLODBias &&
            mMaxAnisotropy == bTI.mMaxAnisotropy &&
            mEnableMipmaps == bTI.mEnableMipmaps;

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), mAlphaSetting, mClampLODBias, mMaxAnisotropy, mEnableMipmaps);
}

public enum JUTTransparency
{
    /// <summary>
    /// No Transperancy
    /// </summary>
    OPAQUE = 0x00,
    /// <summary>
    /// Only allows fully Transperant pixels to be see through
    /// </summary>
    CUTOUT = 0x01,
    /// <summary>
    /// Allows Partial Transperancy. Also known as XLUCENT
    /// </summary>
    TRANSLUCENT = 0x02,
    /// <summary>
    /// Unknown
    /// </summary>
    SPECIAL = 0xCC
}
