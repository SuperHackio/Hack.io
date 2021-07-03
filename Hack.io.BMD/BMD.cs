using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hack.io.Util;
using OpenTK;
using OpenTK.Graphics;
using static Hack.io.J3D.JUtility;
using static Hack.io.J3D.J3DGraph;
using static Hack.io.J3D.NameTableIO;
using System.Drawing;

//Heavily based on the SuperBMD Library.
namespace Hack.io.BMD
{
    public class BMD
    {
        public string FileName { get; set; }
        public INF1 Scenegraph { get; protected set; }
        public VTX1 VertexData { get; protected set; }
        public EVP1 SkinningEnvelopes { get; protected set; }
        public DRW1 PartialWeightData { get; protected set; }
        public JNT1 Joints { get; protected set; }
        public SHP1 Shapes { get; protected set; }
        public MAT3 Materials { get; protected set; }
        public TEX1 Textures { get; protected set; }

        private static readonly string Magic = "J3D2bmd3";

        public BMD() { }
        public BMD(string Filename)
        {
            FileStream FS = new FileStream(Filename, FileMode.Open);
            Read(FS);
            FS.Close();
            FileName = Filename;
        }
        public BMD(Stream BMD) => Read(BMD);

        public static bool CheckFile(string Filename)
        {
            FileStream FS = new FileStream(Filename, FileMode.Open);
            bool result = FS.ReadString(8).Equals(Magic);
            FS.Close();
            return result;
        }
        public static bool CheckFile(Stream BMD) => BMD.ReadString(8).Equals(Magic);

        public virtual void Save(string Filename)
        {
            FileStream FS = new FileStream(Filename, FileMode.Create);
            Write(FS);
            FS.Close();
            FileName = Filename;
        }
        public virtual void Save(Stream BMD) => Write(BMD);

        protected virtual void Read(Stream BMD)
        {
            if (!BMD.ReadString(8).Equals(Magic))
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

            BMD.Position += 0x08+16;
            Scenegraph = new INF1(BMD, out int VertexCount);
            VertexData = new VTX1(BMD, VertexCount);
            SkinningEnvelopes = new EVP1(BMD);
            PartialWeightData = new DRW1(BMD);
            Joints = new JNT1(BMD);
            Shapes = new SHP1(BMD);
            SkinningEnvelopes.SetInverseBindMatrices(Joints.FlatSkeleton);
            Shapes.SetVertexWeights(SkinningEnvelopes, PartialWeightData);
            Joints.InitBoneFamilies(Scenegraph);
            Materials = new MAT3(BMD);
            if (BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0) == 0x4D444C33)
            {
                int mdl3Size = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                BMD.Position += mdl3Size-0x08;
            }
            else
                BMD.Position -= 0x04;
            Textures = new TEX1(BMD);
            Materials.SetTextureNames(Textures);
            //VertexData.StipUnused(Shapes);
        }

        protected virtual void Write(Stream BMD)
        {
            BMD.WriteString(Magic);
            bool IsBDL = false;
            BMD.Write(new byte[8] { 0xDD, 0xDD, 0xDD, 0xDD, 0x00, 0x00, 0x00, (byte)(IsBDL ? 0x09 : 0x08) }, 0, 8);
            BMD.Write(new byte[16], 0, 16);

            Scenegraph.Write(BMD, Shapes, VertexData);
            VertexData.Write(BMD);
            SkinningEnvelopes.Write(BMD);
            PartialWeightData.Write(BMD);
            Joints.Write(BMD);
            Shapes.Write(BMD);
            Textures.UpdateTextures(Materials);
            Materials.Write(BMD);
            Textures.Write(BMD);

            BMD.Position = 0x08;
            BMD.WriteReverse(BitConverter.GetBytes((int)BMD.Length), 0, 4);
        }

        public class INF1
        {
            public Node Root { get; set; } = null;
            public J3DLoadFlags ScalingRule { get; set; }

            private static readonly string Magic = "INF1";

            public INF1() { }
            public INF1(Stream BMD, out int VertexCount)
            {
                long ChunkStart = BMD.Position;
                if (!BMD.ReadString(4).Equals(Magic))
                    throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

                int ChunkSize = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                ScalingRule = (J3DLoadFlags)BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                BMD.Position += 0x02;
                BMD.Position += 0x04;
                VertexCount = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int HierarchyOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                BMD.Position = ChunkStart + HierarchyOffset;

                Node parent = new Node(BMD, null);
                Node node = null;

                Root = parent;
                do
                {
                    node = new Node(BMD, parent);

                    if (node.Type == NodeType.OpenChild)
                    {
                        Node newNode = new Node(BMD, parent);
                        parent.Children.Add(newNode);
                        parent = newNode;
                    }
                    else if (node.Type == NodeType.CloseChild)
                        parent = parent.Parent;
                    else if (node.Type != NodeType.End)
                    {
                        parent.Parent.Children.Add(node);
                        node.SetParent(parent.Parent);
                        parent = node;
                    }

                } while (node.Type != NodeType.End);

                BMD.Position = ChunkStart + ChunkSize;
            }

            public void Write(Stream writer, SHP1 ShapesForMatrixGroupCount, VTX1 VerticiesForVertexCount)
            {
                long start = writer.Position;

                writer.WriteString("INF1");
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for section size
                writer.WriteReverse(BitConverter.GetBytes((short)ScalingRule), 0, 2);
                writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                writer.WriteReverse(BitConverter.GetBytes(SHP1.CountPackets(ShapesForMatrixGroupCount)), 0, 4); // Number of packets
                writer.WriteReverse(BitConverter.GetBytes(VerticiesForVertexCount.Attributes.Positions.Count), 0, 4); // Number of vertex positions
                writer.WriteReverse(BitConverter.GetBytes(24), 0, 4);

                Root.Write(writer);

                writer.WriteReverse(BitConverter.GetBytes((short)0x0000), 0, 2);
                writer.WriteReverse(BitConverter.GetBytes((short)0x0000), 0, 2);

                AddPadding(writer, 32);

                long end = writer.Position;
                writer.Position = start+4;
                writer.WriteReverse(BitConverter.GetBytes((int)(end - start)), 0, 4);
                writer.Position = end;
            }

            public class Node
            {
                public Node Parent { get; set; }

                public NodeType Type { get; set; }
                public int Index { get; set; }
                public List<Node> Children { get; set; } = new List<Node>();

                public Node()
                {
                    Parent = null;
                    Type = NodeType.End;
                    Index = 0;
                }

                public Node(Stream BMD, Node parent)
                {
                    Parent = parent;
                    Type = (NodeType)BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                    Index = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                }

                public Node(NodeType type, int index, Node parent)
                {
                    Type = type;
                    Index = index;
                    Parent = parent;

                    if (Parent != null)
                        Parent.Children.Add(this);

                    Children = new List<Node>();
                }

                public void Write(Stream BMD)
                {
                    BMD.WriteReverse(BitConverter.GetBytes((short)Type), 0, 2);
                    BMD.WriteReverse(BitConverter.GetBytes((short)Index), 0, 2);
                    if (Children.Count > 0)
                    {
                        BMD.WriteReverse(BitConverter.GetBytes((short)0x0001), 0, 2);
                        BMD.WriteReverse(BitConverter.GetBytes((short)0x0000), 0, 2);
                    }
                    for (int i = 0; i < Children.Count; i++)
                    {
                        Children[i].Write(BMD);
                    }
                    if (Children.Count > 0)
                    {
                        BMD.WriteReverse(BitConverter.GetBytes((short)0x0002), 0, 2);
                        BMD.WriteReverse(BitConverter.GetBytes((short)0x0000), 0, 2);
                    }
                }

                public void SetParent(Node parent)
                {
                    Parent = parent;
                }

                public override string ToString()
                {
                    return $"{ Type } : { Index }";
                }
            }

            public int FetchMaterialIndex(int ShapeID)
            {
                return Search(Root, ShapeID, NodeType.Shape, NodeType.Material);
            }

            private int Search(Node Root, int Index, NodeType IndexType, NodeType SearchType)
            {
                if (Root.Type == IndexType && Root.Index == Index)
                {
                    switch (IndexType)
                    {
                        case NodeType.Joint:
                            break;
                        case NodeType.Material:
                            break;
                        case NodeType.Shape:
                            switch (SearchType)
                            {
                                case NodeType.Joint:
                                    break;
                                case NodeType.Material:
                                    return Root.Parent.Index;
                                case NodeType.Shape:
                                    break;
                                default:
                                    throw new Exception("Bruh Moment!!");
                            }
                            break;
                        default:
                            throw new Exception("Bruh Moment!!");
                    }
                    return -1;
                }
                else if (Root.Children.Count > 0)
                {
                    for (int i = 0; i < Root.Children.Count; i++)
                    {
                        int value = Search(Root.Children[i], Index, IndexType, SearchType);
                        if (value != -1)
                            return value;
                    }
                    return -1;
                }
                else
                    return -1;
            }

            public enum J3DLoadFlags
            {
                // Scaling rule
                ScalingRule_Basic = 0x00000000,
                ScalingRule_XSI = 0x00000001,
                ScalingRule_Maya = 0x00000002,
                ScalingRule_Mask = 0x0000000F,
                //unfinished documentations
            }

            public enum NodeType
            {
                End = 0,
                OpenChild = 1,
                CloseChild = 2,
                Joint = 16,
                Material = 17,
                Shape = 18
            }
        }
        public class VTX1
        {
            public VertexData Attributes { get; set; } = new VertexData();
            public SortedDictionary<GXVertexAttribute, Tuple<GXDataType, byte>> StorageFormats { get; set; } = new SortedDictionary<GXVertexAttribute, Tuple<GXDataType, byte>>();

            private static readonly string Magic = "VTX1";

            public VTX1(Stream BMD, int VertexCount)
            {
                long ChunkStart = BMD.Position;
                if (!BMD.ReadString(4).Equals(Magic))
                    throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

                int ChunkSize = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                BMD.Position += 0x04;
                int[] attribDataOffsets = new int[13]
                {
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0),
                    BitConverter.ToInt32(BMD.ReadReverse(0,4),0)
                };
                GXVertexAttribute attrib = (GXVertexAttribute)BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);

