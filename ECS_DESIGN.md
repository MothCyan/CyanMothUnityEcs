# Unity 高性能 ECS 重构设计文档

> 目标：从零实现一个面向 Unity 的底层高性能 ECS。它不追求 DOTS 生态级扩展性，但核心存储、遍历和结构变更必须走 `Archetype + Chunk + SoA` 路线。可以牺牲组件数量上限、API 泛化能力、运行时动态性和部分易用扩展，换取更高的缓存命中、更少分支、更少 GC 和更可控的性能。

---

## 阅读辅助

专业名词解释已拆分到 [ECS_TERMS.md](ECS_TERMS.md)。

具体实施顺序已拆分到 [ECS_IMPLEMENTATION_ROADMAP.md](ECS_IMPLEMENTATION_ROADMAP.md)。

无 Baker / 无 SubScene 的专项替代方案见 [ECS_NO_BAKER_SUBSCENE.md](ECS_NO_BAKER_SUBSCENE.md)。

从使用层逐层下钻到底层的调用链见 [ECS_USAGE_LAYERED_FLOW.md](ECS_USAGE_LAYERED_FLOW.md)。

当前脚本、类、字段和 API 的逐项说明见 [ECS_API_REFERENCE.md](ECS_API_REFERENCE.md)。

主文档只保留 ECS 优化链路、创新方案和底层重构路线。

---

## 一、目标重新确认

这不是教学型 ECS，也不是简单 Sparse Set ECS。

真正目标是：

- 底层极致优化：数据按 Chunk 连续存储，组件按 SoA 布局。
- 热路径零托管分配：系统更新、查询遍历、组件访问不产生 GC。
- 查询无逐实体 Has 检查：通过 Archetype 预筛选，遍历时只扫匹配 Chunk。
- 结构变更延迟批处理：Add/Remove/Destroy 进入 CommandBuffer，统一回放。
- API 比 DOTS 简单：不引入复杂 baker、subscene、authoring 生态，先做运行时核心。
- 可牺牲扩展性：固定组件类型上限、固定 Query 泛型上限、固定 Chunk 策略都可以接受。

---

## 二、当前文档需要修正的问题

上一版文档把轻量化理解成了 “Sparse Set + 简单 Query”。这和现在确认的目标冲突。

### 1. Sparse Set 不符合底层极致优化目标

Sparse Set 的优点是实现简单、增删组件 O(1)、调试方便，但它的多组件 Query 会天然存在：

- 以某个组件池为驱动集合。
- 对其他组件池逐实体执行 `Has(entity)`。
- 跨数组跳转取组件。
- 多组件数据不保证同 Chunk 局部性。

这会导致热路径里有额外分支、间接访问和缓存不稳定。对于目标中的“极致优化”，它只能作为对照方案，不能作为核心方案。

### 2. 旧高性能方案的问题不是方向错，而是边界不清

原本的 Archetype + Chunk 方向是对的，但有几个需要重构的点：

- 一上来塞入太多高级能力：Jobs、Burst、Enableable、版本追踪、调试窗口都混在核心里。
- `Entity = int` 虽然快，但对外 API 类型安全差，容易误用。
- `ulong TypeMask` 固定 64 个组件类型，太窄；可以改成固定 128 位掩码，仍然很快。
- `delegate*`、函数指针、Burst 兼容不该作为 MVP API 的前提。
- `CreateEntity` 后逐个 `AddComponent` 会触发多次迁移，必须提供批量创建 API。
- Unity 引用型数据不应默认进入高性能组件池，否则会污染数据模型。
- CommandBuffer 需要作为第一版核心，而不是后期优化。

重构后的路线：保留 Archetype + Chunk，但把 MVP 边界收紧，只实现高性能核心闭环。

---

## 三、核心取舍

| 维度 | 决策 |
|---|---|
| 存储模型 | Archetype + Chunk |
| 组件布局 | SoA，每种组件在 Chunk 内连续 |
| Entity 对外表示 | `Entity { Id, Version }` |
| Entity 内部定位 | `id -> chunk/index/archetype/version` 平行数组 |
| Query | Archetype 预匹配，Chunk 级遍历 |
| 结构变更 | CommandBuffer 延迟回放 |
| 组件约束 | `unmanaged struct` 优先 |
| 组件类型上限 | 固定 128 个 |
| Query 泛型上限 | MVP 支持 1 到 4 个组件 |
| 多线程 | 第一版单线程，后续再做 Jobs |
| Burst | 第一版不强依赖，接口预留 |
| Sparse Set | 不作为核心，仅可用于编辑器调试索引或对照 Benchmark |

---

## 四、整体架构

```
World
├── TypeRegistry
│   └── ComponentType metadata
├── EntityStore
│   ├── versions[]
│   ├── chunks[]
│   ├── indices[]
│   └── archetypeIds[]
├── ArchetypeStore
│   ├── archetypes[]
│   ├── mask -> archetypeId
│   └── graph edges
├── ChunkAllocator
│   ├── aligned native blocks
│   └── free chunk lists
├── QueryCache
│   └── query mask -> matching archetypes
├── CommandBuffer
│   └── deferred structural changes
└── SystemPipeline
    └── deterministic sequential update
```

热路径目标：

- Query 不遍历所有 Entity。
- Query 不逐实体判断组件组合。
- Query 不分配临时 List。
- 组件访问不装箱、不反射、不 LINQ。
- 结构变更不打断当前遍历。

---

## 五、Entity 设计

对外保留类型安全，对内保持数组索引性能。

```csharp
public readonly struct Entity : IEquatable<Entity>
{
    public static readonly Entity Null = new Entity(0, 0);

    public readonly int Id;
    public readonly int Version;

    public bool IsNull => Id == 0;

    public Entity(int id, int version)
    {
        Id = id;
        Version = version;
    }

    public bool Equals(Entity other) => Id == other.Id && Version == other.Version;
    public override string ToString() => IsNull ? "Entity.Null" : $"Entity({Id}:{Version})";
}
```

EntityStore：

```csharp
internal sealed unsafe class EntityStore
{
    private int[] _versions;
    private IntPtr[] _chunks;
    private int[] _indices;
    private int[] _archetypeIds;
    private int[] _freeIds;
    private int _freeCount;
    private int _nextId;

    public Entity Create();
    public bool IsAlive(Entity entity);
    public void Destroy(Entity entity);

    public Chunk* GetChunk(Entity entity);
    public int GetIndex(Entity entity);
    public int GetArchetypeId(Entity entity);
    public void SetLocation(Entity entity, Chunk* chunk, int index, int archetypeId);
}
```

规则：

- `Id == 0` 保留给 Null。
- 销毁时 `Version++`，旧句柄立即失效。
- `_chunks[id] == IntPtr.Zero` 表示当前不存活。
- 热路径内部可以直接使用 `entity.Id`，不用额外哈希。

---

## 六、ComponentType 与掩码

组件必须是 unmanaged struct。

```csharp
public interface IComponentData { }
```

示例：

```csharp
public struct Position : IComponentData
{
    public float X;
    public float Y;
    public float Z;
}

public struct Velocity : IComponentData
{
    public float X;
    public float Y;
    public float Z;
}
```

ComponentType：

```csharp
public readonly struct ComponentType
{
    public readonly int Index;
    public readonly int Size;
    public readonly int Align;
    public readonly bool IsTag;

    public ComponentMask Mask => ComponentMask.FromIndex(Index);
}
```

固定 128 位掩码：

```csharp
public struct ComponentMask : IEquatable<ComponentMask>
{
    public ulong Lo;
    public ulong Hi;

    public static ComponentMask FromIndex(int index)
    {
        return index < 64
            ? new ComponentMask { Lo = 1UL << index }
            : new ComponentMask { Hi = 1UL << (index - 64) };
    }

    public bool ContainsAll(ComponentMask other)
    {
        return (Lo & other.Lo) == other.Lo &&
               (Hi & other.Hi) == other.Hi;
    }

    public bool Intersects(ComponentMask other)
    {
        return ((Lo & other.Lo) | (Hi & other.Hi)) != 0;
    }
}
```

取舍：

- 固定 128 个组件类型，避免动态 BitSet 分配。
- 组件注册发生在初始化阶段，热路径不查反射。
- 组件类型超过上限时直接报错，不做自动扩容。

---

## 七、Archetype 设计

Archetype 表示一组完全相同的组件组合。

```csharp
internal sealed unsafe class Archetype
{
    public int Id;
    public ComponentMask Mask;
    public ComponentType[] Types;

    public int EntitySize;
    public int ChunkCapacity;
    public int[] Offsets;
    public int[] Strides;

    public Chunk* FirstChunk;
    public Chunk* LastChunk;
    public Chunk* FirstFreeChunk;

    public int Version;

    public int[] AddEdges;
    public int[] RemoveEdges;
}
```

关键规则：

- `Types` 按 `ComponentType.Index` 排序，保证同组合得到同 Archetype。
- `Offsets[i]` 是组件数组在 Chunk 内的起始偏移。
- `Strides[i]` 是组件大小，对 tag 组件为 0。
- `AddEdges[typeIndex]` 指向添加该组件后的 Archetype。
- `RemoveEdges[typeIndex]` 指向移除该组件后的 Archetype。
- 边懒创建，第一次结构变更时生成。

---

## 八、Chunk 内存布局

默认 Chunk 大小为 16 KiB，64 字节对齐。

```
Chunk memory block
0x0000  ChunkHeader
        padding to 64-byte alignment
        Entity[Capacity]
        ComponentArray[0]
        ComponentArray[1]
        ...
        ComponentArray[N-1]
```

Header：

```csharp
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Chunk
{
    public int ArchetypeId;
    public int Count;
    public int Capacity;
    public int Sequence;
    public int Flags;
    public int Reserved;

    public Chunk* Next;
    public Chunk* Prev;
    public Chunk* NextFree;

    public int* ChangeVersions;
}
```

说明：

- Header 放在 native block 头部。
- `Entity[]` 存每个槽位对应的实体句柄或实体 Id。
- 组件数据区按 SoA 排列。
- `ChangeVersions` 第一版可以为 null，不启用增量过滤；后续按 Archetype 组件数量分配。
- `NextFree` 用于 Archetype 的可写 Chunk 链表。

容量计算：

```
headerSize = Align(sizeof(Chunk), 64)
available = ChunkSize - headerSize
perEntity = sizeof(Entity) + sum(aligned component sizes)
capacity = floor(available / perEntity)
```

实际布局要按组件对齐重新计算：

