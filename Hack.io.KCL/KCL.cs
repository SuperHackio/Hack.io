using Hack.io.Class;
using Hack.io.Interface;
using Hack.io.Utility;
using System.Diagnostics;
using System.Numerics;

namespace Hack.io.KCL;

/// <summary>
/// A Prism and Octree based collision file format used in some games like Super Mario Galaxy.
/// Does not contain the Sphere Radius that games like Mario Kart Wii require.
/// </summary>
public class KCL : ILoadSaveFile
{
    /// <summary>
    /// The thickness of all prisms in this file.
    /// </summary>
    public float PrismThickness = 40.0f;
    /// <summary>
    /// The minimum vertex position that this model uses. Defines the starting point of the Octree
    /// </summary>
    public Vector3 MinCoords;
    /// <summary>
    /// TODO: Add a full explaination
    /// </summary>
    public TypeVector3<uint> CoordinateMask;
    /// <summary>
    /// TODO: Add a full explaination
    /// </summary>
    public TypeVector3<uint> CoordinateShift;
    /// <summary>
    /// A List of all Prisms in this KCL
    /// </summary>
    public List<Prism> PrismList = [];
    /// <summary>
    /// The Octree root nodes of this KCL.
    /// </summary>
    public List<IOctreeNode> OctreeRoot = [];

    /// <summary>
    /// Returns the number of Octree Nodes that should be present in <see cref="IOctreeNode"/> based on the <see cref="CoordinateMask"/> and <see cref="CoordinateShift"/> values.
    /// </summary>
    public int IntendedRootNodeCount => ((~(int)CoordinateMask.X >> (int)CoordinateShift.X) + 1)
                                      * ((~(int)CoordinateMask.Y >> (int)CoordinateShift.X) + 1)
                                      * ((~(int)CoordinateMask.Z >> (int)CoordinateShift.X) + 1);

    /// <summary>
    /// Returns the number of Octree Nodes individually on the X Y and Z axis that should be present in <see cref="IOctreeNode"/> based on the <see cref="CoordinateMask"/> and <see cref="CoordinateShift"/> values.
    /// </summary>
    public (int x, int y, int z) RootGridSize => (((~(int)CoordinateMask.X) >> (int)CoordinateShift.X) + 1,
                                                  ((~(int)CoordinateMask.Y) >> (int)CoordinateShift.X) + 1,
                                                  ((~(int)CoordinateMask.Z) >> (int)CoordinateShift.X) + 1);

    /// <summary>
    /// Calculates the overall size of the Octree root
    /// </summary>
    public Vector3 RootCellSize => new(1 << (int)CoordinateShift.X,
                                       1 << (int)CoordinateShift.X,
                                       1 << (int)CoordinateShift.X);
    /// <summary>
    /// The number of Prisms attached to the Octree Root directly
    /// </summary>
    public int RootPrismCount
    {
        get
        {
            int count = 0;
            foreach (IOctreeNode item in OctreeRoot)
                if (item is OctreeLeaf OL)
                    count += OL.Count;
            return count;
        }
    }


