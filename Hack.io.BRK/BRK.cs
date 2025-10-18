using System.Text;
using Hack.io.Interface;
using Hack.io.Utility;
using Hack.io.J3D;
using static Hack.io.BRK.BRK;

namespace Hack.io.BRK;

/// <summary>
/// Binary Register Keyframes<para/>
/// J3D file format for controlling the Color Registers inside a 3D model
/// </summary>
public class BRK : J3DAnimationBase<Animation>, ILoadSaveFile
{
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const uint MAGIC = 0x62726B31;
    /// <inheritdoc cref="J3D.DocGen.COMMON_CHUNKMAGIC"/>
    public const uint CHUNKMAGIC = 0x54524B31;

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
        Strm.Position++; //Padding 0xFF
        Duration = Strm.ReadUInt16();

        ushort RegisterCount = Strm.ReadUInt16(),
               ConstantCount = Strm.ReadUInt16();

        ushort RegisterRedCount = Strm.ReadUInt16(),
               RegisterGreenCount = Strm.ReadUInt16(),
               RegisterBlueCount = Strm.ReadUInt16(),
               RegisterAlphaCount = Strm.ReadUInt16();

        ushort ConstantRedCount = Strm.ReadUInt16(),
               ConstantGreenCount = Strm.ReadUInt16(),
               ConstantBlueCount = Strm.ReadUInt16(),
               ConstantAlphaCount = Strm.ReadUInt16();

        uint RegisterAnimationTableOffset = Strm.ReadUInt32() + ChunkStart,
             ConstantAnimationTableOffset = Strm.ReadUInt32() + ChunkStart,
             
             RegisterRemapTableOffset = Strm.ReadUInt32() + ChunkStart,
             ConstantRemapTableOffset = Strm.ReadUInt32() + ChunkStart,
             
             RegisterNameTableOffset = Strm.ReadUInt32() + ChunkStart,
             ConstantNameTableOffset = Strm.ReadUInt32() + ChunkStart;

        uint RegisterRedTableOffset = Strm.ReadUInt32() + ChunkStart,
             RegisterGreenTableOffset = Strm.ReadUInt32() + ChunkStart,
             RegisterBlueTableOffset = Strm.ReadUInt32() + ChunkStart,
             RegisterAlphaTableOffset = Strm.ReadUInt32() + ChunkStart;

        uint ConstantRedTableOffset = Strm.ReadUInt32() + ChunkStart,
             ConstantGreenTableOffset = Strm.ReadUInt32() + ChunkStart,
             ConstantBlueTableOffset = Strm.ReadUInt32() + ChunkStart,
             ConstantAlphaTableOffset = Strm.ReadUInt32() + ChunkStart;

        //These should both be in the file, even if one of them has no names in it...
        string[]? RegisterMaterialNames = null, ConstantMaterialNames = null;
        if (RegisterNameTableOffset != 0)
            RegisterMaterialNames = Strm.ReadJ3DStringTable((int)RegisterNameTableOffset);
        if (ConstantNameTableOffset != 0)
            ConstantMaterialNames = Strm.ReadJ3DStringTable((int)ConstantNameTableOffset);

        ushort[]? RegisterRemapIndicies = null, ConstantRemapIndicies = null;
        if (RegisterRemapTableOffset != 0)
        {
            Strm.Position = RegisterRemapTableOffset;
            RegisterRemapIndicies = Strm.ReadMultiUInt16(RegisterCount);
        }
        if (ConstantRemapTableOffset != 0)
        {
            Strm.Position = ConstantRemapTableOffset;
            ConstantRemapIndicies = Strm.ReadMultiUInt16(ConstantCount);
        }

        short[] RedTable, GreenTable, BlueTable, AlphaTable;

