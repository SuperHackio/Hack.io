using Hack.io.Utility;
using static Hack.io.Utility.MathUtil;
using Hack.io.BCSV;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;

namespace Hack.io.KCL
{
    /// <summary>
    /// Super Mario Galaxy Collision files. Also supports .pa files with Hack.io.BCSV
    /// </summary>
    public class KCL
    {
        /// <summary>
        /// Filename of this KCL file.
        /// </summary>
        public string? FileName { get; set; } = null;
        public List<Vector3> Positions = new List<Vector3>();
        public List<Vector3> Normals = new List<Vector3>();
        public List<KCLFace> Triangles = new List<KCLFace>();
        public Octree[] OctreeRoots = null;
        public Vector3 MinCoords;

        uint MaskX, MaskY, MaskZ, ShiftX, ShiftY, ShiftZ;
        
        public KCL() {}
        public KCL(string file, BackgroundWorker? bgw = null)
        {
            FileStream FS = new FileStream(file, FileMode.Open);
            Read(FS, bgw);
            FS.Close();
            FileName = file;
        }
        public KCL(WavefrontObj obj, BCSV.BCSV CollisionCodes, int MaxTrianglesPerCube, int MinCubeWidth, BackgroundWorker? bgw = null)
        {
            bgw?.ReportProgress(0, 0);
            if (obj.Count > 0xFFFE)
                throw new GeometryOverflowException(obj.Count, 0xFFFE);

            float min_x = 0, min_y = 0, min_z = 0, max_x = 0, max_y = 0, max_z = 0;
            Dictionary<string, int> positionTable = new Dictionary<string, int>();
            Dictionary<string, int> normalTable = new Dictionary<string, int>();
            List<KCLFace> faces = new List<KCLFace>();
            Dictionary<ushort, Triangle> triangledict = new Dictionary<ushort, Triangle>();
            for (int i = 0; i < obj.Count; i++)
            {
                KCLFace face = new KCLFace();
                Triangle triangle = obj[i];

                Vector3 direction = Vector3.Cross(
                    triangle[1] - triangle[0],
                    triangle[2] - triangle[0]);

                if (((direction.X * direction.X) + (direction.Y * direction.Y) + (direction.Z * direction.Z)) < 0.001)
                    continue;
                direction = Vector3.Normalize(direction);

                for (int j = 0; j < 3; j++)
                {
                    min_x = Math.Min(triangle[j].X, min_x);
                    min_y = Math.Min(triangle[j].Y, min_y);
                    min_z = Math.Min(triangle[j].Z, min_z);
                    max_x = Math.Max(triangle[j].X, max_x);
                    max_y = Math.Max(triangle[j].Y, max_y);
                    max_z = Math.Max(triangle[j].Z, max_z);
                }
                
                //Calculate the ABC normal values.
                Vector3 normalA = Vector3.Cross(direction, triangle[2] - triangle[0]);
                Vector3 normalB = (-(Vector3.Cross(direction, triangle[1] - triangle[0])));
                Vector3 normalC = Vector3.Cross(direction, triangle[1] - triangle[2]);
                //Normalize the ABC normal values.
                normalA = Vector3.Normalize(normalA);
                normalB = Vector3.Normalize(normalB);
                normalC = Vector3.Normalize(normalC);

                //Vector3 normalA = Vector3.Cross(triangle[0] - triangle[2], triangle[3]).Unit();
                //Vector3 normalB = Vector3.Cross(triangle[1] - triangle[0], triangle[3]).Unit();
                //Vector3 normalC = Vector3.Cross(triangle[2] - triangle[1], triangle[3]).Unit();
                face.Length = Vector3.Dot(triangle[1] - triangle[0], normalC);
                face.PositionIndex = (ushort)IndexOfVertex(triangle[0], Positions, positionTable);
                face.DirectionIndex = (ushort)IndexOfVertex(triangle[3], Normals, normalTable);
                face.NormalAIndex = (ushort)IndexOfVertex(normalA, Normals, normalTable);
                face.NormalBIndex = (ushort)IndexOfVertex(normalB, Normals, normalTable);
                face.NormalCIndex = (ushort)IndexOfVertex(normalC, Normals, normalTable);
                face.GroupIndex = (ushort)triangle.GroupIndex;
                Triangles.Add(face);
                triangledict.Add((ushort)triangledict.Count, triangle);

                bgw?.ReportProgress((int)GetPercentOf(i, obj.Count), 0);
            }
            Vector3 Max = new Vector3(max_x, max_y, max_z);
            MinCoords = new Vector3(min_x, min_y, min_z);
            bgw?.ReportProgress(100, 0);

            if (positionTable.Count >= 0xFFFF)
                throw new GeometryOverflowException(positionTable.Count, 0xFFFF, "Verticies");
            if (normalTable.Count >= 0xFFFF)
                throw new GeometryOverflowException(normalTable.Count, 0xFFFF, "Normals");

            //Positions.AddRange(vw.Verticies);
            //Normals.AddRange(nw.Verticies);

            //Generate Octree
            bgw?.ReportProgress(0, 1);
            //GetBounds(out Vector3 minCoordinate, out Vector3 maxCoordinate);
            Vector3 size = Max - MinCoords;
            
            uint ExponentX = (uint)GetNext2Exponent(size.X);
            uint ExponentY = (uint)GetNext2Exponent(size.Y);
            uint ExponentZ = (uint)GetNext2Exponent(size.Z);
            float m = Math.Min(Math.Min(size.X, size.Y), size.Z);
            int cubeSizePower = GetNext2Exponent(m);
            int mx = GetNext2Exponent(2048);
            if (cubeSizePower > mx)
                cubeSizePower = GetNext2Exponent(2048);

            int cubeSize = 1 << cubeSizePower;
            ShiftX = (uint)cubeSizePower;
            ShiftY = (uint)(ExponentX - cubeSizePower);
            ShiftZ = (uint)(ExponentX - cubeSizePower + ExponentY - cubeSizePower);

            MaskX = (uint)(0xFFFFFFFF << (int)ExponentX);
            MaskY = (uint)(0xFFFFFFFF << (int)ExponentY);
            MaskZ = (uint)(0xFFFFFFFF << (int)ExponentZ);

            uint CubeCountX = (uint)Math.Max(1, (1 << (int)ExponentX) / cubeSize),
                 CubeCountY = (uint)Math.Max(1, (1 << (int)ExponentY) / cubeSize),
                 CubeCountZ = (uint)Math.Max(1, (1 << (int)ExponentZ) / cubeSize);

            // Generate the root nodes, which are square cubes required to cover all of the model.
            OctreeRoots = new Octree[CubeCountX * CubeCountY * CubeCountZ];


            int cubeBlow = 2;
            int index = 0;
            for (int z = 0; z < CubeCountZ; z++)
            {
                for (int y = 0; y < CubeCountY; y++)
                {
                    for (int x = 0; x < CubeCountX; x++)
                    {
                        Vector3 cubePosition = MinCoords + ((float)cubeSize) * new Vector3(x, y, z);
                        if (index >= OctreeRoots.Length)
                        {
                            //Something went REALLY WRONG for this to happen...
                            throw new Exception($"Octree Failure: count={OctreeRoots.Length} z={CubeCountZ} y={CubeCountY} x={CubeCountX} cube={cubeSize} size={size.ToString()} min={MinCoords.ToString()} max={Max.ToString()}");
                        }
                        OctreeRoots[index++] = new Octree(triangledict, cubePosition, cubeSize, MaxTrianglesPerCube, 1048576, MinCubeWidth, cubeBlow, 10);
                        bgw?.ReportProgress((int)GetPercentOf(index, OctreeRoots.Length), 1);
                    }
                }
            }
            bgw?.ReportProgress(100, 1);
        }

