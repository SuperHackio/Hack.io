﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Hack.io;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using static Hack.io.Util.GenericExtensions;

namespace Hack.io.J3D
{
    public static class J3DGraph
    {
        /// <summary>
        /// Padding string
        /// </summary>
        public static readonly string Padding = "Hack.io © Super Hackio Incorporated 2018-2021";

        /// <summary>
        /// Adds Padding to the Current Position in the provided Stream
        /// </summary>
        /// <param name="J3DFile">The Stream to add padding to</param>
        /// <param name="multiple">The byte multiple to pad to</param>
        public static void AddPadding(Stream J3DFile, int multiple)
        {
            int PadCount = 0;
            while (J3DFile.Position % multiple != 0)
                J3DFile.WriteByte((byte)Padding[PadCount++]);
        }

        /// <summary>
        /// Find the start of a sequence in the AllList, if the sequence exists, returns -1
        /// </summary>
        /// <param name="AllList"></param>
        /// <param name="Sequence"></param>
        /// <returns></returns>
        public static int FindSequence<T>(List<T> AllList, List<T> Sequence)
        {
            int matchup = 0, start = -1;

            bool found = false, started = false;

            for (int i = 0; i < AllList.Count; i++)
            {
                if (AllList[i].Equals(Sequence[matchup]))
                {
                    if (!started)
                    {
                        start = i;
                        started = true;
                    }
                    matchup++;
                    if (matchup == Sequence.Count)
                    {
                        found = true;
                        break;
                    }
                }
                else
                {
                    matchup = 0;
                    start = -1;
                    started = false;
                }
            }
            if (!found)
                start = -1;
            return start;
        }

        /// <summary>
        /// Represents a J3D Animation Track
        /// </summary>
        public class J3DKeyFrame
        {
            /// <summary>
            /// The Time in the timeline that this keyframe is assigned to
            /// </summary>
            public ushort Time { get; set; }
            /// <summary>
            /// The Value to set to
            /// </summary>
            public float Value { get; set; }
            /// <summary>
            /// Tangents affect the interpolation between two consecutive keyframes
            /// </summary>
            public float IngoingTangent { get; set; }
            /// <summary>
            /// Tangents affect the interpolation between two consecutive keyframes
            /// </summary>
            public float OutgoingTangent { get; set; }

            public J3DKeyFrame(ushort time, float value, float ingoing = 0, float? outgoing = null)
            {
                Time = time;
                Value = value;
                IngoingTangent = ingoing;
                OutgoingTangent = outgoing ?? ingoing;
            }
            public J3DKeyFrame(List<float> Data, int i, short Count, short Index, int Tangent)
            {
                TangentMode TM = (TangentMode)Tangent;
                if (Count == 1)
                {
                    Time = 0;
                    Value = Data[Index];
                    IngoingTangent = 0;
                    OutgoingTangent = 0;
                }
                else
                {
                    Time = (ushort)Data[i];
                    Value = Data[i + 1];
                    IngoingTangent = Data[i + 2];
                    OutgoingTangent = TM == TangentMode.DESYNC ? Data[i + 3] : IngoingTangent;
                }
            }
            /// <summary>
            /// Converts the values based on a rotation multiplier
            /// </summary>
            /// <param name="RotationFraction">The byte in the file that determines the rotation fraction</param>
            /// <param name="Revert">Undo the conversion</param>
            public void ConvertRotation(byte RotationFraction, bool Revert = false)
            {
                float RotationMultiplier = (float)(Math.Pow(RotationFraction, 2) * (180.0 / 32768.0));
                Value           = Revert ? Value           / RotationMultiplier : Value           * RotationMultiplier;
                IngoingTangent  = Revert ? IngoingTangent  / RotationMultiplier : IngoingTangent  * RotationMultiplier;
                OutgoingTangent = Revert ? OutgoingTangent / RotationMultiplier : OutgoingTangent * RotationMultiplier;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => string.Format("Time: {0}, Value: {1}, Ingoing: {2}, Outgoing: {3}", Time, Value, IngoingTangent, OutgoingTangent);

            public override bool Equals(object obj)
            {
                return obj is J3DKeyFrame frame &&
                        Time == frame.Time &&
                        Value == frame.Value &&
                        IngoingTangent == frame.IngoingTangent &&
                        OutgoingTangent == frame.OutgoingTangent;
            }

            public override int GetHashCode()
            {
                var hashCode = 2107829771;
                hashCode = hashCode * -1521134295 + Time.GetHashCode();
                hashCode = hashCode * -1521134295 + Value.GetHashCode();
                hashCode = hashCode * -1521134295 + IngoingTangent.GetHashCode();
                hashCode = hashCode * -1521134295 + OutgoingTangent.GetHashCode();
                return hashCode;
            }

            public static bool operator ==(J3DKeyFrame frame1, J3DKeyFrame frame2) => EqualityComparer<J3DKeyFrame>.Default.Equals(frame1, frame2);

            public static bool operator !=(J3DKeyFrame frame1, J3DKeyFrame frame2) => !(frame1 == frame2);
        }

        /// <summary>
        /// J3D Looping Modes
        /// </summary>
        public enum LoopMode : byte
        {
            /// <summary>
            /// Play Once then Stop.
            /// </summary>
            ONCE = 0x00,
            /// <summary>
            /// Play Once then Stop and reset to the first frame.
            /// </summary>
            ONCERESET = 0x01,
            /// <summary>
            /// Constantly play the animation.
            /// </summary>
            REPEAT = 0x02,
            /// <summary>
            /// Play the animation to the end. then reverse the animation and play to the start, then Stop.
            /// </summary>
            ONCEANDMIRROR = 0x03,
            /// <summary>
            /// Play the animation to the end. then reverse the animation and play to the start, repeat.
            /// </summary>
            REPEATANDMIRROR = 0x04
        }

        /// <summary>
        /// J3D Tangent Modes
        /// </summary>
        public enum TangentMode : short
        {
            /// <summary>
            /// One tangent value is stored, used for both the incoming and outgoing tangents
            /// </summary>
            SYNC = 0x00,
            /// <summary>
            /// Two tangent values are stored, the incoming and outgoing tangents, respectively
            /// </summary>
            DESYNC = 0x01
        }
    }