        if (RegisterAnimationTableOffset != 0)
        {
            if (RegisterMaterialNames is null || RegisterRemapIndicies is null)
                throw new IOException("File might be corrupted");

            RedTable   = Strm.ReadMultiAtOffset(RegisterRedTableOffset,   StreamUtil.ReadMultiInt16, RegisterRedCount);
            GreenTable = Strm.ReadMultiAtOffset(RegisterGreenTableOffset, StreamUtil.ReadMultiInt16, RegisterGreenCount);
            BlueTable  = Strm.ReadMultiAtOffset(RegisterBlueTableOffset,  StreamUtil.ReadMultiInt16, RegisterBlueCount);
            AlphaTable = Strm.ReadMultiAtOffset(RegisterAlphaTableOffset, StreamUtil.ReadMultiInt16, RegisterAlphaCount);

            for (int i = 0; i < RegisterCount; i++)
            {
                Animation anim = new() { MaterialName = RegisterMaterialNames[i], RegisterType = AnimationType.REGISTER }; //This is the only thing that doesn't get remapped
                int Index = RegisterRemapIndicies[i]; //Assuming this is how it works...

                Strm.Position = RegisterAnimationTableOffset + (Index * 0x1C);
                anim.Red   = J3D.Utility.ReadAnimationTrackInt16(Strm, RedTable, 1);
                anim.Green = J3D.Utility.ReadAnimationTrackInt16(Strm, GreenTable, 1);
                anim.Blue  = J3D.Utility.ReadAnimationTrackInt16(Strm, BlueTable, 1);
                anim.Alpha = J3D.Utility.ReadAnimationTrackInt16(Strm, AlphaTable, 1);
                anim.RegisterTarget = (byte)Strm.ReadByte();
                Strm.Position += 0x03;

                Add(anim);
            }
        }

        if (ConstantAnimationTableOffset != 0)
        {
            if (ConstantMaterialNames is null || ConstantRemapIndicies is null)
                throw new IOException("File might be corrupted");

            RedTable   = Strm.ReadMultiAtOffset(ConstantRedTableOffset,   StreamUtil.ReadMultiInt16, ConstantRedCount);
            GreenTable = Strm.ReadMultiAtOffset(ConstantGreenTableOffset, StreamUtil.ReadMultiInt16, ConstantGreenCount);
            BlueTable  = Strm.ReadMultiAtOffset(ConstantBlueTableOffset,  StreamUtil.ReadMultiInt16, ConstantBlueCount);
            AlphaTable = Strm.ReadMultiAtOffset(ConstantAlphaTableOffset, StreamUtil.ReadMultiInt16, ConstantAlphaCount);

            for (int i = 0; i < ConstantCount; i++)
            {
                Animation anim = new() { MaterialName = ConstantMaterialNames[i], RegisterType = AnimationType.CONSTANT }; //This is the only thing that doesn't get remapped
                int Index = ConstantRemapIndicies[i]; //Assuming this is how it works...

                Strm.Position = ConstantAnimationTableOffset + (Index * 0x1C);
                anim.Red = J3D.Utility.ReadAnimationTrackInt16(Strm, RedTable, 1);
                anim.Green = J3D.Utility.ReadAnimationTrackInt16(Strm, GreenTable, 1);
                anim.Blue = J3D.Utility.ReadAnimationTrackInt16(Strm, BlueTable, 1);
                anim.Alpha = J3D.Utility.ReadAnimationTrackInt16(Strm, AlphaTable, 1);
                anim.RegisterTarget = (byte)Strm.ReadByte();
                Strm.Position += 0x03;

                Add(anim);
            }
        }

