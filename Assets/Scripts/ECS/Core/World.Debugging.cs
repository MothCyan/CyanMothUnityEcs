namespace CyanMothUnityEcs
{
    public unsafe sealed partial class World
    {
        /// <summary>
        /// 获取当前 World 的调试统计快照。
        /// 这个方法会遍历 Archetype 的 Chunk 链表，适合调试面板和 Benchmark，不建议放进热路径系统里每实体调用。
        /// </summary>
        public WorldStats GetStats()
        {
            ThrowIfDisposed();

            int totalCapacity = 0;
            int chunkCount = 0;

            for (int i = 0; i < _archetypes.Count; i++)
            {
                Archetype archetype = _archetypes.GetByIndex(i);
                for (Chunk* chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next)
                {
                    chunkCount++;
                    totalCapacity += chunk->Capacity;
                }
            }

            float utilization = totalCapacity == 0 ? 0 : (float)_entities.AliveCount / totalCapacity;
            return new WorldStats(
                _entities.AliveCount,
                _entities.CreatedCapacity,
                _archetypes.Count,
                chunkCount,
                _chunks.ReservedChunkCount,
                totalCapacity,
                _commands.Count,
                utilization);
        }

        /// <summary>
        /// 获取实体所在 Chunk 上指定组件的 ChangeVersion。
        /// 这个 API 面向调试和测试，后续 Query change filter 会复用同一份底层数据。
        /// </summary>
        public int GetChangeVersion<T>(Entity entity)
            where T : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType type = TypeRegistry.Get<T>();
            Chunk* chunk = GetEntityChunk(entity, out _, out Archetype archetype);
            int slot = archetype.GetTypeSlot(type.Index);
            if (slot < 0)
                throw new InvalidOperationException($"实体不包含组件 {type.ManagedType.Name}。");

            if (chunk->ChangeVersions == null)
                return 0;

            return chunk->ChangeVersions[slot];
        }
    }
}
