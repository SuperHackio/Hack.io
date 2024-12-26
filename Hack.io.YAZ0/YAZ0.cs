using Hack.io.Interface;
using Hack.io.Utility;
using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Hack.io.YAZ0;

/// <summary>
/// Class containing methods to compress and decompress Data into Yaz0
/// </summary>
public static class YAZ0
{
    /// <inheritdoc cref="DocGen.DOC_MAGIC"/>
    public const string MAGIC = "Yaz0";

    /// <summary>
    /// Checks the data for Yaz0 Encoding
    /// </summary>
    /// <param name="Data">The stream of data to check</param>
    /// <returns>TRUE if the stream is Yaz0 encoded</returns>
    public static bool Check(Stream Data) => Data.IsMagicMatch(MAGIC);

    /// <summary>
    /// Attempts to decompress the given data as YAZ0.
    /// </summary>
    /// <param name="Data">The data to decode</param>
    /// <returns>The byte[] of decoded data. Will be the same as the input if it is not YAZ0 encoded</returns>
    public static byte[] Decompress(byte[] Data) => Decode(Data);
    /// <summary>
    /// Encodes the given data as Yaz0.<para/>- Note: Don't double Yaz0 encode data, it doesn't save any space
    /// </summary>
    /// <param name="Data">The data to encode</param>
    /// <param name="BGW">A <see cref="BackgroundWorker"/> that will report the percentage complete out of 100.<para/>Set to NULL to disable</param>
    /// <param name="UseQuick">If TRUE, will use a different, faster encoding.</param>
    /// <returns>The byte[] of encoded data.</returns>
    public static byte[] Compress(byte[] Data, BackgroundWorker? BGW, bool UseQuick = false) => UseQuick ? QuickEncode(Data, BGW) : Encode(Data, BGW, DEFAULT_STRENGTH);
    /// <summary>
    /// Encodes the given data as Yaz0.<para/>Use this if you indent to use <see cref="FileUtil.RunForFileBytes(string, Func{byte[], byte[]})"/> (and don't care about strength or progress reporting)
    /// </summary>
    /// <param name="Data">The data to encode</param>
    /// <returns>The byte[] of encoded data.</returns>
    public static byte[] Compress(byte[] Data) => Encode(Data, null, DEFAULT_STRENGTH);

    //====================================================================================================

    private static byte[] Decode(byte[] Data)
    {
        using MemoryStream YAZ0 = new(Data);
        if (!Check(YAZ0))
            return Data; //NO MORE EXCEPTIONS!!!

        uint DecompressedSize = YAZ0.ReadUInt32(),
            CompressedDataOffset = YAZ0.ReadUInt32(),
            UncompressedDataOffset = YAZ0.ReadUInt32();

        List<byte> Decoding = new();
        while (Decoding.Count < DecompressedSize)
        {
            byte FlagByte = (byte)YAZ0.ReadByte();
            BitArray FlagSet = new(new byte[1] { FlagByte });

            for (int i = 7; i > -1 && (Decoding.Count < DecompressedSize); i--)
            {
                if (FlagSet[i] == true)
                    Decoding.Add((byte)YAZ0.ReadByte());
                else
                {
                    byte Tmp = (byte)YAZ0.ReadByte();
                    int Offset = (((byte)(Tmp & 0x0F) << 8) | (byte)YAZ0.ReadByte()) + 1,
                        Length = (Tmp & 0xF0) == 0 ? YAZ0.ReadByte() + 0x12 : (byte)((Tmp & 0xF0) >> 4) + 2;

                    for (int j = 0; j < Length; j++)
                        Decoding.Add(Decoding[^Offset]);
                }
            }
        }
        return Decoding.ToArray();
    }

    //====================================================================================================

    private record struct Ret(int SrcPos, int DstPos);

    private const int DEFAULT_STRENGTH = 0x1000;
    private static uint ByteCountA;
    private static uint MatchPos;
    private static int PrevFlag = 0;

