using System;
using System.Diagnostics;
using System.IO;
using System.Text;
[assembly: CLSCompliant(true)]

namespace Hack.io.Utility;

/// <summary>
/// A static class for stream helper functions
/// </summary>
public static class StreamUtil
{
    //Registers the code page provider so we can use SHIFT-JIS
    static StreamUtil() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>
    /// Shortcut to Encoding.GetEncoding("Shift-JIS")
    /// </summary>
    public static Encoding ShiftJIS => Encoding.GetEncoding("Shift-JIS");

    //====================================================================================================

    //This is used as the Target output only.
    private static bool UseBigEndian = false;

    /// <summary>
    /// Gets the current Endian setting.
    /// </summary>
    /// <returns>TRUE if "Big Endian" is active.<para/>FALSE if "LIttle Endian" is active.</returns>
    public static bool GetCurrentEndian() => UseBigEndian;
    /// <summary>
    /// Sets the current Endian
    /// </summary>
    /// <param name="Endian">The endian to use:<para/>TRUE = "Big Endian"<para/>FALSE = "LIttle Endian"</param>
    public static void SetEndian(bool Endian) => UseBigEndian = Endian;
    /// <summary>
    /// Sets the current Endian to "Big Endian"
    /// </summary>
    public static void SetEndianBig() => SetEndian(true);
    /// <summary>
    /// Sets the current Endian to "Little Endian"
    /// </summary>
    public static void SetEndianLittle() => SetEndian(false);

    /// <summary>
    /// Converts the endian bytes to the desired endian.
    /// </summary>
    /// <param name="data">The span of bytes to switch</param>
    /// <param name="Invert">If TRUE, the data will come out in the opposite endian</param>
    public static void ApplyEndian<T>(Span<T> data, bool Invert = false)
    {
        //If the system endian matches the file's endian, we can do nothing!
        if (!UseBigEndian == BitConverter.IsLittleEndian)
            return;
        if (!Invert)
            data.Reverse();
    }

    //====================================================================================================

    /// <summary>
    /// Reads a set amount of bytes from memory into an array. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="Count">The number of bytes to read<para/>MAX 8</param>
    /// <returns>a byte[] with the read data.</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    public static byte[] ReadEndian(this Stream Strm, int Count)
    {
        if (Count > 8)
            throw new ArgumentException($"\"{nameof(Count)}\" cannot be greater than 8", nameof(Count));
        if (Count < 0)
            throw new ArgumentException($"\"{nameof(Count)}\" cannot be less than 0", nameof(Count));

        long curpos = Strm.Position;
        Span<byte> read = stackalloc byte[Count];
        int bytecount = Strm.Read(read);

        if (bytecount != Count)
            throw new IOException($"Failed to read the file at {curpos}");

        ApplyEndian(read);
        return read.ToArray();
    }
    /// <summary>
    /// An alternative to <see cref="Stream.ReadByte"/>
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting byte</returns>
    [CLSCompliant(false)]
    public static sbyte ReadInt8(this Stream Strm) => (sbyte)Strm.ReadByte();
    /// <summary>
    /// An alternative to <see cref="Stream.ReadByte"/>
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting byte</returns>
    public static byte ReadUInt8(this Stream Strm) => (byte)Strm.ReadByte();
    /// <summary>
    /// Reads an Int16 from the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting Int16 from the file</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static short ReadInt16(this Stream Strm) => BitConverter.ToInt16(Strm.ReadEndian(sizeof(short)));
    /// <summary>
    /// Reads an UInt16 from the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting UInt16 from the file</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    [CLSCompliant(false)]
    public static ushort ReadUInt16(this Stream Strm) => BitConverter.ToUInt16(Strm.ReadEndian(sizeof(ushort)));
    /// <summary>
    /// Reads an Int32 from the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting Int32 from the file</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static int ReadInt32(this Stream Strm) => BitConverter.ToInt32(Strm.ReadEndian(sizeof(int)));
    /// <summary>
    /// Reads an UInt32 from the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting UInt32 from the file</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    [CLSCompliant(false)]
    public static uint ReadUInt32(this Stream Strm) => BitConverter.ToUInt32(Strm.ReadEndian(sizeof(uint)));
    /// <summary>
    /// Reads an Int64 from the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting Int64 from the file</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static long ReadInt64(this Stream Strm) => BitConverter.ToInt64(Strm.ReadEndian(sizeof(long)));
    /// <summary>
    /// Reads an UInt64 from the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting UInt64 from the file</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    [CLSCompliant(false)]
    public static ulong ReadUInt64(this Stream Strm) => BitConverter.ToUInt64(Strm.ReadEndian(sizeof(ulong)));
    /// <summary>
    /// Reads a Half from the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting Half from the file</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static Half ReadHalf(this Stream Strm) => BitConverter.ToHalf(Strm.ReadEndian(sizeof(short)));
    /// <summary>
    /// Reads a Single from the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting Single from the file</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static float ReadSingle(this Stream Strm) => BitConverter.ToSingle(Strm.ReadEndian(sizeof(float)));
    /// <summary>
    /// Reads a Double from the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting Double from the file</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="IOException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static double ReadDouble(this Stream Strm) => BitConverter.ToDouble(Strm.ReadEndian(sizeof(double)));

