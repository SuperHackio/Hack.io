using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hack.io.RARC
{
    /// <summary>
    /// Nintendo File Archive used in WII/GC Games.
    /// <para/> NOTE: THIS IS NOT A U8 ARCHIVE!
    /// </summary>
    public class RARC
    {
        #region Fields and Properties
        /// <summary>
        /// Filename of this Archive.
        /// <para/>Set using <see cref="Save(string)"/>;
        /// </summary>
        public string FileName { get; private set; } = null;
        /// <summary>
        /// Get the name of the archive without the path
        /// </summary>
        public string Name { get { return FileName == null ? FileName : new FileInfo(FileName).Name; } }
        /// <summary>
        /// The Root Directory of the Archive
        /// </summary>
        public Directory Root { get; set; }
        /// <summary>
        /// File Identifier
        /// </summary>
        private readonly string Magic = "RARC";
        #endregion

        #region Constructors
        /// <summary>
        /// Create an empty archive
        /// </summary>
        public RARC() { }
        /// <summary>
        /// Open an archive
        /// </summary>
        /// <param name="filename">Archive full filepath</param>
        public RARC(string filename)
        {
            FileStream RARCFile = new FileStream(filename, FileMode.Open);
            Read(RARCFile);
            RARCFile.Close();
            FileName = filename;
        }
        /// <summary>
        /// Open an archive that's stored inside a stream.
        /// <para/> Stream will be a <see cref="MemoryStream"/> if the Hack.io.YAZ0 library was used.
        /// </summary>
        /// <param name="RARCFile">Memorystream containing the archiev</param>
        /// <param name="filename">Filename to give</param>
        public RARC(Stream RARCFile, string filename = null)
        {
            Read(RARCFile);
            FileName = filename;
        }
        #endregion

        #region Public Functions

        #region File Functions
        /// <summary>
        /// Get or Set a file based on a path. When setting, if the file doesn't exist, it will be added (Along with any missing subdirectories)
        /// </summary>
        /// <param name="Path">The Path to take. Does not need the Root name to start, but cannot start with a '/'</param>
        /// <returns></returns>
        public object this[string Path]
        {
            get
            {
                if (Path.StartsWith(Root.Name+"/"))
                    Path = Path.Substring(Root.Name.Length+1);
                return Root[Path];
            }
            set
            {
                if (Path.StartsWith(Root.Name + "/"))
                    Path = Path.Substring(Root.Name.Length + 1);
                Root[Path] = value;
            }
        }
        /// <summary>
        /// Checks to see if an Item Exists based on a Path
        /// </summary>
        /// <param name="Path">The path to take</param>
        /// <returns>false if the Item isn't found</returns>
        public bool ItemExists(string Path) => Root.ItemKeyExists(Path);
        #endregion

        /// <summary>
        /// Save the Archive to a File
        /// </summary>
        /// <param name="filepath">New file to save to</param>
        public void Save(string filepath)
        {
            FileStream fs = new FileStream(filepath, FileMode.Create);
            Save(fs);
            fs.Close();
        }
        /// <summary>
        /// Write the Archive to a Stream
        /// </summary>
        /// <param name="RARCFile"></param>
        public void Save(Stream RARCFile)
        {
            Dictionary<File, uint> FileOffsets = new Dictionary<File, uint>();
            uint dataoffset = 0;
            byte[] DataByteBuffer = GetDataBytes(Root, ref FileOffsets, ref dataoffset).ToArray();
            short FileID = 0;
            int NextFolderID = 1;
            List<RARCFileEntry> FlatFileList = GetFlatFileList(Root, FileOffsets, ref FileID, 0, ref NextFolderID, -1);
            uint FirstFileOffset = 0;
            List<RARCDirEntry> FlatDirectoryList = GetFlatDirectoryList(Root, ref FirstFileOffset);
            FlatDirectoryList.Insert(0, new RARCDirEntry() { FileCount = (ushort)(Root.Items.Count + 2), FirstFileOffset = 0, Name = Root.Name, NameHash = StringToHash(Root.Name), NameOffset = 0, Type = "ROOT" });
            Dictionary<string, uint> StringLocations = new Dictionary<string, uint>();
            byte[] StringDataBuffer = GetStringTableBytes(FlatFileList, Root.Name, ref StringLocations).ToArray();

            #region File Writing
            RARCFile.WriteString(Magic);
            RARCFile.Write(new byte[12] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x20, 0xDD, 0xDD, 0xDD, 0xDD }, 0, 12);
            RARCFile.Write(new byte[16] { 0xEE, 0xEE, 0xEE, 0xEE, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0, 16);
            RARCFile.WriteReverse(BitConverter.GetBytes(FlatDirectoryList.Count), 0, 4);
            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //Directory Nodes Location (-0x20)
            RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList.Count), 0, 4);
            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //File Entries Location (-0x20)
            RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //String Table Size
            RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //string Table Location (-0x20)
            RARCFile.WriteReverse(BitConverter.GetBytes((ushort)FlatFileList.Count), 0, 2);
            RARCFile.Write(new byte[2] { 0x01, 0x00 }, 0, 2);
            RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
            long DirectoryEntryOffset = RARCFile.Position;

            #region Directory Nodes
            for (int i = 0; i < FlatDirectoryList.Count; i++)
            {
                RARCFile.WriteString(FlatDirectoryList[i].Type);
                RARCFile.WriteReverse(BitConverter.GetBytes(StringLocations[FlatDirectoryList[i].Name]), 0, 4);
                RARCFile.WriteReverse(BitConverter.GetBytes(FlatDirectoryList[i].NameHash), 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(FlatDirectoryList[i].FileCount), 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(FlatDirectoryList[i].FirstFileOffset), 0, 4);
            }

            #region Padding
            while (RARCFile.Position % 32 != 0)
                RARCFile.WriteByte(0x00);
            #endregion
            #endregion

            long FileEntryOffset = RARCFile.Position;

            #region File Entries
            for (int i = 0; i < FlatFileList.Count; i++)
            {
                RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList[i].FileID), 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(FlatFileList[i].Name)), 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList[i].Type), 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes((ushort)StringLocations[FlatFileList[i].Name]), 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList[i].ModularA), 0, 4);
                RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList[i].ModularB), 0, 4);
                RARCFile.Write(new byte[4], 0, 4);
            }
            #region Padding
            while (RARCFile.Position % 32 != 0)
                RARCFile.WriteByte(0x00);
            #endregion
            #endregion

            long StringTableOffset = RARCFile.Position;

            #region String Table
            RARCFile.Write(StringDataBuffer, 0, StringDataBuffer.Length);

            #region Padding
            while (RARCFile.Position % 32 != 0)
                RARCFile.WriteByte(0x00);
            #endregion
            #endregion

            long FileTableOffset = RARCFile.Position;

            #region File Table
            RARCFile.Write(DataByteBuffer, 0, DataByteBuffer.Length);
            #endregion

            #region Header
            RARCFile.Position = 0x04;
            RARCFile.WriteReverse(BitConverter.GetBytes((int)RARCFile.Length), 0, 4);
            RARCFile.Position += 0x04;
            RARCFile.WriteReverse(BitConverter.GetBytes((int)(FileTableOffset - 0x20)), 0, 4);
            RARCFile.WriteReverse(BitConverter.GetBytes((int)(RARCFile.Length - FileTableOffset)), 0, 4);
            RARCFile.WriteReverse(BitConverter.GetBytes((int)(RARCFile.Length - FileTableOffset)), 0, 4);
            RARCFile.Position += 0x0C;
            RARCFile.WriteReverse(BitConverter.GetBytes((int)(DirectoryEntryOffset - 0x20)), 0, 4);
            RARCFile.Position += 0x04;
            RARCFile.WriteReverse(BitConverter.GetBytes((int)(FileEntryOffset - 0x20)), 0, 4);
            RARCFile.WriteReverse(BitConverter.GetBytes((int)(FileTableOffset - StringTableOffset)), 0, 4);
            RARCFile.WriteReverse(BitConverter.GetBytes((int)(StringTableOffset - 0x20)), 0, 4);
            #endregion

            #endregion
        }
        /// <summary>
        /// Create an Archive from a Folder
        /// </summary>
        /// <param name="Folderpath">Folder to make an archive from</param>
        public void Import(string Folderpath) => Root = new Directory(Folderpath);
        /// <summary>
        /// Dump the contents of this archive to a folder
        /// </summary>
        /// <param name="FolderPath">The Path to save to. Should be a folder</param>
        /// <param name="Overwrite">If there are contents already at the chosen location, delete them?</param>
        public void Export(string FolderPath, bool Overwrite = false)
        {
            FolderPath = Path.Combine(FolderPath, Root.Name);
            if (System.IO.Directory.Exists(FolderPath))
            {
                if (Overwrite)
                {
                    System.IO.Directory.Delete(FolderPath, true);
                    System.IO.Directory.CreateDirectory(FolderPath);
                }
                else
                    throw new Exception("Target directory is occupied");
            }
            else
                System.IO.Directory.CreateDirectory(FolderPath);

            Root.Export(FolderPath);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            int Count = 0;//Root.CountAllFiles();
            return $"{new FileInfo(FileName).Name} - {Count} File{(Count > 1 ? "s" : "")} total";
        }
        #endregion

        /// <summary>
        /// Folder contained inside the Archive. Can contain more <see cref="Directory"/>s if desired, as well as <see cref="File"/>s
        /// </summary>
        public class Directory
        {
            /// <summary>
            /// The name of the Directory
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// The contents of this directory.
            /// </summary>
            public Dictionary<string, object> Items { get; set; } = new Dictionary<string, object>();

            /// <summary>
            /// Create a new Archive Directory
            /// </summary>
            public Directory() {}
            /// <summary>
            /// Import a Folder into a RARCDirectory
            /// </summary>
            /// <param name="FolderPath"></param>
            public Directory(string FolderPath)
            {
                DirectoryInfo DI = new DirectoryInfo(FolderPath);
                Name = DI.Name;
                CreateFromFolder(FolderPath);
            }
            internal Directory(int ID, List<RARCDirEntry> DirectoryNodeList, List<RARCFileEntry> FlatFileList, uint DataBlockStart, Stream RARCFile)
            {
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

            /// <summary>
            /// Export this Directory to a folder.
            /// </summary>
            /// <param name="FolderPath">Folder to Export to. Don't expect the files to appear here. Expect a Folder with this <see cref="Name"/> to appear</param>
            public void Export(string FolderPath)
            {
                foreach (KeyValuePair<string, object> item in Items)
                {
                    if (item.Value is File file)
                    {
                        file.Save(FolderPath);
                    }
                    else if (item.Value is Directory directory)
                    {
                        string newstring = Path.Combine(FolderPath, directory.Name);
                        System.IO.Directory.CreateDirectory(newstring);
                        directory.Export(newstring);
                    }
                }
            }
            /// <summary>
            /// Get or Set a file based on a path. When setting, if the file doesn't exist, it will be added (Along with any missing subdirectories)
            /// </summary>
            /// <param name="Path">The Path to take</param>
            /// <returns></returns>
            public object this[string Path]
            {
                get
                {
                    string[] PathSplit = Path.Split('/');
                    if (!ItemKeyExists(PathSplit[0]))
                        return null;
                    return (PathSplit.Length > 1 && Items[PathSplit[0]] is Directory dir) ? dir[Path.Substring(PathSplit[0].Length + 1)] : Items[PathSplit[0]];
                }
                set
                {
                    string[] PathSplit = Path.Split('/');
                    if (!ItemKeyExists(PathSplit[0]))
                    {
                        if (PathSplit.Length == 1)
                            Items.Add(PathSplit[0], value);
                        else
                        {
                            Items.Add(PathSplit[0], new Directory() { Name = PathSplit[0] });
                            ((Directory)Items[PathSplit[0]])[Path.Substring(PathSplit[0].Length + 1)] = value;
                        }
                    }
                    else
                    {
                        if (PathSplit.Length == 1)
                            Items[PathSplit[0]] = value;
                        else if (Items[PathSplit[0]] is Directory dir)
                            dir[Path.Substring(PathSplit[0].Length + 1)] = value;
                    }
                }
            }
            /// <summary>
            /// Checks to see if an Item Exists based on a Path
            /// </summary>
            /// <param name="Path">The path to take</param>
            /// <returns>false if the Item isn't found</returns>
            public bool ItemExists(string Path)
            {
                string[] PathSplit = Path.Split('/');
                if (PathSplit.Length > 1 && Items[PathSplit[0]] is Directory dir)
                    return dir.ItemExists(Path.Substring(PathSplit[0].Length + 1));
                else if (PathSplit.Length > 1)
                    return false;
                else
                    return ItemKeyExists(PathSplit[0]);
            }
            /// <summary>
            /// Checks to see if an item exists in this directory only
            /// </summary>
            /// <param name="ItemName">The name of the Item to look for (Case Sensitive)</param>
            /// <returns>false if the Item doesn't exist</returns>
            public bool ItemKeyExists(string ItemName) => Items.ContainsKey(ItemName);

            internal string ToTypeString() => Name.ToUpper().PadRight(4, ' ').Substring(0, 4);

            private void CreateFromFolder(string FolderPath)
            {
                string[] Found = System.IO.Directory.GetFiles(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < Found.Length; i++)
                {
                    File temp = new File(Found[i]);
                    Items.Add(temp.Name, temp);
                }

                string[] SubDirs = System.IO.Directory.GetDirectories(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < SubDirs.Length; i++)
                {
                    Directory temp = new Directory(SubDirs[i]);
                    Items.Add(temp.Name, temp);
                }
            }
        }

        /// <summary>
        /// File contained inside the Archive
        /// </summary>
        public class File
        {
            /// <summary>
            /// Name of the File
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// The extension of this file
            /// </summary>
            public string Extension
            {
                get
                {
                    if (Name == null)
                        return null;
                    string[] parts = Name.Split('.');
                    return "." + parts[parts.Length - 1].ToLower();
                }
            }
            /// <summary>
            /// The Actual Data for the file
            /// </summary>
            public byte[] FileData { get; set; }
            /// <summary>
            /// Load a File's Data based on a path
            /// </summary>
            /// <param name="Filepath"></param>
            public File(string Filepath)
            {
                Name = new FileInfo(Filepath).Name;
                FileData = System.IO.File.ReadAllBytes(Filepath);
            }
            internal File(RARCFileEntry entry, uint DataBlockStart, Stream RARCFile)
            {
                Name = entry.Name;
                RARCFile.Position = DataBlockStart + entry.ModularA;
                FileData = RARCFile.Read(0, entry.ModularB);
            }
            /// <summary>
            /// Saves this file to the Computer's Disk
            /// </summary>
            /// <param name="Folderpath">The full path to save to</param>
            /// <param name="NewName">Set this to rename the output file</param>
            public void Save(string Folderpath, string NewName = null)
            {
                if ((Name == null || Name == "") && (NewName == null || NewName == ""))
                    throw new ArgumentNullException($"Arguments 'Name' & 'NewName' are NULL!");
                System.IO.File.WriteAllBytes(Path.Combine(Folderpath, NewName ?? Name), FileData);
            }
            /// <summary>
            /// Compare this file to another
            /// </summary>
            /// <param name="obj">The Object to check</param>
            /// <returns>True if the files are identical</returns>
            public override bool Equals(object obj)
            {
                return obj is File file &&
                       Name == file.Name &&
                       Extension == file.Extension &&
                       EqualityComparer<byte[]>.Default.Equals(FileData, file.FileData);
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                var hashCode = -138733157;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Extension);
                hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(FileData);
                return hashCode;
            }
        }

        #region Internals
        /// <summary>
        /// Only used when Reading / Writing
        /// </summary>
        internal class RARCDirEntry
        {
            /// <summary>
            /// Directory Type. usually the first 4 letters of the <see cref="Name"/>. If the <see cref="Name"/> is shorter than 4, the missing spots will be ' ' (space)
            /// </summary>
            public string Type { get; set; }
            public string Name { get; set; }
            public uint NameOffset { get; set; }
            public ushort NameHash { get; set; }
            public ushort FileCount { get; set; }
            public uint FirstFileOffset { get; set; }

            public RARCDirEntry() { }
            public RARCDirEntry(Stream RARCFile, uint StringTableOffset)
            {
                Type = RARCFile.ReadString(4);
                NameOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);
                NameHash = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
                FileCount = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
                FirstFileOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);

                long pauseposition = RARCFile.Position;
                RARCFile.Position = StringTableOffset + NameOffset;
                Name = RARCFile.ReadString();
                RARCFile.Position = pauseposition;
            }

            internal void Write(Stream RARCFile, Dictionary<string, uint> StringLocations)
            {

            }

            public override string ToString() => $"{Name} ({Type}) [0x{NameHash.ToString("X4")}] {FileCount} File(s)";
        }

        /// <summary>
        /// Only used when Reading / Writing
        /// </summary>
        internal class RARCFileEntry
        {
            public short FileID;
            public short Type;
            public string Name;
            /// <summary>
            /// For files: offset to file data in file data section, for subdirectories: index of the corresponding directory node
            /// </summary>
            public int ModularA;
            /// <summary>
            /// For files: size of the file, for subdirectories: always 0x10 (size of the node entry?)
            /// </summary>
            public int ModularB;
            internal short NameHash;

            public override string ToString() => $"({FileID}) {Name}, {Type.ToString("X").PadLeft(4, '0')}, [{ModularA.ToString("X").PadLeft(8, '0')}][{ModularB.ToString("X").PadLeft(8, '0')}]";
        }

        /// <summary>
        /// Generates a 2 byte hash from a string
        /// </summary>
        /// <param name="Input">string to convert</param>
        /// <returns>hashed string</returns>
        internal ushort StringToHash(string Input)
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
        #endregion

        #region Privates
        private void Read(Stream RARCFile)
        {
            if (RARCFile.ReadString(4) != Magic)
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");
            uint FileSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                TrashData = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                DataOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;
            RARCFile.Position += 0x10; //Skip the Lengths and Unknowns
            uint DirectoryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                DirectoryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20,
                FileEntryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                FileEntryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;
            RARCFile.Position += 0x04;
            uint StringTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;

#if DEBUG
            //string XML = $"<RarcHeader StringOffset=\"0x{StringTableOffset.ToString("X8")}\">\n";
#endif

            #region Directory Nodes
            RARCFile.Position = DirectoryTableOffset;

            List<RARCDirEntry> FlatDirectoryList = new List<RARCDirEntry>();

            for (int i = 0; i < DirectoryCount; i++)
#if DEBUG
            {
                RARCDirEntry DEBUGTEMP = new RARCDirEntry(RARCFile, StringTableOffset);
                FlatDirectoryList.Add(DEBUGTEMP);
                long pauseposition = RARCFile.Position;
                RARCFile.Position = StringTableOffset + DEBUGTEMP.NameOffset;
                string DEBUGDIRNAME = RARCFile.ReadString();
                //XML += $"<RarcDirectoryEntry Name=" + ($"\"{DEBUGDIRNAME}\"").PadRight(20, ' ') + $" Type=\"{DEBUGTEMP.Type.PadLeft(4, ' ')}\" NameHash=\"0x{DEBUGTEMP.NameHash.ToString("X4")}\" FirstFileOffset=\"{DEBUGTEMP.FirstFileOffset}\" FileCount=\"{DEBUGTEMP.FileCount}\"/>\n";
                RARCFile.Position = pauseposition;
            }
#else
                FlatDirectoryList.Add(new RARCDirEntry(RARCFile, StringTableOffset));
#endif
            #endregion

            #region File Nodes
            List<RARCFileEntry> FlatFileList = new List<RARCFileEntry>();
            RARCFile.Seek(FileEntryTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < FileEntryCount; i++)
            {
                FlatFileList.Add(new RARCFileEntry()
                {
                    FileID = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0),
                    NameHash = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0),
                    Type = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0)
                });
                ushort CurrentNameOffset = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
                FlatFileList[FlatFileList.Count - 1].ModularA = BitConverter.ToInt32(RARCFile.ReadReverse(0, 4), 0);
                FlatFileList[FlatFileList.Count - 1].ModularB = BitConverter.ToInt32(RARCFile.ReadReverse(0, 4), 0);
                RARCFile.Position += 0x04;
                long Pauseposition = RARCFile.Position;
                RARCFile.Seek(StringTableOffset + CurrentNameOffset, SeekOrigin.Begin);
                FlatFileList[FlatFileList.Count - 1].Name = RARCFile.ReadString();
                RARCFile.Position = Pauseposition;
            }
