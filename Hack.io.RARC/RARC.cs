using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hack.io.Util;

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
        /// If false, the user must set all unique ID's for each file
        /// </summary>
        public bool KeepFileIDsSynced { get; set; } = true;
        /// <summary>
        /// The total amount of files inside this archive.
        /// </summary>
        public int TotalFileCount => Root?.GetCountAndChildren() ?? 0;
        /// <summary>
        /// Gets the next free File ID
        /// </summary>
        public short NextFreeFileID => GetNextFreeID();
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
        /// Get or Set a file based on a path. When setting, if the file doesn't exist, it will be added (Along with any missing subdirectories). Set the file to null to delete it
        /// </summary>
        /// <param name="Path">The Path to take. Does not need the Root name to start, but cannot start with a '/'</param>
        /// <returns></returns>
        public object this[string Path]
        {
            get
            {
                if (Root is null || Path is null)
                    return null;
                if (Path.StartsWith(Root.Name+"/"))
                    Path = Path.Substring(Root.Name.Length+1);
                return Root[Path];
            }
            set
            {
                if (!(value is File || value is Directory || value is null))
                    throw new Exception($"Invalid object type of {value.GetType().ToString()}");

                if (Root is null)
                    Root = new Directory(this, null) { Name = Path.Split('/')[0] };

                if (Path.StartsWith(Root.Name + "/"))
                    Path = Path.Substring(Root.Name.Length + 1);
                
                if (!KeepFileIDsSynced && value is File file && file.ID == -1 && !ItemExists(Path))
                    file.ID = GetNextFreeID();
                Root[Path] = value;
            }
        }
        /// <summary>
        /// Checks to see if an Item Exists based on a Path
        /// </summary>
        /// <param name="Path">The path to take</param>
        /// <returns>false if the Item isn't found</returns>
        public bool ItemExists(string Path)
        {
            if (Path.StartsWith(Root.Name + "/"))
                Path = Path.Substring(Root.Name.Length + 1);
            return Root.ItemExists(Path);
        }
        /// <summary>
        /// This will return the absolute path of an item if it exists in some way. Useful if you don't know the casing of the filename inside the file. Returns null if nothing is found.
        /// </summary>
        /// <param name="Path">The path to get the Actual path from</param>
        /// <returns>null if nothing is found</returns>
        public string GetItemKeyFromNoCase(string Path)
        {
            if (Path.ToLower().StartsWith(Root.Name.ToLower() + "/"))
                Path = Path.Substring(Root.Name.Length + 1);
            return Root.GetItemKeyFromNoCase(Path, true);
        }
        /// <summary>
        /// Clears all the files out of this archive
        /// </summary>
        public void ClearAll() { Root.Clear(); }
        /// <summary>
        /// Moves an item to a new directory
        /// </summary>
        /// <param name="OriginalPath"></param>
        /// <param name="NewPath"></param>
        public void MoveItem(string OriginalPath, string NewPath)
        {
            if (OriginalPath.StartsWith(Root.Name + "/"))
                OriginalPath = OriginalPath.Substring(Root.Name.Length + 1);
            if (OriginalPath.Equals(NewPath))
                return;
            if (ItemExists(NewPath))
                throw new Exception("An item with that name already exists in that directory");


            dynamic dest = this[OriginalPath];
            string[] split = NewPath.Split('/');
            dest.Name = split[split.Length - 1];
            this[OriginalPath] = null;
            this[NewPath] = dest;
        }
        #endregion

        /// <summary>
        /// Save the Archive to a File
        /// </summary>
        /// <param name="filepath">New file to save to</param>
        public void Save(string filepath)
        {
            FileName = filepath;
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
            uint MRAMSize = 0, ARAMSize = 0, DVDSize = 0;
            byte[] DataByteBuffer = GetDataBytes(Root, ref FileOffsets, ref dataoffset, ref MRAMSize, ref ARAMSize, ref DVDSize).ToArray();
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
            RARCFile.Write(new byte[16] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x20, 0xDD, 0xDD, 0xDD, 0xDD, 0xEE, 0xEE, 0xEE, 0xEE }, 0, 16);
            RARCFile.WriteReverse(BitConverter.GetBytes(MRAMSize), 0, 4);
            RARCFile.WriteReverse(BitConverter.GetBytes(ARAMSize), 0, 4);
            RARCFile.WriteReverse(BitConverter.GetBytes(DVDSize), 0, 4);
            //Data Header
            RARCFile.WriteReverse(BitConverter.GetBytes(FlatDirectoryList.Count), 0, 4);
            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //Directory Nodes Location (-0x20)
            RARCFile.WriteReverse(BitConverter.GetBytes(FlatFileList.Count), 0, 4);
            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); //File Entries Location (-0x20)
            RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //String Table Size
            RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //string Table Location (-0x20)
            RARCFile.WriteReverse(BitConverter.GetBytes((ushort)FlatFileList.Count), 0, 2);
            RARCFile.WriteByte((byte)(KeepFileIDsSynced ? 0x01 : 0x00));
            RARCFile.Write(new byte[5], 0, 5);
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
            RARCFile.Position += 0x10;
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
            int Count = Root?.GetCountAndChildren() ?? 0;
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
            /// The parent directory (Null if non-existant)
            /// </summary>
            public Directory Parent { get; set; }
            private RARC OwnerArchive;

            /// <summary>
            /// Create a new Archive Directory
            /// </summary>
            public Directory() {}
            /// <summary>
            /// Create a new, child directory
            /// </summary>
            /// <param name="Owner">The Owner Archive</param>
            /// <param name="parentdir">The Parent Directory. NULL if this is the Root Directory</param>
            public Directory(RARC Owner, Directory parentdir) { OwnerArchive = Owner; Parent = parentdir; }
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
                        file.Save(FolderPath+"\\"+file.Name);
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
                    if (!ItemKeyExists(PathSplit[0]) && !(value is null))
                    {
                        ((dynamic)value).Parent = this;
                        if (PathSplit.Length == 1)
                        {
                            if (value is Directory dir)
                                dir.OwnerArchive = OwnerArchive;
                            ((dynamic)value).Parent = this;
                            Items.Add(PathSplit[0], value);
                        }
                        else
                        {
                            Items.Add(PathSplit[0], new Directory(OwnerArchive, this) { Name = PathSplit[0] });
                            ((Directory)Items[PathSplit[0]])[Path.Substring(PathSplit[0].Length + 1)] = value;
                        }
                    }
                    else
                    {
                        if (PathSplit.Length == 1)
                        {
                            if (value is null)
                            {
                                if (ItemKeyExists(PathSplit[0]))
                                    Items.Remove(PathSplit[0]);
                                else
                                    return;
                            }
                            else
                            {
                                ((dynamic)value).Parent = this;
                                Items[PathSplit[0]] = value;
                            }
                        }
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
            /// <summary>
            /// 
            /// </summary>
            /// <param name="Path"></param>
            /// <param name="AttachRootName"></param>
            /// <returns></returns>
            public string GetItemKeyFromNoCase(string Path, bool AttachRootName = false)
            {
                string[] PathSplit = Path.Split('/');
                if (PathSplit.Length > 1)
                {
                    string result = Items.FirstOrDefault(x => string.Equals(x.Key, PathSplit[0], StringComparison.OrdinalIgnoreCase)).Key;
                    if (result == null)
                        return null;
                    else
                        result = ((Directory)Items[result]).GetItemKeyFromNoCase(Path.Substring(PathSplit[0].Length + 1), true);
                    return result == null ? null : (AttachRootName ? Name + "/" : "") + result;
                }
                else if (PathSplit.Length > 1)
                    return null;
                else
                {
                    string result = Items.FirstOrDefault(x => string.Equals(x.Key, PathSplit[0], StringComparison.OrdinalIgnoreCase)).Key;
                    return result == null ? null : (AttachRootName ? Name + "/" : "") + result;
                }
            }
            /// <summary>
            /// Clears all the items out of this directory
            /// </summary>
            public void Clear()
            {
                foreach (KeyValuePair<string, object> item in Items)
                {
                    if (item.Value is Directory dir)
                        dir.Clear();
                }
                Items.Clear();
            }
            /// <summary>
            /// Returns the amount of Items in this directory (Items in subdirectories not included)
            /// </summary>
            public int Count => Items.Count;
            /// <summary>
            /// The full path of this directory. Cannot be used if this .arc doesn't belong to a RARC object
            /// </summary>
            public string FullPath
            {
                get
                {
                    if (OwnerArchive != null)
                    {
                        StringBuilder path = new StringBuilder();
                        GetFullPath(path);
                        return path.ToString();
                    }
                    else
                        throw new InvalidOperationException("In order to use this, this directory must be part of a directory with a parent that is connected to a RARC object");
                }
            }
            internal void GetFullPath(StringBuilder Path)
            {
                if (Parent != null)
                {
                    Parent.GetFullPath(Path);
                    Path.Append("/");
                    Path.Append(Name);
                }
                else
                {
                    Path.Append(Name);
                }
            }
            internal int GetCountAndChildren()
            {
                int count = 0;
                foreach (KeyValuePair<string, object> item in Items)
                {
                    if (item.Value is Directory dir)
                        count += dir.GetCountAndChildren();
                    else
                        count++;
                }
                return count;
            }
            /// <summary>
            /// Checks to see if this directory has an owner archive
            /// </summary>
            public bool HasOwnerArchive => OwnerArchive != null;
            /// <summary>
            /// Sorts the Items inside this directory using the provided string[]. This string[] MUST contain all entries inside this directory
            /// </summary>
            /// <param name="NewItemOrder"></param>
            public void SortItemsByOrder(string[] NewItemOrder)
            {
                if (NewItemOrder.Length != Items.Count)
                    throw new Exception("Missing Items that exist in this Directory, but not in the provided Item Order");
                Dictionary<string, object> NewItems = new Dictionary<string, object>();
                for (int i = 0; i < NewItemOrder.Length; i++)
                {
                    if (!Items.ContainsKey(NewItemOrder[i]))
                        throw new Exception("Missing Items that exist in this Directory, but not in the provided Item Order (Potentually a typo)");
                    NewItems.Add(NewItemOrder[i], Items[NewItemOrder[i]]);
                }
                Items = NewItems;
            }
            /// <summary>
            /// Moves an item from it's current directory to a new directory
            /// </summary>
            /// <param name="ItemKey">The Key of the Item</param>
            /// <param name="TargetDirectory"></param>
            public void MoveItemToDirectory(string ItemKey, Directory TargetDirectory)
            {
                if (TargetDirectory.ItemKeyExists(ItemKey))
                    throw new Exception($"There is already a file with the name {ItemKey} inside {TargetDirectory.Name}");

                TargetDirectory[ItemKey] = Items[ItemKey];
                Items.Remove(ItemKey);
            }
            /// <summary>
            /// Rename an item in the directory
            /// </summary>
            /// <param name="OldName"></param>
            /// <param name="NewName"></param>
            public void RenameItem(string OldName, string NewName)
            {
                if (ItemKeyExists(NewName))
                    throw new Exception($"There is already a file with the name {NewName} inside {Name}");
                dynamic activeitem = (dynamic)Items[OldName];
                Items.Remove(OldName);
                activeitem.Name = NewName;
                Items.Add(NewName, activeitem);
            }

            internal string ToTypeString() => Name.ToUpper().PadRight(4, ' ').Substring(0, 4);
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"{Name} - {Items.Count} Item(s)";
            /// <summary>
            /// Create a RARC.Directory. You cannot use this function unless this directory is empty
            /// </summary>
            /// <param name="FolderPath"></param>
            public void CreateFromFolder(string FolderPath)
            {
                if (Items.Count > 0)
                    throw new Exception("Cannot create a directory from a folder if Items exist");
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
                    if (parts.Length == 1)
                        return "";
                    return "." + parts[parts.Length - 1].ToLower();
                }
            }
            /// <summary>
            /// Extra settings for this File.<para/>Default: <see cref="FileAttribute.FILE"/> | <see cref="FileAttribute.PRELOAD_TO_MRAM"/>
            /// </summary>
            public FileAttribute FileSettings { get; set; } = FileAttribute.FILE | FileAttribute.PRELOAD_TO_MRAM;
            /// <summary>
            /// The ID of the file in the archive
            /// </summary>
            public short ID { get; set; } = -1;
            /// <summary>
            /// The Actual Data for the file
            /// </summary>
            public byte[] FileData { get; set; }
            /// <summary>
            /// The parent directory (Null if non-existant)
            /// </summary>
            public Directory Parent { get; set; }
            /// <summary>
            /// Load a File's Data based on a path
            /// </summary>
            /// <param name="Filepath"></param>
            public File(string Filepath)
            {
                Name = new FileInfo(Filepath).Name;
                FileData = System.IO.File.ReadAllBytes(Filepath);
            }
            /// <summary>
            /// Create a File from a MemoryStream
            /// </summary>
            /// <param name="name">The name of the file</param>
            /// <param name="ms">The Memory Stream to use</param>
            public File(string name, MemoryStream ms)
            {
                Name = name;
                FileData = ms.ToArray();
            }
            internal File(RARCFileEntry entry, uint DataBlockStart, Stream RARCFile)
            {
                Name = entry.Name;
                FileSettings = entry.RARCFileType;
                ID = entry.FileID;
                RARCFile.Position = DataBlockStart + entry.ModularA;
                FileData = RARCFile.Read(0, entry.ModularB);
            }
            /// <summary>
            /// Saves this file to the Computer's Disk
            /// </summary>
            /// <param name="Filepath">The full path to save to</param>
            public void Save(string Filepath)
            {
                System.IO.File.WriteAllBytes(Filepath, FileData);
            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="left"></param>
            /// <param name="right"></param>
            /// <returns></returns>
            public static bool operator ==(File left, File right) => left.Equals(right);
            /// <summary>
            /// 
            /// </summary>
            /// <param name="left"></param>
            /// <param name="right"></param>
            /// <returns></returns>
            public static bool operator !=(File left, File right) => !left.Equals(right);
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
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"{ID} - {Name} ({FileSettings.ToString()}) [0x{FileData.Length.ToString("X8")}]";

            /// <summary>
            /// The full path of this file. Cannot be used if this file doesn't belong to a RARC object somehow
            /// </summary>
            public string FullPath
            {
                get
                {
                    if (Parent.HasOwnerArchive)
                    {
                        StringBuilder path = new StringBuilder();
                        GetFullPath(path);
                        return path.ToString();
                    }
                    else
                        throw new InvalidOperationException("In order to use this, this file must be part of a directory with a parent that is connected to a RARC object");
                }
            }
            private void GetFullPath(StringBuilder Path)
            {
                if (Parent != null)
                {
                    Parent.GetFullPath(Path);
                    Path.Append("/");
                    Path.Append(Name);
                }
                else
                {
                    Path.Append(Name);
                }
            }

            //=====================================================================

            /// <summary>
            /// Cast a File to a MemoryStream
            /// </summary>
            /// <param name="x"></param>
            public static explicit operator MemoryStream(File x) => new MemoryStream(x.FileData);
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
            internal FileAttribute RARCFileType => (FileAttribute)((Type & 0xFF00) >> 8);

            public override string ToString() => $"({FileID}) {Name}, {Type.ToString("X").PadLeft(4, '0')} ({RARCFileType.ToString()}), [{ModularA.ToString("X").PadLeft(8, '0')}][{ModularB.ToString("X").PadLeft(8, '0')}]";
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
            #region Header
            if (RARCFile.ReadString(4) != Magic)
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");
            uint FileSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                DataHeaderOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                DataOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20,
                DataLength = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                MRAMSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                ARAMSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);
            RARCFile.Position += 0x04; //Skip the supposed padding
            #endregion

            #region Data Header
            uint DirectoryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                    DirectoryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20,
                    FileEntryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                    FileEntryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20,
                    StringTableSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                    StringTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;
            ushort NextFreeFileID = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
            KeepFileIDsSynced = RARCFile.ReadByte() != 0x00;
            #endregion

