using System;
using System.Collections.Generic;
using System.IO;
using Hack.io.MSBT;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hack.io.Util;

namespace Hack.io.MSBF
{
    public class MSBF
    {
        const string Magic = "MsgFlwBn";
        /// <summary>
        /// Filename of this MSBF file.
        /// </summary>
        public string FileName { get; set; } = null;

        public MSBT.MSBT TextFile;
        public List<Node> Nodes = new List<Node>();
        /// <summary>
        /// Each condition node will index into this list to find their jumps
        /// nodes index in pairs of 2, so 0x00, 0x02, 0x04, etc.
        /// </summary>
        public List<ushort> ConditionJumpNodes = new List<ushort>();
        
        /// <summary>
        /// File Identifier
        /// </summary>
        public MSBF(string file)
        {
            FileStream MSBFFile = new FileStream(file, FileMode.Open);
            Read(MSBFFile);
            MSBFFile.Close();
            FileName = file;
        }
        public MSBF(Stream MSBFFile) => Read(MSBFFile);

        public void Save(string file)
        {
            FileStream MSBFFile = new FileStream(file, FileMode.Create);
            Write(MSBFFile);
            MSBFFile.Close();
            FileName = file;
        }
        public MemoryStream Save()
        {
            MemoryStream ms = new MemoryStream();
            Write(ms);
            return ms;
        }

        private void Read(Stream MSBFFile)
        {
            if (!MSBFFile.ReadString(0x8).Equals(Magic))
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

            MSBFFile.Position += 0x18;

            List<(string MSBTTag, uint NodeIndex)> FlowMSBTHookNames = new List<(string MSBTTag, uint NodeIndex)>();
            while (MSBFFile.Position < MSBFFile.Length)
            {
                string CurrentSectionMagic = MSBFFile.ReadString(4);

                switch (CurrentSectionMagic)
                {
                    case "FLW2":
                        ReadFLW2(MSBFFile);
                        break;
                    case "FEN1":
                        ReadFEN1(MSBFFile, ref FlowMSBTHookNames);
                        break;
                    default:
                        throw new Exception($"{CurrentSectionMagic} section found, but this is not supported.");
                }
            }

            for (int i = 0; i < FlowMSBTHookNames.Count; i++)
            {
                if (Nodes[(int)FlowMSBTHookNames[i].NodeIndex] is EntryNode EN)
                {
                    EN.MSBTEntryTag = FlowMSBTHookNames[i].MSBTTag;
                }
            }
        }

        private void Write(Stream MSBFFile)
        {
            MSBFFile.WriteString(Magic);
            MSBFFile.Write(new byte[2] { 0xFE, 0xFF }, 0, 2);
            MSBFFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x03 }, 0, 4);
            MSBFFile.Write(new byte[2] { 000, 0x02 }, 0, 2); //2 sections
            MSBFFile.Write(new byte[2], 0, 2); //Padding
            MSBFFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //Size placeholder
            MSBFFile.Write(new byte[10], 0, 10); //More padding

            //FLW2 section
            long FLW2Start = MSBFFile.Position;
            MSBFFile.WriteString("FLW2");
            MSBFFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //Size placeholder
            MSBFFile.Write(new byte[8], 0, 8); //padding
            MSBFFile.WriteReverse(BitConverter.GetBytes((ushort)Nodes.Count), 0, 2);
            MSBFFile.WriteReverse(BitConverter.GetBytes((ushort)ConditionJumpNodes.Count), 0, 2); //usually (ConditionNodeCount * 2) * 4
            MSBFFile.Write(new byte[4], 0, 4); //padding to the 8th most likely

            //TODO: Make everything reference based
            for (int i = 0; i < Nodes.Count; i++)
            {
                Nodes[i].Write(MSBFFile);
            }
            for (int i = 0; i < ConditionJumpNodes.Count; i++)
            {
                MSBFFile.WriteReverse(BitConverter.GetBytes(ConditionJumpNodes[i]), 0, 2);
            }
            long FLW2End = MSBFFile.Position;
            MSBFFile.PadTo(16, 0xAB);

            //Fen1 time
            //Flow Entry section
            long FEN1Start = MSBFFile.Position;
            MSBFFile.WriteString("FEN1");
            MSBFFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //Size placeholder
            MSBFFile.Write(new byte[8], 0, 8); //padding

            //Always 59 for some reason
            uint GroupCount = 59;
            MSBFFile.WriteReverse(BitConverter.GetBytes(GroupCount), 0, 4);

