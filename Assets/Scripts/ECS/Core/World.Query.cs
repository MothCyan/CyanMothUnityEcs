using System;

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

        internal void ForEach<T1>(int queryId, QueryAction<T1> action)
            where T1 : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i]);
                    }

                    MarkComponentChanged(chunk, archetype, t1);
                }
            }
        }

        internal void ForEachWrite<T1, TWrite>(int queryId, QueryAction<T1> action)
            where T1 : unmanaged, IComponentData
            where TWrite : unmanaged, IComponentData
        {
            ComponentType writeType = GetWriteType<TWrite>(TypeRegistry.Get<T1>());

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i]);
                    }

                    MarkComponentChanged(chunk, archetype, writeType);
                }
            }
        }

        internal void ForEachReadOnly<T1>(int queryId, ReadOnlyQueryAction<T1> action)
            where T1 : unmanaged, IComponentData
        {
            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], in c1[i]);
                    }
                }
            }
        }

        internal void ForEachChanged<T1, TChanged>(int queryId, int sinceVersion, QueryAction<T1> action)
            where T1 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();
            ComponentType t1 = TypeRegistry.Get<T1>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i]);
                    }

                    MarkComponentChanged(chunk, archetype, t1);
                }
            }
        }

        internal void ForEachChangedWrite<T1, TChanged, TWrite>(int queryId, int sinceVersion, QueryAction<T1> action)
            where T1 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
            where TWrite : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();
            ComponentType writeType = GetWriteType<TWrite>(TypeRegistry.Get<T1>());

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i]);
                    }

                    MarkComponentChanged(chunk, archetype, writeType);
                }
            }
        }

        internal void ForEachChangedReadOnly<T1, TChanged>(int queryId, int sinceVersion, ReadOnlyQueryAction<T1> action)
            where T1 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], in c1[i]);
                    }
                }
            }
        }

        internal void ForEach<T1, T2>(int queryId, QueryAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i], ref c2[i]);
                    }

                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                }
            }
        }

        internal void ForEachWrite<T1, T2, TWrite>(int queryId, QueryAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where TWrite : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType writeType = GetWriteType<TWrite>(t1, t2);

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i], ref c2[i]);
                    }

                    MarkComponentChanged(chunk, archetype, writeType);
                }
            }
        }

        internal void ForEachReadOnly<T1, T2>(int queryId, ReadOnlyQueryAction<T1, T2> action)
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

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], in c1[i], in c2[i]);
                    }
                }
            }
        }

        internal void ForEachChanged<T1, T2, TChanged>(int queryId, int sinceVersion, QueryAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i], ref c2[i]);
                    }

                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                }
            }
        }

        internal void ForEachChangedWrite<T1, T2, TChanged, TWrite>(int queryId, int sinceVersion, QueryAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
            where TWrite : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType writeType = GetWriteType<TWrite>(t1, t2);

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i], ref c2[i]);
                    }

                    MarkComponentChanged(chunk, archetype, writeType);
                }
            }
        }

        internal void ForEachChangedReadOnly<T1, T2, TChanged>(int queryId, int sinceVersion, ReadOnlyQueryAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], in c1[i], in c2[i]);
                    }
                }
            }
        }

        internal void ForEach<T1, T2, T3>(int queryId, QueryAction<T1, T2, T3> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    T3* c3 = (T3*)((byte*)chunk + match.Offset3);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i], ref c2[i], ref c3[i]);
                    }

                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                    MarkComponentChanged(chunk, archetype, t3);
                }
            }
        }

        internal void ForEachWrite<T1, T2, T3, TWrite>(int queryId, QueryAction<T1, T2, T3> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
            where TWrite : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();
            ComponentType writeType = GetWriteType<TWrite>(t1, t2, t3);

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    T3* c3 = (T3*)((byte*)chunk + match.Offset3);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i], ref c2[i], ref c3[i]);
                    }

                    MarkComponentChanged(chunk, archetype, writeType);
                }
            }
        }

        internal void ForEachReadOnly<T1, T2, T3>(int queryId, ReadOnlyQueryAction<T1, T2, T3> action)
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

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    T3* c3 = (T3*)((byte*)chunk + match.Offset3);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], in c1[i], in c2[i], in c3[i]);
                    }
                }
            }
        }

        internal void ForEachChanged<T1, T2, T3, TChanged>(int queryId, int sinceVersion, QueryAction<T1, T2, T3> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    T3* c3 = (T3*)((byte*)chunk + match.Offset3);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i], ref c2[i], ref c3[i]);
                    }

                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                    MarkComponentChanged(chunk, archetype, t3);
                }
            }
        }

        internal void ForEachChangedWrite<T1, T2, T3, TChanged, TWrite>(int queryId, int sinceVersion, QueryAction<T1, T2, T3> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
            where TWrite : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();
            ComponentType writeType = GetWriteType<TWrite>(t1, t2, t3);

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    T3* c3 = (T3*)((byte*)chunk + match.Offset3);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], ref c1[i], ref c2[i], ref c3[i]);
                    }

                    MarkComponentChanged(chunk, archetype, writeType);
                }
            }
        }

        internal void ForEachChangedReadOnly<T1, T2, T3, TChanged>(int queryId, int sinceVersion, ReadOnlyQueryAction<T1, T2, T3> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    Entity* entities = GetEntityArray(chunk, archetype);
                    T1* c1 = (T1*)((byte*)chunk + match.Offset1);
                    T2* c2 = (T2*)((byte*)chunk + match.Offset2);
                    T3* c3 = (T3*)((byte*)chunk + match.Offset3);
                    for (int i = 0; i < chunk->Count; i++)
                    {
                        if (!IsSlotEnabledForQuery(match, chunk, archetype, i))
                            continue;

                        action(entities[i], in c1[i], in c2[i], in c3[i]);
                    }
                }
            }
        }

        internal void ForEachChunk<T1>(int queryId, ChunkAction<T1> action)
            where T1 : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    action(GetEntityArray(chunk, archetype), (T1*)((byte*)chunk + match.Offset1), chunk->Count);
                    MarkComponentChanged(chunk, archetype, t1);
                }
            }
        }

        internal void ForEachEnabledChunk<T1, TEnabled>(int queryId, EnabledChunkAction<T1> action)
            where T1 : unmanaged, IComponentData
            where TEnabled : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType enabledType = TypeRegistry.Get<TEnabled>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    action(
                        GetEnabledChunk(chunk, archetype, enabledType, GetEnabledSlot(enabledType, t1, match.Slot1)),
                        GetEntityArray(chunk, archetype),
                        (T1*)((byte*)chunk + match.Offset1),
                        chunk->Count);
                    MarkComponentChanged(chunk, archetype, t1);
                }
            }
        }

        internal void ForEachChunkReadOnly<T1>(int queryId, ChunkAction<T1> action)
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

        internal void ForEachChangedChunk<T1, TChanged>(int queryId, int sinceVersion, ChunkAction<T1> action)
            where T1 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();
            ComponentType t1 = TypeRegistry.Get<T1>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    action(GetEntityArray(chunk, archetype), (T1*)((byte*)chunk + match.Offset1), chunk->Count);
                    MarkComponentChanged(chunk, archetype, t1);
                }
            }
        }

        internal void ForEachChangedChunkReadOnly<T1, TChanged>(int queryId, int sinceVersion, ChunkAction<T1> action)
            where T1 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    action(GetEntityArray(chunk, archetype), (T1*)((byte*)chunk + match.Offset1), chunk->Count);
                }
            }
        }

        internal void ForEachChunk<T1, T2>(int queryId, ChunkAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();

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
                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                }
            }
        }

        internal void ForEachEnabledChunk<T1, T2, TEnabled>(int queryId, EnabledChunkAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where TEnabled : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType enabledType = TypeRegistry.Get<TEnabled>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    action(
                        GetEnabledChunk(chunk, archetype, enabledType, GetEnabledSlot(enabledType, t1, match.Slot1, t2, match.Slot2)),
                        GetEntityArray(chunk, archetype),
                        (T1*)((byte*)chunk + match.Offset1),
                        (T2*)((byte*)chunk + match.Offset2),
                        chunk->Count);
                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                }
            }
        }

        internal void ForEachChunkReadOnly<T1, T2>(int queryId, ChunkAction<T1, T2> action)
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

        internal void ForEachChangedChunk<T1, T2, TChanged>(int queryId, int sinceVersion, ChunkAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    action(
                        GetEntityArray(chunk, archetype),
                        (T1*)((byte*)chunk + match.Offset1),
                        (T2*)((byte*)chunk + match.Offset2),
                        chunk->Count);
                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                }
            }
        }

        internal void ForEachChangedChunkReadOnly<T1, T2, TChanged>(int queryId, int sinceVersion, ChunkAction<T1, T2> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
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
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();

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
                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                    MarkComponentChanged(chunk, archetype, t3);
                }
            }
        }

        internal void ForEachEnabledChunk<T1, T2, T3, TEnabled>(int queryId, EnabledChunkAction<T1, T2, T3> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
            where TEnabled : unmanaged, IComponentData
        {
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();
            ComponentType enabledType = TypeRegistry.Get<TEnabled>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0)
                        continue;

                    action(
                        GetEnabledChunk(chunk, archetype, enabledType, GetEnabledSlot(enabledType, t1, match.Slot1, t2, match.Slot2, t3, match.Slot3)),
                        GetEntityArray(chunk, archetype),
                        (T1*)((byte*)chunk + match.Offset1),
                        (T2*)((byte*)chunk + match.Offset2),
                        (T3*)((byte*)chunk + match.Offset3),
                        chunk->Count);
                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                    MarkComponentChanged(chunk, archetype, t3);
                }
            }
        }

        internal void ForEachChunkReadOnly<T1, T2, T3>(int queryId, ChunkAction<T1, T2, T3> action)
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

        internal void ForEachChangedChunk<T1, T2, T3, TChanged>(int queryId, int sinceVersion, ChunkAction<T1, T2, T3> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();
            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
                        continue;

                    action(
                        GetEntityArray(chunk, archetype),
                        (T1*)((byte*)chunk + match.Offset1),
                        (T2*)((byte*)chunk + match.Offset2),
                        (T3*)((byte*)chunk + match.Offset3),
                        chunk->Count);
                    MarkComponentChanged(chunk, archetype, t1);
                    MarkComponentChanged(chunk, archetype, t2);
                    MarkComponentChanged(chunk, archetype, t3);
                }
            }
        }

        internal void ForEachChangedChunkReadOnly<T1, T2, T3, TChanged>(int queryId, int sinceVersion, ChunkAction<T1, T2, T3> action)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
            where TChanged : unmanaged, IComponentData
        {
            ComponentType changedType = TypeRegistry.Get<TChanged>();

            foreach (QueryArchetypeMatch match in _queryCache.GetMatchingArchetypes(queryId))
            {
                Archetype archetype = _archetypes.GetById(match.ArchetypeId);

                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    if (chunk->Count == 0 || !ChunkChangedSince(chunk, archetype, changedType, sinceVersion))
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

        private static bool ChunkChangedSince(Chunk* chunk, Archetype archetype, ComponentType changedType, int sinceVersion)
        {
            if (changedType.IsTag || chunk->ChangeVersions == null)
                return false;

            int changedSlot = archetype.GetTypeSlot(changedType.Index);
            return changedSlot >= 0 && chunk->ChangeVersions[changedSlot] > sinceVersion;
        }

        private static ComponentType GetWriteType<TWrite>(ComponentType t1)
            where TWrite : unmanaged, IComponentData
        {
            ComponentType writeType = TypeRegistry.Get<TWrite>();
            if (writeType.Index == t1.Index)
                return writeType;

            throw new InvalidOperationException($"Write component {writeType.ManagedType.Name} is not part of this query.");
        }

        private static ComponentType GetWriteType<TWrite>(ComponentType t1, ComponentType t2)
            where TWrite : unmanaged, IComponentData
        {
            ComponentType writeType = TypeRegistry.Get<TWrite>();
            if (writeType.Index == t1.Index || writeType.Index == t2.Index)
                return writeType;

            throw new InvalidOperationException($"Write component {writeType.ManagedType.Name} is not part of this query.");
        }

        private static ComponentType GetWriteType<TWrite>(ComponentType t1, ComponentType t2, ComponentType t3)
            where TWrite : unmanaged, IComponentData
        {
            ComponentType writeType = TypeRegistry.Get<TWrite>();
            if (writeType.Index == t1.Index || writeType.Index == t2.Index || writeType.Index == t3.Index)
                return writeType;

            throw new InvalidOperationException($"Write component {writeType.ManagedType.Name} is not part of this query.");
        }

        private static bool IsSlotEnabledForQuery(QueryArchetypeMatch match, Chunk* chunk, Archetype archetype, int slot)
        {
            return IsComponentSlotEnabled(match.Slot1, chunk, archetype, slot) &&
                   IsComponentSlotEnabled(match.Slot2, chunk, archetype, slot) &&
                   IsComponentSlotEnabled(match.Slot3, chunk, archetype, slot);
        }

        private static bool IsComponentSlotEnabled(int typeSlot, Chunk* chunk, Archetype archetype, int entitySlot)
        {
            if (typeSlot < 0)
                return true;

            ComponentType type = archetype.Types[typeSlot];
            if (!type.IsEnableable)
                return true;

            byte* mask = GetEnabledMask(chunk, archetype, typeSlot);
            return mask == null || GetEnabledBit(mask, entitySlot);
        }

        private static int GetEnabledSlot(ComponentType enabledType, ComponentType t1, int slot1)
        {
            if (enabledType.Index == t1.Index)
                return slot1;

            return ArchetypeLayout.MissingOffset;
        }

        private static int GetEnabledSlot(ComponentType enabledType, ComponentType t1, int slot1, ComponentType t2, int slot2)
        {
            if (enabledType.Index == t1.Index)
                return slot1;
            if (enabledType.Index == t2.Index)
                return slot2;

            return ArchetypeLayout.MissingOffset;
        }

        private static int GetEnabledSlot(ComponentType enabledType, ComponentType t1, int slot1, ComponentType t2, int slot2, ComponentType t3, int slot3)
        {
            if (enabledType.Index == t1.Index)
                return slot1;
            if (enabledType.Index == t2.Index)
                return slot2;
            if (enabledType.Index == t3.Index)
                return slot3;

            return ArchetypeLayout.MissingOffset;
        }

        private static EnabledChunk GetEnabledChunk(Chunk* chunk, Archetype archetype, ComponentType enabledType, int typeSlot)
        {
            if (typeSlot < 0)
                throw new InvalidOperationException($"Archetype {archetype.Id} does not contain component {enabledType.ManagedType.Name}.");

            if (!enabledType.IsEnableable)
                return new EnabledChunk(null, chunk->Count);

            return new EnabledChunk(GetEnabledMask(chunk, archetype, typeSlot), chunk->Count);
        }

        private static Entity* GetEntityArray(Chunk* chunk, Archetype archetype)
        {
            return (Entity*)((byte*)chunk + archetype.Layout.EntityOffset);
        }
    }
}
