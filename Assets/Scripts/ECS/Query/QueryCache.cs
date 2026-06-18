using System;
using System.Collections.Generic;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// Query 到 Archetype 的匹配缓存。
    /// ArchetypeStore 版本变化时，缓存会重新扫描 Archetype 列表。
    /// </summary>
    internal sealed class QueryCache
    {
        private readonly ArchetypeStore _archetypes;
        private readonly List<QueryRecord> _records = new List<QueryRecord>();

        public QueryCache(ArchetypeStore archetypes)
        {
            _archetypes = archetypes ?? throw new ArgumentNullException(nameof(archetypes));
        }

        public int GetOrCreate(ComponentMask include, ComponentMask exclude)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                QueryRecord record = _records[i];
                if (record.Include == include && record.Exclude == exclude)
                    return i;
            }

            QueryRecord created = new QueryRecord(include, exclude);
            _records.Add(created);
            int queryId = _records.Count - 1;
            Refresh(queryId);
            return queryId;
        }

        public int[] GetMatchingArchetypes(int queryId)
        {
            if ((uint)queryId >= _records.Count)
                throw new ArgumentOutOfRangeException(nameof(queryId), queryId, "QueryId 超出范围。");

            QueryRecord record = _records[queryId];
            if (record.CachedArchetypeVersion != _archetypes.Version)
                Refresh(queryId);

            return _records[queryId].ArchetypeIds;
        }

        private void Refresh(int queryId)
        {
            QueryRecord record = _records[queryId];
            List<int> ids = new List<int>();

            for (int i = 0; i < _archetypes.Count; i++)
            {
                Archetype archetype = _archetypes.GetByIndex(i);
                if (!archetype.Mask.ContainsAll(record.Include))
                    continue;
                if (!record.Exclude.IsEmpty && archetype.Mask.Intersects(record.Exclude))
                    continue;

                ids.Add(archetype.Id);
            }

            record.ArchetypeIds = ids.ToArray();
            record.CachedArchetypeVersion = _archetypes.Version;
            _records[queryId] = record;
        }

        private struct QueryRecord
        {
            public readonly ComponentMask Include;
            public readonly ComponentMask Exclude;
            public int CachedArchetypeVersion;
            public int[] ArchetypeIds;

            public QueryRecord(ComponentMask include, ComponentMask exclude)
            {
                Include = include;
                Exclude = exclude;
                CachedArchetypeVersion = -1;
                ArchetypeIds = Array.Empty<int>();
            }
        }
    }
}
