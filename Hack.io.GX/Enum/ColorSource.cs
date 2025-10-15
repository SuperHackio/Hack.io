namespace Hack.io.GX;

public enum GXColorSource : byte
{
    /// <summary>
    /// Source colors from the a single register
    /// </summary>
    SOURCE_REGISTER,
    /// <summary>
    /// Source colors from the model's Vertex Colors (Default)
    /// </summary>
    SOURCE_VERTEX,
};