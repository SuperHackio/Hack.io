using System.Data;
using System.Text;
using Hack.io.Interface;
using Hack.io.Utility;

namespace Hack.io.BCSV;

/// <summary>
/// Binary Comma Separated Values<para/>
/// Represents a table like structure used in J3D Games
/// </summary>
public class BCSV : ILoadSaveFile
{
    #region Properties
    /// <summary>
    /// The Dictionary containing all the fields in this BCSV
    /// </summary>
    protected Dictionary<uint, Field> Fields { get; set; } = [];
    /// <summary>
    /// The list of Entries in this BCSV
    /// </summary>
    protected List<Entry> Entries { get; set; } = [];
    /// <summary>
    /// The encoding to use while handling text. Should only realistically be Shift-JIS or UTF-8
    /// </summary>
    public Encoding Encoding
    {
        get => encoding;
        set
        {
            encoding = value;
            for (int i = 0; i < EntryCount; i++)
                Entries[i].Encoding = encoding;
        }
    }

    /// <summary>
    /// The field data calculator to use on saving
    /// </summary>
    public FieldDataCalculator OnSaveFieldCalculator
    {
        get => fieldDataCalculator;
        set
        {
            if (value is null)
                throw new NullReferenceException($"You MUST set a Field Calculator. Use {nameof(CalculateFieldDataDefault)} instead of null");
            fieldDataCalculator = value;
        }
    }

    //READONLY
    /// <summary>
    /// The number of fields in this BCSV
    /// </summary>
    public int FieldCount => Fields == null ? -1 : Fields.Count;
    /// <summary>
    /// The number of entries in this BCSV
    /// </summary>
    public int EntryCount => Entries == null ? -1 : Entries.Count;

