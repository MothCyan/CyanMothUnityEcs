namespace CyanMothUnityEcs
{
    /// <summary>
    /// World 的只读调试快照。
    /// 它只保存统计结果，不持有 Chunk 指针，因此可以安全显示在 UI 或日志中。
    /// </summary>
    public readonly struct WorldStats
    {
        public readonly int AliveEntityCount;
        public readonly int CreatedEntityCapacity;
        public readonly int ArchetypeCount;
        public readonly int ChunkCount;
        public readonly int ReservedChunkCount;
        public readonly int TotalChunkCapacity;
        public readonly int CommandCount;
        public readonly float ChunkUtilization;

        public WorldStats(
            int aliveEntityCount,
            int createdEntityCapacity,
            int archetypeCount,
            int chunkCount,
            int reservedChunkCount,
            int totalChunkCapacity,
            int commandCount,
            float chunkUtilization)
        {
            AliveEntityCount = aliveEntityCount;
            CreatedEntityCapacity = createdEntityCapacity;
            ArchetypeCount = archetypeCount;
            ChunkCount = chunkCount;
            ReservedChunkCount = reservedChunkCount;
            TotalChunkCapacity = totalChunkCapacity;
            CommandCount = commandCount;
            ChunkUtilization = chunkUtilization;
        }

        public override string ToString()
        {
            return $"Entities={AliveEntityCount}, Archetypes={ArchetypeCount}, Chunks={ChunkCount}, Commands={CommandCount}, Utilization={ChunkUtilization:0.00%}";
        }
    }
}