    /// <inheritdoc/>
    public void Load(Stream Strm)
    {
        long Start = Strm.Position;

        uint PositionsOffset = Strm.ReadUInt32();
        uint NormalsOffset = Strm.ReadUInt32();
        uint TrianglesOffset = Strm.ReadUInt32() + 0x10;
        uint OctreeOffset = Strm.ReadUInt32();
        PrismThickness = Strm.ReadSingle();
        MinCoords = new(Strm.ReadMultiSingle(3));
        CoordinateMask = new(Strm.ReadMultiUInt32(3));
        CoordinateShift = new(Strm.ReadMultiUInt32(3));

        // This newer version of Hack.io.KCL only
        Strm.Position = Start + OctreeOffset;
        int OctreeNodeCount = IntendedRootNodeCount;

        for (int i = 0; i < OctreeNodeCount; i++)
        {
            IOctreeNode node = ReadOctreeNode(Start + OctreeOffset);
            OctreeRoot.Add(node);
        }

        IOctreeNode ReadOctreeNode(long baseOffset)
        {
            uint key = Strm.ReadUInt32();
            uint Offset = key & 0x3FFFFFFF;
            bool IsLeaf = (key & 0x80000000) != 0;
            bool Unknown = (key & 0x40000000) != 0;

            long PausePosition = Strm.Position;
            uint FinalOffset = (uint)baseOffset + Offset;

            IOctreeNode Node;
            if (IsLeaf)
            {
                // If this Octree node is a leaf, it contains a list of triangles
                Strm.Position = FinalOffset + sizeof(ushort);
                OctreeLeaf TriangleIndices = [];
                ushort index;
                while ((index = Strm.ReadUInt16()) != 0x00)
                {
                    ushort prismidx = (ushort)(index - 1);

                    long PausePosition2 = Strm.Position;
                    // Create a new Prism
                    Strm.Position = Start + TrianglesOffset + (prismidx * 16);
                    Prism CurrentPrism = new()
                    {
                        Height = Strm.ReadSingle(),
                        Position = ReadVec3FromTable(PositionsOffset, Strm.ReadUInt16()),
                        FaceNormal = ReadVec3FromTable(NormalsOffset, Strm.ReadUInt16()),
                        EdgeANormal = ReadVec3FromTable(NormalsOffset, Strm.ReadUInt16()),
                        EdgeBNormal = ReadVec3FromTable(NormalsOffset, Strm.ReadUInt16()),
                        EdgeCNormal = ReadVec3FromTable(NormalsOffset, Strm.ReadUInt16()),
                        Attribute = Strm.ReadUInt16()
                    };

                    // Check if such a prism already exists
                    // If it doesn't, add to prism list and use new index
                    // Otherwise, use existing index
                    int PrismIndex = PrismList.IndexOf(CurrentPrism);
                    if (PrismIndex < 0)
                    {
                        PrismIndex = PrismList.Count;
                        PrismList.Add(CurrentPrism);
                    }

                    TriangleIndices.Add((ushort)PrismIndex);

                    Strm.Position = PausePosition2;
                }
                Node = TriangleIndices;
            }
            else
            {
                // If this Octree node is a branch, it contains 8 child Octree nodes
                Strm.Position = FinalOffset;
                OctreeBranch b = [];
                for (int i = 0; i < 8; i++)
                    b.Add(ReadOctreeNode(FinalOffset));

                Node = b;
            }

            Strm.Position = PausePosition;
            return Node;
        }
        Vector3 ReadVec3FromTable(long TableOffset, int index)
        {
            long PausePosition = Strm.Position;
            Strm.Position = Start + TableOffset;
            Strm.Position += index * 0x0C;

            Vector3 result = new(Strm.ReadMultiSingle(3));

            Strm.Position = PausePosition;
            return result;
        }
    }

