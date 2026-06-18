using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// Archetype 表示一组完全相同的组件组合。
    /// 它保存组件列表、组件掩码、Chunk 布局，以及结构变更需要的边缓存。
    /// </summary>
    internal unsafe sealed class Archetype
    {
        public readonly int Id;
        public readonly ComponentMask Mask;
        public readonly ComponentType[] Types;
        public readonly ArchetypeLayout Layout;
        public readonly int[] AddEdges;
        public readonly int[] RemoveEdges;

        private readonly int[] _typeSlots;
        private readonly int[] _componentOffsets;
        private readonly int[] _componentStrides;

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
            _typeSlots = CreateFilledArray(ArchetypeLayout.MissingOffset);
            _componentOffsets = CreateFilledArray(ArchetypeLayout.MissingOffset);
            _componentStrides = CreateFilledArray(0);
            BuildComponentLookup();
        }

        public bool Has(ComponentType type)
        {
            return Mask.Contains(type.Index);
        }

        public int GetTypeSlot(int typeIndex)
        {
            ValidateTypeIndex(typeIndex);
            return _typeSlots[typeIndex];
        }

        public int GetComponentOffset(int typeIndex)
        {
            ValidateContains(typeIndex);
            return _componentOffsets[typeIndex];
        }

        public int GetComponentStride(int typeIndex)
        {
            ValidateContains(typeIndex);
            return _componentStrides[typeIndex];
        }

        private void BuildComponentLookup()
        {
            for (int slot = 0; slot < Types.Length; slot++)
            {
                ComponentType type = Types[slot];
                _typeSlots[type.Index] = slot;
                _componentOffsets[type.Index] = Layout.GetComponentOffset(slot);
                _componentStrides[type.Index] = Layout.GetComponentStride(slot);
            }
        }

        private void ValidateContains(int typeIndex)
        {
            ValidateTypeIndex(typeIndex);
            if (_typeSlots[typeIndex] < 0)
                throw new InvalidOperationException($"Archetype {Id} 不包含组件 TypeIndex {typeIndex}。");
        }

        private static void ValidateTypeIndex(int typeIndex)
        {
            if ((uint)typeIndex >= TypeRegistry.MaxComponentTypes)
                throw new ArgumentOutOfRangeException(nameof(typeIndex), typeIndex, "TypeIndex 超出范围。");
        }

        private static int[] CreateEmptyEdges()
        {
            return CreateFilledArray(-1);
        }

        private static int[] CreateFilledArray(int value)
        {
            int[] values = new int[TypeRegistry.MaxComponentTypes];
            for (int i = 0; i < values.Length; i++)
                values[i] = value;
            return values;
        }
    }
}
