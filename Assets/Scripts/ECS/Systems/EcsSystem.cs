using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// ECS 系统基类。
    /// 系统只负责写一段“每帧要做什么”的逻辑，真正的数据仍然存放在 World 的 Archetype + Chunk 中。
    /// </summary>
    public abstract class EcsSystem
    {
        /// <summary>
        /// 当前系统所属的 World。
        /// OnCreate、OnUpdate、OnDestroy 中都可以通过它访问实体、组件、Query 和 CommandBuffer。
        /// </summary>
        public World World { get; private set; }

        /// <summary>
        /// 系统是否已经被加入某个 SystemPipeline。
        /// 第一版不允许一个系统实例同时挂到多个管线，避免生命周期混乱。
        /// </summary>
        public bool IsAttached => World != null;

        internal void Attach(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            if (IsAttached)
                throw new InvalidOperationException($"{GetType().Name} 已经加入过 SystemPipeline。");

            World = world;
            OnCreate();
        }

        internal void Update(float deltaTime)
        {
            if (!IsAttached)
                throw new InvalidOperationException($"{GetType().Name} 还没有加入 SystemPipeline。");

            OnUpdate(deltaTime);
        }

        internal void Detach()
        {
            if (!IsAttached)
                return;

            try
            {
                OnDestroy();
            }
            finally
            {
                World = null;
            }
        }

        /// <summary>
        /// 系统加入管线时调用一次。
        /// 适合在这里缓存 Query，避免每帧重复构造查询句柄。
        /// </summary>
        protected virtual void OnCreate()
        {
        }

        /// <summary>
        /// 每次 SystemPipeline.Update 时调用。
        /// 这里是系统的主体逻辑。
        /// </summary>
        protected abstract void OnUpdate(float deltaTime);

        /// <summary>
        /// 管线释放时调用一次。
        /// 适合释放系统自己持有的托管资源或 Unity 桥接对象引用。
        /// </summary>
        protected virtual void OnDestroy()
        {
        }
    }
}