        public void Save(string file, BackgroundWorker? bgw = null)
        {
            FileStream FS = new FileStream(file, FileMode.Create);
            Write(FS, bgw);
            FS.Close();
            FileName = file;
        }
        
        public void Read(Stream KCLFile, BackgroundWorker? bgw = null)
        {
            int MaxItemsForPogress = 0;
            bgw?.ReportProgress(0, 0);
            long FileStart = KCLFile.Position;
            //Header - There's no magic so we'll just have to hope the user inputted a real KCL file...
            int PositionsOffset = KCLFile.ReadInt32();
            int NormalsOffset = KCLFile.ReadInt32();
            int TrianglesOffset = KCLFile.ReadInt32() + 0x10; //Why...?
            int OctreeOffset = KCLFile.ReadInt32();
            float Thickness = KCLFile.ReadSingle();
            MinCoords = new Vector3(KCLFile.ReadSingle(), KCLFile.ReadSingle(), KCLFile.ReadSingle());
            MaskX = KCLFile.ReadUInt32();
            MaskY = KCLFile.ReadUInt32();
            MaskZ = KCLFile.ReadUInt32();
            ShiftX = KCLFile.ReadUInt32();
            ShiftY = KCLFile.ReadUInt32();
            ShiftZ = KCLFile.ReadUInt32();
            int PositionCount = (NormalsOffset - PositionsOffset) / 12,
            NormalCount = (TrianglesOffset - NormalsOffset) / 12,
            TriangleCount = (OctreeOffset - TrianglesOffset) / 16,
            OctreeNodeCount
                 = ((~(int)MaskX >> (int)ShiftX) + 1)
                 * ((~(int)MaskY >> (int)ShiftX) + 1)
                 * ((~(int)MaskZ >> (int)ShiftX) + 1);
            MaxItemsForPogress = PositionCount + NormalCount + TriangleCount + OctreeNodeCount;

            //Section 1 - Verticies
            KCLFile.Position = FileStart + PositionsOffset;
            for (int i = 0; i < PositionCount; i++)
            {
                Positions.Add(new Vector3(KCLFile.ReadSingle(), KCLFile.ReadSingle(), KCLFile.ReadSingle()));
                bgw?.ReportProgress((int)GetPercentOf(i, MaxItemsForPogress), 0);
            }

            //Section 2 - Normals
            KCLFile.Position = FileStart + NormalsOffset;
            for (int i = 0; i < NormalCount; i++)
            {
                Normals.Add(new Vector3(KCLFile.ReadSingle(), KCLFile.ReadSingle(), KCLFile.ReadSingle()));
                bgw?.ReportProgress((int)GetPercentOf(PositionCount + i, MaxItemsForPogress), 0);
            }

            //Section 3 - Triangles
            KCLFile.Position = FileStart + TrianglesOffset;
            
            for (int i = 0; i < TriangleCount; i++)
            {
                Triangles.Add(new KCLFace(KCLFile));
                bgw?.ReportProgress((int)GetPercentOf(PositionCount + NormalCount + i, MaxItemsForPogress), 0);
            }

            //Section 4 - Spatial Index
            KCLFile.Position = FileStart + OctreeOffset;

            OctreeRoots = new Octree[OctreeNodeCount];
            for (int i = 0; i < OctreeNodeCount; i++)
            {
                OctreeRoots[i] = new Octree(KCLFile, FileStart + OctreeOffset);
                bgw?.ReportProgress((int)GetPercentOf(PositionCount + NormalCount + TriangleCount + i, MaxItemsForPogress), 0);
            }
            bgw?.ReportProgress(100, 0);
        }

