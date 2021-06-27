using System;
using System.Collections.Generic;
using System.IO;
using Hack.io.MSBT;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hack.io.MSBF
{
    public class MSBF
    {
        public MSBT.MSBT TextFile;
        public List<Node> Nodes = new List<Node>();
        public List<ushort> NodePairs = new List<ushort>();



        /// <summary>
        /// File Identifier
        /// </summary>
        private readonly string Magic = "MsgFlwBn";
        public MSBF(string file)
        {
            FileStream FS = new FileStream(file, FileMode.Open);
            Read(FS);
            FS.Close();
        }
        public MSBF(Stream MSBFFile) => Read(MSBFFile);


        private void Read(Stream MSBFFile)
        {
            if (!MSBFFile.ReadString(0x8).Equals(Magic))
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

            MSBFFile.Position += 0x18;

            while (MSBFFile.Position < MSBFFile.Length)
            {
                string CurrentSectionMagic = MSBFFile.ReadString(4);

                switch (CurrentSectionMagic)
                {
                    case "FLW2":
                        ReadFLW2(MSBFFile);
                        break;
                    case "FEN1":
                        ReadFEN1(MSBFFile);
                        break;
                    default:
                        throw new Exception($"{CurrentSectionMagic} section found, but this is not supported.");
                }
            }
        }

        private void ReadFLW2(Stream MSBFFile)
        {
            uint SectionSize = BitConverter.ToUInt32(MSBFFile.ReadReverse(0, 4), 0);
            MSBFFile.Position += 0x08; //padding

            ushort NodeCount = BitConverter.ToUInt16(MSBFFile.ReadReverse(0, 2), 0);
            ushort NodePairCount = BitConverter.ToUInt16(MSBFFile.ReadReverse(0, 2), 0);
            MSBFFile.Position += 0x04; //I believe this is technically padding to the 0x08, but in reality, it's always the same address, and thus, the same size

            for (int i = 0; i < NodeCount; i++)
            {
                NodeType type = (NodeType)BitConverter.ToUInt16(MSBFFile.ReadReverse(0, 2), 0);

                switch (type)
                {
                    case NodeType.MESSAGE:
                        Nodes.Add(new MessageNode(MSBFFile));
                        Nodes[Nodes.Count - 1].Parent = this;
                        break;
                    case NodeType.ENTRY:
                        Nodes.Add(new EntryNode(MSBFFile));
                        Nodes[Nodes.Count - 1].Parent = this;
                        break;
                    case NodeType.EVENT:
                        Nodes.Add(new EventNode(MSBFFile));
                        Nodes[Nodes.Count - 1].Parent = this;
                        break;
                    case NodeType.BRANCH:
                        Nodes.Add(new BranchNode(MSBFFile));
                        Nodes[Nodes.Count - 1].Parent = this;
                        break;
                    default:
                        throw new Exception($"Unsupported NodeType: {(int)type}");
                }
            }

            for (int i = 0; i < NodePairCount; i++)
            {
                NodePairs.Add(BitConverter.ToUInt16(MSBFFile.ReadReverse(0, 2), 0));
            }

            while (MSBFFile.Position % 0x10 != 0)
                MSBFFile.Position++;
        }

        public struct TableEntry
        {
            public uint Count;
            public List<TablePair> Pairs;
        }

        public struct TablePair
        {
            public string Label;
            public uint FlowOffset;
        }
        List<TableEntry> mEntries;
        private void ReadFEN1(Stream MSBFFile)
        {
            //mTable = new Dictionary<string, uint>();
            mEntries = new List<TableEntry>();

            int size = BitConverter.ToInt32(MSBFFile.ReadReverse(0, 4), 0);
            MSBFFile.Position += 0x08;
            long loc = MSBFFile.Position;
            uint count = BitConverter.ToUInt32(MSBFFile.ReadReverse(0, 4), 0);

            for (int i = 0; i < count; i++)
            {
                TableEntry e = new TableEntry();
                e.Count = BitConverter.ToUInt32(MSBFFile.ReadReverse(0, 4), 0);

                int offs = BitConverter.ToInt32(MSBFFile.ReadReverse(0, 4), 0);

                e.Pairs = new List<TablePair>();

                long pos = MSBFFile.Position;
                MSBFFile.Position = loc + offs;

                for (int j = 0; j < e.Count; j++)
                {
                    TablePair p = new TablePair();
                    p.Label = MSBFFile.ReadString(MSBFFile.ReadByte());
                    p.FlowOffset = BitConverter.ToUInt32(MSBFFile.ReadReverse(0, 4), 0);

                    e.Pairs.Add(p);
                }

                MSBFFile.Position = pos;

                /*if (e.mIsValid == 1)
                {
                    string str = file.ReadStringAt(loc + e.mPtr);
                    uint val = file.ReadUInt32At(loc + e.mPtr + str.Length + 1);
                    mTable.Add(str, val);
                }*/

                mEntries.Add(e);
            }

            MSBFFile.Position = loc + size;

            while (MSBFFile.Position % 0x10 != 0)
                MSBFFile.Position++;
        }

        public abstract class Node
        {
            public abstract NodeType Type { get; }
            protected ushort[] Data = new ushort[5];
            public MSBF Parent;

            public Node(Stream MSBFFile)
            {
                //Don't read the type byte here
                for (int i = 0; i < 5; i++)
                {
                    Data[i] = BitConverter.ToUInt16(MSBFFile.ReadReverse(0, 2), 0);
                }
            }

            public void Save(Stream MSBFFile)
            {
                MSBFFile.WriteReverse(BitConverter.GetBytes((ushort)Type), 0, 2);
                for (int i = 0; i < 5; i++)
                {
                    MSBFFile.WriteReverse(BitConverter.GetBytes(Data[i]), 0, 2);
                }
            }
        }

        public sealed class MessageNode : Node
        {
            public override NodeType Type => NodeType.MESSAGE;
            public ushort GroupID
            {
                get => Data[1];
                set => Data[1] = value;
            }
            public ushort MessageID
            {
                get => Data[2];
                set => Data[2] = value;
            }
            public MSBT.MSBT.Label Message
            {
                get
                {
                    if (Parent is null || Parent.TextFile is null)
                        return null;

                    return Parent.TextFile.GetSortedLabels[MessageID];
                }
            }
            public ushort NextNodeID
            {
                get => Data[3];
                set => Data[3] = value;
            }
            public Node NextNode
            {
                get
                {
                    if (Parent is null || NextNodeID == 0xFFFF)
                        return null;

                    return Parent.Nodes[NextNodeID];
                }
            }

            public MessageNode(Stream MSBFFile) : base(MSBFFile) { }
        }

        public sealed class BranchNode : Node
        {
            public override NodeType Type => NodeType.BRANCH;
            public ushort Unknown
            {
                get => Data[1];
                set => Data[1] = value;
            }
            public ushort Condition
            {
                get => Data[2];
                set => Data[2] = value;
            }
            public ushort YesNoChoice
            {
                get => Data[3];
                set => Data[3] = value;
            }
            public ushort NodePairID
            {
                get => Data[4];
                set => Data[4] = value;
            }
            public Node SuccessNode
            {
                get
                {
                    if (Parent is null)
                        return null;

                    return Parent.Nodes[Parent.NodePairs[NodePairID]];
                }
            }
            public Node FailureNode
            {
                get
                {
                    if (Parent is null)
                        return null;
                    return Parent.Nodes[Parent.NodePairs[NodePairID+1]];
                }
            }

            public BranchNode(Stream MSBFFile) : base(MSBFFile)
            {
            }
        }

        public sealed class EventNode : Node
        {
            public override NodeType Type => NodeType.EVENT;
            public ushort Event
            {
                get => Data[1];
                set => Data[1] = value;
            }
            public ushort JumpFlowID
            {
                get => Data[2];
                set => Data[2] = value;
            }
            public ushort FlowID
            {
                get => Data[4];
                set => Data[4] = value;
            }

            public EventNode(Stream MSBFFile) : base(MSBFFile) { }
        }

        public sealed class EntryNode : Node
        {
            public override NodeType Type => NodeType.ENTRY;
            public ushort NextNodeID
            {
                get => Data[1];
                set => Data[1] = value;
            }
            public Node NextNode
            {
                get
                {
                    if (Parent is null)
                        return null;

                    return Parent.Nodes[NextNodeID];
                }
            }

            public EntryNode(Stream MSBFFile) : base(MSBFFile) { }
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
    }
}
