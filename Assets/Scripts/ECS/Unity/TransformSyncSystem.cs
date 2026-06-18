using UnityEngine;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 把 ECS 中的 Position2D 同步到 Unity Transform。
    /// 这是显式桥接系统：只有带 TransformProxy 的实体才会产生 Unity 对象访问成本。
    /// </summary>
    public sealed unsafe class TransformSyncSystem : EcsSystem
    {
        private readonly TransformBridge _bridge;
        private Query<Position2D, TransformProxy> _query;

        public TransformSyncSystem(TransformBridge bridge)
        {
            _bridge = bridge;
        }

        protected override void OnCreate()
        {
            _query = World.Query<Position2D, TransformProxy>();
        }

        protected override void OnUpdate(float deltaTime)
        {
            _query.ForEachChunk((Entity* entities, Position2D* positions, TransformProxy* proxies, int count) =>
            {
                for (int i = 0; i < count; i++)
                {
                    if (!_bridge.TryGet(proxies[i].Id, out Transform transform))
                        continue;

                    Vector3 current = transform.position;
                    transform.position = new Vector3(positions[i].X, positions[i].Y, current.z);
                }
            });
        }
    }
}