    /// <summary>
    /// Reads a value as an Enum instead of the type defined in T
    /// </summary>
    /// <typeparam name="E">The Enum to return</typeparam>
    /// <typeparam name="T">The Datatype to read from the file</typeparam>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="Reader">The function to read a single instance of T from the file.</param>
    /// <returns></returns>
    public static E ReadEnum<E, T>(this Stream Strm, Func<Stream, T> Reader)
        where E : Enum
        where T : unmanaged
    {
        return (E)(dynamic)Reader(Strm);
    }

    /// <summary>
    /// Reads multiple of the same data type.<para/>Example:<para/>-
    /// <example>MyStream.ReadMulti(3, StreamUtil.ReadSingle);</example>
    /// </summary>
    /// <typeparam name="T">Needs to be one of the supported Read types.</typeparam>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of items of type T to read.</param>
    /// <param name="Reader">The function to read a single instance of T from the file.</param>
    /// <returns>An array of T</returns>
    /// <exception cref="ArgumentException"/>
    public static T[] ReadMulti<T>(this Stream Strm, int EntryCount, Func<Stream, T> Reader)
    {
        if (EntryCount < 0)
            throw new ArgumentException($"\"{nameof(EntryCount)}\" cannot be less than 0", nameof(EntryCount));

        T[] values= new T[EntryCount];
        for (int i = 0; i < EntryCount; i++)
            values[i] = Reader(Strm);
        return values;
    }
    /// <summary>
    /// Reads multiple Int16
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of Int16 to read.</param>
    /// <returns>A short[] of length <paramref name="EntryCount"/></returns>
    /// <exception cref="ArgumentException"/>
    public static short[] ReadMultiInt16(this Stream Strm, int EntryCount) => Strm.ReadMulti(EntryCount, ReadInt16);
    /// <summary>
    /// Reads multiple UInt16
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of UInt16 to read.</param>
    /// <returns>A ushort[] of length <paramref name="EntryCount"/></returns>
    /// <exception cref="ArgumentException"/>
    [CLSCompliant(false)]
    public static ushort[] ReadMultiUInt16(this Stream Strm, int EntryCount) => Strm.ReadMulti(EntryCount, ReadUInt16);
    /// <summary>
    /// Reads multiple Int32
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of Int32 to read.</param>
    /// <returns>A int[] of length <paramref name="EntryCount"/></returns>
    /// <exception cref="ArgumentException"/>
    public static int[] ReadMultiInt32(this Stream Strm, int EntryCount) => Strm.ReadMulti(EntryCount, ReadInt32);
    /// <summary>
    /// Reads multiple UInt32
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of UInt32 to read.</param>
    /// <returns>A uint[] of length <paramref name="EntryCount"/></returns>
    /// <exception cref="ArgumentException"/>
    [CLSCompliant(false)]
    public static uint[] ReadMultiUInt32(this Stream Strm, int EntryCount) => Strm.ReadMulti(EntryCount, ReadUInt32);
    /// <summary>
    /// Reads multiple Int64
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of Int64 to read.</param>
    /// <returns>A long[] of length <paramref name="EntryCount"/></returns>
    /// <exception cref="ArgumentException"/>
    public static long[] ReadMultiInt64(this Stream Strm, int EntryCount) => Strm.ReadMulti(EntryCount, ReadInt64);
    /// <summary>
    /// Reads multiple UInt64
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of UInt64 to read.</param>
    /// <returns>A ulong[] of length <paramref name="EntryCount"/></returns>
    /// <exception cref="ArgumentException"/>
    [CLSCompliant(false)]
    public static ulong[] ReadMultiUInt64(this Stream Strm, int EntryCount) => Strm.ReadMulti(EntryCount, ReadUInt64);
    /// <summary>
    /// Reads multiple Half
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of Half to read.</param>
    /// <returns>A Half[] of length <paramref name="EntryCount"/></returns>
    /// <exception cref="ArgumentException"/>
    public static Half[] ReadMultiHalf(this Stream Strm, int EntryCount) => Strm.ReadMulti(EntryCount, ReadHalf);
    /// <summary>
    /// Reads multiple Single
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of Single to read.</param>
    /// <returns>A float[] of length <paramref name="EntryCount"/></returns>
    /// <exception cref="ArgumentException"/>
    public static float[] ReadMultiSingle(this Stream Strm, int EntryCount) => Strm.ReadMulti(EntryCount, ReadSingle);
    /// <summary>
    /// Reads multiple Double
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="EntryCount">The number of Double to read.</param>
    /// <returns>A double[] of length <paramref name="EntryCount"/></returns>
    /// <exception cref="ArgumentException"/>
    public static double[] ReadMultiDouble(this Stream Strm, int EntryCount) => Strm.ReadMulti(EntryCount, ReadDouble);

