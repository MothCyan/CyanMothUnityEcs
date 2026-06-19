using System;
using System.Collections.Generic;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// Query 命中的单个 Archetype 信息。
    /// 除了 ArchetypeId，还缓存本次 Query 需要的组件偏移，避免每帧遍历时重复查表。
    /// </summary>
    internal readonly struct QueryArchetypeMatch
    {
        public readonly int ArchetypeId;
        public readonly int Offset1;
        public readonly int Offset2;
        public readonly int Offset3;
        public readonly int Slot1;
        public readonly int Slot2;
        public readonly int Slot3;

        public QueryArchetypeMatch(int archetypeId, int offset1, int offset2, int offset3, int slot1, int slot2, int slot3)
        {
            ArchetypeId = archetypeId;
            Offset1 = offset1;
            Offset2 = offset2;
            Offset3 = offset3;
            Slot1 = slot1;
            Slot2 = slot2;
            Slot3 = slot3;
        }
    }

    /// <summary>
    /// Query 到 Archetype 的匹配缓存。
    /// 缓存内容包含命中的 ArchetypeId，以及该 Query 所需组件在 Chunk 内的偏移。
    /// </summary>
    internal sealed class QueryCache
    {
        private readonly ArchetypeStore _archetypes;
        private readonly List<QueryRecord> _records = new List<QueryRecord>();

        public QueryCache(ArchetypeStore archetypes)
        {
            _archetypes = archetypes ?? throw new ArgumentNullException(nameof(archetypes));
        }

        public int GetOrCreate(ComponentMask include, ComponentMask exclude, params int[] componentTypeIndices)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                QueryRecord record = _records[i];
                if (record.Include == include &&
                    record.Exclude == exclude &&
                    SameComponentTypes(record.ComponentTypeIndices, componentTypeIndices))
                    return i;
            }

            QueryRecord created = new QueryRecord(include, exclude, componentTypeIndices);
            _records.Add(created);
            int queryId = _records.Count - 1;
            Refresh(queryId);
            return queryId;
        }

        public QueryArchetypeMatch[] GetMatchingArchetypes(int queryId)
        {
            if ((uint)queryId >= _records.Count)
                throw new ArgumentOutOfRangeException(nameof(queryId), queryId, "QueryId 超出范围。");

            QueryRecord record = _records[queryId];
            if (record.CachedArchetypeVersion != _archetypes.Version)
                Refresh(queryId);

            return _records[queryId].Matches;
        }

        private void Refresh(int queryId)
        {
            QueryRecord record = _records[queryId];
            List<QueryArchetypeMatch> matches = new List<QueryArchetypeMatch>();

            for (int i = 0; i < _archetypes.Count; i++)
            {
                Archetype archetype = _archetypes.GetByIndex(i);
                if (!archetype.Mask.ContainsAll(record.Include))
                    continue;
                if (!record.Exclude.IsEmpty && archetype.Mask.Intersects(record.Exclude))
                    continue;

                matches.Add(new QueryArchetypeMatch(
                    archetype.Id,
                    GetOffset(archetype, record.ComponentTypeIndices, 0),
                    GetOffset(archetype, record.ComponentTypeIndices, 1),
                    GetOffset(archetype, record.ComponentTypeIndices, 2),
                    GetSlot(archetype, record.ComponentTypeIndices, 0),
                    GetSlot(archetype, record.ComponentTypeIndices, 1),
                    GetSlot(archetype, record.ComponentTypeIndices, 2)));
            }

            record.Matches = matches.ToArray();
            record.CachedArchetypeVersion = _archetypes.Version;
            _records[queryId] = record;
        }

        private static int GetOffset(Archetype archetype, int[] componentTypeIndices, int index)
        {
            if (index >= componentTypeIndices.Length)
                return ArchetypeLayout.MissingOffset;

            return archetype.GetComponentOffset(componentTypeIndices[index]);
        }

        private static int GetSlot(Archetype archetype, int[] componentTypeIndices, int index)
        {
            if (index >= componentTypeIndices.Length)
                return ArchetypeLayout.MissingOffset;

            return archetype.GetTypeSlot(componentTypeIndices[index]);
        }

        private static bool SameComponentTypes(int[] left, int[] right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private struct QueryRecord
        {
            public readonly ComponentMask Include;
            public readonly ComponentMask Exclude;
            public readonly int[] ComponentTypeIndices;
            public int CachedArchetypeVersion;
            public QueryArchetypeMatch[] Matches;

            public QueryRecord(ComponentMask include, ComponentMask exclude, int[] componentTypeIndices)
            {
                Include = include;
                Exclude = exclude;
                ComponentTypeIndices = componentTypeIndices ?? Array.Empty<int>();
                CachedArchetypeVersion = -1;
                Matches = Array.Empty<QueryArchetypeMatch>();
            }
        }
    }
}
