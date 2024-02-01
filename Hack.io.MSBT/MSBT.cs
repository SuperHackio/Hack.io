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
        FileUtil.ExceptionOnMisMatchedBOM(Strm);
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

            if (Header.Equals(MAGIC_LBL1))
                ReadLBL1();
            if (Header.Equals(MAGIC_ATR1))
                ReadATR1();
            if (Header.Equals(MAGIC_TXT2))
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
                int EntryCount = Strm.ReadInt32();
                uint Offset = Strm.ReadUInt32();
                Strm.Position = ChunkStart + Offset;

                for (int l = 0; l < EntryCount; l++)
                {
                    byte length = Strm.ReadUInt8();
                    string label = Strm.ReadString(length, Encoding.ASCII);
                    int Index = Strm.ReadInt32();
                    TemporaryLabelStorage.Add(Index, label);
                }
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

                uint StringOffset = Strm.ReadUInt32();
                Strm.Position = ChunkStart + StringOffset;
                NewAttribute.Comment = Strm.ReadString(TextEncoding, TextEncoding.GetStride());

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
        throw new NotImplementedException();
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
            set =>  mAttributes = value;
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