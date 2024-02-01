namespace Hack.io.J3D;

/// <summary>
/// J3D Animation Loop modes
/// </summary>
public enum LoopMode
{
    /// <summary>
    /// Play Once then Stop.
    /// </summary>
    ONCE = 0,
    /// <summary>
    /// Play Once then Stop and reset to the first frame.
    /// </summary>
    ONCE_RESET = 1,
    /// <summary>
    /// Constantly play the animation.
    /// </summary>
    REPEAT = 2,
    /// <summary>
    /// Play the animation to the end. then reverse the animation and play to the start, then Stop.
    /// </summary>
    ONCE_MIRROR = 3,
    /// <summary>
    /// Play the animation to the end. then reverse the animation and play to the start, repeat.
    /// </summary>
    REPEAT_MIRROR = 4,
}