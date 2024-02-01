using Hack.io.Class;
using Hack.io.Utility;
using System.Text;

namespace Hack.io.RARC;

/// <summary>
/// Nintendo File Archive used in WII/GC Games.
/// <para/> NOTE: THIS IS NOT A U8 ARCHIVE!
/// </summary>
public class RARC : Archive
{
    #region CONSTANTS
    /// <inheritdoc cref="Interface.DocGen.DOC_MAGIC"/>
    public const string MAGIC = "RARC";
    #endregion

    #region Properties
    /// <summary>
    /// If false, the user must set all unique ID's for each file
    /// </summary>
    public bool KeepFileIDsSynced { get; set; } = true;

    /// <summary>
    /// Gets the next free File ID
    /// </summary>
    public short NextFreeFileID => GetNextFreeID();
    #endregion

    #region Constructors
    /// <summary>
    /// Create an empty archive
    /// </summary>
    public RARC() : base() { }
    #endregion

    #region Functions
    //PUBLIC

    //INTERNAL
    /// <summary>
    /// Generates a 2 byte hash from a string
    /// </summary>
    /// <param name="Input">string to convert</param>
    /// <returns>hashed string</returns>
    internal static ushort StringToHash(string Input)
    {
        int Hash = 0;
        for (int i = 0; i < Input.Length; i++)
        {
            Hash *= 3;
            Hash += Input[i];
            Hash = 0xFFFF & Hash; //cast to short 
        }

        return (ushort)Hash;
    }

    //PROTECTED
    /// <inheritdoc/>
    protected override void OnItemSet(object? value, string Path)
    {
        if (!KeepFileIDsSynced && value is File file && file.ID == -1 && !ItemExists(Path))
            file.ID = GetNextFreeID();
    }