        Strm.Position = ChunkStart + ChunkSize;
    }

    /// <inheritdoc/>
    public void Save(Stream Strm)
    {
        List<Animation> Registers = new(this.Where(x => x.RegisterType == AnimationType.REGISTER));
        List<Animation> Constants = new(this.Where(x => x.RegisterType == AnimationType.CONSTANT));

        long Start = Strm.Position;
        Strm.WriteUInt32(0x4A334431); // J3D1
        Strm.WriteUInt32(MAGIC);
        Strm.WritePlaceholder(4); //FileSize
        Strm.WriteUInt32(1);
        Strm.WriteJ3DSubVersion();

        long ChunkStart = Strm.Position;
        Strm.WriteUInt32(CHUNKMAGIC);
        Strm.WritePlaceholder(4); //ChunkSize
        Strm.WriteByte((byte)Loop);
        Strm.WriteByte(0xFF); //Padding
        Strm.WriteUInt16(Duration);
        Strm.WriteUInt16((ushort)Registers.Count);
        Strm.WriteUInt16((ushort)Constants.Count);
        Strm.WritePlaceholderMulti(2, 8); //Count Placeholders
        Strm.WritePlaceholderMulti(4, 14); //Offset Placeholders
        Strm.PadTo(32, J3D.Utility.PADSTRING);

        ushort CurrentRegisterRemap = 0;
        ushort CurrentConstantRemap = 0;
        List<string> RegisterNames = [];
        List<string> ConstantNames = [];
        List<ushort> RegisterRemapTable = [];
        List<ushort> ConstantRemapTable = [];
        List<short> RegisterRedTable = [];
        List<short> RegisterGreenTable = [];
        List<short> RegisterBlueTable = [];
        List<short> RegisterAlphaTable = [];
        List<short> ConstantRedTable = [];
        List<short> ConstantGreenTable = [];
        List<short> ConstantBlueTable = [];
        List<short> ConstantAlphaTable = [];

        long RegisterTableOffset = Strm.Position;
        foreach (Animation current in Registers)
        {
            RegisterNames.Add(current.MaterialName);
            ushort RemapIndex = CurrentRegisterRemap++;//Here would be a good idea to search and see if there's any other identical tracks that have already been written
            RegisterRemapTable.Add(RemapIndex);

            J3D.Utility.WriteAnimationTrackInt16(Strm, Registers[RemapIndex].Red, 1, ref RegisterRedTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, Registers[RemapIndex].Green, 1, ref RegisterGreenTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, Registers[RemapIndex].Blue, 1, ref RegisterBlueTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, Registers[RemapIndex].Alpha, 1, ref RegisterAlphaTable);
            Strm.WriteByte(current.RegisterTarget);
            Strm.PadTo(4, 0xFF);
        }

        long ConstantTableOffset = Strm.Position;
        foreach (Animation current in Constants)
        {
            ConstantNames.Add(current.MaterialName);
            ushort RemapIndex = CurrentConstantRemap++;//Here would be a good idea to search and see if there's any other identical tracks that have already been written
            ConstantRemapTable.Add(RemapIndex);

            J3D.Utility.WriteAnimationTrackInt16(Strm, Constants[RemapIndex].Red, 1, ref ConstantRedTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, Constants[RemapIndex].Green, 1, ref ConstantGreenTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, Constants[RemapIndex].Blue, 1, ref ConstantBlueTable);
            J3D.Utility.WriteAnimationTrackInt16(Strm, Constants[RemapIndex].Alpha, 1, ref ConstantAlphaTable);
            Strm.WriteByte(current.RegisterTarget);
            Strm.PadTo(4, 0xFF);
        }

        long RegisterRedTableOffset = Strm.Position;
        Strm.WriteMultiInt16(RegisterRedTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long RegisterGreenTableOffset = Strm.Position;
        Strm.WriteMultiInt16(RegisterGreenTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long RegisterBlueTableOffset = Strm.Position;
        Strm.WriteMultiInt16(RegisterBlueTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long RegisterAlphaTableOffset = Strm.Position;
        Strm.WriteMultiInt16(RegisterAlphaTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);


        long ConstantRedTableOffset = Strm.Position;
        Strm.WriteMultiInt16(ConstantRedTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long ConstantGreenTableOffset = Strm.Position;
        Strm.WriteMultiInt16(ConstantGreenTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long ConstantBlueTableOffset = Strm.Position;
        Strm.WriteMultiInt16(ConstantBlueTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long ConstantAlphaTableOffset = Strm.Position;
        Strm.WriteMultiInt16(ConstantAlphaTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);


        long RegisterRemapTableOffset = Strm.Position;
        Strm.WriteMultiUInt16(RegisterRemapTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long ConstantRemapTableOffset = Strm.Position;
        Strm.WriteMultiUInt16(ConstantRemapTable);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long RegisterNameTableOffset = Strm.Position;
        Strm.WriteJ3DStringTable(RegisterNames);
        Strm.PadTo(4, J3D.Utility.PADSTRING);

        long ConstantNameTableOffset = Strm.Position;
        Strm.WriteJ3DStringTable(ConstantNames);
        Strm.PadTo(32, J3D.Utility.PADSTRING);

        long FileLength = Strm.Position;

        Strm.Position = Start + 0x08;
        Strm.WriteUInt32((uint)(FileLength - Start));

        Strm.Position = ChunkStart + 0x04;
        Strm.WriteUInt32((uint)StreamUtil.ApplyEndian(FileLength - (ChunkStart - Start)));

        Strm.Position = ChunkStart + 0x10;
        Strm.WriteUInt16((ushort)RegisterRedTable.Count);
        Strm.WriteUInt16((ushort)RegisterGreenTable.Count);
        Strm.WriteUInt16((ushort)RegisterBlueTable.Count);
        Strm.WriteUInt16((ushort)RegisterAlphaTable.Count);
        Strm.WriteUInt16((ushort)ConstantRedTable.Count);
        Strm.WriteUInt16((ushort)ConstantGreenTable.Count);
        Strm.WriteUInt16((ushort)ConstantBlueTable.Count);
        Strm.WriteUInt16((ushort)ConstantAlphaTable.Count);
        Strm.WriteUInt32(Registers.Count > 0 ? (uint)(RegisterTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Constants.Count > 0 ? (uint)(ConstantTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Registers.Count > 0 ? (uint)(RegisterRemapTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Constants.Count > 0 ? (uint)(ConstantRemapTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32((uint)(RegisterNameTableOffset - ChunkStart));
        Strm.WriteUInt32((uint)(ConstantNameTableOffset - ChunkStart));
        Strm.WriteUInt32(Registers.Count > 0 ? (uint)(RegisterRedTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Registers.Count > 0 ? (uint)(RegisterGreenTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Registers.Count > 0 ? (uint)(RegisterBlueTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Registers.Count > 0 ? (uint)(RegisterAlphaTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Constants.Count > 0 ? (uint)(ConstantRedTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Constants.Count > 0 ? (uint)(ConstantGreenTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Constants.Count > 0 ? (uint)(ConstantBlueTableOffset - ChunkStart) : 0);
        Strm.WriteUInt32(Constants.Count > 0 ? (uint)(ConstantAlphaTableOffset - ChunkStart) : 0);

        Strm.Position = FileLength;
    }

    /// <inheritdoc cref="J3D.DocGen.COMMON_ANIMATIONCLASS"/>
    public class Animation : IJ3DAnimationContainer
    {
        /// <inheritdoc cref="J3D.DocGen.COMMON_MATERIALNAME"/>
        public string MaterialName { get; set; } = "";

        /// <summary>
        /// The type of register to apply to
        /// </summary>
        public AnimationType RegisterType { get; set; }
        private byte mRegisterId;
        /// <summary>
        /// The target register to apply to (selection based on <see cref="RegisterType"/>)
        /// </summary>
        public byte RegisterTarget
        {
            get => mRegisterId;
            set
            {
                if (value < 0 || value > 3)
                    throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(RegisterTarget)} must be either 0, 1, 2, or 3");
                mRegisterId = value;
            }
        }

        public J3DAnimationTrack Red { get; set; } = [];
        public J3DAnimationTrack Green { get; set; } = [];
        public J3DAnimationTrack Blue { get; set; } = [];
        public J3DAnimationTrack Alpha { get; set; } = [];

        public override string ToString() => $"{MaterialName} - {RegisterType}: {RegisterTarget}";

        public override bool Equals(object? obj)
            => obj is Animation animation &&
            MaterialName == animation.MaterialName &&
            RegisterType == animation.RegisterType &&
            RegisterTarget == animation.RegisterTarget &&
            Red.Equals(animation.Red) &&
            Green.Equals(animation.Green) &&
            Blue.Equals(animation.Blue) &&
            Alpha.Equals(animation.Alpha);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(MaterialName);
            hash.Add(RegisterType);
            hash.Add(RegisterTarget);
            hash.Add(Red);
            hash.Add(Green);
            hash.Add(Blue);
            hash.Add(Alpha);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Enum representing what registers can be targeted by an animation
    /// </summary>
    public enum AnimationType
    {
        /// <summary>
        /// Targets C0, C1, C2, and CPrev(?)
        /// </summary>
        REGISTER,
        /// <summary>
        /// Targets K0, K1, K2, and K3
        /// </summary>
        CONSTANT
    }
}