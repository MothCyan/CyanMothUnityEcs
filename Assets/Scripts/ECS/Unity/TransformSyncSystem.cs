using UnityEngine;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 2D 位置运动系统。
    /// 它只处理纯 ECS 数据：读取 Velocity2D，写入 Position2D，不直接访问 Unity 对象。
    /// </summary>
    public sealed class Position2DMoveSystem : EcsSystem
    {
        private Query<Position2D, Velocity2D> _query;

        protected override void OnCreate()
        {
            _query = World.Query<Position2D, Velocity2D>();
        }

        protected override void OnUpdate(float deltaTime)
        {
            _query.ForEachWrite<Position2D>((Entity entity, ref Position2D position, ref Velocity2D velocity) =>
            {
                position.X += velocity.X * deltaTime;
                position.Y += velocity.Y * deltaTime;
            });
        }
    }

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

    /// <summary>
    /// 把 ECS 中的 SpriteRenderState 同步到 Unity SpriteRenderer。
    /// 只有带 SpriteRendererProxy 的实体才会访问 Unity 渲染组件。
    /// </summary>
    public sealed unsafe class SpriteRendererSyncSystem : EcsSystem
    {
        private readonly SpriteRendererBridge _bridge;
        private Query<SpriteRendererProxy, SpriteRenderState> _query;

        public SpriteRendererSyncSystem(SpriteRendererBridge bridge)
        {
            _bridge = bridge;
        }

        protected override void OnCreate()
        {
            _query = World.Query<SpriteRendererProxy, SpriteRenderState>();
        }

        protected override void OnUpdate(float deltaTime)
        {
            _query.ForEachReadOnly((Entity entity, in SpriteRendererProxy proxy, in SpriteRenderState state) =>
            {
                if (!_bridge.TryGet(proxy.Id, out SpriteRenderer renderer))
                    return;

                renderer.enabled = state.Visible != 0;
                renderer.color = new Color(state.R, state.G, state.B, state.A);
            });
        }
    }
}
