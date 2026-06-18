using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// ECS 世界的统一入口。
    /// World 持有实体、Archetype 和 Chunk 分配器，并把上层 API 转成底层连续内存读写。
    /// </summary>
    public unsafe sealed partial class World : IDisposable
    {
        private readonly EntityStore _entities;
        private readonly ArchetypeStore _archetypes;
        private readonly ChunkAllocator _chunks;
        private readonly CommandBuffer _commands;
        private readonly QueryCache _queryCache;
        private bool _disposed;

        public World()
        {
            _entities = new EntityStore();
            _archetypes = new ArchetypeStore();
            _chunks = new ChunkAllocator();
            _commands = new CommandBuffer();
            _queryCache = new QueryCache(_archetypes);
        }

        public int CreatedEntityCapacity => _entities.CreatedCapacity;
        public int ArchetypeCount => _archetypes.Count;
        public CommandBuffer Commands => _commands;

        public bool IsAlive(Entity entity)
        {
            ThrowIfDisposed();
            return _entities.IsAlive(entity);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _commands.Clear();
            _chunks.Dispose();
            _disposed = true;
        }

        public void Playback()
        {
            ThrowIfDisposed();
            _commands.Playback(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(World));
        }
    }
}
