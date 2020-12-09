using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hack.io;

namespace Hack.io.CIT
{
    /// <summary>
    /// Chord Information Table.
    /// </summary>
    public class CIT
    {
        private static readonly string Magic = "CITS";
        /// <summary>
        /// A list of Chords used in this file
        /// </summary>
        public List<Chord> Chords { get; set; } = new List<Chord>();
        /// <summary>
        /// A list of ScalePairs used in this file
        /// </summary>
        public List<Tuple<Scale, Scale>> Scales { get; set; } = new List<Tuple<Scale, Scale>>();
        /// <summary>
        /// Create an empty CIT
        /// </summary>
        public CIT()
        {

        }
        /// <summary>
        /// Load a CIT From a file
        /// </summary>
        /// <param name="file"></param>
        public CIT(string file)
        {
            FileStream FS = new FileStream(file, FileMode.Open);
            Read(FS);
            FS.Close();
        }
        /// <summary>
        /// Read a CIT from a Stream
        /// </summary>
        /// <param name="stream"></param>
        public CIT(Stream stream) => Read(stream);
        /// <summary>
        /// Save this CIT to a file
        /// </summary>
        /// <param name="file"></param>
        public void Save(string file)
        {
            FileStream FS = new FileStream(file, FileMode.Create);
            Write(FS);
            FS.Close();
        }
        /// <summary>
        /// Save this CIT into a stream
        /// </summary>
        /// <param name="stream"></param>
        public void Save(Stream stream) => Write(stream);

