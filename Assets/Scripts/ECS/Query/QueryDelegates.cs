namespace CyanMothUnityEcs
{
    public unsafe readonly struct EnabledChunk
    {
        public readonly byte* Mask;
        public readonly int Count;

        public EnabledChunk(byte* mask, int count)
        {
            Mask = mask;
            Count = count;
        }

        public bool IsEnabled(int slot)
        {
            if ((uint)slot >= Count)
                return false;
            if (Mask == null)
                return true;

            return (Mask[slot >> 3] & (1 << (slot & 7))) != 0;
        }
    }

    public delegate void QueryAction<T1>(Entity entity, ref T1 c1)
        where T1 : unmanaged, IComponentData;

    public delegate void ReadOnlyQueryAction<T1>(Entity entity, in T1 c1)
        where T1 : unmanaged, IComponentData;

    public delegate void QueryAction<T1, T2>(Entity entity, ref T1 c1, ref T2 c2)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData;

    public delegate void ReadOnlyQueryAction<T1, T2>(Entity entity, in T1 c1, in T2 c2)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData;

    public delegate void QueryAction<T1, T2, T3>(Entity entity, ref T1 c1, ref T2 c2, ref T3 c3)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
        where T3 : unmanaged, IComponentData;

    public delegate void ReadOnlyQueryAction<T1, T2, T3>(Entity entity, in T1 c1, in T2 c2, in T3 c3)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
        where T3 : unmanaged, IComponentData;

    public unsafe delegate void ChunkAction<T1>(Entity* entities, T1* c1, int count)
        where T1 : unmanaged, IComponentData;

    public unsafe delegate void EnabledChunkAction<T1>(EnabledChunk enabled, Entity* entities, T1* c1, int count)
        where T1 : unmanaged, IComponentData;

    public unsafe delegate void ChunkAction<T1, T2>(Entity* entities, T1* c1, T2* c2, int count)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData;

    public unsafe delegate void EnabledChunkAction<T1, T2>(EnabledChunk enabled, Entity* entities, T1* c1, T2* c2, int count)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData;

    public unsafe delegate void ChunkAction<T1, T2, T3>(Entity* entities, T1* c1, T2* c2, T3* c3, int count)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
        where T3 : unmanaged, IComponentData;

    public unsafe delegate void EnabledChunkAction<T1, T2, T3>(EnabledChunk enabled, Entity* entities, T1* c1, T2* c2, T3* c3, int count)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
        where T3 : unmanaged, IComponentData;
}
