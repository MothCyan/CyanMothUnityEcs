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
    }
}