    /// <summary>
    /// !!ADVANCED USERS ONLY!!<para/>
    /// Reads an offset from the current position, jumps to that offset, reads a value there, then jumps back to just after reading the offset.
    /// </summary>
    /// <typeparam name="T">Needs to be one of the supported Read types.</typeparam>
    /// <typeparam name="Tout">Needs to be one of the supported Read types.</typeparam>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="OffsetReader">The function that will read the offset value</param>
    /// <param name="RelativeToPosition">The offset to uses as a base position</param>
    /// <param name="Reader">The function that will read the value at the offset</param>
    /// <returns>The value at the offset</returns>
    public static Tout ReadFromOffset<T, Tout>(this Stream Strm, Func<Stream, T> OffsetReader, long RelativeToPosition, Func<Stream, Tout> Reader)
        where T : unmanaged
    {
        T Offset = OffsetReader(Strm);
        long PausePosition = Strm.Position;
        Strm.Position = RelativeToPosition + long.Parse(Offset.ToString() ?? "0"); //Real "non generic numbers" moment...ugh
        Tout result = Reader(Strm);
        Strm.Position = PausePosition;
        return result;
    }
    /// <summary>
    /// !!ADVANCED USERS ONLY!!<para/>
    /// Reads an offset from the current position, jumps to that offset, reads multiple values there, then jumps back to just after reading the offset.
    /// </summary>
    /// <typeparam name="T">Needs to be one of the supported Read types.</typeparam>
    /// <typeparam name="Tout">Needs to be one of the supported Read types.</typeparam>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="OffsetReader">The function that will read the offset value</param>
    /// <param name="RelativeToPosition">The offset to uses as a base position</param>
    /// <param name="MultiReader">The function that will read the values at the offset</param>
    /// <param name="EntryCount">The number of values to read</param>
    /// <returns>An array of values at the offset</returns>
    public static Tout[] ReadMultiFromOffset<T, Tout>(this Stream Strm, Func<Stream, T> OffsetReader, long RelativeToPosition, Func<Stream, int, Tout[]> MultiReader, int EntryCount)
        where T : unmanaged
    {
        T Offset = OffsetReader(Strm);
        long PausePosition = Strm.Position;
        Strm.Position = RelativeToPosition + long.Parse(Offset.ToString() ?? "0"); //Real "non generic numbers" moment...ugh
        Tout[] result = MultiReader(Strm, EntryCount);
        Strm.Position = PausePosition;
        return result;
    }
    /// <summary>
    /// !!ADVANCED USERS ONLY!!<para/>
    /// Reads an offset from the current position, jumps to that offset, reads a value there, then jumps back to just after reading the offset.<para/>
    /// This is repeated "<paramref name="OffsetCount"/>" times
    /// </summary>
    /// <typeparam name="T">Needs to be one of the supported Read types.</typeparam>
    /// <typeparam name="Tout">Needs to be one of the supported Read types.</typeparam>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="OffsetReader">The function that will read the offset value</param>
    /// <param name="OffsetCount">The number of offsets to read</param>
    /// <param name="RelativeToPosition">The offset to uses as a base position</param>
    /// <param name="Reader">The function that will read the value at the offset</param>
    /// <returns>An array containing the value at each offset</returns>
    public static Tout[] ReadFromOffsetMulti<T, Tout>(this Stream Strm, Func<Stream, T> OffsetReader, int OffsetCount, long RelativeToPosition, Func<Stream, Tout> Reader)
        where T : unmanaged
    {
        Tout[] results = new Tout[OffsetCount];
        for (int i = 0; i < OffsetCount; i++)
            results[i] = Strm.ReadFromOffset(OffsetReader, RelativeToPosition, Reader);
        return results;
    }
    /// <summary>
    /// !!ADVANCED USERS ONLY!!<para/>
    /// Reads an offset from the current position, jumps to that offset, reads multiple values there, then jumps back to just after reading the offset.<para/>
    /// This is repeated "<paramref name="OffsetCount"/>" times
    /// </summary>
    /// <typeparam name="T">Needs to be one of the supported Read types.</typeparam>
    /// <typeparam name="Tout">Needs to be one of the supported Read types.</typeparam>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="OffsetReader">The function that will read the offset value</param>
    /// <param name="OffsetCount">The number of offsets to read</param>
    /// <param name="RelativeToPosition">The offset to uses as a base position</param>
    /// <param name="MultiReader">The function that will read the values at the offset</param>
    /// <param name="EntryCount">The number of values to read</param>
    /// <returns>An array of arrays that contain the values at the offsets</returns>
    public static Tout[][] ReadMultiFromOffsetMulti<T, Tout>(this Stream Strm, Func<Stream, T> OffsetReader, int OffsetCount, long RelativeToPosition, Func<Stream, int, Tout[]> MultiReader, int EntryCount)
        where T : unmanaged
    {
        Tout[][] results = new Tout[OffsetCount][];
        for (int i = 0; i < OffsetCount; i++)
            results[i] = Strm.ReadMultiFromOffset(OffsetReader, RelativeToPosition, MultiReader, EntryCount);
        return results;
    }

