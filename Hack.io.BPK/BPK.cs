using System.Text;
using Hack.io.Interface;
using Hack.io.Utility;
using Hack.io.J3D;
using static Hack.io.BPK.BPK;

namespace Hack.io.BPK;

/// <summary>
/// Binary Palette Keyframes<para/>
/// J3D file format for controlling the Material Color inside a 3D model
/// </summary>
public class BPK : J3DAnimationBase<Animation>, ILoadSaveFile
{
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const string MAGIC = "J3D1bpk1";
    /// <inheritdoc cref="J3D.DocGen.COMMON_CHUNKMAGIC"/>
    public const string CHUNKMAGIC = "PAK1";

    /// <inheritdoc/>
    public void Load(Stream Strm)
    {
        FileUtil.ExceptionOnBadMagic(Strm, MAGIC);
        uint FileSize = Strm.ReadUInt32(),
            ChunkCount = Strm.ReadUInt32();
        Strm.ReadJ3DSubVersion();

        //Only 1 chunk is supported
        uint ChunkStart = (uint)Strm.Position;
        FileUtil.ExceptionOnBadMagic(Strm, CHUNKMAGIC);
        uint ChunkSize = Strm.ReadUInt32();
        Loop = Strm.ReadEnum<LoopMode, byte>(StreamUtil.ReadUInt8);
        Strm.Position+=0x03; //Padding 0xFF
        Duration = Strm.ReadUInt16();

        ushort AnimationCount = Strm.ReadUInt16();

        ushort RedCount = Strm.ReadUInt16(),
               GreenCount = Strm.ReadUInt16(),
               BlueCount = Strm.ReadUInt16(),
               AlphaCount = Strm.ReadUInt16();

        uint AnimationTableOffset = Strm.ReadUInt32() + ChunkStart,
            RemapTableOffset = Strm.ReadUInt32() + ChunkStart,
            MaterialSTOffset = Strm.ReadUInt32() + ChunkStart;

        uint RedTableOffset = Strm.ReadUInt32() + ChunkStart,
             GreenTableOffset = Strm.ReadUInt32() + ChunkStart,
             BlueTableOffset = Strm.ReadUInt32() + ChunkStart,
             AlphaTableOffset = Strm.ReadUInt32() + ChunkStart;

        short[] RedTable = Strm.ReadMultiAtOffset(RedTableOffset,   StreamUtil.ReadMultiInt16, RedCount),
            GreenTable   = Strm.ReadMultiAtOffset(GreenTableOffset, StreamUtil.ReadMultiInt16, GreenCount),
            BlueTable    = Strm.ReadMultiAtOffset(BlueTableOffset,  StreamUtil.ReadMultiInt16, BlueCount),
            AlphaTable   = Strm.ReadMultiAtOffset(AlphaTableOffset, StreamUtil.ReadMultiInt16, AlphaCount);

        ushort[] RemapIndicies = Strm.ReadMultiAtOffset(RemapTableOffset, StreamUtil.ReadMultiUInt16, AnimationCount);

        string[] MaterialNames = Strm.ReadJ3DStringTable((int)MaterialSTOffset);

        for (int i = 0; i < AnimationCount; i++)
        {
            Animation anim = new() { MaterialName = MaterialNames[i] }; //This is the only thing that doesn't get remapped
            int Index = RemapIndicies[i]; //Assuming this is how it works...

            Strm.Position = AnimationTableOffset + (Index * 0x18);
            anim.Red = J3D.Utility.ReadAnimationTrackInt16(Strm, RedTable, 1);
            anim.Green = J3D.Utility.ReadAnimationTrackInt16(Strm, GreenTable, 1);
            anim.Blue = J3D.Utility.ReadAnimationTrackInt16(Strm, BlueTable, 1);
            anim.Alpha = J3D.Utility.ReadAnimationTrackInt16(Strm, AlphaTable, 1);

            Add(anim);
        }

        Strm.Position = ChunkStart + ChunkSize;
    }

    /// <inheritdoc/>
    public void Save(Stream Strm)
    {
        long Start = Strm.Position;
        Strm.WriteString(MAGIC, Encoding.ASCII, null);
        Strm.WritePlaceholder(4); //FileSize
        Strm.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4); //Chunk Count
        Strm.WriteJ3DSubVersion();

