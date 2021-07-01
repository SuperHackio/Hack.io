using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hack.io.Util;
using static Hack.io.J3D.J3DGraph;

namespace Hack.io.BRK
{
    /// <summary>
    /// Binary Register Keyframes<para/>Register Colour and Constant Colour animation
    /// </summary>
    public class BRK
    {
        /// <summary>
        /// Filename of this BRK file.<para/>Set using <see cref="Save(string)"/>;
        /// </summary>
        public string Name { get; set; } = null;
        /// <summary>
        /// Loop Mode of the BRK animation. See the <seealso cref="LoopMode"/> enum for values
        /// </summary>
        public LoopMode Loop { get; set; } = LoopMode.ONCE;
        /// <summary>
        /// Length of the animation in Frames. (Game Framerate = 1 second)
        /// </summary>
        public ushort Time { get; set; }
        /// <summary>
        /// Animations that apply to the Colour Registers
        /// </summary>
        public List<Animation> RegisterColourAnimations { get; set; } = new List<Animation>();
        /// <summary>
        /// Animations that apply to the Colour Constants
        /// </summary>
        public List<Animation> ConstantColourAnimations { get; set; } = new List<Animation>();

        private const string Magic = "J3D1brk1";
        private const string Magic2 = "TRK1";

        /// <summary>
        /// Create an Empty BRK
        /// </summary>
        public BRK() { }
        /// <summary>
        /// Open a BRK from a file
        /// </summary>
        /// <param name="filename">File to open</param>
        public BRK(string filename)
        {
            FileStream BRKFile = new FileStream(filename, FileMode.Open);
            Read(BRKFile);
            BRKFile.Close();
            Name = filename;
        }
        /// <summary>
        /// Create a BRK from a Stream.
        /// </summary>
        /// <param name="BRKStream">Stream containing the BRK</param>
        /// <param name="filename">Name to give this BRK file</param>
        public BRK(Stream BRKStream, string filename = null)
        {
            Read(BRKStream);
            Name = filename;
        }
        /// <summary>
        /// Read a BRK from an Archive File (RARCFile)
        /// </summary>
        /// <param name="ArchiveFile">File that is supposedly a BRK File</param>
        public BRK(RARC.RARC.File ArchiveFile)
        {
            Read((MemoryStream)ArchiveFile);
            Name = ArchiveFile.Name;
        }
        /// <summary>
        /// Save the BRK to a file
        /// </summary>
        /// <param name="Filename">New file to save to</param>
        public void Save(string Filename = null)
        {
            if (Name == null && Filename == null)
                throw new Exception("No Filename has been given");
            else if (Filename != null)
                Name = Filename;

            FileStream BRKFile = new FileStream(Name, FileMode.Create);

            Write(BRKFile);

            BRKFile.Close();
        }
        /// <summary>
        /// Save the BRK file to a memorystream
        /// </summary>
        /// <returns></returns>
        public MemoryStream Save()
        {
            MemoryStream MS = new MemoryStream();
            Write(MS);
            return MS;
        }
        /// <summary>
        /// Generates a 2 byte hash from a string
        /// </summary>
        /// <param name="Input">string to convert</param>
        /// <returns>hashed string</returns>
        public ushort StringToHash(string Input)
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
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{(Name == null ? "Unnamed BRK file": new FileInfo(Name).Name)} {(RegisterColourAnimations.Count > 0 ? $"[{RegisterColourAnimations.Count} Register Animations] " : "")}{(ConstantColourAnimations.Count > 0 ? $"[{ConstantColourAnimations.Count} Constant Animations]" : "")}";

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
            /// Technically unknown. It's guessed to be a RegisterID
            /// </summary>
            public byte RegisterID { get; set; }
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
            public override string ToString() => $"{MaterialName} - Register {RegisterID} [{RedFrames.Count}|{GreenFrames.Count}|{BlueFrames.Count}|{AlphaFrames.Count}]";

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
                /// Read a keyframe from a file
                /// </summary>
                /// <param name="BRKFile">The file to read from</param>
                /// <param name="IsSingular">Set to true if there is only 1 keyframe</param>
                public Keyframe(Stream BRKFile, bool IsSingular)
                {
                    if (IsSingular)
                        Value = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                    else
                    {
                        Time = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                        Value = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                        IngoingTangent = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                        OutgoingTangent = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                    }
                }
                /// <summary>
                /// 
                /// </summary>
                /// <returns></returns>
                public override string ToString() => $"Time: {Time} [{Value}|{IngoingTangent}|{OutgoingTangent}]";
                /// <summary>
                /// 
                /// </summary>
                /// <param name="obj"></param>
                /// <returns></returns>
                public override bool Equals(object obj)
                {
                    if (obj is Keyframe compa)
                        return Time == compa.Time && Value == compa.Value && IngoingTangent == compa.IngoingTangent && OutgoingTangent == compa.OutgoingTangent;
                    else
                        return base.Equals(obj);
                }
                /// <summary>
                /// 
                /// </summary>
                /// <returns></returns>
                public override int GetHashCode()
                {
                    return base.GetHashCode();
                }
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
                        keyframes.Add(new Animation.Keyframe() { Time = (short)Data[(int)i + 0], Value = (short)(Data[(int)i + 1] * Scale), IngoingTangent = (short)(Data[(int)i + 2] * Scale), OutgoingTangent = (short)(Data[(int)i + 3] * Scale) });
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