    /// <inheritdoc/>
    protected override void Read(Stream Strm)
    {
        #region Header
        FileUtil.ExceptionOnBadMagic(Strm, MAGIC);

        uint FileSize = Strm.ReadUInt32(),
            DataHeaderOffset = Strm.ReadUInt32(),
            DataOffset = Strm.ReadUInt32() + 0x20,
            DataLength = Strm.ReadUInt32(),
            MRAMSize = Strm.ReadUInt32(),
            ARAMSize = Strm.ReadUInt32();
        Strm.Position += 0x04; //Skip the supposed padding
        #endregion

        #region Data Header
        uint DirectoryCount = Strm.ReadUInt32(),
                DirectoryTableOffset = Strm.ReadUInt32() + 0x20,
                FileEntryCount = Strm.ReadUInt32(),
                FileEntryTableOffset = Strm.ReadUInt32() + 0x20,
                StringTableSize = Strm.ReadUInt32(),
                StringTableOffset = Strm.ReadUInt32() + 0x20;
        ushort NextFreeFileID = Strm.ReadUInt16();
        KeepFileIDsSynced = Strm.ReadByte() != 0x00;
        #endregion

        #region Directory Nodes
        Strm.Position = DirectoryTableOffset;

        List<RARCDirEntry> FlatDirectoryList = new();

        for (int i = 0; i < DirectoryCount; i++)
            FlatDirectoryList.Add(new(Strm, StringTableOffset));
        #endregion

        #region File Nodes
        List<RARCFileEntry> FlatFileList = new();
        Strm.Seek(FileEntryTableOffset, SeekOrigin.Begin);
        for (int i = 0; i < FileEntryCount; i++)
        {
            FlatFileList.Add(new RARCFileEntry()
            {
                FileID = Strm.ReadInt16(),
                NameHash = Strm.ReadInt16(),
                Type = Strm.ReadInt16()
            });
            ushort CurrentNameOffset = Strm.ReadUInt16();
            FlatFileList[^1].ModularA = Strm.ReadInt32();
            FlatFileList[^1].ModularB = Strm.ReadInt32();
            Strm.Position += 0x04;
            long Pauseposition = Strm.Position;
            Strm.Seek(StringTableOffset + CurrentNameOffset, SeekOrigin.Begin);
            FlatFileList[^1].Name = Strm.ReadString(StreamUtil.ShiftJIS);
            Strm.Position = Pauseposition;
        }

        List<Directory> Directories = new();
        for (int i = 0; i < FlatDirectoryList.Count; i++)
        {
            Directories.Add(new Directory(this, i, FlatDirectoryList, FlatFileList, DataOffset, Strm));
        }

        for (int i = 0; i < Directories.Count; i++)
        {
            List<KeyValuePair<string, object>> templist = new();
            foreach (KeyValuePair<string, object> DirectoryItem in Directories[i].Items)
            {
                if (DirectoryItem.Value is RARCFileEntry fe)
                {
                    if (DirectoryItem.Key.Equals("."))
                    {
                        if (fe.ModularA == 0)
                            Root = Directories[fe.ModularA];
                        continue;
                    }
                    if (DirectoryItem.Key.Equals(".."))
                    {
                        if (fe.ModularA == -1 || fe.ModularA > Directories.Count)
                            continue;
                        Directories[i].Parent = Directories[fe.ModularA];
                        continue;
                    }
                    if (!Directories[fe.ModularA].Name.Equals(DirectoryItem.Key))
                        Directories[fe.ModularA].Name = DirectoryItem.Key;
                    templist.Add(new KeyValuePair<string, object>(DirectoryItem.Key, Directories[fe.ModularA]));
                }
                else
                {
                    ((File)DirectoryItem.Value).Parent = Directories[i];
                    templist.Add(DirectoryItem);
                }
            }
            Directories[i].Items = templist.ToDictionary(K => K.Key, V => V.Value);
        }
        #endregion
    }
    /// <inheritdoc/>
    protected override void Write(Stream Strm)
    {
        if (Root is null)
            throw new NullReferenceException(NULL_ROOT_EXCEPTION);

        Dictionary<ArchiveFile, uint> FileOffsets = new();
        uint dataoffset = 0;
        uint MRAMSize = 0, ARAMSize = 0, DVDSize = 0;
        byte[] DataByteBuffer = GetDataBytes(Root, ref FileOffsets, ref dataoffset, ref MRAMSize, ref ARAMSize, ref DVDSize).ToArray();
        short FileID = 0;
        int NextFolderID = 1;
        List<RARCFileEntry> FlatFileList = GetFlatFileList(Root, FileOffsets, ref FileID, 0, ref NextFolderID, -1);
        uint FirstFileOffset = 0;
        List<RARCDirEntry> FlatDirectoryList = GetFlatDirectoryList(Root, ref FirstFileOffset);
        FlatDirectoryList.Insert(0, new RARCDirEntry() { FileCount = (ushort)(Root.Items.Count + 2), FirstFileOffset = 0, Name = Root.Name, NameHash = StringToHash(Root.Name), NameOffset = 0, Type = "ROOT" });
        Dictionary<string, uint> StringLocations = new();
        byte[] StringDataBuffer = GetStringTableBytes(FlatFileList, Root.Name, ref StringLocations).ToArray();

        #region File Writing
        long StartPosition = Strm.Position;
        Strm.WriteString(MAGIC, Encoding.ASCII, null);
        Strm.Write(new byte[16] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x20, 0xDD, 0xDD, 0xDD, 0xDD, 0xEE, 0xEE, 0xEE, 0xEE }, 0, 16);
        Strm.WriteUInt32(MRAMSize);
        Strm.WriteUInt32(ARAMSize);
        Strm.WriteUInt32(DVDSize);
        //Data Header
        Strm.WriteInt32(FlatDirectoryList.Count);
        Strm.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //Directory Nodes Location (-0x20)
        Strm.WriteInt32(FlatFileList.Count);
        Strm.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //File Entries Location (-0x20)
        Strm.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //String Table Size
        Strm.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //string Table Location (-0x20)
        Strm.WriteUInt16((ushort)FlatFileList.Count);
        Strm.WriteByte((byte)(KeepFileIDsSynced ? 0x01 : 0x00));
        Strm.Write(new byte[5], 0, 5);
        long DirectoryEntryOffset = Strm.Position;

