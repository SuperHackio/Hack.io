using System.Text;
using Hack.io.Interface;
using Hack.io.Utility;
using Hack.io.J3D;
using static Hack.io.BCK.BCK;

namespace Hack.io.BCK;

public class BCK : J3DAnimationBase<Animation>, ILoadSaveFile
{
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const string MAGIC = "J3D1bck1";
    /// <inheritdoc cref="J3D.DocGen.COMMON_CHUNKMAGIC"/>
    public const string CHUNKMAGIC = "ANK1";

    /// <summary>
    /// Rotational Multiplier.<para/>
    /// An angle scale of 1 means you can have rotations between -180 and 180. An angle scale of 2 allows for -360 to 360.
    /// </summary>
    public sbyte RotationMultiplier { get; set; }

    /// <summary>
    /// Represents sounds that are baked into the BCK.<para/>
    /// Set to NULL to exclude it from the saved file completely
    /// </summary>
    public BAS.BAS? EmbeddedSounds;

    public void Load(Stream Strm)
    {
        uint StartPosition = (uint)Strm.Position;
        FileUtil.ExceptionOnBadMagic(Strm, MAGIC);
        uint FileSize = Strm.ReadUInt32(),
            ChunkCount = Strm.ReadUInt32();
        Strm.Position += 0x0C; //Strm.ReadJ3DSubVersion(); //This is not used the same way the other formats are
        uint SoundOffset = Strm.ReadUInt32() + StartPosition;

        //Only 1 chunk is supported
        uint ChunkStart = (uint)Strm.Position;
        FileUtil.ExceptionOnBadMagic(Strm, CHUNKMAGIC);
        uint ChunkSize = Strm.ReadUInt32();
        Loop = Strm.ReadEnum<LoopMode, byte>(StreamUtil.ReadUInt8);
        RotationMultiplier = (sbyte)Strm.ReadByte();
        double POW = Math.Pow(2, RotationMultiplier);
        float rotationScale = (float)(POW) * (180.0f / 32768.0f);
        Duration = Strm.ReadUInt16();

        ushort BoneCount = Strm.ReadUInt16(),
               ScaleCount = Strm.ReadUInt16(),
               RotationCount = Strm.ReadUInt16(),
               TranslationCount = Strm.ReadUInt16();

        uint AnimationTableOffset = Strm.ReadUInt32() + ChunkStart,
             ScaleTableOffset = Strm.ReadUInt32() + ChunkStart,
             RotationTableOffset = Strm.ReadUInt32() + ChunkStart,
             TranslationTableOffset = Strm.ReadUInt32() + ChunkStart;

        //Pad to the nearest 32

        float[] ScaleTable       = Strm.ReadMultiAtOffset(ScaleTableOffset,       StreamUtil.ReadMultiSingle, ScaleCount);
        short[] RotationTable    = Strm.ReadMultiAtOffset(RotationTableOffset,    StreamUtil.ReadMultiInt16,  RotationCount);
        float[] TranslationTable = Strm.ReadMultiAtOffset(TranslationTableOffset, StreamUtil.ReadMultiSingle, TranslationCount);

        for (int i = 0; i < BoneCount; i++)
        {
            Animation anim = new();

            Strm.Position = AnimationTableOffset + (i * 0x36);

            anim.ScaleX = J3D.Utility.ReadAnimationTrackFloat(Strm, ScaleTable, 1);
            anim.RotationX = J3D.Utility.ReadAnimationTrackInt16(Strm, RotationTable, rotationScale);
            anim.TranslationX = J3D.Utility.ReadAnimationTrackFloat(Strm, TranslationTable, 1);

            anim.ScaleY = J3D.Utility.ReadAnimationTrackFloat(Strm, ScaleTable, 1);
            anim.RotationY = J3D.Utility.ReadAnimationTrackInt16(Strm, RotationTable, rotationScale);
            anim.TranslationY = J3D.Utility.ReadAnimationTrackFloat(Strm, TranslationTable, 1);

            anim.ScaleZ = J3D.Utility.ReadAnimationTrackFloat(Strm, ScaleTable, 1);
            anim.RotationZ = J3D.Utility.ReadAnimationTrackInt16(Strm, RotationTable, rotationScale);
            anim.TranslationZ = J3D.Utility.ReadAnimationTrackFloat(Strm, TranslationTable, 1);

            Add(anim);
        }

        Strm.Position = ChunkStart + ChunkSize;

        EmbeddedSounds = null;

        //Bonus!
        if (SoundOffset == 0xFFFFFFFF)
            return;

        Strm.Position = SoundOffset;
        EmbeddedSounds = new();
        EmbeddedSounds.Load(Strm);

        Strm.Position = ChunkStart + ChunkSize;
    }

    public void Save(Stream Strm)
    {
        double POW = Math.Pow(2, RotationMultiplier);
        float rotationScale = (float)(POW) * (180.0f / 32768.0f);

        long Start = Strm.Position;
        Strm.WriteString(MAGIC, Encoding.ASCII, null);
        Strm.WritePlaceholder(4); //FileSize
        Strm.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4); //Chunk Count
        Strm.Write(CollectionUtil.InitilizeArray((byte)0xFF, 0x10));