#if DEBUG
            //string XML = $"<RarcHeader Magic=\"{Magic}\"  FileSize=\"0x{FileSize.ToString("X8")}\"  DataHeaderOffset=\"0x{DataHeaderOffset.ToString("X8")}\"  DataOffset=\"0x{DataOffset.ToString("X8")}\"  DataLength=\"0x{DataLength.ToString("X8")}\"  MRAM=\"0x{MRAMSize.ToString("X8")}\"  ARAM=\"0x{ARAMSize.ToString("X8")}\"/>\n" +
            //    $"<DataHeader DirectoryCount=\"{DirectoryCount.ToString("2")}\"  DirectoryTableOffset=\"0x{DirectoryTableOffset.ToString("X8")}\"  FileEntryCount=\"{FileEntryCount.ToString("2")}\"  FileEntryTableOffset=\"0x{FileEntryTableOffset.ToString("X8")}\"  StringTableSize=\"0x{StringTableSize.ToString("X8")}\"  StringTableOffset=\"0x{StringTableOffset.ToString("X8")}\"  NextFreeID=\"{NextFreeFileID.ToString("0000")}\"  SyncFileIDs=\"{KeepFileIDsSynced}\"/>\n";
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
#if DEBUG
            //for (int i = 0; i < FlatFileList.Count; i++)
            //    XML += $"<RarcFileEntry ID=\"{FlatFileList[i].FileID.ToString("0000").PadLeft(5, '+')}\" Name=" + ($"\"{FlatFileList[i].Name}\"").PadRight(30, ' ') + $" Type=\"{FlatFileList[i].Type.ToString("X4")}\"\t RARCFileType=\"{(FlatFileList[i].RARCFileType.ToString()+ "\"").PadRight(12, ' ')}\t FileOrDirectory=\"{FlatFileList[i].ModularA.ToString("X").PadLeft(8, '0')}\"\t Size=\"{FlatFileList[i].ModularB.ToString("X").PadLeft(8, '0')}\" />\n";

            //System.IO.File.WriteAllText("Original.xml", XML);
