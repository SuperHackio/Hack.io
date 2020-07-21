using Hack.io.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hack.io.BPK
{
    /// <summary>
    /// Binary Palette Keyframes
    /// </summary>
    public class BPK
    {
        /// <summary>
        /// Filename of this BPK file.<para/>Set using <see cref="Save(string)"/>;
        /// </summary>
        public string FileName { get; private set; } = null;
        /// <summary>
        /// Loop Mode of the BPK animation. See the <seealso cref="LoopMode"/> enum for values
        /// </summary>
        public LoopMode Loop { get; set; } = LoopMode.Once;
        /// <summary>
        /// Length of the animation in Frames. (Game Framerate = 1 second)
        /// </summary>
        public ushort Time { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<Animation> ColourAnimations { get; set; } = new List<Animation>();

        private readonly string Magic = "J3D1bpk1";
        private readonly string Magic2 = "PAK1";
        /// <summary>
        /// Create a new BPK file
        /// </summary>
        public BPK() { }
        /// <summary>
        /// Open a BPK from a file
        /// </summary>
        /// <param name="filename"></param>
        public BPK(string filename)
        {
            FileStream BPKFile = new FileStream(filename, FileMode.Open);
            if (BPKFile.ReadString(8) != Magic)
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

            uint Filesize = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0);
            uint SectionCount = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0);
            if (SectionCount != 1)
                throw new Exception(SectionCount > 1 ? "More than 1 section is in this BPK! Please send it to Super Hackio for investigation" : "There are no sections in this BPK!");

            BPKFile.Seek(0x10, SeekOrigin.Current);
            uint TPT1Start = (uint)BPKFile.Position;
            if (BPKFile.ReadString(4) != Magic2)
                throw new Exception($"Invalid Identifier. Expected \"{Magic2}\"");

            uint TPT1Length = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0);
            Loop = (LoopMode)BPKFile.ReadByte();
            BPKFile.Position += 3;
            Time = BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0);

            ushort ColourAnimationCount = BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0), RedCount = BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0),
                GreenCount = BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0), BlueCount = BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0), AlphaCount = BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0);

            uint ColourAnimationOffset = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0) + TPT1Start,
                RemapTableOffset = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0) + TPT1Start, NameSTOffset = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0) + TPT1Start,
                RedOffset = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0) + TPT1Start, GreenOffset = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0) + TPT1Start,
                BlueOffset = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0) + TPT1Start, AlphaOffset = BitConverter.ToUInt32(BPKFile.ReadReverse(0, 4), 0) + TPT1Start;

            BPKFile.Seek(NameSTOffset, SeekOrigin.Begin);
            List<KeyValuePair<ushort, string>> MaterialNames = new List<KeyValuePair<ushort, string>>();
            ushort StringCount = BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0);
            BPKFile.Seek(2, SeekOrigin.Current);
            for (int i = 0; i < StringCount; i++)
            {
                ushort hash = BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0);
                long PausePosition = BPKFile.Position + 2;
                BPKFile.Seek(NameSTOffset + BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0), SeekOrigin.Begin);
                MaterialNames.Add(new KeyValuePair<ushort, string>(hash, BPKFile.ReadString()));
                BPKFile.Position = PausePosition;
            }

            BPKFile.Seek(RemapTableOffset, SeekOrigin.Begin);
            List<ushort> RemapTable = new List<ushort>();
            for (int i = 0; i < ColourAnimationCount; i++)
                RemapTable.Add(BitConverter.ToUInt16(BPKFile.ReadReverse(0, 2), 0));

            List<short> RedTable = new List<short>();
            List<short> GreenTable = new List<short>();
            List<short> BlueTable = new List<short>();
            List<short> AlphaTable = new List<short>();

            BPKFile.Seek(RedOffset, SeekOrigin.Begin);
            for (int i = 0; i < RedCount; i++)
                RedTable.Add(BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0));

            BPKFile.Seek(GreenOffset, SeekOrigin.Begin);
            for (int i = 0; i < GreenCount; i++)
                GreenTable.Add(BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0));

            BPKFile.Seek(BlueOffset, SeekOrigin.Begin);
            for (int i = 0; i < BlueCount; i++)
                BlueTable.Add(BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0));

            BPKFile.Seek(AlphaOffset, SeekOrigin.Begin);
            for (int i = 0; i < AlphaCount; i++)
                AlphaTable.Add(BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0));

            BPKFile.Seek(ColourAnimationOffset, SeekOrigin.Begin);
            short KeyFrameCount, TargetKeySet, TangentType;
            for (int i = 0; i < ColourAnimationCount; i++)
            {
                Animation Anim = new Animation() { MaterialName = MaterialNames[i].Value, RemapID = RemapTable[i] };
                KeyFrameCount = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                Anim.RedFrames = ReadKeyframe(RedTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                Anim.GreenFrames = ReadKeyframe(GreenTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                Anim.BlueFrames = ReadKeyframe(BlueTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BPKFile.ReadReverse(0, 2), 0);
                Anim.AlphaFrames = ReadKeyframe(AlphaTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                ColourAnimations.Add(Anim);
            }

            BPKFile.Close();
            FileName = filename;
        }
        /// <summary>
        /// Save the BPK file
        /// </summary>
        /// <param name="Filename">Filename</param>
        public void Save(string Filename = null)
        {
            if (FileName == null && Filename == null)
                throw new Exception("No Filename has been given");
            else if (Filename != null)
                FileName = Filename;

            string Padding = "Hack.io.BPK © Super Hackio Incorporated 2020";
            FileStream BTPFile = new FileStream(Filename, FileMode.Create);

            BTPFile.WriteString(Magic);
            BTPFile.Write(new byte[8] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x01 }, 0, 8);
            BTPFile.Write(new byte[16] { 0x53, 0x56, 0x52, 0x31, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 16);
            BTPFile.WriteString(Magic2);
            BTPFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BTPFile.WriteByte((byte)Loop);
            BTPFile.WriteByte(0xFF);
            BTPFile.WriteByte(0xFF);
            BTPFile.WriteByte(0xFF);
            BTPFile.WriteReverse(BitConverter.GetBytes(Time), 0, 2);

            List<short> RedTable = new List<short>();
            List<short> GreenTable = new List<short>();
            List<short> BlueTable = new List<short>();
            List<short> AlphaTable = new List<short>();

            for (int i = 0; i < ColourAnimations.Count; i++)
            {
                FindMatch(ref RedTable, ColourAnimations[i].RedFrames);
                FindMatch(ref GreenTable, ColourAnimations[i].GreenFrames);
                FindMatch(ref BlueTable, ColourAnimations[i].BlueFrames);
                FindMatch(ref AlphaTable, ColourAnimations[i].AlphaFrames);
            }

            BTPFile.WriteReverse(BitConverter.GetBytes((ushort)ColourAnimations.Count), 0, 2);
            BTPFile.WriteReverse(BitConverter.GetBytes((ushort)RedTable.Count), 0, 2);
            BTPFile.WriteReverse(BitConverter.GetBytes((ushort)GreenTable.Count), 0, 2);
            BTPFile.WriteReverse(BitConverter.GetBytes((ushort)BlueTable.Count), 0, 2);
            BTPFile.WriteReverse(BitConverter.GetBytes((ushort)AlphaTable.Count), 0, 2);

            int offs = 0x40;
            BTPFile.WriteReverse(BitConverter.GetBytes(offs), 0, 4);
            offs += ColourAnimations.Count * 4 * 3 * 2;
            #region Padding
            while (offs % 4 != 0)
                offs++;
            #endregion
            int RedOffset = offs;
            offs += RedTable.Count * 2;
            #region Padding
            while (offs % 4 != 0)
                offs++;
            #endregion
            int GreenOffset = offs;
            offs += GreenTable.Count * 2;
            #region Padding
            while (offs % 4 != 0)
                offs++;
            #endregion
            int BlueOffset = offs;
            offs += BlueTable.Count * 2;
            #region Padding
            while (offs % 4 != 0)
                offs++;
            #endregion
            int AlphaOffset = offs;
            offs += AlphaTable.Count * 2;
            #region Padding
            while (offs % 4 != 0)
                offs++;
            #endregion
            BTPFile.WriteReverse(BitConverter.GetBytes(offs), 0, 4);
            offs += ColourAnimations.Count * 2;
            #region Padding
            while (offs % 4 != 0)
                offs++;
            #endregion
            BTPFile.WriteReverse(BitConverter.GetBytes(offs), 0, 4);
            BTPFile.WriteReverse(BitConverter.GetBytes(RedOffset), 0, 4);
            BTPFile.WriteReverse(BitConverter.GetBytes(GreenOffset), 0, 4);
            BTPFile.WriteReverse(BitConverter.GetBytes(BlueOffset), 0, 4);
            BTPFile.WriteReverse(BitConverter.GetBytes(AlphaOffset), 0, 4);

            #region Padding
            int PadCount = 0;
            while (BTPFile.Position % 16 != 0)
                BTPFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            for (int i = 0; i < ColourAnimations.Count; i++)
            {
                BTPFile.WriteReverse(BitConverter.GetBytes((short)ColourAnimations[i].RedFrames.Count), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref RedTable, ColourAnimations[i].RedFrames)), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes((short)(ColourAnimations[i].RedFrames.Any(R => R.IngoingTangent != R.OutgoingTangent) ? 1 : 1)), 0, 2);

                BTPFile.WriteReverse(BitConverter.GetBytes((short)ColourAnimations[i].GreenFrames.Count), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref GreenTable, ColourAnimations[i].GreenFrames)), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes((short)(ColourAnimations[i].GreenFrames.Any(G => G.IngoingTangent != G.OutgoingTangent) ? 1 : 1)), 0, 2);

                BTPFile.WriteReverse(BitConverter.GetBytes((short)ColourAnimations[i].BlueFrames.Count), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref BlueTable, ColourAnimations[i].BlueFrames)), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes((short)(ColourAnimations[i].BlueFrames.Any(B => B.IngoingTangent != B.OutgoingTangent) ? 1 : 1)), 0, 2);

                BTPFile.WriteReverse(BitConverter.GetBytes((short)ColourAnimations[i].AlphaFrames.Count), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref AlphaTable, ColourAnimations[i].AlphaFrames)), 0, 2);
                BTPFile.WriteReverse(BitConverter.GetBytes((short)(ColourAnimations[i].AlphaFrames.Any(A => A.IngoingTangent != A.OutgoingTangent) ? 1 : 1)), 0, 2);
            }

            #region Padding
            PadCount = 0;
            while (BTPFile.Position % 4 != 0)
                BTPFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            for (int i = 0; i < RedTable.Count; i++)
                BTPFile.WriteReverse(BitConverter.GetBytes(RedTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BTPFile.Position % 4 != 0)
                BTPFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            for (int i = 0; i < GreenTable.Count; i++)
                BTPFile.WriteReverse(BitConverter.GetBytes(GreenTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BTPFile.Position % 4 != 0)
                BTPFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            for (int i = 0; i < BlueTable.Count; i++)
                BTPFile.WriteReverse(BitConverter.GetBytes(BlueTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BTPFile.Position % 4 != 0)
                BTPFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            for (int i = 0; i < AlphaTable.Count; i++)
                BTPFile.WriteReverse(BitConverter.GetBytes(AlphaTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BTPFile.Position % 4 != 0)
                BTPFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            string[] matnames = new string[ColourAnimations.Count];
            for (int i = 0; i < ColourAnimations.Count; i++)
            {
                BTPFile.WriteReverse(BitConverter.GetBytes(ColourAnimations[i].RemapID), 0, 2);
                matnames[i] = ColourAnimations[i].MaterialName;
            }

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
            BTPFile.Position = 0x24;
            BTPFile.WriteReverse(BitConverter.GetBytes((uint)(BTPFile.Length - 0x20)), 0, 4);

            BTPFile.Close();
        }

        /// <summary>
        /// BPK Looping Modes
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
            public ushort RemapID { get; set; }
            /// <summary>
            /// List of Red Keyframes
            /// </summary>
            public List<Keyframe> RedFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Green Keyframes
            /// </summary>
            public List<Keyframe> GreenFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Blue Keyframes
            /// </summary>
            public List<Keyframe> BlueFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Alpha Keyframes
            /// </summary>
            public List<Keyframe> AlphaFrames { get; set; } = new List<Keyframe>();

            /// <summary>
            /// Create an empty animation
            /// </summary>
            public Animation() { }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"{MaterialName} [{RedFrames.Count}|{GreenFrames.Count}|{BlueFrames.Count}|{AlphaFrames.Count}]";

            /// <summary>
            /// Animation Keyframe Data
            /// </summary>
            public class Keyframe
            {
                /// <summary>
                /// The Time in the timeline that this keyframe is assigned to
                /// </summary>
                public short Time { get; set; }
                /// <summary>
                /// The Value to set to
                /// </summary>
                public short Value { get; set; }
                /// <summary>
                /// Tangents affect the interpolation between two consecutive keyframes
                /// </summary>
                public short IngoingTangent { get; set; }
                /// <summary>
                /// Tangents affect the interpolation between two consecutive keyframes
                /// </summary>
                public short OutgoingTangent { get; set; }

                /// <summary>
                /// Create an Empty Keyframe
                /// </summary>
                public Keyframe() { }
                /// <summary>
                /// 
                /// </summary>
                /// <returns></returns>
                public override string ToString() => $"Time: {Time} [{Value}{(IngoingTangent != 0 ? $"|{IngoingTangent}":"")}]";
            }
        }
        
        private List<Animation.Keyframe> ReadKeyframe(List<short> Data, float Scale, double Count, double Index, int TangentType)
        {
            List<Animation.Keyframe> keyframes = new List<Animation.Keyframe>();

            if (Count == 1)
                keyframes.Add(new Animation.Keyframe() { Time = 0, Value = (short)(Data[(int)Index] * Scale), IngoingTangent = 0, OutgoingTangent = 0 });
            else
            {
                if (TangentType == 0x00)
                {
                    for (double i = Index; i < Index + 3 * Count; i += 3)
                    {
                        float Tangents = Data[(int)i + 2] * Scale;
                        keyframes.Add(new Animation.Keyframe() { Time = (short)Data[(int)i + 0], Value = (short)(Data[(int)i + 1] * Scale), IngoingTangent = (short)Tangents, OutgoingTangent = (short)Tangents });
                    }
                }
                else if (TangentType == 0x01)
                {
                    for (double i = Index; i < Index + 4 * Count; i += 4)
                        keyframes.Add(new Animation.Keyframe() { Time = (short)Data[(int)i + 0], Value = (short)(Data[(int)i + 1] * Scale), IngoingTangent = (short)(Data[(int)i + 2] * Scale), OutgoingTangent = (short)(Data[(int)i + 3] * Scale)});

                }
            }
            return keyframes;
        }


        private short FindMatch(ref List<short> FullList, List<Animation.Keyframe> sequence)
        {
            List<short> currentSequence = new List<short>();
            if (sequence.Count == 1)
                currentSequence.Add(sequence[0].Value);
            else
            {
                for (int i = 0; i < sequence.Count; i++)
                {
                    currentSequence.Add(sequence[i].Time);
                    currentSequence.Add(sequence[i].Value);
                    currentSequence.Add(sequence[i].IngoingTangent);
                    currentSequence.Add(sequence[i].OutgoingTangent);
                }
            }
            if (!FullList.ContainsSubsequence(currentSequence))
            {
                FullList.AddRange(currentSequence);
            }

            return (short)FullList.SubListIndex(0, currentSequence);
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
    }
}
