using Hack.io.Interface;
using Hack.io.Utility;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hack.io.MSBF;

public class MSBF : ILoadSaveFile
{
    public const int LABEL_MAX_LENGTH = 255;
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const string MAGIC = "MsgFlwBn";
    public const string MAGIC_FLW2 = "FLW2";
    public const string MAGIC_FEN1 = "FEN1";
    public const string MAGIC_REF1 = "REF1";

    [DisallowNull]
    public List<EntryNode> Flows = [];


    public void Load(Stream Strm)
    {
        long FileStart = Strm.Position;
        FileUtil.ExceptionOnBadMagic(Strm, MAGIC);
        FileUtil.ExceptionOnMisMatchedBOM(Strm);
        Strm.Position += 0x03;
        if (Strm.ReadUInt8() != 0x03)
            throw new NotImplementedException("MSBF versions other than 3 are currently not supported");

        ushort SectionCount = Strm.ReadUInt16();
        Strm.Position += 0x02;
        uint FileSize = Strm.ReadUInt32();
        Strm.Position += 0x0A;

        Dictionary<int, string> TemporaryLabelStorage = [];
        List<NodeBase> TemporaryNodes = [];
        List<ushort> TemporaryBranchIndicies = [];

        for (int i = 0; i < SectionCount; i++)
        {
            long ChunkStart = Strm.Position;
            string Header = Strm.ReadString(4, Encoding.ASCII);
            uint ChunkSize = Strm.ReadUInt32();
            Strm.Position += 0x08;

            if (Header.Equals(MAGIC_FLW2))
                ReadFLW2();
            if (Header.Equals(MAGIC_FEN1))
                ReadFEN1();
            if (Header.Equals(MAGIC_REF1))
                ReadREF1();

            Strm.Position = ChunkStart + 0x10 + ChunkSize;
            if (ChunkSize % 16 > 0)
                Strm.Position += (16 - (ChunkSize % 16));
        }

        for (int i = 0; i < TemporaryNodes.Count; i++)
        {
            NodeBase Current = TemporaryNodes[i];

            if (Current is EntryNode Entry)
            {
                if (TemporaryLabelStorage.TryGetValue(i, out string? Label))
                    Entry.Label = Label;
                else
                    throw new KeyNotFoundException($"Failed to find a Label for node {i}");

                Flows.Add(Entry);
                Entry.NextNode = GetNodeAtIndex(Entry.Argument1);
                continue;
            }

            if (Current is MessageNode Message)
            {
                Message.NextNode = GetNodeAtIndex(Message.Argument3);
                continue;
            }

            if (Current is BranchNode Branch)
            {
                Branch.NextNode = GetNodeAtIndex(TemporaryBranchIndicies[Branch.Argument4]);
                Branch.NextNodeElse = GetNodeAtIndex(TemporaryBranchIndicies[Branch.Argument4+1]);
                continue;
            }

            if (Current is EventNode Event)
            {
                Event.NextNode = GetNodeAtIndex(Event.Argument2);
                continue;
            }
        }

        Strm.Position = FileStart + FileSize;

        NodeBase? GetNodeAtIndex(ushort index)
        {
            if (index == 0xFFFF)
                return null;
            return TemporaryNodes[index];
        }

        void ReadFLW2()
        {
            long ChunkStart = Strm.Position;
            ushort NodeCount = Strm.ReadUInt16();
            ushort IndexCount = Strm.ReadUInt16();
            Strm.Position += 0x04;

            for (int i = 0; i < NodeCount; i++)
            {
                NodeType Type = Strm.ReadEnum<NodeType, ushort>(StreamUtil.ReadUInt16);
                NodeBase Current = Type switch
                {
                    NodeType.MESSAGE => new MessageNode(),
                    NodeType.BRANCH => new BranchNode(),
                    NodeType.EVENT => new EventNode(),
                    NodeType.ENTRY => new EntryNode(),
                    _ => throw new InvalidOperationException($"Invalid node {Type}"),
                };
                Current.Load(Strm);
                TemporaryNodes.Add(Current);
            }

            TemporaryBranchIndicies.AddRange(Strm.ReadMultiUInt16(IndexCount));
        }

        void ReadFEN1()
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

        void ReadREF1()
        {
            long ChunkStart = Strm.Position;
            throw new NotImplementedException("Send the file with REF1 to SuperHackio on Github");
        }
    }

