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

            _world.ForEach(_queryId, action);
        }

        public void ForEachReadOnly(ReadOnlyQueryAction<T1> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachReadOnly(_queryId, action);
        }

        public void ForEachChunk(ChunkAction<T1> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChunk(_queryId, action);
        }

        public void ForEachEnabledChunk<TEnabled>(EnabledChunkAction<T1> action)
            where TEnabled : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachEnabledChunk<T1, TEnabled>(_queryId, action);
        }

        public void ForEachChanged<TChanged>(int sinceVersion, QueryAction<T1> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChanged<T1, TChanged>(_queryId, sinceVersion, action);
        }

        public void ForEachChangedReadOnly<TChanged>(int sinceVersion, ReadOnlyQueryAction<T1> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChangedReadOnly<T1, TChanged>(_queryId, sinceVersion, action);
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

            _world.ForEach(_queryId, action);
        }

        public void ForEachReadOnly(ReadOnlyQueryAction<T1, T2> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachReadOnly(_queryId, action);
        }

        public void ForEachChunk(ChunkAction<T1, T2> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChunk(_queryId, action);
        }

        public void ForEachEnabledChunk<TEnabled>(EnabledChunkAction<T1, T2> action)
            where TEnabled : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachEnabledChunk<T1, T2, TEnabled>(_queryId, action);
        }

        public void ForEachChanged<TChanged>(int sinceVersion, QueryAction<T1, T2> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChanged<T1, T2, TChanged>(_queryId, sinceVersion, action);
        }

        public void ForEachChangedReadOnly<TChanged>(int sinceVersion, ReadOnlyQueryAction<T1, T2> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChangedReadOnly<T1, T2, TChanged>(_queryId, sinceVersion, action);
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

            _world.ForEach(_queryId, action);
        }

        public void ForEachReadOnly(ReadOnlyQueryAction<T1, T2, T3> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachReadOnly(_queryId, action);
        }

        public void ForEachChunk(ChunkAction<T1, T2, T3> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChunk(_queryId, action);
        }

        public void ForEachEnabledChunk<TEnabled>(EnabledChunkAction<T1, T2, T3> action)
            where TEnabled : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachEnabledChunk<T1, T2, T3, TEnabled>(_queryId, action);
        }

        public void ForEachChanged<TChanged>(int sinceVersion, QueryAction<T1, T2, T3> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChanged<T1, T2, T3, TChanged>(_queryId, sinceVersion, action);
        }

        public void ForEachChangedReadOnly<TChanged>(int sinceVersion, ReadOnlyQueryAction<T1, T2, T3> action)
            where TChanged : unmanaged, IComponentData
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _world.ForEachChangedReadOnly<T1, T2, T3, TChanged>(_queryId, sinceVersion, action);
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
