using Hack.io.Interface;
using Hack.io.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hack.io.MSBT;

public class MSBT : ILoadSaveFile
{
    public const int LABEL_MAX_LENGTH = 255;
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const string MAGIC = "MsgStdBn";
    public const string MAGIC_LBL1 = "LBL1";
    public const string MAGIC_ATR1 = "ATR1";
    public const string MAGIC_TXT2 = "TXT2";
    public const string MAGIC_LBL1_LE = "1LBL";
    public const string MAGIC_ATR1_LE = "1RTA";
    public const string MAGIC_TXT2_LE = "2TXT";
    
    private Encoding mEncoding = Encoding.UTF8;
    public Encoding TextEncoding
    {
        get => mEncoding;
        set
        {
            if (value != Encoding.UTF8 && value != Encoding.Unicode && value != Encoding.BigEndianUnicode)
                throw new ArgumentException($"Encoding value cannot be {value.EncodingName}");
            mEncoding = value;
        }
    }

    [DisallowNull]
    public List<Message> Messages { get; set; } = new();
    public int Count => Messages.Count;

    public void Load(Stream Strm)
    {
        long FileStart = Strm.Position;
        FileUtil.ExceptionOnBadMagic(Strm, MAGIC);
        ushort BOM = Strm.ReadUInt16();
        if (BOM == 0xFEFF)
        {
            StreamUtil.SetEndianBig();
        }
        else if (BOM == 0xFFFE)
        {
            StreamUtil.SetEndianLittle();
        }
        else
        {
            throw new InvalidOperationException($"Unknown File BOM {BOM:X2}");
        }
        Strm.Position += 0x02;
        byte EncByte = Strm.ReadUInt8();
        if (EncByte == 0)
            TextEncoding = Encoding.UTF8;
        if (EncByte == 1)
            TextEncoding = StreamUtil.GetCurrentEndian() ? Encoding.BigEndianUnicode : Encoding.Unicode;
        else
            throw new IOException("Encoding is invalid");

        if (Strm.ReadUInt8() != 0x03)
            throw new NotImplementedException("MSBT versions other than 3 are currently not supported");

        ushort SectionCount = Strm.ReadUInt16();
        Strm.Position += 0x02;
        uint FileSize = Strm.ReadUInt32();
        Strm.Position += 0x0A;

        Dictionary<int, string> TemporaryLabelStorage = new();
        List<Attribute> TemporaryAttributes = new();

        for (int i = 0; i < SectionCount; i++)
        {
            long ChunkStart = Strm.Position;
            string Header = Strm.ReadString(4, Encoding.ASCII);
            uint ChunkSize = Strm.ReadUInt32();
            Strm.Position += 0x08;

            if (Header.Equals(MAGIC_LBL1) || Header.Equals(MAGIC_LBL1_LE))
                ReadLBL1();
            if (Header.Equals(MAGIC_ATR1) || Header.Equals(MAGIC_ATR1_LE))
                ReadATR1();
            if (Header.Equals(MAGIC_TXT2) || Header.Equals(MAGIC_TXT2_LE))
                ReadTXT2();

            Strm.Position = ChunkStart + 0x10 + ChunkSize;
            if (ChunkSize % 16 > 0)
                Strm.Position += (16 - (ChunkSize % 16));
        }

        //Join things
        for (int i = 0; i < Messages.Count; i++)
        {
            Message Current = Messages[i];
            if (TemporaryLabelStorage.ContainsKey(i))
                Current.Label = TemporaryLabelStorage[i];
            else
                throw new KeyNotFoundException($"Failed to find a Label for message {i}");

            if (i < TemporaryAttributes.Count)
                Current.Attributes = TemporaryAttributes[i];
            else
                Current.Attributes = new(); //Use the defaults
        }

        Strm.Position = FileStart + FileSize;



        void ReadLBL1()
        {
            long ChunkStart = Strm.Position;
            uint Count = Strm.ReadUInt32();
            long BucketStart = Strm.Position;

            for (uint i = 0; i < Count; i++)
            {
                Strm.Position = BucketStart + (i * 8);
                uint EntryCount = Strm.ReadUInt32();
                uint Offset = Strm.ReadUInt32();
                Strm.Position = ChunkStart + Offset;
                LabelEntry labelEntry = new();
                labelEntry.numStrings = EntryCount;
                labelEntry.indexes = new int[EntryCount];
                labelEntry.strings = new string[EntryCount];

                for (int l = 0; l < EntryCount; l++)
                {
                    byte length = Strm.ReadUInt8();
                    string label = Strm.ReadString(length, Encoding.ASCII);
                    int Index = Strm.ReadInt32();
                    TemporaryLabelStorage.Add(Index, label);
                    labelEntry.indexes[l] = Index;
                    labelEntry.strings[l] = label;
                }
                LabelEntries.Add(labelEntry);
            }
        }

        void ReadATR1()
        {
            long ChunkStart = Strm.Position;
            uint Count = Strm.ReadUInt32();
            uint Size = Strm.ReadUInt32();

            for (int i = 0; i < Count; i++)
            {
                Strm.Position = ChunkStart + 0x08 + (i * Size);
                Attribute NewAttribute = new()
                {
                    SoundId = Strm.ReadUInt8(),
                    CameraType = Strm.ReadEnum<CameraType, byte>(StreamUtil.ReadUInt8),
                    TalkType = Strm.ReadEnum<TalkType, byte>(StreamUtil.ReadUInt8),
                    MessageBoxType = Strm.ReadEnum<MessageBoxType, byte>(StreamUtil.ReadUInt8),
                    CameraId = Strm.ReadUInt16(),
                    MessageAreaId = Strm.ReadUInt8(),
                    AlreadyTalked = Strm.ReadUInt8()
                };

                uint StringOffset = (uint)StreamUtil.ApplyEndian(Strm.ReadUInt32());
                long curPos = Strm.Position;
                Strm.Position = ChunkStart + StringOffset;
                NewAttribute.Comment = Strm.ReadString(TextEncoding, TextEncoding.GetStride());
                if (Size == 0x10)
                {
                    // In the Switch port of SMG2 an unknown string offset has been added
                    Strm.Position = curPos;
                    StringOffset = (uint)StreamUtil.ApplyEndian(Strm.ReadUInt32());
                    Strm.Position = ChunkStart + StringOffset;
                    NewAttribute.Unknown = Strm.ReadString(TextEncoding, TextEncoding.GetStride());
                }
                TemporaryAttributes.Add(NewAttribute);
            }
        }

        void ReadTXT2()
        {
            long ChunkStart = Strm.Position;
            uint Count = Strm.ReadUInt32();

            for (int i = 0; i < Count; i++)
            {
                Strm.Position = ChunkStart + 0x04 + (i * 0x04);
                uint StringOffset = Strm.ReadUInt32();
                Strm.Position = ChunkStart + StringOffset;

                Message Msg = new();
                Msg.ReadFromBinary(Strm, TextEncoding);
                Messages.Add(Msg);
            }
        }
    }