    public void Save(Stream Strm)
    {
        long FileStart = Strm.Position;
        List<NodeBase> TemporaryNodes = [];
        List<ushort> TemporaryBranchIndicies = [];

        Strm.WriteString(MAGIC, Encoding.ASCII, null);
        Strm.WriteUInt16(0xFEFF);
        Strm.Write(CollectionUtil.InitilizeArray<byte>(0, 3));
        Strm.WriteUInt8(3); //Version
        Strm.WritePlaceholder(2); //Section Count
        Strm.WriteUInt16(0);
        Strm.WritePlaceholder(4); //Filesize
        Strm.Write(CollectionUtil.InitilizeArray<byte>(0, 0x0A));

        WriteFLW2();
        WriteFEN1();
        long FileEnd = Strm.Position;

        Strm.Position = FileStart + 0x0E;
        Strm.WriteUInt16(2);
        Strm.Position += 0x02;
        Strm.WriteUInt32((uint)(FileEnd - FileStart));

        void WriteFLW2()
        {
            GetFlattenedNodes(ref TemporaryNodes);

            long ChunkStart = Strm.Position;
            Strm.WriteString(MAGIC_FLW2, Encoding.ASCII, null);
            Strm.WritePlaceholder(4); //Size
            Strm.Write(CollectionUtil.InitilizeArray<byte>(0, 0x08));
            Strm.WriteUInt16((ushort)TemporaryNodes.Count);
            long IndexPosition = Strm.Position;
            Strm.WritePlaceholder(2); //Index count
            Strm.Write(CollectionUtil.InitilizeArray<byte>(0, 0x04));

            for (int i = 0; i < TemporaryNodes.Count; i++)
            {
                NodeBase Current = TemporaryNodes[i];

                if (Current is EntryNode Entry)
                {
                    Entry.Argument1 = GetNodeIndex(Entry.NextNode);
                }

                if (Current is MessageNode Message)
                {
                    Message.Argument3 = GetNodeIndex(Message.NextNode);
                }

                if (Current is BranchNode Branch)
                {
                    Branch.Argument4 = (ushort)TemporaryBranchIndicies.Count;
                    TemporaryBranchIndicies.Add(GetNodeIndex(Branch.NextNode));
                    TemporaryBranchIndicies.Add(GetNodeIndex(Branch.NextNodeElse));
                }

                if (Current is EventNode Event)
                {
                    Event.Argument2 = GetNodeIndex(Event.NextNode);
                }

                Strm.WriteEnum<NodeType, ushort>(Current.Type, StreamUtil.WriteUInt16);
                Current.Save(Strm);
            }

            for (int i = 0; i < TemporaryBranchIndicies.Count; i++)
                Strm.WriteUInt16(TemporaryBranchIndicies[i]);

            long PausePosition = Strm.Position;
            Strm.Position = ChunkStart + 0x04;
            Strm.WriteUInt32((uint)(PausePosition - ChunkStart - 0x10));
            Strm.Position = IndexPosition;
            Strm.WriteUInt16((ushort)TemporaryBranchIndicies.Count);

            Strm.Position = PausePosition;
            Strm.PadTo(16, 0xAB);

            ushort GetNodeIndex(NodeBase node)
            {
                if (node is null)
                    return 0xFFFF;
                return (ushort)TemporaryNodes.IndexOf(node);
            }
        }

        void WriteFEN1()
        {
            ushort BucketCount = 59; //Fixed bucket size in SMG2

            List<(int index, string Label)> TemporaryLabels = [];
            for (int i = 0; i < Flows.Count; i++)
                TemporaryLabels.Add((TemporaryNodes.IndexOf(Flows[i]), Flows[i].Label));

            List<List<(byte[] Label, int Id)>> Buckets = [];
            for (int i = 0; i < BucketCount; i++)
                Buckets.Add([]);

            long ChunkStart = Strm.Position;
            Strm.WriteString(MAGIC_FEN1, Encoding.ASCII, null);
            Strm.WritePlaceholder(4);
            Strm.Write(CollectionUtil.InitilizeArray<byte>(0, 0x08));

            Strm.WriteUInt32(BucketCount);
            for (int i = 0; i < TemporaryLabels.Count; i++)
            {
                byte[] x = Encoding.ASCII.GetBytes(TemporaryLabels[i].Label);
                //List<byte> xx = new();
                //xx.AddRange(x);
                //xx.Add(0);
                //x = xx.ToArray();
                int Index = CalcBucket(x);
                Buckets[Index].Add((x, TemporaryLabels[i].index));
            }


            uint LabelOffset = (uint)(4 + (BucketCount * 8));

            foreach (List<(byte[] Label, int Id)> Bucket in Buckets)
            {
                Strm.WriteUInt32((uint)Bucket.Count);
                Strm.WriteUInt32(LabelOffset);
                long PausePosition = Strm.Position;

                Strm.Position = (ChunkStart + 0x10) + LabelOffset;
                for (int i = 0; i < Bucket.Count; i++)
                {
                    Strm.WriteUInt8((byte)Bucket[i].Label.Length);
                    Strm.Write(Bucket[i].Label);
                    Strm.WriteUInt32((uint)Bucket[i].Id);
                }

                LabelOffset = (uint)(Strm.Position - (ChunkStart + 0x10));
                Strm.Position = PausePosition;
            }

            Strm.Seek(0, SeekOrigin.End);
            long PausePositionJr = Strm.Position;
            Strm.Position = ChunkStart + 0x04;
            Strm.WriteUInt32((uint)(PausePositionJr - ChunkStart));

            Strm.Position = PausePositionJr;
            Strm.PadTo(16, 0xAB);

            int CalcBucket(byte[] encoded)
            {
                long hsh = 0;
                foreach (byte b in encoded)
                    hsh = (hsh * 0x492 + b) & 0xFFFFFFFF;
                return (int)(hsh % BucketCount);
            }
        }
    }

