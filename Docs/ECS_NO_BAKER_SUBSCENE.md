# 无 Baker / 无 SubScene 替代方案

> 目标：不用 Unity DOTS 的 Baker 和 SubScene，也能保留 Unity 场景编辑便利性，并在运行时生成高性能 `Archetype + Chunk + SoA` ECS 数据。核心原则是：转换发生在冷路径，模拟发生在热路径。

---

## 一、为什么要替代 Baker 和 SubScene

Unity ECS 的 Baker/SubScene 很强，但它带来一整套额外流程：

```text
Authoring MonoBehaviour
-> Baker
-> 烘焙 Entity 数据
-> Entity Scene
-> SubScene 加载/卸载
-> Runtime World
```

它适合大型 DOTS 内容管线，但对自研轻量高性能 ECS 来说有几个问题：

- 学习成本高。
- 调试路径间接。
- 运行时数据和编辑器数据之间隔了一层烘焙产物。
- 很多行为要服从 DOTS 的流程。
- 自研 ECS 的核心目标会被 Unity Entities 生态牵着走。

本项目要替换成：

```text
Authoring MonoBehaviour
-> RuntimeAuthoringCollector
-> SpawnCatalog
-> ArchetypePrefab
-> World.CreateMany / InstantiateMany
-> SceneSectionRuntime
```

一句话：

```text
不用离线 Baker
不用 SubScene
用运行时收集 + 纯数据目录 + 自己的分区加载
```

---

## 二、核心原则

### 1. Authoring 只在冷路径

Authoring MonoBehaviour 只允许在这些时机被读取：

- `EcsRunner.Awake`
- 关卡加载
- Section 加载
- 手动刷新 SpawnCatalog
- 编辑器调试工具

禁止：

```text
每帧 FindObjectsOfType
每帧扫描场景 Authoring
每帧从 MonoBehaviour 同步 ECS 数据
```

### 2. ECS 热路径只处理 Chunk 数据

每帧 Simulation 只能处理：

```text
World
Archetype
Chunk
Component pointer
CommandBuffer
```

不能处理：

```text
Authoring MonoBehaviour
Scene GameObject
SerializedObject
反射转换
FindObjectsOfType
```

### 3. Unity 引用不能进入 Chunk

ECS Component 不保存：

- `GameObject`
- `Transform`
- `MonoBehaviour`
- `ScriptableObject`
- `string`
- `class`

Unity 对象只存在 Bridge 里：

```text
TransformProxy.Id -> TransformBridge.Transforms[id]
```

### 4. 大量生成必须批量化

不允许大量实体走：

```text
Create
Add
Add
Add
```

必须走：

```text
CreateMany
InstantiateMany
PrewarmArchetype
```

---

## 三、总体架构

```
Unity Scene
├── EcsWorldAuthoring
├── EcsSpawnAuthoring
├── EcsPrefabAuthoring
└── EcsSectionAuthoring

Runtime Bootstrap
├── RuntimeAuthoringCollector
├── SpawnCatalogBuilder
├── TransformBridge
└── SceneSectionBuilder

ECS Runtime
├── World
├── ArchetypePrefab
├── SpawnCatalog
├── SceneSectionRuntime
├── StreamingService
└── SystemPipeline
```

数据流：

```text
Unity Scene Authoring
-> Collect
-> Convert to pure data
-> Prewarm
-> Create ECS entities
-> Run simulation
-> Bridge back to Unity presentation
```

---

## 四、模块职责

| 模块 | 类型 | 职责 |
|---|---|---|
| `EcsWorldAuthoring` | MonoBehaviour | 场景里的 ECS 启动配置 |
| `EcsSpawnAuthoring` | MonoBehaviour | 描述一批要生成的实体 |
| `EcsPrefabAuthoring` | MonoBehaviour / ScriptableObject | 描述实体模板 |
| `EcsSectionAuthoring` | MonoBehaviour | 描述一个加载分区 |
| `RuntimeAuthoringCollector` | 普通 C# 类 | 运行时收集 Authoring |
| `SpawnCatalogBuilder` | 普通 C# 类 | 把 Authoring 转成 SpawnCatalog |
| `SpawnCatalog` | 纯 C# 数据 | 保存生成请求 |
| `ArchetypePrefab` | 纯 C# 数据 | 保存 ECS 实体模板 |
| `SceneSectionRuntime` | 纯 C# 数据 | 管理一组已生成实体 |
| `StreamingService` | 普通 C# 类 | 控制 Section 加载/卸载 |
| `TransformBridge` | Bridge 类 | 保存 Unity Transform 引用 |