```csharp
offset = Align(headerSize + sizeof(Entity) * capacity, component.Align);
componentOffset[i] = offset;
offset += component.Size * capacity;
```

如果计算后超过 ChunkSize，则降低 capacity 重新计算，直到能放下。

---

## 九、ChunkAllocator

第一版使用 native 内存池，避免每个 Chunk 独立造成系统分配抖动。

```csharp
internal unsafe sealed class ChunkAllocator : IDisposable
{
    private const int ChunkSize = 16 * 1024;
    private const int Alignment = 64;

    private IntPtr[] _blocks;
    private Chunk* _freeList;

    public Chunk* Allocate(int archetypeId, int capacity);
    public void Free(Chunk* chunk);
    public void Dispose();
}
```

实现原则：

- 大块分配，例如一次分配 256 个 Chunk。
- 每个 Chunk 地址 64 字节对齐。
- 释放时只回收到 free list，不立刻归还系统。
- World Dispose 时统一释放所有 block。

---

## 十、World API

对外 API 要比 DOTS 简单，但底层仍然走高性能路径。

```csharp
public sealed unsafe class World : IDisposable
{
    public Entity Create();

    public Entity Create<T1>(T1 c1)
        where T1 : unmanaged, IComponentData;

    public Entity Create<T1, T2>(T1 c1, T2 c2)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData;

    public Entity Create<T1, T2, T3>(T1 c1, T2 c2, T3 c3)
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
        where T3 : unmanaged, IComponentData;

    public bool IsAlive(Entity entity);
    public void Destroy(Entity entity);

    public void Add<T>(Entity entity, T component)
        where T : unmanaged, IComponentData;

    public void Remove<T>(Entity entity)
        where T : unmanaged, IComponentData;

    public bool Has<T>(Entity entity)
        where T : unmanaged, IComponentData;

    public ref T Get<T>(Entity entity)
        where T : unmanaged, IComponentData;

    public void Set<T>(Entity entity, T component)
        where T : unmanaged, IComponentData;

    public Query<T1> Query<T1>()
        where T1 : unmanaged, IComponentData;

    public Query<T1, T2> Query<T1, T2>()
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData;

    public Query<T1, T2, T3> Query<T1, T2, T3>()
        where T1 : unmanaged, IComponentData
        where T2 : unmanaged, IComponentData
        where T3 : unmanaged, IComponentData;

    public CommandBuffer Commands { get; }
    public void Playback();
    public void Dispose();
}
```

重要规则：

- `Create<T1,T2,T3>` 直接进入最终 Archetype，避免多次迁移。
- 系统更新期间的 `Add/Remove/Destroy` 默认进入 CommandBuffer。
- `Get<T>` 是随机访问 API，不作为批量逻辑首选。
- 批量逻辑必须通过 Query 或 ChunkIterator。

---

## 十一、结构变更流程

结构变更统一走迁移：

```
Entity E 当前在 Archetype A: [Position, Velocity]
Add Health 后目标 Archetype B: [Position, Velocity, Health]

1. 通过 A.AddEdges[Health] 找 B
2. 没有边则创建 B，并建立 A <-> B 的边
3. 从 B 获取有空位 Chunk
4. 在目标 Chunk 分配新 slot
5. 拷贝 A 与 B 共有组件
6. 写入新增组件 Health
7. 更新 EntityStore 中 E 的 chunk/index/archetype
8. 从旧 Chunk swap-remove E
9. 如果旧 slot 被最后一个实体填充，更新被移动实体的位置
10. 旧 Chunk 为空则回收到 Archetype/Allocator
```

迁移函数：

```csharp
private void MoveEntity(
    Entity entity,
    Archetype source,
    Chunk* sourceChunk,
    int sourceIndex,
    Archetype target,
    ComponentType addedType,
    void* addedData);
```

第一版不做复杂命令合并，但要保证顺序确定。

---

## 十二、CommandBuffer

CommandBuffer 是核心，不是后期优化。

```csharp
public unsafe sealed class CommandBuffer
{
    private byte[] _buffer;
    private int _position;

    public void Create();
    public void Destroy(Entity entity);

    public void Add<T>(Entity entity, T component)
        where T : unmanaged, IComponentData;

    public void Remove<T>(Entity entity)
        where T : unmanaged, IComponentData;

    public void Playback(World world);
    public void Clear();
}
```

命令格式：

```
CommandHeader
├── CommandKind kind
├── int entityId
├── int entityVersion
├── int typeIndex
└── int payloadSize

Payload
└── raw component bytes
```

规则：

- `Playback` 时如果 Entity 已失效，跳过该命令。
- 同一实体同一组件的冲突命令按记录顺序执行。
- Query 遍历期间禁止立即结构变更，全部写入 CommandBuffer。
- `Playback` 只在系统之间或 `World.Update` 固定位置发生。

---

## 十三、Query 设计

Query 不使用 Sparse Set，不逐实体 Has。

查询由组件掩码匹配 Archetype：

```csharp
public readonly unsafe struct Query<T1, T2>
    where T1 : unmanaged, IComponentData
    where T2 : unmanaged, IComponentData
{
    private readonly World _world;
    private readonly int _queryId;

    public void ForEach(QueryAction<T1, T2> action);
    public void ForEachChunk(ChunkAction<T1, T2> action);
}
```

代理：

```csharp
public delegate void QueryAction<T1, T2>(Entity entity, ref T1 c1, ref T2 c2)
    where T1 : unmanaged, IComponentData
    where T2 : unmanaged, IComponentData;

public unsafe delegate void ChunkAction<T1, T2>(
    Entity* entities,
    T1* c1,
    T2* c2,
    int count)
    where T1 : unmanaged, IComponentData
    where T2 : unmanaged, IComponentData;
```

执行流程：

```
Query<Position, Velocity>
1. 构造 IncludeMask = Position | Velocity
2. QueryCache 找所有 Mask 包含 IncludeMask 的 Archetype
3. 遍历这些 Archetype 的 Chunk 链表
4. 从 Chunk layout 取 Position* 和 Velocity*
5. for i in Count 连续扫描
```

QueryCache：

```csharp
internal sealed class QueryCache
{
    private int _archetypeVersion;
    private QueryRecord[] _records;

    public int GetOrCreate(ComponentMask include, ComponentMask exclude);
    public ReadOnlySpan<int> GetMatchingArchetypes(int queryId);
    public void InvalidateWhenArchetypeCreated();
}
```

第一版支持：

- `With<T>` 固定泛型 Query。
- `Without<T>` 可选，但不进入第一阶段核心。
- `ForEachChunk` 作为最高性能入口。
- `ForEach` 作为开发便利入口。

---

## 十四、SystemPipeline

系统层保持简单，底层保持高性能。

```csharp
public abstract class EcsSystem
{
    protected World World { get; private set; }

    internal void Attach(World world)
    {
        World = world;
        OnCreate();
    }

    protected virtual void OnCreate() { }
    protected abstract void OnUpdate(float deltaTime);
    protected virtual void OnDestroy() { }
}
```

Pipeline：

```csharp
public sealed class SystemPipeline
{
    private EcsSystem[] _systems;
    private int _count;

    public void Add(EcsSystem system);
    public void Update(float deltaTime);
    public void Dispose();
}
```

更新规则：

```
for each system:
    system.OnUpdate(dt)
    world.Playback()
```

第一版不做依赖排序和并行调度。执行顺序由添加顺序决定。

---

## 十五、Unity 集成边界

高性能组件不直接保存 UnityEngine.Object 引用。

推荐分层：

```
Pure ECS World
├── Position
├── Rotation
├── Velocity
├── Health
└── RenderProxyId

Unity Bridge
├── RenderProxyId -> Transform
├── RenderProxyId -> GameObject
└── SyncSystem
```

如果第一版为了方便必须同步 Transform，使用外部映射表：

```csharp
public sealed class TransformBridge
{
    private Transform[] _transforms;

    public Transform Get(int proxyId);
    public int Register(Transform transform);
}
```

组件只存整数句柄：

```csharp
public struct TransformProxy : IComponentData
{
    public int Id;
}
```

这样不会把引用型对象塞进 Chunk 数据区。

---

## 十六、无 Baker / 无 SubScene 方案

目标：保留 Unity 场景编辑的便利性，但不使用 DOTS Baker 和 SubScene。编辑器里的对象只负责描述“要生成什么”，运行时由我们自己的 Bootstrap 转成 ECS 数据。

### 1. 总体替代链路

```
Unity Scene
-> EcsWorldAuthoring
-> EcsSpawnAuthoring / EcsSectionAuthoring
-> RuntimeAuthoringCollector
-> SpawnCatalog
-> ArchetypePrefab
-> World.PrewarmArchetype
-> World.CreateMany / World.Instantiate
-> SceneSectionRuntime
```

这条链路替代：

```text
Authoring MonoBehaviour
-> Baker
-> Entity Scene
-> SubScene streaming
```

### 2. 角色划分

| 模块 | 替代 DOTS 中的什么 | 责任 |
|---|---|---|
| `EcsWorldAuthoring` | SubScene 入口配置 | 指定要启动的 World、系统列表、初始场景配置 |
| `EcsSpawnAuthoring` | Baker 输入数据 | 在 Inspector 中描述要生成的 ECS 实体 |
| `RuntimeAuthoringCollector` | Baker | 运行时收集 Authoring 并转成纯数据 |
| `SpawnCatalog` | 烘焙产物 | 保存可实例化的 ECS 模板 |
| `ArchetypePrefab` | Entity Prefab | 保存 ArchetypeId 和默认组件数据 |
| `SceneSectionRuntime` | SubScene section | 管理一组实体的加载、启用、卸载 |
| `StreamingService` | SubScene streaming | 按距离、关卡状态或手动请求加载分区 |

### 3. Authoring 只保留描述数据

Authoring 是给 Unity Inspector 用的，不进入 ECS 热路径。

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

运行时转换成纯数据：

```csharp
public struct SpawnRequest
{
    public int Count;
    public Position StartPosition;
    public Position Spacing;
    public Velocity InitialVelocity;
    public Health InitialHealth;
    public int TransformProxyStart;
}
```

规则：

- Authoring 可以引用 Unity 对象。
- SpawnRequest 不引用 Unity 对象。
- ECS Component 不引用 Unity 对象。

### 4. RuntimeAuthoringCollector 替代 Baker

运行时收集场景里的 Authoring：

