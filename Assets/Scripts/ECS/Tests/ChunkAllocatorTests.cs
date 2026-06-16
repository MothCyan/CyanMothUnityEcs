using System;
using NUnit.Framework;

#pragma warning disable CS8500

namespace CyanMothUnityEcs.Tests
{
    public unsafe sealed class ChunkAllocatorTests
    {
        [Test]
        public void UnsafeUtil_Align_WorksForIntegersAndPointers()
        {
            Assert.AreEqual(0, UnsafeUtil.Align(0, 64));
            Assert.AreEqual(64, UnsafeUtil.Align(1, 64));
            Assert.AreEqual(64, UnsafeUtil.Align(64, 64));
            Assert.AreEqual(128, UnsafeUtil.Align(65, 64));

            IntPtr aligned = UnsafeUtil.Align(new IntPtr(65), 64);
            Assert.AreEqual(new IntPtr(128), aligned);
            Assert.IsTrue(UnsafeUtil.IsAligned(aligned, 64));
        }

        [Test]
        public void Allocate_ReturnsAlignedChunkAndInitializesHeader()
        {
            using (ChunkAllocator allocator = new ChunkAllocator(chunksPerBlock: 2))
            {
                Chunk* chunk = allocator.Allocate(archetypeId: 7, capacity: 123);

                Assert.IsTrue(UnsafeUtil.IsAligned(new IntPtr(chunk), Chunk.Alignment));
                Assert.AreEqual(7, chunk->ArchetypeId);
                Assert.AreEqual(0, chunk->Count);
                Assert.AreEqual(123, chunk->Capacity);
                Assert.AreEqual(0, chunk->Sequence);
                Assert.AreEqual(IntPtr.Zero, new IntPtr(chunk->Next));
                Assert.AreEqual(IntPtr.Zero, new IntPtr(chunk->Prev));
                Assert.AreEqual(IntPtr.Zero, new IntPtr(chunk->NextFree));
            }
        }

        [Test]
        public void Free_ReusesChunkFromFreeList()
        {
            using (ChunkAllocator allocator = new ChunkAllocator(chunksPerBlock: 1))
            {
                Chunk* first = allocator.Allocate(archetypeId: 1, capacity: 10);
                allocator.Free(first);

                Chunk* second = allocator.Allocate(archetypeId: 2, capacity: 20);

                Assert.AreEqual(new IntPtr(first), new IntPtr(second));
                Assert.AreEqual(2, second->ArchetypeId);
                Assert.AreEqual(20, second->Capacity);
            }
        }

        [Test]
        public void Allocate_RequestsNewBlockWhenFreeListIsEmpty()
        {
            using (ChunkAllocator allocator = new ChunkAllocator(chunksPerBlock: 1))
            {
                Chunk* first = allocator.Allocate(archetypeId: 1, capacity: 1);
                Chunk* second = allocator.Allocate(archetypeId: 1, capacity: 1);

                Assert.AreNotEqual(new IntPtr(first), new IntPtr(second));
                Assert.AreEqual(2, allocator.BlockCount);
            }
        }

        [Test]
        public void Allocate_ThrowsAfterDispose()
        {
            ChunkAllocator allocator = new ChunkAllocator();
            allocator.Dispose();

            Assert.Throws<ObjectDisposedException>(() => allocator.Allocate(archetypeId: 0, capacity: 0));
        }
    }
}
