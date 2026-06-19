using System;

namespace CyanMothUnityEcs
{
    public readonly unsafe struct Query<T1>
        where T1 : unmanaged, IComponentData
    {
        private readonly World _world;
        private readonly int _queryId;

        internal Query(World world, int queryId)
        {
            _world = world;
            _queryId = queryId;
        }

        public void ForEach(QueryAction<T1> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ForEachChunk((Entity* entities, T1* c1, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], ref c1[i]);
            });
        }

        public void ForEachReadOnly(ReadOnlyQueryAction<T1> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChunkReadOnly<T1>(_queryId, (Entity* entities, T1* c1, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], in c1[i]);
            });
        }

        public void ForEachChunk(ChunkAction<T1> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChunk(_queryId, action);
        }

        public void ForEachChanged<TChanged>(int sinceVersion, QueryAction<T1> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ForEachChangedChunk<TChanged>(sinceVersion, (Entity* entities, T1* c1, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], ref c1[i]);
            });
        }

        public void ForEachChangedReadOnly<TChanged>(int sinceVersion, ReadOnlyQueryAction<T1> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChangedChunkReadOnly<T1, TChanged>(_queryId, sinceVersion, (Entity* entities, T1* c1, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], in c1[i]);
            });
        }

        public void ForEachChangedChunk<TChanged>(int sinceVersion, ChunkAction<T1> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChangedChunk<T1, TChanged>(_queryId, sinceVersion, action);
        }
    }

    public readonly unsafe struct Query<T1, T2>
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
    {
        private readonly World _world;
        private readonly int _queryId;

        internal Query(World world, int queryId)
        {
            _world = world;
            _queryId = queryId;
        }

        public void ForEach(QueryAction<T1, T2> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ForEachChunk((Entity* entities, T1* c1, T2* c2, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], ref c1[i], ref c2[i]);
            });
        }

        public void ForEachReadOnly(ReadOnlyQueryAction<T1, T2> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChunkReadOnly<T1, T2>(_queryId, (Entity* entities, T1* c1, T2* c2, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], in c1[i], in c2[i]);
            });
        }

        public void ForEachChunk(ChunkAction<T1, T2> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChunk(_queryId, action);
        }

        public void ForEachChanged<TChanged>(int sinceVersion, QueryAction<T1, T2> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ForEachChangedChunk<TChanged>(sinceVersion, (Entity* entities, T1* c1, T2* c2, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], ref c1[i], ref c2[i]);
            });
        }

        public void ForEachChangedReadOnly<TChanged>(int sinceVersion, ReadOnlyQueryAction<T1, T2> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChangedChunkReadOnly<T1, T2, TChanged>(_queryId, sinceVersion, (Entity* entities, T1* c1, T2* c2, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], in c1[i], in c2[i]);
            });
        }

        public void ForEachChangedChunk<TChanged>(int sinceVersion, ChunkAction<T1, T2> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChangedChunk<T1, T2, TChanged>(_queryId, sinceVersion, action);
        }
    }

    public readonly unsafe struct Query<T1, T2, T3>
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
        where T3 : unmanaged, IComponentData
    {
        private readonly World _world;
        private readonly int _queryId;

        internal Query(World world, int queryId)
        {
            _world = world;
            _queryId = queryId;
        }

        public void ForEach(QueryAction<T1, T2, T3> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ForEachChunk((Entity* entities, T1* c1, T2* c2, T3* c3, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], ref c1[i], ref c2[i], ref c3[i]);
            });
        }

        public void ForEachReadOnly(ReadOnlyQueryAction<T1, T2, T3> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChunkReadOnly<T1, T2, T3>(_queryId, (Entity* entities, T1* c1, T2* c2, T3* c3, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], in c1[i], in c2[i], in c3[i]);
            });
        }

        public void ForEachChunk(ChunkAction<T1, T2, T3> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChunk(_queryId, action);
        }

        public void ForEachChanged<TChanged>(int sinceVersion, QueryAction<T1, T2, T3> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ForEachChangedChunk<TChanged>(sinceVersion, (Entity* entities, T1* c1, T2* c2, T3* c3, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], ref c1[i], ref c2[i], ref c3[i]);
            });
        }

        public void ForEachChangedReadOnly<TChanged>(int sinceVersion, ReadOnlyQueryAction<T1, T2, T3> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChangedChunkReadOnly<T1, T2, T3, TChanged>(_queryId, sinceVersion, (Entity* entities, T1* c1, T2* c2, T3* c3, int count) =>
            {
                for (int i = 0; i < count; i++)
                    action(entities[i], in c1[i], in c2[i], in c3[i]);
            });
        }

        public void ForEachChangedChunk<TChanged>(int sinceVersion, ChunkAction<T1, T2, T3> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChangedChunk<T1, T2, T3, TChanged>(_queryId, sinceVersion, action);
        }
    }
}
