using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hack.io.Interface;

/// <summary>
/// The interface indicating that this type can be Loaded and Saved
/// </summary>
public interface ILoadSaveFile
{
    /// <summary>
    /// Loads the format data off a <see cref="FileStream"/>, <see cref="MemoryStream"/>, or other <see cref="Stream"/> class.
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    public void Load(Stream Strm);
    /// <summary>
    /// Saves the format data to a <see cref="FileStream"/>, <see cref="MemoryStream"/>, or other <see cref="Stream"/> class.
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    public void Save(Stream Strm);
}

sealed partial class DocGen
{
    /// <summary>
    /// The file identifier
    /// </summary>
    public const string DOC_MAGIC = "";
}