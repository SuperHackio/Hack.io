using System.Text;
using Hack.io.Utility;

namespace Hack.io.J3D;

public static partial class Utility
{
    //Yes I know this is too long to ever get fully written but I don't care. This is useful in case something goes wrong with a file's save
    public const string PADSTRING = "Hack.io was the last code to touch this file";
    public const string MAGIC_J3D1 = "J3D1"; // Animation Formats
    public const string MAGIC_J3D2 = "J3D2"; // Model/Material Formats
    public const string SUBVERSION_1 = "SVR1"; // Animation Formats
    public const string SUBVERSION_2 = "SVR2"; // No idea if this is seen anywhere
    public const string SUBVERSION_3 = "SVR3"; // Model Formats

    public static bool ReadJ3DSubVersion(this Stream Strm, out int Version)
    {
        Version = -1;
        if (CheckMagic(SUBVERSION_1))
            Version = 1;
        else if (CheckMagic(SUBVERSION_2))
            Version = 2;
        else if (CheckMagic(SUBVERSION_3))
            Version = 3;

        Strm.Position += 0x10; // Skip over the data before returning
        return Version != -1;

        bool CheckMagic(string str)
        {
            string s = Strm.ReadString(str.Length, Encoding.ASCII);
            bool v = s.Equals(str);
            Strm.Position -= str.Length;
            return v;
        }
    }
    public static void WriteJ3DSubVersion(this Stream Strm, int Ver = 1)
    {
        Ver -= 1; // Adjust for indexing
        string[] verlist = [SUBVERSION_1, SUBVERSION_2, SUBVERSION_3];
        Strm.WriteString(verlist[Ver], Encoding.ASCII, null);
        Strm.Write(CollectionUtil.InitilizeArray<byte>(0xFF, 0x0C));
    }

    public static string[] ReadJ3DStringTable(this Stream Strm, int StringTableStartPosition)
    {
        Strm.Position = StringTableStartPosition;

        short Count = Strm.ReadInt16();
        Strm.Position += 0x02;

        string[] AllStrings = new string[Count];
        for (int i = 0; i < Count; i++)
        {
            Strm.Position += 0x02;
            short CurStringOffset = Strm.ReadInt16();
            long PausePosition = Strm.Position;
            Strm.Position = StringTableStartPosition + CurStringOffset;

            AllStrings[i] = Strm.ReadStringJIS();

            Strm.Position = PausePosition;
        }

        return AllStrings;
    }
    public static void WriteJ3DStringTable(this Stream Strm, IList<string> Values)
    {
        long start = Strm.Position;

        Strm.WriteInt16((short)Values.Count);
        Strm.Write([0xFF, 0xFF]);

        foreach (string st in Values)
        {
            Strm.WriteUInt16(HashString(st));
            Strm.Write(new byte[2]);
        }

        long curOffset = Strm.Position;
        for (int i = 0; i < Values.Count; i++)
        {
            Strm.Seek((int)(start + (6 + i * 4)), SeekOrigin.Begin);
            Strm.WriteInt16((short)(curOffset - start));
            Strm.Seek((int)curOffset, SeekOrigin.Begin);

            Strm.WriteStringJIS(Values[i]);

            curOffset = Strm.Position;
        }
    }
    public static uint CalculateJ3DStringTableSize(IList<string> Values)
    {
        int Size = 4 + (4 * Values.Count);
        for (int i = 0; i < Values.Count; i++)
            Size += StreamUtil.ShiftJIS.GetBytes(Values[i]).Length + 1; // Plus one for null terminator
        return (uint)Size;
    }

    /// <summary>
    /// Checks <paramref name="ChunkCount"/> amount of chunks to see if we can find the requested one
    /// </summary>
    /// <param name="Strm"></param>
    /// <param name="ChunkCount"></param>
    /// <param name="StartOffset"></param>
    /// <returns></returns>
    public static bool TryGetOffsetOfChunk(this Stream Strm, string Magic, uint ChunkCount, long StartOffset, out long Address)
    {
        Address = -1;
        Strm.Position = StartOffset;
        for (int i = 0; i < ChunkCount; i++)
        {
            long ChunkStart = Strm.Position;
            string ChunkMagic = Strm.ReadMagic(4);
            uint ChunkSize = Strm.ReadUInt32();

            if (ChunkMagic.Equals(Magic))
            {
                Address = ChunkStart;
                return true;
            }

            Strm.Position = ChunkStart + ChunkSize;
        }
        return false;
    }

