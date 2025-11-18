using Hack.io.Interface;
using Hack.io.Utility;
using System.Text;
using static Hack.io.CANM.CANM.Track;

namespace Hack.io.CANM;

public class CANM : ILoadSaveFile
{
    #region CONSTANTS
    /// <inheritdoc cref="DocGen.DOC_MAGIC"/>
    public const string MAGIC = "ANDO";

    /// <summary>
    /// Indicates that this CANM file holds a value for every possible frame
    /// </summary>
    public const string FRAMETYPE_CANM = "CANM";
    /// <summary>
    /// Indicates that this CANM file holds only keyframes and uses Hermite Interpolation to figure them out
    /// </summary>
    public const string FRAMETYPE_CKAN = "CKAN";
    #endregion

    private Dictionary<TrackSelection, Track> Tracks = [];

    /// <summary>
    /// Determines the length of the animation (in frames)
    /// </summary>
    public int Length;
    public bool IsFullFrames;

    /// <summary>
    /// Unknown Value
    /// </summary>
    public int Unknown1; // 0x00000001
    /// <summary>
    /// Unknown Value
    /// </summary>
    public int Unknown2; // 0x00000000
    /// <summary>
    /// Unknown Value
    /// </summary>
    public int Unknown3; // 0x00000001
    /// <summary>
    /// Unknown Value
    /// </summary>
    public int Unknown4; // 0x00000004

    public CANM() => InitDictionary();

    public Track this[TrackSelection sel]
    {
        get => Tracks[sel];
        set => Tracks[sel] = value;
    }

    public void Load(Stream Strm)
    {
        long Start = Strm.Position;
        FileUtil.ExceptionOnBadMagic(Strm, MAGIC);

        string FrameTypeInFile = Strm.ReadString(4, Encoding.ASCII);
        IsFullFrames = FrameTypeInFile.Equals(FRAMETYPE_CANM);

        Unknown1 = Strm.ReadInt32();
        Unknown2 = Strm.ReadInt32();
        Unknown3 = Strm.ReadInt32();
        Unknown4 = Strm.ReadInt32();
        Length = Strm.ReadInt32();

        int DataOffset = Strm.ReadInt32();

        foreach (TrackSelection suit in Enum.GetValues<TrackSelection>())
            Tracks[suit].Load(Strm, Start + 0x20 + DataOffset, IsFullFrames);
    }

