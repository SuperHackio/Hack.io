using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Hack.io.Class;

/// <inheritdoc cref="ITypeVector"/>
public readonly struct TypeVector2<T> : ITypeVector, IEquatable<TypeVector2<T>>, IFormattable
    where T : unmanaged, INumber<T>
{
    /// <summary>
    /// The X Value of this vector
    /// </summary>
    public readonly T X { get => _values[0]; set => _values[0] = value; }
    /// <summary>
    /// The Y Value of this vector
    /// </summary>
    public readonly T Y { get => _values[1]; set => _values[1] = value; }

    private readonly T[] _values = new T[2];

    /// <summary>
    /// Creates a vector with the default values for X Y based on the type
    /// </summary>
    public TypeVector2() : this(default, default)
    {

    }
    /// <summary>
    /// Creates a vector using the first 2 values in the list
    /// </summary>
    /// <param name="data">The data array</param>
    public TypeVector2(IList<T> data) : this(data[0], data[1])
    {

    }
    /// <summary>
    /// Creates a vector with the specified values for X Y
    /// </summary>
    /// <param name="x">The X Value</param>
    /// <param name="y">The Y Value</param>
    public TypeVector2(T x, T y)
    {
        X = x;
        Y = y;
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(_values[0], _values[1]);
    /// <inheritdoc/>
    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TypeVector2<T> vec && X.Equals(vec.X) && Y.Equals(vec.Y);
    /// <inheritdoc/>
    public readonly bool Equals(TypeVector2<T> vec) => X.Equals(vec.X) && Y.Equals(vec.Y);
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(TypeVector2<T> left, TypeVector2<T> right) => left.Equals(right);
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(TypeVector2<T> left, TypeVector2<T> right) => !(left == right);

    /// <summary>Returns the string representation of the current instance using default formatting.</summary>
    /// <returns>The string representation of the current instance.</returns>
    /// <remarks>This method returns a string in which each element of the vector is formatted using the "G" (general) format string and the formatting conventions of the current thread culture. The "&lt;" and "&gt;" characters are used to begin and end the string, and the current culture's <see cref="NumberFormatInfo.NumberGroupSeparator" /> property followed by a space is used to separate each element.</remarks>
    public override readonly string ToString() => ToString("G", CultureInfo.CurrentCulture);
    /// <summary>Returns the string representation of the current instance using the specified format string to format individual elements.</summary>
    /// <param name="format">A standard or custom numeric format string that defines the format of individual elements.</param>
    /// <returns>The string representation of the current instance.</returns>
    /// <remarks>This method returns a string in which each element of the vector is formatted using <paramref name="format" /> and the current culture's formatting conventions. The "&lt;" and "&gt;" characters are used to begin and end the string, and the current culture's <see cref="NumberFormatInfo.NumberGroupSeparator" /> property followed by a space is used to separate each element.</remarks>
    /// <related type="Article" href="/dotnet/standard/base-types/standard-numeric-format-strings">Standard Numeric Format Strings</related>
    /// <related type="Article" href="/dotnet/standard/base-types/custom-numeric-format-strings">Custom Numeric Format Strings</related>
    public readonly string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format) => ToString(format, CultureInfo.CurrentCulture);
    /// <summary>Returns the string representation of the current instance using the specified format string to format individual elements and the specified format provider to define culture-specific formatting.</summary>
    /// <param name="format">A standard or custom numeric format string that defines the format of individual elements.</param>
    /// <param name="formatProvider">A format provider that supplies culture-specific formatting information.</param>
    /// <returns>The string representation of the current instance.</returns>
    /// <remarks>This method returns a string in which each element of the vector is formatted using <paramref name="format" /> and <paramref name="formatProvider" />. The "&lt;" and "&gt;" characters are used to begin and end the string, and the format provider's <see cref="NumberFormatInfo.NumberGroupSeparator" /> property followed by a space is used to separate each element.</remarks>
    /// <related type="Article" href="/dotnet/standard/base-types/standard-numeric-format-strings">Standard Numeric Format Strings</related>
    /// <related type="Article" href="/dotnet/standard/base-types/custom-numeric-format-strings">Custom Numeric Format Strings</related>
    public readonly string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format, IFormatProvider? formatProvider)
    {
        string separator = NumberFormatInfo.GetInstance(formatProvider).NumberGroupSeparator;

        return $"<{X.ToString(format, formatProvider)}{separator} {Y.ToString(format, formatProvider)}>";
    }

    /// <summary>
    /// Gets the internal value array
    /// </summary>
    /// <param name="vec"></param>
    public static implicit operator T[](TypeVector2<T> vec) => vec._values;
}