    private static ushort HashString(string str)
    {
        ushort hash = 0;

        foreach (char c in str)
        {
            hash *= 3;
            hash += c;
        }

        return hash;
    }

    //=======================================================================================================================================

    public static J3DAnimationTrack ReadAnimationTrackFloat(this Stream Strm, float[] Data, float Scale)
    {
        ushort Count = Strm.ReadUInt16(),
            animIndex = Strm.ReadUInt16();
        TangentMode TangentType = Strm.ReadEnum<TangentMode, ushort>(StreamUtil.ReadUInt16);
        return TranslateTrackOnLoad(Data, Scale, Count, animIndex, TangentType);
    }
    public static J3DAnimationTrack ReadAnimationTrackInt16(this Stream Strm, short[] Data, float Scale)
    {
        ushort Count = Strm.ReadUInt16(),
            animIndex = Strm.ReadUInt16();
        TangentMode TangentType = Strm.ReadEnum<TangentMode, ushort>(StreamUtil.ReadUInt16);
        return TranslateTrackOnLoad(Data, Scale, Count, animIndex, TangentType);
    }

    public static J3DAnimationTrack TranslateTrackOnLoad(float[] Data, float Scale, ushort Count, ushort Index, TangentMode TangentType)
    {
        if (Count == 0)
            throw new InvalidOperationException("Zero length tracks not allowed!");

        J3DAnimationTrack Track = new() { Tangent = TangentType };
        if (Count == 1)
        {
            Track.Add(new(0, Data[Index] * Scale));
            return Track;
        }

        if (TangentType == TangentMode.SINGLE)
        {
            for (int i = Index; i < Index + 3 * Count; i += 3)
            {
                J3DKeyFrame Frame = new(
                    (ushort)Data[i + 0],
                    Data[i + 1] * Scale,
                    Data[i + 2] * Scale
                    );
                Track.Add(Frame);
            }
        }
        else if (TangentType == TangentMode.DOUBLE)
        {
            for (int i = Index; i < Index + 4 * Count; i += 4)
            {
                J3DKeyFrame Frame = new(
                    (ushort)Data[i + 0],
                    Data[i + 1] * Scale,
                    Data[i + 2] * Scale,
                    Data[i + 3] * Scale
                    );
                Track.Add(Frame);
            }
        }

        return Track;
    }
    public static J3DAnimationTrack TranslateTrackOnLoad(short[] Data, float Scale, ushort Count, ushort Index, TangentMode TangentType)
    {
        J3DAnimationTrack Track = new() { Tangent = TangentType };
        if (Count == 1)
        {
            Track.Add(new(0, Data[Index] * Scale));
            return Track;
        }

        if (TangentType == TangentMode.SINGLE)
        {
            for (int i = Index; i < Index + 3 * Count; i += 3)
            {
                J3DKeyFrame Frame = new(
                    (ushort)Data[i + 0],
                    Data[i + 1] * Scale,
                    Data[i + 2] * Scale
                    );
                Track.Add(Frame);
            }
        }
        else if (TangentType == TangentMode.DOUBLE)
        {
            for (int i = Index; i < Index + 4 * Count; i += 4)
            {
                J3DKeyFrame Frame = new(
                    (ushort)Data[i + 0],
                    Data[i + 1] * Scale,
                    Data[i + 2] * Scale,
                    Data[i + 3] * Scale
                    );
                Track.Add(Frame);
            }
        }
        else
            throw new InvalidOperationException("Zero length tracks not allowed!");

        return Track;
    }

