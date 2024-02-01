using Hack.io.Utility;
using System.Collections;
using System.ComponentModel;
using System.Text;

namespace Hack.io.YAY0;

/// <summary>
/// Class containing methods to compress and decompress Data into Yay0
/// </summary>
public static class YAY0
{
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const string MAGIC = "Yay0";

    /// <summary>
    /// Checks the data for Yay0 Encoding
    /// </summary>
    /// <param name="Data">The stream of data to check</param>
    /// <returns>TRUE if the stream is Yay0 encoded</returns>
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
    public static byte[] Compress(byte[] Data, BackgroundWorker? BGW) => Encode(Data, BGW);
    /// <summary>
    /// Encodes the given data as Yay0.<para/>Use this if you indent to use <see cref="FileUtil.RunForFileBytes(string, Func{byte[], byte[]})"/> (and don't care about strength or progress reporting)
    /// </summary>
    /// <param name="Data">The data to encode</param>
    /// <returns>The byte[] of encoded data.</returns>
    public static byte[] Compress(byte[] Data) => Encode(Data, null);

    //====================================================================================================

    private static byte[] Decode(byte[] Data)
    {
        MemoryStream YAY0 = new(Data);
        if (!Check(YAY0))
            return Data; //NO MORE EXCEPTIONS!!!

        uint uncompressedSize = YAY0.ReadUInt32(),
            linkTableOffset = YAY0.ReadUInt32(),
            byteChunkAndCountModifiersOffset = YAY0.ReadUInt32();

        int maskBitCounter = 0,
            currentOffsetInDestBuffer = 0,
            currentMask = 0;

        byte[] uncompressedData = new byte[uncompressedSize];

        while (currentOffsetInDestBuffer < uncompressedSize)
        {
            // If we're out of bits, get the next mask.
            if (maskBitCounter == 0)
            {
                currentMask = YAY0.ReadInt32();
                maskBitCounter = 32;
            }

            // If the next bit is set, the chunk is non-linked and just copy it from the non-link table.
            // Do a copy otherwise.
            if (((uint)currentMask & (uint)0x80000000) == 0x80000000)
            {
                long pauseposition = YAY0.Position;
                YAY0.Position = byteChunkAndCountModifiersOffset++;
                uncompressedData[currentOffsetInDestBuffer++] = (byte)YAY0.ReadByte();
                YAY0.Position = pauseposition;
            }
            else
            {
                // Read 16-bit from the link table
                long pauseposition = YAY0.Position;
                YAY0.Position = linkTableOffset;
                ushort link = YAY0.ReadUInt16();
                linkTableOffset += 2;
                YAY0.Position = pauseposition;

                // Calculate the offset
                int offset = currentOffsetInDestBuffer - (link & 0xfff);

                // Calculate the count
                int count = link >> 12;

                if (count == 0)
                {
                    pauseposition = YAY0.Position;
                    YAY0.Position = byteChunkAndCountModifiersOffset++;
                    byte countModifier = (byte)YAY0.ReadByte();
                    YAY0.Position = pauseposition;
                    count = countModifier + 18;
                }
                else
                    count += 2;

                // Copy the block
                int blockCopy = offset;

                for (int i = 0; i < count; i++)
                    uncompressedData[currentOffsetInDestBuffer++] = uncompressedData[blockCopy++ - 1];
            }

            // Get the next bit in the mask.
            currentMask <<= 1;
            maskBitCounter--;
        }

        return uncompressedData;
    }

    //====================================================================================================

