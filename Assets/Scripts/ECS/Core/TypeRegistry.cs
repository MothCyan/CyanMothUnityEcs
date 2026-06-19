using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 为组件类型分配进程生命周期内稳定的 TypeIndex。
    /// 注册表刻意保持简单：固定上限、不注销、不运行时重排；
    /// 这样 ComponentMask 和 Archetype Layout 才能始终稳定。
    /// </summary>
    public static class TypeRegistry
    {
        public const int MaxComponentTypes = 128;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Type, ComponentType> TypesByManagedType = new Dictionary<Type, ComponentType>(MaxComponentTypes);
        private static readonly ComponentType[] TypesByIndex = new ComponentType[MaxComponentTypes];
        private static int _count;

        public static int Count
        {
            get
            {
                lock (SyncRoot)
                    return _count;
            }
        }

        public static ComponentType Register<T>() where T : unmanaged, IComponentData
        {
            Type managedType = typeof(T);

            lock (SyncRoot)
            {
                if (TypesByManagedType.TryGetValue(managedType, out ComponentType existing))
                    return existing;

                if (_count >= MaxComponentTypes)
                    throw new InvalidOperationException($"CyanMothUnityEcs supports at most {MaxComponentTypes} component types in this implementation pass.");

                int size = SizeOf<T>();
                int align = EstimateAlignment(size);
                bool isTag = size == 1 && managedType.GetFields().Length == 0;
                bool isEnableable = typeof(IEnableableComponent).IsAssignableFrom(managedType);

                ComponentType componentType = new ComponentType(_count, size, align, isTag, isEnableable, managedType);
                TypesByManagedType.Add(managedType, componentType);
                TypesByIndex[_count] = componentType;
                _count++;
                return componentType;
            }
        }

        public static ComponentType Get<T>() where T : unmanaged, IComponentData
        {
            return ComponentTypeCache<T>.Type;
        }

        public static ComponentType GetByIndex(int index)
        {
            if ((uint)index >= MaxComponentTypes)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Component type index must be in [0, {MaxComponentTypes - 1}].");

            lock (SyncRoot)
            {
                if (index >= _count)
                    throw new InvalidOperationException($"No component type registered at index {index}.");

                return TypesByIndex[index];
            }
        }

        /// <summary>
        /// 仅供测试重置注册表状态。运行时业务代码不要调用；
        /// 正常游戏流程里 TypeIndex 一旦分配就必须保持稳定。
        /// </summary>
        public static void ClearForTests()
        {
            lock (SyncRoot)
            {
                TypesByManagedType.Clear();
                Array.Clear(TypesByIndex, 0, TypesByIndex.Length);
                _count = 0;
            }
        }

        private static int SizeOf<T>() where T : unmanaged
        {
            // Unity 当前 C# 配置下，Marshal.SizeOf 可以处理普通 unmanaged struct。
            // 空 struct 在 .NET 中大小会表现为 1；布局层会把它当作 tag 组件处理。
            return Marshal.SizeOf<T>();
        }

        private static int EstimateAlignment(int size)
        {
            if (size >= 8)
                return 8;
            if (size >= 4)
                return 4;
            if (size >= 2)
                return 2;
            return 1;
        }
    }
}
