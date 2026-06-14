using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 单个组件类型的运行时元数据。
    /// 组件注册完成后，Archetype 布局和 Query 掩码都使用这里缓存的数据，
    /// 避免在热路径里做反射。
    /// </summary>
    public readonly struct ComponentType : IEquatable<ComponentType>
    {
        public readonly int Index;
        public readonly int Size;
        public readonly int Align;
        public readonly bool IsTag;
        public readonly Type ManagedType;
        public readonly ComponentMask Mask;

        public ComponentType(int index, int size, int align, bool isTag, Type managedType)
        {
            Index = index;
            Size = size;
            Align = align;
            IsTag = isTag;
            ManagedType = managedType ?? throw new ArgumentNullException(nameof(managedType));
            Mask = ComponentMask.FromIndex(index);
        }

        public bool Equals(ComponentType other)
        {
            return Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is ComponentType other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Index;
        }

        public override string ToString()
        {
            return $"{ManagedType.Name}[{Index}] Size={Size} Align={Align}";
        }
    }
}
