namespace CyanMothUnityEcs
{
    /// <summary>
    /// 每个组件泛型类型各自拥有一份静态缓存。
    /// 第一次访问会完成注册或查表，之后热路径可以直接复用缓存好的组件元数据。
    /// </summary>
    public static class ComponentTypeCache<T> where T : unmanaged, IComponentData
    {
        public static readonly ComponentType Type = TypeRegistry.Register<T>();
    }
}
