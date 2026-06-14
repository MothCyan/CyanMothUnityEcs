using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 固定大小的组件集合。
    /// 128 位掩码可以让 Archetype 和 Query 匹配保持低分支、零分配；
    /// 代价是第一版明确限制最多 128 种组件类型。
    /// </summary>
    public readonly struct ComponentMask : IEquatable<ComponentMask>
    {
        public static readonly ComponentMask Empty = default;

        public readonly ulong Lo;
        public readonly ulong Hi;

        public bool IsEmpty => (Lo | Hi) == 0UL;

        public ComponentMask(ulong lo, ulong hi)
        {
            Lo = lo;
            Hi = hi;
        }

        public static ComponentMask FromIndex(int index)
        {
            ValidateIndex(index);
            return index < 64
                ? new ComponentMask(1UL << index, 0UL)
                : new ComponentMask(0UL, 1UL << (index - 64));
        }

        public ComponentMask Add(int index)
        {
            ComponentMask bit = FromIndex(index);
            return new ComponentMask(Lo | bit.Lo, Hi | bit.Hi);
        }

        public ComponentMask Add(ComponentMask other)
        {
            return new ComponentMask(Lo | other.Lo, Hi | other.Hi);
        }

        public ComponentMask Remove(int index)
        {
            ComponentMask bit = FromIndex(index);
            return new ComponentMask(Lo & ~bit.Lo, Hi & ~bit.Hi);
        }

        public bool Contains(int index)
        {
            ComponentMask bit = FromIndex(index);
            return (Lo & bit.Lo) == bit.Lo && (Hi & bit.Hi) == bit.Hi;
        }

        public bool ContainsAll(ComponentMask other)
        {
            return (Lo & other.Lo) == other.Lo && (Hi & other.Hi) == other.Hi;
        }

        public bool Intersects(ComponentMask other)
        {
            return ((Lo & other.Lo) | (Hi & other.Hi)) != 0UL;
        }

        public bool Equals(ComponentMask other)
        {
            return Lo == other.Lo && Hi == other.Hi;
        }

        public override bool Equals(object obj)
        {
            return obj is ComponentMask other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Lo * 397) ^ (int)(Lo >> 32) ^ ((int)Hi * 397) ^ (int)(Hi >> 32);
            }
        }

        public override string ToString()
        {
            return $"0x{Hi:X16}_{Lo:X16}";
        }

        public static bool operator ==(ComponentMask left, ComponentMask right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ComponentMask left, ComponentMask right)
        {
            return !left.Equals(right);
        }

        private static void ValidateIndex(int index)
        {
            if ((uint)index >= TypeRegistry.MaxComponentTypes)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Component type index must be in [0, {TypeRegistry.MaxComponentTypes - 1}].");
        }
    }
}
