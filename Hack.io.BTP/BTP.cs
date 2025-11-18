using Hack.io.Interface;
using Hack.io.J3D;
using Hack.io.Utility;
using System.Text;
using static Hack.io.BTP.BTP;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Hack.io.BTP;

public class BTP : J3DAnimationBase<Animation>, ILoadSaveFile
{
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const uint MAGIC = 0x62747031;
    /// <inheritdoc cref="J3D.DocGen.COMMON_CHUNKMAGIC"/>
    public const uint CHUNKMAGIC = 0x54505431;

    public void Load(Stream Strm)
    {
        FileUtil.ExceptionOnBadJ3DMagic(Strm, MAGIC);
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
            TextureIndexCount = Strm.ReadUInt16();

        uint AnimationTableOffset = Strm.ReadUInt32() + ChunkStart,
             TextureIndexTableOffset = Strm.ReadUInt32() + ChunkStart,
             MaterialIndexTableOffset = Strm.ReadUInt32() + ChunkStart,
             MaterialSTOffset = Strm.ReadUInt32() + ChunkStart;

        ushort[] TextureIndexTable = Strm.ReadMultiAtOffset(TextureIndexTableOffset, StreamUtil.ReadMultiUInt16, TextureIndexCount);

        ushort[] MaterialIndicies = Strm.ReadMultiAtOffset(MaterialIndexTableOffset, StreamUtil.ReadMultiUInt16, AnimationCount);

        string[] MaterialNames = Strm.ReadJ3DStringTable((int)MaterialSTOffset);

        for (int i = 0; i < AnimationCount; i++)
        {
            Animation anim = new() { MaterialName = MaterialNames[i], MaterialId = MaterialIndicies[i] }; //This is the only thing that doesn't get remapped
            Strm.Position = AnimationTableOffset + (i * 0x08);

            ushort Count = Strm.ReadUInt16(),
                First = Strm.ReadUInt16();
            anim.TextureId = Strm.ReadUInt8();

            anim.AddRange(TextureIndexTable[First..(First+Count)]);
            Add(anim);
        }

        Strm.Position = ChunkStart + ChunkSize;
    }

    public void Save(Stream Strm)
    {
        long Start = Strm.Position;
        Strm.WriteUInt32(0x4A334431); // J3D1
        Strm.WriteUInt32(MAGIC);
        Strm.WritePlaceholder(4); //FileSize
        Strm.WriteUInt32(1); // ChunkCount
        Strm.Write(CollectionUtil.InitilizeArray((byte)0xFF, 0x10));

        long ChunkStart = Strm.Position;
        Strm.WriteUInt32(CHUNKMAGIC);
        Strm.WritePlaceholder(4); //ChunkSize
        Strm.WriteByte((byte)Loop);
        Strm.WriteByte(0xFF);
        Strm.WriteUInt16(Duration);

        Strm.WriteUInt16((ushort)Count);
        Strm.WritePlaceholderMulti(2, 1); //Count Placeholders
        Strm.WritePlaceholderMulti(4, 4); //Offset Placeholders

        List<ushort> TextureIndexTable = new();
        List<string> Names = new();
        List<ushort> MaterialIdTable = new();

        long AnimationTableOffset = Strm.Position;
        for (int i = 0; i < Count; i++)
        {
            Animation current = this[i];
            Names.Add(current.MaterialName);
            MaterialIdTable.Add(current.MaterialId);
            int Index = TextureIndexTable.SubListIndex(0, current);
            if (Index == -1)
            {
                Index = TextureIndexTable.Count;
                TextureIndexTable.AddRange(current);
            }
            Strm.WriteUInt16((ushort)current.Count);
            Strm.WriteUInt16((ushort)Index);
            Strm.WriteByte(current.TextureId);
            Strm.Write(CollectionUtil.InitilizeArray((byte)0xFF, 0x03));
        }

        long TextureIndexTableOffset = Strm.Position;
        Strm.WriteMultiUInt16(TextureIndexTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long MaterialIndexTableOffset = Strm.Position;
        Strm.WriteMultiUInt16(MaterialIdTable);
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

        Strm.Position = ChunkStart + 0x0E;
        Strm.WriteUInt16((ushort)TextureIndexTable.Count);
        Strm.WriteUInt32((uint)(AnimationTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(TextureIndexTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(MaterialIndexTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(StringTableOffset - ChunkStart));

        Strm.Position = FileLength;
    }

    /// <inheritdoc cref="J3D.DocGen.COMMON_ANIMATIONCLASS"/>
    public class Animation : List<ushort>, IJ3DAnimationContainer
    {
        /// <inheritdoc cref="J3D.DocGen.COMMON_MATERIALNAME"/>
        public string MaterialName { get; set; } = "";
        /// <summary>
        /// index to the Texture inside the material to target
        /// </summary>
        public byte TextureId { get; set; }
        /// <summary>
        /// Index of the material this animation targets
        /// </summary>
        public ushort MaterialId { get; set; }

        public override string ToString() => $"{MaterialName} - Texture {TextureId}";

        public override bool Equals(object? obj) => obj is Animation animation &&
                   MaterialName == animation.MaterialName &&
                   TextureId == animation.TextureId &&
                   this.SequenceEqual(animation);

        public override int GetHashCode() => HashCode.Combine(MaterialName, TextureId, this as List<ushort>);
    }
}