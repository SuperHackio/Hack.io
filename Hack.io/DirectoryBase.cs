using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hack.io.Util
{
    /// <summary>
    /// An interface to determine if a class is an archive
    /// </summary>
    public interface IArchive
    {
        
    }

    /// <summary>
    /// Folder contained inside the Archive. Can contain more <see cref="ArchiveDirectory{OwnerType}"/>s if desired, as well as <see cref="ArchiveFile"/>s
    /// </summary>
    public class ArchiveDirectory<OwnerType> where OwnerType : class, IArchive
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
        public ArchiveDirectory<OwnerType> Parent { get; set; }
        /// <summary>
        /// The Archive that owns this directory
        /// </summary>
        protected OwnerType OwnerArchive;

        /// <summary>
        /// Create a new Archive Directory
        /// </summary>
        public ArchiveDirectory() { }
        /// <summary>
        /// Create a new, child directory
        /// </summary>
        /// <param name="Owner">The Owner Archive</param>
        /// <param name="parentdir">The Parent Directory. NULL if this is the Root Directory</param>
        public ArchiveDirectory(OwnerType Owner, ArchiveDirectory<OwnerType> parentdir) { OwnerArchive = Owner; Parent = parentdir; }
        /// <summary>
        /// Import a Folder into a RARCDirectory
        /// </summary>
        /// <param name="FolderPath"></param>
        /// <param name="Owner"></param>
        public ArchiveDirectory(string FolderPath, OwnerType Owner)
        {
            DirectoryInfo DI = new DirectoryInfo(FolderPath);
            Name = DI.Name;
            CreateFromFolder(FolderPath);
            OwnerArchive = Owner;
        }

        /// <summary>
        /// Export this Directory to a folder.
        /// </summary>
        /// <param name="FolderPath">Folder to Export to. Don't expect the files to appear here. Expect a Folder with this <see cref="Name"/> to appear</param>
        public void Export(string FolderPath)
        {
            Directory.CreateDirectory(FolderPath);
            foreach (KeyValuePair<string, object> item in Items)
            {
                if (item.Value is ArchiveFile<OwnerType> file)
                {
                    file.Save(FolderPath + "/" + file.Name);
                }
                else if (item.Value is ArchiveDirectory<OwnerType> directory)
                {
                    string newstring = Path.Combine(FolderPath, directory.Name);
                    Directory.CreateDirectory(newstring);
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
                return (PathSplit.Length > 1 && Items[PathSplit[0]] is ArchiveDirectory<OwnerType> dir) ? dir[Path.Substring(PathSplit[0].Length + 1)] : Items[PathSplit[0]];
            }
            set
            {
                string[] PathSplit = Path.Split('/');
                if (!ItemKeyExists(PathSplit[0]) && !(value is null))
                {
                    ((dynamic)value).Parent = this;
                    if (PathSplit.Length == 1)
                    {
                        if (value is ArchiveDirectory<OwnerType> dir)
                            dir.OwnerArchive = OwnerArchive;
                        ((dynamic)value).Parent = this;
                        Items.Add(PathSplit[0], value);
                    }
                    else
                    {
                        Items.Add(PathSplit[0], new ArchiveDirectory<OwnerType>(OwnerArchive, this) { Name = PathSplit[0] });
                        ((ArchiveDirectory<OwnerType>)Items[PathSplit[0]])[Path.Substring(PathSplit[0].Length + 1)] = value;
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
                    else if (Items[PathSplit[0]] is ArchiveDirectory<OwnerType> dir)
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
            if (PathSplit.Length > 1 && ItemKeyExists(PathSplit[0]) && Items[PathSplit[0]] is ArchiveDirectory<OwnerType> dir)
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
                    result = ((ArchiveDirectory<OwnerType>)Items[result]).GetItemKeyFromNoCase(Path.Substring(PathSplit[0].Length + 1), true);
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
                if (item.Value is ArchiveDirectory<OwnerType> dir)
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Path"></param>
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
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetCountAndChildren()
        {
            int count = 0;
            foreach (KeyValuePair<string, object> item in Items)
            {
                if (item.Value is ArchiveDirectory<OwnerType> dir)
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
        public void MoveItemToDirectory(string ItemKey, ArchiveDirectory<OwnerType> TargetDirectory)
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{Name} - {Items.Count} Item(s)";
        /// <summary>
        /// Create an ArchiveDirectory. You cannot use this function unless this directory is empty
        /// </summary>
        /// <param name="FolderPath"></param>
        /// <param name="OwnerArchive"></param>
        public void CreateFromFolder(string FolderPath, OwnerType OwnerArchive = null)
        {
            if (Items.Count > 0)
                throw new Exception("Cannot create a directory from a folder if Items exist");
            string[] Found = Directory.GetFiles(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < Found.Length; i++)
            {
                ArchiveFile<OwnerType> temp = new ArchiveFile<OwnerType>(Found[i]);
                Items[temp.Name] = temp;
            }

            string[] SubDirs = Directory.GetDirectories(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < SubDirs.Length; i++)
            {
                ArchiveDirectory<OwnerType> temp = new ArchiveDirectory<OwnerType>(SubDirs[i], OwnerArchive);
                Items[temp.Name] = temp;
            }
        }
    }

    /// <summary>
    /// File contained inside the Archive
    /// </summary>
    public class ArchiveFile<OwnerType> where OwnerType : class, IArchive
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
        /// The Actual Data for the file
        /// </summary>
        public byte[] FileData { get; set; }
        /// <summary>
        /// The parent directory (Null if non-existant)
        /// </summary>
        public ArchiveDirectory<OwnerType> Parent { get; set; }
        /// <summary>
        /// Empty file
        /// </summary>
        public ArchiveFile() { }
        /// <summary>
        /// Load a File's Data based on a path
        /// </summary>
        /// <param name="Filepath"></param>
        public ArchiveFile(string Filepath)
        {
            Name = new FileInfo(Filepath).Name;
            FileData = File.ReadAllBytes(Filepath);
        }
        /// <summary>
        /// Create a File from a MemoryStream
        /// </summary>
        /// <param name="name">The name of the file</param>
        /// <param name="ms">The Memory Stream to use</param>
        public ArchiveFile(string name, MemoryStream ms)
        {
            Name = name;
            FileData = ms.ToArray();
        }
        /// <summary>
        /// Saves this file to the Computer's Disk
        /// </summary>
        /// <param name="Filepath">The full path to save to</param>
        public void Save(string Filepath)
        {
            File.WriteAllBytes(Filepath, FileData);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(ArchiveFile<OwnerType> left, ArchiveFile<OwnerType> right) => left.Equals(right);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(ArchiveFile<OwnerType> left, ArchiveFile<OwnerType> right) => !left.Equals(right);
        /// <summary>
        /// Compare this file to another
        /// </summary>
        /// <param name="obj">The Object to check</param>
        /// <returns>True if the files are identical</returns>
        public override bool Equals(object obj)
        {
            return obj is ArchiveFile<OwnerType> file &&
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
        public override string ToString() => $"{Name} [0x{FileData.Length.ToString("X8")}]";

        /// <summary>
        /// The full path of this file. Cannot be used if this file doesn't belong to a RARC object somehow
        /// </summary>
        public string FullPath
        {
            get
            {
                if (Parent?.HasOwnerArchive ?? false)
                {
                    StringBuilder path = new StringBuilder();
                    GetFullPath(path);
                    return path.ToString();
                }
                else
                    throw new InvalidOperationException("In order to use this, this file must be part of a directory with a parent that is connected to a RARC object");
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Path"></param>
        protected void GetFullPath(StringBuilder Path)
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
        public static explicit operator MemoryStream(ArchiveFile<OwnerType> x) => new MemoryStream(x.FileData);
    }
}