                while (attrib != GXVertexAttribute.Null)
                {
                    GXComponentCount componentCount = (GXComponentCount)BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                    GXDataType componentType = (GXDataType)BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                    byte fractionalBitCount = (byte)BMD.ReadByte();
                    StorageFormats.Add(attrib, new Tuple<GXDataType, byte>(componentType, fractionalBitCount));

                    BMD.Position += 0x03;
                    long curPos = BMD.Position;

                    int attribOffset = GetAttributeDataOffset(attribDataOffsets, ChunkSize, attrib, VertexCount, out int attribDataSize);
                    int attribCount = GetAttributeDataCount(attribDataSize, attrib, componentType, componentCount);
                    Attributes.SetAttributeData(attrib, LoadAttributeData(BMD, (int)(ChunkStart + attribOffset), attribCount, fractionalBitCount, attrib, componentType, componentCount));

                    BMD.Position = curPos;
                    attrib = (GXVertexAttribute)BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                }
                BMD.Position = ChunkStart + ChunkSize;
            }

            public object LoadAttributeData(Stream BMD, int offset, int count, byte frac, GXVertexAttribute attribute, GXDataType dataType, GXComponentCount compCount)
            {
                BMD.Seek(offset, SeekOrigin.Begin);
                object final = null;

                switch (attribute)
                {
                    case GXVertexAttribute.Position:
                        switch (compCount)
                        {
                            case GXComponentCount.Position_XY:
                                final = LoadVec2Data(BMD, frac, count, dataType);
                                break;
                            case GXComponentCount.Position_XYZ:
                                final = LoadVec3Data(BMD, frac, count, dataType);
                                break;
                        }
                        break;
                    case GXVertexAttribute.Normal:
                        switch (compCount)
                        {
                            case GXComponentCount.Normal_XYZ:
                                final = LoadVec3Data(BMD, frac, count, dataType);
                                break;
                            case GXComponentCount.Normal_NBT:
                                break;
                            case GXComponentCount.Normal_NBT3:
                                break;
                        }
                        break;
                    case GXVertexAttribute.Color0:
                    case GXVertexAttribute.Color1:
                        final = LoadColorData(BMD, count, dataType);
                        break;
                    case GXVertexAttribute.Tex0:
                    case GXVertexAttribute.Tex1:
                    case GXVertexAttribute.Tex2:
                    case GXVertexAttribute.Tex3:
                    case GXVertexAttribute.Tex4:
                    case GXVertexAttribute.Tex5:
                    case GXVertexAttribute.Tex6:
                    case GXVertexAttribute.Tex7:
                        switch (compCount)
                        {
                            case GXComponentCount.TexCoord_S:
                                final = LoadSingleFloat(BMD, frac, count, dataType);
                                break;
                            case GXComponentCount.TexCoord_ST:
                                final = LoadVec2Data(BMD, frac, count, dataType);
                                break;
                        }
                        break;
                }

                return final;
            }

            private List<float> LoadSingleFloat(Stream BMD, byte frac, int count, GXDataType dataType)
            {
                List<float> floatList = new List<float>();

                for (int i = 0; i < count; i++)
                {
                    switch (dataType)
                    {
                        case GXDataType.Unsigned8:
                            byte compu81 = (byte)BMD.ReadByte();
                            float compu81Float = (float)compu81 / (float)(1 << frac);
                            floatList.Add(compu81Float);
                            break;
                        case GXDataType.Signed8:
                            sbyte comps81 = (sbyte)BMD.ReadByte();
                            float comps81Float = (float)comps81 / (float)(1 << frac);
                            floatList.Add(comps81Float);
                            break;
                        case GXDataType.Unsigned16:
                            ushort compu161 = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                            float compu161Float = (float)compu161 / (float)(1 << frac);
                            floatList.Add(compu161Float);
                            break;
                        case GXDataType.Signed16:
                            short comps161 = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                            float comps161Float = (float)comps161 / (float)(1 << frac);
                            floatList.Add(comps161Float);
                            break;
                        case GXDataType.Float32:
                            floatList.Add(BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0));
                            break;
                    }
                }

                return floatList;
            }

            private List<Vector2> LoadVec2Data(Stream BMD, byte frac, int count, GXDataType dataType)
            {
                List<Vector2> vec2List = new List<Vector2>();

                for (int i = 0; i < count; i++)
                {
                    switch (dataType)
                    {
                        case GXDataType.Unsigned8:
                            byte compu81 = (byte)BMD.ReadByte();
                            byte compu82 = (byte)BMD.ReadByte();
                            float compu81Float = (float)compu81 / (float)(1 << frac);
                            float compu82Float = (float)compu82 / (float)(1 << frac);
                            vec2List.Add(new Vector2(compu81Float, compu82Float));
                            break;
                        case GXDataType.Signed8:
                            sbyte comps81 = (sbyte)BMD.ReadByte();
                            sbyte comps82 = (sbyte)BMD.ReadByte();
                            float comps81Float = (float)comps81 / (float)(1 << frac);
                            float comps82Float = (float)comps82 / (float)(1 << frac);
                            vec2List.Add(new Vector2(comps81Float, comps82Float));
                            break;
                        case GXDataType.Unsigned16:
                            ushort compu161 = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                            ushort compu162 = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                            float compu161Float = (float)compu161 / (float)(1 << frac);
                            float compu162Float = (float)compu162 / (float)(1 << frac);
                            vec2List.Add(new Vector2(compu161Float, compu162Float));
                            break;
                        case GXDataType.Signed16:
                            short comps161 = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                            short comps162 = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                            float comps161Float = (float)comps161 / (float)(1 << frac);
                            float comps162Float = (float)comps162 / (float)(1 << frac);
                            vec2List.Add(new Vector2(comps161Float, comps162Float));
                            break;
                        case GXDataType.Float32:
                            vec2List.Add(new Vector2(BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0)));
                            break;
                    }
                }

                return vec2List;
            }

            private List<Vector3> LoadVec3Data(Stream BMD, byte frac, int count, GXDataType dataType)
            {
                List<Vector3> vec3List = new List<Vector3>();

                for (int i = 0; i < count; i++)
                {
                    switch (dataType)
                    {
                        case GXDataType.Unsigned8:
                            byte compu81 = (byte)BMD.ReadByte();
                            byte compu82 = (byte)BMD.ReadByte();
                            byte compu83 = (byte)BMD.ReadByte();
                            float compu81Float = (float)compu81 / (float)(1 << frac);
                            float compu82Float = (float)compu82 / (float)(1 << frac);
                            float compu83Float = (float)compu83 / (float)(1 << frac);
                            vec3List.Add(new Vector3(compu81Float, compu82Float, compu83Float));
                            break;
                        case GXDataType.Signed8:
                            sbyte comps81 = (sbyte)BMD.ReadByte();
                            sbyte comps82 = (sbyte)BMD.ReadByte();
                            sbyte comps83 = (sbyte)BMD.ReadByte();
                            float comps81Float = (float)comps81 / (float)(1 << frac);
                            float comps82Float = (float)comps82 / (float)(1 << frac);
                            float comps83Float = (float)comps83 / (float)(1 << frac);
                            vec3List.Add(new Vector3(comps81Float, comps82Float, comps83Float));
                            break;
                        case GXDataType.Unsigned16:
                            ushort compu161 = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                            ushort compu162 = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                            ushort compu163 = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                            float compu161Float = (float)compu161 / (float)(1 << frac);
                            float compu162Float = (float)compu162 / (float)(1 << frac);
                            float compu163Float = (float)compu163 / (float)(1 << frac);
                            vec3List.Add(new Vector3(compu161Float, compu162Float, compu163Float));
                            break;
                        case GXDataType.Signed16:
                            short comps161 = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                            short comps162 = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                            short comps163 = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                            float comps161Float = (float)comps161 / (float)(1 << frac);
                            float comps162Float = (float)comps162 / (float)(1 << frac);
                            float comps163Float = (float)comps163 / (float)(1 << frac);
                            vec3List.Add(new Vector3(comps161Float, comps162Float, comps163Float));
                            break;
                        case GXDataType.Float32:
                            vec3List.Add(new Vector3(BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0)));
                            break;
                    }
                }

                return vec3List;
            }

            private List<Color4> LoadColorData(Stream BMD, int count, GXDataType dataType)
            {
                List<Color4> colorList = new List<Color4>();

                for (int i = 0; i < count; i++)
                {
                    switch (dataType)
                    {
                        case GXDataType.RGB565:
                            short colorShort = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                            int r5 = (colorShort & 0xF800) >> 11;
                            int g6 = (colorShort & 0x07E0) >> 5;
                            int b5 = (colorShort & 0x001F);
                            colorList.Add(new Color4((float)r5 / 255.0f, (float)g6 / 255.0f, (float)b5 / 255.0f, 1.0f));
                            break;
                        case GXDataType.RGB8:
                            byte r8 = (byte)BMD.ReadByte();
                            byte g8 = (byte)BMD.ReadByte();
                            byte b8 = (byte)BMD.ReadByte();
                            BMD.Position++;
                            colorList.Add(new Color4((float)r8 / 255.0f, (float)g8 / 255.0f, (float)b8 / 255.0f, 1.0f));
                            break;
                        case GXDataType.RGBX8:
                            byte rx8 = (byte)BMD.ReadByte();
                            byte gx8 = (byte)BMD.ReadByte();
                            byte bx8 = (byte)BMD.ReadByte();
                            BMD.Position++;
                            colorList.Add(new Color4((float)rx8 / 255.0f, (float)gx8 / 255.0f, (float)bx8 / 255.0f, 1.0f));
                            break;
                        case GXDataType.RGBA4:
                            short colorShortA = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                            int r4 = (colorShortA & 0xF000) >> 12;
                            int g4 = (colorShortA & 0x0F00) >> 8;
                            int b4 = (colorShortA & 0x00F0) >> 4;
                            int a4 = (colorShortA & 0x000F);
                            colorList.Add(new Color4((float)r4 / 255.0f, (float)g4 / 255.0f, (float)b4 / 255.0f, (float)a4 / 255.0f));
                            break;
                        case GXDataType.RGBA6:
                            int colorInt = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                            int r6 = (colorInt & 0xFC0000) >> 18;
                            int ga6 = (colorInt & 0x03F000) >> 12;
                            int b6 = (colorInt & 0x000FC0) >> 6;
                            int a6 = (colorInt & 0x00003F);
                            colorList.Add(new Color4((float)r6 / 255.0f, (float)ga6 / 255.0f, (float)b6 / 255.0f, (float)a6 / 255.0f));
                            break;
                        case GXDataType.RGBA8:
                            byte ra8 = (byte)BMD.ReadByte();
                            byte ga8 = (byte)BMD.ReadByte();
                            byte ba8 = (byte)BMD.ReadByte();
                            byte aa8 = (byte)BMD.ReadByte();
                            colorList.Add(new Color4((float)ra8 / 255.0f, (float)ga8 / 255.0f, (float)ba8 / 255.0f, (float)aa8 / 255.0f));
                            break;
                    }
                }
                return colorList;
            }

            private int GetAttributeDataOffset(int[] offsets, int vtx1Size, GXVertexAttribute attribute, int VertexCount, out int size)
            {
                int offset = 0;
                size = 0;
                Vtx1OffsetIndex start = Vtx1OffsetIndex.PositionData;

                switch (attribute)
                {
                    case GXVertexAttribute.Position:
                        start = Vtx1OffsetIndex.PositionData;
                        offset = offsets[(int)Vtx1OffsetIndex.PositionData];
                        break;
                    case GXVertexAttribute.Normal:
                        start = Vtx1OffsetIndex.NormalData;
                        offset = offsets[(int)Vtx1OffsetIndex.NormalData];
                        break;
                    case GXVertexAttribute.Color0:
                        start = Vtx1OffsetIndex.Color0Data;
                        offset = offsets[(int)Vtx1OffsetIndex.Color0Data];
                        break;
                    case GXVertexAttribute.Color1:
                        start = Vtx1OffsetIndex.Color1Data;
                        offset = offsets[(int)Vtx1OffsetIndex.Color1Data];
                        break;
                    case GXVertexAttribute.Tex0:
                        start = Vtx1OffsetIndex.TexCoord0Data;
                        offset = offsets[(int)Vtx1OffsetIndex.TexCoord0Data];
                        break;
                    case GXVertexAttribute.Tex1:
                        start = Vtx1OffsetIndex.TexCoord1Data;
                        offset = offsets[(int)Vtx1OffsetIndex.TexCoord1Data];
                        break;
                    case GXVertexAttribute.Tex2:
                        start = Vtx1OffsetIndex.TexCoord2Data;
                        offset = offsets[(int)Vtx1OffsetIndex.TexCoord2Data];
                        break;
                    case GXVertexAttribute.Tex3:
                        start = Vtx1OffsetIndex.TexCoord3Data;
                        offset = offsets[(int)Vtx1OffsetIndex.TexCoord3Data];
                        break;
                    case GXVertexAttribute.Tex4:
                        start = Vtx1OffsetIndex.TexCoord4Data;
                        offset = offsets[(int)Vtx1OffsetIndex.TexCoord4Data];
                        break;
                    case GXVertexAttribute.Tex5:
                        start = Vtx1OffsetIndex.TexCoord5Data;
                        offset = offsets[(int)Vtx1OffsetIndex.TexCoord5Data];
                        break;
                    case GXVertexAttribute.Tex6:
                        start = Vtx1OffsetIndex.TexCoord6Data;
                        offset = offsets[(int)Vtx1OffsetIndex.TexCoord6Data];
                        break;
                    case GXVertexAttribute.Tex7:
                        start = Vtx1OffsetIndex.TexCoord7Data;
                        offset = offsets[(int)Vtx1OffsetIndex.TexCoord7Data];
                        break;
                    default:
                        throw new ArgumentException("attribute");
                }

                for (int i = (int)start + 1; i < 13; i++)
                {
                    if (i == 12)
                    {
                        size = vtx1Size - offset;
                        break;
                    }

                    int nextOffset = offsets[i];

                    if (nextOffset == 0)
                        continue;
                    else
                    {
                        size = nextOffset - offset;
                        break;
                    }
                }

                return offset;
            }

            private int GetAttributeDataCount(int size, GXVertexAttribute attribute, GXDataType dataType, GXComponentCount compCount)
            {
                int compCnt = 0;
                int compStride = 0;

                if (attribute == GXVertexAttribute.Color0 || attribute == GXVertexAttribute.Color1)
                {
                    switch (dataType)
                    {
                        case GXDataType.RGB565:
                        case GXDataType.RGBA4:
                            compCnt = 1;
                            compStride = 2;
                            break;
                        case GXDataType.RGB8:
                        case GXDataType.RGBX8:
                        case GXDataType.RGBA6:
                        case GXDataType.RGBA8:
                            compCnt = 4;
                            compStride = 1;
                            break;
                    }
                }
                else
                {
                    switch (dataType)
                    {
                        case GXDataType.Unsigned8:
                        case GXDataType.Signed8:
                            compStride = 1;
                            break;
                        case GXDataType.Unsigned16:
                        case GXDataType.Signed16:
                            compStride = 2;
                            break;
                        case GXDataType.Float32:
                            compStride = 4;
                            break;
                    }

                    switch (attribute)
                    {
                        case GXVertexAttribute.Position:
                            if (compCount == GXComponentCount.Position_XY)
                                compCnt = 2;
                            else if (compCount == GXComponentCount.Position_XYZ)
                                compCnt = 3;
                            break;
                        case GXVertexAttribute.Normal:
                            if (compCount == GXComponentCount.Normal_XYZ)
                                compCnt = 3;
                            break;
                        case GXVertexAttribute.Tex0:
                        case GXVertexAttribute.Tex1:
                        case GXVertexAttribute.Tex2:
                        case GXVertexAttribute.Tex3:
                        case GXVertexAttribute.Tex4:
                        case GXVertexAttribute.Tex5:
                        case GXVertexAttribute.Tex6:
                        case GXVertexAttribute.Tex7:
                            if (compCount == GXComponentCount.TexCoord_S)
                                compCnt = 1;
                            else if (compCount == GXComponentCount.TexCoord_ST)
                                compCnt = 2;
                            break;
                    }
                }

                return size / (compCnt * compStride);
            }

            public void Write(Stream writer)
            {
                long start = writer.Position;

                writer.WriteString("VTX1");
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for section size
                writer.WriteReverse(BitConverter.GetBytes(0x40), 0, 4); // Offset to attribute data

                for (int i = 0; i < 13; i++) // Placeholders for attribute data offsets
                    writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4); //I can't use the typical 0xDD here because this actually is setting the values to be "empty", it's not a placeholder

                WriteAttributeHeaders(writer);

                AddPadding(writer, 32);

                WriteAttributeData(writer, (int)start);

                long end = writer.Position;
                long length = (end - start);

                writer.Position = (int)start + 4;
                writer.WriteReverse(BitConverter.GetBytes((int)length), 0, 4);
                writer.Position = end;
            }

            private void WriteAttributeHeaders(Stream writer)
            {
                foreach (GXVertexAttribute attrib in Enum.GetValues(typeof(GXVertexAttribute)))
                {
                    if (!Attributes.ContainsAttribute(attrib) || attrib == GXVertexAttribute.PositionMatrixIdx)
                        continue;

                    writer.WriteReverse(BitConverter.GetBytes((int)attrib), 0, 4);

                    switch (attrib)
                    {
                        case GXVertexAttribute.PositionMatrixIdx:
                            break;
                        case GXVertexAttribute.Position:
                            writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);
                            writer.WriteReverse(BitConverter.GetBytes((int)StorageFormats[attrib].Item1), 0, 4);
                            writer.WriteByte(StorageFormats[attrib].Item2);
                            writer.Write(new byte[3] { 0xFF, 0xFF, 0xFF }, 0, 3);
                            break;
                        case GXVertexAttribute.Normal:
                            writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
                            writer.WriteReverse(BitConverter.GetBytes((int)StorageFormats[attrib].Item1), 0, 4);
                            writer.WriteByte(StorageFormats[attrib].Item2);
                            writer.Write(new byte[3] { 0xFF, 0xFF, 0xFF }, 0, 3);
                            break;
                        case GXVertexAttribute.Color0:
                        case GXVertexAttribute.Color1:
                            writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);
                            writer.WriteReverse(BitConverter.GetBytes((int)StorageFormats[attrib].Item1), 0, 4);
                            writer.WriteByte(StorageFormats[attrib].Item2);
                            writer.Write(new byte[3] { 0xFF, 0xFF, 0xFF }, 0, 3);
                            break;
                        case GXVertexAttribute.Tex0:
                        case GXVertexAttribute.Tex1:
                        case GXVertexAttribute.Tex2:
                        case GXVertexAttribute.Tex3:
                        case GXVertexAttribute.Tex4:
                        case GXVertexAttribute.Tex5:
                        case GXVertexAttribute.Tex6:
                        case GXVertexAttribute.Tex7:
                            writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);
                            writer.WriteReverse(BitConverter.GetBytes((int)StorageFormats[attrib].Item1), 0, 4);
                            writer.WriteByte(StorageFormats[attrib].Item2);
                            writer.Write(new byte[3] { 0xFF, 0xFF, 0xFF }, 0, 3);
                            break;
                    }
                }

                writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0xFF }, 0, 4);
                writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);
                writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
                writer.Write(new byte[4] { 0x00, 0xFF, 0xFF, 0xFF }, 0, 4);
            }

            private void WriteAttributeData(Stream writer, int baseOffset)
            {
                foreach (GXVertexAttribute attrib in Enum.GetValues(typeof(GXVertexAttribute)))
                {
                    if (!Attributes.ContainsAttribute(attrib) || attrib == GXVertexAttribute.PositionMatrixIdx)
                        continue;

                    long endOffset = writer.Position;

                    switch (attrib)
                    {
                        case GXVertexAttribute.Position:
                            writer.Position = baseOffset + 0x0C;
                            writer.WriteReverse(BitConverter.GetBytes((int)(writer.Length - baseOffset)), 0, 4);
                            writer.Position = (int)endOffset;

                            foreach (Vector3 posVec in (List<Vector3>)Attributes.GetAttributeData(attrib))
                            {
                                switch (StorageFormats[attrib].Item1)
                                {
                                    case GXDataType.Unsigned8:
                                        writer.WriteByte((byte)Math.Round(posVec.X * (1 << StorageFormats[attrib].Item2)));
                                        writer.WriteByte((byte)Math.Round(posVec.Y * (1 << StorageFormats[attrib].Item2)));
                                        writer.WriteByte((byte)Math.Round(posVec.Z * (1 << StorageFormats[attrib].Item2)));
                                        break;
                                    case GXDataType.Signed8:
                                        writer.WriteByte((byte)((sbyte)Math.Round(posVec.X * (1 << StorageFormats[attrib].Item2))));
                                        writer.WriteByte((byte)((sbyte)Math.Round(posVec.Y * (1 << StorageFormats[attrib].Item2))));
                                        writer.WriteByte((byte)((sbyte)Math.Round(posVec.Z * (1 << StorageFormats[attrib].Item2))));
                                        break;
                                    case GXDataType.Unsigned16:
                                        writer.WriteReverse(BitConverter.GetBytes((ushort)Math.Round(posVec.X * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((ushort)Math.Round(posVec.Y * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((ushort)Math.Round(posVec.Z * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        break;
                                    case GXDataType.Signed16:
                                        writer.WriteReverse(BitConverter.GetBytes((short)Math.Round(posVec.X * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((short)Math.Round(posVec.Y * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((short)Math.Round(posVec.Z * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        break;
                                    case GXDataType.Float32:
                                        writer.WriteReverse(BitConverter.GetBytes(posVec.X), 0, 4);
                                        writer.WriteReverse(BitConverter.GetBytes(posVec.Y), 0, 4);
                                        writer.WriteReverse(BitConverter.GetBytes(posVec.Z), 0, 4);
                                        break;
                                }
                            }
                            break;
                        case GXVertexAttribute.Normal:
                            writer.Position = baseOffset + 0x10;
                            writer.WriteReverse(BitConverter.GetBytes((int)(writer.Length - baseOffset)), 0, 4);
                            writer.Position = (int)endOffset;

                            foreach (Vector3 normVec in Attributes.Normals)
                            {
                                switch (StorageFormats[attrib].Item1)
                                {
                                    case GXDataType.Unsigned8:
                                        writer.WriteByte((byte)Math.Round(normVec.X * (1 << StorageFormats[attrib].Item2)));
                                        writer.WriteByte((byte)Math.Round(normVec.Y * (1 << StorageFormats[attrib].Item2)));
                                        writer.WriteByte((byte)Math.Round(normVec.Z * (1 << StorageFormats[attrib].Item2)));
                                        break;
                                    case GXDataType.Signed8:
                                        writer.WriteByte((byte)((sbyte)Math.Round(normVec.X * (1 << StorageFormats[attrib].Item2))));
                                        writer.WriteByte((byte)((sbyte)Math.Round(normVec.Y * (1 << StorageFormats[attrib].Item2))));
                                        writer.WriteByte((byte)((sbyte)Math.Round(normVec.Z * (1 << StorageFormats[attrib].Item2))));
                                        break;
                                    case GXDataType.Unsigned16:
                                        writer.WriteReverse(BitConverter.GetBytes((ushort)Math.Round(normVec.X * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((ushort)Math.Round(normVec.Y * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((ushort)Math.Round(normVec.Z * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        break;
                                    case GXDataType.Signed16:
                                        writer.WriteReverse(BitConverter.GetBytes((short)Math.Round(normVec.X * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((short)Math.Round(normVec.Y * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((short)Math.Round(normVec.Z * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        break;
                                    case GXDataType.Float32:
                                        writer.WriteReverse(BitConverter.GetBytes(normVec.X), 0, 4);
                                        writer.WriteReverse(BitConverter.GetBytes(normVec.Y), 0, 4);
                                        writer.WriteReverse(BitConverter.GetBytes(normVec.Z), 0, 4);
                                        break;
                                }
                            }
                            break;
                        case GXVertexAttribute.Color0:
                        case GXVertexAttribute.Color1:
                            writer.Position = baseOffset + 0x18 + (int)(attrib - 11) * 4;
                            writer.WriteReverse(BitConverter.GetBytes((int)(writer.Length - baseOffset)), 0, 4);
                            writer.Position = (int)endOffset;

                            foreach (Color4 col in (List<Color4>)Attributes.GetAttributeData(attrib))
                            {
                                writer.WriteByte((byte)(col.R * 255));
                                writer.WriteByte((byte)(col.G * 255));
                                writer.WriteByte((byte)(col.B * 255));
                                writer.WriteByte((byte)(col.A * 255));
                            }
                            break;
                        case GXVertexAttribute.Tex0:
                        case GXVertexAttribute.Tex1:
                        case GXVertexAttribute.Tex2:
                        case GXVertexAttribute.Tex3:
                        case GXVertexAttribute.Tex4:
                        case GXVertexAttribute.Tex5:
                        case GXVertexAttribute.Tex6:
                        case GXVertexAttribute.Tex7:
                            writer.Position = baseOffset + 0x20 + (int)(attrib - 13) * 4;
                            writer.WriteReverse(BitConverter.GetBytes((int)(writer.Length - baseOffset)), 0, 4);
                            writer.Position = (int)endOffset;

                            foreach (Vector2 texVec in (List<Vector2>)Attributes.GetAttributeData(attrib))
                            {
                                switch (StorageFormats[attrib].Item1)
                                {
                                    case GXDataType.Unsigned8:
                                        writer.WriteByte((byte)Math.Round(texVec.X * (1 << StorageFormats[attrib].Item2)));
                                        writer.WriteByte((byte)Math.Round(texVec.Y * (1 << StorageFormats[attrib].Item2)));
                                        break;
                                    case GXDataType.Signed8:
                                        writer.WriteByte((byte)((sbyte)Math.Round(texVec.X * (1 << StorageFormats[attrib].Item2))));
                                        writer.WriteByte((byte)((sbyte)Math.Round(texVec.Y * (1 << StorageFormats[attrib].Item2))));
                                        break;
                                    case GXDataType.Unsigned16:
                                        writer.WriteReverse(BitConverter.GetBytes((ushort)Math.Round(texVec.X * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((ushort)Math.Round(texVec.Y * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        break;
                                    case GXDataType.Signed16:
                                        writer.WriteReverse(BitConverter.GetBytes((short)Math.Round(texVec.X * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        writer.WriteReverse(BitConverter.GetBytes((short)Math.Round(texVec.Y * (1 << StorageFormats[attrib].Item2))), 0, 2);
                                        break;
                                    case GXDataType.Float32:
                                        writer.WriteReverse(BitConverter.GetBytes(texVec.X), 0, 4);
                                        writer.WriteReverse(BitConverter.GetBytes(texVec.Y), 0, 4);
                                        break;
                                }
                            }
                            break;
                    }
                    AddPadding(writer, 32);
                }
            }

            internal void StipUnused(SHP1 Shapes)
            {
                List<SHP1.Vertex> UsedVerticies = Shapes.GetAllUsedVertices();
                SortedDictionary<uint, Vector3> NewPositions = new SortedDictionary<uint, Vector3>(), NewNormals = new SortedDictionary<uint, Vector3>();
                SortedDictionary<uint, Color4> NewColours0 = new SortedDictionary<uint, Color4>(), NewColours1 = new SortedDictionary<uint, Color4>();
                SortedDictionary<uint, Vector2> NewTexCoord0 = new SortedDictionary<uint, Vector2>(), NewTexCoord1 = new SortedDictionary<uint, Vector2>(),
                    NewTexCoord2 = new SortedDictionary<uint, Vector2>(), NewTexCoord3 = new SortedDictionary<uint, Vector2>(), NewTexCoord4 = new SortedDictionary<uint, Vector2>(),
                    NewTexCoord5 = new SortedDictionary<uint, Vector2>(), NewTexCoord6 = new SortedDictionary<uint, Vector2>(), NewTexCoord7 = new SortedDictionary<uint, Vector2>();

                bool HasPosition = Attributes.ContainsAttribute(GXVertexAttribute.Position), HasNormal = Attributes.ContainsAttribute(GXVertexAttribute.Normal),
                    HasColour0 = Attributes.ContainsAttribute(GXVertexAttribute.Color0), HasColour1 = Attributes.ContainsAttribute(GXVertexAttribute.Color1),
                    HasTex0 = Attributes.ContainsAttribute(GXVertexAttribute.Tex0), HasTex1 = Attributes.ContainsAttribute(GXVertexAttribute.Tex1),
                    HasTex2 = Attributes.ContainsAttribute(GXVertexAttribute.Tex2), HasTex3 = Attributes.ContainsAttribute(GXVertexAttribute.Tex3),
                    HasTex4 = Attributes.ContainsAttribute(GXVertexAttribute.Tex4), HasTex5 = Attributes.ContainsAttribute(GXVertexAttribute.Tex5),
                    HasTex6 = Attributes.ContainsAttribute(GXVertexAttribute.Tex6), HasTex7 = Attributes.ContainsAttribute(GXVertexAttribute.Tex7);

                for (int i = 0; i < UsedVerticies.Count; i++)
                {
                    if (HasPosition && !NewPositions.ContainsKey(UsedVerticies[i].PositionIndex))
                        NewPositions.Add(UsedVerticies[i].PositionIndex, Attributes.Positions[(int)UsedVerticies[i].PositionIndex]);

                    if (HasNormal && !NewNormals.ContainsKey(UsedVerticies[i].NormalIndex))
                        NewNormals.Add(UsedVerticies[i].NormalIndex, Attributes.Normals[(int)UsedVerticies[i].NormalIndex]);


                    if (HasColour0 && !NewColours0.ContainsKey(UsedVerticies[i].Color0Index))
                        NewColours0.Add(UsedVerticies[i].Color0Index, Attributes.Color_0[(int)UsedVerticies[i].Color0Index]);

                    if (HasColour1 && !NewColours1.ContainsKey(UsedVerticies[i].Color1Index))
                        NewColours1.Add(UsedVerticies[i].Color1Index, Attributes.Color_1[(int)UsedVerticies[i].Color1Index]);


                    if (HasTex0 && !NewTexCoord0.ContainsKey(UsedVerticies[i].TexCoord0Index))
                        NewTexCoord0.Add(UsedVerticies[i].TexCoord0Index, Attributes.TexCoord_0[(int)UsedVerticies[i].TexCoord0Index]);

                    if (HasTex1 && !NewTexCoord1.ContainsKey(UsedVerticies[i].TexCoord1Index))
                        NewTexCoord1.Add(UsedVerticies[i].TexCoord1Index, Attributes.TexCoord_1[(int)UsedVerticies[i].TexCoord1Index]);

                    if (HasTex2 && !NewTexCoord2.ContainsKey(UsedVerticies[i].TexCoord2Index))
                        NewTexCoord2.Add(UsedVerticies[i].TexCoord2Index, Attributes.TexCoord_2[(int)UsedVerticies[i].TexCoord2Index]);

                    if (HasTex3 && !NewTexCoord3.ContainsKey(UsedVerticies[i].TexCoord3Index))
                        NewTexCoord3.Add(UsedVerticies[i].TexCoord3Index, Attributes.TexCoord_3[(int)UsedVerticies[i].TexCoord3Index]);

                    if (HasTex4 && !NewTexCoord4.ContainsKey(UsedVerticies[i].TexCoord4Index))
                        NewTexCoord4.Add(UsedVerticies[i].TexCoord4Index, Attributes.TexCoord_4[(int)UsedVerticies[i].TexCoord4Index]);

                    if (HasTex5 && !NewTexCoord5.ContainsKey(UsedVerticies[i].TexCoord5Index))
                        NewTexCoord5.Add(UsedVerticies[i].TexCoord5Index, Attributes.TexCoord_5[(int)UsedVerticies[i].TexCoord5Index]);

                    if (HasTex6 && !NewTexCoord6.ContainsKey(UsedVerticies[i].TexCoord6Index))
                        NewTexCoord6.Add(UsedVerticies[i].TexCoord6Index, Attributes.TexCoord_6[(int)UsedVerticies[i].TexCoord6Index]);

                    if (HasTex7 && !NewTexCoord7.ContainsKey(UsedVerticies[i].TexCoord7Index))
                        NewTexCoord7.Add(UsedVerticies[i].TexCoord7Index, Attributes.TexCoord_7[(int)UsedVerticies[i].TexCoord7Index]);
                }

                if (HasPosition)
                    Attributes.SetAttributeData(GXVertexAttribute.Position, NewPositions.Values.ToList());

                if (HasNormal)
                    Attributes.SetAttributeData(GXVertexAttribute.Normal, NewNormals.Values.ToList());

                if (HasColour0)
                    Attributes.SetAttributeData(GXVertexAttribute.Color0, NewColours0.Values.ToList());
                if (HasColour1)
                    Attributes.SetAttributeData(GXVertexAttribute.Color1, NewColours1.Values.ToList());

                if (HasTex0)
                    Attributes.SetAttributeData(GXVertexAttribute.Tex0, NewTexCoord0.Values.ToList());
                if (HasTex1)
                    Attributes.SetAttributeData(GXVertexAttribute.Tex1, NewTexCoord1.Values.ToList());
                if (HasTex2)
                    Attributes.SetAttributeData(GXVertexAttribute.Tex2, NewTexCoord2.Values.ToList());
                if (HasTex3)
                    Attributes.SetAttributeData(GXVertexAttribute.Tex3, NewTexCoord3.Values.ToList());
                if (HasTex4)
                    Attributes.SetAttributeData(GXVertexAttribute.Tex4, NewTexCoord4.Values.ToList());
                if (HasTex5)
                    Attributes.SetAttributeData(GXVertexAttribute.Tex5, NewTexCoord5.Values.ToList());
                if (HasTex6)
                    Attributes.SetAttributeData(GXVertexAttribute.Tex6, NewTexCoord6.Values.ToList());
                if (HasTex7)
                    Attributes.SetAttributeData(GXVertexAttribute.Tex7, NewTexCoord7.Values.ToList());
            }

            public Dictionary<GXVertexAttribute, object> this[SHP1.Vertex IndexProvider]
            {
                get
                {
                    return Attributes.FetchDataForShapeVertex(IndexProvider);
                }
            }

            public class VertexData
            {
                private List<GXVertexAttribute> m_Attributes = new List<GXVertexAttribute>();

                public List<Vector3> Positions { get; set; } = new List<Vector3>();
                public List<Vector3> Normals { get; set; } = new List<Vector3>();
                public List<Color4> Color_0 { get; private set; } = new List<Color4>();
                public List<Color4> Color_1 { get; private set; } = new List<Color4>();
                public List<Vector2> TexCoord_0 { get; private set; } = new List<Vector2>();
                public List<Vector2> TexCoord_1 { get; private set; } = new List<Vector2>();
                public List<Vector2> TexCoord_2 { get; private set; } = new List<Vector2>();
                public List<Vector2> TexCoord_3 { get; private set; } = new List<Vector2>();
                public List<Vector2> TexCoord_4 { get; private set; } = new List<Vector2>();
                public List<Vector2> TexCoord_5 { get; private set; } = new List<Vector2>();
                public List<Vector2> TexCoord_6 { get; private set; } = new List<Vector2>();
                public List<Vector2> TexCoord_7 { get; private set; } = new List<Vector2>();

                public VertexData() { }

                public bool ContainsAttribute(GXVertexAttribute attribute) => m_Attributes.Contains(attribute);

                public object GetAttributeData(GXVertexAttribute attribute)
                {
                    if (!ContainsAttribute(attribute))
                        return null;

                    switch (attribute)
                    {
                        case GXVertexAttribute.Position:
                            return Positions;
                        case GXVertexAttribute.Normal:
                            return Normals;
                        case GXVertexAttribute.Color0:
                            return Color_0;
                        case GXVertexAttribute.Color1:
                            return Color_1;
                        case GXVertexAttribute.Tex0:
                            return TexCoord_0;
                        case GXVertexAttribute.Tex1:
                            return TexCoord_1;
                        case GXVertexAttribute.Tex2:
                            return TexCoord_2;
                        case GXVertexAttribute.Tex3:
                            return TexCoord_3;
                        case GXVertexAttribute.Tex4:
                            return TexCoord_4;
                        case GXVertexAttribute.Tex5:
                            return TexCoord_5;
                        case GXVertexAttribute.Tex6:
                            return TexCoord_6;
                        case GXVertexAttribute.Tex7:
                            return TexCoord_7;
                        default:
                            throw new ArgumentException("attribute");
                    }
                }

                public void SetAttributeData(GXVertexAttribute attribute, object data)
                {
                    if (!ContainsAttribute(attribute))
                        m_Attributes.Add(attribute);

                    switch (attribute)
                    {
                        case GXVertexAttribute.Position:
                            if (data.GetType() != typeof(List<Vector3>))
                                throw new ArgumentException("position data");
                            else
                                Positions = (List<Vector3>)data;
                            break;
                        case GXVertexAttribute.Normal:
                            if (data.GetType() != typeof(List<Vector3>))
                                throw new ArgumentException("normal data");
                            else
                                Normals = (List<Vector3>)data;
                            break;
                        case GXVertexAttribute.Color0:
                            if (data.GetType() != typeof(List<Color4>))
                                throw new ArgumentException("color0 data");
                            else
                                Color_0 = (List<Color4>)data;
                            break;
                        case GXVertexAttribute.Color1:
                            if (data.GetType() != typeof(List<Color4>))
                                throw new ArgumentException("color1 data");
                            else
                                Color_1 = (List<Color4>)data;
                            break;
                        case GXVertexAttribute.Tex0:
                            if (data.GetType() != typeof(List<Vector2>))
                                throw new ArgumentException("texcoord0 data");
                            else
                                TexCoord_0 = (List<Vector2>)data;
                            break;
                        case GXVertexAttribute.Tex1:
                            if (data.GetType() != typeof(List<Vector2>))
                                throw new ArgumentException("texcoord1 data");
                            else
                                TexCoord_1 = (List<Vector2>)data;
                            break;
                        case GXVertexAttribute.Tex2:
                            if (data.GetType() != typeof(List<Vector2>))
                                throw new ArgumentException("texcoord2 data");
                            else
                                TexCoord_2 = (List<Vector2>)data;
                            break;
                        case GXVertexAttribute.Tex3:
                            if (data.GetType() != typeof(List<Vector2>))
                                throw new ArgumentException("texcoord3 data");
                            else
                                TexCoord_3 = (List<Vector2>)data;
                            break;
                        case GXVertexAttribute.Tex4:
                            if (data.GetType() != typeof(List<Vector2>))
                                throw new ArgumentException("texcoord4 data");
                            else
                                TexCoord_4 = (List<Vector2>)data;
                            break;
                        case GXVertexAttribute.Tex5:
                            if (data.GetType() != typeof(List<Vector2>))
                                throw new ArgumentException("texcoord5 data");
                            else
                                TexCoord_5 = (List<Vector2>)data;
                            break;
                        case GXVertexAttribute.Tex6:
                            if (data.GetType() != typeof(List<Vector2>))
                                throw new ArgumentException("texcoord6 data");
                            else
                                TexCoord_6 = (List<Vector2>)data;
                            break;
                        case GXVertexAttribute.Tex7:
                            if (data.GetType() != typeof(List<Vector2>))
                                throw new ArgumentException("texcoord7 data");
                            else
                                TexCoord_7 = (List<Vector2>)data;
                            break;
                    }
                }

                public void SetAttributesFromList(List<GXVertexAttribute> attributes)
                {
                    m_Attributes = new List<GXVertexAttribute>(attributes);
                }

                internal Dictionary<GXVertexAttribute, object> FetchDataForShapeVertex(SHP1.Vertex Source)
                {
                    Dictionary<GXVertexAttribute, object> Values = new Dictionary<GXVertexAttribute, object>();
                    foreach (GXVertexAttribute Attribute in m_Attributes)
                    {
                        switch (Attribute)
                        {
                            case GXVertexAttribute.Position:
                                Values.Add(Attribute, Positions[(int)Source.PositionIndex]);
                                break;
                            case GXVertexAttribute.Normal:
                                Values.Add(Attribute, Normals[(int)Source.NormalIndex]);
                                break;
                            case GXVertexAttribute.Color0:
                                Values.Add(Attribute, Color_0[(int)Source.Color0Index]);
                                break;
                            case GXVertexAttribute.Color1:
                                Values.Add(Attribute, Color_1[(int)Source.Color1Index]);
                                break;
                            case GXVertexAttribute.Tex0:
                                Values.Add(Attribute, TexCoord_0[(int)Source.TexCoord0Index]);
                                break;
                            case GXVertexAttribute.Tex1:
                                Values.Add(Attribute, TexCoord_1[(int)Source.TexCoord1Index]);
                                break;
                            case GXVertexAttribute.Tex2:
                                Values.Add(Attribute, TexCoord_2[(int)Source.TexCoord2Index]);
                                break;
                            case GXVertexAttribute.Tex3:
                                Values.Add(Attribute, TexCoord_3[(int)Source.TexCoord3Index]);
                                break;
                            case GXVertexAttribute.Tex4:
                                Values.Add(Attribute, TexCoord_4[(int)Source.TexCoord4Index]);
                                break;
                            case GXVertexAttribute.Tex5:
                                Values.Add(Attribute, TexCoord_5[(int)Source.TexCoord5Index]);
                                break;
                            case GXVertexAttribute.Tex6:
                                Values.Add(Attribute, TexCoord_6[(int)Source.TexCoord6Index]);
                                break;
                            case GXVertexAttribute.Tex7:
                                Values.Add(Attribute, TexCoord_7[(int)Source.TexCoord7Index]);
                                break;
                            default:
                                throw new ArgumentException("attribute");
                        }
                    }
                    return Values;
                }
            }
        }
        public class EVP1
        {
            public List<Weight> Weights { get; private set; } = new List<Weight>();
            public List<Matrix4> InverseBindMatrices { get; private set; } = new List<Matrix4>();

            private static readonly string Magic = "EVP1";

            public EVP1(Stream BMD)
            {
                int ChunkStart = (int)BMD.Position;
                if (!BMD.ReadString(4).Equals(Magic))
                    throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

                int ChunkSize = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int entryCount = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                BMD.Position += 0x02;

                int weightCountsOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int boneIndicesOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int weightDataOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int inverseBindMatricesOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);

                List<int> counts = new List<int>();
                List<float> weights = new List<float>();
                List<int> indices = new List<int>();

                for (int i = 0; i < entryCount; i++)
                    counts.Add(BMD.ReadByte());

                BMD.Seek(boneIndicesOffset + ChunkStart, SeekOrigin.Begin);

                for (int i = 0; i < entryCount; i++)
                {
                    for (int j = 0; j < counts[i]; j++)
                    {
                        indices.Add(BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0));
                    }
                }

                BMD.Seek(weightDataOffset + ChunkStart, SeekOrigin.Begin);

                for (int i = 0; i < entryCount; i++)
                {
                    for (int j = 0; j < counts[i]; j++)
                    {
                        weights.Add(BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0));
                    }
                }

                int totalRead = 0;
                for (int i = 0; i < entryCount; i++)
                {
                    Weight weight = new Weight();

                    for (int j = 0; j < counts[i]; j++)
                    {
                        weight.AddWeight(weights[totalRead + j], indices[totalRead + j]);
                    }

                    Weights.Add(weight);
                    totalRead += counts[i];
                }

                BMD.Seek(inverseBindMatricesOffset + ChunkStart, SeekOrigin.Begin);
                int matrixCount = (ChunkSize - inverseBindMatricesOffset) / 48;

                for (int i = 0; i < matrixCount; i++)
                {
                    Matrix3x4 invBind = new Matrix3x4(BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0),
                                                      BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0),
                                                      BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0));

                    InverseBindMatrices.Add(new Matrix4(invBind.Row0, invBind.Row1, invBind.Row2, Vector4.UnitW));
                }

                BMD.Position = ChunkStart + ChunkSize;
            }

            public void SetInverseBindMatrices(List<Bone> flatSkel)
            {
                if (InverseBindMatrices.Count == 0)
                {
                    // If the original file didn't specify any inverse bind matrices, use default values instead of all zeroes.
                    // And these must be set both in the skeleton and the EVP1.
                    for (int i = 0; i < flatSkel.Count; i++)
                    {
                        Matrix4 newMat = new Matrix4(Vector4.UnitX, Vector4.UnitY, Vector4.UnitZ, Vector4.UnitW);
                        InverseBindMatrices.Add(newMat);
                        flatSkel[i].SetInverseBindMatrix(newMat);
                    }
                    return;
                }

                for (int i = 0; i < flatSkel.Count; i++)
                {
                    Matrix4 newMat = InverseBindMatrices[i];
                    flatSkel[i].SetInverseBindMatrix(newMat);
                }
            }

            public void Write(Stream writer)
            {
                long start = writer.Position;

                writer.WriteString("EVP1");
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for section size
                writer.WriteReverse(BitConverter.GetBytes((short)Weights.Count), 0, 2);
                writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                if (Weights.Count == 0)
                {
                    writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
                    writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
                    writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
                    writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
                    writer.Position = start + 4;
                    writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x20 }, 0, 4);
                    writer.Seek(0, SeekOrigin.End);
                    AddPadding(writer, 8);
                    return;
                }
                
                writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x1C }, 0, 4); // Offset to weight count data. Always 28
                writer.WriteReverse(BitConverter.GetBytes(28 + Weights.Count), 0, 4); // Offset to bone/weight indices. Always 28 + the number of weights
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for weight data offset
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for inverse bind matrix data offset

                foreach (Weight w in Weights)
                    writer.WriteByte((byte)w.Count);

                foreach (Weight w in Weights)
                {
                    foreach (int inte in w.BoneIndices)
                        writer.WriteReverse(BitConverter.GetBytes((short)inte), 0, 2);
                }

                AddPadding(writer, 4);

                long curOffset = writer.Position;

                writer.Position = start + 20;
                writer.WriteReverse(BitConverter.GetBytes((int)(curOffset - start)), 0, 4);
                writer.Position = curOffset;

                foreach (Weight w in Weights)
                {
                    foreach (float fl in w.Weights)
                        writer.WriteReverse(BitConverter.GetBytes(fl), 0, 4);
                }

                curOffset = writer.Position;

                writer.Position = start + 24;
                writer.WriteReverse(BitConverter.GetBytes((int)(curOffset - start)), 0, 4);
                writer.Position = curOffset;

                foreach (Matrix4 mat in InverseBindMatrices)
                {
                    Vector4 Row1 = mat.Row0;
                    Vector4 Row2 = mat.Row1;
                    Vector4 Row3 = mat.Row2;

                    writer.WriteReverse(BitConverter.GetBytes(Row1.X), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(Row1.Y), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(Row1.Z), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(Row1.W), 0, 4);

                    writer.WriteReverse(BitConverter.GetBytes(Row2.X), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(Row2.Y), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(Row2.Z), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(Row2.W), 0, 4);

                    writer.WriteReverse(BitConverter.GetBytes(Row3.X), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(Row3.Y), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(Row3.Z), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(Row3.W), 0, 4);
                }

                AddPadding(writer, 32);

                long end = writer.Position;
                long length = end - start;

                writer.Position = start + 4;
                writer.WriteReverse(BitConverter.GetBytes((int)length), 0, 4);
                writer.Position = end;
            }

            public class Weight
            {
                public List<float> Weights { get; private set; }
                public List<int> BoneIndices { get; private set; }
                public int Count { get => Weights.Count; }

                public Weight()
                {
                    Weights = new List<float>();
                    BoneIndices = new List<int>();
                }

                public void AddWeight(float weight, int boneIndex)
                {
                    Weights.Add(weight);
                    BoneIndices.Add(boneIndex);
                }
            }

            public class Bone
            {
                public string Name { get; private set; }
                public Bone Parent { get; internal set; }
                public List<Bone> Children { get; private set; }
                public Matrix4 InverseBindMatrix { get; private set; }
                public Matrix4 TransformationMatrix { get; private set; }
                public SHP1.BoundingVolume Bounds { get; private set; }

                private short m_MatrixType;
                private bool InheritParentScale;
                private Vector3 m_Scale;
                private Quaternion m_Rotation;
                private Vector3 m_Translation;

                public Bone(string name)
                {
                    Name = name;
                    Children = new List<Bone>();
                    Bounds = new SHP1.BoundingVolume();
                    m_Scale = Vector3.One;
                }

                public Bone(Stream BMD, string name)
                {
                    Children = new List<Bone>();

                    Name = name;
                    m_MatrixType = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                    InheritParentScale = BMD.ReadByte() == 0;

                    BMD.Position++;

                    m_Scale = new Vector3(BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0));

                    short xRot = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                    short yRot = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                    short zRot = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);

                    float xConvRot = (float)(xRot * 180.0 / 32767.0);
                    float yConvRot = (float)(yRot * 180.0 / 32767.0);
                    float zConvRot = (float)(zRot * 180.0 / 32767.0);

                    Vector3 rotFull = new Vector3((float)(xConvRot * (Math.PI / 180.0)), (float)(yConvRot * (Math.PI / 180.0)), (float)(zConvRot * (Math.PI / 180.0)));

                    m_Rotation = Quaternion.FromAxisAngle(new Vector3(0, 0, 1), rotFull.Z) *
                                 Quaternion.FromAxisAngle(new Vector3(0, 1, 0), rotFull.Y) *
                                 Quaternion.FromAxisAngle(new Vector3(1, 0, 0), rotFull.X);

                    BMD.Position += 0x02;

                    m_Translation = new Vector3(BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0));

                    TransformationMatrix = Matrix4.CreateScale(m_Scale) *
                                           Matrix4.CreateFromQuaternion(m_Rotation) *
                                           Matrix4.CreateTranslation(m_Translation);

                    Bounds = new SHP1.BoundingVolume(BMD);
                }

                public void SetInverseBindMatrix(Matrix4 matrix)
                {
                    InverseBindMatrix = matrix;
                }

                public byte[] ToBytes()
                {
                    List<byte> outList = new List<byte>();

                    using (MemoryStream writer = new MemoryStream())
                    {
                        writer.WriteReverse(BitConverter.GetBytes(m_MatrixType), 0, 2);
                        writer.WriteByte((byte)(InheritParentScale ? 0x00 : 0x01));
                        writer.WriteByte(0xFF);

                        Vector3 Euler = new Vector3();

                        float ysqr = m_Rotation.Y * m_Rotation.Y;

                        float t0 = 2.0f * (m_Rotation.W * m_Rotation.X + m_Rotation.Y * m_Rotation.Z);
                        float t1 = 1.0f - 2.0f * (m_Rotation.X * m_Rotation.X + ysqr);

                        Euler.X = (float)Math.Atan2(t0, t1);

                        float t2 = 2.0f * (m_Rotation.W * m_Rotation.Y - m_Rotation.Z * m_Rotation.X);
                        t2 = t2 > 1.0f ? 1.0f : t2;
                        t2 = t2 < -1.0f ? -1.0f : t2;

                        Euler.Y = (float)Math.Asin(t2);

                        float t3 = 2.0f * (m_Rotation.W * m_Rotation.Z + m_Rotation.X * m_Rotation.Y);
                        float t4 = 1.0f - 2.0f * (ysqr + m_Rotation.Z * m_Rotation.Z);

                        Euler.Z = (float)Math.Atan2(t3, t4);

                        Euler.X = Euler.X * (float)(180.0 / Math.PI);
                        Euler.Y = Euler.Y * (float)(180.0 / Math.PI);
                        Euler.Z = Euler.Z * (float)(180.0 / Math.PI);

                        short[] compressRot = new short[3];

                        //compressRot[0] = (ushort)(Euler.X * 32767.0 / 180.0);
                        //compressRot[1] = (ushort)(Euler.Y * 32767.0 / 180.0);
                        //compressRot[2] = (ushort)(Euler.Z * 32767.0 / 180.0);

                        //Some of this is broken apparently
                        compressRot[0] = (short)(Euler.X < 180 && Euler.X > -180 ? (m_MatrixType == 0x0002 ? Math.Round(Euler.X) * 32767.0 / 180.0 : Math.Round(Euler.X * 32767.0 / 180.0)) : (Euler.X >= 180 ? -32768 : 32767));
                        compressRot[1] = (short)(Euler.Y < 180 && Euler.Y > -180 ? (m_MatrixType == 0x0002 ? Math.Round(Euler.Y) * 32767.0 / 180.0 : Math.Round(Euler.Y * 32767.0 / 180.0)) : (Euler.Y >= 180 ? -32768 : 32767));
                        compressRot[2] = (short)(Euler.Z < 180 && Euler.Z > -180 ? (m_MatrixType == 0x0002 ? Math.Round(Euler.Z) * 32767.0 / 180.0 : Math.Round(Euler.Z * 32767.0 / 180.0)) : (Euler.Z >= 180 ? -32768 : 32767));

                        //=====
                        //Console.WriteLine(Name);
                        //Console.WriteLine($"Matrix Mode: {m_MatrixType.ToString("X4")}");
                        //Console.WriteLine($"compressRot[0]: {compressRot[0].ToString("X4")}");
                        //Console.WriteLine($"compressRot[1]: {compressRot[1].ToString("X4")}");
                        //Console.WriteLine($"compressRot[2]: {compressRot[2].ToString("X4")}");
                        //Console.WriteLine();
                        //=====

                        writer.WriteReverse(BitConverter.GetBytes(m_Scale.X), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(m_Scale.Y), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(m_Scale.Z), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(compressRot[0]), 0, 2);
                        writer.WriteReverse(BitConverter.GetBytes(compressRot[1]), 0, 2);
                        writer.WriteReverse(BitConverter.GetBytes(compressRot[2]), 0, 2);
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                        writer.WriteReverse(BitConverter.GetBytes(m_Translation.X), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(m_Translation.Y), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(m_Translation.Z), 0, 4);

                        Bounds.Write(writer);

                        outList.AddRange(writer.ToArray());
                    }

                    return outList.ToArray();
                }
            }
        }
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
        public class JNT1
        {
            public List<EVP1.Bone> FlatSkeleton { get; private set; } = new List<EVP1.Bone>();
            public Dictionary<string, int> BoneNameIndices { get; private set; } = new Dictionary<string, int>();

            private static readonly string Magic = "JNT1";

            public JNT1(Stream BMD)
            {
                int ChunkStart = (int)BMD.Position;
                if (!BMD.ReadString(4).Equals(Magic))
                    throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

                int jnt1Size = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int jointCount = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                BMD.Position += 0x02;
                
                int jointDataOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int internTableOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int nameTableOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);

                List<string> names = new List<string>();

                BMD.Seek(ChunkStart + nameTableOffset, SeekOrigin.Begin);

                short stringCount = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                BMD.Position += 0x02;

                for (int i = 0; i < stringCount; i++)
                {
                    BMD.Position += 0x02;
                    short nameOffset = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                    long saveReaderPos = BMD.Position;
                    BMD.Position = ChunkStart + nameTableOffset + nameOffset;

                    names.Add(BMD.ReadString());

                    BMD.Position = saveReaderPos;
                }

                int highestRemap = 0;
                List<int> remapTable = new List<int>();
                BMD.Seek(ChunkStart + internTableOffset, SeekOrigin.Begin);
                for (int i = 0; i < jointCount; i++)
                {
                    int test = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                    remapTable.Add(test);

                    if (test > highestRemap)
                        highestRemap = test;
                }

                List<EVP1.Bone> tempList = new List<EVP1.Bone>();
                BMD.Seek(ChunkStart + jointDataOffset, SeekOrigin.Begin);
                for (int i = 0; i <= highestRemap; i++)
                {
                    tempList.Add(new EVP1.Bone(BMD, names[i]));
                }

                for (int i = 0; i < jointCount; i++)
                {
                    FlatSkeleton.Add(tempList[remapTable[i]]);
                }

                foreach (EVP1.Bone bone in FlatSkeleton)
                    BoneNameIndices.Add(bone.Name, FlatSkeleton.IndexOf(bone));

                BMD.Position = ChunkStart + jnt1Size;
            }

            public void Write(Stream writer)
            {
                long start = writer.Position;

                writer.WriteString("JNT1");
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for section size
                writer.WriteReverse(BitConverter.GetBytes((short)FlatSkeleton.Count), 0, 2);
                writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x18 }, 0, 4); // Offset to joint data, always 24
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for remap data offset
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for name table offset

                List<string> names = new List<string>();
                foreach (EVP1.Bone bone in FlatSkeleton)
                {
                    byte[] BoneData = bone.ToBytes();
                    writer.Write(BoneData, 0, BoneData.Length);
                    names.Add(bone.Name);
                }

                long curOffset = writer.Position;

                writer.Seek((int)(start + 16), SeekOrigin.Begin);
                writer.WriteReverse(BitConverter.GetBytes((int)(curOffset - start)), 0, 4);
                writer.Seek((int)curOffset, SeekOrigin.Begin);

                for (int i = 0; i < FlatSkeleton.Count; i++)
                    writer.WriteReverse(BitConverter.GetBytes((short)i), 0, 2);

                AddPadding(writer, 4);

                curOffset = writer.Position;

                writer.Seek((int)(start + 20), SeekOrigin.Begin);
                writer.WriteReverse(BitConverter.GetBytes((int)(curOffset - start)), 0, 4);
                writer.Seek((int)curOffset, SeekOrigin.Begin);

                writer.WriteStringTable(names);

                AddPadding(writer, 32);

                long end = writer.Position;
                long length = end - start;

                writer.Seek((int)start + 4, SeekOrigin.Begin);
                writer.WriteReverse(BitConverter.GetBytes((int)length), 0, 4);
                writer.Seek((int)end, SeekOrigin.Begin);
            }

            public void InitBoneFamilies(INF1 Scenegraph)
            {
                List<EVP1.Bone> processedJoints = new List<EVP1.Bone>();
                IterateHierarchyForSkeletonRecursive(Scenegraph.Root, processedJoints, -1);
            }
            private void IterateHierarchyForSkeletonRecursive(INF1.Node curNode, List<EVP1.Bone> processedJoints, int parentIndex)
            {
                switch (curNode.Type)
                {
                    case INF1.NodeType.Joint:
                        EVP1.Bone joint = FlatSkeleton[curNode.Index];

                        if (parentIndex >= 0)
                        {
                            joint.Parent = processedJoints[parentIndex];
                        }
                        processedJoints.Add(joint);
                        break;
                }

                parentIndex = processedJoints.Count - 1;
                foreach (var child in curNode.Children)
                    IterateHierarchyForSkeletonRecursive(child, processedJoints, parentIndex);
            }
        }
        public class SHP1
        {
            public List<Shape> Shapes { get; private set; } = new List<Shape>();
            public List<int> RemapTable { get; private set; } = new List<int>();
            /// <summary>
            /// Get a shape with respect to the Remap table
            /// </summary>
            /// <param name="Index"></param>
            /// <returns></returns>
            public Shape this[int Index] { get => Shapes[RemapTable[Index]]; }

            private static readonly string Magic = "SHP1";

            public SHP1(Stream BMD)
            {
                int ChunkStart = (int)BMD.Position;
                if (!BMD.ReadString(4).Equals(Magic))
                    throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

                int shp1Size = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int ShapeEntryCount = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                BMD.Position += 0x02;

                int shapeHeaderDataOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int shapeRemapTableOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int StringTableOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int attributeDataOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int DRW1IndexTableOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int primitiveDataOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int MatrixDataOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                int PacketInfoDataOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);


                // Remap table
                BMD.Seek(ChunkStart + shapeRemapTableOffset, SeekOrigin.Begin);
                int highestIndex = int.MinValue;
                for (int i = 0; i < ShapeEntryCount; i++)
                {
                    RemapTable.Add(BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0));

                    if (RemapTable[i] > highestIndex)
                        highestIndex = RemapTable[i];
                }

                for (int SID = 0; SID < ShapeEntryCount; SID++)
                {
                    // Shapes can have different attributes for each shape. (ie: Some have only Position, while others have Pos & TexCoord, etc.) Each 
                    // shape (which has a consistent number of attributes) it is split into individual packets, which are a collection of geometric primitives.
                    // Each packet can have individual unique skinning data.
                    //      ~Probably LordNed
                    //
                    // . . .
                    //Why Nintendo why

                    BMD.Position = ChunkStart + shapeHeaderDataOffset + (0x28 * SID);
                    long ShapeStart = BMD.Position;
                    Shape CurrentShape = new Shape() { MatrixType = (DisplayFlags)BMD.ReadByte() };
                    BMD.Position++;
                    ushort PacketCount = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                    ushort batchAttributeOffset = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                    ushort firstMatrixIndex = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                    ushort firstPacketIndex = BitConverter.ToUInt16(BMD.ReadReverse(0, 2), 0);
                    BMD.Position += 0x02;
                    BoundingVolume shapeVol = new BoundingVolume(BMD);

                    ShapeVertexDescriptor Desc = new ShapeVertexDescriptor(BMD, BMD.Position = ChunkStart + attributeDataOffset + batchAttributeOffset);
                    CurrentShape.Bounds = shapeVol;
                    CurrentShape.Descriptor = Desc;
                    Shapes.Add(CurrentShape);

                    for (int PacketID = 0; PacketID < PacketCount; PacketID++)
                    {
                        Packet Pack = new Packet();

                        // The packets are all stored linearly and then they point to the specific size and offset of the data for this particular packet.
                        BMD.Position = ChunkStart + PacketInfoDataOffset + ((firstPacketIndex + PacketID) * 0x8); /* 0x8 is the size of one Packet entry */

                        int PacketSize = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);
                        int PacketOffset = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);

                        BMD.Position = ChunkStart +  MatrixDataOffset + (firstMatrixIndex + PacketID) * 0x08;
                        // 8 bytes long
                        Pack.DRW1MatrixID = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                        short MatrixCount = BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0);
                        int StartingMatrix = BitConverter.ToInt32(BMD.ReadReverse(0, 4), 0);


                        BMD.Position = ChunkStart + DRW1IndexTableOffset + (StartingMatrix * 0x2);
                        int LastID = 0;
                        for (int m = 0; m < MatrixCount; m++)
                        {
                            Pack.MatrixIndices.Add(BitConverter.ToInt16(BMD.ReadReverse(0, 2), 0));
                            if (Pack.MatrixIndices[m] == -1)
                                Pack.MatrixIndices[m] = LastID;
                            else
                                LastID = Pack.MatrixIndices[m];
                        }
                        
                        Pack.ReadPrimitives(BMD, Desc, ChunkStart + primitiveDataOffset + PacketOffset, PacketSize);
                        CurrentShape.Packets.Add(Pack);
                    }
                }

                BMD.Position = ChunkStart + shp1Size;
            }

            public void SetVertexWeights(EVP1 envelopes, DRW1 drawList)
            {
                for (int i = 0; i < Shapes.Count; i++)
                {
                    for (int j = 0; j < Shapes[i].Packets.Count; j++)
                    {
                        foreach (Primitive prim in Shapes[i].Packets[j].Primitives)
                        {
                            foreach (Vertex vert in prim.Vertices)
                            {
                                if (Shapes[i].Descriptor.CheckAttribute(GXVertexAttribute.PositionMatrixIdx))
                                {
                                    int drw1Index = Shapes[i].Packets[j].MatrixIndices[(int)vert.PositionMatrixIDxIndex];
                                    int curPacketIndex = j;
                                    while (drw1Index == -1)
                                    {
                                        curPacketIndex--;
                                        drw1Index = Shapes[i].Packets[curPacketIndex].MatrixIndices[(int)vert.PositionMatrixIDxIndex];
                                    }

                                    if (drawList.WeightTypeCheck[(int)drw1Index])
                                    {
                                        int evp1Index = drawList.Indices[(int)drw1Index];
                                        vert.SetWeight(envelopes.Weights[evp1Index]);
                                    }
                                    else
                                    {
                                        EVP1.Weight vertWeight = new EVP1.Weight();
                                        vertWeight.AddWeight(1.0f, drawList.Indices[(int)drw1Index]);
                                        vert.SetWeight(vertWeight);
                                    }
                                }
                                else
                                {
                                    EVP1.Weight vertWeight = new EVP1.Weight();
                                    vertWeight.AddWeight(1.0f, drawList.Indices[Shapes[i].Packets[j].MatrixIndices[0]]);
                                    vert.SetWeight(vertWeight);
                                }
                            }
                        }
                    }
                }
            }

            public static int CountPackets(SHP1 Target)
            {
                int packetCount = 0;
                foreach (Shape shape in Target.Shapes)
                    packetCount += shape.Packets.Count;
                return packetCount;
            }

            public override string ToString() => $"SHP1: {Shapes.Count} Shapes";

            internal List<Vertex> GetAllUsedVertices()
            {
                List<Vertex> results = new List<Vertex>();
                for (int i = 0; i < Shapes.Count; i++)
                    results.AddRange(Shapes[i].GetAllUsedVertices());
                return results;
            }

            public void Write(Stream writer)
            {
                long start = writer.Position;

                List<byte> RemapTableData = new List<byte>();
                for (int i = 0; i < RemapTable.Count; i++)
                    RemapTableData.AddRange(BitConverter.GetBytes((short)RemapTable[i]).Reverse());

                int RemapTableOffset = 0x2C + (0x28 * Shapes.Count), NameTableOffset = 0, AttributeTableOffset, DRW1IndexTableOffset, PrimitiveDataOffset, MatrixDataOffset, PrimitiveLocationDataOffset;
                //In the event that a BMD/BDL with a Name Table for Shapes is found, the saving code will go here

                writer.WriteString("SHP1");
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for Section Size
                writer.WriteReverse(BitConverter.GetBytes((short)Shapes.Count), 0, 2);
                writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x2C }, 0, 4); // ShapeDataOffset
                writer.WriteReverse(BitConverter.GetBytes(RemapTableOffset), 0, 4);
                writer.WriteReverse(BitConverter.GetBytes(NameTableOffset), 0, 4);
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for AttributeTableOffset
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for MatrixTableOffset
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for PrimitiveDataOffset
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for MatrixDataOffset
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for MatrixGroupTableOffset

                for (int SID = 0; SID < Shapes.Count; SID++)
                    Shapes[SID].Write(writer);

                writer.Write(RemapTableData.ToArray(), 0, RemapTableData.Count);

                AddPadding(writer, 4);
                AddPadding(writer, 32);

                AttributeTableOffset = (int)writer.Position;
                List<Tuple<ShapeVertexDescriptor, int>> descriptorOffsets = WriteShapeAttributeDescriptors(writer);

                DRW1IndexTableOffset = (int)writer.Position;
                List<Tuple<Packet, int>> packetMatrixOffsets = WritePacketMatrixIndices(writer);
                AddPadding(writer, 32);

                PrimitiveDataOffset = (int)writer.Position;
                List<Tuple<int, int>> PrimitiveOffsets = WritePrimitives(writer);
                AddPadding(writer, 32);

                MatrixDataOffset = (int)writer.Position;
                WriteMatrixData(writer, packetMatrixOffsets);

                PrimitiveLocationDataOffset = (int)writer.Position;
                foreach (Tuple<int, int> tup in PrimitiveOffsets)
                {
                    writer.WriteReverse(BitConverter.GetBytes(tup.Item1), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(tup.Item2), 0, 4);
                }
                AddPadding(writer, 32);

                writer.Position = start + 0x2C;
                foreach (Shape shape in Shapes)
                {
                    writer.Position += 0x04;
                    writer.WriteReverse(BitConverter.GetBytes((short)descriptorOffsets.Find(x => x.Item1 == shape.Descriptor).Item2), 0, 2);
                    writer.WriteReverse(BitConverter.GetBytes((short)packetMatrixOffsets.IndexOf(packetMatrixOffsets.Find(x => x.Item1 == shape.Packets[0]))), 0, 2);
                    writer.WriteReverse(BitConverter.GetBytes((short)packetMatrixOffsets.IndexOf(packetMatrixOffsets.Find(x => x.Item1 == shape.Packets[0]))), 0, 2);
                    writer.Position += 0x1E;
                }

                writer.Position = start + 0x04;
                writer.WriteReverse(BitConverter.GetBytes((int)(writer.Length - start)), 0, 4);
                writer.Position += 0x10;
                writer.WriteReverse(BitConverter.GetBytes((int)(AttributeTableOffset - start)), 0, 4);
                writer.WriteReverse(BitConverter.GetBytes((int)(DRW1IndexTableOffset - start)), 0, 4);
                writer.WriteReverse(BitConverter.GetBytes((int)(PrimitiveDataOffset - start)), 0, 4);
                writer.WriteReverse(BitConverter.GetBytes((int)(MatrixDataOffset - start)), 0, 4);
                writer.WriteReverse(BitConverter.GetBytes((int)(PrimitiveLocationDataOffset - start)), 0, 4);
                writer.Position = writer.Length;
            }

            private List<Tuple<ShapeVertexDescriptor, int>> WriteShapeAttributeDescriptors(Stream writer)
            {
                List<Tuple<ShapeVertexDescriptor, int>> outList = new List<Tuple<ShapeVertexDescriptor, int>>();
                List<ShapeVertexDescriptor> written = new List<ShapeVertexDescriptor>();

                long start = writer.Position;

                foreach (Shape shape in Shapes)
                {
                    if (written.Any(SVD => SVD == shape.Descriptor))
                        continue;
                    else
                    {
                        outList.Add(new Tuple<ShapeVertexDescriptor, int>(shape.Descriptor, (int)(writer.Position - start)));
                        shape.Descriptor.Write(writer);
                        written.Add(shape.Descriptor);
                    }
                }
                return outList;
            }

            private List<Tuple<Packet, int>> WritePacketMatrixIndices(Stream writer)
            {
                List<Tuple<Packet, int>> outList = new List<Tuple<Packet, int>>();

                int indexOffset = 0;
                foreach (Shape shape in Shapes)
                {
                    foreach (Packet pack in shape.Packets)
                    {
                        outList.Add(new Tuple<Packet, int>(pack, indexOffset));

                        int Last = -1;
                        for (int i = 0; i < pack.MatrixIndices.Count; i++)
                        {
                            if (i > 0 && pack.MatrixIndices[i] == Last)
                                writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                            else
                            {
                                writer.WriteReverse(BitConverter.GetBytes((ushort)pack.MatrixIndices[i]), 0, 2);
                                Last = pack.MatrixIndices[i];
                            }
                            indexOffset++;
                        }
                    }
                }

                return outList;
            }

            private List<Tuple<int, int>> WritePrimitives(Stream writer)
            {
                List<Tuple<int, int>> outList = new List<Tuple<int, int>>();

                long start = writer.Position;

                foreach (Shape shape in Shapes)
                {
                    foreach (Packet pack in shape.Packets)
                    {
                        int offset = (int)(writer.Position - start);

                        foreach (Primitive prim in pack.Primitives)
                        {
                            prim.Write(writer, shape.Descriptor);
                        }

                        writer.PadTo(32);

                        outList.Add(new Tuple<int, int>((int)((writer.Position - start) - offset), offset));
                    }
                }

                return outList;
            }

            private void WriteMatrixData(Stream writer, List<Tuple<Packet, int>> MatrixOffsets)
            {
                int StartingIndex = 0;
                for (int i = 0; i < Shapes.Count; i++)
                {
                    for (int y = 0; y < Shapes[i].Packets.Count; y++)
                    {
                        writer.WriteReverse(BitConverter.GetBytes(Shapes[i].Packets[y].DRW1MatrixID), 0, 2);
                        writer.WriteReverse(BitConverter.GetBytes((short)Shapes[i].Packets[y].MatrixIndices.Count), 0, 2);
                        writer.WriteReverse(BitConverter.GetBytes(StartingIndex), 0, 4);
                        StartingIndex += Shapes[i].Packets[y].MatrixIndices.Count;
                    }
                }
            }

            public class Shape
            {
                public ShapeVertexDescriptor Descriptor { get; set; } = new ShapeVertexDescriptor();

                public DisplayFlags MatrixType { get; set; } = DisplayFlags.MultiMatrix;
                public BoundingVolume Bounds { get; set; } = new BoundingVolume();

                public List<Packet> Packets { get; set; } = new List<Packet>();
                
                // The maximum number of unique vertex weights that can be in a single shape packet without causing visual errors.
                private const int MaxMatricesPerPacket = 10;

                public Shape()
                {
                }

                public Shape(DisplayFlags matrixType) : this()
                {
                    MatrixType = matrixType;
                }

                public Shape(ShapeVertexDescriptor desc, BoundingVolume bounds, List<Packet> prims, DisplayFlags matrixType)
                {
                    Descriptor = desc;
                    Bounds = bounds;
                    Packets = prims;
                    MatrixType = matrixType;
                }

                internal List<Vertex> GetAllUsedVertices()
                {
                    List<Vertex> results = new List<Vertex>();
                    for (int i = 0; i < Packets.Count; i++)
                    {
                        for (int x = 0; x < Packets[i].Primitives.Count; x++)
                        {
                            results.AddRange(Packets[i].Primitives[x].Vertices);
                        }
                    }
                    return results;
                }

                public void Write(Stream writer)
                {
                    writer.WriteByte((byte)MatrixType);
                    writer.WriteByte(0xFF);
                    writer.WriteReverse(BitConverter.GetBytes((short)Packets.Count), 0, 2);
                    writer.Write(new byte[2] { 0xDD, 0xDD }, 0, 2); // Placeholder for descriptor offset
                    writer.Write(new byte[2] { 0xDD, 0xDD }, 0, 2); // Placeholder for starting packet index
                    writer.Write(new byte[2] { 0xDD, 0xDD }, 0, 2); // Placeholder for starting packet matrix index offset
                    writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                    Bounds.Write(writer);
                }

                public override string ToString() => $"Shape: {MatrixType.ToString()}";
            }

            public class ShapeVertexDescriptor
            {
                public SortedDictionary<GXVertexAttribute, Tuple<VertexInputType, int>> Attributes { get; private set; } = new SortedDictionary<GXVertexAttribute, Tuple<VertexInputType, int>>();

                public ShapeVertexDescriptor() { }

                public ShapeVertexDescriptor(Stream reader, long offset)
                {
                    Attributes = new SortedDictionary<GXVertexAttribute, Tuple<VertexInputType, int>>();
                    reader.Position = offset;

                    int index = 0;
                    GXVertexAttribute attrib = (GXVertexAttribute)BitConverter.ToInt32(reader.ReadReverse(0, 4), 0);

                    while (attrib != GXVertexAttribute.Null)
                    {
                        Attributes.Add(attrib, new Tuple<VertexInputType, int>((VertexInputType)BitConverter.ToInt32(reader.ReadReverse(0, 4), 0), index));

                        index++;
                        attrib = (GXVertexAttribute)BitConverter.ToInt32(reader.ReadReverse(0, 4), 0);
                    }
                }

                public bool CheckAttribute(GXVertexAttribute attribute) => Attributes.ContainsKey(attribute);

                public void SetAttribute(GXVertexAttribute attribute, VertexInputType inputType, int vertexIndex)
                {
                    if (CheckAttribute(attribute))
                        throw new Exception($"Attribute \"{ attribute }\" is already in the vertex descriptor!");

                    Attributes.Add(attribute, new Tuple<VertexInputType, int>(inputType, vertexIndex));
                }

                public List<GXVertexAttribute> GetActiveAttributes() => new List<GXVertexAttribute>(Attributes.Keys);

                public int GetAttributeIndex(GXVertexAttribute attribute)
                {
                    if (CheckAttribute(attribute))
                        return Attributes[attribute].Item2;
                    else
                        throw new ArgumentException("attribute");
                }

                public VertexInputType GetAttributeType(GXVertexAttribute attribute)
                {
                    if (CheckAttribute(attribute))
                        return Attributes[attribute].Item1;
                    else
                        throw new ArgumentException("attribute");
                }

                public void Write(Stream writer)
                {
                    if (CheckAttribute(GXVertexAttribute.PositionMatrixIdx))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.PositionMatrixIdx), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.PositionMatrixIdx].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Position))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Position), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Position].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Normal))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Normal), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Normal].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Color0))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Color0), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Color0].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Color1))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Color1), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Color1].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Tex0))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Tex0), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Tex0].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Tex1))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Tex1), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Tex1].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Tex2))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Tex2), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Tex2].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Tex3))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Tex3), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Tex3].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Tex4))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Tex4), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Tex4].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Tex5))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Tex5), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Tex5].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Tex6))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Tex6), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Tex6].Item1), 0, 4);
                    }

                    if (CheckAttribute(GXVertexAttribute.Tex7))
                    {
                        writer.WriteReverse(BitConverter.GetBytes((int)GXVertexAttribute.Tex7), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((int)Attributes[GXVertexAttribute.Tex7].Item1), 0, 4);
                    }

                    // Null attribute
                    writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0xFF }, 0, 4);
                    writer.Write(new byte[4], 0, 4);
                }

                public override string ToString() => $"Descriptor: {Attributes.Count} Attributes";

                public override bool Equals(object obj)
                {
                    if (!(obj is ShapeVertexDescriptor descriptor) || Attributes.Count != descriptor.Attributes.Count)
                        return false;
                    foreach (KeyValuePair<GXVertexAttribute, Tuple<VertexInputType, int>> item in Attributes)
                    {
                        if (!descriptor.CheckAttribute(item.Key) || Attributes[item.Key].Item1 != descriptor.Attributes[item.Key].Item1 || Attributes[item.Key].Item2 != descriptor.Attributes[item.Key].Item2)
                            return false;
                    }
                    return true;
                }

                public override int GetHashCode()
                {
                    return -2135698220 + EqualityComparer<SortedDictionary<GXVertexAttribute, Tuple<VertexInputType, int>>>.Default.GetHashCode(Attributes);
                }

                public static bool operator ==(ShapeVertexDescriptor descriptor1, ShapeVertexDescriptor descriptor2)
                {
                    return descriptor1.Equals(descriptor2);
                }

                public static bool operator !=(ShapeVertexDescriptor descriptor1, ShapeVertexDescriptor descriptor2)
                {
                    return !(descriptor1 == descriptor2);
                }
            }

            public class Packet
            {
                public List<Primitive> Primitives { get; private set; }
                public short DRW1MatrixID { get; set; }
                public List<int> MatrixIndices { get; private set; }

                public Packet()
                {
                    Primitives = new List<Primitive>();
                    MatrixIndices = new List<int>();
                }

                public void ReadPrimitives(Stream reader, ShapeVertexDescriptor desc, long Location, int Size)
                {
                    reader.Position = Location;

                    while (true)
                    {
                        GXPrimitiveType type = (GXPrimitiveType)reader.PeekByte();
                        if (type == 0 || reader.Position >= Size + Location)
                            break;
                        Primitive prim = new Primitive(reader, desc);
                        Primitives.Add(prim);
                    }
                }

                public override string ToString() => $"Packet: {Primitives.Count} Primitives, {MatrixIndices.Count} MatrixIndicies";
            }

            public class Primitive
            {
                public GXPrimitiveType PrimitiveType { get; private set; }
                public List<Vertex> Vertices { get; private set; }

                public Primitive()
                {
                    PrimitiveType = GXPrimitiveType.Lines;
                    Vertices = new List<Vertex>();
                }

                public Primitive(GXPrimitiveType primType)
                {
                    PrimitiveType = primType;
                    Vertices = new List<Vertex>();
                }

                public Primitive(Stream reader, ShapeVertexDescriptor activeAttribs)
                {
                    Vertices = new List<Vertex>();

                    PrimitiveType = (GXPrimitiveType)(reader.ReadByte() & 0xF8);
                    int vertCount = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);

                    for (int i = 0; i < vertCount; i++)
                    {
                        Vertex vert = new Vertex();

                        foreach (GXVertexAttribute attrib in activeAttribs.Attributes.Keys)
                        {
                            switch (activeAttribs.GetAttributeType(attrib))
                            {
                                case VertexInputType.Direct:
                                    vert.SetAttributeIndex(attrib, attrib == GXVertexAttribute.PositionMatrixIdx ? (uint)(reader.ReadByte() / 3) : (byte)reader.ReadByte());
                                    break;
                                case VertexInputType.Index8:
                                    vert.SetAttributeIndex(attrib, (uint)reader.ReadByte());
                                    break;
                                case VertexInputType.Index16:
                                    ushort temp = BitConverter.ToUInt16(reader.ReadReverse(0, 2), 0);
                                    vert.SetAttributeIndex(attrib, temp);
                                    break;
                                case VertexInputType.None:
                                    throw new Exception("Found \"None\" as vertex input type in Primitive(reader, activeAttribs)!");
                            }
                        }

                        Vertices.Add(vert);
                    }
                }

                public void Write(Stream writer, ShapeVertexDescriptor desc)
                {
                    writer.WriteByte((byte)PrimitiveType);
                    writer.WriteReverse(BitConverter.GetBytes((short)Vertices.Count), 0, 2);

                    foreach (Vertex vert in Vertices)
                        vert.Write(writer, desc);
                }

                public override string ToString() => $"{PrimitiveType.ToString()}, {Vertices.Count} Vertices";
            }

            public class Vertex
            {
                public uint PositionMatrixIDxIndex { get; private set; }
                public uint PositionIndex { get; private set; }
                public uint NormalIndex { get; private set; }
                public uint Color0Index { get; private set; }
                public uint Color1Index { get; private set; }
                public uint TexCoord0Index { get; private set; }
                public uint TexCoord1Index { get; private set; }
                public uint TexCoord2Index { get; private set; }
                public uint TexCoord3Index { get; private set; }
                public uint TexCoord4Index { get; private set; }
                public uint TexCoord5Index { get; private set; }
                public uint TexCoord6Index { get; private set; }
                public uint TexCoord7Index { get; private set; }

                public uint Tex0MtxIndex { get; private set; }
                public uint Tex1MtxIndex { get; private set; }
                public uint Tex2MtxIndex { get; private set; }
                public uint Tex3MtxIndex { get; private set; }
                public uint Tex4MtxIndex { get; private set; }
                public uint Tex5MtxIndex { get; private set; }
                public uint Tex6MtxIndex { get; private set; }
                public uint Tex7MtxIndex { get; private set; }

                public uint PositionMatrixIndex { get; set; }
                public uint NormalMatrixIndex { get; set; }

                public EVP1.Weight VertexWeight { get; private set; } = new EVP1.Weight();

                public Vertex() { }

                public Vertex(Vertex src)
                {
                    // The position matrix index index is specific to the packet the vertex is in.
                    // So if copying a vertex across different packets, this value will be wrong and it needs to be recalculated manually.
                    PositionMatrixIDxIndex = src.PositionMatrixIDxIndex;

                    PositionIndex = src.PositionIndex;
                    NormalIndex = src.NormalIndex;
                    Color0Index = src.Color0Index;
                    Color1Index = src.Color1Index;
                    TexCoord0Index = src.TexCoord0Index;
                    TexCoord1Index = src.TexCoord1Index;
                    TexCoord2Index = src.TexCoord2Index;
                    TexCoord3Index = src.TexCoord3Index;
                    TexCoord4Index = src.TexCoord4Index;
                    TexCoord5Index = src.TexCoord5Index;
                    TexCoord6Index = src.TexCoord6Index;
                    TexCoord7Index = src.TexCoord7Index;

                    Tex0MtxIndex = src.Tex0MtxIndex;
                    Tex1MtxIndex = src.Tex1MtxIndex;
                    Tex2MtxIndex = src.Tex2MtxIndex;
                    Tex3MtxIndex = src.Tex3MtxIndex;
                    Tex4MtxIndex = src.Tex4MtxIndex;
                    Tex5MtxIndex = src.Tex5MtxIndex;
                    Tex6MtxIndex = src.Tex6MtxIndex;
                    Tex7MtxIndex = src.Tex7MtxIndex;

                    VertexWeight = src.VertexWeight;
                }

                public uint GetAttributeIndex(GXVertexAttribute attribute)
                {
                    switch (attribute)
                    {
                        case GXVertexAttribute.PositionMatrixIdx:
                            return PositionMatrixIDxIndex;
                        case GXVertexAttribute.Position:
                            return PositionIndex;
                        case GXVertexAttribute.Normal:
                            return NormalIndex;
                        case GXVertexAttribute.Color0:
                            return Color0Index;
                        case GXVertexAttribute.Color1:
                            return Color1Index;
                        case GXVertexAttribute.Tex0:
                            return TexCoord0Index;
                        case GXVertexAttribute.Tex1:
                            return TexCoord1Index;
                        case GXVertexAttribute.Tex2:
                            return TexCoord2Index;
                        case GXVertexAttribute.Tex3:
                            return TexCoord3Index;
                        case GXVertexAttribute.Tex4:
                            return TexCoord4Index;
                        case GXVertexAttribute.Tex5:
                            return TexCoord5Index;
                        case GXVertexAttribute.Tex6:
                            return TexCoord6Index;
                        case GXVertexAttribute.Tex7:
                            return TexCoord7Index;
                        case GXVertexAttribute.Tex0Mtx:
                            return Tex0MtxIndex;
                        case GXVertexAttribute.Tex1Mtx:
                            return Tex1MtxIndex;
                        case GXVertexAttribute.Tex2Mtx:
                            return Tex2MtxIndex;
                        case GXVertexAttribute.Tex3Mtx:
                            return Tex3MtxIndex;
                        case GXVertexAttribute.Tex4Mtx:
                            return Tex4MtxIndex;
                        case GXVertexAttribute.Tex5Mtx:
                            return Tex5MtxIndex;
                        case GXVertexAttribute.Tex6Mtx:
                            return Tex6MtxIndex;
                        case GXVertexAttribute.Tex7Mtx:
                            return Tex7MtxIndex;
                        default:
                            throw new ArgumentException(String.Format("attribute {0}", attribute));
                    }
                }

                public void SetAttributeIndex(GXVertexAttribute attribute, uint index)
                {
                    switch (attribute)
                    {
                        case GXVertexAttribute.PositionMatrixIdx:
                            PositionMatrixIDxIndex = index;
                            break;
                        case GXVertexAttribute.Position:
                            PositionIndex = index;
                            break;
                        case GXVertexAttribute.Normal:
                            NormalIndex = index;
                            break;
                        case GXVertexAttribute.Color0:
                            Color0Index = index;
                            break;
                        case GXVertexAttribute.Color1:
                            Color1Index = index;
                            break;
                        case GXVertexAttribute.Tex0:
                            TexCoord0Index = index;
                            break;
                        case GXVertexAttribute.Tex1:
                            TexCoord1Index = index;
                            break;
                        case GXVertexAttribute.Tex2:
                            TexCoord2Index = index;
                            break;
                        case GXVertexAttribute.Tex3:
                            TexCoord3Index = index;
                            break;
                        case GXVertexAttribute.Tex4:
                            TexCoord4Index = index;
                            break;
                        case GXVertexAttribute.Tex5:
                            TexCoord5Index = index;
                            break;
                        case GXVertexAttribute.Tex6:
                            TexCoord6Index = index;
                            break;
                        case GXVertexAttribute.Tex7:
                            TexCoord7Index = index;
                            break;
                        case GXVertexAttribute.Tex0Mtx:
                            Tex0MtxIndex = index;
                            break;
                        case GXVertexAttribute.Tex1Mtx:
                            Tex1MtxIndex = index;
                            break;
                        case GXVertexAttribute.Tex2Mtx:
                            Tex2MtxIndex = index;
                            break;
                        case GXVertexAttribute.Tex3Mtx:
                            Tex3MtxIndex = index;
                            break;
                        case GXVertexAttribute.Tex4Mtx:
                            Tex4MtxIndex = index;
                            break;
                        case GXVertexAttribute.Tex5Mtx:
                            Tex5MtxIndex = index;
                            break;
                        case GXVertexAttribute.Tex6Mtx:
                            Tex6MtxIndex = index;
                            break;
                        case GXVertexAttribute.Tex7Mtx:
                            Tex7MtxIndex = index;
                            break;
                        default:
                            throw new ArgumentException(String.Format("attribute {0}", attribute));
                    }
                }

                public void SetWeight(EVP1.Weight weight)
                {
                    VertexWeight = weight;
                }

                public void Write(Stream writer, ShapeVertexDescriptor desc)
                {
                    if (desc.CheckAttribute(GXVertexAttribute.PositionMatrixIdx))
                    {
                        WriteAttributeIndex(writer, PositionMatrixIDxIndex * 3, desc.Attributes[GXVertexAttribute.PositionMatrixIdx].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Position))
                    {
                        WriteAttributeIndex(writer, PositionIndex, desc.Attributes[GXVertexAttribute.Position].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Normal))
                    {
                        WriteAttributeIndex(writer, NormalIndex, desc.Attributes[GXVertexAttribute.Normal].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Color0))
                    {
                        WriteAttributeIndex(writer, Color0Index, desc.Attributes[GXVertexAttribute.Color0].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Color1))
                    {
                        WriteAttributeIndex(writer, Color1Index, desc.Attributes[GXVertexAttribute.Color1].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Tex0))
                    {
                        WriteAttributeIndex(writer, TexCoord0Index, desc.Attributes[GXVertexAttribute.Tex0].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Tex1))
                    {
                        WriteAttributeIndex(writer, TexCoord1Index, desc.Attributes[GXVertexAttribute.Tex1].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Tex2))
                    {
                        WriteAttributeIndex(writer, TexCoord2Index, desc.Attributes[GXVertexAttribute.Tex2].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Tex3))
                    {
                        WriteAttributeIndex(writer, TexCoord3Index, desc.Attributes[GXVertexAttribute.Tex3].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Tex4))
                    {
                        WriteAttributeIndex(writer, TexCoord4Index, desc.Attributes[GXVertexAttribute.Tex4].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Tex5))
                    {
                        WriteAttributeIndex(writer, TexCoord5Index, desc.Attributes[GXVertexAttribute.Tex5].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Tex6))
                    {
                        WriteAttributeIndex(writer, TexCoord6Index, desc.Attributes[GXVertexAttribute.Tex6].Item1);
                    }

                    if (desc.CheckAttribute(GXVertexAttribute.Tex7))
                    {
                        WriteAttributeIndex(writer, TexCoord7Index, desc.Attributes[GXVertexAttribute.Tex7].Item1);
                    }
                }

                private void WriteAttributeIndex(Stream writer, uint value, VertexInputType type)
                {
                    switch (type)
                    {
                        case VertexInputType.Direct:
                        case VertexInputType.Index8:
                            writer.WriteByte((byte)value);
                            break;
                        case VertexInputType.Index16:
                            writer.WriteReverse(BitConverter.GetBytes((short)value), 0, 2);
                            break;
                        case VertexInputType.None:
                        default:
                            throw new ArgumentException("vertex input type");
                    }
                }
            }

            public class BoundingVolume
            {
                public float SphereRadius { get; private set; }
                public Vector3 MinBounds { get; private set; }
                public Vector3 MaxBounds { get; private set; }

                public Vector3 Center => (MaxBounds + MinBounds) / 2;

                public BoundingVolume()
                {
                    MinBounds = new Vector3();
                    MaxBounds = new Vector3();
                }

                public BoundingVolume(Stream BMD)
                {
                    SphereRadius = BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0);

                    MinBounds = new Vector3(BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0));
                    MaxBounds = new Vector3(BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0), BitConverter.ToSingle(BMD.ReadReverse(0, 4), 0));
                }

                public void GetBoundsValues(List<Vector3> positions)
                {
                    float minX = float.MaxValue;
                    float minY = float.MaxValue;
                    float minZ = float.MaxValue;

                    float maxX = float.MinValue;
                    float maxY = float.MinValue;
                    float maxZ = float.MinValue;

                    foreach (Vector3 vec in positions)
                    {
                        if (vec.X > maxX)
                            maxX = vec.X;
                        if (vec.Y > maxY)
                            maxY = vec.Y;
                        if (vec.Z > maxZ)
                            maxZ = vec.Z;

                        if (vec.X < minX)
                            minX = vec.X;
                        if (vec.Y < minY)
                            minY = vec.Y;
                        if (vec.Z < minZ)
                            minZ = vec.Z;
                    }

                    MinBounds = new Vector3(minX, minY, minZ);
                    MaxBounds = new Vector3(maxX, maxY, maxZ);
                    SphereRadius = (MaxBounds - Center).Length;
                }

                public void Write(Stream writer)
                {
                    writer.WriteReverse(BitConverter.GetBytes(SphereRadius), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(MinBounds.X), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(MinBounds.Y), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(MinBounds.Z), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(MaxBounds.X), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(MaxBounds.Y), 0, 4);
                    writer.WriteReverse(BitConverter.GetBytes(MaxBounds.Z), 0, 4);
                }

                public override string ToString() => $"Min: {MinBounds.ToString()}, Max: {MaxBounds.ToString()}, Radius: {SphereRadius.ToString()}, Center: {Center.ToString()}";
            }
        }
        public class MAT3
        {
            #region Fields and Properties
            private List<Material> m_Materials;
            public Material this[string MaterialName]
            {
                get
                {
                    for (int i = 0; i < m_Materials.Count; i++)
                    {
                        if (m_Materials[i].Name.Equals(MaterialName))
                            return m_Materials[i];
                    }
                    return null;
                }
                set
                {
                    if (!(value is Material || value is null))
                        throw new ArgumentException("Value is not a Material!", "value");
                    for (int i = 0; i < m_Materials.Count; i++)
                    {
                        if (m_Materials[i].Name.Equals(MaterialName))
                        {
                            if (value is null)
                                m_Materials.RemoveAt(i);
                            else
                                m_Materials[i] = value;
                            return;
                        }
                    }
                    if (!(value is null))
                        m_Materials.Add(value);
                }
            }
            public Material this[int Index]
            {
                get
                {
                    return m_Materials[Index];
                }
                set
                {
                    if (!(value is Material || value is null))
                        throw new ArgumentException("Value is not a Material!", "value");

                    m_Materials[Index] = value;
                }
            }

            public int Count => m_Materials.Count;
            #endregion

            private static readonly string Magic = "MAT3";

            public MAT3(Stream reader)
            {
                List<int> m_RemapIndices = new List<int>();
                List<string> m_MaterialNames = new List<string>();

                List<Material.IndirectTexturing> m_IndirectTexBlock = new List<Material.IndirectTexturing>();
                List<CullMode> m_CullModeBlock = new List<CullMode>();
                List<Color4> m_MaterialColorBlock = new List<Color4>();
                List<Material.ChannelControl> m_ChannelControlBlock = new List<Material.ChannelControl>();
                List<Color4> m_AmbientColorBlock = new List<Color4>();
                List<Color4> m_LightingColorBlock = new List<Color4>();
                List<Material.TexCoordGen> m_TexCoord1GenBlock = new List<Material.TexCoordGen>();
                List<Material.TexCoordGen> m_TexCoord2GenBlock = new List<Material.TexCoordGen>();
                List<Material.TexMatrix> m_TexMatrix1Block = new List<Material.TexMatrix>();
                List<Material.TexMatrix> m_TexMatrix2Block = new List<Material.TexMatrix>();
                List<short> m_TexRemapBlock = new List<short>();
                List<Material.TevOrder> m_TevOrderBlock = new List<Material.TevOrder>();
                List<Color4> m_TevColorBlock = new List<Color4>();
                List<Color4> m_TevKonstColorBlock = new List<Color4>();
                List<Material.TevStage> m_TevStageBlock = new List<Material.TevStage>();
                List<Material.TevSwapMode> m_SwapModeBlock = new List<Material.TevSwapMode>();
                List<Material.TevSwapModeTable> m_SwapTableBlock = new List<Material.TevSwapModeTable>();
                List<Material.Fog> m_FogBlock = new List<Material.Fog>();
                List<Material.AlphaCompare> m_AlphaCompBlock = new List<Material.AlphaCompare>();
                List<Material.BlendMode> m_blendModeBlock = new List<Material.BlendMode>();
                List<Material.NBTScaleHolder> m_NBTScaleBlock = new List<Material.NBTScaleHolder>();

                List<Material.ZModeHolder> m_zModeBlock = new List<Material.ZModeHolder>();
                List<bool> m_zCompLocBlock = new List<bool>();
                List<bool> m_ditherBlock = new List<bool>();

                List<byte> NumColorChannelsBlock = new List<byte>();
                List<byte> NumTexGensBlock = new List<byte>();
                List<byte> NumTevStagesBlock = new List<byte>();


                int ChunkStart = (int)reader.Position;
                if (!reader.ReadString(4).Equals(Magic))
                    throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

                int mat3Size = BitConverter.ToInt32(reader.ReadReverse(0, 4), 0);
                int matCount = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                long matInitOffset = 0;
                reader.Position += 0x02;

                for (Mat3OffsetIndex i = 0; i <= Mat3OffsetIndex.NBTScaleData; ++i)
                {
                    int sectionOffset = BitConverter.ToInt32(reader.ReadReverse(0, 4), 0);

                    if (sectionOffset == 0)
                        continue;

                    long curReaderPos = reader.Position;
                    int nextOffset = BitConverter.ToInt32(reader.ReadReverse(0, 4), 0);
                    reader.Position -= 0x04;
                    int sectionSize = 0;

                    if (i == Mat3OffsetIndex.NBTScaleData)
                    {

                    }

                    if (nextOffset == 0 && i != Mat3OffsetIndex.NBTScaleData)
                    {
                        long saveReaderPos = reader.Position;

                        reader.Position += 4;

                        while (BitConverter.ToInt32(reader.ReadReverse(0, 4), 0) == 0)
                            reader.Position += 0;

                        reader.Position -= 0x04;
                        nextOffset = BitConverter.ToInt32(reader.ReadReverse(0, 4), 0);
                        reader.Position -= 0x04;
                        sectionSize = nextOffset - sectionOffset;

                        reader.Position = saveReaderPos;
                    }
                    else if (i == Mat3OffsetIndex.NBTScaleData)
                        sectionSize = mat3Size - sectionOffset;
                    else
                        sectionSize = nextOffset - sectionOffset;

                    reader.Position = ChunkStart + sectionOffset;
                    int count;
                    switch (i)
                    {
                        case Mat3OffsetIndex.MaterialData:
                            matInitOffset = reader.Position;
                            break;
                        case Mat3OffsetIndex.IndexData:
                            m_RemapIndices = new List<int>();

                            for (int index = 0; index < matCount; index++)
                                m_RemapIndices.Add(BitConverter.ToInt16(reader.ReadReverse(0, 2), 0));

                            break;
                        case Mat3OffsetIndex.NameTable:
                            m_MaterialNames = new List<string>();

                            reader.Position = ChunkStart + sectionOffset;

                            short stringCount = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                            reader.Position += 0x02;

                            for (int y = 0; y < stringCount; y++)
                            {
                                reader.Position += 0x02;
                                short nameOffset = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                                long saveReaderPos = reader.Position;
                                reader.Position = ChunkStart + sectionOffset + nameOffset;

                                m_MaterialNames.Add(reader.ReadString());

                                reader.Position = saveReaderPos;
                            }
                            break;
                        case Mat3OffsetIndex.IndirectData:
                            m_IndirectTexBlock = new List<Material.IndirectTexturing>();
                            count = sectionSize / 312;

                            for (int y = 0; y < count; y++)
                                m_IndirectTexBlock.Add(new Material.IndirectTexturing(reader));
                            break;
                        case Mat3OffsetIndex.CullMode:
                            m_CullModeBlock = new List<CullMode>();
                            count = sectionSize / 4;

                            for (int y = 0; y < count; y++)
                                m_CullModeBlock.Add((CullMode)BitConverter.ToInt32(reader.ReadReverse(0, 4), 0));
                            break;
                        case Mat3OffsetIndex.MaterialColor:
                            m_MaterialColorBlock = ReadColours(reader, sectionOffset, sectionSize);
                            break;
                        case Mat3OffsetIndex.ColorChannelCount:
                            NumColorChannelsBlock = new List<byte>();

                            for (int chanCnt = 0; chanCnt < sectionSize; chanCnt++)
                            {
                                byte chanCntIn = (byte)reader.ReadByte();

                                if (chanCntIn < 84)
                                    NumColorChannelsBlock.Add(chanCntIn);
                            }

                            break;
                        case Mat3OffsetIndex.ColorChannelData:
                            m_ChannelControlBlock = new List<Material.ChannelControl>();
                            count = sectionSize / 8;

                            for (int y = 0; y < count; y++)
                                m_ChannelControlBlock.Add(new Material.ChannelControl(reader));
                            break;
                        case Mat3OffsetIndex.AmbientColorData:
                            m_AmbientColorBlock = ReadColours(reader, sectionOffset, sectionSize);
                            break;
                        case Mat3OffsetIndex.LightData:
                            m_LightingColorBlock = ReadColours(reader, sectionOffset, sectionSize);
                            break;
                        case Mat3OffsetIndex.TexGenCount:
                            NumTexGensBlock = new List<byte>();

                            for (int genCnt = 0; genCnt < sectionSize; genCnt++)
                            {
                                byte genCntIn = (byte)reader.ReadByte();

                                if (genCntIn < 84)
                                    NumTexGensBlock.Add(genCntIn);
                            }

                            break;
                        case Mat3OffsetIndex.TexCoordData:
                            m_TexCoord1GenBlock = ReadTexCoordGens(reader, sectionOffset, sectionSize);
                            break;
                        case Mat3OffsetIndex.TexCoord2Data:
                            m_TexCoord2GenBlock = ReadTexCoordGens(reader, sectionOffset, sectionSize);
                            break;
                        case Mat3OffsetIndex.TexMatrixData:
                            m_TexMatrix1Block = ReadTexMatrices(reader, sectionOffset, sectionSize);
                            break;
                        case Mat3OffsetIndex.TexMatrix2Data:
                            m_TexMatrix2Block = ReadTexMatrices(reader, sectionOffset, sectionSize);
                            break;
                        case Mat3OffsetIndex.TexNoData:
                            m_TexRemapBlock = new List<short>();
                            int texNoCnt = sectionSize / 2;

                            for (int texNo = 0; texNo < texNoCnt; texNo++)
                                m_TexRemapBlock.Add(BitConverter.ToInt16(reader.ReadReverse(0, 2), 0));

                            break;
                        case Mat3OffsetIndex.TevOrderData:
                            m_TevOrderBlock = new List<Material.TevOrder>();
                            count = sectionSize / 4;

                            for (int y = 0; y < count; y++)
                                m_TevOrderBlock.Add(new Material.TevOrder(reader));
                            break;
                        case Mat3OffsetIndex.TevColorData:
                            m_TevColorBlock = ReadColours(reader, sectionOffset, sectionSize, true);
                            break;
                        case Mat3OffsetIndex.TevKColorData:
                            m_TevKonstColorBlock = ReadColours(reader, sectionOffset, sectionSize);
                            break;
                        case Mat3OffsetIndex.TevStageCount:
                            NumTevStagesBlock = new List<byte>();

                            for (int stgCnt = 0; stgCnt < sectionSize; stgCnt++)
                            {
                                byte stgCntIn = (byte)reader.ReadByte();

                                if (stgCntIn < 84)
                                    NumTevStagesBlock.Add(stgCntIn);
                            }

                            break;
                        case Mat3OffsetIndex.TevStageData:
                            m_TevStageBlock = new List<Material.TevStage>();
                            count = sectionSize / 20;

                            for (int y = 0; y < count; y++)
                                m_TevStageBlock.Add(new Material.TevStage(reader));
                            break;
                        case Mat3OffsetIndex.TevSwapModeData:
                            m_SwapModeBlock = new List<Material.TevSwapMode>();
                            count = sectionSize / 4;

                            for (int y = 0; y < count; y++)
                                m_SwapModeBlock.Add(new Material.TevSwapMode(reader));
                            break;
                        case Mat3OffsetIndex.TevSwapModeTable:
                            m_SwapTableBlock = new List<Material.TevSwapModeTable>();
                            count = sectionSize / 4;

                            for (int y = 0; y < count; y++)
                                m_SwapTableBlock.Add(new Material.TevSwapModeTable(reader));
                            break;
                        case Mat3OffsetIndex.FogData:
                            m_FogBlock = new List<Material.Fog>();
                            count = sectionSize / 44;

                            for (int y = 0; y < count; y++)
                                m_FogBlock.Add(new Material.Fog(reader));
                            break;
                        case Mat3OffsetIndex.AlphaCompareData:
                            m_AlphaCompBlock = new List<Material.AlphaCompare>();
                            count = sectionSize / 8;

                            for (int y = 0; y < count; y++)
                                m_AlphaCompBlock.Add(new Material.AlphaCompare(reader));
                            break;
                        case Mat3OffsetIndex.BlendData:
                            m_blendModeBlock = new List<Material.BlendMode>();
                            count = sectionSize / 4;

                            for (int y = 0; y < count; y++)
                                m_blendModeBlock.Add(new Material.BlendMode(reader));
                            break;
                        case Mat3OffsetIndex.ZModeData:
                            m_zModeBlock = new List<Material.ZModeHolder>();
                            count = sectionSize / 4;

                            for (int y = 0; y < count; y++)
                                m_zModeBlock.Add(new Material.ZModeHolder(reader));
                            break;
                        case Mat3OffsetIndex.ZCompLoc:
                            m_zCompLocBlock = new List<bool>();

                            for (int zcomp = 0; zcomp < sectionSize; zcomp++)
                            {
                                byte boolIn = (byte)reader.ReadByte();

                                if (boolIn > 1)
                                    break;

                                m_zCompLocBlock.Add(Convert.ToBoolean(boolIn));
                            }

                            break;
                        case Mat3OffsetIndex.DitherData:
                            m_ditherBlock = new List<bool>();

                            for (int dith = 0; dith < sectionSize; dith++)
                            {
                                byte boolIn = (byte)reader.ReadByte();

                                if (boolIn > 1)
                                    break;

                                m_ditherBlock.Add(Convert.ToBoolean(boolIn));
                            }

                            break;
                        case Mat3OffsetIndex.NBTScaleData:
                            m_NBTScaleBlock = new List<Material.NBTScaleHolder>();
                            count = sectionSize / 16;

                            for (int y = 0; y < count; y++)
                                m_NBTScaleBlock.Add(new Material.NBTScaleHolder(reader));
                            break;
                    }

                    reader.Position = curReaderPos;
                }

                int highestMatIndex = 0;

                for (int i = 0; i < matCount; i++)
                {
                    if (m_RemapIndices[i] > highestMatIndex)
                        highestMatIndex = m_RemapIndices[i];
                }

                reader.Position = matInitOffset;
                m_Materials = new List<Material>();
                for (int i = 0; i <= highestMatIndex; i++)
                {
                    LoadInitData(reader, m_RemapIndices[i], m_MaterialNames, m_IndirectTexBlock, m_CullModeBlock, m_MaterialColorBlock,  m_ChannelControlBlock,  m_AmbientColorBlock,
                        m_LightingColorBlock, m_TexCoord1GenBlock, m_TexCoord2GenBlock, m_TexMatrix1Block, m_TexMatrix2Block, m_TexRemapBlock, m_TevOrderBlock, m_TevColorBlock,
                        m_TevKonstColorBlock, m_TevStageBlock, m_SwapModeBlock, m_SwapTableBlock, m_FogBlock, m_AlphaCompBlock, m_blendModeBlock, m_NBTScaleBlock,  m_zModeBlock,
                        m_zCompLocBlock, m_ditherBlock, NumColorChannelsBlock, NumTexGensBlock, NumTevStagesBlock);
                }

                reader.Seek(ChunkStart + mat3Size, SeekOrigin.Begin);

                List<Material> matCopies = new List<Material>();
                for (int i = 0; i < m_RemapIndices.Count; i++)
                {
                    Material originalMat = m_Materials[m_RemapIndices[i]];
                    Material copyMat = new Material(originalMat) { Name = m_MaterialNames[i] };
                    matCopies.Add(copyMat);
                }

                m_Materials = matCopies;
            }

            public bool Any(Func<Material, bool> predicate)
            {
                if (predicate == null)
                    throw new ArgumentException("predicate");
                foreach (Material element in m_Materials)
                    if (predicate(element))
                        return true;
                return false;
            }

            public void SetTextureNames(TEX1 textures)
            {
                foreach (Material mat in m_Materials)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (mat.TextureIndices[i] == -1)
                            continue;

                        mat.Textures[i] = textures[mat.TextureIndices[i]];
                        //mat.TextureNames[i] = textures[mat.TextureIndices[i]].FileName;
                        //mat.TextureNames[i] = textures.getTextureInstanceName(mat.TextureIndices[i]);
                    }
                }
            }

            #region I/O
            private void LoadInitData(Stream reader, int matindex, List<string> m_MaterialNames, List<Material.IndirectTexturing> m_IndirectTexBlock, List<CullMode> m_CullModeBlock,
            List<Color4> m_MaterialColorBlock, List<Material.ChannelControl> m_ChannelControlBlock, List<Color4> m_AmbientColorBlock, List<Color4> m_LightingColorBlock,
            List<Material.TexCoordGen> m_TexCoord1GenBlock, List<Material.TexCoordGen> m_TexCoord2GenBlock, List<Material.TexMatrix> m_TexMatrix1Block, List<Material.TexMatrix> m_TexMatrix2Block,
            List<short> m_TexRemapBlock, List<Material.TevOrder> m_TevOrderBlock, List<Color4> m_TevColorBlock, List<Color4> m_TevKonstColorBlock, List<Material.TevStage> m_TevStageBlock,
            List<Material.TevSwapMode> m_SwapModeBlock, List<Material.TevSwapModeTable> m_SwapTableBlock, List<Material.Fog> m_FogBlock, List<Material.AlphaCompare> m_AlphaCompBlock,
            List<Material.BlendMode> m_blendModeBlock, List<Material.NBTScaleHolder> m_NBTScaleBlock, List<Material.ZModeHolder> m_zModeBlock, List<bool> m_zCompLocBlock,
            List<bool> m_ditherBlock, List<byte> NumColorChannelsBlock, List<byte> NumTexGensBlock, List<byte> NumTevStagesBlock)
            {
                Material mat = new Material
                {
                    Name = m_MaterialNames[matindex],
                    Flag = (byte)reader.ReadByte(),
                    CullMode = m_CullModeBlock[reader.ReadByte()],
                    LightChannelCount = NumColorChannelsBlock[reader.ReadByte()]
                };
                reader.Position += 0x02;

                if (matindex < m_IndirectTexBlock.Count)
                {
                    mat.IndTexEntry = m_IndirectTexBlock[matindex];
                }
                else
                {
                    Console.WriteLine("Warning: Material {0} referenced an out of range IndirectTexBlock index", mat.Name);
                }
                mat.ZCompLoc = m_zCompLocBlock[reader.ReadByte()];
                mat.ZMode = m_zModeBlock[reader.ReadByte()];

                if (m_ditherBlock == null || m_ditherBlock.Count == 0)
                    reader.Position++;
                else
                    mat.Dither = m_ditherBlock[reader.ReadByte()];

                int matColorIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                if (matColorIndex != -1)
                    mat.MaterialColors[0] = m_MaterialColorBlock[matColorIndex];
                matColorIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                if (matColorIndex != -1)
                    mat.MaterialColors[1] = m_MaterialColorBlock[matColorIndex];

                for (int i = 0; i < 4; i++)
                {
                    int chanIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (chanIndex == -1)
                        continue;
                    else if (chanIndex < m_ChannelControlBlock.Count)
                    {
                        mat.ChannelControls[i] = m_ChannelControlBlock[chanIndex];
                    }
                    else
                    {
                        Console.WriteLine(string.Format("Warning for material {0} i={2}, color channel index out of range: {1}", mat.Name, chanIndex, i));
                    }
                }
                for (int i = 0; i < 2; i++)
                {
                    int ambColorIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (ambColorIndex == -1)
                        continue;
                    else if (ambColorIndex < m_AmbientColorBlock.Count)
                    {
                        mat.AmbientColors[i] = m_AmbientColorBlock[ambColorIndex];
                    }
                    else
                    {
                        Console.WriteLine(string.Format("Warning for material {0} i={2}, ambient color index out of range: {1}", mat.Name, ambColorIndex, i));
                    }
                }

                for (int i = 0; i < 8; i++)
                {
                    int lightIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if ((lightIndex == -1) || (lightIndex > m_LightingColorBlock.Count) || (m_LightingColorBlock.Count == 0))
                        continue;
                    else
                        mat.LightingColors[i] = m_LightingColorBlock[lightIndex];
                }

                for (int i = 0; i < 8; i++)
                {
                    int texGenIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (texGenIndex == -1)
                        continue;
                    else if (texGenIndex < m_TexCoord1GenBlock.Count)
                        mat.TexCoord1Gens[i] = m_TexCoord1GenBlock[texGenIndex];
                    else
                        Console.WriteLine(string.Format("Warning for material {0} i={2}, TexCoord1GenBlock index out of range: {1}", mat.Name, texGenIndex, i));
                }

                for (int i = 0; i < 8; i++)
                {
                    int texGenIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (texGenIndex == -1)
                        continue;
                    else
                        mat.PostTexCoordGens[i] = m_TexCoord2GenBlock[texGenIndex];
                }

                for (int i = 0; i < 10; i++)
                {
                    int texMatIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (texMatIndex == -1)
                        continue;
                    else
                        mat.TexMatrix1[i] = m_TexMatrix1Block[texMatIndex];
                }

                for (int i = 0; i < 20; i++)
                {
                    int texMatIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (texMatIndex == -1)
                        continue;
                    else if (texMatIndex < (m_TexMatrix2Block?.Count ?? 0))
                        mat.PostTexMatrix[i] = m_TexMatrix2Block[texMatIndex];
                    else
                        Console.WriteLine(string.Format("Warning for material {0}, TexMatrix2Block index out of range: {1}", mat.Name, texMatIndex));
                }

                for (int i = 0; i < 8; i++)
                {
                    int texIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (texIndex == -1)
                        continue;
                    else
                        mat.TextureIndices[i] = m_TexRemapBlock[texIndex];
                }

                for (int i = 0; i < 4; i++)
                {
                    int tevKColor = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (tevKColor == -1)
                        continue;
                    else
                        mat.KonstColors[i] = m_TevKonstColorBlock[tevKColor];
                }

                for (int i = 0; i < 16; i++)
                {
                    mat.ColorSels[i] = (KonstColorSel)reader.ReadByte();
                }

                for (int i = 0; i < 16; i++)
                {
                    mat.AlphaSels[i] = (KonstAlphaSel)reader.ReadByte();
                }

                for (int i = 0; i < 16; i++)
                {
                    int tevOrderIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (tevOrderIndex == -1)
                        continue;
                    else
                        mat.TevOrders[i] = m_TevOrderBlock[tevOrderIndex];
                }

                for (int i = 0; i < 4; i++)
                {
                    int tevColor = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (tevColor == -1)
                        continue;
                    else
                        mat.TevColors[i] = m_TevColorBlock[tevColor];
                }

                for (int i = 0; i < 16; i++)
                {
                    int tevStageIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (tevStageIndex == -1)
                        continue;
                    else
                        mat.TevStages[i] = m_TevStageBlock[tevStageIndex];
                }

                for (int i = 0; i < 16; i++)
                {
                    int tevSwapModeIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if (tevSwapModeIndex == -1)
                        continue;
                    else
                        mat.SwapModes[i] = m_SwapModeBlock[tevSwapModeIndex];
                }

                for (int i = 0; i < 16; i++)
                {
                    int tevSwapModeTableIndex = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                    if ((tevSwapModeTableIndex < 0) || (tevSwapModeTableIndex >= m_SwapTableBlock.Count))
                        continue;
                    else
                    {
                        if (tevSwapModeTableIndex >= m_SwapTableBlock.Count)
                            continue;

                        mat.SwapTables[i] = m_SwapTableBlock[tevSwapModeTableIndex];
                    }
                }

                if (m_FogBlock.Count == 0)
                    reader.Position += 0x02;
                else
                    mat.FogInfo = m_FogBlock[BitConverter.ToInt16(reader.ReadReverse(0, 2), 0)];
                mat.AlphCompare = m_AlphaCompBlock[BitConverter.ToInt16(reader.ReadReverse(0, 2), 0)];
                mat.BMode = m_blendModeBlock[BitConverter.ToInt16(reader.ReadReverse(0, 2), 0)];

                if (m_NBTScaleBlock.Count == 0)
                    reader.Position += 0x02;
                else
                    mat.NBTScale = m_NBTScaleBlock[BitConverter.ToInt16(reader.ReadReverse(0, 2), 0)];
                m_Materials.Add(mat);
            }
            private static List<Color4> ReadColours(Stream reader, int offset, int size, bool IsInt16 = false)
            {
                List<Color4> colors = new List<Color4>();
                int count = size / (IsInt16 ? 8 : 4);

                if (IsInt16)
                {
                    for (int i = 0; i < count; i++)
                    {
                        short r = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                        short g = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                        short b = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                        short a = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);

                        colors.Add(new Color4((float)r / 255, (float)g / 255, (float)b / 255, (float)a / 255));
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        byte r = (byte)reader.ReadByte();
                        byte g = (byte)reader.ReadByte();
                        byte b = (byte)reader.ReadByte();
                        byte a = (byte)reader.ReadByte();

                        colors.Add(new Color4((float)r / 255, (float)g / 255, (float)b / 255, (float)a / 255));
                    }
                }


                return colors;
            }
            private static List<Material.TexCoordGen> ReadTexCoordGens(Stream reader, int offset, int size)
            {
                List<Material.TexCoordGen> gens = new List<Material.TexCoordGen>();
                int count = size / 4;

                for (int i = 0; i < count; i++)
                    gens.Add(new Material.TexCoordGen(reader));

                return gens;
            }
            private static List<Material.TexMatrix> ReadTexMatrices(Stream reader, int offset, int size)
            {
                List<Material.TexMatrix> matrices = new List<Material.TexMatrix>();
                int count = size / 100;

                for (int i = 0; i < count; i++)
                    matrices.Add(new Material.TexMatrix(reader));

                return matrices;
            }

            public void Write(Stream writer)
            {
                long start = writer.Position;
                List<int> m_RemapIndices = new List<int>();
                List<string> m_MaterialNames = new List<string>();

                List<Material.IndirectTexturing> m_IndirectTexBlock = new List<Material.IndirectTexturing>();
                List<CullMode> m_CullModeBlock = new List<CullMode>() { CullMode.Back, CullMode.Front, CullMode.None };
                List<Color4> m_MaterialColorBlock = new List<Color4>();
                List<Material.ChannelControl> m_ChannelControlBlock = new List<Material.ChannelControl>();
                List<Color4> m_AmbientColorBlock = new List<Color4>();
                List<Color4> m_LightingColorBlock = new List<Color4>();
                List<Material.TexCoordGen> m_TexCoord1GenBlock = new List<Material.TexCoordGen>();
                List<Material.TexCoordGen> m_TexCoord2GenBlock = new List<Material.TexCoordGen>();
                List<Material.TexMatrix> m_TexMatrix1Block = new List<Material.TexMatrix>();
                List<Material.TexMatrix> m_TexMatrix2Block = new List<Material.TexMatrix>();
                List<short> m_TexRemapBlock = new List<short>();
                List<Material.TevOrder> m_TevOrderBlock = new List<Material.TevOrder>();
                List<Color4> m_TevColorBlock = new List<Color4>();
                List<Color4> m_TevKonstColorBlock = new List<Color4>();
                List<Material.TevStage> m_TevStageBlock = new List<Material.TevStage>();
                List<Material.TevSwapMode> m_SwapModeBlock = new List<Material.TevSwapMode>() { new Material.TevSwapMode(0,0), new Material.TevSwapMode(0,0) };
                List<Material.TevSwapModeTable> m_SwapTableBlock = new List<Material.TevSwapModeTable>();
                List<Material.Fog> m_FogBlock = new List<Material.Fog>();
                List<Material.AlphaCompare> m_AlphaCompBlock = new List<Material.AlphaCompare>();
                List<Material.BlendMode> m_blendModeBlock = new List<Material.BlendMode>();
                List<Material.NBTScaleHolder> m_NBTScaleBlock = new List<Material.NBTScaleHolder>();

                List<Material.ZModeHolder> m_zModeBlock = new List<Material.ZModeHolder>();
                List<bool> m_zCompLocBlock = new List<bool>() { false, true };
                List<bool> m_ditherBlock = new List<bool>() { false, true };

                List<byte> NumColorChannelsBlock = new List<byte>();
                List<byte> NumTexGensBlock = new List<byte>();
                List<byte> NumTevStagesBlock = new List<byte>();

                // Calculate what the unique materials are and update the duplicate remap indices list.
                List<Material> uniqueMaterials = new List<Material>();
                for (int i = 0; i < m_Materials.Count; i++)
                {
                    Material mat = m_Materials[i];
                    int duplicateRemapIndex = -1;
                    for (int j = 0; j < i; j++)
                    {
                        Material othermat = m_Materials[j];
                        if (mat == othermat)
                        {
                            duplicateRemapIndex = uniqueMaterials.IndexOf(othermat);
                            break;
                        }
                    }
                    if (duplicateRemapIndex >= 0)
                        m_RemapIndices.Add(duplicateRemapIndex);
                    else
                    {
                        m_RemapIndices.Add(uniqueMaterials.Count);
                        uniqueMaterials.Add(mat);
                    }

                    m_MaterialNames.Add(mat.Name);

                    m_IndirectTexBlock.Add(mat.IndTexEntry);
                    if (m_Materials[i].LightChannelCount > 2)
                        m_Materials[i].LightChannelCount = 2;
                }

                writer.WriteString(Magic);
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for section size
                writer.WriteReverse(BitConverter.GetBytes((short)m_RemapIndices.Count), 0, 2);
                writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x84 }, 0, 4); // Offset to material init data. Always 132

                int[] Offsets = new int[29];
                for (int i = 0; i < 29; i++)
                    writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for Offsets

                bool[] writtenCheck = new bool[uniqueMaterials.Count];
                List<string> names = m_MaterialNames;

                for (int i = 0; i < m_RemapIndices.Count; i++)
                {
                    if (writtenCheck[m_RemapIndices[i]])
                        continue;
                    else
                    {
                        WriteMaterialInitData(writer, uniqueMaterials[m_RemapIndices[i]], ref m_CullModeBlock, ref m_MaterialColorBlock, ref m_ChannelControlBlock, ref m_AmbientColorBlock,
                        ref m_LightingColorBlock, ref m_TexCoord1GenBlock, ref m_TexCoord2GenBlock, ref m_TexMatrix1Block, ref m_TexMatrix2Block, ref m_TexRemapBlock, ref m_TevOrderBlock, ref m_TevColorBlock,
                        ref m_TevKonstColorBlock, ref m_TevStageBlock, ref m_SwapModeBlock, ref m_SwapTableBlock, ref m_FogBlock, ref m_AlphaCompBlock, ref m_blendModeBlock, ref m_NBTScaleBlock, ref m_zModeBlock,
                        ref m_zCompLocBlock, ref m_ditherBlock, ref NumColorChannelsBlock, ref NumTexGensBlock, ref NumTevStagesBlock);
                        writtenCheck[m_RemapIndices[i]] = true;
                    }
                }

                long curOffset = writer.Position;

                // Remap indices offset
                Offsets[0] = (int)(curOffset - start);

                for (int i = 0; i < m_RemapIndices.Count; i++)
                    writer.WriteReverse(BitConverter.GetBytes((short)m_RemapIndices[i]), 0, 2);

                AddPadding(writer, 4);

                curOffset = writer.Position;

                // Name table offset
                Offsets[1] = (int)(curOffset - start);

                writer.WriteStringTable(names);
                AddPadding(writer, 8);

                curOffset = writer.Position;

                // Indirect texturing offset
                Offsets[2] = (int)(curOffset - start);

                //IndirectTexturingIO.Write(writer, m_IndirectTexBlock);
                foreach (Material.IndirectTexturing ind in m_IndirectTexBlock)
                    ind.Write(writer);

                curOffset = writer.Position;

                // Cull mode offset
                Offsets[3] = (int)(curOffset - start);

                //CullModeIO.Write(writer, m_CullModeBlock);
                for (int i = 0; i < m_CullModeBlock.Count; i++)
                    writer.WriteReverse(BitConverter.GetBytes((int)m_CullModeBlock[i]), 0, 4);

                curOffset = writer.Position;

                // Material colors offset
                Offsets[4] = (int)(curOffset - start);

                //ColorIO.Write(writer, m_MaterialColorBlock);
                for (int i = 0; i < m_MaterialColorBlock.Count; i++)
                {
                    writer.WriteByte((byte)(m_MaterialColorBlock[i].R * 255));
                    writer.WriteByte((byte)(m_MaterialColorBlock[i].G * 255));
                    writer.WriteByte((byte)(m_MaterialColorBlock[i].B * 255));
                    writer.WriteByte((byte)(m_MaterialColorBlock[i].A * 255));
                }

                curOffset = writer.Position;

                // Color channel count offset
                Offsets[5] = (int)(curOffset - start);

                foreach (byte chanNum in NumColorChannelsBlock)
                    writer.WriteByte(chanNum);

                AddPadding(writer, 4);

                curOffset = writer.Position;

                // Color channel data offset
                Offsets[6] = (int)(curOffset - start);

                //ColorChannelIO.Write(writer, m_ChannelControlBlock);
                foreach (Material.ChannelControl chan in m_ChannelControlBlock)
                    chan.Write(writer);

                AddPadding(writer, 4);

                curOffset = writer.Position;

                // ambient color data offset
                Offsets[7] = (int)(curOffset - start);

                //ColorIO.Write(writer, m_AmbientColorBlock);
                for (int i = 0; i < m_AmbientColorBlock.Count; i++)
                {
                    writer.WriteByte((byte)(m_AmbientColorBlock[i].R * 255));
                    writer.WriteByte((byte)(m_AmbientColorBlock[i].G * 255));
                    writer.WriteByte((byte)(m_AmbientColorBlock[i].B * 255));
                    writer.WriteByte((byte)(m_AmbientColorBlock[i].A * 255));
                }

                curOffset = writer.Position;

                // light color data offset
                Offsets[8] = (int)(curOffset - start);

                if (m_LightingColorBlock != null)
                {
                    //ColorIO.Write(writer, m_LightingColorBlock);
                    for (int i = 0; i < m_LightingColorBlock.Count; i++)
                    {
                        writer.WriteByte((byte)(m_LightingColorBlock[i].R * 255));
                        writer.WriteByte((byte)(m_LightingColorBlock[i].G * 255));
                        writer.WriteByte((byte)(m_LightingColorBlock[i].B * 255));
                        writer.WriteByte((byte)(m_LightingColorBlock[i].A * 255));
                    }
                }

                curOffset = writer.Position;

                // tex gen count data offset
                Offsets[9] = (int)(curOffset - start);

                foreach (byte texGenCnt in NumTexGensBlock)
                    writer.WriteByte(texGenCnt);

                AddPadding(writer, 4);

                curOffset = writer.Position;

                // tex coord 1 data offset
                Offsets[10] = (int)(curOffset - start);

                //TexCoordGenIO.Write(writer, m_TexCoord1GenBlock);
                foreach (Material.TexCoordGen gen in m_TexCoord1GenBlock)
                    gen.Write(writer);


                curOffset = writer.Position;

                // tex coord 2 data offset AKA PostTexGenInfoOffset
                Offsets[11] = (m_TexCoord2GenBlock == null || m_TexCoord2GenBlock.Count == 0) ? 0 : (int)(curOffset - start);

                //TexCoordGenIO.Write(writer, m_TexCoord2GenBlock);
                if (m_TexCoord2GenBlock != null && m_TexCoord2GenBlock.Count != 0)
                {
                    foreach (Material.TexCoordGen gen in m_TexCoord2GenBlock)
                        gen.Write(writer);
                }

                curOffset = writer.Position;

                // tex matrix 1 data offset
                Offsets[12] = (int)(curOffset - start);

                //TexMatrixIO.Write(writer, m_TexMatrix1Block);
                foreach (Material.TexMatrix mat in m_TexMatrix1Block)
                    mat.Write(writer);


                curOffset = writer.Position;

                // tex matrix 2 data offset
                Offsets[13] = (m_TexMatrix2Block == null || m_TexMatrix2Block.Count == 0) ? 0 : (int)(curOffset - start);

                //TexMatrixIO.Write(writer, m_TexMatrix2Block);
                if (m_TexMatrix2Block != null && m_TexMatrix2Block.Count != 0)
                {
                    foreach (Material.TexMatrix gen in m_TexMatrix2Block)
                        gen.Write(writer);
                }


                curOffset = writer.Position;

                // tex number data offset
                Offsets[14] = (int)(curOffset - start);

                foreach (int inte in m_TexRemapBlock)
                    writer.WriteReverse(BitConverter.GetBytes((short)inte), 0, 2);

                AddPadding(writer, 4);

                curOffset = writer.Position;

                // tev order data offset
                Offsets[15] = (int)(curOffset - start);

                //TevOrderIO.Write(writer, m_TevOrderBlock);
                foreach (Material.TevOrder order in m_TevOrderBlock)
                    order.Write(writer);

                curOffset = writer.Position;

                // tev color data offset
                Offsets[16] = (int)(curOffset - start);

                //Int16ColorIO.Write(writer, m_TevColorBlock);
                for (int i = 0; i < m_TevColorBlock.Count; i++)
                {
                    writer.WriteReverse(BitConverter.GetBytes((short)(m_TevColorBlock[i].R * 255)), 0, 2);
                    writer.WriteReverse(BitConverter.GetBytes((short)(m_TevColorBlock[i].G * 255)), 0, 2);
                    writer.WriteReverse(BitConverter.GetBytes((short)(m_TevColorBlock[i].B * 255)), 0, 2);
                    writer.WriteReverse(BitConverter.GetBytes((short)(m_TevColorBlock[i].A * 255)), 0, 2);
                }

                curOffset = writer.Position;

                // tev konst color data offset
                Offsets[17] = (int)(curOffset - start);

                //ColorIO.Write(writer, m_TevKonstColorBlock);
                for (int i = 0; i < m_TevKonstColorBlock.Count; i++)
                {
                    writer.WriteByte((byte)(m_TevKonstColorBlock[i].R * 255));
                    writer.WriteByte((byte)(m_TevKonstColorBlock[i].G * 255));
                    writer.WriteByte((byte)(m_TevKonstColorBlock[i].B * 255));
                    writer.WriteByte((byte)(m_TevKonstColorBlock[i].A * 255));
                }

                curOffset = writer.Position;

                // tev stage count data offset
                Offsets[18] = (int)(curOffset - start);

                foreach (byte bt in NumTevStagesBlock)
                    writer.WriteByte(bt);

                AddPadding(writer, 4);

                curOffset = writer.Position;

                // tev stage data offset
                Offsets[19] = (int)(curOffset - start);

                //TevStageIO.Write(writer, m_TevStageBlock);
                foreach (Material.TevStage stage in m_TevStageBlock)
                    stage.Write(writer);

                curOffset = writer.Position;

                // tev swap mode offset
                Offsets[20] = (int)(curOffset - start);

                //TevSwapModeIO.Write(writer, m_SwapModeBlock);
                foreach (Material.TevSwapMode mode in m_SwapModeBlock)
                    mode.Write(writer);

                curOffset = writer.Position;

                // tev swap mode table offset
                Offsets[21] = (int)(curOffset - start);

                //TevSwapModeTableIO.Write(writer, m_SwapTableBlock);
                foreach (Material.TevSwapModeTable table in m_SwapTableBlock)
                    table.Write(writer);

                curOffset = writer.Position;

                // fog data offset
                Offsets[22] = (int)(curOffset - start);

                //FogIO.Write(writer, m_FogBlock);
                foreach (Material.Fog fog in m_FogBlock)
                    fog.Write(writer);

                curOffset = writer.Position;

                // alpha compare offset
                Offsets[23] = (int)(curOffset - start);

                //AlphaCompareIO.Write(writer, m_AlphaCompBlock);
                foreach (Material.AlphaCompare comp in m_AlphaCompBlock)
                    comp.Write(writer);

                curOffset = writer.Position;

                // blend data offset
                Offsets[24] = (int)(curOffset - start);

                //BlendModeIO.Write(writer, m_blendModeBlock);
                foreach (Material.BlendMode mode in m_blendModeBlock)
                    mode.Write(writer);

                curOffset = writer.Position;

                // zmode data offset
                Offsets[25] = (int)(curOffset - start);

                //ZModeIO.Write(writer, m_zModeBlock);
                foreach (Material.ZModeHolder mode in m_zModeBlock)
                    mode.Write(writer);

                curOffset = writer.Position;

                // z comp loc data offset
                Offsets[26] = (int)(curOffset - start);

                foreach (bool bol in m_zCompLocBlock)
                    writer.WriteByte((byte)(bol ? 0x01 : 0x00));

                AddPadding(writer, 4);

                curOffset = writer.Position;

                //Dither Block
                if (m_ditherBlock != null && m_ditherBlock.Count != 0)
                {
                    // dither data offset
                    Offsets[27] = (int)(curOffset - start);

                    foreach (bool bol in m_ditherBlock)
                        writer.WriteByte((byte)(bol ? 0x01 : 0x00));

                    AddPadding(writer, 4);
                }

                curOffset = writer.Position;

                // NBT Scale data offset
                Offsets[28] = (int)(curOffset - start);

                //NBTScaleIO.Write(writer, m_NBTScaleBlock);
                foreach (Material.NBTScaleHolder scale in m_NBTScaleBlock)
                    scale.Write(writer);

                AddPadding(writer, 32);

                writer.Position = start + 4;
                writer.WriteReverse(BitConverter.GetBytes((int)(writer.Length - start)), 0, 4);
                writer.Position += 0x08;
                for (int i = 0; i < 29; i++)
                    writer.WriteReverse(BitConverter.GetBytes(Offsets[i]), 0, 4);
                writer.Position = writer.Length;
            }
            private void WriteMaterialInitData(Stream writer, Material mat, ref List<CullMode> m_CullModeBlock,
            ref List<Color4> m_MaterialColorBlock, ref List<Material.ChannelControl> m_ChannelControlBlock, ref List<Color4> m_AmbientColorBlock, ref List<Color4> m_LightingColorBlock,
            ref List<Material.TexCoordGen> m_TexCoord1GenBlock, ref List<Material.TexCoordGen> m_TexCoord2GenBlock, ref List<Material.TexMatrix> m_TexMatrix1Block, ref List<Material.TexMatrix> m_TexMatrix2Block,
            ref List<short> m_TexRemapBlock, ref List<Material.TevOrder> m_TevOrderBlock, ref List<Color4> m_TevColorBlock, ref List<Color4> m_TevKonstColorBlock, ref List<Material.TevStage> m_TevStageBlock,
            ref List<Material.TevSwapMode> m_SwapModeBlock, ref List<Material.TevSwapModeTable> m_SwapTableBlock, ref List<Material.Fog> m_FogBlock, ref List<Material.AlphaCompare> m_AlphaCompBlock,
            ref List<Material.BlendMode> m_blendModeBlock, ref List<Material.NBTScaleHolder> m_NBTScaleBlock, ref List<Material.ZModeHolder> m_zModeBlock, ref List<bool> m_zCompLocBlock,
            ref List<bool> m_ditherBlock, ref List<byte> NumColorChannelsBlock, ref List<byte> NumTexGensBlock, ref List<byte> NumTevStagesBlock)
            {
                writer.WriteByte(mat.Flag);

                if (!m_CullModeBlock.Any(CM => CM == mat.CullMode))
                    m_CullModeBlock.Add(mat.CullMode);
                writer.WriteByte((byte)m_CullModeBlock.IndexOf(mat.CullMode));

                if (!NumColorChannelsBlock.Any(NCC => NCC == mat.LightChannelCount))
                    NumColorChannelsBlock.Add(mat.LightChannelCount);
                writer.WriteByte((byte)NumColorChannelsBlock.IndexOf(mat.LightChannelCount));

                if (!NumTexGensBlock.Any(NTG => NTG == mat.NumTexGensCount))
                    NumTexGensBlock.Add(mat.NumTexGensCount);
                writer.WriteByte((byte)NumTexGensBlock.IndexOf(mat.NumTexGensCount));

                if (!NumTevStagesBlock.Any(NTS => NTS == mat.NumTevStagesCount))
                    NumTevStagesBlock.Add(mat.NumTevStagesCount);
                writer.WriteByte((byte)NumTevStagesBlock.IndexOf(mat.NumTevStagesCount));

                if (!m_zCompLocBlock.Any(ZCL => ZCL == mat.ZCompLoc))
                    m_zCompLocBlock.Add(mat.ZCompLoc);
                writer.WriteByte((byte)m_zCompLocBlock.IndexOf(mat.ZCompLoc));

                if (!m_zModeBlock.Any(ZM => ZM == mat.ZMode))
                    m_zModeBlock.Add(mat.ZMode);
                writer.WriteByte((byte)m_zModeBlock.IndexOf(mat.ZMode));

                if (!m_ditherBlock.Any(Ditherer => Ditherer == mat.Dither))
                    m_ditherBlock.Add(mat.Dither);
                writer.WriteByte((byte)m_ditherBlock.IndexOf(mat.Dither));

                if (mat.MaterialColors[0].HasValue)
                {
                    if (!m_MaterialColorBlock.Any(MatCol => MatCol == mat.MaterialColors[0].Value))
                        m_MaterialColorBlock.Add(mat.MaterialColors[0].Value);
                    writer.WriteReverse(BitConverter.GetBytes((short)m_MaterialColorBlock.IndexOf(mat.MaterialColors[0].Value)), 0, 2);
                }
                else
                    writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                if (mat.MaterialColors[1].HasValue)
                {
                    if (!m_MaterialColorBlock.Any(MatCol => MatCol == mat.MaterialColors[1].Value))
                        m_MaterialColorBlock.Add(mat.MaterialColors[1].Value);
                    writer.WriteReverse(BitConverter.GetBytes((short)m_MaterialColorBlock.IndexOf(mat.MaterialColors[1].Value)), 0, 2);
                }
                else
                    writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                for (int i = 0; i < 4; i++)
                {
                    if (mat.ChannelControls[i] != null)
                    {
                        if (!m_ChannelControlBlock.Any(ChanCol => ChanCol == mat.ChannelControls[i].Value))
                            m_ChannelControlBlock.Add(mat.ChannelControls[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_ChannelControlBlock.IndexOf(mat.ChannelControls[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                if (mat.AmbientColors[0].HasValue)
                {
                    if (!m_AmbientColorBlock.Any(AmbCol => AmbCol == mat.AmbientColors[0].Value))
                        m_AmbientColorBlock.Add(mat.AmbientColors[0].Value);
                    writer.WriteReverse(BitConverter.GetBytes((short)m_AmbientColorBlock.IndexOf(mat.AmbientColors[0].Value)), 0, 2);
                }
                else
                    writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                if (mat.AmbientColors[1].HasValue)
                {
                    if (!m_AmbientColorBlock.Any(AmbCol => AmbCol == mat.AmbientColors[1].Value))
                        m_AmbientColorBlock.Add(mat.AmbientColors[1].Value);
                    writer.WriteReverse(BitConverter.GetBytes((short)m_AmbientColorBlock.IndexOf(mat.AmbientColors[1].Value)), 0, 2);
                }
                else
                    writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                for (int i = 0; i < 8; i++)
                {
                    if (mat.LightingColors[i] != null)
                    {
                        if (!m_LightingColorBlock.Any(LightCol => LightCol == mat.LightingColors[i].Value))
                            m_LightingColorBlock.Add(mat.LightingColors[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_LightingColorBlock.IndexOf(mat.LightingColors[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 8; i++)
                {
                    if (mat.TexCoord1Gens[i] != null)
                    {
                        if (!m_TexCoord1GenBlock.Any(TexCoord => TexCoord == mat.TexCoord1Gens[i].Value))
                            m_TexCoord1GenBlock.Add(mat.TexCoord1Gens[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_TexCoord1GenBlock.IndexOf(mat.TexCoord1Gens[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 8; i++)
                {
                    if (mat.PostTexCoordGens[i] != null)
                    {
                        if (!m_TexCoord2GenBlock.Any(PostTexCoord => PostTexCoord == mat.PostTexCoordGens[i].Value))
                            m_TexCoord2GenBlock.Add(mat.PostTexCoordGens[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_TexCoord2GenBlock.IndexOf(mat.PostTexCoordGens[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 10; i++)
                {
                    if (mat.TexMatrix1[i] != null)
                    {
                        if (!m_TexMatrix1Block.Any(TexMat => TexMat == mat.TexMatrix1[i].Value))
                            m_TexMatrix1Block.Add(mat.TexMatrix1[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_TexMatrix1Block.IndexOf(mat.TexMatrix1[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 20; i++)
                {
                    if (mat.PostTexMatrix[i] != null)
                    {
                        if (!m_TexMatrix2Block.Any(PostTexMat => PostTexMat == mat.PostTexMatrix[i].Value))
                            m_TexMatrix2Block.Add(mat.PostTexMatrix[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_TexMatrix2Block.IndexOf(mat.PostTexMatrix[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 8; i++)
                {
                    if (mat.TextureIndices[i] != -1)
                    {
                        if (!m_TexRemapBlock.Any(TexId => TexId == (short)mat.TextureIndices[i]))
                            m_TexRemapBlock.Add((short)mat.TextureIndices[i]);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_TexRemapBlock.IndexOf((short)mat.TextureIndices[i])), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 4; i++)
                {
                    if (mat.KonstColors[i] != null)
                    {
                        if (!m_TevKonstColorBlock.Any(KCol => KCol == mat.KonstColors[i].Value))
                            m_TevKonstColorBlock.Add(mat.KonstColors[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_TevKonstColorBlock.IndexOf(mat.KonstColors[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 16; i++)
                    writer.WriteByte((byte)mat.ColorSels[i]);

                for (int i = 0; i < 16; i++)
                    writer.WriteByte((byte)mat.AlphaSels[i]);

                for (int i = 0; i < 16; i++)
                {
                    if (mat.TevOrders[i] != null)
                    {
                        if (!m_TevOrderBlock.Any(TevOrder => TevOrder == mat.TevOrders[i].Value))
                            m_TevOrderBlock.Add(mat.TevOrders[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_TevOrderBlock.IndexOf(mat.TevOrders[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 4; i++)
                {
                    if (mat.TevColors[i] != null)
                    {
                        if (!m_TevColorBlock.Any(TevCol => TevCol == mat.TevColors[i].Value))
                            m_TevColorBlock.Add(mat.TevColors[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_TevColorBlock.IndexOf(mat.TevColors[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 16; i++)
                {
                    if (mat.TevStages[i] != null)
                    {
                        if (!m_TevStageBlock.Any(TevStg => TevStg == mat.TevStages[i].Value))
                            m_TevStageBlock.Add(mat.TevStages[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_TevStageBlock.IndexOf(mat.TevStages[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 16; i++)
                {
                    if (mat.SwapModes[i] != null)
                    {
                        if (!m_SwapModeBlock.Any(SwapMode => SwapMode == mat.SwapModes[i].Value))
                            m_SwapModeBlock.Add(mat.SwapModes[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_SwapModeBlock.IndexOf(mat.SwapModes[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                for (int i = 0; i < 16; i++)
                {
                    if (mat.SwapTables[i] != null)
                    {
                        if (!m_SwapTableBlock.Any(SwapTable => SwapTable == mat.SwapTables[i].Value))
                            m_SwapTableBlock.Add(mat.SwapTables[i].Value);
                        writer.WriteReverse(BitConverter.GetBytes((short)m_SwapTableBlock.IndexOf(mat.SwapTables[i].Value)), 0, 2);
                    }
                    else
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                }

                if (!m_FogBlock.Any(Fog => Fog == mat.FogInfo))
                    m_FogBlock.Add(mat.FogInfo);
                writer.WriteReverse(BitConverter.GetBytes((short)m_FogBlock.IndexOf(mat.FogInfo)), 0, 2);

                if (!m_AlphaCompBlock.Any(AlphaComp => AlphaComp == mat.AlphCompare))
                    m_AlphaCompBlock.Add(mat.AlphCompare);
                writer.WriteReverse(BitConverter.GetBytes((short)m_AlphaCompBlock.IndexOf(mat.AlphCompare)), 0, 2);

                if (!m_blendModeBlock.Any(Blend => Blend == mat.BMode))
                    m_blendModeBlock.Add(mat.BMode);
                writer.WriteReverse(BitConverter.GetBytes((short)m_blendModeBlock.IndexOf(mat.BMode)), 0, 2);

                if (!m_NBTScaleBlock.Any(NBT => NBT == mat.NBTScale))
                    m_NBTScaleBlock.Add(mat.NBTScale);
                writer.WriteReverse(BitConverter.GetBytes((short)m_NBTScaleBlock.IndexOf(mat.NBTScale)), 0, 2);
            }
            #endregion

            public class Material
            {
                public string Name;
                public byte Flag;
                public bool IsTranslucent => (Flag & 3) == 0;
                public byte NumTexGensCount
                {
                    get
                    {
                        byte value = 0;
                        for (int i = 0; i < TexMatrix1.Length; i++)
                        {
                            if (TexMatrix1[i].HasValue)
                                value++;
                        }
                        return value;
                    }
                }
                public byte NumTevStagesCount
                {
                    get
                    {
                        byte value = 0;
                        for (int i = 0; i < TevStages.Length; i++)
                        {
                            if (TevStages[i].HasValue)
                                value++;
                        }
                        return value;
                    }
                }

                public CullMode CullMode;
                public byte LightChannelCount;
                public bool ZCompLoc;
                public bool Dither;

                /// <summary>
                /// Only used during Read/write
                /// </summary>
                internal int[] TextureIndices;
                //public string[] TextureNames;
                /// <summary>
                /// Holds references to the Textures that this material uses
                /// </summary>
                public BTI.BTI[] Textures;

                public IndirectTexturing IndTexEntry;
                public Color4?[] MaterialColors;
                public ChannelControl?[] ChannelControls;
                public Color4?[] AmbientColors;
                public Color4?[] LightingColors;
                public TexCoordGen?[] TexCoord1Gens;
                public TexCoordGen?[] PostTexCoordGens;
                public TexMatrix?[] TexMatrix1;
                public TexMatrix?[] PostTexMatrix;
                public TevOrder?[] TevOrders;
                public KonstColorSel[] ColorSels;
                public KonstAlphaSel[] AlphaSels;
                public Color4?[] TevColors;
                public Color4?[] KonstColors;
                public TevStage?[] TevStages;
                public TevSwapMode?[] SwapModes;
                public TevSwapModeTable?[] SwapTables;

                public Fog FogInfo;
                public AlphaCompare AlphCompare;
                public BlendMode BMode;
                public ZModeHolder ZMode;
                public NBTScaleHolder NBTScale;

                public Material()
                {
                    CullMode = CullMode.Back;
                    LightChannelCount = 1;
                    MaterialColors = new Color4?[2] { new Color4(1, 1, 1, 1), null };

                    ChannelControls = new ChannelControl?[4];

                    IndTexEntry = new IndirectTexturing();

                    AmbientColors = new Color4?[2] { new Color4(50f / 255f, 50f / 255f, 50f / 255f, 50f / 255f), null };
                    LightingColors = new Color4?[8];

                    TexCoord1Gens = new TexCoordGen?[8];
                    PostTexCoordGens = new TexCoordGen?[8];

                    TexMatrix1 = new TexMatrix?[10];
                    PostTexMatrix = new TexMatrix?[20];

                    TextureIndices = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };
                    //TextureNames = new string[8] { "", "", "", "", "", "", "", "" };
                    Textures = new BTI.BTI[8];

                    KonstColors = new Color4?[4];
                    KonstColors[0] = new Color4(1, 1, 1, 1);

                    ColorSels = new KonstColorSel[16];
                    AlphaSels = new KonstAlphaSel[16];

                    TevOrders = new TevOrder?[16];
                    //TevOrders[0] = new TevOrder(TexCoordId.TexCoord0, TexMapId.TexMap0, GXColorChannelId.Color0);

                    TevColors = new Color4?[4];
                    TevColors[0] = new Color4(1, 1, 1, 1);

                    TevStages = new TevStage?[16];

                    SwapModes = new TevSwapMode?[16];
                    SwapModes[0] = new TevSwapMode(0, 0);

                    SwapTables = new TevSwapModeTable?[16];
                    SwapTables[0] = new TevSwapModeTable(0, 1, 2, 3);

                    AlphCompare = new AlphaCompare(AlphaCompare.CompareType.Greater, 127, AlphaCompare.AlphaOp.And, AlphaCompare.CompareType.Always, 0);
                    ZMode = new ZModeHolder(true, AlphaCompare.CompareType.LEqual, true);
                    BMode = new BlendMode(BlendMode.BlendModeID.Blend, BlendMode.BlendModeControl.SrcAlpha, BlendMode.BlendModeControl.InverseSrcAlpha, BlendMode.LogicOp.NoOp);
                    NBTScale = new NBTScaleHolder(0, Vector3.Zero);
                    FogInfo = new Fog(0, false, 0, 0, 0, 0, 0, new Color4(0, 0, 0, 0), new float[10]);
                }

                public Material(Material src)
                {
                    Flag = src.Flag;
                    CullMode = src.CullMode;
                    LightChannelCount = src.LightChannelCount;
                    ZCompLoc = src.ZCompLoc;
                    Dither = src.Dither;
                    TextureIndices = src.TextureIndices;
                    //TextureNames = src.TextureNames;
                    Textures = src.Textures;
                    IndTexEntry = src.IndTexEntry;
                    MaterialColors = src.MaterialColors;
                    ChannelControls = src.ChannelControls;
                    AmbientColors = src.AmbientColors;
                    LightingColors = src.LightingColors;
                    TexCoord1Gens = src.TexCoord1Gens;
                    PostTexCoordGens = src.PostTexCoordGens;
                    TexMatrix1 = src.TexMatrix1;
                    PostTexMatrix = src.PostTexMatrix;
                    TevOrders = src.TevOrders;
                    ColorSels = src.ColorSels;
                    AlphaSels = src.AlphaSels;
                    TevColors = src.TevColors;
                    KonstColors = src.KonstColors;
                    TevStages = src.TevStages;
                    SwapModes = src.SwapModes;
                    SwapTables = src.SwapTables;

                    FogInfo = src.FogInfo;
                    AlphCompare = src.AlphCompare;
                    BMode = src.BMode;
                    ZMode = src.ZMode;
                    NBTScale = src.NBTScale;
                }

                public void AddChannelControl(TevOrder.GXColorChannelId id, bool enable, ChannelControl.ColorSrc MatSrcColor, ChannelControl.LightId litId, ChannelControl.DiffuseFn diffuse, ChannelControl.J3DAttenuationFn atten, ChannelControl.ColorSrc ambSrcColor)
                {
                    ChannelControl control = new ChannelControl
                    {
                        Enable = enable,
                        MaterialSrcColor = MatSrcColor,
                        LitMask = litId,
                        DiffuseFunction = diffuse,
                        AttenuationFunction = atten,
                        AmbientSrcColor = ambSrcColor
                    };

                    ChannelControls[(int)id] = control;
                }

                public void AddTexMatrix(TexGenType projection, byte type, Vector3 effectTranslation, Vector2 scale, float rotation, Vector2 translation, Matrix4 matrix)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        if (TexMatrix1[i] == null)
                        {
                            TexMatrix1[i] = new TexMatrix(projection, type, effectTranslation, scale, rotation, translation, matrix);
                            break;
                        }

                        if (i == 9)
                            throw new Exception($"TexMatrix1 array for material \"{ Name }\" is full!");
                    }
                }

                public void AddTexIndex(int index)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (TextureIndices[i] == -1)
                        {
                            TextureIndices[i] = index;
                            break;
                        }

                        if (i == 7)
                            throw new Exception($"TextureIndex array for material \"{ Name }\" is full!");
                    }
                }

                public void AddTevOrder(TexCoordId coordId, TexMapId mapId, TevOrder.GXColorChannelId colorChanId)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (TevOrders[i] == null)
                        {
                            TevOrders[i] = new TevOrder(coordId, mapId, colorChanId);
                            break;
                        }

                        if (i == 7)
                            throw new Exception($"TevOrder array for material \"{ Name }\" is full!");
                    }
                }

                public Material Clone()
                {
                    Material Target = new Material()
                    {
                        Name = Name,
                        Flag = Flag,
                        CullMode = CullMode,
                        LightChannelCount = LightChannelCount,
                        ZCompLoc = ZCompLoc,
                        Dither = Dither,
                        IndTexEntry = IndTexEntry.Clone(),

                        MaterialColors = new Color4?[MaterialColors.Length],
                        ChannelControls = new ChannelControl?[ChannelControls.Length],
                        AmbientColors = new Color4?[AmbientColors.Length],
                        LightingColors = new Color4?[LightingColors.Length],
                        TexCoord1Gens = new TexCoordGen?[TexCoord1Gens.Length],
                        PostTexCoordGens = new TexCoordGen?[PostTexCoordGens.Length],
                        TexMatrix1 = new TexMatrix?[TexMatrix1.Length],
                        PostTexMatrix = new TexMatrix?[PostTexMatrix.Length],
                        TevOrders = new TevOrder?[TevOrders.Length],
                        ColorSels = new KonstColorSel[ColorSels.Length],
                        AlphaSels = new KonstAlphaSel[AlphaSels.Length],
                        TevColors = new Color4?[TevColors.Length],
                        KonstColors = new Color4?[KonstColors.Length],
                        TevStages = new TevStage?[TevStages.Length],
                        SwapModes = new TevSwapMode?[SwapModes.Length],
                        SwapTables = new TevSwapModeTable?[SwapTables.Length],

                        FogInfo = FogInfo.Clone(),
                        AlphCompare = AlphCompare.Clone(),
                        BMode = BMode.Clone(),
                        ZMode = ZMode.Clone(),
                        NBTScale = NBTScale.Clone()
                    };

                    Target.TextureIndices = new int[8];
                    //Target.TextureNames = new string[8];
                    Target.Textures = new BTI.BTI[8];
                    for (int i = 0; i < 8; i++)
                    {
                        Target.TextureIndices[i] = TextureIndices[i];
                        //Target.TextureNames[i] = TextureNames[i];
                        Target.Textures[i] = Textures[i];
                    }
                    for (int i = 0; i < MaterialColors.Length; i++)
                    {
                        if (MaterialColors[i].HasValue)
                            Target.MaterialColors[i] = new Color4(MaterialColors[i].Value.R, MaterialColors[i].Value.G, MaterialColors[i].Value.B, MaterialColors[i].Value.A);
                    }
                    for (int i = 0; i < ChannelControls.Length; i++)
                    {
                        if (ChannelControls[i].HasValue)
                            Target.ChannelControls[i] = ChannelControls[i].Value.Clone();
                    }
                    for (int i = 0; i < AmbientColors.Length; i++)
                    {
                        if (AmbientColors[i].HasValue)
                            Target.AmbientColors[i] = new Color4(AmbientColors[i].Value.R, AmbientColors[i].Value.G, AmbientColors[i].Value.B, AmbientColors[i].Value.A);
                    }
                    for (int i = 0; i < LightingColors.Length; i++)
                    {
                        if (LightingColors[i].HasValue)
                            Target.LightingColors[i] = new Color4(LightingColors[i].Value.R, LightingColors[i].Value.G, LightingColors[i].Value.B, LightingColors[i].Value.A);
                    }
                    for (int i = 0; i < TexCoord1Gens.Length; i++)
                    {
                        if (TexCoord1Gens[i].HasValue)
                            Target.TexCoord1Gens[i] = TexCoord1Gens[i].Value.Clone();
                    }
                    for (int i = 0; i < PostTexCoordGens.Length; i++)
                    {
                        if (PostTexCoordGens[i].HasValue)
                            Target.PostTexCoordGens[i] = PostTexCoordGens[i].Value.Clone();
                    }
                    for (int i = 0; i < TexMatrix1.Length; i++)
                    {
                        if (TexMatrix1[i].HasValue)
                            Target.TexMatrix1[i] = TexMatrix1[i].Value.Clone();
                    }
                    for (int i = 0; i < PostTexMatrix.Length; i++)
                    {
                        if (PostTexMatrix[i].HasValue)
                            Target.PostTexMatrix[i] = PostTexMatrix[i].Value.Clone();
                    }
                    for (int i = 0; i < TevOrders.Length; i++)
                    {
                        if (TevOrders[i].HasValue)
                            Target.TevOrders[i] = TevOrders[i].Value.Clone();
                    }
                    for (int i = 0; i < ColorSels.Length; i++)
                        Target.ColorSels[i] = ColorSels[i];
                    for (int i = 0; i < AlphaSels.Length; i++)
                        Target.AlphaSels[i] = AlphaSels[i];
                    for (int i = 0; i < TevColors.Length; i++)
                    {
                        if (TevColors[i].HasValue)
                            Target.TevColors[i] = new Color4(TevColors[i].Value.R, TevColors[i].Value.G, TevColors[i].Value.B, TevColors[i].Value.A);
                    }
                    for (int i = 0; i < KonstColors.Length; i++)
                    {
                        if (KonstColors[i].HasValue)
                            Target.KonstColors[i] = new Color4(KonstColors[i].Value.R, KonstColors[i].Value.G, KonstColors[i].Value.B, KonstColors[i].Value.A);
                    }
                    for (int i = 0; i < TevStages.Length; i++)
                    {
                        if (TevStages[i].HasValue)
                            Target.TevStages[i] = TevStages[i].Value.Clone();
                    }
                    for (int i = 0; i < SwapModes.Length; i++)
                    {
                        if (SwapModes[i].HasValue)
                            Target.SwapModes[i] = SwapModes[i].Value.Clone();
                    }
                    for (int i = 0; i < SwapTables.Length; i++)
                    {
                        if (SwapTables[i].HasValue)
                            Target.SwapTables[i] = SwapTables[i].Value.Clone();
                    }

                    return Target;
                }

                public override string ToString()
                {
                    return $"{Name}";
                }

                public override bool Equals(object obj)
                {
                    if (!(obj is Material right))
                        return false;
                    if (Flag != right.Flag)
                        return false;
                    if (CullMode != right.CullMode)
                        return false;
                    if (LightChannelCount != right.LightChannelCount)
                        return false;
                    if (NumTexGensCount != right.NumTexGensCount)
                        return false;
                    if (NumTevStagesCount != right.NumTevStagesCount)
                        return false;
                    if (ZCompLoc != right.ZCompLoc)
                        return false;
                    if (ZMode != right.ZMode)
                        return false;
                    if (Dither != right.Dither)
                        return false;

                    for (int i = 0; i < 2; i++)
                    {
                        if (MaterialColors[i] != right.MaterialColors[i])
                            return false;
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        if (ChannelControls[i] != right.ChannelControls[i])
                            return false;
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        if (AmbientColors[i] != right.AmbientColors[i])
                            return false;
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        if (LightingColors[i] != right.LightingColors[i])
                            return false;
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        if (TexCoord1Gens[i] != right.TexCoord1Gens[i]) // TODO: does != actually work on these types of things?? might need custom operators
                            return false;
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        if (PostTexCoordGens[i] != right.PostTexCoordGens[i])
                            return false;
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        if (TexMatrix1[i] != right.TexMatrix1[i])
                            return false;
                    }
                    for (int i = 0; i < 20; i++)
                    {
                        if (PostTexMatrix[i] != right.PostTexMatrix[i])
                            return false;
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        //if (TextureNames[i] != right.TextureNames[i])
                        if (Textures[i]?.ImageEquals(right.Textures[i]) ?? true)
                            return false;
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        if (KonstColors[i] != right.KonstColors[i])
                            return false;
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        if (ColorSels[i] != right.ColorSels[i])
                            return false;
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        if (AlphaSels[i] != right.AlphaSels[i])
                            return false;
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        if (TevOrders[i] != right.TevOrders[i])
                            return false;
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        if (TevColors[i] != right.TevColors[i])
                            return false;
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        if (TevStages[i] != right.TevStages[i])
                            return false;
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        if (SwapModes[i] != right.SwapModes[i])
                            return false;
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        if (SwapTables[i] != right.SwapTables[i])
                            return false;
                    }

                    if (FogInfo != right.FogInfo)
                        return false;
                    if (AlphCompare != right.AlphCompare)
                        return false;
                    if (BMode != right.BMode)
                        return false;
                    if (NBTScale != right.NBTScale)
                        return false;

                    return true;
                }

                public override int GetHashCode()
                {
                    var hashCode = 1712440529;
                    hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
                    hashCode = hashCode * -1521134295 + Flag.GetHashCode();
                    hashCode = hashCode * -1521134295 + LightChannelCount.GetHashCode();
                    hashCode = hashCode * -1521134295 + NumTexGensCount.GetHashCode();
                    hashCode = hashCode * -1521134295 + NumTevStagesCount.GetHashCode();
                    hashCode = hashCode * -1521134295 + CullMode.GetHashCode();
                    hashCode = hashCode * -1521134295 + ZCompLoc.GetHashCode();
                    hashCode = hashCode * -1521134295 + Dither.GetHashCode();
                    hashCode = hashCode * -1521134295 + EqualityComparer<int[]>.Default.GetHashCode(TextureIndices);
                    //hashCode = hashCode * -1521134295 + EqualityComparer<string[]>.Default.GetHashCode(TextureNames);
                    hashCode = hashCode * -1521134295 + EqualityComparer<IndirectTexturing>.Default.GetHashCode(IndTexEntry);
                    hashCode = hashCode * -1521134295 + EqualityComparer<Color4?[]>.Default.GetHashCode(MaterialColors);
                    hashCode = hashCode * -1521134295 + EqualityComparer<ChannelControl?[]>.Default.GetHashCode(ChannelControls);
                    hashCode = hashCode * -1521134295 + EqualityComparer<Color4?[]>.Default.GetHashCode(AmbientColors);
                    hashCode = hashCode * -1521134295 + EqualityComparer<Color4?[]>.Default.GetHashCode(LightingColors);
                    hashCode = hashCode * -1521134295 + EqualityComparer<TexCoordGen?[]>.Default.GetHashCode(TexCoord1Gens);
                    hashCode = hashCode * -1521134295 + EqualityComparer<TexCoordGen?[]>.Default.GetHashCode(PostTexCoordGens);
                    hashCode = hashCode * -1521134295 + EqualityComparer<TexMatrix?[]>.Default.GetHashCode(TexMatrix1);
                    hashCode = hashCode * -1521134295 + EqualityComparer<TexMatrix?[]>.Default.GetHashCode(PostTexMatrix);
                    hashCode = hashCode * -1521134295 + EqualityComparer<TevOrder?[]>.Default.GetHashCode(TevOrders);
                    hashCode = hashCode * -1521134295 + EqualityComparer<KonstColorSel[]>.Default.GetHashCode(ColorSels);
                    hashCode = hashCode * -1521134295 + EqualityComparer<KonstAlphaSel[]>.Default.GetHashCode(AlphaSels);
                    hashCode = hashCode * -1521134295 + EqualityComparer<Color4?[]>.Default.GetHashCode(TevColors);
                    hashCode = hashCode * -1521134295 + EqualityComparer<Color4?[]>.Default.GetHashCode(KonstColors);
                    hashCode = hashCode * -1521134295 + EqualityComparer<TevStage?[]>.Default.GetHashCode(TevStages);
                    hashCode = hashCode * -1521134295 + EqualityComparer<TevSwapMode?[]>.Default.GetHashCode(SwapModes);
                    hashCode = hashCode * -1521134295 + EqualityComparer<TevSwapModeTable?[]>.Default.GetHashCode(SwapTables);
                    hashCode = hashCode * -1521134295 + EqualityComparer<Fog>.Default.GetHashCode(FogInfo);
                    hashCode = hashCode * -1521134295 + EqualityComparer<AlphaCompare>.Default.GetHashCode(AlphCompare);
                    hashCode = hashCode * -1521134295 + EqualityComparer<BlendMode>.Default.GetHashCode(BMode);
                    hashCode = hashCode * -1521134295 + EqualityComparer<ZModeHolder>.Default.GetHashCode(ZMode);
                    hashCode = hashCode * -1521134295 + EqualityComparer<NBTScaleHolder>.Default.GetHashCode(NBTScale);
                    return hashCode;
                }

                public class IndirectTexturing
                {
                    /// <summary>
                    /// Determines if an indirect texture lookup is to take place
                    /// </summary>
                    public bool HasLookup;
                    /// <summary>
                    /// The number of indirect texturing stages to use
                    /// </summary>
                    public byte IndTexStageNum;

                    public IndirectTevOrder?[] TevOrders;

                    /// <summary>
                    /// The dynamic 2x3 matrices to use when transforming the texture coordinates
                    /// </summary>
                    public IndirectTexMatrix[] Matrices;
                    /// <summary>
                    /// U and V scales to use when transforming the texture coordinates
                    /// </summary>
                    public IndirectTexScale[] Scales;
                    /// <summary>
                    /// Instructions for setting up the specified TEV stage for lookup operations
                    /// </summary>
                    public IndirectTevStage[] TevStages;

                    public IndirectTexturing()
                    {
                        HasLookup = false;
                        IndTexStageNum = 0;

                        TevOrders = new IndirectTevOrder?[16];
                        for (int i = 0; i < 16; i++)
                            TevOrders[i] = new IndirectTevOrder(TexCoordId.Null, TexMapId.Null);

                        Matrices = new IndirectTexMatrix[3];
                        for (int i = 0; i < 3; i++)
                            Matrices[i] = new IndirectTexMatrix(new Matrix2x3(0.5f, 0.0f, 0.0f, 0.0f, 0.5f, 0.0f), 1);

                        Scales = new IndirectTexScale[4];
                        for (int i = 0; i < 4; i++)
                            Scales[i] = new IndirectTexScale(IndirectScale.ITS_1, IndirectScale.ITS_1);

                        TevStages = new IndirectTevStage[16];
                        for (int i = 0; i < 3; i++)
                            TevStages[i] = new IndirectTevStage(
                                TevStageId.TevStage0,
                                IndirectFormat.ITF_8,
                                IndirectBias.S,
                                IndirectMatrix.ITM_OFF,
                                IndirectWrap.ITW_OFF,
                                IndirectWrap.ITW_OFF,
                                false,
                                false,
                                IndirectAlpha.ITBA_OFF
                                );
                    }
                    public IndirectTexturing(Stream reader)
                    {
                        HasLookup = reader.ReadByte() > 0;
                        IndTexStageNum = (byte)reader.ReadByte();
                        reader.Position += 0x02;

                        TevOrders = new IndirectTevOrder?[16];
                        for (int i = 0; i < 16; i++)
                        {
                            short val = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0);
                            reader.Position -= 0x02;
                            if (val >= 0)
                                TevOrders[i] = new IndirectTevOrder(reader);

                        }

                        Matrices = new IndirectTexMatrix[3];
                        for (int i = 0; i < 3; i++)
                            Matrices[i] = new IndirectTexMatrix(reader);

                        Scales = new IndirectTexScale[4];
                        for (int i = 0; i < 4; i++)
                            Scales[i] = new IndirectTexScale(reader);

                        TevStages = new IndirectTevStage[16];
                        for (int i = 0; i < 16; i++)
                            TevStages[i] = new IndirectTevStage(reader);
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte((byte)(HasLookup ? 0x01 : 0x00));
                        writer.WriteByte(IndTexStageNum);

                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);

                        for (int i = 0; i < 4; i++)
                            TevOrders[i].Value.Write(writer);

                        for (int i = 0; i < 3; i++)
                            Matrices[i].Write(writer);

                        for (int i = 0; i < 4; i++)
                            Scales[i].Write(writer);

                        for (int i = 0; i < 16; i++)
                            TevStages[i].Write(writer);
                    }

                    public IndirectTexturing Clone()
                    {
                        IndirectTevOrder?[] NewOrders = new IndirectTevOrder?[TevOrders.Length];
                        IndirectTexMatrix[] NewMatrix = new IndirectTexMatrix[Matrices.Length];
                        IndirectTexScale[] NewScales = new IndirectTexScale[Scales.Length];
                        IndirectTevStage[] NewStages = new IndirectTevStage[TevStages.Length];

                        for (int i = 0; i < TevOrders.Length; i++)
                            NewOrders[i] = new IndirectTevOrder(TevOrders[i].Value.TexCoord, TevOrders[i].Value.TexMap);

                        for (int i = 0; i < Matrices.Length; i++)
                            NewMatrix[i] = new IndirectTexMatrix(new Matrix2x3(Matrices[i].Matrix.M11, Matrices[i].Matrix.M12, Matrices[i].Matrix.M13, Matrices[i].Matrix.M21, Matrices[i].Matrix.M22, Matrices[i].Matrix.M23), Matrices[i].Exponent);

                        for (int i = 0; i < Scales.Length; i++)
                            NewScales[i] = new IndirectTexScale(Scales[i].ScaleS, Scales[i].ScaleT);

                        for (int i = 0; i < TevStages.Length; i++)
                            NewStages[i] = new IndirectTevStage(TevStages[i].TevStageID, TevStages[i].IndTexFormat, TevStages[i].IndTexBias, TevStages[i].IndTexMtxId, TevStages[i].IndTexWrapS, TevStages[i].IndTexWrapT, TevStages[i].AddPrev, TevStages[i].UtcLod, TevStages[i].AlphaSel);

                        return new IndirectTexturing() { HasLookup = HasLookup, IndTexStageNum = IndTexStageNum, TevOrders = NewOrders, Matrices = NewMatrix, Scales = NewScales, TevStages = NewStages };
                    }

                    public override bool Equals(object obj)
                    {
                        return obj is IndirectTexturing texturing &&
                               HasLookup == texturing.HasLookup &&
                               IndTexStageNum == texturing.IndTexStageNum &&
                               EqualityComparer<IndirectTevOrder?[]>.Default.Equals(TevOrders, texturing.TevOrders) &&
                               EqualityComparer<IndirectTexMatrix[]>.Default.Equals(Matrices, texturing.Matrices) &&
                               EqualityComparer<IndirectTexScale[]>.Default.Equals(Scales, texturing.Scales) &&
                               EqualityComparer<IndirectTevStage[]>.Default.Equals(TevStages, texturing.TevStages);
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = -407782791;
                        hashCode = hashCode * -1521134295 + HasLookup.GetHashCode();
                        hashCode = hashCode * -1521134295 + IndTexStageNum.GetHashCode();
                        hashCode = hashCode * -1521134295 + EqualityComparer<IndirectTevOrder?[]>.Default.GetHashCode(TevOrders);
                        hashCode = hashCode * -1521134295 + EqualityComparer<IndirectTexMatrix[]>.Default.GetHashCode(Matrices);
                        hashCode = hashCode * -1521134295 + EqualityComparer<IndirectTexScale[]>.Default.GetHashCode(Scales);
                        hashCode = hashCode * -1521134295 + EqualityComparer<IndirectTevStage[]>.Default.GetHashCode(TevStages);
                        return hashCode;
                    }

                    public struct IndirectTevOrder
                    {
                        public TexCoordId TexCoord;
                        public TexMapId TexMap;

                        public IndirectTevOrder(TexCoordId coordId, TexMapId mapId)
                        {
                            TexCoord = coordId;
                            TexMap = mapId;
                        }

                        public IndirectTevOrder(Stream reader)
                        {
                            TexCoord = (TexCoordId)reader.ReadByte();
                            TexMap = (TexMapId)reader.ReadByte();
                        }

                        public void Write(Stream writer)
                        {
                            writer.WriteByte((byte)TexCoord);
                            writer.WriteByte((byte)TexMap);
                            writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2); //TODO: remove?
                        }

                        public override bool Equals(object obj)
                        {
                            if (!(obj is IndirectTevOrder order))
                                return false;

                            return TexCoord == order.TexCoord &&
                                   TexMap == order.TexMap;
                        }

                        public override int GetHashCode()
                        {
                            var hashCode = -584153469;
                            hashCode = hashCode * -1521134295 + TexCoord.GetHashCode();
                            hashCode = hashCode * -1521134295 + TexMap.GetHashCode();
                            return hashCode;
                        }

                        public static bool operator ==(IndirectTevOrder order1, IndirectTevOrder order2) => order1.Equals(order2);

                        public static bool operator !=(IndirectTevOrder order1, IndirectTevOrder order2) => !(order1 == order2);
                    }
                    public struct IndirectTexMatrix
                    {
                        /// <summary>
                        /// The floats that make up the matrix
                        /// </summary>
                        public Matrix2x3 Matrix;
                        /// <summary>
                        /// The exponent (of 2) to multiply the matrix by
                        /// </summary>
                        public byte Exponent;

                        public IndirectTexMatrix(Matrix2x3 matrix, byte exponent)
                        {
                            Matrix = matrix;

                            Exponent = exponent;
                        }

                        public IndirectTexMatrix(Stream reader)
                        {
                            Matrix = new Matrix2x3(
                                BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0),
                                BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0));

                            Exponent = (byte)reader.ReadByte();

                            reader.Position += 0x03;
                        }

                        public void Write(Stream writer)
                        {
                            writer.WriteReverse(BitConverter.GetBytes(Matrix.M11), 0, 4);
                            writer.WriteReverse(BitConverter.GetBytes(Matrix.M12), 0, 4);
                            writer.WriteReverse(BitConverter.GetBytes(Matrix.M13), 0, 4);

                            writer.WriteReverse(BitConverter.GetBytes(Matrix.M21), 0, 4);
                            writer.WriteReverse(BitConverter.GetBytes(Matrix.M22), 0, 4);
                            writer.WriteReverse(BitConverter.GetBytes(Matrix.M23), 0, 4);

                            writer.WriteByte(Exponent);
                            writer.WriteByte(0xFF);
                            writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                        }

                        public override bool Equals(object obj)
                        {
                            if (!(obj is IndirectTexMatrix matrix))
                                return false;
                            
                            return Matrix.Equals(matrix.Matrix) &&
                                   Exponent == matrix.Exponent;
                        }

                        public override int GetHashCode()
                        {
                            var hashCode = 428002898;
                            hashCode = hashCode * -1521134295 + EqualityComparer<Matrix2x3>.Default.GetHashCode(Matrix);
                            hashCode = hashCode * -1521134295 + Exponent.GetHashCode();
                            return hashCode;
                        }

                        public static bool operator ==(IndirectTexMatrix matrix1, IndirectTexMatrix matrix2)
                        {
                            return matrix1.Equals(matrix2);
                        }

                        public static bool operator !=(IndirectTexMatrix matrix1, IndirectTexMatrix matrix2)
                        {
                            return !(matrix1 == matrix2);
                        }
                    }
                    public class IndirectTexScale
                    {
                        /// <summary>
                        /// Scale value for the source texture coordinates' S (U) component
                        /// </summary>
                        public IndirectScale ScaleS { get; private set; }
                        /// <summary>
                        /// Scale value for the source texture coordinates' T (V) component
                        /// </summary>
                        public IndirectScale ScaleT { get; private set; }

                        public IndirectTexScale(IndirectScale s, IndirectScale t)
                        {
                            ScaleS = s;
                            ScaleT = t;
                        }

                        public IndirectTexScale(Stream reader)
                        {
                            ScaleS = (IndirectScale)reader.ReadByte();
                            ScaleT = (IndirectScale)reader.ReadByte();
                            reader.Position += 0x02;
                        }

                        public void Write(Stream writer)
                        {
                            writer.WriteByte((byte)ScaleS);
                            writer.WriteByte((byte)ScaleT);
                            writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                        }

                        public override bool Equals(object obj)
                        {
                            var scale = obj as IndirectTexScale;
                            return scale != null &&
                                   ScaleS == scale.ScaleS &&
                                   ScaleT == scale.ScaleT;
                        }

                        public override int GetHashCode()
                        {
                            var hashCode = 302584437;
                            hashCode = hashCode * -1521134295 + ScaleS.GetHashCode();
                            hashCode = hashCode * -1521134295 + ScaleT.GetHashCode();
                            return hashCode;
                        }

                        public static bool operator ==(IndirectTexScale scale1, IndirectTexScale scale2)
                        {
                            return EqualityComparer<IndirectTexScale>.Default.Equals(scale1, scale2);
                        }

                        public static bool operator !=(IndirectTexScale scale1, IndirectTexScale scale2)
                        {
                            return !(scale1 == scale2);
                        }
                    }
                    public struct IndirectTevStage
                    {
                        public TevStageId TevStageID;
                        public IndirectFormat IndTexFormat;
                        public IndirectBias IndTexBias;
                        public IndirectMatrix IndTexMtxId;
                        public IndirectWrap IndTexWrapS;
                        public IndirectWrap IndTexWrapT;
                        public bool AddPrev;
                        public bool UtcLod;
                        public IndirectAlpha AlphaSel;

                        public IndirectTevStage(TevStageId stageId, IndirectFormat format, IndirectBias bias, IndirectMatrix matrixId, IndirectWrap wrapS, IndirectWrap wrapT, bool addPrev, bool utcLod, IndirectAlpha alphaSel)
                        {
                            TevStageID = stageId;
                            IndTexFormat = format;
                            IndTexBias = bias;
                            IndTexMtxId = matrixId;
                            IndTexWrapS = wrapS;
                            IndTexWrapT = wrapT;
                            AddPrev = addPrev;
                            UtcLod = utcLod;
                            AlphaSel = alphaSel;
                        }

                        public IndirectTevStage(Stream reader)
                        {
                            TevStageID = (TevStageId)reader.ReadByte();
                            IndTexFormat = (IndirectFormat)reader.ReadByte();
                            IndTexBias = (IndirectBias)reader.ReadByte();
                            IndTexMtxId = (IndirectMatrix)reader.ReadByte();
                            IndTexWrapS = (IndirectWrap)reader.ReadByte();
                            IndTexWrapT = (IndirectWrap)reader.ReadByte();
                            AddPrev = reader.ReadByte() > 0;
                            UtcLod = reader.ReadByte() > 0;
                            AlphaSel = (IndirectAlpha)reader.ReadByte();
                            reader.Position += 0x03;
                        }

                        public void Write(Stream writer)
                        {
                            writer.WriteByte((byte)TevStageID);
                            writer.WriteByte((byte)IndTexFormat);
                            writer.WriteByte((byte)IndTexBias);
                            writer.WriteByte((byte)IndTexMtxId);
                            writer.WriteByte((byte)IndTexWrapS);
                            writer.WriteByte((byte)IndTexWrapT);
                            writer.WriteByte((byte)(AddPrev ? 0x01 : 0x00));
                            writer.WriteByte((byte)(UtcLod ? 0x01 : 0x00));
                            writer.WriteByte((byte)AlphaSel);

                            writer.Write(new byte[] { 0xFF, 0xFF, 0xFF }, 0, 3);
                        }

                        public override bool Equals(object obj)
                        {
                            if (!(obj is IndirectTevStage stage))
                                return false;
                            
                            return TevStageID == stage.TevStageID &&
                                   IndTexFormat == stage.IndTexFormat &&
                                   IndTexBias == stage.IndTexBias &&
                                   IndTexMtxId == stage.IndTexMtxId &&
                                   IndTexWrapS == stage.IndTexWrapS &&
                                   IndTexWrapT == stage.IndTexWrapT &&
                                   AddPrev == stage.AddPrev &&
                                   UtcLod == stage.UtcLod &&
                                   AlphaSel == stage.AlphaSel;
                        }

                        public override int GetHashCode()
                        {
                            var hashCode = -1309543118;
                            hashCode = hashCode * -1521134295 + TevStageID.GetHashCode();
                            hashCode = hashCode * -1521134295 + IndTexFormat.GetHashCode();
                            hashCode = hashCode * -1521134295 + IndTexBias.GetHashCode();
                            hashCode = hashCode * -1521134295 + IndTexMtxId.GetHashCode();
                            hashCode = hashCode * -1521134295 + IndTexWrapS.GetHashCode();
                            hashCode = hashCode * -1521134295 + IndTexWrapT.GetHashCode();
                            hashCode = hashCode * -1521134295 + AddPrev.GetHashCode();
                            hashCode = hashCode * -1521134295 + UtcLod.GetHashCode();
                            hashCode = hashCode * -1521134295 + AlphaSel.GetHashCode();
                            return hashCode;
                        }

                        public static bool operator ==(IndirectTevStage stage1, IndirectTevStage stage2) => stage1.Equals(stage2);

                        public static bool operator !=(IndirectTevStage stage1, IndirectTevStage stage2) => !(stage1 == stage2);
                    }

                    public enum IndirectFormat
                    {
                        ITF_8,
                        ITF_5,
                        ITF_4,
                        ITF_3
                    }
                    public enum IndirectBias
                    {
                        None,
                        S,
                        T,
                        ST,
                        U,
                        SU,
                        TU,
                        STU
                    }
                    public enum IndirectAlpha
                    {
                        ITBA_OFF,

                        ITBA_S,
                        ITBA_T,
                        ITBA_U
                    }
                    public enum IndirectMatrix
                    {
                        ITM_OFF,

                        ITM_0,
                        ITM_1,
                        ITM_2,

                        ITM_S0 = 5,
                        ITM_S1,
                        ITM_S2,

                        ITM_T0 = 9,
                        ITM_T1,
                        ITM_T2
                    }
                    public enum IndirectWrap
                    {
                        ITW_OFF,

                        ITW_256,
                        ITW_128,
                        ITW_64,
                        ITW_32,
                        ITW_16,
                        ITW_0
                    }
                    public enum IndirectScale
                    {
                        ITS_1,
                        ITS_2,
                        ITS_4,
                        ITS_8,
                        ITS_16,
                        ITS_32,
                        ITS_64,
                        ITS_128,
                        ITS_256
                    }

                    public static bool operator ==(IndirectTexturing texturing1, IndirectTexturing texturing2) => texturing1.Equals(texturing2);

                    public static bool operator !=(IndirectTexturing texturing1, IndirectTexturing texturing2) => !(texturing1 == texturing2);
                }
                public struct ChannelControl
                {
                    public bool Enable;
                    public ColorSrc MaterialSrcColor;
                    public LightId LitMask;
                    public DiffuseFn DiffuseFunction;
                    public J3DAttenuationFn AttenuationFunction;
                    public ColorSrc AmbientSrcColor;

                    public ChannelControl(bool enable, ColorSrc matSrcColor, LightId litMask, DiffuseFn diffFn, J3DAttenuationFn attenFn, ColorSrc ambSrcColor)
                    {
                        Enable = enable;
                        MaterialSrcColor = matSrcColor;
                        LitMask = litMask;
                        DiffuseFunction = diffFn;
                        AttenuationFunction = attenFn;
                        AmbientSrcColor = ambSrcColor;
                    }

                    public ChannelControl(Stream reader)
                    {
                        Enable = reader.ReadByte() > 0;
                        MaterialSrcColor = (ColorSrc)reader.ReadByte();
                        LitMask = (LightId)reader.ReadByte();
                        DiffuseFunction = (DiffuseFn)reader.ReadByte();
                        AttenuationFunction = (J3DAttenuationFn)reader.ReadByte();
                        AmbientSrcColor = (ColorSrc)reader.ReadByte();

                        reader.Position += 0x02;
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte((byte)(Enable ? 0x01 : 0x00));
                        writer.WriteByte((byte)MaterialSrcColor);
                        writer.WriteByte((byte)LitMask);
                        writer.WriteByte((byte)DiffuseFunction);
                        writer.WriteByte((byte)AttenuationFunction);
                        writer.WriteByte((byte)AmbientSrcColor);

                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                    }

                    public ChannelControl Clone() => new ChannelControl(Enable, MaterialSrcColor, LitMask, DiffuseFunction, AttenuationFunction, AmbientSrcColor);

                    public override bool Equals(object obj)
                    {
                        if (!(obj is ChannelControl control))
                            return false;
                        return Enable == control.Enable &&
                               MaterialSrcColor == control.MaterialSrcColor &&
                               LitMask == control.LitMask &&
                               DiffuseFunction == control.DiffuseFunction &&
                               AttenuationFunction == control.AttenuationFunction &&
                               AmbientSrcColor == control.AmbientSrcColor;
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = -1502031869;
                        hashCode = hashCode * -1521134295 + Enable.GetHashCode();
                        hashCode = hashCode * -1521134295 + MaterialSrcColor.GetHashCode();
                        hashCode = hashCode * -1521134295 + LitMask.GetHashCode();
                        hashCode = hashCode * -1521134295 + DiffuseFunction.GetHashCode();
                        hashCode = hashCode * -1521134295 + AttenuationFunction.GetHashCode();
                        hashCode = hashCode * -1521134295 + AmbientSrcColor.GetHashCode();
                        return hashCode;
                    }

                    public enum ColorSrc
                    {
                        Register = 0, // Use Register Colors
                        Vertex = 1 // Use Vertex Colors
                    }
                    public enum LightId
                    {
                        Light0 = 0x001,
                        Light1 = 0x002,
                        Light2 = 0x004,
                        Light3 = 0x008,
                        Light4 = 0x010,
                        Light5 = 0x020,
                        Light6 = 0x040,
                        Light7 = 0x080,
                        None = 0x000
                    }
                    public enum DiffuseFn
                    {
                        None = 0,
                        Signed = 1,
                        Clamp = 2
                    }
                    public enum J3DAttenuationFn
                    {
                        None_0 = 0,
                        Spec = 1,
                        None_2 = 2,
                        Spot = 3
                    }

                    public static bool operator ==(ChannelControl control1, ChannelControl control2) => control1.Equals(control2);

                    public static bool operator !=(ChannelControl control1, ChannelControl control2) => !(control1 == control2);
                }
                public struct TexCoordGen
                {
                    public TexGenType Type;
                    public TexGenSrc Source;
                    public TexMatrixId TexMatrixSource;

                    public TexCoordGen(Stream reader)
                    {
                        Type = (TexGenType)reader.ReadByte();
                        Source = (TexGenSrc)reader.ReadByte();
                        TexMatrixSource = (TexMatrixId)reader.ReadByte();

                        reader.Position++;
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte((byte)Type);
                        writer.WriteByte((byte)Source);
                        writer.WriteByte((byte)TexMatrixSource);

                        // Pad entry to 4 bytes
                        writer.WriteByte(0xFF);
                    }

                    public TexCoordGen Clone() => new TexCoordGen() { Type = Type, Source = Source, TexMatrixSource = TexMatrixSource };

                    public override bool Equals(object obj)
                    {
                        if (!(obj is TexCoordGen gen))
                            return false;
                        return Type == gen.Type &&
                               Source == gen.Source &&
                               TexMatrixSource == gen.TexMatrixSource;
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = -1253954333;
                        hashCode = hashCode * -1521134295 + Type.GetHashCode();
                        hashCode = hashCode * -1521134295 + Source.GetHashCode();
                        hashCode = hashCode * -1521134295 + TexMatrixSource.GetHashCode();
                        return hashCode;
                    }

                    public static bool operator ==(TexCoordGen gen1, TexCoordGen gen2) => gen1.Equals(gen2);

                    public static bool operator !=(TexCoordGen gen1, TexCoordGen gen2) => !(gen1 == gen2);
                }
                public struct TexMatrix
                {
                    public TexGenType Projection;
                    public TexMtxMapMode MappingMode;
                    public bool IsMaya;
                    public Vector3 Center;
                    public Vector2 Scale;
                    public float Rotation;
                    public Vector2 Translation;
                    public Matrix4 ProjectionMatrix;

                    public TexMatrix(TexGenType projection, byte info, Vector3 effectTranslation, Vector2 scale, float rotation, Vector2 translation, Matrix4 matrix)
                    {
                        Projection = projection;
                        MappingMode = (TexMtxMapMode)(info & 0x3F);
                        IsMaya = (info & ~0x3F) != 0;
                        Center = effectTranslation;

                        Scale = scale;
                        Rotation = rotation;
                        Translation = translation;

                        ProjectionMatrix = matrix;
                    }

                    public TexMatrix(Stream reader)
                    {
                        Projection = (TexGenType)reader.ReadByte();
                        byte info = (byte)reader.ReadByte();
                        MappingMode = (TexMtxMapMode)(info & 0x3F);
                        IsMaya = (info & ~0x3F) != 0;
                        reader.Position += 0x02;
                        Center = new Vector3(BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0));
                        Scale = new Vector2(BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0));
                        Rotation = BitConverter.ToInt16(reader.ReadReverse(0, 2), 0) * (180 / 32768f);
                        reader.Position += 0x02;
                        Translation = new Vector2(BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0));

                        ProjectionMatrix = new Matrix4(
                            BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0),
                            BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0),
                            BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0),
                            BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0));
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte((byte)Projection);
                        writer.WriteByte((byte)((IsMaya ? 0x80 : 0) | (byte)MappingMode));
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                        writer.WriteReverse(BitConverter.GetBytes(Center.X), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(Center.Y), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(Center.Z), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(Scale.X), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(Scale.Y), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes((short)(Rotation * (32768.0f / 180))), 0, 2);
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                        writer.WriteReverse(BitConverter.GetBytes(Translation.X), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(Translation.Y), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M11), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M12), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M13), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M14), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M21), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M22), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M23), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M24), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M31), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M32), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M33), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M34), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M41), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M42), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M43), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(ProjectionMatrix.M44), 0, 4);
                    }

                    public TexMatrix Clone()
                    {
                        return new TexMatrix()
                        {
                            Projection = Projection,
                            MappingMode = MappingMode,
                            IsMaya = IsMaya,
                            Center = new Vector3(Center.X, Center.Y, Center.Z),
                            Scale = new Vector2(Scale.X, Scale.Y),
                            Rotation = Rotation,
                            Translation = new Vector2(Translation.X, Translation.Y),
                            ProjectionMatrix = new Matrix4(
                                ProjectionMatrix.M11, ProjectionMatrix.M12, ProjectionMatrix.M13, ProjectionMatrix.M14,
                                ProjectionMatrix.M21, ProjectionMatrix.M22, ProjectionMatrix.M23, ProjectionMatrix.M24,
                                ProjectionMatrix.M31, ProjectionMatrix.M32, ProjectionMatrix.M33, ProjectionMatrix.M34,
                                ProjectionMatrix.M41, ProjectionMatrix.M42, ProjectionMatrix.M43, ProjectionMatrix.M44)
                        };
                    }

                    public override bool Equals(object obj)
                    {
                        if (!(obj is TexMatrix matrix))
                            return false;
                        
                        return Projection == matrix.Projection &&
                               MappingMode == matrix.MappingMode &&
                               IsMaya == matrix.IsMaya &&
                               Center.Equals(matrix.Center) &&
                               Scale.Equals(matrix.Scale) &&
                               Rotation == matrix.Rotation &&
                               Translation.Equals(matrix.Translation) &&
                               ProjectionMatrix.Equals(matrix.ProjectionMatrix);
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = 1621759504;
                        hashCode = hashCode * -1521134295 + Projection.GetHashCode();
                        hashCode = hashCode * -1521134295 + MappingMode.GetHashCode();
                        hashCode = hashCode * -1521134295 + IsMaya.GetHashCode();
                        hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(Center);
                        hashCode = hashCode * -1521134295 + EqualityComparer<Vector2>.Default.GetHashCode(Scale);
                        hashCode = hashCode * -1521134295 + Rotation.GetHashCode();
                        hashCode = hashCode * -1521134295 + EqualityComparer<Vector2>.Default.GetHashCode(Translation);
                        hashCode = hashCode * -1521134295 + EqualityComparer<Matrix4>.Default.GetHashCode(ProjectionMatrix);
                        return hashCode;
                    }

                    public static bool operator ==(TexMatrix matrix1, TexMatrix matrix2) => matrix1.Equals(matrix2);

                    public static bool operator !=(TexMatrix matrix1, TexMatrix matrix2) => !(matrix1 == matrix2);
                }
                public struct TevOrder
                {
                    public TexCoordId TexCoord;
                    public TexMapId TexMap;
                    public GXColorChannelId ChannelId;

                    public TevOrder(TexCoordId texCoord, TexMapId texMap, GXColorChannelId chanID)
                    {
                        TexCoord = texCoord;
                        TexMap = texMap;
                        ChannelId = chanID;
                    }

                    public TevOrder(Stream reader)
                    {
                        TexCoord = (TexCoordId)reader.ReadByte();
                        TexMap = (TexMapId)reader.ReadByte();
                        ChannelId = (GXColorChannelId)reader.ReadByte();
                        reader.Position++;
                    }
                    public void Write(Stream writer)
                    {
                        writer.WriteByte((byte)TexCoord);
                        writer.WriteByte((byte)TexMap);
                        writer.WriteByte((byte)ChannelId);
                        writer.WriteByte(0xFF);
                    }

                    public TevOrder Clone() => new TevOrder(TexCoord, TexMap, ChannelId);

                    public override bool Equals(object obj)
                    {
                        if (!(obj is TevOrder order))
                            return false;
                        return TexCoord == order.TexCoord &&
                               TexMap == order.TexMap &&
                               ChannelId == order.ChannelId;
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = -1126351388;
                        hashCode = hashCode * -1521134295 + TexCoord.GetHashCode();
                        hashCode = hashCode * -1521134295 + TexMap.GetHashCode();
                        hashCode = hashCode * -1521134295 + ChannelId.GetHashCode();
                        return hashCode;
                    }

                    public enum GXColorChannelId
                    {
                        Color0 = 0,
                        Color1 = 1,
                        Alpha0 = 2,
                        Alpha1 = 3,
                        Color0A0 = 4,
                        Color1A1 = 5,
                        ColorZero = 6,
                        AlphaBump = 7,
                        AlphaBumpN = 8,
                        ColorNull = 0xFF
                    }

                    public static bool operator ==(TevOrder order1, TevOrder order2) => order1.Equals(order2);

                    public static bool operator !=(TevOrder order1, TevOrder order2) => !(order1 == order2);
                }
                public struct TevStage
                {
                    public CombineColorInput ColorInA;
                    public CombineColorInput ColorInB;
                    public CombineColorInput ColorInC;
                    public CombineColorInput ColorInD;

                    public TevOp ColorOp;
                    public TevBias ColorBias;
                    public TevScale ColorScale;
                    public bool ColorClamp;
                    public TevRegisterId ColorRegId;

                    public CombineAlphaInput AlphaInA;
                    public CombineAlphaInput AlphaInB;
                    public CombineAlphaInput AlphaInC;
                    public CombineAlphaInput AlphaInD;

                    public TevOp AlphaOp;
                    public TevBias AlphaBias;
                    public TevScale AlphaScale;
                    public bool AlphaClamp;
                    public TevRegisterId AlphaRegId;

                    public TevStage(Stream reader)
                    {
                        reader.Position++;

                        ColorInA = (CombineColorInput)reader.ReadByte();
                        ColorInB = (CombineColorInput)reader.ReadByte();
                        ColorInC = (CombineColorInput)reader.ReadByte();
                        ColorInD = (CombineColorInput)reader.ReadByte();

                        ColorOp = (TevOp)reader.ReadByte();
                        ColorBias = (TevBias)reader.ReadByte();
                        ColorScale = (TevScale)reader.ReadByte();
                        ColorClamp = reader.ReadByte() > 0;
                        ColorRegId = (TevRegisterId)reader.ReadByte();

                        AlphaInA = (CombineAlphaInput)reader.ReadByte();
                        AlphaInB = (CombineAlphaInput)reader.ReadByte();
                        AlphaInC = (CombineAlphaInput)reader.ReadByte();
                        AlphaInD = (CombineAlphaInput)reader.ReadByte();

                        AlphaOp = (TevOp)reader.ReadByte();
                        AlphaBias = (TevBias)reader.ReadByte();
                        AlphaScale = (TevScale)reader.ReadByte();
                        AlphaClamp = reader.ReadByte() > 0;
                        AlphaRegId = (TevRegisterId)reader.ReadByte();

                        reader.Position++;
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte(0xFF);

                        writer.WriteByte((byte)ColorInA);
                        writer.WriteByte((byte)ColorInB);
                        writer.WriteByte((byte)ColorInC);
                        writer.WriteByte((byte)ColorInD);

                        writer.WriteByte((byte)ColorOp);
                        writer.WriteByte((byte)ColorBias);
                        writer.WriteByte((byte)ColorScale);
                        writer.WriteByte((byte)(ColorClamp ? 0x01 : 0x00));
                        writer.WriteByte((byte)ColorRegId);

                        writer.WriteByte((byte)AlphaInA);
                        writer.WriteByte((byte)AlphaInB);
                        writer.WriteByte((byte)AlphaInC);
                        writer.WriteByte((byte)AlphaInD);

                        writer.WriteByte((byte)AlphaOp);
                        writer.WriteByte((byte)AlphaBias);
                        writer.WriteByte((byte)AlphaScale);
                        writer.WriteByte((byte)(AlphaClamp ? 0x01 : 0x00));
                        writer.WriteByte((byte)AlphaRegId);

                        writer.WriteByte(0xFF);
                    }

                    public TevStage Clone() => new TevStage()
                    {
                        ColorInA = ColorInA,
                        ColorInB = ColorInB,
                        ColorInC = ColorInC,
                        ColorInD = ColorInD,
                        ColorOp = ColorOp,
                        ColorBias = ColorBias,
                        ColorScale = ColorScale,
                        ColorClamp = ColorClamp,
                        ColorRegId = ColorRegId,
                        AlphaInA = AlphaInA,
                        AlphaInB = AlphaInB,
                        AlphaInC = AlphaInC,
                        AlphaInD = AlphaInD,
                        AlphaOp = AlphaOp,
                        AlphaBias = AlphaBias,
                        AlphaScale = AlphaScale,
                        AlphaClamp = AlphaClamp,
                        AlphaRegId = AlphaRegId
                    };

                    public override string ToString()
                    {
                        string ret = "";

                        ret += $"Color In A: { ColorInA }\n";
                        ret += $"Color In B: { ColorInB }\n";
                        ret += $"Color In C: { ColorInC }\n";
                        ret += $"Color In D: { ColorInD }\n";

                        ret += '\n';

                        ret += $"Color Op: { ColorOp }\n";
                        ret += $"Color Bias: { ColorBias }\n";
                        ret += $"Color Scale: { ColorScale }\n";
                        ret += $"Color Clamp: { ColorClamp }\n";
                        ret += $"Color Reg ID: { ColorRegId }\n";

                        ret += '\n';

                        ret += $"Alpha In A: { AlphaInA }\n";
                        ret += $"Alpha In B: { AlphaInB }\n";
                        ret += $"Alpha In C: { AlphaInC }\n";
                        ret += $"Alpha In D: { AlphaInD }\n";

                        ret += '\n';

                        ret += $"Alpha Op: { AlphaOp }\n";
                        ret += $"Alpha Bias: { AlphaBias }\n";
                        ret += $"Alpha Scale: { AlphaScale }\n";
                        ret += $"Alpha Clamp: { AlphaClamp }\n";
                        ret += $"Alpha Reg ID: { AlphaRegId }\n";

                        ret += '\n';

                        return ret;
                    }

                    public override bool Equals(object obj)
                    {
                        if (!(obj is TevStage stage))
                            return false;

                        return ColorInA == stage.ColorInA &&
                               ColorInB == stage.ColorInB &&
                               ColorInC == stage.ColorInC &&
                               ColorInD == stage.ColorInD &&
                               ColorOp == stage.ColorOp &&
                               ColorBias == stage.ColorBias &&
                               ColorScale == stage.ColorScale &&
                               ColorClamp == stage.ColorClamp &&
                               ColorRegId == stage.ColorRegId &&
                               AlphaInA == stage.AlphaInA &&
                               AlphaInB == stage.AlphaInB &&
                               AlphaInC == stage.AlphaInC &&
                               AlphaInD == stage.AlphaInD &&
                               AlphaOp == stage.AlphaOp &&
                               AlphaBias == stage.AlphaBias &&
                               AlphaScale == stage.AlphaScale &&
                               AlphaClamp == stage.AlphaClamp &&
                               AlphaRegId == stage.AlphaRegId;
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = -411571779;
                        hashCode = hashCode * -1521134295 + ColorInA.GetHashCode();
                        hashCode = hashCode * -1521134295 + ColorInB.GetHashCode();
                        hashCode = hashCode * -1521134295 + ColorInC.GetHashCode();
                        hashCode = hashCode * -1521134295 + ColorInD.GetHashCode();
                        hashCode = hashCode * -1521134295 + ColorOp.GetHashCode();
                        hashCode = hashCode * -1521134295 + ColorBias.GetHashCode();
                        hashCode = hashCode * -1521134295 + ColorScale.GetHashCode();
                        hashCode = hashCode * -1521134295 + ColorClamp.GetHashCode();
                        hashCode = hashCode * -1521134295 + ColorRegId.GetHashCode();
                        hashCode = hashCode * -1521134295 + AlphaInA.GetHashCode();
                        hashCode = hashCode * -1521134295 + AlphaInB.GetHashCode();
                        hashCode = hashCode * -1521134295 + AlphaInC.GetHashCode();
                        hashCode = hashCode * -1521134295 + AlphaInD.GetHashCode();
                        hashCode = hashCode * -1521134295 + AlphaOp.GetHashCode();
                        hashCode = hashCode * -1521134295 + AlphaBias.GetHashCode();
                        hashCode = hashCode * -1521134295 + AlphaScale.GetHashCode();
                        hashCode = hashCode * -1521134295 + AlphaClamp.GetHashCode();
                        hashCode = hashCode * -1521134295 + AlphaRegId.GetHashCode();
                        return hashCode;
                    }

                    public enum CombineColorInput
                    {
                        ColorPrev = 0,  // ! < Use Color Value from previous TEV stage
                        AlphaPrev = 1,  // ! < Use Alpha Value from previous TEV stage
                        C0 = 2,         // ! < Use the Color Value from the Color/Output Register 0
                        A0 = 3,         // ! < Use the Alpha value from the Color/Output Register 0
                        C1 = 4,         // ! < Use the Color Value from the Color/Output Register 1
                        A1 = 5,         // ! < Use the Alpha value from the Color/Output Register 1
                        C2 = 6,         // ! < Use the Color Value from the Color/Output Register 2
                        A2 = 7,         // ! < Use the Alpha value from the Color/Output Register 2
                        TexColor = 8,   // ! < Use the Color value from Texture
                        TexAlpha = 9,   // ! < Use the Alpha value from Texture
                        RasColor = 10,  // ! < Use the color value from rasterizer
                        RasAlpha = 11,  // ! < Use the alpha value from rasterizer
                        One = 12,
                        Half = 13,
                        Konst = 14,
                        Zero = 15       // 
                    }
                    public enum CombineAlphaInput
                    {
                        AlphaPrev = 0,  // Use the Alpha value form the previous TEV stage
                        A0 = 1,         // Use the Alpha value from the Color/Output Register 0
                        A1 = 2,         // Use the Alpha value from the Color/Output Register 1
                        A2 = 3,         // Use the Alpha value from the Color/Output Register 2
                        TexAlpha = 4,   // Use the Alpha value from the Texture
                        RasAlpha = 5,   // Use the Alpha value from the rasterizer
                        Konst = 6,
                        Zero = 7
                    }
                    public enum TevOp
                    {
                        Add = 0,
                        Sub = 1,
                        Comp_R8_GT = 8,
                        Comp_R8_EQ = 9,
                        Comp_GR16_GT = 10,
                        Comp_GR16_EQ = 11,
                        Comp_BGR24_GT = 12,
                        Comp_BGR24_EQ = 13,
                        Comp_RGB8_GT = 14,
                        Comp_RGB8_EQ = 15,
                        Comp_A8_EQ = Comp_RGB8_EQ,
                        Comp_A8_GT = Comp_RGB8_GT
                    }
                    public enum TevBias
                    {
                        Zero = 0,
                        AddHalf = 1,
                        SubHalf = 2
                    }
                    public enum TevScale
                    {
                        Scale_1 = 0,
                        Scale_2 = 1,
                        Scale_4 = 2,
                        Divide_2 = 3
                    }
                    public enum TevRegisterId
                    {
                        TevPrev,
                        TevReg0,
                        TevReg1,
                        TevReg2
                    }

                    public static bool operator ==(TevStage stage1, TevStage stage2) => stage1.Equals(stage2);

                    public static bool operator !=(TevStage stage1, TevStage stage2) => !(stage1 == stage2);
                }
                public struct TevSwapMode
                {
                    public byte RasSel;
                    public byte TexSel;

                    public TevSwapMode(byte rasSel, byte texSel)
                    {
                        RasSel = rasSel;
                        TexSel = texSel;
                    }

                    public TevSwapMode(Stream reader)
                    {
                        RasSel = (byte)reader.ReadByte();
                        TexSel = (byte)reader.ReadByte();
                        reader.Position += 0x02;
                    }
                    public void Write(Stream writer)
                    {
                        writer.WriteByte(RasSel);
                        writer.WriteByte(TexSel);
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                    }

                    public TevSwapMode Clone() => new TevSwapMode(RasSel, TexSel);

                    public override bool Equals(object obj)
                    {
                        if (!(obj is TevSwapMode mode))
                            return false;
                        return RasSel == mode.RasSel &&
                               TexSel == mode.TexSel;
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = 2132594825;
                        hashCode = hashCode * -1521134295 + RasSel.GetHashCode();
                        hashCode = hashCode * -1521134295 + TexSel.GetHashCode();
                        return hashCode;
                    }

                    public static bool operator ==(TevSwapMode mode1, TevSwapMode mode2)
                    {
                        return mode1.Equals(mode2);
                    }

                    public static bool operator !=(TevSwapMode mode1, TevSwapMode mode2)
                    {
                        return !(mode1 == mode2);
                    }
                }
                public struct TevSwapModeTable
                {
                    public byte R;
                    public byte G;
                    public byte B;
                    public byte A;

                    public TevSwapModeTable(byte r, byte g, byte b, byte a)
                    {
                        R = r;
                        G = g;
                        B = b;
                        A = a;
                    }

                    public TevSwapModeTable(Stream reader)
                    {
                        R = (byte)reader.ReadByte();
                        G = (byte)reader.ReadByte();
                        B = (byte)reader.ReadByte();
                        A = (byte)reader.ReadByte();
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte(R);
                        writer.WriteByte(G);
                        writer.WriteByte(B);
                        writer.WriteByte(A);
                    }

                    public TevSwapModeTable Clone() => new TevSwapModeTable(R, G, B, A);

                    public override bool Equals(object obj)
                    {
                        if (!(obj is TevSwapModeTable table))
                            return false;
                        return R == table.R &&
                               G == table.G &&
                               B == table.B &&
                               A == table.A;
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = 1960784236;
                        hashCode = hashCode * -1521134295 + R.GetHashCode();
                        hashCode = hashCode * -1521134295 + G.GetHashCode();
                        hashCode = hashCode * -1521134295 + B.GetHashCode();
                        hashCode = hashCode * -1521134295 + A.GetHashCode();
                        return hashCode;
                    }

                    public static bool operator ==(TevSwapModeTable table1, TevSwapModeTable table2)
                    {
                        return table1.Equals(table2);
                    }

                    public static bool operator !=(TevSwapModeTable table1, TevSwapModeTable table2)
                    {
                        return !(table1 == table2);
                    }
                }
                public struct Fog
                {
                    public byte Type;
                    public bool Enable;
                    public ushort Center;
                    public float StartZ;
                    public float EndZ;
                    public float NearZ;
                    public float FarZ;
                    public Color4 Color;
                    public float[] RangeAdjustmentTable;

                    public Fog(byte type, bool enable, ushort center, float startZ, float endZ, float nearZ, float farZ, Color4 color, float[] rangeAdjust)
                    {
                        Type = type;
                        Enable = enable;
                        Center = center;
                        StartZ = startZ;
                        EndZ = endZ;
                        NearZ = nearZ;
                        FarZ = farZ;
                        Color = color;
                        RangeAdjustmentTable = rangeAdjust;
                    }

                    public Fog(Stream reader)
                    {
                        RangeAdjustmentTable = new float[10];

                        Type = (byte)reader.ReadByte();
                        Enable = reader.ReadByte() > 0;
                        Center = BitConverter.ToUInt16(reader.ReadReverse(0, 2), 0);
                        StartZ = BitConverter.ToSingle(reader.ReadReverse(0, 4), 0);
                        EndZ = BitConverter.ToSingle(reader.ReadReverse(0, 4), 0);
                        NearZ = BitConverter.ToSingle(reader.ReadReverse(0, 4), 0);
                        FarZ = BitConverter.ToSingle(reader.ReadReverse(0, 4), 0);
                        Color = new Color4((float)reader.ReadByte() / 255, (float)reader.ReadByte() / 255, (float)reader.ReadByte() / 255, (float)reader.ReadByte() / 255);

                        for (int i = 0; i < 10; i++)
                        {
                            ushort inVal = BitConverter.ToUInt16(reader.ReadReverse(0, 2), 0);
                            RangeAdjustmentTable[i] = (float)inVal / 256;
                        }
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte(Type);
                        writer.WriteByte((byte)(Enable ? 0x01 : 0x00));
                        writer.WriteReverse(BitConverter.GetBytes(Center), 0, 2);
                        writer.WriteReverse(BitConverter.GetBytes(StartZ), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(EndZ), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(NearZ), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(FarZ), 0, 4);
                        writer.WriteByte((byte)(Color.R * 255));
                        writer.WriteByte((byte)(Color.G * 255));
                        writer.WriteByte((byte)(Color.B * 255));
                        writer.WriteByte((byte)(Color.A * 255));

                        for (int i = 0; i < 10; i++)
                            writer.WriteReverse(BitConverter.GetBytes((ushort)(RangeAdjustmentTable[i] * 256)), 0, 2);
                    }

                    public Fog Clone()
                    {
                        float[] temp = new float[RangeAdjustmentTable.Length];
                        for (int i = 0; i < RangeAdjustmentTable.Length; i++)
                            temp[i] = RangeAdjustmentTable[i];
                        return new Fog(Type, Enable, Center, StartZ, EndZ, NearZ, FarZ, new Color4(Color.R, Color.G, Color.B, Color.A), temp);
                    }

                    public override bool Equals(object obj)
                    {
                        if (!(obj is Fog fog))
                            return false;
                        return Type == fog.Type &&
                               Enable == fog.Enable &&
                               Center == fog.Center &&
                               StartZ == fog.StartZ &&
                               EndZ == fog.EndZ &&
                               NearZ == fog.NearZ &&
                               FarZ == fog.FarZ &&
                               Color.Equals(fog.Color) &&
                               EqualityComparer<float[]>.Default.Equals(RangeAdjustmentTable, fog.RangeAdjustmentTable);
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = 1878492404;
                        hashCode = hashCode * -1521134295 + Type.GetHashCode();
                        hashCode = hashCode * -1521134295 + Enable.GetHashCode();
                        hashCode = hashCode * -1521134295 + Center.GetHashCode();
                        hashCode = hashCode * -1521134295 + StartZ.GetHashCode();
                        hashCode = hashCode * -1521134295 + EndZ.GetHashCode();
                        hashCode = hashCode * -1521134295 + NearZ.GetHashCode();
                        hashCode = hashCode * -1521134295 + FarZ.GetHashCode();
                        hashCode = hashCode * -1521134295 + EqualityComparer<Color4>.Default.GetHashCode(Color);
                        hashCode = hashCode * -1521134295 + EqualityComparer<float[]>.Default.GetHashCode(RangeAdjustmentTable);
                        return hashCode;
                    }

                    public static bool operator ==(Fog fog1, Fog fog2) => fog1.Equals(fog2);

                    public static bool operator !=(Fog fog1, Fog fog2) => !(fog1 == fog2);
                }
                public struct AlphaCompare
                {
                    /// <summary> subfunction 0 </summary>
                    public CompareType Comp0;
                    /// <summary> Reference value for subfunction 0. </summary>
                    public byte Reference0;
                    /// <summary> Alpha combine control for subfunctions 0 and 1. </summary>
                    public AlphaOp Operation;
                    /// <summary> subfunction 1 </summary>
                    public CompareType Comp1;
                    /// <summary> Reference value for subfunction 1. </summary>
                    public byte Reference1;

                    public AlphaCompare(CompareType comp0, byte ref0, AlphaOp operation, CompareType comp1, byte ref1)
                    {
                        Comp0 = comp0;
                        Reference0 = ref0;
                        Operation = operation;
                        Comp1 = comp1;
                        Reference1 = ref1;
                    }

                    public AlphaCompare(Stream reader)
                    {
                        Comp0 = (CompareType)reader.ReadByte();
                        Reference0 = (byte)reader.ReadByte();
                        Operation = (AlphaOp)reader.ReadByte();
                        Comp1 = (CompareType)reader.ReadByte();
                        Reference1 = (byte)reader.ReadByte();
                        reader.Position += 0x03;
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte((byte)Comp0);
                        writer.WriteByte(Reference0);
                        writer.WriteByte((byte)Operation);
                        writer.WriteByte((byte)Comp1);
                        writer.WriteByte(Reference1);
                        writer.WriteByte(0xFF);
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                    }

                    public AlphaCompare Clone() => new AlphaCompare(Comp0, Reference0, Operation, Comp1, Reference1);

                    public override bool Equals(object obj)
                    {
                        if (!(obj is AlphaCompare compare))
                            return false;
                        return Comp0 == compare.Comp0 &&
                               Reference0 == compare.Reference0 &&
                               Operation == compare.Operation &&
                               Comp1 == compare.Comp1 &&
                               Reference1 == compare.Reference1;
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = 233009852;
                        hashCode = hashCode * -1521134295 + Comp0.GetHashCode();
                        hashCode = hashCode * -1521134295 + Reference0.GetHashCode();
                        hashCode = hashCode * -1521134295 + Operation.GetHashCode();
                        hashCode = hashCode * -1521134295 + Comp1.GetHashCode();
                        hashCode = hashCode * -1521134295 + Reference1.GetHashCode();
                        return hashCode;
                    }

                    public enum CompareType
                    {
                        Never = 0,
                        Less = 1,
                        Equal = 2,
                        LEqual = 3,
                        Greater = 4,
                        NEqual = 5,
                        GEqual = 6,
                        Always = 7
                    }
                    public enum AlphaOp
                    {
                        And = 0,
                        Or = 1,
                        XOR = 2,
                        XNOR = 3
                    }

                    public static bool operator ==(AlphaCompare compare1, AlphaCompare compare2) => compare1.Equals(compare2);

                    public static bool operator !=(AlphaCompare compare1, AlphaCompare compare2) => !(compare1 == compare2);
                }
                public struct BlendMode
                {
                    /// <summary> Blending Type </summary>
                    public BlendModeID Type;
                    /// <summary> Blending Control </summary>
                    public BlendModeControl SourceFact;
                    /// <summary> Blending Control </summary>
                    public BlendModeControl DestinationFact;
                    /// <summary> What operation is used to blend them when <see cref="Type"/> is set to <see cref="GXBlendMode.Logic"/>. </summary>
                    public LogicOp Operation; // Seems to be logic operators such as clear, and, copy, equiv, inv, invand, etc.

                    public BlendMode(BlendModeID type, BlendModeControl src, BlendModeControl dest, LogicOp operation)
                    {
                        Type = type;
                        SourceFact = src;
                        DestinationFact = dest;
                        Operation = operation;
                    }

                    public BlendMode(Stream reader)
                    {
                        Type = (BlendModeID)reader.ReadByte();
                        SourceFact = (BlendModeControl)reader.ReadByte();
                        DestinationFact = (BlendModeControl)reader.ReadByte();
                        Operation = (LogicOp)reader.ReadByte();
                    }

                    public void Write(Stream write)
                    {
                        write.WriteByte((byte)Type);
                        write.WriteByte((byte)SourceFact);
                        write.WriteByte((byte)DestinationFact);
                        write.WriteByte((byte)Operation);
                    }

                    public BlendMode Clone() => new BlendMode(Type, SourceFact, DestinationFact, Operation);

                    public override bool Equals(object obj)
                    {
                        if (!(obj is BlendMode mode))
                            return false;
                        return Type == mode.Type &&
                               SourceFact == mode.SourceFact &&
                               DestinationFact == mode.DestinationFact &&
                               Operation == mode.Operation;
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = -565238750;
                        hashCode = hashCode * -1521134295 + Type.GetHashCode();
                        hashCode = hashCode * -1521134295 + SourceFact.GetHashCode();
                        hashCode = hashCode * -1521134295 + DestinationFact.GetHashCode();
                        hashCode = hashCode * -1521134295 + Operation.GetHashCode();
                        return hashCode;
                    }

                    public enum BlendModeID
                    {
                        None = 0,
                        Blend = 1,
                        Logic = 2,
                        Subtract = 3
                    }
                    public enum BlendModeControl
                    {
                        Zero = 0,               // ! < 0.0
                        One = 1,                // ! < 1.0
                        SrcColor = 2,           // ! < Source Color
                        InverseSrcColor = 3,    // ! < 1.0 - (Source Color)
                        SrcAlpha = 4,           // ! < Source Alpha
                        InverseSrcAlpha = 5,    // ! < 1.0 - (Source Alpha)
                        DstAlpha = 6,           // ! < Framebuffer Alpha
                        InverseDstAlpha = 7     // ! < 1.0 - (Framebuffer Alpha)
                    }
                    public enum LogicOp
                    {
                        Clear = 0,
                        And = 1,
                        Copy = 3,
                        Equiv = 9,
                        Inv = 10,
                        InvAnd = 4,
                        InvCopy = 12,
                        InvOr = 13,
                        NAnd = 14,
                        NoOp = 5,
                        NOr = 8,
                        Or = 7,
                        RevAnd = 2,
                        RevOr = 11,
                        Set = 15,
                        XOr = 6
                    }

                    public static bool operator ==(BlendMode mode1, BlendMode mode2) => mode1.Equals(mode2);

                    public static bool operator !=(BlendMode mode1, BlendMode mode2) => !(mode1 == mode2);
                }
                public struct ZModeHolder
                {
                    /// <summary> If false, ZBuffering is disabled and the Z buffer is not updated. </summary>
                    public bool Enable;

                    /// <summary> Determines the comparison that is performed.
                    /// The newely rasterized Z value is on the left while the value from the Z buffer is on the right.
                    /// If the result of the comparison is false, the newly rasterized pixel is discarded. </summary>
                    public AlphaCompare.CompareType Function;

                    /// <summary> If true, the Z buffer is updated with the new Z value after a comparison is performed. 
                    /// Example: Disabling this would prevent a write to the Z buffer, useful for UI elements or other things
                    /// that shouldn't write to Z Buffer. See glDepthMask. </summary>
                    public bool UpdateEnable;

                    public ZModeHolder(bool enable, AlphaCompare.CompareType func, bool update)
                    {
                        Enable = enable;
                        Function = func;
                        UpdateEnable = update;
                    }

                    public ZModeHolder(Stream reader)
                    {
                        Enable = reader.ReadByte() > 0;
                        Function = (AlphaCompare.CompareType)reader.ReadByte();
                        UpdateEnable = reader.ReadByte() > 0;
                        reader.Position++;
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte((byte)(Enable ? 0x01 : 0x00));
                        writer.WriteByte((byte)Function);
                        writer.WriteByte((byte)(UpdateEnable ? 0x01 : 0x00));
                        writer.WriteByte(0xFF);
                    }

                    public ZModeHolder Clone() => new ZModeHolder(Enable, Function, UpdateEnable);

                    public override int GetHashCode()
                    {
                        var hashCode = -1724780622;
                        hashCode = hashCode * -1521134295 + Enable.GetHashCode();
                        hashCode = hashCode * -1521134295 + Function.GetHashCode();
                        hashCode = hashCode * -1521134295 + UpdateEnable.GetHashCode();
                        return hashCode;
                    }

                    public override bool Equals(object obj)
                    {
                        if (!(obj is ZModeHolder holder))
                            return false;
                        
                        return Enable == holder.Enable &&
                               Function == holder.Function &&
                               UpdateEnable == holder.UpdateEnable;
                    }

                    public static bool operator ==(ZModeHolder holder1, ZModeHolder holder2) => holder1.Equals(holder2);

                    public static bool operator !=(ZModeHolder holder1, ZModeHolder holder2) => !(holder1 == holder2);
                }
                public struct NBTScaleHolder
                {
                    public byte Unknown1;
                    
                    public Vector3 Scale;

                    public NBTScaleHolder(byte unk1, Vector3 scale)
                    {
                        Unknown1 = unk1;
                        Scale = scale;
                    }

                    public NBTScaleHolder(Stream reader)
                    {
                        Unknown1 = (byte)reader.ReadByte();
                        reader.Position += 0x03;
                        Scale = new Vector3(BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0), BitConverter.ToSingle(reader.ReadReverse(0, 4), 0));
                    }

                    public void Write(Stream writer)
                    {
                        writer.WriteByte(Unknown1);
                        writer.WriteByte(0xFF);
                        writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                        writer.WriteReverse(BitConverter.GetBytes(Scale.X), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(Scale.Y), 0, 4);
                        writer.WriteReverse(BitConverter.GetBytes(Scale.Z), 0, 4);
                    }

                    public NBTScaleHolder Clone() => new NBTScaleHolder(Unknown1, new Vector3(Scale.X, Scale.Y, Scale.Z));

                    public override bool Equals(object obj)
                    {
                        if (!(obj is NBTScaleHolder holder))
                            return false;
                        return Unknown1 == holder.Unknown1 &&
                               Scale.Equals(holder.Scale);
                    }

                    public override int GetHashCode()
                    {
                        var hashCode = 1461352585;
                        hashCode = hashCode * -1521134295 + Unknown1.GetHashCode();
                        hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(Scale);
                        return hashCode;
                    }

                    public static bool operator ==(NBTScaleHolder holder1, NBTScaleHolder holder2)
                    {
                        return holder1.Equals(holder2);
                    }

                    public static bool operator !=(NBTScaleHolder holder1, NBTScaleHolder holder2)
                    {
                        return !(holder1 == holder2);
                    }
                }

                public static bool operator ==(Material material1, Material material2) => material1.Equals(material2);

                public static bool operator !=(Material material1, Material material2) => !(material1 == material2);
            }

            public enum CullMode
            {
                None = 0,   // Do not cull any primitives
                Front = 1,  // Cull front-facing primitives
                Back = 2,   // Cull back-facing primitives
                All = 3     // Cull all primitives
            }
            public enum TexCoordId
            {
                TexCoord0 = 0,
                TexCoord1 = 1,
                TexCoord2 = 2,
                TexCoord3 = 3,
                TexCoord4 = 4,
                TexCoord5 = 5,
                TexCoord6 = 6,
                TexCoord7 = 7,
                Null = 0xFF
            }
            public enum TexMapId
            {
                TexMap0,
                TexMap1,
                TexMap2,
                TexMap3,
                TexMap4,
                TexMap5,
                TexMap6,
                TexMap7,

                Null = 0xFF,
            }
            public enum TexMatrixId
            {
                Identity = 60,
                TexMtx0 = 30,
                TexMtx1 = 33,
                TexMtx2 = 36,
                TexMtx3 = 39,
                TexMtx4 = 42,
                TexMtx5 = 45,
                TexMtx6 = 48,
                TexMtx7 = 51,
                TexMtx8 = 54,
                TexMtx9 = 57
            }
            public enum TexGenType
            {
                Matrix3x4 = 0,
                Matrix2x4 = 1,
                Bump0 = 2,
                Bump1 = 3,
                Bump2 = 4,
                Bump3 = 5,
                Bump4 = 6,
                Bump5 = 7,
                Bump6 = 8,
                Bump7 = 9,
                SRTG = 10
            }
            public enum TexMtxMapMode
            {
                None = 0x00,
                // Uses "Basic" conventions, no -1...1 remap.
                // Peach Beach uses EnvmapBasic, not sure on what yet...
                EnvmapBasic = 0x01,
                ProjmapBasic = 0x02,
                ViewProjmapBasic = 0x03,
                // Unknown: 0x04, 0x05. No known uses.
                // Uses "Old" conventions, remaps translation in fourth component
                // TODO(jstpierre): Figure out the geometric interpretation of old vs. new
                EnvmapOld = 0x06,
                // Uses "New" conventions, remaps translation in third component
                Envmap = 0x07,
                Projmap = 0x08,
                ViewProjmap = 0x09,
                // Environment map, but based on a custom effect matrix instead of the default view
                // matrix. Used by certain actors in Wind Waker, like zouK1 in Master Sword Chamber.
                EnvmapOldEffectMtx = 0x0A,
                EnvmapEffectMtx = 0x0B,
            }
            public enum TexGenSrc
            {
                Position = 0,
                Normal = 1,
                Binormal = 2,
                Tangent = 3,
                Tex0 = 4,
                Tex1 = 5,
                Tex2 = 6,
                Tex3 = 7,
                Tex4 = 8,
                Tex5 = 9,
                Tex6 = 10,
                Tex7 = 11,
                TexCoord0 = 12,
                TexCoord1 = 13,
                TexCoord2 = 14,
                TexCoord3 = 15,
                TexCoord4 = 16,
                TexCoord5 = 17,
                TexCoord6 = 18,
                Color0 = 19,
                Color1 = 20,
            }
            public enum TevStageId
            {
                TevStage0,
                TevStage1,
                TevStage2,
                TevStage3,
                TevStage4,
                TevStage5,
                TevStage6,
                TevStage7,
                TevStage8,
                TevStage9,
                TevStage10,
                TevStage11,
                TevStage12,
                TevStage13,
                TevStage14,
                TevStage15
            }

            public enum KonstColorSel
            {
                KCSel_1 = 0x00,     // Constant 1.0
                KCSel_7_8 = 0x01,   // Constant 7/8
                KCSel_6_8 = 0x02,   // Constant 3/4
                KCSel_5_8 = 0x03,   // Constant 5/8
                KCSel_4_8 = 0x04,   // Constant 1/2
                KCSel_3_8 = 0x05,   // Constant 3/8
                KCSel_2_8 = 0x06,   // Constant 1/4
                KCSel_1_8 = 0x07,   // Constant 1/8

                KCSel_K0 = 0x0C,    // K0[RGB] Register
                KCSel_K1 = 0x0D,    // K1[RGB] Register
                KCSel_K2 = 0x0E,    // K2[RGB] Register
                KCSel_K3 = 0x0F,    // K3[RGB] Register
                KCSel_K0_R = 0x10,  // K0[RRR] Register
                KCSel_K1_R = 0x11,  // K1[RRR] Register
                KCSel_K2_R = 0x12,  // K2[RRR] Register
                KCSel_K3_R = 0x13,  // K3[RRR] Register
                KCSel_K0_G = 0x14,  // K0[GGG] Register
                KCSel_K1_G = 0x15,  // K1[GGG] Register
                KCSel_K2_G = 0x16,  // K2[GGG] Register
                KCSel_K3_G = 0x17,  // K3[GGG] Register
                KCSel_K0_B = 0x18,  // K0[BBB] Register
                KCSel_K1_B = 0x19,  // K1[BBB] Register
                KCSel_K2_B = 0x1A,  // K2[BBB] Register
                KCSel_K3_B = 0x1B,  // K3[BBB] Register
                KCSel_K0_A = 0x1C,  // K0[AAA] Register
                KCSel_K1_A = 0x1D,  // K1[AAA] Register
                KCSel_K2_A = 0x1E,  // K2[AAA] Register
                KCSel_K3_A = 0x1F   // K3[AAA] Register
            }
            public enum KonstAlphaSel
            {
                KASel_1 = 0x00,     // Constant 1.0
                KASel_7_8 = 0x01,   // Constant 7/8
                KASel_6_8 = 0x02,   // Constant 3/4
                KASel_5_8 = 0x03,   // Constant 5/8
                KASel_4_8 = 0x04,   // Constant 1/2
                KASel_3_8 = 0x05,   // Constant 3/8
                KASel_2_8 = 0x06,   // Constant 1/4
                KASel_1_8 = 0x07,   // Constant 1/8
                KASel_K0_R = 0x10,  // K0[R] Register
                KASel_K1_R = 0x11,  // K1[R] Register
                KASel_K2_R = 0x12,  // K2[R] Register
                KASel_K3_R = 0x13,  // K3[R] Register
                KASel_K0_G = 0x14,  // K0[G] Register
                KASel_K1_G = 0x15,  // K1[G] Register
                KASel_K2_G = 0x16,  // K2[G] Register
                KASel_K3_G = 0x17,  // K3[G] Register
                KASel_K0_B = 0x18,  // K0[B] Register
                KASel_K1_B = 0x19,  // K1[B] Register
                KASel_K2_B = 0x1A,  // K2[B] Register
                KASel_K3_B = 0x1B,  // K3[B] Register
                KASel_K0_A = 0x1C,  // K0[A] Register
                KASel_K1_A = 0x1D,  // K1[A] Register
                KASel_K2_A = 0x1E,  // K2[A] Register
                KASel_K3_A = 0x1F   // K3[A] Register
            }

            public enum Mat3OffsetIndex
            {
                MaterialData = 0,
                IndexData = 1,
                NameTable = 2,
                IndirectData = 3,
                CullMode = 4,
                MaterialColor = 5,
                ColorChannelCount = 6,
                ColorChannelData = 7,
                AmbientColorData = 8,
                LightData = 9,
                TexGenCount = 10,
                TexCoordData = 11,
                TexCoord2Data = 12,
                TexMatrixData = 13,
                TexMatrix2Data = 14,
                TexNoData = 15,
                TevOrderData = 16,
                TevColorData = 17,
                TevKColorData = 18,
                TevStageCount = 19,
                TevStageData = 20,
                TevSwapModeData = 21,
                TevSwapModeTable = 22,
                FogData = 23,
                AlphaCompareData = 24,
                BlendData = 25,
                ZModeData = 26,
                ZCompLoc = 27,
                DitherData = 28,
                NBTScaleData = 29
            }
        }
        public class TEX1
        {
            public List<BTI.BTI> Textures { get; private set; } = new List<BTI.BTI>();

            private static readonly string Magic = "TEX1";

            /// <summary>
            /// Get texture by Index
            /// </summary>
            /// <param name="Index">Texture ID</param>
            /// <returns></returns>
            public BTI.BTI this[int Index]
            {
                get
                {
                    if (Textures != null && Textures.Count > Index)
                        return Textures[Index];
                    throw new ArgumentException("TEX1[] (GET)", "Index");
                }
                set
                {
                    if (Textures == null)
                        Textures = new List<BTI.BTI>();

                    if (!(value is BTI.BTI || value is null) || Index > Textures.Count || Index < 0 || (value is null && Index == Textures.Count))
                        throw new ArgumentException("TEX1[] (SET)", "Index");
                    
                    if (value is null)
                        Textures.RemoveAt(Index);
                    else if (Index == Textures.Count)
                        Textures.Add(value);
                    else
                        Textures[Index] = value;
                }
            }
            /// <summary>
            /// Get Texture by FileName
            /// </summary>
            /// <param name="TextureName">Texture FileName</param>
            /// <returns></returns>
            public BTI.BTI this[string TextureName]
            {
                get
                {
                    if (Textures == null)
                    {
                        Console.WriteLine("There are no textures currently loaded.");
                        return null;
                    }

                    if (Textures.Count == 0)
                    {
                        Console.WriteLine("There are no textures currently loaded.");
                        return null;
                    }

                    foreach (BTI.BTI tex in Textures)
                    {
                        if (tex.FileName.Equals(TextureName))
                            return tex;
                    }

                    Console.Write($"No texture with the name { TextureName } was found.");
                    return null;
                }

                set
                {
                    if (Textures == null)
                        Textures = new List<BTI.BTI>();

                    if (!(value is BTI.BTI || value is null))
                        return;

                    for (int i = 0; i < Textures.Count; i++)
                    {
                        if (Textures[i].FileName.Equals(TextureName))
                        {
                            if (value is null)
                                Textures.RemoveAt(i);
                            else
                                Textures[i] = value;
                            return;
                        }
                    }
                    if (!(value is null))
                        Textures.Add(value);
                }
            }
            /// <summary>
            /// Gets the total amount of textures in this section
            /// </summary>
            public int Count => Textures.Count;

            public TEX1(Stream BMDFile)
            {
                int ChunkStart = (int)BMDFile.Position;
                if (!BMDFile.ReadString(4).Equals(Magic))
                    throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

                int tex1Size = BitConverter.ToInt32(BMDFile.ReadReverse(0, 4), 0);
                short texCount = BitConverter.ToInt16(BMDFile.ReadReverse(0, 2), 0);
                BMDFile.Position += 0x02;

                int textureHeaderOffset = BitConverter.ToInt32(BMDFile.ReadReverse(0, 4), 0);
                int textureNameTableOffset = BitConverter.ToInt32(BMDFile.ReadReverse(0, 4), 0);

                List<string> names = new List<string>();

                BMDFile.Seek(ChunkStart + textureNameTableOffset, System.IO.SeekOrigin.Begin);

                short stringCount = BitConverter.ToInt16(BMDFile.ReadReverse(0, 2), 0);
                BMDFile.Position += 0x02;

                for (int i = 0; i < stringCount; i++)
                {
                    BMDFile.Position += 0x02;
                    short nameOffset = BitConverter.ToInt16(BMDFile.ReadReverse(0, 2), 0);
                    long saveReaderPos = BMDFile.Position;
                    BMDFile.Position = ChunkStart + textureNameTableOffset + nameOffset;

                    names.Add(BMDFile.ReadString());

                    BMDFile.Position = saveReaderPos;
                }


                BMDFile.Seek(textureHeaderOffset + ChunkStart, SeekOrigin.Begin);

                for (int i = 0; i < texCount; i++)
                {
                    BMDFile.Seek((ChunkStart + 0x20 + (0x20 * i)), SeekOrigin.Begin);

                    BTI.BTI img = new BTI.BTI(BMDFile) { FileName = names[i] };
                    Textures.Add(img);
                }
            }

            public void Write(Stream writer)
            {
                long start = writer.Position;

                writer.WriteString(Magic);
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for section size
                writer.WriteReverse(BitConverter.GetBytes((short)Textures.Count), 0, 2);
                writer.Write(new byte[2] { 0xFF, 0xFF }, 0, 2);
                writer.Write(new byte[4] { 0x00, 0x00, 0x00, 0x20 }, 0, 4); // Offset to the start of the texture data. Always 32
                writer.Write(new byte[4] { 0xDD, 0xDD, 0xDD, 0xDD }, 0, 4); // Placeholder for string table offset

                AddPadding(writer, 32);

                List<string> names = new List<string>();
                Dictionary<BTI.BTI, long> WrittenImages = new Dictionary<BTI.BTI, long>();

                long ImageDataOffset = start + (writer.Position - start) + (0x20 * Textures.Count);
                for (int i = 0; i < Textures.Count; i++)
                {
                    long x = -1;
                    foreach (KeyValuePair<BTI.BTI, long> item in WrittenImages)
                    {
                        if (item.Key.ImageEquals(Textures[i]))
                        {
                            x = item.Value;
                            break;
                        }
                    }
                    if (x == -1)
                    {
                        WrittenImages.Add(Textures[i], ImageDataOffset);
                        Textures[i].Save(writer, ref ImageDataOffset);
                    }
                    else
                        Textures[i].Save(writer, ref x);
                    names.Add(Textures[i].FileName);
                }

                writer.Position = writer.Length;
                // Write texture name table offset
                int NameTableOffset = (int)(writer.Position - start);

                writer.WriteStringTable(names);

                AddPadding(writer, 32);
                
                // Write TEX1 size
                writer.Position = start + 4;
                writer.WriteReverse(BitConverter.GetBytes((int)(writer.Length - start)), 0, 4);
                writer.Position = start + 0x10;
                writer.WriteReverse(BitConverter.GetBytes(NameTableOffset), 0, 4);
            }

            public bool Contains(BTI.BTI Image) => Textures.Any(I => I.Equals(Image));
            public bool ContainsImage(BTI.BTI Image) => Textures.Any(I => I.ImageEquals(Image));
            public int GetTextureIndex(BTI.BTI Image)
            {
                if (Image is null)
                    throw new ArgumentException("BMD.TEX1.GetTextureIndex()", "Image");
                for (int i = 0; i < Count; i++)
                    if (Image == Textures[i])
                        return i;
                return -1;
            }

            public static List<KeyValuePair<int, BTI.BTI>?> FetchUsedTextures(TEX1 Textures, MAT3.Material Material)
            {
                List<KeyValuePair<int, BTI.BTI>?> UsedTextures = new List<KeyValuePair<int, BTI.BTI>?>();
                for (int i = 0; i < 8; i++)
                {
                    if (Material.TextureIndices[i] != -1 && Material.TextureIndices[i] < Textures.Count)
                        UsedTextures.Add(new KeyValuePair<int, BTI.BTI>(Material.TextureIndices[i], Textures[Material.TextureIndices[i]]));
                    else if (Material.TextureIndices[i] != -1)
                        UsedTextures.Add(null);
                }
                return UsedTextures;
            }

            public void UpdateTextures(MAT3 Materials)
            {
                for (int i = 0; i < Materials.Count; i++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        if (Materials[i].Textures[x] is null)
                            Materials[i].TextureIndices[x] = -1;
                        else if (Contains(Materials[i].Textures[x]))
                            Materials[i].TextureIndices[x] = GetTextureIndex(Materials[i].Textures[x]);
                        else
                        {
                            Materials[i].TextureIndices[x] = Textures.Count;
                            this[Textures.Count] = Materials[i].Textures[x];
                        }
                    }
                }
            }
        }

        public enum GXVertexAttribute
        {
            PositionMatrixIdx = 0,
            Tex0Mtx = 1,
            Tex1Mtx = 2,
            Tex2Mtx = 3,
            Tex3Mtx = 4,
            Tex4Mtx = 5,
            Tex5Mtx = 6,
            Tex6Mtx = 7,
            Tex7Mtx = 8,
            Position = 9,
            Normal = 10,
            Color0 = 11,
            Color1 = 12,
            Tex0 = 13,
            Tex1 = 14,
            Tex2 = 15,
            Tex3 = 16,
            Tex4 = 17,
            Tex5 = 18,
            Tex6 = 19,
            Tex7 = 20,
            PositionMatrixArray = 21,
            NormalMatrixArray = 22,
            TextureMatrixArray = 23,
            LitMatrixArra = 24,
            NormalBinormalTangent = 25,
            MaxAttr = 26,
            Null = 255
        }
        public enum GXDataType
        {
            Unsigned8, RGB565 = 0x0,
            Signed8, RGB8 = 0x1,
            Unsigned16, RGBX8 = 0x2,
            Signed16, RGBA4 = 0x3,
            Float32, RGBA6 = 0x4,
            RGBA8 = 0x5
        }
        public enum GXComponentCount
        {
            Position_XY = 0,
            Position_XYZ,

            Normal_XYZ = 0,
            Normal_NBT,
            Normal_NBT3,

            Color_RGB = 0,
            Color_RGBA,

            TexCoord_S = 0,
            TexCoord_ST
        }
        public enum Vtx1OffsetIndex
        {
            PositionData,
            NormalData,
            NBTData,
            Color0Data,
            Color1Data,
            TexCoord0Data,
            TexCoord1Data,
            TexCoord2Data,
            TexCoord3Data,
            TexCoord4Data,
            TexCoord5Data,
            TexCoord6Data,
            TexCoord7Data,
        }
        /// <summary>
        /// Determines how the position and normal matrices are calculated for a shape
        /// </summary>
        public enum DisplayFlags
        {
            /// <summary>
            /// Use a Single Matrix
            /// </summary>
            SingleMatrix,
            /// <summary>
            /// Billboard along all axis
            /// </summary>
            Billboard,
            /// <summary>
            /// Billboard Only along the Y axis
            /// </summary>
            BillboardY,
            /// <summary>
            /// Use Multiple Matrixies (Skinned models)
            /// </summary>
            MultiMatrix
        }
        public enum VertexInputType
        {
            None,
            Direct,
            Index8,
            Index16
        }
        public enum GXPrimitiveType
        {
            Points = 0xB8,
            Lines = 0xA8,
            LineStrip = 0xB0,
            Triangles = 0x90,
            TriangleStrip = 0x98,
            TriangleFan = 0xA0,
            Quads = 0x80,
        }
        public static OpenTK.Graphics.OpenGL.PrimitiveType FromGXToOpenTK(GXPrimitiveType Type)
        {
            switch (Type)
            {
                case GXPrimitiveType.Points:
                    return OpenTK.Graphics.OpenGL.PrimitiveType.Points;
                case GXPrimitiveType.Lines:
                    return OpenTK.Graphics.OpenGL.PrimitiveType.Lines;
                case GXPrimitiveType.LineStrip:
                    return OpenTK.Graphics.OpenGL.PrimitiveType.LineStrip;
                case GXPrimitiveType.Triangles:
                    return OpenTK.Graphics.OpenGL.PrimitiveType.Triangles;
                case GXPrimitiveType.TriangleStrip:
                    return OpenTK.Graphics.OpenGL.PrimitiveType.TriangleStrip;
                case GXPrimitiveType.TriangleFan:
                    return OpenTK.Graphics.OpenGL.PrimitiveType.TriangleFan;
                case GXPrimitiveType.Quads:
                    return OpenTK.Graphics.OpenGL.PrimitiveType.Quads;
            }
            throw new Exception("Bruh moment!!");
        }
        public static OpenTK.Graphics.OpenGL.TextureWrapMode FromGXToOpenTK(GXWrapMode Type)
        {
            switch (Type)
            {
                case GXWrapMode.CLAMP:
                    return OpenTK.Graphics.OpenGL.TextureWrapMode.Clamp;
                case GXWrapMode.REPEAT:
                    return OpenTK.Graphics.OpenGL.TextureWrapMode.Repeat;
                case GXWrapMode.MIRRORREAPEAT:
                    return OpenTK.Graphics.OpenGL.TextureWrapMode.MirroredRepeat;
            }
            throw new Exception("Bruh moment!!");
        }
        public static OpenTK.Graphics.OpenGL.TextureMinFilter FromGXToOpenTK_Min(GXFilterMode Type)
        {
            switch (Type)
            {
                case GXFilterMode.Nearest:
                    return OpenTK.Graphics.OpenGL.TextureMinFilter.Nearest;
                case GXFilterMode.Linear:
                    return OpenTK.Graphics.OpenGL.TextureMinFilter.Linear;
                case GXFilterMode.NearestMipmapNearest:
                    return OpenTK.Graphics.OpenGL.TextureMinFilter.NearestMipmapNearest;
                case GXFilterMode.NearestMipmapLinear:
                    return OpenTK.Graphics.OpenGL.TextureMinFilter.NearestMipmapLinear;
                case GXFilterMode.LinearMipmapNearest:
                    return OpenTK.Graphics.OpenGL.TextureMinFilter.LinearMipmapNearest;
                case GXFilterMode.LinearMipmapLinear:
                    return OpenTK.Graphics.OpenGL.TextureMinFilter.LinearMipmapLinear;
            }
            throw new Exception("Bruh moment!!");
        }
        public static OpenTK.Graphics.OpenGL.TextureMagFilter FromGXToOpenTK_Mag(GXFilterMode Type)
        {
            switch (Type)
            {
                case GXFilterMode.Nearest:
                    return OpenTK.Graphics.OpenGL.TextureMagFilter.Nearest;
                case GXFilterMode.Linear:
                    return OpenTK.Graphics.OpenGL.TextureMagFilter.Linear;
                case GXFilterMode.NearestMipmapNearest:
                case GXFilterMode.NearestMipmapLinear:
                case GXFilterMode.LinearMipmapNearest:
                case GXFilterMode.LinearMipmapLinear:
                    break;
            }
            throw new Exception("Bruh moment!!");
        }
        public static OpenTK.Graphics.OpenGL.CullFaceMode? FromGXToOpenTK(MAT3.CullMode Type)
        {
            switch (Type)
            {
                case MAT3.CullMode.None:
                    return null;
                case MAT3.CullMode.Front:
                    return OpenTK.Graphics.OpenGL.CullFaceMode.Back;
                case MAT3.CullMode.Back:
                    return OpenTK.Graphics.OpenGL.CullFaceMode.Front;
                case MAT3.CullMode.All:
                    return OpenTK.Graphics.OpenGL.CullFaceMode.FrontAndBack;
            }
            throw new Exception("Bruh moment!!");
        }
        public static OpenTK.Graphics.OpenGL.BlendingFactor FromGXToOpenTK(MAT3.Material.BlendMode.BlendModeControl Factor)
        {
            switch (Factor)
            {
                case MAT3.Material.BlendMode.BlendModeControl.Zero:
                    return OpenTK.Graphics.OpenGL.BlendingFactor.Zero;
                case MAT3.Material.BlendMode.BlendModeControl.One:
                    return OpenTK.Graphics.OpenGL.BlendingFactor.One;
                case MAT3.Material.BlendMode.BlendModeControl.SrcColor:
                    return OpenTK.Graphics.OpenGL.BlendingFactor.SrcColor;
                case MAT3.Material.BlendMode.BlendModeControl.InverseSrcColor:
                    return OpenTK.Graphics.OpenGL.BlendingFactor.OneMinusSrcColor;
                case MAT3.Material.BlendMode.BlendModeControl.SrcAlpha:
                    return OpenTK.Graphics.OpenGL.BlendingFactor.SrcAlpha;
                case MAT3.Material.BlendMode.BlendModeControl.InverseSrcAlpha:
                    return OpenTK.Graphics.OpenGL.BlendingFactor.OneMinusSrcAlpha;
                case MAT3.Material.BlendMode.BlendModeControl.DstAlpha:
                    return OpenTK.Graphics.OpenGL.BlendingFactor.DstAlpha;
                case MAT3.Material.BlendMode.BlendModeControl.InverseDstAlpha:
                    return OpenTK.Graphics.OpenGL.BlendingFactor.OneMinusDstAlpha;
                default:
                    Console.WriteLine("Unsupported BlendModeControl: \"{0}\" in FromGXToOpenTK!", Factor);
                    return OpenTK.Graphics.OpenGL.BlendingFactor.SrcAlpha;

            }
        }


        //=====================================================================

        /// <summary>
        /// Cast a RARCFile to a BMD
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator BMD(RARC.RARC.File x) => new BMD((MemoryStream)x) { FileName = x.Name };

        //=====================================================================
    }

    public class BDL : BMD
    {
        public MDL3 MatDisplayList { get; private set; }
        private static readonly string Magic = "J3D2bdl4";

        public BDL() : base()
        {
        }

        public BDL(string Filename) : base(Filename)
        {
        }

        public BDL(Stream BDL) : base(BDL)
        {
        }

        protected override void Read(Stream BDLFile)
        {
            if (!BDLFile.ReadString(8).Equals(Magic))
                throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

            BDLFile.Position += 0x08 + 16;
            Scenegraph = new INF1(BDLFile, out int VertexCount);
            VertexData = new VTX1(BDLFile, VertexCount);
            SkinningEnvelopes = new EVP1(BDLFile);
            PartialWeightData = new DRW1(BDLFile);
            Joints = new JNT1(BDLFile);
            Shapes = new SHP1(BDLFile);
            SkinningEnvelopes.SetInverseBindMatrices(Joints.FlatSkeleton);
            Shapes.SetVertexWeights(SkinningEnvelopes, PartialWeightData);
            Joints.InitBoneFamilies(Scenegraph);
            Materials = new MAT3(BDLFile);
            MatDisplayList = new MDL3(BDLFile);
            Textures = new TEX1(BDLFile);
            Materials.SetTextureNames(Textures);
            //VertexData.StipUnused(Shapes);
        }

        public class MDL3
        {
            public List<Packet> Packets { get; set; } = new List<Packet>();
            private static readonly string Magic = "MDL3";
            public MDL3(Stream BDL)
            {
                int ChunkStart = (int)BDL.Position;
                if (!BDL.ReadString(4).Equals(Magic))
                    throw new Exception($"Invalid Identifier. Expected \"{Magic}\"");

                int mdl3Size = BitConverter.ToInt32(BDL.ReadReverse(0, 4), 0);
                short EntryCount = BitConverter.ToInt16(BDL.ReadReverse(0, 2), 0);
                BDL.Position += 0x02; //Skip the padding
                uint PacketListingOffset = BitConverter.ToUInt32(BDL.ReadReverse(0, 4), 0), SubPacketOffset = BitConverter.ToUInt32(BDL.ReadReverse(0, 4), 0),
                    MatrixIDOffset = BitConverter.ToUInt32(BDL.ReadReverse(0, 4), 0), UnknownOffset = BitConverter.ToUInt32(BDL.ReadReverse(0, 4), 0),
                    IndiciesOffset = BitConverter.ToUInt32(BDL.ReadReverse(0, 4), 0), StringTableOFfset = BitConverter.ToUInt32(BDL.ReadReverse(0, 4), 0);

                BDL.Position = ChunkStart + PacketListingOffset;
                for (int i = 0; i < EntryCount; i++)
                    Packets.Add(new Packet(BDL));


                BDL.Position = ChunkStart + mdl3Size;
            }

            public class Packet
            {
                public List<GXCommand> Commands { get; set; } = new List<GXCommand>();

                public Packet(Stream BDL)
                {
                    long PausePosition = BDL.Position;
                    uint Offset = BitConverter.ToUInt32(BDL.ReadReverse(0, 4), 0), Size = BitConverter.ToUInt32(BDL.ReadReverse(0, 4), 0);
                    BDL.Position = PausePosition + Offset;
                    long PacketLimit = BDL.Position + Size;
                    while (BDL.Position < PacketLimit)
                    {
                        byte id = (byte)BDL.ReadByte();
                        GXCommand CurrentCommand = id == 0x61 ? new BPCommand(BDL) as GXCommand : (id == 0x10 ? new XFCommand(BDL) as GXCommand : null);
                        if (CurrentCommand == null)
                            break;
                        Commands.Add(CurrentCommand);
                    }
                    BDL.Position = PausePosition + 8;
                }
            }

            public abstract class GXCommand
            {
                public virtual byte Identifier => 0x00;
                public abstract int GetRegister();
                public abstract object GetData();
            }
            public class BPCommand : GXCommand
            {
                public override byte Identifier => 0x61;
                public BPRegister Register { get; set; }
                public UInt24 Value { get; set; }

                public BPCommand(Stream BDL)
                {
                    Register = (BPRegister)BDL.ReadByte();
                    Value = BitConverterEx.ToUInt24(BDL.ReadReverse(0, 3), 0);
                }

                public override string ToString() => $"BP Command: {Register}, {Value.ToString()}";

                public override int GetRegister() => (int)Register;

                public override object GetData() => Value;
            }
            public class XFCommand : GXCommand
            {
                public override byte Identifier => 0x10;
                public XFRegister Register { get; set; }
                public List<IXFArgument> Arguments { get; set; } = new List<IXFArgument>();

                public XFCommand(Stream BDL)
                {
                    int DataLength = (BitConverter.ToInt16(BDL.ReadReverse(0, 2), 0) + 1) * 4;
                    Register = (XFRegister)BitConverter.ToInt16(BDL.ReadReverse(0, 2), 0);
                    switch (Register)
                    {
                        case XFRegister.SETTEXMTX0:
                        case XFRegister.SETTEXMTX1:
                            Arguments.Add(new XFTexMatrix(BDL));
                            break;
                        default:
                            BDL.Read(0, DataLength);
                            break;
                    }
                }
                public override string ToString() => $"XF Command: {Register}, {Arguments.Count}";

                public override int GetRegister() => (int)Register;

                public override object GetData() => Arguments;

                public interface IXFArgument
                {

                }
                public class XFTexMatrix : IXFArgument
                {
                    public Matrix2x4 CompiledMatrix { get; set; }

                    public XFTexMatrix(Stream BDL)
                    {
                        CompiledMatrix = new Matrix2x4(
                            BitConverter.ToSingle(BDL.ReadReverse(0, 4), 0), BitConverter.ToSingle(BDL.ReadReverse(0, 4), 0), BitConverter.ToSingle(BDL.ReadReverse(0, 4), 0), BitConverter.ToSingle(BDL.ReadReverse(0, 4), 0),
                            BitConverter.ToSingle(BDL.ReadReverse(0, 4), 0), BitConverter.ToSingle(BDL.ReadReverse(0, 4), 0), BitConverter.ToSingle(BDL.ReadReverse(0, 4), 0), BitConverter.ToSingle(BDL.ReadReverse(0, 4), 0)
                            );
                    }

                    public XFTexMatrix(MAT3.Material.TexMatrix Source)
                    {
                        double theta = Source.Rotation * 3.141592;
                        double sinR = Math.Sin(theta);
                        double cosR = Math.Cos(theta);
                        
                        CompiledMatrix = Source.IsMaya ? new Matrix2x4(
                            (float)(Source.Scale.X * cosR), (float)(Source.Scale.X * -sinR), 0.0f, (float)(Source.Scale.X * (-0.5f * sinR - 0.5f * cosR + 0.5f - Source.Translation.X)),
                            (float)(-Source.Scale.Y * sinR), (float)(Source.Scale.Y * cosR), 0.0f, (float)(Source.Scale.Y * (0.5f * sinR - 0.5f * cosR - 0.5f + Source.Translation.Y) + 1.0f)
                            ) : new Matrix2x4(
                            (float)(Source.Scale.X * cosR), (float)(Source.Scale.X * -sinR), (float)(Source.Translation.X + Source.Center.X + Source.Scale.X * (sinR * Source.Center.Y - cosR * Source.Center.X)), 0.0f,
                            (float)(Source.Scale.Y * sinR), (float)(Source.Scale.Y * cosR), (float)(Source.Translation.Y + Source.Center.Y + -Source.Scale.Y * (-sinR * Source.Center.X + cosR * Source.Center.Y)), 0.0f
                            );

                        Matrix4 Test = Matrix4.Identity;
                        float[] temp = new float[4 * 4];
                        temp[0] = (float)(Source.Scale.X * cosR);
                        temp[4] = (float)(Source.Scale.X * -sinR);
                        temp[12] = (float)(Source.Translation.X + Source.Center.X + Source.Scale.X * (sinR * Source.Center.Y - cosR * Source.Center.X));
                    }
                }
            }
        }

        public enum BPRegister : int
        {
            GEN_MODE = 0x00,

            IND_MTXA0 = 0x06,
            IND_MTXB0 = 0x07,
            IND_MTXC0 = 0x08,
            IND_MTXA1 = 0x09,
            IND_MTXB1 = 0x0A,
            IND_MTXC1 = 0x0B,
            IND_MTXA2 = 0x0C,
            IND_MTXB2 = 0x0D,
            IND_MTXC2 = 0x0E,
            IND_IMASK = 0x0F,

            IND_CMD0 = 0x10,
            IND_CMD1 = 0x11,
            IND_CMD2 = 0x12,
            IND_CMD3 = 0x13,
            IND_CMD4 = 0x14,
            IND_CMD5 = 0x15,
            IND_CMD6 = 0x16,
            IND_CMD7 = 0x17,
            IND_CMD8 = 0x18,
            IND_CMD9 = 0x19,
            IND_CMDA = 0x1A,
            IND_CMDB = 0x1B,
            IND_CMDC = 0x1C,
            IND_CMDD = 0x1D,
            IND_CMDE = 0x1E,
            IND_CMDF = 0x1F,

            SCISSOR_0 = 0x20,
            SCISSOR_1 = 0x21,

            SU_LPSIZE = 0x22,
            SU_COUNTER = 0x23,
            RAS_COUNTER = 0x24,

            RAS1_SS0 = 0x25,
            RAS1_SS1 = 0x26,
            RAS1_IREF = 0x27,

            RAS1_TREF0 = 0x28,
            RAS1_TREF1 = 0x29,
            RAS1_TREF2 = 0x2A,
            RAS1_TREF3 = 0x2B,
            RAS1_TREF4 = 0x2C,
            RAS1_TREF5 = 0x2D,
            RAS1_TREF6 = 0x2E,
            RAS1_TREF7 = 0x2F,

            SU_SSIZE0 = 0x30,
            SU_TSIZE0 = 0x31,
            SU_SSIZE1 = 0x32,
            SU_TSIZE1 = 0x33,
            SU_SSIZE2 = 0x34,
            SU_TSIZE2 = 0x35,
            SU_SSIZE3 = 0x36,
            SU_TSIZE3 = 0x37,
            SU_SSIZE4 = 0x38,
            SU_TSIZE4 = 0x39,
            SU_SSIZE5 = 0x3A,
            SU_TSIZE5 = 0x3B,
            SU_SSIZE6 = 0x3C,
            SU_TSIZE6 = 0x3D,
            SU_SSIZE7 = 0x3E,
            SU_TSIZE7 = 0x3F,

            PE_ZMODE = 0x40,
            PE_CMODE0 = 0x41, // dithering / blend mode / color_update / alpha_update / set_dither
            PE_CMODE1 = 0x42, // destination alpha
            PE_CONTROL = 0x43, // comp z location z_comp_loc(0x43000040)pixel_fmt(0x43000041)
            field_mask = 0x44,
            PE_DONE = 0x45,
            clock = 0x46,
            PE_TOKEN = 0x47, // token B (16 bit)
            PE_TOKEN_INT = 0x48, // token A (16 bit)
            EFB_SOURCE_RECT_TOP_LEFT = 0x49,
            EFB_SOURCE_RECT_WIDTH_HEIGHT = 0x4A,
            XFB_TARGET_ADDRESS = 0x4B,


            DISP_COPY_Y_SCALE = 0x4E,
            PE_COPY_CLEAR_AR = 0x4F,
            PE_COPY_CLEAR_GB = 0x50,
            PE_COPY_CLEAR_Z = 0x51,
            PE_COPY_EXECUTE = 0x52,

            SCISSOR_BOX_OFFSET = 0x59,

            TEX_LOADTLUT0 = 0x64,
            TEX_LOADTLUT1 = 0x65,

            TX_SET_MODE0_I0 = 0x80,
            TX_SET_MODE0_I1 = 0x81,
            TX_SET_MODE0_I2 = 0x82,
            TX_SET_MODE0_I3 = 0x83,

            TX_SET_MODE1_I0 = 0x84,
            TX_SET_MODE1_I1 = 0x85,
            TX_SET_MODE1_I2 = 0x86,
            TX_SET_MODE1_I3 = 0x87,

            TX_SETIMAGE0_I0 = 0x88,
            TX_SETIMAGE0_I1 = 0x89,
            TX_SETIMAGE0_I2 = 0x8A,
            TX_SETIMAGE0_I3 = 0x8B,

            TX_SETIMAGE1_I0 = 0x8C,
            TX_SETIMAGE1_I1 = 0x8D,
            TX_SETIMAGE1_I2 = 0x8E,
            TX_SETIMAGE1_I3 = 0x8F,

            TX_SETIMAGE2_I0 = 0x90,
            TX_SETIMAGE2_I1 = 0x91,
            TX_SETIMAGE2_I2 = 0x92,
            TX_SETIMAGE2_I3 = 0x93,

            TX_SETIMAGE3_I0 = 0x94,
            TX_SETIMAGE3_I1 = 0x95,
            TX_SETIMAGE3_I2 = 0x96,
            TX_SETIMAGE3_I3 = 0x97,

            TX_LOADTLUT0 = 0x98,
            TX_LOADTLUT1 = 0x99,
            TX_LOADTLUT2 = 0x9A,
            TX_LOADTLUT3 = 0x9B,


            TX_SET_MODE0_I4 = 0xA0,
            TX_SET_MODE0_I5 = 0xA1,
            TX_SET_MODE0_I6 = 0xA2,
            TX_SET_MODE0_I7 = 0xA3,

            TX_SET_MODE1_I4 = 0xA4,
            TX_SET_MODE1_I5 = 0xA5,
            TX_SET_MODE1_I6 = 0xA6,
            TX_SET_MODE1_I7 = 0xA7,

            TX_SETIMAGE0_I4 = 0xA8,
            TX_SETIMAGE0_I5 = 0xA9,
            TX_SETIMAGE0_I6 = 0xAA,
            TX_SETIMAGE0_I7 = 0xAB,

            TX_SETIMAGE1_I4 = 0xAC,
            TX_SETIMAGE1_I5 = 0xAD,
            TX_SETIMAGE1_I6 = 0xAE,
            TX_SETIMAGE1_I7 = 0xAF,

            TX_SETIMAGE2_I4 = 0xB0,
            TX_SETIMAGE2_I5 = 0xB1,
            TX_SETIMAGE2_I6 = 0xB2,
            TX_SETIMAGE2_I7 = 0xB3,

            TX_SETIMAGE3_I4 = 0xB4,
            TX_SETIMAGE3_I5 = 0xB5,
            TX_SETIMAGE3_I6 = 0xB6,
            TX_SETIMAGE3_I7 = 0xB7,

            TX_SETTLUT_I4 = 0xB8,
            TX_SETTLUT_I5 = 0xB9,
            TX_SETTLUT_I6 = 0xBA,
            TX_SETTLUT_I7 = 0xBB,


            TEV_COLOR_ENV_0 = 0xC0,
            TEV_ALPHA_ENV_0 = 0xC1,

            TEV_COLOR_ENV_1 = 0xC2,
            TEV_ALPHA_ENV_1 = 0xC3,

            TEV_COLOR_ENV_2 = 0xC4,
            TEV_ALPHA_ENV_2 = 0xC5,

            TEV_COLOR_ENV_3 = 0xC6,
            TEV_ALPHA_ENV_3 = 0xC7,

            TEV_COLOR_ENV_4 = 0xC8,
            TEV_ALPHA_ENV_4 = 0xC9,

            TEV_COLOR_ENV_5 = 0xCA,
            TEV_ALPHA_ENV_5 = 0xCB,

            TEV_COLOR_ENV_6 = 0xCC,
            TEV_ALPHA_ENV_6 = 0xCD,

            TEV_COLOR_ENV_7 = 0xCE,
            TEV_ALPHA_ENV_7 = 0xCF,

            TEV_COLOR_ENV_8 = 0xD0,
            TEV_ALPHA_ENV_8 = 0xD1,

            TEV_COLOR_ENV_9 = 0xD2,
            TEV_ALPHA_ENV_9 = 0xD3,

            TEV_COLOR_ENV_A = 0xD4,
            TEV_ALPHA_ENV_A = 0xD5,

            TEV_COLOR_ENV_B = 0xD6,
            TEV_ALPHA_ENV_B = 0xD7,

            TEV_COLOR_ENV_C = 0xD8,
            TEV_ALPHA_ENV_C = 0xD9,

            TEV_COLOR_ENV_D = 0xDA,
            TEV_ALPHA_ENV_D = 0xDB,

            TEV_COLOR_ENV_E = 0xDC,
            TEV_ALPHA_ENV_E = 0xDD,

            TEV_COLOR_ENV_F = 0xDE,
            TEV_ALPHA_ENV_F = 0xDF,

            TEV_REGISTERL_0 = 0xE0,
            TEV_REGISTERH_0 = 0xE1,

            TEV_REGISTERL_1 = 0xE2,
            TEV_REGISTERH_1 = 0xE3,

            TEV_REGISTERL_2 = 0xE4,
            TEV_REGISTERH_2 = 0xE5,

            TEV_REGISTERL_3 = 0xE6,
            TEV_REGISTERH_3 = 0xE7,

            FOG_RANGE = 0xE8,

            TEV_FOG_PARAM_0 = 0xEE,
            TEV_FOG_PARAM_1 = 0xEF,
            TEV_FOG_PARAM_2 = 0xF0,
            TEV_FOG_PARAM_3 = 0xF1,

            TEV_FOG_COLOR = 0xF2,

            TEV_ALPHAFUNC = 0xF3,
            TEV_Z_ENV_0 = 0xF4,
            TEV_Z_ENV_1 = 0xF5,

            TEV_KSEL_0 = 0xF6,
            TEV_KSEL_1 = 0xF7,
            TEV_KSEL_2 = 0xF8,
            TEV_KSEL_3 = 0xF9,
            TEV_KSEL_4 = 0xFA,
            TEV_KSEL_5 = 0xFB,
            TEV_KSEL_6 = 0xFC,
            TEV_KSEL_7 = 0xFD,

            BP_MASK = 0xFE
        }
        public enum XFRegister : int
        {
            /// <summary>
            /// Set the number of Channels
            /// </summary>
            SETNUMCHAN = 0x1009,
            /// <summary>
            /// Set Channel0's Ambient Colour
            /// </summary>
            SETCHAN0_AMBCOLOR = 0x100A,
            /// <summary>
            /// Set Channel0's Material Colour
            /// </summary>
            SETCHAN0_MATCOLOR = 0x100C,
            /// <summary>
            /// Set Channel0's Colour
            /// </summary>
            SETCHAN0_COLOR = 0x100E,
            /// <summary>
            /// Set the number of Texture Generators
            /// </summary>
            SETNUMTEXGENS = 0x103F,
            /// <summary>
            /// Set the Texture Matrix Information
            /// </summary>
            SETTEXMTXINFO = 0x1040,
            /// <summary>
            /// Set the Position Matrix Information
            /// </summary>
            SETPOSMTXINFO = 0x1050,

            SETTEXMTX0 = 0x0078,
            SETTEXMTX1 = 0x0084,
            SETTEXMTX2 = 0x0090,
            SETTEXMTX3 = 0x009C,
            SETTEXMTX4 = 0x00A8,
            SETTEXMTX5 = 0x00B4,
            SETTEXMTX6 = 0x00C0,
            SETTEXMTX7 = 0x00CC,
            SETTEXMTX8 = 0x00D8,
            SETTEXMTX9 = 0x00E4
        }



        //=====================================================================

        /// <summary>
        /// Cast a RARCFile to a BDL
        /// </summary>
        /// <param name="x"></param>
        public static implicit operator BDL(RARC.RARC.File x) => new BDL((MemoryStream)x) { FileName = x.Name };

        //=====================================================================
    }
}
