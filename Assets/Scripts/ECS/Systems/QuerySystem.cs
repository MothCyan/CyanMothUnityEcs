namespace CyanMothUnityEcs
{
    /// <summary>
    /// 单组件 Query 系统模板。
    /// 它会在 OnCreate 时自动缓存 Query，业务系统只需要实现带 Query 参数的 OnUpdate。
    /// </summary>
    public abstract class QuerySystem<T1> : EcsSystem
        where T1 : unmanaged, IComponentData
    {
        protected Query<T1> Query { get; private set; }

        protected sealed override void OnCreate()
        {
            Query = World.Query<T1>();
            OnQueryCreate();
        }

        protected sealed override void OnUpdate(float deltaTime)
        {
            OnUpdate(deltaTime, Query);
        }

        protected virtual void OnQueryCreate()
        {
        }

        protected abstract void OnUpdate(float deltaTime, Query<T1> query);
    }

    /// <summary>
    /// 双组件 Query 系统模板。
    /// </summary>
    public abstract class QuerySystem<T1, T2> : EcsSystem
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
    {
        protected Query<T1, T2> Query { get; private set; }

        protected sealed override void OnCreate()
        {
            Query = World.Query<T1, T2>();
            OnQueryCreate();
        }

        protected sealed override void OnUpdate(float deltaTime)
        {
            OnUpdate(deltaTime, Query);
        }

        protected virtual void OnQueryCreate()
        {
        }

        protected abstract void OnUpdate(float deltaTime, Query<T1, T2> query);
    }

    /// <summary>
    /// 三组件 Query 系统模板。
    /// </summary>
    public abstract class QuerySystem<T1, T2, T3> : EcsSystem
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
        where T3 : unmanaged, IComponentData
    {
        protected Query<T1, T2, T3> Query { get; private set; }

        protected sealed override void OnCreate()
        {
            Query = World.Query<T1, T2, T3>();
            OnQueryCreate();
        }

        protected sealed override void OnUpdate(float deltaTime)
        {
            OnUpdate(deltaTime, Query);
        }

        protected virtual void OnQueryCreate()
        {
        }

        protected abstract void OnUpdate(float deltaTime, Query<T1, T2, T3> query);
    }
}
