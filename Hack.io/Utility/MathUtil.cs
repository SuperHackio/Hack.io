namespace Hack.io.Utility;

/// <summary>
/// A static class for math helper functions
/// </summary>
public static class MathUtil
{
    /// <summary>
    /// Clamps a value to the specified minimum and maximum value
    /// </summary>
    /// <typeparam name="T">IComparable</typeparam>
    /// <param name="val">The value to clamp</param>
    /// <param name="min">Minimum value to clamp to</param>
    /// <param name="max">Maximum value to clamp to</param>
    /// <returns>Max or Min, depending on Val</returns>
    public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
    {
        if (val.CompareTo(min) < 0) return min;
        else if (val.CompareTo(max) > 0) return max;
        else return val;
    }
    
    /// <summary>
    /// Lerp 2 bytes via a time
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <param name="t"></param>
    /// <returns></returns>
    public static byte Lerp(byte min, byte max, float t) => (byte)(((1 - t) * min) + (t * max)).Clamp(0, 255);
    /// <summary>
    /// Lerp 2 floats via a time
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <param name="t"></param>
    /// <returns></returns>
    public static float Lerp(float min, float max, float t) => ((1 - t) * min) + (t * max);
    /// <summary>
    /// Lerp 2 floats via a time
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <param name="t"></param>
    /// <returns></returns>
    public static double Lerp(double min, double max, double t) => ((1 - t) * min) + (t * max);

    /// <summary>
    /// Gets the percent value of a given number. Usually used by Background Workers
    /// </summary>
    /// <param name="Current"></param>
    /// <param name="Max"></param>
    /// <param name="OutOf"></param>
    /// <returns></returns>
    public static float GetPercentOf(float Current, float Max, float OutOf = 100f) => Current / Max * OutOf;

    /// <summary>
    /// Scales a number between W and X to be between Y and Z
    /// </summary>
    /// <param name="valueIn"></param>
    /// <param name="baseMin"></param>
    /// <param name="baseMax"></param>
    /// <param name="limitMin"></param>
    /// <param name="limitMax"></param>
    /// <returns></returns>
    public static double Scale(double valueIn, double baseMin, double baseMax, double limitMin, double limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;

    /// <summary>
    /// Converts a Radian to a Degree
    /// </summary>
    /// <param name="angle">Radian angle</param>
    /// <returns>Degree Angle</returns>
    public static float RadianToDegree(this float angle)
    {
        return (float)(angle * (180.0 / Math.PI));
    }
    /// <summary>
    /// Converts a Degree to a Radian
    /// </summary>
    /// <param name="angle">Degree Angle</param>
    /// <returns>Radian Angle</returns>
    public static float DegreeToRadian(this float angle)
    {
        return (float)(Math.PI * angle / 180.0);
    }

    /// <summary>
    /// Returns the decimal part of a number
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    public static double GetDecimal(double number) => (int)((decimal)number % 1 * 100);

    /// <summary>
    /// Returns the Rightmost bit that is set
    /// </summary>
    /// <param name="value">the value to look at</param>
    /// <returns>an integer with the rightmost bit set</returns>
    public static int LeastSigBitSet(int value)
    {
        return (value & -value);
    }

    /// <summary>
    /// Returns the Leftmost bit that is set
    /// </summary>
    /// <param name="value">the value to look at</param>
    /// <returns>an integer with the leftmost bit set</returns>
    public static int MostSigBitSet(int value)
    {
        value |= (value >> 1);
        value |= (value >> 2);
        value |= (value >> 4);
        value |= (value >> 8);
        value |= (value >> 16);

        return (value & ~(value >> 1));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public static int GetLeadingZeros(int i)
    {
        if (i == 0)
            return 32;

        const int numIntBits = sizeof(int) * 8; //compile time constant
                                                //do the smearing
        i |= i >> 1;
        i |= i >> 2;
        i |= i >> 4;
        i |= i >> 8;
        i |= i >> 16;
        //count the ones
        i -= i >> 1 & 0x55555555;
        i = (i >> 2 & 0x33333333) + (i & 0x33333333);
        i = (i >> 4) + i & 0x0f0f0f0f;
        i += i >> 8;
        i += i >> 16;
        return numIntBits - (i & 0x0000003f); //subtract # of 1s from 32
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public static int GetTrailingZeros(int i)
    {
        if (i == 0)
            return 32;

        // HD, Figure 5-14
        int y;
        int n = 31;
        y = i << 16; if (y != 0) { n -= 16; i = y; }
        y = i << 8; if (y != 0) { n -= 8; i = y; }
        y = i << 4; if (y != 0) { n -= 4; i = y; }
        y = i << 2; if (y != 0) { n -= 2; i = y; }
        return n - (int)((uint)(i << 1) >> 31);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public static int GetMaxBits(int i) => 32 - GetLeadingZeros(i);
}
