namespace CyanMothUnityEcs
{
    /// <summary>
    /// 可存入 ECS Chunk 的组件标记接口。
    /// 组件只保存数据，不写行为；行为交给 System，这样 Chunk 数据才能保持紧凑、
    /// 可预测，并且在 Archetype 迁移时更容易整块复制。
    /// </summary>
    public interface IComponentData
    {
    }

    /// <summary>
    /// 可启用组件标记接口。
    /// 实现这个接口的组件可以在不 Add/Remove、不迁移 Archetype 的情况下临时启用或禁用。
    /// </summary>
    public interface IEnableableComponent : IComponentData
    {
    }
}