    public static void WriteAnimationTrackFloat(this Stream Strm, J3DAnimationTrack Track, float Scale, ref List<float> Table)
    {
        List<float> Data = TranslateTrackOnSaveF(Scale, Track);
        int Index = Table.SubListIndex(0, Data);
        if (Index == -1)
        {
            Index = Table.Count;
            Table.AddRange(Data);
        }

        Strm.WriteUInt16((ushort)Track.Count);
        Strm.WriteUInt16((ushort)Index);
        Strm.WriteEnum<TangentMode, ushort>(Track.Tangent, StreamUtil.WriteUInt16);
    }
    public static void WriteAnimationTrackInt16(this Stream Strm, J3DAnimationTrack Track, float Scale, ref List<short> Table)
    {
        List<short> Data = TranslateTrackOnSaveS(Scale, Track);
        int Index = Table.SubListIndex(0, Data);
        if (Index == -1)
        {
            Index = Table.Count;
            Table.AddRange(Data);
        }

        Strm.WriteUInt16((ushort)Track.Count);
        Strm.WriteUInt16((ushort)Index);
        Strm.WriteEnum<TangentMode, ushort>(Track.Tangent, StreamUtil.WriteUInt16);
    }

    public static List<float> TranslateTrackOnSaveF(float Scale, J3DAnimationTrack Track)
    {
        if (Track.Count == 0)
            throw new InvalidOperationException("Zero length tracks not allowed!");
        List<float> Data = [];

        if (Track.Count == 1)
        {
            Data.Add(Track[0].Value / Scale);
            return Data;
        }

        for (int i = 0; i < Track.Count; i++)
        {
            Data.Add(Track[i].Time);
            Data.Add(Track[i].Value / Scale);
            Data.Add(Track[i].IngoingTangent / Scale);
            if (Track.Tangent == TangentMode.DOUBLE)
                Data.Add(Track[i].OutgoingTangent / Scale);
        }
        return Data;
    }
    public static List<short> TranslateTrackOnSaveS(float Scale, J3DAnimationTrack Track)
    {
        if (Track.Count == 0)
            throw new InvalidOperationException("Zero length tracks not allowed!");
        List<short> Data = [];

        if (Track.Count == 1)
        {
            Data.Add((short)(Track[0].Value / Scale));
            return Data;
        }

        for (int i = 0; i < Track.Count; i++)
        {
            Data.Add((short)Track[i].Time);
            Data.Add((short)(Track[i].Value / Scale));
            Data.Add((short)(Track[i].IngoingTangent / Scale));
            if (Track.Tangent == TangentMode.DOUBLE)
                Data.Add((short)(Track[i].OutgoingTangent / Scale));
        }
        return Data;
    }
}

public static partial class Utility
{
    /// <summary>
    /// Calculates a Linear slope between 2 keyframes
    /// </summary>
    /// <param name="FirstKey">The first keyframe (should be earlier in the timeline)</param>
    /// <param name="SecondKey">The second keyframe (should be later in the timeline, ideally the one right after the keyframe passed into <paramref name="FirstKey"/>)</param>
    /// <returns>a float value that can be put into FirstKey's Outgoing and SecondKey's Ingoing</returns>
    public static float CalculateLinearSlope(J3DKeyFrame FirstKey, J3DKeyFrame SecondKey) => (SecondKey.Value - FirstKey.Value) / (SecondKey.Time - FirstKey.Time);

    
    public static bool IsExistKeyframe(J3DAnimationTrack Track, ushort Time)
    {
        for (int i = 0; i < Track.Count; i++)
            if (Time == Track[i].Time)
                return true;
        return false;
    }

    public static ushort? GetNextKeyframeIndex(J3DAnimationTrack Track, ushort Time)
    {
        for (int i = 0; i < Track.Count; i++)
            if (Time < Track[i].Time)
                return (ushort)i;
        return null;
    }

    public static float GetValueAtFrame(J3DAnimationTrack Track, ushort Time)
    {
        if (Track.Count == 0)
            return 0;
        ushort? NextFrameId = GetNextKeyframeIndex(Track, Time);

        if (NextFrameId is null)
            return Track[^1].Value;
        if (NextFrameId == 0)
            return Track[0].Value;

        return GetHermiteInterpolation(Track[NextFrameId.Value-1], Track[NextFrameId.Value], Time);
    }