    /// <summary>
    /// Reads a value at the given absolute offset.<param/>Does not put the Stream Position back where it came from.
    /// </summary>
    /// <typeparam name="Tout">The type to return</typeparam>
    /// <param name="Strm">The stream to read</param>
    /// <param name="Offset">The position in the stream to read from</param>
    /// <param name="Reader">The function to use to read</param>
    /// <returns>the read value</returns>
    public static Tout ReadAtOffset<Tout>(this Stream Strm, long Offset, Func<Stream, Tout> Reader)
    {
        Strm.Position = Offset;
        return Reader(Strm);
    }
    /// <summary>
    /// Reads multiple values at the given absolute offset.<param/>Does not put the Stream Position back where it came from.
    /// </summary>
    /// <typeparam name="Tout"></typeparam>
    /// <param name="Strm">The stream to read</param>
    /// <param name="Offset">The position in the stream to read from</param>
    /// <param name="MultiReader">The function that will read the values at the offset</param>
    /// <param name="EntryCount">The number of values to read</param>
    /// <returns>An array of values at the offset</returns>
    public static Tout[] ReadMultiAtOffset<Tout>(this Stream Strm, long Offset, Func<Stream, int, Tout[]> MultiReader, int EntryCount)
    {
        Strm.Position = Offset;
        return MultiReader(Strm, EntryCount);
    }

    /// <summary>
    /// Reads a value that has varying length. Maxes out at 28 bits read.
    /// </summary>
    /// <param name="Strm"></param>
    /// <returns></returns>
    public static int? ReadVariableLength(this Stream Strm)
    {
        int vlq = 0;
        byte temp;
        int counter = 0;
        do
        {
            temp = Strm.ReadUInt8();
            vlq = (vlq << 7) | (temp & 0x7F);
            if (++counter >= 4)
                return null;
        } while ((temp & 0x80) > 0);
        return vlq;
    }

