using Hack.io.Interface;
using Hack.io.Utility;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Hack.io.Class;

/// <summary>
/// The base class for Archive like systems
/// </summary>
public abstract class Archive : ILoadSaveFile
{
    #region CONSTANTS
    /// <summary>
    /// String for when the ROOT is null.
    /// </summary>
    protected const string NULL_ROOT_EXCEPTION = "The root of the archive is NULL";
    #endregion

    #region Properties
    /// <summary>
    /// Filename of this Archive.
    /// </summary>
    public string? FileName { get; set; } = null;
    /// <summary>
    /// The Root Directory of the Archive
    /// </summary>
    public ArchiveDirectory? Root { get; set; }

    //INDEXER
    /// <summary>
    /// Get or Set a file based on a path. When setting, if the file doesn't exist, it will be added (Along with any missing subdirectories). Set the file to null to delete it
    /// </summary>
    /// <param name="Path">The Path to take. Does not need the Root name to start, but cannot start with a '/'</param>
    /// <returns></returns>
    public object? this[string Path]
    {
        get
        {
            if (Root is null || Path is null)
                return null;
            if (Path.StartsWith(Root.Name + "/"))
                Path = Path[(Root.Name.Length + 1)..];
            return Root[Path];
        }
        set
        {
            if (!(value is ArchiveFile || value is ArchiveDirectory || value is null))
                throw new Exception($"Invalid object type of {value.GetType()}");

            if (Root is null)
            {
                Root = NewDirectory(this, null);
                Root.Name = Path.Split('/')[0];
            }

            if (Path.StartsWith(Root.Name + "/"))
                Path = Path[(Root.Name.Length + 1)..];

            OnItemSet(value, Path);
            Root[Path] = value;
        }
    }

    //READONLY
    /// <summary>
    /// Get the name of the archive without the path
    /// </summary>
    public string? Name => FileName == null ? null : new FileInfo(FileName).Name;
    /// <summary>
    /// The total amount of files inside this archive.
    /// </summary>
    public int TotalFileCount => Root?.GetCountAndChildren() ?? 0;
    #endregion

    #region Functions
    //PUBLIC
    /// <inheritdoc/>
    public void Load(Stream Strm) => Read(Strm);
    /// <inheritdoc/>
    public void Save(Stream Strm) => Write(Strm);

    /// <summary>
    /// Checks to see if an Item Exists based on a Path
    /// </summary>
    /// <param name="Path">The path to take</param>
    /// <param name="IgnoreCase">Ignore casing of the file</param>
    /// <returns>false if the Item isn't found</returns>
    public bool ItemExists(string Path, bool IgnoreCase = false)
    {
        if (Root is null)
            return false; //There's no way the item can exist if the root doesn't exist. Lol
        if (Path.StartsWith(Root.Name + "/"))
            Path = Path[(Root.Name.Length + 1)..];
        return Root.ItemExists(Path, IgnoreCase);
    }
    /// <summary>
    /// This will return the absolute path of an item if it exists in some way. Useful if you don't know the casing of the filename inside the file. Returns null if nothing is found.
    /// </summary>
    /// <param name="Path">The path to get the Actual path from</param>
    /// <returns>null if nothing is found</returns>
    public string? GetItemKeyFromNoCase(string Path)
    {
        if (Root is null)
            return null;
        if (Path.ToLower().StartsWith(Root.Name.ToLower() + "/"))
            Path = Path[(Root.Name.Length + 1)..];
        return Root.GetItemKeyFromNoCase(Path, true);
    }
    /// <summary>
    /// Clears all the files out of this archive
    /// </summary>
    public void ClearAll() => Root?.Clear();
    /// <summary>
    /// Moves an item to a new directory
    /// </summary>
    /// <param name="OriginalPath"></param>
    /// <param name="NewPath"></param>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public void MoveItem(string OriginalPath, string NewPath)
    {
        if (Root is null)
            throw new NullReferenceException(NULL_ROOT_EXCEPTION); //Can't do anything if the root isn't set

        if (OriginalPath.StartsWith(Root.Name + "/"))
            OriginalPath = OriginalPath[(Root.Name.Length + 1)..];

        if (OriginalPath.Equals(NewPath))
            return;

        if (ItemExists(NewPath))
            throw new InvalidOperationException("An item with that name already exists in that directory");


        dynamic? dest = this[OriginalPath] ?? throw new FileNotFoundException(string.Format(FILENOTFOUNDEXCEPTION, OriginalPath));
        string[] split = NewPath.Split('/');
        dest.Name = split[^1];
        this[OriginalPath] = null;
        this[NewPath] = dest;
    }
    /// <summary>
    /// Search the archive for files that match the regex
    /// </summary>
    /// <param name="Pattern">The regex pattern to use</param>
    /// <param name="RootLevelOnly">If TRUE, all subdirectories will be skipped</param>
    /// <param name="IgnoreCase">Ignore the filename casing</param>
    /// <returns>A list of Archive paths that match the pattern</returns>
    public List<string> FindItems(string Pattern, bool RootLevelOnly = false, bool IgnoreCase = false) => Root?.FindItems(Pattern, RootLevelOnly, IgnoreCase) ?? throw new NullReferenceException(NULL_ROOT_EXCEPTION);