    private static uint EncodeSimple(byte[] src, int size, int pos, ref uint pMatchPos, int Strength)
    {
        int startPos = pos - Strength;
        uint numBytes = 1;
        uint matchPos = 0;

        if (startPos < 0)
            startPos = 0;
        for (int i = startPos; i < pos; i++)
        {
            int j;
            int check = Math.Min(size - pos, Strength);
            for (j = 0; j < check; j++)
                if (src[i + j] != src[j + pos])
                    break;

            if (j > numBytes)
            {
                numBytes = (uint)j;
                matchPos = (uint)i;
            }
        }
        pMatchPos = matchPos;
        if (numBytes == 2)
            numBytes = 1;
        return numBytes;
    }

    private static uint EncodeAdvanced(byte[] src, int size, int pos, ref uint pMatchPos, int Strength)
    {
        // if prevFlag is set, it means that the previous position was determined by look-ahead try.
        // so just use it. this is not the best optimization, but nintendo's choice for speed.
        if (PrevFlag == 1)
        {
            pMatchPos = MatchPos;
            PrevFlag = 0;
            return ByteCountA;
        }
        PrevFlag = 0;
        uint numBytes = EncodeSimple(src, size, pos, ref MatchPos, Strength);
        pMatchPos = MatchPos;

        // if this position is RLE encoded, then compare to copying 1 byte and next position(pos+1) encoding
        if (numBytes >= 3)
        {
            ByteCountA = EncodeSimple(src, size, pos + 1, ref MatchPos, Strength);
            // if the next position encoding is +2 longer than current position, choose it.
            // this does not guarantee the best optimization, but fairly good optimization with speed.
            if (ByteCountA >= numBytes + 2)
            {
                numBytes = 1;
                PrevFlag = 1;
            }
        }
        return numBytes;
    }

    private static byte[] Encode(byte[] src, BackgroundWorker? BGW, int Strength)
    {
        ByteCountA = 0;
        MatchPos = 0;
        PrevFlag = 0;
        List<byte> OutputFile = new() { 0x59, 0x61, 0x7A, 0x30 };
        Span<byte> len = BitConverter.GetBytes(src.Length);
        StreamUtil.ApplyEndian(len);
        OutputFile.AddRange(len.ToArray());
        OutputFile.AddRange(new byte[8]);
        Ret r = new(0, 0);
        byte[] dst = new byte[24];
        int dstSize = 0;
        int lastpercent = -1;

        uint validBitCount = 0;
        byte currCodeByte = 0;

        while (r.SrcPos < src.Length)
        {
            if (BGW?.CancellationPending ?? false)
                return [];

            uint numBytes;
            uint matchPos = 0;
            uint srcPosBak;

            numBytes = EncodeAdvanced(src, src.Length, r.SrcPos, ref matchPos, Strength);
            if (numBytes < 3)
            {
                //straight copy
                dst[r.DstPos] = src[r.SrcPos];
                r.DstPos++;
                r.SrcPos++;
                //set flag for straight copy
                currCodeByte |= (byte)(0x80 >> (int)validBitCount);
            }
            else
            {
                //RLE part
                uint dist = (uint)(r.SrcPos - matchPos - 1);
                byte byte1;
                byte byte2;
                byte byte3;

                if (numBytes >= 0x12) // 3 byte encoding
                {
                    byte1 = (byte)(0 | (dist >> 8));
                    byte2 = (byte)(dist & 0xff);
                    dst[r.DstPos++] = byte1;
                    dst[r.DstPos++] = byte2;
                    // maximum runlength for 3 byte encoding
                    if (numBytes > 0xff + 0x12)
                    {
                        numBytes = (uint)(0xff + 0x12);
                    }
                    byte3 = (byte)(numBytes - 0x12);
                    dst[r.DstPos++] = byte3;
                }
                else // 2 byte encoding
                {
                    byte1 = (byte)(((numBytes - 2) << 4) | (dist >> 8));
                    byte2 = (byte)(dist & 0xff);
                    dst[r.DstPos++] = byte1;
                    dst[r.DstPos++] = byte2;
                }
                r.SrcPos += (int)numBytes;
            }
            validBitCount++;
            //Write eight codes
            if (validBitCount == 8)
            {
                OutputFile.Add(currCodeByte);
                for (int i = 0; i < r.DstPos; i++)
                    OutputFile.Add(dst[i]);
                dstSize += r.DstPos + 1;

#if DEBUG
                srcPosBak = (uint)r.SrcPos; //This is for debugging purposes
#endif
                currCodeByte = 0;
                validBitCount = 0;
                r.DstPos = 0;
            }
            float percent = MathUtil.GetPercentOf(r.SrcPos + 1, src.Length);
            int p = (int)percent;
            if (lastpercent != p)
            {
                BGW?.ReportProgress(p);
                lastpercent = p;
            }
        }

        if (validBitCount > 0)
        {
            OutputFile.Add(currCodeByte);
            for (int i = 0; i < r.DstPos; i++)
                OutputFile.Add(dst[i]);
            r.DstPos = 0;
        }

        return OutputFile.ToArray();
    }

