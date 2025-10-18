using Hack.io.Class;
using Hack.io.Utility;
using System.Text;

namespace Hack.io.U8;

/// <summary>
/// NW4R Archive.<para/>Not to be confused with RARC Archives
/// </summary>
public class U8 : Archive
{
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const uint MAGIC = 0x55AA382D;

    /// <summary>
    /// Create an empty U8 archive
    /// </summary>
    public U8()
    {
    }

    /// <inheritdoc/>
    protected override void Read(Stream Strm)
    {
        FileUtil.ExceptionOnBadMagic(Strm, MAGIC);

        uint OffsetToNodeSection = Strm.ReadUInt32(); //usually 0x20
        _ = Strm.ReadUInt32();
        _ = Strm.ReadUInt32();
        Strm.Position += 0x10; //Skip reserved bytes. All are 0xCC

        //Node time
        //Each node is 0x0C bytes each
        //The first node is node 0

        //Node format:
        //0x00 = byte = IsDirectory
        //0x01 = Int24..... oh no...
        //0x04 = File: Offset to data start | Directory: Index of Parent Directory
        //0x08 = File: Size of the File | Directory: Index of the directory's first node?

        //Root has total number of nodes
        Strm.Position = OffsetToNodeSection;
        U8Node RootNode = new(Strm);

        //Root has total number of nodes 
        int TotalNodeCount = RootNode.Size;
        long StringTableLocation = OffsetToNodeSection + (TotalNodeCount * 0x0C);

        //Read all our entries
        List<U8Node> entries =
        [
            RootNode
        ];
        List<object> FlatItems = [];
        for (int i = 0; i < TotalNodeCount; i++)
        {
            var node = new U8Node(Strm);
            entries.Add(node);
            long PausePosition = Strm.Position;
            if (entries[i].IsDirectory)
            {
                ArchiveDirectory dir = new();
                Strm.Position = StringTableLocation + entries[i].NameOffset;
                dir.Name = Strm.ReadStringJIS();
                FlatItems.Add(dir);
                dir.OwnerArchive = this;
            }
            else
            {
                ArchiveFile file = new();
                Strm.Position = StringTableLocation + entries[i].NameOffset;
                file.Name = Strm.ReadStringJIS();
                Strm.Position = entries[i].DataOffset;
                file.FileData = new byte[entries[i].Size];
                Strm.Read(file.FileData);
                FlatItems.Add(file);
            }
            Strm.Position = PausePosition;
        }
        entries.RemoveAt(entries.Count - 1);
        Stack<ArchiveDirectory> DirectoryStack = new();
        DirectoryStack.Push((ArchiveDirectory)FlatItems[0]);
        for (int i = 1; i < entries.Count; i++)
        {
            if (entries[i].IsDirectory)
            {
                int parent = entries[i].DataOffset;
                _ = entries[i].Size;

                if (FlatItems[parent] is ArchiveDirectory dir)
                {
                    ArchiveDirectory curdir = (ArchiveDirectory)FlatItems[i];
                    dir.Items.Add(curdir.Name, curdir);
                    curdir.Parent = dir;
                }
                DirectoryStack.Push((ArchiveDirectory)FlatItems[i]);
            }
            else
            {
                DirectoryStack.Peek().Items.Add(((dynamic)FlatItems[i]).Name, FlatItems[i]);
            }
            if (i == entries[FlatItems.IndexOf(DirectoryStack.Peek())].Size - 1)
            {
                DirectoryStack.Pop();
            }
        }
        Root = (ArchiveDirectory)FlatItems[0];
    }
    /// <inheritdoc/>
    protected override void Write(Stream Strm)
    {
        if (Root is null)
            throw new NullReferenceException(NULL_ROOT_EXCEPTION);

        List<dynamic> FlatItems = [];

        AddItems(Root);
        //The archive has been flattened hooray
        Dictionary<string, uint> StringOffsets = [];
        List<byte> StringBytes = GetStringTableBytes(FlatItems, ref StringOffsets);

        uint DataOffset = (uint)(0x20 + (FlatItems.Count * 0x0C) + StringBytes.Count);
        DataOffset += 0x20 - (DataOffset % 0x20);
        //while (DataOffset % 16 != 0)
        //    DataOffset++;
        Dictionary<ArchiveFile, uint> DataOffsets = [];
        List<byte> DataBytes = GetDataBytes(FlatItems, DataOffset, ref DataOffsets);

        List<U8Node> Nodes = [];
        Stack<ArchiveDirectory> DirectoryStack = new();
        for (int i = 0; i < FlatItems.Count; i++)
        {
            U8Node newnode = new() { NameOffset = StringOffsets[FlatItems[i].Name] };
            if (FlatItems[i] is ArchiveDirectory dir)
            {
                if (DirectoryStack.Count > 1)
                    while (!ReferenceEquals(DirectoryStack.Peek(), dir.Parent))
                    {
                        Nodes[FlatItems.IndexOf(DirectoryStack.Peek())].Size = i;
                        DirectoryStack.Pop();
                    }
                newnode.IsDirectory = true;
                if (i != 0) //Node is not the Root
                {
                    if (dir.Parent is null)
                        throw new NullReferenceException($"Directory \"{dir.Name}\" does not have a parent.");
                    newnode.DataOffset = FlatItems.IndexOf(dir.Parent);
                }
                else
                {
                    newnode.Size = FlatItems.Count;
                }
                DirectoryStack.Push(dir);
            }
            else
            {
                newnode.DataOffset = (int)DataOffsets[(ArchiveFile)FlatItems[i]];
                newnode.Size = ((ArchiveFile)FlatItems[i]).FileData?.Length ?? throw new FileNotFoundException($"File \"{FlatItems[i].Name}\" has no data");
            }
            Nodes.Add(newnode);
            if (DirectoryStack.Peek().Items.Count == 0 || object.ReferenceEquals(FlatItems[i], DirectoryStack.Peek().Items.Last().Value))
            {
                int index = FlatItems.IndexOf(DirectoryStack.Peek());
                Nodes[index].Size = i + 1;
                DirectoryStack.Pop();
            }
        }

        while (DirectoryStack.Count > 0)
        {
            Nodes[FlatItems.IndexOf(DirectoryStack.Peek())].Size = Nodes.Count;
            DirectoryStack.Pop();
        }

        //Write the Header
        Strm.WriteUInt32(MAGIC);
        Strm.WriteInt32(0x20);
        Strm.WriteInt32(Nodes.Count * 0x0C + StringBytes.Count);
        Strm.WriteUInt32(DataOffset);
        Strm.Write(CollectionUtil.InitilizeArray<byte>(0xCC, 16), 0, 16);

        //Write the Nodes
        for (int i = 0; i < Nodes.Count; i++)
            Nodes[i].Write(Strm);

        //Write the strings
        Strm.Write([.. StringBytes], 0, StringBytes.Count);
        Strm.PadTo(0x20, 0);

        //Write the File Data
        Strm.Write([.. DataBytes], 0, DataBytes.Count);

        void AddItems(ArchiveDirectory dir)
        {
            FlatItems.Add(dir);
            List<ArchiveDirectory> subdirs = [];
            foreach (var item in dir.Items)
            {
                if (item.Value is ArchiveDirectory d)
                    subdirs.Add(d);
                else
                    FlatItems.Add(item.Value);
            }
            for (int i = 0; i < subdirs.Count; i++)
                AddItems(subdirs[i]);
        }
    }

