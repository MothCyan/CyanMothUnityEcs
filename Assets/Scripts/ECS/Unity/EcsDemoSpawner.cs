using UnityEngine;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 最小 ECS Demo 生成器。
    /// 它批量创建带 Position2DAuthoring 的 GameObject，用来快速验证 Authoring、运动系统、桥接同步和调试浮层。
    /// </summary>
    public sealed class EcsDemoSpawner : MonoBehaviour
    {
        [SerializeField]
        private EcsRunner runner;

        [SerializeField]
        private Sprite sprite;

        [SerializeField]
        private int count = 64;

        [SerializeField]
        private Vector2 areaSize = new Vector2(12, 7);

        [SerializeField]
        private float minSpeed = 0.5f;

        [SerializeField]
        private float maxSpeed = 2.5f;

        [SerializeField]
        private bool spawnOnAwake = true;

        [SerializeField]
        private bool convertImmediately = true;

        private int _spawnedCount;
        private Sprite _runtimeSprite;

        public int SpawnedCount => _spawnedCount;

        private void Awake()
        {
            if (spawnOnAwake)
                Spawn();
        }

        /// <summary>
        /// 生成一批 Demo 对象。
        /// 每个对象都会带 Position2DAuthoring；如果配置了 Sprite，也会带 SpriteRenderer。
        /// </summary>
        public void Spawn()
        {
            if (_spawnedCount > 0)
                return;

            EcsRunner targetRunner = ResolveRunner();
            int safeCount = Mathf.Max(0, count);
            for (int i = 0; i < safeCount; i++)
            {
                GameObject item = CreateItem(i);
                Position2DAuthoring authoring = item.AddComponent<Position2DAuthoring>();
                authoring.InitialPosition = item.transform.position;
                authoring.InitialVelocity = CreateVelocity(i, safeCount);

                if (convertImmediately && targetRunner != null)
                    targetRunner.Convert(authoring);

                _spawnedCount++;
            }
        }

        private EcsRunner ResolveRunner()
        {
            if (runner != null)
                return runner;

#if UNITY_2023_1_OR_NEWER
            runner = FindFirstObjectByType<EcsRunner>();
#else
            runner = FindObjectOfType<EcsRunner>();
#endif
            return runner;
        }

        private GameObject CreateItem(int index)
        {
            GameObject item = new GameObject($"CyanMoth ECS Demo {index:000}");
            item.transform.SetParent(transform, worldPositionStays: false);
            item.transform.localPosition = CreatePosition(index);

            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = ResolveSprite();
            renderer.color = CreateColor(index);
            renderer.sortingOrder = index;
            return item;
        }

        private Sprite ResolveSprite()
        {
            if (sprite != null)
                return sprite;

            if (_runtimeSprite == null)
            {
                _runtimeSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f),
                    1f);
            }

            return _runtimeSprite;
        }

        private Vector3 CreatePosition(int index)
        {
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, count))));
            int row = index / columns;
            int column = index % columns;
            float x01 = columns <= 1 ? 0.5f : column / (float)(columns - 1);
            float y01 = columns <= 1 ? 0.5f : row / (float)(columns - 1);
            float x = (x01 - 0.5f) * areaSize.x;
            float y = (y01 - 0.5f) * areaSize.y;
            return new Vector3(x, y, 0);
        }

        private Vector2 CreateVelocity(int index, int total)
        {
            float t = total <= 1 ? 0 : index / (float)(total - 1);
            float angle = t * Mathf.PI * 2f;
            float speed = Mathf.Lerp(minSpeed, maxSpeed, Mathf.PingPong(index * 0.37f, 1f));
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        }

        private static Color CreateColor(int index)
        {
            float hue = Mathf.Repeat(index * 0.071f, 1f);
            return Color.HSVToRGB(hue, 0.72f, 1f);
        }
    }
}
