using System;
using System.Collections.Generic;
using System.IO;
using static Hack.io.J3D.J3DGraph;

//Heavily based on the SuperBMD Library.
namespace Hack.io.BMD
{
    public partial class BMD
    {
        public class DRW1
        {
            public List<bool> WeightTypeCheck { get; private set; } = new List<bool>();
            public List<int> Indices { get; private set; } = new List<int>();

            private static readonly string Magic = "DRW1";

            public DRW1(Stream BMD)
            {
                int ChunkStart = (int)BMD.Position;
                if (!BMD.ReadString(4).Equals(Magic))
                    throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

                int ChunkSize = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int entryCount = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                BMD.Position += 0x2;

                int boolDataOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int indexDataOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);

                WeightTypeCheck = new List<bool>();

                BMD.Seek(ChunkStart + boolDataOffset, System.IO.SeekOrigin.Begin);
                for (int i = 0; i < entryCount; i++)
                    WeightTypeCheck.Add(BMD.ReadByte() > 0);

                BMD.Seek(ChunkStart + indexDataOffset, System.IO.SeekOrigin.Begin);
                for (int i = 0; i < entryCount; i++)
                    Indices.Add(BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0));

                BMD.Position = ChunkStart + ChunkSize;
            }

            public void Write(Stream writer)
            {
                long start = writer.Position;

                writer.WriteString("DRW1");
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for section size
                writer.WriteReverse(BitConverter.GetBytes((short)WeightTypeCheck.Count), 0, 2);
                writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x14 }, 0, 4); // Offset to weight type bools, always 20
                long IndiciesOffset = writer.Position;
                writer.WriteReverse(BitConverter.GetBytes(20 + WeightTypeCheck.Count), 0, 4); // Offset to indices, always 20 + number of weight type bools

                foreach (bool bol in WeightTypeCheck)
                    writer.WriteByte((byte)(bol ? 0x01 : 0x00));

                AddPadding(writer, 2);

                uint IndOffs = (uint)(writer.Position - start);
                foreach (int inte in Indices)
                    writer.WriteReverse(BitConverter.GetBytes((short)inte), 0, 2);

                AddPadding(writer, 32);

                long end = writer.Position;
                long length = end - start;

                writer.Position = start + 4;
                writer.WriteReverse(BitConverter.GetBytes((int)length), 0, 4);
                writer.Position = start + 0x10;
                writer.WriteReverse(BitConverter.GetBytes(IndOffs), 0, 4); // Offset to indices, always 20 + number of weight type bools
                writer.Position = end;
            }
        }

        //=====================================================================
    }
}
