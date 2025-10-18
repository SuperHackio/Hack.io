using System.Text;
using Hack.io.Interface;
using Hack.io.Utility;
using Hack.io.J3D;
using static Hack.io.BTK.BTK;

namespace Hack.io.BTK;

/// <summary>
/// Binary Texture Keyframes<para/>
/// J3D file format for controlling the texture coordinate settings inside a 3D model's texture generators
/// </summary>
public class BTK : J3DAnimationBase<Animation>, ILoadSaveFile
{
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const uint MAGIC = 0x62746B31;
    /// <inheritdoc cref="J3D.DocGen.COMMON_CHUNKMAGIC"/>
    public const uint CHUNKMAGIC = 0x54544B31;

    /// <summary>
    /// If true, uses Maya math instead of normal J3D Math
    /// </summary>
    public bool UseMaya { get; set; }
    /// <summary>
    /// Rotational Multiplier.<para/>
    /// An angle scale of 1 means you can have rotations between -180 and 180. An angle scale of 2 allows for -360 to 360.
    /// </summary>
    public sbyte RotationMultiplier { get; set; }

    /// <inheritdoc/>
    public void Load(Stream Strm)
    {
        FileUtil.ExceptionOnBadJ3DMagic(Strm, MAGIC);
        uint FileSize = Strm.ReadUInt32(),
            ChunkCount = Strm.ReadUInt32();
        Strm.ReadJ3DSubVersion();

        //Only 1 chunk is supported
        uint ChunkStart = (uint)Strm.Position;
        FileUtil.ExceptionOnBadMagic(Strm, CHUNKMAGIC);
        uint ChunkSize = Strm.ReadUInt32();
        Loop = Strm.ReadEnum<LoopMode, byte>(StreamUtil.ReadUInt8);
        RotationMultiplier = (sbyte)Strm.ReadByte();
        float rotationScale = (float)(Math.Pow(2, RotationMultiplier) / 0x7FFF);
        Duration = Strm.ReadUInt16();

        ushort AnimationCount = (ushort)(Strm.ReadUInt16() / 3),
               ScaleCount = Strm.ReadUInt16(),
               RotationCount = Strm.ReadUInt16(),
               TranslationCount = Strm.ReadUInt16();

        uint AnimationTableOffset = Strm.ReadUInt32() + ChunkStart,
             RemapTableOffset = Strm.ReadUInt32() + ChunkStart,
             MaterialSTOffset = Strm.ReadUInt32() + ChunkStart,
             TextureMapIDTableOffset = Strm.ReadUInt32() + ChunkStart,
             TextureCenterTableOffset = Strm.ReadUInt32() + ChunkStart,
             ScaleTableOffset = Strm.ReadUInt32() + ChunkStart,
             RotationTableOffset = Strm.ReadUInt32() + ChunkStart,
             TranslationTableOffset = Strm.ReadUInt32() + ChunkStart;

        Strm.Position = ChunkStart + 0x5C;
        UseMaya = Strm.ReadUInt32() == 1;

        float[] ScaleTable       = Strm.ReadMultiAtOffset(ScaleTableOffset,       StreamUtil.ReadMultiSingle, ScaleCount);
        short[] RotationTable    = Strm.ReadMultiAtOffset(RotationTableOffset,    StreamUtil.ReadMultiInt16,  RotationCount);
        float[] TranslationTable = Strm.ReadMultiAtOffset(TranslationTableOffset, StreamUtil.ReadMultiSingle, TranslationCount);

        string[] MaterialNames = Strm.ReadJ3DStringTable((int)MaterialSTOffset);

        Strm.Position = RemapTableOffset;
        ushort[] RemapIndicies = Strm.ReadMultiUInt16(AnimationCount);

        for (int i = 0; i < AnimationCount; i++)
        {
            Animation anim = new() { MaterialName = MaterialNames[i] }; //This is the only thing that doesn't get remapped
            int Index = RemapIndicies[i]; //Assuming this is how it works...

            Strm.Position = TextureMapIDTableOffset + Index;
            anim.TextureGeneratorId = Strm.ReadUInt8(); //Potential issue? Does the game map these by identity always? Or is this included in the Remap? (It's not so obvious unlike the names)

            Strm.Position = TextureCenterTableOffset + (Index * 0x0C);
            anim.Center = Strm.ReadMultiSingle(anim.Center.Length);

            Strm.Position = AnimationTableOffset + (Index * 0x36);

            anim.ScaleU = J3D.Utility.ReadAnimationTrackFloat(Strm, ScaleTable, 1);
            anim.RotationU = J3D.Utility.ReadAnimationTrackInt16(Strm, RotationTable, rotationScale);
            anim.TranslationU = J3D.Utility.ReadAnimationTrackFloat(Strm, TranslationTable, 1);

            anim.ScaleV = J3D.Utility.ReadAnimationTrackFloat(Strm, ScaleTable, 1);
            anim.RotationV = J3D.Utility.ReadAnimationTrackInt16(Strm, RotationTable, rotationScale);
            anim.TranslationV = J3D.Utility.ReadAnimationTrackFloat(Strm, TranslationTable, 1);

            anim.ScaleW = J3D.Utility.ReadAnimationTrackFloat(Strm, ScaleTable, 1);
            anim.RotationW = J3D.Utility.ReadAnimationTrackInt16(Strm, RotationTable, rotationScale);
            anim.TranslationW = J3D.Utility.ReadAnimationTrackFloat(Strm, TranslationTable, 1);

            Add(anim);
        }

        Strm.Position = ChunkStart + ChunkSize;
    }

