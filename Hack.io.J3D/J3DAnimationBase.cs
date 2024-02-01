using Hack.io.Utility;

namespace Hack.io.J3D;

/// <summary>
/// Base class for all J3D Animations
/// </summary>
public abstract class J3DAnimationBase<T> : List<T>
    where T : class, IJ3DAnimationContainer
{
    /// <summary>
    /// Loop Mode of the animation. See the <seealso cref="LoopMode"/> enum for values
    /// </summary>
    public LoopMode Loop { get; set; } = LoopMode.ONCE;
    /// <summary>
    /// Length of the animation in Frames. (Game Framerate = 1 second)
    /// </summary>
    public ushort Duration { get; set; }

    /// <inheritdoc/>
    public override string ToString() => $"[{Duration}, {Loop}]";

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is J3DAnimationBase<T> other &&
            Loop == other.Loop &&
            Duration == other.Duration &&
            this.SequenceEqual(other);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Loop, Duration, this as List<T>);
}