    /// <summary>
    /// Create an Archive from a Folder
    /// </summary>
    /// <param name="Folderpath">Folder to make an archive from</param>
    public void Import(string Folderpath)
    {
        Root = NewDirectory(this, null);
        Root.CreateFromFolder(Folderpath);
    }
    /// <summary>
    /// Dump the contents of this archive to a folder
    /// </summary>
    /// <param name="FolderPath">The Path to save to. Should be a folder</param>
    /// <param name="Overwrite">If there are contents already at the chosen location, delete them?</param>
    public virtual void Export(string FolderPath, bool Overwrite = false)
    {
        if (Root is null)
            throw new NullReferenceException(NULL_ROOT_EXCEPTION);

        FolderPath = Path.Combine(FolderPath, Root.Name);
        if (Directory.Exists(FolderPath))
        {
            if (Overwrite)
            {
                Directory.Delete(FolderPath, true);
                Directory.CreateDirectory(FolderPath);
            }
            else
                throw new Exception("Target directory is occupied");
        }
        else
            Directory.CreateDirectory(FolderPath);

        Root.Export(FolderPath);
    }

    /// <summary>
    /// Reads a file inside this archive into the provided class
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="ArchivePath">The path in the archive (Files only)</param>
    /// <param name="Destination">The reference to the object to load into</param>
    /// <exception cref="FileNotFoundException">Thrown when the file isn't found inside the archive</exception>
    /// <exception cref="InvalidOperationException">Thrown when the selected ArchivePath doesn't lead to an actual file</exception>
    public void ReadFile<T>(string ArchivePath, ref T Destination)
        where T : ILoadSaveFile
    {
        object target = this[ArchivePath] ?? throw new FileNotFoundException(string.Format(FILENOTFOUNDEXCEPTION, ArchivePath));

        if (target is not ArchiveFile af)
            throw new InvalidOperationException("Cannot use a directory.");
        Destination.Load((MemoryStream)af);
    }

    /// <summary>
    /// Writes a file into this archive from the provided class<para/>Note that if the ArchivePath does not exist, it will be created.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="ArchivePath"></param>
    /// <param name="Source"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void WriteFile<T>(string ArchivePath, ref T Source)
        where T : ILoadSaveFile
    {
        string[] pathsplit = ArchivePath.Split('/');
        object target = this[ArchivePath] ?? (ArchivePath.EndsWith('/') ? throw new InvalidOperationException("Cannot create directories here.") : new ArchiveFile() { Name = pathsplit[^1] });

        if (target is not ArchiveFile af)
            throw new InvalidOperationException("Cannot use a directory.");

        MemoryStream Data = new();
        Source.Save(Data);
        af.Load(Data);
        this[ArchivePath] = af;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        int Count = Root?.GetCountAndChildren() ?? 0;
        return $"{Name} - {Count} File{(Count > 1 ? "s" : "")} total";
    }

    //PROTECTED
    /// <summary>
    /// The Binary I/O function for reading the file
    /// </summary>
    /// <param name="ArchiveFile"></param>
    protected abstract void Read(Stream ArchiveFile);
    /// <summary>
    /// The Binary I/O function for writing the file
    /// </summary>
    /// <param name="ArchiveFile"></param>
    protected abstract void Write(Stream ArchiveFile);

    /// <summary>
    /// Executed when you use ArchiveBase["FilePath"] to set a file
    /// </summary>
    /// <param name="value"></param>
    /// <param name="Path"></param>
    protected virtual void OnItemSet(object? value, string Path)
    {
        //Does nothing in the base class
    }