    /// <inheritdoc/>
    public void Save(Stream Strm)
    {
        float rotationScale = (float)(Math.Pow(2, RotationMultiplier) / 0x7FFF);

        long Start = Strm.Position;
        Strm.WriteUInt32(0x4A334431); // J3D1
        Strm.WriteUInt32(MAGIC);
        Strm.WritePlaceholder(4); //FileSize
        Strm.WriteUInt32(1); // ChunkCount
        Strm.WriteJ3DSubVersion();

        long ChunkStart = Strm.Position;
        Strm.WriteUInt32(CHUNKMAGIC);
        Strm.WritePlaceholder(4); //ChunkSize
        Strm.WriteByte((byte)Loop);
        Strm.WriteByte((byte)RotationMultiplier);
        Strm.WriteUInt16(Duration);
        Strm.WriteUInt16((ushort)(Count * 3));
        Strm.WritePlaceholderMulti(2, 3); //Count Placeholders
        Strm.WritePlaceholderMulti(4, 8); //Offset Placeholders
        //Here's normally the post tex data but...there's no support for something that's never used (to anyones knowledge)
        Strm.Position = ChunkStart + 0x5C;
        Strm.WriteInt32(UseMaya ? 1 : 0);

        List<string> Names = new();
        List<ushort> RemapIndexTable = new();
        List<byte> GeneratorTable = new();
        List<float[]> CenterTable = new();
        List<float> ScaleTable = new();
        List<short> RotationTable = new();
        List<float> TranslationTable = new();

        long AnimationTableOffset = Strm.Position;
        for (int i = 0; i < Count; i++)
        {
            Names.Add(this[i].MaterialName);
            int RemapIndex = i; //Here would be a good idea to search and see if there's any other identical tracks that have already been written
            RemapIndexTable.Add((ushort)RemapIndex);
            GeneratorTable.Add(this[RemapIndex].TextureGeneratorId);
            CenterTable.Add(this[RemapIndex].Center);

            J3D.Utility.WriteAnimationTrackFloat(Strm, this[RemapIndex].ScaleU, 1, ref ScaleTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, this[RemapIndex].RotationU, rotationScale, ref RotationTable);
            J3D.Utility.WriteAnimationTrackFloat(Strm, this[RemapIndex].TranslationU, 1, ref TranslationTable);

            J3D.Utility.WriteAnimationTrackFloat(Strm, this[RemapIndex].ScaleV, 1, ref ScaleTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, this[RemapIndex].RotationV, rotationScale, ref RotationTable);
            J3D.Utility.WriteAnimationTrackFloat(Strm, this[RemapIndex].TranslationV, 1, ref TranslationTable);

            J3D.Utility.WriteAnimationTrackFloat(Strm, this[RemapIndex].ScaleW, 1, ref ScaleTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, this[RemapIndex].RotationW, rotationScale, ref RotationTable);
            J3D.Utility.WriteAnimationTrackFloat(Strm, this[RemapIndex].TranslationW, 1, ref TranslationTable);
        }

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
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        //Generator Table
        long TexMapIDTableOffset = Strm.Position;
        Strm.WriteMulti(GeneratorTable, StreamUtil.WriteUInt8);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long CenterTableOffset = Strm.Position;
        Strm.WriteMulti(CenterTable, StreamUtil.WriteMultiSingle); //I can't believe this works lol
        Strm.PadTo(4, J3D.Utility.PADSTRING);

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
        Strm.WriteUInt32((uint)((FileLength - (ChunkStart - Start)) - 0x04));

        Strm.Position = ChunkStart + 0x0E;
        Strm.WriteUInt16((ushort)ScaleTable.Count);
        Strm.WriteUInt16((ushort)RotationTable.Count);
        Strm.WriteUInt16((ushort)TranslationTable.Count);
        Strm.WriteUInt32((uint)(AnimationTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(RemapTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(StringTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(TexMapIDTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(CenterTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(ScaleTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(RotationTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(TranslationTableOffset - ChunkStart));

        Strm.Position = FileLength;
    }

    /// <inheritdoc cref="J3D.DocGen.COMMON_ANIMATIONCLASS"/>
    public class Animation : IJ3DAnimationContainer
    {
        /// <inheritdoc cref="J3D.DocGen.COMMON_MATERIALNAME"/>
        public string MaterialName { get; set; } = "";
        /// <summary>
        /// index to the Texture Generator inside the model to target
        /// </summary>
        public byte TextureGeneratorId { get; set; }
        /// <summary>
        /// The Origin of Rotation
        /// </summary>
        public float[] Center
        {
            get => mCenter;
            set
            {
                if (value is null)
                    throw new NullReferenceException("Cannot set Center to Null");
                if (value.Length != 3)
                    throw new IndexOutOfRangeException("The provided array length does not match 3");
                mCenter = value;
            }
        }
        /// <summary>
        /// The X Origin of Rotation
        /// </summary>
        public float CenterU { get => mCenter[0]; set => mCenter[0] = value; }
        /// <summary>
        /// The Y Origin of Rotation
        /// </summary>
        public float CenterV { get => mCenter[1]; set => mCenter[1] = value; }
        /// <summary>
        /// Not very useful
        /// </summary>
        public float CenterW { get => mCenter[2]; set => mCenter[2] = value; }

        public J3DAnimationTrack ScaleU { get; set; } = new();
        public J3DAnimationTrack ScaleV { get; set; } = new();
        public J3DAnimationTrack ScaleW { get; set; } = new();
        public J3DAnimationTrack RotationU { get; set; } = new();
        public J3DAnimationTrack RotationV { get; set; } = new();
        public J3DAnimationTrack RotationW { get; set; } = new();
        public J3DAnimationTrack TranslationU { get; set; } = new();
        public J3DAnimationTrack TranslationV { get; set; } = new();
        public J3DAnimationTrack TranslationW { get; set; } = new();

        private float[] mCenter = new float[3];

        public override string ToString() => $"{MaterialName} - Generator {TextureGeneratorId}";

        public override bool Equals(object? obj) => obj is Animation animation &&
                   MaterialName == animation.MaterialName &&
                   TextureGeneratorId == animation.TextureGeneratorId &&
                   mCenter.SequenceEqual(animation.mCenter) &&
                   ScaleU.Equals(animation.ScaleU) &&
                   RotationU.Equals(animation.RotationU) &&
                   TranslationU.Equals(animation.TranslationU) &&
                   ScaleV.Equals(animation.ScaleV) &&
                   RotationV.Equals(animation.RotationV) &&
                   TranslationV.Equals(animation.TranslationV) &&
                   ScaleW.Equals(animation.ScaleW) &&
                   RotationW.Equals(animation.RotationW) &&
                   TranslationW.Equals(animation.TranslationW);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(MaterialName);
            hash.Add(TextureGeneratorId);
            hash.Add(CenterU);
            hash.Add(CenterV);
            hash.Add(CenterW);
            hash.Add(ScaleU);
            hash.Add(RotationU);
            hash.Add(TranslationU);
            hash.Add(ScaleV);
            hash.Add(RotationV);
            hash.Add(TranslationV);
            hash.Add(ScaleW);
            hash.Add(RotationW);
            hash.Add(TranslationW);
            return hash.ToHashCode();
        }
    }
}