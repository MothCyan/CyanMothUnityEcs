using UnityEngine;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 2D 位置组件。
    /// 只保存纯数值，不直接保存 Transform 或 GameObject，这样数据可以安全放进 Chunk。
    /// </summary>
    public struct Position2D : IComponentData
    {
        public float X;
        public float Y;

        public Position2D(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// 轻量 Authoring 组件：把场景里的 GameObject 转成 ECS 的 Position2D 实体。
    /// 它运行时直接创建 Entity，不经过 Baker，也不依赖 SubScene。
    /// </summary>
    public sealed class Position2DAuthoring : MonoBehaviour
    {
        [SerializeField]
        private bool useTransformPosition = true;

        [SerializeField]
        private Vector2 initialPosition;

        [SerializeField]
        private bool syncTransform = true;

        [SerializeField]
        private bool syncSpriteRenderer = true;

        public Entity Entity { get; private set; }
        public bool HasEntity => !Entity.IsNull;

        public bool SyncTransform
        {
            get => syncTransform;
            set => syncTransform = value;
        }

        public Vector2 InitialPosition
        {
            get => initialPosition;
            set
            {
                initialPosition = value;
                useTransformPosition = false;
            }
        }

        public bool SyncSpriteRenderer
        {
            get => syncSpriteRenderer;
            set => syncSpriteRenderer = value;
        }

        internal Entity CreateEntity(World world, TransformBridge transformBridge, SpriteRendererBridge spriteRendererBridge = null)
        {
            if (world == null)
                throw new System.ArgumentNullException(nameof(world));

            Vector2 position = useTransformPosition
                ? new Vector2(transform.position.x, transform.position.y)
                : initialPosition;

            Position2D positionComponent = new Position2D(position.x, position.y);
            bool hasTransform = syncTransform && transformBridge != null;
            SpriteRenderer spriteRenderer = null;
            bool hasSprite = syncSpriteRenderer &&
                             spriteRendererBridge != null &&
                             TryGetComponent(out spriteRenderer);

            if (hasTransform && hasSprite)
            {
                Entity = world.Create(
                    positionComponent,
                    new TransformProxy(transformBridge.Register(transform)),
                    new SpriteRendererProxy(spriteRendererBridge.Register(spriteRenderer)));
                world.Add(Entity, SpriteRenderState.FromColor(spriteRenderer.color, spriteRenderer.enabled));
            }
            else if (hasTransform)
            {
                Entity = world.Create(positionComponent, new TransformProxy(transformBridge.Register(transform)));
            }
            else if (hasSprite)
            {
                Entity = world.Create(
                    positionComponent,
                    new SpriteRendererProxy(spriteRendererBridge.Register(spriteRenderer)),
                    SpriteRenderState.FromColor(spriteRenderer.color, spriteRenderer.enabled));
            }
            else
            {
                Entity = world.Create(positionComponent);
            }

            return Entity;
        }
    }
}
