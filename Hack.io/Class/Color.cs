using System.Numerics;

namespace Hack.io.Class;

/// <summary>
/// Represents a Color value of any binary number type
/// </summary>
/// <typeparam name="T"></typeparam>
public struct Color<T> where T : unmanaged, IBinaryNumber<T>
{
    /// <summary>
    /// Value of Red
    /// </summary>
    public T R;
    /// <summary>
    /// Value of Green
    /// </summary>
    public T G;
    /// <summary>
    /// Value of Blue
    /// </summary>
    public T B;
    /// <summary>
    /// Value of Alpha
    /// </summary>
    public T A;

    /// <summary>
    /// Creates the Default color of Transperant Black (0,0,0,0)
    /// </summary>
    public Color()
    {
        R = T.Zero;
        G = T.Zero;
        B = T.Zero;
        A = T.Zero;
    }

    /// <summary>
    /// Creates a color with Alpha set to Zerp
    /// </summary>
    /// <param name="r">Red component</param>
    /// <param name="g">Green component</param>
    /// <param name="b">Blue component</param>
    public Color(T r, T g, T b)
    {
        R = r;
        G = g;
        B = b;
        A = T.Zero;
    }

    /// <summary>
    /// Creates a color
    /// </summary>
    /// <param name="r">Red component</param>
    /// <param name="g">Green component</param>
    /// <param name="b">Blue component</param>
    /// <param name="a">Alpha component</param>
    public Color(T r, T g, T b, T a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    /// Reads a color from a Stream
    /// </summary>
    /// <param name="Strm">The stream to read from</param>
    /// <param name="Reader">The function to use to read each value<para/>For example, <see cref="Hack.io.Utility.StreamUtil.ReadUInt8(Stream)"/> will read one byte for R, G, B, and A. (4 bytes total)</param>
    /// <returns>The read color values</returns>
    public static Color<T> ReadColor(Stream Strm, Func<Stream, T> Reader)
    {
        var color = new Color<T>
        {
            R = Reader(Strm),
            G = Reader(Strm),
            B = Reader(Strm),
            A = Reader(Strm)
        };
        return color;
    }

    /// <summary>
    /// Writes a color to a Stream
    /// </summary>
    /// <param name="Strm">The stream to write to</param>
    /// <param name="Col">The color value to write to the stream</param>
    /// <param name="Writer">The function to use to write each value<para/>For example, <see cref="Hack.io.Utility.StreamUtil.WriteUInt8(Stream, byte)"/> will write one byte for R, G, B, and A. (4 bytes total)</param>
    public static void WriteColor(Stream Strm, Color<T> Col, Action<Stream, T> Writer)
    {
        Writer(Strm, Col.R);
        Writer(Strm, Col.G);
        Writer(Strm, Col.B);
        Writer(Strm, Col.A);
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is Color<T> color &&
               R == color.R &&
               G == color.G &&
               B == color.B &&
               A == color.A;

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(R, G, B, A);

    /// <inheritdoc/>
    public static bool operator ==(Color<T> left, Color<T> right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(Color<T> left, Color<T> right) => !(left == right);
}