        public void Write(Stream KCLFile, BackgroundWorker? bgw = null)
        {
            int OctreeNodeCount = GetNodeCount(OctreeRoots);
            Queue<Octree[]> queuedNodes = new Queue<Octree[]>();
            Dictionary<ushort[], int> indexPool = CreateIndexBuffer(queuedNodes);

            int MaxItemsForPogress = Positions.Count + Normals.Count + Triangles.Count + OctreeNodeCount;
            foreach (KeyValuePair<ushort[], int> item in indexPool)
                MaxItemsForPogress += item.Key.Length;

            bgw?.ReportProgress(0, 2);
            long FileStart = KCLFile.Position;
            //Header
            KCLFile.WriteUInt32(0x38);
            KCLFile.WriteUInt32((uint)(0x38 + (Positions.Count*12)));
            KCLFile.WriteUInt32((uint)(0x38 + (Positions.Count * 12) + (Normals.Count * 12) - 0x10));
            uint OctreeOffset = (uint)((0x38 + (Positions.Count * 12) + (Normals.Count * 12) + (Triangles.Count * 0x10)));
            KCLFile.WriteUInt32(OctreeOffset);

            KCLFile.WriteSingle(40f);
            KCLFile.WriteSingle(MinCoords.X);
            KCLFile.WriteSingle(MinCoords.Y);
            KCLFile.WriteSingle(MinCoords.Z);
            KCLFile.WriteUInt32(MaskX);
            KCLFile.WriteUInt32(MaskY);
            KCLFile.WriteUInt32(MaskZ);
            KCLFile.WriteUInt32(ShiftX);
            KCLFile.WriteUInt32(ShiftY);
            KCLFile.WriteUInt32(ShiftZ);

            for (int i = 0; i < Positions.Count; i++)
            {
                KCLFile.WriteSingle(Positions[i].X);
                KCLFile.WriteSingle(Positions[i].Y);
                KCLFile.WriteSingle(Positions[i].Z);
                bgw?.ReportProgress((int)GetPercentOf(i, MaxItemsForPogress), 2);
            }
            for (int i = 0; i < Normals.Count; i++)
            {
                KCLFile.WriteSingle(Normals[i].X);
                KCLFile.WriteSingle(Normals[i].Y);
                KCLFile.WriteSingle(Normals[i].Z);
                bgw?.ReportProgress((int)GetPercentOf(Positions.Count + i, MaxItemsForPogress), 2);
            }
            for (int i = 0; i < Triangles.Count; i++)
            {
                Triangles[i].Write(KCLFile);
                bgw?.ReportProgress((int)GetPercentOf(Positions.Count + Normals.Count + i, MaxItemsForPogress), 2);
            }

            //Octree time
            int triangleListPos = OctreeNodeCount * sizeof(uint);

            queuedNodes.Enqueue(OctreeRoots);
            int OctreeCounter = 0;
            while (queuedNodes.Count > 0)
            {
                Octree[] nodes = queuedNodes.Dequeue();
                long offset = KCLFile.Position - FileStart - OctreeOffset;
                foreach (Octree node in nodes)
                {
                    if (node.Children == null)
                    {
                        // Node is a leaf and points to triangle index list.
                        ushort[] indices = node.TriangleIndices.ToArray();
                        int listPos = triangleListPos + indexPool[indices];
                        node.Key = (uint)Octree.Flags.Values | (uint)(listPos - offset - sizeof(ushort));
                    }
                    else
                    {
                        // Node is a branch and points to 8 children.
                        node.Key = (uint)(nodes.Length + queuedNodes.Count * 8) * sizeof(uint);
                        queuedNodes.Enqueue(node.Children);
                    }
                    KCLFile.WriteUInt32(node.Key);
                    bgw?.ReportProgress((int)GetPercentOf(Positions.Count + Normals.Count + Triangles.Count + OctreeCounter++, MaxItemsForPogress), 2);
                }
            }

            int indexindex = 0;
            foreach (var ind in indexPool)
            {
                //Last value skip. Uses terminator of previous index list
                if (ind.Key.Length == 0)
                    break;
                //Save the index lists and terminator
                for (int i = 0; i < ind.Key.Length; i++)
                    KCLFile.WriteUInt16((ushort)(ind.Key[i] + 1)); //-1 indexed
                KCLFile.Write(new byte[2], 0, 2); // Terminator

                bgw?.ReportProgress((int)GetPercentOf(Positions.Count + Normals.Count + Triangles.Count + OctreeNodeCount + indexindex++ , MaxItemsForPogress), 2);
            }
            bgw?.ReportProgress(100, 2);
        }

        private int IndexOfVertex(Vector3 value, List<Vector3> valueList, Dictionary<string, int> lookupTable)
        {
            //Correct all -0's... no idea why they appear
            //if (value.X == -0)
            //    value.X = 0;
            //if (value.Y == -0)
            //    value.Y = 0;
            //if (value.Z == -0)
            //    value.Z = 0;
            string key = value.ToString();
            if (!lookupTable.ContainsKey(key))
            {
                valueList.Add(value);
                lookupTable.Add(key, lookupTable.Count);
            }

            return lookupTable[key];
        }

        /// <summary>
        /// Gets the next power of 2 which results in a value bigger than or equal to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to which the next power of 2 will be determined.</param>
        /// <returns>The next power of resulting in a value bigger than or equal to the given value.</returns>
        internal static int GetNext2Exponent(float value)
        {
            if (value <= 1) return 0;
            return (int)Math.Ceiling(Math.Log(value, 2));
        }

        public Triangle GetTriangle(KCLFace prism)
        {
            Vector3 A = Positions[prism.PositionIndex];
            Vector3 CrossA = Vector3.Cross(Normals[prism.NormalAIndex], Normals[prism.DirectionIndex]);
            Vector3 CrossB = Vector3.Cross(Normals[prism.NormalBIndex], Normals[prism.DirectionIndex]);
            Vector3 B = A + CrossB * (prism.Length / Vector3.Dot(CrossB, Normals[prism.NormalCIndex]));
            Vector3 C = A + CrossA * (prism.Length / Vector3.Dot(CrossA, Normals[prism.NormalCIndex]));
            Vector3 N = Vector3.Normalize(Vector3.Cross(B - A, C - A));
            return new Triangle() { Vertex1 = A, Vertex2 = B, Vertex3 = C, Normal = N, GroupIndex = prism.GroupIndex };
        }

        private int GetNodeCount(Octree[] nodes)
        {
            int count = nodes.Length;
            foreach (Octree node in nodes)
                if (node.Children != null)
                    count += GetNodeCount(node.Children);
            return count;
        }