    /// <summary>
    /// Reads a string from the Stream that's NULL terminated.<para/>This method is faster for single byte encodings
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="Enc">The encoding to use. Should only be 1 byte per character.</param>
    /// <returns>The resulting string</returns>
    /// <exception cref="EndOfStreamException"></exception>
    public static string ReadString(this Stream Strm, Encoding Enc)
    {
        List<byte> bytes = [];
        while (Strm.ReadByte() != 0)
        {
            Strm.Position -= 1;
            int c = Strm.ReadByte();
            if (c == -1 || Strm.Position >= Strm.Length)
                throw new EndOfStreamException($"{nameof(ReadString)} was unable to locate the end of the string before the end of the file.");

            bytes.Add((byte)c);
        }
        byte[] conversionbytes = [.. bytes];
        return Enc.GetString(conversionbytes, 0, conversionbytes.Length);
    }
    /// <summary>
    /// Reads a string from the Stream that's NULL terminated.<para/>This method is for multi-byte encodings
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="Enc">The encoding to use.</param>
    /// <param name="ByteCount">The stride of the Encoding. Variable Length not supported</param>
    /// <returns>The resulting string</returns>
    /// <exception cref="EndOfStreamException"></exception>
    public static string ReadString(this Stream Strm, Encoding Enc, int ByteCount)
    {
        List<byte> bytes = [];
        byte[] Checker = new byte[ByteCount];
        bool IsDone = false;
        do
        {
            if (Strm.Position > Strm.Length)
                throw new EndOfStreamException($"{nameof(ReadString)} was unable to locate the end of the string before the end of the file.");
            Strm.Read(Checker, 0, ByteCount);
            if (Checker.All(B => B == 0x00))
            {
                IsDone = true;
                break;
            }
            bytes.AddRange(Checker);
        } while (!IsDone);
        byte[] conversionbytes = [.. bytes];
        return Enc.GetString(conversionbytes, 0, conversionbytes.Length);
    }
    /// <summary>
    /// Reads a string from the Stream that's fixed in size.<para/>This method is faster for single byte encodings
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="Enc">The encoding to use. Should only be 1 byte per character.</param>
    /// <param name="StringLength">The number of characters to read</param>
    /// <returns>The resulting string</returns>
    /// <exception cref="IOException"></exception>
    public static string ReadString(this Stream Strm, int StringLength, Encoding Enc)
    {
        byte[] bytes = new byte[StringLength];
        if (Strm.Read(bytes, 0, StringLength) != bytes.Length)
            throw new IOException("Failed to read the string.");
        return Enc.GetString(bytes, 0, StringLength);
    }
    /// <summary>
    /// Reads a string from the Stream that's fixed in size.<para/>This method is for multi-byte encodings
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="Enc">The encoding to use.</param>
    /// <param name="StringLength">The number of characters to read</param>
    /// <param name="ByteCount">The stride of the Encoding. Variable Length not supported</param>
    /// <returns>The resulting string</returns>
    /// <exception cref="IOException"></exception>
    public static string ReadString(this Stream Strm, int StringLength, Encoding Enc, int ByteCount)
    {
        StringLength *= ByteCount;
        byte[] bytes = new byte[StringLength];
        if (Strm.Read(bytes, 0, StringLength) != bytes.Length)
            throw new IOException("Failed to read the string.");
        return Enc.GetString(bytes, 0, StringLength);
    }

    /// <summary>
    /// Reads a "SHIFT-JIS" string from the Stream that's NULL terminated.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting string</returns>
    /// <exception cref="EndOfStreamException"></exception>
    public static string ReadStringJIS(this Stream Strm) => Strm.ReadString(ShiftJIS);
    /// <summary>
    /// Reads an "ASCII" string from the Stream that's NULL terminated.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <returns>The resulting string</returns>
    /// <exception cref="EndOfStreamException"></exception>
    public static string ReadStringASCII(this Stream Strm) => Strm.ReadString(Encoding.ASCII);

    /// <summary>
    /// Checks the stream for a given Magic identifier.<para/>Advances the Stream's Position forwards by Magic.Length
    /// </summary>
    /// <param name="Strm">The stream to read</param>
    /// <param name="Magic">The magic to check</param>
    /// <returns>TRUE if the next bytes match the magic, FALSE otherwise.</returns>
    public static bool IsMagicMatch(this Stream Strm, ReadOnlySpan<byte> Magic)
    {
        Debug.Assert(Magic.Length is > 0 and < 16);

        Span<byte> read = stackalloc byte[Magic.Length]; //Should be fine since MAGIC's are typically only 4 bytes long.
        Strm.ReadExactly(read);
        ApplyEndian(read, true);
        return read.SequenceEqual(Magic);
    }
    /// <inheritdoc cref="IsMagicMatch(Stream, ReadOnlySpan{byte})"/>
    public static bool IsMagicMatch(this Stream Strm, ReadOnlySpan<char> Magic) => IsMagicMatch(Strm, Magic, Encoding.ASCII);
    /// <summary>
    /// Checks the stream for a given Magic identifier.<para/>Advances the Stream's Position forwards by Magic.Length
    /// </summary>
    /// <param name="Strm">The stream to read</param>
    /// <param name="Magic">The magic to check</param>
    /// <param name="Enc">The encoding that should be used when reading the file</param>
    /// <returns>TRUE if the next bytes match the magic, FALSE otherwise.</returns>
    public static bool IsMagicMatch(this Stream Strm, ReadOnlySpan<char> Magic, Encoding Enc)
    {
        Debug.Assert(Magic.Length is > 0 and < 16);

        string str = Strm.ReadString(Magic.Length, Enc);
        Span<char> to = new(str.ToCharArray());
        ApplyEndian(to, true);
        return to.SequenceEqual(Magic);
    }

    //====================================================================================================