    public void Save(Stream Strm)
    {
        //long Start = Strm.Position;
        Strm.WriteString(MAGIC, Encoding.ASCII, null);
        Strm.WriteString(IsFullFrames ? FRAMETYPE_CANM : FRAMETYPE_CKAN, Encoding.ASCII, null);
        Strm.WriteInt32((int)StreamUtil.ApplyEndian(Unknown1));
        Strm.WriteInt32((int)StreamUtil.ApplyEndian(Unknown2));
        Strm.WriteInt32((int)StreamUtil.ApplyEndian(Unknown3));
        Strm.WriteInt32((int)StreamUtil.ApplyEndian(Unknown4));
        Strm.WriteInt32(Length);
        Strm.WriteInt32(IsFullFrames ? 0x40 : 0x60);

        List<float> FrameData = [];
        foreach (TrackSelection suit in Enum.GetValues<TrackSelection>())
            Tracks[suit].Save(Strm, ref FrameData, IsFullFrames);

        Strm.WriteInt32((FrameData.Count+2) * sizeof(float));
        Strm.WriteMultiSingle(FrameData);
        Strm.Write(new byte[] { 0x3D, 0xCC, 0xCC, 0xCD, 0x4E, 0x6E, 0x6B, 0x28, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 12);
    }

    //================================================================

    private void InitDictionary()
    {
        Tracks ??= [];

        foreach (TrackSelection suit in Enum.GetValues<TrackSelection>())
            Tracks.Add(suit, []);
    }

    //================================================================

    public class Track : List<Frame>
    {
        public bool UseSingleSlope;

        public void Load(Stream Strm, long DataPos, bool IsCanm)
        {
            int FileCount = Strm.ReadInt32();
            int Start = Strm.ReadInt32();

            if (!IsCanm)
                UseSingleSlope = Strm.ReadInt32() == 0;

            long PausePosition = Strm.Position;

            Strm.Seek(DataPos + 0x04 + (0x04 * Start), SeekOrigin.Begin);
            for (int i = 0; i < FileCount; i++)
            {
                Frame cur = new();

                if (FileCount == 1)
                {
                    cur.Value = Strm.ReadSingle();
                    Add(cur);
                    continue;
                }


                if (IsCanm)
                {
                    cur.FrameId = i;
                    cur.Value = Strm.ReadSingle();
                }
                else
                {
                    cur.FrameId = Strm.ReadSingle();
                    cur.Value = Strm.ReadSingle();
                    cur.InSlope = Strm.ReadSingle();
                    if (!UseSingleSlope)
                        cur.OutSlope = Strm.ReadSingle();
                    //else
                    //    cur.OutSlope = cur.InSlope;
                }
                Add(cur);
            }
            Strm.Seek(PausePosition, SeekOrigin.Begin);
        }

        public void Save(Stream Strm, ref List<float> Data, bool IsCanm)
        {
            List<float> MyData = [];
            for (int i = 0; i < Count; i++)
            {
                Frame cur = this[i];
                if (Count == 1)
                {
                    MyData.Add(cur.Value);
                    continue;
                }
                if (IsCanm)
                {
                    MyData.Add(cur.Value);
                }
                else
                {
                    MyData.Add(cur.FrameId);
                    MyData.Add(cur.Value);
                    MyData.Add(cur.InSlope);
                    if (!UseSingleSlope)
                        MyData.Add(cur.OutSlope);
                }
            }

            int DataIndex = Data.SubListIndex(0, MyData); //Leaving this here for the potential compression
            if (DataIndex == -1)
            {
                DataIndex = Data.Count;
                Data.AddRange(MyData);
            }

            Strm.WriteInt32(Count);
            Strm.WriteInt32(DataIndex);
            if (!IsCanm)
                Strm.WriteInt32(UseSingleSlope ? 0 : 1);

        }

        public bool ContainsFrame(float frame)
        {
            for (int i = 0; i < Count; i++)
            {
                if (this[i].FrameId == frame)
                    return true;
            }
            return false;
        }

        public float GetNextOpenFrame(int AnimLength)
        {
            Frame NewFrame = new() { FrameId = -1 };
            for (int i = 0; i <= AnimLength; i++)
            {
                if (this.Any(o => o.FrameId == i))
                    continue;
                NewFrame.FrameId = i;
                break;
            }
            return NewFrame.FrameId;
        }

        public class Frame
        {
            public float FrameId;
            public float Value;
            public float InSlope;
            public float OutSlope;

            public override string ToString() => $"{FrameId}: {Value} [{InSlope}/{OutSlope}]";

            public void CopyTo(Frame Dest)
            {
                Dest.FrameId = FrameId;
                Dest.Value = Value;
                Dest.InSlope = InSlope;
                Dest.OutSlope = OutSlope;
            }

            public const string CLIPBOARD_HEAD = "CANMFrame";

            public string ToClipboard()
            {
                string Head = CLIPBOARD_HEAD;

                StringBuilder sb = new();
                sb.Append(Head);
                sb.Append('%');
                sb.Append(FrameId);
                sb.Append('%');
                sb.Append(Value);
                sb.Append('%');
                sb.Append(InSlope);
                sb.Append('%');
                sb.Append(OutSlope);
                return sb.ToString();
            }

            public bool FromClipboard(string input)
            {
                if (!input.StartsWith(CLIPBOARD_HEAD + "%"))
                    return false;

                string[] currentdata = input.Split('%');
                if (currentdata.Length != 5)
                    return false;

                if (!float.TryParse(currentdata[1], out float ClipFrame))
                    return false;
                if (!float.TryParse(currentdata[2], out float ClipValue))
                    return false;
                if (!float.TryParse(currentdata[3], out float ClipIn))
                    return false;
                if (!float.TryParse(currentdata[4], out float ClipOut))
                    return false;

                FrameId = (float)Math.Floor(ClipFrame);
                Value = ClipValue;
                InSlope = ClipIn;
                OutSlope = ClipOut;
                return true;
            }
        }
    }

    public enum TrackSelection
    {
        /// <summary>
        /// X Position of the Camera
        /// </summary>
        PositionX,
        /// <summary>
        /// Y Position of the Camera
        /// </summary>
        PositionY,
        /// <summary>
        /// Z Position of the Camera
        /// </summary>
        PositionZ,
        /// <summary>
        /// X Position to look at
        /// </summary>
        TargetX,
        /// <summary>
        /// Y Position to look at
        /// </summary>
        TargetY,
        /// <summary>
        /// Z Position to look at
        /// </summary>
        TargetZ,
        /// <summary>
        /// The Camera's Roll Value. This rotate the camera view
        /// </summary>
        Roll,
        /// <summary>
        /// Field of View. Specifically a FoV-Y
        /// </summary>
        FieldOfView
    }
}