        //Create an index buffer to find matching index lists
        private Dictionary<ushort[], int> CreateIndexBuffer(Queue<Octree[]> queuedNodes)
        {
            Dictionary<ushort[], int> indexPool = new Dictionary<ushort[], int>(new IndexEqualityComparer());
            int offset = 0;
            queuedNodes.Enqueue(OctreeRoots);
            int index = 0;
            while (queuedNodes.Count > 0)
            {
                Octree[] nodes = queuedNodes.Dequeue();
                foreach (Octree node in nodes)
                {
                    if (node.Children == null)
                    {
                        ushort[] indices = node.TriangleIndices.ToArray();
                        if (node.TriangleIndices.Count > 0 && !indexPool.ContainsKey(indices))
                        {
                            indexPool.Add(indices, offset);
                            offset += (node.TriangleIndices.Count + 1) * sizeof(ushort); //+1 to add terminator
                            index++;
                        }
                    }
                    else
                    {
                        // Node is a branch and points to 8 children.
                        queuedNodes.Enqueue(node.Children);
                    }
                }
            }
            //Empty values are last in the buffer using the last terminator
            indexPool.Add(new ushort[0], offset - sizeof(ushort));
            return indexPool;
        }

        private class IndexEqualityComparer : IEqualityComparer<ushort[]>
        {
            public bool Equals(ushort[]? x, ushort[]? y)
            {
                if (x.Length != y.Length)
                    return false;
                for (int i = 0; i < x.Length; i++)
                    if (x[i] != y[i])
                        return false;
                return true;
            }

            public int GetHashCode(ushort[] obj)
            {
                int result = 17;
                for (int i = 0; i < obj.Length; i++)
                    unchecked
                    {
                        result = result * 23 + obj[i];
                    }
                return result;
            }
        }

        public class KCLFace
        {
            public float Length;
            public ushort PositionIndex;
            public ushort DirectionIndex;
            public ushort NormalAIndex, NormalBIndex, NormalCIndex;
            public ushort GroupIndex;

            public KCLFace() { }
            public KCLFace(Stream KCLFile)
            {
                Length = KCLFile.ReadSingle();
                PositionIndex = KCLFile.ReadUInt16();
                DirectionIndex = KCLFile.ReadUInt16();
                NormalAIndex = KCLFile.ReadUInt16();
                NormalBIndex = KCLFile.ReadUInt16();
                NormalCIndex = KCLFile.ReadUInt16();
                GroupIndex = KCLFile.ReadUInt16();
            }

            public override string ToString() => $"KCLFace: P = {PositionIndex} | D = {DirectionIndex} | A = {NormalAIndex} | B = {NormalBIndex} | C = {NormalCIndex} | Group = {GroupIndex}";

            internal void Write(Stream KCLFile)
            {
                KCLFile.WriteSingle(Length);
                KCLFile.WriteUInt16(PositionIndex);
                KCLFile.WriteUInt16(DirectionIndex);
                KCLFile.WriteUInt16(NormalAIndex);
                KCLFile.WriteUInt16(NormalBIndex);
                KCLFile.WriteUInt16(NormalCIndex);
                KCLFile.WriteUInt16(GroupIndex);
            }
        }

        public class Octree : IEnumerable<Octree>
        {
            // ---- CONSTANTS ----------------------------------------------------------------------------------------------

            /// <summary>
            /// The number of children of an octree node.
            /// </summary>
            public const int ChildCount = 8;

            /// <summary>
            /// The bits storing the flags of this node.
            /// </summary>
            protected const uint _flagMask = 0b11000000_00000000_00000000_00000000;

            // ---- CONSTRUCTORS & DESTRUCTOR ------------------------------------------------------------------------------
            
            internal Octree(Stream KCLFile, long parentOffset)
            {
                Key = KCLFile.ReadUInt32();
                int terminator = 0x00;

                // Get and seek to the data offset in bytes relative to the parent node's start.
                long offset = parentOffset + Key & ~_flagMask;
                if ((Key >> 31) == 1) //Check for leaf
                {
                    // Node is a leaf and key points to triangle list starting 2 bytes later.
                    long pauseposition = KCLFile.Position;
                    KCLFile.Position = offset + sizeof(ushort);
                        TriangleIndices = new List<ushort>();
                        ushort index;
                        while ((index = KCLFile.ReadUInt16()) != terminator)
                        {
                            TriangleIndices.Add((ushort)(index - 1));
                        }

                    KCLFile.Position = pauseposition;
                }
                else
                {
                    // Node is a branch and points to 8 child nodes.
                    long pauseposition = KCLFile.Position;
                    KCLFile.Position = offset;

                    Octree[] children = new Octree[ChildCount];
                    for (int i = 0; i < ChildCount; i++)
                    {
                        children[i] = new Octree(KCLFile, offset);
                    }
                    Children = children;
                    
                    KCLFile.Position = pauseposition;
                }
            }