```csharp
public sealed class RuntimeAuthoringCollector
{
    public SpawnCatalog Collect(Scene scene, TransformBridge transformBridge)
    {
        // 找 EcsSpawnAuthoring
        // 转成 SpawnRequest
        // 注册 Transform 到 TransformBridge
        // 生成 SpawnCatalog
    }
}
```

执行时机：

```text
EcsRunner.Awake
-> RuntimeAuthoringCollector.Collect(activeScene)
-> SpawnCatalog.Build(world)
-> SpawnCatalog.Spawn(world)
```

优点：

- 没有 Baker 编译和烘焙流程。
- Play Mode 修改场景数据后更直观。
- 单元测试可以直接构造 SpawnCatalog，不依赖 Unity Baker。

代价：

- 启动时会有一次运行时转换成本。
- 大型场景需要自己做分区和预热。

### 5. SpawnCatalog 替代烘焙产物

`SpawnCatalog` 是运行时生成的纯 C# 数据目录。

```csharp
public sealed class SpawnCatalog
{
    private SpawnRequest[] _requests;

    public void Prewarm(World world);
    public void Spawn(World world);
}
```

预热：

```csharp
public void Prewarm(World world)
{
    world.PrewarmArchetype<Position, Velocity, Health>(TotalDynamicUnits);
    world.PrewarmArchetype<Position, TransformProxy>(TotalLinkedObjects);
}
```

生成：

```csharp
public void Spawn(World world)
{
    foreach (ref readonly SpawnRequest request in _requests)
    {
        world.CreateMany<Position, Velocity, Health>(request.Count, ...);
    }
}
```

优化点：

- 先统计总量，再预热 Archetype。
- 使用 `CreateMany` 连续写 Chunk。
- 避免逐 GameObject 转换时多次查 Archetype。

### 6. ArchetypePrefab 替代 Entity Prefab

常用实体模板用 `ArchetypePrefab` 表示。

```csharp
public sealed class ArchetypePrefab
{
    public int ArchetypeId;
    public byte[] DefaultComponentData;
    public ComponentType[] Types;
}
```

构建：

```csharp
ArchetypePrefab enemyPrefab = world.CreatePrefab(
    new Position(),
    new Velocity(),
    new Health { Current = 100, Max = 100 });
```

实例化：

```csharp
Entity enemy = world.Instantiate(enemyPrefab);
```

批量实例化：

```csharp
world.InstantiateMany(enemyPrefab, count, (int i, Entity e, ComponentWriter writer) =>
{
    writer.Set(new Position { X = i, Y = 0, Z = 0 });
});
```

优化点：

- Prefab 已经知道目标 Archetype。
- 实例化不需要重新组合组件类型。
- 默认组件数据可以直接 memcpy。

### 7. SceneSectionRuntime 替代 SubScene

不用 SubScene，但仍然需要“按区域管理实体”。

```csharp
public sealed class SceneSectionRuntime
{
    public int SectionId;
    public Bounds Bounds;
    public EntityRange Entities;
    public bool Loaded;

    public void Load(World world);
    public void Unload(World world);
}
```

`EntityRange` 可以先简单实现为实体数组：

```csharp
public struct EntityRange
{
    public Entity[] Entities;
}
```

后续优化成：

```text
SectionId component
Chunk-level section list
Archetype + section 分组
```

加载流程：

```text
StreamingService.RequestLoad(sectionId)
-> SceneSectionRuntime.Load(world)
-> SpawnCatalog.SpawnSection(sectionId)
-> 记录创建出来的 Entity
-> Loaded = true
```

卸载流程：

```text
StreamingService.RequestUnload(sectionId)
-> 遍历 section.Entities
-> World.Commands.Destroy(entity)
-> World.Playback()
-> 清空 EntityRange
-> Loaded = false
```

优化点：

- 不依赖 Unity SubScene。
- 可按距离、任务、房间、波次手动加载。
- 卸载逻辑完全可控。

### 8. StreamingService 替代 SubScene Streaming

```csharp
public sealed class StreamingService
{
    private SceneSectionRuntime[] _sections;

    public void Update(Vector3 cameraPosition, World world)
    {
        for (int i = 0; i < _sections.Length; i++)
        {
            bool shouldLoad = _sections[i].Bounds.Contains(cameraPosition);

            if (shouldLoad && !_sections[i].Loaded)
                _sections[i].Load(world);

            if (!shouldLoad && _sections[i].Loaded)
                _sections[i].Unload(world);
        }
    }
}
```

第一版可以更简单：

```text
手动 LoadSection(id)
手动 UnloadSection(id)
```

等核心稳定后再做距离流式加载。

### 9. 场景对象同步策略

有些实体需要 Unity 表现对象，有些不需要。

纯逻辑实体：

```text
Position
Velocity
Health
```

有表现对象的实体：

```text
Position
Velocity
TransformProxy
```

桥接流程：

```text
EcsSpawnAuthoring.LinkTransform = true
-> RuntimeAuthoringCollector 注册 Transform
-> 得到 proxyId
-> 创建 TransformProxy { Id = proxyId }
-> TransformSyncSystem 每帧写回 Transform
```

销毁时：

```text
Destroy entity
-> TransformProxyCleanupSystem
-> TransformBridge.Unregister(proxyId)
-> 可选 Destroy(gameObject) 或归还对象池
```

### 10. 与 Baker/SubScene 的取舍

| 维度 | DOTS Baker/SubScene | 本项目 Runtime Authoring |
|---|---|---|
| 数据转换时机 | 编辑器烘焙 | 运行时启动/加载 |
| 学习成本 | 高 | 低 |
| 调试直觉 | 间接，需要理解烘焙产物 | 直接，场景对象到 SpawnCatalog |
| 大型场景加载 | SubScene 支持完整 | 需要自己做 Section/Streaming |
| 启动性能 | 烘焙后更快 | 有运行时转换成本 |
| 可控性 | 跟随 DOTS 流程 | 完全自定义 |
| 适合阶段 | 大规模成熟管线 | 自研 ECS 初期和中型项目 |

结论：

```text
第一版干掉 Baker/SubScene 是合理的
但必须补上 RuntimeAuthoringCollector、SpawnCatalog、SceneSectionRuntime
否则只是把复杂度从 DOTS 隐藏流程转移到零散业务代码里
```

### 11. 性能边界：转换成本不能进入热路径

干掉 Baker/SubScene 不应该影响 ECS 模拟热路径。真正的性能边界是：

```text
Authoring / Collect / SpawnCatalog / Section Load
只能发生在启动、加载、刷怪、关卡切换这些冷路径

System Update / Query / ForEachChunk / Playback
必须只处理已经进入 Chunk 的纯 ECS 数据
```

也就是说，替代 Baker/SubScene 后，成本分布应该是：

| 阶段 | 是否允许额外开销 | 说明 |
|---|---|---|
| 启动收集 Authoring | 允许 | 扫描 MonoBehaviour、生成 SpawnCatalog |
| Section 加载 | 允许，但要可预算 | 创建实体、预热 Chunk、批量写入 |
| 每帧 Simulation Query | 不允许 | 只能 Chunk 连续遍历 |
| 每帧结构变更 Playback | 只允许 ECS 内部迁移成本 | 不允许扫描 Unity 场景 |
| 每帧 Transform 同步 | 允许桥接成本，但必须显式可控 | 只同步有 `TransformProxy` 的实体 |
| DebugWindow | 允许 | 调试工具不进入 Release 热路径 |

如果做到下面几点，Runtime Authoring 不会让模拟性能远远落后：

- `RuntimeAuthoringCollector` 只在启动或加载 section 时运行。
- `SpawnCatalog` 只保存纯数据请求，不在每帧查找 Unity 对象。
- 大量实体创建走 `CreateMany` / `InstantiateMany`，不逐个 Add 组件。
- `World.PrewarmArchetype` 在生成前执行，避免运行中频繁分配 Chunk。
- ECS Component 保持 unmanaged，不保存 `GameObject` / `Transform`。
- 每帧系统只用 `Query.ForEachChunk` 访问 Chunk。
- Transform 同步只针对需要表现对象的实体，并且通过 `TransformProxy.Id` 查桥接表。
- Section 加载做预算控制，例如每帧最多创建 N 个实体，避免单帧尖峰。

和 Unity ECS 的真实差距应该这样看：

| 场景 | 本项目方案 | Unity ECS/DOTS |
|---|---|---|
| 单线程 Chunk 遍历 | 可以接近，取决于 layout 和代码生成 | 很强，成熟且可 Burst |
| 多线程 Jobs | 第一版没有 | 强 |
| Burst 编译 | 第一版不依赖 | 强 |
| 大型场景离线烘焙 | 运行时转换有加载成本 | Baker/SubScene 更强 |
| 编辑器管线复杂度 | 低 | 高 |
| 调试直觉 | 直接 | 需要理解烘焙和 Entity Scene |

结论：

```text
不用 Baker/SubScene 会损失离线烘焙和成熟 Streaming 的优势
但不必然损失每帧 ECS 模拟性能

只要转换留在冷路径
热路径仍然是 Archetype + Chunk + SoA
就没有违背 ECS 的初衷
```

真正需要警惕的是这几种错误做法：

```text
每帧 FindObjectsOfType 收集 Authoring
每帧从 MonoBehaviour 读写 ECS 组件
每个实体保存 Transform 引用
每个实体一个 GameObject 必须同步
每帧临时生成 SpawnCatalog
结构变更时逐实体 Add 多个组件
```

这些才会让性能远远落后。

### 12. 实施顺序

不要一开始就做完整场景流式。

推荐顺序：

```text
1. EcsRunner 手动创建 World
2. 手写 SpawnCatalog 测试 CreateMany
3. EcsSpawnAuthoring -> RuntimeAuthoringCollector
4. TransformBridge + TransformProxy
5. ArchetypePrefab
6. SceneSectionRuntime 手动 Load/Unload
7. StreamingService 自动加载
8. DebugWindow 展示 Section/Spawn/TransformProxy 状态
```

验收：

- 不写 Baker 也能从场景生成 ECS 实体。
- 不用 SubScene 也能按 section 加载/卸载一批实体。
- ECS Chunk 中没有 UnityEngine.Object。
- Transform 同步通过 Bridge 完成。
- 大量实体生成走 `CreateMany` 或 `InstantiateMany`。

---

## 十七、区别于 Unity ECS 的优化方案

这里的 “Unity ECS” 指 Unity DOTS/Entities 的通用 ECS 方案。本项目不照搬它的完整生态，而是保留底层高性能思想，砍掉不服务当前目标的复杂度。

