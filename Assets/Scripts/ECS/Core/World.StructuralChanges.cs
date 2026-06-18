using System;

namespace CyanMothUnityEcs
{
    public unsafe sealed partial class World
    {
        public void Add<T>(Entity entity, T component)
            where T : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType addedType = TypeRegistry.Get<T>();
            Chunk* sourceChunk = GetEntityChunk(entity, out int sourceSlot, out Archetype sourceArchetype);

            if (sourceArchetype.Has(addedType))
            {
                Set(entity, component);
                return;
            }

            int targetId = sourceArchetype.AddEdges[addedType.Index];
            Archetype targetArchetype;
            if (targetId >= 0)
            {
                targetArchetype = _archetypes.GetById(targetId);
            }
            else
            {
                targetArchetype = _archetypes.GetOrCreate(sourceArchetype.Mask.Add(addedType.Mask));
                sourceArchetype.AddEdges[addedType.Index] = targetArchetype.Id;
                targetArchetype.RemoveEdges[addedType.Index] = sourceArchetype.Id;
            }

            MoveEntityToArchetype(entity, sourceArchetype, sourceChunk, sourceSlot, targetArchetype, addedType, &component);
        }

        public void Remove<T>(Entity entity)
            where T : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType removedType = TypeRegistry.Get<T>();
            Chunk* sourceChunk = GetEntityChunk(entity, out int sourceSlot, out Archetype sourceArchetype);

            if (!sourceArchetype.Has(removedType))
                return;

            int targetId = sourceArchetype.RemoveEdges[removedType.Index];
            Archetype targetArchetype;
            if (targetId >= 0)
            {
                targetArchetype = _archetypes.GetById(targetId);
            }
            else
            {
                targetArchetype = _archetypes.GetOrCreate(sourceArchetype.Mask.Remove(removedType.Index));
                sourceArchetype.RemoveEdges[removedType.Index] = targetArchetype.Id;
                targetArchetype.AddEdges[removedType.Index] = sourceArchetype.Id;
            }

            MoveEntityToArchetype(entity, sourceArchetype, sourceChunk, sourceSlot, targetArchetype, default, null);
        }

        public void Destroy(Entity entity)
        {
            ThrowIfDisposed();

            Chunk* chunk = GetEntityChunk(entity, out int slot, out Archetype archetype);
            bool wasFull = chunk->Count == chunk->Capacity;

            RemoveEntityAt(archetype, chunk, slot);
            _entities.Release(entity);

            if (chunk->Count == 0)
            {
                RemoveFromFreeList(archetype, chunk);
                UnlinkChunk(archetype, chunk);
                _chunks.Free(chunk);
                return;
            }

            if (wasFull)
                AddToFreeList(archetype, chunk);
        }

        private void MoveEntityToArchetype(
            Entity entity,
            Archetype sourceArchetype,
            Chunk* sourceChunk,
            int sourceSlot,
            Archetype targetArchetype,
            ComponentType addedType,
            void* addedData)
        {
            bool sourceWasFull = sourceChunk->Count == sourceChunk->Capacity;
            Chunk* targetChunk = GetWritableChunk(targetArchetype);
            int targetSlot = targetChunk->Count++;

            WriteEntity(targetChunk, targetArchetype, targetSlot, entity);
            CopySharedComponents(sourceArchetype, sourceChunk, sourceSlot, targetArchetype, targetChunk, targetSlot);

            if (addedData != null && !addedType.IsTag)
                WriteRawComponent(targetChunk, targetArchetype, targetSlot, addedType, addedData);

            _entities.SetLocation(entity, new IntPtr(targetChunk), targetSlot, targetArchetype.Id);

            if (targetChunk->Count == targetChunk->Capacity)
                RemoveFromFreeList(targetArchetype, targetChunk);

            RemoveEntityAt(sourceArchetype, sourceChunk, sourceSlot);
            RecycleSourceChunkAfterMigration(sourceArchetype, sourceChunk, sourceWasFull);
        }

        private static void CopySharedComponents(
            Archetype sourceArchetype,
            Chunk* sourceChunk,
            int sourceSlot,
            Archetype targetArchetype,
            Chunk* targetChunk,
            int targetSlot)
        {
            for (int i = 0; i < sourceArchetype.Types.Length; i++)
            {
                ComponentType type = sourceArchetype.Types[i];
                if (type.IsTag || !targetArchetype.Has(type))
                    continue;

                int sourceOffset = sourceArchetype.GetComponentOffset(type.Index);
                int targetOffset = targetArchetype.GetComponentOffset(type.Index);
                int stride = sourceArchetype.GetComponentStride(type.Index);
                byte* source = (byte*)sourceChunk + sourceOffset + stride * sourceSlot;
                byte* target = (byte*)targetChunk + targetOffset + stride * targetSlot;
                UnsafeUtil.Copy(source, target, stride);
            }
        }

        private static void WriteRawComponent(Chunk* chunk, Archetype archetype, int slot, ComponentType type, void* data)
        {
            int offset = archetype.GetComponentOffset(type.Index);
            int stride = archetype.GetComponentStride(type.Index);
            byte* target = (byte*)chunk + offset + stride * slot;
            UnsafeUtil.Copy(data, target, stride);
        }

        private void RecycleSourceChunkAfterMigration(Archetype archetype, Chunk* chunk, bool wasFull)
        {
            if (chunk->Count == 0)
            {
                RemoveFromFreeList(archetype, chunk);
                UnlinkChunk(archetype, chunk);
                _chunks.Free(chunk);
                return;
            }

            if (wasFull)
                AddToFreeList(archetype, chunk);
        }

        private void RemoveEntityAt(Archetype archetype, Chunk* chunk, int slot)
        {
            int lastSlot = chunk->Count - 1;
            if (slot < 0 || slot > lastSlot)
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "要删除的 Chunk slot 超出当前实体数量。");

            if (slot != lastSlot)
            {
                Entity* entities = GetEntityArray(chunk, archetype);
                Entity movedEntity = entities[lastSlot];

                entities[slot] = movedEntity;
                MoveComponentRows(archetype, chunk, sourceSlot: lastSlot, targetSlot: slot);
                _entities.SetLocation(movedEntity, new IntPtr(chunk), slot, archetype.Id);
            }

            chunk->Count--;
        }

        private static void MoveComponentRows(Archetype archetype, Chunk* chunk, int sourceSlot, int targetSlot)
        {
            for (int i = 0; i < archetype.Types.Length; i++)
            {
                ComponentType type = archetype.Types[i];
                if (type.IsTag)
                    continue;

                int offset = archetype.Layout.GetComponentOffset(i);
                int stride = archetype.Layout.GetComponentStride(i);
                byte* componentBase = (byte*)chunk + offset;
                byte* source = componentBase + stride * sourceSlot;
                byte* target = componentBase + stride * targetSlot;
                UnsafeUtil.Copy(source, target, stride);
            }
        }

        private static void UnlinkChunk(Archetype archetype, Chunk* chunk)
        {
            if (chunk->Prev != null)
                chunk->Prev->Next = chunk->Next;
            else
                archetype.FirstChunk = chunk->Next;

            if (chunk->Next != null)
                chunk->Next->Prev = chunk->Prev;
            else
                archetype.LastChunk = chunk->Prev;

            chunk->Next = null;
            chunk->Prev = null;
        }
    }
}
