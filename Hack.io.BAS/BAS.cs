using Hack.io.Interface;
using Hack.io.Utility;

namespace Hack.io.BAS;

/// <summary>
/// Sound Data.<para/>
/// Can be embedded into BCKs as well in certain games
/// </summary>
public class BAS : List<BAS.Sound>, ILoadSaveFile
{
    public byte UNKNOWN;

    public void Load(Stream Strm)
    {
        //This format has no magic
        ushort SoundEntryCount = Strm.ReadUInt16();
        UNKNOWN = Strm.ReadUInt8();
        Strm.Position += 0x05; //Unknown Zeros
        for (int i = 0; i < SoundEntryCount; i++)
        {
            Sound snd = new()
            {
                SoundId = Strm.ReadUInt32(),
                StartFrame = Strm.ReadSingle(),
                EndFrame = Strm.ReadSingle(),
                CoarsePitch = Strm.ReadSingle(),
                Flags = Strm.ReadUInt32(),
                Volume = Strm.ReadUInt8(),
                FinePitch = Strm.ReadUInt8(),
                LoopCount = Strm.ReadUInt8(),
                Panning = Strm.ReadUInt8(),
                UNKNOWN = Strm.ReadUInt8()
            };
            Add(snd);

            Strm.Position += 0x07;
        }
    }

    public void Save(Stream Strm)
    {
        Strm.WriteUInt16((ushort)Count);
        Strm.WriteByte(UNKNOWN);
        Strm.PadTo(0x08);
        for (int i = 0; i < Count; i++)
        {
            Sound snd = this[i];
            Strm.WriteUInt32(snd.SoundId);
            Strm.WriteSingle(snd.StartFrame);
            Strm.WriteSingle(snd.EndFrame);
            Strm.WriteSingle(snd.CoarsePitch);
            Strm.WriteUInt32(snd.Flags);
            Strm.WriteByte(snd.Volume);
            Strm.WriteByte(snd.FinePitch);
            Strm.WriteByte(snd.LoopCount);
            Strm.WriteByte(snd.Panning);
            Strm.WriteByte(snd.UNKNOWN);
            Strm.PadTo(0x08);
        }
        //Strm.Write(CollectionUtil.InitilizeArray((byte)0x00, 0x18));
    }

    public class Sound
    {
        public uint SoundId;
        public float StartFrame;
        public float EndFrame;
        public float CoarsePitch;
        public uint Flags;
        public byte Volume;
        public byte FinePitch;
        public byte LoopCount;
        public byte Panning;
        public byte UNKNOWN;

        public override bool Equals(object? obj)
        {
            return obj is Sound sound &&
                   SoundId == sound.SoundId &&
                   StartFrame == sound.StartFrame &&
                   EndFrame == sound.EndFrame &&
                   CoarsePitch == sound.CoarsePitch &&
                   Flags == sound.Flags &&
                   Volume == sound.Volume &&
                   FinePitch == sound.FinePitch &&
                   LoopCount == sound.LoopCount &&
                   Panning == sound.Panning &&
                   UNKNOWN == sound.UNKNOWN;
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(SoundId);
            hash.Add(StartFrame);
            hash.Add(EndFrame);
            hash.Add(CoarsePitch);
            hash.Add(Flags);
            hash.Add(Volume);
            hash.Add(FinePitch);
            hash.Add(LoopCount);
            hash.Add(Panning);
            hash.Add(UNKNOWN);
            return hash.ToHashCode();
        }

        public override string ToString() => $"{SoundId} [{StartFrame}-{EndFrame}]";
    }
}