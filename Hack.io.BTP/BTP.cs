using Hack.io.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hack.io.BTP
{
    /// <summary>
    /// Binary Texture Pattern
    /// </summary>
    public class BTP
    {
        /// <summary>
        /// Filename of this BTP file.<para/>Set using <see cref="Save(string)"/>;
        /// </summary>
        public string FileName { get; private set; } = null;
        /// <summary>
        /// Loop Mode of the BRK animation. See the <seealso cref="LoopMode"/> enum for values
        /// </summary>
        public LoopMode Loop { get; set; } = LoopMode.Once;
        /// <summary>
        /// Length of the animation in Frames. (Game Framerate = 1 second)
        /// </summary>
        public ushort Time { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<Animation> TextureAnimations { get; set; } = new List<Animation>();

        private readonly static string Magic = "J3D1btp1";
        private readonly static string Magic2 = "TPT1";

        /// <summary>
        /// Create an Empty BTP
        /// </summary>
        public BTP() { }
        /// <summary>
        /// Open a BTP from a file
        /// </summary>
        /// <param name="filename"></param>
        public BTP(string filename)
        {
            FileStream BTPFile = new FileStream(filename, FileMode.Open);
            if (BTPFile.ReadString(8) != Magic)
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

            uint Filesize = BitConverter.ToUInt32(BTPFile.ReadReverse(0, 4), 0);
            uint SectionCount = BitConverter.ToUInt32(BTPFile.ReadReverse(0, 4), 0);
            if (SectionCount != 1)
                throw new Exception(SectionCount > 1 ? "More than 1 section is in this BTP! Please send it to Super Hackio for investigation" : "There are no sections in this BTP!");

            BTPFile.Seek(0x10, SeekOrigin.Current);
            uint TPT1Start = (uint)BTPFile.Position;
            if (BTPFile.ReadString(4) != Magic2)
                throw new Exception($"Invalid Identifier. Expected \"{Magic2}\"");

            uint TPT1Length = BitConverter.ToUInt32(BTPFile.ReadReverse(0, 4), 0);
            Loop = (LoopMode)BTPFile.ReadByte();
            BTPFile.ReadByte();
            Time = BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0);

            ushort MaterialAnimTableCount = BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0), TexIDTableCount = BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0);
            uint MaterialAnimTableOffset = BitConverter.ToUInt32(BTPFile.ReadReverse(0, 4), 0) + TPT1Start, TexIDTableOffset = BitConverter.ToUInt32(BTPFile.ReadReverse(0, 4), 0) + TPT1Start,
                RemapTableOffset = BitConverter.ToUInt32(BTPFile.ReadReverse(0, 4), 0) + TPT1Start, NameSTOffset = BitConverter.ToUInt32(BTPFile.ReadReverse(0, 4), 0) + TPT1Start;

            BTPFile.Seek(NameSTOffset, SeekOrigin.Begin);
            List<KeyValuePair<ushort, string>> MaterialNames = new List<KeyValuePair<ushort, string>>();
            ushort StringCount = BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0);
            BTPFile.Seek(2, SeekOrigin.Current);
            for (int i = 0; i < StringCount; i++)
            {
                ushort hash = BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0);
                long PausePosition = BTPFile.Position + 2;
                BTPFile.Seek(NameSTOffset + BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0), SeekOrigin.Begin);
                MaterialNames.Add(new KeyValuePair<ushort, string>(hash, BTPFile.ReadString()));
                BTPFile.Position = PausePosition;
            }

            BTPFile.Seek(RemapTableOffset, SeekOrigin.Begin);
            List<ushort> RemapTable = new List<ushort>();
            for (int i = 0; i < MaterialAnimTableCount; i++)
                RemapTable.Add(BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0));

            ushort TextureFrameCount, TextureFirstID;
            for (int i = 0; i < MaterialAnimTableCount; i++)
            {
                Animation Anim = new Animation() { MaterialName = MaterialNames[i].Value, RemapIndex = RemapTable[i] };

                BTPFile.Seek(MaterialAnimTableOffset + (i * 0x08), SeekOrigin.Begin);
                TextureFrameCount = BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0);
                TextureFirstID = BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0);
                Anim.TexMapIndex = (byte)BTPFile.ReadByte();
                for (int j = 0; j < TextureFrameCount; j++)
                {
                    BTPFile.Seek(TexIDTableOffset + ((TextureFirstID + j) * 0x02), SeekOrigin.Begin);
                    Anim.TextureFrames.Add(BitConverter.ToUInt16(BTPFile.ReadReverse(0, 2), 0));
                }

                TextureAnimations.Add(Anim);
            }

            BTPFile.Close();
            FileName = filename;
        }
        /// <summary>
        /// Save the BTPto a file
        /// </summary>
        /// <param name="Filename">New file to save to</param>
        public void Save(string Filename = null)
        {
            if (FileName == null && Filename == null)
                throw new Exception("No Filename has been given");
            else if (Filename != null)
                FileName = Filename;

            string Padding = "Hack.io.BTP © Super Hackio Incorporated 2019-2020";
            FileStream BTPFile = new FileStream(Filename, FileMode.Create);

            BTPFile.WriteString(Magic);
            BTPFile.Write(new byte[8] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x01 }, 0, 8);
            BTPFile.Write(new byte[16] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, }, 0, 16);
            
            uint TPT1Start = (uint)BTPFile.Position;
            BTPFile.WriteString(Magic2);
            BTPFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BTPFile.WriteByte((byte)Loop);
            BTPFile.WriteByte(0xFF);
            BTPFile.WriteReverse(BitConverter.GetBytes(Time), 0, 2);
            
            BTPFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations.Count), 0, 2);
            List<ushort> FullTexIDList = new List<ushort>();
            for (int i = 0; i < TextureAnimations.Count; i++)
                FindMatch(ref FullTexIDList, TextureAnimations[i].TextureFrames);
            BTPFile.WriteReverse(BitConverter.GetBytes((ushort)FullTexIDList.Count), 0, 2);

            long MatAnimTableOffsetPos = BTPFile.Position;
            int offs = 0x20;
            BTPFile.WriteReverse(BitConverter.GetBytes(offs), 0, 4);

            long TexIDTableOffsetPos = BTPFile.Position;
            offs += TextureAnimations.Count * 8;
            BTPFile.WriteReverse(BitConverter.GetBytes(offs), 0, 4);

            long RemapTableOffsetPos = BTPFile.Position;
            offs += FullTexIDList.Count * 2;
            #region Padding
            while (offs % 4 != 0)
                offs++;
            #endregion
            BTPFile.WriteReverse(BitConverter.GetBytes(offs), 0, 4);

            long NameSTOffsetPos = BTPFile.Position;
            offs += TextureAnimations.Count * 2;
            #region Padding
            while (offs % 4 != 0)
                offs++;
            #endregion
            BTPFile.WriteReverse(BitConverter.GetBytes(offs), 0, 4);

            List<byte> Remaps = new List<byte>();
            string[] matnames = new string[TextureAnimations.Count];
            for (int i = 0; i < TextureAnimations.Count; i++)
            {
                BTPFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].TextureFrames.Count), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes((ushort)FindMatch(ref FullTexIDList, TextureAnimations[i].TextureFrames)), 0, 2);
                BTPFile.WriteByte(TextureAnimations[i].TexMapIndex);
                BTPFile.WriteByte(0xFF);
                BTPFile.WriteByte(0xFF);
                BTPFile.WriteByte(0xFF);
                byte[] temp = BitConverter.GetBytes(TextureAnimations[i].RemapIndex);
                temp.Reverse();
                Remaps.AddRange(temp);
                matnames[i] = TextureAnimations[i].MaterialName;
            }

            for (int i = 0; i < FullTexIDList.Count; i++)
                BTPFile.WriteReverse(BitConverter.GetBytes(FullTexIDList[i]), 0, 2);

            #region Padding
            int PadCount = 0;
            while (BTPFile.Position % 4 != 0)
                BTPFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            BTPFile.Write(Remaps.ToArray(), 0, Remaps.Count);

            #region Padding
            PadCount = 0;
            while (BTPFile.Position % 4 != 0)
                BTPFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            BTPFile.WriteReverse(BitConverter.GetBytes((ushort)matnames.Length), 0, 2);
            BTPFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
            ushort stringofffset = (ushort)(4 + (4 * matnames.Length));
            List<byte> bytestrings = new List<byte>();
            for (int i = 0; i < matnames.Length; i++)
            {
                BTPFile.WriteReverse(BitConverter.GetBytes(StringToHash(matnames[i])), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes(stringofffset), 0, 2);
                byte[] currentstring = Encoding.GetEncoding(932).GetBytes(matnames[i]);
                stringofffset += (ushort)(currentstring.Length + 1);
                bytestrings.AddRange(currentstring);
                bytestrings.Add(0x00);
            }
            BTPFile.Write(bytestrings.ToArray(), 0, bytestrings.Count);
            #region Padding
            PadCount = 0;
            while (BTPFile.Position % 32 != 0)
                BTPFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            BTPFile.Position = 0x08;
            BTPFile.WriteReverse(BitConverter.GetBytes((uint)BTPFile.Length), 0, 4);
            BTPFile.Position = TPT1Start+0x04;
            BTPFile.WriteReverse(BitConverter.GetBytes((uint)(BTPFile.Length-TPT1Start)), 0, 4);

            BTPFile.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{new FileInfo(FileName).Name} {(TextureAnimations.Count > 0 ? $"[{TextureAnimations.Count} Texture Animation{(TextureAnimations.Count > 1 ? "s" : "")}] " : "")}";


        /// <summary>
        /// BTK Looping Modes
        /// </summary>
        public enum LoopMode : byte
        {
            /// <summary>
            /// Play Once then Stop
            /// </summary>
            Default = 0x00,
            /// <summary>
            /// Play Once then Stop
            /// </summary>
            Once = 0x01,
            /// <summary>
            /// Constantly play the animation
            /// </summary>
            Loop = 0x02,
            /// <summary>
            /// Play the animation to the end. then reverse the animation and play to the start, then Stop
            /// </summary>
            OnceAndReverse = 0x03,
            /// <summary>
            /// Play the animation to the end. then reverse the animation and play to the start, Looped
            /// </summary>
            OnceAndReverseLoop = 0x04
        }

        /// <summary>
        /// Animation Container
        /// </summary>
        public class Animation
        {
            /// <summary>
            /// Name of the Material that this animation applies to
            /// </summary>
            public string MaterialName { get; set; }
            /// <summary>
            /// Unknown
            /// </summary>
            public ushort RemapIndex { get; set; }
            /// <summary>
            /// The Texture map to change
            /// </summary>
            public byte TexMapIndex { get; set; }
            /// <summary>
            /// List of texture ID's.<para/>These ID's are relative to the entire BMD/BDL Texture Storage.
            /// </summary>
            public List<ushort> TextureFrames { get; set; } = new List<ushort>();

            /// <summary>
            /// Create an empty animation
            /// </summary>
            public Animation() { }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"{MaterialName} [{TextureFrames.Count} Frame{(TextureFrames.Count > 1 ? "s":"")}]";
        }

        /// <summary>
        /// Generates a 2 byte hash from a string
        /// </summary>
        /// <param name="Input">string to convert</param>
        /// <returns>hashed string</returns>
        static internal ushort StringToHash(string Input)
        {
            int Hash = 0;
            for (int i = 0; i < Input.Length; i++)
            {
                Hash *= 3;
                Hash += Input[i];
                Hash = 0xFFFF & Hash; //cast to short 
            }

            return (ushort)Hash;
        }

        private short FindMatch(ref List<ushort> FullList, List<ushort> currentSequence)
        {
            if (!FullList.ContainsSubsequence(currentSequence))
            {
                FullList.AddRange(currentSequence);
            }

            return (short)FullList.SubListIndex(0, currentSequence);
        }
    }
}