/// <inheritdoc cref="ITypeVector"/>
public readonly struct TypeVector3<T> : ITypeVector, IEquatable<TypeVector3<T>>, IFormattable
    where T : unmanaged, INumber<T>
{
    /// <summary>
    /// The X Value of this vector
    /// </summary>
    public readonly T X { get => _values[0]; set => _values[0] = value; }
    /// <summary>
    /// The Y Value of this vector
    /// </summary>
    public readonly T Y { get => _values[1]; set => _values[1] = value; }
    /// <summary>
    /// The Z Value of this vector
    /// </summary>
    public readonly T Z { get => _values[2]; set => _values[2] = value; }

    private readonly T[] _values = new T[3];

    /// <summary>
    /// Creates a vector with the default values for X Y Z based on the type
    /// </summary>
    public TypeVector3() : this(default, default, default)
    {

    }
    /// <summary>
    /// Creates a vector using the first 3 values in the list
    /// </summary>
    /// <param name="data">The data array</param>
    public TypeVector3(IList<T> data) : this(data[0], data[1], data[2])
    {

    }
    /// <summary>
    /// Creates a vector with the specified values for X Y Z
    /// </summary>
    /// <param name="x">The X Value</param>
    /// <param name="y">The Y Value</param>
    /// <param name="z">The Z Value</param>
    public TypeVector3(T x, T y, T z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(_values[0], _values[1], _values[2]);
    /// <inheritdoc/>
    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TypeVector3<T> vec && X.Equals(vec.X) && Y.Equals(vec.Y) && Z.Equals(vec.Z);
    /// <inheritdoc/>
    public readonly bool Equals(TypeVector3<T> vec) => X.Equals(vec.X) && Y.Equals(vec.Y) && Z.Equals(vec.Z);
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(TypeVector3<T> left, TypeVector3<T> right) => left.Equals(right);
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(TypeVector3<T> left, TypeVector3<T> right) => !(left == right);

    /// <summary>Returns the string representation of the current instance using default formatting.</summary>
    /// <returns>The string representation of the current instance.</returns>
    /// <remarks>This method returns a string in which each element of the vector is formatted using the "G" (general) format string and the formatting conventions of the current thread culture. The "&lt;" and "&gt;" characters are used to begin and end the string, and the current culture's <see cref="NumberFormatInfo.NumberGroupSeparator" /> property followed by a space is used to separate each element.</remarks>
    public override readonly string ToString() => ToString("G", CultureInfo.CurrentCulture);
    /// <summary>Returns the string representation of the current instance using the specified format string to format individual elements.</summary>
    /// <param name="format">A standard or custom numeric format string that defines the format of individual elements.</param>
    /// <returns>The string representation of the current instance.</returns>
    /// <remarks>This method returns a string in which each element of the vector is formatted using <paramref name="format" /> and the current culture's formatting conventions. The "&lt;" and "&gt;" characters are used to begin and end the string, and the current culture's <see cref="NumberFormatInfo.NumberGroupSeparator" /> property followed by a space is used to separate each element.</remarks>
    /// <related type="Article" href="/dotnet/standard/base-types/standard-numeric-format-strings">Standard Numeric Format Strings</related>
    /// <related type="Article" href="/dotnet/standard/base-types/custom-numeric-format-strings">Custom Numeric Format Strings</related>
    public readonly string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format) => ToString(format, CultureInfo.CurrentCulture);
    /// <summary>Returns the string representation of the current instance using the specified format string to format individual elements and the specified format provider to define culture-specific formatting.</summary>
    /// <param name="format">A standard or custom numeric format string that defines the format of individual elements.</param>
    /// <param name="formatProvider">A format provider that supplies culture-specific formatting information.</param>
    /// <returns>The string representation of the current instance.</returns>
    /// <remarks>This method returns a string in which each element of the vector is formatted using <paramref name="format" /> and <paramref name="formatProvider" />. The "&lt;" and "&gt;" characters are used to begin and end the string, and the format provider's <see cref="NumberFormatInfo.NumberGroupSeparator" /> property followed by a space is used to separate each element.</remarks>
    /// <related type="Article" href="/dotnet/standard/base-types/standard-numeric-format-strings">Standard Numeric Format Strings</related>
    /// <related type="Article" href="/dotnet/standard/base-types/custom-numeric-format-strings">Custom Numeric Format Strings</related>
    public readonly string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format, IFormatProvider? formatProvider)
    {
        string separator = NumberFormatInfo.GetInstance(formatProvider).NumberGroupSeparator;

        return $"<{X.ToString(format, formatProvider)}{separator} {Y.ToString(format, formatProvider)}{separator} {Z.ToString(format, formatProvider)}>";
    }

    /// <summary>
    /// Gets the internal value array
    /// </summary>
    /// <param name="vec"></param>
    public static implicit operator T[](TypeVector3<T> vec) => vec._values;
}