            internal Octree(Dictionary<ushort, Triangle> triangles, Vector3 cubePosition, float cubeSize, int maxTrianglesInCube, int maxCubeSize, int minCubeSize, int cubeBlow, int maxDepth, int depth = 0)
            {
                Key = 0;
                //Adjust the cube sizes based on EFE's method
                Vector3 cubeCenter = cubePosition + new Vector3(cubeSize / 2f, cubeSize / 2f, cubeSize / 2f);
                float newsize = cubeSize + cubeBlow;
                Vector3 newPosition = cubeCenter - new Vector3(newsize / 2f, newsize / 2f, newsize / 2f);

                // Go through all triangles and remember them if they overlap with the region of this cube.
                Dictionary<ushort, Triangle> containedTriangles = new Dictionary<ushort, Triangle>();
                foreach (KeyValuePair<ushort, Triangle> triangle in triangles)
                {
                    if (TriangleCubeOverlap(triangle.Value, newPosition, newsize))
                    {
                        containedTriangles.Add(triangle.Key, triangle.Value);
                    }
                }

                float halfWidth = cubeSize / 2f;

                bool isTriangleList = cubeSize <= maxCubeSize && containedTriangles.Count <= maxTrianglesInCube || cubeSize <= minCubeSize || depth > maxDepth;

                if (containedTriangles.Count > maxTrianglesInCube && halfWidth >= minCubeSize)
                {
                    // Too many triangles are in this cube, and it can still be subdivided into smaller cubes.
                    float childCubeSize = cubeSize / 2f;
                    Children = new Octree[ChildCount];
                    int i = 0;
                    for (int z = 0; z < 2; z++)
                    {
                        for (int y = 0; y < 2; y++)
                        {
                            for (int x = 0; x < 2; x++)
                            {
                                Vector3 childCubePosition = cubePosition + childCubeSize * new Vector3(x, y, z);
                                Children[i++] = new Octree(containedTriangles, childCubePosition, childCubeSize,
                                    maxTrianglesInCube, maxCubeSize, minCubeSize, cubeBlow, maxDepth, depth + 1);
                            }
                        }
                    }
                }
                else
                {
                    // Either the amount of triangles in this cube is okay or it cannot be subdivided any further.
                    TriangleIndices = containedTriangles.Keys.ToList();
                }
            }

            internal Octree()
            {

            }

            // ---- PROPERTIES ---------------------------------------------------------------------------------------------

            /// <summary>
            /// Gets the octree key used to reference this node.
            /// </summary>
            public uint Key { get; internal set; }

            /// <summary>
            /// Gets the eight children of this node.
            /// </summary>
            public Octree[]? Children { get; internal set; } = null;

            /// <summary>
            /// Gets the indices to triangles of the model appearing in this cube.
            /// </summary>
            public List<ushort> TriangleIndices { get; internal set; }

            // ---- METHODS (PUBLIC) ---------------------------------------------------------------------------------------

            /// <summary>
            /// Returns an enumerator that iterates through the collection.
            /// </summary>
            /// <returns>An enumerator that can be used to iterate through the collection.</returns>
            public IEnumerator<Octree> GetEnumerator() => Children == null ? null : ((IEnumerable<Octree>)Children).GetEnumerator();

            /// <summary>
            /// Returns an enumerator that iterates through a collection.
            /// </summary>
            /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
            IEnumerator IEnumerable.GetEnumerator() => Children?.GetEnumerator();

            public bool IsEmpty() => TriangleIndices?.Count == 0;

            public override string ToString() => IsEmpty() ? "Empty Octree" : $"{Key & 0x0FFFFFFF} | {TriangleIndices?.Count} tris | {(Children is null ? "No Children" : "Children")}";

            public static bool TriangleCubeOverlap(Triangle t, Vector3 Position, float BoxSize)
            {
                float half = BoxSize / 2f;
                //Position is the min pos, so add half the box size
                Position += new Vector3(half, half, half);
                Vector3 v0 = t[0] - Position;
                Vector3 v1 = t[1] - Position;
                Vector3 v2 = t[2] - Position;

                float min = Math.Min(Math.Min(v0.X, v1.X), v2.X);
                float max = Math.Max(Math.Max(v0.X, v1.X), v2.X);
                if (min > half || max < -half) return false;
                if (Math.Min(Math.Min(v0.Y, v1.Y), v2.Y) > half || Math.Max(Math.Max(v0.Y, v1.Y), v2.Y) < -half) return false;
                if (Math.Min(Math.Min(v0.Z, v1.Z), v2.Z) > half || Math.Max(Math.Max(v0.Z, v1.Z), v2.Z) < -half) return false;

                float d = Vector3.Dot(t.Normal, v0);
                double r = half * (Math.Abs(t.Normal.X) + Math.Abs(t.Normal.Y) + Math.Abs(t.Normal.Z));
                if (d > r || d < -r) return false;

                Vector3 e = v1 - v0;
                if (AxisTest(e.Z, -e.Y, v0.Y, v0.Z, v2.Y, v2.Z, half)) return false;
                if (AxisTest(-e.Z, e.X, v0.X, v0.Z, v2.X, v2.Z, half)) return false;
                if (AxisTest(e.Y, -e.X, v1.X, v1.Y, v2.X, v2.Y, half)) return false;

                e = v2 - v1;
                if (AxisTest(e.Z, -e.Y, v0.Y, v0.Z, v2.Y, v2.Z, half)) return false;
                if (AxisTest(-e.Z, e.X, v0.X, v0.Z, v2.X, v2.Z, half)) return false;
                if (AxisTest(e.Y, -e.X, v0.X, v0.Y, v1.X, v1.Y, half)) return false;

                e = v0 - v2;
                if (AxisTest(e.Z, -e.Y, v0.Y, v0.Z, v1.Y, v1.Z, half)) return false;
                if (AxisTest(-e.Z, e.X, v0.X, v0.Z, v1.X, v1.Z, half)) return false;
                if (AxisTest(e.Y, -e.X, v1.X, v1.Y, v2.X, v2.Y, half)) return false;
                return true;
            }
            private static bool AxisTest(double a1, double a2, double b1, double b2, double c1, double c2, double half)
            {
                var p = a1 * b1 + a2 * b2;
                var q = a1 * c1 + a2 * c2;
                var r = half * (Math.Abs(a1) + Math.Abs(a2));
                return Math.Min(p, q) > r || Math.Max(p, q) < -r;
            }

            // ---- ENUMERATIONS -------------------------------------------------------------------------------------------

            internal enum Flags : uint
            {
                Divide = 0b00000000_00000000_00000000_00000000,
                Values = 0b10000000_00000000_00000000_00000000,
                NoData = 0b11000000_00000000_00000000_00000000
            }
        }

