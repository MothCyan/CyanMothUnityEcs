using UnityEngine;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// Unity 场景中的 ECS 启动器。
    /// 它把 Unity 生命周期转换成 World 和 SystemPipeline 的创建、更新、释放。
    /// </summary>
    public class EcsRunner : MonoBehaviour
    {
        [SerializeField]
        private bool addTransformSyncSystem = true;

        public World World { get; private set; }
        public SystemPipeline Pipeline { get; private set; }
        public TransformBridge TransformBridge { get; private set; }
        public bool IsRunning => World != null;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        /// <summary>
        /// 手动初始化 ECS。
        /// 测试或自定义启动流程可以直接调用它；重复调用不会重复创建 World。
        /// </summary>
        public void Initialize()
        {
            if (IsRunning)
                return;

            World = new World();
            Pipeline = new SystemPipeline(World);
            TransformBridge = new TransformBridge();

            Configure(Pipeline, World, TransformBridge);
        }

        /// <summary>
        /// 执行一帧 ECS 更新。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsRunning)
                return;

            Pipeline.Update(deltaTime);
        }

        /// <summary>
        /// 释放 ECS。
        /// </summary>
        public void Shutdown()
        {
            if (!IsRunning)
                return;

            Pipeline.Dispose();
            TransformBridge.Dispose();
            World.Dispose();

            Pipeline = null;
            TransformBridge = null;
            World = null;
        }

        /// <summary>
        /// 子类可以重写这里注册自己的系统。
        /// 默认只加入 TransformSyncSystem，保证第一版 Unity Bridge 开箱能同步 Transform。
        /// </summary>
        protected virtual void Configure(SystemPipeline pipeline, World world, TransformBridge transformBridge)
        {
            if (addTransformSyncSystem)
                pipeline.Add(new TransformSyncSystem(transformBridge));
        }
    }
}
