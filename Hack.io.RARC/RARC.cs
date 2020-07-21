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
        /// <summary>
        /// Filename of this Archive.
        /// <para/>Set using <see cref="Save(string)"/>;
        /// </summary>
        public string FileName { get; private set; } = null;
        /// <summary>
        /// Get the name of the archive without the path
        /// </summary>
        public string Name { get { return FileName == null ? FileName: new FileInfo(FileName).Name; } }
        /// <summary>
        /// The Root Directory of the Archive
        /// </summary>
        public RARCDirectory Root { get; set; }
        /// <summary>
        /// File Identifier
        /// </summary>
        private readonly string Magic = "RARC";

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
        /// <summary>
        /// Save the Archive
        /// </summary>
        /// <param name="Filename">New file to save to</param>
        public void Save(string Filename = null)
        {
            if (FileName == null && Filename == null)
                throw new Exception("No Filename has been given");
            else if (Filename != null)
                FileName = Filename;

            Root.BuildType(true);
            
            if (Root.Type != "ROOT")
                Root.Type = "ROOT";

            FileStream RARCFile = new FileStream(FileName, FileMode.Create);
            RARCFile.WriteString(Magic);
            RARCFile.Write(new byte[12] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x20, 0xDD, 0xDD, 0xDD, 0xDD }, 0, 12);
            RARCFile.Write(new byte[16] { 0xEE, 0xEE, 0xEE, 0xEE, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}, 0, 16);
            RARCFile.WriteReverse(BitConverter.GetBytes(Root.CountAllDirectories()+1), 0, 4);
            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            List<RARCDirectory> AllDirectories = Root.GetAllDirectories();
            List<KeyValuePair<uint, RARCDirectory>> AllDirIDs = new List<KeyValuePair<uint, RARCDirectory>> { new KeyValuePair<uint, RARCDirectory>(0, Root) };
            for (int i = 0; i < AllDirectories.Count; i++)
                AllDirIDs.Add(new KeyValuePair<uint, RARCDirectory>((uint)i + 1, AllDirectories[i]));
            int TotalFileCount = 0, TempCount = 0;
            List<int> FolderValues = new List<int>();
            for (int i = 0; i < AllDirIDs.Count; i++)
            {
                TempCount += 2;
                TempCount += AllDirIDs[i].Value.Files.Count;
                TempCount += AllDirIDs[i].Value.SubDirectories.Count;
                FolderValues.Add(TempCount);
                TotalFileCount += TempCount;
                TempCount = 0;
            }
            RARCFile.WriteReverse(BitConverter.GetBytes(TotalFileCount), 0, 4);
            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //String Table Size
            RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4); //string Table Location (-0x20)
            RARCFile.WriteReverse(BitConverter.GetBytes((ushort)TotalFileCount), 0, 2);
            RARCFile.Write(new byte[2] { 0x01, 0x00 }, 0, 2);
            RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
            long DirectoryEntryOffset = RARCFile.Position;
            RARCFile.WriteString(Root.Type);
            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(Root.Name)), 0, 2);
            RARCFile.WriteReverse(BitConverter.GetBytes((ushort)FolderValues[0]), 0, 2);
            RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            for (int i = 0; i < AllDirectories.Count; i++)
            {
                RARCFile.WriteString(AllDirectories[i].Type);
                RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
                RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(AllDirectories[i].Name)),0,2);
                RARCFile.WriteReverse(BitConverter.GetBytes((ushort)FolderValues[i + 1]), 0, 2);
                RARCFile.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4);
            }
            #region Padding
            while (RARCFile.Position % 32 != 0)
                RARCFile.WriteByte(0x00);
            #endregion
            long FileEntryOffset = RARCFile.Position;

            List<RARCDirEntry> FinalDIRSetup = UnbuildFolder(AllDirIDs);

            List<RARCFileEntry> FinalFileIDs = new List<RARCFileEntry>();
            uint CurrentID = 0;

            for (int i = 0; i < FinalDIRSetup.Count; i++)
            {
                for (int j = 0; j < FinalDIRSetup[i].Files.Count; j++)
                {
                    RARCFile.WriteReverse(BitConverter.GetBytes((ushort)FinalDIRSetup[i].Files[j].ID), 0, 2);
                    RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(FinalDIRSetup[i].Files[j].Name)), 0, 2);
                    RARCFile.Write(new byte[2] { 0x11, 0x00 }, 0, 2);
                    RARCFile.Write(new byte[2] { 0xDD, 0xDD }, 0, 2);
                    RARCFile.Write(new byte[4] { 0xEE, 0xEE, 0xEE, 0xEE }, 0, 4);
                    RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].Files[j].FileData.Length), 0, 4);
                    RARCFile.Write(new byte[4], 0, 4);

                    FinalFileIDs.Add(new RARCFileEntry() { FileID = (short)FinalDIRSetup[i].Files[j].ID, Name = FinalDIRSetup[i].Files[j].Name, ModularA = 0, ModularB = 0x10, Type = 0x1100 });
                    CurrentID++;
                }
                for (int j = 0; j < FinalDIRSetup[i].SubDirIDs.Count; j++)
                {
                    RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                    RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(FinalDIRSetup[(int)FinalDIRSetup[i].SubDirIDs[j]].Name)), 0, 2);
                    RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
                    RARCFile.Write(new byte[2] { 0xDD, 0xDD }, 0, 2);
                    RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].SubDirIDs[j]), 0, 4);
                    RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
                    RARCFile.Write(new byte[4], 0, 4);
                    FinalFileIDs.Add(new RARCFileEntry() { FileID = -1, Name = FinalDIRSetup[(int)FinalDIRSetup[i].SubDirIDs[j]].Name, ModularA = (int)FinalDIRSetup[(int)FinalDIRSetup[i].SubDirIDs[j]].ID, ModularB = 0x10, Type = 0x0200 });
                    CurrentID++;
                }
                RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(".")), 0, 2);
                RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
                RARCFile.Write(new byte[2] { 0x00, 0x00 }, 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].ID), 0, 4);
                RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
                RARCFile.Write(new byte[4], 0, 4);
                FinalFileIDs.Add(new RARCFileEntry() { FileID = -1, Name = ".", ModularA = (int)FinalDIRSetup[i].ID, ModularB = 0x10, Type = 0x0200 });
                CurrentID++;

                RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash("..")), 0, 2);
                RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
                RARCFile.Write(new byte[2] { 0x00, 0x02 }, 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].ParentID), 0, 4);
                RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
                RARCFile.Write(new byte[4], 0, 4);
                FinalFileIDs.Add(new RARCFileEntry() { FileID = -1, Name = "..", ModularA = (int)FinalDIRSetup[i].ParentID, ModularB = 0x10, Type = 0x0200 });
                CurrentID++;
            }

            //string XML = "";
            //for (int i = 0; i < FinalFileIDs.Count; i++)
            //{
            //    XML += $"<RarcFileEntry ID=\"{FinalFileIDs[i].FileID}\"\t Name=\"{FinalFileIDs[i].Name}\"\t\t Type=\"{FinalFileIDs[i].Type.ToString("X").PadLeft(4, '0')}\"\t FileOrDirectory=\"{FinalFileIDs[i].ModularA.ToString("X").PadLeft(8, '0')}\"\t Size=\"{FinalFileIDs[i].ModularB.ToString("X").PadLeft(8, '0')}\" />\n";
            //}
            //File.WriteAllText("ONew.xml", XML);

            #region Padding
            while (RARCFile.Position % 32 != 0)
                RARCFile.WriteByte(0x00);
            #endregion
            long StringTableOffset = RARCFile.Position;
            List<string> StringTable = new List<string>() { ".", ".." };
            for (int i = 0; i < FinalDIRSetup.Count; i++)
            {
                if (!StringTable.Any(O => O.Equals(FinalDIRSetup[i].Name)))
                    StringTable.Add(FinalDIRSetup[i].Name);
                for (int j = 0; j < FinalDIRSetup[i].Files.Count; j++)
                    if (!StringTable.Any(O => O.Equals(FinalDIRSetup[i].Files[j].Name)))
                        StringTable.Add(FinalDIRSetup[i].Files[j].Name);
            }
            for (int i = 0; i < StringTable.Count; i++)
                RARCFile.WriteString(StringTable[i], 0x00);

            #region Padding
            while (RARCFile.Position % 32 != 0)
                RARCFile.WriteByte(0x00);
            #endregion

            long FileTableOffset = RARCFile.Position;
            List<KeyValuePair<byte[], long>> FileOffsets = new List<KeyValuePair<byte[], long>>();

            for (int i = 0; i < FinalDIRSetup.Count; i++)
            {
                for (int j = 0; j < FinalDIRSetup[i].Files.Count; j++)
                {
                    FileOffsets.Add(new KeyValuePair<byte[], long>(FinalDIRSetup[i].Files[j].FileData, RARCFile.Position - FileTableOffset));
                    RARCFile.Write(FinalDIRSetup[i].Files[j].FileData, 0, FinalDIRSetup[i].Files[j].FileData.Length);
                    #region Padding
                    while (RARCFile.Position % 32 != 0)
                        RARCFile.WriteByte(0x00);
                    #endregion
                }
            }


            #region Offset Writing
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

            #region Directory Entries
            RARCFile.Position = DirectoryEntryOffset;
            bool breakout = false;
            for (int i = 0; i < FinalDIRSetup.Count; i++)
            {
                RARCFile.Position += 0x04;
                RARCFile.WriteReverse(BitConverter.GetBytes(GetStringOffset(StringTable, FinalDIRSetup[i].Name)), 0, 4);
                RARCFile.Position += 0x04;
                int j = 0;
                for (j = 0; j < FinalFileIDs.Count; j++)
                {
                    if (FinalDIRSetup[i].SubDirIDs.Count == 0 && FinalDIRSetup[i].Files.Count == 0)
                    {
                        for (int x = 0; x < FinalFileIDs.Count; x++)
                        {
                            if ((FinalFileIDs[x].Type == 0x0200 && FinalFileIDs[x].Name.Equals(".")) && (FinalFileIDs[x].ModularA == FinalDIRSetup[i].ID))
                            {
                                RARCFile.WriteReverse(BitConverter.GetBytes(x), 0, 4);
                                breakout = true;
                                break;
                            }
                        }
                        if (breakout)
                            break;
                    }
                    else if (FinalDIRSetup[i].Files.Count == 0)
                    {
                        if (FinalFileIDs[j].ModularA == FinalDIRSetup[i].SubDirIDs[0])
                        {
                            RARCFile.WriteReverse(BitConverter.GetBytes(j), 0, 4);
                            break;
                        }
                    }
                    else
                    {
                        if (FinalFileIDs[j].FileID == FinalDIRSetup[i].Files[0].ID)
                        {
                            RARCFile.WriteReverse(BitConverter.GetBytes(j), 0, 4);
                            break;
                        }
                    }
                }
            }
            ;
            #endregion

            #region File entries
            RARCFile.Position = FileEntryOffset;
            for (int i = 0; i < FinalDIRSetup.Count; i++)
            {
                for (int j = 0; j < FinalDIRSetup[i].Files.Count; j++)
                {
                    RARCFile.Position += 0x06;
                    RARCFile.WriteReverse(BitConverter.GetBytes((ushort)GetStringOffset(StringTable, FinalDIRSetup[i].Files[j].Name)), 0, 2);
                    for (int x = 0; x < FileOffsets.Count; x++)
                    {
                        if (FileOffsets[x].Key == FinalDIRSetup[i].Files[j].FileData)
                        {
                            RARCFile.WriteReverse(BitConverter.GetBytes((uint)FileOffsets[x].Value), 0, 4);
                            break;
                        }
                    }
                    RARCFile.Position += 0x08;
                }
                for (int j = 0; j < FinalDIRSetup[i].SubDirIDs.Count; j++)
                {
                    RARCFile.Position += 0x06;
                    RARCFile.WriteReverse(BitConverter.GetBytes((ushort)GetStringOffset(StringTable, FinalDIRSetup[(int)FinalDIRSetup[i].SubDirIDs[j]].Name)), 0, 2);
                    RARCFile.Position += 0x0C;
                }
                RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash(".")), 0, 2);
                RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
                RARCFile.Write(new byte[2] { 0x00, 0x00 }, 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(i), 0, 4);
                RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
                RARCFile.Write(new byte[4], 0, 4);

                RARCFile.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(StringToHash("..")), 0, 2);
                RARCFile.Write(new byte[2] { 0x02, 0x00 }, 0, 2);
                RARCFile.Write(new byte[2] { 0x00, 0x02 }, 0, 2);
                RARCFile.WriteReverse(BitConverter.GetBytes(FinalDIRSetup[i].ParentID), 0, 4);
                RARCFile.Write(new byte[4] { 0x00, 0x00, 0x00, 0x10 }, 0, 4);
                RARCFile.Write(new byte[4], 0, 4);
            }
            #endregion
            #endregion

            RARCFile.Close();
        }
        /// <summary>
        /// Create an Archive from a Folder
        /// </summary>
        /// <param name="Folderpath">Folder to make an archive from</param>
        public void Import(string Folderpath) => Root = new RARCDirectory(Folderpath);
        /// <summary>
        /// Dump the contents of this archive to a folder
        /// </summary>
        /// <param name="FolderPath">The Path to save to. Should be a folder</param>
        /// <param name="Overwrite">If there are contents already at the chosen location, delete them?</param>
        public void Export(string FolderPath, bool Overwrite = false)
        {
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
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            int Count = Root.CountAllFiles();
            return $"{new FileInfo(FileName).Name} - {Count} File{(Count > 1 ? "s" : "")} total";
        }
        /// <summary>
        /// Search the archive for a file. Returns a list in case there are multiple different files with the same name. (Though this is unlikely)
        /// </summary>
        /// <param name="Filename">Filename to search for</param>
        /// <returns>List of RARC Files</returns>
        public List<RARCFile> FindFile(string Filename) => Root.Search(Filename);
        /// <summary>
        /// Search the archive for all files with a given extension
        /// </summary>
        /// <param name="Extension"></param>
        /// <returns></returns>
        public List<RARCFile> FindFileTypes(string Extension)
        {
            if (!Extension.StartsWith("."))
                Extension = "." + Extension;

            return Root.SearchExtensions(Extension);
        }
        /// <summary>
        /// Replaces a file in the archive based on it's name
        /// </summary>
        /// <param name="FileToReplace">Name of the file to replace</param>
        /// <param name="NewFile">Data to replace</param>
        public bool ReplaceFileByName(string FileToReplace, RARCFile NewFile) => Root.ReplaceFile(FileToReplace, NewFile);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        public void ReplaceOrAdd(RARCFile file) => Root.ReplaceOrAdd(file, false);

        private RARCDirectory BuildFolder(List<RARCDirEntry> AllDirectories)
        {
            RARCDirectory ROOT = new RARCDirectory();

            for (int i = 0; i < AllDirectories.Count; i++)
            {
                if (AllDirectories[i].ParentID == 0xFFFFFFFF)
                {
                    ROOT = new RARCDirectory() { ID = AllDirectories[i].ID, Files = AllDirectories[i].Files, Type = AllDirectories[i].Type, Name = AllDirectories[i].Name };
                    break;
                }
            }

            for (int i = 0; i < AllDirectories.Count; i++)
            {
                if (AllDirectories[i].ParentID == 0)
                {
                    ROOT.SubDirectories.Add(new RARCDirectory() { ID = AllDirectories[i].ID, Files = AllDirectories[i].Files, Type = AllDirectories[i].Type, Name = AllDirectories[i].Name });
                }
                else if (AllDirectories[i].ParentID != 0xFFFFFFFF)
                {
                    SetDirList(ROOT, AllDirectories[i]);
                }
            }
            
            return ROOT;
        }

        private List<RARCDirEntry> UnbuildFolder(List<KeyValuePair<uint, RARCDirectory>> AllDirs)
        {
            List<RARCDirEntry> Data = new List<RARCDirEntry>();

            #region Setup the Directory Entries
            for (int i = 0; i < AllDirs.Count; i++)
            {
                RARCDirEntry Dir = new RARCDirEntry() { Files = AllDirs[i].Value.Files, Name = AllDirs[i].Value.Name, Type = AllDirs[i].Value.Type, ID = (uint)i };
                for (int j = 0; j < AllDirs[i].Value.SubDirectories.Count; j++)
                    for (int x = 0; x < AllDirs.Count; x++)
                        if (AllDirs[x].Value == AllDirs[i].Value.SubDirectories[j])
                            Dir.SubDirIDs.Add(AllDirs[x].Key);
                
                Data.Add(Dir);
            } 
            #endregion

            #region Setup File IDs
            int FileID = 0;
            for (int i = 0; i < Data.Count; i++)
            {
                for (int j = 0; j < Data[i].Files.Count; j++)
                {
                    Data[i].Files[j].ID = FileID;
                    FileID++;
                }
                //FileID += 3;
            }
            #endregion

            #region Setup Parent IDs
            for (int i = 0; i < AllDirs.Count; i++)
            {
                RARCDirEntry Dir = new RARCDirEntry() { Files = AllDirs[i].Value.Files, Name = AllDirs[i].Value.Name, Type = AllDirs[i].Value.Type };
                for (int j = 0; j < AllDirs[i].Value.SubDirectories.Count; j++)
                    for (int x = 0; x < AllDirs.Count; x++)
                        if (AllDirs[x].Value == AllDirs[i].Value.SubDirectories[j])
                        {
                            Data[x].ParentID = (uint)i;
                            break;
                        }
            } 
            #endregion

            return Data;
        }

        private void SetDirList(RARCDirectory Root, RARCDirEntry SubDir)
        {
            for (int i = 0; i < Root.SubDirectories.Count; i++)
            {
                if (Root.SubDirectories[i].ID == SubDir.ParentID)
                {
                    Root.SubDirectories[i].SubDirectories.Add(new RARCDirectory() { ID = SubDir.ID, Files = SubDir.Files, Type = SubDir.Type, Name = SubDir.Name });
                }
                SetDirList(Root.SubDirectories[i], SubDir);
            }
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

        private uint GetStringOffset(List<string> StringTable, string Target)
        {
            int Offset = 0;
            for (int i = 0; i < StringTable.Count; i++)
            {
                if (StringTable[i].Equals(Target))
                    break;

                Offset += StringTable[i].Length + 1;
            }
            return (uint)Offset;
        }

        private void Read(Stream RARCFile)
        {
            if (RARCFile.ReadString(4) != Magic)
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

            uint FileSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0), TrashData = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0),
                DataOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;

            RARCFile.Position += 0x10;
            uint DirectoryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0), DirectoryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20,
                FileEntryCount = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0), FileEntryTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;
            RARCFile.Position += 0x04;
            uint StringTableOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0) + 0x20;

            List<RARCFile> AllFiles = new List<RARCFile>();
            List<RARCDirEntry> AllDirectories = new List<RARCDirEntry>();

            #region Unused Debugging
            /*List<RARCFileEntry> TEST = new List<RARCFileEntry>();
            * RARCFile.Seek(FileEntryTableOffset, SeekOrigin.Begin);
            * for (int i = 0; i < FileEntryCount; i++)
            * {
            *     TEST.Add(new RARCFileEntry()
            *     {
            *         FileID = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0),
            *         NameHash = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0),
            *         Type = BitConverter.ToInt16(RARCFile.ReadReverse(0, 2), 0)
            *     });
            *     ushort CurrentNameOffset = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
            *     TEST[TEST.Count - 1].ModularA = BitConverter.ToInt32(RARCFile.ReadReverse(0, 4), 0);
            *     TEST[TEST.Count - 1].ModularB = BitConverter.ToInt32(RARCFile.ReadReverse(0, 4), 0);
            *     RARCFile.Position += 0x04;
            *     long Pauseposition = RARCFile.Position;
            *     RARCFile.Seek(StringTableOffset + CurrentNameOffset, SeekOrigin.Begin);
            *     TEST[TEST.Count - 1].Name = RARCFile.ReadString();
            *     RARCFile.Position = Pauseposition;
            * }
            * string XML = "";
            * for (int i = 0; i < TEST.Count; i++)
            * {
            *     XML += $"<RarcFileEntry ID=\"{TEST[i].FileID}\"\t Name=\"{TEST[i].Name}\"\t\t Type=\"{TEST[i].Type.ToString("X").PadLeft(4, '0')}\"\t FileOrDirectory=\"{TEST[i].ModularA.ToString("X").PadLeft(8, '0')}\"\t Size=\"{TEST[i].ModularB.ToString("X").PadLeft(8, '0')}\" />\n";
            * }
            * File.WriteAllText("Original.xml", XML);
            */ 
            #endregion

            for (int i = 0; i < DirectoryCount; i++)
            {
                RARCFile.Seek(DirectoryTableOffset + (i * 0x10), SeekOrigin.Begin);
                RARCDirEntry Dir = new RARCDirEntry() { Type = RARCFile.ReadString(4) };
                uint NameOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);
                ushort NameHash = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0), FileCount = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
                uint FileFirstIndex = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);
                RARCFile.Seek(StringTableOffset + NameOffset, SeekOrigin.Begin);
                Dir.Name = RARCFile.ReadString();

                List<uint> SubDirIDs = new List<uint>();
                List<RARCFile> FolderFilesList = new List<RARCFile>();

                uint FileEntryID = FileEntryTableOffset + (FileFirstIndex * 0x14);
                for (int j = 0; j < FileCount; j++)
                {
                    RARCFile.Seek(FileEntryID + (j * 0x14), SeekOrigin.Begin);
                    ushort FileID = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0), CurrentNameHash = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
                    byte Flags = (byte)RARCFile.ReadByte();
                    RARCFile.ReadByte();
                    ushort CurrentNameOffset = BitConverter.ToUInt16(RARCFile.ReadReverse(0, 2), 0);
                    uint CurrentEntryDataOffset = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0), CurrentEntryDataSize = BitConverter.ToUInt32(RARCFile.ReadReverse(0, 4), 0);
                    RARCFile.Seek(StringTableOffset + CurrentNameOffset, SeekOrigin.Begin);
                    string CurrentName = RARCFile.ReadString();

                    if (CurrentName == ".")
                    {
                        Dir.ID = CurrentEntryDataOffset;
                        continue;
                    }
                    if (CurrentName == "..")
                    {
                        Dir.ParentID = CurrentEntryDataOffset;
                        continue;
                    }

                    bool IsDirectory = (Flags & 0x02) != 0;
                    if (IsDirectory)
                    {
                        SubDirIDs.Add(CurrentEntryDataOffset);
                    }
                    else
                    {
                        uint TotalOffset = DataOffset + CurrentEntryDataOffset;
                        RARCFile.Seek(TotalOffset, SeekOrigin.Begin);
                        RARCFile File = new RARCFile() { ID = FileID, Name = CurrentName, FileData = RARCFile.Read(0, (int)CurrentEntryDataSize) };
                        FolderFilesList.Add(File);
                        AllFiles.Add(File);
                    }
                }
                Dir.Files.AddRange(FolderFilesList);
                Dir.SubDirIDs.AddRange(SubDirIDs);
                AllDirectories.Add(Dir);

            }
            Root = BuildFolder(AllDirectories);
        }
    }

    /// <summary>
    /// File contained inside the Archive
    /// </summary>
    public class RARCFile
    {
        /// <summary>
        /// ID of the File. Set during the saving process
        /// </summary>
        public int ID { get; internal set; }
        /// <summary>
        /// Name of the File
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The extension of this file
        /// </summary>
        public string Extension { get
            {
                string[] parts = Name.Split('.');
                return "."+parts[parts.Length - 1].ToLower();
            }
        }
        /// <summary>
        /// The Actual Data for the file
        /// </summary>
        public byte[] FileData { get; set; }
        /// <summary>
        /// Create a new RARC File
        /// </summary>
        public RARCFile() { }
        /// <summary>
        /// Create a new RARC File from a File
        /// </summary>
        /// <param name="Filename"></param>
        public RARCFile(string Filename)
        {
            Name = new FileInfo(Filename).Name;
            FileData = File.ReadAllBytes(Filename);
        }
        /// <summary>
        /// Saves the File to the Disk.
        /// <para/>WARNING: The file will always try to overwrite what is already there
        /// </summary>
        /// <param name="Folderpath">Folder to save this file to</param>
        /// <param name="NewName">Override of the Actual Filename</param>
        /// <exception cref="IOException">Thrown if the Folderpath doesn't exist OR if the given NewName cannot be written to</exception>
        /// <exception cref="ArgumentNullException">Thrown if the <see cref="Name"/> of this file is Null or empty and if the Parameter <paramref name="NewName"/> was not assigned</exception>
        public void Save(string Folderpath, string NewName = null)
        {
            if ((Name == null || Name == "") && (NewName == null || NewName == ""))
                throw new ArgumentNullException($"Arguments 'Name' & 'NewName' are NULL!");
            File.WriteAllBytes(Path.Combine(Folderpath, NewName ?? Name), FileData);
        }
        /// <summary>
        /// Get a <see cref="MemoryStream"/> of this file. This <see cref="MemoryStream"/> does not contain anything other than the <see cref="FileData"/>
        /// </summary>
        /// <returns></returns>
        public MemoryStream GetMemoryStream() => new MemoryStream(FileData, 0, FileData.Length, false, true);
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"({ID}) {Name} - 0x{FileData.Length.ToString("X").PadLeft(8, '0')}";
    }

    /// <summary>
    /// Folder contained inside the Archive. Can contain more <see cref="RARCDirectory"/>s if desired, as well as <see cref="RARCFile"/>s
    /// </summary>
    public class RARCDirectory
    {
        /// <summary>
        /// Name of the Directory
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// ID of the Directory
        /// <para/>Not used when saving
        /// </summary>
        internal uint ID { get; set; }
        /// <summary>
        /// Directory Type. usually the first 4 letters of the <see cref="Name"/>. If the <see cref="Name"/> is shorter than 4, the missing spots will be ' ' (space)
        /// <para/>Set automatically upon saving the Archive
        /// </summary>
        public string Type { get; internal set; }
        /// <summary>
        /// List of all files inside this Directory
        /// </summary>
        public List<RARCFile> Files { get; set; } = new List<RARCFile>();
        /// <summary>
        /// List of Subdirectories contained in this directory
        /// </summary>
        public List<RARCDirectory> SubDirectories { get; set; } = new List<RARCDirectory>();
        /// <summary>
        /// Create an empty Directory
        /// </summary>
        public RARCDirectory() { }
        /// <summary>
        /// Create a Directory from a folder
        /// </summary>
        /// <param name="FolderPath"></param>
        internal RARCDirectory(string FolderPath)
        {
            DirectoryInfo DI = new DirectoryInfo(FolderPath);
            Name = DI.Name;
            Type = new string(new char[4] { Name.ToUpper().PadRight(4, ' ')[0], Name.ToUpper().PadRight(4, ' ')[1], Name.ToUpper().PadRight(4, ' ')[2], Name.ToUpper().PadRight(4, ' ')[3] });
            CreateFromFolder(FolderPath, 0);
        }
        private RARCDirectory(string FolderPath, int FileID)
        {
            DirectoryInfo DI = new DirectoryInfo(FolderPath);
            Name = DI.Name;
            Type = new string(new char[4] { Name.ToUpper().PadRight(4, ' ')[0], Name.ToUpper().PadRight(4, ' ')[1], Name.ToUpper().PadRight(4, ' ')[2], Name.ToUpper().PadRight(4, ' ')[3] });
            CreateFromFolder(FolderPath, FileID);
        }
        /// <summary>
        /// Export this Directory to a folder.
        /// </summary>
        /// <param name="FolderPath">Folder to Export to. Don't expect the files to appear here. Expect a Folder with this <see cref="Name"/> to appear</param>
        public void Export(string FolderPath)
        {
            Files.ForEach(File => File.Save(FolderPath));
            SubDirectories.ForEach(delegate (RARCDirectory Dir)
            {
                string newstring = Path.Combine(FolderPath, Dir.Name);
                Directory.CreateDirectory(newstring);
                Dir.Export(newstring);
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            int Count = CountAllFiles();
            return $"({Type}) {Name}{(Count > 0 ? $" {Count} File{(Count > 1 ? "s" : "")}" : "")}|{(SubDirectories.Count > 0 ? $" {SubDirectories.Count} Sub Directory{(SubDirectories.Count > 1 ? "s" : "")}" : "")}";
        }
        /// <summary>
        /// Find what index a given folder has
        /// </summary>
        /// <param name="foldername">Name of the folder to find</param>
        /// <returns></returns>
        public int GetSubDirIndex(string foldername)
        {
            if (SubDirectories.Count == 0)
                return -1;

            for (int i = 0; i < SubDirectories.Count; i++)
                if (SubDirectories[i].Name.ToLower().Equals(foldername.ToLower()))
                    return i;
            return -1;
        }

        private void CreateFromFolder(string FolderPath, int fileid)
        {
            string[] Found = Directory.GetFiles(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < Found.Length; i++)
            {
                Files.Add(new RARCFile(Found[i]) { ID = fileid });
                fileid++;
            }

            string[] SubDirs = Directory.GetDirectories(FolderPath, "*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < SubDirs.Length; i++)
            {
                SubDirectories.Add(new RARCDirectory(SubDirs[i], fileid));
            }
        }
        /// <summary>
        /// Search this SubDirectory for all files with a given name.
        /// </summary>
        /// <param name="Filename">Filename to look for</param>
        /// <returns></returns>
        public List<RARCFile> Search(string Filename)
        {
            List<RARCFile> FoundFiles = new List<RARCFile>();

            for (int i = 0; i < Files.Count; i++)
                if (System.Text.RegularExpressions.Regex.IsMatch(Files[i].Name.ToLower(), Util.StringEx.WildCardToRegular(Filename.ToLower())))
                    FoundFiles.Add(Files[i]);

            for (int i = 0; i < SubDirectories.Count; i++)
                FoundFiles.AddRange(SubDirectories[i].Search(Filename));

            return FoundFiles;
        }
        /// <summary>
        /// Search this subdirectory for all files with a given extension
        /// </summary>
        /// <param name="Extension">Extension to look for</param>
        /// <returns></returns>
        public List<RARCFile> SearchExtensions(string Extension)
        {
            List<RARCFile> FoundFiles = new List<RARCFile>();
            FileInfo fi = new FileInfo(""+Extension);
            for (int i = 0; i < Files.Count; i++)
                if (new FileInfo(Files[i].Name).Extension.ToLower() == fi.Extension.ToLower())
                    FoundFiles.Add(Files[i]);

            for (int i = 0; i < SubDirectories.Count; i++)
                FoundFiles.AddRange(SubDirectories[i].SearchExtensions(Extension));

            return FoundFiles;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="TopLevelOnly"></param>
        public void ReplaceOrAdd(RARCFile file, bool TopLevelOnly = true)
        {
            if (!ReplaceFile(file.Name, file, TopLevelOnly))
                Files.Add(file);
        }

        internal bool ReplaceFile(string FileToReplace, RARCFile NewFile, bool TopOnly = false)
        {
            bool Caught = false;
            for (int i = 0; i < Files.Count; i++)
            {
                if (Files[i].Name.ToLower().Equals(FileToReplace.ToLower()))
                {
                    NewFile.ID = i;
                    Files[i] = NewFile;
                    Caught = true;
                    break;
                }
            }
            if (!TopOnly && !Caught)
            {
                for (int i = 0; i < SubDirectories.Count; i++)
                {
                    Caught = SubDirectories[i].ReplaceFile(FileToReplace, NewFile);
                    if (Caught)
                        break;
                }
            }
            return Caught;
        }
        
        internal int CountAllFiles()
        {
            int Count = Files.Count;
            for (int i = 0; i < SubDirectories.Count; i++)
                Count += SubDirectories[i].CountAllFiles();
            return Count;
        }

        internal int CountAllDirectories()
        {
            int Count = SubDirectories.Count;
            for (int i = 0; i < SubDirectories.Count; i++)
                Count += SubDirectories[i].CountAllDirectories();
            return Count;
        }

        internal List<RARCDirectory> GetAllDirectories()
        {
            List<RARCDirectory> Found = new List<RARCDirectory>();
            for (int i = 0; i < SubDirectories.Count; i++)
            {
                Found.Add(SubDirectories[i]);
                Found.AddRange(SubDirectories[i].GetAllDirectories());
            }
            return Found;
        }
        /// <summary>
        /// Rebuilds all the TYPE values
        /// </summary>
        /// <param name="Recursive">If false or unprovided, only change the top layer</param>
        internal void BuildType(bool Recursive = false)
        {
            Type = new string(new char[4] { Name.ToUpper().PadRight(4, ' ')[0], Name.ToUpper().PadRight(4, ' ')[1], Name.ToUpper().PadRight(4, ' ')[2], Name.ToUpper().PadRight(4, ' ')[3] });
            if (Recursive)
                for (int i = 0; i < SubDirectories.Count; i++)
                    SubDirectories[i].BuildType(Recursive);
        }

        /// <summary>
        /// Straight index to a directory or file. Returns NULL if not found during a GET
        /// </summary>
        /// <param name="DirectoryORFile"></param>
        /// <returns></returns>
        public object this[string DirectoryORFile]
        {
            get
            {
                for (int i = 0; i < SubDirectories.Count; i++)
                {
                    if (SubDirectories[i].Name.Equals(DirectoryORFile))
                        return SubDirectories[i];
                }
                for (int i = 0; i < Files.Count; i++)
                {
                    if (Files[i].Name.Equals(DirectoryORFile))
                        return Files[i];
                }
                return null;
            }
            set
            {
                for (int i = 0; i < SubDirectories.Count; i++)
                {
                    if (SubDirectories[i].Name.Equals(DirectoryORFile))
                        SubDirectories[i] = (RARCDirectory)value;
                }
                for (int i = 0; i < Files.Count; i++)
                {
                    if (Files[i].Name.Equals(DirectoryORFile))
                        Files[i] = (RARCFile)value;
                }
            }
        }
    }

    /// <summary>
    /// Only used when Reading / Writing
    /// </summary>
    internal class RARCDirEntry
    {
        /// <summary>
        /// Directory Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Directory Type. usually the first 4 letters of the <see cref="Name"/>. If the <see cref="Name"/> is shorter than 4, the missing spots will be ' ' (space)
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// List of Files in this directory
        /// </summary>
        public List<RARCFile> Files { get; set; } = new List<RARCFile>();
        /// <summary>
        /// ID's for any Subdirectories
        /// </summary>
        public List<uint> SubDirIDs { get; set; } = new List<uint>();
        /// <summary>
        /// ID of the parent to this Directory
        /// <para/>0xFFFFFFFF if this is the Root Directory
        /// </summary>
        public uint ParentID { get; set; } = 0xFFFFFFFF;
        /// <summary>
        /// ID of this directory
        /// </summary>
        /// <returns></returns>
        public uint ID { get; set; } = 0x00;
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"({Type}) {Name}{(Files.Count > 0 ? $" {Files.Count} Files":"")}";
    }

    internal class RARCFileEntry
    {
        public short FileID;
        public short Type;
        public string Name;
        public int ModularA;
        public int ModularB;

        public override string ToString()
        {
            return $"({FileID}) {Name}, {Type.ToString("X").PadLeft(4, '0')}, [{ModularA.ToString("X").PadLeft(8, '0')}][{ModularB.ToString("X").PadLeft(8, '0')}]";
        }
    }
}
