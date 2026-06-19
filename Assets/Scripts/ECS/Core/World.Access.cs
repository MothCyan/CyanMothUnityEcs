using System;

namespace CyanMothUnityEcs
{
    public unsafe sealed partial class World
    {
        public bool Has<T>(Entity entity)
            where T : unmanaged, IComponentData
        {
            ThrowIfDisposed();
            _entities.Validate(entity);

            ComponentType type = TypeRegistry.Get<T>();
            Archetype archetype = _archetypes.GetById(_entities.GetArchetypeId(entity));
            return archetype.Has(type);
        }

        public ref T Get<T>(Entity entity)
            where T : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType type = TypeRegistry.Get<T>();
            if (type.IsTag)
                throw new InvalidOperationException($"Tag 组件 {type.ManagedType.Name} 没有可返回的数据引用。");

            Chunk* chunk = GetEntityChunk(entity, out int slot, out Archetype archetype);
            int offset = archetype.GetComponentOffset(type.Index);
            int stride = archetype.GetComponentStride(type.Index);
            return ref *(T*)((byte*)chunk + offset + stride * slot);
        }

        public void Set<T>(Entity entity, T component)
            where T : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType type = TypeRegistry.Get<T>();
            if (type.IsTag)
                return;

            Chunk* chunk = GetEntityChunk(entity, out int slot, out Archetype archetype);
            WriteComponent(chunk, archetype, slot, type, component);
        }

        public bool IsComponentEnabled<T>(Entity entity)
            where T : unmanaged, IEnableableComponent
        {
            ThrowIfDisposed();

            ComponentType type = TypeRegistry.Get<T>();
            Chunk* chunk = GetEntityChunk(entity, out int slot, out Archetype archetype);
            int typeSlot = archetype.GetTypeSlot(type.Index);
            if (typeSlot < 0)
                throw new InvalidOperationException($"实体不包含组件 {type.ManagedType.Name}。");

            byte* mask = GetEnabledMask(chunk, archetype, typeSlot);
            return mask == null || GetEnabledBit(mask, slot);
        }

        public void SetComponentEnabled<T>(Entity entity, bool enabled)
            where T : unmanaged, IEnableableComponent
        {
            ThrowIfDisposed();

            ComponentType type = TypeRegistry.Get<T>();
            Chunk* chunk = GetEntityChunk(entity, out int slot, out Archetype archetype);
            int typeSlot = archetype.GetTypeSlot(type.Index);
            if (typeSlot < 0)
                throw new InvalidOperationException($"实体不包含组件 {type.ManagedType.Name}。");

            byte* mask = GetEnabledMask(chunk, archetype, typeSlot);
            if (mask == null)
                return;

            SetEnabledBit(mask, slot, enabled);
            MarkComponentChanged(chunk, archetype, type);
        }

        private Chunk* GetEntityChunk(Entity entity, out int slot, out Archetype archetype)
        {
            _entities.Validate(entity);

            Chunk* chunk = (Chunk*)_entities.GetChunk(entity);
            slot = _entities.GetIndex(entity);
            archetype = _archetypes.GetById(_entities.GetArchetypeId(entity));

            if (slot < 0 || slot >= chunk->Count)
                throw new InvalidOperationException($"{entity} 的 Chunk slot 已经超出当前 Chunk 范围。");

            return chunk;
        }
    }
}