/// <inheritdoc cref="ITypeVector"/>
public readonly struct TypeVector4<T> : ITypeVector, IEquatable<TypeVector4<T>>, IFormattable
    where T : unmanaged, INumber<T>
{
    /// <summary>
    /// The X Value of this vector
    /// </summary>
    public readonly T X { get => _values[0]; set => _values[0] = value; }
    /// <summary>
    /// The Y Value of this vector
    /// </summary>
    public readonly T Y { get => _values[1]; set => _values[1] = value; }
    /// <summary>
    /// The Z Value of this vector
    /// </summary>
    public readonly T Z { get => _values[2]; set => _values[2] = value; }
    /// <summary>
    /// The W Value of this vector
    /// </summary>
    public readonly T W { get => _values[3]; set => _values[3] = value; }

    private readonly T[] _values = new T[4];

    /// <summary>
    /// Creates a vector with the default values for X Y Z W based on the type
    /// </summary>
    public TypeVector4() : this(default, default, default, default)
    {

    }
    /// <summary>
    /// Creates a vector using the first 4 values in the list
    /// </summary>
    /// <param name="data">The data array</param>
    public TypeVector4(IList<T> data) : this(data[0], data[1], data[2], data[3])
    {

    }
    /// <summary>
    /// Creates a vector with the specified values for X Y Z W
    /// </summary>
    /// <param name="x">The X Value</param>
    /// <param name="y">The Y Value</param>
    /// <param name="z">The Z Value</param>
    /// <param name="w">The W Value</param>
    public TypeVector4(T x, T y, T z, T w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(_values[0], _values[1], _values[2], _values[3]);
    /// <inheritdoc/>
    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TypeVector4<T> vec && X.Equals(vec.X) && Y.Equals(vec.Y) && Z.Equals(vec.Z) && W.Equals(vec.W);
    /// <inheritdoc/>
    public readonly bool Equals(TypeVector4<T> vec) => X.Equals(vec.X) && Y.Equals(vec.Y) && Z.Equals(vec.Z) && W.Equals(vec.W);
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(TypeVector4<T> left, TypeVector4<T> right) => left.Equals(right);
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(TypeVector4<T> left, TypeVector4<T> right) => !(left == right);

    /// <summary>Returns the string representation of the current instance using default formatting.</summary>
    /// <returns>The string representation of the current instance.</returns>
    /// <remarks>This method returns a string in which each element of the vector is formatted using the "G" (general) format string and the formatting conventions of the current thread culture. The "&lt;" and "&gt;" characters are used to begin and end the string, and the current culture's <see cref="NumberFormatInfo.NumberGroupSeparator" /> property followed by a space is used to separate each element.</remarks>
    public override readonly string ToString() => ToString("G", CultureInfo.CurrentCulture);
    /// <summary>Returns the string representation of the current instance using the specified format string to format individual elements.</summary>
    /// <param name="format">A standard or custom numeric format string that defines the format of individual elements.</param>
    /// <returns>The string representation of the current instance.</returns>
    /// <remarks>This method returns a string in which each element of the vector is formatted using <paramref name="format" /> and the current culture's formatting conventions. The "&lt;" and "&gt;" characters are used to begin and end the string, and the current culture's <see cref="NumberFormatInfo.NumberGroupSeparator" /> property followed by a space is used to separate each element.</remarks>
    /// <related type="Article" href="/dotnet/standard/base-types/standard-numeric-format-strings">Standard Numeric Format Strings</related>
    /// <related type="Article" href="/dotnet/standard/base-types/custom-numeric-format-strings">Custom Numeric Format Strings</related>
    public readonly string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format) => ToString(format, CultureInfo.CurrentCulture);
    /// <summary>Returns the string representation of the current instance using the specified format string to format individual elements and the specified format provider to define culture-specific formatting.</summary>
    /// <param name="format">A standard or custom numeric format string that defines the format of individual elements.</param>
    /// <param name="formatProvider">A format provider that supplies culture-specific formatting information.</param>
    /// <returns>The string representation of the current instance.</returns>
    /// <remarks>This method returns a string in which each element of the vector is formatted using <paramref name="format" /> and <paramref name="formatProvider" />. The "&lt;" and "&gt;" characters are used to begin and end the string, and the format provider's <see cref="NumberFormatInfo.NumberGroupSeparator" /> property followed by a space is used to separate each element.</remarks>
    /// <related type="Article" href="/dotnet/standard/base-types/standard-numeric-format-strings">Standard Numeric Format Strings</related>
    /// <related type="Article" href="/dotnet/standard/base-types/custom-numeric-format-strings">Custom Numeric Format Strings</related>
    public readonly string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format, IFormatProvider? formatProvider)
    {
        string separator = NumberFormatInfo.GetInstance(formatProvider).NumberGroupSeparator;

        return $"<{X.ToString(format, formatProvider)}{separator} {Y.ToString(format, formatProvider)}{separator} {Z.ToString(format, formatProvider)}{separator} {W.ToString(format, formatProvider)}>";
    }

    /// <summary>
    /// Gets the internal value array
    /// </summary>
    /// <param name="vec"></param>
    public static implicit operator T[](TypeVector4<T> vec) => vec._values;
}

/// <summary>
/// TypeVectors are vectors that can hold a fixed amount of data values of varying types.
/// </summary>
public interface ITypeVector
{

}