    //INDEXORS
    /// <summary>
    /// Gets or sets the BCSV.Entry at the specified index.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public Entry this[int index]
    {
        get => Entries[index];
        set
        {
            UpdateEntryFields(ref value);
            Entries[index] = value;
        }
    }
    /// <summary>
    /// Gets or sets the value associated with the specified hash.
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public Field this[uint hash]
    {
        get { return Fields[hash]; }
        set
        {
            Fields[hash] = value;
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry e = Entries[i];
                UpdateEntryFields(ref e);
            }
        }
    }
    #endregion

    #region Fields
    private Encoding encoding = StreamUtil.ShiftJIS;
    private FieldDataCalculator fieldDataCalculator = CalculateFieldDataDefault;
    #endregion

    /// <summary>
    /// Creates an empty BCSV
    /// </summary>
    public BCSV() { }

    #region Functions
    /// <summary>
    /// Adds a new Entry to the BCSV.<para/>IMPORTANT: The input entry will have it's fields edited to match the field definition of this BCSV.
    /// </summary>
    /// <param name="NewElement">The new entry to add</param>
    public void Add(Entry NewElement)
    {
        UpdateEntryFields(ref NewElement);
        Entries.Add(NewElement);
    }
    /// <summary>
    /// Adds a new Field to the BCSV.<para/>IMPORTANT: This will update all BCSV entries to have the missing field data. Default values will be used.
    /// </summary>
    /// <param name="NewElement">The new field to add</param>
    /// <exception cref="DuplicateNameException">Thrown if the field already exists in the BCSV</exception>
    public void Add(Field NewElement)
    {
        if (Fields.ContainsKey(NewElement.HashName))
            throw new DuplicateNameException(string.Format(JMapException.FIELD_ALREADY_EXISTS_ERROR, NewElement.HashName));
        Fields.Add(NewElement.HashName, NewElement);
        for (int i = 0; i < EntryCount; i++)
            Entries[i].Data.Add(NewElement.HashName, GetDefaultValue(NewElement.DataType));
    }

    /// <inheritdoc cref="AddRange(IReadOnlyList{Entry})"/>
    public void Add(params Entry[] NewElements) => AddRange(NewElements);
    /// <inheritdoc cref="AddRange(IReadOnlyList{Field})"/>
    public void Add(params Field[] NewElements) => AddRange(NewElements);

    /// <summary>
    /// Add multiple entries at once to the BCSV
    /// </summary>
    /// <param name="NewElements">Collection of entries to add</param>
    public void AddRange(IReadOnlyList<Entry> NewElements)
    {
        for (int i = 0; i < NewElements.Count; i++)
            Add(NewElements[i]);
    }
    /// <summary>
    /// Add multiple fields at once to the BCSV
    /// </summary>
    /// <param name="NewElements">Collection of fields to add</param>
    public void AddRange(IReadOnlyList<Field> NewElements)
    {
        for (int i = 0; i < NewElements.Count; i++)
            Add(NewElements[i]);
    }

    /// <summary>
    /// Insert an Entry at the specified index
    /// </summary>
    /// <param name="Index">The index to insert at</param>
    /// <param name="NewElement">The Entry to insert</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void Insert(int Index, Entry NewElement)
    {
        UpdateEntryFields(ref NewElement);
        Entries.Insert(Index, NewElement);
    }
    
    /// <summary>
    /// Insert multiple Entry objects at once to the BCSV at the specified index
    /// </summary>
    /// <param name="Index">The index to insert at</param>
    /// <param name="NewElements">The entries to insert</param>
    public void InsertRange(int Index, IReadOnlyList<Entry> NewElements)
    {
        for (int i = 0; i < NewElements.Count; i++)
        {
            Entry e = NewElements[i];
            UpdateEntryFields(ref e);
        }
        Entries.InsertRange(Index, NewElements);
    }
    
    /// <summary>
    /// Removes a specific Entry
    /// </summary>
    /// <param name="Target">The entry to remove</param>
    public void Remove(Entry Target) => Entries.Remove(Target);
    /// <summary>
    /// Removes all entries that match the <see cref="Predicate{Entry}"/>
    /// </summary>
    /// <param name="match">The conditions to remove</param>
    /// <returns>the number of removed entries</returns>
    public int RemoveAll(Predicate<Entry> match) => Entries.RemoveAll(match);
    /// <summary>
    /// Remove the Entry at the given index
    /// </summary>
    /// <param name="Index">index to remove at</param>
    /// <exception cref="IndexOutOfRangeException">The index is outside the bounds of the array</exception>
    public void RemoveAt(int Index) => Entries.RemoveAt(Index);
    /// <summary>
    /// Remove several Entry objects at once
    /// </summary>
    /// <param name="Index">The zero-based starting index of the range of entries to remove.</param>
    /// <param name="Count">The number of entries to remove.</param>
    public void RemoveRange(int Index, int Count) => Entries.RemoveRange(Index, Count);

    /// <inheritdoc cref="List{Entry}.Reverse()"/>
    public void Reverse() => Entries.Reverse();
    /// <inheritdoc cref="List{Entry}.Reverse(int,int)"/>
    public void Reverse(int index, int count) => Entries.Reverse(index, count);

    /// <summary>
    /// Empty some data. Can empty the Entries or Fields. Or Both.<para/>By default, this only clears out the Entries
    /// </summary>
    /// <param name="ClearEntries">Clear the entry list?</param>
    /// <param name="ClearFields">Clear the field dictionary?<para/>Note that clearing the fields will also clear the data inside the entries, but not change the number of entries</param>
    public void Clear(bool ClearEntries = true, bool ClearFields = false)
    {
        if (ClearEntries)
            Entries.Clear();
        if (ClearFields)
        {
            Fields.Clear();
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry e = Entries[i];
                UpdateEntryFields(ref e);
            }
        }
    }

    /// <summary>
    /// Sort the entries by a given function
    /// </summary>
    /// <param name="Comparison"></param>
    public void Sort(Comparison<Entry> Comparison) => Entries.Sort(Comparison);

    /// <summary>
    /// Determines if this BCSV contains a field with the given Hash
    /// </summary>
    /// <param name="hash">The hash to look for</param>
    /// <returns></returns>
    public bool ContainsField(uint hash) => Fields.ContainsKey(hash);

    /// <summary>
    /// Sets all fields to be recalculated. See <see cref="Field.AutoRecalc"/>.
    /// </summary>
    public void SetAllRecalculate(bool toggle = true)
    {
        foreach (Field item in Fields.Values)
            item.AutoRecalc = toggle;
    }
    
    
    private void UpdateEntryFields(ref Entry Element)
    {
        Element.Encoding = Encoding;
        Element.FillMissingFields(Fields);
        Element.RemoveMissingFields(Fields);
    }
    internal ushort CalcEntrySize()
    {
        ushort Size = 0;
        foreach (Field item in Fields.Values)
            Size = (ushort)Math.Max(Size, item.EntryOffset + GetDataTypeSize(item.DataType));
        return Size;
    }
    #endregion

    /// <inheritdoc/>
    public virtual void Load(Stream Strm)
    {
        Fields.Clear();
        Entries.Clear();

        int entrycount = Strm.ReadInt32();
        int fieldcount = Strm.ReadInt32();
        uint dataoffset = Strm.ReadUInt32();
        uint entrysize = Strm.ReadUInt32();

        for (int i = 0; i < fieldcount; i++)
        {
            Field f = new()
            {
                HashName = Strm.ReadUInt32(),
                Bitmask = Strm.ReadUInt32(),
                EntryOffset = Strm.ReadUInt16(),
                ShiftAmount = (byte)Strm.ReadByte(),
                DataType = (DataTypes)Strm.ReadByte()
            };
            Fields.Add(f.HashName, f);
        }
        Span<byte> charreadarray = stackalloc byte[32];
        long stringtable = dataoffset + (entrycount * entrysize);
        for (int i = 0; i < entrycount; i++)
        {
            Entry e = CreateEntry();
            e.Encoding = Encoding;
            foreach (Field field in Fields.Values)
            {
                Strm.Position = dataoffset + (entrysize * i) + field.EntryOffset;
                switch (field.DataType)
                {
                    case DataTypes.INT32:
                        e.Data.Add(field.HashName, (int)((Strm.ReadInt32() & field.Bitmask) >> field.ShiftAmount));
                        break;
                    case DataTypes.CHARARRAY:
                        Strm.Read(charreadarray);
                        string s = Encoding.GetString(charreadarray);
                        e.Data.Add(field.HashName, s);
                        break;
                    case DataTypes.FLOAT:
                        float f = Strm.ReadSingle();
                        e.Data.Add(field.HashName, f);
                        break;
                    case DataTypes.UINT32:
                        e.Data.Add(field.HashName, (Strm.ReadUInt32() & field.Bitmask) >> field.ShiftAmount);
                        break;
                    case DataTypes.INT16:
                        e.Data.Add(field.HashName, (short)((Strm.ReadInt16() & field.Bitmask) >> field.ShiftAmount));
                        break;
                    case DataTypes.BYTE:
                        e.Data.Add(field.HashName, (byte)(((byte)Strm.ReadByte() & field.Bitmask) >> field.ShiftAmount));
                        break;
                    case DataTypes.STRING:
                        uint Offset = Strm.ReadUInt32();
                        Strm.Position = stringtable + Offset;
                        e.Data.Add(field.HashName, Strm.ReadString(Encoding));
                        break;
                    case DataTypes.NULL:
                    default:
                        throw new NullReferenceException();
                }
            }
            Entries.Add(e);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InternalBufferOverflowException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public virtual void Save(Stream Strm)
    {
        BCSV c = this;
        OnSaveFieldCalculator(ref c);
        ushort entrysize = CalcEntrySize();
        while (entrysize % 4 != 0)
            entrysize++;

        #region Collect the strings
        List<string> Strings = [];// { "5324" };
        foreach (Field field in Fields.Values)
        {
            if (field.DataType != DataTypes.STRING) //CHARARRAY doesn't count towards this
                continue;
            for (int e = 0; e < EntryCount; e++)
            {
                string str = (string)this[e][field];
                if (!Strings.Contains(str))
                    Strings.Add(str);
            }
        }
        #endregion

        Strm.WriteInt32(EntryCount);
        Strm.WriteInt32(FieldCount);
        Strm.WritePlaceholder(4);
        Strm.WriteInt32(entrysize);

        foreach (Field field in Fields.Values)
        {
            Strm.WriteUInt32(field.HashName);
            Strm.WriteUInt32(field.Bitmask);
            Strm.WriteUInt16(field.EntryOffset);
            Strm.WriteByte(field.ShiftAmount);
            Strm.WriteByte((byte)field.DataType);
        }
        //Is this even needed?
        Strm.PadTo(0x04);

        uint DataPos = (uint)Strm.Position;
        for (int entryID = 0; entryID < EntryCount; entryID++)
        {
            Entry entry = Entries[entryID];
            long basepos = Strm.Position;
            Strm.Write(new byte[entrysize], 0, entrysize); //Needed for masking and stuff
            foreach (Field field in Fields.Values)
            {
                Strm.Position = basepos + field.EntryOffset;
                object obj = entry[field];
                switch (field.DataType)
                {
                    case DataTypes.INT32:
                        {
                            if (obj is not int i)
                                throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, obj.GetType().Name, typeof(int).Name));
                            int cur = (int)(Strm.Peek(StreamUtil.ReadInt32) & ~field.Bitmask);
                            int tar = (int)((i << field.ShiftAmount) & field.Bitmask);
                            Strm.WriteInt32(tar | cur);
                        }
                        break;
                    case DataTypes.CHARARRAY:
                        byte[] ba;
                        if (obj is char[] ca)
                            ba = Encoding.GetBytes(ca);
                        else if (obj is string str)
                            ba = Encoding.GetBytes(str);
                        else
                            throw new InvalidCastException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, obj.GetType().Name, $"{typeof(char[]).Name}/{typeof(string).Name}"));
                        if (ba.Length > 32)
                            throw new InternalBufferOverflowException(string.Format(JMapException.CHARARRAY_TOO_BIG_ERROR, ba.Length, JMapException.MAX_CHARARRAY_SIZE));
                        Strm.Write(new ReadOnlySpan<byte>(ba));
                        break;
                    case DataTypes.FLOAT:
                        if (obj is not float f)
                            throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, obj.GetType().Name, typeof(float).Name));
                        Strm.WriteSingle(f);
                        break;
                    case DataTypes.UINT32:
                        {
                            if (obj is not uint ui)
                                throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, obj.GetType().Name, typeof(uint).Name));
                            uint cur = (uint)(Strm.Peek(StreamUtil.ReadInt32) & ~field.Bitmask);
                            uint tar = (ui << field.ShiftAmount) & field.Bitmask;
                            Strm.WriteUInt32(tar | cur);
                        }
                        break;
                    case DataTypes.INT16:
                        {
                            if (obj is not short s)
                                throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, obj.GetType().Name, typeof(short).Name));
                            short cur = (short)(Strm.Peek(StreamUtil.ReadInt16) & ~field.Bitmask);
                            short tar = (short)((s << field.ShiftAmount) & field.Bitmask);
                            Strm.WriteInt16((short)(tar | cur));
                        }
                        break;
                    case DataTypes.BYTE:
                        {
                            if (obj is not byte b)
                                throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, obj.GetType().Name, typeof(byte).Name));
                            byte cur = (byte)(Strm.PeekByte() & ~field.Bitmask);
                            byte tar = (byte)((b << field.ShiftAmount) & field.Bitmask);
                            Strm.WriteByte((byte)(tar | cur));
                        }
                        break;
                    case DataTypes.STRING:
                        if (obj is not string str2)
                            throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, obj.GetType().Name, typeof(string).Name));

                        uint StringOffset = 0;
                        for (int j = 0; j < Strings.Count; j++)
                        {
                            string curstr = Strings[j];
                            if (str2.Equals(curstr))
                            {
                                Strm.WriteUInt32(StringOffset);
                                break;
                            }
                            StringOffset += (uint)(Encoding.GetByteCount(curstr) + 1);
                        }
                        break;
                    case DataTypes.NULL:
                    default:
                        throw new NullReferenceException(string.Format(JMapException.INVALID_DATATYPE_ERROR, field.DataType));
                }
            }
            Strm.PadTo(0x04);
            Strm.Position = basepos + entrysize;
        }

        for (int i = 0; i < Strings.Count; i++)
        {
            Strm.WriteString(Strings[i], Encoding);
        }
        Strm.PadTo(16, 0x40);
        long EndPosition = Strm.Position;

        Strm.Position = 0x08;
        Strm.WriteUInt32(DataPos);

        Strm.Position = EndPosition;
    }

    /// <summary>
    /// Override this if you need to have it load a different class instead of BCSV.Entry
    /// </summary>
    /// <returns></returns>
    protected virtual Entry CreateEntry() => new();

    /// <summary>
    /// A BCSV Field. Use one to define how Data in BCSVEntry objects is managed while saving
    /// </summary>
    public sealed class Field
    {
        /// <summary>
        /// The numerical hash representation of the field name.
        /// </summary>
        public uint HashName { get; set; }
        /// <summary>
        /// The type of data being held inside this field
        /// </summary>
        public DataTypes DataType { get; set; }
        /// <summary>
        /// The number that determines how the value is read from the file.<para/>
        /// Can be manually calculated, but if you choose to do that, you should manually calculate the Bitmask, ShiftAmount, and EntryOffset for all fields in the BCSV
        /// </summary>
        public uint Bitmask
        {
            get => _Bitmask;
            set
            {
                if (AutoRecalc)
                    throw new Exception("Can't set the Bitmask if AutoRecalc is enabled.");
                _Bitmask = value;
            }
        }
        private uint _Bitmask = 0xFFFFFFFF;
        /// <summary>
        /// The number of bits to shift while reading the value from the file.<para/>
        /// Can be manually calculated, but if you choose to do that, you should manually calculate the Bitmask, ShiftAmount, and EntryOffset for all fields in the BCSV
        /// </summary>
        public byte ShiftAmount
        {
            get => _ShiftAmount;
            set
            {
                if (AutoRecalc)
                    throw new Exception("Can't set the ShiftAmount if AutoRecalc is enabled.");
                _ShiftAmount = value;
            }
        }
        private byte _ShiftAmount;
        /// <summary>
        /// The offset within the binary entry that this field is located at.<para/>
        /// Can be manually calculated, but if you choose to do that, you should manually calculate the Bitmask, ShiftAmount, and EntryOffset for all fields in the BCSV
        /// </summary>
        public ushort EntryOffset { get; set; }
        /// <summary>
        /// Setting this to true will auto-recalculate the Bitmask and Shift Amount on save
        /// </summary>
        public bool AutoRecalc { get; set; }

        /// <summary>
        /// Gets the default value for this BCSVField's DataType
        /// </summary>
        /// <returns>The default value for this field's DataType</returns>
        public object GetDefaultValue() => BCSV.GetDefaultValue(DataType);

        /// <inheritdoc/>
        public override string ToString() => $"0x{HashName:X8} - {DataType}";
        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Field field &&
                   HashName == field.HashName &&
                   DataType == field.DataType &&
                   EntryOffset == field.EntryOffset &&
                   _Bitmask == field._Bitmask &&
                   _ShiftAmount == field._ShiftAmount &&
                   AutoRecalc == field.AutoRecalc;
        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(HashName, DataType, _Bitmask, _ShiftAmount, EntryOffset, AutoRecalc);
    }
    /// <summary>
    /// A BCSV Entry. Use one of these to store data as defined by the BCSV Fields
    /// </summary>
    public class Entry
    {
        /// <summary>
        /// The Data held in this BCSVEntry. The Key is the Hash that the Data Value belongs to
        /// </summary>
        protected internal Dictionary<uint, object> Data { get; set; } = [];
        /// <summary>
        /// The encoding to use while handling text. Should only realistically be <see cref="StreamUtil.ShiftJIS"/> or <see cref="Encoding.UTF8"/>
        /// </summary>
        protected internal Encoding Encoding { get; set; } = StreamUtil.ShiftJIS;

        /// <summary>
        /// Access the data inside this BCSV Entry
        /// </summary>
        /// <param name="field">The field to get from the entry</param>
        /// <returns>the data for the requested field in this entry</returns>
        public object this[Field field]
        {
            get
            {
                if (!Data.TryGetValue(field.HashName, out object? value))
                    throw new IndexOutOfRangeException(string.Format(JMapException.FIELD_DOES_NOT_EXIST_ERROR, field.HashName));
                return value;
            }
            set
            {
                if (!Data.ContainsKey(field.HashName))
                    throw new IndexOutOfRangeException(string.Format(JMapException.FIELD_DOES_NOT_EXIST_ERROR, field.HashName));
                ArgumentNullException.ThrowIfNull(value);
                switch (field.DataType)
                {
                    case DataTypes.INT32:
                        {
                            if (value is uint u)
                                value = (int)u;
                            else if (value is short s)
                                value = (int)s;
                            else if (value is ushort us)
                                value = (int)us;
                            else if (value is byte b)
                                value = (int)b;
                            else if (value is sbyte sb)
                                value = (int)sb;
                        }

                        if (value is not int)
                            throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, value.GetType().Name, typeof(int).Name));
                        break;
                    case DataTypes.CHARARRAY:
                        if (value is char[] ca)
                        {
                            int count = Encoding.GetByteCount(ca);
                            if (count > JMapException.MAX_CHARARRAY_SIZE)
                                throw new ArgumentOutOfRangeException(nameof(value), string.Format(JMapException.CHARARRAY_TOO_BIG_ERROR, count, JMapException.MAX_CHARARRAY_SIZE));
                        }

                        if (value is string str)
                        {
                            int count = Encoding.GetByteCount(str);
                            if (count > JMapException.MAX_CHARARRAY_SIZE)
                                throw new ArgumentOutOfRangeException(nameof(value), string.Format(JMapException.CHARARRAY_TOO_BIG_ERROR, count, JMapException.MAX_CHARARRAY_SIZE));
                        }

                        if (value is not string or char[])
                            throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, value.GetType().Name, $"{typeof(char[]).Name}/{typeof(string).Name}"));
                        break;
                    case DataTypes.FLOAT:
                        if (value is Half h)
                            value = (float)h;
                        if (value is not float)
                            throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, value.GetType().Name, typeof(float).Name));
                        break;
                    case DataTypes.UINT32:
                        {
                            if (value is int u)
                                value = (uint)u;
                            else if (value is short s)
                                value = (uint)s;
                            else if (value is ushort us)
                                value = (uint)us;
                            else if (value is byte b)
                                value = (uint)b;
                            else if (value is sbyte sb)
                                value = (uint)sb;
                        }

                        if (value is not uint)
                            throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, value.GetType().Name, typeof(uint).Name));
                        break;
                    case DataTypes.INT16:
                        {
                            if (value is uint u && u <= ushort.MaxValue && u >= ushort.MinValue)
                                value = (ushort)u;
                            else if (value is int i && i <= short.MaxValue && i >= short.MinValue)
                                value = (short)i;
                            if (value is ushort us)
                                value = (short)us;
                            else if (value is byte b)
                                value = (short)b;
                            else if (value is sbyte sb)
                                value = (short)sb;
                        }

                        if (value is not short)
                            throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, value.GetType().Name, typeof(short).Name));
                        Data[field.HashName] = value;
                        break;
                    case DataTypes.BYTE:
                        {
                            if (value is uint u && u <= byte.MaxValue && u >= byte.MinValue)
                                value = (byte)u;
                            else if (value is int i && i <= sbyte.MaxValue && i >= sbyte.MinValue)
                                value = (sbyte)i;
                            else if (value is ushort us && us <= byte.MaxValue && us >= byte.MinValue)
                                value = (byte)us;
                            else if (value is short s && s <= sbyte.MaxValue && s >= sbyte.MinValue)
                                value = (sbyte)s;

                            if (value is sbyte sb)
                                value = (byte)sb;
                        }

                        if (value is not byte)
                            throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, value.GetType().Name, typeof(short).Name));
                        break;
                    case DataTypes.STRING:
                        if (value is float or Half or uint or int or ushort or short or byte or sbyte)
                            value = value.ToString() ?? "&";

                        if (value is char[] ca2)
                            value = new string(ca2);

                        if (value is not string)
                            throw new ArgumentException(string.Format(JMapException.INCORRECT_DATATYPE_ERROR, value.GetType().Name, typeof(short).Name));
                        break;
                    case DataTypes.NULL:
                    default:
                        throw new NullReferenceException(string.Format(JMapException.INVALID_DATATYPE_ERROR, field.DataType));
                }
                Data[field.HashName] = value;
            }
        }
        
        /// <summary>
        /// Copies the data from this Entry to another Entry
        /// </summary>
        /// <param name="Target">The entry to copy the data onto</param>
        public void CopyTo(Entry Target)
        {
            Target.Encoding = Encoding;
            Target.Data = new(Data);
        }

        /// <summary>
        /// Attempt to load data from a Clipboard string
        /// </summary>
        /// <param name="input">The Clipboard input string</param>
        /// <param name="Head">The head to try and paste with (for validation purposes)</param>
        /// <returns>true if successful</returns>
        public bool FromClipboard(string input, string Head = "BCSVEntry")
        {
            if (!input.StartsWith(Head+"|"))
                return false;

            Dictionary<uint, object> backup = new(Data);
            try
            {
                string[] DataSplit = input.Split('|');

                Data.Clear();
                for (int i = 1; i < DataSplit.Length; i++)
                {
                    string[] currentdata = DataSplit[i].Split('%');
                    if (uint.TryParse(currentdata[0], System.Globalization.NumberStyles.HexNumber, null, out uint result))
                    {
                        Type? t = Type.GetType("System." + currentdata[2], true) ?? throw new NullReferenceException($"Failed to map {currentdata[2]} to a system property");
                        Data.Add(result, Convert.ChangeType(currentdata[1], t));
                    }
                    else
                    {
                        throw new IOException($"Failed to decode {currentdata[0]} as Hex");
                    }
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
        /// Create a BCSV string that can be copied to the clipboard
        /// </summary>
        /// <param name="Head">The head to use in the copy (for validation purposes)</param>
        /// <returns></returns>
        public string ToClipboard(string Head = "BCSVEntry")
        {
            StringBuilder sb = new();
            sb.Append(Head);
            foreach (var item in Data)
            {
                sb.Append('|');
                sb.Append(item.Key.ToString("X8"));
                sb.Append('%');
                sb.Append(item.Value.ToString());
                sb.Append('%');
                sb.Append(item.Value.GetType().ToString().Replace("System.", ""));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Adds the value into the BCSV entry
        /// </summary>
        /// <param name="Hash"></param>
        /// <param name="value"></param>
        public void Add(uint Hash, object value) => Data.Add(Hash, value);
        /// <summary>
        /// Tries to add the value into the BCSV Entry
        /// </summary>
        /// <param name="Hash"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryAdd(uint Hash, object value) => Data.TryAdd(Hash, value);
        /// <summary>
        /// Checks to see if this BCSV Entry contains data for the given hash
        /// </summary>
        /// <param name="Hash">the hash to look for</param>
        /// <returns>TRUE if the data exists, FALSE otherwise</returns>
        public bool Contains(uint Hash) => Data.ContainsKey(Hash);

        /// <summary>
        /// Removes the data for the given hash
        /// </summary>
        /// <param name="Hash"></param>
        public void Remove(uint Hash) => Data.Remove(Hash);

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Entry e &&
                Encoding == e.Encoding &&
                CollectionUtil.Equals(Data, e.Data);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Data, Encoding);

        //Adds fields that are missing from the field dictionary
        internal void FillMissingFields(Dictionary<uint, Field> fields)
        {
            foreach (Field item in fields.Values.Where(item => !Data.ContainsKey(item.HashName)))
                Data.Add(item.HashName, GetDefaultValue(item.DataType));
        }
        //Removes fields that are missing from the field dictionary
        internal void RemoveMissingFields(Dictionary<uint, Field> fields)
        {
            foreach (Field item in fields.Values)
            {
                if (Data.ContainsKey(item.HashName))
                    continue;
                Data.Remove(item.HashName);
            }
        }
    }

    /// <summary>
    /// BCSV.Field Data Types
    /// </summary>
    public enum DataTypes : byte
    {
        /// <summary>
        /// 32 bit integer (4 Bytes)
        /// </summary>
        INT32 = 0,
        /// <summary>
        /// A 32 byte array of characters. (32 bytes)<para/>Like a string, but limited to 32 bytes. <see cref="byte"/>[] and <see cref="string"/> are accepted here
        /// </summary>
        CHARARRAY = 1,
        /// <summary>
        /// 32 bit decimal value (4 Bytes)
        /// </summary>
        FLOAT = 2,
        /// <summary>
        /// Unsigned 32 bit integer (4 Bytes)<para/>It's worth noting that most games do not actually use this to tell the difference between UInt and Int
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
        /// Refers to NULL.<para/>DO NOT USE as it cannot be written to a file.
        /// </summary>
        NULL = 7
    }

    /// <summary>
    /// Represents a function that can be used to hash a string for BCSV purposes
    /// </summary>
    /// <param name="Str">The characters to hash</param>
    /// <returns></returns>
    public delegate uint BCSVHashFunction(ReadOnlySpan<char> Str);
    /// <summary>
    /// Converts a stringn to an Old Hash. Used in older Jsys games (Luigi's Mansion)
    /// </summary>
    /// <param name="Str">the string to convert</param>
    /// <returns>the hashed of the string</returns>
    public static uint StringToHash_Old(ReadOnlySpan<char> Str)
    {
        uint ret = 0;
        for (int i = 0; i < Str.Length; i++)
        {
            ret *= (ret << 8) & 0xFFFFFFFF;
            ret += Str[i];
            ret %= 33554393;
        }
        return ret;
    }
    /// <summary>
    /// Converts a stringn to a JGadget Hash. Used in newer Jsys games (SMG, SMG2, DKJB)
    /// </summary>
    /// <param name="Str">the string to convert</param>
    /// <returns>the hashed of the string</returns>
    public static uint StringToHash_JGadget(ReadOnlySpan<char> Str)
    {
        uint ret = 0;
        for (int i = 0; i < Str.Length; i++)
        {
            ret *= 0x1F;
            ret += Str[i];
        }
        return ret;
    }

    /// <summary>
    /// Represents a function that can be used to calculate field data before saving.<para/>
    /// Be aware that these will edit the BCSV field data.
    /// </summary>
    /// <param name="target"></param>
    public delegate void FieldDataCalculator(ref BCSV target);
    /// <summary>
    /// The function used to organize the fields
    /// </summary>
    /// <param name="target">the target BCSV</param>
    public static void CalculateFieldDataDefault(ref BCSV target)
    {
        //Basic, uncompressed BCSV organization
        List<Field> SortedFields = [.. target.Fields.Values];
        SortedFields.Sort(JGadgetFieldSort);

        ushort entrysize = 0;
        for (int i = 0; i < SortedFields.Count; i++)
        {
            Field currentfield = SortedFields[i];
            if (currentfield.AutoRecalc)
            {
                currentfield.AutoRecalc = false;
                currentfield.Bitmask = GetDefaultBitmask(currentfield.DataType);
                currentfield.ShiftAmount = 0;
                currentfield.EntryOffset = entrysize; //I leave it up to the user to make sure that "non autorecalc" fields mesh well...in other words it's either all manual or all automatic!
            }
            entrysize += GetDataTypeSize(currentfield.DataType);
        }
    }

    /// <summary>
    /// Gets the default value of the given DataType
    /// </summary>
    /// <param name="type">the DataTypes to get the default for</param>
    /// <returns>The default value of the DataType</returns>
    public static object GetDefaultValue(DataTypes type) => type switch
    {
        DataTypes.INT32 => default(int),
        DataTypes.CHARARRAY => "", //Yes, I know it's a string.
        DataTypes.FLOAT => default(float),
        DataTypes.UINT32 => default(uint),
        DataTypes.INT16 => default(short),
        DataTypes.BYTE => default(byte),
        DataTypes.STRING => "",
        _ => throw new NullReferenceException(),
    };
    /// <summary>
    /// Gets the default value for the bitmask of the given Data Type
    /// </summary>
    /// <param name="type">the DataTypes to get the default for</param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    public static uint GetDefaultBitmask(DataTypes type) => type switch
    {
        DataTypes.INT32 or DataTypes.FLOAT or DataTypes.UINT32 or DataTypes.STRING => 0xFFFFFFFF,
        DataTypes.CHARARRAY => 0x00000000,
        DataTypes.INT16 => 0x0000FFFF,
        DataTypes.BYTE => 0x000000FF,
        _ => throw new NullReferenceException(),
    };
    /// <summary>
    /// Gets the byte size of the given data type
    /// </summary>
    /// <param name="type">The type to get the size of</param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    public static ushort GetDataTypeSize(DataTypes type) => type switch
    {
        DataTypes.INT32 or DataTypes.FLOAT or DataTypes.UINT32 or DataTypes.STRING => 0x04,
        DataTypes.CHARARRAY => JMapException.MAX_CHARARRAY_SIZE,
        DataTypes.INT16 => 0x02,
        DataTypes.BYTE => 0x01,
        _ => throw new NullReferenceException(),
    };

    /// <summary>
    /// Determines the order of fields based on official format specs.<para/>This is a <see cref="Comparison{T}"/>
    /// </summary>
    /// <param name="L"></param>
    /// <param name="R"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    protected static int JGadgetFieldSort(Field L, Field R)
    {
        int LP = GetPriority(L.DataType), RP = GetPriority(R.DataType);
        return LP.CompareTo(RP);

        static int GetPriority(DataTypes DT) => DT switch
        {
            DataTypes.INT32 => 2,
            DataTypes.CHARARRAY => 0,
            DataTypes.FLOAT => 1,
            DataTypes.UINT32 => 3,
            DataTypes.INT16 => 4,
            DataTypes.BYTE => 5,
            DataTypes.STRING => 6,
            _ => throw new NullReferenceException(),
        };
    }
}

internal static class JMapException //Not typically what this class is
{
    internal const int MAX_CHARARRAY_SIZE = 32;
    internal const string INCORRECT_DATATYPE_ERROR = "The provided value is of the incorrect DataTypes ({0} != {1})";
    internal const string FIELD_DOES_NOT_EXIST_ERROR = "This BCSV entry does not contain data for {0}";
    internal const string CHARARRAY_TOO_BIG_ERROR = "Characters encode to more than {1} bytes. {0} > {1}";
    internal const string INVALID_DATATYPE_ERROR = "DataTypes cannot be {0}";
    internal const string FIELD_ALREADY_EXISTS_ERROR = "The BCSV already contains a field with hash {0}";
}