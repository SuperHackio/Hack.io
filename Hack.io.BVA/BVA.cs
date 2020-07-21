using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hack.io.BVA
{
    /// <summary>
    /// Binary Visiblilty Animation
    /// </summary>
    public class BVA
    {
        /// <summary>
        /// Filename of this <see cref="BVA"/> file.<para/>Set using <see cref="Save(string)"/>;
        /// </summary>
        public string Name { get; private set; } = null;
        /// <summary>
        /// Loop Mode of the BRK animation. See the <seealso cref="LoopMode"/> enum for values
        /// </summary>
        public LoopMode Loop { get; set; } = LoopMode.Once;
        /// <summary>
        /// Length of the animation in Frames. (Game Framerate = 1 second)
        /// <para/>Set during the saving process
        /// </summary>
        public ushort Time { get; private set; }
        /// <summary>
        /// Animations that apply to Certain shapes in a model
        /// </summary>
        public List<Animation> VisibilityAnimations { get; set; } = new List<Animation>();

        private readonly static string Magic = "J3D1bva1";
        private readonly static string Magic2 = "VAF1";

        /// <summary>
        /// Create an Empty BVA
        /// </summary>
        public BVA() { }
        /// <summary>
        /// Open a BVA from a file
        /// </summary>
        /// <param name="filename"></param>
        public BVA(string filename)
        {
            FileStream BVAFile = new FileStream(filename, FileMode.Open);
            Read(BVAFile);
            BVAFile.Close();
            Name = filename;
        }
        /// <summary>
        /// Read a BVA File from a Stream
        /// </summary>
        /// <param name="Stream"></param>
        public BVA(Stream Stream) => Read(Stream);

        /// <summary>
        /// Save the BVA to a file
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
        /// Save the BVA file to a memorystream
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
        public override string ToString() => $"{new FileInfo(Name).Name} {(VisibilityAnimations.Count > 0 ? $"[{VisibilityAnimations.Count} Visibility Animation{(VisibilityAnimations.Count > 1 ? "s" : "")}] " : "")}";

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
            /// Visibility Triggers
            /// <para/>Set a keyframe to TRUE to show the shape. Set to False to hide the shape
            /// </summary>
            public List<bool> Keyframes { get; set; } = new List<bool>();
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"{Keyframes.Count} Keyframe{(Keyframes.Count > 1 ? "s":"")}";
        }

        private void Read(Stream BVAFile)
        {
            if (BVAFile.ReadString(8) != Magic)
                throw new Exception("Invalid Identifier. Expected \"J3D1bva1\"");

            uint Filesize = BitConverter.ToUInt32(BVAFile.ReadReverse(0, 4), 0);
            uint SectionCount = BitConverter.ToUInt32(BVAFile.ReadReverse(0, 4), 0);
            if (SectionCount != 1)
                throw new Exception(SectionCount > 1 ? "More than 1 section is in this BVA! Please send it to Super Hackio for investigation" : "There are no sections in this BVA!");

            BVAFile.Seek(0x10, SeekOrigin.Current);
            uint VAF1Start = (uint)BVAFile.Position;
            if (BVAFile.ReadString(4) != Magic2)
                throw new Exception("Invalid Identifier. Expected \"VAF1\"");

            uint VAF1Length = BitConverter.ToUInt32(BVAFile.ReadReverse(0, 4), 0);
            Loop = (LoopMode)BVAFile.ReadByte();
            BVAFile.ReadByte();
            Time = BitConverter.ToUInt16(BVAFile.ReadReverse(0, 2), 0);

            ushort VisibilityAnimationCount = BitConverter.ToUInt16(BVAFile.ReadReverse(0, 2), 0), ShowTableCount = BitConverter.ToUInt16(BVAFile.ReadReverse(0, 2), 0);
            uint VisibilityAnimTableOffset = BitConverter.ToUInt32(BVAFile.ReadReverse(0, 4), 0) + VAF1Start, ShowTableOffset = BitConverter.ToUInt32(BVAFile.ReadReverse(0, 4), 0) + VAF1Start;

            ushort ShowCount, ShowFirstID;
            BVAFile.Seek(VisibilityAnimTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < VisibilityAnimationCount; i++)
            {
                ShowCount = BitConverter.ToUInt16(BVAFile.ReadReverse(0, 2), 0);
                ShowFirstID = BitConverter.ToUInt16(BVAFile.ReadReverse(0, 2), 0);
                long pauseposition = BVAFile.Position;
                Animation Anim = new Animation();
                for (int j = 0; j < ShowCount; j++)
                {
                    BVAFile.Seek(ShowTableOffset + ShowFirstID + j, SeekOrigin.Begin);
                    Anim.Keyframes.Add(BVAFile.ReadByte() == 0x01);
                }
                VisibilityAnimations.Add(Anim);
                BVAFile.Position = pauseposition;
            }
        }

        private void Write(Stream BRKFile)
        {
            string Padding = "Hack.io.BVA © Super Hackio Incorporated 2019-2020";

            Time = 0;
            for (int i = 0; i < VisibilityAnimations.Count; i++)
                if (Time < VisibilityAnimations[i].Keyframes.Count - 1)
                    Time = (ushort)(VisibilityAnimations[i].Keyframes.Count - 1);

            BRKFile.WriteString(Magic);
            BRKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BRKFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);
            BRKFile.Write(new byte[16] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 16);
            long VAFStart = BRKFile.Position;
            BRKFile.WriteString(Magic2);
            BRKFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            BRKFile.WriteByte((byte)Loop);
            BRKFile.WriteByte(0xFF); //Padding
            BRKFile.WriteReverse(BitConverter.GetBytes(Time), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((ushort)VisibilityAnimations.Count), 0, 2);
            long ReturnToWriteOffsets = BRKFile.Position;
            BRKFile.Write(new byte[2] { 0xDD, 0xDD }, 0, 2);
            BRKFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4);
            BRKFile.Write(new byte[4] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, 4);
            List<bool> ShowList = new List<bool>();
            #region Padding
            int PadCount = 0;
            while (BRKFile.Position % 32 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion
            long AnimOffset = BRKFile.Position;
            for (int i = 0; i < VisibilityAnimations.Count; i++)
            {
                BRKFile.WriteReverse(BitConverter.GetBytes((ushort)VisibilityAnimations[i].Keyframes.Count), 0, 2);
                BRKFile.Write(new byte[2] { 0xDD, 0xDD }, 0, 2);
            }
            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 16 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
            #endregion
            long ShowOffset = BRKFile.Position;
            List<ushort> ShowOffsets = new List<ushort>();

            for (int i = 0; i < VisibilityAnimations.Count; i++)
            {
                if (ShowList.Count == 0)
                {
                    ShowOffsets.Add(0);
                    for (int j = 0; j < VisibilityAnimations[i].Keyframes.Count; j++)
                        ShowList.Add(VisibilityAnimations[i].Keyframes[j]);
                }
                else
                {
                    if (MatchFind(ShowList, VisibilityAnimations[i].Keyframes, out int ID))
                        ShowOffsets.Add((ushort)ID);
                    else
                    {
                        ShowOffsets.Add((ushort)ShowList.Count);
                        for (int j = 0; j < VisibilityAnimations[i].Keyframes.Count; j++)
                            ShowList.Add(VisibilityAnimations[i].Keyframes[j]);
                    }
                }
            }
            for (int i = 0; i < ShowList.Count; i++)
                BRKFile.WriteByte((byte)(ShowList[i] ? 0x01 : 0x00));

            #region Padding
            PadCount = 0;
            while (BRKFile.Position % 4 != 0)
            {
                BRKFile.WriteByte((byte)Padding[PadCount]);
                PadCount++;
            }
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
            BRKFile.WriteReverse(BitConverter.GetBytes((uint)BRKFile.Length), 0, 4);
            BRKFile.Position = 0x24;
            BRKFile.WriteReverse(BitConverter.GetBytes((uint)(BRKFile.Length - VAFStart)), 0, 4);
            BRKFile.Seek(ReturnToWriteOffsets, SeekOrigin.Begin);
            BRKFile.WriteReverse(BitConverter.GetBytes((ushort)ShowList.Count), 0, 2);
            BRKFile.WriteReverse(BitConverter.GetBytes((uint)(AnimOffset - VAFStart)), 0, 4);
            BRKFile.WriteReverse(BitConverter.GetBytes((uint)(ShowOffset - VAFStart)), 0, 4);
            BRKFile.Seek(AnimOffset, SeekOrigin.Begin);
            for (int i = 0; i < ShowOffsets.Count; i++)
            {
                BRKFile.Position += 0x02;
                BRKFile.WriteReverse(BitConverter.GetBytes(ShowOffsets[i]), 0, 2);
            }
        }

        private bool MatchFind(List<bool> AllValues, List<bool> CurrentValues, out int StartID)
        {
            StartID = 0;
            if (CurrentValues.Count > AllValues.Count)
                return false;
            else
            {
                for (int i = 0; i < AllValues.Count; i++)
                {
                    if (i + CurrentValues.Count > AllValues.Count)
                        return false;
                    else
                    {
                        bool Found = true;
                        for (int x = 0; x < CurrentValues.Count; x++)
                        {
                            if (CurrentValues[x] != AllValues[i + x])
                                Found = false;
                            if (!Found)
                                break;
                        }
                        if (Found)
                        {
                            StartID = i;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        //=====================================================================

        /// <summary>
        /// Cast a BVA to a RARCFile
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator RARC.RARCFile(BVA x)
        {
            return new RARC.RARCFile() { FileData = x.Save().GetBuffer(), Name = x.Name };
        }

        /// <summary>
        /// Cast a RARCFile to a BVA
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator BVA(RARC.RARCFile x)
        {
            return new BVA(x.GetMemoryStream());
        }

        //=====================================================================
    }
}
