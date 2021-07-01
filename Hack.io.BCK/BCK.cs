using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hack.io.BMD;
using Hack.io.Util;
using static Hack.io.J3D.J3DGraph;

namespace Hack.io.BCK
{
    /// <summary>
    /// Binary Curve Keyframes
    /// </summary>
    public class BCK
    {
        private const string Magic = "J3D1bck1";
        private const string Magic2 = "ANK1";
        /// <summary>
        /// The name of this BCK
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Loop Mode of the BRK animation. See the <seealso cref="LoopMode"/> enum for values
        /// </summary>
        public LoopMode Loop { get; set; } = LoopMode.ONCE;
        /// <summary>
        /// Length of the animation in Frames. (Game Framerate = 1 second)
        /// </summary>
        public ushort Duration { get; set; }
        /// <summary>
        /// Multiplier for fixed-point decimal
        /// </summary>
        public byte RotationFraction { get; set; }
        /// <summary>
        /// List of Animations. there should only be as many animations as there are bones in the model
        /// </summary>
        public List<BoneAnimation> JointAnimations { get; set; } = new List<BoneAnimation>();

        /// <summary>
        /// Create an empty BCK
        /// </summary>
        public BCK()
        {
        }
        /// <summary>
        /// Open a BCK from a file
        /// </summary>
        /// <param name="filename"></param>
        public BCK(string filename)
        {
            FileStream BCKFile = new FileStream(filename, FileMode.Open);

            Read(BCKFile);

            BCKFile.Close();
            Name = filename;
        }


        /// <summary>
        /// Save a BCK to a file
        /// </summary>
        /// <param name="filename"></param>
        public void Save(string filename)
        {
            FileStream BCKFile = new FileStream(filename, FileMode.Create);

            Write(BCKFile);

            BCKFile.Close();
            Name = filename;
        }
        /// <summary>
        /// !! Hack.io.BMD Is required to use this !!<para/>Read in the Names of Bones from a model file. The file must have the same amount of bones as there are JointAnimations
        /// </summary>
        /// <param name="Model">The model to get the bone names from</param>
        public void LoadBoneNames(BMD.BMD Model)
        {
            if (JointAnimations.Count != Model.Joints.FlatSkeleton.Count)
                throw new Exception("Number of Joints in the model doesn't match the number of Joint animations");
            for (int i = 0; i < Model.Joints.FlatSkeleton.Count; i++)
                JointAnimations[i].JointName = Model.Joints.FlatSkeleton[i].Name;
        }
        /// <summary>
        /// !! Hack.io.BMD Is required to use this !!<para/>Read in the Names of Bones from a model file. The file must have the same amount of bones as there are JointAnimations
        /// </summary>
        /// <param name="Model">The model to get the bone names from</param>
        public void LoadBoneNames(BMD.BDL Model)
        {
            if (JointAnimations.Count != Model.Joints.FlatSkeleton.Count)
                throw new Exception("Number of Joints in the model doesn't match the number of Joint animations");
            for (int i = 0; i < Model.Joints.FlatSkeleton.Count; i++)
                JointAnimations[i].JointName = Model.Joints.FlatSkeleton[i].Name;
        }

        private void Read(Stream BCKFile)
        {
            if (BCKFile.ReadString(8) != Magic)
                throw new Exception($"Invalid Magic. Expected \"{Magic}\"");

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
            RotationFraction = (byte)BCKFile.ReadByte();
            Duration = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0);

