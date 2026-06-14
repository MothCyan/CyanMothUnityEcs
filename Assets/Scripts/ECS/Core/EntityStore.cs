using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 把对外的 Entity 句柄映射到当前存储位置。
    /// 它刻意不理解组件字节内容；后续存储层移动 Chunk 或槽位时，
    /// 只需要更新这张位置表。
    /// </summary>+
    public sealed class EntityStore
    {
        private const int NullEntityId = 0;
        private const int DefaultCapacity = 1024;

        private int[] _versions;
        private IntPtr[] _chunks;
        private int[] _indices;
        private int[] _archetypeIds;
        private int[] _freeIds;
        private int _freeCount;
        private int _nextId;

        public EntityStore(int initialCapacity = DefaultCapacity)
        {
            int capacity = Math.Max(initialCapacity, 2);
            _versions = new int[capacity];
            _chunks = new IntPtr[capacity];
            _indices = new int[capacity];
            _archetypeIds = new int[capacity];
            _freeIds = new int[capacity];
            _nextId = 1;
        }

        public int Capacity => _versions.Length;
        public int CreatedCapacity => _nextId;

        public Entity Create()
        {
            int id;

            if (_freeCount > 0)
            {
                id = _freeIds[--_freeCount];
            }
            else
            {
                id = _nextId++;
                EnsureEntityCapacity(id);
            }

            return new Entity(id, _versions[id]);
        }

        public bool IsAlive(Entity entity)
        {
            return IsValidId(entity.Id) &&
                   entity.Version == _versions[entity.Id] &&
                   _chunks[entity.Id] != IntPtr.Zero;
        }

        public void Validate(Entity entity)
        {
            if (entity.IsNull)
                throw new InvalidOperationException("Entity.Null is not a valid live entity.");

            if (!IsValidId(entity.Id))
                throw new InvalidOperationException($"Entity id {entity.Id} is outside the store range.");

            if (entity.Version != _versions[entity.Id])
                throw new InvalidOperationException($"{entity} is stale. Current version is {_versions[entity.Id]}.");

            if (_chunks[entity.Id] == IntPtr.Zero)
                throw new InvalidOperationException($"{entity} is not alive.");
        }

        public void Release(Entity entity)
        {
            Validate(entity);

            int id = entity.Id;
            _chunks[id] = IntPtr.Zero;
            _indices[id] = 0;
            _archetypeIds[id] = 0;
            _versions[id]++;
            PushFreeId(id);
        }

        public void SetLocation(Entity entity, IntPtr chunk, int index, int archetypeId)
        {
            if (chunk == IntPtr.Zero)
                throw new ArgumentException("Live entity location must point to a non-zero chunk.", nameof(chunk));
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Chunk index cannot be negative.");
            if (archetypeId < 0)
                throw new ArgumentOutOfRangeException(nameof(archetypeId), archetypeId, "Archetype id cannot be negative.");

            if (!IsValidId(entity.Id) || entity.Version != _versions[entity.Id])
                throw new InvalidOperationException($"{entity} is not a valid entity for this store.");

            _chunks[entity.Id] = chunk;
            _indices[entity.Id] = index;
            _archetypeIds[entity.Id] = archetypeId;
        }

        public IntPtr GetChunk(Entity entity)
        {
            Validate(entity);
            return _chunks[entity.Id];
        }

        public int GetIndex(Entity entity)
        {
            Validate(entity);
            return _indices[entity.Id];
        }

        public int GetArchetypeId(Entity entity)
        {
            Validate(entity);
            return _archetypeIds[entity.Id];
        }

        private bool IsValidId(int id)
        {
            return id > NullEntityId && id < _nextId && id < _versions.Length;
        }

        private void EnsureEntityCapacity(int id)
        {
            if (id < _versions.Length)
                return;

            int newCapacity = _versions.Length;
            while (id >= newCapacity)
                newCapacity *= 2;

            Array.Resize(ref _versions, newCapacity);
            Array.Resize(ref _chunks, newCapacity);
            Array.Resize(ref _indices, newCapacity);
            Array.Resize(ref _archetypeIds, newCapacity);
            Array.Resize(ref _freeIds, newCapacity);
        }

        private void PushFreeId(int id)
        {
            if (_freeCount == _freeIds.Length)
                Array.Resize(ref _freeIds, _freeIds.Length * 2);

            _freeIds[_freeCount++] = id;
        }
    }
}
