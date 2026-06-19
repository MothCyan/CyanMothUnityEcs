using UnityEngine;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 运行时 ECS 调试浮层。
    /// 它只读取 WorldStats 快照，不持有 Chunk 指针，也不参与 ECS 热路径。
    /// </summary>
    public sealed class EcsDebugOverlay : MonoBehaviour
    {
        [SerializeField]
        private EcsRunner runner;

        [SerializeField]
        private bool autoFindRunner = true;

        [SerializeField]
        private bool showWhenNotRunning = true;

        [SerializeField]
        private Rect windowRect = new Rect(12, 12, 260, 190);

        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;

        public EcsRunner Runner
        {
            get => runner;
            set => runner = value;
        }

        private void Awake()
        {
            TryAutoFindRunner();
        }

        private void OnGUI()
        {
            EnsureStyles();
            TryAutoFindRunner();

            GUILayout.BeginArea(windowRect, GUI.skin.box);
            GUILayout.Label("CyanMoth ECS", _titleStyle);

            if (runner == null)
            {
                DrawLine("Runner", "未找到");
                GUILayout.EndArea();
                return;
            }

            DrawLine("Runner", runner.name);
            if (!runner.IsRunning)
            {
                if (showWhenNotRunning)
                    DrawLine("状态", "未运行");

                GUILayout.EndArea();
                return;
            }

            WorldStats stats = runner.World.GetStats();
            DrawLine("实体", stats.AliveEntityCount.ToString());
            DrawLine("实体容量", stats.CreatedEntityCapacity.ToString());
            DrawLine("Archetype", stats.ArchetypeCount.ToString());
            DrawLine("Chunk", stats.ChunkCount.ToString());
            DrawLine("预留 Chunk", stats.ReservedChunkCount.ToString());
            DrawLine("Chunk 容量", stats.TotalChunkCapacity.ToString());
            DrawLine("Chunk 利用率", stats.ChunkUtilization.ToString("0.00%"));
            DrawLine("待回放命令", stats.CommandCount.ToString());
            DrawLine("Authoring 实体", runner.AuthoredEntityCount.ToString());
            DrawLine("Transform 桥接", runner.TransformBridge.Count.ToString());
            DrawLine("Sprite 桥接", runner.SpriteRendererBridge.Count.ToString());
            GUILayout.EndArea();
        }

        private void TryAutoFindRunner()
        {
            if (runner != null || !autoFindRunner)
                return;

#if UNITY_2023_1_OR_NEWER
            runner = FindFirstObjectByType<EcsRunner>();
#else
            runner = FindObjectOfType<EcsRunner>();
#endif
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };
        }

        private void DrawLine(string name, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _labelStyle, GUILayout.Width(92));
            GUILayout.Label(value, _labelStyle);
            GUILayout.EndHorizontal();
        }
    }
}