    /// <summary>
    /// Creates a new directory
    /// </summary>
    /// <returns></returns>
    protected virtual ArchiveDirectory NewDirectory() => new();
    /// <summary>
    /// Creates a new directory
    /// </summary>
    /// <param name="Owner">The owner Archive</param>
    /// <param name="parent">The parent directory</param>
    /// <returns></returns>
    protected virtual ArchiveDirectory NewDirectory(Archive? Owner, ArchiveDirectory? parent) => new(Owner, parent);
    #endregion

    private const string FILENOTFOUNDEXCEPTION = "The path \"{0}\" could not be found";
}

/// <summary>
/// Folder contained inside the Archive. Can contain more <see cref="ArchiveDirectory"/>s if desired, as well as <see cref="ArchiveFile"/>s
/// </summary>
public class ArchiveDirectory
{
    #region CONSTANTS
    private const string DEFAULT_DIRECTORY_NAME = "NewDirectory";
    #endregion

    #region Properties
    /// <summary>
    /// The name of the Directory
    /// </summary>
    public string Name { get; set; } = DEFAULT_DIRECTORY_NAME;
    /// <summary>
    /// The contents of this directory.
    /// </summary>
    public Dictionary<string, object> Items { get; set; } = [];
    /// <summary>
    /// The parent directory (Null if non-existant)
    /// </summary>
    public ArchiveDirectory? Parent { get; set; }