        long ChunkStart = Strm.Position;
        Strm.WriteString(CHUNKMAGIC, Encoding.ASCII, null);
        Strm.WritePlaceholder(4); //ChunkSize
        Strm.WriteByte((byte)Loop);
        Strm.WriteByte((byte)RotationMultiplier);
        Strm.WriteUInt16(Duration);
        Strm.WriteUInt16((ushort)Count);
        Strm.WritePlaceholderMulti(2, 3); //Count Placeholders
        Strm.WritePlaceholderMulti(4, 4); //Offset Placeholders
        Strm.PadTo(32, J3D.Utility.PADSTRING);

        List<float> ScaleTable = new();
        List<short> RotationTable = new();
        List<float> TranslationTable = new();

        long AnimationTableOffset = Strm.Position;
        for (int i = 0; i < Count; i++)
        {
            J3D.Utility.WriteAnimationTrackFloat(Strm, this[i].ScaleX, 1, ref ScaleTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, this[i].RotationX, rotationScale, ref RotationTable);
            J3D.Utility.WriteAnimationTrackFloat(Strm, this[i].TranslationX, 1, ref TranslationTable);
                                                            
            J3D.Utility.WriteAnimationTrackFloat(Strm, this[i].ScaleY, 1, ref ScaleTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, this[i].RotationY, rotationScale, ref RotationTable);
            J3D.Utility.WriteAnimationTrackFloat(Strm, this[i].TranslationY, 1, ref TranslationTable);
                                                            
            J3D.Utility.WriteAnimationTrackFloat(Strm, this[i].ScaleZ, 1, ref ScaleTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, this[i].RotationZ, rotationScale, ref RotationTable);
            J3D.Utility.WriteAnimationTrackFloat(Strm, this[i].TranslationZ, 1, ref TranslationTable);
        }

        Strm.PadTo(4, J3D.Utility.PADSTRING); //Assumption

        long ScaleTableOffset = Strm.Position;
        Strm.WriteMultiSingle(ScaleTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long RotationTableOffset = Strm.Position;
        Strm.WriteMultiInt16(RotationTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long TranslationTableOffset = Strm.Position;
        Strm.WriteMultiSingle(TranslationTable);
        Strm.PadTo(32, J3D.Utility.PADSTRING);

        long FileLength = Strm.Position;

        Strm.Position = Start + 0x08;
        Strm.WriteUInt32((uint)(FileLength - Start));

        Strm.Position = ChunkStart + 0x04;
        Strm.WriteUInt32((uint)(FileLength - (ChunkStart - Start)));

        Strm.Position = ChunkStart + 0x0E;
        Strm.WriteUInt16((ushort)ScaleTable.Count);
        Strm.WriteUInt16((ushort)RotationTable.Count);
        Strm.WriteUInt16((ushort)TranslationTable.Count);
        Strm.WriteUInt32((uint)(AnimationTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(ScaleTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(RotationTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(TranslationTableOffset - ChunkStart));

        Strm.Position = FileLength;

        if (EmbeddedSounds is null)
            return;

        long SoundOffset = FileLength;
        EmbeddedSounds.Save(Strm);
        Strm.Write(CollectionUtil.InitilizeArray((byte)0x00, 0x18)); //Is this needed?

        FileLength = Strm.Position;


        Strm.Position = Start + 0x08;
        Strm.WriteUInt32((uint)(FileLength - Start));

        Strm.Position = Start + 0x1C;
        Strm.WriteUInt32((uint)(SoundOffset - Start));

        Strm.Position = FileLength;
    }


    /// <inheritdoc cref="J3D.DocGen.COMMON_ANIMATIONCLASS"/>
    public class Animation : IJ3DAnimationContainer
    {
        public J3DAnimationTrack ScaleX { get; set; } = new();
        public J3DAnimationTrack ScaleY { get; set; } = new();
        public J3DAnimationTrack ScaleZ { get; set; } = new();
        public J3DAnimationTrack RotationX { get; set; } = new();
        public J3DAnimationTrack RotationY { get; set; } = new();
        public J3DAnimationTrack RotationZ { get; set; } = new();
        public J3DAnimationTrack TranslationX { get; set; } = new();
        public J3DAnimationTrack TranslationY { get; set; } = new();
        public J3DAnimationTrack TranslationZ { get; set; } = new();

        public override string ToString() => $"Joint";

        public override bool Equals(object? obj) => obj is Animation animation &&
                   ScaleX.Equals(animation.ScaleX) &&
                   RotationX.Equals(animation.RotationX) &&
                   TranslationX.Equals(animation.TranslationX) &&
                   ScaleY.Equals(animation.ScaleY) &&
                   RotationY.Equals(animation.RotationY) &&
                   TranslationY.Equals(animation.TranslationY) &&
                   ScaleZ.Equals(animation.ScaleZ) &&
                   RotationZ.Equals(animation.RotationZ) &&
                   TranslationZ.Equals(animation.TranslationZ);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(ScaleX);
            hash.Add(RotationX);
            hash.Add(TranslationX);
            hash.Add(ScaleY);
            hash.Add(RotationY);
            hash.Add(TranslationY);
            hash.Add(ScaleZ);
            hash.Add(RotationZ);
            hash.Add(TranslationZ);
            return hash.ToHashCode();
        }
    }
}