        private void Read(Stream CITFile)
        {
            CITFile.Position += 0x04;

            if (!CITFile.ReadString(4).Equals(Magic))
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

            uint FileSize = BitConverter.ToUInt32(CITFile.ReadReverse(0, 4), 0);
            ushort ChordCount = BitConverter.ToUInt16(CITFile.ReadReverse(0, 2), 0), ScaleCount = BitConverter.ToUInt16(CITFile.ReadReverse(0, 2), 0);
            int[] ChordPointers = new int[ChordCount], ScalePairPointers = new int[ScaleCount];
            for (int i = 0; i < ChordCount; i++)
            {
                ChordPointers[i] = BitConverter.ToInt32(CITFile.ReadReverse(0, 4), 0);
                long PausePosition = CITFile.Position;
                CITFile.Position = ChordPointers[i];
                Chords.Add(new Chord((Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte()));
                CITFile.Position = PausePosition;
            }
            for (int i = 0; i < ScaleCount; i++)
            {
                ScalePairPointers[i] = BitConverter.ToInt32(CITFile.ReadReverse(0, 4), 0);
                long PausePosition = CITFile.Position;
                CITFile.Position = ScalePairPointers[i];
                int OffsetA = BitConverter.ToInt32(CITFile.ReadReverse(0, 4), 0);
                int OffsetB = BitConverter.ToInt32(CITFile.ReadReverse(0, 4), 0);
                CITFile.Position = OffsetA;
                Scale ScaleA = new Scale((Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte());
                CITFile.Position = OffsetB;
                Scale ScaleB = new Scale((Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte(), (Note)CITFile.ReadByte());
                Scales.Add(new Tuple<Scale, Scale>(ScaleA, ScaleB));
                CITFile.Position = PausePosition;
            }
        }

        private void Write(Stream CITFile)
        {
            CITFile.Write(new byte[4], 0, 4);
            CITFile.WriteString(Magic);
            CITFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            CITFile.WriteReverse(BitConverter.GetBytes((ushort)Chords.Count), 0, 2);
            CITFile.WriteReverse(BitConverter.GetBytes((ushort)Scales.Count), 0, 2);
            int TableOffset = 0x10 + (Chords.Count * 4) + (Scales.Count * 4);
            List<byte> ChordBytes = new List<byte>();
            for (int i = 0; i < Chords.Count; i++)
            {
                CITFile.WriteReverse(BitConverter.GetBytes(TableOffset), 0, 4);
                TableOffset += 0x08;
                ChordBytes.Add((byte)Chords[i].RootNote);
                ChordBytes.Add((byte)Chords[i].Triad.Note1);
                ChordBytes.Add((byte)Chords[i].Triad.Note2);
                ChordBytes.Add((byte)Chords[i].Triad.Note3);
                ChordBytes.Add((byte)Chords[i].Extension.Note1);
                ChordBytes.Add((byte)Chords[i].Extension.Note2);
                ChordBytes.Add((byte)Chords[i].Extension.Note3);
                ChordBytes.Add((byte)Chords[i].Extension.Note4);
            }
            List<byte> ScaleBytes = new List<byte>();
            for (int i = 0; i < Scales.Count; i++)
            {
                CITFile.WriteReverse(BitConverter.GetBytes(TableOffset), 0, 4);
                ScaleBytes.AddRange(BitConverter.GetBytes(TableOffset + 0x08).Reverse());
                ScaleBytes.AddRange(BitConverter.GetBytes(TableOffset + 0x08 + 0x0C).Reverse());
                ScaleBytes.Add((byte)Scales[i].Item1.Note1);
                ScaleBytes.Add((byte)Scales[i].Item1.Note2);
                ScaleBytes.Add((byte)Scales[i].Item1.Note3);
                ScaleBytes.Add((byte)Scales[i].Item1.Note4);
                ScaleBytes.Add((byte)Scales[i].Item1.Note5);
                ScaleBytes.Add((byte)Scales[i].Item1.Note6);
                ScaleBytes.Add((byte)Scales[i].Item1.Note7);
                ScaleBytes.Add((byte)Scales[i].Item1.Note8);
                ScaleBytes.Add((byte)Scales[i].Item1.Note9);
                ScaleBytes.Add((byte)Scales[i].Item1.Note10);
                ScaleBytes.Add((byte)Scales[i].Item1.Note11);
                ScaleBytes.Add((byte)Scales[i].Item1.Note12);
                ScaleBytes.Add((byte)Scales[i].Item2.Note1);
                ScaleBytes.Add((byte)Scales[i].Item2.Note2);
                ScaleBytes.Add((byte)Scales[i].Item2.Note3);
                ScaleBytes.Add((byte)Scales[i].Item2.Note4);
                ScaleBytes.Add((byte)Scales[i].Item2.Note5);
                ScaleBytes.Add((byte)Scales[i].Item2.Note6);
                ScaleBytes.Add((byte)Scales[i].Item2.Note7);
                ScaleBytes.Add((byte)Scales[i].Item2.Note8);
                ScaleBytes.Add((byte)Scales[i].Item2.Note9);
                ScaleBytes.Add((byte)Scales[i].Item2.Note10);
                ScaleBytes.Add((byte)Scales[i].Item2.Note11);
                ScaleBytes.Add((byte)Scales[i].Item2.Note12);
                TableOffset += 0x20;
            }
            CITFile.Write(ChordBytes.ToArray(), 0, ChordBytes.Count);
            CITFile.Write(ScaleBytes.ToArray(), 0, ScaleBytes.Count);
            CITFile.Position = 0x08;
            CITFile.WriteReverse(BitConverter.GetBytes((uint)CITFile.Length), 0, 4);
            CITFile.Position = CITFile.Length;
            CITFile.PadTo(32);
        }

        /// <summary>
        /// A musical chord used by CIT files.
        /// </summary>
        public class Chord
        {
            /// <summary>
            /// The Root Note of the Chord
            /// </summary>
            public Note RootNote { get; set; } = Note.NONE;
            /// <summary>
            /// The main Triad of the Chord
            /// </summary>
            public Triad Triad { get; set; } = new Triad();
            /// <summary>
            /// Any extensions to the Triad
            /// </summary>
            public Extensions Extension { get; set; } = new Extensions();
            /// <summary>
            /// Creates a new chord
            /// </summary>
            /// <param name="Root"></param>
            /// <param name="TriadNote1"></param>
            /// <param name="TriadNote2"></param>
            /// <param name="TriadNote3"></param>
            /// <param name="ExtensionNote1"></param>
            /// <param name="ExtensionNote2"></param>
            /// <param name="ExtensionNote3"></param>
            /// <param name="ExtensionNote4"></param>
            public Chord(Note Root, Note TriadNote1, Note TriadNote2, Note TriadNote3, Note ExtensionNote1, Note ExtensionNote2, Note ExtensionNote3, Note ExtensionNote4)
            {
                RootNote = Root;
                Triad = new Triad(TriadNote1, TriadNote2, TriadNote3);
                Extension = new Extensions(ExtensionNote1, ExtensionNote2, ExtensionNote3, ExtensionNote4);
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"RootNote: {RootNote.ToString().PadRight(2,' ')}, Triad: {Triad.Note1.ToString().PadRight(2, ' ')} {Triad.Note2.ToString().PadRight(2, ' ')} {Triad.Note3.ToString().PadRight(2, ' ')}, Extensions: {Extension.Note1.ToString().PadRight(2, ' ')} {Extension.Note2.ToString().PadRight(2, ' ')} {Extension.Note3.ToString().PadRight(2, ' ')} {Extension.Note4.ToString().PadRight(2, ' ')}";
        }
        /// <summary>
        /// A Chord Triad used by CIT Files
        /// </summary>
        public struct Triad
        {
            /// <summary>
            /// The Root Key of the Triad (Or whatever the lowest note is)
            /// </summary>
            public Note Note1;
            /// <summary>
            /// The Third/Second note in the triad (Depends on what the triad is)
            /// </summary>
            public Note Note2;
            /// <summary>
            /// The Fifth/Fourth note in the triad (Depends on what the triad is)
            /// </summary>
            public Note Note3;
            /// <summary>
            /// Create a new Triad
            /// </summary>
            /// <param name="note1">Root note</param>
            /// <param name="note2">Third/Second note</param>
            /// <param name="note3">Fifth/Fourth note</param>
            public Triad(Note note1, Note note2, Note note3)
            {
                Note1 = note1;
                Note2 = note2;
                Note3 = note3;
            }
        }
        /// <summary>
        /// Triad Extensions used by CIT Files
        /// </summary>
        public struct Extensions
        {
            /// <summary>
            /// Seventh/Sixth note in the Extension (Depends on what the extension is)
            /// </summary>
            public Note Note1;
            /// <summary>
            /// Nineth/Eighth note in the Extension (Depends on what the extension is)
            /// </summary>
            public Note Note2;
            /// <summary>
            /// Eleventh/Tenth note in the Extension (Depends on what the extension is)
            /// </summary>
            public Note Note3;
            /// <summary>
            /// Thirteenth/Twelvth note in the Extension (Depends on what the extension is)
            /// </summary>
            public Note Note4;
            /// <summary>
            /// Creates an extension so you can go beyond triads
            /// </summary>
            /// <param name="note1">Seventh/Sixth note</param>
            /// <param name="note2">Nineth/Eighth note</param>
            /// <param name="note3">Eleventh/Tenth note</param>
            /// <param name="note4">Thirteenth/Twelvth note</param>
            public Extensions(Note note1, Note note2, Note note3, Note note4)
            {
                Note1 = note1;
                Note2 = note2;
                Note3 = note3;
                Note4 = note4;
            }
        }
        /// <summary>
        /// A musical scale used by CIT Files
        /// </summary>
        public struct Scale
        {
            /// <summary>
            /// Component Note 1
            /// </summary>
            public Note Note1;
            /// <summary>
            /// Component Note 2
            /// </summary>
            public Note Note2;
            /// <summary>
            /// Component Note 3
            /// </summary>
            public Note Note3;
            /// <summary>
            /// Component Note 4
            /// </summary>
            public Note Note4;
            /// <summary>
            /// Component Note 5
            /// </summary>
            public Note Note5;
            /// <summary>
            /// Component Note 6
            /// </summary>
            public Note Note6;
            /// <summary>
            /// Component Note 7
            /// </summary>
            public Note Note7;
            /// <summary>
            /// Component Note 8
            /// </summary>
            public Note Note8;
            /// <summary>
            /// Component Note 9
            /// </summary>
            public Note Note9;
            /// <summary>
            /// Component Note 10
            /// </summary>
            public Note Note10;
            /// <summary>
            /// Component Note 11
            /// </summary>
            public Note Note11;
            /// <summary>
            /// Component Note 12
            /// </summary>
            public Note Note12;
            /// <summary>
            /// Create a new Scale
            /// </summary>
            /// <param name="note1"></param>
            /// <param name="note2"></param>
            /// <param name="note3"></param>
            /// <param name="note4"></param>
            /// <param name="note5"></param>
            /// <param name="note6"></param>
            /// <param name="note7"></param>
            /// <param name="note8"></param>
            /// <param name="note9"></param>
            /// <param name="note10"></param>
            /// <param name="note11"></param>
            /// <param name="note12"></param>
            public Scale(Note note1, Note note2, Note note3, Note note4, Note note5, Note note6, Note note7, Note note8, Note note9, Note note10, Note note11, Note note12)
            {
                Note1 = note1;
                Note2 = note2;
                Note3 = note3;
                Note4 = note4;
                Note5 = note5;
                Note6 = note6;
                Note7 = note7;
                Note8 = note8;
                Note9 = note9;
                Note10 = note10;
                Note11 = note11;
                Note12 = note12;
            }
            /// <summary>
            /// Creates a copy of this Scale
            /// </summary>
            /// <returns></returns>
            public Scale Copy() => new Scale(Note1, Note2, Note3, Note4, Note5, Note6, Note7, Note8, Note9, Note10, Note11, Note12);
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"Components: {Note1.ToString()} {Note2.ToString()} {Note3.ToString()} {Note4.ToString()} {Note5.ToString()} {Note6.ToString()} {Note7.ToString()} {Note8.ToString()} {Note9.ToString()} {Note10.ToString()} {Note11.ToString()} {Note12.ToString()}";
            /// <summary>
            /// Compares this Scale to another object (Preferably another Scale object)
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public override bool Equals(object obj)
            {
                if (!(obj is Scale))
                {
                    return false;
                }

                var scale = (Scale)obj;
                return Note1 == scale.Note1 &&
                       Note2 == scale.Note2 &&
                       Note3 == scale.Note3 &&
                       Note4 == scale.Note4 &&
                       Note5 == scale.Note5 &&
                       Note6 == scale.Note6 &&
                       Note7 == scale.Note7 &&
                       Note8 == scale.Note8 &&
                       Note9 == scale.Note9 &&
                       Note10 == scale.Note10 &&
                       Note11 == scale.Note11 &&
                       Note12 == scale.Note12;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                var hashCode = -1148755155;
                hashCode = hashCode * -1521134295 + Note1.GetHashCode();
                hashCode = hashCode * -1521134295 + Note2.GetHashCode();
                hashCode = hashCode * -1521134295 + Note3.GetHashCode();
                hashCode = hashCode * -1521134295 + Note4.GetHashCode();
                hashCode = hashCode * -1521134295 + Note5.GetHashCode();
                hashCode = hashCode * -1521134295 + Note6.GetHashCode();
                hashCode = hashCode * -1521134295 + Note7.GetHashCode();
                hashCode = hashCode * -1521134295 + Note8.GetHashCode();
                hashCode = hashCode * -1521134295 + Note9.GetHashCode();
                hashCode = hashCode * -1521134295 + Note10.GetHashCode();
                hashCode = hashCode * -1521134295 + Note11.GetHashCode();
                hashCode = hashCode * -1521134295 + Note12.GetHashCode();
                return hashCode;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="scale1"></param>
            /// <param name="scale2"></param>
            /// <returns></returns>
            public static bool operator ==(Scale scale1, Scale scale2)
            {
                return scale1.Equals(scale2);
            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="scale1"></param>
            /// <param name="scale2"></param>
            /// <returns></returns>
            public static bool operator !=(Scale scale1, Scale scale2)
            {
                return !(scale1 == scale2);
            }
        }
        /// <summary>
        /// All possible notes that can be used in CIT Files
        /// </summary>
        public enum Note : byte
        {
            /// <summary>
            /// C
            /// </summary>
            C = 0x00,
            /// <summary>
            /// C#
            /// </summary>
            Db = 0x01,
            /// <summary>
            /// D
            /// </summary>
            D = 0x02,
            /// <summary>
            /// D#
            /// </summary>
            Eb = 0x03,
            /// <summary>
            /// E
            /// </summary>
            E = 0x04,
            /// <summary>
            /// F
            /// </summary>
            F = 0x05,
            /// <summary>
            /// F#
            /// </summary>
            Gb = 0x06,
            /// <summary>
            /// G
            /// </summary>
            G = 0x07,
            /// <summary>
            /// G#
            /// </summary>
            Ab = 0x08,
            /// <summary>
            /// A
            /// </summary>
            A = 0x09,
            /// <summary>
            /// A#
            /// </summary>
            Bb = 0x0A,
            /// <summary>
            /// B
            /// </summary>
            B = 0x0B,
            /// <summary>
            /// Indicates no note
            /// </summary>
            NONE = 0x7F
        }
    }
}