    /// <summary>
    /// Writes a set of bytes to the stream
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Data">The data to write</param>
    /// <exception cref="ArgumentException"></exception>"
    public static void WriteEndian(this Stream Strm, byte[] Data)
    {
        if (Data.Length > 8)
            throw new ArgumentException($"\"{nameof(Data)}\" cannot be larger than 8", nameof(Data));
        if (Data.Length < 0)
            throw new ArgumentException($"\"{nameof(Data)}\" cannot be smaller than 0", nameof(Data));

        Span<byte> span = stackalloc byte[Data.Length];
        Data.CopyTo(span);
        ApplyEndian(span);
        Strm.Write(span);
    }
    /// <summary>
    /// An alternative to <see cref="Stream.WriteByte(byte)"/>
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    [CLSCompliant(false)]
    public static void WriteInt8(this Stream Strm, sbyte Value) => Strm.WriteByte((byte)Value);
    /// <summary>
    /// An alternative to <see cref="Stream.WriteByte(byte)"/>
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    public static void WriteUInt8(this Stream Strm, byte Value) => Strm.WriteByte(Value);
    /// <summary>
    /// Writes an Int16 to the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    public static void WriteInt16(this Stream Strm, short Value) => Strm.WriteEndian(BitConverter.GetBytes(Value));
    /// <summary>
    /// Writes an UInt16 to the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    [CLSCompliant(false)]
    public static void WriteUInt16(this Stream Strm, ushort Value) => Strm.WriteEndian(BitConverter.GetBytes(Value));
    /// <summary>
    /// Writes an Int32 to the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    public static void WriteInt32(this Stream Strm, int Value) => Strm.WriteEndian(BitConverter.GetBytes(Value));
    /// <summary>
    /// Writes an UInt32 to the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    [CLSCompliant(false)]
    public static void WriteUInt32(this Stream Strm, uint Value) => Strm.WriteEndian(BitConverter.GetBytes(Value));
    /// <summary>
    /// Writes an Int64 to the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    public static void WriteInt64(this Stream Strm, long Value) => Strm.WriteEndian(BitConverter.GetBytes(Value));
    /// <summary>
    /// Writes an UInt64 to the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    [CLSCompliant(false)]
    public static void WriteUInt64(this Stream Strm, ulong Value) => Strm.WriteEndian(BitConverter.GetBytes(Value));
    /// <summary>
    /// Writes a Half to the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    public static void WriteHalf(this Stream Strm, Half Value) => Strm.WriteEndian(BitConverter.GetBytes(Value));
    /// <summary>
    /// Writes a single to the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    public static void WriteSingle(this Stream Strm, float Value) => Strm.WriteEndian(BitConverter.GetBytes(Value));
    /// <summary>
    /// Writes a double to the stream. Respects Endian.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    public static void WriteDouble(this Stream Strm, double Value) => Strm.WriteEndian(BitConverter.GetBytes(Value));

    /// <summary>
    /// Writes a value as an Enum instead of the type defined in T
    /// </summary>
    /// <typeparam name="E">The Enum to write</typeparam>
    /// <typeparam name="T">The Datatype to write to the file</typeparam>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Value">The value to write</param>
    /// <param name="Writer">The action to write a single instance of T to the file.</param>
    public static void WriteEnum<E, T>(this Stream Strm, E Value, Action<Stream, T> Writer)
        where E : Enum
        where T : unmanaged
    {
        Writer(Strm, (T)(dynamic)Value);
    }