        public class PaEntry : BCSV.BCSV.Entry
        {
            private const string CAMERA_ID = "camera_id";
            private const uint CAMERA_ID_MASK = 0x000000FF;
            private const byte CAMERA_ID_SHIFT = 0;
            private const string SOUND_CODE = "Sound_code";
            private const uint SOUND_CODE_MASK = 0x00007F00;
            private const byte SOUND_CODE_SHIFT = 8;
            private const string FLOOR_CODE = "Floor_code";
            private const uint FLOOR_CODE_MASK = 0x01F8000;
            private const byte FLOOR_CODE_SHIFT = 15;
            private const string WALL_CODE = "Wall_code";
            private const uint WALL_CODE_MASK = 0x01E00000;
            private const byte WALL_CODE_SHIFT = 21;
            private const string CAMERA_THROUGH = "Camera_through";
            private const uint CAMERA_THROUGH_MASK = 0x02000000;
            private const byte CAMERA_THROUGH_SHIFT = 25;

            public int CameraID
            {
                get => (int)Data[BCSV.BCSV.StringToHash_JGadget(CAMERA_ID)];
                set => Data[BCSV.BCSV.StringToHash_JGadget(CAMERA_ID)] = (int)(value & (CAMERA_ID_MASK >> CAMERA_ID_SHIFT));
            }
            public int SoundCode
            {
                get => (int)Data[BCSV.BCSV.StringToHash_JGadget(SOUND_CODE)];
                set => Data[BCSV.BCSV.StringToHash_JGadget(SOUND_CODE)] = (int)(value & (SOUND_CODE_MASK >> SOUND_CODE_SHIFT));
            }
            public int FloorCode
            {
                get => (int)Data[BCSV.BCSV.StringToHash_JGadget(FLOOR_CODE)];
                set => Data[BCSV.BCSV.StringToHash_JGadget(FLOOR_CODE)] = (int)(value & (FLOOR_CODE_MASK >> FLOOR_CODE_SHIFT));
            }
            public int WallCode
            {
                get => (int)Data[BCSV.BCSV.StringToHash_JGadget(WALL_CODE)];
                set => Data[BCSV.BCSV.StringToHash_JGadget(WALL_CODE)] = (int)(value & (WALL_CODE_MASK >> WALL_CODE_SHIFT));
            }
            public int CameraThrough
            {
                get => (int)Data[BCSV.BCSV.StringToHash_JGadget(CAMERA_THROUGH)];
                set => Data[BCSV.BCSV.StringToHash_JGadget(CAMERA_THROUGH)] = (int)(value & (CAMERA_THROUGH_MASK >> WALL_CODE_SHIFT));
            }

            /// <summary>
            /// The index of the string is the number for the BCSV Entry. 23 sound codes in SMG2
            /// </summary>
            public static readonly string[] SOUND_CODES = new string[]
            {
                "Default",
                "Dirt",
                "Grass",
                "Stone",
                "Marble Tile",
                "Wood",
                "Hollow Wood",
                "Metal",
                "Snow",
                "Ice",
                "No Sound",
                "Desert Sand",
                "Beach Sand",
                "Carpet",
                "Mud",
                "Honey",
                "Metal (Higher Pitched)",
                "Marble Tile (Snow w/ Bulb Berry)",
                "Marble Tile (Dirt w/ Bulb Berry)",
                "Metal (Soil w/ Bulb Berry)",
                "Cloud",
                "Marble Tile (Beach Sand w/ Bulb Berry)",
                "Marble Tile (Desert Sand w/ Bulb Berry)"
            };

            /// <summary>
            /// The index of the string is the number for the BCSV Entry. 44 Floor codes in SMG2
            /// </summary>
            public static readonly string[] FLOOR_CODES = new string[]
            {
                "Default", //0
                "Death", //1
                "Encourage Slipping", //2
                "Prevent Slipping", //3
                "Inflict Knockback Damage", //4
                "Skateable Ice", //5
                "Bouncy (Small Bounce)", //6
                "Bouncy (Medium Bounce)", //7
                "Bouncy (Large Bounce)", //8
                "Force Sliding [Surface must be tilted]", //9
                "Lava", //10
                "Bouncy (Small Bounce) [Slightly Different]", //11
                "Wandering Dry Bones Repellant", //12
                "Sand", //13
                "Glass", //14
                "Inflict Electric Damage", //15
                "Activate Return Bubble", //16
                "Quicksand", //17
                "Poison Quicksand", //18
                "No Traction", //19
                "Chest Deep Water Pool Floor",
                "Waist Deep Water Pool Floor",
                "Knees Deep Water Pool Floor",
                "Water Puddle Floor",
                "Inflict Spike Damage",
                "Deadly Quicksand",
                "Snow",
                "Move Player in Rail Direction",
                "Activate MoveAreaSphere",
                "Allow Crushing the Player",
                "Sand (No Footprints)",
                "Deadly Poison Quicksand",
                "Mud",
                "Ice (w/ Player Reflection)",
                "Bouncy (Small Bounce) [Beach Umbrella]",
                "Non-Skatable Ice",
                "No Spin Drill Dig",
                "Grass",
                "Cloud",
                "Allow Crushing the Player (No Slipping)",
                "Activate ForceDashCube",
                "Dark Matter",
                "Dusty",
                "Snow (No Slipping)"
            };

            /// <summary>
            /// The index of the string is the number for the BCSV Entry. 9 Wall codes in SMG2
            /// </summary>
            public static readonly string[] WALL_CODES = new string[]
            {
                "Default",
                "No Wall Jumps",
                "No Auto Ledge Getup",
                "No Ledge Grabbing",
                "Ghost Through",
                "No Sidestepping",
                "Rebound the Player",
                "Honey Climb",
                "No Action"
            };

            public PaEntry() { Init(); }
            public PaEntry(Dictionary<uint, BCSV.BCSV.Field> FieldSource)
            {
                Data = new Dictionary<uint, object>();
                foreach (var kv in FieldSource)
                    Data.Add(kv.Key, kv.Value.GetDefaultValue());
                Init();
            }
            private void Init() => CameraID = 0xFF;
        }
    }

    public class GeometryOverflowException : Exception
    {
        public GeometryOverflowException(int count, int max, string item = "Triangles") : base($"Too Many {item}! The Max {item} count is {max} (0x{max.ToString("X4")}). You have {count - max} {item.ToLower()} too many.") { }
    }

