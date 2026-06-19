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

        [SerializeField]
        private bool addSpriteRendererSyncSystem = true;

        [SerializeField]
        private bool convertPosition2DAuthoringOnInitialize = true;

        public World World { get; private set; }
        public SystemPipeline Pipeline { get; private set; }
        public TransformBridge TransformBridge { get; private set; }
        public SpriteRendererBridge SpriteRendererBridge { get; private set; }
        public int AuthoredEntityCount { get; private set; }
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
            SpriteRendererBridge = new SpriteRendererBridge();

            Configure(Pipeline, World, TransformBridge, SpriteRendererBridge);

            if (convertPosition2DAuthoringOnInitialize)
                ConvertPosition2DAuthoring();
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
            SpriteRendererBridge.Dispose();
            TransformBridge.Dispose();
            World.Dispose();

            Pipeline = null;
            SpriteRendererBridge = null;
            TransformBridge = null;
            World = null;
            AuthoredEntityCount = 0;
        }

        /// <summary>
        /// 子类可以重写这里注册自己的系统。
        /// 默认只加入 TransformSyncSystem，保证第一版 Unity Bridge 开箱能同步 Transform。
        /// </summary>
        protected virtual void Configure(SystemPipeline pipeline, World world, TransformBridge transformBridge)
        {
            Configure(pipeline, world, transformBridge, SpriteRendererBridge);
        }

        /// <summary>
        /// 子类可以重写这里注册自己的系统，并同时拿到 Transform 和 SpriteRenderer 桥接表。
        /// </summary>
        protected virtual void Configure(SystemPipeline pipeline, World world, TransformBridge transformBridge, SpriteRendererBridge spriteRendererBridge)
        {
            if (addTransformSyncSystem)
                pipeline.Add(new TransformSyncSystem(transformBridge));
            if (addSpriteRendererSyncSystem)
                pipeline.Add(new SpriteRendererSyncSystem(spriteRendererBridge));
        }

        /// <summary>
        /// 扫描场景中的 Position2DAuthoring，并直接创建 ECS Entity。
        /// 这是轻量版替代 Baker/SubScene 的第一条运行时 Authoring 链路。
        /// </summary>
        protected virtual void ConvertPosition2DAuthoring()
        {
#if UNITY_2023_1_OR_NEWER
            Position2DAuthoring[] authorings = FindObjectsByType<Position2DAuthoring>(FindObjectsSortMode.None);
#else
            Position2DAuthoring[] authorings = FindObjectsOfType<Position2DAuthoring>();
#endif
            for (int i = 0; i < authorings.Length; i++)
            {
                if (authorings[i].HasEntity)
                    continue;

                authorings[i].CreateEntity(World, TransformBridge, SpriteRendererBridge);
                AuthoredEntityCount++;
            }
        }
    }
}
