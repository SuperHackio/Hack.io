using Hack.io.Utility;
using System.Drawing;

namespace Hack.io.GX;

public static partial class Utility
{
    public static int CalculateTextureDataSize(GXTextureFormat Format, int Width, int Height, int Count)
    {
        int value = 0;
        for (int i = 0; i < Count; i++)
            value += CalculateTextureDataSize(Format, Width >> i, Height >> i);
        return value;
    }
    public static int CalculateTextureDataSize(GXTextureFormat Format, int Width, int Height)
    {
        int tileCols, tileRows, size;
        switch (Format)
        {
            //4
            case GXTextureFormat.I4:
            case GXTextureFormat.C4:
                tileCols = ((Width + 7) >> 3);
                tileRows = ((Height + 7) >> 3);
                size = tileCols * tileRows * 32;
                return size;

            //8
            case GXTextureFormat.I8:
            case GXTextureFormat.IA4:
            case GXTextureFormat.C8:
                tileCols = ((Width + 7) >> 3);
                tileRows = ((Height + 3) >> 2);
                size = tileCols * tileRows * 32;
                return size;

            //16
            case GXTextureFormat.IA8:
            case GXTextureFormat.RGB565:
            case GXTextureFormat.RGB5A3:
            case GXTextureFormat.C14X2:
                tileCols = ((Width + 3) >> 2);
                tileRows = ((Height + 3) >> 2);
                size = tileCols * tileRows * 32;
                return size;

            //32
            case GXTextureFormat.RGBA8:
                tileCols = ((Width + 3) >> 2);
                tileRows = ((Height + 3) >> 2);
                size = tileCols * tileRows * 64;
                return size;

            //bruh
            case GXTextureFormat.CMPR:
                //Images must get padded manually instead.
                tileCols = ((Width + 7) >> 3);
                tileRows = ((Height + 7) >> 3);

                size = (tileRows * tileCols * 32);
                return size;

            default:
                throw new Exception($"Unknown Image format {Format}");
        }
    }

    public static ReadOnlySpan<byte> GetSingleMipmapData(ReadOnlySpan<byte> Source, int Width, int Height, int Index, int Stride = 4)
    {
        int size = CalculateDataSizeForSingleMipmap(Width, Height, Index, Stride);
        int start = CalculateDataSizeForMultiMipmap(Width, Height, Index - 1, Stride);
        if (size == 0)
            return null;
        if (size > Source.Length)
            throw new IndexOutOfRangeException($"{Width >> Index} * {Height >> Index} = {size}. This is larger than {nameof(Source.Length)}");
        if (start + size > Source.Length)
            throw new IndexOutOfRangeException($"{Width >> Index} * {Height >> Index} = {size}. Starting at {start}, this is larger than {nameof(Source.Length)}");

        return Source.Slice(start, size);
    }

    public static int CalculateDataSizeForSingleMipmap(int Width, int Height, int Index, int Stride = 4) => (Width >> Index) * (Height >> Index) * Stride;
    public static int CalculateDataSizeForMultiMipmap(int Width, int Height, int Count, int Stride = 4)
    {
        int value = 0;
        for (int i = 0; i < Count; i++)
            value += CalculateDataSizeForSingleMipmap(Width, Height, i, Stride);
        return value;
    }

    public static void ExceptionOnWrongFormat(GXTextureFormat format, GXTexture tex)
    {
        if (tex.TextureFormat != format)
            throw new Exception($"Texture format {format} does not match {nameof(tex.TextureFormat)}");
    }

    static (byte R, byte G, byte B, byte A)
        GetPixel(byte[] Source, int Width, int Height, int X, int Y, int Offset) => ReadPixel(Source, (((Y * Width) + X) * 4) + Offset);
    static (byte R, byte G, byte B, byte A)
        ReadPixel(byte[] Source, int Position) => (Source[Position + 3], Source[Position + 2], Source[Position + 1], Source[Position]);
}

public static partial class Utility
{
    // Encoding

