using Hack.io.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hack.io.BCSV
{
    /// <summary>
    /// Binary Comma Seperated Values<para/>A table like format used for storing data
    /// </summary>
    public class BCSV
    {
        /// <summary>
        /// Filename of this BCSV file.
        /// </summary>
        public string FileName { get; set; } = null;

        /// <summary>
        /// Create a new BCSV
        /// </summary>
        public BCSV()
        {
            Fields = new Dictionary<uint, BCSVField>();
            Entries = new List<BCSVEntry>();
        }
        /// <summary>
        /// Open a BCSV File
        /// </summary>
        /// <param name="Filename">filepath</param>
        public BCSV(string Filename)
        {
            FileStream fs = new FileStream(Filename, FileMode.Open);
            Read(fs);
            fs.Close();
            FileName = Filename;
        }
        /// <summary>
        /// Read a BCSV from a stream. The stream position must be at the start of the BCSV File
        /// </summary>
        /// <param name="BCSV">The stream to read from</param>
        public BCSV(Stream BCSV) => Read(BCSV);

        /// <summary>
        /// Gets or sets the BCSVEntry at the specified index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public BCSVEntry this[int index]
        {
            get { return Entries[index]; }
            set { Entries[index] = value; }
        }
        /// <summary>
        /// Gets or sets the value associated with the specified hash.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public BCSVField this[uint hash]
        {
            get { return Fields[hash]; }
            set { Fields[hash] = value; }
        }

        /// <summary>
        /// The Dictionary containing all the fields in this BCSV
        /// </summary>
        public Dictionary<uint, BCSVField> Fields { get; set; }
        /// <summary>
        /// The number of fields in this BCSV
        /// </summary>
        public int FieldCount => Fields == null ? -1 : Fields.Count;
        /// <summary>
        /// The list of Entries in this BCSV
        /// </summary>
        public List<BCSVEntry> Entries { get; set; }
        /// <summary>
        /// The number of entries in this BCSV
        /// </summary>
        public int EntryCount => Entries == null ? -1 : Entries.Count;

        /// <summary>
        /// Save the BCSV to a file
        /// </summary>
        /// <param name="Filename"></param>
        public void Save(string Filename)
        {
            FileStream fs = new FileStream(Filename, FileMode.Create);
            Save(fs);
            fs.Close();
            FileName = Filename;
        }
        /// <summary>
        /// Save the BCSV using a Stream
        /// </summary>
        /// <param name="BCSV"></param>
        public void Save(Stream BCSV)
        {
            BCSV.WriteReverse(BitConverter.GetBytes(EntryCount), 0, 4);

            ushort offset = 0;
            List<KeyValuePair<uint, BCSVField>> FieldList = Fields.ToList();
            for (int i = 0; i < FieldList.Count; i++)
            {
                BCSVField currentfield = FieldList[i].Value;
                if (currentfield.AutoRecalc)
                {
                    currentfield.Bitmask = currentfield.DataType == DataTypes.BYTE ? 0x000000FF : (currentfield.DataType == DataTypes.INT16 ? 0x0000FFFF : 0xFFFFFFFF);
                    currentfield.ShiftAmount = 0;
                }
                currentfield.EntryOffset = offset;
                offset += (ushort)(currentfield.DataType == DataTypes.BYTE ? 1 : (currentfield.DataType == DataTypes.INT16 ? 2 : 4));
            }
            while (offset % 4 != 0)
                offset++;

            #region Fill the Entries
            for (int i = 0; i < EntryCount; i++)
                Entries[i].FillMissingFields(Fields);
            #endregion

            #region Collect the strings
            List<string> Strings = new List<string>();// { "5324" };
            for (int i = 0; i < EntryCount; i++)
            {
                for (int j = 0; j < Entries[i].Data.Count; j++)
                {
                    if (Fields.ContainsKey(Entries[i].Data.ElementAt(j).Key) && Fields[Entries[i].Data.ElementAt(j).Key].DataType == DataTypes.STRING)
                    {
                        if (!Strings.Any(O => O.Equals((string)Entries[i].Data.ElementAt(j).Value)))
                            Strings.Add((string)Entries[i].Data.ElementAt(j).Value);
                    }
                }
            }
            #endregion

            BCSV.WriteReverse(BitConverter.GetBytes(Fields.Count), 0, 4);
            BCSV.Write(new byte[4], 0, 4);
            BCSV.WriteReverse(BitConverter.GetBytes((int)offset), 0, 4);

            Console.WriteLine("Writing the Fields:");
            for (int i = 0; i < Fields.Count; i++)
            {
                Fields.ElementAt(i).Value.Write(BCSV);
                Console.Write($"\r{Math.Min(((float)(i + 1) / (float)Fields.Count) * 100.0f, 100.0f)}%          ");
            }
            Console.WriteLine("Complete!");

            while (BCSV.Position % 4 != 0)
                BCSV.WriteByte(0x00);

            uint DataPos = (uint)BCSV.Position;
            BCSV.Position = 0x08;
            BCSV.WriteReverse(BitConverter.GetBytes(DataPos), 0, 4);
            BCSV.Position = DataPos;
            Console.WriteLine("Writing the Entries:");
            for (int i = 0; i < EntryCount; i++)
            {
                Entries[i].Save(BCSV, Fields, offset, Strings);
                while (BCSV.Position % 4 != 0)
                    BCSV.WriteByte(0x00);
                Console.Write($"\r{Math.Min(((float)(i + 1) / (float)Entries.Count) * 100.0f, 100.0f)}%          ");
            }
            Console.WriteLine("Complete!");
            for (int i = 0; i < Strings.Count; i++)
            {
                BCSV.WriteString(Strings[i], 0x00);
            }
            uint numPadding = 16 - (uint)(BCSV.Position % 16);
            byte[] padding = new byte[numPadding];
            for (int i = 0; i < numPadding; i++)
                padding[i] = 64;
            BCSV.Write(padding, 0, (int)numPadding);
        }
        /// <summary>
        /// Save the BCSV to a MemoryStream
        /// </summary>
        /// <returns></returns>
        public MemoryStream Save()
        {
            MemoryStream ms = new MemoryStream();
            Save(ms);
            return ms;
        }

        /// <summary>
        /// Add a BCSVEntry to the Entry List
        /// </summary>
        /// <param name="entry">Entry to add</param>
        public void Add(BCSVEntry entry) => Entries.Add(entry);
        /// <summary>
        /// Add a BCSVField to the Field Dictionary
        /// </summary>
        /// <param name="field">Field to add</param>
        public void Add(BCSVField field) => Fields.Add(field.HashName, field);
        /// <summary>
        /// Add multiple BCSVEntry objects at once to the Entry List
        /// </summary>
        /// <param name="list">List of entries to add</param>
        public void AddRange(List<BCSVEntry> list) => Entries.AddRange(list);
        /// <summary>
        /// Add multiple BCSVEntry objects at once to the Entry List
        /// </summary>
        /// <param name="array">Array of entries to add</param>
        public void AddRange(BCSVEntry[] array) => Entries.AddRange(array);
        /// <summary>
        /// Add Multiple BCSVField objects at once to the Field Dictionary
        /// </summary>
        /// <param name="list">List of fields to add</param>
        public void AddRange(List<BCSVField> list)
        {
            for (int i = 0; i < list.Count; i++)
                Fields.Add(list[i].HashName, list[i]);
        }
        /// <summary>
        /// Add Multiple BCSVField objects at once to the Field Dictionary
        /// </summary>
        /// <param name="array">Array of fields to add</param>
        public void AddRange(BCSVField[] array)
        {
            for (int i = 0; i < array.Length; i++)
                Fields.Add(array[i].HashName, array[i]);
        }
        /// <summary>
        /// Insert a BCSVEntry to the Entry List at a specified position
        /// </summary>
        /// <param name="index">The Index to add the new entry at</param>
        /// <param name="entry">Entry to add</param>
        public void Insert(int index, BCSVEntry entry) => Entries.Insert(index, entry);
        /// <summary>
        /// Add multiple BCSVEntry objects at once to the Entry List at a specified position
        /// </summary>
        /// <param name="index">The Index to add the new entries at</param>
        /// <param name="list">List of entries to add</param>
        public void InsertRange(int index, List<BCSVEntry> list) => Entries.InsertRange(index, list);
        /// <summary>
        /// Add multiple BCSVEntry objects at once to the Entry List at a specified position
        /// </summary>
        /// <param name="index">The Index to add the new entries at</param>
        /// <param name="array">Array of entries to add</param>
        public void InsertRange(int index, BCSVEntry[] array) => Entries.InsertRange(index, array);
        /// <summary>
        /// Remove the BCSVEntry at the given index
        /// </summary>
        /// <param name="index">index to remove at</param>
        /// <exception cref="IndexOutOfRangeException">The index is outside the bounds of the array</exception>
        public void Remove(int index) => Entries.RemoveAt(index);
        /// <summary>
        /// Removes a specific BCSVEntry
        /// </summary>
        /// <param name="entry">The entry to remove</param>
        public void Remove(BCSVEntry entry) => Entries.Remove(entry);
        /// <summary>
        /// Remove the BCSVField with the given hash
        /// </summary>
        /// <param name="hashkey">the hash used to remove the field</param>
        public void Remove(uint hashkey) => Fields.Remove(hashkey);
        /// <summary>
        /// Remove the BCSVField with the Given name (Case-Sensitive)
        /// </summary>
        /// <param name="Fieldname">The name of the field to remove</param>
        public void Remove(string Fieldname) => Fields.Remove(FieldNameToHash(Fieldname));
        /// <summary>
        /// Remove several BCSVEntry objects at once
        /// </summary>
        /// <param name="First">The zero-based starting index of the range of entries to remove.</param>
        /// <param name="Count">The number of entries to remove.</param>
        public void RemoveRange(int First, int Count) => Entries.RemoveRange(First, Count);
        /// <summary>
        /// Removes all BCSV Entries that match a certain condition
        /// </summary>
        /// <param name="match"></param>
        public void RemoveAll(Predicate<BCSVEntry> match) => Entries.RemoveAll(match);
        /// <summary>
        /// Empty some data. Can empty the Entries or Fields. Or Both.<para/>By default, this only clears out the Entries
        /// </summary>
        /// <param name="ClearEntries">Clear the entry list?</param>
        /// <param name="ClearFields">Clear the field dictionary?</param>
        public void Clear(bool ClearEntries = true, bool ClearFields = false)
        {
            if (ClearEntries)
                Entries.Clear();
            if (ClearFields)
                Fields.Clear();
        }
        /// <summary>
        /// Sort the BCSV Entries by a given function
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="KeySelector"></param>
        public void Sort<TSource, TKey>(Func<BCSVEntry, TKey> KeySelector) => Entries = Entries.OrderBy(KeySelector).ToList();
        /// <summary>
        /// Determines if this BCSV contains a field with the given Hash
        /// </summary>
        /// <param name="hash">The hash to look for</param>
        /// <returns></returns>
        public bool ContainsField(uint hash) => Fields.ContainsKey(hash);
        /// <summary>
        /// Determines if this BCSV contains a field with the given Name. (Case-Sensitive)
        /// </summary>
        /// <param name="Fieldname">The Name of the Field</param>
        /// <returns></returns>
        public bool ContainsField(string Fieldname) => Fields.ContainsKey(FieldNameToHash(Fieldname));

        /// <summary>
        /// Converts a field name to a hash.
        /// </summary>
        /// <param name="field">the string to convert</param>
        /// <returns>the hashed string</returns>
        public static uint FieldNameToHash(string field)
        {
            uint ret = 0;
            foreach (char ch in field)
            {
                ret *= 0x1F;
                ret += ch;
            }
            return ret;
        }
        
        //public int InsertAndCombine(int StartingValue, int AddingValue, out uint Mask, out byte ShiftVal)
        //{
        //    Mask = 0;
        //    ShiftVal = 0;

        //    BitArray StartingBits = new BitArray(new int[] { StartingValue });
        //    BitArray AddingBits = new BitArray(new int[] { AddingValue });

        //    int MinAddBitSize = GetMinLength(AddingValue);
        //    bool success = false;
        //    for (int i = 0; i < StartingBits.Count - MinAddBitSize; i++)
        //    {
        //        if (StartingBits[i] == AddingBits[0])
        //        {
        //            bool CanFit = true;
        //            for (int j = 0; j < MinAddBitSize; j++)
        //            {
        //                if (StartingBits[i + j] != AddingBits[j])
        //                {
        //                    CanFit = false;
        //                    break;
        //                }
        //            }

        //            if (CanFit)
        //            {
        //                success = true;
        //                BitArray BitMask = new BitArray(new int[1]);
        //                for (int j = 0; j < MinAddBitSize; j++)
        //                {
        //                    BitMask[i + j] = true;
        //                }
        //                Mask = (uint)BitMask.ToInt32();
        //                ShiftVal = (byte)i;
        //                break;
        //            }
        //        }
        //    }
        //    if (!success)
        //    {
        //        int NumberInsertLocation = GetMinLength(StartingValue);
        //        int BackwardsOffset = 0;
        //        bool HasFoundFit = false;
        //        int BestFitOffset = 0;
        //        while (NumberInsertLocation - BackwardsOffset > 0)
        //        {
        //            if (StartingBits[NumberInsertLocation - BackwardsOffset] == AddingBits[0])
        //            {
        //                bool CanFit = true;
        //                for (int i = 0; i < MinAddBitSize; i++)
        //                {
        //                    if (AddingBits[i] && StartingBits[(NumberInsertLocation - BackwardsOffset) + i] != AddingBits[i])
        //                    {
        //                        if ((NumberInsertLocation - BackwardsOffset) + i < NumberInsertLocation)
        //                        {
        //                            CanFit = false;
        //                            break;
        //                        }
        //                    }
        //                }

        //                if (CanFit)
        //                {
        //                    HasFoundFit = true;
        //                    BestFitOffset = NumberInsertLocation - BackwardsOffset;
        //                    break;
        //                }
        //            }
        //            BackwardsOffset++;
        //        }

        //        for (int j = 0; j < MinAddBitSize; j++)
        //        {
        //            StartingBits[HasFoundFit ? BestFitOffset + j : NumberInsertLocation] = AddingBits[j];
        //        }

        //        BitArray BitMask = new BitArray(new int[1]);
        //        for (int j = 0; j < MinAddBitSize; j++)
        //        {
        //            BitMask[HasFoundFit ? BestFitOffset + j : NumberInsertLocation] = true;
        //        }
        //        Mask = (uint)BitMask.ToInt32();
        //        ShiftVal = (byte)(HasFoundFit ? BestFitOffset : NumberInsertLocation);
        //    }


        //    return StartingBits.ToInt32();
        //}

        //private int GetMinLength(int val)
        //{
        //    for (int i = 28; i >= 0; i -= 4)
        //        if ((val >> i) > 0)
        //            return i + 4;
        //    return 0;
        //}


        internal void Read(Stream BCSV)
        {
            Fields = new Dictionary<uint, BCSVField>();
            Entries = new List<BCSVEntry>();

            int entrycount = BitConverter.ToInt32(BCSV.ReadReverse(0, 4), 0);
            //Console.Write($"{entrycount} Entries ");
            int fieldcount = BitConverter.ToInt32(BCSV.ReadReverse(0, 4), 0);
            //Console.WriteLine($"done with {fieldcount} fields");
            uint dataoffset = BitConverter.ToUInt32(BCSV.ReadReverse(0, 4), 0);
            uint entrysize = BitConverter.ToUInt32(BCSV.ReadReverse(0, 4), 0);

            //Console.WriteLine("Loading Fields:");
            for (int i = 0; i < fieldcount; i++)
            {
                BCSVField currentfield = new BCSVField(BCSV);
                Fields.Add(currentfield.HashName, currentfield);
                //Console.Write($"\r{Math.Min(((float)(i + 1) / (float)fieldcount) * 100.0f, 100.0f)}%          ");
            }
            //Console.WriteLine("Complete!");

            //Console.WriteLine("Loading Entries:");
            for (int i = 0; i < entrycount; i++)
            {
                BCSVEntry currententry = new BCSVEntry(BCSV, Fields, dataoffset + (entrycount * entrysize));
                Entries.Add(currententry);
                BCSV.Position += entrysize;

                //Console.Write($"\r{Math.Min(((float)(i + 1) / (float)entrycount) * 100.0f, 100.0f)}%          ");
            }
            //Console.WriteLine("Complete!");
        }


        //=====================================================================

        /// <summary>
        /// Cast a BCSV to a RARCFile
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator RARC.RARC.File(BCSV x) => new RARC.RARC.File(x.FileName, x.Save());

        /// <summary>
        /// Cast a RARCFile to a BCSV
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator BCSV(RARC.RARC.File x) => new BCSV((MemoryStream)x) { FileName = x.Name };

        //=====================================================================
    }

    /// <summary>
    /// A BCSV Field. Use one to define how Data in BCSVEntry objects is managed while saving
    /// </summary>
    public class BCSVField
    {
        /// <summary>
        /// The numerical hash representation of the field name.
        /// </summary>
        public uint HashName { get; set; }
        /// <summary>
        /// The number that determines how the value is read from the file
        /// </summary>
        public uint Bitmask { get; set; }
        /// <summary>
        /// The offset within the binary entry that this field is located at. Automatically calculated while saving.
        /// </summary>
        public ushort EntryOffset { get; internal set; }
        /// <summary>
        /// The number of bits to shift while reading the value from the file
        /// </summary>
        public byte ShiftAmount { get; set; }
        /// <summary>
        /// The type of data being held inside this field
        /// </summary>
        public DataTypes DataType { get; set; }
        /// <summary>
        /// Setting this to true will auto-recalculate the Bitmask and Shift Amount on Save
        /// </summary>
        public bool AutoRecalc { get; set; }
        /// <summary>
        /// Create a new, empty BCSV Field
        /// </summary>
        public BCSVField()
        {

        }

        internal BCSVField(Stream BCSV)
        {
            HashName = BitConverter.ToUInt32(BCSV.ReadReverse(0, 4), 0);
            Bitmask = BitConverter.ToUInt32(BCSV.ReadReverse(0, 4), 0);
            EntryOffset = BitConverter.ToUInt16(BCSV.ReadReverse(0, 2), 0);
            ShiftAmount = (byte)BCSV.ReadByte();
            DataType = (DataTypes)BCSV.ReadByte();
        }

        internal void Write(Stream BCSV)
        {
            BCSV.WriteReverse(BitConverter.GetBytes(HashName), 0, 4);
            BCSV.WriteReverse(BitConverter.GetBytes(Bitmask), 0, 4);
            BCSV.WriteReverse(BitConverter.GetBytes(EntryOffset), 0, 2);
            BCSV.WriteByte(ShiftAmount);
            BCSV.WriteByte((byte)DataType);
        }
        /// <summary>
        /// Gets the default value for this BCSVField's DataType
        /// </summary>
        /// <returns>The default value for this field's DataType</returns>
        public object GetDefaultValue() => GetDefaultValue(DataType);
        /// <summary>
        /// Gets the default value of the given DataType
        /// </summary>
        /// <param name="type">the DataType to get the default for</param>
        /// <returns>The default value of the DataType</returns>
        public static object GetDefaultValue(DataTypes type)
        {
            switch (type)
            {
                case DataTypes.INT32:
                    return default(int);
                case DataTypes.FLOAT:
                    return default(float);
                case DataTypes.UINT32:
                    return default(uint);
                case DataTypes.INT16:
                    return default(short);
                case DataTypes.BYTE:
                    return default(byte);
                case DataTypes.STRING:
                    return "";
                default:
                    return null;
            }
        }
    }
    /// <summary>
    /// A BCSV Entry. Use one of these to store data as defined by the BCSV Fields
    /// </summary>
    public class BCSVEntry
    {
        /// <summary>
        /// The Data held in this BCSVEntry. The Key is the Hash that the Data Value belongs to
        /// </summary>
        public Dictionary<uint, object> Data { get; set; }
        /// <summary>
        /// Create an empty BCSV Entry
        /// </summary>
        public BCSVEntry() => Data = new Dictionary<uint, object>();
        /// <summary>
        /// Create a new entry with the provided Dictionary of fields
        /// </summary>
        /// <param name="FieldSource">The dictionary of fields</param>
        public BCSVEntry(Dictionary<uint, BCSVField> FieldSource)
        {
            Data = new Dictionary<uint, object>();
            foreach (KeyValuePair<uint, BCSVField> field in FieldSource)
                Data.Add(field.Key, field.Value.GetDefaultValue());
        }
        internal BCSVEntry(Stream BCSV, Dictionary<uint, BCSVField> fields, long StringOffset)
        {
            long EntryStartPosition = BCSV.Position;
            Data = new Dictionary<uint, object>();
            for (int i = 0; i < fields.Count; i++)
            {
                BCSV.Position = EntryStartPosition + fields.ElementAt(i).Value.EntryOffset;
                switch (fields.ElementAt(i).Value.DataType)
                {
                    case DataTypes.INT32:
                        int readvalue = BitConverter.ToInt32(BCSV.ReadReverse(0, 4), 0);
                        uint Bitmask = fields.ElementAt(i).Value.Bitmask;
                        byte Shift = fields.ElementAt(i).Value.ShiftAmount;
                        Data.Add(fields.ElementAt(i).Key, (int)((readvalue & Bitmask) >> Shift));
                        break;
                    case DataTypes.UNKNOWN:
                        Data.Add(fields.ElementAt(i).Key, null);
                        Console.WriteLine("=== WARNING ===");
                        Console.WriteLine("BCSV Entry is of the UNKNOWN type (0x01). This shouldn't happen.");
                        break;
                    case DataTypes.FLOAT:
                        Data.Add(fields.ElementAt(i).Key, BitConverter.ToSingle(BCSV.ReadReverse(0, 4), 0));
                        break;
                    case DataTypes.UINT32:
                        Data.Add(fields.ElementAt(i).Key, BitConverter.ToUInt32(BCSV.ReadReverse(0, 4), 0));
                        break;
                    case DataTypes.INT16:
                        Data.Add(fields.ElementAt(i).Key, BitConverter.ToInt16(BCSV.ReadReverse(0, 2), 0));
                        break;
                    case DataTypes.BYTE:
                        Data.Add(fields.ElementAt(i).Key, (byte)BCSV.ReadByte());
                        break;
                    case DataTypes.STRING:
                        BCSV.Position = StringOffset + BitConverter.ToUInt32(BCSV.ReadReverse(0, 4), 0);
                        Data.Add(fields.ElementAt(i).Key, BCSV.ReadString());
                        break;
                    case DataTypes.NULL:
                        Data.Add(fields.ElementAt(i).Key, null);
                        Console.WriteLine("=== WARNING ===");
                        Console.WriteLine("BCSV Entry is of the NULL type (0x07). This shouldn't happen.");
                        break;
                }
            }
            BCSV.Position = EntryStartPosition;
        }

        internal void Save(Stream BCSV, Dictionary<uint, BCSVField> fields, uint DataLength, List<string> Strings)
        {
            long OriginalPosition = BCSV.Position;
            BCSV.Write(new byte[DataLength], 0, (int)DataLength);
            foreach (KeyValuePair<uint, BCSVField> Field in fields)
            {
                BCSV.Position = OriginalPosition + Field.Value.EntryOffset;
                if (Data.ContainsKey(Field.Key))
                {
                    switch (Field.Value.DataType)
                    {
                        case DataTypes.INT32:
                            if (Field.Value.Bitmask != 0xFFFFFFFF)
                                BCSV.WriteReverse(BitConverter.GetBytes((int.Parse(Data[Field.Key].ToString()) << Field.Value.ShiftAmount) & (int)Field.Value.Bitmask), 0, 4);
                            else
                                BCSV.WriteReverse(BitConverter.GetBytes(int.Parse(Data[Field.Key].ToString())), 0, 4);
                            break;
                        case DataTypes.UNKNOWN:
                            break;
                        case DataTypes.FLOAT:
                            BCSV.WriteReverse(BitConverter.GetBytes((float)Data[Field.Key]), 0, 4);
                            break;
                        case DataTypes.UINT32:
                            BCSV.WriteReverse(BitConverter.GetBytes((uint)Data[Field.Key]), 0, 4);
                            break;
                        case DataTypes.INT16:
                            BCSV.WriteReverse(BitConverter.GetBytes((short)Data[Field.Key]), 0, 2);
                            break;
                        case DataTypes.BYTE:
                            BCSV.WriteByte((byte)Data[Field.Key]);
                            break;
                        case DataTypes.STRING:
                            Encoding enc = Encoding.GetEncoding(932);
                            uint StringOffset = 0;
                            for (int j = 0; j < Strings.Count; j++)
                            {
                                if (Strings[j].Equals((string)Data[Field.Key]))
                                {
                                    BCSV.WriteReverse(BitConverter.GetBytes(StringOffset), 0, 4);
                                    break;
                                }
                                StringOffset += (uint)(enc.GetBytes(Strings[j]).Length + 1);
                            }
                            break;
                        case DataTypes.NULL:
                            break;
                    }
                }
            }
            BCSV.Position = OriginalPosition + DataLength;
        }

        /// <summary>
        /// Shortcut to Data[hash]
        /// </summary>
        /// <param name="hash">The hash to get</param>
        /// <returns></returns>
        public object this[uint hash]
        {
            get { return Data[hash]; }
            set
            {
                if (!(value is int) && !(value is float) && !(value is uint) && !(value is short) && !(value is byte) && !(value is string))
                    throw new Exception($"The provided object is not supported by BCSV. Value is of type \"{value.GetType().ToString()}\"");
                Data[hash] = value;
            }
        }

        internal void FillMissingFields(Dictionary<uint, BCSVField> fields)
        {
            for (int i = 0; i < fields.Count; i++)
                if (!Data.ContainsKey(fields.ElementAt(i).Key))
                    Data.Add(fields.ElementAt(i).Key, fields.ElementAt(i).Value.GetDefaultValue());
        }
        /// <summary>
        /// Checks if this BCSV Contains data for a given hash
        /// </summary>
        /// <param name="hash">The hash to look for</param>
        /// <returns>true if data for the hash has been found, false if it has not been found</returns>
        public bool ContainsKey(uint hash) => Data.ContainsKey(hash);
        /// <summary>
        /// Checks if this BCSV Contains data for a given <see cref="BCSVField.HashName"/>
        /// </summary>
        /// <param name="Field">The <see cref="BCSVField.HashName"/> to check</param>
        /// <returns>true if data for the <see cref="BCSVField.HashName"/> has been found, false if it has not been found</returns>
        public bool ContainsKey(BCSVField Field) => Data.ContainsKey(Field.HashName);
        /// <summary>
        /// Gets the value associated with the specified hash, if it exists
        /// </summary>
        /// <param name="hash">The hash of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns></returns>
        public bool TryGetValue(uint hash, out object value) => Data.TryGetValue(hash, out value);
        /// <summary>
        /// Attempt to load data from the Clipboard
        /// </summary>
        /// <param name="input">The Clipboard input string</param>
        /// <returns>true if successful</returns>
        public bool FromClipboard(string input)
        {
            if (!input.StartsWith("BCSVEntry|"))
                return false;

            int DataIndex = 0;
            Dictionary<uint, object> backup = Data;
            try
            {
                string[] DataSplit = input.Split('|');

                Data.Clear();
                for (int i = 1; i < DataSplit.Length; i++)
                {
                    string[] currentdata = DataSplit[i].Split('%');
                    DataIndex = i;
                    if (uint.TryParse(currentdata[0], System.Globalization.NumberStyles.HexNumber, null, out uint result))
                        Data.Add(result, Convert.ChangeType(currentdata[1], Type.GetType("System." + currentdata[2], true)));
                }
            }
            catch (Exception)
            {
                //Console.WriteLine($"Error Pasting at Data Set {DataIndex}: " + e.Message);
                Data = backup;
                return false;
            }
            return true;
        }
        /// <summary>
        /// Copies this BCSVEntry into the clipboard as a string
        /// </summary>
        /// <returns></returns>
        public string ToClipboard()
        {
            string clip = "BCSVEntry";
            for (int i = 0; i < Data.Count; i++)
                clip += "|" + Data.ElementAt(i).Key.ToString("X8") + "%" + Data.ElementAt(i).Value.ToString() + "%" + Data.ElementAt(i).Value.GetType().ToString().Replace("System.", "");
            return clip;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Left"></param>
        /// <param name="Right"></param>
        /// <returns></returns>
        public static bool operator ==(BCSVEntry Left, BCSVEntry Right) => Left.Equals(Right);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Left"></param>
        /// <param name="Right"></param>
        /// <returns></returns>
        public static bool operator !=(BCSVEntry Left, BCSVEntry Right) => !Left.Equals(Right);
        /// <summary>
        /// Auto-Generated
        /// </summary>
        /// <param name="obj">Object to compare to</param>
        /// <returns></returns>
        public override bool Equals(object obj) => obj is BCSVEntry entry;

        /// <summary>
        /// Auto-Generated
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() => -301143667 + EqualityComparer<Dictionary<uint, object>>.Default.GetHashCode(Data);
    }
    /// <summary>
    /// BCSVField Data Types
    /// </summary>
    public enum DataTypes : byte
    {
        /// <summary>
        /// 32 bit integer (4 Bytes)
        /// </summary>
        INT32 = 0,
        /// <summary>
        /// Unknown. Don't use.
        /// </summary>
        UNKNOWN = 1,
        /// <summary>
        /// 32 bit decimal value (4 Bytes)
        /// </summary>
        FLOAT = 2,
        /// <summary>
        /// Unsigned 32 bit integer (4 Bytes)
        /// </summary>
        UINT32 = 3,
        /// <summary>
        /// 16 bit integer (2 Bytes)
        /// </summary>
        INT16 = 4,
        /// <summary>
        /// 8 bit integer (1 Byte)
        /// </summary>
        BYTE = 5,
        /// <summary>
        /// A set of characters (4 Bytes, characters added to the String table)
        /// </summary>
        STRING = 6,
        /// <summary>
        /// NULL. Don't use, as it cannot be written to a file.
        /// </summary>
        NULL = 7
    }
}