    private static byte[] Encode(byte[] file, BackgroundWorker? BGW)
    {
        List<byte> layoutBits = new();
        List<byte> dictionary = new();

        List<byte> uncompressedData = new();
        List<int[]> compressedData = new();

        int maxDictionarySize = 4096;
        int maxMatchLength = 255 + 0x12;
        int minMatchLength = 3;
        int decompressedSize = 0;
        int lastpercent = -1;

        for (int i = 0; i < file.Length; i++)
        {
            if (dictionary.Contains(file[i]))
            {
                //check for best match
                int[] matches = FindAllMatches(ref dictionary, file[i]);
                int[] bestMatch = FindLargestMatch(ref dictionary, matches, ref file, i, maxMatchLength);

                if (bestMatch[1] >= minMatchLength)
                {
                    //add to compressedData
                    layoutBits.Add(0);
                    bestMatch[0] = dictionary.Count - bestMatch[0]; //sets offset in relation to end of dictionary

                    for (int j = 0; j < bestMatch[1]; j++)
                        dictionary.Add(file[i + j]);

                    i = i + bestMatch[1] - 1;

                    compressedData.Add(bestMatch);
                    decompressedSize += bestMatch[1];
                }
                else
                {
                    //add to uncompressed data
                    layoutBits.Add(1);
                    uncompressedData.Add(file[i]);
                    dictionary.Add(file[i]);
                    decompressedSize++;
                }
            }
            else
            {
                //uncompressed data
                layoutBits.Add(1);
                uncompressedData.Add(file[i]);
                dictionary.Add(file[i]);
                decompressedSize++;
            }

            if (dictionary.Count > maxDictionarySize)
            {
                int overflow = dictionary.Count - maxDictionarySize;
                dictionary.RemoveRange(0, overflow);
            }

            //Is this even correct??
            float percent = MathUtil.GetPercentOf(i + 1, file.Length);
            int p = (int)percent;
            if (lastpercent != p)
            {
                BGW?.ReportProgress(p);
                lastpercent = p;
            }
        }

        return BuildYAY0CompressedBlock(ref layoutBits, ref uncompressedData, ref compressedData, decompressedSize, 0);
    }

    private static int[] FindAllMatches(ref List<byte> dictionary, byte match)
    {
        List<int> matchPositons = new();

        for (int i = 0; i < dictionary.Count; i++)
            if (dictionary[i] == match)
                matchPositons.Add(i);

        return matchPositons.ToArray();
    }

    private static int[] FindLargestMatch(ref List<byte> dictionary, int[] matchesFound, ref byte[] file, int fileIndex, int maxMatch)
    {
        int[] matchSizes = new int[matchesFound.Length];

        for (int i = 0; i < matchesFound.Length; i++)
        {
            int matchSize = 1;
            bool matchFound = true;

            while (matchFound && matchSize < maxMatch && (fileIndex + matchSize < file.Length) && (matchesFound[i] + matchSize < dictionary.Count)) //NOTE: This could be relevant to compression issues? I suspect it's more related to writing
            {
                if (file[fileIndex + matchSize] == dictionary[matchesFound[i] + matchSize])
                    matchSize++;
                else
                    matchFound = false;
            }

            matchSizes[i] = matchSize;
        }

        int[] bestMatch = new int[2];

        bestMatch[0] = matchesFound[0];
        bestMatch[1] = matchSizes[0];

        for (int i = 1; i < matchesFound.Length; i++)
        {
            if (matchSizes[i] > bestMatch[1])
            {
                bestMatch[0] = matchesFound[i];
                bestMatch[1] = matchSizes[i];
            }
        }

        return bestMatch;
    }

