using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Hack.io.Util
{
    /// <summary>
    /// I need this because Nintendo had this strange idea that using Int24 is OK
    /// </summary>
    public struct Int24
    {
        /// <summary>
        /// 
        /// </summary>
        public const int MaxValue = 8388607;
        /// <summary>
        /// 
        /// </summary>
        public const int MinValue = -8388608;
        /// <summary>
        /// 
        /// </summary>
        public const int BitMask = -16777216;
        /// <summary>
        /// The value of this Int24 as an Int32
        /// </summary>
        public int Value { get; }
        /// <summary>
        /// Create a new Int24
        /// </summary>
        /// <param name="Value"></param>
        public Int24(int Value)
        {
            ValidateNumericRange(Value);
            this.Value = ApplyBitMask(Value);
        }

        private static void ValidateNumericRange(int value)
        {
            if (value > (MaxValue + 1) || value < MinValue)
                throw new OverflowException($"Value of {value} will not fit in a 24-bit signed integer");
        }
        private static int ApplyBitMask(int value) => (value & 0x00800000) > 0 ? value | BitMask : value & ~BitMask;
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => Value.ToString();
    }
    /// <summary>
    /// 
    /// </summary>
    public struct UInt24
    {
        private const uint MaxValue32 = 0x00ffffff;
        private const uint MinValue32 = 0x00000000;
        /// <summary>
        /// 
        /// </summary>
        public const uint BitMask = 0xff000000;
        /// <summary>
        /// The value of this Int24 as an Int32
        /// </summary>
        public uint Value { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public UInt24(uint value)
        {
            ValidateNumericRange(value);
            Value = ApplyBitMask(value);
        }
        private static void ValidateNumericRange(uint value)
        {
            if (value > MaxValue32)
                throw new OverflowException(string.Format("Value of {0} will not fit in a 24-bit unsigned integer", value));
        }
        private static uint ApplyBitMask(uint value) => (value & ~BitMask);
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => Value.ToString();
    }
}