    public void Save(Stream Strm)
    {
        Strm.WriteString("MsgStdBn", Encoding.ASCII, null);
        Strm.WriteUInt16(0xFEFF);
        Strm.Position += 0x02;
        if (TextEncoding == Encoding.UTF8)
            Strm.WriteUInt8(0);
        else if (TextEncoding == Encoding.BigEndianUnicode || TextEncoding == Encoding.Unicode)
            Strm.WriteUInt8(1);
        Strm.WriteUInt8(0x03);
        long FileStart = Strm.Position;
        // Section Count
        // Strm.Position += 0x02
        // File Size
        // Strm.Position += 0x0A
        Strm.Position += 0x12;

        ushort SectionCount = 0;
        WriteLBL1();
        WriteATR1();
        WriteTXT2();
        uint FileSize = (uint)Strm.Position;

        Strm.Position = FileStart;
        Strm.WriteUInt16(SectionCount);
        Strm.Position += 0x02;
        Strm.WriteUInt32(FileSize);
        Strm.Close();


        /*uint CalcHashBucketIndex(string label, uint bucketCount)
        {
            uint hash = 0;
            foreach (char c in label)
                hash = (hash * 0x492 + c) & 0xFFFFFFFF;
            return hash % bucketCount;
        }*/

        void WriteLBL1()
        {
            Strm.WriteUInt32(0x4C424C31); // LBL1
            long SectionStart = Strm.Position;
            Strm.Position += 0xC;
            long ChunkStart = Strm.Position;
            int labelCount = LabelEntries.Count;
            Strm.WriteUInt32((uint)labelCount);

            long LabelEntrySize = 4 + labelCount * 8;
            long StringSize = 0;

            for (int i = 0; i < labelCount; i++)
            {
                LabelEntry Current = LabelEntries[i];
                Strm.Position = ChunkStart + 4 + i * 8;
                Strm.WriteUInt32(Current.numStrings);
                uint StringOffset = (uint)(LabelEntrySize + StringSize);
                Strm.WriteUInt32(StringOffset);
                Strm.Position = ChunkStart + StringOffset;
                for (int j = 0; j < Current.numStrings; j++)
                {
                    Strm.WriteUInt8((byte)Current.strings[j].Length);
                    Strm.WriteString(Current.strings[j], Encoding.ASCII, null);
                    Strm.WriteInt32(Current.indexes[j]);
                    StringSize += 1 + Current.strings[j].Length + 4;
                }
            }

            long AfterEntries = Strm.Position;
            uint SectionSize = (uint)(AfterEntries - ChunkStart);
            Strm.Position = SectionStart;
            Strm.WriteUInt32(SectionSize);
            Strm.Position = AfterEntries;
            Strm.PadTo(16, 0xAB);
            SectionCount++;
        }

        void WriteATR1()
        {
            Strm.WriteUInt32(0x41545231); // ATR1
            long SectionStart = Strm.Position;
            Strm.Position += 0xC;
            long ChunkStart = Strm.Position;
            int bucketCount = Messages.Count;
            Strm.WriteUInt32((uint)bucketCount);

            uint AttrSize = 0xC;
            if (Messages.Count > 0 && Messages[0].Attributes.Unknown != null)
                AttrSize = 0x10;
            Strm.WriteUInt32(AttrSize);

            long StringSize = 0;
            for (int i = 0; i < bucketCount; i++)
            {
                Attribute Current = Messages[i].Attributes;
                Strm.WriteByte(Current.SoundId);
                Strm.WriteEnum<CameraType, byte>(Current.CameraType, StreamUtil.WriteUInt8);
                Strm.WriteEnum<TalkType, byte>(Current.TalkType, StreamUtil.WriteUInt8);
                Strm.WriteEnum<MessageBoxType, byte>(Current.MessageBoxType, StreamUtil.WriteUInt8);
                Strm.WriteUInt16(Current.CameraId);
                Strm.WriteByte(Current.MessageAreaId);
                Strm.WriteByte(Current.AlreadyTalked);

                uint StringOffset = (uint)(8 + AttrSize * bucketCount + StringSize);
                Strm.WriteUInt32((uint)StreamUtil.ApplyEndian(StringOffset));
                long curPos = Strm.Position;
                Strm.Position = ChunkStart + StringOffset;
                Strm.WriteString(Current.Comment, TextEncoding);
                StringSize += TextEncoding.GetByteCount(Current.Comment) + TextEncoding.GetByteCount("\0");
                Strm.Position = curPos;

                if (Current.Unknown != null)
                {
                    StringOffset = (uint)(8 + AttrSize * bucketCount + StringSize);
                    Strm.WriteUInt32((uint)StreamUtil.ApplyEndian(StringOffset));
                    curPos = Strm.Position;
                    Strm.Position = ChunkStart + StringOffset;
                    Strm.WriteString(Current.Comment, TextEncoding);
                    StringSize += TextEncoding.GetByteCount(Current.Comment) + TextEncoding.GetByteCount("\0");
                    Strm.Position = curPos;
                }
            }
            long AfterEntries = 8 + AttrSize * bucketCount + StringSize;
            uint SectionSize = (uint)AfterEntries;
            Strm.Position = SectionStart;
            Strm.WriteUInt32(SectionSize);

            Strm.Position = ChunkStart + AfterEntries;
            Strm.PadTo(16, 0xAB);
            SectionCount++;
        }
        
        void WriteTXT2()
        {
            Strm.WriteUInt32(0x54585432); // TXT2
            long SectionStart = Strm.Position;
            Strm.Position += 0xC;
            long ChunkStart = Strm.Position;
            int messageCount = Messages.Count;
            Strm.WriteUInt32((uint)messageCount);

            uint TextSize = 0;
            long curPos;
            for (int i = 0; i < messageCount; i++)
            {
                Message Current = Messages[i];
                uint StringOffset = (uint)(4 + messageCount * 4 + TextSize); // offsets are relative to ChunkStart
                Strm.WriteUInt32(StringOffset);

                curPos = Strm.Position;
                Strm.Position = ChunkStart + StringOffset;
                long before = Strm.Position;
                Current.WriteToBinary(Strm, TextEncoding);
                long after = Strm.Position;

                uint written = (uint)(after - before); // how many bytes this message used
                TextSize += written;

                Strm.Position = curPos;
            }

            // section size = 4 (count) + 4*messageCount (offsets) + TextSize
            uint sectionSize = (uint)(4 + 4 * messageCount + TextSize);
            long afterEntries = ChunkStart + sectionSize;
            Strm.Position = SectionStart;
            Strm.WriteUInt32(sectionSize);

            Strm.Position = ChunkStart + sectionSize;
            Strm.PadTo(16, 0xAB);
            SectionCount++;
        }

    }