    /// <inheritdoc/>
    public void Save(Stream Strm)
    {
        long Start = Strm.Position;

        // Build an array of unique vectors
        int TotalPositions = 0;
        int TotalNormals = 0;
        List<Vector3> UniquePositions = [];
        List<Vector3> UniqueNormals = [];

        foreach (Prism p in PrismList)
        {
            UniquePositions.AddIfNotContains(p.Position);
            TotalPositions++;
            UniqueNormals.AddIfNotContains(p.FaceNormal);
            UniqueNormals.AddIfNotContains(p.EdgeANormal);
            UniqueNormals.AddIfNotContains(p.EdgeBNormal);
            UniqueNormals.AddIfNotContains(p.EdgeCNormal);
            TotalNormals += 4;
        }

        const uint HEADER_SIZE = 0x38; // Header is a fixed size
        uint OffsetForHeader;
        Strm.WriteUInt32(OffsetForHeader = HEADER_SIZE); // Offset to Position Vectors
        Strm.WriteUInt32(OffsetForHeader += (uint)UniquePositions.Count * 12u); // Offset to Normal Vectors
        var mult = (uint)UniqueNormals.Count * 12u;
        Strm.WriteUInt32(OffsetForHeader + mult - 0x10); // Offset to Prism structs
        Strm.WriteUInt32(OffsetForHeader += mult + ((uint)PrismList.Count * 0x10u)); // Offset to Octree
        Strm.WriteSingle(PrismThickness);
        Strm.WriteSingle(MinCoords.X);
        Strm.WriteSingle(MinCoords.Y);
        Strm.WriteSingle(MinCoords.Z);
        Strm.WriteUInt32(CoordinateMask.X);
        Strm.WriteUInt32(CoordinateMask.Y);
        Strm.WriteUInt32(CoordinateMask.Z);
        Strm.WriteUInt32(CoordinateShift.X);
        Strm.WriteUInt32(CoordinateShift.Y);
        Strm.WriteUInt32(CoordinateShift.Z);

        // Write all the positions
        foreach (Vector3 vec in UniquePositions)
        {
            Strm.WriteSingle(vec.X);
            Strm.WriteSingle(vec.Y);
            Strm.WriteSingle(vec.Z);
        }

        // Write all the normals
        foreach (Vector3 vec in UniqueNormals)
        {
            Strm.WriteSingle(vec.X);
            Strm.WriteSingle(vec.Y);
            Strm.WriteSingle(vec.Z);
        }

        // Write all the prisms
        foreach (Prism p in PrismList)
        {
            Strm.WriteSingle(p.Height);
            Strm.WriteUInt16((ushort)UniquePositions.IndexOf(p.Position));
            Strm.WriteUInt16((ushort)UniqueNormals.IndexOf(p.FaceNormal));
            Strm.WriteUInt16((ushort)UniqueNormals.IndexOf(p.EdgeANormal));
            Strm.WriteUInt16((ushort)UniqueNormals.IndexOf(p.EdgeBNormal));
            Strm.WriteUInt16((ushort)UniqueNormals.IndexOf(p.EdgeCNormal));
            Strm.WriteUInt16(p.Attribute);
        }


        // Write the Octree
        // As old as this code is, it's the only thing I could get working...
        int triangleListPos = GetNodeCount(OctreeRoot) * sizeof(uint);
        Queue<List<IOctreeNode>> queuedNodes = new();
        Dictionary<ushort[], int> indexPool = CreateIndexBuffer(queuedNodes);


        queuedNodes.Enqueue(OctreeRoot);
        while (queuedNodes.Count > 0)
        {
            List<IOctreeNode> nodes = queuedNodes.Dequeue();
            long offset = Strm.Position - Start - OffsetForHeader;
            foreach (IOctreeNode node in nodes)
            {
                uint Key = 0xDDDDDDDD;
                if (node is OctreeLeaf OL)
                {
                    // Node is a leaf and points to triangle index list.
                    ushort[] indices = [.. OL];
                    int listPos = triangleListPos + indexPool[indices];
                    Key = (uint)0x80000000 | (uint)(listPos - offset - sizeof(ushort));
                }
                else if (node is OctreeBranch OB)
                {
                    // Node is a branch and points to 8 children.
                    Key = (uint)(nodes.Count + queuedNodes.Count * 8) * sizeof(uint);
                    queuedNodes.Enqueue(OB);
                }
                Strm.WriteUInt32(Key);
            }
        }

        foreach (var ind in indexPool)
        {
            //Last value skip. Uses terminator of previous index list
            if (ind.Key.Length == 0)
                break;

            //Save the index lists and terminator
            for (int i = 0; i < ind.Key.Length; i++)
                Strm.WriteUInt16((ushort)(ind.Key[i] + 1)); //-1 indexed
            Strm.WriteUInt16((ushort)0); // Terminator
        }
    }
    

