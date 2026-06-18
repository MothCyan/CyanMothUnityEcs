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
        private bool _disposed;

        public World()
        {
            _entities = new EntityStore();
            _archetypes = new ArchetypeStore();
            _chunks = new ChunkAllocator();
        }

        public int CreatedEntityCapacity => _entities.CreatedCapacity;
        public int ArchetypeCount => _archetypes.Count;

        public bool IsAlive(Entity entity)
        {
            ThrowIfDisposed();
            return _entities.IsAlive(entity);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _chunks.Dispose();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(World));
        }
    }
}
