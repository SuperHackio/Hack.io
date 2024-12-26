using Hack.io.Interface;
using Hack.io.Utility;
using System.Text;

namespace Hack.io.CIT;

/// <summary>
/// Chord Information Table.
/// </summary>
public class CIT : ILoadSaveFile
{
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    private static readonly string MAGIC = "CITS";
    /// <summary>
    /// A list of Chords used in this file
    /// </summary>
    public List<Chord> Chords { get; set; } = [];
    /// <summary>
    /// A list of ScalePairs used in this file
    /// </summary>
    public List<(Scale Up, Scale Down)> Scales { get; set; } = [];

    public void Load(Stream Strm)
    {
        Strm.Position += 0x04;

        FileUtil.ExceptionOnBadMagic(Strm, MAGIC);

        _ = Strm.ReadUInt32();
        ushort ChordCount = Strm.ReadUInt16(), ScaleCount = Strm.ReadUInt16();
        int[] ChordPointers = new int[ChordCount], ScalePairPointers = new int[ScaleCount];
        for (int i = 0; i < ChordCount; i++)
        {
            ChordPointers[i] = Strm.ReadInt32();
            long PausePosition = Strm.Position;
            Strm.Position = ChordPointers[i];
            Chords.Add(new((Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte()));
            Strm.Position = PausePosition;
        }
        for (int i = 0; i < ScaleCount; i++)
        {
            ScalePairPointers[i] = Strm.ReadInt32();
            long PausePosition = Strm.Position;
            Strm.Position = ScalePairPointers[i];
            int OffsetA = Strm.ReadInt32();
            int OffsetB = Strm.ReadInt32();
            Strm.Position = OffsetA;
            Scale ScaleA = new((Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte());
            Strm.Position = OffsetB;
            Scale ScaleB = new((Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte(), (Note)Strm.ReadByte());
            Scales.Add((ScaleA, ScaleB));
            Strm.Position = PausePosition;
        }
    }

    public void Save(Stream Strm)
    {
        Strm.WriteUInt32(0x00000000); //Reserved for the game
        Strm.WriteString(MAGIC, Encoding.ASCII, null);
        Strm.WritePlaceholder(4);
        Strm.WriteInt16((short)Chords.Count);
        Strm.WriteInt16((short)Scales.Count);

        int TableOffset = 0x10 + (Chords.Count * 4) + (Scales.Count * 4);
        List<byte> ChordBytes = [];
        for (int i = 0; i < Chords.Count; i++)
        {
            Strm.WriteInt32(TableOffset);
            TableOffset += 0x08;
            ChordBytes.Add((byte)Chords[i].BassNote);
            ChordBytes.Add((byte)Chords[i].ToneNotes.Root);
            ChordBytes.Add((byte)Chords[i].ToneNotes.A);
            ChordBytes.Add((byte)Chords[i].ToneNotes.B);
            ChordBytes.Add((byte)Chords[i].ToneNotes.C);
            ChordBytes.Add((byte)Chords[i].AddNotes.A);
            ChordBytes.Add((byte)Chords[i].AddNotes.B);
            ChordBytes.Add((byte)Chords[i].AddNotes.C);
        }
        MemoryStream ms = new(Scales.Count * 0x20);
        for (int i = 0; i < Scales.Count; i++)
        {
            Strm.WriteInt32(TableOffset);
            ms.WriteInt32(TableOffset + 0x08);
            ms.WriteInt32(TableOffset + 0x08 + 0x0C);
            ms.WriteByte((byte)Scales[i].Up.Note1);
            ms.WriteByte((byte)Scales[i].Up.Note2);
            ms.WriteByte((byte)Scales[i].Up.Note3);
            ms.WriteByte((byte)Scales[i].Up.Note4);
            ms.WriteByte((byte)Scales[i].Up.Note5);
            ms.WriteByte((byte)Scales[i].Up.Note6);
            ms.WriteByte((byte)Scales[i].Up.Note7);
            ms.WriteByte((byte)Scales[i].Up.Note8);
            ms.WriteByte((byte)Scales[i].Up.Note9);
            ms.WriteByte((byte)Scales[i].Up.Note10);
            ms.WriteByte((byte)Scales[i].Up.Note11);
            ms.WriteByte((byte)Scales[i].Up.Note12);
            ms.WriteByte((byte)Scales[i].Down.Note1);
            ms.WriteByte((byte)Scales[i].Down.Note2);
            ms.WriteByte((byte)Scales[i].Down.Note3);
            ms.WriteByte((byte)Scales[i].Down.Note4);
            ms.WriteByte((byte)Scales[i].Down.Note5);
            ms.WriteByte((byte)Scales[i].Down.Note6);
            ms.WriteByte((byte)Scales[i].Down.Note7);
            ms.WriteByte((byte)Scales[i].Down.Note8);
            ms.WriteByte((byte)Scales[i].Down.Note9);
            ms.WriteByte((byte)Scales[i].Down.Note10);
            ms.WriteByte((byte)Scales[i].Down.Note11);
            ms.WriteByte((byte)Scales[i].Down.Note12);
            TableOffset += 0x20;
        }
        Strm.Write([.. ChordBytes]);
        Strm.Write(ms.ToArray());
        Strm.Position = 0x08;
        Strm.WriteUInt32((uint)Strm.Length);
        Strm.Position = Strm.Length;
        Strm.PadTo(32);
    }

    /// <summary>
    /// A musical chord used by CIT files.
    /// </summary>
    /// <remarks>
    /// Creates a new chord
    /// </remarks>
    /// <param name="Bass"></param>
    /// <param name="Tone1"></param>
    /// <param name="Tone2"></param>
    /// <param name="Tone3"></param>
    /// <param name="Tone4"></param>
    /// <param name="Add1"></param>
    /// <param name="Add2"></param>
    /// <param name="Add3"></param>
    public class Chord(Note Bass, Note Tone1, Note Tone2, Note Tone3, Note Tone4, Note Add1, Note Add2, Note Add3)
    {
        /// <summary>
        /// The Root Note of the Chord
        /// </summary>
        public Note BassNote { get; set; } = Bass;

        public (Note Root, Note A, Note B, Note C) ToneNotes { get; set; } = (Tone1, Tone2, Tone3, Tone4);
        public (Note A, Note B, Note C) AddNotes { get; set; } = (Add1, Add2, Add3);

        public override string ToString() => $"Components: {BassNote} | {ToneNotes} | {AddNotes}";

        public override bool Equals(object? obj) => obj is Chord Other &&
                Other.BassNote == BassNote &&
                Other.ToneNotes.Root == ToneNotes.Root &&
                Other.ToneNotes.A == ToneNotes.A &&
                Other.ToneNotes.B == ToneNotes.B &&
                Other.ToneNotes.C == ToneNotes.C &&
                Other.AddNotes.A == AddNotes.A &&
                Other.AddNotes.B == AddNotes.B &&
                Other.AddNotes.C == AddNotes.C;

        public override int GetHashCode() => HashCode.Combine(BassNote, ToneNotes, AddNotes);
    }

    /// <summary>
    /// A musical scale used by CIT Files
    /// </summary>
    /// <remarks>
    /// Create a new Scale
    /// </remarks>
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
    public struct Scale(Note note1, Note note2, Note note3, Note note4, Note note5, Note note6, Note note7, Note note8, Note note9, Note note10, Note note11, Note note12)
    {
        /// <summary>
        /// Component Note 1
        /// </summary>
        public Note Note1 = note1;
        /// <summary>
        /// Component Note 2
        /// </summary>
        public Note Note2 = note2;
        /// <summary>
        /// Component Note 3
        /// </summary>
        public Note Note3 = note3;
        /// <summary>
        /// Component Note 4
        /// </summary>
        public Note Note4 = note4;
        /// <summary>
        /// Component Note 5
        /// </summary>
        public Note Note5 = note5;
        /// <summary>
        /// Component Note 6
        /// </summary>
        public Note Note6 = note6;
        /// <summary>
        /// Component Note 7
        /// </summary>
        public Note Note7 = note7;
        /// <summary>
        /// Component Note 8
        /// </summary>
        public Note Note8 = note8;
        /// <summary>
        /// Component Note 9
        /// </summary>
        public Note Note9 = note9;
        /// <summary>
        /// Component Note 10
        /// </summary>
        public Note Note10 = note10;
        /// <summary>
        /// Component Note 11
        /// </summary>
        public Note Note11 = note11;
        /// <summary>
        /// Component Note 12
        /// </summary>
        public Note Note12 = note12;

        /// <summary>
        /// Creates a copy of this Scale
        /// </summary>
        /// <returns></returns>
        public readonly Scale Copy() => new(Note1, Note2, Note3, Note4, Note5, Note6, Note7, Note8, Note9, Note10, Note11, Note12);
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString() => $"Components: {Note1} {Note2} {Note3} {Note4} {Note5} {Note6} {Note7} {Note8} {Note9} {Note10} {Note11} {Note12}";
        /// <summary>
        /// Compares this Scale to another object (Preferably another Scale object)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override readonly bool Equals(object? obj) => obj is Scale scale &&
                   Note1 == scale.Note1 &&
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
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override readonly int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Note1);
            hash.Add(Note2);
            hash.Add(Note3);
            hash.Add(Note4);
            hash.Add(Note5);
            hash.Add(Note6);
            hash.Add(Note7);
            hash.Add(Note8);
            hash.Add(Note9);
            hash.Add(Note10);
            hash.Add(Note11);
            hash.Add(Note12);
            return hash.ToHashCode();
        }
    }
    /// <summary>
    /// All possible notes that can be used in CIT Files
    /// </summary>
    public enum Note : byte
    {
        /// <summary>
        /// C, B#
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
        /// E, Fb
        /// </summary>
        E = 0x04,
        /// <summary>
        /// F, E#
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

    public static Note NoteFromString(string? value)
    {
        return value switch
        {
            "B#" or "C" => Note.C,
            "C#" or "Db" => Note.Db,
            "D" => Note.D,
            "D#" or "Eb" => Note.Eb,
            "E" or "Fb" => Note.E,
            "E#" or "F" => Note.F,
            "F#" or "Gb" => Note.Gb,
            "G" => Note.G,
            "G#" or "Ab" => Note.Ab,
            "A" => Note.A,
            "A#" or "Bb" => Note.Bb,
            "B" or "Cb" => Note.B,
            _ => Note.NONE,
        };
    }
}