        #region Directory Nodes
        for (int i = 0; i < FlatDirectoryList.Count; i++)
        {
            Strm.WriteString(FlatDirectoryList[i].Type, StreamUtil.ShiftJIS, null);
            Strm.WriteUInt32(StringLocations[FlatDirectoryList[i].Name]);
            Strm.WriteUInt16(FlatDirectoryList[i].NameHash);
            Strm.WriteUInt16(FlatDirectoryList[i].FileCount);
            Strm.WriteUInt32(FlatDirectoryList[i].FirstFileOffset);
        }
        Strm.PadTo(32);
        #endregion

        long FileEntryOffset = Strm.Position;

        #region File Entries
        for (int i = 0; i < FlatFileList.Count; i++)
        {
            Strm.WriteInt16(FlatFileList[i].FileID);
            Strm.WriteUInt16(StringToHash(FlatFileList[i].Name));
            Strm.WriteInt16(FlatFileList[i].Type);
            Strm.WriteUInt16((ushort)StringLocations[FlatFileList[i].Name]);
            Strm.WriteInt32(FlatFileList[i].ModularA);
            Strm.WriteInt32(FlatFileList[i].ModularB);
            Strm.Write(new byte[4], 0, 4);
        }
        Strm.PadTo(32);
        #endregion

        long StringTableOffset = Strm.Position;

        #region String Table
        Strm.Write(StringDataBuffer, 0, StringDataBuffer.Length);

        Strm.PadTo(32);
        #endregion

        long FileTableOffset = Strm.Position;

        #region File Table
        Strm.Write(DataByteBuffer, 0, DataByteBuffer.Length);
        #endregion
        long EndPosition = Strm.Position;

        #region Header
        Strm.Position = StartPosition + 0x04;
        Strm.WriteInt32((int)(Strm.Length - StartPosition));
        Strm.Position += 0x04;
        Strm.WriteInt32((int)((FileTableOffset - 0x20) - StartPosition));
        Strm.WriteInt32((int)(EndPosition - (FileTableOffset - StartPosition)));
        Strm.Position += 0x10;
        Strm.WriteInt32((int)((DirectoryEntryOffset - 0x20)- StartPosition));
        Strm.Position += 0x04;
        Strm.WriteInt32((int)((FileEntryOffset - 0x20) - StartPosition));
        Strm.WriteInt32((int)((FileTableOffset - StartPosition) - (StringTableOffset - StartPosition)));
        Strm.WriteInt32((int)((StringTableOffset - 0x20) - StartPosition));
        #endregion

