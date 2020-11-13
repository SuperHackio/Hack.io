using Hack.io.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hack.io.BCK
{
    /// <summary>
    /// Binary Curve Keyframes
    /// </summary>
    public class BCK
    {
        /// <summary>
        /// Filename of this BCK file.<para/>Set using <see cref="Save(string)"/>;
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
        /// Unknown.
        /// </summary>
        public float RotationMultiplier { get; set; }
        /// <summary>
        /// Animations that apply to the SRT values of a Texture
        /// </summary>
        public List<Animation> JointAnimations { get; set; } = new List<Animation>();

        private readonly static string Magic = "J3D1bck1";
        private readonly static string Magic2 = "ANK1";

        /// <summary>
        /// Create an Empty BCK
        /// </summary>
        public BCK() { }
        /// <summary>
        /// Open a BCK from a file
        /// </summary>
        /// <param name="filename"></param>
        public BCK(string filename)
        {
            FileStream BCKFile = new FileStream(filename, FileMode.Open);

            Read(BCKFile);

            BCKFile.Close();
            FileName = filename;
        }
        /// <summary>
        /// Create a BCK from a Stream.
        /// </summary>
        /// <param name="BCKStream">Stream containing the BCK</param>
        /// <param name="filename">Name to give this BCK file</param>
        public BCK(Stream BCKStream, string filename = null)
        {
            Read(BCKStream);
            FileName = filename;
        }

        /// <summary>
        /// Save the BCK to a file
        /// </summary>
        /// <param name="Filename">New file to save to</param>
        public void Save(string Filename = null)
        {
            if (FileName == null && Filename == null)
                throw new Exception("No Filename has been given");
            else if (Filename != null)
                FileName = Filename;

            FileStream BCKFile = new FileStream(FileName, FileMode.Create);

            Write(BCKFile);

            BCKFile.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{new FileInfo(FileName).Name} {(JointAnimations.Count > 0 ? $"[{JointAnimations.Count} Joint Animation{(JointAnimations.Count > 1 ? "s" : "")}] " : "")}";

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
            /// List of Scale X Keyframes
            /// </summary>
            public List<Keyframe> ScaleXFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Scale Y Keyframes
            /// </summary>
            public List<Keyframe> ScaleYFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Scale Z Keyframes
            /// </summary>
            public List<Keyframe> ScaleZFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Rotation X Keyframes
            /// </summary>
            public List<Keyframe> RotationXFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Rotation Y Keyframes
            /// </summary>
            public List<Keyframe> RotationYFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Rotation Z Keyframes
            /// </summary>
            public List<Keyframe> RotationZFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Translation X Keyframes
            /// </summary>
            public List<Keyframe> TranslationXFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Translation Y Keyframes
            /// </summary>
            public List<Keyframe> TranslationYFrames { get; set; } = new List<Keyframe>();
            /// <summary>
            /// List of Translation Z Keyframes
            /// </summary>
            public List<Keyframe> TranslationZFrames { get; set; } = new List<Keyframe>();

            /// <summary>
            /// Create an empty animation
            /// </summary>
            public Animation() { }
            /// <summary>
            /// Create an animation with the given amount of keyframes
            /// </summary>
            public Animation(int ScaleCount = 1, int RotationCount = 1, int TranslationCount = 1)
            {
                for (int i = 0; i < ScaleCount; i++)
                {
                    ScaleXFrames.Add(new Keyframe() { Value = 1 });
                    ScaleYFrames.Add(new Keyframe() { Value = 1 });
                    ScaleZFrames.Add(new Keyframe() { Value = 1 });
                }
                for (int i = 0; i < RotationCount; i++)
                {
                    RotationXFrames.Add(new Keyframe());
                    RotationYFrames.Add(new Keyframe());
                    RotationZFrames.Add(new Keyframe());
                }
                for (int i = 0; i < TranslationCount; i++)
                {
                    TranslationXFrames.Add(new Keyframe());
                    TranslationYFrames.Add(new Keyframe());
                    TranslationZFrames.Add(new Keyframe());
                }
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"Joint [{TranslationXFrames.Count},{TranslationYFrames.Count},{TranslationZFrames.Count}|{RotationXFrames.Count},{RotationYFrames.Count},{RotationZFrames.Count}|{ScaleXFrames.Count},{ScaleYFrames.Count},{ScaleZFrames.Count}]";
            /// <summary>
            /// 
            /// </summary>
            /// <param name="Left"></param>
            /// <param name="Right"></param>
            /// <returns></returns>
            public static bool operator ==(Animation Left, Animation Right) => Left.Equals(Right);

            /// <summary>
            /// 
            /// </summary>
            /// <param name="Left"></param>
            /// <param name="Right"></param>
            /// <returns></returns>
            public static bool operator !=(Animation Left, Animation Right) => !Left.Equals(Right);

            /// <summary>
            /// 
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public override bool Equals(object obj)
            {
                return obj is Animation anim &&
                    TranslationXFrames.SequenceEqual(anim.TranslationXFrames) && TranslationYFrames.SequenceEqual(anim.TranslationYFrames) && TranslationZFrames.SequenceEqual(anim.TranslationZFrames) &&
                    RotationXFrames.SequenceEqual(anim.RotationXFrames)       && RotationYFrames.SequenceEqual(anim.RotationYFrames)       && RotationZFrames.SequenceEqual(anim.RotationZFrames) &&
                    ScaleXFrames.SequenceEqual(anim.ScaleXFrames)             && ScaleYFrames.SequenceEqual(anim.ScaleYFrames)             && ScaleZFrames.SequenceEqual(anim.ScaleZFrames);
            }
            /// <summary>
            /// Audo-Generated
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                var hashCode = 1799963556;
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Keyframe>>.Default.GetHashCode(ScaleXFrames);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Keyframe>>.Default.GetHashCode(ScaleYFrames);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Keyframe>>.Default.GetHashCode(ScaleZFrames);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Keyframe>>.Default.GetHashCode(RotationXFrames);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Keyframe>>.Default.GetHashCode(RotationYFrames);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Keyframe>>.Default.GetHashCode(RotationZFrames);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Keyframe>>.Default.GetHashCode(TranslationXFrames);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Keyframe>>.Default.GetHashCode(TranslationYFrames);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Keyframe>>.Default.GetHashCode(TranslationZFrames);
                return hashCode;
            }

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
                public override string ToString() => $"Time: {Time} [{Value*(180/Math.PI)}]";

                /// <summary>
                /// 
                /// </summary>
                /// <param name="Left"></param>
                /// <param name="Right"></param>
                /// <returns></returns>
                public static bool operator ==(Keyframe Left, Keyframe Right) => Left.Equals(Right);

                /// <summary>
                /// 
                /// </summary>
                /// <param name="Left"></param>
                /// <param name="Right"></param>
                /// <returns></returns>
                public static bool operator !=(Keyframe Left, Keyframe Right) => !Left.Equals(Right);

                /// <summary>
                /// 
                /// </summary>
                /// <param name="obj"></param>
                /// <returns></returns>
                public override bool Equals(object obj) => obj is Keyframe x && Time.Equals(x.Time) && Value.Equals(x.Value) && IngoingTangent.Equals(x.IngoingTangent) && OutgoingTangent.Equals(x.OutgoingTangent);
                /// <summary>
                /// Auto-Generated
                /// </summary>
                /// <returns></returns>
                public override int GetHashCode()
                {
                    var hashCode = 2107829771;
                    hashCode = hashCode * -1521134295 + Time.GetHashCode();
                    hashCode = hashCode * -1521134295 + Value.GetHashCode();
                    hashCode = hashCode * -1521134295 + IngoingTangent.GetHashCode();
                    hashCode = hashCode * -1521134295 + OutgoingTangent.GetHashCode();
                    return hashCode;
                }
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
        /// <summary>
        /// For rotations
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="Scale"></param>
        /// <param name="Count"></param>
        /// <param name="Index"></param>
        /// <param name="TangentType"></param>
        /// <returns></returns>
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
        
        private void Read(Stream BCKFile)
        {
            if (BCKFile.ReadString(8) != Magic)
                throw new Exception("Invalid Identifier. Expected \"J3D1bck1\"");

            uint Filesize = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0);
            uint SectionCount = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0);
            if (SectionCount != 1)
                throw new Exception(SectionCount > 1 ? "More than 1 section is in this BCK! Please send it to Super Hackio for investigation" : "There are no sections in this BCK!");

            BCKFile.Seek(0x10, SeekOrigin.Current);
            uint TRKStart = (uint)BCKFile.Position;

            if (BCKFile.ReadString(4) != Magic2)
                throw new Exception("Invalid Identifier. Expected \"ANK1\"");

            uint TRK1Length = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0);
            Loop = (LoopMode)BCKFile.ReadByte();
            RotationMultiplier = (float)(Math.Pow(BCKFile.ReadByte(), 2) / 32767);
            Time = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0);

            ushort AnimationCount = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0),
                ScaleCount = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0), RotationCount = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0), TranslationCount = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0);

            uint AnimationTableOffset = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0) + TRKStart, ScaleTableOffset = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0) + TRKStart,
                RotationTableOffset = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0) + TRKStart, TranslationTableOffset = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0) + TRKStart;

            List<float> ScaleTable = new List<float>();
            List<short> RotationTable = new List<short>();
            List<float> TranslationTable = new List<float>();

            BCKFile.Seek(ScaleTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < ScaleCount; i++)
                ScaleTable.Add(BitConverter.ToSingle(BCKFile.ReadReverse(0, 4), 0));

            BCKFile.Seek(RotationTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < RotationCount; i++)
                RotationTable.Add(BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0));

            BCKFile.Seek(TranslationTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < TranslationCount; i++)
                TranslationTable.Add(BitConverter.ToSingle(BCKFile.ReadReverse(0, 4), 0));

            BCKFile.Seek(AnimationTableOffset, SeekOrigin.Begin);
            short KeyFrameCount, TargetKeySet, TangentType;
            for (int i = 0; i < AnimationCount; i++)
            {
                Animation Anim = new Animation();
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                Anim.ScaleXFrames = ReadKeyframe(ScaleTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                Anim.RotationXFrames = ReadKeyframe(RotationTable, RotationMultiplier, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                Anim.TranslationXFrames = ReadKeyframe(TranslationTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                Anim.ScaleYFrames = ReadKeyframe(ScaleTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                Anim.RotationYFrames = ReadKeyframe(RotationTable, RotationMultiplier, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                Anim.TranslationYFrames = ReadKeyframe(TranslationTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                Anim.ScaleZFrames = ReadKeyframe(ScaleTable, 1, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                Anim.RotationZFrames = ReadKeyframe(RotationTable, RotationMultiplier, KeyFrameCount, TargetKeySet, TangentType);
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                Anim.TranslationZFrames = ReadKeyframe(TranslationTable, 1, KeyFrameCount, TargetKeySet, TangentType);

                JointAnimations.Add(Anim);
            }
        }

        private void Write(Stream BCKFile)
        {
            string Padding = "Hack.io.BCK © Super Hackio Incorporated 2020";

            BCKFile.WriteString(Magic);
            BCKFile.Write(new byte[8] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x01 }, 0, 8);
            BCKFile.Write(new byte[16] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 16);

            uint TRKStart = (uint)BCKFile.Position;
            BCKFile.WriteString(Magic2);
            BCKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BCKFile.WriteByte((byte)Loop);
            double tmp = (Math.Log(2) / Math.Log(RotationMultiplier));
            BCKFile.WriteByte((byte)tmp);
            BCKFile.WriteReverse(BitConverter.GetBytes(Time), 0, 2);
            BCKFile.WriteReverse(BitConverter.GetBytes((ushort)JointAnimations.Count), 0, 2);
            long ValueCountPausePosition = BCKFile.Position;
            BCKFile.Write(new byte[6] { 0xAA, 0xAA, 0xBB, 0xBB, 0xCC, 0xCC }, 0, 6);
            long DataOffsetsPausePosition = BCKFile.Position;
            for (int i = 0; i < 4; i++)
                BCKFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4);

            #region Padding
            int PadCount = 0;
            while (BCKFile.Position % 32 != 0)
            {
                BCKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion
            uint AnimationOffset = (uint)BCKFile.Position;
            for (int i = 0; i < JointAnimations.Count; i++)
            {
                BCKFile.WriteReverse(BitConverter.GetBytes((short)JointAnimations[i].ScaleXFrames.Count), 0, 2);
                BCKFile.WriteReverse(new byte[2], 0, 2);
                BCKFile.WriteReverse(BitConverter.GetBytes((short)(JointAnimations[i].ScaleXFrames.Any(SU => SU.IngoingTangent != SU.OutgoingTangent) ? 1 : 0)), 0, 2);

                BCKFile.WriteReverse(BitConverter.GetBytes((short)JointAnimations[i].RotationXFrames.Count), 0, 2);
                BCKFile.WriteReverse(new byte[2], 0, 2);
                BCKFile.WriteReverse(BitConverter.GetBytes((short)(JointAnimations[i].RotationXFrames.Any(RU => RU.IngoingTangent != RU.OutgoingTangent) ? 1 : 0)), 0, 2);

                BCKFile.WriteReverse(BitConverter.GetBytes((short)JointAnimations[i].TranslationXFrames.Count), 0, 2);
                BCKFile.WriteReverse(new byte[2], 0, 2);
                BCKFile.WriteReverse(BitConverter.GetBytes((short)(JointAnimations[i].TranslationXFrames.Any(TU => TU.IngoingTangent != TU.OutgoingTangent) ? 1 : 0)), 0, 2);
                //==
                BCKFile.WriteReverse(BitConverter.GetBytes((short)JointAnimations[i].ScaleYFrames.Count), 0, 2);
                BCKFile.WriteReverse(new byte[2], 0, 2);
                BCKFile.WriteReverse(BitConverter.GetBytes((short)(JointAnimations[i].ScaleYFrames.Any(SV => SV.IngoingTangent != SV.OutgoingTangent) ? 1 : 0)), 0, 2);

                BCKFile.WriteReverse(BitConverter.GetBytes((short)JointAnimations[i].RotationYFrames.Count), 0, 2);
                BCKFile.WriteReverse(new byte[2], 0, 2);
                BCKFile.WriteReverse(BitConverter.GetBytes((short)(JointAnimations[i].RotationYFrames.Any(RV => RV.IngoingTangent != RV.OutgoingTangent) ? 1 : 0)), 0, 2);

                BCKFile.WriteReverse(BitConverter.GetBytes((short)JointAnimations[i].TranslationYFrames.Count), 0, 2);
                BCKFile.WriteReverse(new byte[2], 0, 2);
                BCKFile.WriteReverse(BitConverter.GetBytes((short)(JointAnimations[i].TranslationYFrames.Any(TV => TV.IngoingTangent != TV.OutgoingTangent) ? 1 : 0)), 0, 2);
                //==
                BCKFile.WriteReverse(BitConverter.GetBytes((short)JointAnimations[i].ScaleZFrames.Count), 0, 2);
                BCKFile.WriteReverse(new byte[2], 0, 2);
                BCKFile.WriteReverse(BitConverter.GetBytes((short)(JointAnimations[i].ScaleZFrames.Any(SW => SW.IngoingTangent != SW.OutgoingTangent) ? 1 : 0)), 0, 2);

                BCKFile.WriteReverse(BitConverter.GetBytes((short)JointAnimations[i].RotationZFrames.Count), 0, 2);
                BCKFile.WriteReverse(new byte[2], 0, 2);
                BCKFile.WriteReverse(BitConverter.GetBytes((short)(JointAnimations[i].RotationZFrames.Any(RW => RW.IngoingTangent != RW.OutgoingTangent) ? 1 : 0)), 0, 2);

                BCKFile.WriteReverse(BitConverter.GetBytes((short)JointAnimations[i].TranslationZFrames.Count), 0, 2);
                BCKFile.WriteReverse(new byte[2], 0, 2);
                BCKFile.WriteReverse(BitConverter.GetBytes((short)(JointAnimations[i].TranslationZFrames.Any(TW => TW.IngoingTangent != TW.OutgoingTangent) ? 1 : 0)), 0, 2);
            }

            #region Padding
            PadCount = 0;
            while (BCKFile.Position % 32 != 0)
            {
                BCKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            #region Scale

            long ScaleTableOffset = BCKFile.Position;
            List<float[]> ScaleTable = new List<float[]>();
            List<float> FinalScaleTable = new List<float>();
            for (int i = 0; i < JointAnimations.Count; i++)
            {
                for (int j = 0; j < JointAnimations[i].ScaleXFrames.Count; j++)
                {
                    if (!(ScaleTable.Any(O => O[0] == JointAnimations[i].ScaleXFrames[j].Time) && ScaleTable.Any(O => O[1] == JointAnimations[i].ScaleXFrames[j].Value)
                        && ScaleTable.Any(O => O[2] == JointAnimations[i].ScaleXFrames[j].IngoingTangent) && ScaleTable.Any(O => O[3] == JointAnimations[i].ScaleXFrames[j].OutgoingTangent)))
                    {
                        ScaleTable.Add(new float[4] { JointAnimations[i].ScaleXFrames[j].Time, JointAnimations[i].ScaleXFrames[j].Value, JointAnimations[i].ScaleXFrames[j].IngoingTangent, JointAnimations[i].ScaleXFrames[j].OutgoingTangent });
                    }
                }
                for (int j = 0; j < JointAnimations[i].ScaleYFrames.Count; j++)
                {
                    if (!(ScaleTable.Any(O => O[0] == JointAnimations[i].ScaleYFrames[j].Time) && ScaleTable.Any(O => O[1] == JointAnimations[i].ScaleYFrames[j].Value)
                        && ScaleTable.Any(O => O[2] == JointAnimations[i].ScaleYFrames[j].IngoingTangent) && ScaleTable.Any(O => O[3] == JointAnimations[i].ScaleYFrames[j].OutgoingTangent)))
                    {
                        ScaleTable.Add(new float[4] { JointAnimations[i].ScaleYFrames[j].Time, JointAnimations[i].ScaleYFrames[j].Value, JointAnimations[i].ScaleYFrames[j].IngoingTangent, JointAnimations[i].ScaleYFrames[j].OutgoingTangent });
                    }
                }
                for (int j = 0; j < JointAnimations[i].ScaleZFrames.Count; j++)
                {
                    if (!(ScaleTable.Any(O => O[0] == JointAnimations[i].ScaleZFrames[j].Time) && ScaleTable.Any(O => O[1] == JointAnimations[i].ScaleZFrames[j].Value)
                        && ScaleTable.Any(O => O[2] == JointAnimations[i].ScaleZFrames[j].IngoingTangent) && ScaleTable.Any(O => O[3] == JointAnimations[i].ScaleZFrames[j].OutgoingTangent)))
                    {
                        ScaleTable.Add(new float[4] { JointAnimations[i].ScaleZFrames[j].Time, JointAnimations[i].ScaleZFrames[j].Value, JointAnimations[i].ScaleZFrames[j].IngoingTangent, JointAnimations[i].ScaleZFrames[j].OutgoingTangent });
                    }
                }
            }
            ushort scalewrite = 0;
            if (ScaleTable.Count > 1)
                for (int i = 0; i < ScaleTable.Count; i++)
                {
                    BCKFile.WriteReverse(BitConverter.GetBytes(ScaleTable[i][0]), 0, 4);
                    BCKFile.WriteReverse(BitConverter.GetBytes(ScaleTable[i][1]), 0, 4);
                    BCKFile.WriteReverse(BitConverter.GetBytes(ScaleTable[i][2]), 0, 4);
                    BCKFile.WriteReverse(BitConverter.GetBytes(ScaleTable[i][3]), 0, 4);
                    scalewrite += 4;
                    FinalScaleTable.Add(ScaleTable[i][0]);
                    FinalScaleTable.Add(ScaleTable[i][1]);
                    FinalScaleTable.Add(ScaleTable[i][2]);
                    FinalScaleTable.Add(ScaleTable[i][3]);
                }
            else
            {
                BCKFile.WriteReverse(BitConverter.GetBytes(ScaleTable[0][1]), 0, 4);
                scalewrite++;
                FinalScaleTable.Add(ScaleTable[0][1]);
            }

            #region Padding
            PadCount = 0;
            while (BCKFile.Position % 32 != 0)
            {
                BCKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            #endregion

            #region Rotation

            long RotationTableOffset = BCKFile.Position;
            List<short[]> RotationTable = new List<short[]>();
            List<short> FinalRotationTable = new List<short>();
            for (int i = 0; i < JointAnimations.Count; i++)
            {
                for (int j = 0; j < JointAnimations[i].RotationXFrames.Count; j++)
                {
                    if (!(RotationTable.Any(O => O[0] == JointAnimations[i].RotationXFrames[j].Time) && RotationTable.Any(O => O[1] == JointAnimations[i].RotationXFrames[j].Value)
                        && RotationTable.Any(O => O[2] == JointAnimations[i].RotationXFrames[j].IngoingTangent) && RotationTable.Any(O => O[3] == JointAnimations[i].RotationXFrames[j].OutgoingTangent)))
                    {
                        RotationTable.Add(new short[4] { (short)JointAnimations[i].RotationXFrames[j].Time, (short)(JointAnimations[i].RotationXFrames[j].Value / RotationMultiplier), (short)(JointAnimations[i].RotationXFrames[j].IngoingTangent / RotationMultiplier), (short)(JointAnimations[i].RotationXFrames[j].OutgoingTangent / RotationMultiplier) });
                    }
                }
                for (int j = 0; j < JointAnimations[i].RotationYFrames.Count; j++)
                {
                    if (!(RotationTable.Any(O => O[0] == JointAnimations[i].RotationYFrames[j].Time) && RotationTable.Any(O => O[1] == JointAnimations[i].RotationYFrames[j].Value)
                        && RotationTable.Any(O => O[2] == JointAnimations[i].RotationYFrames[j].IngoingTangent) && RotationTable.Any(O => O[3] == JointAnimations[i].RotationYFrames[j].OutgoingTangent)))
                    {
                        RotationTable.Add(new short[4] { (short)JointAnimations[i].RotationYFrames[j].Time, (short)(JointAnimations[i].RotationYFrames[j].Value / RotationMultiplier), (short)(JointAnimations[i].RotationYFrames[j].IngoingTangent / RotationMultiplier), (short)(JointAnimations[i].RotationYFrames[j].OutgoingTangent / RotationMultiplier) });
                    }
                }
                for (int j = 0; j < JointAnimations[i].RotationZFrames.Count; j++)
                {
                    if (!(RotationTable.Any(O => O[0] == JointAnimations[i].RotationZFrames[j].Time) && RotationTable.Any(O => O[1] == JointAnimations[i].RotationZFrames[j].Value)
                        && RotationTable.Any(O => O[2] == JointAnimations[i].RotationZFrames[j].IngoingTangent) && RotationTable.Any(O => O[3] == JointAnimations[i].RotationZFrames[j].OutgoingTangent)))
                    {
                        RotationTable.Add(new short[4] { (short)JointAnimations[i].RotationZFrames[j].Time, (short)(JointAnimations[i].RotationZFrames[j].Value / RotationMultiplier), (short)(JointAnimations[i].RotationZFrames[j].IngoingTangent / RotationMultiplier), (short)(JointAnimations[i].RotationZFrames[j].OutgoingTangent / RotationMultiplier) });
                    }
                }
            }
            ushort rotationwrite = 0;
            if (RotationTable.Count > 1)
                for (int i = 0; i < RotationTable.Count; i++)
                {
                    BCKFile.WriteReverse(BitConverter.GetBytes(RotationTable[i][0]), 0, 2);
                    BCKFile.WriteReverse(BitConverter.GetBytes(RotationTable[i][1]), 0, 2);
                    BCKFile.WriteReverse(BitConverter.GetBytes(RotationTable[i][2]), 0, 2);
                    BCKFile.WriteReverse(BitConverter.GetBytes(RotationTable[i][3]), 0, 2);
                    rotationwrite += 4;
                    FinalRotationTable.Add(RotationTable[i][0]);
                    FinalRotationTable.Add(RotationTable[i][1]);
                    FinalRotationTable.Add(RotationTable[i][2]);
                    FinalRotationTable.Add(RotationTable[i][3]);
                }
            else
            {
                BCKFile.WriteReverse(BitConverter.GetBytes(RotationTable[0][1]), 0, 2);
                rotationwrite++;
                FinalRotationTable.Add(RotationTable[0][1]);
            }

            #region Padding
            PadCount = 0;
            while (BCKFile.Position % 32 != 0)
            {
                BCKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            #endregion

            #region Translation

            long TranslationTableOffset = BCKFile.Position;
            List<float[]> TranslationTable = new List<float[]>();
            List<float> FinalTranslationTable = new List<float>();
            for (int i = 0; i < JointAnimations.Count; i++)
            {
                for (int j = 0; j < JointAnimations[i].TranslationXFrames.Count; j++)
                {
                    if (!(TranslationTable.Any(O => O[0] == JointAnimations[i].TranslationXFrames[j].Time) && TranslationTable.Any(O => O[1] == JointAnimations[i].TranslationXFrames[j].Value)
                        && TranslationTable.Any(O => O[2] == JointAnimations[i].TranslationXFrames[j].IngoingTangent) && TranslationTable.Any(O => O[3] == JointAnimations[i].TranslationXFrames[j].OutgoingTangent)))
                    {
                        TranslationTable.Add(new float[4] { JointAnimations[i].TranslationXFrames[j].Time, JointAnimations[i].TranslationXFrames[j].Value, JointAnimations[i].TranslationXFrames[j].IngoingTangent, JointAnimations[i].TranslationXFrames[j].OutgoingTangent });
                    }
                }
                for (int j = 0; j < JointAnimations[i].TranslationYFrames.Count; j++)
                {
                    if (!(TranslationTable.Any(O => O[0] == JointAnimations[i].TranslationYFrames[j].Time) && TranslationTable.Any(O => O[1] == JointAnimations[i].TranslationYFrames[j].Value)
                        && TranslationTable.Any(O => O[2] == JointAnimations[i].TranslationYFrames[j].IngoingTangent) && TranslationTable.Any(O => O[3] == JointAnimations[i].TranslationYFrames[j].OutgoingTangent)))
                    {
                        TranslationTable.Add(new float[4] { JointAnimations[i].TranslationYFrames[j].Time, JointAnimations[i].TranslationYFrames[j].Value, JointAnimations[i].TranslationYFrames[j].IngoingTangent, JointAnimations[i].TranslationYFrames[j].OutgoingTangent });
                    }
                }
                for (int j = 0; j < JointAnimations[i].TranslationZFrames.Count; j++)
                {
                    if (!(TranslationTable.Any(O => O[0] == JointAnimations[i].TranslationZFrames[j].Time) && TranslationTable.Any(O => O[1] == JointAnimations[i].TranslationZFrames[j].Value)
                        && TranslationTable.Any(O => O[2] == JointAnimations[i].TranslationZFrames[j].IngoingTangent) && TranslationTable.Any(O => O[3] == JointAnimations[i].TranslationZFrames[j].OutgoingTangent)))
                    {
                        TranslationTable.Add(new float[4] { JointAnimations[i].TranslationZFrames[j].Time, JointAnimations[i].TranslationZFrames[j].Value, JointAnimations[i].TranslationZFrames[j].IngoingTangent, JointAnimations[i].TranslationZFrames[j].OutgoingTangent });
                    }
                }
            }
            ushort translationwrite = 0;
            if (TranslationTable.Count > 1)
                for (int i = 0; i < TranslationTable.Count; i++)
                {
                    BCKFile.WriteReverse(BitConverter.GetBytes(TranslationTable[i][0]), 0, 4);
                    BCKFile.WriteReverse(BitConverter.GetBytes(TranslationTable[i][1]), 0, 4);
                    BCKFile.WriteReverse(BitConverter.GetBytes(TranslationTable[i][2]), 0, 4);
                    BCKFile.WriteReverse(BitConverter.GetBytes(TranslationTable[i][3]), 0, 4);
                    translationwrite += 4;
                    FinalTranslationTable.Add(TranslationTable[i][0]);
                    FinalTranslationTable.Add(TranslationTable[i][1]);
                    FinalTranslationTable.Add(TranslationTable[i][2]);
                    FinalTranslationTable.Add(TranslationTable[i][3]);
                }
            else
            {
                BCKFile.WriteReverse(BitConverter.GetBytes(TranslationTable[0][1]), 0, 4);
                translationwrite++;
                FinalTranslationTable.Add(TranslationTable[0][1]);
            }

            #region Padding
            PadCount = 0;
            while (BCKFile.Position % 32 != 0)
            {
                BCKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion

            #endregion

            BCKFile.Position = 0x08;
            BCKFile.WriteReverse(BitConverter.GetBytes((int)BCKFile.Length), 0, 4);
            BCKFile.Position = TRKStart+4;
            BCKFile.WriteReverse(BitConverter.GetBytes((int)(BCKFile.Length - TRKStart)), 0, 4);
            BCKFile.Position = ValueCountPausePosition;
            BCKFile.WriteReverse(BitConverter.GetBytes(scalewrite), 0, 2);
            BCKFile.WriteReverse(BitConverter.GetBytes(rotationwrite), 0, 2);
            BCKFile.WriteReverse(BitConverter.GetBytes(translationwrite), 0, 2);

            BCKFile.WriteReverse(BitConverter.GetBytes(AnimationOffset - TRKStart), 0, 4);
            BCKFile.WriteReverse(BitConverter.GetBytes((uint)(ScaleTableOffset - TRKStart)), 0, 4);
            BCKFile.WriteReverse(BitConverter.GetBytes((uint)(RotationTableOffset - TRKStart)), 0, 4);
            BCKFile.WriteReverse(BitConverter.GetBytes((uint)(TranslationTableOffset - TRKStart)), 0, 4);

            #region Assign the Keysets

            BCKFile.Position = AnimationOffset;
            for (int i = 0; i < JointAnimations.Count; i++)
            {
                //ScaleUFrames
                BCKFile.Position += 2;
                BCKFile.WriteReverse(BitConverter.GetBytes(GetKeyID(FinalScaleTable, JointAnimations[i].ScaleXFrames)), 0, 2);
                BCKFile.Position += 2;
                //RotationUFrames
                BCKFile.Position += 2;
                BCKFile.WriteReverse(BitConverter.GetBytes(GetKeyID(FinalRotationTable, JointAnimations[i].RotationXFrames)), 0, 2);
                BCKFile.Position += 2;
                //TranslationUFrames
                BCKFile.Position += 2;
                BCKFile.WriteReverse(BitConverter.GetBytes(GetKeyID(FinalTranslationTable, JointAnimations[i].TranslationXFrames)), 0, 2);
                BCKFile.Position += 2;
                //==
                BCKFile.Position += 2;
                BCKFile.WriteReverse(BitConverter.GetBytes(GetKeyID(FinalScaleTable, JointAnimations[i].ScaleYFrames)), 0, 2);
                BCKFile.Position += 2;

                BCKFile.Position += 2;
                BCKFile.WriteReverse(BitConverter.GetBytes(GetKeyID(FinalRotationTable, JointAnimations[i].RotationYFrames)), 0, 2);
                BCKFile.Position += 2;

                BCKFile.Position += 2;
                BCKFile.WriteReverse(BitConverter.GetBytes(GetKeyID(FinalTranslationTable, JointAnimations[i].TranslationYFrames)), 0, 2);
                BCKFile.Position += 2;
                //==
                BCKFile.Position += 2;
                BCKFile.WriteReverse(BitConverter.GetBytes(GetKeyID(FinalScaleTable, JointAnimations[i].ScaleZFrames)), 0, 2);
                BCKFile.Position += 2;

                BCKFile.Position += 2;
                BCKFile.WriteReverse(BitConverter.GetBytes(GetKeyID(FinalRotationTable, JointAnimations[i].RotationZFrames)), 0, 2);
                BCKFile.Position += 2;

                BCKFile.Position += 2;
                BCKFile.WriteReverse(BitConverter.GetBytes(GetKeyID(FinalTranslationTable, JointAnimations[i].TranslationZFrames)), 0, 2);
                BCKFile.Position += 2;
            }

            #endregion
        }

        private ushort GetKeyID(List<float> KeyTable, List<Animation.Keyframe> CurrentAnim)
        {
            if (CurrentAnim.Count == 1)
            {
                for (ushort i = 0; i < KeyTable.Count; i++)
                {
                    if (KeyTable[i] == CurrentAnim[0].Value)
                    {
                        return i;
                    }
                }
                throw new Exception("Error in Hack.io.BCK: Couldn't find a matching keyset. The exported file has been corrupted.");
            }
            else
            {
                for (ushort i = 0; i < KeyTable.Count; i++)
                {
                    if (KeyTable[i] == CurrentAnim[0].Time)
                    {
                        bool isbroken = false;
                        ushort j;
                        for (j = 0; j < CurrentAnim.Count; j++)
                        {
                            if (KeyTable[i + (j * 4) + 0] != CurrentAnim[j].Time)
                                isbroken = true;
                            if (KeyTable[i + (j * 4) + 1] != CurrentAnim[j].Value)
                                isbroken = true;
                            if (KeyTable[i + (j * 4) + 2] != CurrentAnim[j].IngoingTangent)
                                isbroken = true;
                            if (KeyTable[i + (j * 4) + 3] != CurrentAnim[j].OutgoingTangent)
                                isbroken = true;

                            if (isbroken)
                                break;
                        }
                        if (!isbroken)
                            return i;
                    }
                }
                throw new Exception("Error in Hack.io.BCK: Couldn't find a matching keyset. The exported file has been corrupted.");
            }
        }

        private ushort GetKeyID(List<short> KeyTable, List<Animation.Keyframe> CurrentAnim)
        {
            if (CurrentAnim.Count == 1)
            {
                for (ushort i = 0; i < KeyTable.Count; i++)
                {
                    if (KeyTable[i] == (short)CurrentAnim[0].Value)
                    {
                        return i;
                    }
                }
                throw new Exception("Error in Hack.io.BCK: Couldn't find a matching keyset. The exported file has been corrupted.");
            }
            else
            {
                for (ushort i = 0; i < KeyTable.Count; i++)
                {
                    if (KeyTable[i] == CurrentAnim[0].Time)
                    {
                        bool isbroken = false;
                        ushort j;
                        for (j = 0; j < CurrentAnim.Count; j++)
                        {
                            if (KeyTable[i + (j * 4) + 0] != (short)CurrentAnim[j].Time)
                                isbroken = true;
                            if (KeyTable[i + (j * 4) + 1] != (short)(CurrentAnim[j].Value / RotationMultiplier))
                                isbroken = true;
                            if (KeyTable[i + (j * 4) + 2] != (short)(CurrentAnim[j].IngoingTangent / RotationMultiplier))
                                isbroken = true;
                            if (KeyTable[i + (j * 4) + 3] != (short)(CurrentAnim[j].OutgoingTangent / RotationMultiplier))
                                isbroken = true;

                            if (isbroken)
                                break;
                        }
                        if (!isbroken)
                            return i;
                    }
                }
                throw new Exception("Error in Hack.io.BCK: Couldn't find a matching keyset. The exported file has been corrupted.");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Left"></param>
        /// <param name="Right"></param>
        /// <returns></returns>
        public static bool operator ==(BCK Left, BCK Right) => Left.Equals(Right);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Left"></param>
        /// <param name="Right"></param>
        /// <returns></returns>
        public static bool operator !=(BCK Left, BCK Right) => !Left.Equals(Right);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj) => obj is BCK bck && FileName.Equals(bck.FileName) && Loop.Equals(bck.Loop) && Time.Equals(bck.Time) && JointAnimations.SequenceEqual(bck.JointAnimations);

        /// <summary>
        /// Auto-Generated
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            var hashCode = 1663351623;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FileName);
            hashCode = hashCode * -1521134295 + Loop.GetHashCode();
            hashCode = hashCode * -1521134295 + Time.GetHashCode();
            hashCode = hashCode * -1521134295 + RotationMultiplier.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<List<Animation>>.Default.GetHashCode(JointAnimations);
            return hashCode;
        }
    }
}
