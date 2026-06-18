namespace CyanMothUnityEcs
{
    public unsafe sealed partial class World
    {
        public Query<T1> Query<T1>()
            where T1 : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType t1 = TypeRegistry.Get<T1>();
            int queryId = _queryCache.GetOrCreate(t1.Mask, ComponentMask.Empty, t1.Index);
            return new Query<T1>(this, queryId);
        }

        public Query<T1, T2> Query<T1, T2>()
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentMask include = t1.Mask.Add(t2.Mask);
            int queryId = _queryCache.GetOrCreate(include, ComponentMask.Empty, t1.Index, t2.Index);
            return new Query<T1, T2>(this, queryId);
        }

        public Query<T1, T2, T3> Query<T1, T2, T3>()
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();
            ComponentMask include = t1.Mask.Add(t2.Mask).Add(t3.Mask);
            int queryId = _queryCache.GetOrCreate(include, ComponentMask.Empty, t1.Index, t2.Index, t3.Index);
            return new Query<T1, T2, T3>(this, queryId);
        }

        internal void ForEachChunk<T1>(int queryId, ChunkAction<T1> action)
            where T1 : unmanaged, IComponentData
        {
            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    action(GetEntityArray(chunk, archetype), (T1*)((byte*)chunk + match.Offset1), chunk->Count);
                }
            }
        }

        internal void ForEachChunk<T1, T2>(int queryId, ChunkAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    action(
                        GetEntityArray(chunk, archetype),
                        (T1*)((byte*)chunk + match.Offset1),
                        (T2*)((byte*)chunk + match.Offset2),
                        chunk->Count);
                }
            }
        }

        internal void ForEachChunk<T1, T2, T3>(int queryId, ChunkAction<T1, T2, T3> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    action(
                        GetEntityArray(chunk, archetype),
                        (T1*)((byte*)chunk + match.Offset1),
                        (T2*)((byte*)chunk + match.Offset2),
                        (T3*)((byte*)chunk + match.Offset3),
                        chunk->Count);
                }
            }
        }

        private static Entity* GetEntityArray(Chunk* chunk, Archetype archetype)
        {
            return (Entity*)((byte*)chunk + archetype.Layout.EntityOffset);
        }
    }
}