    /// <summary>
    /// Writes multiple of the same data type.<para/>Example:<para/>-
    /// <example>MyStream.WriteMulti(MyFloatArray, StreamUtil.WriteSingle);</example> 
    /// </summary>
    /// <typeparam name="T">Needs to be one of the supported Write types.</typeparam>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The array of values to write</param>
    /// <param name="Writer">The action to write a single instance of T to the file.</param>
    /// <exception cref="ArgumentException"></exception>
    public static void WriteMulti<T>(this Stream Strm, IList<T> Values, Action<Stream, T> Writer)
    {
        if (Values.Count < 0)
            throw new ArgumentException($"\"{nameof(Values)}\" cannot be smaller than 0", nameof(Values));

        for (int i = 0; i < Values.Count; i++)
            Writer(Strm, Values[i]);
    }
    /// <summary>
    /// Writes multiple Int16
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The Int16 values to write</param>
    public static void WriteMultiInt16(this Stream Strm, IList<short> Values) => Strm.WriteMulti(Values, WriteInt16);
    /// <summary>
    /// Writes multiple UInt16
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The UInt16 values to write</param>
    [CLSCompliant(false)]
    public static void WriteMultiUInt16(this Stream Strm, IList<ushort> Values) => Strm.WriteMulti(Values, WriteUInt16);
    /// <summary>
    /// Writes multiple Int32
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The Int32 values to write</param>
    public static void WriteMultiInt32(this Stream Strm, IList<int> Values) => Strm.WriteMulti(Values, WriteInt32);
    /// <summary>
    /// Writes multiple UInt32
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The UInt32 values to write</param>
    [CLSCompliant(false)]
    public static void WriteMultiUInt32(this Stream Strm, IList<uint> Values) => Strm.WriteMulti(Values, WriteUInt32);
    /// <summary>
    /// Writes multiple Int64
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The Int64 values to write</param>
    public static void WriteMultiInt64(this Stream Strm, IList<long> Values) => Strm.WriteMulti(Values, WriteInt64);
    /// <summary>
    /// Writes multiple UInt64
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The UInt64 values to write</param>
    [CLSCompliant(false)]
    public static void WriteMultiUInt64(this Stream Strm, IList<ulong> Values) => Strm.WriteMulti(Values, WriteUInt64);
    /// <summary>
    /// Writes multiple Half
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The Half values to write</param>
    public static void WriteMultiHalf(this Stream Strm, IList<Half> Values) => Strm.WriteMulti(Values, WriteHalf);
    /// <summary>
    /// Writes multiple Single
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The Single values to write</param>
    public static void WriteMultiSingle(this Stream Strm, IList<float> Values) => Strm.WriteMulti(Values, WriteSingle);
    /// <summary>
    /// Writes multiple Double
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Values">The Double values to write</param>
    public static void WriteMultiDouble(this Stream Strm, IList<double> Values) => Strm.WriteMulti(Values, WriteDouble);

    /// <summary>
    /// Writes a value that has varying length.
    /// </summary>
    /// <param name="Strm"></param>
    /// <param name="value"></param>
    public static void WriteVariableLength(this Stream Strm, int value)
    {
        int vbck = value;
        int buffer;
        byte last;
        buffer = value & 0x7F;
        while ((value >>= 7) > 0)
        {
            buffer <<= 8;
            buffer |= 0x80;
            buffer += (value & 0x7F);
        }
        do
        {
            last = unchecked((byte)buffer);
            Strm.WriteByte(last);
            buffer >>= 8;
        } while (unchecked((byte)(buffer & 0x80)) > 0);
        if ((last & 0x80) > 0)
            Strm.WriteByte(unchecked((byte)buffer));
    }

    /// <summary>
    /// Writes a string to the Stream that can be NULL terminated.<para/>This method is faster for single byte encodings
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="String">The string to write</param>
    /// <param name="Enc">The encoding to write the string in</param>
    /// <param name="Terminator">The terminator byte. Set to NULL to dsiable termination (for MAGICs and whatnot)</param>
    public static void WriteString(this Stream Strm, string String, Encoding Enc, byte? Terminator = 0x00)
    {
        byte[] Write = Enc.GetBytes(String);
        Strm.Write(Write, 0, Write.Length);
        if (Terminator is not null)
            Strm.WriteByte(Terminator.Value);
    }
    /// <summary>
    /// Writes a string to the Stream that can be NULL terminated.<para/>This method is for multi-byte encodings
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="String">The string to write</param>
    /// <param name="Enc">The encoding to write the string in</param>
    /// <param name="ByteCount">The stride of the encoding. Variable Length not supported</param>
    /// <param name="Terminator">The byte to use for the Terminator. Set to NULL to dsiable termination (for MAGICs and whatnot)</param>
    public static void WriteString(this Stream Strm, string String, Encoding Enc, int ByteCount, byte? Terminator = 0x00)
    {
        byte[] Write = Enc.GetBytes(String);
        Strm.Write(Write, 0, Write.Length);

        if (Terminator is not null)
        {
            byte[] Term = new byte[ByteCount];
            for (int i = 0; i < ByteCount; i++)
                Term[i] = Terminator.Value;
            Strm.Write(Term, 0, Term.Length);
        }
    }

    /// <summary>
    /// Writes a "SHIFT-JIS" string to the Stream that's be NULL terminated.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="String">the string to write</param>
    /// <param name="Terminator">The byte to use for the Terminator. Set to NULL to dsiable termination (for MAGICs and whatnot)</param>
    public static void WriteStringJIS(this Stream Strm, string String, byte? Terminator = 0x00) => Strm.WriteString(String, ShiftJIS, Terminator);

    //====================================================================================================

