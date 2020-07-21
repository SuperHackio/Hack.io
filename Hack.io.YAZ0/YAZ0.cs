using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Hack.io.YAZ0
{
    /// <summary>
    /// Class containing methods to compress and decompress Data into Yaz0
    /// </summary>
    public static class YAZ0
    {
        private static readonly string Magic = "Yaz0";
        /// <summary>
        /// Decompress a file into a memorystream
        /// </summary>
        /// <param name="Filename"></param>
        /// <returns></returns>
        public static MemoryStream Decompress(string Filename)
        {
            FileStream YAZ0 = new FileStream(Filename, FileMode.Open);
            MemoryStream MS = new MemoryStream(Decomp(YAZ0));
            YAZ0.Close();
            return MS;
        }
        /// <summary>
        /// Decompress a byte[] into a memorystream
        /// </summary>
        /// <param name="Data"></param>
        /// <returns></returns>
        public static MemoryStream Decompress(byte[] Data)
        {
            return new MemoryStream(Decomp(new MemoryStream(Data)));
        }
        /// <summary>
        /// Compress a file
        /// </summary>
        /// <param name="Filename">File to compress</param>
        /// <param name="Quick">If true, takes shorter time to compress, but is overall weaker then if disabled (resulting in larger files)</param>
        public static void Compress(string Filename, bool Quick = false)
        {
            byte[] Final;
            if (Quick)
            {
                byte[] Original = File.ReadAllBytes(Filename);
                Final = QuickCompress(Original);
            }
            else
            {
                FileStream YAZ0 = new FileStream(Filename, FileMode.Open);
                Final = DoCompression(YAZ0);
                YAZ0.Close();
            }
            File.WriteAllBytes(Filename, Final);
        }
        /// <summary>
        /// Compress a MemoryStream
        /// </summary>
        /// <param name="YAZ0">MemoryStream to compress</param>
        public static byte[] Compress(MemoryStream YAZ0)
        {
            return DoCompression(YAZ0);
        }
        /// <summary>
        /// Checks a given file for Yaz0 Encoding
        /// </summary>
        /// <param name="Filename">File to check</param>
        /// <returns>true if the file is Yaz0 Encoded</returns>
        public static bool Check(string Filename)
        {
            FileStream YAZ0 = new FileStream(Filename, FileMode.Open);
            bool Check = YAZ0.ReadString(4) == Magic;
            YAZ0.Close();
            return Check;
        }

        
        private static byte[] DoCompression(Stream YAZ0)
        {
            MemoryStream Temp = new MemoryStream();
            YAZ0.CopyTo(Temp);
            byte[] file = Temp.GetBuffer();

            List<byte> InstructionBits = new List<byte>();
            List<byte> SetDictionaries = new List<byte>();
            List<byte> UncompressedData = new List<byte>();
            List<int[]> CompressedData = new List<int[]>();

            int maxDictionarySize = 4096;
            int minMatchLength = 3;
            int maxMatchLength = 255 + 0x12;
            int decompressedSize = 0;

            for (int i = 0; i < file.Length; i++)
            {
                if (SetDictionaries.Contains(file[i]))
                {
                    //compressed data
                    int[] matches = FindAllMatches(ref SetDictionaries, file[i]);
                    int[] bestMatch = FindLargestMatch(ref SetDictionaries, matches, ref file, i, maxMatchLength);

                    if (bestMatch[1] >= minMatchLength)
                    {
                        InstructionBits.Add(0);
                        bestMatch[0] = SetDictionaries.Count - bestMatch[0];

                        for (int j = 0; j < bestMatch[1]; j++)
                            SetDictionaries.Add(file[i + j]);

                        i = i + bestMatch[1] - 1;

                        CompressedData.Add(bestMatch);
                        decompressedSize += bestMatch[1];
                    }
                    else
                    {
                        //uncompressed data
                        InstructionBits.Add(1);
                        UncompressedData.Add(file[i]);
                        SetDictionaries.Add(file[i]);
                        decompressedSize++;
                    }
                }
                else
                {
                    //uncompressed data
                    InstructionBits.Add(1);
                    UncompressedData.Add(file[i]);
                    SetDictionaries.Add(file[i]);
                    decompressedSize++;
                }

                if (SetDictionaries.Count > maxDictionarySize)
                {
                    int overflow = SetDictionaries.Count - maxDictionarySize;
                    SetDictionaries.RemoveRange(0, overflow);
                }
            }

            return BuildFinalBlocks(ref InstructionBits, ref UncompressedData, ref CompressedData, decompressedSize, 0);
        }
        //From https://github.com/Gericom/EveryFileExplorer/blob/master/CommonCompressors/YAZ0.cs
        private static unsafe byte[] QuickCompress(byte[] Data)
        {
            byte* dataptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(Data, 0);

            byte[] result = new byte[Data.Length + Data.Length / 8 + 0x10];
            byte* resultptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(result, 0);
            *resultptr++ = (byte)'Y';
            *resultptr++ = (byte)'a';
            *resultptr++ = (byte)'z';
            *resultptr++ = (byte)'0';
            *resultptr++ = (byte)((Data.Length >> 24) & 0xFF);
            *resultptr++ = (byte)((Data.Length >> 16) & 0xFF);
            *resultptr++ = (byte)((Data.Length >> 8) & 0xFF);
            *resultptr++ = (byte)((Data.Length >> 0) & 0xFF);
            for (int i = 0; i < 8; i++) *resultptr++ = 0;
            int length = Data.Length;
            int dstoffs = 16;
            int Offs = 0;
            while (true)
            {
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
                if (Offs >= length) break;
            }
            while ((dstoffs % 4) != 0) dstoffs++;
            byte[] realresult = new byte[dstoffs];
            Array.Copy(result, realresult, dstoffs);
            return realresult;
        }

        private static int[] FindAllMatches(ref List<byte> dictionary, byte match)
        {
            List<int> matchPositons = new List<int>();

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

        private static byte[] BuildFinalBlocks(ref List<byte> layoutBits, ref List<byte> uncompressedData, ref List<int[]> offsetLengthPairs, int decompressedSize, int offset)
        {
            List<byte> finalYAZ0Block = new List<byte>();
            List<byte> layoutBytes = new List<byte>();
            List<byte> compressedDataBytes = new List<byte>();
            List<byte> extendedLengthBytes = new List<byte>();

            //add Yaz0 magic number
            finalYAZ0Block.AddRange(Encoding.ASCII.GetBytes("Yaz0"));

            byte[] decompressedSizeArray = BitConverter.GetBytes(decompressedSize);
            Array.Reverse(decompressedSizeArray);
            finalYAZ0Block.AddRange(decompressedSizeArray);

            //add 8 0's per format specification
            for (int i = 0; i < 8; i++)
                finalYAZ0Block.Add(0);

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

            //Final Calculations
            foreach (int[] offsetLengthPair in offsetLengthPairs)
            {
                //if < 18, set 4 bits -2 as matchLength
                //if >= 18, set matchLength == 0, write length to new byte - 0x12

                int adjustedOffset = offsetLengthPair[0];
                int adjustedLength = (offsetLengthPair[1] >= 18) ? 0 : offsetLengthPair[1] - 2; //critical, 4 bit range is 0-15. Number must be at least 3 (if 2, when -2 is done, it will think it is 3 byte format), -2 is how it can store up to 17 without an extra byte because +2 will be added on decompression

                if (adjustedLength == 0)
                    extendedLengthBytes.Add((byte)(offsetLengthPair[1] - 18));

                int compressedInt = (adjustedLength << 12) | adjustedOffset - 1;

                byte[] compressed2Byte = new byte[2];
                compressed2Byte[0] = (byte)(compressedInt & 0XFF);
                compressed2Byte[1] = (byte)((compressedInt >> 8) & 0xFF);

                compressedDataBytes.Add(compressed2Byte[1]);
                compressedDataBytes.Add(compressed2Byte[0]);
            }

            //Finish
            for (int i = 0; i < layoutBytes.Count; i++)
            {
                finalYAZ0Block.Add(layoutBytes[i]);

                BitArray arrayOfBits = new BitArray(new byte[1] { layoutBytes[i] });

                for (int j = 7; ((j > -1) && ((uncompressedData.Count > 0) || (compressedDataBytes.Count > 0))); j--)
                {
                    if (arrayOfBits[j] == true)
                    {
                        finalYAZ0Block.Add(uncompressedData[0]);
                        uncompressedData.RemoveAt(0);
                    }
                    else
                    {
                        if (compressedDataBytes.Count > 0)
                        {
                            int length = compressedDataBytes[0] >> 4;

                            finalYAZ0Block.Add(compressedDataBytes[0]);
                            finalYAZ0Block.Add(compressedDataBytes[1]);
                            compressedDataBytes.RemoveRange(0, 2);

                            if (length == 0)
                            {
                                finalYAZ0Block.Add(extendedLengthBytes[0]);
                                extendedLengthBytes.RemoveAt(0);
                            }
                        }
                    }
                }


            }

            return finalYAZ0Block.ToArray();
        }

        private static byte[] Decomp(Stream YAZ0)
        {
            YAZ0.Position = 0;
            if (YAZ0.ReadString(4) != Magic)
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

            uint DecompressedSize = BitConverter.ToUInt32(YAZ0.ReadReverse(0, 4), 0), CompressedDataOffset = BitConverter.ToUInt32(YAZ0.ReadReverse(0, 4), 0), UncompressedDataOffset = BitConverter.ToUInt32(YAZ0.ReadReverse(0, 4), 0);

            List<byte> Decoding = new List<byte>();
            while (Decoding.Count < DecompressedSize)
            {
                byte FlagByte = (byte)YAZ0.ReadByte();
                BitArray FlagSet = new BitArray(new byte[1] { FlagByte });

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
                            Decoding.Add(Decoding[Decoding.Count - Offset]);
                    }
                }
            }
            return Decoding.ToArray();
        }
    }
}