    public class WavefrontObj : List<Triangle>
    {
        public List<string> GroupNames = new List<string>();
        public WavefrontObj() : base() { }

        public static WavefrontObj OpenWavefront(string OBJFile)
        {
            List<Vector3> Verticies = new List<Vector3>();
            Dictionary<string, int> GroupTable = new Dictionary<string, int>();
            string[] Lines = File.ReadAllLines(OBJFile);
            string? GroupName = null;
            int GroupID = 0;
            WavefrontObj Triangles = new WavefrontObj();

            for (int i = 0; i < Lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(Lines[i]) || Lines[i].StartsWith("#"))
                    continue;

                string[] args = Lines[i].Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

                if (args[0].Equals("usemtl"))
                {
                    GroupName = args[1];
                    if (!GroupTable.ContainsKey(GroupName))
                    {
                        GroupTable.Add(GroupName, GroupTable.Count);
                        Triangles.GroupNames.Add(GroupName);
                    }
                    GroupID = GroupTable[GroupName];
                }
                else if (args[0].Equals("v"))
                {
                    Verticies.Add(new Vector3(float.Parse(args[1], CultureInfo.InvariantCulture), float.Parse(args[2], CultureInfo.InvariantCulture), float.Parse(args[3], CultureInfo.InvariantCulture)));
                }
                else if (args[0].Equals("f"))
                {
                    if (Triangles.GroupNames.Count == 0)
                        Triangles.GroupNames.Add(GroupName);

                    Vector3 U = Verticies[int.Parse(args[1].Split('/')[0])-1],
                        V = Verticies[int.Parse(args[2].Split('/')[0]) - 1],
                        W = Verticies[int.Parse(args[3].Split('/')[0]) - 1];
                    if (Vector3.Cross(V - U, W - U).NormalSquare() < 0.001)
                        continue; //Haven't had issues with this yet...

                    Triangles.Add(new Triangle(U, V, W, GroupID));
                }
            }


            if (Triangles.GroupNames[0] is null || Triangles.GroupNames[0].Equals("None"))
                Triangles.GroupNames[0] = "Default";

            return Triangles;
        }

        public static WavefrontObj CreateWavefront(KCL kcl, BCSV.BCSV? CollisionCodes = null)
        {
            WavefrontObj Triangles = new WavefrontObj();
            int LastGroup = -1;
            Dictionary<int, string> Groups = new Dictionary<int, string>();
            for (int i = 0; i < kcl.Triangles.Count; i++)
            {
                Triangles.Add(kcl.GetTriangle(kcl.Triangles[i]));

                if (Triangles[Triangles.Count-1].GroupIndex != LastGroup)
                {
                    LastGroup = Triangles[Triangles.Count - 1].GroupIndex;

                    if (CollisionCodes is null)
                    {
                        string item = $"Material{LastGroup}";
                        if (!Groups.ContainsKey(LastGroup))
                            Groups.Add(LastGroup, item);
                    }
                    else
                    {
                        string item = $"{((KCL.PaEntry)CollisionCodes[LastGroup]).CameraID} {KCL.PaEntry.SOUND_CODES[((KCL.PaEntry)CollisionCodes[LastGroup]).SoundCode]} {KCL.PaEntry.FLOOR_CODES[((KCL.PaEntry)CollisionCodes[LastGroup]).FloorCode]} {KCL.PaEntry.WALL_CODES[((KCL.PaEntry)CollisionCodes[LastGroup]).WallCode]} {((KCL.PaEntry)CollisionCodes[LastGroup]).CameraThrough}";
                        if (!Groups.ContainsKey(LastGroup))
                            Groups.Add(LastGroup, item);
                    }  
                }
            }

            int m = 0;
            foreach (KeyValuePair<int, string> item in Groups)
            {
                if (item.Key > m)
                    m = item.Key;
            }
            for (int i = 0; i < m+1; i++)
            {
                if (!Groups.ContainsKey(i))
                    Triangles.GroupNames.Insert(Math.Min(i, Triangles.GroupNames.Count), $"Unused {i}");
                else
                    Triangles.GroupNames.Insert(Math.Min(i, Triangles.GroupNames.Count), Groups[i]);
            }

            return Triangles;
        }

