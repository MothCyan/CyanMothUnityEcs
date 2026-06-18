using System;

namespace CyanMothUnityEcs
{
    public unsafe sealed partial class World
    {
        /// <summary>
        /// 批量创建单组件实体，并把创建出的 Entity 写入输出数组。
        /// 这个 API 会复用同一个 Archetype 和 ComponentType，避免调用方重复写 Create 循环。
        /// </summary>
        public void CreateMany<T1>(T1[] c1, Entity[] entities)
            where T1 : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            ValidateCreateManyInputs(c1, entities, c1?.Length ?? 0);

            ComponentType t1 = TypeRegistry.Get<T1>();
            Archetype archetype = _archetypes.GetOrCreate(t1);

            for (int i = 0; i < c1.Length; i++)
            {
                Entity entity = AllocateEntity(archetype, out Chunk* chunk, out int slot);
                WriteComponent(chunk, archetype, slot, t1, c1[i]);
                entities[i] = entity;
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

            for (int i = 0; i < count; i++)
            {
                Entity entity = AllocateEntity(archetype, out Chunk* chunk, out int slot);
                WriteComponent(chunk, archetype, slot, t1, c1[i]);
                WriteComponent(chunk, archetype, slot, t2, c2[i]);
                entities[i] = entity;
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

            for (int i = 0; i < count; i++)
            {
                Entity entity = AllocateEntity(archetype, out Chunk* chunk, out int slot);
                WriteComponent(chunk, archetype, slot, t1, c1[i]);
                WriteComponent(chunk, archetype, slot, t2, c2[i]);
                WriteComponent(chunk, archetype, slot, t3, c3[i]);
                entities[i] = entity;
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
    }
}