        long ChunkStart = Strm.Position;
        Strm.WriteString(CHUNKMAGIC, Encoding.ASCII, null);
        Strm.WritePlaceholder(4); //ChunkSize
        Strm.WriteByte((byte)Loop);
        Strm.PadTo(0x04, 0xFF); //Padding
        Strm.WriteUInt16(Duration);
        Strm.WriteUInt16((ushort)Count);
        Strm.WritePlaceholderMulti(2, 4); //Count Placeholders
        Strm.WritePlaceholderMulti(4, 7); //Offset Placeholders
        Strm.PadTo(16, J3D.Utility.PADSTRING);

        List<string> Names = new();
        List<ushort> RemapIndexTable = new();
        List<short> RedTable = new();
        List<short> GreenTable = new();
        List<short> BlueTable = new();
        List<short> AlphaTable = new();

        long AnimationTableOffset = Strm.Position;
        for (int i = 0; i < Count; i++)
        {
            Names.Add(this[i].MaterialName);
            int RemapIndex = i; //Here would be a good idea to search and see if there's any other identical tracks that have already been written
            RemapIndexTable.Add((ushort)RemapIndex);

            J3D.Utility.WriteAnimationTrackInt16(Strm, this[RemapIndex].Red,   1, ref RedTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, this[RemapIndex].Green, 1, ref GreenTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, this[RemapIndex].Blue,  1, ref BlueTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, this[RemapIndex].Alpha, 1, ref AlphaTable);
        }

        long RedTableOffset = Strm.Position;
        Strm.WriteMultiInt16(RedTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long GreenTableOffset = Strm.Position;
        Strm.WriteMultiInt16(GreenTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long BlueTableOffset = Strm.Position;
        Strm.WriteMultiInt16(BlueTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long AlphaTableOffset = Strm.Position;
        Strm.WriteMultiInt16(AlphaTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        //Remap table!
        //TODO: Experiment with this. maybe it's useful for onboard file compression
        // for now though just use Identity.
        long RemapTableOffset = Strm.Position;
        Strm.WriteMultiUInt16(RemapIndexTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        //String Table
        long StringTableOffset = Strm.Position;
        Strm.WriteJ3DStringTable(Names);
        Strm.PadTo(32, J3D.Utility.PADSTRING);

        long FileLength = Strm.Position;

        Strm.Position = Start + 0x08;
        Strm.WriteUInt32((uint)(FileLength - Start));

        Strm.Position = ChunkStart + 0x04;
        Strm.WriteUInt32((uint)(FileLength - (ChunkStart - Start)));

        Strm.Position = ChunkStart + 0x10;
        Strm.WriteUInt16((ushort)RedTable.Count);
        Strm.WriteUInt16((ushort)GreenTable.Count);
        Strm.WriteUInt16((ushort)BlueTable.Count);
        Strm.WriteUInt16((ushort)AlphaTable.Count);
        Strm.WriteUInt32((uint)(AnimationTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(RemapTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(StringTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(RedTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(GreenTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(BlueTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(AlphaTableOffset - ChunkStart));

        Strm.Position = FileLength;
    }

    /// <inheritdoc cref="J3D.DocGen.COMMON_ANIMATIONCLASS"/>
    public class Animation : IJ3DAnimationContainer
    {
        /// <inheritdoc cref="J3D.DocGen.COMMON_MATERIALNAME"/>
        public string MaterialName { get; set; } = "";

        public J3DAnimationTrack Red { get; set; } = new();
        public J3DAnimationTrack Green { get; set; } = new();
        public J3DAnimationTrack Blue { get; set; } = new();
        public J3DAnimationTrack Alpha { get; set; } = new();

        public override string ToString() => $"{MaterialName} - Material Color 0";

        public override bool Equals(object? obj)
            => obj is Animation animation &&
            MaterialName == animation.MaterialName &&
            Red.Equals(animation.Red) &&
            Green.Equals(animation.Green) &&
            Blue.Equals(animation.Blue) &&
            Alpha.Equals(animation.Alpha);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(MaterialName);
            hash.Add(Red);
            hash.Add(Green);
            hash.Add(Blue);
            hash.Add(Alpha);
            return hash.ToHashCode();
        }
    }
}