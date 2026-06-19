using System;

namespace CyanMothUnityEcs
{
    public unsafe sealed partial class World
    {
        public Entity Create<T1>(T1 c1)
            where T1 : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType t1 = TypeRegistry.Get<T1>();
            Archetype archetype = _archetypes.GetOrCreate(t1);
            Entity entity = AllocateEntity(archetype, out Chunk* chunk, out int slot);

            WriteComponent(chunk, archetype, slot, t1, c1);
            return entity;
        }

        public Entity Create<T1, T2>(T1 c1, T2 c2)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            Archetype archetype = _archetypes.GetOrCreate(t1, t2);
            Entity entity = AllocateEntity(archetype, out Chunk* chunk, out int slot);

            WriteComponent(chunk, archetype, slot, t1, c1);
            WriteComponent(chunk, archetype, slot, t2, c2);
            return entity;
        }

        public Entity Create<T1, T2, T3>(T1 c1, T2 c2, T3 c3)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType t1 = TypeRegistry.Get<T1>();
            ComponentType t2 = TypeRegistry.Get<T2>();
            ComponentType t3 = TypeRegistry.Get<T3>();
            Archetype archetype = _archetypes.GetOrCreate(t1, t2, t3);
            Entity entity = AllocateEntity(archetype, out Chunk* chunk, out int slot);

            WriteComponent(chunk, archetype, slot, t1, c1);
            WriteComponent(chunk, archetype, slot, t2, c2);
            WriteComponent(chunk, archetype, slot, t3, c3);
            return entity;
        }

        private Entity AllocateEntity(Archetype archetype, out Chunk* chunk, out int slot)
        {
            chunk = GetWritableChunk(archetype);
            slot = chunk->Count++;

            Entity entity = _entities.Create();
            WriteEntity(chunk, archetype, slot, entity);
            SetAllEnableableBits(chunk, archetype, slot, true);
            _entities.SetLocation(entity, new IntPtr(chunk), slot, archetype.Id);

            if (chunk->Count == chunk->Capacity)
                RemoveFromFreeList(archetype, chunk);

            return entity;
        }

        private Chunk* GetWritableChunk(Archetype archetype)
        {
            if (archetype.FirstFreeChunk != null)
                return archetype.FirstFreeChunk;

            Chunk* chunk = _chunks.Allocate(archetype.Id, archetype.Layout.Capacity);
            InitializeChunkForArchetype(chunk, archetype);
            LinkChunk(archetype, chunk);
            AddToFreeList(archetype, chunk);
            return chunk;
        }

        private static void InitializeChunkForArchetype(Chunk* chunk, Archetype archetype)
        {
            chunk->ChangeVersions = (int*)((byte*)chunk + archetype.Layout.ChangeVersionOffset);
            UnsafeUtil.Clear(chunk->ChangeVersions, archetype.Types.Length * archetype.Layout.ChangeVersionStride);
            InitializeEnabledMasks(chunk, archetype);
        }

        private static void InitializeEnabledMasks(Chunk* chunk, Archetype archetype)
        {
            for (int i = 0; i < archetype.Types.Length; i++)
            {
                if (!archetype.Types[i].IsEnableable)
                    continue;

                byte* mask = GetEnabledMask(chunk, archetype, i);
                UnsafeUtil.Clear(mask, archetype.Layout.EnabledMaskStride);
                for (int slot = 0; slot < archetype.Layout.Capacity; slot++)
                    SetEnabledBit(mask, slot, true);
            }
        }

        private static void LinkChunk(Archetype archetype, Chunk* chunk)
        {
            chunk->Prev = archetype.LastChunk;
            chunk->Next = null;

            if (archetype.LastChunk != null)
                archetype.LastChunk->Next = chunk;
            else
                archetype.FirstChunk = chunk;

            archetype.LastChunk = chunk;
        }

        private static void AddToFreeList(Archetype archetype, Chunk* chunk)
        {
            chunk->NextFree = archetype.FirstFreeChunk;
            archetype.FirstFreeChunk = chunk;
        }

        private static void RemoveFromFreeList(Archetype archetype, Chunk* chunk)
        {
            Chunk* previous = null;
            Chunk* current = archetype.FirstFreeChunk;

            while (current != null)
            {
                if (current == chunk)
                {
                    if (previous == null)
                        archetype.FirstFreeChunk = current->NextFree;
                    else
                        previous->NextFree = current->NextFree;

                    current->NextFree = null;
                    return;
                }

                previous = current;
                current = current->NextFree;
            }
        }

        private static void WriteEntity(Chunk* chunk, Archetype archetype, int slot, Entity entity)
        {
            Entity* entities = (Entity*)((byte*)chunk + archetype.Layout.EntityOffset);
            entities[slot] = entity;
        }

        private static void SetAllEnableableBits(Chunk* chunk, Archetype archetype, int slot, bool enabled)
        {
            for (int i = 0; i < archetype.Types.Length; i++)
            {
                if (!archetype.Types[i].IsEnableable)
                    continue;

                byte* mask = GetEnabledMask(chunk, archetype, i);
                if (mask != null)
                    SetEnabledBit(mask, slot, enabled);
            }
        }

        private void WriteComponent<T>(Chunk* chunk, Archetype archetype, int slot, ComponentType type, T component)
            where T : unmanaged, IComponentData
        {
            if (type.IsTag)
                return;

            int offset = archetype.GetComponentOffset(type.Index);
            int stride = archetype.GetComponentStride(type.Index);
            byte* target = (byte*)chunk + offset + stride * slot;
            UnsafeUtil.Copy(&component, target, stride);
            MarkComponentChanged(chunk, archetype, type);
        }

        private void MarkComponentChanged(Chunk* chunk, Archetype archetype, ComponentType type)
        {
            if (type.IsTag || chunk->ChangeVersions == null)
                return;

            int slot = archetype.GetTypeSlot(type.Index);
            if (slot >= 0)
                chunk->ChangeVersions[slot] = ++_changeVersion;
        }

        private static byte* GetEnabledMask(Chunk* chunk, Archetype archetype, int typeSlot)
        {
            int offset = archetype.Layout.GetEnabledMaskOffset(typeSlot);
            return offset == ArchetypeLayout.MissingOffset ? null : (byte*)chunk + offset;
        }

        private static bool GetEnabledBit(byte* mask, int slot)
        {
            return (mask[slot >> 3] & (1 << (slot & 7))) != 0;
        }

        private static void SetEnabledBit(byte* mask, int slot, bool enabled)
        {
            int byteIndex = slot >> 3;
            int bit = 1 << (slot & 7);
            if (enabled)
                mask[byteIndex] = (byte)(mask[byteIndex] | bit);
            else
                mask[byteIndex] = (byte)(mask[byteIndex] & ~bit);
        }
    }
}
