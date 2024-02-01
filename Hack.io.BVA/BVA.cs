using Hack.io.Interface;
using Hack.io.J3D;
using Hack.io.Utility;
using System.Text;
using static Hack.io.BVA.BVA;

namespace Hack.io.BVA;

public class BVA : J3DAnimationBase<Animation>, ILoadSaveFile
{
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const string MAGIC = "J3D1bva1";
    /// <inheritdoc cref="J3D.DocGen.COMMON_CHUNKMAGIC"/>
    public const string CHUNKMAGIC = "VAF1";

    public void Load(Stream Strm)
    {
        FileUtil.ExceptionOnBadMagic(Strm, MAGIC);
        uint FileSize = Strm.ReadUInt32(),
            ChunkCount = Strm.ReadUInt32();
        Strm.Position += 0x10; //Strm.ReadJ3DSubVersion();

        //Only 1 chunk is supported
        uint ChunkStart = (uint)Strm.Position;
        FileUtil.ExceptionOnBadMagic(Strm, CHUNKMAGIC);
        uint ChunkSize = Strm.ReadUInt32();
        Loop = Strm.ReadEnum<LoopMode, byte>(StreamUtil.ReadUInt8);
        Strm.Position++; //Padding 0xFF
        Duration = Strm.ReadUInt16();

        ushort AnimationCount = Strm.ReadUInt16(),
            VisibleCount = Strm.ReadUInt16();

        uint AnimationTableOffset = Strm.ReadUInt32() + ChunkStart,
             VisibleTableOffset = Strm.ReadUInt32() + ChunkStart;

        //Padding

        Strm.Position = VisibleTableOffset;
        byte[] Temp = new byte[VisibleCount];
        Strm.Read(Temp);

        bool[] VisibilityTable = new bool[VisibleCount];
        for (int i = 0; i < VisibleCount; i++)
            VisibilityTable[i] = Temp[i] != 0;

        for (int i = 0; i < AnimationCount; i++)
        {
            Animation anim = new();
            Strm.Position = AnimationTableOffset + (i * 0x04);

            ushort Count = Strm.ReadUInt16(),
                First = Strm.ReadUInt16();

            anim.AddRange(VisibilityTable[First..(First + Count)]);
            Add(anim);
        }

        Strm.Position = ChunkStart + ChunkSize;
    }

    public void Save(Stream Strm)
    {
        long Start = Strm.Position;
        Strm.WriteString(MAGIC, Encoding.ASCII, null);
        Strm.WritePlaceholder(4); //FileSize
        Strm.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4); //Chunk Count
        Strm.Write(CollectionUtil.InitilizeArray((byte)0xFF, 0x10));

        long ChunkStart = Strm.Position;
        Strm.WriteString(CHUNKMAGIC, Encoding.ASCII, null);
        Strm.WritePlaceholder(4); //ChunkSize
        Strm.WriteByte((byte)Loop);
        Strm.WriteByte(0xFF);
        Strm.WriteUInt16(Duration);

        Strm.WriteUInt16((ushort)Count);
        Strm.WritePlaceholderMulti(2, 1); //Count Placeholders
        Strm.WritePlaceholderMulti(4, 2); //Offset Placeholders
        Strm.PadTo(0x10, J3D.Utility.PADSTRING);

        List<bool> VisibilityTable = new();
        long AnimationTableOffset = Strm.Position;
        for (int i = 0; i < Count; i++)
        {
            Animation current = this[i];
            int Index = VisibilityTable.SubListIndex(0, current);
            if (Index == -1)
            {
                Index = VisibilityTable.Count;
                VisibilityTable.AddRange(current);
            }
            Strm.WriteUInt16((ushort)current.Count);
            Strm.WriteUInt16((ushort)Index);
        }

        long VisibilityTableOffset = Strm.Position;
        for (int i = 0; i < VisibilityTable.Count; i++)
            Strm.WriteByte((byte)(VisibilityTable[i] ? 0x01 : 0x00));
        Strm.PadTo(4, J3D.Utility.PADSTRING);
        //Yes these are 2 different padding cases
        Strm.PadTo(32, J3D.Utility.PADSTRING);

        long FileLength = Strm.Position;

        Strm.Position = Start + 0x08;
        Strm.WriteUInt32((uint)(FileLength - Start));

        Strm.Position = ChunkStart + 0x04;
        Strm.WriteUInt32((uint)(FileLength - (ChunkStart - Start)));

        Strm.Position = ChunkStart + 0x0E;
        Strm.WriteUInt16((ushort)VisibilityTable.Count);
        Strm.WriteUInt32((uint)(AnimationTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(VisibilityTableOffset - ChunkStart));

        Strm.Position = FileLength;
    }

    /// <inheritdoc cref="J3D.DocGen.COMMON_ANIMATIONCLASS"/>
    public class Animation : List<bool>, IJ3DAnimationContainer
    {
        public override string ToString() => $"Count: {Count}";

        public override bool Equals(object? obj) => obj is Animation animation &&
                   this.SequenceEqual(animation);

        public override int GetHashCode() => HashCode.Combine(this as List<bool>);
    }
}