            //Lets collect all the unique entries
            //No two initilizer nodes can have the same MSBTTag
            List<(uint Hash, uint Offset)> Groups = new List<(uint, uint)>();
            List<byte> WrittenLabels = new List<byte>();
            MakeGroups(GroupCount, ref Groups, ref WrittenLabels, out uint FinalOffset);

            //Groups = Groups.OrderBy(A => A.Hash).ToList();

            int ActualDataCount = 0;
            for (int i = 0; i < Groups.Count; i++)
            {
                var HashCount = Groups.Where(a => a.Hash.Equals(Groups[i].Hash)).Count();
                uint hashdiff = Groups[i].Hash;
                if (i == 0)
                    goto FirstTimeSkip;

                hashdiff = Groups[i].Hash - Groups[i - 1].Hash;
                if ((hashdiff == 0) && ((Groups.Count - 1) != i) && (Groups[i + 1].Hash - Groups[i].Hash == 0))
                    continue;

                FirstTimeSkip:
                int LoopCountChange = 0;
                int BranchDataChenge = -1;
                if (i == 0)
                {
                    LoopCountChange = 1;
                    BranchDataChenge = 0;
                }
                for (int j = 0; j < (hashdiff + LoopCountChange); j++)
                {
                    MSBFFile.WriteReverse((j == hashdiff + BranchDataChenge) ? BitConverter.GetBytes(HashCount) : new byte[4], 0, 4);
                    MSBFFile.WriteReverse(BitConverter.GetBytes(Groups[i].Offset), 0, 4);
                    ActualDataCount++;
                }
            }
            //Fill out the rest of the empty data
            if (ActualDataCount < GroupCount)
                for (int i = 0; i < GroupCount - ActualDataCount; i++)
                {
                    MSBFFile.WriteReverse(new byte[4], 0, 4);
                    MSBFFile.WriteReverse(BitConverter.GetBytes(FinalOffset), 0, 4);
                }

            MSBFFile.Write(WrittenLabels.ToArray(), 0, WrittenLabels.Count);
            long FEN1End = MSBFFile.Position;
            MSBFFile.PadTo(16, 0xAB);