#endif


            List<Directory> Directories = new List<Directory>();
            for (int i = 0; i < FlatDirectoryList.Count; i++)
            {
                Directories.Add(new Directory(this, i, FlatDirectoryList, FlatFileList, DataOffset, RARCFile));
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
        private List<byte> GetDataBytes(Directory Root, ref Dictionary<File, uint> Offsets, ref uint LocalOffset, ref uint MRAMSize, ref uint ARAMSize, ref uint DVDSize)
        {
            List<byte> DataBytesMRAM = new List<byte>();
            List<byte> DataBytesARAM = new List<byte>();
            List<byte> DataBytesDVD = new List<byte>();
            //First, we must sort the files in the correct order
            //MRAM First. ARAM Second, DVD Last
            List<File> MRAM = new List<File>(), ARAM = new List<File>(), DVD = new List<File>();
            SortFilesByLoadType(Root, ref MRAM, ref ARAM, ref DVD);

            for (int i = 0; i < MRAM.Count; i++)
            {

                if (Offsets.Any(OFF => OFF.Key.FileData.SequenceEqual(MRAM[i].FileData)))
                {
                    Offsets.Add(MRAM[i], Offsets[Offsets.Keys.First(FILE => FILE.FileData.SequenceEqual(MRAM[i].FileData))]);
                }
                else
                {
                    List<byte> CurrentMRAMFile = MRAM[i].FileData.ToList();
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
                List<byte> temp = new List<byte>();
                temp.AddRange(ARAM[i].FileData);

                while (temp.Count % 32 != 0)
                    temp.Add(0x00);
                DataBytesARAM.AddRange(temp);
                LocalOffset += (uint)temp.Count;
            }
            ARAMSize = LocalOffset - MRAMSize;
            for (int i = 0; i < DVD.Count; i++)
            {
                Offsets.Add(DVD[i], LocalOffset);
                List<byte> temp = new List<byte>();
                temp.AddRange(DVD[i].FileData);

                while (temp.Count % 32 != 0)
                    temp.Add(0x00);
                DataBytesDVD.AddRange(temp);
                LocalOffset += (uint)temp.Count;
            }
            DVDSize = LocalOffset - ARAMSize - MRAMSize;

            List<byte> DataBytes = new List<byte>();
            DataBytes.AddRange(DataBytesMRAM);
            DataBytes.AddRange(DataBytesARAM);
            DataBytes.AddRange(DataBytesDVD);
            return DataBytes;
        }
        private void SortFilesByLoadType(Directory Root, ref List<File> MRAM, ref List<File> ARAM, ref List<File> DVD)
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
                        throw new Exception($"File entry \"{file.ToString()}\" is not set as being loaded into any type of RAM, or from DVD.");
                }
            }
        }
        private List<RARCFileEntry> GetFlatFileList(Directory Root, Dictionary<File, uint> FileOffsets, ref short GlobalFileID, int CurrentFolderID, ref int NextFolderID, int BackwardsFolderID)
        {
            List<RARCFileEntry> FileList = new List<RARCFileEntry>();
            List<KeyValuePair<int, Directory>> Directories = new List<KeyValuePair<int, Directory>>();
            foreach (KeyValuePair<string, object> item in Root.Items)
            {
                if (item.Value is File file)
                {
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
        private List<File> GetFlatFileList(Directory Root)
        {
            List<File> FileList = new List<File>();
            foreach (KeyValuePair<string, object> item in Root.Items)
            {
                if (item.Value is File file)
                {
                    FileList.Add(file);
                }
                else if (item.Value is Directory Currentdir)
                {
                    FileList.AddRange(GetFlatFileList(Currentdir));
                    FileList.Add(null);
                }
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
        
        private short GetNextFreeID()
        {
            List<short> AllIDs = new List<short>();
            List<File> FlatFileList = GetFlatFileList(Root);
            for (int i = 0; i < FlatFileList.Count; i++)
                AllIDs.Add(FlatFileList[i]?.ID ?? (short)AllIDs.Count);
            if (AllIDs.Count == 0)
                return 0;
            int a = AllIDs.OrderBy(x => x).First();
            int b = AllIDs.OrderBy(x => x).Last();
            List<int> LiterallyAllIDs = Enumerable.Range(0, b - a + 1).ToList();
            List<short> Shorts = new List<short>();
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
        #endregion

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
    }
}