    //INDEXER
    /// <summary>
    /// Get or Set a file based on a path. When setting, if the file doesn't exist, it will be added (Along with any missing subdirectories)
    /// </summary>
    /// <param name="Path">The Path to take</param>
    /// <returns></returns>
    public object? this[string Path]
    {
        get
        {
            string[] PathSplit = Path.Split('/');
            if (!ItemKeyExists(PathSplit[0]))
                return null;
            return (PathSplit.Length > 1 && Items[PathSplit[0]] is ArchiveDirectory dir) ? dir[Path[(PathSplit[0].Length + 1)..]] : Items[PathSplit[0]];
        }
        set
        {
            string[] PathSplit = Path.Split('/');
            if (!ItemKeyExists(PathSplit[0]) && value is not null)
            {
                ((dynamic)value).Parent = this;
                if (PathSplit.Length == 1)
                {
                    if (value is ArchiveDirectory dir)
                        dir.OwnerArchive = OwnerArchive;
                    ((dynamic)value).Parent = this;
                    Items.Add(PathSplit[0], value);

                    if (value is ArchiveFile f && string.IsNullOrEmpty(f.Name))
                    {
                        f.Name = PathSplit[0]; //If the file has no name, assign it the name defined in the path
                    }
                }
                else
                {
                    if (OwnerArchive is null)
                        throw new NullReferenceException($"Cannot auto create a new Directory without an {nameof(OwnerArchive)}");

                    ArchiveDirectory dir = NewDirectory(OwnerArchive, this);
                    dir.Name = PathSplit[0];
                    Items.Add(PathSplit[0], dir);
                    ((ArchiveDirectory)Items[PathSplit[0]])[Path[(PathSplit[0].Length + 1)..]] = value;
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

                        if (value is ArchiveFile f && string.IsNullOrEmpty(f.Name))
                        {
                            f.Name = PathSplit[0]; //If the file has no name, assign it the name defined in the path
                        }
                    }
                }
                else if (Items[PathSplit[0]] is ArchiveDirectory dir)
                    dir[Path[(PathSplit[0].Length + 1)..]] = value;
            }
        }
    }
    
    //READONLY
    /// <summary>
    /// The full path of this directory. Cannot be used if this .arc doesn't belong to a RARC object
    /// </summary>
    public string FullPath
    {
        get
        {
            if (OwnerArchive != null)
            {
                StringBuilder path = new();
                GetFullPath(path);
                return path.ToString();
            }
            else
                throw new InvalidOperationException("In order to use this, this directory must be part of a directory with a parent that is connected to an Archive object");
        }
    }
    /// <summary>
    /// Returns the amount of Items in this directory (Items in subdirectories not included)
    /// </summary>
    public int Count => Items.Count;
    /// <summary>
    /// Checks to see if this directory has an owner archive
    /// </summary>
    public bool HasOwnerArchive => OwnerArchive != null;
    #endregion

    #region Fields
    /// <summary>
    /// The Archive that owns this directory
    /// </summary>
    public Archive? OwnerArchive;
    #endregion

    #region Constructors
    /// <summary>
    /// Create a new Archive Directory
    /// </summary>
    public ArchiveDirectory() { }
    /// <summary>
    /// Create a new, child directory
    /// </summary>
    /// <param name="Owner">The Owner Archive</param>
    /// <param name="parentdir">The Parent Directory. NULL if this is the Root Directory</param>
    public ArchiveDirectory(Archive? Owner, ArchiveDirectory? parentdir) { OwnerArchive = Owner; Parent = parentdir; }
    #endregion

    #region Functions
    /// <summary>
    /// Create an ArchiveDirectory. You cannot use this function unless this directory is empty
    /// </summary>
    /// <param name="FolderPath">The Disk folder path to import</param>
    /// <param name="OwnerArchive">The <paramref name="OwnerArchive"/> [Optional]</param>
    public void CreateFromFolder(string FolderPath, Archive? OwnerArchive = null)
    {
        if (Items.Count > 0)
            throw new Exception("Cannot create a directory from a folder if Items exist");
        string[] Found = Directory.GetFiles(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < Found.Length; i++)
        {
            ArchiveFile temp = new()
            {
                Name = new FileInfo(Found[i]).Name
            };
            FileUtil.LoadFile(Found[i], temp.Load);
            Items[temp.Name] = temp;
        }

        string[] SubDirs = Directory.GetDirectories(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < SubDirs.Length; i++)
        {
            ArchiveDirectory temp = NewDirectory();
            temp.OwnerArchive = OwnerArchive;
            temp.CreateFromFolder(SubDirs[i]);
            Items[temp.Name] = temp;
        }
    }
    /// <summary>
    /// Checks to see if an Item Exists based on a Path
    /// </summary>
    /// <param name="Path">The path to take</param>
    /// <param name="IgnoreCase">Ignore casing</param>
    /// <returns>false if the Item isn't found</returns>
    public bool ItemExists(string Path, bool IgnoreCase = false)
    {
        string[] PathSplit = Path.Split('/');
        if (PathSplit.Length > 1 && ItemKeyExists(PathSplit[0]) && Items[PathSplit[0]] is ArchiveDirectory dir)
            return dir.ItemExists(Path[(PathSplit[0].Length + 1)..], IgnoreCase);
        else if (PathSplit.Length > 1)
            return false;
        else
            return ItemKeyExists(PathSplit[0], IgnoreCase);
    }
    /// <summary>
    /// Checks to see if an item exists in this directory only
    /// </summary>
    /// <param name="ItemName">The name of the Item to look for (Case Sensitive)</param>
    /// <param name="IgnoreCase">Ignore casing</param>
    /// <returns>false if the Item doesn't exist</returns>
    public bool ItemKeyExists(string ItemName, bool IgnoreCase = false)
    {
        if (!IgnoreCase)
            return Items.ContainsKey(ItemName);

        foreach (KeyValuePair<string, object> item in Items)
            if (item.Key.Equals(ItemName, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
    /// <summary>
    /// Gets an Item Key without Case Sensitivity
    /// </summary>
    /// <param name="Path">Archive path to the file.</param>
    /// <param name="AttachRootName">if TRUE, will attatch the archive root name at the front</param>
    /// <param name="Comparison">Don't need to set.</param>
    /// <returns>NULL if the item cannot be found. Otherwise, a full archive path.</returns>
    /// <exception cref="ArgumentException"></exception>
    public string? GetItemKeyFromNoCase(string Path, bool AttachRootName = false, StringComparison Comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrWhiteSpace(Path))
            throw new ArgumentException($"Invalid {nameof(Path)} \"{Path}\"", nameof(Path));

        string[] PathSplit = Path.Split('/');
        if (PathSplit.Length > 1)
        {
            string? result = Items.FirstOrDefault(x => string.Equals(x.Key, PathSplit[0], Comparison)).Key;
            if (result == null)
                return null;
            else
            {
                dynamic? target = Items[result];
                if (target == null)
                    return null;
                ArchiveDirectory dir = (ArchiveDirectory)target;
                string pth = Path[(PathSplit[0].Length + 1)..];
                result = dir.GetItemKeyFromNoCase(pth, true, Comparison);
            }
            return result == null ? null : (AttachRootName ? Name + "/" : "") + result;
        }
        else if (PathSplit.Length > 1)
            return null;
        else
        {
            string result = Items.FirstOrDefault(x => string.Equals(x.Key, PathSplit[0], Comparison)).Key;
            return result == null ? null : (AttachRootName ? Name + "/" : "") + result;
        }
    }
    /// <summary>
    /// Search the directory for files that match the regex
    /// </summary>
    /// <param name="Pattern">The regex pattern to use</param>
    /// <param name="TopLevelOnly">If true, all subdirectories will be skipped</param>
    /// <param name="IgnoreCase">Ignore the filename casing</param>
    /// <returns>List of Item Keys</returns>
    public List<string> FindItems(string Pattern, bool TopLevelOnly = false, bool IgnoreCase = false)
    {
        List<string> results = [];
        StringComparison sc = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        RegexOptions ro = (IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) | RegexOptions.Singleline;
        foreach (KeyValuePair<string, object> item in Items)
        {
            if (item.Value is ArchiveFile File)
            {
                if (File.Name is null)
                    continue;
                //Performance Enhancement
                if ((Pattern.StartsWith('*') && File.Name.EndsWith(Pattern[1..], sc)) || (Pattern.EndsWith('*') && File.Name.StartsWith(Pattern[^1..], sc)))
                    goto Success;

                string regexPattern = StringUtil.WildCardToRegex(Pattern);
                if (Regex.IsMatch(File.Name, regexPattern, ro))
                    goto Success;

                continue;
            Success:
                results.Add(File.FullPath);
            }
            else if (item.Value is ArchiveDirectory Directory && !TopLevelOnly)
                results.AddRange(Directory.FindItems(Pattern, IgnoreCase: IgnoreCase));
        }
        return results;
    }
    /// <summary>
    /// Moves an item from it's current directory to a new directory
    /// </summary>
    /// <param name="ItemKey">The Key of the Item</param>
    /// <param name="TargetDirectory">The directory to move the item to</param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    public void MoveItemToDirectory(string ItemKey, ArchiveDirectory TargetDirectory)
    {
        if (TargetDirectory.ItemKeyExists(ItemKey))
            throw new UnauthorizedAccessException($"There is already a file with the name {ItemKey} inside {TargetDirectory.Name}");

        TargetDirectory[ItemKey] = Items[ItemKey];
        Items.Remove(ItemKey);
    }
    /// <summary>
    /// Rename an item in the directory
    /// </summary>
    /// <param name="OldKey">The old key of the item (Case-sensitive)</param>
    /// <param name="NewKey">The new key of the item (Case-sensitive)</param>
    public void RenameItem(string OldKey, string NewKey)
    {
        if (ItemKeyExists(NewKey))
            throw new Exception($"There is already a file with the name {NewKey} inside {Name}");
        dynamic activeitem = Items[OldKey];
        Items.Remove(OldKey);
        activeitem.Name = NewKey;
        Items.Add(NewKey, activeitem);
    }
    /// <summary>
    /// Clears all the items out of this directory
    /// </summary>
    public void Clear()
    {
        foreach (KeyValuePair<string, object> item in Items)
        {
            if (item.Value is ArchiveDirectory dir)
                dir.Clear();
        }
        Items.Clear();
    }
    /// <summary>
    /// Gets the number of items inside this directory and it's children
    /// </summary>
    /// <returns>the total number of items inside this directory. Directories themselves not included.</returns>
    public int GetCountAndChildren()
    {
        int count = 0;
        foreach (KeyValuePair<string, object> item in Items)
        {
            if (item.Value is ArchiveDirectory dir)
                count += dir.GetCountAndChildren();
            else
                count++;
        }
        return count;
    }
    /// <summary>
    /// Sorts the Items inside this directory using the provided string[]. This string[] MUST contain all entries inside this directory
    /// </summary>
    /// <param name="NewItemOrder"></param>
    public void SortItemsByOrder(string[] NewItemOrder)
    {
        if (NewItemOrder.Length != Items.Count)
            throw new Exception("Missing Items that exist in this Directory, but not in the provided Item Order");
        Dictionary<string, object> NewItems = [];
        for (int i = 0; i < NewItemOrder.Length; i++)
        {
            if (!Items.TryGetValue(NewItemOrder[i], out object? value))
                throw new Exception("Missing Items that exist in this Directory, but not in the provided Item Order (Potentually a typo)");
            NewItems.Add(NewItemOrder[i], value);
        }
        Items = NewItems;
    }
    /// <summary>
    /// Export this Directory to a folder.
    /// </summary>
    /// <param name="FolderPath">Folder to Export to. Don't expect the files to appear here. Expect a Folder with this <see cref="Name"/> to appear</param>
    public void Export(string FolderPath)
    {
        FileUtil.CreateDirectoryIfNotExist(FolderPath);
        foreach (KeyValuePair<string, object> item in Items)
        {
            if (item.Value is ArchiveFile file)
            {
                FileUtil.SaveFile(FolderPath + "/" + file.Name, file.Save);
            }
            else if (item.Value is ArchiveDirectory directory)
            {
                string newstring = Path.Combine(FolderPath, directory.Name);
                FileUtil.CreateDirectoryIfNotExist(newstring);
                directory.Export(newstring);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Name} - {Items.Count} Item(s)";

    //PROTECTED
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    protected virtual ArchiveDirectory NewDirectory() => new();
    /// <summary>
    /// 
    /// </summary>
    /// <param name="Owner"></param>
    /// <param name="parent"></param>
    /// <returns></returns>
    protected virtual ArchiveDirectory NewDirectory(Archive? Owner, ArchiveDirectory parent) => new(Owner, parent);

    //INTERNAL
    internal void GetFullPath(StringBuilder Path)
    {
        if (Parent != null)
        {
            Parent.GetFullPath(Path);
            Path.Append('/');
            Path.Append(Name);
        }
        else
        {
            Path.Append(Name);
        }
    }
    #endregion
}

/// <summary>
/// File contained inside the Archive
/// </summary>
public class ArchiveFile : ILoadSaveFile
{
    #region CONSTANTS
    private const string FILEDATA_IS_NULL = "FileData is NULL";
    #endregion

    #region Properties
    /// <summary>
    /// Name of the File
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// The Actual Data for the file
    /// </summary>
    public byte[]? FileData { get; set; }
    /// <summary>
    /// The parent directory (Null if non-existant)
    /// </summary>
    public ArchiveDirectory? Parent { get; set; }

    //READONLY
    /// <summary>
    /// The full path of this file. Cannot be used if this file doesn't belong to an Archive somehow
    /// </summary>
    public string FullPath
    {
        get
        {
            if (Parent?.HasOwnerArchive ?? false)
            {
                StringBuilder path = new();
                GetFullPath(path);
                return path.ToString();
            }
            else
                throw new InvalidOperationException("In order to use this, this file must be part of a directory with a parent that is connected to an Archive object");
        }
    }
    /// <summary>
    /// The extension of this file
    /// </summary>
    public string? Extension
    {
        get
        {
            if (Name is null)
                return null;
            string[] parts = Name.Split('.');
            if (parts.Length == 1)
                return "";
            return "." + parts[^1].ToLower();
        }
    }
    /// <summary>
    /// The length of the file in bytes. Shortcut for FileData.Length.
    /// </summary>
    public int Length => FileData?.Length ?? -1;
    #endregion

    #region Constructors
    /// <summary>
    /// Empty file
    /// </summary>
    public ArchiveFile() { }
    #endregion

    #region Functions
    //PUBLIC
    /// <inheritdoc/>
    public void Load(Stream Strm) => FileData = Strm.ToArray();
    /// <inheritdoc/>
    public void Save(Stream Strm)
    {
        if (FileData is null)
            throw new InvalidOperationException(FILEDATA_IS_NULL);
        Strm.Write(FileData);
    }

    /// <summary>
    /// Copies the data from this <see cref="ArchiveFile"/> to another <see cref="ArchiveFile"/>
    /// </summary>
    /// <param name="target">The destination <see cref="ArchiveFile"/> instance</param>
    public void CopyTo(ArchiveFile target)
    {
        target.Name = Name;
        target.FileData = null;
        if (FileData is not null)
        {
            target.FileData = new byte[FileData.Length];
            FileData.CopyTo(target.FileData, 0);
        }
        target.Parent = Parent;
    }
    
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ArchiveFile file &&
               string.Equals(Name,file.Name) &&
               string.Equals(Extension, file.Extension) &&
               CollectionUtil.Equals(FileData, file.FileData);
    }
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Extension, FileData);
    /// <inheritdoc/>
    public override string ToString() => $"{Name ?? "<null>"} [0x{FileData?.Length ?? 0:X8}]";

    //PROTECTED
    /// <summary>
    /// This is a helper function.<para/>
    /// Gets the full path of this archive file.
    /// </summary>
    /// <param name="Path">The incoming path to append</param>
    protected void GetFullPath(StringBuilder Path)
    {
        if (Parent != null)
        {
            Parent.GetFullPath(Path);
            Path.Append('/');
        }
        Path.Append(Name);
    }

    //OPERATORS
    /// <summary>
    /// Cast a File to a MemoryStream
    /// </summary>
    /// <param name="x"></param>
    public static explicit operator MemoryStream(ArchiveFile x) => new(x.FileData ?? throw new NullReferenceException(FILEDATA_IS_NULL));
    #endregion
}