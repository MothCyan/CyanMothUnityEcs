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
            AddRaw(entity, addedType, &component);
        }

        internal void AddRaw(Entity entity, ComponentType addedType, void* componentData)
        {
            ThrowIfDisposed();

            Chunk* sourceChunk = GetEntityChunk(entity, out int sourceSlot, out Archetype sourceArchetype);

            if (sourceArchetype.Has(addedType))
            {
                if (componentData != null && !addedType.IsTag)
                    WriteExistingRawComponent(entity, addedType, componentData);
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

            MoveEntityToArchetype(entity, sourceArchetype, sourceChunk, sourceSlot, targetArchetype, addedType, componentData);
        }

        public void Remove<T>(Entity entity)
            where T : unmanaged, IComponentData
        {
            ThrowIfDisposed();

            ComponentType removedType = TypeRegistry.Get<T>();
            RemoveRaw(entity, removedType);
        }

        internal void RemoveRaw(Entity entity, ComponentType removedType)
        {
            ThrowIfDisposed();

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

            if (addedType.IsEnableable)
                SetEnabledForMigratedComponent(targetChunk, targetArchetype, targetSlot, addedType, true);

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
                CopyChangeVersion(sourceArchetype, sourceChunk, targetArchetype, targetChunk, type);
                CopyEnabledState(sourceArchetype, sourceChunk, sourceSlot, targetArchetype, targetChunk, targetSlot, type);
            }
        }

        private static void CopyEnabledState(
            Archetype sourceArchetype,
            Chunk* sourceChunk,
            int sourceEntitySlot,
            Archetype targetArchetype,
            Chunk* targetChunk,
            int targetEntitySlot,
            ComponentType type)
        {
            if (!type.IsEnableable)
                return;

            int sourceTypeSlot = sourceArchetype.GetTypeSlot(type.Index);
            int targetTypeSlot = targetArchetype.GetTypeSlot(type.Index);
            if (sourceTypeSlot < 0 || targetTypeSlot < 0)
                return;

            byte* sourceMask = GetEnabledMask(sourceChunk, sourceArchetype, sourceTypeSlot);
            byte* targetMask = GetEnabledMask(targetChunk, targetArchetype, targetTypeSlot);
            if (sourceMask == null || targetMask == null)
                return;

            SetEnabledBit(targetMask, targetEntitySlot, GetEnabledBit(sourceMask, sourceEntitySlot));
        }

        private static void CopyChangeVersion(
            Archetype sourceArchetype,
            Chunk* sourceChunk,
            Archetype targetArchetype,
            Chunk* targetChunk,
            ComponentType type)
        {
            if (sourceChunk->ChangeVersions == null || targetChunk->ChangeVersions == null)
                return;

            int sourceSlot = sourceArchetype.GetTypeSlot(type.Index);
            int targetSlot = targetArchetype.GetTypeSlot(type.Index);
            if (sourceSlot >= 0 && targetSlot >= 0)
                targetChunk->ChangeVersions[targetSlot] = Math.Max(
                    targetChunk->ChangeVersions[targetSlot],
                    sourceChunk->ChangeVersions[sourceSlot]);
        }

        private void WriteRawComponent(Chunk* chunk, Archetype archetype, int slot, ComponentType type, void* data)
        {
            int offset = archetype.GetComponentOffset(type.Index);
            int stride = archetype.GetComponentStride(type.Index);
            byte* target = (byte*)chunk + offset + stride * slot;
            UnsafeUtil.Copy(data, target, stride);
            MarkComponentChanged(chunk, archetype, type);
        }

        private static void SetEnabledForMigratedComponent(Chunk* chunk, Archetype archetype, int entitySlot, ComponentType type, bool enabled)
        {
            int typeSlot = archetype.GetTypeSlot(type.Index);
            if (typeSlot < 0)
                return;

            byte* mask = GetEnabledMask(chunk, archetype, typeSlot);
            if (mask != null)
                SetEnabledBit(mask, entitySlot, enabled);
        }

        private void WriteExistingRawComponent(Entity entity, ComponentType type, void* data)
        {
            Chunk* chunk = GetEntityChunk(entity, out int slot, out Archetype archetype);
            WriteRawComponent(chunk, archetype, slot, type, data);
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
                MoveEnabledBits(archetype, chunk, sourceSlot: lastSlot, targetSlot: slot);
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

        private static void MoveEnabledBits(Archetype archetype, Chunk* chunk, int sourceSlot, int targetSlot)
        {
            for (int i = 0; i < archetype.Types.Length; i++)
            {
                if (!archetype.Types[i].IsEnableable)
                    continue;

                byte* mask = GetEnabledMask(chunk, archetype, i);
                if (mask == null)
                    continue;

                SetEnabledBit(mask, targetSlot, GetEnabledBit(mask, sourceSlot));
                SetEnabledBit(mask, sourceSlot, true);
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