        private void Read(Stream BRKFile)
        {
            if (BRKFile.ReadString(8) != Magic)
                throw new Exception("Invalid Identifier. Expected \"J3D1brk1\"");

            uint Filesize = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0);
            uint SectionCount = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0);
            if (SectionCount != 1)
                throw new Exception(SectionCount > 1 ? "More than 1 section is in this BRK! Please send it to Super Hackio for investigation" : "There are no sections in this BRK!");

            BRKFile.Seek(0x10, SeekOrigin.Current);
            uint TRKStart = (uint)BRKFile.Position;

            if (BRKFile.ReadString(4) != Magic2)
                throw new Exception("Invalid Identifier. Expected \"TRK1\"");

            uint TRK1Length = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0);
            Loop = (LoopMode)BRKFile.ReadByte();
            BRKFile.ReadByte();
            Time = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0);

            ushort RegisterCount = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0), ConstantCount = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0),
                RegisterRedParts = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0), RegisterGreenParts = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0),
                RegisterBlueParts = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0), RegisterAlphaParts = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0),
                ConstantRedParts = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0), ConstantGreenParts = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0),
                ConstantBlueParts = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0), ConstantAlphaParts = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0);

            uint RegisterAnimOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart, ConstantAnimOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart,
                RegisterIndexOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart, ConstantIndexOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart,
                RegisterSTOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart, ConstantSTOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart,

                RegisterRedOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart, RegisterGreenOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart,
                RegisterBlueOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart, RegisterAlphaOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart,
                ConstantRedOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart, ConstantGreenOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart,
                ConstantBlueOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart, ConstantAlphaOffset = BitConverter.ToUInt32(BRKFile.ReadReverse(0, 4), 0) + TRKStart;

            BRKFile.Seek(RegisterIndexOffset, SeekOrigin.Begin);
            List<ushort> RegisterIDs = new List<ushort>();
            for (int i = 0; i < RegisterCount; i++)
                RegisterIDs.Add(BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0));

            BRKFile.Seek(ConstantIndexOffset, SeekOrigin.Begin);
            List<ushort> ConstantIDs = new List<ushort>();
            for (int i = 0; i < ConstantCount; i++)
                ConstantIDs.Add(BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0));

            BRKFile.Seek(RegisterSTOffset, SeekOrigin.Begin);
            List<KeyValuePair<ushort, string>> RegisterStrings = new List<KeyValuePair<ushort, string>>();
            ushort StringCount = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0);
            BRKFile.Seek(2, SeekOrigin.Current);
            for (int i = 0; i < StringCount; i++)
            {
                ushort hash = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0);
                long PausePosition = BRKFile.Position + 2;
                BRKFile.Seek(RegisterSTOffset + BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0), SeekOrigin.Begin);
                RegisterStrings.Add(new KeyValuePair<ushort, string>(hash, BRKFile.ReadString()));
                BRKFile.Position = PausePosition;
            }

            BRKFile.Seek(ConstantSTOffset, SeekOrigin.Begin);
            List<KeyValuePair<ushort, string>> ConstantStrings = new List<KeyValuePair<ushort, string>>();
            StringCount = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0);
            BRKFile.Seek(2, SeekOrigin.Current);
            for (int i = 0; i < StringCount; i++)
            {
                ushort hash = BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0);
                long PausePosition = BRKFile.Position + 2;
                BRKFile.Seek(ConstantSTOffset + BitConverter.ToUInt16(BRKFile.ReadReverse(0, 2), 0), SeekOrigin.Begin);
                ConstantStrings.Add(new KeyValuePair<ushort, string>(hash, BRKFile.ReadString()));
                BRKFile.Position = PausePosition;
            }

            List<short> RegisterRedTable = new List<short>();
            List<short> RegisterGreenTable = new List<short>();
            List<short> RegisterBlueTable = new List<short>();
            List<short> RegisterAlphaTable = new List<short>();

            BRKFile.Seek(RegisterRedOffset, SeekOrigin.Begin);
            for (int i = 0; i < RegisterRedParts; i++)
                RegisterRedTable.Add(BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0));

            BRKFile.Seek(RegisterGreenOffset, SeekOrigin.Begin);
            for (int i = 0; i < RegisterGreenParts; i++)
                RegisterGreenTable.Add(BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0));

            BRKFile.Seek(RegisterBlueOffset, SeekOrigin.Begin);
            for (int i = 0; i < RegisterBlueParts; i++)
                RegisterBlueTable.Add(BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0));

            BRKFile.Seek(RegisterAlphaOffset, SeekOrigin.Begin);
            for (int i = 0; i < RegisterAlphaParts; i++)
                RegisterAlphaTable.Add(BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0));

            List<short> ConstantRedTable = new List<short>();
            List<short> ConstantGreenTable = new List<short>();
            List<short> ConstantBlueTable = new List<short>();
            List<short> ConstantAlphaTable = new List<short>();

            BRKFile.Seek(ConstantRedOffset, SeekOrigin.Begin);
            for (int i = 0; i < ConstantRedParts; i++)
                ConstantRedTable.Add(BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0));

            BRKFile.Seek(ConstantGreenOffset, SeekOrigin.Begin);
            for (int i = 0; i < ConstantGreenParts; i++)
                ConstantGreenTable.Add(BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0));

            BRKFile.Seek(ConstantBlueOffset, SeekOrigin.Begin);
            for (int i = 0; i < ConstantBlueParts; i++)
                ConstantBlueTable.Add(BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0));

            BRKFile.Seek(ConstantAlphaOffset, SeekOrigin.Begin);
            for (int i = 0; i < ConstantAlphaParts; i++)
                ConstantAlphaTable.Add(BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0));

            //---------------------------------------------------------------------------------------

            short KeyFrameCount, TargetKeySet, TangentType;
            BRKFile.Seek(RegisterAnimOffset, SeekOrigin.Begin);
            for (int i = 0; i < RegisterCount; i++)
            {
                Animation Anim = new Animation() { MaterialName = RegisterStrings[i].Value };

                KeyFrameCount = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                Anim.RedFrames = ReadKeyframe(RegisterRedTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                Anim.GreenFrames = ReadKeyframe(RegisterGreenTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                Anim.BlueFrames = ReadKeyframe(RegisterBlueTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                Anim.AlphaFrames = ReadKeyframe(RegisterAlphaTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                Anim.RegisterID = (byte)BRKFile.ReadByte();
                while (BRKFile.Position % 4 != 0)
                    BRKFile.Position++;

                RegisterColourAnimations.Add(Anim);
            }

            BRKFile.Seek(ConstantAnimOffset, SeekOrigin.Begin);
            for (int i = 0; i < ConstantCount; i++)
            {
                Animation Anim = new Animation() { MaterialName = ConstantStrings[i].Value };
                KeyFrameCount = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                Anim.RedFrames = ReadKeyframe(ConstantRedTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                Anim.GreenFrames = ReadKeyframe(ConstantGreenTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                Anim.BlueFrames = ReadKeyframe(ConstantBlueTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BRKFile.ReadReverse(0, 2), 0);
                Anim.AlphaFrames = ReadKeyframe(ConstantAlphaTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                Anim.RegisterID = (byte)BRKFile.ReadByte();
                while (BRKFile.Position % 4 != 0)
                    BRKFile.Position++;

                ConstantColourAnimations.Add(Anim);
            }
        }

        private void Write(Stream BRKFile)
        {
            string Padding = "Hack.io.BRK © Super Hackio Incorporated 2020";

            BRKFile.WriteString(Magic);
            BRKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BRKFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);
            BRKFile.WriteString("SVR1");
            BRKFile.Write(new byte[12] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 12);
            long TRKStart = BRKFile.Position;
            BRKFile.WriteString(Magic2);
            BRKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BRKFile.WriteByte((byte)Loop);
            BRKFile.WriteByte(0xFF); //Padding
            BRKFile.WriteReverse(BitConverter.GetBytes(Time), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((ushort)RegisterColourAnimations.Count), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((ushort)ConstantColourAnimations.Count), 0, 2);

            long ColourCountsPausePosition = BRKFile.Position;
            BRKFile.Write(new byte[16] { 0xAA, 0xAA, 0XBB, 0xBB, 0xCC, 0xCC, 0XDD, 0xDD, 0xAA, 0xAA, 0XBB, 0xBB, 0xCC, 0xCC, 0XDD, 0xDD }, 0, 16);
            long DataOffsetsPausePosition = BRKFile.Position;
            for (int i = 0; i < 14; i++)
                BRKFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4);

            #region Padding
            int PadCount = 0;
            while (BRKFile.Position % 32 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            long RegisterAnimStartPausePosition = BRKFile.Position;
            for (int i = 0; i < RegisterColourAnimations.Count; i++)
            {
                BRKFile.WriteReverse(BitConverter.GetBytes((short)RegisterColourAnimations[i].RedFrames.Count), 0, 2);
                BRKFile.WriteReverse(new byte[2], 0, 2);
                BRKFile.WriteReverse(BitConverter.GetBytes((short)(RegisterColourAnimations[i].RedFrames.Any(R => R.IngoingTangent != R.OutgoingTangent) ? 1 : 1)), 0, 2);

                BRKFile.WriteReverse(BitConverter.GetBytes((short)RegisterColourAnimations[i].GreenFrames.Count), 0, 2);
                BRKFile.WriteReverse(new byte[2], 0, 2);
                BRKFile.WriteReverse(BitConverter.GetBytes((short)(RegisterColourAnimations[i].GreenFrames.Any(G => G.IngoingTangent != G.OutgoingTangent) ? 1 : 1)), 0, 2);

                BRKFile.WriteReverse(BitConverter.GetBytes((short)RegisterColourAnimations[i].BlueFrames.Count), 0, 2);
                BRKFile.WriteReverse(new byte[2], 0, 2);
                BRKFile.WriteReverse(BitConverter.GetBytes((short)(RegisterColourAnimations[i].BlueFrames.Any(B => B.IngoingTangent != B.OutgoingTangent) ? 1 : 1)), 0, 2);

                BRKFile.WriteReverse(BitConverter.GetBytes((short)RegisterColourAnimations[i].AlphaFrames.Count), 0, 2);
                BRKFile.WriteReverse(new byte[2], 0, 2);
                BRKFile.WriteReverse(BitConverter.GetBytes((short)(RegisterColourAnimations[i].AlphaFrames.Any(A => A.IngoingTangent != A.OutgoingTangent) ? 1 : 1)), 0, 2);

                BRKFile.WriteByte(RegisterColourAnimations[i].RegisterID);
                while (BRKFile.Position % 4 != 0)
                    BRKFile.WriteByte(0xFF);
            }

            long ConstantAnimStartPausePosition = BRKFile.Position;
            for (int i = 0; i < ConstantColourAnimations.Count; i++)
            {
                BRKFile.WriteReverse(BitConverter.GetBytes((short)ConstantColourAnimations[i].RedFrames.Count), 0, 2);
                BRKFile.WriteReverse(new byte[2], 0, 2);
                BRKFile.WriteReverse(BitConverter.GetBytes((short)(ConstantColourAnimations[i].RedFrames.Any(R => R.IngoingTangent != R.OutgoingTangent) ? 1 : 1)), 0, 2);

                BRKFile.WriteReverse(BitConverter.GetBytes((short)ConstantColourAnimations[i].GreenFrames.Count), 0, 2);
                BRKFile.WriteReverse(new byte[2], 0, 2);
                BRKFile.WriteReverse(BitConverter.GetBytes((short)(ConstantColourAnimations[i].GreenFrames.Any(G => G.IngoingTangent != G.OutgoingTangent) ? 1 : 1)), 0, 2);

                BRKFile.WriteReverse(BitConverter.GetBytes((short)ConstantColourAnimations[i].BlueFrames.Count), 0, 2);
                BRKFile.WriteReverse(new byte[2], 0, 2);
                BRKFile.WriteReverse(BitConverter.GetBytes((short)(ConstantColourAnimations[i].BlueFrames.Any(B => B.IngoingTangent != B.OutgoingTangent) ? 1 : 1)), 0, 2);

                BRKFile.WriteReverse(BitConverter.GetBytes((short)ConstantColourAnimations[i].AlphaFrames.Count), 0, 2);
                BRKFile.WriteReverse(new byte[2], 0, 2);
                BRKFile.WriteReverse(BitConverter.GetBytes((short)(ConstantColourAnimations[i].AlphaFrames.Any(A => A.IngoingTangent != A.OutgoingTangent) ? 1 : 1)), 0, 2);

                BRKFile.WriteByte(ConstantColourAnimations[i].RegisterID);
                while (BRKFile.Position % 4 != 0)
                    BRKFile.WriteByte(0xFF);
            }

            #region Register Keyframe Data

            List<short> RegisterRedTable = new List<short>();
            List<short> RegisterGreenTable = new List<short>();
            List<short> RegisterBlueTable = new List<short>();
            List<short> RegisterAlphaTable = new List<short>();

            for (int i = 0; i < RegisterColourAnimations.Count; i++)
            {
                FindMatch(ref RegisterRedTable, RegisterColourAnimations[i].RedFrames);
                FindMatch(ref RegisterGreenTable, RegisterColourAnimations[i].GreenFrames);
                FindMatch(ref RegisterBlueTable, RegisterColourAnimations[i].BlueFrames);
                FindMatch(ref RegisterAlphaTable, RegisterColourAnimations[i].AlphaFrames);
            }
            long RegisterRedOffset = BRKFile.Position;
            for (int i = 0; i < RegisterRedTable.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes(RegisterRedTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            long RegisterGreenOffset = BRKFile.Position;
            for (int i = 0; i < RegisterGreenTable.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes(RegisterGreenTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            long RegisterBlueOffset = BRKFile.Position;
            for (int i = 0; i < RegisterBlueTable.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes(RegisterBlueTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            long RegisterAlphaOffset = BRKFile.Position;
            for (int i = 0; i < RegisterAlphaTable.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes(RegisterAlphaTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            #endregion

            #region Constant Keyframe Data

            List<short> ConstantRedTable = new List<short>();
            List<short> ConstantGreenTable = new List<short>();
            List<short> ConstantBlueTable = new List<short>();
            List<short> ConstantAlphaTable = new List<short>();

            for (int i = 0; i < ConstantColourAnimations.Count; i++)
            {
                FindMatch(ref ConstantRedTable, ConstantColourAnimations[i].RedFrames);
                FindMatch(ref ConstantGreenTable, ConstantColourAnimations[i].GreenFrames);
                FindMatch(ref ConstantBlueTable, ConstantColourAnimations[i].BlueFrames);
                FindMatch(ref ConstantAlphaTable, ConstantColourAnimations[i].AlphaFrames);
            }
            long ConstantRedOffset = BRKFile.Position;
            for (int i = 0; i < ConstantRedTable.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes(ConstantRedTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            long ConstantGreenOffset = BRKFile.Position;
            for (int i = 0; i < ConstantGreenTable.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes(ConstantGreenTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            long ConstantBlueOffset = BRKFile.Position;
            for (int i = 0; i < ConstantBlueTable.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes(ConstantBlueTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            long ConstantAlphaOffset = BRKFile.Position;
            for (int i = 0; i < ConstantAlphaTable.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes(ConstantAlphaTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            #endregion

            long RegisterIndexPosition = BRKFile.Position;
            for (int i = 0; i < RegisterColourAnimations.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes((short)i), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            long ConstantIndexPosition = BRKFile.Position;
            for (int i = 0; i < ConstantColourAnimations.Count; i++)
                BRKFile.WriteReverse(BitConverter.GetBytes((short)i), 0, 2);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            #region String Tables
            List<string> RegisterStrings = new List<string>();
            for (int i = 0; i < RegisterColourAnimations.Count; i++)
                RegisterStrings.Add(RegisterColourAnimations[i].MaterialName);

            long RegisterST = BRKFile.Position;
            BRKFile.WriteReverse(BitConverter.GetBytes((short)RegisterStrings.Count), 0, 2);
            BRKFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
            for (int i = 0; i < RegisterStrings.Count; i++)
            {
                BRKFile.WriteReverse(BitConverter.GetBytes(StringToHash(RegisterStrings[i])), 0, 2);
                int StringOffset = RegisterStrings.Count * 4 + 4;
                for (int j = 0; j < i; j++)
                {
                    StringOffset += Encoding.GetEncoding(932).GetBytes(RegisterStrings[j]).Length + 1;
                }
                BRKFile.WriteReverse(BitConverter.GetBytes((short)StringOffset), 0, 2);
            }
            for (int i = 0; i < RegisterStrings.Count; i++)
                BRKFile.WriteString(RegisterStrings[i], 0x00);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 16 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            List<string> ConstantStrings = new List<string>();
            for (int i = 0; i < ConstantColourAnimations.Count; i++)
                ConstantStrings.Add(ConstantColourAnimations[i].MaterialName);

            long ConstantST = BRKFile.Position;
            BRKFile.WriteReverse(BitConverter.GetBytes((short)ConstantStrings.Count), 0, 2);
            BRKFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
            for (int i = 0; i < ConstantStrings.Count; i++)
            {
                BRKFile.WriteReverse(BitConverter.GetBytes(StringToHash(ConstantStrings[i])), 0, 2);
                int StringOffset = ConstantStrings.Count * 4 + 4;
                for (int j = 0; j < i; j++)
                {
                    StringOffset += Encoding.GetEncoding(932).GetBytes(ConstantStrings[j]).Length + 1;
                }
                BRKFile.WriteReverse(BitConverter.GetBytes((short)StringOffset), 0, 2);
            }
            for (int i = 0; i < ConstantStrings.Count; i++)
                BRKFile.WriteString(ConstantStrings[i], 0x00);

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 16 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion
            #endregion

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 32 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            BRKFile.Position = 0x08;
            BRKFile.WriteReverse(BitConverter.GetBytes((int)BRKFile.Length), 0, 4);

            BRKFile.Seek(TRKStart + 4, SeekOrigin.Begin);
            BRKFile.WriteReverse(BitConverter.GetBytes((int)(BRKFile.Length - TRKStart)), 0, 4);

            BRKFile.Seek(ColourCountsPausePosition, SeekOrigin.Begin);

            #region Register Colour Counts
            BRKFile.WriteReverse(BitConverter.GetBytes((short)RegisterRedTable.Count), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((short)RegisterGreenTable.Count), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((short)RegisterBlueTable.Count), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((short)RegisterAlphaTable.Count), 0, 2);
            #endregion

            #region Constant Colour Counts
            BRKFile.WriteReverse(BitConverter.GetBytes((short)ConstantRedTable.Count), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((short)ConstantGreenTable.Count), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((short)ConstantBlueTable.Count), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((short)ConstantAlphaTable.Count), 0, 2);
            #endregion

            #region Offsets
            int temp = RegisterColourAnimations.Count > 0 ? ((int)RegisterAnimStartPausePosition - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = ConstantColourAnimations.Count > 0 ? ((int)ConstantAnimStartPausePosition - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = RegisterColourAnimations.Count > 0 ? ((int)RegisterIndexPosition - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = ConstantColourAnimations.Count > 0 ? ((int)ConstantIndexPosition - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            BRKFile.WriteReverse(BitConverter.GetBytes((int)RegisterST - (int)TRKStart), 0, 4);
            BRKFile.WriteReverse(BitConverter.GetBytes((int)ConstantST - (int)TRKStart), 0, 4);

            temp = RegisterColourAnimations.Count > 0 ? ((int)RegisterRedOffset - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = RegisterColourAnimations.Count > 0 ? ((int)RegisterGreenOffset - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = RegisterColourAnimations.Count > 0 ? ((int)RegisterBlueOffset - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = RegisterColourAnimations.Count > 0 ? ((int)RegisterAlphaOffset - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = ConstantColourAnimations.Count > 0 ? ((int)ConstantRedOffset - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = ConstantColourAnimations.Count > 0 ? ((int)ConstantGreenOffset - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = ConstantColourAnimations.Count > 0 ? ((int)ConstantBlueOffset - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);

            temp = ConstantColourAnimations.Count > 0 ? ((int)ConstantAlphaOffset - (int)TRKStart) : 0;
            BRKFile.WriteReverse(BitConverter.GetBytes(temp), 0, 4);
            #endregion

            #region Value Indexing

            BRKFile.Seek(RegisterAnimStartPausePosition, SeekOrigin.Begin);
            for (int i = 0; i < RegisterColourAnimations.Count; i++)
            {
                BRKFile.Position += 2;
                BRKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref RegisterRedTable, RegisterColourAnimations[i].RedFrames)), 0, 2);
                BRKFile.Position += 2;

                BRKFile.Position += 2;
                BRKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref RegisterGreenTable, RegisterColourAnimations[i].GreenFrames)), 0, 2);
                BRKFile.Position += 2;

                BRKFile.Position += 2;
                BRKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref RegisterBlueTable, RegisterColourAnimations[i].BlueFrames)), 0, 2);
                BRKFile.Position += 2;

                BRKFile.Position += 2;
                BRKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref RegisterAlphaTable, RegisterColourAnimations[i].AlphaFrames)), 0, 2);
                BRKFile.Position += 3;

                while (BRKFile.Position % 4 != 0)
                    BRKFile.Position++;
            }

            BRKFile.Seek(ConstantAnimStartPausePosition, SeekOrigin.Begin);
            for (int i = 0; i < ConstantColourAnimations.Count; i++)
            {
                BRKFile.Position += 2;
                BRKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref ConstantRedTable, ConstantColourAnimations[i].RedFrames)), 0, 2);
                BRKFile.Position += 2;

                BRKFile.Position += 2;
                BRKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref ConstantGreenTable, ConstantColourAnimations[i].GreenFrames)), 0, 2);
                BRKFile.Position += 2;

                BRKFile.Position += 2;
                BRKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref ConstantBlueTable, ConstantColourAnimations[i].BlueFrames)), 0, 2);
                BRKFile.Position += 2;

                BRKFile.Position += 2;
                BRKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref ConstantAlphaTable, ConstantColourAnimations[i].AlphaFrames)), 0, 2);
                BRKFile.Position += 3;

                while (BRKFile.Position % 4 != 0)
                    BRKFile.Position++;
            }
            #endregion
        }

        //=====================================================================

        /// <summary>
        /// Cast a BRK to a RARCFile
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator RARC.RARC.File(BRK x)
        {
            return new RARC.RARC.File(x.Name, x.Save());
        }

        /// <summary>
        /// Cast a RARCFile to a BRK
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator BRK(RARC.RARC.File x)
        {
            return new BRK((MemoryStream)x, x.Name);
        }

        //=====================================================================
    }
}