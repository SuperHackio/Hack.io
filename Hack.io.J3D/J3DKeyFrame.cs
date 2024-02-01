namespace Hack.io.J3D;

/// <summary>
/// Represents a J3D Keyframe
/// </summary>
public class J3DKeyFrame
{
    /// <summary>
    /// The Time in the timeline that this keyframe is assigned to
    /// </summary>
    public ushort Time { get; set; }
    /// <summary>
    /// The Value to set to
    /// </summary>
    public float Value { get; set; }
    /// <summary>
    /// Tangents affect the interpolation between two consecutive keyframes
    /// </summary>
    public float IngoingTangent { get; set; }
    /// <summary>
    /// Tangents affect the interpolation between two consecutive keyframes
    /// </summary>
    public float OutgoingTangent { get; set; }

    public J3DKeyFrame(ushort time, float value, float ingoing = 0, float? outgoing = null)
    {
        Time = time;
        Value = value;
        IngoingTangent = ingoing;
        OutgoingTangent = outgoing ?? ingoing;
    }

    /// <summary>
    /// Converts the values based on a rotation multiplier
    /// </summary>
    /// <param name="RotationFraction">The byte in the file that determines the rotation fraction</param>
    /// <param name="Revert">Undo the conversion</param>
    public void ConvertRotation(byte RotationFraction, bool Revert = false)
    {
        float RotationMultiplier = (float)(Math.Pow(RotationFraction, 2) * (180.0 / 32768.0));
        Value = Revert ? Value / RotationMultiplier : Value * RotationMultiplier;
        IngoingTangent = Revert ? IngoingTangent / RotationMultiplier : IngoingTangent * RotationMultiplier;
        OutgoingTangent = Revert ? OutgoingTangent / RotationMultiplier : OutgoingTangent * RotationMultiplier;
    }

    /// <inheritdoc/>
    public override string ToString() => string.Format("Time: {0}, Value: {1}, Ingoing: {2}, Outgoing: {3}", Time, Value, IngoingTangent, OutgoingTangent);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is J3DKeyFrame frame &&
                Time == frame.Time &&
                Value == frame.Value &&
                IngoingTangent == frame.IngoingTangent &&
                OutgoingTangent == frame.OutgoingTangent;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Time, Value, IngoingTangent, OutgoingTangent);
}