### 1. Runtime-first，而不是 Baking-first

Unity ECS 强依赖 Authoring/Baker/SubScene 工作流，适合大型内容管线，但学习和调试成本高。

本项目第一版选择 Runtime-first：

- 运行时直接创建 World。
- 运行时直接注册组件。
- 运行时直接创建实体和组件。
- Prefab/Authoring 后置，不作为核心前提。

优化点：

- 核心逻辑不依赖编辑器烘焙。
- Play Mode 里可以快速迭代。
- 单元测试可以直接构造 World，不需要场景和 Baker。

### 2. 固定上限换热路径简单

Unity ECS 需要服务非常泛化的场景，类型、查询、系统、世界、烘焙、调度都必须高度扩展。

本项目可以牺牲扩展性：

- 组件类型固定 128 个。
- Query 泛型第一版固定支持 1 到 4 个组件。
- Chunk 大小固定 16 KiB。
- 第一版单 World、单线程、确定顺序执行。

优化点：

- 掩码是固定 128 位，不需要动态 BitSet。
- Query 匹配逻辑更直接。
- 调试和 Benchmark 更稳定。
- 后续代码生成更容易。

### 3. Chunk 级 API 是一等入口

Unity ECS 更鼓励通过 `SystemAPI`、`IJobEntity`、`Entities.ForEach` 等方式间接访问数据。

本项目直接把 Chunk 级遍历作为最高性能入口：

```csharp
query.ForEachChunk((Entity* entities, Position* positions, Velocity* velocities, int count) =>
{
    for (int i = 0; i < count; i++)
    {
        positions[i].X += velocities[i].X * dt;
        positions[i].Y += velocities[i].Y * dt;
        positions[i].Z += velocities[i].Z * dt;
    }
});
```

优化点：

- 用户可以明确看到热循环。
- 没有逐实体 `HasComponent`。
- 没有隐藏调度层。
- 便于后续替换成 Burst/Jobs 版本。

### 4. CommandBuffer 更激进地服务结构变更合并

Unity ECS 的 ECB 偏通用，服务多线程、多系统、多播放点。

本项目第一版单线程，可以做更直接的合并：

```text
Destroy(E) 之后的 Add/Remove/Set 全部丢弃
Add<T>(E) 后 Remove<T>(E) 可以抵消
Add<T>(E, a) 后 Add<T>(E, b) 保留最后一次
Remove<T>(E) 后 Add<T>(E, b) 变成 Set/Add 最终状态
```

优化点：

- 减少无效 Archetype 迁移。
- 降低 Gameplay 中频繁状态切换的成本。
- Playback 更可预测。

第一版可以先不做完整合并，但命令格式必须给合并留位置。

### 5. 强制热冷数据拆分

Unity ECS 允许用户自由定义组件，框架不会强制拆分冷热字段。

本项目文档和示例要明确禁止大杂烩组件：

```csharp
// 不推荐
public struct UnitData : IComponentData
{
    public Position Position;
    public Velocity Velocity;
    public int Health;
    public int Level;
    public int NameId;
}
```

推荐：

```csharp
public struct Position : IComponentData { public float X, Y, Z; }
public struct Velocity : IComponentData { public float X, Y, Z; }
public struct Health : IComponentData { public int Current, Max; }
public struct NameId : IComponentData { public int Value; }
```

优化点：

- 移动系统只读写 Position/Velocity。
- 生命系统只读写 Health。
- 冷数据不会被热循环带进缓存。

### 6. UnityEngine.Object 不进入高性能 Chunk

Unity ECS 支持 Managed Component，但这会让性能模型复杂化。

本项目第一版不支持高性能 Chunk 内保存托管引用：

- 不存 `GameObject`。
- 不存 `Transform`。
- 不存 `MonoBehaviour`。
- 不存 `string`。
- 不存 `class`。

Unity 对象通过外部桥接表访问：

```text
TransformProxy.Id -> TransformBridge.Transforms[id]
```

优化点：

- Chunk 数据保持 blittable/unmanaged。
- 后续 Burst/Jobs 适配更顺。
- 数据和 Unity 对象生命周期解耦。

### 7. QueryCache 显式失效，而不是复杂安全句柄系统

Unity ECS 为了安全和并发维护了很多类型句柄、版本和依赖规则。

本项目第一版只维护：

```text
ArchetypeStore.Version
QueryCache.CachedArchetypeVersion
```

当新 Archetype 创建时：

```text
ArchetypeStore.Version++
QueryCache 在下一次执行时增量刷新或重建
```

优化点：

- 逻辑简单。
- 单线程下足够安全。
- 不引入复杂依赖系统。

### 8. Archetype 和 Chunk 预热

Unity ECS 通常依赖实际创建路径自然生成 Archetype。

本项目可以提供预热 API：

```csharp
world.PrewarmArchetype<Position, Velocity>(entityCapacity: 100_000);
world.PrewarmArchetype<Position, Velocity, Health>(entityCapacity: 10_000);
```

优化点：

- 避免战斗开始时第一次创建 Archetype 卡顿。
- 提前分配 Chunk。
- Benchmark 更稳定。

### 9. 调试工具服务性能模型

调试窗口不只是看 Entity 列表，而是围绕 Chunk/Archetype 性能模型展示：

- Archetype 数量。
- 每个 Archetype 的 Chunk 数。
- Chunk 利用率。
- 空 Chunk 数量。
- 结构变更次数。
- 迁移次数。
- Query 命中哪些 Archetype。
- 每个系统耗时。

这能直接暴露性能问题，而不是只展示数据内容。

### 10. 干掉 Baker 和 SubScene

Unity ECS 的 Baker/SubScene 解决的是“大型内容在编辑器中烘焙成高性能运行时数据”的问题，但它也带来一整套额外心智负担：

- 要理解 Authoring 和 Runtime Entity 的转换关系。
- 要维护 Baker 代码。
- 要处理 SubScene 的加载、卸载和烘焙状态。
- Play Mode 调试时经常需要关心数据到底来自场景、烘焙产物还是运行时。

本项目选择不用 Baker/SubScene。替代方案是：

```text
轻量 Authoring MonoBehaviour
-> Runtime Bootstrap 扫描/收集
-> SpawnCatalog 转成纯数据模板
-> World.PrewarmArchetype
-> World.CreateMany / InstantiatePrefab
-> SceneSectionRuntime 管理加载和卸载
```

核心原则：

- 编辑器数据只作为输入，不直接成为运行时 ECS 数据。
- 转换发生在运行时启动或加载点，而不是 Unity 烘焙流程。
- 所有运行时实体都由 `World` 显式创建。
- 场景分区用普通 C# 数据结构管理，不依赖 SubScene。

---

## 十八、完整 ECS 链路

这一节描述从 Unity 启动到 World 销毁的完整链路。它是后续实现的主干，所有模块都围绕这条链路服务。

### 1. 启动链路

```
Unity EcsRunner.Awake()
-> WorldBootstrap.CreateWorld()
-> TypeRegistry.Initialize()
-> ComponentType<T>.Warmup()
-> World 创建 EntityStore / ArchetypeStore / ChunkAllocator / QueryCache / CommandBuffer
-> SystemPipeline 创建
-> SystemPipeline.Add(system)
-> system.Attach(world)
-> system.OnCreate()
-> World.PrewarmArchetype(...)
-> 等待 Unity Update
```

核心数据：

- `TypeRegistry` 保存所有组件元数据。
- `EntityStore` 保存实体位置和版本。
- `ArchetypeStore` 保存组件组合到 Archetype 的映射。
- `ChunkAllocator` 持有 native chunk 内存。
- `QueryCache` 缓存 Query 到 Archetype 列表。
- `SystemPipeline` 保存系统执行顺序。

校验点：

- 类型注册必须发生在创建实体前。
- `ComponentType<T>.Index` 一旦分配，不允许改变。
- `World` 初始化后不能有 GC 热路径依赖。
- 常用 Archetype 应该可以预热，避免第一次战斗创建卡顿。

### 2. 组件注册链路

```
ComponentType<T>.Index 首次访问
-> TypeRegistry.Register<T>()
-> 检查 typeof(T) 是否重复注册
-> 检查 T 是否 unmanaged / IComponentData
-> 分配 typeIndex
-> 计算 size
-> 计算 alignment
-> 创建 ComponentMask
-> 写入 TypeRegistry.Types[typeIndex]
-> 写入 TypeRegistry.TypeToIndex[typeof(T)]
-> 缓存到 ComponentType<T>.Type
```

核心数据：

- `ComponentType.Index`
- `ComponentType.Size`
- `ComponentType.Align`
- `ComponentType.Mask`
- `ComponentType.IsTag`

热路径要求：

- `World.Query<T>` 不做反射查找。
- `World.Get<T>` 不做字典查找。
- 泛型静态缓存必须承接常用访问。
- 超过 128 个组件类型直接报错，不自动扩容。

失败处理：

- 重复注册同类型直接返回已有类型。
- 非 unmanaged 组件直接抛异常。
- `typeIndex >= 128` 直接抛异常。

### 3. 创建实体链路

推荐路径：

```
World.Create<T1,T2,T3>(c1, c2, c3)
-> 读取 ComponentType<T1/T2/T3>
-> 构造 ComponentMask
-> ArchetypeStore.GetOrCreate(mask, types)
   -> 若不存在，创建 Archetype
   -> 计算 Chunk layout
   -> ArchetypeStore.Version++
   -> QueryCache 标记失效
-> EntityStore.Allocate()
   -> 从 freeIds 复用或 nextId++
   -> version 保持当前值
   -> 返回 Entity(id, version)
-> Archetype.GetWritableChunk()
   -> 优先 FirstFreeChunk
   -> 没有则 ChunkAllocator.Allocate()
-> slot = chunk.Count++
-> 写 chunk Entity 区：entities[slot] = entity
-> 写组件数组：
   componentPtr<T1>(chunk)[slot] = c1
   componentPtr<T2>(chunk)[slot] = c2
   componentPtr<T3>(chunk)[slot] = c3
-> EntityStore.SetLocation(entity, chunk, slot, archetypeId)
-> 若 chunk.Count == chunk.Capacity，从 free chunk 链移除
-> 返回 Entity
```

不推荐路径：

```
Create()
-> Add<T1>
-> Add<T2>
-> Add<T3>
```

原因是会触发多次 Archetype 迁移。

优化点：

- 批量创建 API 必须优先实现。
- `CreateMany<T1,T2>` 后续可以一次取 Chunk 空位，连续写入。
- 常用 Archetype 应预热，减少运行时 `GetOrCreate`。