    /// <summary>
    /// Reads out all the bytes from a stream.
    /// </summary>
    /// <param name="Strm">The stream to read out.<para/>If this is a MemoryStream, <see cref="MemoryStream.ToArray"/> is called instead.</param>
    /// <returns>A byte[] of the streams contents.</returns>
    public static byte[] ToArray(this Stream Strm)
    {
        if (Strm is MemoryStream Mstrm)
            return Mstrm.ToArray();

        using MemoryStream memoryStream = new();
        Strm.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Peek the next byte
    /// </summary>
    /// <param name="Strm">The Stream to peek</param>
    /// <returns>The next byte to be read</returns>
    [DebuggerStepThrough]
    public static byte PeekByte(this Stream Strm)
    {
        byte val = (byte)Strm.ReadByte();
        Strm.Position--;
        return val;
    }

    /// <summary>
    /// Peeks the value at the position of the stream.<para/>The stream position is not advanced
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="Strm">The stream to read</param>
    /// <param name="Reader">The function that determines what gets read</param>
    /// <returns>the value at the position of the stream.</returns>
    public static T Peek<T>(this Stream Strm, Func<Stream, T> Reader) where T : struct
    {
        long start = Strm.Position;
        T v = Reader(Strm);
        Strm.Position = start;
        return v;
    }

    /// <summary>
    /// Creates a slice of a MemoryStream. This does make a copy of the data.
    /// </summary>
    /// <param name="Strm">The stream to slice from</param>
    /// <param name="StartPosition">The starting point to slice</param>
    /// <param name="Length">The length of the slice</param>
    /// <returns>a MemoryStream containing the data in the slice</returns>
    public static MemoryStream CreateStreamSlice(this Stream Strm, long StartPosition, long Length)
    {
        long PausePosition = Strm.Position;
        byte[] data = new byte[Length];
        Strm.Position = StartPosition;
        Strm.Read(data);
        Strm.Position = PausePosition;

        return new MemoryStream(data);
    }

    //====================================================================================================

    /// <summary>
    /// Adds padding to the current position in the provided stream
    /// </summary>
    /// <param name="Strm">The Stream to add padding to</param>
    /// <param name="Multiple">The byte multiple to pad to</param>
    /// <param name="Padding">The byte to use as padding</param>
    [DebuggerStepThrough]
    public static void PadTo(this Stream Strm, int Multiple, byte Padding = 0x00)
    {
        int NeededPadding = CalculatePaddingLength(Strm.Position, Multiple);
        Strm.Write(CollectionUtil.InitilizeArray(Padding, NeededPadding), 0, NeededPadding);
    }
    /// <summary>
    /// Adds padding to the current position in the provided stream
    /// </summary>
    /// <param name="Strm">The Stream to add padding to</param>
    /// <param name="Multiple">The byte multiple to pad to</param>
    /// <param name="PadString">The string to use as padding</param>
    [DebuggerStepThrough]
    public static void PadTo(this Stream Strm, int Multiple, string PadString)
    {
        int NeededPadding = CalculatePaddingLength(Strm.Position, Multiple);
        if (PadString.Length < NeededPadding)
            throw new ArgumentException($"The {nameof(PadString)} \"{PadString}\" is too short. ({NeededPadding}/{PadString.Length})", nameof(PadString));

        string UsedPadding = PadString[..NeededPadding];
        Strm.WriteString(UsedPadding, Encoding.ASCII, null);
    }

    /// <summary>
    /// Calculates how much padding is actually needed
    /// </summary>
    /// <param name="StrmPos">The number to calculate padding from</param>
    /// <param name="Multiple">The number to calculate padding to</param>
    /// <returns>The number to calculated padding</returns>
    public static int CalculatePaddingLength(long StrmPos, int Multiple)
    {
        int result = (int)StrmPos % Multiple;
        if (result == 0)
            return 0;
        return Multiple - result;
    }


    /// <summary>
    /// Writes filler data to the stream.<para/>Easily identifiable with 0xDD.
    /// </summary>
    /// <param name="Strm">The Stream to write to</param>
    /// <param name="ByteCount">The number of placeholder bytes to write</param>
    public static void WritePlaceholder(this Stream Strm, int ByteCount) => Strm.Write(CollectionUtil.InitilizeArray<byte>(0xDD, ByteCount));
    /// <summary>
    /// Writes multiple instances of filler data to the stream.Easily identifiable with 0xDD.
    /// </summary>
    /// <param name="Strm">The Stream to write to</param>
    /// <param name="ByteCount">The number of placeholder bytes to write</param>
    /// <param name="ElementCount">The number of elements to write placeholders for</param>
    public static void WritePlaceholderMulti(this Stream Strm, int ByteCount, int ElementCount) => Strm.WritePlaceholder(ByteCount * ElementCount);
}