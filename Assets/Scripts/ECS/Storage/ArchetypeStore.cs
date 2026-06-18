using System;
using System.Collections.Generic;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 管理所有 Archetype。
    /// 同一个组件组合只会创建一个 Archetype，后续 Query 和结构迁移都依赖这个稳定映射。
    /// </summary>
    internal sealed class ArchetypeStore
    {
        private readonly Dictionary<ComponentMask, int> _idsByMask = new Dictionary<ComponentMask, int>();
        private readonly List<Archetype> _archetypes = new List<Archetype>();

        public int Count => _archetypes.Count;
        public int Version { get; private set; }

        public Archetype GetOrCreate(params ComponentType[] types)
        {
            ComponentType[] normalizedTypes = NormalizeTypes(types);
            ComponentMask mask = BuildMask(normalizedTypes);
            return GetOrCreate(mask, normalizedTypes);
        }

        public Archetype GetOrCreate(ComponentMask mask)
        {
            ComponentType[] types = BuildTypesFromMask(mask);
            return GetOrCreate(mask, types);
        }

        private Archetype GetOrCreate(ComponentMask mask, ComponentType[] normalizedTypes)
        {
            if (normalizedTypes == null)
                throw new ArgumentNullException(nameof(normalizedTypes));

            if (_idsByMask.TryGetValue(mask, out int existingId))
                return _archetypes[existingId];

            int id = _archetypes.Count;
            ArchetypeLayout layout = ArchetypeLayout.Create(normalizedTypes);
            Archetype archetype = new Archetype(id, mask, normalizedTypes, layout);

            _idsByMask.Add(mask, id);
            _archetypes.Add(archetype);
            Version++;
            return archetype;
        }

        public Archetype GetById(int id)
        {
            if ((uint)id >= _archetypes.Count)
                throw new ArgumentOutOfRangeException(nameof(id), id, "ArchetypeId 超出范围。");

            return _archetypes[id];
        }

        public Archetype GetByIndex(int index)
        {
            return GetById(index);
        }

        public bool TryFind(ComponentMask mask, out Archetype archetype)
        {
            if (_idsByMask.TryGetValue(mask, out int id))
            {
                archetype = _archetypes[id];
                return true;
            }

            archetype = null;
            return false;
        }

        private static ComponentType[] NormalizeTypes(ComponentType[] types)
        {
            if (types == null)
                throw new ArgumentNullException(nameof(types));

            ComponentType[] normalized = new ComponentType[types.Length];
            Array.Copy(types, normalized, types.Length);
            Array.Sort(normalized, (left, right) => left.Index.CompareTo(right.Index));

            for (int i = 1; i < normalized.Length; i++)
            {
                if (normalized[i - 1].Index == normalized[i].Index)
                    throw new ArgumentException($"组件类型 {normalized[i].ManagedType.Name} 重复出现在同一个 Archetype 中。", nameof(types));
            }

            return normalized;
        }

        private static ComponentMask BuildMask(ComponentType[] types)
        {
            ComponentMask mask = ComponentMask.Empty;
            for (int i = 0; i < types.Length; i++)
                mask = mask.Add(types[i].Mask);
            return mask;
        }

        private static ComponentType[] BuildTypesFromMask(ComponentMask mask)
        {
            List<ComponentType> types = new List<ComponentType>();
            for (int i = 0; i < TypeRegistry.Count; i++)
            {
                ComponentType type = TypeRegistry.GetByIndex(i);
                if (mask.Contains(type.Index))
                    types.Add(type);
            }

            return types.ToArray();
        }
    }
}