### 4. Query 构建链路

```
World.Query<T1,T2>()
-> 读取 ComponentType<T1/T2>
-> includeMask = T1.Mask | T2.Mask
-> excludeMask = default
-> QueryCache.GetOrCreate(includeMask, excludeMask)
   -> 若 query record 不存在，创建 QueryRecord
   -> 若 QueryRecord.ArchetypeVersion != ArchetypeStore.Version，刷新匹配 Archetype
   -> 匹配规则：
      archetype.Mask.ContainsAll(includeMask)
      && !archetype.Mask.Intersects(excludeMask)
-> 返回 Query<T1,T2>(world, queryId)
```

热路径要求：

- Query 对象本身是轻量结构。
- 每帧重复 Query 不重复分配。
- 新 Archetype 出现后再刷新匹配列表。
- QueryCache 的匹配列表用数组池或内部可复用数组，不能每帧 new List。

缓存失效规则：

```
ArchetypeStore 创建新 Archetype
-> ArchetypeStore.Version++
-> QueryCache 不立刻重建所有 Query
-> Query 下次执行时发现版本变化
-> 只刷新当前 QueryRecord
```

### 5. Query 执行链路

```
Query<T1,T2>.ForEachChunk(action)
-> QueryCache.GetMatchingArchetypes(queryId)
-> for each archetypeId
   -> archetype = ArchetypeStore[archetypeId]
   -> offset1 = archetype.GetOffset(T1.Index)
   -> offset2 = archetype.GetOffset(T2.Index)
   -> for chunk = archetype.FirstChunk; chunk != null; chunk = chunk->Next
      -> if chunk->Count == 0 continue
      -> entities = ChunkData.GetEntities(chunk)
      -> c1 = (T1*)ChunkData.GetComponentPtr(chunk, offset1)
      -> c2 = (T2*)ChunkData.GetComponentPtr(chunk, offset2)
      -> action(entities, c1, c2, chunk->Count)
```

热路径要求：

- 不逐实体判断组件是否存在。
- 不跨组件池跳转。
- 不创建临时集合。
- 不调用 LINQ/foreach 枚举器。
- Offset 应在 QueryRecord 或 ArchetypeQueryMatch 中缓存，避免每帧重复查找。

便利版链路：

```
Query<T1,T2>.ForEach(action)
-> 内部调用 ForEachChunk
-> for i in count
   -> action(entity[i], ref c1[i], ref c2[i])
```

最高性能系统应优先使用 `ForEachChunk`。

### 6. 系统更新链路

```
Unity EcsRunner.Update()
-> dt = Time.deltaTime
-> SystemPipeline.Update(dt)
   -> for i in system count
      -> world.BeginSystem(system)
      -> system.OnUpdate(dt)
      -> world.EndSystem(system)
      -> world.Playback()
-> World.EndFrame()
```

规则：

- 系统顺序确定。
- 每个系统后播放命令，避免命令积累过久。
- 后续可以增加 UpdateGroup，但第一版不做复杂依赖排序。

状态标记：

- `World.IsIterating`：Query 遍历时为 true。
- `World.CurrentSystem`：调试和统计用。
- `World.FrameVersion`：组件写入版本和统计用。
- `World.Stats`：记录系统耗时、命令数、迁移数。

### 7. 组件读写链路

批量读写：

```
Query.ForEachChunk
-> Archetype match
-> Chunk pointer
-> component pointer
-> for loop
-> 写入组件数据
-> 标记 chunk component change version
```

随机读写：

```
World.Get<T>(Entity)
-> EntityStore.Validate(entity)
   -> id 范围检查
   -> version 检查
   -> chunk != null 检查
-> chunk = EntityStore.GetChunk(entity)
-> index = EntityStore.GetIndex(entity)
-> archetype = ArchetypeStore[chunk->ArchetypeId]
-> offset = archetype.GetOffset(ComponentType<T>.Index)
-> ptr = ChunkData.GetComponentPtr<T>(chunk, offset)
-> return ref ptr[index]
```

原则：

- 批量逻辑优先 Query。
- `Get<T>` 用于少量随机访问，不用于大规模循环。
- `Set<T>` 可以复用 `Get<T>`，但要额外标记 ChangeVersion。
- 第一版可以先只做版本字段，不启用过滤。

### 8. 结构变更链路

系统中调用：

```
World.Add/Remove/Destroy
-> CommandBuffer.Append(command)
-> command 记录 entity id/version/type/payload
```

回放：

```
World.Playback
-> CommandBuffer.BeginPlayback()
-> 可选：CommandBuffer.Compact()
-> for each command
   -> ValidateEntity(entity)
   -> switch command.kind
      -> AddComponent
      -> RemoveComponent
      -> DestroyEntity
      -> CreateEntity
-> CommandBuffer.Clear()
```

AddComponent 迁移链路：

```
Add<T>(E, data)
-> sourceChunk = EntityStore.GetChunk(E)
-> sourceArch = ArchetypeStore[sourceChunk->ArchetypeId]
-> if sourceArch.Has(T) then Set<T>(E, data), return
-> targetArch = sourceArch.GetAddEdge(T)
   -> 若 edge 不存在，ArchetypeStore.GetOrCreate(sourceMask | T.Mask)
-> targetChunk = targetArch.GetWritableChunk()
-> targetIndex = targetChunk->Count++
-> 拷贝 sourceArch 与 targetArch 的共有组件
-> 写入 T data 到目标组件数组
-> 写入 Entity 到目标 Entity 数组
-> EntityStore.SetLocation(E, targetChunk, targetIndex, targetArch.Id)
-> sourceChunk.RemoveAtSwapBack(sourceIndex)
   -> 如果末尾实体被移动到 sourceIndex
   -> EntityStore.SetLocation(movedEntity, sourceChunk, sourceIndex, sourceArch.Id)
-> 更新 chunk free/full 链表
```

RemoveComponent 迁移链路：

```
Remove<T>(E)
-> sourceArch = 当前 Archetype
-> if !sourceArch.Has(T) return
-> targetArch = sourceArch.GetRemoveEdge(T)
-> 分配目标 slot
-> 拷贝除 T 外的共有组件
-> 更新 EntityStore
-> 旧 Chunk swap-remove
```

CommandBuffer 合并策略：

```text
Destroy(E) 后续命令丢弃
Add<T>(E, a) + Add<T>(E, b) 保留 b
Add<T>(E) + Remove<T>(E) 抵消
Remove<T>(E) + Add<T>(E, b) 变成最终 Add/Set
```

第一版可以只保证顺序正确，第二版再做合并。

### 9. 销毁实体链路

```
Destroy(Entity)
-> CommandBuffer.AppendDestroy(entity)
-> World.Playback()
-> ValidateEntity(entity)
-> chunk = EntityStore.GetChunk(entity)
-> index = EntityStore.GetIndex(entity)
-> arch = ArchetypeStore[chunk->ArchetypeId]
-> lastIndex = chunk->Count - 1
-> if index != lastIndex
   -> 将 lastIndex 的 Entity 移到 index
   -> 对 arch.Types 中每个非 tag 组件执行 memcpy slot
   -> EntityStore.SetLocation(movedEntity, chunk, index, arch.Id)
-> chunk->Count--
-> EntityStore.Release(entity)
   -> chunks[id] = null
   -> indices[id] = 0
   -> archetypeIds[id] = 0
   -> versions[id]++
   -> freeIds.Push(id)
-> 如果 chunk 为空，挂到空闲 Chunk 链或归还 ChunkAllocator
```

要求：

- 旧 Entity 句柄失效。
- 空 Chunk 进入 free list。
- Query 不会再访问销毁实体。
- 销毁不需要逐个调用 RemoveComponent。

### 10. Unity 同步链路

```
Unity Update
-> ECS Simulation Systems
-> World.Playback()
-> Unity Bridge Systems
   -> TransformSyncSystem Query<Position, TransformProxy>
   -> TransformBridge.Get(proxy.Id)
   -> transform.position = position
-> 可选 Render/Animation/Physics 同步
```

规则：

- UnityEngine.Object 不进入 Chunk。
- ECS 可以独立于场景运行测试。
- Transform 同步是桥接系统，不是 ECS 核心。

数据边界：

```text
ECS Chunk: TransformProxy { int Id }
Bridge: int Id -> Transform
Unity: Transform / GameObject / Animator
```

这样 ECS 数据仍然保持 unmanaged。

### 11. 调试与统计链路

每帧收集：

```text
World entity count
Archetype count
Chunk count
Chunk utilization
Command count
Migration count
Query execution time
System execution time
GC alloc
```

调试窗口应该按性能问题组织，而不是只按实体列表组织。

采集位置：

```
World.BeginFrame
-> 重置 frame stats
SystemPipeline.Update
-> 记录每个系统耗时
World.Playback
-> 记录 command count / migration count
Query.ForEachChunk
-> 记录 query count / chunk count / entity count
World.EndFrame
-> 汇总 GC alloc / chunk utilization
```

调试视图：

- System 耗时排行。
- Query 命中 Archetype 列表。
- Archetype Chunk 利用率。
- CommandBuffer 回放成本。
- 结构迁移热点组件。

### 12. 关闭链路

```
EcsRunner.OnDestroy
-> SystemPipeline.Dispose()
   -> 逆序调用 system.OnDestroy()
-> World.Playback()
-> CommandBuffer.Clear()
-> ArchetypeStore.Clear()
-> QueryCache.Clear()
-> ChunkAllocator.Dispose()
   -> 释放所有 native block
-> EntityStore.Clear()
-> World 标记 disposed
```

要求：

- native block 全部释放。
- CommandBuffer 清空。
- 不留下 Unity Play Mode 泄漏。

### 13. 一帧完整执行顺序

把上面的链路合并成一帧，就是：

```
EcsRunner.Update()
-> World.BeginFrame()
-> SystemPipeline.Update(dt)
   -> MovementSystem.OnUpdate(dt)
      -> Query<Position, Velocity>.ForEachChunk
      -> 写 Position
   -> World.Playback()
   -> DamageSystem.OnUpdate(dt)
      -> Query<Health>.ForEachChunk
      -> 可能写 Destroy 命令
   -> World.Playback()
      -> 执行 Destroy
      -> Chunk swap-remove
      -> EntityStore version++
   -> SpawnSystem.OnUpdate(dt)
      -> 写 Create 命令或直接批量 Create
   -> World.Playback()
      -> 分配 Entity
      -> 写入目标 Archetype Chunk
-> BridgePipeline.Update(dt)
   -> TransformSyncSystem.OnUpdate(dt)
   -> 写回 Unity Transform
-> World.EndFrame()
-> DebugStats.Flush()
```

