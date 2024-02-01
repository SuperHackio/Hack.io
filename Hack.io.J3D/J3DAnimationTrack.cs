using Hack.io.Utility;

namespace Hack.io.J3D;

/// <summary>
/// Represents a J3D Animation Track
/// </summary>
public class J3DAnimationTrack : List<J3DKeyFrame>
{
    /// <summary>
    /// The type of animation tangent to use
    /// </summary>
    public TangentMode Tangent { get; set; }

    /// <inheritdoc/>
    public override string ToString() => $"{Tangent}: {Count}";

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is J3DAnimationTrack other && Tangent == other.Tangent && this.SequenceEqual(other);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Tangent, this as List<J3DKeyFrame>);
}

/// <summary>
/// Use of this interface indicates that the class is a holder of J3D Animation Tracks<para/>
/// This is pretty much just here for Generic Constraints
/// </summary>
public interface IJ3DAnimationContainer { }