    public void GetFlattenedNodes(ref List<NodeBase> TemporaryNodes)
    {
        foreach (EntryNode item in Flows)
            FlattenNode(item, ref TemporaryNodes);
    }

    private static void FlattenNode(NodeBase Node, ref List<NodeBase> TemporaryNodes)
    {
        if (!TemporaryNodes.Contains(Node))
            TemporaryNodes.Add(Node);

        if (Node.NextNode is not null && !TemporaryNodes.Contains(Node.NextNode))
            FlattenNode(Node.NextNode, ref TemporaryNodes);

        if (Node is BranchNode BN && BN.NextNodeElse is not null && !TemporaryNodes.Contains(BN.NextNodeElse))
            FlattenNode(BN.NextNodeElse, ref TemporaryNodes);
    }

    public abstract class NodeBase : ILoadSaveFile
    {
        public const int ARGUMENT_COUNT = 5;
        public abstract NodeType Type { get; }
        [AllowNull]
        public NodeBase NextNode { get; set; }
        protected ushort[] Data = new ushort[ARGUMENT_COUNT];

        public ushort Argument0
        {
            get => Data[0];
            set => Data[0] = value;
        }
        public ushort Argument1
        {
            get => Data[1];
            set => Data[1] = value;
        }
        public ushort Argument2
        {
            get => Data[2];
            set => Data[2] = value;
        }
        public ushort Argument3
        {
            get => Data[3];
            set => Data[3] = value;
        }
        public ushort Argument4
        {
            get => Data[4];
            set => Data[4] = value;
        }

        public void Load(Stream Strm) => Data = Strm.ReadMultiUInt16(ARGUMENT_COUNT);
        public void Save(Stream Strm) => Strm.WriteMultiUInt16(Data);
    }

    public sealed class MessageNode : NodeBase
    {
        public override NodeType Type => NodeType.MESSAGE;

        public ushort MessageIndex
        {
            get => Argument2;
            set => Argument2 = value;
        }

        public MessageNode() : base()
        {
            Argument1 = 0x88;
            Argument2 = 0xFFFF;
            Argument3 = 0xFFFF;
        }
    }

    public sealed class BranchNode : NodeBase
    {
        public override NodeType Type => NodeType.BRANCH;
        [AllowNull]
        public NodeBase NextNodeElse { get; set; }

        public Conditions BranchCondition
        {
            get => (Conditions)Argument2;
            set => Argument2 = (ushort)value;
        }

        public ushort Parameter
        {
            get => Argument3;
            set => Argument3 = value;
        }

        public BranchNode()
        {
            Argument1 = 2;
        }
    }

    public sealed class EventNode : NodeBase
    {
        public override NodeType Type => NodeType.EVENT;

        public Events EventType
        {
            get => (Events)Argument1;
            set => Argument1 = (ushort)value;
        }

        public ushort Parameter
        {
            get => Argument4;
            set => Argument4 = value;
        }

        public EventNode()
        {
            Argument2 = 0xFFFF;
        }
    }

    public sealed class EntryNode : NodeBase
    {
        public override NodeType Type => NodeType.ENTRY;

        private string mLabel = "";
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

        public EntryNode() : base()
        {
            Argument1 = 0xFFFF;
        }
    }


    /// <summary>
    /// An Enum for all the node types.
    /// </summary>
    public enum NodeType : ushort
    {
        MESSAGE = 1,
        BRANCH = 2,
        EVENT = 3,
        ENTRY = 4
    }


    public enum Conditions : ushort
    {
        YesNoResult = 0,
        BranchFunc = 1,
        PlayerDistance = 2,
        SW_A = 3,
        SW_B = 4,
        PlayerMode_Normal = 5,
        PlayerMode_Bee = 6,
        PlayerMode_Boo = 7,
        PowerStarAppeared = 8,
        IsLuigi = 9,
        IsInDemo = 10,
        MessageAlreadyReadFlag = 11,
        _120StarEnding = 12,
        UNKNOWN_0x0D = 13,
        PlayerMode_Yoshi = 14,
        PlayerMode_Cloud = 15,
        PlayerMode_Rock = 16
    }

    public static bool IsUseParameter(Conditions condition) => condition switch
    {
        Conditions.YesNoResult or Conditions.BranchFunc => true,
        _ => false,
    };

    public enum Events
    {
        EventFunc,
        EventFunc_,
        ChainToNextNode,
        ForwardFlow,
        AnimeFunc,
        ON_SW_A,
        ON_SW_B,
        KillFunc,
        OFF_SW_A,
        OFF_SW_B,
        HideBubblePointer,
        ShowBubblePointer,
    }

    public static bool IsUseParameter(Events Event) => Event switch
    {
        Events.EventFunc or Events.EventFunc_ or Events.AnimeFunc or Events.KillFunc => true,
        _ => false,
    };
}