这一帧里最重要的性能边界：

- Simulation 系统只访问 ECS Chunk。
- Unity 同步放在模拟之后。
- 结构变更只在 Playback 发生。
- Query 不因结构变更中途失效。
- 销毁实体通过 Version 防止旧句柄误用。

### 14. 模块依赖方向

实现时依赖方向必须单向：

```
Unity Layer
-> Systems
-> World
-> Query / Commands
-> ArchetypeStore / EntityStore
-> Chunk / Allocator
-> UnsafeUtil
```

禁止反向依赖：

- Core 不引用 UnityEngine。
- Storage 不引用 Systems。
- Query 不负责结构变更。
- CommandBuffer 不直接持有系统。
- Chunk 不知道组件泛型类型，只处理 offset/size。

这能保证后续替换 Jobs/Burst 或调试工具时，不会把核心链路搅乱。

---

## 十九、完整 ECS 使用链路

这一节从使用者视角描述完整流程：一个玩法开发者如何从零接入 ECS、定义数据、编写系统、创建实体、运行更新、处理结构变更、同步 Unity 对象并调试性能。

### 1. 创建 ECS 目录和基础入口

项目建议先建立固定目录：

```
Assets/Scripts/ECS/
├── Components/
├── Systems/
├── Bootstrap/
├── UnityBridge/
└── Debugging/
```

使用者第一步只需要关心：

```text
Components 里放数据
Systems 里放行为
Bootstrap 里创建 World 和系统
UnityBridge 里做 Transform/GameObject 同步
```

### 2. 定义组件

组件只写数据，不写逻辑。

```csharp
public struct Position : IComponentData
{
    public float X;
    public float Y;
    public float Z;
}

public struct Velocity : IComponentData
{
    public float X;
    public float Y;
    public float Z;
}

public struct Health : IComponentData
{
    public int Current;
    public int Max;
}
```

使用规则：

- 高频访问字段拆成独立组件。
- 不在组件里放 `GameObject`、`Transform`、`MonoBehaviour`、`string`。
- 组件优先保持 blittable/unmanaged。
- 组件名表达数据含义，不表达行为。

不推荐：

```csharp
public struct PlayerData : IComponentData
{
    public Position Position;
    public Velocity Velocity;
    public Health Health;
    public int Exp;
    public int NameId;
}
```

推荐：

```csharp
Position
Velocity
Health
Experience
NameId
```

### 3. 编写系统

系统只写行为，不持有实体数据所有权。

```csharp
public sealed class MovementSystem : EcsSystem
{
    private Query<Position, Velocity> _query;

    protected override void OnCreate()
    {
        _query = World.Query<Position, Velocity>();
    }

    protected override void OnUpdate(float dt)
    {
        _query.ForEachChunk((Entity* entities, Position* positions, Velocity* velocities, int count) =>
        {
            for (int i = 0; i < count; i++)
            {
                positions[i].X += velocities[i].X * dt;
                positions[i].Y += velocities[i].Y * dt;
                positions[i].Z += velocities[i].Z * dt;
            }
        });
    }
}
```

使用规则：

- 热路径系统优先缓存 Query。
- 热路径系统优先使用 `ForEachChunk`。
- 少量逻辑或调试工具可以使用 `ForEach`。
- 系统中需要 Add/Remove/Destroy 时，写入 `World.Commands`。

### 4. 创建 World 和注册系统

Unity 场景里挂一个 `EcsRunner`。

```csharp
public sealed class GameEcsRunner : MonoBehaviour
{
    private World _world;
    private SystemPipeline _pipeline;

    private void Awake()
    {
        _world = new World();
        RegisterComponents(_world);
        Prewarm(_world);

        _pipeline = new SystemPipeline(_world);
        _pipeline.Add(new MovementSystem());
        _pipeline.Add(new DamageSystem());
        _pipeline.Add(new DeathSystem());
        _pipeline.Add(new TransformSyncSystem());
    }

    private void Update()
    {
        _pipeline.Update(Time.deltaTime);
    }

    private void OnDestroy()
    {
        _pipeline.Dispose();
        _world.Dispose();
    }
}
```

注册和预热：

```csharp
private static void RegisterComponents(World world)
{
    world.Register<Position>();
    world.Register<Velocity>();
    world.Register<Health>();
    world.Register<TransformProxy>();
}

private static void Prewarm(World world)
{
    world.PrewarmArchetype<Position, Velocity>(100_000);
    world.PrewarmArchetype<Position, Velocity, Health>(10_000);
}
```

使用规则：

- 组件注册在实体创建前完成。
- 常用组合提前预热。
- 系统添加顺序就是第一版执行顺序。

### 5. 创建实体

推荐使用一次性创建 API。

```csharp
Entity entity = world.Create(
    new Position { X = 0, Y = 0, Z = 0 },
    new Velocity { X = 1, Y = 0, Z = 0 },
    new Health { Current = 100, Max = 100 });
```

批量创建：

```csharp
for (int i = 0; i < 100_000; i++)
{
    world.Create(
        new Position { X = i, Y = 0, Z = 0 },
        new Velocity { X = 1, Y = 0, Z = 0 });
}
```

后续优化 API：

```csharp
world.CreateMany<Position, Velocity>(100_000, (int i, out Position p, out Velocity v) =>
{
    p = new Position { X = i, Y = 0, Z = 0 };
    v = new Velocity { X = 1, Y = 0, Z = 0 };
});
```

使用规则：

- 优先 `Create<T1,T2,T3>`。
- 少用 `Create()` 后连续 `Add<T>`。
- 大量生成使用 `CreateMany`。

### 6. 查询和更新实体

开发便利版：

```csharp
World.Query<Position, Velocity>().ForEach((Entity entity, ref Position position, ref Velocity velocity) =>
{
    position.X += velocity.X * dt;
    position.Y += velocity.Y * dt;
    position.Z += velocity.Z * dt;
});
```

高性能版：

```csharp
World.Query<Position, Velocity>().ForEachChunk((Entity* entities, Position* positions, Velocity* velocities, int count) =>
{
    for (int i = 0; i < count; i++)
    {
        positions[i].X += velocities[i].X * dt;
        positions[i].Y += velocities[i].Y * dt;
        positions[i].Z += velocities[i].Z * dt;
    }
});
```

使用规则：

- 原型期可以用 `ForEach`。
- 性能敏感系统改成 `ForEachChunk`。
- Query 只匹配 Archetype，不逐实体 Has。

### 7. 随机访问单个实体

少量随机访问可以使用：

```csharp
if (world.IsAlive(entity) && world.Has<Health>(entity))
{
    ref Health health = ref world.Get<Health>(entity);
    health.Current -= 10;
}
```

使用规则：

- `Get<T>` 不用于大规模循环。
- 外部长期保存 Entity 时必须依赖 Version 校验。
- UI、锁定目标、单体交互可以使用随机访问。

### 8. 添加和移除组件

系统里不要直接破坏当前 Query，统一写命令：

```csharp
public sealed class DamageSystem : EcsSystem
{
    private Query<Health> _query;

    protected override void OnCreate()
    {
        _query = World.Query<Health>();
    }

    protected override void OnUpdate(float dt)
    {
        _query.ForEach((Entity entity, ref Health health) =>
        {
            if (health.Current <= 0)
            {
                World.Commands.Add(entity, new DeadTag());
            }
        });
    }
}
```

移除组件：

```csharp
World.Commands.Remove<Velocity>(entity);
```

销毁实体：

```csharp
World.Commands.Destroy(entity);
```

使用规则：

- Add/Remove 会触发 Archetype 迁移。
- 高频开关状态后续用 Enableable，不要频繁 Add/Remove。
- 每个系统后 `SystemPipeline` 自动 `World.Playback()`。

### 9. 处理生命周期事件

第一版不内建复杂事件系统。生命周期可以用 Tag 或 Command 表达。

出生：

```csharp
Entity e = World.Create(
    new Position(),
    new Velocity(),
    new SpawnedThisFrameTag());
```

死亡：

```csharp
World.Commands.Add(entity, new DeadTag());
```

清理：

```csharp
public sealed class CleanupSystem : EcsSystem
{
    protected override void OnUpdate(float dt)
    {
        World.Query<DeadTag>().ForEach((Entity entity, ref DeadTag dead) =>
        {
            World.Commands.Destroy(entity);
        });
    }
}
```

使用规则：

- Tag 组件用于短期状态。
- 真正频繁开关的状态后续改 Enableable。
- 清理系统通常放在帧末。

### 10. Unity 对象桥接

ECS 组件只保存 proxy id。

```csharp
public struct TransformProxy : IComponentData
{
    public int Id;
}
```

桥接表保存 Unity 引用：

```csharp
public sealed class TransformBridge
{
    private Transform[] _items;

    public int Register(Transform transform);
    public Transform Get(int id);
    public void Unregister(int id);
}
```

同步系统：

```csharp
public sealed class TransformSyncSystem : EcsSystem
{
    private readonly TransformBridge _bridge;
    private Query<Position, TransformProxy> _query;

    public TransformSyncSystem(TransformBridge bridge)
    {
        _bridge = bridge;
    }

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
                Transform transform = _bridge.Get(proxies[i].Id);
                transform.position = new Vector3(positions[i].X, positions[i].Y, positions[i].Z);
            }
        });
    }
}
```

使用规则：

- Unity 引用不进入 Chunk。
- 桥接系统放在模拟系统之后。
- 纯逻辑测试可以不创建任何 Unity 对象。

### 11. 调试使用链路

开发时打开 `EcsDebugWindow`：

```text
Window -> ECS -> Debugger
```

调试路径：

```text
看 System 耗时
-> 找最慢 Query
-> 看 Query 命中的 Archetype
-> 看 Archetype 的 Chunk 利用率
-> 看 CommandBuffer 迁移次数
-> 看是否有频繁 Add/Remove
```

需要展示：

- Entity 总数。
- Archetype 总数。
- Chunk 总数。
- 每个 Archetype 的组件组合。
- 每个 Archetype 的 Chunk 利用率。
- 每帧结构变更数量。
- 每帧 GC Alloc。
- 每个系统耗时。

### 12. 性能使用链路

每新增一个核心系统，都要跑三类检查：