    public static byte[] BuildYAY0CompressedBlock(ref List<byte> layoutBits, ref List<byte> uncompressedData, ref List<int[]> offsetLengthPairs, int decompressedSize, int offset)
    {
        List<byte> finalYAY0Block = new();
        List<byte> layoutBytes = new();
        List<byte> compressedDataBytes = new();
        List<byte> extendedLengthBytes = new();

        int compressedOffset = 16 + offset; //header size
        int uncompressedOffset;

        //add Yay0 magic number
        finalYAY0Block.AddRange(Encoding.ASCII.GetBytes("Yay0"));

        Span<byte> len = BitConverter.GetBytes(decompressedSize);
        StreamUtil.ApplyEndian(len);
        finalYAY0Block.AddRange(len.ToArray());

        //assemble layout bytes
        while (layoutBits.Count > 0)
        {
            while (layoutBits.Count < 8)
                layoutBits.Add(0);

            string layoutBitsString = layoutBits[0].ToString() + layoutBits[1].ToString() + layoutBits[2].ToString() + layoutBits[3].ToString()
                    + layoutBits[4].ToString() + layoutBits[5].ToString() + layoutBits[6].ToString() + layoutBits[7].ToString();

            byte[] layoutByteArray = new byte[1];
            layoutByteArray[0] = Convert.ToByte(layoutBitsString, 2);
            layoutBytes.Add(layoutByteArray[0]);
            layoutBits.RemoveRange(0, (layoutBits.Count < 8) ? layoutBits.Count : 8);

        }

        //assemble offsetLength shorts
        foreach (int[] offsetLengthPair in offsetLengthPairs)
        {
            //if < 18, set 4 bits -2 as matchLength
            //if >= 18, set matchLength == 0, write length to new byte - 0x12

            int adjustedOffset = offsetLengthPair[0];
            int adjustedLength = (offsetLengthPair[1] >= 18) ? 0 : offsetLengthPair[1] - 2; //vital, 4 bit range is 0-15. Number must be at least 3 (if 2, when -2 is done, it will think it is 3 byte format), -2 is how it can store up to 17 without an extra byte because +2 will be added on decompression

            int compressedInt = (adjustedLength << 12) | adjustedOffset - 1;

            byte[] compressed2Byte = new byte[2];
            compressed2Byte[0] = (byte)(compressedInt & 0xFF);
            compressed2Byte[1] = (byte)((compressedInt >> 8) & 0xFF);

            compressedDataBytes.Add(compressed2Byte[1]);
            compressedDataBytes.Add(compressed2Byte[0]);

            if (adjustedLength == 0)
            {
                extendedLengthBytes.Add((byte)(offsetLengthPair[1] - 18));
            }
        }

        //pad layout bits if needed
        while (layoutBytes.Count % 4 != 0)
        {
            layoutBytes.Add(0);
        }

        compressedOffset += layoutBytes.Count;

        //add final compressed offset
        byte[] compressedOffsetArray = BitConverter.GetBytes(compressedOffset);
        Array.Reverse(compressedOffsetArray);
        finalYAY0Block.AddRange(compressedOffsetArray);

        //add final uncompressed offset
        uncompressedOffset = compressedOffset + compressedDataBytes.Count;
        byte[] uncompressedOffsetArray = BitConverter.GetBytes(uncompressedOffset);
        Array.Reverse(uncompressedOffsetArray);
        finalYAY0Block.AddRange(uncompressedOffsetArray);

        //add layout bits
        foreach (byte layoutByte in layoutBytes)                 //add layout bytes to file
        {
            finalYAY0Block.Add(layoutByte);
        }

        //add compressed data
        foreach (byte compressedByte in compressedDataBytes)     //add compressed bytes to file
        {
            finalYAY0Block.Add(compressedByte);
        }

        //non-compressed/additional-length bytes
        {
            for (int i = 0; i < layoutBytes.Count; i++)
            {
                BitArray arrayOfBits = new(new byte[1] { layoutBytes[i] });

                for (int j = 7; ((j > -1) && ((uncompressedData.Count > 0) || (compressedDataBytes.Count > 0))); j--)
                {
                    if (arrayOfBits[j] == true)
                    {
                        finalYAY0Block.Add(uncompressedData[0]);
                        uncompressedData.RemoveAt(0);
                    }
                    else
                    {
                        if (compressedDataBytes.Count > 0)
                        {
                            int length = compressedDataBytes[0] >> 4;
                            compressedDataBytes.RemoveRange(0, 2);

                            if (length == 0)
                            {
                                finalYAY0Block.Add(extendedLengthBytes[0]);
                                extendedLengthBytes.RemoveAt(0);
                            }


                        }
                    }
                }
            }
        }

        return finalYAY0Block.ToArray();
    }
}