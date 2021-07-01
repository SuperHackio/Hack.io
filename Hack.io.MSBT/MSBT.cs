using System;
using System.Collections.Generic;
using System.IO;
using Hack.io;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hack.io.Util;

namespace Hack.io.MSBT
{
    public class MSBT
    {
        EncodingByte TextEncoding { get; set; }
        /// <summary>
        /// Should always return Encoding.BigEndianUnicode
        /// </summary>
        /// <returns></returns>
        public Encoding GetEncoding() => TextEncoding == EncodingByte.UTF8 ? Encoding.UTF8 : Encoding.BigEndianUnicode;
        List<Label> Messages = new List<Label>();
        public Label this[int LabelID]
        {
            get => Messages[LabelID];
        }

        /// <summary>
        /// File Identifier
        /// </summary>
        private const string Magic = "MsgStdBn";
        private const ushort SectionCount = 3;

        public MSBT(string file)
        {
            FileStream FS = new FileStream(file, FileMode.Open);
            Read(FS);
            FS.Close();
        }
        public MSBT(Stream Stream)
        {
            Read(Stream);
        }
        public void Save(string file)
        {
            FileStream FS = new FileStream(file, FileMode.Create);
            Write(FS);
            FS.Close();
        }
        public List<Label> GetSortedLabels
        {
            get
            {
                List<Label> Result = new List<Label>();
                for (int i = 0; i < Messages.Count; i++)
                {
                    Result.Add(Messages[i]);
                }
                Result.Sort(LabelSortDelegate);
                return Result;
            }
        }

        private int LabelSortDelegate(Label x, Label y)
        {
            return x.Name.CompareTo(y.Name);
        }