    public static void ReverseAnimation(J3DAnimationTrack Track, ushort TotalDuration)
    {
        if (Track.Count == 1)
            return; //Do not reverse tracks like this because they are static
        for (int i = 0; i < Track.Count; i++)
        {
            float ing = Track[i].IngoingTangent;
            float outg = Track[i].OutgoingTangent;
            CollectionUtil.SwapValues(ref ing, ref outg);
            Track[i] = new((ushort)(TotalDuration - Track[i].Time), Track[i].Value, ing, outg);
        }
        Track.Reverse();

        if (Track[0].Time > 0)
            Track.Insert(0, new(0, Track[0].Value));
    }

    //public static T CreateAnimationFromSlice<T, A>(T Source, ushort StartTime, ushort Duration)
    //    where T : J3DAnimationBase<A>, new()
    //    where A : class, IJ3DAnimationContainer
    //{
    //    T result = new() { Loop = Source.Loop, Duration = Duration };
    //    for (int i = 0; i < Source.Count; i++)
    //    {

    //    }
    //}


    public static J3DAnimationTrack CreateTrackSlice(J3DAnimationTrack Track, ushort StartTime, ushort Duration)
    {
        throw new NotImplementedException();

        //J3DAnimationTrack NewTrack = new() { Tangent = Track.Tangent };
        //if (Track.Count == 1)
        //{
        //    NewTrack.Add(new(0, Track[0].Value, Track[0].IngoingTangent, Track[0].OutgoingTangent));
        //    return NewTrack;
        //}

        //ushort? FirstKeyframeId = GetNextKeyframeIndex(Track, (ushort)(StartTime-1));
        //ushort? SecondKeyframeId = GetNextKeyframeIndex(Track, (ushort)((StartTime+Duration)-1));

        //if (FirstKeyframeId is null || SecondKeyframeId is null)
        //{
        //    //What do we dooooo????
        //    throw new Exception();
        //}

        //for (int i = 0; i <= SecondKeyframeId - FirstKeyframeId; i++)
        //{
        //    int idx = FirstKeyframeId.Value + i;
        //    NewTrack.Add(new((ushort)(Track[idx].Time - StartTime), Track[idx].Value, Track[idx].IngoingTangent, Track[idx].OutgoingTangent));
        //}


        //return NewTrack;
    }


    #region Mathematique
    private static float GetHermiteInterpolation(J3DKeyFrame FirstKey, J3DKeyFrame SecondKey, ushort Frame)
    {
        float length = FirstKey.Time - SecondKey.Time;
        float t = (Frame - FirstKey.Time) / length;
        return GetPointHermite(FirstKey.Value, SecondKey.Value, FirstKey.OutgoingTangent * length, SecondKey.IngoingTangent * length, t);
    }

    private static float GetPointHermite(float p0, float p1, float s0, float s1, float Time)
    {
        float[] Vector =
        [
            (p0 *  2) + (p1 * -2) + (s0 *  1) +  (s1 *  1),
            (p0 * -3) + (p1 *  3) + (s0 * -2) +  (s1 * -1),
            (p0 *  0) + (p1 *  0) + (s0 *  1) +  (s1 *  0),
            (p0 *  1) + (p1 *  0) + (s0 *  0) +  (s1 *  0)
        ];
        return GetPointCubic(Vector, Time);
    }

    private static float GetPointCubic(ReadOnlySpan<float> cf, float t)
    {
        if (cf.Length != 4)
            throw new ArgumentOutOfRangeException(nameof(cf));
        return (((cf[0] * t + cf[1]) * t + cf[2]) * t + cf[3]);
    } 
    #endregion
}

sealed partial class DocGen
{
    /// <summary>
    /// The chunk identifier
    /// </summary>
    public const string COMMON_CHUNKMAGIC = "";
    /// <summary>
    /// Name of the Material that this animation applies to
    /// </summary>
    public const string COMMON_MATERIALNAME = "";
    /// <summary>
    /// Represents an animation entry for the parent class
    /// </summary>
    public const string COMMON_ANIMATIONCLASS = "";
}