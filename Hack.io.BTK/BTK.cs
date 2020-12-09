using Hack.io.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Hack.io.J3D.J3DGraph;

namespace Hack.io.BTK
{
    /// <summary>
    /// Binary Texture SRT Keys.
    /// <para/>Texture Scale, Rotation, and Translation Animation.
    /// </summary>
    public class BTK
    {
        /// <summary>
        /// Filename of this BRK file.<para/>Set using <see cref="Save(string)"/>;
        /// </summary>
        public string Name { get; private set; } = null;
        /// <summary>
        /// Loop Mode of the BRK animation. See the <seealso cref="LoopMode"/> enum for values
        /// </summary>
        public LoopMode Loop { get; set; } = LoopMode.ONCE;
        /// <summary>
        /// Length of the animation in Frames. (Game Framerate = 1 second)
        /// </summary>
        public ushort Time { get; set; }
        /// <summary>
        /// Unknown.
        /// </summary>
        public sbyte RotationMultiplier { get; set; }
        /// <summary>
        /// Animations that apply to the SRT values of a Texture
        /// </summary>
        public List<Animation> TextureAnimations { get; set; } = new List<Animation>();

        private readonly static string Magic = "J3D1btk1";
        private readonly static string Magic2 = "TTK1";

        /// <summary>
        /// Create an Empty BTK
        /// </summary>
        public BTK() { }
        /// <summary>
        /// Open a BTK from a file
        /// </summary>
        /// <param name="filename">File to open</param>
        public BTK(string filename)
        {
            FileStream BTKFile = new FileStream(filename, FileMode.Open);

            Read(BTKFile);

            BTKFile.Close();
            Name = filename;
        }
        /// <summary>
        /// Create a BTK from a Stream.
        /// </summary>
        /// <param name="BTKStream">Stream containing the BRK</param>
        /// <param name="filename">Name to give this BRK file</param>
        public BTK(Stream BTKStream, string filename = null)
        {
            Read(BTKStream);
            Name = filename;
        }
        /// <summary>
        /// Save the BTK to a file
        /// </summary>
        /// <param name="Filename">New file to save to</param>
        public void Save(string Filename = null)
        {
            if (Name == null && Filename == null)
                throw new Exception("No Filename has been given");
            else if (Filename != null)
                Name = Filename;

            FileStream BTKFile = new FileStream(Name, FileMode.Create);

            Write(BTKFile);

            BTKFile.Close();
        }
        /// <summary>
        /// Save the BTK file to a memorystream
        /// </summary>
        /// <returns></returns>
        public MemoryStream Save()
        {
            MemoryStream MS = new MemoryStream();
            Write(MS);
            return MS;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{(Name == null ? "Unnamed BTK file" : new FileInfo(Name).Name)} {(TextureAnimations.Count > 0 ? $"[{TextureAnimations.Count} Texture SRT Animations] " : "")}";

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
            /// ID of the texture within the material that this animation affects.
            /// </summary>
            public byte MaterialTextureID { get; set; }
            /// <summary>
            /// The Origin of Rotation
            /// </summary>
            public float[] Center { get; set; }
            /// <summary>
            /// Unknown
            /// </summary>
            public ushort RemapIndex { get; set; }
            /// <summary>
            /// List of Scale U Keyframes
            /// </summary>
            public List<Keyframe> ScaleUFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Scale V Keyframes
            /// </summary>
            public List<Keyframe> ScaleVFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Scale W Keyframes
            /// </summary>
            public List<Keyframe> ScaleWFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Rotation U Keyframes
            /// </summary>
            public List<Keyframe> RotationUFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Rotation V Keyframes
            /// </summary>
            public List<Keyframe> RotationVFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Rotation W Keyframes
            /// </summary>
            public List<Keyframe> RotationWFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Translation U Keyframes
            /// </summary>
            public List<Keyframe> TranslationUFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Translation V Keyframes
            /// </summary>
            public List<Keyframe> TranslationVFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Translation W Keyframes
            /// </summary>
            public List<Keyframe> TranslationWFrames { get; set; } = new List<Keyframe>();

            /// <summary>
            /// Create an empty animation
            /// </summary>
            public Animation() { }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"{MaterialName} (Texture {MaterialTextureID}) [{TranslationUFrames.Count + TranslationVFrames.Count + TranslationWFrames.Count}|{RotationUFrames.Count + RotationVFrames.Count + RotationWFrames.Count}|{ScaleUFrames.Count + ScaleVFrames.Count + ScaleWFrames.Count}]";

            /// <summary>
            /// Animation Keyframe Data.
            /// <para/>Data is stored as <see cref="float"/>s
            /// </summary>
            public class Keyframe
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

                /// <summary>
                /// Create an Empty Keyframe
                /// </summary>
                public Keyframe() { }
                /// <summary>
                /// 
                /// </summary>
                /// <returns></returns>
                public override string ToString() => $"Time: {Time} [{Value}]";
            }
        }