```text
功能正确性
-> 是否 Query 到正确实体
-> 是否结构变更后数据仍正确

内存正确性
-> 是否无 GC Alloc
-> 是否无 native memory leak

性能正确性
-> 是否线性扩展
-> 是否 Chunk 利用率合理
-> 是否结构变更次数可控
```

推荐 Benchmark 流程：

```text
1,000 entities
-> 10,000 entities
-> 100,000 entities
-> 500,000 entities
```

每档记录：

- Update 耗时。
- Playback 耗时。
- Query 耗时。
- Chunk 数量。
- GC Alloc。
- 迁移次数。

### 13. 一次完整玩法使用示例

```
1. 定义 Position / Velocity / Health / TransformProxy
2. 编写 MovementSystem / DamageSystem / DeathSystem / TransformSyncSystem
3. 在 GameEcsRunner.Awake 创建 World
4. 注册组件类型
5. 预热常用 Archetype
6. 添加系统到 Pipeline
7. SpawnSystem 或场景脚本批量 Create 实体
8. 每帧 Pipeline.Update
9. MovementSystem 通过 ForEachChunk 更新 Position
10. DamageSystem 写 DeadTag 或 Destroy 命令
11. World.Playback 执行结构变更
12. TransformSyncSystem 写回 Unity Transform
13. EcsDebugWindow 查看性能统计
14. OnDestroy 释放 Pipeline 和 World
```

最小使用闭环：

```text
Component
-> System
-> World
-> Create Entity
-> Query Update
-> CommandBuffer Playback
-> Unity Bridge
-> Dispose
```

---

## 二十、用户开发便捷度优化方案

高性能 ECS 容易把使用体验做得很硬。这个项目的目标不是复刻 DOTS 的复杂体验，而是在不破坏底层性能模型的前提下，提供更顺手的开发入口。

核心原则：

```text
便捷 API 可以存在
但便捷 API 必须编译/转发到底层高性能路径
不能在热路径里引入反射、装箱、LINQ、临时分配
```

### 1. 组件定义保持简单

用户只需要写：

```csharp
public struct Position : IComponentData
{
    public float X;
    public float Y;
    public float Z;
}
```

不要求用户写：

- TypeIndex。
- Size。
- Alignment。
- Mask。
- Serializer。
- Baker。

这些由 `TypeRegistry` 和后续 Source Generator 自动处理。

### 2. 提供开发便利版 Query

给用户两个层级：

开发便利版：

```csharp
World.Query<Position, Velocity>().ForEach((Entity e, ref Position p, ref Velocity v) =>
{
    p.X += v.X * dt;
});
```

高性能版：

```csharp
World.Query<Position, Velocity>().ForEachChunk((Entity* entities, Position* p, Velocity* v, int count) =>
{
    for (int i = 0; i < count; i++)
    {
        p[i].X += v[i].X * dt;
    }
});
```

规则：

- 原型期默认用 `ForEach`。
- 性能系统切到 `ForEachChunk`。
- 两者共用同一份 QueryCache 和 Archetype 匹配结果。
- `ForEach` 只是 `ForEachChunk` 的包装，不走另一套存储。

### 3. 提供批量创建辅助 API

用户不应该手写复杂 Chunk 填充逻辑。提供：

```csharp
World.CreateMany<Position, Velocity>(count, (int i, out Position p, out Velocity v) =>
{
    p = new Position { X = i };
    v = new Velocity { X = 1 };
});
```

底层执行：

```text
一次获取目标 Archetype
-> 连续获取 Chunk 空位
-> 连续写 Entity
-> 连续写组件数组
-> 批量更新 EntityStore
```

优化点：

- 用户写起来像普通批量初始化。
- 底层避免 N 次 Archetype 查找。
- 底层避免 N 次可写 Chunk 查询。

### 4. 提供 ArchetypePrefab

常用实体组合可以定义成运行时 prefab。

```csharp
ArchetypePrefab bulletPrefab = World.CreatePrefab(
    new Position(),
    new Velocity(),
    new Damage(),
    new Lifetime());

Entity bullet = World.Instantiate(bulletPrefab);
```

底层执行：

```text
Prefab 保存 ArchetypeId
Prefab 保存默认组件数据 blob
Instantiate 直接进入目标 Archetype
按 blob 写默认组件
```

优化点：

- 用户不用重复写组件组合。
- 避免 Create 后多次 Add。
- 可以预热 Chunk。

### 5. 提供 System 模板基类

普通系统：

```csharp
public abstract class EcsSystem
{
    protected World World { get; private set; }
    protected virtual void OnCreate() { }
    protected abstract void OnUpdate(float dt);
    protected virtual void OnDestroy() { }
}
```

常用 Query 系统可以加模板：

```csharp
public abstract class QuerySystem<T1, T2> : EcsSystem
    where T1 : unmanaged, IComponentData
    where T2 : unmanaged, IComponentData
{
    private Query<T1, T2> _query;

    protected override void OnCreate()
    {
        _query = World.Query<T1, T2>();
    }

    protected override void OnUpdate(float dt)
    {
        _query.ForEach(OnEach);
    }

    protected abstract void OnEach(Entity entity, ref T1 c1, ref T2 c2);
}
```

这类模板面向易用性，不作为最高性能推荐。

### 6. 提供命令辅助方法

用户写：

```csharp
World.Destroy(entity);
World.Add(entity, new DeadTag());
World.Remove<Velocity>(entity);
```

系统内部根据状态决定：

```text
如果正在 Query 遍历
-> 写入 CommandBuffer

如果不在遍历
-> 第一版也可以统一写入 CommandBuffer
-> 系统后 Playback
```

这样用户不需要记住“什么时候能立即改结构”。

### 7. 提供清晰错误信息

高性能框架最怕报错难懂。需要在 Debug 模式下提供明确错误：

```text
Entity 已失效：Entity(120:3)，当前版本 4
组件未注册：Velocity
组件不是 unmanaged：Inventory
Query 组件数量超过上限：Query<T1,T2,T3,T4,T5>
World 已 Dispose
结构变更发生在非法阶段
```

Release 模式可以关闭部分检查。

### 8. 提供可视化调试入口

用户不应该靠猜 Chunk 状态。调试窗口提供：

- Entity 搜索。
- Archetype 列表。
- Chunk 利用率。
- Query 命中结果。
- System 耗时。
- CommandBuffer 回放耗时。
- 最近 N 帧结构变更统计。

调试窗口只读核心数据，不参与核心链路。

### 9. 提供 Unity Authoring 轻封装

不走 DOTS Baking，但可以给 Unity 用户一个轻量 Authoring：

```csharp
public sealed class EcsSpawnAuthoring : MonoBehaviour
{
    public int Count;
    public Vector3 StartPosition;
    public Vector3 Velocity;
}
```

运行时转换：

```csharp
public sealed class EcsSpawnBootstrap : MonoBehaviour
{
    public EcsSpawnAuthoring[] Spawns;

    public void Spawn(World world)
    {
        foreach (var spawn in Spawns)
        {
            world.CreateMany<Position, Velocity>(spawn.Count, ...);
        }
    }
}
```

规则：

- Authoring 只在启动/加载时读。
- 运行时数据进入 ECS Chunk。
- 不引入完整 Baking 流程。

### 10. 提供推荐编码规范

为了让团队写法统一，文档和模板要固定：

```text
组件：名词，纯数据
系统：动词/行为 + System
桥接：Unity 对象只放 Bridge
Tag：短期状态或分类
Enableable：高频开关状态
Prefab：固定组件组合
Query：系统 OnCreate 缓存
```

这能降低学习成本，也方便后续做代码生成。

---

## 二十一、底层重构方案

底层重构要按层推进，不能一上来同时写 World、Query、CommandBuffer、System。正确路线是先把数据所有权打稳，再逐层接 API。

### 1. 分层目标

```
API Layer
-> World / Entity / Query / System

Execution Layer
-> SystemPipeline / CommandBuffer / Playback

Query Layer
-> QueryCache / Query<T> / ForEachChunk

Storage Layer
-> ArchetypeStore / Archetype / Chunk

Memory Layer
-> ChunkAllocator / UnsafeUtil

Type Layer
-> TypeRegistry / ComponentType / ComponentMask

Entity Layer
-> EntityStore
```

依赖方向只能向下，不能反向。

### 2. 第一步：重构类型系统

先实现：

- `IComponentData`
- `ComponentMask`
- `ComponentType`
- `ComponentType<T>`
- `TypeRegistry`

目标：

```text
注册组件
-> 得到固定 typeIndex
-> 得到 size / align / mask
-> 泛型静态缓存可用
```

验收：

- 注册同一组件多次返回同一类型。
- 超过 128 个组件报错。
- unmanaged 检查有效。
- Query/Get 不需要反射。

### 3. 第二步：重构 EntityStore

实现：

- `versions[]`
- `chunks[]`
- `indices[]`
- `archetypeIds[]`
- `freeIds[]`

目标：

```text
Entity 只负责身份
EntityStore 负责位置
Chunk 不负责实体生命周期
```

验收：

- Create/Destroy 后 Version 正确。
- 旧 Entity 无法访问新实体。
- swap-remove 后能更新被移动实体位置。

### 4. 第三步：重构 ChunkAllocator

实现：

- 大块 native 内存分配。
- 64 字节对齐。
- Chunk free list。
- Dispose 统一释放。

目标：

```text
Chunk 分配不走托管堆
Chunk 释放不立即归还系统
World.Dispose 释放所有 native block
```

验收：

- 分配大量 Chunk 无 GC。
- 地址满足 alignment。
- Dispose 后无 native 泄漏。

### 5. 第四步：重构 Archetype 与 Chunk Layout

实现：

- `Archetype`
- `ArchetypeStore`
- layout offset 计算。
- Chunk capacity 计算。
- mask -> archetype 查找。

目标：

```text
同一组件组合只创建一个 Archetype
Archetype 能计算 Chunk 内每种组件的数组位置
Chunk 能连续存储 Entity 和组件数据
```

验收：

- `[Position, Velocity]` 和 `[Velocity, Position]` 得到同一 Archetype。
- Chunk capacity 不越界。
- 组件 offset 对齐正确。
- Tag 组件不占数据区。

### 6. 第五步：重构 World 创建路径

实现：

- `World.Create<T1>`
- `World.Create<T1,T2>`
- `World.Create<T1,T2,T3>`
- `World.Get<T>`
- `World.Set<T>`
- `World.Has<T>`

目标：