    private Dictionary<ushort[], int> CreateIndexBuffer(Queue<List<IOctreeNode>> queuedNodes)
    {
        Dictionary<ushort[], int> indexPool = new(new IndexEqualityComparer());
        int offset = 0;
        queuedNodes.Enqueue(OctreeRoot);
        while (queuedNodes.Count > 0)
        {
            List<IOctreeNode> nodes = queuedNodes.Dequeue();
            foreach (IOctreeNode node in nodes)
            {
                if (node is OctreeLeaf OL)
                {
                    ushort[] indices = [.. OL];
                    if (OL.Count > 0 && !indexPool.ContainsKey(indices))
                    {
                        indexPool.Add(indices, offset);
                        offset += (OL.Count + 1) * sizeof(ushort); //+1 to add terminator
                    }
                }
                else if (node is OctreeBranch OB)
                {
                    // Node is a branch and points to 8 children.
                    queuedNodes.Enqueue(OB);
                }
            }
        }
        // No index pools made?
        // Make a default empty one
        if (indexPool.Count == 0)
        {
            indexPool.Add([0x0000], offset);
            offset += 1 * sizeof(ushort); //+1 to add terminator
        }

        //Empty values are last in the buffer using the last terminator
        indexPool.Add([], offset - sizeof(ushort));
        return indexPool;
    }

    private static int GetNodeCount(List<IOctreeNode> List)
    {
        int count = List.Count;
        foreach (IOctreeNode node in List)
            if (node is OctreeBranch OB)
                count += GetNodeCount(OB);
        return count;
    }