---

## 五、Authoring 层设计

### EcsWorldAuthoring

场景入口配置。

```csharp
public sealed class EcsWorldAuthoring : MonoBehaviour
{
    public bool AutoStart = true;
    public bool CollectActiveScene = true;
    public EcsSectionAuthoring[] InitialSections;
}
```

用途：

- 指定是否自动启动 ECS。
- 指定启动时加载哪些 Section。
- 给 `EcsRunner` 提供场景级配置。

### EcsSpawnAuthoring

描述一批实体。

```csharp
public sealed class EcsSpawnAuthoring : MonoBehaviour
{
    public int Count = 1;
    public Vector3 StartPosition;
    public Vector3 Spacing;
    public Vector3 InitialVelocity;
    public int Health = 100;
    public bool LinkTransform;
}
```

它不直接创建 ECS Entity，只描述数据。

### EcsPrefabAuthoring

描述一个模板。

第一版可以用 MonoBehaviour：

```csharp
public sealed class EcsPrefabAuthoring : MonoBehaviour
{
    public Vector3 DefaultVelocity;
    public int DefaultHealth = 100;
    public bool HasTransform;
}
```

后续可以改成 ScriptableObject：

```csharp
[CreateAssetMenu]
public sealed class EcsPrefabAsset : ScriptableObject
{
    public Vector3 DefaultVelocity;
    public int DefaultHealth;
}
```

### EcsSectionAuthoring

描述一个场景分区。

```csharp
public sealed class EcsSectionAuthoring : MonoBehaviour
{
    public int SectionId;
    public Bounds Bounds;
    public bool LoadOnStart;
    public EcsSpawnAuthoring[] Spawns;
}
```

它替代 SubScene 的 section 概念。

---

## 六、运行时收集层

### RuntimeAuthoringCollector

收集场景 Authoring。

```csharp
public sealed class RuntimeAuthoringCollector
{
    public RuntimeAuthoringData Collect(Scene scene, TransformBridge transformBridge)
    {
        // 1. 找 EcsWorldAuthoring
        // 2. 找 EcsSectionAuthoring
        // 3. 找每个 section 下的 EcsSpawnAuthoring
        // 4. 注册需要桥接的 Transform
        // 5. 输出 RuntimeAuthoringData
    }
}
```

输出数据：

```csharp
public sealed class RuntimeAuthoringData
{
    public EcsWorldConfig WorldConfig;
    public SectionBuildData[] Sections;
}
```

注意：

- `Collect` 是冷路径。
- 可以使用 Unity API。
- 不允许在 `System.Update` 中调用。

### SectionBuildData

```csharp
public struct SectionBuildData
{
    public int SectionId;
    public Bounds Bounds;
    public bool LoadOnStart;
    public SpawnRequest[] Spawns;
}
```

### SpawnRequest

```csharp
public struct SpawnRequest
{
    public int Count;
    public Position StartPosition;
    public Position Spacing;
    public Velocity InitialVelocity;
    public Health InitialHealth;
    public int TransformProxyStart;
    public bool HasTransformProxy;
}
```

`SpawnRequest` 是纯数据，不持有 Unity 引用。

---

## 七、SpawnCatalog

`SpawnCatalog` 是运行时生成目录，替代烘焙产物。

```csharp
public sealed class SpawnCatalog
{
    private SectionSpawnData[] _sections;

    public void Prewarm(World world);
    public SceneSectionRuntime SpawnSection(World world, int sectionId);
}
```

### SectionSpawnData

```csharp
public sealed class SectionSpawnData
{
    public int SectionId;
    public Bounds Bounds;
    public SpawnRequest[] Requests;
}
```

### Prewarm

预热先统计总量。

```csharp
public void Prewarm(World world)
{
    int movableCount = Count<Position, Velocity>();
    int linkedCount = Count<Position, TransformProxy>();

    world.PrewarmArchetype<Position, Velocity>(movableCount);
    world.PrewarmArchetype<Position, Velocity, Health>(movableCount);
    world.PrewarmArchetype<Position, TransformProxy>(linkedCount);
}
```

### SpawnSection

```csharp
public SceneSectionRuntime SpawnSection(World world, int sectionId)
{
    SectionSpawnData data = FindSection(sectionId);
    EntityList created = new EntityList(data.TotalCount);

    foreach (SpawnRequest request in data.Requests)
    {
        SpawnRequestEntities(world, request, created);
    }

    return new SceneSectionRuntime(sectionId, data.Bounds, created);
}
```