    public Message? FindByLabel(string Label)
    {
        for (int i = 0; i < Messages.Count; i++)
        {
            if (Messages[i].Label.Equals(Label))
                return Messages[i];
        }
        return null;
    }

    public ushort IndexOf(Message message) => (ushort)Messages.IndexOf(message);

    public struct LabelEntry
    {
        public uint numStrings;
        public int[] indexes;
        public string[] strings;
    }
    
    public List<LabelEntry> LabelEntries { get; set; } = new();

    public class Message
    {
        private string mLabel = "";
        private string mContent = "";
        private Attribute mAttributes;

        public string Label
        {
            get => mLabel;
            set
            {
                if (value.Length > LABEL_MAX_LENGTH)
                    throw new OutOfMemoryException($"The label \"{value}\" exceeds the {LABEL_MAX_LENGTH} character limit");
                mLabel = value;
            }
        }
        public string Content
        {
            get => mContent;
            set => mContent = value;
        }

        public Attribute Attributes
        {
            get => mAttributes;
            set => mAttributes = value;
        }



        internal void ReadFromBinary(Stream Strm, Encoding Enc)
        {
            while (Strm.Position < Strm.Length) //Forced limited to avoid softlock
            {
                char Current = Strm.ReadString(1, Enc, Enc.GetStride())[0];

                if (Current == 0)
                    break;

                if (Current == 0x0E)
                {
                    mContent += ReadTag(Strm, Enc);
                    continue;
                }

                if (Current == '[')
                {
                    mContent += "\\[";
                    continue;
                }

                if (Current == ']')
                {
                    mContent += "\\]";
                    continue;
                }

                if (Current == '\\')
                {
                    mContent += "\\\\";
                    continue;
                }

                mContent += Current;
            }
        }