    #region Internals
    /// <summary>
    /// Only used when Reading / Writing
    /// </summary>
    internal class U8Node
    {
        public bool IsDirectory;
        public uint NameOffset;
        public int DataOffset;
        public int Size;

        public U8Node() { }

        public U8Node(Stream Strm)
        {
            uint firstFour = Strm.ReadUInt32();
            IsDirectory = (firstFour & 0xFF000000) > 0;
            NameOffset = firstFour & 0x00FFFFFF;
            DataOffset = Strm.ReadInt32();
            Size = Strm.ReadInt32();
        }

        public void Write(Stream Strm)
        {
            uint f = (uint)(IsDirectory ? 0x01000000 : 0x00000000);
            uint firstfour = (f | (NameOffset & 0x00FFFFFF));
            Strm.WriteUInt32(firstfour);
            Strm.WriteInt32(DataOffset);
            Strm.WriteInt32(Size);
        }

        public override string ToString() => $"{(IsDirectory ? "Directory" : "File")}: {NameOffset} | {DataOffset} | {Size}";
    }

    private static List<byte> GetDataBytes(List<dynamic> FlatFileList, uint DataStart, ref Dictionary<ArchiveFile, uint> Offsets)
    {
        List<byte> FileBytes = [];
        for (int i = 0; i < FlatFileList.Count; i++)
        {
            if (FlatFileList[i] is not ArchiveFile file)
                continue;

            if (file.FileData is null)
                throw new NullReferenceException("FileData cannot be NULL");

            if (Offsets.Any(OFF =>
            {
                if (OFF.Key.FileData is null)
                    throw new NullReferenceException("FileData cannot be NULL");
                return OFF.Key.FileData.SequenceEqual(file.FileData);
            }))
            {
                Offsets.Add(file, Offsets[Offsets.Keys.First(FILE =>
                {
                    if (FILE.FileData is null)
                        throw new NullReferenceException("FileData cannot be NULL");
                    return FILE.FileData.SequenceEqual(file.FileData);
                })]);
            }
            else
            {
                List<byte> CurrentMRAMFile = [.. file.FileData];
                while (CurrentMRAMFile.Count % 32 != 0)
                    CurrentMRAMFile.Add(0x00);
                Offsets.Add(file, (uint)FileBytes.Count + DataStart);
                FileBytes.AddRange(CurrentMRAMFile);
            }
        }
        return FileBytes;
    }
    private static List<byte> GetStringTableBytes(List<dynamic> FlatFileList, ref Dictionary<string, uint> Offsets)
    {
        List<byte> strings = [];
        Encoding enc = Encoding.GetEncoding(932);

        for (int i = 0; i < FlatFileList.Count; i++)
        {
            if (!Offsets.ContainsKey(FlatFileList[i].Name))
            {
                Offsets.Add(FlatFileList[i].Name, (uint)strings.Count);
                strings.AddRange(enc.GetBytes(FlatFileList[i].Name));
                strings.Add(0x00);
            }
        }
        return strings;
    }
    #endregion
}