using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using static Hack.io.J3D.JUtility;

namespace Hack.io.BTI
{
    public class BTI
    {
        /// <summary>
        /// Filename of this <see cref="BTI"/> file.
        /// </summary>
        public string FileName { get; set; } = null;
        private List<Bitmap> mipmaps = new List<Bitmap>();
        public GXImageFormat Format { get; set; }
        public GXPaletteFormat PaletteFormat { get; set; }
        public JUTTransparency AlphaSetting { get; set; }
        public GXWrapMode WrapS { get; set; }
        public GXWrapMode WrapT { get; set; }
        public GXFilterMode MagnificationFilter { get; set; }
        public GXFilterMode MinificationFilter { get; set; }
        public float MinLOD { get; set; } // Fixed point number, 1/8 = conversion (ToDo: is this multiply by 8 or divide...)
        public float MaxLOD { get; set; } // Fixed point number, 1/8 = conversion (ToDo: is this multiply by 8 or divide...)
        public bool EnableEdgeLOD { get; set; }
        public float LODBias { get; set; } // Fixed point number, 1/100 = conversion
        public bool ClampLODBias { get; set; }
        public byte MaxAnisotropy { get; set; }

        #region Auto Properties
        /// <summary>
        /// The Amount of images inside this BTI. Basically the mipmap count
        /// </summary>
        public int ImageCount => mipmaps == null ? -1 : mipmaps.Count;
        public Bitmap this[int MipmapLevel]
        {
            get => mipmaps[MipmapLevel];
            set
            {
                if (MipmapLevel < 0)
                    throw new ArgumentOutOfRangeException("MipmapLevel");
                if (MipmapLevel == 0)
                    mipmaps[0] = value;
                else
                {
                    int requiredmipwidth = mipmaps[0].Width, requiredmipheight = mipmaps[0].Height;
                    for (int i = 0; i < MipmapLevel; i++)
                    {
                        if (requiredmipwidth == 1 || requiredmipwidth == 1)
                            throw new Exception($"The provided Mipmap Level is too high and will provide image dimensions less than 1x1. Currently, the Max Mipmap Level is {i-1}");
                        requiredmipwidth /= 2;
                        requiredmipheight /= 2;
                    }
                    if (value.Width != requiredmipwidth || value.Height != requiredmipheight)
                        throw new Exception($"The dimensions of the provided mipmap are supposed to be {requiredmipwidth}x{requiredmipheight}");

                    if (MipmapLevel == mipmaps.Count)
                        mipmaps.Add(value);
                    else if (MipmapLevel > mipmaps.Count)
                    {
                        while (mipmaps.Count - 1 != MipmapLevel)
                            mipmaps.Add(new Bitmap(mipmaps[mipmaps.Count - 1], new Size(mipmaps[mipmaps.Count - 1].Width / 2, mipmaps[mipmaps.Count - 1].Height / 2)));
                        mipmaps.Add(value);
                    }
                    else
                        mipmaps[MipmapLevel] = value;
                }
            }
        }
        #endregion

        public BTI(string filename)
        {
            FileStream BTIFile = new FileStream(filename, FileMode.Open);
            Read(BTIFile);
            BTIFile.Close();
        }

        public BTI(Stream memorystream) => Read(memorystream);

        public void Save(string filename)
        {
            FileStream BTIFile = new FileStream(filename, FileMode.Create);
            long BaseDataOffset = 0x20;
            Write(BTIFile, ref BaseDataOffset);
            BTIFile.Close();
        }

        public void Save(Stream BTIFile, ref long DataOffset) => Write(BTIFile, ref DataOffset);