        internal static string ReadTag(Stream Strm, Encoding Enc)
        {
            long TagStart = Strm.Position;
            ushort Group = Strm.ReadUInt16();
            ushort TagId = Strm.ReadUInt16();
            ushort Size = Strm.ReadUInt16();

            //if (Group == 0)
            //{

            //}

            byte[] Data = new byte[Size];
            Strm.Read(Data);
            string d = "";
            for (int i = 0; i < Size; i++)
                d += Data[i].ToString("X2");
            return $"[{Group}:{TagId};{d}]";
        }

        internal void WriteToBinary(Stream Strm, Encoding Enc)
        {
            for (int i = 0; i < mContent.Length; i++)
            {
                // guard against lookahead out of range
                if (mContent[i] == '\\' && i + 1 < mContent.Length && mContent[i + 1] == '[')
                {
                    Strm.WriteString("[", Enc);
                    i++;
                    continue;
                }
                if (mContent[i] == '\\' && i + 1 < mContent.Length && mContent[i + 1] == ']')
                {
                    Strm.WriteString("]", Enc);
                    i++;
                    continue;
                }
                if (mContent[i] == '\\' && i + 1 < mContent.Length && mContent[i + 1] == '\\')
                {
                    Strm.WriteString("\\", Enc);
                    i++;
                    continue;
                }

                if (mContent[i] == '[')
                {
                    // WriteTag returns the number of characters consumed (including brackets)
                    int consumed = WriteTag(mContent, i, Strm, Enc);
                    if (consumed <= 0)
                        throw new InvalidOperationException("WriteTag failed to consume characters");
                    // the for-loop will increment i by 1; compensate:
                    i += consumed - 1;
                    continue;
                }

                Strm.WriteString(mContent[i].ToString(), Enc, null, !StreamUtil.GetCurrentEndian());
            }

            Strm.WriteString("\0", Enc, null);
        }

