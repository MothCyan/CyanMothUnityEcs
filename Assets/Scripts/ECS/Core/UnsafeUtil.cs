using System;
using System.Runtime.CompilerServices;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// unsafe 层的小工具集合。
    /// 这里集中处理对齐、拷贝和清零，避免后续 Chunk / Archetype 代码里散落重复指针运算。
    /// </summary>
    internal static unsafe class UnsafeUtil
    {
        public static int SizeOf<T>() where T : unmanaged
        {
            return sizeof(T);
        }

        public static int Align(int value, int alignment)
        {
            ValidateAlignment(alignment);
            return (value + alignment - 1) & ~(alignment - 1);
        }

        public static long Align(long value, int alignment)
        {
            ValidateAlignment(alignment);
            return (value + alignment - 1L) & ~(alignment - 1L);
        }

        public static IntPtr Align(IntPtr address, int alignment)
        {
            ValidateAlignment(alignment);
            long raw = address.ToInt64();
            return new IntPtr(Align(raw, alignment));
        }

        public static bool IsAligned(IntPtr address, int alignment)
        {
            ValidateAlignment(alignment);
            return (address.ToInt64() & (alignment - 1L)) == 0L;
        }

        public static void Copy(void* source, void* destination, int byteCount)
        {
            if (byteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount), byteCount, "复制字节数不能为负数。");

            if (byteCount == 0)
                return;

            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            Buffer.MemoryCopy(source, destination, byteCount, byteCount);
        }

        public static void Clear(void* destination, int byteCount)
        {
            if (byteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount), byteCount, "清零字节数不能为负数。");

            if (byteCount == 0)
                return;

            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            byte* bytes = (byte*)destination;
            for (int i = 0; i < byteCount; i++)
                bytes[i] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateAlignment(int alignment)
        {
            if (alignment <= 0)
                throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "对齐值必须大于 0。");

            if ((alignment & (alignment - 1)) != 0)
                throw new ArgumentException("对齐值必须是 2 的幂。", nameof(alignment));
        }
    }
}