//#if DEBUG
//            for (int i = 0; i < FlatFileList.Count; i++)
//                XML += $"<RarcFileEntry ID=\"{FlatFileList[i].FileID.ToString("000").PadLeft(4, '+')}\" Name=" + ($"\"{FlatFileList[i].Name}\"").PadRight(30, ' ') + $" Type=\"{FlatFileList[i].Type.ToString("X").PadLeft(4, '0')}\"\t FileOrDirectory=\"{FlatFileList[i].ModularA.ToString("X").PadLeft(8, '0')}\"\t Size=\"{FlatFileList[i].ModularB.ToString("X").PadLeft(8, '0')}\" />\n";

//            System.IO.File.WriteAllText("Original.xml", XML);
//#endif


            List<Directory> Directories = new List<Directory>();
            for (int i = 0; i < FlatDirectoryList.Count; i++)
            {
                Directories.Add(new Directory(i, FlatDirectoryList, FlatFileList, DataOffset, RARCFile));
            }

            for (int i = 0; i < Directories.Count; i++)
            {
                List<KeyValuePair<string, object>> templist = new List<KeyValuePair<string, object>>();
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
                            continue;
                        if (!Directories[fe.ModularA].Name.Equals(DirectoryItem.Key))
                            Directories[fe.ModularA].Name = DirectoryItem.Key;
                        templist.Add(new KeyValuePair<string, object>(DirectoryItem.Key, Directories[fe.ModularA]));
                    }
                    else
                        templist.Add(DirectoryItem);
                }
                Directories[i].Items = templist.ToDictionary(K => K.Key, V => V.Value);
            }
            #endregion
        }
        private List<byte> GetDataBytes(Directory Root, ref Dictionary<File, uint> Offsets, ref uint LocalOffset)
        {
            List<byte> DataBytes = new List<byte>();
            foreach (KeyValuePair<string, object> item in Root.Items)
            {
                if (item.Value is Directory dir)
                {
                    DataBytes.AddRange(GetDataBytes(dir, ref Offsets, ref LocalOffset));
                }
                else if (item.Value is File file)
                {
                    Offsets.Add(file, LocalOffset);
                    List<byte> temp = new List<byte>();
                    temp.AddRange(file.FileData);

                    while (temp.Count % 32 != 0)
                        temp.Add(0x00);
                    DataBytes.AddRange(temp);
                    LocalOffset += (uint)temp.Count;
                }
            }
            return DataBytes;
        }
        private List<RARCFileEntry> GetFlatFileList(Directory Root, Dictionary<File, uint> FileOffsets, ref short GlobalFileID, int CurrentFolderID, ref int NextFolderID, int BackwardsFolderID)
        {
            List<RARCFileEntry> FileList = new List<RARCFileEntry>();
            List<KeyValuePair<int, Directory>> Directories = new List<KeyValuePair<int, Directory>>();
            foreach (KeyValuePair<string, object> item in Root.Items)
            {
                if (item.Value is File file)
                {
                    FileList.Add(new RARCFileEntry() { FileID = GlobalFileID++, Name = file.Name, ModularA = (int)FileOffsets[file], ModularB = file.FileData.Length, Type = 0x1100 });
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
        private List<RARCDirEntry> GetFlatDirectoryList(Directory Root, ref uint FirstFileOffset)
        {
            List<RARCDirEntry> FlatDirectoryList = new List<RARCDirEntry>();
            List<RARCDirEntry> TemporaryList = new List<RARCDirEntry>();
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
        private List<byte> GetStringTableBytes(List<RARCFileEntry> FlatFileList, string RootName, ref Dictionary<string, uint> Offsets)
        {
            List<byte> strings = new List<byte>();
            Encoding enc = Encoding.GetEncoding(932);
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

        #region Junk
        //public void Save(string Filename = null)
        //{
        //    if (FileName == null && Filename == null)
        //        throw new Exception("No Filename has been given");
        //    else if (Filename != null)
        //        FileName = Filename;

        //    Root.BuildType(true);

        //    if (Root.Type != "ROOT")
        //        Root.Type = "ROOT";

        //    FileStream RARCFile = new FileStream(FileName, FileMode.Create);
        //    RARCFile.WriteString(Magic);
        //    RARCFile.Write(new byte[12] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x20, 0xDD, 0xDD, 0xDD, 0xDD }, 0, 12);
        //    RARCFile.Write(new byte[16] { 0xEE, 0xEE, 0xEE, 0xEE, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}, 0, 16);
        //    RARCFile.WriteReverse(BitConverter.GetBytes(Root.CountAllDirectories()+1), 0, 4);
        //    RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
        //    List<RARCDirectory> AllDirectories = Root.GetAllDirectories();
        //    List<KeyValuePair<uint, RARCDirectory>> AllDirIDs = new List<KeyValuePair<uint, RARCDirectory>> { new KeyValuePair<uint, RARCDirectory>(0, Root) };
        //    for (int i = 0; i < AllDirectories.Count; i++)
        //        AllDirIDs.Add(new KeyValuePair<uint, RARCDirectory>((uint)i + 1, AllDirectories[i]));
        //    int TotalFileCount = 0, TempCount = 0;
        //    List<int> FolderValues = new List<int>();
        //    for (int i = 0; i < AllDirIDs.Count; i++)
        //    {
        //        TempCount += 2;
        //        TempCount += AllDirIDs[i].Value.GetFiles().Count;
        //        TempCount += AllDirIDs[i].Value.GetDirectories().Count;
        //        FolderValues.Add(TempCount);
        //        TotalFileCount += TempCount;
        //        TempCount = 0;
        //    }
        //    RARCFile.WriteReverse(BitConverter.GetBytes(TotalFileCount), 0, 4);
        //    RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
        //    RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //String Table Size
        //    RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //string Table Location (-0x20)
        //    RARCFile.WriteReverse(BitConverter.GetBytes((ushort)TotalFileCount), 0, 2);
        //    RARCFile.Write(new byte[2] { 0x01, 0x00 }, 0, 2);
        //    RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
        //    long DirectoryEntryOffset = RARCFile.Position;
        //    RARCFile.WriteString(Root.Type);
        //    RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
        //    RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(Root.Name)), 0, 2);
        //    RARCFile.WriteReverse(BitConverter.GetBytes((ushort)FolderValues[0]), 0, 2);
        //    RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
        //    for (int i = 0; i < AllDirectories.Count; i++)
        //    {
        //        RARCFile.WriteString(AllDirectories[i].Type);
        //        RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
        //        RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(AllDirectories[i].Name)),0,2);
        //        RARCFile.WriteReverse(BitConverter.GetBytes((ushort)FolderValues[i + 1]), 0, 2);
        //        RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
        //    }
        //    #region Padding
        //    while (RARCFile.Position % 32 != 0)
        //        RARCFile.WriteByte(0x00);
        //    #endregion
        //    long FileEntryOffset = RARCFile.Position;

        //    List<RARCDirEntry> FinalDIRSetup = UnbuildFolder(AllDirIDs);

        //    List<RARCFileEntry> FinalFileIDs = new List<RARCFileEntry>();
        //    uint CurrentID = 0;

        //    for (int i = 0; i < FinalDIRSetup.Count; i++)
        //    {
        //        for (int j = 0; j < FinalDIRSetup[i].Files.Count; j++)
        //        {
        //            RARCFile.WriteReverse(BitConverter.GetBytes((ushort)FinalDIRSetup[i].Files[j].ID), 0, 2);
        //            RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(FinalDIRSetup[i].Files[j].Name)), 0, 2);
        //            RARCFile.Write(new byte[2] { 0x11, 0x00 }, 0, 2);
        //            RARCFile.Write(new byte[2] { 0xDD, 0xDD }, 0, 2);
        //            RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4);
        //            RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].Files[j].FileData.Length), 0, 4);
        //            RARCFile.Write(new byte[4], 0, 4);

        //            FinalFileIDs.Add(new RARCFileEntry() { FileID = (short)FinalDIRSetup[i].Files[j].ID, Name = FinalDIRSetup[i].Files[j].Name, ModularA = 0, ModularB = 0x10, Type = 0x1100 });
        //            CurrentID++;
        //        }
        //        for (int j = 0; j < FinalDIRSetup[i].SubDirIDs.Count; j++)
        //        {
        //            RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
        //            RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(FinalDIRSetup[(int)FinalDIRSetup[i].SubDirIDs[j]].Name)), 0, 2);
        //            RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
        //            RARCFile.Write(new byte[2] { 0xDD, 0xDD }, 0, 2);
        //            RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].SubDirIDs[j]), 0, 4);
        //            RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
        //            RARCFile.Write(new byte[4], 0, 4);
        //            FinalFileIDs.Add(new RARCFileEntry() { FileID = -1, Name = FinalDIRSetup[(int)FinalDIRSetup[i].SubDirIDs[j]].Name, ModularA = (int)FinalDIRSetup[(int)FinalDIRSetup[i].SubDirIDs[j]].ID, ModularB = 0x10, Type = 0x0200 });
        //            CurrentID++;
        //        }
        //        RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
        //        RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(".")), 0, 2);
        //        RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
        //        RARCFile.Write(new byte[2] { 0x00, 0x00 }, 0, 2);
        //        RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].ID), 0, 4);
        //        RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
        //        RARCFile.Write(new byte[4], 0, 4);
        //        FinalFileIDs.Add(new RARCFileEntry() { FileID = -1, Name = ".", ModularA = (int)FinalDIRSetup[i].ID, ModularB = 0x10, Type = 0x0200 });
        //        CurrentID++;

        //        RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
        //        RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash("..")), 0, 2);
        //        RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
        //        RARCFile.Write(new byte[2] { 0x00, 0x02 }, 0, 2);
        //        RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].ParentID), 0, 4);
        //        RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
        //        RARCFile.Write(new byte[4], 0, 4);
        //        FinalFileIDs.Add(new RARCFileEntry() { FileID = -1, Name = "..", ModularA = (int)FinalDIRSetup[i].ParentID, ModularB = 0x10, Type = 0x0200 });
        //        CurrentID++;
        //    }

        //    //string XML = "";
        //    //for (int i = 0; i < FinalFileIDs.Count; i++)
        //    //{
        //    //    XML += $"<RarcFileEntry ID=\"{FinalFileIDs[i].FileID}\"\t Name=\"{FinalFileIDs[i].Name}\"\t\t Type=\"{FinalFileIDs[i].Type.ToString("X").PadLeft(4, '0')}\"\t FileOrDirectory=\"{FinalFileIDs[i].ModularA.ToString("X").PadLeft(8, '0')}\"\t Size=\"{FinalFileIDs[i].ModularB.ToString("X").PadLeft(8, '0')}\" />\n";
        //    //}
        //    //File.WriteAllText("ONew.xml", XML);

        //    #region Padding
        //    while (RARCFile.Position % 32 != 0)
        //        RARCFile.WriteByte(0x00);
        //    #endregion
        //    long StringTableOffset = RARCFile.Position;
        //    List<string> StringTable = new List<string>() { ".", ".." };
        //    for (int i = 0; i < FinalDIRSetup.Count; i++)
        //    {
        //        if (!StringTable.Any(O => O.Equals(FinalDIRSetup[i].Name)))
        //            StringTable.Add(FinalDIRSetup[i].Name);
        //        for (int j = 0; j < FinalDIRSetup[i].Files.Count; j++)
        //            if (!StringTable.Any(O => O.Equals(FinalDIRSetup[i].Files[j].Name)))
        //                StringTable.Add(FinalDIRSetup[i].Files[j].Name);
        //    }
        //    for (int i = 0; i < StringTable.Count; i++)
        //        RARCFile.WriteString(StringTable[i], 0x00);

        //    #region Padding
        //    while (RARCFile.Position % 32 != 0)
        //        RARCFile.WriteByte(0x00);
        //    #endregion

        //    long FileTableOffset = RARCFile.Position;
        //    List<KeyValuePair<byte[], long>> FileOffsets = new List<KeyValuePair<byte[], long>>();

        //    for (int i = 0; i < FinalDIRSetup.Count; i++)
        //    {
        //        for (int j = 0; j < FinalDIRSetup[i].Files.Count; j++)
        //        {
        //            FileOffsets.Add(new KeyValuePair<byte[], long>(FinalDIRSetup[i].Files[j].FileData, RARCFile.Position - FileTableOffset));
        //            RARCFile.Write(FinalDIRSetup[i].Files[j].FileData, 0, FinalDIRSetup[i].Files[j].FileData.Length);
        //            #region Padding
        //            while (RARCFile.Position % 32 != 0)
        //                RARCFile.WriteByte(0x00);
        //            #endregion
        //        }
        //    }


        //    #region Offset Writing
        //    #region Header
        //    RARCFile.Position = 0x04;
        //    RARCFile.WriteReverse(BitConverter.GetBytes((int)RARCFile.Length), 0, 4);
        //    RARCFile.Position += 0x04;
        //    RARCFile.WriteReverse(BitConverter.GetBytes((int)(FileTableOffset - 0x20)), 0, 4);
        //    RARCFile.WriteReverse(BitConverter.GetBytes((int)(RARCFile.Length - FileTableOffset)), 0, 4);
        //    RARCFile.WriteReverse(BitConverter.GetBytes((int)(RARCFile.Length - FileTableOffset)), 0, 4);
        //    RARCFile.Position += 0x0C;
        //    RARCFile.WriteReverse(BitConverter.GetBytes((int)(DirectoryEntryOffset - 0x20)), 0, 4);
        //    RARCFile.Position += 0x04;
        //    RARCFile.WriteReverse(BitConverter.GetBytes((int)(FileEntryOffset - 0x20)), 0, 4);
        //    RARCFile.WriteReverse(BitConverter.GetBytes((int)(FileTableOffset - StringTableOffset)), 0, 4);
        //    RARCFile.WriteReverse(BitConverter.GetBytes((int)(StringTableOffset - 0x20)), 0, 4);
        //    #endregion

        //    #region Directory Entries
        //    RARCFile.Position = DirectoryEntryOffset;
        //    bool breakout = false;
        //    for (int i = 0; i < FinalDIRSetup.Count; i++)
        //    {
        //        RARCFile.Position += 0x04;
        //        RARCFile.WriteReverse(BitConverter.GetBytes(GetStringOffset(StringTable, FinalDIRSetup[i].Name)), 0, 4);
        //        RARCFile.Position += 0x04;
        //        int j = 0;
        //        for (j = 0; j < FinalFileIDs.Count; j++)
        //        {
        //            if (FinalDIRSetup[i].SubDirIDs.Count == 0 && FinalDIRSetup[i].Files.Count == 0)
        //            {
        //                for (int x = 0; x < FinalFileIDs.Count; x++)
        //                {
        //                    if ((FinalFileIDs[x].Type == 0x0200 && FinalFileIDs[x].Name.Equals(".")) && (FinalFileIDs[x].ModularA == FinalDIRSetup[i].ID))
        //                    {
        //                        RARCFile.WriteReverse(BitConverter.GetBytes(x), 0, 4);
        //                        breakout = true;
        //                        break;
        //                    }
        //                }
        //                if (breakout)
        //                    break;
        //            }
        //            else if (FinalDIRSetup[i].Files.Count == 0)
        //            {
        //                if (FinalFileIDs[j].ModularA == FinalDIRSetup[i].SubDirIDs[0])
        //                {
        //                    RARCFile.WriteReverse(BitConverter.GetBytes(j), 0, 4);
        //                    break;
        //                }
        //            }
        //            else
        //            {
        //                if (FinalFileIDs[j].FileID == FinalDIRSetup[i].Files[0].ID)
        //                {
        //                    RARCFile.WriteReverse(BitConverter.GetBytes(j), 0, 4);
        //                    break;
        //                }
        //            }
        //        }
        //    }
        //    ;
        //    #endregion

        //    #region File entries
        //    RARCFile.Position = FileEntryOffset;
        //    for (int i = 0; i < FinalDIRSetup.Count; i++)
        //    {
        //        for (int j = 0; j < FinalDIRSetup[i].Files.Count; j++)
        //        {
        //            RARCFile.Position += 0x06;
        //            RARCFile.WriteReverse(BitConverter.GetBytes((ushort)GetStringOffset(StringTable, FinalDIRSetup[i].Files[j].Name)), 0, 2);
        //            for (int x = 0; x < FileOffsets.Count; x++)
        //            {
        //                if (FileOffsets[x].Key == FinalDIRSetup[i].Files[j].FileData)
        //                {
        //                    RARCFile.WriteReverse(BitConverter.GetBytes((uint)FileOffsets[x].Value), 0, 4);
        //                    break;
        //                }
        //            }
        //            RARCFile.Position += 0x08;
        //        }
        //        for (int j = 0; j < FinalDIRSetup[i].SubDirIDs.Count; j++)
        //        {
        //            RARCFile.Position += 0x06;
        //            RARCFile.WriteReverse(BitConverter.GetBytes((ushort)GetStringOffset(StringTable, FinalDIRSetup[(int)FinalDIRSetup[i].SubDirIDs[j]].Name)), 0, 2);
        //            RARCFile.Position += 0x0C;
        //        }
        //        RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
        //        RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(".")), 0, 2);
        //        RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
        //        RARCFile.Write(new byte[2] { 0x00, 0x00 }, 0, 2);
        //        RARCFile.WriteReverse(BitConverter.GetBytes(i), 0, 4);
        //        RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
        //        RARCFile.Write(new byte[4], 0, 4);

        //        RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
        //        RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash("..")), 0, 2);
        //        RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
        //        RARCFile.Write(new byte[2] { 0x00, 0x02 }, 0, 2);
        //        RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].ParentID), 0, 4);
        //        RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
        //        RARCFile.Write(new byte[4], 0, 4);
        //    }
        //    #endregion
        //    #endregion

        //    RARCFile.Close();
        //}
        //private RARCDirectory BuildFolder(List<RARCDirEntry> AllDirectories)
        //{
        //    RARCDirectory ROOT = new RARCDirectory();

        //    for (int i = 0; i < AllDirectories.Count; i++)
        //    {
        //        if (AllDirectories[i].ParentID == 0xFFFFFFFF)
        //        {
        //            ROOT = new RARCDirectory() { ID = AllDirectories[i].ID, Type = AllDirectories[i].Type, Name = AllDirectories[i].Name };
        //            for (int x = 0; x < AllDirectories[i].Files.Count; x++)
        //            {
        //                ROOT.Items.Add(AllDirectories[i].Files[x].Name, AllDirectories[i].Files[x]);
        //            }
        //            break;
        //        }
        //    }

        //    for (int i = 0; i < AllDirectories.Count; i++)
        //    {
        //        if (AllDirectories[i].ParentID == 0)
        //        {
        //            RARCDirectory dir = new RARCDirectory() { ID = AllDirectories[i].ID, Type = AllDirectories[i].Type, Name = AllDirectories[i].Name };
        //            for (int x = 0; x < AllDirectories[i].Files.Count; x++)
        //            {
        //                dir.Items.Add(AllDirectories[i].Files[x].Name, AllDirectories[i].Files[x]);
        //            }
        //            ROOT.Items.Add(AllDirectories[i].Name, dir);
        //        }
        //        else if (AllDirectories[i].ParentID != 0xFFFFFFFF)
        //        {
        //            SetDirList(ROOT, AllDirectories[i]);
        //        }
        //    }

        //    return ROOT;
        //}

        //private List<RARCDirEntry> UnbuildFolder(List<KeyValuePair<uint, RARCDirectory>> AllDirs)
        //{
        //    List<RARCDirEntry> Data = new List<RARCDirEntry>();

        //    #region Setup the Directory Entries
        //    for (int i = 0; i < AllDirs.Count; i++)
        //    {
        //        Dictionary<string, RARCFile> FileList = AllDirs[i].Value.GetFiles();
        //        Dictionary<string, RARCDirectory> DirectoryList = AllDirs[i].Value.GetDirectories();
        //        RARCDirEntry Dir = new RARCDirEntry() { Files = FileList.Values.ToList(), Name = AllDirs[i].Value.Name, Type = AllDirs[i].Value.Type, ID = (uint)i };
        //        foreach (KeyValuePair<string, RARCDirectory> CurrentDir in DirectoryList)
        //            for (int x = 0; x < AllDirs.Count; x++)
        //                if (AllDirs[x].Value == CurrentDir.Value)
        //                    Dir.SubDirIDs.Add(AllDirs[x].Key);

        //        Data.Add(Dir);
        //    } 
        //    #endregion

        //    #region Setup File IDs
        //    int FileID = 0;
        //    for (int i = 0; i < Data.Count; i++)
        //    {
        //        for (int j = 0; j < Data[i].Files.Count; j++)
        //        {
        //            Data[i].Files[j].ID = FileID;
        //            FileID++;
        //        }
        //        //FileID += 3;
        //    }
        //    #endregion

        //    #region Setup Parent IDs
        //    for (int i = 0; i < AllDirs.Count; i++)
        //    {
        //        Dictionary<string, RARCFile> FileList = AllDirs[i].Value.GetFiles();
        //        Dictionary<string, RARCDirectory> DirectoryList = AllDirs[i].Value.GetDirectories();
        //        RARCDirEntry Dir = new RARCDirEntry() { Files = FileList.Values.ToList(), Name = AllDirs[i].Value.Name, Type = AllDirs[i].Value.Type };
        //        foreach (KeyValuePair<string, RARCDirectory> CurrentDir in DirectoryList)
        //            for (int x = 0; x < AllDirs.Count; x++)
        //                if (AllDirs[x].Value == CurrentDir.Value)
        //                {
        //                    Data[x].ParentID = (uint)i;
        //                    break;
        //                }
        //    } 
        //    #endregion

        //    return Data;
        //}

        //private void SetDirList(RARCDirectory Root, RARCDirEntry SubDir)
        //{
        //    Dictionary<string, RARCFile> FileList = Root.GetFiles();
        //    Dictionary<string, RARCDirectory> DirectoryList = Root.GetDirectories();
        //    foreach (KeyValuePair<string, RARCDirectory> CurrentDir in DirectoryList)
        //    {
        //        if (CurrentDir.Value.ID == SubDir.ParentID)
        //        {
        //            CurrentDir.Value.Items.Add(SubDir.Name, new RARCDirectory() { ID = SubDir.ID, Items = SubDir.Files.ToDictionary(key => key.Name, x => (object)x), Type = SubDir.Type, Name = SubDir.Name });
        //        }
        //        SetDirList(CurrentDir.Value, SubDir);
        //    }
        //}

        //private uint GetStringOffset(List<string> StringTable, string Target)
        //{
        //    int Offset = 0;
        //    for (int i = 0; i < StringTable.Count; i++)
        //    {
        //        if (StringTable[i].Equals(Target))
        //            break;

        //        Offset += StringTable[i].Length + 1;
        //    }
        //    return (uint)Offset;
        //}

        //Old Reading Code
        //private void Read(Stream RARCFile)
        //{
        //    if (RARCFile.ReadString(4) != Magic)
        //        throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

        //    uint FileSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0), TrashData = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
        //        DataOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;

        //    RARCFile.Position += 0x10;
        //    uint DirectoryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0), DirectoryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20,
        //        FileEntryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0), FileEntryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;
        //    RARCFile.Position += 0x04;
        //    uint StringTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;

        //    List<RARCFile> AllFiles = new List<RARCFile>();
        //    List<RARCDirEntry> AllDirectories = new List<RARCDirEntry>();

        //    #region Unused Debugging
        //    List<RARCFileEntry> TEST = new List<RARCFileEntry>();
        //    RARCFile.Seek(FileEntryTableOffset, SeekOrigin.Begin);
        //     for (int i = 0; i < FileEntryCount; i++)
        //     {
        //         TEST.Add(new RARCFileEntry()
        //         {
        //             FileID = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0),
        //             NameHash = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0),
        //             Type = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0)
        //         });
        //         ushort CurrentNameOffset = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
        //         TEST[TEST.Count - 1].ModularA = BitConverter.ToInt32(RARCFile.ReadReverse(0, 4), 0);
        //         TEST[TEST.Count - 1].ModularB = BitConverter.ToInt32(RARCFile.ReadReverse(0, 4), 0);
        //         RARCFile.Position += 0x04;
        //         long Pauseposition = RARCFile.Position;
        //         RARCFile.Seek(StringTableOffset + CurrentNameOffset, SeekOrigin.Begin);
        //         TEST[TEST.Count - 1].Name = RARCFile.ReadString();
        //         RARCFile.Position = Pauseposition;
        //     }
        //     string XML = "";
        //     for (int i = 0; i < TEST.Count; i++)
        //     {
        //         XML += $"<RarcFileEntry ID=\"{TEST[i].FileID.ToString("000").PadLeft(4,'+')}\" Name=" + ($"\"{TEST[i].Name}\"").PadRight(20, ' ') + $" Type=\"{TEST[i].Type.ToString("X").PadLeft(4, '0')}\"\t FileOrDirectory=\"{TEST[i].ModularA.ToString("X").PadLeft(8, '0')}\"\t Size=\"{TEST[i].ModularB.ToString("X").PadLeft(8, '0')}\" />\n";
        //     }
        //     File.WriteAllText("Original.xml", XML);

        //    #endregion

        //    for (int i = 0; i < DirectoryCount; i++)
        //    {
        //        RARCFile.Seek(DirectoryTableOffset + (i * 0x10), SeekOrigin.Begin);
        //        RARCDirEntry Dir = new RARCDirEntry() { Type = RARCFile.ReadString(4) };
        //        uint NameOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);
        //        ushort NameHash = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0), FileCount = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
        //        uint FileFirstIndex = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);
        //        RARCFile.Seek(StringTableOffset + NameOffset, SeekOrigin.Begin);
        //        Dir.Name = RARCFile.ReadString();

        //        List<uint> SubDirIDs = new List<uint>();
        //        List<RARCFile> FolderFilesList = new List<RARCFile>();

        //        uint FileEntryID = FileEntryTableOffset + (FileFirstIndex * 0x14);
        //        for (int j = 0; j < FileCount; j++)
        //        {
        //            RARCFile.Seek(FileEntryID + (j * 0x14), SeekOrigin.Begin);
        //            ushort FileID = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0), CurrentNameHash = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
        //            byte Flags = (byte)RARCFile.ReadByte();
        //            RARCFile.ReadByte();
        //            ushort CurrentNameOffset = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
        //            uint CurrentEntryDataOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0), CurrentEntryDataSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);
        //            RARCFile.Seek(StringTableOffset + CurrentNameOffset, SeekOrigin.Begin);
        //            string CurrentName = RARCFile.ReadString();

        //            if (CurrentName == ".")
        //            {
        //                Dir.ID = CurrentEntryDataOffset;
        //                continue;
        //            }
        //            if (CurrentName == "..")
        //            {
        //                Dir.ParentID = CurrentEntryDataOffset;
        //                continue;
        //            }

        //            bool IsDirectory = (Flags & 0x02) != 0;
        //            if (IsDirectory)
        //            {
        //                SubDirIDs.Add(CurrentEntryDataOffset);
        //            }
        //            else
        //            {
        //                uint TotalOffset = DataOffset + CurrentEntryDataOffset;
        //                RARCFile.Seek(TotalOffset, SeekOrigin.Begin);
        //                RARCFile File = new RARCFile() { ID = FileID, Name = CurrentName, FileData = RARCFile.Read(0, (int)CurrentEntryDataSize) };
        //                FolderFilesList.Add(File);
        //                AllFiles.Add(File);
        //            }
        //        }
        //        Dir.Files.AddRange(FolderFilesList);
        //        Dir.SubDirIDs.AddRange(SubDirIDs);
        //        AllDirectories.Add(Dir);

        //    }
        //    Root = BuildFolder(AllDirectories);
        //} 
        #endregion
    }

    //public class RARCFile
    //{
    //    /// <summary>
    //    /// ID of the File. Set during the saving process
    //    /// </summary>
    //    public int ID { get; internal set; }
    //    /// <summary>
    //    /// Name of the File
    //    /// </summary>
    //    public string Name { get; set; }
    //    /// <summary>
    //    /// The extension of this file
    //    /// </summary>
    //    public string Extension { get
    //        {
    //            string[] parts = Name.Split('.');
    //            return "."+parts[parts.Length - 1].ToLower();
    //        }
    //    }
    //    /// <summary>
    //    /// The Actual Data for the file
    //    /// </summary>
    //    public byte[] FileData { get; set; }
    //    /// <summary>
    //    /// Create a new RARC File
    //    /// </summary>
    //    public RARCFile() { }
    //    /// <summary>
    //    /// Create a new RARC File from a File
    //    /// </summary>
    //    /// <param name="Filename"></param>
    //    public RARCFile(string Filename)
    //    {
    //        Name = new FileInfo(Filename).Name;
    //        FileData = File.ReadAllBytes(Filename);
    //    }
    //    /// <summary>
    //    /// Saves the File to the Disk.
    //    /// <para/>WARNING: The file will always try to overwrite what is already there
    //    /// </summary>
    //    /// <param name="Folderpath">Folder to save this file to</param>
    //    /// <param name="NewName">Override of the Actual Filename</param>
    //    /// <exception cref="IOException">Thrown if the Folderpath doesn't exist OR if the given NewName cannot be written to</exception>
    //    /// <exception cref="ArgumentNullException">Thrown if the <see cref="Name"/> of this file is Null or empty and if the Parameter <paramref name="NewName"/> was not assigned</exception>
    //    public void Save(string Folderpath, string NewName = null)
    //    {
    //        if ((Name == null || Name == "") && (NewName == null || NewName == ""))
    //            throw new ArgumentNullException($"Arguments 'Name' & 'NewName' are NULL!");
    //        File.WriteAllBytes(Path.Combine(Folderpath, NewName ?? Name), FileData);
    //    }
    //    /// <summary>
    //    /// Get a <see cref="MemoryStream"/> of this file. This <see cref="MemoryStream"/> does not contain anything other than the <see cref="FileData"/>
    //    /// </summary>
    //    /// <returns></returns>
    //    public MemoryStream GetMemoryStream() => new MemoryStream(FileData, 0, FileData.Length, false, true);
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    /// <returns></returns>
    //    public override string ToString() => $"({ID}) {Name} - 0x{FileData.Length.ToString("X").PadLeft(8, '0')}";
    //}

    //public class RARCDirectory
    //{
    //    /// <summary>
    //    /// Name of the Directory
    //    /// </summary>
    //    public string Name { get; set; }
    //    /// <summary>
    //    /// ID of the Directory
    //    /// <para/>Not used when saving
    //    /// </summary>
    //    internal uint ID { get; set; }
    //    /// <summary>
    //    /// Directory Type. usually the first 4 letters of the <see cref="Name"/>. If the <see cref="Name"/> is shorter than 4, the missing spots will be ' ' (space)
    //    /// <para/>Set automatically upon saving the Archive
    //    /// </summary>
    //    public string Type { get; internal set; }
    //    /// <summary>
    //    /// Create an empty Directory
    //    /// </summary>
    //    public RARCDirectory() { }
    //    internal RARCDirectory(string FolderPath)
    //    {
    //        DirectoryInfo DI = new DirectoryInfo(FolderPath);
    //        Name = DI.Name;
    //        Type = new string(new char[4] { Name.ToUpper().PadRight(4, ' ')[0], Name.ToUpper().PadRight(4, ' ')[1], Name.ToUpper().PadRight(4, ' ')[2], Name.ToUpper().PadRight(4, ' ')[3] });
    //        CreateFromFolder(FolderPath, 0);
    //    }
    //    private RARCDirectory(string FolderPath, int FileID)
    //    {
    //        DirectoryInfo DI = new DirectoryInfo(FolderPath);
    //        Name = DI.Name;
    //        Type = new string(new char[4] { Name.ToUpper().PadRight(4, ' ')[0], Name.ToUpper().PadRight(4, ' ')[1], Name.ToUpper().PadRight(4, ' ')[2], Name.ToUpper().PadRight(4, ' ')[3] });
    //        CreateFromFolder(FolderPath, FileID);
    //    }

    //    public RARCFile GetFile(string FilePath, bool CaseInsensitive = false)
    //    {
    //        string[] Segments = FilePath.Split('/');
    //        if (Segments.Length > 1)
    //        {
    //            Dictionary<string, RARCDirectory> Subdirs = GetDirectories();
    //            foreach (KeyValuePair<string, RARCDirectory> Entry in Subdirs)
    //            {
    //                if (!(CaseInsensitive ? Entry.Key.ToLower().Equals(Segments[0].ToLower()) : Entry.Key.Equals(Segments[0])))
    //                    continue;
    //                RARCFile temp = Entry.Value.GetFile(FilePath.Replace(Segments[0] + "/", ""), CaseInsensitive);
    //                if (temp != null)
    //                {
    //                    return temp;
    //                }
    //            }
    //        }
    //        else
    //        {
    //            Dictionary<string, RARCFile> files = GetFiles();
    //            foreach (KeyValuePair<string, RARCFile> file in files)
    //            {
    //                if (CaseInsensitive ? file.Value.Name.ToLower().Equals(Segments[0].ToLower()) : file.Value.Name.Equals(Segments[0]))
    //                {
    //                    return file.Value;
    //                }
    //            }
    //        }
    //        return null;
    //    }

    //    public bool SetFile(string FilePath, RARCFile File, bool CaseInsensitive = false)
    //    {
    //        string[] Segments = FilePath.Split('/');
    //        if (Segments.Length > 1)
    //        {
    //            Dictionary<string, RARCDirectory> Subdirs = GetDirectories();
    //            foreach (KeyValuePair<string, RARCDirectory> Entry in Subdirs)
    //            {
    //                if (!(CaseInsensitive ? Entry.Key.ToLower().Equals(Segments[0].ToLower()) : Entry.Key.Equals(Segments[0])))
    //                    continue;
    //                if (Entry.Value.SetFile(FilePath.Replace(Segments[0] + "/", ""), File, CaseInsensitive))
    //                    return true;
    //            }
    //        }
    //        else
    //        {
    //            foreach (KeyValuePair<string, object> file in Items)
    //            {
    //                if (file.Value is RARCFile currentfile && (CaseInsensitive ? currentfile.Name.ToLower().Equals(Segments[0].ToLower()) : currentfile.Name.Equals(Segments[0])))
    //                {
    //                    Items[file.Key] = File;
    //                    return true;
    //                }
    //            }
    //        }
    //        return false;
    //    }

    //    public bool RenameItem(string FilePath, string NewName, bool CaseInsensitive = false)
    //    {
    //        string[] Segments = FilePath.Split('/');
    //        if (Segments.Length > 1)
    //        {
    //            Dictionary<string, RARCDirectory> Subdirs = GetDirectories();
    //            foreach (KeyValuePair<string, RARCDirectory> Entry in Subdirs)
    //            {
    //                if (!(CaseInsensitive ? Entry.Key.ToLower().Equals(Segments[0].ToLower()) : Entry.Key.Equals(Segments[0])))
    //                    continue;
    //                if (Entry.Value.RenameItem(FilePath.Replace(Segments[0] + "/", ""), NewName, CaseInsensitive))
    //                    return true;
    //            }
    //        }
    //        else
    //        {
    //            foreach (KeyValuePair<string, object> file in Items)
    //            {
    //                if (file.Value is RARCFile currentfile && (CaseInsensitive ? currentfile.Name.ToLower().Equals(Segments[0].ToLower()) : currentfile.Name.Equals(Segments[0])))
    //                {
    //                    Items.Remove(file.Key);
    //                    Items.Add(NewName, file.Value);
    //                    return true;
    //                }
    //                else if (file.Value is RARCDirectory currentdirectory && (CaseInsensitive ? currentdirectory.Name.ToLower().Equals(Segments[0].ToLower()) : currentdirectory.Name.Equals(Segments[0])))
    //                {
    //                    Items.Remove(file.Key);
    //                    Items.Add(NewName, file.Value);
    //                    return true;
    //                }
    //            }
    //        }
    //        return false;
    //    }

    //    public bool CreateItemPath(string FilePath, RARCFile File, bool CaseInsensitive = false)
    //    {
    //        string[] Segments = FilePath.Split('/');
    //        if (Segments.Length > 1)
    //        {
    //            Dictionary<string, RARCDirectory> Subdirs = GetDirectories();
    //            foreach (KeyValuePair<string, RARCDirectory> Entry in Subdirs)
    //            {
    //                if (!(CaseInsensitive ? Entry.Key.ToLower().Equals(Segments[0].ToLower()) : Entry.Key.Equals(Segments[0])))
    //                    continue;
    //                if (Entry.Value.CreateItemPath(FilePath.Replace(Segments[0] + "/", ""), File, CaseInsensitive))
    //                    return true;
    //            }
    //            RARCDirectory newdir = new RARCDirectory() { Name = Segments[0] };
    //            newdir.CreateItemPath(FilePath.Replace(Segments[0] + "/", ""), File, CaseInsensitive);
    //            Items.Add(Segments[0], newdir);
    //            return true;
    //        }
    //        else
    //        {
    //            foreach (KeyValuePair<string, object> file in Items)
    //            {
    //                if (file.Value is RARCFile currentfile && (CaseInsensitive ? currentfile.Name.ToLower().Equals(Segments[0].ToLower()) : currentfile.Name.Equals(Segments[0])))
    //                {
    //                    Items[file.Key] = File;
    //                    return true;
    //                }
    //            }
    //            Items.Add(Segments[0], File);
    //            return true;
    //        }
    //    }

    //    public bool DeleteItem(string FilePath, bool CaseInsensitive = false)
    //    {
    //        string[] Segments = FilePath.Split('/');
    //        if (Segments.Length > 1)
    //        {
    //            Dictionary<string, RARCDirectory> Subdirs = GetDirectories();
    //            foreach (KeyValuePair<string, RARCDirectory> Entry in Subdirs)
    //            {
    //                if (!(CaseInsensitive ? Entry.Key.ToLower().Equals(Segments[0].ToLower()) : Entry.Key.Equals(Segments[0])))
    //                    continue;
    //                if (Entry.Value.DeleteItem(FilePath.Replace(Segments[0] + "/", ""), CaseInsensitive))
    //                    return true;
    //            }
    //        }
    //        else
    //        {
    //            foreach (KeyValuePair<string, object> file in Items)
    //            {
    //                if (file.Value is RARCFile currentfile && (CaseInsensitive ? currentfile.Name.ToLower().Equals(Segments[0].ToLower()) : currentfile.Name.Equals(Segments[0])))
    //                {
    //                    Items.Remove(file.Key);
    //                    return true;
    //                }
    //                else if (file.Value is RARCDirectory currentdirectory && (CaseInsensitive ? currentdirectory.Name.ToLower().Equals(Segments[0].ToLower()) : currentdirectory.Name.Equals(Segments[0])))
    //                {
    //                    Items.Remove(file.Key);
    //                    return true;
    //                }
    //            }
    //        }
    //        return false;
    //    }

    //    public RARCDirectory GetDirectory(string FilePath, bool CaseInsensitive = false)
    //    {
    //        string[] Segments = FilePath.Split('/');
    //        if (Segments.Length > 1)
    //        {
    //            Dictionary<string, RARCDirectory> Subdirs = GetDirectories();
    //            foreach (KeyValuePair<string, RARCDirectory> Entry in Subdirs)
    //            {
    //                if (!(CaseInsensitive ? Entry.Key.ToLower().Equals(Segments[0].ToLower()) : Entry.Key.Equals(Segments[0])))
    //                    continue;
    //                RARCDirectory temp = Entry.Value.GetDirectory(FilePath.Replace(Segments[0] + "/", ""), CaseInsensitive);
    //                if (temp != null)
    //                {
    //                    return temp;
    //                }
    //            }
    //        }
    //        else
    //        {
    //            Dictionary<string, RARCDirectory> Subdirs = GetDirectories();
    //            foreach (KeyValuePair<string, RARCDirectory> file in Subdirs)
    //            {
    //                if (CaseInsensitive ? file.Value.Name.ToLower().Equals(Segments[0].ToLower()) : file.Value.Name.Equals(Segments[0]))
    //                {
    //                    return file.Value;
    //                }
    //            }
    //        }
    //        return null;
    //    }

    //    internal List<KeyValuePair<string, RARCFile>> SearchFile(string Filename, bool CaseInsensitive, string Currentpath)
    //    {
    //        List<KeyValuePair<string, RARCFile>> Found = new List<KeyValuePair<string, RARCFile>>();
    //        foreach (KeyValuePair<string, object> Item in Items)
    //        {
    //            if (Item.Value is RARCDirectory Dir)
    //            {
    //                Found.AddRange(Dir.SearchFile(Filename, CaseInsensitive, Currentpath+$"{Name}/"));
    //            }
    //            else if (Item.Value is RARCFile file)
    //            {
    //                if (CaseInsensitive ? file.Name.ToLower().Equals(Filename.ToLower()) : file.Name.Equals(Filename))
    //                    Found.Add(new KeyValuePair<string, RARCFile>(Currentpath, file));
    //            }
    //        }

    //        return Found;
    //    }

    //    /// <summary>
    //    /// Export this Directory to a folder.
    //    /// </summary>
    //    /// <param name="FolderPath">Folder to Export to. Don't expect the files to appear here. Expect a Folder with this <see cref="Name"/> to appear</param>
    //    public void Export(string FolderPath)
    //    {
    //        //Files.ForEach(File => File.Save(FolderPath));
    //        //SubDirectories.ForEach(delegate (RARCDirectory Dir)
    //        //{
    //        //    string newstring = Path.Combine(FolderPath, Dir.Name);
    //        //    Directory.CreateDirectory(newstring);
    //        //    Dir.Export(newstring);
    //        //});
    //    }
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    /// <returns></returns>
    //    public override string ToString()
    //    {
    //        int Count = GetFiles().Count;
    //        return $"({Type}) {Name}{(Count > 0 ? $" {Count} File{(Count > 1 ? "s" : "")}" : "")}|{(GetDirectories().Count > 0 ? $" {GetDirectories().Count} Sub Directory{(GetDirectories().Count > 1 ? "s" : "")}" : "")}";
    //    }
    //    /// <summary>
    //    /// Find what index a given folder has
    //    /// </summary>
    //    /// <param name="foldername">Name of the folder to find</param>
    //    /// <returns></returns>
    //    public int GetSubDirIndex(string foldername)
    //    {
    //        //if (SubDirectories.Count == 0)
    //        //    return -1;

    //        //for (int i = 0; i < SubDirectories.Count; i++)
    //        //    if (SubDirectories[i].Name.ToLower().Equals(foldername.ToLower()))
    //        //        return i;
    //        return -1;
    //    }

    //    private void CreateFromFolder(string FolderPath, int fileid)
    //    {
    //        //string[] Found = Directory.GetFiles(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
    //        //for (int i = 0; i < Found.Length; i++)
    //        //{
    //        //    Files.Add(new RARCFile(Found[i]) { ID = fileid });
    //        //    fileid++;
    //        //}

    //        //string[] SubDirs = Directory.GetDirectories(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
    //        //for (int i = 0; i < SubDirs.Length; i++)
    //        //{
    //        //    SubDirectories.Add(new RARCDirectory(SubDirs[i], fileid));
    //        //}
    //    }


    //    internal int CountAllFiles()
    //    {
    //        Dictionary<string, RARCDirectory> Dict = GetDirectories();
    //        int Count = GetFiles().Count;
    //        foreach (KeyValuePair<string, RARCDirectory> Entry in Dict)
    //            Count += Entry.Value.CountAllFiles();
    //        return Count;
    //    }
    //    internal int CountAllDirectories()
    //    {
    //        Dictionary<string, RARCDirectory> Dict = GetDirectories();
    //        int Count = Dict.Count;
    //        foreach (KeyValuePair<string, RARCDirectory> Entry in Dict)
    //            Count += Entry.Value.CountAllDirectories();
    //        return Count;
    //    }
    //    /// <summary>
    //    /// Rebuilds all the TYPE values
    //    /// </summary>
    //    /// <param name="Recursive">If false or unprovided, only change the top layer</param>
    //    internal void BuildType(bool Recursive = false)
    //    {
    //        Type = new string(new char[4] { Name.ToUpper().PadRight(4, ' ')[0], Name.ToUpper().PadRight(4, ' ')[1], Name.ToUpper().PadRight(4, ' ')[2], Name.ToUpper().PadRight(4, ' ')[3] });
    //        if (Recursive)
    //        {
    //            Dictionary<string, RARCDirectory> DirectoryList = GetDirectories();
    //            foreach (KeyValuePair<string, RARCDirectory> CurrentDir in DirectoryList)
    //            {
    //                CurrentDir.Value.BuildType(Recursive);
    //            }
    //        }
    //    }

    //    public Dictionary<string, object> Items { get; set; } = new Dictionary<string, object>();
    //    public Dictionary<string, RARCFile> GetFiles()
    //    {
    //        Dictionary<string, RARCFile> result = new Dictionary<string, RARCFile>();
    //        foreach (KeyValuePair<string, object> Item in Items)
    //        {
    //            if (Item.Value is RARCFile x)
    //                result.Add(Item.Key, x);
    //        }
    //        return result;
    //    }
    //    public Dictionary<string, RARCDirectory> GetDirectories()
    //    {
    //        Dictionary<string, RARCDirectory> result = new Dictionary<string, RARCDirectory>();
    //        foreach (KeyValuePair<string, object> Item in Items)
    //        {
    //            if (Item.Value is RARCDirectory x)
    //                result.Add(Item.Key, x);
    //        }
    //        return result;
    //    }
    //    public KeyValuePair<string, object> GetItem(string Key, bool CaseInsensitive = false)
    //    {
    //        foreach (KeyValuePair<string, object> Item in Items)
    //        {
    //            if (CaseInsensitive ? ((dynamic)Item.Value).Name.ToLower().Equals(Key.ToLower()) : ((dynamic)Item.Value).Name.Equals(Key))
    //                return Item;
    //        }
    //        return new KeyValuePair<string, object>(null, null);
    //    }
    //    public bool ItemExists(string Key, bool CaseInsensitive = false)
    //    {
    //        foreach (KeyValuePair<string, object> Item in Items)
    //        {
    //            if (CaseInsensitive ? ((dynamic)Item.Value).Name.ToLower().Equals(Key.ToLower()) : ((dynamic)Item.Value).Name.Equals(Key))
    //                return true;
    //        }
    //        return false;
    //    }

    //    internal List<RARCDirectory> GetAllDirectories()
    //    {
    //        List<RARCDirectory> Found = new List<RARCDirectory>();
    //        foreach (KeyValuePair<string, object> Item in Items)
    //        {
    //            if (Item.Value is RARCDirectory dir)
    //            {
    //                Found.Add(dir);
    //                Found.AddRange(dir.GetAllDirectories());
    //            }
    //        }
    //        return Found;
    //    }
    //}

    //internal class RARCDirEntry
    //{
    //    /// <summary>
    //    /// Directory Name
    //    /// </summary>
    //    public string Name { get; set; }
    //    /// <summary>
    //    /// Directory Type. usually the first 4 letters of the <see cref="Name"/>. If the <see cref="Name"/> is shorter than 4, the missing spots will be ' ' (space)
    //    /// </summary>
    //    public string Type { get; set; }
    //    /// <summary>
    //    /// List of Files in this directory
    //    /// </summary>
    //    public List<RARCFile> Files { get; set; } = new List<RARCFile>();
    //    /// <summary>
    //    /// ID's for any Subdirectories
    //    /// </summary>
    //    public List<uint> SubDirIDs { get; set; } = new List<uint>();
    //    /// <summary>
    //    /// ID of the parent to this Directory
    //    /// <para/>0xFFFFFFFF if this is the Root Directory
    //    /// </summary>
    //    public uint ParentID { get; set; } = 0xFFFFFFFF;
    //    /// <summary>
    //    /// ID of this directory
    //    /// </summary>
    //    /// <returns></returns>
    //    public uint ID { get; set; } = 0x00;
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    /// <returns></returns>
    //    public override string ToString() => $"({Type}) {Name}{(Files.Count > 0 ? $" {Files.Count} Files":"")}";
    //}




    //    public class RARC2
    //    {
    //        /// <summary>
    //        /// Filepath of this Archive. NULL if the archive doesn't exist yet.
    //        /// </summary>
    //        public string Filepath { get; private set; } = null;
    //        /// <summary>
    //        /// Get the name of the archive without the path
    //        /// </summary>
    //        public string Name { get { return Filepath == null ? Filepath : new FileInfo(Filepath).Name; } }
    //        /// <summary>
    //        /// The Root Directory of the Archive
    //        /// </summary>
    //        public RARCDirectory Root { get; set; }
    //        /// <summary>
    //        /// File Identifier
    //        /// </summary>
    //        private readonly string Magic = "RARC";

    //        public RARC2() { }
    //        public RARC2(string Filepath)
    //        {
    //            FileStream fs = new FileStream(Filepath, FileMode.Open);
    //            Read(fs);
    //            fs.Close();
    //        }
    //        public RARC2(Stream RARCFile) => Read(RARCFile);

    //        private void Read(Stream RARCFile)
    //        {
    //            if (RARCFile.ReadString(4) != Magic)
    //                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");
    //            uint FileSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
    //                TrashData = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
    //                DataOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;
    //            RARCFile.Position += 0x10; //Skip the Lengths and Unknowns
    //            uint DirectoryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
    //                DirectoryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20,
    //                FileEntryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
    //                FileEntryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;
    //            RARCFile.Position += 0x04;
    //            uint StringTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;

    //#if DEBUG
    //            string XML = "";
    //#endif

    //            #region Directory Nodes
    //            RARCFile.Position = DirectoryTableOffset;

    //            List<RARCDirEntry> FlatDirectoryList = new List<RARCDirEntry>();

    //            for (int i = 0; i < DirectoryCount; i++)
    //#if DEBUG
    //            {
    //                RARCDirEntry DEBUGTEMP = new RARCDirEntry(RARCFile, StringTableOffset);
    //                FlatDirectoryList.Add(DEBUGTEMP);
    //                long pauseposition = RARCFile.Position;
    //                RARCFile.Position = StringTableOffset + DEBUGTEMP.NameOffset;
    //                string DEBUGDIRNAME = RARCFile.ReadString();
    //                XML += $"<RarcDirectoryEntry Name=" + ($"\"{DEBUGDIRNAME}\"").PadRight(20, ' ') + $" Type=\"{DEBUGTEMP.Type.PadLeft(4, ' ')}\" NameHash=\"0x{DEBUGTEMP.NameHash.ToString("X4")}\" FirstFileOffset=\"{DEBUGTEMP.FirstFileOffset}\" FileCount=\"{DEBUGTEMP.FileCount}\"/>\n";
    //                RARCFile.Position = pauseposition;
    //            }
    //#else
    //            DirectoryNodeList.Add(new RARCDirEntry(RARCFile, StringTAbleOffset));
    //#endif
    //            #endregion

    //            #region File Nodes
    //            List<RARCFileEntry> FlatFileList = new List<RARCFileEntry>();
    //            RARCFile.Seek(FileEntryTableOffset, SeekOrigin.Begin);
    //            for (int i = 0; i < FileEntryCount; i++)
    //            {
    //                FlatFileList.Add(new RARCFileEntry()
    //                {
    //                    FileID = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0),
    //                    NameHash = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0),
    //                    Type = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0)
    //                });
    //                ushort CurrentNameOffset = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
    //                FlatFileList[FlatFileList.Count - 1].ModularA = BitConverter.ToInt32(RARCFile.ReadReverse(0, 4), 0);
    //                FlatFileList[FlatFileList.Count - 1].ModularB = BitConverter.ToInt32(RARCFile.ReadReverse(0, 4), 0);
    //                RARCFile.Position += 0x04;
    //                long Pauseposition = RARCFile.Position;
    //                RARCFile.Seek(StringTableOffset + CurrentNameOffset, SeekOrigin.Begin);
    //                FlatFileList[FlatFileList.Count - 1].Name = RARCFile.ReadString();
    //                RARCFile.Position = Pauseposition;
    //            }
    //#if DEBUG
    //            for (int i = 0; i < FlatFileList.Count; i++)
    //            {
    //                XML += $"<RarcFileEntry ID=\"{FlatFileList[i].FileID.ToString("000").PadLeft(4, '+')}\" Name=" + ($"\"{FlatFileList[i].Name}\"").PadRight(30, ' ') + $" Type=\"{FlatFileList[i].Type.ToString("X").PadLeft(4, '0')}\"\t FileOrDirectory=\"{FlatFileList[i].ModularA.ToString("X").PadLeft(8, '0')}\"\t Size=\"{FlatFileList[i].ModularB.ToString("X").PadLeft(8, '0')}\" />\n";
    //            }
    //            File.WriteAllText("Original.xml", XML);
    //#endif


    //            List<RARCDirectory> Directories = new List<RARCDirectory>();
    //            for (int i = 0; i < FlatDirectoryList.Count; i++)
    //            {
    //                Directories.Add(new RARCDirectory(i, FlatDirectoryList, FlatFileList, DataOffset, RARCFile));
    //            }

    //            for (int i = 0; i < Directories.Count; i++)
    //            {
    //                List<KeyValuePair<string, object>> templist = new List<KeyValuePair<string, object>>();
    //                foreach (KeyValuePair<string, object> DirectoryItem in Directories[i].Items)
    //                {
    //                    if (DirectoryItem.Value is RARCFileEntry fe)
    //                    {
    //                        if (DirectoryItem.Key.Equals("."))
    //                        {
    //                            if (fe.ModularA == 0)
    //                                Root = Directories[fe.ModularA];
    //                            continue;
    //                        }
    //                        if (DirectoryItem.Key.Equals(".."))
    //                            continue;
    //                        templist.Add(new KeyValuePair<string, object>(DirectoryItem.Key, Directories[fe.ModularA]));
    //                    }
    //                    else
    //                    {
    //                        templist.Add(DirectoryItem);
    //                    }
    //                }
    //                Directories[i].Items = templist.ToDictionary(K => K.Key, V => V.Value);
    //            }
    //            #endregion
    //        }

    //        public void Save(string filepath)
    //        {
    //            FileStream fs = new FileStream(filepath, FileMode.Create);
    //            Save(fs);
    //            fs.Close();
    //        }
    //        public void Save(Stream RARCFile)
    //        {
    //            Dictionary<RARCFile, uint> FileOffsets = new Dictionary<RARCFile, uint>();
    //            uint dataoffset = 0;
    //            byte[] DataByteBuffer = GetDataBytes(Root, ref FileOffsets, ref dataoffset).ToArray();
    //            short FileID = 0;
    //            int NextFolderID = 1;
    //            List<RARCFileEntry> FlatFileList = GetFlatFileList(Root, FileOffsets, ref FileID, 0, ref NextFolderID, -1);
    //            uint FirstFileOffset = 0;
    //            List<RARCDirEntry> FlatDirectoryList = GetFlatDirectoryList(Root, ref FirstFileOffset);
    //            FlatDirectoryList.Insert(0, new RARCDirEntry() { FileCount = (ushort)(Root.Items.Count + 2), FirstFileOffset = 0, Name = Root.Name, NameHash = StringToHash(Root.Name), NameOffset = 0, Type = "ROOT" });
    //            Dictionary<string, uint> StringLocations = new Dictionary<string, uint>();
    //            byte[] StringDataBuffer = GetStringTableBytes(FlatFileList, Root.Name, ref StringLocations).ToArray();

    //            #region File Writing
    //            RARCFile.WriteString(Magic);
    //            RARCFile.Write(new byte[12] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x20, 0xDD, 0xDD, 0xDD, 0xDD }, 0, 12);
    //            RARCFile.Write(new byte[16] { 0xEE, 0xEE, 0xEE, 0xEE, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0, 16);
    //            RARCFile.WriteReverse(BitConverter.GetBytes(FlatDirectoryList.Count), 0, 4);
    //            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //Directory Nodes Location (-0x20)
    //            RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList.Count), 0, 4);
    //            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //File Entries Location (-0x20)
    //            RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //String Table Size
    //            RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //string Table Location (-0x20)
    //            RARCFile.WriteReverse(BitConverter.GetBytes((ushort)FlatFileList.Count), 0, 2);
    //            RARCFile.Write(new byte[2] { 0x01, 0x00 }, 0, 2);
    //            RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
    //            long DirectoryEntryOffset = RARCFile.Position;

    //            #region Directory Nodes
    //            for (int i = 0; i < FlatDirectoryList.Count; i++)
    //            {
    //                RARCFile.WriteString(FlatDirectoryList[i].Type);
    //                RARCFile.WriteReverse(BitConverter.GetBytes(StringLocations[FlatDirectoryList[i].Name]), 0, 4);
    //                RARCFile.WriteReverse(BitConverter.GetBytes(FlatDirectoryList[i].NameHash), 0, 2);
    //                RARCFile.WriteReverse(BitConverter.GetBytes(FlatDirectoryList[i].FileCount), 0, 2);
    //                RARCFile.WriteReverse(BitConverter.GetBytes(FlatDirectoryList[i].FirstFileOffset), 0, 4);
    //            }

    //            #region Padding
    //            while (RARCFile.Position % 32 != 0)
    //                RARCFile.WriteByte(0x00);
    //            #endregion
    //            #endregion

    //            long FileEntryOffset = RARCFile.Position;

    //            #region File Entries
    //            for (int i = 0; i < FlatFileList.Count; i++)
    //            {
    //                RARCFile.Write(BitConverter.GetBytes(FlatFileList[i].FileID), 0, 2);
    //                RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(FlatFileList[i].Name)), 0, 2);
    //                RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList[i].Type), 0, 2);
    //                RARCFile.WriteReverse(BitConverter.GetBytes((ushort)StringLocations[FlatFileList[i].Name]), 0, 2);
    //                RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList[i].ModularA), 0, 4);
    //                RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList[i].ModularB), 0, 4);
    //                RARCFile.Write(new byte[4], 0, 4);
    //            }
    //            #region Padding
    //            while (RARCFile.Position % 32 != 0)
    //                RARCFile.WriteByte(0x00);
    //            #endregion
    //            #endregion

    //            long StringTableOffset = RARCFile.Position;

    //            #region String Table
    //            RARCFile.Write(StringDataBuffer, 0, StringDataBuffer.Length);

    //            #region Padding
    //            while (RARCFile.Position % 32 != 0)
    //                RARCFile.WriteByte(0x00);
    //            #endregion
    //            #endregion

    //            long FileTableOffset = RARCFile.Position;

    //            #region File Table
    //            RARCFile.Write(DataByteBuffer, 0, DataByteBuffer.Length);
    //            #endregion

    //            #region Header
    //            RARCFile.Position = 0x04;
    //            RARCFile.WriteReverse(BitConverter.GetBytes((int)RARCFile.Length), 0, 4);
    //            RARCFile.Position += 0x04;
    //            RARCFile.WriteReverse(BitConverter.GetBytes((int)(FileTableOffset - 0x20)), 0, 4);
    //            RARCFile.WriteReverse(BitConverter.GetBytes((int)(RARCFile.Length - FileTableOffset)), 0, 4);
    //            RARCFile.WriteReverse(BitConverter.GetBytes((int)(RARCFile.Length - FileTableOffset)), 0, 4);
    //            RARCFile.Position += 0x0C;
    //            RARCFile.WriteReverse(BitConverter.GetBytes((int)(DirectoryEntryOffset - 0x20)), 0, 4);
    //            RARCFile.Position += 0x04;
    //            RARCFile.WriteReverse(BitConverter.GetBytes((int)(FileEntryOffset - 0x20)), 0, 4);
    //            RARCFile.WriteReverse(BitConverter.GetBytes((int)(FileTableOffset - StringTableOffset)), 0, 4);
    //            RARCFile.WriteReverse(BitConverter.GetBytes((int)(StringTableOffset - 0x20)), 0, 4);
    //            #endregion

    //            #endregion
    //        }
    //        private List<byte> GetDataBytes(RARCDirectory Root, ref Dictionary<RARCFile, uint> Offsets, ref uint LocalOffset)
    //        {
    //            List<byte> DataBytes = new List<byte>();
    //            foreach (KeyValuePair<string, object> item in Root.Items)
    //            {
    //                if (item.Value is RARCDirectory dir)
    //                {
    //                    DataBytes.AddRange(GetDataBytes(dir, ref Offsets, ref LocalOffset));
    //                }
    //                else if (item.Value is RARCFile file)
    //                {
    //                    Offsets.Add(file, LocalOffset);
    //                    List<byte> temp = new List<byte>();
    //                    temp.AddRange(file.FileData);

    //                    while (temp.Count % 32 != 0)
    //                        temp.Add(0x00);
    //                    DataBytes.AddRange(temp);
    //                    LocalOffset += (uint)temp.Count;
    //                }
    //            }
    //            return DataBytes;
    //        }
    //        private List<RARCFileEntry> GetFlatFileList(RARCDirectory Root, Dictionary<RARCFile, uint> FileOffsets, ref short GlobalFileID, int CurrentFolderID, ref int NextFolderID, int BackwardsFolderID)
    //        {
    //            List<RARCFileEntry> FileList = new List<RARCFileEntry>();
    //            List<KeyValuePair<int, RARCDirectory>> Directories = new List<KeyValuePair<int, RARCDirectory>>();
    //            foreach (KeyValuePair<string, object> item in Root.Items)
    //            {
    //                if (item.Value is RARCFile file)
    //                {
    //                    FileList.Add(new RARCFileEntry() { FileID = GlobalFileID++, Name = file.Name, ModularA = (int)FileOffsets[file], ModularB = file.FileData.Length, Type = 0x1100 });
    //                }
    //                else if (item.Value is RARCDirectory Currentdir)
    //                {
    //                    Directories.Add(new KeyValuePair<int, RARCDirectory>(FileList.Count, Currentdir));
    //                    //Dirs.Add(new RARCDirEntry() { FileCount = (ushort)(Currentdir.Items.Count + 2), FirstFileOffset = 0xFFFFFFFF, Name = Currentdir.Name, NameHash = Currentdir.NameToHash(), NameOffset = 0xFFFFFFFF, Type = Currentdir.ToTypeString() });
    //                    FileList.Add(new RARCFileEntry() { FileID = -1, Name = Currentdir.Name, ModularA = NextFolderID++, ModularB = 0x10, Type = 0x0200 });
    //                    GlobalFileID++;
    //                }
    //            }
    //            FileList.Add(new RARCFileEntry() { FileID = -1, Name = ".", ModularA = CurrentFolderID, ModularB = 0x10, Type = 0x0200 });
    //            FileList.Add(new RARCFileEntry() { FileID = -1, Name = "..", ModularA = BackwardsFolderID, ModularB = 0x10, Type = 0x0200 });
    //            GlobalFileID += 2;
    //            for (int i = 0; i < Directories.Count; i++)
    //            {
    //                FileList.AddRange(GetFlatFileList(Directories[i].Value, FileOffsets, ref GlobalFileID, FileList[Directories[i].Key].ModularA, ref NextFolderID, CurrentFolderID));
    //            }
    //            return FileList;
    //        }
    //        private List<RARCDirEntry> GetFlatDirectoryList(RARCDirectory Root, ref uint FirstFileOffset)
    //        {
    //            List<RARCDirEntry> FlatDirectoryList = new List<RARCDirEntry>();
    //            FirstFileOffset += (uint)(Root.Items.Count + 2);
    //            foreach (KeyValuePair<string, object> item in Root.Items)
    //            {
    //                if (item.Value is RARCDirectory Currentdir)
    //                {
    //                    FlatDirectoryList.Add(new RARCDirEntry() { FileCount = (ushort)(Currentdir.Items.Count + 2), FirstFileOffset = FirstFileOffset, Name = Currentdir.Name, NameHash = StringToHash(Currentdir.Name), NameOffset = 0xFFFFFFFF, Type = Currentdir.ToTypeString() });
    //                    FlatDirectoryList.AddRange(GetFlatDirectoryList(Currentdir, ref FirstFileOffset));
    //                }
    //            }
    //            return FlatDirectoryList;
    //        }
    //        private List<byte> GetStringTableBytes(List<RARCFileEntry> FlatFileList, string RootName, ref Dictionary<string, uint> Offsets)
    //        {
    //            List<byte> strings = new List<byte>();
    //            Encoding enc = Encoding.GetEncoding(932);
    //            strings.AddRange(enc.GetBytes(RootName));
    //            strings.Add(0x00);
    //            Offsets.Add(RootName, 0);

    //            Offsets.Add(".", (uint)strings.Count);
    //            strings.AddRange(enc.GetBytes("."));
    //            strings.Add(0x00);

    //            Offsets.Add("..", (uint)strings.Count);
    //            strings.AddRange(enc.GetBytes(".."));
    //            strings.Add(0x00);

    //            for (int i = 0; i < FlatFileList.Count; i++)
    //            {
    //                if (!Offsets.ContainsKey(FlatFileList[i].Name))
    //                {
    //                    Offsets.Add(FlatFileList[i].Name, (uint)strings.Count);
    //                    strings.AddRange(enc.GetBytes(FlatFileList[i].Name));
    //                    strings.Add(0x00);
    //                }
    //            }
    //            return strings;
    //        }

    //        public class RARCDirectory
    //        {
    //            public string Name { get; set; }
    //            public Dictionary<string, object> Items { get; set; } = new Dictionary<string, object>();

    //            internal RARCDirectory(int ID, List<RARCDirEntry> DirectoryNodeList, List<RARCFileEntry> FlatFileList, uint DataBlockStart, Stream RARCFile)
    //            {
    //                Name = DirectoryNodeList[ID].Name;
    //                for (int i = (int)DirectoryNodeList[ID].FirstFileOffset; i < DirectoryNodeList[ID].FileCount + DirectoryNodeList[ID].FirstFileOffset; i++)
    //                {
    //                    //IsDirectory
    //                    if (FlatFileList[i].Type == 0x0200)
    //                    {
    //                        Items.Add(FlatFileList[i].Name, FlatFileList[i]);
    //                    }
    //                    else
    //                    {
    //                        Items.Add(FlatFileList[i].Name, new RARCFile(FlatFileList[i], DataBlockStart, RARCFile));
    //                    }
    //                }
    //            }

    //            internal string ToTypeString() => Name.ToUpper().PadRight(4, ' ').Substring(0, 4);
    //        }

    //        public class RARCFile
    //        {
    //            /// <summary>
    //            /// Name of the File
    //            /// </summary>
    //            public string Name { get; set; }
    //            /// <summary>
    //            /// The extension of this file
    //            /// </summary>
    //            public string Extension
    //            {
    //                get
    //                {
    //                    if (Name == null)
    //                        return null;
    //                    string[] parts = Name.Split('.');
    //                    return "." + parts[parts.Length - 1].ToLower();
    //                }
    //            }
    //            /// <summary>
    //            /// The Actual Data for the file
    //            /// </summary>
    //            public byte[] FileData { get; set; }

    //            internal RARCFile(RARCFileEntry entry, uint DataBlockStart, Stream RARCFile)
    //            {
    //                Name = entry.Name;
    //                RARCFile.Position = DataBlockStart + entry.ModularA;
    //                FileData = RARCFile.Read(0, entry.ModularB);
    //            }
    //        }

    //        internal class RARCDirEntry
    //        {
    //            /// <summary>
    //            /// Directory Type. usually the first 4 letters of the <see cref="Name"/>. If the <see cref="Name"/> is shorter than 4, the missing spots will be ' ' (space)
    //            /// </summary>
    //            public string Type { get; set; }
    //            public string Name { get; set; }
    //            public uint NameOffset { get; set; }
    //            public ushort NameHash { get; set; }
    //            public ushort FileCount { get; set; }
    //            public uint FirstFileOffset { get; set; }

    //            public RARCDirEntry() { }
    //            public RARCDirEntry(Stream RARCFile, uint StringTableOffset)
    //            {
    //                Type = RARCFile.ReadString(4);
    //                NameOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);
    //                NameHash = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
    //                FileCount = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
    //                FirstFileOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);

    //                long pauseposition = RARCFile.Position;
    //                RARCFile.Position = StringTableOffset + NameOffset;
    //                Name = RARCFile.ReadString();
    //                RARCFile.Position = pauseposition;
    //            }

    //            internal void Write(Stream RARCFile, Dictionary<string, uint> StringLocations)
    //            {

    //            }

    //            public override string ToString() => $"{Name} ({Type}) [0x{NameHash.ToString("X4")}] {FileCount} File(s)";
    //        }

    //        internal class RARCFileEntry
    //        {
    //            public short FileID;
    //            public short Type;
    //            public string Name;
    //            /// <summary>
    //            /// For files: offset to file data in file data section, for subdirectories: index of the corresponding directory node
    //            /// </summary>
    //            public int ModularA;
    //            /// <summary>
    //            /// For files: size of the file, for subdirectories: always 0x10 (size of the node entry?)
    //            /// </summary>
    //            public int ModularB;
    //            internal short NameHash;

    //            public override string ToString() => $"({FileID}) {Name}, {Type.ToString("X").PadLeft(4, '0')}, [{ModularA.ToString("X").PadLeft(8, '0')}][{ModularB.ToString("X").PadLeft(8, '0')}]";
    //        }

    //        /// <summary>
    //        /// Generates a 2 byte hash from a string
    //        /// </summary>
    //        /// <param name="Input">string to convert</param>
    //        /// <returns>hashed string</returns>
    //        static internal ushort StringToHash(string Input)
    //        {
    //            int Hash = 0;
    //            for (int i = 0; i < Input.Length; i++)
    //            {
    //                Hash *= 3;
    //                Hash += Input[i];
    //                Hash = 0xFFFF & Hash; //cast to short 
    //            }

    //            return (ushort)Hash;
    //        }
    //    }
}