        private void Read(Stream BTIFile)
        {
            long HeaderStart = BTIFile.Position;
            Format = (GXImageFormat)BTIFile.ReadByte();
            AlphaSetting = (JUTTransparency)BTIFile.ReadByte();
            ushort ImageWidth = BitConverter.ToUInt16(BTIFile.ReadReverse(0, 2), 0);
            ushort ImageHeight = BitConverter.ToUInt16(BTIFile.ReadReverse(0, 2), 0);
            WrapS = (GXWrapMode)BTIFile.ReadByte();
            WrapT = (GXWrapMode)BTIFile.ReadByte();
            bool UsePalettes = BTIFile.ReadByte() > 0;
            short PaletteCount = 0;
            uint PaletteDataAddress = 0;
            byte[] PaletteData = null;
            if (UsePalettes)
            {
                PaletteFormat = (GXPaletteFormat)BTIFile.ReadByte();
                PaletteCount = BitConverter.ToInt16(BTIFile.ReadReverse(0, 2), 0);
                PaletteDataAddress = BitConverter.ToUInt32(BTIFile.ReadReverse(0, 4), 0);
                long PausePosition = BTIFile.Position;
                BTIFile.Position = HeaderStart + PaletteDataAddress;
                PaletteData = BTIFile.Read(0, PaletteCount * 2);
                BTIFile.Position = PausePosition;
            }
            else
                BTIFile.Position += 7;
            bool EnableMipmaps = BTIFile.ReadByte() > 0;
            EnableEdgeLOD = BTIFile.ReadByte() > 0;
            ClampLODBias = BTIFile.ReadByte() > 0;
            MaxAnisotropy = (byte)BTIFile.ReadByte();
            MinificationFilter = (GXFilterMode)BTIFile.ReadByte();
            MagnificationFilter = (GXFilterMode)BTIFile.ReadByte();
            MinLOD = ((sbyte)BTIFile.ReadByte() / 8.0f);
            MaxLOD = ((sbyte)BTIFile.ReadByte() / 8.0f);

            byte TotalImageCount = (byte)BTIFile.ReadByte();
            BTIFile.Position++;
            LODBias = BitConverter.ToInt16(BTIFile.ReadReverse(0, 2), 0) / 100.0f;
            uint ImageDataAddress = BitConverter.ToUInt32(BTIFile.ReadReverse(0, 4), 0);

            BTIFile.Position = HeaderStart + ImageDataAddress;
            ushort ogwidth = ImageWidth, ogheight = ImageHeight;
            for (int i = 0; i < TotalImageCount; i++)
            {
                if (i > 0)
                {
                    ImageWidth = (ushort)(ImageWidth / 2);
                    ImageHeight = (ushort)(ImageHeight / 2);
                }
                byte[] ImageData = null;
                Bitmap Result = null;
                switch (Format)
                {
                    case GXImageFormat.I4:
                        #region DecodeI4
                        ImageData = BTIFile.Read(0, (GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight)) / 2);
                        Result = DecodeImage(ImageData, null, Format, null, null, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.I8:
                        #region DecodeI8
                        ImageData = BTIFile.Read(0, GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight));
                        Result = DecodeImage(ImageData, null, Format, null, null, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.IA4:
                        #region DecodeIA4
                        ImageData = BTIFile.Read(0, GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight));
                        Result = DecodeImage(ImageData, null, Format, null, null, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.IA8:
                        #region DecodeIA8
                        ImageData = BTIFile.Read(0, (GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight)) * 2);
                        Result = DecodeImage(ImageData, null, Format, null, null, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.RGB565:
                        #region DecodeRGB565
                        ImageData = BTIFile.Read(0, (GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight)) * 2);
                        Result = DecodeImage(ImageData, null, Format, null, null, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.RGB5A3:
                        #region DecodeRGB5A3
                        ImageData = BTIFile.Read(0, (GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight)) * 2);
                        Result = DecodeImage(ImageData, null, Format, null, null, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.RGBA32:
                        #region DecodeRGBA32
                        ImageData = BTIFile.Read(0, (GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight)) * 4);
                        Result = DecodeImage(ImageData, null, Format, null, null, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.C4:
                        #region DecodeC4
                        ImageData = BTIFile.Read(0, (GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight)) / 2);
                        Result = DecodeImage(ImageData, PaletteData, Format, PaletteFormat, PaletteCount, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.C8:
                        #region DecodeC8
                        ImageData = BTIFile.Read(0, GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight));
                        Result = DecodeImage(ImageData, PaletteData, Format, PaletteFormat, PaletteCount, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.C14X2:
                        #region DecodeC14X2
                        ImageData = BTIFile.Read(0, (GetFullWidth(ImageWidth) * GetFullHeight(ImageHeight)) * 2);
                        Result = DecodeImage(ImageData, PaletteData, Format, PaletteFormat, PaletteCount, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    case GXImageFormat.CMPR:
                        #region DecodeDXT1
                        int FullHeight = GetFullHeight(ImageHeight), FullWidth = GetFullWidth(ImageWidth);
                        int BytesNeededForEncode = FullWidth * FullHeight * 4 / 8;
                        ImageData = BTIFile.Read(0, BytesNeededForEncode);
                        Result = DecodeImage(ImageData, null, Format, null, null, GetFullWidth(ImageWidth), GetFullHeight(ImageHeight));
                        #endregion
                        break;
                    default:
                        throw new Exception("Invalid format");
                }
                mipmaps.Add(Result);
            }
        }

        private void Write(Stream BTIFile, ref long DataOffset)
        {
            List<byte> ImageData = new List<byte>();
            List<byte> PaletteData = new List<byte>();
            GetImageAndPaletteData(ref ImageData, ref PaletteData, mipmaps, Format, PaletteFormat);
            long HeaderStart = BTIFile.Position;
            int ImageDataStart = (int)((DataOffset + PaletteData.Count) - HeaderStart), PaletteDataStart = (int)(DataOffset - HeaderStart);
            BTIFile.WriteByte((byte)Format);
            BTIFile.WriteByte((byte)AlphaSetting);
            BTIFile.WriteReverse(BitConverter.GetBytes((ushort)mipmaps[0].Width), 0, 2);
            BTIFile.WriteReverse(BitConverter.GetBytes((ushort)mipmaps[0].Height), 0, 2);
            BTIFile.WriteByte((byte)WrapS);
            BTIFile.WriteByte((byte)WrapT);
            if (IsPaletteFormat(Format))
            {
                BTIFile.WriteByte(0x01);
                BTIFile.WriteByte((byte)PaletteFormat);
                BTIFile.WriteReverse(BitConverter.GetBytes((ushort)(PaletteData.Count/2)), 0, 2);
                BTIFile.WriteReverse(BitConverter.GetBytes(PaletteDataStart), 0, 4);
            }
            else
                BTIFile.Write(new byte[8], 0, 8);

            BTIFile.WriteByte((byte)(mipmaps.Count > 1 ? 0x01 : 0x00));
            BTIFile.WriteByte((byte)(EnableEdgeLOD ? 0x01 : 0x00));
            BTIFile.WriteByte((byte)(ClampLODBias ? 0x01 : 0x00));
            BTIFile.WriteByte(MaxAnisotropy);
            BTIFile.WriteByte((byte)MinificationFilter);
            BTIFile.WriteByte((byte)MagnificationFilter);
            BTIFile.WriteByte((byte)(MinLOD * 8));
            BTIFile.WriteByte((byte)(MaxLOD * 8));
            BTIFile.WriteByte((byte)mipmaps.Count);
            BTIFile.WriteByte(0x00);
            BTIFile.WriteReverse(BitConverter.GetBytes((short)(LODBias * 100)), 0, 2);
            BTIFile.WriteReverse(BitConverter.GetBytes(ImageDataStart), 0, 4);

            long Pauseposition = BTIFile.Position;
            BTIFile.Position = DataOffset;

            BTIFile.Write(PaletteData.ToArray(), 0, PaletteData.Count);
            BTIFile.Write(ImageData.ToArray(), 0, ImageData.Count);
            DataOffset = BTIFile.Position;
            BTIFile.Position = Pauseposition;
        }
    }

}