    private static unsafe byte[] QuickEncode(byte[] Src, BackgroundWorker? BGW)
    {
        int lastpercent = -1;
        byte* dataptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(Src, 0);

        byte[] result = new byte[Src.Length + Src.Length / 8 + 0x10];
        byte* resultptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(result, 0);
        *resultptr++ = (byte)'Y';
        *resultptr++ = (byte)'a';
        *resultptr++ = (byte)'z';
        *resultptr++ = (byte)'0';
        *resultptr++ = (byte)((Src.Length >> 24) & 0xFF);
        *resultptr++ = (byte)((Src.Length >> 16) & 0xFF);
        *resultptr++ = (byte)((Src.Length >> 8) & 0xFF);
        *resultptr++ = (byte)((Src.Length >> 0) & 0xFF);
        for (int i = 0; i < 8; i++) *resultptr++ = 0;
        int length = Src.Length;
        int dstoffs = 16;
        int Offs = 0;
        while (true)
        {
            if (BGW?.CancellationPending ?? false)
                return [];

            int headeroffs = dstoffs++;
            resultptr++;
            byte header = 0;
            for (int i = 0; i < 8; i++)
            {
                int comp = 0;
                int back = 1;
                int nr = 2;
                {
                    byte* ptr = dataptr - 1;
                    int maxnum = 0x111;
                    if (length - Offs < maxnum) maxnum = length - Offs;
                    //Use a smaller amount of bytes back to decrease time
                    int maxback = 0x400;//0x1000;
                    if (Offs < maxback) maxback = Offs;
                    maxback = (int)dataptr - maxback;
                    int tmpnr;
                    while (maxback <= (int)ptr)
                    {
                        if (*(ushort*)ptr == *(ushort*)dataptr && ptr[2] == dataptr[2])
                        {
                            tmpnr = 3;
                            while (tmpnr < maxnum && ptr[tmpnr] == dataptr[tmpnr]) tmpnr++;
                            if (tmpnr > nr)
                            {
                                if (Offs + tmpnr > length)
                                {
                                    nr = length - Offs;
                                    back = (int)(dataptr - ptr);
                                    break;
                                }
                                nr = tmpnr;
                                back = (int)(dataptr - ptr);
                                if (nr == maxnum) break;
                            }
                        }
                        --ptr;
                    }
                }
                if (nr > 2)
                {
                    Offs += nr;
                    dataptr += nr;
                    if (nr >= 0x12)
                    {
                        *resultptr++ = (byte)(((back - 1) >> 8) & 0xF);
                        *resultptr++ = (byte)((back - 1) & 0xFF);
                        *resultptr++ = (byte)((nr - 0x12) & 0xFF);
                        dstoffs += 3;
                    }
                    else
                    {
                        *resultptr++ = (byte)((((back - 1) >> 8) & 0xF) | (((nr - 2) & 0xF) << 4));
                        *resultptr++ = (byte)((back - 1) & 0xFF);
                        dstoffs += 2;
                    }
                    comp = 1;
                }
                else
                {
                    *resultptr++ = *dataptr++;
                    dstoffs++;
                    Offs++;
                }
                header = (byte)((header << 1) | ((comp == 1) ? 0 : 1));
                if (Offs >= length)
                {
                    header = (byte)(header << (7 - i));
                    break;
                }
            }
            result[headeroffs] = header;
            if (Offs >= length)
                break;

            float percent = MathUtil.GetPercentOf(Offs + 1, Src.Length);
            int p = (int)percent;
            if (lastpercent != p)
            {
                BGW?.ReportProgress(p);
                lastpercent = p;
            }
        }
        while ((dstoffs % 4) != 0) dstoffs++;
        byte[] realresult = new byte[dstoffs];
        Array.Copy(result, realresult, dstoffs);
        return realresult;
    }
}