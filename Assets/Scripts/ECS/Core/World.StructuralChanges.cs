using System;

namespace CyanMothUnityEcs
{
    public unsafe sealed partial class World
    {
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

        private static Entity* GetEntityArray(Chunk* chunk, Archetype archetype)
        {
            return (Entity*)((byte*)chunk + archetype.Layout.EntityOffset);
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
