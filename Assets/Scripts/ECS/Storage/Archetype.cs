using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// Archetype 表示一组完全相同的组件组合。
    /// 它保存组件列表、组件掩码、Chunk 布局以及后续结构变更需要的边缓存。
    /// </summary>
    internal unsafe sealed class Archetype
    {
        public readonly int Id;
        public readonly ComponentMask Mask;
        public readonly ComponentType[] Types;
        public readonly ArchetypeLayout Layout;
        public readonly int[] AddEdges;
        public readonly int[] RemoveEdges;

        public Chunk* FirstChunk;
        public Chunk* LastChunk;
        public Chunk* FirstFreeChunk;
        public int Version;

        public Archetype(int id, ComponentMask mask, ComponentType[] types, ArchetypeLayout layout)
        {
            if (id < 0)
                throw new ArgumentOutOfRangeException(nameof(id), id, "ArchetypeId 不能为负数。");

            Id = id;
            Mask = mask;
            Types = types ?? throw new ArgumentNullException(nameof(types));
            Layout = layout;
            AddEdges = CreateEmptyEdges();
            RemoveEdges = CreateEmptyEdges();
        }

        public bool Has(ComponentType type)
        {
            return Mask.Contains(type.Index);
        }

        public int GetTypeSlot(int typeIndex)
        {
            for (int i = 0; i < Types.Length; i++)
            {
                if (Types[i].Index == typeIndex)
                    return i;
            }

            return -1;
        }

        public int GetComponentOffset(int typeIndex)
        {
            int slot = GetTypeSlot(typeIndex);
            if (slot < 0)
                throw new InvalidOperationException($"Archetype {Id} 不包含组件 TypeIndex {typeIndex}。");

            return Layout.GetComponentOffset(slot);
        }

        public int GetComponentStride(int typeIndex)
        {
            int slot = GetTypeSlot(typeIndex);
            if (slot < 0)
                throw new InvalidOperationException($"Archetype {Id} 不包含组件 TypeIndex {typeIndex}。");

            return Layout.GetComponentStride(slot);
        }

        private static int[] CreateEmptyEdges()
        {
            int[] edges = new int[TypeRegistry.MaxComponentTypes];
            for (int i = 0; i < edges.Length; i++)
                edges[i] = -1;
            return edges;
        }
    }
}