重点：

- 生成实体时用 `CreateMany`。
- 记录生成出来的 Entity，方便卸载。
- 不在每帧重新构建 SpawnCatalog。

---

## 八、ArchetypePrefab

`ArchetypePrefab` 替代 DOTS Entity Prefab。

### 数据结构

```csharp
public sealed class ArchetypePrefab
{
    public int ArchetypeId;
    public ComponentType[] Types;
    public byte[] DefaultData;
}
```

### 创建

```csharp
ArchetypePrefab enemy = world.CreatePrefab(
    new Position(),
    new Velocity(),
    new Health { Current = 100, Max = 100 });
```

### 实例化

```csharp
Entity e = world.Instantiate(enemy);
```

### 批量实例化

```csharp
world.InstantiateMany(enemy, count, (int i, Entity entity, ComponentWriter writer) =>
{
    writer.Set(new Position { X = i, Y = 0, Z = 0 });
});
```

优化点：

- Prefab 直接保存目标 Archetype。
- 默认数据可以 memcpy。
- 批量实例化可以按 Chunk 连续写。

---

## 九、SceneSectionRuntime

`SceneSectionRuntime` 替代 SubScene section。

```csharp
public sealed class SceneSectionRuntime
{
    public int SectionId { get; }
    public Bounds Bounds { get; }
    public bool Loaded { get; private set; }

    private Entity[] _entities;

    public void MarkLoaded(Entity[] entities);
    public void Unload(World world);
}
```

卸载：

```csharp
public void Unload(World world)
{
    for (int i = 0; i < _entities.Length; i++)
    {
        if (world.IsAlive(_entities[i]))
            world.Commands.Destroy(_entities[i]);
    }

    world.Playback();
    _entities = Array.Empty<Entity>();
    Loaded = false;
}
```

第一版用 `Entity[]` 足够。

后续优化：

```text
SectionId component
Chunk-level section list
Section allocator
按 Section 批量 Destroy
```

---

## 十、StreamingService

`StreamingService` 替代 SubScene streaming。

第一版先手动加载：

```csharp
streaming.LoadSection(1);
streaming.UnloadSection(1);
```

接口：

```csharp
public sealed class StreamingService
{
    private World _world;
    private SpawnCatalog _catalog;
    private Dictionary<int, SceneSectionRuntime> _loaded;

    public void LoadSection(int sectionId);
    public void UnloadSection(int sectionId);
}
```

后续距离加载：

```csharp
public void Update(Vector3 focusPosition)
{
    foreach (SectionSpawnData section in _catalog.Sections)
    {
        bool shouldLoad = section.Bounds.Contains(focusPosition);

        if (shouldLoad)
            LoadSection(section.SectionId);
        else
            UnloadSection(section.SectionId);
    }
}
```

预算加载：

```text
每帧最多创建 N 个实体
每帧最多卸载 N 个实体
大 Section 分多帧生成
```

这是替代 SubScene 后必须补的能力，否则大型场景加载会出现尖峰。

---

## 十一、TransformBridge

Unity 对象通过 Bridge 管理。

```csharp
public sealed class TransformBridge
{
    private Transform[] _items;
    private int[] _freeIds;

    public int Register(Transform transform);
    public Transform Get(int id);
    public void Unregister(int id);
}
```

ECS 组件只存：

```csharp
public struct TransformProxy : IComponentData
{
    public int Id;
}
```

同步系统：

```csharp
public sealed class TransformSyncSystem : EcsSystem
{
    private TransformBridge _bridge;
    private Query<Position, TransformProxy> _query;

    protected override void OnCreate()
    {
        _query = World.Query<Position, TransformProxy>();
    }

    protected override void OnUpdate(float dt)
    {
        _query.ForEachChunk((Entity* entities, Position* positions, TransformProxy* proxies, int count) =>
        {
            for (int i = 0; i < count; i++)
            {
                Transform t = _bridge.Get(proxies[i].Id);
                t.position = new Vector3(positions[i].X, positions[i].Y, positions[i].Z);
            }
        });
    }
}
```

性能边界：

- 只有有表现对象的实体才加 `TransformProxy`。
- 纯逻辑实体不进 TransformSyncSystem。
- 同步系统放在 Simulation 后。

---

## 十二、完整运行流程

启动：

```text
EcsRunner.Awake
-> new World
-> new TransformBridge
-> RuntimeAuthoringCollector.Collect(activeScene)
-> SpawnCatalogBuilder.Build(authoringData)
-> SpawnCatalog.Prewarm(world)
-> SystemPipeline.Add(...)
-> 加载 LoadOnStart sections
```