    public static byte[] Encode_RGBA_to_I4((byte[] Source, int Width, int Height, int Count) SourceData)
    {
        byte[] Data = new byte[CalculateTextureDataSize(GXTextureFormat.I4, SourceData.Width, SourceData.Height, SourceData.Count)];
        int DestPtr = 0;
        int SourceOffset = 0;

        for (int i = 0; i < SourceData.Count; i++)
        {
            int width = SourceData.Width >> i,
                height = SourceData.Height >> i;
            //Number of complete blocks
            int numTileCols = ((width + 7) >> 3);
            int numTileRows = ((height + 7) >> 3);

            //Includes partial blocks
            for (int tileRow = 0; tileRow < numTileRows; tileRow++)
            {
                for (int tileCol = 0; tileCol < numTileCols; tileCol++)
                {
                    Pack(tileCol * 8, tileRow * 8, width, height, SourceOffset);
                    DestPtr += 32; //Next block
                                   //Console.Write($"\rI4: {MathUtil.GetPercentOf(DestPtr / 32, numTileRows * numTileCols)}%\t\t\t\t\t");
                }
            }

            SourceOffset += CalculateDataSizeForSingleMipmap(SourceData.Width, SourceData.Height, i);
        }

        //Console.WriteLine();
        return Data;

        void Pack(int x, int y, int w, int h, int Offset) //Use Data from above
        {
            //Clever way to catch images smaller than complete blocks
            int realRows = w - y;
            int realCols = h - x;

            if (realRows > 8)
                realRows = 8;

            if (realCols > 8)
                realCols = 8;

            for (int row = 0; row < realRows; row++)
            {
                int tilePtr = DestPtr + (row * 4);

                for (int col = 0; col < realCols; col++)
                {
                    (byte R, byte G, byte B, byte A) ActiveCol = GetPixel(SourceData.Source, w, h, x + col, y + row, Offset);
                    byte CurrentCol = (byte)((ActiveCol.R + ActiveCol.G + ActiveCol.B) / 3);

                    if (col % 2 == 0)
                        Data[tilePtr] = (byte)(CurrentCol & 0x00F0);
                    else
                        Data[tilePtr++] |= (byte)((CurrentCol & 0x00F0) >> 4);
                }
            }
        }
    }

    // Decoding

    public static byte[] Decode_I4_to_RGBA(GXTexture Source)
    {
        ExceptionOnWrongFormat(GXTextureFormat.I4, Source);

        byte[] Pixels = new byte[CalculateDataSizeForMultiMipmap(Source.Width, Source.Height, Source.TextureCount)];
        var SourceTex = Source.TextureData;
        int SourceIndex = 0;

        for (int i = 0; i < Source.TextureCount; i++)
        {
            int Width = Source.Width >> i;
            int Height = Source.Height >> i;

            int numBlocksW = Width / 8;
            int numBlocksH = Height / 8;

            for (int blockY = 0; blockY < numBlocksH; blockY++)
            {
                for (int blockX = 0; blockX < numBlocksW; blockX++)
                {
                    // Iterate the pixels in the current block
                    for (int pixelY = 0; pixelY < 8; pixelY++)
                    {
                        for (int pixelX = 0; pixelX < 8; pixelX += 2)
                        {
                            // Bounds check to ensure the pixel is within the image.
                            if ((blockX * 8 + pixelX >= Width) || (blockY * 8 + pixelY >= Height))
                                continue;

                            byte data = SourceTex[SourceIndex++];

                            // Each byte represents two pixels.
                            byte pixel0 = (byte)((data & 0xF0) >> 4);
                            byte pixel1 = (byte)(data & 0x0F);

                            int PixelsIndex = (Width * ((blockY * 8) + pixelY) + (blockX * 8) + pixelX) * 4;

                            Pixels[PixelsIndex] = (byte)(pixel0 * 0x11);
                            Pixels[PixelsIndex + 1] = (byte)(pixel0 * 0x11);
                            Pixels[PixelsIndex + 2] = (byte)(pixel0 * 0x11);
                            Pixels[PixelsIndex + 3] = (byte)(pixel0 * 0x11);

                            Pixels[PixelsIndex + 4] = (byte)(pixel1 * 0x11);
                            Pixels[PixelsIndex + 5] = (byte)(pixel1 * 0x11);
                            Pixels[PixelsIndex + 6] = (byte)(pixel1 * 0x11);
                            Pixels[PixelsIndex + 7] = (byte)(pixel1 * 0x11);
                        }
                    }
                }
            }
        }

        return Pixels;
    }
}

sealed partial class DocGen
{

}