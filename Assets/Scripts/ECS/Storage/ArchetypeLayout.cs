using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 描述一个 Archetype 的数据在 Chunk 内部如何排布。
    /// 它只负责计算偏移和容量，不拥有真实内存。
    /// </summary>
    internal readonly struct ArchetypeLayout
    {
        public const int MissingOffset = -1;

        public readonly int ChunkSize;
        public readonly int HeaderSize;
        public readonly int ChangeVersionOffset;
        public readonly int ChangeVersionStride;
        public readonly int EntityOffset;
        public readonly int EntityStride;
        public readonly int Capacity;
        public readonly int UsedBytes;
        public readonly int[] ComponentOffsets;
        public readonly int[] ComponentStrides;
        public readonly int[] EnabledMaskOffsets;
        public readonly int EnabledMaskStride;

        private ArchetypeLayout(
            int chunkSize,
            int headerSize,
            int changeVersionOffset,
            int changeVersionStride,
            int entityOffset,
            int entityStride,
            int capacity,
            int usedBytes,
            int[] componentOffsets,
            int[] componentStrides,
            int[] enabledMaskOffsets,
            int enabledMaskStride)
        {
            ChunkSize = chunkSize;
            HeaderSize = headerSize;
            ChangeVersionOffset = changeVersionOffset;
            ChangeVersionStride = changeVersionStride;
            EntityOffset = entityOffset;
            EntityStride = entityStride;
            Capacity = capacity;
            UsedBytes = usedBytes;
            ComponentOffsets = componentOffsets;
            ComponentStrides = componentStrides;
            EnabledMaskOffsets = enabledMaskOffsets;
            EnabledMaskStride = enabledMaskStride;
        }

        public static ArchetypeLayout Create(ComponentType[] types)
        {
            return Create(types, Chunk.Size, UnsafeUtil.Align(UnsafeUtil.SizeOf<Chunk>(), Chunk.Alignment));
        }

        public static ArchetypeLayout Create(ComponentType[] types, int chunkSize, int headerSize)
        {
            if (types == null)
                throw new ArgumentNullException(nameof(types));
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be greater than zero.");
            if (headerSize < 0 || headerSize >= chunkSize)
                throw new ArgumentOutOfRangeException(nameof(headerSize), headerSize, "Header size must be smaller than Chunk size.");

            int entityStride = UnsafeUtil.SizeOf<Entity>();
            int perEntityBytes = entityStride;

            for (int i = 0; i < types.Length; i++)
            {
                ComponentType type = types[i];
                if (!type.IsTag)
                    perEntityBytes += type.Size;
            }

            if (perEntityBytes <= 0)
                throw new InvalidOperationException("Each entity needs at least Entity handle storage.");

            int fixedBytes = UnsafeUtil.Align(headerSize, 4) + types.Length * UnsafeUtil.SizeOf<int>();
            int maxCapacity = Math.Max(1, (chunkSize - fixedBytes) / perEntityBytes);

            for (int capacity = maxCapacity; capacity >= 1; capacity--)
            {
                if (TryBuild(types, chunkSize, headerSize, entityStride, capacity, out ArchetypeLayout layout))
                    return layout;
            }

            throw new InvalidOperationException("Component set is too large to fit any entity into one Chunk.");
        }

        public int GetComponentOffset(int typeSlot)
        {
            if ((uint)typeSlot >= ComponentOffsets.Length)
                throw new ArgumentOutOfRangeException(nameof(typeSlot), typeSlot, "Component slot is outside layout range.");

            return ComponentOffsets[typeSlot];
        }

        public int GetComponentStride(int typeSlot)
        {
            if ((uint)typeSlot >= ComponentStrides.Length)
                throw new ArgumentOutOfRangeException(nameof(typeSlot), typeSlot, "Component slot is outside layout range.");

            return ComponentStrides[typeSlot];
        }

        public int GetEnabledMaskOffset(int typeSlot)
        {
            if ((uint)typeSlot >= EnabledMaskOffsets.Length)
                throw new ArgumentOutOfRangeException(nameof(typeSlot), typeSlot, "Component slot is outside layout range.");

            return EnabledMaskOffsets[typeSlot];
        }

        private static bool TryBuild(
            ComponentType[] types,
            int chunkSize,
            int headerSize,
            int entityStride,
            int capacity,
            out ArchetypeLayout layout)
        {
            int[] componentOffsets = new int[types.Length];
            int[] componentStrides = new int[types.Length];
            int[] enabledMaskOffsets = new int[types.Length];
            for (int i = 0; i < enabledMaskOffsets.Length; i++)
                enabledMaskOffsets[i] = MissingOffset;

            int offset = UnsafeUtil.Align(headerSize, 4);
            int changeVersionOffset = offset;
            int changeVersionStride = UnsafeUtil.SizeOf<int>();
            offset += changeVersionStride * types.Length;

            int enabledMaskStride = UnsafeUtil.Align((capacity + 7) / 8, 4);
            for (int i = 0; i < types.Length; i++)
            {
                if (!types[i].IsEnableable)
                    continue;

                enabledMaskOffsets[i] = offset;
                offset += enabledMaskStride;
            }

            offset = UnsafeUtil.Align(offset, 8);
            int entityOffset = offset;
            offset += entityStride * capacity;

            for (int i = 0; i < types.Length; i++)
            {
                ComponentType type = types[i];
                if (type.IsTag)
                {
                    componentOffsets[i] = MissingOffset;
                    componentStrides[i] = 0;
                    continue;
                }

                offset = UnsafeUtil.Align(offset, type.Align);
                componentOffsets[i] = offset;
                componentStrides[i] = type.Size;
                offset += type.Size * capacity;
            }

            int usedBytes = UnsafeUtil.Align(offset, Chunk.Alignment);
            if (usedBytes > chunkSize)
            {
                layout = default;
                return false;
            }

            layout = new ArchetypeLayout(
                chunkSize,
                headerSize,
                changeVersionOffset,
                changeVersionStride,
                entityOffset,
                entityStride,
                capacity,
                usedBytes,
                componentOffsets,
                componentStrides,
                enabledMaskOffsets,
                enabledMaskStride);
            return true;
        }
    }
}