        private void Read(Stream BTKFile)
        {
            if (BTKFile.ReadString(8) != Magic)
                throw new Exception("Invalid Identifier. Expected \"J3D1btk1\"");

            uint Filesize = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0);
            uint SectionCount = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0);
            if (SectionCount != 1)
                throw new Exception(SectionCount > 1 ? "More than 1 section is in this BTK! Please send it to Super Hackio for investigation" : "There are no sections in this BTK!");

            BTKFile.Seek(0x10, SeekOrigin.Current);
            uint TTKStart = (uint)BTKFile.Position;

            if (BTKFile.ReadString(4) != Magic2)
                throw new Exception("Invalid Identifier. Expected \"TTK1\"");

            uint TRK1Length = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0);
            Loop = (LoopMode)BTKFile.ReadByte();
            RotationMultiplier = (sbyte)BTKFile.ReadByte();

            Time = BitConverter.ToUInt16(BTKFile.ReadReverse(0, 2), 0);
            ushort AnimationCount = (ushort)(BitConverter.ToUInt16(BTKFile.ReadReverse(0, 2), 0) / 3),
                ScaleCount = BitConverter.ToUInt16(BTKFile.ReadReverse(0, 2), 0), RotationCount = BitConverter.ToUInt16(BTKFile.ReadReverse(0, 2), 0), TranslationCount = BitConverter.ToUInt16(BTKFile.ReadReverse(0, 2), 0);

            uint AnimationTableOffset = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0) + TTKStart, RemapTableOffset = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0) + TTKStart,
                MaterialSTOffset = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0) + TTKStart, TextureMapIDTableOffset = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0) + TTKStart,
                TextureCenterTableOffset = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0) + TTKStart, ScaleTableOffset = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0) + TTKStart,
                RotationTableOffset = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0) + TTKStart, TranslationTableOffset = BitConverter.ToUInt32(BTKFile.ReadReverse(0, 4), 0) + TTKStart;

            BTKFile.Seek(MaterialSTOffset, SeekOrigin.Begin);
            List<KeyValuePair<ushort, string>> MaterialNames = new List<KeyValuePair<ushort, string>>();
            ushort StringCount = BitConverter.ToUInt16(BTKFile.ReadReverse(0, 2), 0);
            BTKFile.Seek(2, SeekOrigin.Current);
            for (int i = 0; i < StringCount; i++)
            {
                ushort hash = BitConverter.ToUInt16(BTKFile.ReadReverse(0, 2), 0);
                long PausePosition = BTKFile.Position + 2;
                BTKFile.Seek(MaterialSTOffset + BitConverter.ToUInt16(BTKFile.ReadReverse(0, 2), 0), SeekOrigin.Begin);
                MaterialNames.Add(new KeyValuePair<ushort, string>(hash, BTKFile.ReadString()));
                BTKFile.Position = PausePosition;
            }

            BTKFile.Seek(TextureMapIDTableOffset, SeekOrigin.Begin);
            List<byte> Texture_Index = new List<byte>();
            for (int i = 0; i < AnimationCount; i++)
                Texture_Index.Add((byte)BTKFile.ReadByte());

            BTKFile.Seek(TextureCenterTableOffset, SeekOrigin.Begin);
            List<float[]> Centers = new List<float[]>();
            for (int i = 0; i < AnimationCount; i++)
                Centers.Add(new float[3] { BitConverter.ToSingle(BTKFile.ReadReverse(0, 4), 0), BitConverter.ToSingle(BTKFile.ReadReverse(0, 4), 0), BitConverter.ToSingle(BTKFile.ReadReverse(0, 4), 0) });

            BTKFile.Seek(RemapTableOffset, SeekOrigin.Begin);
            List<ushort> RemapTable = new List<ushort>();
            for (int i = 0; i < AnimationCount; i++)
                RemapTable.Add(BitConverter.ToUInt16(BTKFile.ReadReverse(0, 2), 0));

            List<float> ScaleTable = new List<float>();
            List<short> RotationTable = new List<short>();
            List<float> TranslationTable = new List<float>();

            BTKFile.Seek(ScaleTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < ScaleCount; i++)
                ScaleTable.Add(BitConverter.ToSingle(BTKFile.ReadReverse(0, 4), 0));

            BTKFile.Seek(RotationTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < RotationCount; i++)
                RotationTable.Add(BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0));

            BTKFile.Seek(TranslationTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < TranslationCount; i++)
                TranslationTable.Add(BitConverter.ToSingle(BTKFile.ReadReverse(0, 4), 0));

            BTKFile.Seek(AnimationTableOffset, SeekOrigin.Begin);
            short KeyFrameCount, TargetKeySet, TangentType;
            for (int i = 0; i < AnimationCount; i++)
            {
                Animation Anim = new Animation() { MaterialName = MaterialNames[i].Value, MaterialTextureID = Texture_Index[i], RemapIndex = RemapTable[i], Center = Centers[i] };
                KeyFrameCount = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                Anim.ScaleUFrames = ReadKeyframe(ScaleTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                Anim.RotationUFrames = ReadKeyframe(RotationTable, RotationMultiplier, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                Anim.TranslationUFrames = ReadKeyframe(TranslationTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                Anim.ScaleVFrames = ReadKeyframe(ScaleTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                Anim.RotationVFrames = ReadKeyframe(RotationTable, RotationMultiplier, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                Anim.TranslationVFrames = ReadKeyframe(TranslationTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                Anim.ScaleWFrames = ReadKeyframe(ScaleTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                Anim.RotationWFrames = ReadKeyframe(RotationTable, RotationMultiplier, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BTKFile.ReadReverse(0, 2), 0);
                Anim.TranslationWFrames = ReadKeyframe(TranslationTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                TextureAnimations.Add(Anim);
            }
        }

        private void Write(Stream BTKFile)
        {
            string Padding = "Hack.io.BTK © Super Hackio Incorporated 2020";

            BTKFile.WriteString(Magic);
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BTKFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);
            BTKFile.Write(new byte[16] { 0x53, 0x56, 0x52, 0x31, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 16);
            BTKFile.WriteString(Magic2);
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BTKFile.WriteByte((byte)Loop);
            BTKFile.WriteByte((byte)RotationMultiplier);
            BTKFile.WriteReverse(BitConverter.GetBytes(Time), 0, 2);
            BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations.Count * 3)), 0, 2);
            BTKFile.Write(new byte[2] { 0xDD, 0xDD }, 0, 2);
            BTKFile.Write(new byte[2] { 0xEE, 0xEE }, 0, 2);
            BTKFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
            //Animation Table Offset
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            //Remap Table Offset
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            //Material ST Offset
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            //TexMtxTable Offset
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            //Center Table Offset
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            //Scale Table Offset
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            //Rotation Table Offset
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            //Translation Table Offset
            BTKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);

            #region Padding
            //int PadCount = 0;
            while (BTKFile.Position < 0x80)
                BTKFile.WriteByte(0x00);
            #endregion

            long AnimationTableOffset = BTKFile.Position;
            for (int i = 0; i < TextureAnimations.Count; i++)
            {
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].ScaleUFrames.Count), 0, 2);
                BTKFile.WriteReverse(new byte[2], 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations[i].ScaleUFrames.Any(S => S.IngoingTangent != S.OutgoingTangent) ? 1 : 1)), 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].RotationUFrames.Count), 0, 2);
                BTKFile.WriteReverse(new byte[2], 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations[i].RotationUFrames.Any(S => S.IngoingTangent != S.OutgoingTangent) ? 1 : 1)), 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].TranslationUFrames.Count), 0, 2);
                BTKFile.WriteReverse(new byte[2], 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations[i].TranslationUFrames.Any(S => S.IngoingTangent != S.OutgoingTangent) ? 1 : 1)), 0, 2);

                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].ScaleVFrames.Count), 0, 2);
                BTKFile.WriteReverse(new byte[2], 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations[i].ScaleVFrames.Any(S => S.IngoingTangent != S.OutgoingTangent) ? 1 : 1)), 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].RotationVFrames.Count), 0, 2);
                BTKFile.WriteReverse(new byte[2], 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations[i].RotationVFrames.Any(S => S.IngoingTangent != S.OutgoingTangent) ? 1 : 1)), 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].TranslationVFrames.Count), 0, 2);
                BTKFile.WriteReverse(new byte[2], 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations[i].TranslationVFrames.Any(S => S.IngoingTangent != S.OutgoingTangent) ? 1 : 1)), 0, 2);

                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].ScaleWFrames.Count), 0, 2);
                BTKFile.WriteReverse(new byte[2], 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations[i].ScaleWFrames.Any(S => S.IngoingTangent != S.OutgoingTangent) ? 1 : 1)), 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].RotationWFrames.Count), 0, 2);
                BTKFile.WriteReverse(new byte[2], 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations[i].RotationWFrames.Any(S => S.IngoingTangent != S.OutgoingTangent) ? 1 : 1)), 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TextureAnimations[i].TranslationWFrames.Count), 0, 2);
                BTKFile.WriteReverse(new byte[2], 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes((ushort)(TextureAnimations[i].TranslationWFrames.Any(S => S.IngoingTangent != S.OutgoingTangent) ? 1 : 1)), 0, 2);
            }
            #region Padding
            int PadCount = 0;
            while (BTKFile.Position % 4 != 0)
                BTKFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            long RemapTableOffset = BTKFile.Position;
            List<string> strings = new List<string>();
            for (int i = 0; i < TextureAnimations.Count; i++)
            {
                BTKFile.WriteReverse(BitConverter.GetBytes(TextureAnimations[i].RemapIndex), 0, 2);
                strings.Add(TextureAnimations[i].MaterialName);
            }

            #region Padding
            PadCount = 0;
            while (BTKFile.Position % 4 != 0)
                BTKFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            long StringTableOffset = BTKFile.Position;
            BTKFile.WriteReverse(BitConverter.GetBytes((ushort)strings.Count), 0, 2);
            BTKFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
            ushort stringofffset = (ushort)(4 + (4 * strings.Count));
            List<byte> bytestrings = new List<byte>();
            byte[] TexMapID = new byte[TextureAnimations.Count];
            for (int i = 0; i < TextureAnimations.Count; i++)
            {
                BTKFile.WriteReverse(BitConverter.GetBytes(StringToHash(strings[i])), 0, 2);
                BTKFile.WriteReverse(BitConverter.GetBytes(stringofffset), 0, 2);
                byte[] currentstring = Encoding.GetEncoding(932).GetBytes(strings[i]);
                stringofffset += (ushort)(currentstring.Length + 1);
                bytestrings.AddRange(currentstring);
                bytestrings.Add(0x00);
                TexMapID[i] = TextureAnimations[i].MaterialTextureID;
            }
            BTKFile.Write(bytestrings.ToArray(), 0, bytestrings.Count);

            #region Padding
            PadCount = 0;
            while (BTKFile.Position % 4 != 0)
                BTKFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            long TexMapIDTableOffset = BTKFile.Position;
            BTKFile.Write(TexMapID, 0, TexMapID.Length);

            #region Padding
            PadCount = 0;
            while (BTKFile.Position % 4 != 0)
                BTKFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            long CenterTableOffset = BTKFile.Position;
            for (int i = 0; i < TextureAnimations.Count; i++)
            {
                BTKFile.WriteReverse(BitConverter.GetBytes(TextureAnimations[i].Center[0]), 0, 4);
                BTKFile.WriteReverse(BitConverter.GetBytes(TextureAnimations[i].Center[1]), 0, 4);
                BTKFile.WriteReverse(BitConverter.GetBytes(TextureAnimations[i].Center[2]), 0, 4);
            }

            #region Padding
            PadCount = 0;
            while (BTKFile.Position % 4 != 0)
                BTKFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            List<float> ScaleTable = new List<float>();
            List<short> RotationTable = new List<short>();
            List<float> TranslationTable = new List<float>();

            for (int i = 0; i < TextureAnimations.Count; i++)
            {
                FindMatch(ref ScaleTable, TextureAnimations[i].ScaleUFrames, 1);
                FindMatch(ref ScaleTable, TextureAnimations[i].ScaleVFrames, 1);
                FindMatch(ref ScaleTable, TextureAnimations[i].ScaleWFrames, 1);
                FindMatch(ref RotationTable, TextureAnimations[i].RotationUFrames, RotationMultiplier);
                FindMatch(ref RotationTable, TextureAnimations[i].RotationVFrames, RotationMultiplier);
                FindMatch(ref RotationTable, TextureAnimations[i].RotationWFrames, RotationMultiplier);
                FindMatch(ref TranslationTable, TextureAnimations[i].TranslationUFrames, 1);
                FindMatch(ref TranslationTable, TextureAnimations[i].TranslationVFrames, 1);
                FindMatch(ref TranslationTable, TextureAnimations[i].TranslationWFrames, 1);
            }
            long ScaleTableOffset = BTKFile.Position;
            for (int i = 0; i < ScaleTable.Count; i++)
                BTKFile.WriteReverse(BitConverter.GetBytes(ScaleTable[i]), 0, 4);

            #region Padding
            PadCount = 0;
            while (BTKFile.Position % 4 != 0)
                BTKFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            long RotationTableOffset = BTKFile.Position;
            for (int i = 0; i < RotationTable.Count; i++)
                BTKFile.WriteReverse(BitConverter.GetBytes(RotationTable[i]), 0, 2);

            #region Padding
            PadCount = 0;
            while (BTKFile.Position % 4 != 0)
                BTKFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            long TranslationTableOffset = BTKFile.Position;
            for (int i = 0; i < TranslationTable.Count; i++)
                BTKFile.WriteReverse(BitConverter.GetBytes(TranslationTable[i]), 0, 4);

            #region Padding
            PadCount = 0;
            while (BTKFile.Position % 32 != 0)
                BTKFile.WriteByte((byte)Padding[PadCount++]);
            #endregion

            BTKFile.Position = 0x08;
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)BTKFile.Length), 0, 4);
            BTKFile.Position = 0x24;
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)(BTKFile.Length - 0x20)), 0, 4);
            BTKFile.Position = 0x2E;
            BTKFile.WriteReverse(BitConverter.GetBytes((ushort)ScaleTable.Count), 0, 2);
            BTKFile.WriteReverse(BitConverter.GetBytes((ushort)RotationTable.Count), 0, 2);
            BTKFile.WriteReverse(BitConverter.GetBytes((ushort)TranslationTable.Count), 0, 2);
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)(AnimationTableOffset - 0x20)), 0, 4);
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)(RemapTableOffset - 0x20)), 0, 4);
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)(StringTableOffset - 0x20)), 0, 4);
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)(TexMapIDTableOffset - 0x20)), 0, 4);
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)(CenterTableOffset - 0x20)), 0, 4);
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)(ScaleTableOffset - 0x20)), 0, 4);
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)(RotationTableOffset - 0x20)), 0, 4);
            BTKFile.WriteReverse(BitConverter.GetBytes((uint)(TranslationTableOffset - 0x20)), 0, 4);

            BTKFile.Seek(AnimationTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < TextureAnimations.Count; i++)
            {
                BTKFile.Position += 2;
                BTKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref ScaleTable, TextureAnimations[i].ScaleUFrames, 1)), 0, 2);
                BTKFile.Position += 2;

                BTKFile.Position += 2;
                BTKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref RotationTable, TextureAnimations[i].RotationUFrames, RotationMultiplier)), 0, 2);
                BTKFile.Position += 2;

                BTKFile.Position += 2;
                BTKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref TranslationTable, TextureAnimations[i].TranslationUFrames, 1)), 0, 2);
                BTKFile.Position += 2;


                BTKFile.Position += 2;
                BTKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref ScaleTable, TextureAnimations[i].ScaleVFrames, 1)), 0, 2);
                BTKFile.Position += 2;

                BTKFile.Position += 2;
                BTKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref RotationTable, TextureAnimations[i].RotationVFrames, RotationMultiplier)), 0, 2);
                BTKFile.Position += 2;

                BTKFile.Position += 2;
                BTKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref TranslationTable, TextureAnimations[i].TranslationVFrames, 1)), 0, 2);
                BTKFile.Position += 2;


                BTKFile.Position += 2;
                BTKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref ScaleTable, TextureAnimations[i].ScaleWFrames, 1)), 0, 2);
                BTKFile.Position += 2;

                BTKFile.Position += 2;
                BTKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref RotationTable, TextureAnimations[i].RotationWFrames, RotationMultiplier)), 0, 2);
                BTKFile.Position += 2;

                BTKFile.Position += 2;
                BTKFile.WriteReverse(BitConverter.GetBytes(FindMatch(ref TranslationTable, TextureAnimations[i].TranslationWFrames, 1)), 0, 2);
                BTKFile.Position += 2;
            }
        }

        private List<Animation.Keyframe> ReadKeyframe(List<float> Data, float Scale, double Count, double Index, int TangentType)
        {
            List<Animation.Keyframe> keyframes = new List<Animation.Keyframe>();

            if (Count == 1)
                keyframes.Add(new Animation.Keyframe() { Time = 0, Value = Data[(int)Index] * Scale, IngoingTangent = 0, OutgoingTangent = 0 });
            else
            {
                if (TangentType == 0x00)
                {
                    for (double i = Index; i < Index + 3 * Count; i += 3)
                    {
                        float Tangents = Data[(int)i + 2] * Scale;
                        keyframes.Add(new Animation.Keyframe() { Time = (ushort)Data[(int)i + 0], Value = Data[(int)i + 1] * Scale, IngoingTangent = Tangents, OutgoingTangent = Tangents });
                    }
                }
                else if (TangentType == 0x01)
                {
                    for (double i = Index; i < Index + 4 * Count; i += 4)
                        keyframes.Add(new Animation.Keyframe() { Time = (ushort)Data[(int)i + 0], Value = Data[(int)i + 1] * Scale, IngoingTangent = Data[(int)i + 2] * Scale, OutgoingTangent = Data[(int)i + 3] * Scale });

                }
            }
            return keyframes;
        }

        private List<Animation.Keyframe> ReadKeyframe(List<short> Data, float Scale, double Count, double Index, int TangentType)
        {
            List<Animation.Keyframe> keyframes = new List<Animation.Keyframe>();

            if (Count == 1)
                keyframes.Add(new Animation.Keyframe() { Time = 0, Value = Data[(int)Index] * Scale, IngoingTangent = 0, OutgoingTangent = 0 });
            else
            {
                if (TangentType == 0x00)
                {
                    for (double i = Index; i < Index + 3 * Count; i += 3)
                    {
                        float Tangents = Data[(int)i + 2] * Scale;
                        keyframes.Add(new Animation.Keyframe() { Time = (ushort)Data[(int)i + 0], Value = Data[(int)i + 1] * Scale, IngoingTangent = Tangents, OutgoingTangent = Tangents });
                    }
                }
                else if (TangentType == 0x01)
                {
                    for (double i = Index; i < Index + 4 * Count; i += 4)
                        keyframes.Add(new Animation.Keyframe() { Time = (ushort)Data[(int)i + 0], Value = Data[(int)i + 1] * Scale, IngoingTangent = Data[(int)i + 2] * Scale, OutgoingTangent = Data[(int)i + 3] * Scale });

                }
            }
            return keyframes;
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

        private short FindMatch(ref List<float> FullList, List<Animation.Keyframe> sequence, float scale)
        {
            List<float> currentSequence = new List<float>();
            if (sequence.Count == 1)
                currentSequence.Add(sequence[0].Value/scale);
            else
            {
                for (int i = 0; i < sequence.Count; i++)
                {
                    currentSequence.Add(sequence[i].Time);
                    currentSequence.Add(sequence[i].Value/scale);
                    currentSequence.Add(sequence[i].IngoingTangent/scale);
                    currentSequence.Add(sequence[i].OutgoingTangent/scale);
                }
            }
            if (!FullList.ContainsSubsequence(currentSequence))
            {
                FullList.AddRange(currentSequence);
            }

            return (short)FullList.SubListIndex(0, currentSequence);
        }

        private short FindMatch(ref List<short> FullList, List<Animation.Keyframe> sequence, float scale)
        {
            List<short> currentSequence = new List<short>();
            if (sequence.Count == 1)
                currentSequence.Add((short)((ushort)(sequence[0].Value/scale)));
            else
            {
                for (int i = 0; i < sequence.Count; i++)
                {
                    currentSequence.Add((short)sequence[i].Time);
                    currentSequence.Add((short)((ushort)(sequence[i].Value / scale)));
                    currentSequence.Add((short)((ushort)(sequence[i].IngoingTangent/scale)));
                    currentSequence.Add((short)((ushort)(sequence[i].OutgoingTangent/scale)));
                }
            }
            if (!FullList.ContainsSubsequence(currentSequence))
            {
                FullList.AddRange(currentSequence);
            }

            return (short)FullList.SubListIndex(0, currentSequence);
        }


        //=====================================================================

        /// <summary>
        /// Cast a BTK to a RARCFile
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator RARC.RARC.File(BTK x)
        {
            return new RARC.RARC.File(x.Name, x.Save());
        }

        /// <summary>
        /// Cast a RARCFile to a BTK
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator BTK(RARC.RARC.File x)
        {
            return new BTK((MemoryStream)x, x.Name);
        }

        //=====================================================================
    }
}
