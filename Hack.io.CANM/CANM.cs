using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

 //It's a keyframe setup. The header is size 0x20 with constant values. Following that are keyframe indexers. Listed in order, they are: 

 //   0x20 x_pos 
 //   0x2C y_pos 
 //   0x38 z_pos 
 //   0x44 dir_x 
 //   0x50 dir_y 
 //   0x5C dir_z 
 //   0x68 unknown 
 //   0x74 Zoom

 //   Each keyframe indexer is size 0xC 

 //   keyframe_indexer: 
 //   0x00 KeyframeCount 
 //   0x04 DataIndex
 //   0x08 Padding

 //   If element_count is 1, then the camera will use only 1 float value for that field throughout the intro.
 //   Otherwise, if element count is greater than 1, then the camera will use multiple keyframes which are 3 floats each: 

 //   Keyframe: 
 //   0x00 Time 
 //   0x04 Value
 //   0x08 Velocity


 //   The value table is at 0x80. It starts with a uint value for its size and then lists the table values as floats.
 //   It seems to always end with the floating point values 0.1, 1E+9 and NAN.

namespace Hack.io.CANM
{
    /// <summary>
    /// Intro Camera Files
    /// </summary>
    public class CANM
    {
        /// <summary>
        /// Filename of this CANM file.<para/>Set using <see cref="Save(string)"/>;
        /// </summary>
        public string FileName { get; private set; } = null;
        /// <summary>
        /// CANM File Header
        /// </summary>
        public Header Header;
        /// <summary>
        /// List of Keyframes
        /// </summary>
        public List<Keyset> Keys = new List<Keyset>();
        /// <summary>
        /// Open a CANM from a file
        /// </summary>
        /// <param name="filename">File to Open</param>
        public CANM(string filename)
        {
            FileStream canmFile = new FileStream(filename, FileMode.Open);
            Header = new Header(canmFile);

            for (int i = 0; i < 8; i++)
            {
                Keys.Add(new Keyset(canmFile, (Keyset.Value)i));
            }
            
            Header.FloatTableSize = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);

            foreach (Keyset ks in Keys)
                ks.GetKeyframes(canmFile);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Filename">New file to save to</param>
        public void Save(string Filename = null)
        {
            if (FileName == null && Filename == null)
                throw new Exception("No Filename has been given");
            else if (Filename != null)
                FileName = Filename;

            FileStream canmFile = new FileStream(FileName, FileMode.Create);

            byte[] Wright = new byte[4];
            canmFile.WriteString("ANDOCKAN");
            canmFile.WriteReverse(BitConverter.GetBytes(Header.Unknown1), 0, 4);
            canmFile.WriteReverse(BitConverter.GetBytes(Header.Unknown2), 0, 4);
            canmFile.WriteReverse(BitConverter.GetBytes(Header.Unknown3), 0, 4);
            canmFile.WriteReverse(BitConverter.GetBytes(Header.Unknown4), 0, 4);
            canmFile.WriteReverse(BitConverter.GetBytes(Header.Unknown5), 0, 4);
            Wright = BitConverter.GetBytes(Header.OffsetToFloats);
            canmFile.WriteReverse(Wright, 0, 4);


            int prevoffset = 0;
            for (int i = 0; i < Keys.Count; i++)
            {
                canmFile.WriteReverse(BitConverter.GetBytes(Keys[i].KeyframeCount), 0, 4); //Write KeyframeCount

                Wright = BitConverter.GetBytes(prevoffset);
                prevoffset += Keys[i].KeyframeCount * (Keys[i].KeyframeCount > 1 ? 3 : 1); //Write DataIndex
                canmFile.WriteReverse(Wright, 0, 4);

                canmFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);//Padding
            }

            List<byte> Miles = new List<byte>();
            foreach (Keyset K in Keys)
            {
                for (int i = 0; i < K.Keyframes.Count; i++)
                {
                    if (K.KeyframeCount == 1)
                    {
                        Wright = BitConverter.GetBytes(K.Keyframes[i].Value);
                        Array.Reverse(Wright);
                        Miles.AddRange(Wright);
                        continue;
                    }
                    Wright = BitConverter.GetBytes(K.Keyframes[i].Time);
                    Array.Reverse(Wright);
                    Miles.AddRange(Wright);
                    Wright = BitConverter.GetBytes(K.Keyframes[i].Value);
                    Array.Reverse(Wright);
                    Miles.AddRange(Wright);
                    Wright = BitConverter.GetBytes(K.Keyframes[i].Velocity);
                    Array.Reverse(Wright);
                    Miles.AddRange(Wright);
                }
            }

            Wright = new byte[4];
            Wright = BitConverter.GetBytes(Miles.Count + 8);
            Array.Reverse(Wright);
            canmFile.Write(Wright, 0, 4);
            Wright = Miles.ToArray();
            canmFile.Write(Wright, 0, Wright.Length);

