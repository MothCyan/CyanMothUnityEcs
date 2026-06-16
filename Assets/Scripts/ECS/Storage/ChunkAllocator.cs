using System;
using System.Runtime.InteropServices;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// Chunk 内存分配器。
    /// 它一次申请一批 native memory，然后切成多个固定大小 Chunk，避免每个 Chunk 都触发系统分配。
    /// </summary>
    internal unsafe sealed class ChunkAllocator : IDisposable
    {
        private const int DefaultChunksPerBlock = 64;

        private readonly int _chunksPerBlock;
        private IntPtr[] _rawBlocks;
        private int _blockCount;
        private Chunk* _freeList;
        private int _nextSequence;
        private bool _disposed;

        public ChunkAllocator(int chunksPerBlock = DefaultChunksPerBlock)
        {
            if (chunksPerBlock <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunksPerBlock), chunksPerBlock, "每个内存块至少要包含一个 Chunk。");

            _chunksPerBlock = chunksPerBlock;
            _rawBlocks = new IntPtr[4];
        }

        public int BlockCount => _blockCount;
        public int ChunkSize => Chunk.Size;
        public int Alignment => Chunk.Alignment;


        public Chunk* Allocate(int archetypeId, int capacity)
        {
            ThrowIfDisposed();

            if (archetypeId < 0)
                throw new ArgumentOutOfRangeException(nameof(archetypeId), archetypeId, "ArchetypeId 不能为负数。");
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Chunk 容量不能为负数。");

            if (_freeList == null)
                AllocateBlock();

            Chunk* chunk = _freeList;
            _freeList = chunk->NextFree;

            chunk->Reset(archetypeId, capacity, _nextSequence++);
            return chunk;
        }

        public void Free(Chunk* chunk)
        {
            ThrowIfDisposed();

            if (chunk == null)
                return;

            // 释放到 free list 时清理头部状态，避免旧链表指针或计数被误读。
            chunk->Reset(-1, 0, 0);
            chunk->NextFree = _freeList;
            _freeList = chunk;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            for (int i = 0; i < _blockCount; i++)
                Marshal.FreeHGlobal(_rawBlocks[i]);

            Array.Clear(_rawBlocks, 0, _rawBlocks.Length);
            _blockCount = 0;
            _freeList = null;
            _disposed = true;
        }

        private void AllocateBlock()
        {
            int blockBytes = Chunk.Size * _chunksPerBlock + Chunk.Alignment - 1;
            IntPtr rawBlock = Marshal.AllocHGlobal(blockBytes);
            IntPtr alignedStart = UnsafeUtil.Align(rawBlock, Chunk.Alignment);

            if (_blockCount == _rawBlocks.Length)
                Array.Resize(ref _rawBlocks, _rawBlocks.Length * 2);

            _rawBlocks[_blockCount++] = rawBlock;

            byte* cursor = (byte*)alignedStart;
            for (int i = 0; i < _chunksPerBlock; i++)
            {
                Chunk* chunk = (Chunk*)(cursor + Chunk.Size * i);
                UnsafeUtil.Clear(chunk, UnsafeUtil.SizeOf<Chunk>());
                chunk->Reset(-1, 0, 0);
                chunk->NextFree = _freeList;
                _freeList = chunk;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChunkAllocator));
        }
    }
}
