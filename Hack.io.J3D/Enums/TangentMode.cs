namespace Hack.io.J3D;

/// <summary>
/// J3D Animation Tangent Modes
/// </summary>
public enum TangentMode : short
{
    /// <summary>
    /// One tangent value is stored, used for both the incoming and outgoing tangents
    /// </summary>
    SINGLE = 0x00,
    /// <summary>
    /// Two tangent values are stored, the incoming and outgoing tangents, respectively
    /// </summary>
    DOUBLE = 0x01
}