            //DON'T FORGET TO WRITE THE SECTION SIZES!!!
            MSBFFile.Position = 0x12;
            MSBFFile.WriteReverse(BitConverter.GetBytes((int)MSBFFile.Length), 0, 4);
            MSBFFile.Position = FLW2Start + 0x04;
            MSBFFile.WriteReverse(BitConverter.GetBytes((int)(FLW2End - (FLW2Start + 0x10))), 0, 4);
            MSBFFile.Position = FEN1Start + 0x04;
            MSBFFile.WriteReverse(BitConverter.GetBytes((int)(FEN1End - (FEN1Start + 0x10))), 0, 4);
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
                ConditionJumpNodes.Add(BitConverter.ToUInt16(MSBFFile.ReadReverse(0, 2), 0));
            }

            while (MSBFFile.Position % 0x10 != 0)
                MSBFFile.Position++;
        }

        /// <summary>
        /// FEN1 = Flow Entry (most likely0
        /// </summary>
        /// <param name="MSBFFile"></param>
        private void ReadFEN1(Stream MSBFFile, ref List<(string MSBTTag, uint NodeIndex)> mEntries)
        {
            List<(uint Hash, uint OfMSBFFileet)> Groups = new List<(uint Hash, uint OfMSBFFileet)>();
            int size = BitConverter.ToInt32(MSBFFile.ReadReverse(0, 4), 0);
            MSBFFile.Position += 0x08;
            long LabelStart = MSBFFile.Position;
            uint GroupCount = BitConverter.ToUInt32(MSBFFile.ReadReverse(0, 4), 0);
            for (int y = 0; y < GroupCount; y++)
            {
                Groups.Add((BitConverter.ToUInt32(MSBFFile.ReadReverse(0, 4), 0), BitConverter.ToUInt32(MSBFFile.ReadReverse(0, 4), 0)));
            }

            foreach ((uint, uint) grp in Groups)
            {
                MSBFFile.Position = LabelStart + grp.Item2;
                Console.WriteLine("Group ID: " + (uint)Groups.IndexOf(grp));
                for (int z = 0; z < grp.Item1; z++)
                {
                    uint length = Convert.ToUInt32(MSBFFile.ReadByte());
                    string Name = MSBFFile.ReadString((int)length);
                    uint Index = BitConverter.ToUInt32(MSBFFile.ReadReverse(0, 4), 0);
                    uint Checksum = (uint)Groups.IndexOf(grp);

                    mEntries.Add((Name, Index));

                    Console.WriteLine("Label: " + Name);
                }
                Console.WriteLine();
            }

            MSBFFile.Position = LabelStart + size;

            while (MSBFFile.Position % 0x10 != 0)
                MSBFFile.Position++;
        }

        private void MakeGroups(uint GroupCount, ref List<(uint, uint)> Groups, ref List<byte> LabelData, out uint FinalOffset)
        {
            uint Offset = 4 + (8 * GroupCount);

            List<(uint Hash, EntryNode Node)> SortingGroups = new List<(uint Hash, EntryNode Node)>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i] is EntryNode EN)
                {
                    SortingGroups.Add((LabelChecksum(EN.MSBTEntryTag, GroupCount), EN));
                }
            }
            SortingGroups = SortingGroups.OrderBy(EN => EN.Hash).ToList();
            for (int i = 0; i < SortingGroups.Count; i++)
            {
                Groups.Add((SortingGroups[i].Hash, Offset));
                Offset += (uint)(SortingGroups[i].Node.MSBTEntryTag.Length + 1 + 4);
                LabelData.Add((byte)SortingGroups[i].Node.MSBTEntryTag.Length);
                LabelData.AddRange(Encoding.UTF8.GetBytes(SortingGroups[i].Node.MSBTEntryTag));
                LabelData.AddRange(BitConverter.GetBytes(Nodes.IndexOf(SortingGroups[i].Node)).Reverse());
            }
            FinalOffset = Offset;
        }
        /// <summary>
        /// Same as MSBT
        /// </summary>
        /// <param name="label"></param>
        /// <param name="GroupCount"></param>
        /// <returns></returns>
        private uint LabelChecksum(string label, uint GroupCount)
        {
            uint hash = 0;
            foreach (char c in label)
            {
                hash *= 0x492;
                hash += c;
            }
            return (hash & 0xFFFFFFFF) % GroupCount;
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

            public void Write(Stream MSBFFile)
            {
                MSBFFile.WriteReverse(BitConverter.GetBytes((ushort)Type), 0, 2);
                for (int i = 0; i < 5; i++)
                {
                    MSBFFile.WriteReverse(BitConverter.GetBytes(Data[i]), 0, 2);
                }
            }

            public override string ToString() => "Invalid";
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

            public override string ToString() => $"Message Node: {MessageID} ({GroupID})";
        }

        public sealed class BranchNode : Node
        {
            public override NodeType Type => NodeType.BRANCH;
            /// <summary>
            /// Likely Padding, or possibly the amount of condition jump nodes to use
            /// </summary>
            public ushort Unknown
            {
                get => Data[1];
                set => Data[1] = value;
            }
            /// <summary>
            /// The condition type
            /// </summary>
            public ushort Condition
            {
                get => Data[2];
                set => Data[2] = value;
            }
            /// <summary>
            /// Depends on what the condition is
            /// </summary>
            public ushort ConditionParameter
            {
                get => Data[3];
                set => Data[3] = value;
            }
            /// <summary>
            /// True is always the number pointed to by this number. False is always this+1
            /// </summary>
            public ushort JumpNodeIndex
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

                    return Parent.Nodes[Parent.ConditionJumpNodes[JumpNodeIndex]];
                }
            }
            public Node FailureNode
            {
                get
                {
                    if (Parent is null)
                        return null;
                    return Parent.Nodes[Parent.ConditionJumpNodes[JumpNodeIndex+1]];
                }
            }

            public BranchNode(Stream MSBFFile) : base(MSBFFile)
            {
            }

            public override string ToString() => $"Branch Node: {Condition}({ConditionParameter})";
        }

        public sealed class EventNode : Node
        {
            public override NodeType Type => NodeType.EVENT;
            /// <summary>
            /// The Event ID.
            /// </summary>
            public ushort Event
            {
                get => Data[1];
                set => Data[1] = value;
            }
            public ushort NextNodeID
            {
                get => Data[2];
                set => Data[2] = value;
            }
            public ushort EventParameter
            {
                get => Data[4];
                set => Data[4] = value;
            }

            public EventNode(Stream MSBFFile) : base(MSBFFile) { }

            public override string ToString()
            {
                return $"Event Node: {Event}({EventParameter})";
            }
        }

        public sealed class EntryNode : Node
        {
            public string MSBTEntryTag { get; set; }
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

            public override string ToString() => $"Flow Entry: {MSBTEntryTag} -> {NextNodeID}";
        }

        //=====================================================================

        /// <summary>
        /// Cast a MSBF to a ArchiveFile
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator ArchiveFile(MSBF x) => new ArchiveFile(x.FileName, x.Save());

        /// <summary>
        /// Cast a ArchiveFile to a MSBF
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator MSBF(ArchiveFile x) => new MSBF((MemoryStream)x) { FileName = x.Name };

        //=====================================================================

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
