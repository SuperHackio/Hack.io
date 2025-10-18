using System.Diagnostics;
using System.Text;

namespace Hack.io.Utility;

/// <summary>
/// A static class for File helper functions
/// </summary>
public static class FileUtil
{
    /// <summary>
    /// Check if a file cannot be opened.
    /// </summary>
    /// <param name="file">File to check for</param>
    /// <returns>TRUE if the file cannot be accessed</returns>
    [DebuggerStepThrough]
    public static bool IsFileLocked(this FileInfo file)
    {
        FileStream? stream = null;

        try
        {
            stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch (IOException)
        {
            return true;
        }
        finally
        {
            stream?.Close();
        }

        return false; //File not locked by the system
    }

    /// <summary>
    /// Creates a directory if it doesn't exist
    /// </summary>
    /// <param name="DirPath">The full path to the desired directory</param>
    [DebuggerStepThrough]
    public static void CreateDirectoryIfNotExist(string DirPath)
    {
        if (!Directory.Exists(DirPath))
            Directory.CreateDirectory(DirPath);
    }

    /// <summary>
    /// Loads a file to the disk.<para/>This exists so I don't have to keep writing FileStream Ctors
    /// </summary>
    /// <param name="FilePath">The system path to read from</param>
    /// <param name="Action">The method used to read the file</param>
    /// <param name="Access">The type of access to create for this file</param>
    public static void LoadFile(string FilePath, Action<Stream> Action, FileAccess Access = FileAccess.ReadWrite) => RunForFileStream(FilePath, FileMode.Open, Access, Action);
    /// <summary>
    /// Loads a file to the disk.<para/>This exists so I don't have to keep writing FileStream Ctors
    /// </summary>
    /// <param name="FilePath">The system path to read from</param>
    /// <param name="Function">The method used to read the file</param>
    /// <param name="Access">The type of access to create for this file</param>
    public static TResult LoadFile<TResult>(string FilePath, Func<Stream, TResult> Function, FileAccess Access = FileAccess.ReadWrite) => RunForFileStream(FilePath, FileMode.Open, Access, Function);

    /// <summary>
    /// Saves a file to the disk.<para/>This exists so I don't have to keep writing FileStream save functions.
    /// </summary>
    /// <param name="FilePath">The system path to write to</param>
    /// <param name="Action">The method used to save the file</param>
    /// <param name="Access">The type of access to create for this file</param>
    public static void SaveFile(string FilePath, Action<Stream> Action, FileAccess Access = FileAccess.ReadWrite) => RunForFileStream(FilePath, FileMode.Create, Access, Action);
    /// <summary>
    /// Saves a file to the disk.<para/>This exists so I don't have to keep writing FileStream save functions.
    /// </summary>
    /// <param name="FilePath">The system path to write to</param>
    /// <param name="Function">The method used to save the file</param>
    /// <param name="Access">The type of access to create for this file</param>
    public static TResult SaveFile<TResult>(string FilePath, Func<Stream, TResult> Function, FileAccess Access = FileAccess.ReadWrite) => RunForFileStream(FilePath, FileMode.Create, Access, Function);

    /// <summary>
    /// Executes a function using a FileStream from FilePath
    /// </summary>
    /// <param name="FilePath">The system path to write to</param>
    /// <param name="Mode">The mode to access the file in</param>
    /// <param name="Access">The type of access to create for this file</param>
    /// <param name="Action">The method used on the FileStream</param>
    public static void RunForFileStream(string FilePath, FileMode Mode, FileAccess Access, Action<Stream> Action)
    {
        using FileStream fs = new(FilePath, Mode, Access);
        Action(fs);
        fs.Close();
    }
    /// <summary>
    /// Executes a function using a FileStream from FilePath
    /// </summary>
    /// <param name="FilePath">The system path to write to</param>
    /// <param name="Mode">The mode to access the file in</param>
    /// <param name="Access">The type of access to create for this file</param>
    /// <param name="Function">The method used on the FileStream</param>
    public static TResult RunForFileStream<TResult>(string FilePath, FileMode Mode, FileAccess Access, Func<Stream, TResult> Function)
    {
        using FileStream fs = new(FilePath, Mode, Access);
        TResult result = Function(fs);
        fs.Close();
        return result;
    }

    /// <summary>
    /// Executes a function on all the bytes of a given file.
    /// </summary>
    /// <param name="FilePath">The system path to use to</param>
    /// <param name="Function">The function to run on all the bytes</param>
    /// <returns>The processed bytes</returns>
    public static byte[] RunForFileBytes(string FilePath, Func<byte[], byte[]> Function) => Function(File.ReadAllBytes(FilePath));

    /// <summary>
    /// throws an exception if the current stream position does not contain the requested magic
    /// </summary>
    /// <param name="Strm">The stream to check</param>
    /// <param name="Magic">The magic to check for</param>
    /// <exception cref="BadImageFormatException"></exception>
    public static void ExceptionOnBadMagic(Stream Strm, ReadOnlySpan<byte> Magic)
    {
        if (!Strm.IsMagicMatch(Magic))
            throw new BadImageFormatException($"Invalid Magic. Expected \"{Magic.ToString()}\"");
    }
    /// <inheritdoc cref="ExceptionOnBadMagic(Stream, ReadOnlySpan{byte})" />
    public static void ExceptionOnBadMagic(Stream Strm, ReadOnlySpan<char> Magic)
    {
        if (!Strm.IsMagicMatch(Magic))
            throw new BadImageFormatException($"Invalid Magic. Expected \"{Magic}\"");
    }
    /// <summary>
    /// throws an exception if the current stream position does not contain the requested magic
    /// </summary>
    /// <param name="Strm">The stream to check</param>
    /// <param name="Magic">The magic to check for</param>
    /// <param name="Enc">The encoding to read the stream with</param>
    /// <exception cref="BadImageFormatException"></exception>
    public static void ExceptionOnBadMagic(Stream Strm, ReadOnlySpan<char> Magic, Encoding Enc)
    {
        if (!Strm.IsMagicMatch(Magic, Enc))
            throw new BadImageFormatException($"Invalid Magic. Expected \"{Magic}\"");
    }

    /// <summary>
    /// throws an exception if the current stream position does not contain the requested magic
    /// </summary>
    /// <param name="Strm">The stream to check</param>
    /// <param name="Magic">The magic to check for</param>
    /// <param name="BothEndians">Whether to check for both endians</param>
    /// <exception cref="BadImageFormatException"></exception>
    public static void ExceptionOnBadMagic(Stream Strm, ReadOnlySpan<byte> Magic, bool BothEndians = false)
    {
        if (!Strm.IsMagicMatch(Magic, BothEndians))
            throw new BadImageFormatException($"Invalid Magic. Expected \"{Magic.ToString()}\"");
    }
    /// <inheritdoc cref="ExceptionOnBadMagic(Stream, ReadOnlySpan{byte}, bool)" />
    public static void ExceptionOnBadMagic(Stream Strm, ReadOnlySpan<char> Magic, bool BothEndians = false)
    {
        if (!Strm.IsMagicMatch(Magic, BothEndians))
            throw new BadImageFormatException($"Invalid Magic. Expected \"{Magic.ToString()}\"");
    }
    /// <summary>
    /// throws an exception if the current stream position does not contain the requested magic
    /// </summary>
    /// <param name="Strm">The stream to check</param>
    /// <param name="Magic">The magic to check for</param>
    /// <param name="Enc">The encoding to read the stream with</param>
    /// <param name="AlternativeMagic">The alternative magic to check for</param>
    /// <exception cref="BadImageFormatException"></exception>
    public static void ExceptionOnBadMagic(Stream Strm, ReadOnlySpan<char> Magic, Encoding Enc, ReadOnlySpan<char> AlternativeMagic)
    {
        if (!Strm.IsMagicMatch(Magic, Enc) && !Strm.IsMagicMatch(AlternativeMagic, Enc))
            throw new BadImageFormatException($"Invalid Magic. Expected \"{Magic}\" or \"{AlternativeMagic}\"");
    }

    /// <summary>
    /// Tries to read a file from the disk using one of the provided decompressors.
    /// </summary>
    /// <param name="FilePath">The system path to read from</param>
    /// <param name="Action">The method used to read the file</param>
    /// <param name="DecompressOptions">An array of decoding options to attempt</param>
    /// <returns>-1 if none of the decompressors work. Otherwise will return the index of the decompressor that was used</returns>
    public static int LoadFileWithDecompression(string FilePath, Action<Stream> Action, params (Func<Stream, bool> CheckFunc, Func<byte[], byte[]> DecodeFunction)[] DecompressOptions)
    {
        using FileStream fs = new(FilePath, FileMode.Open);

        byte[] d = new byte[fs.Length];
        d = fs.ToArray();
        byte[]? Result = null;
        for (int i = 0; i < DecompressOptions.Length; i++)
        {
            fs.Position = 0;
            if (DecompressOptions[i].CheckFunc(fs))
            {
                Result = DecompressOptions[i].DecodeFunction(d);
                using MemoryStream ms = new(Result);
                Action(ms);
                return i;
            }
        }
        return -1;
    }
    /// <summary>
    /// Tries to get the decompressed bytes of a file from the disk using one of the provided decompressors.
    /// </summary>
    /// <param name="FilePath">The system path to read from</param>
    /// <param name="DecompressOptions">An array of decoding options to attempt</param>
    /// <returns>null if none of the decompressors work. Otherwise will return the decompressed bytes</returns>
    public static byte[]? ReadWithDecompression(string FilePath, params (Func<Stream, bool> CheckFunc, Func<byte[], byte[]> DecodeFunction)[] DecompressOptions)
    {
        byte[] d = File.ReadAllBytes(FilePath);
        MemoryStream fs = new(d,false);
        for (int i = 0; i < DecompressOptions.Length; i++)
        {
            fs.Position = 0;
            if (DecompressOptions[i].CheckFunc(fs))
                return DecompressOptions[i].DecodeFunction(d);
        }
        return null;
    }
}