            ushort BoneCount = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0),
                ScaleCount = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0), RotationCount = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0), TranslationCount = BitConverter.ToUInt16(BCKFile.ReadReverse(0, 2), 0);

            uint BoneTableOffset = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0) + TRKStart, ScaleTableOffset = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0) + TRKStart,
                RotationTableOffset = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0) + TRKStart, TranslationTableOffset = BitConverter.ToUInt32(BCKFile.ReadReverse(0, 4), 0) + TRKStart;

            List<float> ScaleTable = new List<float>();
            List<float> RotationTable = new List<float>();
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

            BCKFile.Seek(BoneTableOffset, SeekOrigin.Begin);
            short KeyFrameCount, TargetKeySet, TangentType;
            for (int i = 0; i < BoneCount; i++)
            {
                BoneAnimation Anim = new BoneAnimation();

                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                int TangentResult = (TangentType == 0x00 ? 3 : 4);
                for (int j = TargetKeySet; j < TargetKeySet + TangentResult * KeyFrameCount; j += TangentResult) 
                    Anim.ScaleX.Add(new J3DKeyFrame(ScaleTable, j, KeyFrameCount, TargetKeySet, TangentType));
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentResult = (TangentType == 0x00 ? 3 : 4);
                for (int j = TargetKeySet; j < TargetKeySet + TangentResult * KeyFrameCount; j += TangentResult)
                {
                    J3DKeyFrame key = new J3DKeyFrame(RotationTable, j, KeyFrameCount, TargetKeySet, TangentType);
                    key.ConvertRotation(RotationFraction);
                    Anim.RotationX.Add(key);
                }
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentResult = (TangentType == 0x00 ? 3 : 4);
                for (int j = TargetKeySet; j < TargetKeySet + TangentResult * KeyFrameCount; j += TangentResult)
                    Anim.TranslationX.Add(new J3DKeyFrame(TranslationTable, j, KeyFrameCount, TargetKeySet, TangentType));

                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentResult = (TangentType == 0x00 ? 3 : 4);
                for (int j = TargetKeySet; j < TargetKeySet + TangentResult * KeyFrameCount; j += TangentResult)
                    Anim.ScaleY.Add(new J3DKeyFrame(ScaleTable, j, KeyFrameCount, TargetKeySet, TangentType));
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentResult = (TangentType == 0x00 ? 3 : 4);
                for (int j = TargetKeySet; j < TargetKeySet + TangentResult * KeyFrameCount; j += TangentResult)
                {
                    J3DKeyFrame key = new J3DKeyFrame(RotationTable, j, KeyFrameCount, TargetKeySet, TangentType);
                    key.ConvertRotation(RotationFraction);
                    Anim.RotationY.Add(key);
                }
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentResult = (TangentType == 0x00 ? 3 : 4);
                for (int j = TargetKeySet; j < TargetKeySet + TangentResult * KeyFrameCount; j += TangentResult)
                    Anim.TranslationY.Add(new J3DKeyFrame(TranslationTable, j, KeyFrameCount, TargetKeySet, TangentType));

                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentResult = (TangentType == 0x00 ? 3 : 4);
                for (int j = TargetKeySet; j < TargetKeySet + TangentResult * KeyFrameCount; j += TangentResult)
                    Anim.ScaleZ.Add(new J3DKeyFrame(ScaleTable, j, KeyFrameCount, TargetKeySet, TangentType));
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentResult = (TangentType == 0x00 ? 3 : 4);
                for (int j = TargetKeySet; j < TargetKeySet + TangentResult * KeyFrameCount; j += TangentResult)
                {
                    J3DKeyFrame key = new J3DKeyFrame(RotationTable, j, KeyFrameCount, TargetKeySet, TangentType);
                    key.ConvertRotation(RotationFraction);
                    Anim.RotationZ.Add(key);
                }
                KeyFrameCount = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TargetKeySet = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentType = BitConverter.ToInt16(BCKFile.ReadReverse(0, 2), 0);
                TangentResult = (TangentType == 0x00 ? 3 : 4);
                for (int j = TargetKeySet; j < TargetKeySet + TangentResult * KeyFrameCount; j += TangentResult)
                    Anim.TranslationZ.Add(new J3DKeyFrame(TranslationTable, j, KeyFrameCount, TargetKeySet, TangentType));

                JointAnimations.Add(Anim);
            }
        }

        private void Write(Stream BCKFile)
        {
            List<float> AllTranslations = new List<float>();
            Dictionary<BoneAnimation, int> TranslationXOffsets = new Dictionary<BoneAnimation, int>();
            Dictionary<BoneAnimation, int> TranslationYOffsets = new Dictionary<BoneAnimation, int>();
            Dictionary<BoneAnimation, int> TranslationZOffsets = new Dictionary<BoneAnimation, int>();
            List<short> AllRotations = new List<short>();
            Dictionary<BoneAnimation, int> RotationXOffsets = new Dictionary<BoneAnimation, int>();
            Dictionary<BoneAnimation, int> RotationYOffsets = new Dictionary<BoneAnimation, int>();
            Dictionary<BoneAnimation, int> RotationZOffsets = new Dictionary<BoneAnimation, int>();
            List<float> AllScales = new List<float>();
            Dictionary<BoneAnimation, int> ScaleXOffsets = new Dictionary<BoneAnimation, int>();
            Dictionary<BoneAnimation, int> ScaleYOffsets = new Dictionary<BoneAnimation, int>();
            Dictionary<BoneAnimation, int> ScaleZOffsets = new Dictionary<BoneAnimation, int>();
            List<byte> AnimationTrackData = new List<byte>();

            for (int i = 0; i < JointAnimations.Count; i++)
            {
                RevertAnimation(JointAnimations[i],
                    ref AllTranslations, ref TranslationXOffsets, ref TranslationYOffsets, ref TranslationZOffsets,
                    ref AllRotations, ref RotationXOffsets, ref RotationYOffsets, ref RotationZOffsets,
                    ref AllScales, ref ScaleXOffsets, ref ScaleYOffsets, ref ScaleZOffsets);

                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].ScaleX.Count).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)ScaleXOffsets[JointAnimations[i]]).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].ScaleXTangent).Reverse());

                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].RotationX.Count).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)RotationXOffsets[JointAnimations[i]]).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].RotationXTangent).Reverse());

                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].TranslationX.Count).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)TranslationXOffsets[JointAnimations[i]]).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].TranslationXTangent).Reverse());

                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].ScaleY.Count).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)ScaleYOffsets[JointAnimations[i]]).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].ScaleYTangent).Reverse());

                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].RotationY.Count).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)RotationYOffsets[JointAnimations[i]]).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].RotationXTangent).Reverse());

                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].TranslationY.Count).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)TranslationYOffsets[JointAnimations[i]]).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].TranslationYTangent).Reverse());

                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].ScaleZ.Count).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)ScaleZOffsets[JointAnimations[i]]).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].ScaleZTangent).Reverse());

                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].RotationZ.Count).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)RotationZOffsets[JointAnimations[i]]).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].RotationZTangent).Reverse());

                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].TranslationZ.Count).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)TranslationZOffsets[JointAnimations[i]]).Reverse());
                AnimationTrackData.AddRange(BitConverter.GetBytes((ushort)JointAnimations[i].TranslationZTangent).Reverse());
            }


            BCKFile.WriteString(Magic);
            BCKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BCKFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);
            BCKFile.Write(new byte[16] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 16);

            uint Ank1Start = (uint)BCKFile.Position;
            BCKFile.WriteString(Magic2);
            BCKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BCKFile.WriteByte((byte)Loop);
            BCKFile.WriteByte(RotationFraction);
            BCKFile.WriteReverse(BitConverter.GetBytes(Duration), 0, 2);
            BCKFile.WriteReverse(BitConverter.GetBytes((ushort)JointAnimations.Count), 0, 2);
            BCKFile.WriteReverse(BitConverter.GetBytes((ushort)AllScales.Count), 0, 2);
            BCKFile.WriteReverse(BitConverter.GetBytes((ushort)AllRotations.Count), 0, 2);
            BCKFile.WriteReverse(BitConverter.GetBytes((ushort)AllTranslations.Count), 0, 2);
            BCKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //AnimTableOffset
            BCKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //ScaleTableOffset
            BCKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //RotationTableOffset
            BCKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //TranslationTableOffset

            AddPadding(BCKFile, 32);
            uint AnimTableOffset = (uint)BCKFile.Position;
            BCKFile.Write(AnimationTrackData.ToArray(), 0, AnimationTrackData.Count);
            AddPadding(BCKFile, 32);
            uint ScaleTableOffset = (uint)BCKFile.Position;
            for (int i = 0; i < AllScales.Count; i++)
                BCKFile.WriteReverse(BitConverter.GetBytes(AllScales[i]), 0, 4);
            AddPadding(BCKFile, 32);
            uint RotationTableOffset = (uint)BCKFile.Position;
            for (int i = 0; i < AllRotations.Count; i++)
                BCKFile.WriteReverse(BitConverter.GetBytes(AllRotations[i]), 0, 2);
            AddPadding(BCKFile, 32);
            uint TranslationTableOffset = (uint)BCKFile.Position;
            for (int i = 0; i < AllTranslations.Count; i++)
                BCKFile.WriteReverse(BitConverter.GetBytes(AllTranslations[i]), 0, 4);
            AddPadding(BCKFile, 32);

            BCKFile.Position = 0x08;
            BCKFile.WriteReverse(BitConverter.GetBytes(BCKFile.Length), 0, 4);
            BCKFile.Position = 0x24;
            BCKFile.WriteReverse(BitConverter.GetBytes(BCKFile.Length - Ank1Start), 0, 4);
            BCKFile.Position = 0x34;
            BCKFile.WriteReverse(BitConverter.GetBytes(AnimTableOffset        - Ank1Start), 0, 4);
            BCKFile.WriteReverse(BitConverter.GetBytes(ScaleTableOffset       - Ank1Start), 0, 4);
            BCKFile.WriteReverse(BitConverter.GetBytes(RotationTableOffset    - Ank1Start), 0, 4);
            BCKFile.WriteReverse(BitConverter.GetBytes(TranslationTableOffset - Ank1Start), 0, 4);
        }

        private void RevertAnimation(BoneAnimation Animation,
            ref List<float> AllTranslations, ref Dictionary<BoneAnimation, int> TranslationXOffsets, ref Dictionary<BoneAnimation, int> TranslationYOffsets, ref Dictionary<BoneAnimation, int> TranslationZOffsets,
            ref List<short> AllRotations, ref Dictionary<BoneAnimation, int> RotationXOffsets, ref Dictionary<BoneAnimation, int> RotationYOffsets, ref Dictionary<BoneAnimation, int> RotationZOffsets,
            ref List<float> AllScales, ref Dictionary<BoneAnimation, int> ScaleXOffsets, ref Dictionary<BoneAnimation, int> ScaleYOffsets, ref Dictionary<BoneAnimation, int> ScaleZOffsets)
        {
            RevertAnimationTrack(Animation, Animation.ScaleX, ref AllScales, ref ScaleXOffsets);
            RevertAnimationTrack(Animation, Animation.ScaleY, ref AllScales, ref ScaleYOffsets);
            RevertAnimationTrack(Animation, Animation.ScaleZ, ref AllScales, ref ScaleZOffsets);

            RevertAnimationTrack(Animation, Animation.RotationX, ref AllRotations, ref RotationXOffsets);
            RevertAnimationTrack(Animation, Animation.RotationY, ref AllRotations, ref RotationYOffsets);
            RevertAnimationTrack(Animation, Animation.RotationZ, ref AllRotations, ref RotationZOffsets);

            RevertAnimationTrack(Animation, Animation.TranslationX, ref AllTranslations, ref TranslationXOffsets);
            RevertAnimationTrack(Animation, Animation.TranslationY, ref AllTranslations, ref TranslationYOffsets);
            RevertAnimationTrack(Animation, Animation.TranslationZ, ref AllTranslations, ref TranslationZOffsets);
        }

        private void RevertAnimationTrack(BoneAnimation Animation, List<J3DKeyFrame> AnimationTrack, ref List<float> ActiveValues, ref Dictionary<BoneAnimation, int> ActiveOffsets)
        {
            List<float> CurrentFloatSequence = new List<float>();
            TangentMode TM;
            if (AnimationTrack.Count == 1)
            {
                CurrentFloatSequence.Add(AnimationTrack[0].Value);
                TM = TangentMode.SYNC;
            }
            else
            {
                TM = BoneAnimation.CheckTangent(AnimationTrack);
                for (int i = 0; i < AnimationTrack.Count; i++)
                {
                    CurrentFloatSequence.Add(AnimationTrack[i].Time);
                    CurrentFloatSequence.Add(AnimationTrack[i].Value);
                    CurrentFloatSequence.Add(AnimationTrack[i].IngoingTangent);
                    if (TM == TangentMode.DESYNC)
                        CurrentFloatSequence.Add(AnimationTrack[i].OutgoingTangent);
                }
            }
            int offset = FindSequence(ActiveValues, CurrentFloatSequence);
            if (offset == -1)
            {
                offset = ActiveValues.Count;
                ActiveValues.AddRange(CurrentFloatSequence);
            }
            ActiveOffsets.Add(Animation, offset);
        }
        private void RevertAnimationTrack(BoneAnimation Animation, List<J3DKeyFrame> AnimationTrack, ref List<short> ActiveValues, ref Dictionary<BoneAnimation, int> ActiveOffsets)
        {
            List<short> CurrentFloatSequence = new List<short>();
            TangentMode TM;
            float RotationMultiplier = (float)(Math.Pow(RotationFraction, 2) * (180.0 / 32768.0));
            if (AnimationTrack.Count == 1)
            {
                CurrentFloatSequence.Add((short)(AnimationTrack[0].Value / RotationMultiplier));
                TM = TangentMode.SYNC;
            }
            else
            {
                TM = BoneAnimation.CheckTangent(AnimationTrack);
                for (int i = 0; i < AnimationTrack.Count; i++)
                {
                    CurrentFloatSequence.Add((short)AnimationTrack[i].Time);
                    CurrentFloatSequence.Add((short)(AnimationTrack[i].Value / RotationMultiplier));
                    CurrentFloatSequence.Add((short)(AnimationTrack[i].IngoingTangent / RotationMultiplier));
                    if (TM == TangentMode.DESYNC)
                        CurrentFloatSequence.Add((short)(AnimationTrack[i].OutgoingTangent / RotationMultiplier));
                }
            }
            int offset = FindSequence(ActiveValues, CurrentFloatSequence);
            if (offset == -1)
            {
                offset = ActiveValues.Count;
                ActiveValues.AddRange(CurrentFloatSequence);
            }
            ActiveOffsets.Add(Animation, offset);
        }
        /// <summary>
        /// Represents a collection of Animation Tracks for a given bone (aka Joint)
        /// </summary>
        public class BoneAnimation
        {
            /// <summary>
            /// The name of the Joint this animation will affect
            /// </summary>
            public string JointName { get; set; } = null;
            /// <summary>
            /// X Translation Keyframes
            /// </summary>
            public List<J3DKeyFrame> TranslationX { get; set; } = new List<J3DKeyFrame>();
            /// <summary>
            /// Y Translation Keyframes
            /// </summary>
            public List<J3DKeyFrame> TranslationY { get; set; } = new List<J3DKeyFrame>();
            /// <summary>
            /// Z Translation Keyframes
            /// </summary>
            public List<J3DKeyFrame> TranslationZ { get; set; } = new List<J3DKeyFrame>();
            /// <summary>
            /// X Rotation Keyframes
            /// </summary>
            public List<J3DKeyFrame> RotationX    { get; set; } = new List<J3DKeyFrame>();
            /// <summary>
            /// Y Rotation Keyframes
            /// </summary>
            public List<J3DKeyFrame> RotationY    { get; set; } = new List<J3DKeyFrame>();
            /// <summary>
            /// Z Rotation Keyframes
            /// </summary>
            public List<J3DKeyFrame> RotationZ    { get; set; } = new List<J3DKeyFrame>();
            /// <summary>
            /// X Scale Keyframes
            /// </summary>
            public List<J3DKeyFrame> ScaleX       { get; set; } = new List<J3DKeyFrame>();
            /// <summary>
            /// Y Scale Keyframes
            /// </summary>
            public List<J3DKeyFrame> ScaleY       { get; set; } = new List<J3DKeyFrame>();
            /// <summary>
            /// Z Scale Keyframes
            /// </summary>
            public List<J3DKeyFrame> ScaleZ       { get; set; } = new List<J3DKeyFrame>();
            /// <summary>
            /// The <see cref="TangentMode"/> that the Translation X Track uses
            /// </summary>
            public TangentMode TranslationXTangent => CheckTangent(TranslationX);
            /// <summary>
            /// The <see cref="TangentMode"/> that the Translation Y Track uses
            /// </summary>
            public TangentMode TranslationYTangent => CheckTangent(TranslationY);
            /// <summary>
            /// The <see cref="TangentMode"/> that the Translation Z Track uses
            /// </summary>
            public TangentMode TranslationZTangent => CheckTangent(TranslationZ);
            /// <summary>
            /// The <see cref="TangentMode"/> that the Rotation X Track uses
            /// </summary>
            public TangentMode RotationXTangent => CheckTangent(RotationX);
            /// <summary>
            /// The <see cref="TangentMode"/> that the Rotation Y Track uses
            /// </summary>
            public TangentMode RotationYTangent => CheckTangent(RotationY);
            /// <summary>
            /// The <see cref="TangentMode"/> that the Rotation Z Track uses
            /// </summary>
            public TangentMode RotationZTangent => CheckTangent(RotationZ);
            /// <summary>
            /// The <see cref="TangentMode"/> that the Scale X Track uses
            /// </summary>
            public TangentMode ScaleXTangent => CheckTangent(ScaleX);
            /// <summary>
            /// The <see cref="TangentMode"/> that the Scale Y Track uses
            /// </summary>
            public TangentMode ScaleYTangent => CheckTangent(ScaleY);
            /// <summary>
            /// The <see cref="TangentMode"/> that the Scale Z Track uses
            /// </summary>
            public TangentMode ScaleZTangent => CheckTangent(ScaleZ);
            /// <summary>
            /// Determine the <see cref="TangentMode"/> of a given Keyframe Track
            /// </summary>
            /// <param name="Track">The track to get the <see cref="TangentMode"/> of</param>
            /// <returns></returns>
            public static TangentMode CheckTangent(List<J3DKeyFrame> Track) => Track.Any(KEY => KEY.IngoingTangent != KEY.OutgoingTangent) ? TangentMode.DESYNC : TangentMode.SYNC;

            /// <summary>
            /// JointName is only checked if in both objects JointName is not NULL. Everything else is checked regardless
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public override bool Equals(object obj)
            {
                if (!(obj is BoneAnimation animation))
                    return false;
                bool result = TranslationX.SequenceEqual(animation.TranslationX) &&
                       TranslationY.SequenceEqual(animation.TranslationY) &&
                       TranslationZ.SequenceEqual(animation.TranslationZ) &&
                       RotationX.SequenceEqual(animation.RotationX) &&
                       RotationY.SequenceEqual(animation.RotationY) &&
                       RotationZ.SequenceEqual(animation.RotationZ) &&
                       ScaleX.SequenceEqual(animation.ScaleX) &&
                       ScaleY.SequenceEqual(animation.ScaleY) &&
                       ScaleZ.SequenceEqual(animation.ScaleZ);
                return result && ((JointName != null && animation.JointName != null) ? JointName.Equals(animation.JointName) : true);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                var hashCode = -1797774961;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(JointName);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<J3DKeyFrame>>.Default.GetHashCode(TranslationX);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<J3DKeyFrame>>.Default.GetHashCode(TranslationY);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<J3DKeyFrame>>.Default.GetHashCode(TranslationZ);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<J3DKeyFrame>>.Default.GetHashCode(RotationX);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<J3DKeyFrame>>.Default.GetHashCode(RotationY);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<J3DKeyFrame>>.Default.GetHashCode(RotationZ);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<J3DKeyFrame>>.Default.GetHashCode(ScaleX);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<J3DKeyFrame>>.Default.GetHashCode(ScaleY);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<J3DKeyFrame>>.Default.GetHashCode(ScaleZ);
                hashCode = hashCode * -1521134295 + TranslationXTangent.GetHashCode();
                hashCode = hashCode * -1521134295 + TranslationYTangent.GetHashCode();
                hashCode = hashCode * -1521134295 + TranslationZTangent.GetHashCode();
                hashCode = hashCode * -1521134295 + RotationXTangent.GetHashCode();
                hashCode = hashCode * -1521134295 + RotationYTangent.GetHashCode();
                hashCode = hashCode * -1521134295 + RotationZTangent.GetHashCode();
                hashCode = hashCode * -1521134295 + ScaleXTangent.GetHashCode();
                hashCode = hashCode * -1521134295 + ScaleYTangent.GetHashCode();
                hashCode = hashCode * -1521134295 + ScaleZTangent.GetHashCode();
                return hashCode;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"{JointName ?? "Unnamed Bone"}: Translation: [{TranslationX.Count}, {TranslationY.Count}, {TranslationZ.Count}], Rotation: [{RotationX.Count}, {RotationY.Count}, {RotationZ.Count}], Scale: [{ScaleX.Count}, {ScaleY.Count}, {ScaleZ.Count}]";
            
            /// <summary>
            /// 
            /// </summary>
            /// <param name="animation1"></param>
            /// <param name="animation2"></param>
            /// <returns></returns>
            public static bool operator ==(BoneAnimation animation1, BoneAnimation animation2) => animation1.Equals(animation2);
            
            /// <summary>
            /// 
            /// </summary>
            /// <param name="animation1"></param>
            /// <param name="animation2"></param>
            /// <returns></returns>
            public static bool operator !=(BoneAnimation animation1, BoneAnimation animation2) => !(animation1 == animation2);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"BCK - {(JointAnimations.Count > 0 ? $"[{JointAnimations.Count} Joint Animation{(JointAnimations.Count > 1 ? "s" : "")}] " : "")}";

        /// <summary>
        /// Only checks the Loop Mode, Duration, RotationFraction, and all the Animations inside. See <see cref="BoneAnimation.Equals(object)"/> for more info
        /// </summary>
        /// <param name="obj">The other BCK</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (!(obj is BCK))
                return false;
            BCK BCK = obj as BCK;
            if (Loop != BCK.Loop || Duration != BCK.Duration || RotationFraction != BCK.RotationFraction || JointAnimations.Count != BCK.JointAnimations.Count)
                return false;
            bool IsEqual = true;
            for (int i = 0; i < JointAnimations.Count; i++)
            {
                IsEqual = JointAnimations[i] == BCK.JointAnimations[i];
                if (!IsEqual)
                    break;
            }
            return IsEqual;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            var hashCode = -1421908117;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + Loop.GetHashCode();
            hashCode = hashCode * -1521134295 + Duration.GetHashCode();
            hashCode = hashCode * -1521134295 + RotationFraction.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<List<BoneAnimation>>.Default.GetHashCode(JointAnimations);
            return hashCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="LEFT"></param>
        /// <param name="RIGHT"></param>
        /// <returns></returns>
        public static bool operator ==(BCK LEFT, BCK RIGHT) => LEFT.Equals(RIGHT);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="LEFT"></param>
        /// <param name="RIGHT"></param>
        /// <returns></returns>
        public static bool operator !=(BCK LEFT, BCK RIGHT) => !(LEFT == RIGHT);
    }
}