```text
创建实体时直接进入目标 Archetype
随机访问通过 EntityStore 定位 Chunk/index
Has 只查 Archetype mask
```

验收：

- 创建后组件数据正确。
- `Get<T>` 返回 ref 且能修改原数据。
- 不存在组件时报错明确。

### 7. 第六步：重构结构变更

实现：

- `ArchetypeGraph`
- AddEdge/RemoveEdge。
- Entity 迁移。
- Chunk swap-remove。

目标：

```text
Add/Remove 不直接修改组件池
而是跨 Archetype 迁移整行实体数据
```

验收：

- Add 后旧组件保留，新组件写入。
- Remove 后目标 Archetype 正确。
- 被 swap 的实体位置正确更新。
- 空 Chunk 正确回收。

### 8. 第七步：重构 CommandBuffer

实现：

- 命令序列化格式。
- Add/Remove/Destroy/Create 命令。
- Playback。
- Clear。

目标：

```text
系统中所有结构变更延迟到安全点
Query 遍历期间不会修改当前 Chunk 链
```

验收：

- Query 中 Destroy 安全。
- Query 中 Add/Remove 安全。
- 失效 Entity 命令会跳过。
- Playback 顺序确定。

### 9. 第八步：重构 Query

实现：

- `QueryCache`
- `Query<T1>`
- `Query<T1,T2>`
- `Query<T1,T2,T3>`
- `ForEachChunk`
- `ForEach`

目标：

```text
Query 只遍历匹配 Archetype
Chunk 内连续扫描
不逐实体 Has
```

验收：

- Query 结果正确。
- 新 Archetype 创建后 QueryCache 刷新。
- ForEachChunk 无 GC。
- ForEach 结果与 ForEachChunk 一致。

### 10. 第九步：重构 SystemPipeline

实现：

- `EcsSystem`
- `SystemPipeline`
- 系统顺序执行。
- 每系统后 Playback。
- 系统耗时统计。

目标：

```text
用户只写系统
Pipeline 负责调度和结构变更安全点
```

验收：

- 系统 OnCreate/OnUpdate/OnDestroy 顺序正确。
- 每个系统后命令回放。
- 系统异常能定位系统名。

### 11. 第十步：重构 Unity Bridge

实现：

- `EcsRunner`
- `TransformBridge`
- `TransformProxy`
- `TransformSyncSystem`

目标：

```text
Unity 对象不进入 Chunk
Bridge 系统负责同步边界
```

验收：

- ECS 可脱离 Unity 对象测试。
- Transform 能正确同步。
- 停止 Play Mode 无泄漏。

### 12. 第十一步：加入便捷 API

在底层稳定后再加：

- `CreateMany`
- `ArchetypePrefab`
- `QuerySystem<T1,T2>`
- Authoring 轻封装。
- DebugWindow。

原因：

```text
先稳定底层数据流
再包用户友好 API
避免便捷 API 反过来污染架构
```

### 13. 重构期间的测试顺序

每层都要配测试：

```text
ComponentMask tests
TypeRegistry tests
EntityStore tests
ChunkAllocator tests
ArchetypeLayout tests
WorldCreate/Get tests
StructuralChange tests
CommandBuffer tests
Query tests
SystemPipeline tests
UnityBridge playmode tests
```

性能测试最后接入：

```text
100k Position+Velocity
100k Add/Remove 1%
QueryCache 100 Archetypes
CreateMany 100k
Destroy 100k
```

### 14. 重构完成后的核心数据流

最终底层数据流应该稳定为：

```text
TypeRegistry
-> ArchetypeStore
-> ChunkAllocator
-> World.Create
-> EntityStore location
-> QueryCache
-> ForEachChunk
-> CommandBuffer
-> Playback migration
-> UnityBridge
```

只要这条链路稳定，后续加 Jobs/Burst/Enableable/ChangeVersion 都不会推翻核心设计。

---

## 二十二、第一版 MVP 范围

第一版只做高性能核心闭环：

### 阶段 1：类型与 Entity

| 步骤 | 任务 | 文件 |
|---|---|---|
| 1.1 | `Entity` 句柄 | `Entity.cs` |
| 1.2 | `ComponentMask` 128 位掩码 | `ComponentMask.cs` |
| 1.3 | `ComponentType` 注册 | `ComponentType.cs` |
| 1.4 | `EntityStore` 平行数组 | `EntityStore.cs` |

验收：

- Entity 创建、判活、销毁正确。
- Version 能阻止旧句柄误用。
- 组件类型注册稳定且无热路径反射。

### 阶段 2：Chunk 与 Archetype

| 步骤 | 任务 | 文件 |
|---|---|---|
| 2.1 | `Chunk` header 与内存布局 | `Chunk.cs` |
| 2.2 | `ChunkAllocator` 64 字节对齐分配 | `ChunkAllocator.cs` |
| 2.3 | `Archetype` layout 计算 | `Archetype.cs` |
| 2.4 | `ArchetypeStore` mask 查找 | `ArchetypeStore.cs` |

验收：

- 能创建指定组件组合的 Archetype。
- Chunk capacity 计算正确。
- 组件指针偏移正确。

### 阶段 3：创建与随机访问

| 步骤 | 任务 | 文件 |
|---|---|---|
| 3.1 | `World.Create<T1>` | `World.cs` |
| 3.2 | `World.Create<T1,T2>` | `World.cs` |
| 3.3 | `World.Get<T>` | `World.cs` |
| 3.4 | `World.Set<T>` | `World.cs` |
| 3.5 | `World.Has<T>` | `World.cs` |

验收：

- 批量创建实体不发生多次迁移。
- `Get<T>` 返回正确 ref。
- `Has<T>` 只查 Entity 所属 Archetype mask。

### 阶段 4：结构变更与 CommandBuffer

| 步骤 | 任务 | 文件 |
|---|---|---|
| 4.1 | Archetype Add/Remove edge | `ArchetypeGraph.cs` |
| 4.2 | Entity 迁移 | `World.StructuralChanges.cs` |
| 4.3 | `CommandBuffer` 记录命令 | `CommandBuffer.cs` |
| 4.4 | `Playback` 批量回放 | `World.cs` |

验收：

- Add/Remove 组件后数据保持正确。
- swap-remove 后被移动实体的位置更新正确。
- 遍历期间结构变更不会破坏当前 Chunk。

### 阶段 5：Query

| 步骤 | 任务 | 文件 |
|---|---|---|
| 5.1 | `QueryCache` | `QueryCache.cs` |
| 5.2 | `Query<T1>` | `Query.cs` |
| 5.3 | `Query<T1,T2>` | `Query.cs` |
| 5.4 | `ForEachChunk` | `Query.cs` |
| 5.5 | `ForEach` | `Query.cs` |

验收：

- Query 只遍历匹配 Archetype。
- Chunk 内连续扫描。
- 不产生 GC。
- 不逐实体 HasComponent。

### 阶段 6：System 与 Unity Runner

| 步骤 | 任务 | 文件 |
|---|---|---|
| 6.1 | `EcsSystem` | `EcsSystem.cs` |
| 6.2 | `SystemPipeline` | `SystemPipeline.cs` |
| 6.3 | Unity `EcsRunner` | `EcsRunner.cs` |
| 6.4 | 示例 MovementSystem | `MovementSystem.cs` |

验收：

- Unity Play 模式能驱动 World。
- 每个系统后自动 Playback。
- 移动系统能处理大量实体。

---

## 二十三、目录结构

```
Assets/Scripts/ECS/
├── Core/
│   ├── Entity.cs
│   ├── IComponentData.cs
│   ├── ComponentMask.cs
│   ├── ComponentType.cs
│   ├── TypeRegistry.cs
│   ├── EntityStore.cs
│   ├── World.cs
│   ├── World.StructuralChanges.cs
│   └── UnsafeUtil.cs
├── Storage/
│   ├── Chunk.cs
│   ├── ChunkAllocator.cs
│   ├── Archetype.cs
│   ├── ArchetypeStore.cs
│   └── ArchetypeGraph.cs
├── Query/
│   ├── Query.cs
│   ├── QueryCache.cs
│   └── QueryDelegates.cs
├── Commands/
│   └── CommandBuffer.cs
├── Systems/
│   ├── EcsSystem.cs
│   ├── SystemPipeline.cs
│   └── MovementSystem.cs
├── Unity/
│   ├── EcsRunner.cs
│   └── TransformBridge.cs
└── Debugging/
    └── EcsDebugWindow.cs
```

---

## 二十四、性能验证计划

必须用 Benchmark 验证，而不是只写预期。

### Benchmark 1：连续移动

```
100,000 entities:
Position + Velocity

Query<Position, Velocity>.ForEachChunk
position += velocity * dt
```

指标：

- 每帧耗时。
- GC Alloc。
- 与 MonoBehaviour Update 对比。
- 与 Sparse Set 对照实现对比。

### Benchmark 2：结构变更

```
100,000 entities:
每帧 1% 添加或移除 Health
CommandBuffer.Playback
```

指标：

- Playback 耗时。
- Chunk 迁移次数。
- 空 Chunk 回收数量。

### Benchmark 3：Query 缓存

```
100 archetypes
10 query types
每帧重复执行
```

指标：

- QueryCache 命中率。
- Archetype 创建后重建成本。

---

## 二十五、后续高级优化

这些不进入 MVP，但架构需要能接住。

### 1. Change Version

Chunk 级组件版本号，用于跳过未变化 Chunk。

### 2. Enableable Component

用 bitset 禁用组件，不触发 Archetype 迁移。

### 3. Burst/Jobs 适配

把 `ForEachChunk` 的数据指针导出给 Job，但这必须在核心稳定后做。

### 4. Chunk Defragment

低利用率 Chunk 合并，减少碎片。

### 5. Query 代码生成

为常用 Query 生成静态迭代器，减少 delegate 调用成本。

---

## 二十六、最终结论

本项目应走 `Archetype + Chunk`，不要用 Sparse Set 作为核心存储。

新的重构方向是：

- 对外保持相对简单的 World/Entity/Query/System API。
- 对内采用固定上限、native chunk、SoA、archetype graph 和 command buffer。
- 第一版只做单线程高性能闭环。
- Jobs、Burst、Enableable、ChangeVersion、调试窗口全部后置。

下一步建议：从阶段 1 开始实现 `Entity`、`ComponentMask`、`ComponentType` 和 `EntityStore`，然后尽快进入 Chunk layout 验证。只要 Chunk layout 和迁移流程打稳，后面的 Query 和 System 才会真正站得住。