    private class IndexEqualityComparer : IEqualityComparer<ushort[]>
    {
        public bool Equals(ushort[]? x, ushort[]? y)
        {
            if (x is null && y is null)
                return true;
            if (x is null || y is null)
                return false;
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


    /// <summary>
    /// A Prism that defines where collision is.
    /// </summary>
    public struct Prism : IEquatable<Prism>
    {
        /// <summary>
        /// TODO: Explain this
        /// </summary>
        public float Height;
        /// <summary>
        /// TODO: Explain this
        /// </summary>
        public Vector3 Position;
        /// <summary>
        /// TODO: Explain this
        /// </summary>
        public Vector3 FaceNormal;
        /// <summary>
        /// TODO: Explain this
        /// </summary>
        public Vector3 EdgeANormal;
        /// <summary>
        /// TODO: Explain this
        /// </summary>
        public Vector3 EdgeBNormal;
        /// <summary>
        /// TODO: Explain this
        /// </summary>
        public Vector3 EdgeCNormal;
        /// <summary>
        /// The attribute of this prism. For Super Mario Galaxy, this is an index into a PA file (which is just a BCSV)
        /// </summary>
        public ushort Attribute;

        /// <inheritdoc/>
        public readonly bool Equals(Prism other) => Height.Equals(other.Height)
                && Position.Equals(other.Position)
                && FaceNormal.Equals(other.FaceNormal)
                && EdgeANormal.Equals(other.EdgeANormal)
                && EdgeBNormal.Equals(other.EdgeBNormal)
                && EdgeCNormal.Equals(other.EdgeCNormal)
                && Attribute == other.Attribute;

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is Prism other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => HashCode.Combine(
                Height,
                Position,
                FaceNormal,
                EdgeANormal,
                EdgeBNormal,
                EdgeCNormal,
                Attribute
            );

        /// <inheritdoc/>
        public static bool operator ==(Prism left, Prism right) => left.Equals(right);
        /// <inheritdoc/>
        public static bool operator !=(Prism left, Prism right) => !left.Equals(right);



        /// <summary>
        /// Creates a new prism from 3 position vertices and a face normal.
        /// </summary>
        /// <remarks>TODO: Note winding order (I forgor it atm)</remarks>
        /// <param name="VertexA">The first position vertex</param>
        /// <param name="VertexB">The second position vertex</param>
        /// <param name="VertexC">The third position vertex</param>
        /// <param name="Normal">The normal vector of the face</param>
        /// <param name="Attribute">the attribute value to assign to the prism</param>
        /// <returns>null if the prism failed to generate.</returns>
        public static Prism? Create(Vector3 VertexA, Vector3 VertexB, Vector3 VertexC, Vector3 Normal, ushort Attribute = 0)
        {
            Vector3 BsubA = VertexB - VertexA;
            Vector3 CsubA = VertexC - VertexA;
            Vector3 BsubC = VertexB - VertexC;
            Vector3 direction = Vector3.Cross(BsubA, CsubA);
            if (direction.LengthSquared() < 0.001)
                return null;
            direction = Vector3.Normalize(direction);

            //Calculate the ABC normal values.
            Vector3 normalA = Vector3.Cross(direction, CsubA);
            Vector3 normalB = -Vector3.Cross(direction, BsubA);
            Vector3 normalC = Vector3.Cross(direction, BsubC);
            //Normalize the ABC normal values.
            normalA = Vector3.Normalize(normalA);
            normalB = Vector3.Normalize(normalB);
            normalC = Vector3.Normalize(normalC);

            float length = Vector3.Dot(BsubA, normalC);

            return new()
            {
                Height = length,
                Position = VertexA,
                FaceNormal = Normal,
                EdgeANormal = normalA,
                EdgeBNormal = normalB,
                EdgeCNormal = normalC,
                Attribute = Attribute,
            };
        }
        /// <summary>
        /// Decomposes a prism back into 3 Position Vectors and a face normal
        /// </summary>
        /// <param name="prism">The prism to decompose</param>
        /// <returns>3 position vectors and a face normal</returns>
        public static (Vector3 A, Vector3 B, Vector3 C, Vector3 N) Decompose(Prism prism)
        {
            Vector3 A = prism.Position;
            Vector3 CrossA = Vector3.Cross(prism.EdgeANormal, prism.FaceNormal);
            Vector3 CrossB = Vector3.Cross(prism.EdgeBNormal, prism.FaceNormal);
            Vector3 B = A + CrossB * (prism.Height / Vector3.Dot(CrossB, prism.EdgeCNormal));
            Vector3 C = A + CrossA * (prism.Height / Vector3.Dot(CrossA, prism.EdgeCNormal));
            Vector3 N = Vector3.Normalize(Vector3.Cross(B - A, C - A));
            return (A, B, C, N);
        }
    }
    
    /// <summary>
    /// A Leaf of the Octree, which contains indexes into <see cref="PrismList"/>
    /// </summary>
    [DebuggerDisplay("LEAF = {Count}")]
    public class OctreeLeaf : List<ushort>, IOctreeNode
    {
        /// <inheritdoc/>
        public OctreeLeaf() : base()
        {

        }
        /// <inheritdoc/>
        public OctreeLeaf(int capacity) : base(capacity)
        {

        }
        /// <inheritdoc/>
        public OctreeLeaf(IEnumerable<ushort> collection) : base(collection)
        {

        }

        /// <inheritdoc/>
        public int PrismCount => Count;
    }
    /// <summary>
    /// A Branch of the Octree, which contains 8 child nodes.
    /// </summary>
    [DebuggerDisplay("BRANCH = {Count}")]
    public class OctreeBranch : List<IOctreeNode>, IOctreeNode
    {
        /// <inheritdoc/>
        public int PrismCount
        {
            get
            {
                int count = 0;
                foreach (var item in this)
                    count += item.PrismCount;
                return count;
            }
        }
    }
    /// <summary>
    /// An interface for Octree nodes
    /// </summary>
    public interface IOctreeNode
    {
        /// <summary>
        /// The number of Prisms contained within this node and it's children (if a Branch)
        /// </summary>
        public int PrismCount { get; }
    }
}