        private void Read(Stream FS)
        {
            if (FS.ReadString(8) != Magic)
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");
            if (FS.PeekByte() != 0xFE)
                throw new Exception("Little Endian Not supported");
            FS.Position = 0x0C;
            TextEncoding = (EncodingByte)FS.ReadByte();
            FS.Position = 0x20;

            //Item1 = NumberOfLabels
            //Item2 = Offset
            List<Tuple<uint, uint>> Groups = new List<Tuple<uint, uint>>();
            SortedDictionary<string, Label> SortedLabels = new SortedDictionary<string, Label>();
            List<Attribute> Attribs = new List<Attribute>();

            for (int i = 0; i < SectionCount; i++)
            {
                long ChunkStart = FS.Position;
                long ChunkOffset = 0;
                string Header = FS.ReadString(4);
                uint ChunkSize = BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0);
                ChunkOffset = 0x10;
                FS.Position += 0x08;
                if (Header.Equals("LBL1"))
                {
                    //Labels
                    long LabelStart = FS.Position;
                    uint GroupCount = BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0);
                    for (int y = 0; y < GroupCount; y++)
                    {
                        Groups.Add(new Tuple<uint, uint>(BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0), BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0)));
                    }
                    foreach (Tuple<uint, uint> grp in Groups)
                    {
                        FS.Position = LabelStart + grp.Item2;
                        Console.WriteLine("Group ID: "+ (uint)Groups.IndexOf(grp));
                        for (int z = 0; z < grp.Item1; z++)
                        {
                            Label lbl = new Label { Length = Convert.ToUInt32(FS.ReadByte()) };
                            lbl.Name = FS.ReadString((int)lbl.Length);
                            lbl.Index = BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0);
                            lbl.Checksum = (uint)Groups.IndexOf(grp);
                            SortedLabels.Add(lbl.Name, lbl);
                            Messages.Add(lbl);
                            Console.WriteLine("Label: "+lbl.Name);
                        }
                        Console.WriteLine();
                    }
                    foreach (KeyValuePair<string, Label> lbl in SortedLabels)
                    {
                        uint previousChecksum = lbl.Value.Checksum;
                        lbl.Value.Checksum = LabelChecksum(lbl.Value.Name, (uint)Groups.Count);

                        if (previousChecksum != lbl.Value.Checksum)
                        {
                            Groups[(int)previousChecksum] = new Tuple<uint, uint>(Groups[(int)previousChecksum].Item1 - 1, Groups[(int)previousChecksum].Item2);
                            Groups[(int)lbl.Value.Checksum] = new Tuple<uint, uint>(Groups[(int)previousChecksum].Item1 + 1, Groups[(int)previousChecksum].Item2);
                        }
                    }
                }
                else if (Header.Equals("ATR1"))
                {
                    //Attributes
                    long AttributeStart = FS.Position;
                    uint EntryCount = BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0);
                    FS.Position += 0x04;
                    for (int x = 0; x < EntryCount; x++)
                        SortedLabels.ElementAt(x).Value.Attributes = new Attribute(FS, AttributeStart);
                }
                else if (Header.Equals("TXT2"))
                {
                    //Strings
                    long MessagesStart = FS.Position;
                    uint EntryCount = BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0);
                    Encoding Enc = GetEncoding();
                    for (int x = 0; x < EntryCount; x++)
                    {
                        uint StringOffset = BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0);
                        long PausePosition = FS.Position;
                        FS.Position = MessagesStart + StringOffset;
                        SortedLabels.ElementAt(x).Value.Message = new Message(FS, Enc);
                        FS.Position = PausePosition;
                    }
                }
                else
                    throw new Exception($"Invalid or Unsupported section \"{Header}\"");

                FS.Position = ChunkStart + ChunkOffset + ChunkSize;
                while (FS.PeekByte() == 0xAB)
                    FS.Position++;
            }
        }
        private void Write(Stream FS)
        {
            //Impressive one-liner is impressive.....I think...
            SortedDictionary<string, Label> SortedLabels = new SortedDictionary<string, Label>(Messages.ToDictionary(prop => prop.Name, prop => prop));
            //Item1 = NumberOfLabels
            //Item2 = Offset
            List<Tuple<uint, uint>> Groups = new List<Tuple<uint, uint>>();
            List<byte> WrittenLabels = new List<byte>();

            FS.WriteString(Magic);
            FS.WriteByte(0xFE);
            FS.WriteByte(0xFF);
            FS.Write(new byte[2], 0, 2);
            FS.WriteByte((byte)TextEncoding);
            FS.Write(new byte[3] { 0x03, 0x00, 0x03 }, 0, 3);
            FS.Write(new byte[2], 0, 2);
            FS.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            while (FS.Position < 0x20)
                FS.WriteByte(0x00);

            long LBL1Start = FS.Position;
            FS.WriteString("LBL1");
            FS.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            FS.Write(new byte[8], 0, 8);
            FS.WriteReverse(BitConverter.GetBytes(Messages.Count), 0, 4);

            MakeGroups((int)(LBL1Start+0x10), ref Groups, ref WrittenLabels);
            for (int i = 0; i < Groups.Count; i++)
            {
                FS.WriteReverse(BitConverter.GetBytes(Groups[i].Item1), 0, 4);
                FS.WriteReverse(BitConverter.GetBytes(Groups[i].Item2), 0, 4);
            }
            FS.Write(WrittenLabels.ToArray(), 0, WrittenLabels.Count);
            long LBL1End = FS.Position;
            FS.PadTo(16, 0xAB);

            long ATR1Start = FS.Position;
            FS.WriteString("ATR1");
            FS.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            FS.Write(new byte[8], 0, 8);
            FS.WriteReverse(BitConverter.GetBytes(Messages.Count), 0, 4);
            FS.Write(new byte[4] { 0x00, 0x00, 0x00, 0x0C }, 0, 4);

            Dictionary<string, uint> AttributeStringOffsets = new Dictionary<string, uint>();

            int StringOffset = (int)(FS.Position - 0x08) + 0xC * SortedLabels.Count;
            foreach (KeyValuePair<string, Label> item in SortedLabels)
            {
                Attribute ATR = item.Value.Attributes;
                byte[] temp = ATR.Write(GetEncoding(), ref AttributeStringOffsets, ref StringOffset);
                FS.Write(temp, 0, temp.Length);
            }

            foreach (KeyValuePair<string, uint> item in AttributeStringOffsets)
            {
                string temp = item.Key;
                FS.WriteString(item.Key, GetEncoding());
            }
            long ATR1End = FS.Position;
            FS.PadTo(16, 0xAB);

            long TXT2Start = FS.Position;
            FS.WriteString("TXT2");
            FS.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            FS.Write(new byte[8], 0, 8);
            FS.WriteReverse(BitConverter.GetBytes(Messages.Count), 0, 4);

            List<byte> MessageData = new List<byte>();
            StringOffset = 4 + (4 * Messages.Count);
            foreach (KeyValuePair<string, Label> item in SortedLabels)
            {
                FS.WriteReverse(BitConverter.GetBytes(StringOffset), 0, 4);
                byte[] msg = item.Value.Message.Write(GetEncoding());
                MessageData.AddRange(msg);
                StringOffset += msg.Length;
            }
            FS.Write(MessageData.ToArray(), 0, MessageData.Count);
            long TXT2End = FS.Position;
            FS.PadTo(16, 0xAB);

            //DON'T FORGET TO WRITE THE SECTION SIZES!!!
            FS.Position = 0x12;
            FS.WriteReverse(BitConverter.GetBytes((int)FS.Length),0,4);
            FS.Position = LBL1Start + 0x04;
            FS.WriteReverse(BitConverter.GetBytes((int)(LBL1End - (LBL1Start + 0x10))), 0, 4);
            FS.Position = ATR1Start + 0x04;
            FS.WriteReverse(BitConverter.GetBytes((int)(ATR1End - (ATR1Start + 0x10))), 0, 4);
            FS.Position = TXT2Start + 0x04;
            FS.WriteReverse(BitConverter.GetBytes((int)(TXT2End - (TXT2Start + 0x10))), 0, 4);
        }
        private void MakeGroups(int LabelsStart, ref List<Tuple<uint, uint>> Groups, ref List<byte> LabelData)
        {
            uint Offset = (uint)(LabelsStart + (8 * Messages.Count));
            for (int i = 0; i < Messages.Count; i++)
            {
                Groups.Add(new Tuple<uint, uint>(1, Offset));
                Offset += (uint)(0x01 + Messages[i].Name.Length + 1 + 4);
                LabelData.Add((byte)Messages[i].Name.Length);
                LabelData.AddRange(Encoding.UTF8.GetBytes(Messages[i].Name));
                LabelData.AddRange(BitConverter.GetBytes(i).Reverse());
            }
        }
        private uint LabelChecksum(string label, uint GroupCount)
        {
            uint group = 0;

            for (int i = 0; i < label.Length; i++)
            {
                group *= 0x492;
                group += label[i];
                group &= 0xFFFFFFFF;
            }

            return group % GroupCount;
        }
        public class Label
        {
            public uint Length;
            public string Name;
            public uint Checksum;
            public Attribute Attributes;
            public Message Message;

            public uint Index { get; set; }

            public override string ToString() => (Length > 0 ? Name : (Index + 1).ToString()) + " - " + (Message is null ? "" : Message.ToString());
        }

        public class Attribute
        {
            public byte SoundID { get; set; } = 0;
            public byte CameraSetting { get; set; } = 0;
            public Trigger Trigger { get; set; } = Trigger.TALK;
            public MessageBoxType MessageBox { get; set; } = MessageBoxType.BUBBLE;
            public ushort Unknown3 { get; set; } = 0;
            public sbyte CameraID { get; set; } = -1;
            public sbyte MessageAreaID { get; set; } = -1;
            public string Unknown6 { get; set; }

            public Attribute(Stream FS, long Start)
            {
                SoundID = (byte)FS.ReadByte();
                CameraSetting = (byte)FS.ReadByte();
                Trigger = (Trigger)FS.ReadByte();
                MessageBox = (MessageBoxType)FS.ReadByte();
                Unknown3 = BitConverter.ToUInt16(FS.ReadReverse(0, 2), 0);
                CameraID = (sbyte)FS.ReadByte();
                MessageAreaID = (sbyte)FS.ReadByte();
                uint StringOffset = BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0);
                long PausePosition = FS.Position;
                FS.Position = Start + StringOffset;
                Unknown6 = FS.ReadString(Encoding.BigEndianUnicode);
                FS.Position = PausePosition;
            }
            internal byte[] Write(Encoding Enc, ref Dictionary<string, uint> AttributeStringOffsets, ref int StringOffset)
            {
                List<byte> Data = new List<byte>() { SoundID, CameraSetting, (byte)Trigger, (byte)MessageBox };
                Data.AddRange(BitConverter.GetBytes(Unknown3).Reverse());
                Data.Add((byte)CameraID);
                Data.Add((byte)MessageAreaID);
                if (!AttributeStringOffsets.ContainsKey(Unknown6))
                {
                    AttributeStringOffsets.Add(Unknown6, (uint)StringOffset);
                    StringOffset += Enc.GetByteCount(Unknown6);
                }
                Data.AddRange(BitConverter.GetBytes(AttributeStringOffsets[Unknown6]).Reverse());
                return Data.ToArray();
            }

            public override string ToString() => $"Sound: {SoundID}, CameraSetting: {CameraSetting}, Trigger {Trigger.ToString()}, Box: {MessageBox.ToString()}";
        }

        public class Message
        {
            public List<IMessageObject> Characters { get; set; } = new List<IMessageObject>();

            public Message(Stream FS, Encoding Enc)
            {
                while (FS.Position < FS.Length)
                {
                    char Current = FS.ReadChar(Enc);

                    switch (Current)
                    {
                        case '\0':
                            goto SUCCESS;
                        case (char)0x0E:
                            Opcode Opcode = (Opcode)FS.ReadChar(Enc);
                            switch (Opcode)
                            {
                                case Opcode.SYSTEM:
                                    Characters.Add(new SystemGroup(FS, Enc));
                                    break;
                                case Opcode.DISPLAY:
                                    Characters.Add(new DisplayGroup(FS, Enc));
                                    break;
                                case Opcode.SOUND:
                                    Characters.Add(new SoundGroup(FS, Enc));
                                    break;
                                case Opcode.PICTURE:
                                    Characters.Add(new PictureGroup(FS, Enc));
                                    break;
                                case Opcode.FONTSIZE:
                                    Characters.Add(new FontSizeGroup(FS, Enc));
                                    break;
                                case Opcode.LOCALIZE:
                                    Characters.Add(new LocalizeGroup(FS, Enc));
                                    break;
                                case Opcode.NUMBER:
                                    Characters.Add(new NumberGroup(FS, Enc));
                                    break;
                                case Opcode.STRING:
                                    Characters.Add(new StringGroup(FS, Enc));
                                    break;
                                case Opcode.RACETIME:
                                    Characters.Add(new RaceTimeGroup(FS, Enc));
                                    break;
                                case Opcode.FONT:
                                    Characters.Add(new FontGroup(FS, Enc));
                                    break;
                                default:
                                    throw new Exception($"Unknown MSBT Opcode: {Opcode.ToString("X" + Enc.GetStride())}");
                            }
                            break;
                        default:
                            Characters.Add(new Character() { Char = Current });
                            break;
                    }
                }
                throw new Exception("Stream ended before the String was terminated!");

            SUCCESS:;

            }

            internal byte[] Write(Encoding Enc)
            {
                List<byte> MessageData = new List<byte>();
                for (int i = 0; i < Characters.Count; i++)
                {
                    if (!(Characters[i] is Character))
                    {
                        MessageData.AddRange(Enc.GetBytes(new char[1] { (char)0x0E }));
                        MessageData.AddRange(Enc.GetBytes(new char[1] { (char)((FormatGroup)Characters[i]).Opcode }));
                    }
                    MessageData.AddRange(Characters[i].Write(Enc));
                }
                MessageData.AddRange(new byte[2]);
                return MessageData.ToArray();
            }

            public override string ToString()
            {
                string msg = "";
                for (int i = 0; i < Characters.Count; i++)
                {
                    if (Characters[i] is SystemGroup SG && SG.Action == System.RUBY)
                        msg += SG.ToRubyString();
                    else if (Characters[i] is DisplayGroup S && S.Type == DisplayAction.PRESS_A)
                        msg += " | ";
                    else if (Characters[i] is LocalizeGroup)
                        msg += "(M)";
                    else if (Characters[i] is NumberGroup V)
                        msg += "".PadLeft(V.MaxWidth, '0');
                    if (Characters[i] is Character ch)
                        msg += ch.Char;
                }
                return msg;
            }

            public class Character : IMessageObject
            {
                public char Char { get; set; }

                public void Read(Stream FS, Encoding Enc)
                {
                    Char = FS.ReadChar(Enc);
                }
                public byte[] Write(Encoding Enc)
                {
                    return Enc.GetBytes(Char.ToString());
                }
                public override string ToString() => Char.ToString();
            }

            public abstract class FormatGroup : IMessageObject
            {
                public FormatGroup() { }
                public FormatGroup(Stream FS, Encoding Enc) { }

                public abstract byte[] Write(Encoding Enc);
                internal abstract Opcode Opcode { get; }
            }
            public class SystemGroup : FormatGroup
            {
                public System Action { get; set; } = System.COLOUR;
                /// <summary>
                /// Sets the colour of the Text. Needs the Action to be set to <see cref="System.RUBY"/>
                /// </summary>
                public TextColour Colour { get; set; } = TextColour.NA;
                /// <summary>
                /// JAPANESE ONLY
                /// </summary>
                public string RubyTop { get; set; }
                /// <summary>
                /// JAPANESE ONLY
                /// </summary>
                public string RubyBottom { get; set; }

                internal override Opcode Opcode => Opcode.SYSTEM;

                public SystemGroup(TextColour Col)
                {
                    Action = System.COLOUR;
                    Colour = Col;
                }
                public SystemGroup(string rt, string rb)
                {
                    Action = System.RUBY;
                    RubyTop = rt;
                    RubyBottom = rb;
                }
                public SystemGroup(Stream FS, Encoding Enc) : base(FS, Enc)
                {
                    Action = (System)FS.ReadChar(Enc);
                    int SkipLength = FS.ReadChar(Enc);
                    if (Action == System.COLOUR)
                        Colour = (TextColour)FS.ReadChar(Enc);
                    else
                    {
                        ushort RubyTopLength = FS.ReadChar(Enc);
                        ushort RubyBottomLength = FS.ReadChar(Enc);
                        RubyTop = FS.ReadString(RubyTopLength, Enc);
                        RubyBottom = FS.ReadString(RubyBottomLength, Enc);
                    }
                }

                public override byte[] Write(Encoding Enc)
                {
                    List<byte> Data = new List<byte>();
                    Data.AddRange(BitConverter.GetBytes((ushort)Action).Reverse());
                    if (Action == System.COLOUR)
                    {
                        Data.AddRange(BitConverter.GetBytes((ushort)2).Reverse());
                        Data.AddRange(BitConverter.GetBytes((ushort)Colour).Reverse());
                    }
                    else
                    {
                        Data.AddRange(BitConverter.GetBytes((ushort)6).Reverse());
                        Data.AddRange(BitConverter.GetBytes((ushort)RubyTop.Length).Reverse());
                        Data.AddRange(BitConverter.GetBytes((ushort)RubyBottom.Length).Reverse());
                        Data.AddRange(Enc.GetBytes(RubyTop));
                        Data.AddRange(Enc.GetBytes(RubyBottom));
                    }
                    return Data.ToArray();
                }

                public override string ToString() => Action == System.RUBY ? $"{Action.ToString()} -> {RubyBottom}({RubyTop})" : $"{Action.ToString()} -> {Colour.ToString()}";
                public string ToRubyString()
                {
                    if (Action == System.RUBY)
                        return $"{RubyBottom}({RubyTop})";
                    else
                        throw new InvalidOperationException();
                }
            }
            public class DisplayGroup : FormatGroup
            {
                public DisplayAction Type { get; set; }
                private ushort _frames;
                public ushort Frames
                {
                    get
                    {
                        if (Type != DisplayAction.WAIT)
                            return 0xFFFF;
                        return _frames;
                    }
                    set
                    {
                        if (Type != DisplayAction.WAIT)
                            throw new Exception("Cannot assign a Framecount if the Stop Type is PRESS_A");
                        _frames = value;
                    }
                }

                internal override Opcode Opcode => Opcode.DISPLAY;

                public DisplayGroup(Stream FS, Encoding Enc) : base(FS, Enc)
                {
                    Type = (DisplayAction)FS.ReadChar(Enc);
                    if (Type != DisplayAction.WAIT)
                        FS.Position += 0x04;
                    else
                    {
                        FS.Position += 0x02;
                        Frames = FS.ReadChar(Enc);
                    }
                }

                public override byte[] Write(Encoding Enc)
                {
                    List<byte> Data = new List<byte>();
                    Data.AddRange(BitConverter.GetBytes((ushort)Type).Reverse());
                    if (Type != DisplayAction.WAIT)
                    {
                        Data.AddRange(new byte[Enc.GetStride()]);
                        Data.AddRange(Enc.GetBytes(new char[] { (char)0x0A }));
                    }
                    else
                    {
                        Data.AddRange(new byte[Enc.GetStride()]);
                        Data.AddRange(BitConverter.GetBytes(Frames).Reverse());
                    }
                    return Data.ToArray();
                }

                public override string ToString() => Type == DisplayAction.PRESS_A ? "Wait for player to press A" : $"Wait {Frames} frames";
            }
            public class SoundGroup : FormatGroup
            {
                public string SoundID { get; set; }

                internal override Opcode Opcode => Opcode.SOUND;

                public SoundGroup(Stream FS, Encoding Enc) : base(FS, Enc)
                {
                    uint SkipLength = BitConverter.ToUInt32(FS.ReadReverse(0, 4), 0);
                    ushort StringLength = FS.ReadChar(Enc);
                    SoundID = FS.ReadString(StringLength, Enc);
                }

                public override byte[] Write(Encoding Enc)
                {
                    List<byte> Data = new List<byte>();
                    int TotalSize = (SoundID.Length + 1) * Enc.GetStride();
                    Data.AddRange(BitConverter.GetBytes(TotalSize).Reverse());
                    Data.AddRange(BitConverter.GetBytes((ushort)SoundID.Length).Reverse());
                    Data.AddRange(Enc.GetBytes(SoundID));
                    return Data.ToArray();
                }

                public override string ToString() => $"Play Sound: {SoundID}";
            }
            public class PictureGroup : FormatGroup
            {
                public ushort GlyphIndex { get; set; }
                public char CharacterID { get; set; }

                internal override Opcode Opcode => Opcode.PICTURE;

                public PictureGroup(Stream FS, Encoding Enc) : base(FS, Enc)
                {
                    GlyphIndex = FS.ReadChar(Enc);
                    ushort Font = FS.ReadChar(Enc);
                    CharacterID = (char)(FS.ReadChar(Enc)+0x30);
                }

                public override byte[] Write(Encoding Enc)
                {
                    List<byte> Data = new List<byte>();
                    Data.AddRange(BitConverter.GetBytes((ushort)GlyphIndex).Reverse());
                    Data.AddRange(BitConverter.GetBytes((ushort)2).Reverse());
                    Data.AddRange(BitConverter.GetBytes((ushort)(CharacterID-0x30)).Reverse());
                    return Data.ToArray();
                }

                public override string ToString() => $"Picture: {CharacterID}";
            }
            public class FontSizeGroup : FormatGroup
            {
                public TextSize Size { get; set; }

                internal override Opcode Opcode => Opcode.FONTSIZE;

                public FontSizeGroup(Stream FS, Encoding Enc) : base(FS, Enc)
                {
                    Size = (TextSize)FS.ReadChar(Enc);
                    FS.Position += 0x02;
                }

                public override byte[] Write(Encoding Enc)
                {
                    List<byte> Data = new List<byte>();
                    Data.AddRange(BitConverter.GetBytes((ushort)Size).Reverse());
                    Data.AddRange(BitConverter.GetBytes((ushort)0x0000).Reverse());
                    return Data.ToArray();
                }

                public override string ToString() => $"Change size to {Size.ToString()}";
            }
            public class LocalizeGroup : FormatGroup
            {
                internal override Opcode Opcode => Opcode.LOCALIZE;

                public LocalizeGroup(Stream FS, Encoding Enc) : base(FS, Enc) => FS.Position += 0x06;

                public override byte[] Write(Encoding Enc) => new byte[6] { 0x00, 0x00, 0x00, 0x02, 0x00, 0xCD };

                public override string ToString() => "<Character Name>";
            }
            public class NumberGroup : FormatGroup
            {
                public ushort MaxWidth { get; set; }
                public NumberLength Length { get; set; }
                /// <summary>
                /// Should always be 0
                /// </summary>
                int Value { get; set; }

                internal override Opcode Opcode => Opcode.NUMBER;

                public NumberGroup(Stream FS, Encoding Enc) : base(FS, Enc)
                {
                    MaxWidth = FS.ReadChar(Enc);
                    Length = (NumberLength)FS.ReadChar(Enc);
                    byte[] Data = FS.Read(0, (ushort)Length);
                    Value = BitConverter.ToInt32(Data, 0);
                }

                public override byte[] Write(Encoding Enc)
                {
                    List<byte> Data = new List<byte>();
                    Data.AddRange(BitConverter.GetBytes(MaxWidth).Reverse());
                    Data.AddRange(BitConverter.GetBytes((ushort)Length).Reverse());
                    if (Length == NumberLength.ANY)
                        throw new Exception("I don't know how long this one is supposed to be written as");
                    Data.AddRange(new byte[(int)Length]);
                    return Data.ToArray();
                }

                public override string ToString() => $"Number Variable: {Value.ToString()} ";
            }
            public class StringGroup : FormatGroup
            {
                public string Value { get; set; }

                internal override Opcode Opcode => Opcode.STRING;

                public StringGroup(Stream FS, Encoding Enc) : base(FS, Enc)
                {
                    ushort Length = FS.ReadChar(Enc);
                    Value = FS.ReadString(Enc);
                }

                public override byte[] Write(Encoding Enc)
                {
                    List<byte> Data = new List<byte>();
                    Data.AddRange(BitConverter.GetBytes((ushort)Value.Length).Reverse());
                    Data.AddRange(Enc.GetBytes(Value));
                    return Data.ToArray();
                }

                public override string ToString() => $"String Variable: {Value}";
            }
            public class RaceTimeGroup : FormatGroup
            {
                public ushort Type { get; set; }

                internal override Opcode Opcode => Opcode.RACETIME;

                public RaceTimeGroup(Stream FS, Encoding Enc) : base(FS, Enc) => Type = FS.ReadChar(Enc);

                public override byte[] Write(Encoding Enc) => BitConverter.GetBytes((ushort)Type).Reverse().ToArray();

                public override string ToString() => Type == 5 ? "Current Race Time" : $"Best Time on Race {Type}";
            }
            public class FontGroup : FormatGroup
            {
                public string Value { get; set; }

                internal override Opcode Opcode => Opcode.FONT;

                public FontGroup(Stream FS, Encoding Enc) : base(FS, Enc)
                {
                    ushort StringLength = FS.ReadChar(Enc);
                    ushort stringpointer = FS.ReadChar(Enc);
                    Value = FS.ReadString(StringLength, Enc);
                }

                public override byte[] Write(Encoding Enc)
                {
                    List<byte> Data = new List<byte>();
                    Data.AddRange(BitConverter.GetBytes((ushort)4).Reverse());
                    Data.AddRange(BitConverter.GetBytes((ushort)(Value.Length * Enc.GetStride())).Reverse());
                    Data.AddRange(Enc.GetBytes(Value));
                    return Data.ToArray();
                }
            }

            public interface IMessageObject
            {
                byte[] Write(Encoding Enc);
            }
        }

        public enum EncodingByte: byte
        {
            /// <summary>
            /// <see cref="System.Text.Encoding.UTF8"/>
            /// </summary>
            UTF8 = 0x00,
            /// <summary>
            /// <see cref="System.Text.Encoding.Unicode"/>
            /// </summary>
            Unicode = 0x01
        }

        public enum Trigger : ushort
        {
            TALK,
            SHOUT,
            TALK_AUTO,
            TALK_AUTO_GLOBAL
        }

        public enum MessageBoxType : ushort
        {
            BUBBLE = 0,
            UNKNOWN1 = 1,
            UNKNOWN2 = 2,
            SIGNBOARD = 3,
            UNKNOWN4 = 4,
            UNKNOWN5 = 5,
            UNKNOWN6 = 6,
            UNKNOWN7 = 7,
            UNKNOWN8 = 8,
        }

        internal enum Opcode : ushort
        {
            /// <summary>
            /// Colour Changes and Japanese Ruby
            /// </summary>
            SYSTEM   = 0,
            /// <summary>
            /// Pauses in the Text such as frame delays
            /// </summary>
            DISPLAY  = 1,
            /// <summary>
            /// Play a sound
            /// </summary>
            SOUND    = 2,
            /// <summary>
            /// Insert an image from the PictureFont
            /// </summary>
            PICTURE  = 3,
            /// <summary>
            /// Change the Font Size
            /// </summary>
            FONTSIZE = 4,
            /// <summary>
            /// Inserts the Player's name - Mario | Luigi - based on the Language
            /// </summary>
            LOCALIZE = 5,
            /// <summary>
            /// Represents a number variable
            /// </summary>
            NUMBER   = 6,
            /// <summary>
            /// Represents a string variable. Read/write functionality is currently a guess
            /// </summary>
            STRING = 7,
            /// <summary>
            /// Inserts a given races Best Time or the Current Race's Time. Read/write functionality is currently a guess
            /// </summary>
            RACETIME = 9,
            /// <summary>
            /// Uses the NumberFont. Read/write functionality is currently a guess
            /// </summary>
            FONT     = 10
        }

        public enum System
        {
            /// <summary>
            /// JAPANESE ONLY
            /// </summary>
            RUBY = 0,
            COLOUR = 3
        }

        public enum DisplayAction : ushort
        {
            WAIT = 0,
            PRESS_A = 1,
            YCENTER = 2,
            XCENTER = 3
        }

        public enum TextColour : short
        {
            NA    = -2,
            NONE  = -1,
            BLACK =  0,
            RED   =  1,
            GREEN =  2,
            BLUE  =  3,
            YELLOW = 4,
            PURPLE = 5,
            ORANGE = 6,
            GRAY  =  7
        }

        public enum TextSize : ushort
        {
            SMALL = 0,
            NORMAL = 1,
            LARGE = 2
        }

        public enum NumberLength : ushort
        {
            /// <summary>
            /// %d
            /// </summary>
            ANY = 0,
            /// <summary>
            /// %02d
            /// </summary>
            TWO = 5,
            /// <summary>
            /// %03d
            /// </summary>
            THREE = 6,
            /// <summary>
            /// %04d
            /// </summary>
            FOUR = 7,
            /// <summary>
            /// %05d
            /// </summary>
            FIVE = 8,
            /// <summary>
            /// %06d
            /// </summary>
            SIX = 9
        }
    }
}
