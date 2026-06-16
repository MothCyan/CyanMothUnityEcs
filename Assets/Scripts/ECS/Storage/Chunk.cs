using System.Runtime.InteropServices;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// Chunk 是 ECS 的固定大小数据块。
    /// Header 放在 native block 的开头，Header 后面才是 Entity 数组和各组件数组。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct Chunk
    {
        public const int Size = 16 * 1024;
        public const int Alignment = 64;

        public int ArchetypeId;
        public int Count;
        public int Capacity;
        public int Sequence;
        public int Flags;
        public int Reserved;

        public Chunk* Next;
        public Chunk* Prev;
        public Chunk* NextFree;

        public int* ChangeVersions;

        public void Reset(int archetypeId, int capacity, int sequence)
        {
            ArchetypeId = archetypeId;
            Count = 0;
            Capacity = capacity;
            Sequence = sequence;
            Flags = 0;
            Reserved = 0;
            Next = null;
            Prev = null;
            NextFree = null;
            ChangeVersions = null;
        }
    }
}