        public static void SaveWavefront(string OBJFile, WavefrontObj obj, BCSV.BCSV? CollisionCodes = null, BackgroundWorker? bgw = null)
        {
            bgw?.ReportProgress(0, 0);
            Version version = Assembly.GetEntryAssembly().GetName().Version;
            string result =
$@"#KCL dumped with Hack.io.KCL version {version.ToString()}
#https://github.com/SuperHackio/Hack.io
o {new FileInfo(OBJFile).Name}
";
            List<string> Verts = new List<string>();
            List<string> Norms = new List<string>();
            int Counter = (obj.Count * 3) + obj.Count;
            for (int i = 0; i < obj.Count; i++)
            {
                AddVert(MakeVertexString(obj[i].Vertex1));
                AddVert(MakeVertexString(obj[i].Vertex2));
                AddVert(MakeVertexString(obj[i].Vertex3));
                AddNorm(MakeNormalString(obj[i].Normal));
                bgw?.ReportProgress((int)GetPercentOf(i/4f,Counter),0);
            }

            for (int i = 0; i < Verts.Count; i++)
                result += Verts[i] + Environment.NewLine;
            for (int i = 0; i < Norms.Count; i++)
                result += Norms[i] + Environment.NewLine;

            bgw?.ReportProgress(0, 1);

            int LastGroup = -1;
            for (int i = 0; i < obj.Count; i++)
            {
                if (obj[i].GroupIndex != LastGroup)
                {
                    LastGroup = obj[i].GroupIndex;
                    result += "usemtl ";
                    if (CollisionCodes is null)
                        result += $"Material{LastGroup}" + Environment.NewLine;
                    else
                        result += $"m{LastGroup} | {((KCL.PaEntry)CollisionCodes[LastGroup]).CameraID} | {KCL.PaEntry.SOUND_CODES[((KCL.PaEntry)CollisionCodes[LastGroup]).SoundCode]} | {KCL.PaEntry.FLOOR_CODES[((KCL.PaEntry)CollisionCodes[LastGroup]).FloorCode]} | {KCL.PaEntry.WALL_CODES[((KCL.PaEntry)CollisionCodes[LastGroup]).WallCode]} | {((KCL.PaEntry)CollisionCodes[LastGroup]).CameraThrough}" + Environment.NewLine;
                    result += $"s off{Environment.NewLine}"; //Turn off Smooth Shading
                }

                int targetvec1 = Find(Verts, MakeVertexString(obj[i].Vertex1)),
                    targetvec2 = Find(Verts, MakeVertexString(obj[i].Vertex2)),
                    targetvec3 = Find(Verts, MakeVertexString(obj[i].Vertex3)),
                    targetnorm = Find(Norms, MakeNormalString(obj[i].Normal));
                result += $"f {targetvec1 + 1}//{targetnorm + 1} {targetvec2 + 1}//{targetnorm + 1} {targetvec3 + 1}//{targetnorm + 1}{Environment.NewLine}";
                bgw?.ReportProgress((int)GetPercentOf(i, obj.Count), 1);
            }

            File.WriteAllText(OBJFile, result);
            bgw?.ReportProgress(100, 1);

            string MakeVertexString(Vector3 vector) => MakeVectorString("v", vector, "0.000000");
            string MakeNormalString(Vector3 vector) => MakeVectorString("vn", vector, "0.00000");
            string MakeVectorString(string prefix, Vector3 vector, string format) => $"{prefix} {vector.X.ToString(format)} {vector.Y.ToString(format)} {vector.Z.ToString(format)}";

            void AddVert(string s) => Add(Verts, s);
            void AddNorm(string s) => Add(Norms, s);
            void Add<T>(List<T> list, T item)
            {
                if (!list.Contains(item))
                    list.Add(item);
            }

            int Find<T>(List<T> list, T item)
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i].Equals(item))
                        return i;
                return -1;
            }
        }
    }

    internal class VertexWelder
    {
        // Three randomly chosen large primes. Just like the good 'ol days
        const uint MAGIC_X = 0x8DA6B343;
        const uint MAGIC_Y = 0xD8163841;
        const uint MAGIC_Z = 0x61B40079;
        private readonly float Threshold, CellWidth;
        //public List<List<int>> Buckets = new List<List<int>>();
        public List<Vector3> Verticies = new List<Vector3>();

        public VertexWelder(float threshold)
        {
            Threshold = threshold;
            CellWidth = 16.0f * threshold;
        }

        //private int CalculateHash(int ix, int iy, int iz) => (int)Math.Abs(((ix * MAGIC_X) + (iy * MAGIC_Y) + (iz * MAGIC_Z)) % Buckets.Count);

        public short Add(Vector3 Vertex)
        {
            //Correct all -0's... no idea why they appear
            if (Vertex.X == -0)
                Vertex.X = 0;
            if (Vertex.Y == -0)
                Vertex.Y = 0;
            if (Vertex.Z == -0)
                Vertex.Z = 0;

            for (int i = 0; i < Verticies.Count; i++)
            {
                if (Math.Abs(Vertex.X - Verticies[i].X) < Threshold && Math.Abs(Vertex.Y - Verticies[i].Y) < Threshold && Math.Abs(Vertex.Z - Verticies[i].Z) < Threshold)
                    return (short)i;
            }

            //int MinX = (int)((Vertex.X - Threshold) / CellWidth),
            //    MinY = (int)((Vertex.Y - Threshold) / CellWidth),
            //    MinZ = (int)((Vertex.Z - Threshold) / CellWidth),
            //    MaxX = (int)((Vertex.X + Threshold) / CellWidth),
            //    MaxY = (int)((Vertex.Y + Threshold) / CellWidth),
            //    MaxZ = (int)((Vertex.Z + Threshold) / CellWidth);
            //List<int> Bucket;
            //for (int ix = MinX; ix < MaxX+1; ix++)
            //{
            //    for (int iy = MinY; iy < MaxY+1; iy++)
            //    {
            //        for (int iz = MinZ; iz < MaxZ+1; iz++)
            //        {
            //            int id = CalculateHash(ix, iy, iz);
            //            Bucket = Buckets[id];
            //            for (int i = 0; i < Bucket.Count; i++)
            //            {

            //            }
            //        }
            //    }
            //}


            Verticies.Add(Vertex);
            //Bucket = Buckets[CalculateHash((int)(Vertex.X / CellWidth), (int)(Vertex.Y / CellWidth), (int)(Vertex.Z / CellWidth))];
            //Bucket.Add(Verticies.Count-1);
            return (short)(Verticies.Count - 1);
        }
    }

    /// <summary>
    /// Note: Will probably get moved to it's own project after another project needs it's usage
    /// </summary>
    public struct Triangle
    {
        public Vector3 Vertex1, Vertex2, Vertex3, Normal;
        public int GroupIndex;

        public Vector3 this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return Vertex1;
                    case 1:
                        return Vertex2;
                    case 2:
                        return Vertex3;
                    case 3:
                        return Normal;
                    default:
                        return Vector3.Zero;
                }
            }
        }

        public Triangle(Vector3 u, Vector3 v, Vector3 w, int groupIndex)
        {
            Vertex1 = u;
            Vertex2 = v;
            Vertex3 = w;
            
            Normal = Vector3.Cross(v - u, w - u).Unit();
            GroupIndex = groupIndex;
        }

        public override string ToString()
        {
            return $"Group {GroupIndex}";
        }
    }

    public static class VectorEx
    {
        public static float NormalSquare(this Vector3 vector) => (vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z);
        public static float Normal(this Vector3 vector) => (float)Math.Sqrt(vector.NormalSquare());
        public static Vector3 Unit(this Vector3 origin) => origin / origin.Normal();
    }
}