        Strm.Position = EndPosition;
        #endregion
    }

    /// <inheritdoc/>
    protected override ArchiveDirectory NewDirectory() => new Directory();
    /// <inheritdoc/>
    protected override ArchiveDirectory NewDirectory(Archive? Owner, ArchiveDirectory? parent) => new Directory((RARC?)Owner, (Directory?)parent);

    //PRIVATE
    private short GetNextFreeID()
    {
        if (Root is null)
            throw new NullReferenceException(NULL_ROOT_EXCEPTION);

        List<short> AllIDs = new();
        List<ArchiveFile?> FlatFileList = GetFlatFileList(Root);
        for (int i = 0; i < FlatFileList.Count; i++)
            AllIDs.Add(((File?)FlatFileList[i])?.ID ?? (short)AllIDs.Count);
        if (AllIDs.Count == 0)
            return 0;
        int a = AllIDs.OrderBy(x => x).First();
        int b = AllIDs.OrderBy(x => x).Last();
        List<int> LiterallyAllIDs = Enumerable.Range(0, b - a + 1).ToList();
        List<short> Shorts = new();
        for (int i = 0; i < LiterallyAllIDs.Count; i++)
        {
            Shorts.Add((short)LiterallyAllIDs[i]);
        }

        List<short> Remaining = Shorts.Except(AllIDs).ToList();
        if (Remaining.Count == 0)
            return (short)AllIDs.Count;
        else
            return Remaining.First();
    }

    private List<byte> GetDataBytes(ArchiveDirectory Root, ref Dictionary<ArchiveFile, uint> Offsets, ref uint LocalOffset, ref uint MRAMSize, ref uint ARAMSize, ref uint DVDSize)
    {
        List<byte> DataBytesMRAM = new();
        List<byte> DataBytesARAM = new();
        List<byte> DataBytesDVD = new();
        //First, we must sort the files in the correct order
        //MRAM First. ARAM Second, DVD Last
        List<ArchiveFile> MRAM = new(), ARAM = new(), DVD = new();
        SortFilesByLoadType(Root, ref MRAM, ref ARAM, ref DVD);

        for (int i = 0; i < MRAM.Count; i++)
        {
            if (Offsets.Any(OFF =>
            {
                byte[]? offdata = OFF.Key.FileData;
                byte[]? data = MRAM[i].FileData;
                if (offdata is null)
                    throw new NullReferenceException();
                if (data is null)
                    throw new NullReferenceException();
                return offdata.SequenceEqual(data);
            }))
            {
                Offsets.Add(MRAM[i], Offsets[Offsets.Keys.First(FILE =>
                {
                    byte[]? offdata = FILE.FileData;
                    byte[]? data = MRAM[i].FileData;
                    if (offdata is null)
                        throw new NullReferenceException();
                    if (data is null)
                        throw new NullReferenceException();
                    return offdata.SequenceEqual(data);
                })]);
            }
            else
            {
                List<byte> CurrentMRAMFile = MRAM[i].FileData?.ToList() ?? new();
                while (CurrentMRAMFile.Count % 32 != 0)
                    CurrentMRAMFile.Add(0x00);
                Offsets.Add(MRAM[i], LocalOffset);
                DataBytesMRAM.AddRange(CurrentMRAMFile);
                LocalOffset += (uint)CurrentMRAMFile.Count;
            }
        }
        MRAMSize = LocalOffset;
        for (int i = 0; i < ARAM.Count; i++)
        {
            Offsets.Add(ARAM[i], LocalOffset);
            List<byte> temp = new();
            temp.AddRange(ARAM[i].FileData ?? throw new NullReferenceException());

            while (temp.Count % 32 != 0)
                temp.Add(0x00);
            DataBytesARAM.AddRange(temp);
            LocalOffset += (uint)temp.Count;
        }
        ARAMSize = LocalOffset - MRAMSize;
        for (int i = 0; i < DVD.Count; i++)
        {
            Offsets.Add(DVD[i], LocalOffset);
            List<byte> temp = new();
            temp.AddRange(DVD[i].FileData ?? throw new NullReferenceException());

            while (temp.Count % 32 != 0)
                temp.Add(0x00);
            DataBytesDVD.AddRange(temp);
            LocalOffset += (uint)temp.Count;
        }
        DVDSize = LocalOffset - ARAMSize - MRAMSize;

        List<byte> DataBytes = new();
        DataBytes.AddRange(DataBytesMRAM);
        DataBytes.AddRange(DataBytesARAM);
        DataBytes.AddRange(DataBytesDVD);
        return DataBytes;
    }
    private void SortFilesByLoadType(ArchiveDirectory Root, ref List<ArchiveFile> MRAM, ref List<ArchiveFile> ARAM, ref List<ArchiveFile> DVD)
    {
        foreach (KeyValuePair<string, object> item in Root.Items)
        {
            if (item.Value is Directory dir)
            {
                SortFilesByLoadType(dir, ref MRAM, ref ARAM, ref DVD);
            }
            else if (item.Value is File file)
            {
                if (file.FileSettings.HasFlag(FileAttribute.PRELOAD_TO_MRAM))
                {
                    MRAM.Add(file);
                }
                else if (file.FileSettings.HasFlag(FileAttribute.PRELOAD_TO_ARAM))
                {
                    ARAM.Add(file);
                }
                else if (file.FileSettings.HasFlag(FileAttribute.LOAD_FROM_DVD))
                {
                    DVD.Add(file);
                }
                else
                    throw new Exception($"File entry \"{file}\" is not set as being loaded into any type of RAM, or from DVD.");
            }
        }
    }
    private List<RARCFileEntry> GetFlatFileList(ArchiveDirectory Root, Dictionary<ArchiveFile, uint> FileOffsets, ref short GlobalFileID, int CurrentFolderID, ref int NextFolderID, int BackwardsFolderID)
    {
        List<RARCFileEntry> FileList = new();
        List<KeyValuePair<int, Directory>> Directories = new();
        foreach (KeyValuePair<string, object> item in Root.Items)
        {
            if (item.Value is File file)
            {
                if (file.FileData is null)
                    throw new NullReferenceException();
                FileList.Add(new RARCFileEntry() { FileID = KeepFileIDsSynced ? GlobalFileID++ : file.ID, Name = file.Name, ModularA = (int)FileOffsets[file], ModularB = file.FileData.Length, Type = (short)((ushort)file.FileSettings << 8) });
            }
            else if (item.Value is Directory Currentdir)
            {
                Directories.Add(new KeyValuePair<int, Directory>(FileList.Count, Currentdir));
                //Dirs.Add(new RARCDirEntry() { FileCount = (ushort)(Currentdir.Items.Count + 2), FirstFileOffset = 0xFFFFFFFF, Name = Currentdir.Name, NameHash = Currentdir.NameToHash(), NameOffset = 0xFFFFFFFF, Type = Currentdir.ToTypeString() });
                FileList.Add(new RARCFileEntry() { FileID = -1, Name = Currentdir.Name, ModularA = NextFolderID++, ModularB = 0x10, Type = 0x0200 });
                GlobalFileID++;
            }
        }
        FileList.Add(new RARCFileEntry() { FileID = -1, Name = ".", ModularA = CurrentFolderID, ModularB = 0x10, Type = 0x0200 });
        FileList.Add(new RARCFileEntry() { FileID = -1, Name = "..", ModularA = BackwardsFolderID, ModularB = 0x10, Type = 0x0200 });
        GlobalFileID += 2;
        for (int i = 0; i < Directories.Count; i++)
        {
            FileList.AddRange(GetFlatFileList(Directories[i].Value, FileOffsets, ref GlobalFileID, FileList[Directories[i].Key].ModularA, ref NextFolderID, CurrentFolderID));
        }
        return FileList;
    }
    private List<ArchiveFile?> GetFlatFileList(ArchiveDirectory Root)
    {
        List<ArchiveFile?> FileList = new();
        foreach (KeyValuePair<string, object> item in Root.Items)
        {
            if (item.Value is ArchiveFile file)
            {
                FileList.Add(file);
            }
            else if (item.Value is ArchiveDirectory Currentdir)
            {
                FileList.AddRange(GetFlatFileList(Currentdir));
                FileList.Add(null);
            }
        }
        return FileList;
    }
    private List<RARCDirEntry> GetFlatDirectoryList(ArchiveDirectory Root, ref uint FirstFileOffset)
    {
        List<RARCDirEntry> FlatDirectoryList = new();
        List<RARCDirEntry> TemporaryList = new();
        FirstFileOffset += (uint)(Root.Items.Count + 2);
        foreach (KeyValuePair<string, object> item in Root.Items)
        {
            if (item.Value is Directory Currentdir)
            {
                FlatDirectoryList.Add(new RARCDirEntry() { FileCount = (ushort)(Currentdir.Items.Count + 2), FirstFileOffset = FirstFileOffset, Name = Currentdir.Name, NameHash = StringToHash(Currentdir.Name), NameOffset = 0xFFFFFFFF, Type = Currentdir.ToTypeString() });
                TemporaryList.AddRange(GetFlatDirectoryList(Currentdir, ref FirstFileOffset));
            }
        }
        FlatDirectoryList.AddRange(TemporaryList);
        return FlatDirectoryList;
    }
    private static List<byte> GetStringTableBytes(List<RARCFileEntry> FlatFileList, string RootName, ref Dictionary<string, uint> Offsets)
    {
        List<byte> strings = new();
        Encoding enc = StreamUtil.ShiftJIS;
        strings.AddRange(enc.GetBytes(RootName));
        strings.Add(0x00);
        Offsets.Add(RootName, 0);

        Offsets.Add(".", (uint)strings.Count);
        strings.AddRange(enc.GetBytes("."));
        strings.Add(0x00);

        Offsets.Add("..", (uint)strings.Count);
        strings.AddRange(enc.GetBytes(".."));
        strings.Add(0x00);

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

    #region Child Classes
    //PUBLIC
    /// <summary>
    /// Folder contained inside the Archive. Can contain more <see cref="Directory"/>s if desired, as well as <see cref="File"/>s
    /// </summary>
    public class Directory : ArchiveDirectory
    {
        #region Constructors
        /// <inheritdoc/>
        public Directory() { }
        /// <summary>
        /// Create a new, child directory
        /// </summary>
        /// <param name="Owner">The Owner Archive</param>
        /// <param name="parentdir">The Parent Directory. NULL if this is the Root Directory</param>
        public Directory(RARC? Owner, Directory? parentdir) : base(Owner, parentdir) { }

        internal Directory(RARC Owner, int ID, List<RARCDirEntry> DirectoryNodeList, List<RARCFileEntry> FlatFileList, uint DataBlockStart, Stream RARCFile)
        {
            OwnerArchive = Owner;
            Name = DirectoryNodeList[ID].Name;
            for (int i = (int)DirectoryNodeList[ID].FirstFileOffset; i < DirectoryNodeList[ID].FileCount + DirectoryNodeList[ID].FirstFileOffset; i++)
            {
                //IsDirectory
                if (FlatFileList[i].Type == 0x0200)
                {
                    Items.Add(FlatFileList[i].Name, FlatFileList[i]);
                }
                else
                {
                    Items.Add(FlatFileList[i].Name, new File(FlatFileList[i], DataBlockStart, RARCFile));
                }
            }
        }
        #endregion

        /// <summary>
        /// Create an ArchiveDirectory. You cannot use this function unless this directory is empty
        /// </summary>
        /// <param name="FolderPath"></param>
        /// <param name="OwnerArchive"></param>
        public new void CreateFromFolder(string FolderPath, Archive? OwnerArchive = null)
        {
            if (OwnerArchive is not RARC r)
                throw new Exception();

            if (Items.Count > 0)
                throw new Exception("Cannot create a directory from a folder if Items exist");
            string[] Found = System.IO.Directory.GetFiles(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < Found.Length; i++)
            {
                ArchiveFile temp = new()
                {
                    Name = new FileInfo(Found[i]).Name
                };
                FileUtil.LoadFile(Found[i], temp.Load);
                Items[temp.Name] = temp;
            }

            string[] SubDirs = System.IO.Directory.GetDirectories(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < SubDirs.Length; i++)
            {
                Directory temp = (Directory)NewDirectory();
                temp.OwnerArchive = OwnerArchive;
                temp.CreateFromFolder(SubDirs[i]);
                Items[temp.Name] = temp;
            }
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Name} - {Items.Count} Item(s)";

        internal string ToTypeString() => Name.ToUpper().PadRight(4, ' ')[..4];

        /// <inheritdoc/>
        protected override ArchiveDirectory NewDirectory() => new Directory();
        /// <inheritdoc/>
        protected override ArchiveDirectory NewDirectory(Archive? Owner, ArchiveDirectory? parent) => new Directory((RARC?)Owner, (Directory?)parent);


        public override bool Equals(object? obj)
        {
            if (obj is not Directory OtherDir)
                return false;

            if (Items.Count != OtherDir.Items.Count)
                return false;

            foreach (var item in Items)
            {
                if (!OtherDir.ItemKeyExists(item.Key))
                    return false;
                if (!item.Value.Equals(OtherDir.Items[item.Key]))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// File contained inside the Archive
    /// </summary>
    public class File : ArchiveFile
    {
        /// <summary>
        /// Extra settings for this File.<para/>Default: <see cref="FileAttribute.FILE"/> | <see cref="FileAttribute.PRELOAD_TO_MRAM"/>
        /// </summary>
        public FileAttribute FileSettings { get; set; } = FileAttribute.FILE | FileAttribute.PRELOAD_TO_MRAM;
        /// <summary>
        /// The ID of the file in the archive
        /// </summary>
        public short ID { get; set; } = -1;

        /// <summary>
        /// Empty file
        /// </summary>
        public File() { }
        internal File(RARCFileEntry entry, uint DataBlockStart, Stream RARCFile)
        {
            Name = entry.Name;
            FileSettings = entry.RARCFileType;
            ID = entry.FileID;
            RARCFile.Position = DataBlockStart + entry.ModularA;
            FileData = new byte[entry.ModularB];
            RARCFile.Read(FileData);
        }
        
        /// <inheritdoc/>
        public override string ToString() => $"{ID} - {Name} ({FileSettings}) [0x{FileData?.Length ?? 0:X8}]";

        public override bool Equals(object? obj)
        {
            return obj is File OtherFile &&
                Name.Equals(OtherFile.Name) &&
                FileSettings.Equals(OtherFile.FileSettings) &&
                (FileData is not null && OtherFile.FileData is not null ? (FileData.SequenceEqual(OtherFile.FileData)) : (FileData is null && OtherFile.FileData is null));
        }
    }

    //INTERNAL
    /// <summary>
    /// Only used when Reading / Writing
    /// </summary>
    internal class RARCDirEntry
    {
        /// <summary>
        /// Directory Type. usually the first 4 letters of the <see cref="Name"/>. If the <see cref="Name"/> is shorter than 4, the missing spots will be ' ' (space)
        /// </summary>
        public string Type { get; set; } = "    ";
        public string Name { get; set; } = "";
        public uint NameOffset { get; set; }
        public ushort NameHash { get; set; }
        public ushort FileCount { get; set; }
        public uint FirstFileOffset { get; set; }

        public RARCDirEntry() { }
        public RARCDirEntry(Stream RARCFile, uint StringTableOffset)
        {
            Type = RARCFile.ReadString(4, Encoding.ASCII);
            NameOffset = RARCFile.ReadUInt32();
            NameHash = RARCFile.ReadUInt16();
            FileCount = RARCFile.ReadUInt16();
            FirstFileOffset = RARCFile.ReadUInt32();

            long pauseposition = RARCFile.Position;
            RARCFile.Position = StringTableOffset + NameOffset;
            Name = RARCFile.ReadString(StreamUtil.ShiftJIS);
            RARCFile.Position = pauseposition;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Name} ({Type}) [0x{NameHash:X4}] {FileCount} File(s)";
    }

    /// <summary>
    /// Only used when Reading / Writing
    /// </summary>
    internal class RARCFileEntry
    {
        public short FileID;
        public short Type;
        public string Name = "    ";
        /// <summary>
        /// For files: offset to file data in file data section, for subdirectories: index of the corresponding directory node
        /// </summary>
        public int ModularA;
        /// <summary>
        /// For files: size of the file, for subdirectories: always 0x10 (size of the node entry?)
        /// </summary>
        public int ModularB;
        internal short NameHash;
        internal FileAttribute RARCFileType => (FileAttribute)((Type & 0xFF00) >> 8);
        /// <inheritdoc/>
        public override string ToString() => $"({FileID}) {Name}, {Type.ToString("X").PadLeft(4, '0')} ({RARCFileType}), [{ModularA:X8}][{ModularB:X8}]";
    }
    #endregion

    #region Enums
    /// <summary>
    /// File Attibutes for <see cref="File"/>
    /// </summary>
    [Flags]
    public enum FileAttribute
    {
        /// <summary>
        /// Indicates this is a File
        /// </summary>
        FILE = 0x01,
        /// <summary>
        /// Directory. Not allowed to be used for <see cref="File"/>s, only here for reference
        /// </summary>
        DIRECTORY = 0x02,
        /// <summary>
        /// Indicates that this file is compressed
        /// </summary>
        COMPRESSED = 0x04,
        /// <summary>
        /// Indicates that this file gets Pre-loaded into Main RAM
        /// </summary>
        PRELOAD_TO_MRAM = 0x10,
        /// <summary>
        /// Indicates that this file gets Pre-loaded into Auxiliary RAM
        /// </summary>
        PRELOAD_TO_ARAM = 0x20,
        /// <summary>
        /// Indicates that this file does not get pre-loaded, but rather read from the DVD
        /// </summary>
        LOAD_FROM_DVD = 0x40,
        /// <summary>
        /// Indicates that this file is YAZ0 Compressed
        /// </summary>
        YAZ0_COMPRESSED = 0x80
    }
    #endregion
}