    public static class JUtility
    {
        #region Imaging
        public static Bitmap DecodeImage(Stream TextureFile, byte[] PaletteData, GXImageFormat Format, GXPaletteFormat? PaletteFormat, int? PaletteCount, int ImageWidth, int ImageHeight, int Mipmap)
        {
            Bitmap Result = null;
            byte[] ImageData = null;

            for (int i = 0; i < Mipmap; i++)
            {
                ImageWidth = (ushort)(ImageWidth / 2);
                ImageHeight = (ushort)(ImageHeight / 2);
            }
            int oldWidth = ImageWidth % 4 != 0 ? (ImageWidth / 4) * 4 + 4 : ImageWidth;
            int FullWidth = oldWidth % 8 != 0 ? (oldWidth / 8) * 8 + 8 : oldWidth;

            int oldHeight = ImageHeight % 4 != 0 ? (ImageHeight / 4) * 4 + 4 : ImageHeight;
            int FullHeight = oldHeight % 8 != 0 ? (oldHeight / 8) * 8 + 8 : oldHeight;
            switch (Format)
            {
                case GXImageFormat.I4:
                    #region DecodeI4
                    ImageData = TextureFile.Read(0, (FullWidth * FullHeight) / 2);
                    Result = DecodeImage(ImageData, null, Format, null, null, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.I8:
                    #region DecodeI8
                    ImageData = TextureFile.Read(0, FullWidth * FullHeight);
                    Result = DecodeImage(ImageData, null, Format, null, null, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.IA4:
                    #region DecodeIA4
                    ImageData = TextureFile.Read(0, FullWidth * FullHeight);
                    Result = DecodeImage(ImageData, null, Format, null, null, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.IA8:
                    #region DecodeIA8
                    ImageData = TextureFile.Read(0, (FullWidth * FullHeight) * 2);
                    Result = DecodeImage(ImageData, null, Format, null, null, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.RGB565:
                    #region DecodeRGB565
                    ImageData = TextureFile.Read(0, (FullWidth * FullHeight) * 2);
                    Result = DecodeImage(ImageData, null, Format, null, null, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.RGB5A3:
                    #region DecodeRGB5A3
                    ImageData = TextureFile.Read(0, (FullWidth * FullHeight) * 2);
                    Result = DecodeImage(ImageData, null, Format, null, null, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.RGBA32:
                    #region DecodeRGBA32
                    ImageData = TextureFile.Read(0, (FullWidth * FullHeight) * 4);
                    Result = DecodeImage(ImageData, null, Format, null, null, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.C4:
                    #region DecodeC4
                    ImageData = TextureFile.Read(0, (FullWidth * FullHeight) / 2);
                    Result = DecodeImage(ImageData, PaletteData, Format, PaletteFormat, PaletteCount, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.C8:
                    #region DecodeC8
                    ImageData = TextureFile.Read(0, FullWidth * FullHeight);
                    Result = DecodeImage(ImageData, PaletteData, Format, PaletteFormat, PaletteCount, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.C14X2:
                    #region DecodeC14X2
                    ImageData = TextureFile.Read(0, (FullWidth * FullHeight) * 2);
                    Result = DecodeImage(ImageData, PaletteData, Format, PaletteFormat, PaletteCount, ImageWidth, ImageHeight);
                    #endregion
                    break;
                case GXImageFormat.CMPR:
                    #region DecodeDXT1
                    int BytesNeededForEncode = FullWidth * FullHeight * 4 / 8;
                    ImageData = TextureFile.Read(0, BytesNeededForEncode);
                    Result = DecodeImage(ImageData, null, Format, null, null, ImageWidth, ImageHeight);
                    #endregion
                    break;
                default:
                    throw new Exception($"Invalid format {Format.ToString()}");
            }
            return Result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ImageData"></param>
        /// <param name="PaletteData"></param>
        /// <param name="Format"></param>
        /// <param name="PaletteFormat"></param>
        /// <param name="ColourCounts"></param>
        /// <param name="Width"></param>
        /// <param name="Height"></param>
        /// <returns></returns>
        public static Bitmap DecodeImage(byte[] ImageData, byte[] PaletteData, GXImageFormat Format, GXPaletteFormat? PaletteFormat, int? ColourCounts, int Width, int Height)
        {
            Color[] PaletteColours = null;
            if (PaletteData != null)
                PaletteColours = DecodePalette(PaletteData, PaletteFormat, ColourCounts, Format);
            int BlockWidth = FormatDetails[Format].Item1, BlockHeight = FormatDetails[Format].Item2, BlockDataSize = Format == GXImageFormat.RGBA32 ? 64 : 32,
                offset = 0, BlockX = 0, BlockY = 0, XInBlock = 0, YInBlock = 0;
            Bitmap Result = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            //Lord why can't Bitmap.SetPixel be fast? :weary:
            BitmapData BitMapData = Result.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            IntPtr BitmapPointer = BitMapData.Scan0;
            byte[] Pixels = new byte[Width * Height * 4];
            Marshal.Copy(BitmapPointer, Pixels, 0, Pixels.Length);
            while (BlockY < Height)
            {
                Color[] PixelData = DecodeBlock(ImageData, Format, offset, BlockDataSize, PaletteColours);

                for (int i = 0; i < PixelData.Length; i++)
                {
                    XInBlock = (i % BlockWidth);
                    YInBlock = i / BlockWidth;
                    int xpos = BlockX + XInBlock;
                    int ypos = BlockY + YInBlock;
                    if (xpos >= Width || ypos >= Height)
                        continue;
                    int Start = ((ypos * Width) + xpos) * 4;

                    Pixels[Start] = PixelData[i].B;
                    Pixels[Start + 1] = PixelData[i].G;
                    Pixels[Start + 2] = PixelData[i].R;
                    Pixels[Start + 3] = PixelData[i].A;
                }
                offset += BlockDataSize;
                BlockX += BlockWidth;
                if (BlockX >= Width)
                {
                    BlockX = 0;
                    BlockY += BlockHeight;
                }
            }

            Marshal.Copy(Pixels, 0, BitmapPointer, Pixels.Length);
            Result.UnlockBits(BitMapData);
            return Result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="PaletteData"></param>
        /// <param name="PaletteFormat"></param>
        /// <param name="Count"></param>
        /// <param name="Format"></param>
        /// <returns></returns>
        public static Color[] DecodePalette(byte[] PaletteData, GXPaletteFormat? PaletteFormat, int? Count, GXImageFormat Format)
        {
            List<Color> Colours = new List<Color>();
            int offset = 0;
            for (int i = 0; i < Count; i++)
            {
                ushort Raw = BitConverter.ToUInt16(new byte[2] { PaletteData[offset + 1], PaletteData[offset] }, 0);
                offset += 2;
                switch (PaletteFormat)
                {
                    case GXPaletteFormat.IA8:
                        Colours.Add(IA8ToColor(Raw));
                        break;
                    case GXPaletteFormat.RGB565:
                        Colours.Add(RGB565ToColor(Raw));
                        break;
                    case GXPaletteFormat.RGB5A3:
                        Colours.Add(RGB5A3ToColor(Raw));
                        break;
                    default:
                        throw new Exception("Bad Palette format");
                }
            }

            return Colours.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ImageData"></param>
        /// <param name="Format"></param>
        /// <param name="Offset"></param>
        /// <param name="BlockSize"></param>
        /// <param name="Colours"></param>
        /// <returns></returns>
        public static Color[] DecodeBlock(byte[] ImageData, GXImageFormat Format, int Offset, int BlockSize, Color[] Colours)
        {
            List<Color> Result = new List<Color>();
            if (Offset >= ImageData.Length)
                return Result.ToArray();

            int BlockSizeHalfed = (int)Math.Floor(BlockSize / 2.0);
            switch (Format)
            {
                case GXImageFormat.I4:
                    for (int i = 0; i < BlockSize; i++)
                        for (int nibble = 0; nibble < 2; nibble++)
                            if (Offset + i < ImageData.Length)
                                Result.Add(I4ToColor((byte)((ImageData[Offset + i] >> (1 - nibble) * 4) & 0xF)));
                    break;
                case GXImageFormat.I8:
                    for (int i = 0; i < BlockSize; i++)
                        if (Offset + i < ImageData.Length)
                            Result.Add(I8ToColor(ImageData[Offset + i]));
                    break;
                case GXImageFormat.IA4:
                    for (int i = 0; i < BlockSize; i++)
                        if (Offset + i < ImageData.Length)
                            Result.Add(IA4ToColor(ImageData[Offset + i]));
                    break;
                case GXImageFormat.IA8:
                    for (int i = 0; i < BlockSizeHalfed; i++)
                        if (Offset + i * 2 < ImageData.Length)
                            Result.Add(IA8ToColor(BitConverter.ToUInt16(new byte[2] { ImageData[(Offset + i * 2) + 1], ImageData[Offset + i * 2] }, 0)));
                    break;
                case GXImageFormat.RGB565:
                    for (int i = 0; i < BlockSizeHalfed; i++)
                        if (Offset + i * 2 < ImageData.Length)
                            Result.Add(RGB565ToColor(BitConverter.ToUInt16(new byte[2] { ImageData[(Offset + i * 2) + 1], ImageData[Offset + i * 2] }, 0)));
                    break;
                case GXImageFormat.RGB5A3:
                    for (int i = 0; i < BlockSizeHalfed; i++)
                        if (Offset + i * 2 < ImageData.Length)
                            Result.Add(RGB5A3ToColor(BitConverter.ToUInt16(new byte[2] { ImageData[(Offset + i * 2) + 1], ImageData[Offset + i * 2] }, 0)));
                    break;
                case GXImageFormat.RGBA32:
                    for (int i = 0; i < 16; i++)
                        Result.Add(Color.FromArgb(ImageData[Offset + (i * 2)], ImageData[Offset + (i * 2) + 1], ImageData[Offset + (i * 2) + 32], ImageData[Offset + (i * 2) + 33]));
                    break;
                case GXImageFormat.C4:
                    for (int i = 0; i < BlockSize; i++)
                        for (int nibble = 0; nibble < 2; nibble++)
                        {
                            int Value = (ImageData[Offset + i] >> (1 - nibble) * 4) & 0xF;
                            Result.Add(Value >= Colours.Length ? Color.Black : Colours[Value]);
                        }
                    break;
                case GXImageFormat.C8:
                    for (int i = 0; i < BlockSize; i++)
                        Result.Add(ImageData[Offset + i] >= Colours.Length ? Color.Black : Colours[ImageData[Offset + i]]);
                    break;
                case GXImageFormat.C14X2:
                    for (int i = 0; i < BlockSizeHalfed; i++)
                        Result.Add(Colours[BitConverter.ToUInt16(new byte[2] { ImageData[(Offset + i * 2) + 1], ImageData[Offset + i * 2] }, 0)]);
                    break;
                case GXImageFormat.CMPR:
                    Result.AddRange(new Color[64]);
                    int subblock_offset = Offset;
                    for (int i = 0; i < 4; i++)
                    {
                        int subblock_x = (i % 2) * 4;
                        int subblock_y = ((int)Math.Floor(i / 2.0)) * 4;

                        Colours = GetInterpolatedDXT1Colours(BitConverter.ToUInt16(new byte[2] { ImageData[subblock_offset + 1], ImageData[subblock_offset] }, 0), BitConverter.ToUInt16(new byte[2] { ImageData[subblock_offset + 3], ImageData[subblock_offset + 2] }, 0));
                        for (int j = 0; j < 16; j++)
                            Result[subblock_x + subblock_y * 8 + ((int)Math.Floor(j / 4.0)) * 8 + (j % 4)] = Colours[(((BitConverter.ToInt32(new byte[4] { ImageData[subblock_offset + 7], ImageData[subblock_offset + 6], ImageData[subblock_offset + 5], ImageData[subblock_offset + 4] }, 0)) >> ((15 - j) * 2)) & 3)];
                        subblock_offset += 8;
                    }

                    break;
                default:
                    throw new Exception("Invalid Image Format");
            }
            return Result.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="RawLeft"></param>
        /// <param name="RawRight"></param>
        /// <returns></returns>
        public static Color[] GetInterpolatedDXT1Colours(ushort RawLeft, ushort RawRight)
        {
            Color Left = RGB565ToColor(RawLeft), Right = RGB565ToColor(RawRight);
            Color InterpA, InterpB;

            if (RawLeft > RawRight)
            {
                InterpA = Color.FromArgb((int)Math.Floor((2 * Left.R + 1 * Right.R) / 3.0), (int)Math.Floor((2 * Left.G + 1 * Right.G) / 3.0), (int)Math.Floor((2 * Left.B + 1 * Right.B) / 3.0));
                InterpB = Color.FromArgb((int)Math.Floor((1 * Left.R + 2 * Right.R) / 3.0), (int)Math.Floor((1 * Left.G + 2 * Right.G) / 3.0), (int)Math.Floor((1 * Left.B + 2 * Right.B) / 3.0));
            }
            else
            {
                InterpA = Color.FromArgb((int)Math.Floor(Left.R / 2.0) + (int)Math.Floor(Right.R / 2.0), (int)Math.Floor(Left.G / 2.0) + (int)Math.Floor(Right.G / 2.0), (int)Math.Floor(Left.B / 2.0) + (int)Math.Floor(Right.B / 2.0));
                InterpB = Color.FromArgb(1, (int)Math.Floor((1 * Left.R + 2 * Right.R) / 3.0), (int)Math.Floor((1 * Left.G + 2 * Right.G) / 3.0), (int)Math.Floor((1 * Left.B + 2 * Right.B) / 3.0));
            }

            return new Color[4] { Left, Right, InterpA, InterpB };
        }
        /// <summary>
        /// Use in the context of mipmaps
        /// </summary>
        /// <param name="ImageData"></param>
        /// <param name="PaletteData"></param>
        /// <param name="Images"></param>
        /// <param name="Format"></param>
        /// <param name="PaletteFormat"></param>
        /// <param name="AlphaMode"></param>
        public static void GetImageAndPaletteData(ref List<byte> ImageData, ref List<byte> PaletteData, List<Bitmap> Images, GXImageFormat Format, GXPaletteFormat PaletteFormat)
        {
            Tuple<Dictionary<Color, int>, ushort[]> Palette = CreatePalette(Images, Format, PaletteFormat);
            PaletteData = EncodePalette(Palette.Item2, Format).ToList();
            for (int i = 0; i < Images.Count; i++)
                EncodeImage(ref ImageData, Images[i], Format, Palette.Item1);
        }
        /// <summary>
        /// Use in the context of no mipmaps
        /// </summary>
        /// <param name="ImageData"></param>
        /// <param name="PaletteData"></param>
        /// <param name="Image"></param>
        /// <param name="Format"></param>
        /// <param name="PaletteFormat"></param>
        public static void GetImageAndPaletteData(ref List<byte> ImageData, ref List<byte> PaletteData, Bitmap Image, GXImageFormat Format, GXPaletteFormat PaletteFormat)
        {
            Tuple<Dictionary<Color, int>, ushort[]> Palette = CreatePalette(Image, Format, PaletteFormat);
            PaletteData = EncodePalette(Palette.Item2, Format).ToList();
            EncodeImage(ref ImageData, Image, Format, Palette.Item1);
        }

        public static void EncodeImage(ref List<byte> ImageData, Bitmap Image, GXImageFormat Format, Dictionary<Color, int> ColourIndicies)
        {
            int block_x = 0, block_y = 0;

            while (block_y < Image.Height)
            {
                byte[] block_data = EncodeBlock(Format, Image, ColourIndicies, block_x, block_y);

                ImageData.AddRange(block_data);

                block_x += FormatDetails[Format].Item1;
                if (block_x >= Image.Width)
                {
                    block_x = 0;
                    block_y += FormatDetails[Format].Item2;
                }
            }
        }

        public static Tuple<Dictionary<Color, int>, ushort[]> CreatePalette(List<Bitmap> Images, GXImageFormat Format, GXPaletteFormat PaletteFormat)
        {
            if (!(Format == GXImageFormat.C4 || Format == GXImageFormat.C8 || Format == GXImageFormat.C14X2))
                return new Tuple<Dictionary<Color, int>, ushort[]>(null, null);

            List<byte[]> ImageData = new List<byte[]>();
            List<ushort> encoded_colors = new List<ushort>();
            Dictionary<Color, int> colors_to_color_indexes = new Dictionary<Color, int>();
            for (int i = 0; i < Images.Count; i++)
            {
                ImageData.Add(Images[i].ToByteArray());
                for (int y = 0; y < Images[i].Height; y++)
                {
                    for (int x = 0; x < Images[i].Width; x++)
                    {
                        int z = ((y * Images[i].Width) + x) * 4;
                        Color Col = Color.FromArgb(ImageData[i][z + 3], ImageData[i][z + 2], ImageData[i][z + 1], ImageData[i][z]);
                        ushort ColEncoded = EncodeColour(Col, PaletteFormat);
                        if (!encoded_colors.Contains(ColEncoded))
                            encoded_colors.Add(ColEncoded);
                        if (!colors_to_color_indexes.ContainsKey(Col))
                            colors_to_color_indexes.Add(Col, encoded_colors.IndexOf(ColEncoded));
                    }
                }
            }

            if (encoded_colors.Count > GetMaxColours(Format))
            {
                // If the image has more colors than the selected image format can support, we automatically reduce the number of colors.
                //For C4 and C8, the colors should have already been reduced by Pillow's quantize method.
                // So the maximum number of colors can only be exceeded for C14X2.

                Color[] LimitedPalette = CreateLimitedPalette(Images, GetMaxColours(Format), PaletteFormat != GXPaletteFormat.RGB565);
                encoded_colors = new List<ushort>();
                colors_to_color_indexes = new Dictionary<Color, int>();
                for (int i = 0; i < Images.Count; i++)
                    for (int y = 0; y < Images[i].Height; y++)
                    {
                        for (int x = 0; x < Images[i].Width; x++)
                        {
                            int z = ((y * Images[i].Width) + x) * 4;
                            Color Col = Color.FromArgb(ImageData[i][z + 3], ImageData[i][z + 2], ImageData[i][z + 1], ImageData[i][z]);
                            ushort ColEncoded = EncodeColour(GetNearestColour(Col, LimitedPalette), PaletteFormat);
                            if (!encoded_colors.Contains(ColEncoded))
                                encoded_colors.Add(ColEncoded);
                            if (!colors_to_color_indexes.ContainsKey(Col))
                                colors_to_color_indexes.Add(Col, encoded_colors.IndexOf(ColEncoded));
                        }
                    }
            }
            return new Tuple<Dictionary<Color, int>, ushort[]>(colors_to_color_indexes, encoded_colors.ToArray());
        }
        public static Tuple<Dictionary<Color, int>, ushort[]> CreatePalette(Bitmap Image, GXImageFormat Format, GXPaletteFormat PaletteFormat)
        {
            if (!(Format == GXImageFormat.C4 || Format == GXImageFormat.C8 || Format == GXImageFormat.C14X2))
                return new Tuple<Dictionary<Color, int>, ushort[]>(null, null);

            List<byte> ImageData = new List<byte>();
            List<ushort> encoded_colors = new List<ushort>();
            Dictionary<Color, int> colors_to_color_indexes = new Dictionary<Color, int>();

            ImageData.AddRange(Image.ToByteArray());
            for (int y = 0; y < Image.Height; y++)
            {
                for (int x = 0; x < Image.Width; x++)
                {
                    int z = ((y * Image.Width) + x) * 4;
                    Color Col = Color.FromArgb(ImageData[z + 3], ImageData[z + 2], ImageData[z + 1], ImageData[z]);
                    ushort ColEncoded = EncodeColour(Col, PaletteFormat);
                    if (!encoded_colors.Contains(ColEncoded))
                        encoded_colors.Add(ColEncoded);
                    if (!colors_to_color_indexes.ContainsKey(Col))
                        colors_to_color_indexes.Add(Col, encoded_colors.IndexOf(ColEncoded));
                }
            }

            if (encoded_colors.Count > GetMaxColours(Format))
            {
                // If the image has more colors than the selected image format can support, we automatically reduce the number of colors.
                //For C4 and C8, the colors should have already been reduced by Pillow's quantize method.
                // So the maximum number of colors can only be exceeded for C14X2.

                Color[] LimitedPalette = CreateLimitedPalette(Image, GetMaxColours(Format), PaletteFormat != GXPaletteFormat.RGB565);
                encoded_colors = new List<ushort>();
                colors_to_color_indexes = new Dictionary<Color, int>();

                for (int y = 0; y < Image.Height; y++)
                {
                    for (int x = 0; x < Image.Width; x++)
                    {
                        int z = ((y * Image.Width) + x) * 4;
                        Color Col = Color.FromArgb(ImageData[z + 3], ImageData[z + 2], ImageData[z + 1], ImageData[z]);
                        ushort ColEncoded = EncodeColour(GetNearestColour(Col, LimitedPalette), PaletteFormat);
                        if (!encoded_colors.Contains(ColEncoded))
                            encoded_colors.Add(ColEncoded);
                        if (!colors_to_color_indexes.ContainsKey(Col))
                            colors_to_color_indexes.Add(Col, encoded_colors.IndexOf(ColEncoded));
                    }
                }
            }
            return new Tuple<Dictionary<Color, int>, ushort[]>(colors_to_color_indexes, encoded_colors.ToArray());
        }

        public static byte[] EncodeBlock(GXImageFormat Format, Bitmap Image, Dictionary<Color, int> ColourIndicies, int BlockX, int BlockY)
        {
            byte[] Pixels = Image.ToByteArray();
            byte[] EncodedBlock = new byte[Format == GXImageFormat.RGBA32 ? 64 : 32];
            int Offset = 0, CurrentBlockWidth = FormatDetails[Format].Item1, CurrentBlockHeight = FormatDetails[Format].Item2;

            int PixelIndex;
            byte[] Value;
            switch (Format)
            {
                case GXImageFormat.I4:
                    #region Encode I4
                    for (int y = BlockY; y < BlockY + CurrentBlockHeight; y++)
                    {
                        for (int x = BlockX; x < BlockX + CurrentBlockWidth; x += 2)
                        {
                            int RawColL, RawColR;
                            if (x >= Image.Width || y >= Image.Height)
                                RawColL = 0xF; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + x) * 4;
                                RawColL = Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex]).ToI4();
                            }
                            if ((x + 1) >= Image.Width || y >= Image.Height)
                                RawColR = 0xF; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + (x + 1)) * 4;
                                RawColR = Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex]).ToI4();
                            }
                            EncodedBlock[Offset++] = (byte)(((RawColL & 0xF) << 4) | (RawColR & 0xF));
                        }
                    }
                    #endregion
                    break;
                case GXImageFormat.I8:
                    #region Encode I8
                    for (int y = BlockY; y < BlockY + CurrentBlockHeight; y++)
                    {
                        for (int x = BlockX; x < BlockX + CurrentBlockWidth; x++)
                        {
                            if (x >= Image.Width || y >= Image.Height)
                                EncodedBlock[Offset++] = 0xFF; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + x) * 4;
                                EncodedBlock[Offset++] = Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex]).ToI8();
                            }
                        }
                    }
                    #endregion
                    break;
                case GXImageFormat.IA4:
                    #region Encode IA4
                    for (int y = BlockY; y < BlockY + CurrentBlockHeight; y++)
                    {
                        for (int x = BlockX; x < BlockX + CurrentBlockWidth; x ++)
                        {
                            if (x >= Image.Width || y >= Image.Height)
                                EncodedBlock[Offset++] = 0xFF; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + x) * 4;
                                EncodedBlock[Offset++] = Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex]).ToIA4();
                            }
                        }
                    }
                    #endregion
                    break;
                case GXImageFormat.IA8:
                    #region Encode IA8
                    for (int y = BlockY; y < BlockY + CurrentBlockHeight; y++)
                    {
                        for (int x = BlockX; x < BlockX + CurrentBlockWidth; x++)
                        {
                            if (x >= Image.Width || y >= Image.Height)
                                Value = new byte[2] { 0xFF, 0xFF }; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + x) * 4;
                                Value = BitConverter.GetBytes(Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex]).ToIA8());
                            }
                            EncodedBlock[Offset++] = Value[1];
                            EncodedBlock[Offset++] = Value[0];
                        }
                    }
                    #endregion
                    break;
                case GXImageFormat.RGB565:
                    #region Encode RGB565
                    for (int y = BlockY; y < BlockY + CurrentBlockHeight; y++)
                    {
                        for (int x = BlockX; x < BlockX + CurrentBlockWidth; x++)
                        {
                            if (x >= Image.Width || y >= Image.Height)
                                Value = new byte[2] { 0xFF, 0xFF }; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + x) * 4;
                                Value = BitConverter.GetBytes(Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex]).ToRGB565());
                            }
                            EncodedBlock[Offset++] = Value[1];
                            EncodedBlock[Offset++] = Value[0];
                        }
                    }
                    #endregion
                    break;
                case GXImageFormat.RGB5A3:
                    #region Encode RGB5A3
                    for (int y = BlockY; y < BlockY + CurrentBlockHeight; y++)
                    {
                        for (int x = BlockX; x < BlockX + CurrentBlockWidth; x ++)
                        {
                            if (x >= Image.Width || y >= Image.Height)
                                Value = new byte[2] { 0xFF, 0xFF }; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + x) * 4;
                                Value = BitConverter.GetBytes(Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex]).ToRGB5A3());
                            }
                            EncodedBlock[Offset++] = Value[1];
                            EncodedBlock[Offset++] = Value[0];
                        }
                    }
                    #endregion
                    break;
                case GXImageFormat.RGBA32:
                    #region Encode RGBA32
                    for (int i = 0; i < 16; i++)
                    {
                        int x = BlockX + (i % CurrentBlockWidth), 
                            y = BlockY + ((int)Math.Floor((decimal)i / CurrentBlockWidth));

                        PixelIndex = ((y * Image.Width) + x) * 4;
                        if (x >= Image.Width || y > Image.Height || PixelIndex >= Pixels.Length)
                            Value = new byte[4] { 0xFF, 0xFF, 0xFF, 0xFF }; //If you've been reading this whole thing you'd know what this is for
                        else
                        {
                            Value = new byte[4] { Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex] };
                        }
                        EncodedBlock[i * 2] = Value[0];
                        EncodedBlock[(i * 2) + 01] = Value[1];
                        EncodedBlock[(i * 2) + 32] = Value[2];
                        EncodedBlock[(i * 2) + 33] = Value[3];
                    }
                    #endregion
                    break;
                case GXImageFormat.C4:
                    #region Encode C4
                    for (int y = BlockY; y < BlockY + CurrentBlockHeight; y++)
                    {
                        for (int x = BlockX; x < BlockX + CurrentBlockWidth; x += 2)
                        {
                            int ColIndexL, ColIndexR;
                            if (x >= Image.Width || y >= Image.Height)
                                ColIndexL = 0xF; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + x) * 4;
                                ColIndexL = ColourIndicies[Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex])];
                            }
                            if ((x + 1) >= Image.Width || y >= Image.Height)
                                ColIndexR = 0xF; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + (x + 1)) * 4;
                                ColIndexR = ColourIndicies[Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex])];
                            }
                            EncodedBlock[Offset++] = (byte)(((ColIndexL & 0xF) << 4) | (ColIndexR & 0xF));
                        }
                    }
                    #endregion
                    break;
                case GXImageFormat.C8:
                    #region Encode C8
                    for (int y = BlockY; y < BlockY + CurrentBlockHeight; y++)
                    {
                        for (int x = BlockX; x < BlockX + CurrentBlockWidth; x++)
                        {
                            if (x >= Image.Width || y >= Image.Height)
                                EncodedBlock[Offset++] = 0xFF; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + x) * 4;
                                EncodedBlock[Offset++] = (byte)ColourIndicies[Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex])];
                            }
                        }
                    }
                    #endregion
                    break;
                case GXImageFormat.C14X2:
                    #region Encode C14X2
                    for (int y = BlockY; y < BlockY + CurrentBlockHeight; y++)
                    {
                        for (int x = BlockX; x < BlockX + CurrentBlockWidth; x++)
                        {
                            if (x >= Image.Width || y >= Image.Height)
                                Value = new byte[2] { 0xFF, 0x3F }; //Block Bleeds past image width
                            else
                            {
                                PixelIndex = ((y * Image.Width) + x) * 4;
                                Value = BitConverter.GetBytes(ColourIndicies[Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex])]);
                            }
                            EncodedBlock[Offset++] = Value[1];
                            EncodedBlock[Offset++] = Value[0];
                        }
                    }
                    #endregion
                    break;
                case GXImageFormat.CMPR:
                    #region Encode CMPR
                    for (int SubBlock = 0; SubBlock < 4; SubBlock++)
                    {
                        int subblock_x = BlockX + (SubBlock % 2) * 4, subblock_y = BlockY + (int)Math.Floor(SubBlock / 2.0) * 4;
                        List<Color> AllSubBlockColours = new List<Color>();
                        bool NeedsAlphaColor = false;
                        for (int i = 0; i < 16; i++)
                        {
                            int x = subblock_x + (i % 4), y = subblock_y + (int)Math.Floor(i / 4.0);
                            if (x >= Image.Width || y >= Image.Height)
                                continue;

                            PixelIndex = ((y * Image.Width) + x) * 4;
                            Color Col = Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex]);
                            if (/*AlphaMode != JUTTransparency.SPECIAL &&*/ Col.A < 16)
                                NeedsAlphaColor = true;
                            else
                                AllSubBlockColours.Add(Col);
                        }
                        Tuple<Color, Color> KeyCols = GetBestCMPRKeyColours(AllSubBlockColours);
                        ushort RawColor1 = KeyCols.Item1.ToRGB565(), RawColor2 = KeyCols.Item2.ToRGB565();
                        if ((NeedsAlphaColor && RawColor1 > RawColor2) || (!NeedsAlphaColor && RawColor1 < RawColor2))
                        {
                            SwapValues(ref RawColor1, ref RawColor2);
                            SwapValues(ref KeyCols);
                        }
                        Color[] CMPRColours = GetInterpolatedCMPRColours(RawColor1, RawColor2);
                        CMPRColours[0] = KeyCols.Item1;
                        CMPRColours[1] = KeyCols.Item2;
                        Value = BitConverter.GetBytes(RawColor1);
                        EncodedBlock[Offset++] = Value[1];
                        EncodedBlock[Offset++] = Value[0];
                        Value = BitConverter.GetBytes(RawColor2);
                        EncodedBlock[Offset++] = Value[1];
                        EncodedBlock[Offset++] = Value[0];

                        int ColorIDs = 0;
                        for (int i = 0; i < 16; i++)
                        {
                            int x = subblock_x + (i % 4), y = subblock_y + (int)Math.Floor(i / 4.0);
                            if (x >= Image.Width || y >= Image.Height)
                                continue;

                            PixelIndex = ((y * Image.Width) + x) * 4;
                            Color Col = Color.FromArgb(Pixels[PixelIndex + 3], Pixels[PixelIndex + 2], Pixels[PixelIndex + 1], Pixels[PixelIndex]);
                            ColorIDs |= Array.IndexOf(CMPRColours, CMPRColours.Contains(Col) ? Col : GetNearestColour(Col, CMPRColours)) << ((15 - i) * 2);
                        }
                        Value = BitConverter.GetBytes(ColorIDs);
                        EncodedBlock[Offset++] = Value[3];
                        EncodedBlock[Offset++] = Value[2];
                        EncodedBlock[Offset++] = Value[1];
                        EncodedBlock[Offset++] = Value[0];
                    }
                    #endregion
                    break;
                default:
                    throw new Exception("Invalid Image Format");
            }

            return EncodedBlock;
        }

        public static byte[] EncodePalette(ushort[] RawColours, GXImageFormat Format)
        {
            if (!IsPaletteFormat(Format))
                return new byte[0];

            byte[] PaletteData = new byte[RawColours.Length * 2];
            int Offset = 0;
            for (int i = 0; i < RawColours.Length; i++)
            {
                byte[] temp = BitConverter.GetBytes(RawColours[i]);
                PaletteData[Offset++] = temp[1];
                PaletteData[Offset++] = temp[0];
            }
            return PaletteData;
        }

        public static ushort EncodeColour(Color Col, GXPaletteFormat PaletteFormats)
        {
            if (PaletteFormats == GXPaletteFormat.IA8)
                return Col.ToIA8();
            else if (PaletteFormats == GXPaletteFormat.RGB565)
                return Col.ToRGB565();
            else if (PaletteFormats == GXPaletteFormat.RGB5A3)
                return Col.ToRGB5A3();
            else
                throw new Exception("Invalid Palette Format");
        }

        public static Color[] CreateLimitedPalette(List<Bitmap> Images, int MaxColours, bool Alpha = true)
        {
            List<byte[]> ImageData = new List<byte[]>();
            for (int i = 0; i < Images.Count; i++)
                ImageData.Add(Images[i].ToByteArray());

            int depth;
            if (MaxColours == 16)
                depth = 4;
            else if (MaxColours == 256)
                depth = 8;
            else if (MaxColours == 16384)
                depth = 14;
            else
                throw new Exception($"Unsupported maximum number of colors to generate a palette for: {MaxColours}");

            List<Color> all_pixel_colors = new List<Color>();
            bool already_have_zero_alpha_color = false;
            for (int i = 0; i < Images.Count; i++)
                for (int y = 0; y < Images[i].Height; y++)
                {
                    for (int x = 0; x < Images[i].Width; x++)
                    {
                        int z = ((y * Images[i].Width) + x) * 4;
                        Color Col = Color.FromArgb(ImageData[i][z + 3], ImageData[i][z + 2], ImageData[i][z + 1], ImageData[i][z]);
                        if (!Alpha)
                            Col = Color.FromArgb(Col.R, Col.G, Col.B);
                        else if (Col.A == 0)
                        {
                            if (already_have_zero_alpha_color)
                                continue;
                            already_have_zero_alpha_color = true;
                        }
                        all_pixel_colors.Add(Col);
                    }
                }
            return SplitToBuckets(all_pixel_colors, depth);
        }

        public static Color[] CreateLimitedPalette(Bitmap Image, int MaxColours, bool Alpha = true)
        {
            List<byte> ImageData = new List<byte>();
                ImageData.AddRange(Image.ToByteArray());

            int depth;
            if (MaxColours == 16)
                depth = 4;
            else if (MaxColours == 256)
                depth = 8;
            else if (MaxColours == 16384)
                depth = 14;
            else
                throw new Exception($"Unsupported maximum number of colors to generate a palette for: {MaxColours}");

            List<Color> all_pixel_colors = new List<Color>();
            bool already_have_zero_alpha_color = false;
                for (int y = 0; y < Image.Height; y++)
                {
                    for (int x = 0; x < Image.Width; x++)
                    {
                        int z = ((y * Image.Width) + x) * 4;
                        Color Col = Color.FromArgb(ImageData[z + 3], ImageData[z + 2], ImageData[z + 1], ImageData[z]);
                        if (!Alpha)
                            Col = Color.FromArgb(Col.R, Col.G, Col.B);
                        else if (Col.A == 0)
                        {
                            if (already_have_zero_alpha_color)
                                continue;
                            already_have_zero_alpha_color = true;
                        }
                        all_pixel_colors.Add(Col);
                    }
                }
            return SplitToBuckets(all_pixel_colors, depth);
        }

        private static Color[] SplitToBuckets(List<Color> AllColours, int Depth)
        {
            if (Depth == 0)
                return new Color[1] { AverageColours(AllColours) };

            int RedRange = AllColours.Max(C => C.R) - AllColours.Min(C => C.R);
            int GreenRange = AllColours.Max(C => C.G) - AllColours.Min(C => C.G);
            int BlueRange = AllColours.Max(C => C.B) - AllColours.Min(C => C.B);

            int channel_index_with_highest_range = 0;
            if (GreenRange >= RedRange && GreenRange >= BlueRange)
                channel_index_with_highest_range = 1;
            else if (RedRange >= GreenRange && RedRange >= BlueRange)
                channel_index_with_highest_range = 0;
            else if (BlueRange >= RedRange && BlueRange >= GreenRange)
                channel_index_with_highest_range = 2;

            AllColours = AllColours.OrderBy(C => C.A).ToList();
            AllColours = AllColours.OrderBy(C => channel_index_with_highest_range == 1 ? C.G : (channel_index_with_highest_range == 0 ? C.R : C.B)).ToList();
            List<Color> Palette = new List<Color>();
            int median = (int)Math.Floor(AllColours.Count / 2.0);
            Palette.AddRange(SplitToBuckets(AllColours.GetRange(median, AllColours.Count - median), Depth - 1));
            Palette.AddRange(SplitToBuckets(AllColours.GetRange(0, median), Depth - 1));
            return Palette.ToArray();
        }

        public static Color AverageColours(List<Color> Colours)
        {
            Color transparent_color = Colours.FirstOrDefault(O => O.A == 0);
            if (transparent_color == null)
                // Need to ensure a fully transparent color exists in the final palette if one existed originally.
                return transparent_color;

            int RedSum = 0, GreenSum = 0, BlueSum = 0, AlphaSum = 0;
            for (int i = 0; i < Colours.Count; i++)
            {
                RedSum += Colours[i].R;
                GreenSum += Colours[i].G;
                BlueSum += Colours[i].B;
                AlphaSum += Colours[i].A;
            }
            return Color.FromArgb((int)Math.Floor(AlphaSum / (double)Colours.Count), (int)Math.Floor(RedSum / (double)Colours.Count), (int)Math.Floor(GreenSum / (double)Colours.Count), (int)Math.Floor(BlueSum / (double)Colours.Count));
        }

        private static Color GetNearestColour(Color Col, Color[] Palette)
        {
            if (Palette.Contains(Col))
                return Col;

            if (Col.A < 16)
                for (int i = 0; i < Palette.Length; i++)
                    if (Palette[i].A == 0)
                        return Palette[i];

            int min_dist = 0x7FFFFFFF;
            Color best_color = Palette[0];

            for (int i = 0; i < Palette.Length; i++)
            {
                int currentdistance = GetColorDistance(Col, Palette[i]);
                if (currentdistance < min_dist)
                {
                    if (currentdistance == 0)
                        return Palette[i];

                    min_dist = currentdistance;
                    best_color = Palette[i];
                }
            }

            return best_color;
        }

        private static int GetColorDistance(Color Col1, Color Col2)
        {
            int r_diff = Col1.R - Col2.R;
            int g_diff = Col1.G - Col2.G;
            int b_diff = Col1.B - Col2.B;
            int a_diff = Col1.A - Col2.A;
            double rgb_dist_sqr = (r_diff * r_diff + g_diff * g_diff + b_diff * b_diff) / 3.0;
            return (int)(a_diff * a_diff / 2.0 + rgb_dist_sqr * Col1.A * Col2.A / (255 * 255));

            //Claimed to be faster, but when benchmarked, it only was faster after the 7th 0 in the loop. (aka being run 10000000 times)
            //The other method gives better quality but I'm leaving this here just in case things change.
            //int Dist = Math.Abs(Col1.R - Col2.R);
            //Dist += Math.Abs(Col1.G - Col2.G);
            //Dist += Math.Abs(Col1.B - Col2.B);
            //Dist += Math.Abs(Col1.A - Col2.A);
            //return Dist;
        }

        private static int GetColorDistanceNoAlpha(Color Col1, Color Col2) => Math.Abs(Col1.R - Col2.R) + Math.Abs(Col1.G - Col2.G) + Math.Abs(Col1.B - Col2.B);

        private static Tuple<Color, Color> GetBestCMPRKeyColours(List<Color> AllColours)
        {
            int MaxDistance = -1;
            Color Col1 = Color.Black, Col2 = Color.White;
            for (int i = 0; i < AllColours.Count; i++)
            {
                for (int j = i + 1; j < AllColours.Count; j++)
                {
                    int curr_dist = GetColorDistance(AllColours[i], AllColours[j]);

                    if (curr_dist > MaxDistance)
                    {
                        MaxDistance = curr_dist;
                        Col1 = Color.FromArgb(AllColours[i].R, AllColours[i].G, AllColours[i].B);
                        Col2 = Color.FromArgb(AllColours[j].R, AllColours[j].G, AllColours[j].B);
                    }
                }
            }
            if (MaxDistance == -1)
            {
                Col1 = Color.FromArgb(0, 0, 0);
                Col2 = Color.FromArgb(255, 255, 255);
            }
            else
            {
                if ((Col1.R >> 3) == (Col2.R >> 3) && (Col1.G >> 2) == (Col2.G >> 2) && (Col1.B >> 3) == (Col2.B >> 3))
                    Col2 = ((Col1.R >> 3) == 0 && (Col1.G >> 2) == 0 && (Col1.B >> 3) == 0) ? Color.FromArgb(255, 255, 255) : Color.FromArgb(0, 0, 0);
            }
            return new Tuple<Color, Color>(Col1, Col2);
        }

        private static Color[] GetInterpolatedCMPRColours(ushort RawColour1, ushort RawColour2)
        {
            Color Col1 = RGB565ToColor(RawColour1), Col2 = RGB565ToColor(RawColour2), Col3, Col4;
            if (RawColour1 > RawColour2)
            {
                Col3 = Color.FromArgb((int)Math.Floor((2 * Col1.R + 1 * Col2.R) / 3.0), (int)Math.Floor((2 * Col1.G + 1 * Col2.G) / 3.0), (int)Math.Floor((2 * Col1.B + 1 * Col2.B) / 3.0));
                Col4 = Color.FromArgb((int)Math.Floor((1 * Col1.R + 2 * Col2.R) / 3.0), (int)Math.Floor((1 * Col1.G + 2 * Col2.G) / 3.0), (int)Math.Floor((1 * Col1.B + 2 * Col2.B) / 3.0));
            }
            else
            {
                Col3 = Color.FromArgb((int)Math.Floor(Col1.R / 2.0) + (int)Math.Floor(Col2.R / 2.0), (int)Math.Floor(Col1.G / 2.0) + (int)Math.Floor(Col2.G / 2.0), (int)Math.Floor(Col1.B / 2.0) + (int)Math.Floor(Col2.B / 2.0));
                Col4 = Color.FromArgb(0, 0, 0, 0);
            }
            return new Color[4] { Col1, Col2, Col3, Col4 };
        }

        #region Colour Converters
        public static Color I4ToColor(byte Raw)
        {
            int val = (Raw << 4) | (Raw);
            return Color.FromArgb(val, val, val, val);
        }

        public static byte ToI4(this Color Col) => (byte)((((int)Math.Round(((Col.R * 30) + (Col.G * 59) + (Col.B * 11)) / 100.0)) >> 4) & 0xF);

        public static Color I8ToColor(byte Raw) => Color.FromArgb(Raw, Raw, Raw, Raw);

        public static byte ToI8(this Color Col) => (byte)((int)Math.Round(((Col.R * 30) + (Col.G * 59) + (Col.B * 11)) / 100.0) & 0xFF);

        public static Color IA4ToColor(byte Raw)
        {
            int low_nibble = ((Raw & 0xF) << 4) | Raw & 0xF;
            return Color.FromArgb((((Raw >> 4) & 0xF) << 4) | ((Raw >> 4) & 0xF), low_nibble, low_nibble, low_nibble);
        }

        public static byte ToIA4(this Color Col)
        {
            int Value = (int)Math.Round(((Col.R * 30) + (Col.G * 59) + (Col.B * 11)) / 100.0);
            int Result = 0x00;
            Result |= ((Value >> 4) & 0xF);
            Result |= ((Col.A << 4) & 0xF0);
            return (byte)Result;
        }

        public static Color IA8ToColor(ushort Raw)
        {
            int low_byte = Raw & 0xFF;
            return Color.FromArgb((Raw >> 8) & 0xFF, low_byte, low_byte, low_byte);
        }

        public static ushort ToIA8(this Color Col)
        {
            int Value = (int)Math.Round(((Col.R * 30) + (Col.G * 59) + (Col.B * 11)) / 100.0);
            int Result = 0x0000;
            Result |= Value & 0x00FF;
            Result |= (Col.A << 8) & 0xFF00;
            return (ushort)Result;
        }

        public static Color RGB565ToColor(ushort Raw)
        {
            int Red, Green, Blue, Value;
            Value = ((Raw >> 11) & 0x1F);
            Red = (Value << 3) | (Value >> 2);
            Value = ((Raw >> 5) & 0x3F);
            Green = (Value << 2) | (Value >> 4);
            Value = ((Raw >> 0) & 0x1F);
            Blue = (Value << 3) | (Value >> 2);
            return Color.FromArgb(Red, Green, Blue);
        }

        public static ushort ToRGB565(this Color Col)
        {
            int Result = 0x0000;
            Result |= ((Col.R >> 3) & 0x1F) << 11;
            Result |= ((Col.G >> 2) & 0x3F) << 5;
            Result |= (Col.B >> 3) & 0x1F;
            return (ushort)Result;
        }

        public static Color RGB5A3ToColor(ushort Raw)
        {
            int Red, Green, Blue, Value;
            if ((Raw & 0x8000) == 0)
            {
                Value = ((Raw >> 8) & 0xF);
                Red = (Value << 4) | (Value >> 0);
                Value = ((Raw >> 4) & 0xF);
                Green = (Value << 4) | (Value >> 0);
                Value = ((Raw >> 0) & 0xF);
                Blue = (Value << 4) | (Value >> 0);
                Value = ((Raw >> 12) & 0x7);
                int Alpha = (Value << 5) | (Value << 2) | (Value >> 1);
                return Color.FromArgb(Alpha, Red, Green, Blue);
            }
            else
            {
                Value = ((Raw >> 10) & 0x1F);
                Red = (Value << 3) | (Value >> 2);
                Value = ((Raw >> 5) & 0x1F);
                Green = (Value << 3) | (Value >> 2);
                Value = ((Raw >> 0) & 0x1F);
                Blue = (Value << 3) | (Value >> 2);
                return Color.FromArgb(Red, Green, Blue);
            }
        }

        public static ushort ToRGB5A3(this Color Col)
        {
            int Result;
            if (Col.A != 255)
            {
                Result = 0x0000;
                Result |= (((Col.A >> 5) & 0x7) << 12);
                Result |= (((Col.R >> 4) & 0xF) << 8);
                Result |= (((Col.G >> 4) & 0xF) << 4);
                Result |= (((Col.B >> 4) & 0xF) << 0);
            }
            else
            {
                Result = 0x8000;
                Result |= (((Col.R >> 3) & 0x1F) << 10);
                Result |= (((Col.G >> 3) & 0x1F) << 5);
                Result |= (((Col.B >> 3) & 0x1F) << 0);
            }
            return (ushort)Result;
        }
        #endregion

        public static byte[] ToByteArray(this Bitmap bitmap)
        {
            BitmapData bmpdata = null;
            try
            {
                bmpdata = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int numbytes = bmpdata.Stride * bitmap.Height;
                byte[] bytedata = new byte[numbytes];
                IntPtr ptr = bmpdata.Scan0;

                Marshal.Copy(ptr, bytedata, 0, numbytes);

                return bytedata;
            }
            finally
            {
                if (bmpdata != null)
                    bitmap.UnlockBits(bmpdata);
            }

        }

        public static bool Compare(Bitmap bmp1, Bitmap bmp2)
        {
            if (bmp1 == null || bmp2 == null)
                return false;
            if (object.Equals(bmp1, bmp2))
                return true;
            if (!bmp1.Size.Equals(bmp2.Size) || !bmp1.PixelFormat.Equals(bmp2.PixelFormat))
                return false;

            int bytes = bmp1.Width * bmp1.Height * (Image.GetPixelFormatSize(bmp1.PixelFormat) / 8);

            bool result = true;
            byte[] b1bytes = new byte[bytes];
            byte[] b2bytes = new byte[bytes];

            BitmapData bitmapData1 = bmp1.LockBits(new Rectangle(0, 0, bmp1.Width, bmp1.Height), ImageLockMode.ReadOnly, bmp1.PixelFormat);
            BitmapData bitmapData2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width, bmp2.Height), ImageLockMode.ReadOnly, bmp2.PixelFormat);

            Marshal.Copy(bitmapData1.Scan0, b1bytes, 0, bytes);
            Marshal.Copy(bitmapData2.Scan0, b2bytes, 0, bytes);

            for (int n = 0; n <= bytes - 1; n++)
            {
                if (b1bytes[n] != b2bytes[n])
                {
                    result = false;
                    break;
                }
            }

            bmp1.UnlockBits(bitmapData1);
            bmp2.UnlockBits(bitmapData2);

            return result;
        }

        /// <summary>
        /// Value.Item1 has the BlockWidth, Value.Item2 has the BlockHeight
        /// </summary>
        public static readonly Dictionary<GXImageFormat, Tuple<int, int>> FormatDetails = new Dictionary<GXImageFormat, Tuple<int, int>>()
        {
            { GXImageFormat.I4, new Tuple<int, int>(8, 8) },
            { GXImageFormat.I8, new Tuple<int, int>(8, 4) },
            { GXImageFormat.IA4, new Tuple<int, int>(8, 4) },
            { GXImageFormat.IA8, new Tuple<int, int>(4, 4) },
            { GXImageFormat.RGB565, new Tuple<int, int>(4, 4)},
            { GXImageFormat.RGB5A3, new Tuple<int, int>(4, 4)},
            { GXImageFormat.RGBA32, new Tuple<int, int>(4, 4)},
            { GXImageFormat.C4, new Tuple<int, int>(8, 8) },
            { GXImageFormat.C8, new Tuple<int, int>(8, 4) },
            { GXImageFormat.C14X2, new Tuple<int, int>(4, 4) },
            { GXImageFormat.CMPR, new Tuple<int, int>(8, 8) }
        };

        public static int GetMaxColours(this GXImageFormat Format)
        {
            switch (Format)
            {
                case GXImageFormat.C4:
                    return 1 << 4;
                case GXImageFormat.C8:
                    return 1 << 8;
                case GXImageFormat.C14X2:
                    return 1 << 14;
                default:
                    throw new Exception("Not a Palette format!");
            }
        }

        public static bool IsPaletteFormat(this GXImageFormat Format) => Format == GXImageFormat.C4 || Format == GXImageFormat.C8 || Format == GXImageFormat.C14X2;

        /// <summary>
        /// Enum of Image formats that BTI/TPL supports
        /// </summary>
        public enum GXImageFormat : byte
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
            RGBA32 = 0x06,
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

        public enum GXPaletteFormat : byte
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

        public enum GXWrapMode : short
        {
            CLAMP = 0x00,
            REPEAT = 0x01,
            MIRRORREAPEAT = 0x02
        }

        public enum GXFilterMode : byte
        {
            Nearest = 0x00,
            Linear = 0x01,
            NearestMipmapNearest = 0x02,
            NearestMipmapLinear = 0x03,
            LinearMipmapNearest = 0x04,
            LinearMipmapLinear = 0x05
        }

        public enum JUTTransparency : byte
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
        #endregion
    }

    public static class NameTableIO
    {
        public static List<string> ReadStringTable(this Stream reader, int offset)
        {
            List<string> names = new List<string>();

            reader.Position = offset;

            short stringCount = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
            reader.Position += 0x02;

            for (int i = 0; i < stringCount; i++)
            {
                reader.Position += 0x02;
                short nameOffset = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                long saveReaderPos = reader.Position;
                reader.Position = offset + nameOffset;

                names.Add(reader.ReadString());

                reader.Position = saveReaderPos;
            }

            return names;
        }

        public static void WriteStringTable(this Stream writer, List<string> names)
        {
            long start = writer.Position;

            writer.WriteReverse(BitConverter.GetBytes((short)names.Count), 0, 2);
            writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

            foreach (string st in names)
            {
                writer.WriteReverse(BitConverter.GetBytes(HashString(st)), 0, 2);
                writer.Write(new byte[2], 0, 2);
            }

            long curOffset = writer.Position;
            for (int i = 0; i < names.Count; i++)
            {
                writer.Seek((int)(start + (6 + i * 4)), SeekOrigin.Begin);
                writer.WriteReverse(BitConverter.GetBytes((short)(curOffset - start)), 0, 2);
                writer.Seek((int)curOffset, SeekOrigin.Begin);

                writer.WriteString(names[i], 0x00);

                curOffset = writer.Position;
            }
        }

        private static ushort HashString(string str)
        {
            ushort hash = 0;

            foreach (char c in str)
            {
                hash *= 3;
                hash += (ushort)c;
            }

            return hash;
        }
    }
}
