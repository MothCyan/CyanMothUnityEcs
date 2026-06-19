using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 单组件实体模板。
    /// 它缓存目标 Archetype、组件类型和默认组件值，适合反复创建同一种实体。
    /// </summary>
    public readonly struct ArchetypePrefab<T1>
        where T1 : unmanaged, IComponentData
    {
        internal readonly World Owner;
        internal readonly Archetype Archetype;
        internal readonly ComponentType Type1;
        internal readonly T1 Component1;

        internal ArchetypePrefab(World owner, Archetype archetype, ComponentType type1, T1 component1)
        {
            Owner = owner;
            Archetype = archetype;
            Type1 = type1;
            Component1 = component1;
        }
    }

    /// <summary>
    /// 双组件实体模板。
    /// </summary>
    public readonly struct ArchetypePrefab<T1, T2>
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
    {
        internal readonly World Owner;
        internal readonly Archetype Archetype;
        internal readonly ComponentType Type1;
        internal readonly ComponentType Type2;
        internal readonly T1 Component1;
        internal readonly T2 Component2;

        internal ArchetypePrefab(World owner, Archetype archetype, ComponentType type1, ComponentType type2, T1 component1, T2 component2)
        {
            Owner = owner;
            Archetype = archetype;
            Type1 = type1;
            Type2 = type2;
            Component1 = component1;
            Component2 = component2;
        }
    }

    /// <summary>
    /// 三组件实体模板。
    /// </summary>
    public readonly struct ArchetypePrefab<T1, T2, T3>
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
        where T3 : unmanaged, IComponentData
    {
        internal readonly World Owner;
        internal readonly Archetype Archetype;
        internal readonly ComponentType Type1;
        internal readonly ComponentType Type2;
        internal readonly ComponentType Type3;
        internal readonly T1 Component1;
        internal readonly T2 Component2;
        internal readonly T3 Component3;

        internal ArchetypePrefab(World owner, Archetype archetype, ComponentType type1, ComponentType type2, ComponentType type3, T1 component1, T2 component2, T3 component3)
        {
            Owner = owner;
            Archetype = archetype;
            Type1 = type1;
            Type2 = type2;
            Type3 = type3;
            Component1 = component1;
            Component2 = component2;
            Component3 = component3;
        }
    }

    public unsafe sealed partial class World
    {
        /// <summary>
        /// 创建单组件实体模板。
        /// 后续 Instantiate 会复用这里缓存的 Archetype 和 ComponentType。
        /// </summary>
        public ArchetypePrefab<T1> CreatePrefab<T1>(T1 c1)
            where T1 : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType t1 = TypeRegistry.Get<T1>();
            return new ArchetypePrefab<T1>(this, _archetypes.GetOrCreate(t1), t1, c1);
        }

        /// <summary>
        /// 创建双组件实体模板。
        /// </summary>
        public ArchetypePrefab<T1, T2> CreatePrefab<T1, T2>(T1 c1, T2 c2)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            return new ArchetypePrefab<T1, T2>(this, _archetypes.GetOrCreate(t1, t2), t1, t2, c1, c2);
        }

        /// <summary>
        /// 创建三组件实体模板。
        /// </summary>
        public ArchetypePrefab<T1, T2, T3> CreatePrefab<T1, T2, T3>(T1 c1, T2 c2, T3 c3)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();
            return new ArchetypePrefab<T1, T2, T3>(this, _archetypes.GetOrCreate(t1, t2, t3), t1, t2, t3, c1, c2, c3);
        }

        public Entity Instantiate<T1>(ArchetypePrefab<T1> prefab)
            where T1 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            ValidatePrefabOwner(prefab.Owner);

            Entity entity = AllocateEntity(prefab.Archetype, out Chunk* chunk, out int slot);
            WriteComponent(chunk, prefab.Archetype, slot, prefab.Type1, prefab.Component1);
            return entity;
        }

        public Entity Instantiate<T1, T2>(ArchetypePrefab<T1, T2> prefab)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            ValidatePrefabOwner(prefab.Owner);

            Entity entity = AllocateEntity(prefab.Archetype, out Chunk* chunk, out int slot);
            WriteComponent(chunk, prefab.Archetype, slot, prefab.Type1, prefab.Component1);
            WriteComponent(chunk, prefab.Archetype, slot, prefab.Type2, prefab.Component2);
            return entity;
        }

        public Entity Instantiate<T1, T2, T3>(ArchetypePrefab<T1, T2, T3> prefab)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            ValidatePrefabOwner(prefab.Owner);

            Entity entity = AllocateEntity(prefab.Archetype, out Chunk* chunk, out int slot);
            WriteComponent(chunk, prefab.Archetype, slot, prefab.Type1, prefab.Component1);
            WriteComponent(chunk, prefab.Archetype, slot, prefab.Type2, prefab.Component2);
            WriteComponent(chunk, prefab.Archetype, slot, prefab.Type3, prefab.Component3);
            return entity;
        }

        public void InstantiateMany<T1>(ArchetypePrefab<T1> prefab, int count, Entity[] entities)
            where T1 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            ValidatePrefabOwner(prefab.Owner);
            ValidateInstantiateManyInputs(count, entities);

            int written = 0;
            while (written < count)
            {
                AllocateEntityRange(prefab.Archetype, entities, written, count - written, out Chunk* chunk, out int slotStart, out int batchCount);
                WriteRepeatedComponentRange(chunk, prefab.Archetype, slotStart, prefab.Type1, prefab.Component1, batchCount);
                written += batchCount;
            }
        }

        public void InstantiateMany<T1, T2>(ArchetypePrefab<T1, T2> prefab, int count, Entity[] entities)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            ValidatePrefabOwner(prefab.Owner);
            ValidateInstantiateManyInputs(count, entities);

            int written = 0;
            while (written < count)
            {
                AllocateEntityRange(prefab.Archetype, entities, written, count - written, out Chunk* chunk, out int slotStart, out int batchCount);
                WriteRepeatedComponentRange(chunk, prefab.Archetype, slotStart, prefab.Type1, prefab.Component1, batchCount);
                WriteRepeatedComponentRange(chunk, prefab.Archetype, slotStart, prefab.Type2, prefab.Component2, batchCount);
                written += batchCount;
            }
        }

        public void InstantiateMany<T1, T2, T3>(ArchetypePrefab<T1, T2, T3> prefab, int count, Entity[] entities)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            ValidatePrefabOwner(prefab.Owner);
            ValidateInstantiateManyInputs(count, entities);

            int written = 0;
            while (written < count)
            {
                AllocateEntityRange(prefab.Archetype, entities, written, count - written, out Chunk* chunk, out int slotStart, out int batchCount);
                WriteRepeatedComponentRange(chunk, prefab.Archetype, slotStart, prefab.Type1, prefab.Component1, batchCount);
                WriteRepeatedComponentRange(chunk, prefab.Archetype, slotStart, prefab.Type2, prefab.Component2, batchCount);
                WriteRepeatedComponentRange(chunk, prefab.Archetype, slotStart, prefab.Type3, prefab.Component3, batchCount);
                written += batchCount;
            }
        }

        public Entity[] InstantiateMany<T1>(ArchetypePrefab<T1> prefab, int count)
            where T1 : unmanaged, IComponentData
        {
            ValidateInstantiateCount(count);

            Entity[] entities = new Entity[count];
            InstantiateMany(prefab, count, entities);
            return entities;
        }

        public Entity[] InstantiateMany<T1, T2>(ArchetypePrefab<T1, T2> prefab, int count)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ValidateInstantiateCount(count);

            Entity[] entities = new Entity[count];
            InstantiateMany(prefab, count, entities);
            return entities;
        }

        public Entity[] InstantiateMany<T1, T2, T3>(ArchetypePrefab<T1, T2, T3> prefab, int count)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ValidateInstantiateCount(count);

            Entity[] entities = new Entity[count];
            InstantiateMany(prefab, count, entities);
            return entities;
        }

        /// <summary>
        /// 批量创建单组件实体，并把创建出的 Entity 写入输出数组。
        /// 这个 API 会按 Chunk 连续填充，减少逐实体重复寻找可写 Chunk 的开销。
        /// </summary>
        public void CreateMany<T1>(T1[] c1, Entity[] entities)
            where T1 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            ValidateCreateManyInputs(c1, entities, c1?.Length ?? 0);

            ComponentType t1 = TypeRegistry.Get<T1>();
            Archetype archetype = _archetypes.GetOrCreate(t1);

            int written = 0;
            while (written < c1.Length)
            {
                AllocateEntityRange(archetype, entities, written, c1.Length - written, out Chunk* chunk, out int slotStart, out int batchCount);
                WriteComponentRange(chunk, archetype, slotStart, t1, c1, written, batchCount);
                written += batchCount;
            }
        }

        /// <summary>
        /// 批量创建双组件实体，并把创建出的 Entity 写入输出数组。
        /// </summary>
        public void CreateMany<T1, T2>(T1[] c1, T2[] c2, Entity[] entities)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            int count = c1?.Length ?? 0;
            ValidateCreateManyInputs(c1, entities, count);
            ValidateCreateManyInputs(c2, entities, count);

            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            Archetype archetype = _archetypes.GetOrCreate(t1, t2);

            int written = 0;
            while (written < count)
            {
                AllocateEntityRange(archetype, entities, written, count - written, out Chunk* chunk, out int slotStart, out int batchCount);
                WriteComponentRange(chunk, archetype, slotStart, t1, c1, written, batchCount);
                WriteComponentRange(chunk, archetype, slotStart, t2, c2, written, batchCount);
                written += batchCount;
            }
        }

        /// <summary>
        /// 批量创建三组件实体，并把创建出的 Entity 写入输出数组。
        /// </summary>
        public void CreateMany<T1, T2, T3>(T1[] c1, T2[] c2, T3[] c3, Entity[] entities)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            int count = c1?.Length ?? 0;
            ValidateCreateManyInputs(c1, entities, count);
            ValidateCreateManyInputs(c2, entities, count);
            ValidateCreateManyInputs(c3, entities, count);

            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();
            Archetype archetype = _archetypes.GetOrCreate(t1, t2, t3);

            int written = 0;
            while (written < count)
            {
                AllocateEntityRange(archetype, entities, written, count - written, out Chunk* chunk, out int slotStart, out int batchCount);
                WriteComponentRange(chunk, archetype, slotStart, t1, c1, written, batchCount);
                WriteComponentRange(chunk, archetype, slotStart, t2, c2, written, batchCount);
                WriteComponentRange(chunk, archetype, slotStart, t3, c3, written, batchCount);
                written += batchCount;
            }
        }

        /// <summary>
        /// 便捷版批量创建，会分配 Entity 输出数组。
        /// 热路径或大量数据建议使用带 entities 参数的非分配版本。
        /// </summary>
        public Entity[] CreateMany<T1>(T1[] c1)
            where T1 : unmanaged, IComponentData
        {
            Entity[] entities = new Entity[c1?.Length ?? 0];
            CreateMany(c1, entities);
            return entities;
        }

        /// <summary>
        /// 便捷版批量创建，会分配 Entity 输出数组。
        /// </summary>
        public Entity[] CreateMany<T1, T2>(T1[] c1, T2[] c2)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            Entity[] entities = new Entity[c1?.Length ?? 0];
            CreateMany(c1, c2, entities);
            return entities;
        }

        /// <summary>
        /// 便捷版批量创建，会分配 Entity 输出数组。
        /// </summary>
        public Entity[] CreateMany<T1, T2, T3>(T1[] c1, T2[] c2, T3[] c3)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            Entity[] entities = new Entity[c1?.Length ?? 0];
            CreateMany(c1, c2, c3, entities);
            return entities;
        }

        private void AllocateEntityRange(
            Archetype archetype,
            Entity[] entities,
            int entityStart,
            int requestedCount,
            out Chunk* chunk,
            out int slotStart,
            out int allocatedCount)
        {
            chunk = GetWritableChunk(archetype);
            slotStart = chunk->Count;
            allocatedCount = Math.Min(requestedCount, chunk->Capacity - chunk->Count);

            chunk->Count += allocatedCount;

            for (int i = 0; i < allocatedCount; i++)
            {
                int slot = slotStart + i;
                Entity entity = _entities.Create();
                entities[entityStart + i] = entity;
                WriteEntity(chunk, archetype, slot, entity);
                SetAllEnableableBits(chunk, archetype, slot, true);
                _entities.SetLocation(entity, new IntPtr(chunk), slot, archetype.Id);
            }

            if (chunk->Count == chunk->Capacity)
                RemoveFromFreeList(archetype, chunk);
        }

        private void WriteComponentRange<T>(
            Chunk* chunk,
            Archetype archetype,
            int slotStart,
            ComponentType type,
            T[] components,
            int componentStart,
            int count)
            where T : unmanaged, IComponentData
        {
            if (type.IsTag)
                return;

            int offset = archetype.GetComponentOffset(type.Index);
            int stride = archetype.GetComponentStride(type.Index);
            byte* targetBase = (byte*)chunk + offset + stride * slotStart;

            fixed (T* sourceBase = &components[componentStart])
            {
                for (int i = 0; i < count; i++)
                    UnsafeUtil.Copy(sourceBase + i, targetBase + stride * i, stride);
            }

            MarkComponentChanged(chunk, archetype, type);
        }

        private void WriteRepeatedComponentRange<T>(
            Chunk* chunk,
            Archetype archetype,
            int slotStart,
            ComponentType type,
            T component,
            int count)
            where T : unmanaged, IComponentData
        {
            if (type.IsTag)
                return;

            int offset = archetype.GetComponentOffset(type.Index);
            int stride = archetype.GetComponentStride(type.Index);
            byte* targetBase = (byte*)chunk + offset + stride * slotStart;

            for (int i = 0; i < count; i++)
                UnsafeUtil.Copy(&component, targetBase + stride * i, stride);

            MarkComponentChanged(chunk, archetype, type);
        }

        private static void ValidateCreateManyInputs<T>(T[] components, Entity[] entities, int count)
            where T : unmanaged, IComponentData
        {
            if (components == null)
                throw new ArgumentNullException(nameof(components));
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            if (components.Length != count)
                throw new ArgumentException("所有组件数组长度必须一致。", nameof(components));
            if (entities.Length < count)
                throw new ArgumentException("Entity 输出数组长度不能小于要创建的实体数量。", nameof(entities));
        }
        private void ValidatePrefabOwner(World owner)
        {
            if (!ReferenceEquals(owner, this))
                throw new InvalidOperationException("ArchetypePrefab belongs to another World.");
        }

        private static void ValidateInstantiateCount(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "创建数量不能为负数。");
        }

        private static void ValidateInstantiateManyInputs(int count, Entity[] entities)
        {
            ValidateInstantiateCount(count);
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            if (entities.Length < count)
                throw new ArgumentException("Entity 输出数组长度不能小于要创建的实体数量。", nameof(entities));
        }
    }
}