            canmFile.Write(new byte[] { 0x3D, 0xCC, 0xCC, 0xCD, 0x4E, 0x6E, 0x6B, 0x28, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 12);
        }
    }
    /// <summary>
    /// CANM Header
    /// </summary>
    public class Header
    {
        /// <summary>
        /// File Indicator
        /// </summary>
        private readonly string Magic = "ANDOCKAN"; //0x08
        /// <summary>
        /// 
        /// </summary>
        public int Unknown1; // 0x00000001
        /// <summary>
        /// 
        /// </summary>
        public int Unknown2; // 0x00000000
        /// <summary>
        /// 
        /// </summary>
        public int Unknown3; // 0x00000001
        /// <summary>
        /// 
        /// </summary>
        public int Unknown4; // 0x00000004
        /// <summary>
        /// 
        /// </summary>
        public int Unknown5; // 0x000001E0
        /// <summary>
        /// 
        /// </summary>
        public int OffsetToFloats; // 0x00000060
        /// <summary>
        /// 
        /// </summary>
        public int FloatTableSize = 0;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="canmFile"></param>
        public Header(FileStream canmFile)
        {
            string check = canmFile.ReadString(8, Encoding.ASCII);
            if (check != Magic)
                throw new Exception("Failed to read the file!", new Exception("CANM Header is missing or corrupted."));

            Unknown1 = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);
            Unknown2 = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);
            Unknown3 = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);
            Unknown4 = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);
            Unknown5 = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);
            OffsetToFloats = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);
        }
    }

    /// <summary>
    /// Value Group
    /// </summary>
    public class Keyset
    {
        /// <summary>
        /// Determines what value this is
        /// </summary>
        public Value Name; //Not in the file. | Not to be written to the file.
        /// <summary>
        /// Value options
        /// </summary>
        public enum Value
        {
            /// <summary>
            /// X Position of the Camera
            /// </summary>
            XPos,
            /// <summary>
            /// Y Position of the Camera
            /// </summary>
            YPos,
            /// <summary>
            /// Z Position of the Camera
            /// </summary>
            ZPos,
            /// <summary>
            /// X Position to look at
            /// </summary>
            XDir,
            /// <summary>
            /// Y Position to look at
            /// </summary>
            YDir,
            /// <summary>
            /// Z Position to look at
            /// </summary>
            ZDir,
            /// <summary>
            /// The Camera's Roll Value. This rotate the camera view
            /// </summary>
            Roll,
            /// <summary>
            /// Zoom
            /// </summary>
            Zoom
        }

        /// <summary>
        /// The number of Keyframes
        /// </summary>
        public int KeyframeCount;
        /// <summary>
        /// 
        /// </summary>
        public int DataIndex;
        /// <summary>
        /// 
        /// </summary>
        private readonly int Padding; // 0x00000000
        /// <summary>
        /// 
        /// </summary>
        public List<Keyframe> Keyframes = new List<Keyframe>();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="canmFile"></param>
        /// <param name="value"></param>
        public Keyset(FileStream canmFile, Value value)
        {
            Name = value;
            KeyframeCount = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);
            DataIndex = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);
            Padding = BitConverter.ToInt32(canmFile.ReadReverse(0, 4), 0);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="canmFile"></param>
        public void GetKeyframes(FileStream canmFile)
        {
            long pos = canmFile.Position;
            for (int i = 0; i < KeyframeCount; i++)
            {
                canmFile.Position = pos;
                Keyframes.Add(new Keyframe(canmFile, (DataIndex * 4) + (12 * i), KeyframeCount == 1));
            }
            canmFile.Position = pos;
        }
    }

    /// <summary>
    /// Camera Keyframe
    /// </summary>
    public class Keyframe
    {
        /// <summary>
        /// The keyframe position in the timeline
        /// </summary>
        public float Time; //Position in the Timeline
        /// <summary>
        /// Value. Depends on what keyset this keyframe belongs to
        /// </summary>
        public float Value; //Value (Depends on what is being edited)
        /// <summary>
        /// Smoothness.....kinda
        /// </summary>
        public float Velocity; //Smoothness... kinda
        /// <summary>
        /// 
        /// </summary>
        /// <param name="canmFile"></param>
        /// <param name="ID"></param>
        /// <param name="IsSingle"></param>
        public Keyframe(FileStream canmFile, int ID, bool IsSingle)
        {
            canmFile.Seek(ID, SeekOrigin.Current);
            if (IsSingle)
            {
                Value = BitConverter.ToSingle(canmFile.ReadReverse(0, 4), 0);
                return;
            }
            Time = BitConverter.ToSingle(canmFile.ReadReverse(0, 4), 0);
            Value = BitConverter.ToSingle(canmFile.ReadReverse(0, 4), 0);
            Velocity = BitConverter.ToSingle(canmFile.ReadReverse(0, 4), 0);
        }
        /// <summary>
        /// Create a new Keyframe
        /// </summary>
        public Keyframe()
        {
            Time = 0;
            Value = 0;
            Velocity = 0;
        }
    }
}