每帧：

```text
SystemPipeline.Update(dt)
-> Simulation systems
-> World.Playback after each system
-> StreamingService.Update(optional)
-> TransformSyncSystem
```

卸载 Section：

```text
StreamingService.UnloadSection(id)
-> SceneSectionRuntime.Unload(world)
-> Destroy section entities
-> Playback
-> Unregister Transform proxies
```

关闭：

```text
EcsRunner.OnDestroy
-> StreamingService.UnloadAll
-> SystemPipeline.Dispose
-> World.Dispose
-> TransformBridge.Clear
```

---

## 十三、性能边界

### 冷路径允许做的事

- 扫描 Authoring。
- 读取 Unity 场景。
- 注册 Transform。
- 构建 SpawnCatalog。
- 构建 ArchetypePrefab。
- 预热 Archetype。
- 加载 Section。

### 热路径禁止做的事

- `FindObjectsOfType`
- 反射读取组件字段。
- 从 MonoBehaviour 同步 ECS 数据。
- Entity 保存 Transform 引用。
- 每帧生成 SpawnCatalog。
- 每帧动态组合 ArchetypePrefab。
- 每帧逐实体 Add 多个组件。

### 热路径允许做的事

- `Query.ForEachChunk`
- 组件指针连续遍历。
- `CommandBuffer.Playback`
- Chunk 内 swap-remove。
- TransformProxy 到 TransformBridge 的数组查找。

---

## 十四、和 Unity Baker/SubScene 的对比

| 维度 | Baker/SubScene | 本方案 |
|---|---|---|
| 转换时机 | 编辑器烘焙 | 运行时启动/加载 |
| 学习成本 | 高 | 中低 |
| 调试直觉 | 间接 | 直接 |
| 大型静态场景 | 强 | 需要自己做分区 |
| 启动成本 | 低 | 有转换成本 |
| 每帧模拟性能 | 强 | 可接近，取决于 Chunk/Query 实现 |
| 多线程/Burst | 成熟 | 后续实现 |
| 可控性 | 跟随 DOTS | 完全自定义 |

结论：

```text
本方案不会自动超过 Unity ECS
但它能绕开 Baker/SubScene 的复杂工作流
并保持自研 ECS 的热路径纯净
```

---

## 十五、实施阶段

### 阶段 1：手写 SpawnCatalog

目标：

```text
不用任何 Authoring
直接手写 SpawnCatalog
验证 CreateMany 和 Prewarm
```

验收：

- 能批量生成 100k 实体。
- 不依赖 Baker/SubScene。
- GC Alloc 为 0 或可解释。

### 阶段 2：EcsSpawnAuthoring 收集

目标：

```text
从场景 MonoBehaviour 收集 SpawnRequest
生成 SpawnCatalog
```

验收：

- 修改 Inspector 后 Play 能生成不同实体。
- Collect 只在 Awake 或 LoadSection 发生。

### 阶段 3：TransformBridge

目标：

```text
把 Unity Transform 引用移到 Bridge
ECS 里只保存 TransformProxy
```

验收：

- ECS 组件无 UnityEngine.Object。
- TransformSyncSystem 能同步位置。

### 阶段 4：ArchetypePrefab

目标：

```text
常用模板可复用
InstantiateMany 直接写目标 Archetype
```

验收：

- 实例化不重新组合组件类型。
- 默认组件数据正确。

### 阶段 5：SceneSectionRuntime

目标：

```text
按 section 加载/卸载实体
```

验收：

- LoadSection 创建实体。
- UnloadSection 销毁实体。
- 旧 Entity 句柄失效。

### 阶段 6：StreamingService

目标：

```text
手动或按距离加载 Section
```

验收：

- 可控制加载预算。
- 大 Section 不造成单帧严重尖峰。

---

## 十六、最低可行方案

如果要最小实现，只做这些：

```text
EcsSpawnAuthoring
RuntimeAuthoringCollector
SpawnCatalog
TransformBridge
TransformProxy
CreateMany
```

先不做：

```text
ArchetypePrefab
SceneSectionRuntime
StreamingService
Editor DebugWindow
预算加载
```

最小闭环：

```text
场景里放 EcsSpawnAuthoring
-> Play
-> Collector 收集
-> SpawnCatalog 创建实体
-> MovementSystem 更新
-> TransformSyncSystem 同步
```

这个闭环跑通，就已经完成“无 Baker / 无 SubScene”的第一版。