        internal static int WriteTag(string Content, int StartIndex, Stream Strm, Encoding Enc)
        {
            // StartIndex should point at '['
            if (StartIndex >= Content.Length || Content[StartIndex] != '[')
                throw new ArgumentException("StartIndex must point at '['");

            int endIndex = Content.IndexOf(']', StartIndex + 1);
            if (endIndex == -1)
                throw new InvalidOperationException("Tag is missing closing ']'");

            // extract inside of brackets (without the [ ])
            string inside = Content.Substring(StartIndex + 1, endIndex - (StartIndex + 1));
            // expected format "Group:TagId;HEXDATA"
            string[] parts = inside.Split(new char[] { ':', ';' }, StringSplitOptions.None);
            if (parts.Length < 3)
                throw new InvalidOperationException($"Invalid tag format: {inside}");

            ushort group = Convert.ToUInt16(parts[0]);
            ushort tagId = Convert.ToUInt16(parts[1]);
            string hex = parts[2];

            if (hex.Length % 2 != 0)
                throw new InvalidOperationException("Tag hex data length must be even");

            ushort size = (ushort)(hex.Length / 2);

            Strm.WriteUInt16(0x0E);

            // write group, tagId, size as UInt16 respecting current endianness (use your helpers)
            Strm.WriteUInt16(group);
            Strm.WriteUInt16(tagId);
            Strm.WriteUInt16(size);

            if (group == 2)
            {
                // write the data bytes as they are (no endian inversion for single bytes)
                for (int i = 1; i >= 0; i--)
                {
                    string bs = hex.Substring(i * 2, 2);
                    byte b = Convert.ToByte(bs, 16);
                    Strm.WriteByte(b);
                }
                for (int i = 2; i < size; i++)
                {
                    string bs = hex.Substring(i * 2, 2);
                    byte b = Convert.ToByte(bs, 16);
                    Strm.WriteByte(b);
                }
            }
            else
            {
                // write the data bytes as they are (no endian inversion for single bytes)
                for (int i = size - 1; i >= 0; i--)
                {
                    string bs = hex.Substring(i * 2, 2);
                    byte b = Convert.ToByte(bs, 16);
                    Strm.WriteByte(b);
                }
            }

            // total consumed characters including '[' and ']'
            return (endIndex - StartIndex + 1);
        }

        public override string ToString() => $"{mLabel}: {mContent}";
    }

    public struct Attribute
    {
        public byte SoundId;
        public CameraType CameraType;
        public TalkType TalkType;
        public MessageBoxType MessageBoxType;
        public ushort CameraId;
        public byte MessageAreaId;
        /// <summary>
        /// Unknown functionality
        /// </summary>
        public byte AlreadyTalked;
        [DisallowNull]
        public string Comment;
        public string? Unknown;

        public Attribute()
        {
            SoundId = 0x01;
            CameraType = CameraType.AUTO;
            TalkType = TalkType.TALK;
            MessageBoxType = MessageBoxType.WHITE_BOX;
            CameraId = 0x00;
            MessageAreaId = 0xFF;
            AlreadyTalked = 0xFF;
            Comment = "";
        }

        public override readonly string ToString() => $"Sound: {SoundId}, CameraSetting: {CameraType}, Trigger {TalkType}, Box: {MessageBoxType}, Comment: \"{Comment}\"";
    }

    public enum TalkType : byte
    {
        TALK,
        SHOUT,
        AUTO,
        AUTO_GLOBAL
    }

    public enum CameraType : byte
    {
        /// <summary>
        /// The game will automatically setup the camera for you
        /// </summary>
        AUTO,
        /// <summary>
        /// The game will try to reference a camera found inside the CameraParam.bcam file
        /// </summary>
        MANUAL,
        /// <summary>
        /// The came won't update the camera.
        /// </summary>
        NONE
    }

    public enum MessageBoxType : byte
    {
        WHITE_BOX = 0,
        WHITE_BOX_DUPLICATE = 1,
        WHITE_BOX_NO_ICONA = 2,
        SIGNBOARD = 3,
        ICON_BUBBLE = 4,
        UNKNOWN5 = 5,
        UNKNOWN6 = 6,
        UNKNOWN7 = 7,
        UNKNOWN8 = 8,
    }
}