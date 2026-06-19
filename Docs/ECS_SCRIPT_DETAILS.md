# CyanMothUnityEcs 脚本详细说明

> 这份文档按层级解释所有已写脚本的职责、关键类型、核心 API，以及它们之间的关系。更细的逐字段 API 手册见 `ECS_API_REFERENCE.md`。

---

## 一、整体分层

当前项目按这条链路组织：

```text
Core 核心概念
  -> Storage 内存存储
  -> Query 查询遍历
  -> Commands 延迟结构变更
  -> Systems 系统调度
  -> Unity Bridge 使用层
  -> Debugging / Tests 验证层
```

核心目标是：

```text
不用 Sparse Set
以 Archetype + Chunk + SoA 为底层
用连续内存换取查询和批处理效率
用轻量 Authoring 降低 Unity 使用门槛
```

---

## 二、程序集脚本

### `Assets/Scripts/ECS/CyanMothUnityEcs.asmdef`

主程序集定义。

它决定 ECS 代码怎么被 Unity 编译：

```text
程序集名：CyanMothUnityEcs
默认命名空间：CyanMothUnityEcs
允许 unsafe：true
允许引用 UnityEngine：true
```

为什么要开启 unsafe：

```text
Chunk 内部会使用原始指针
组件数组按 byte offset 访问
结构迁移需要直接复制内存
```

### `Assets/Scripts/ECS/Tests/CyanMothUnityEcs.Tests.asmdef`

测试程序集定义。

它引用主程序集，并允许 NUnit 测试访问 ECS 代码。测试只在 Editor 下编译，不进入运行时包体。

### `Assets/Scripts/ECS/Core/AssemblyInfo.cs`

程序集级配置。

主要用途是让测试程序集可以访问 internal 类型或方法。这样生产 API 可以保持克制，测试仍然能检查底层行为。

---

## 三、Core 核心层

Core 层负责 ECS 的基本概念：组件、实体、World、结构变更、查询入口。

### `IComponentData.cs`

定义组件身份。

关键类型：

```csharp
public interface IComponentData
public interface IEnableableComponent : IComponentData
```

`IComponentData` 表示某个 struct 是 ECS 组件。大多数泛型 API 都要求：

```csharp
where T : unmanaged, IComponentData
```

这意味着组件必须是纯值类型，适合放进 Chunk 连续内存。

`IEnableableComponent` 表示组件可以被临时启用或禁用。禁用不会迁移 Archetype，只改 Chunk 内 enabled bitset。

### `ComponentMask.cs`

组件集合掩码。

它用两个 `ulong` 表示最多 128 种组件：

```text
Lo：TypeIndex 0..63
Hi：TypeIndex 64..127
```

主要用途：

```text
Archetype 用它表示“我拥有哪些组件”
Query 用它表示“我需要哪些组件”
结构变更用它计算 Add/Remove 后的新组件组合
```

常用操作：

```text
Add
Remove
Contains
ContainsAll
```

### `ComponentType.cs`

组件类型元数据。

它保存：

```text
TypeIndex
托管 Type
组件大小
组件对齐
是否实现 IEnableableComponent
```

存储层不会直接依赖 C# 泛型类型，而是大量使用 `ComponentType` 做统一描述。

### `ComponentTypeCache.cs`

泛型类型缓存。

例如第一次访问 `ComponentTypeCache<Position2D>.Type` 时会走 `TypeRegistry`，之后同类型直接复用缓存，避免热路径反复查字典。

### `TypeRegistry.cs`

组件注册中心。

它负责给每个组件类型分配稳定的 `TypeIndex`。这个索引会进入：

```text
ComponentMask
ArchetypeLayout
QueryCache
Chunk enabled bitset
Chunk ChangeVersion
```

测试中会调用清理方法，让每个测试从干净注册表开始。

### `Entity.cs`

实体句柄。

它不是数据本体，而是一个指向 ECS 内部实体记录的轻量句柄：

```text
Id：实体槽位
Version：版本号
```

Version 用来判断旧实体句柄是否已经失效。实体销毁后再复用同一个 Id，会提升 Version，旧句柄就不能误操作新实体。

### `EntityStore.cs`

实体位置表。

每个活着的 Entity 都会在这里记录：

```text
所在 Archetype
所在 Chunk
所在 Chunk 行号
版本号
是否存活
```

它是 `World.Get<T>`、结构迁移、销毁、Query 回写时定位数据的核心表。

### `UnsafeUtil.cs`

unsafe 工具类。

它把底层内存操作集中起来：

```text
SizeOf<T>
Align
IsAligned
Copy
Clear
```

这样 Chunk、ArchetypeLayout、结构迁移不需要到处写重复的指针逻辑。

### `World.cs`

World 是 ECS 的总入口。

它持有：

```text
EntityStore
ArchetypeStore
ChunkAllocator
CommandBuffer
QueryCache
ChangeVersion
```

用户大部分操作都从 World 开始，例如：

```text
Create
CreateMany
Has
Get
Set
Add
Remove
Destroy
Query
GetStats
```

### `World.Create.cs`

负责创建单个实体。

大体流程：

```text
收集组件类型
生成 ComponentMask
从 ArchetypeStore 获取 Archetype
在 Archetype 的可写 Chunk 中分配一行
写 Entity
写组件数据
更新 EntityStore 位置
标记 ChangeVersion
```

### `World.CreateMany.cs`

负责批量创建实体。

它的优化点是按 Chunk 批写：

```text
一次找到目标 Archetype
循环获取可写 Chunk
每个 Chunk 连续分配一段行
连续写 Entity 数组
连续写组件数组
```

这比逐个 `Create` 更接近底层 ECS 的高吞吐路径。

### `World.Access.cs`

负责普通组件访问。

关键 API：

```text
Has<T>
Get<T>
Set<T>
```

它会通过 EntityStore 找到实体所在 Chunk 和行号，再通过 Archetype 的 offset 定位组件数据。

### `World.StructuralChanges.cs`

负责结构变更。

结构变更指会改变实体组件组合的操作：

```text
Add<T>
Remove<T>
Destroy
```

Add/Remove 会把实体从旧 Archetype 迁移到新 Archetype。迁移时会复制共有组件，并处理旧 Chunk 的 swap-remove。

### `World.Query.cs`

负责查询遍历。

它支持：

```text
ForEach
ForEachReadOnly
ForEachWrite<TWrite>
ForEachChanged
ForEachChangedReadOnly
ForEachChangedWrite
ForEachChunk
ForEachEnabledChunk
```

主要优化点：

```text
QueryCache 缓存匹配 Archetype
缓存组件 offset
缓存组件 slot
Chunk 级 ChangeVersion 过滤
enableable 组件逐实体 bitset 过滤
指定写入组件避免误刷新 ChangeVersion
```

### `World.Debugging.cs`

负责调试 API。

关键 API：

```text
GetStats()
GetChangeVersion<T>(Entity)
```

`GetStats()` 会遍历 Archetype 和 Chunk，生成只读统计快照，供 Benchmark 和 Overlay 使用。

---

## 四、Storage 存储层

Storage 层是性能核心，负责 Archetype + Chunk + SoA 存储。

### `Chunk.cs`

Chunk 是固定大小内存块。

它内部包含：

```text
Chunk Header
Entity 数组
每种组件的连续数组
enableable bitset
ChangeVersion 数组
```

同一个 Chunk 内的实体都属于同一个 Archetype，所以它们拥有完全相同的组件组合。

### `ChunkAllocator.cs`

Chunk 原生内存分配器。

它负责：

```text
申请固定大小 Chunk 内存
保证对齐
清理 Chunk Header
统计 ReservedChunkCount
释放所有 Chunk
```

### `ArchetypeLayout.cs`

Archetype 的内存布局计算器。

它根据组件大小和对齐计算：

```text
每个组件数组在 Chunk 中的 offset
每个组件 stride
Chunk 容量
enabled bitset offset
ChangeVersion offset
```

### `Archetype.cs`

一种组件组合的实体存储池。

它管理这个 Archetype 下的 Chunk 链表：

```text
FirstChunk
可写 Chunk
满 Chunk
空 Chunk
```

实体创建时，它负责分配 Chunk 行；实体删除或迁移时，它负责维护 Chunk 内部连续性。

### `ArchetypeStore.cs`

Archetype 注册和查找中心。

它按 `ComponentMask` 查找或创建 Archetype，避免相同组件组合重复建存储结构。

---

## 五、Query 查询层

Query 层把用户写的泛型查询转成底层 Chunk 遍历。

### `QueryDelegates.cs`

定义 Query 回调委托。

包括：

```text
逐实体读写回调
只读 in 参数回调
Chunk 级指针回调
Enableable Chunk 回调
```

### `QueryCache.cs`

Query 缓存。

它保存：

```text
Query 需要的 ComponentMask
匹配到的 Archetype
每个 Archetype 中组件 offset
每个 Archetype 中组件 slot
```

这样真正遍历时不必反复做类型查找和 offset 计算。

### `Query.cs`

用户拿到的 Query 句柄。

示例：

```csharp
Query<Position2D, Velocity2D> query = world.Query<Position2D, Velocity2D>();
query.ForEachWrite<Position2D>((Entity e, ref Position2D p, ref Velocity2D v) => { ... });
```

它本身很轻，真正执行会委托给 World。

---

## 六、Commands 命令层

命令层解决“遍历中不能立即改结构”的问题。

### `CommandKind.cs`

命令类型枚举。

当前包含：

```text
Add
Remove
Destroy
```

### `CommandBuffer.cs`

延迟结构变更缓冲区。

它负责：

```text
记录 Add/Remove/Destroy
把 Add 组件值写入 raw payload
合并同实体同组件命令
Playback 前排序
回放到 World
清空或回收 payload
```

为什么要排序：

```text
让相同初始 Archetype 的实体尽量连续回放
减少结构迁移时来回跳不同 Archetype/Chunk
保留同实体命令顺序
```

---

## 七、Systems 系统层

系统层负责把业务逻辑组织成每帧执行的管线。

### `EcsSystem.cs`

系统基类。

提供：

```text
World
CommandBuffer
LastSystemVersion
OnCreate
OnUpdate
OnDestroy
```

`LastSystemVersion` 用来支持 Changed Query：系统可以只处理上次运行后变过的 Chunk。

### `QuerySystem.cs`

便捷系统基类。

它会在 `OnCreate` 时自动缓存 Query，让业务系统不用重复写 Query 初始化代码。

### `SystemPipeline.cs`

系统管线。

它负责：

```text
Add 系统
按顺序 Update 系统
每个系统执行后 Playback 命令
提交系统 LastSystemVersion
Dispose 时销毁系统
```

---

## 八、Debugging 调试与基准

### `WorldStats.cs`

World 统计快照。

字段包括：

```text
AliveEntityCount
CreatedEntityCapacity
ArchetypeCount
ChunkCount
ReservedChunkCount
TotalChunkCapacity
CommandCount
ChunkUtilization
```

### `EcsBenchmarkResult.cs`

Benchmark 结果结构。

保存：

```text
测试名
迭代次数
耗时 ticks
耗时毫秒
WorldStats
```

### `EcsBenchmark.cs`

内置基准测试。

用于粗略检查：

```text
批量创建
Query 遍历
结构变更
```

它不是最终性能测试框架，但能帮助判断优化方向是否变差。

---

## 九、Unity Bridge 使用层

Unity Bridge 层把纯 ECS 数据接回 Unity 场景对象。

### `Position2D.cs`

包含三个核心类型：

```text
Position2D
Velocity2D
Position2DAuthoring
```

`Position2D` 是位置数据。

`Velocity2D` 是速度数据。

`Position2DAuthoring` 是运行时 Authoring 组件，负责把 GameObject 转成 ECS Entity。它会读取 Transform 位置，创建 `Position2D`，可选添加 `Velocity2D`，并按配置创建 `TransformProxy` 和 `SpriteRendererProxy`。

### `TransformProxy.cs`

Transform 代理组件。

它只保存：

```text
int Id
```

真实 Transform 不进入 Chunk，而是留在 `TransformBridge` 中。

### `TransformBridge.cs`

托管桥接表。

包含：

```text
TransformBridge
SpriteRendererProxy
SpriteRenderState
SpriteRendererBridge
```

Bridge 的作用是把 UnityEngine.Object 隔离在托管表里，让 Chunk 只保存整数 Id 和纯数值。

### `TransformSyncSystem.cs`

包含三个系统：

```text
Position2DMoveSystem
TransformSyncSystem
SpriteRendererSyncSystem
```

`Position2DMoveSystem`：

```text
读取 Velocity2D
写入 Position2D
不访问 Unity 对象
```

`TransformSyncSystem`：

```text
读取 Position2D + TransformProxy
通过 TransformBridge 找 Transform
把 Position2D 写回 transform.position
```

`SpriteRendererSyncSystem`：

```text
读取 SpriteRendererProxy + SpriteRenderState
通过 SpriteRendererBridge 找 SpriteRenderer
同步颜色和 visible
```

### `EcsRunner.cs`

Unity 场景里的 ECS 启动器。

负责：

```text
创建 World
创建 SystemPipeline
创建 TransformBridge
创建 SpriteRendererBridge
注册默认系统
扫描 Position2DAuthoring
每帧 Tick
销毁时释放资源
```

它还提供：

```csharp
public Entity Convert(Position2DAuthoring authoring)
```

用于运行时动态生成对象后，立刻把它转成 ECS Entity。

### `EcsDebugOverlay.cs`

运行时调试浮层。

它使用 Unity IMGUI 显示：

```text
实体数量
Archetype 数量
Chunk 数量
Chunk 利用率
待回放命令数
Authoring 转换数量
Transform 桥接数量
SpriteRenderer 桥接数量
```

它只读取 `World.GetStats()`，不进入 ECS 热路径。

### `EcsDemoSpawner.cs`

最小 Demo 生成器。

它会批量创建对象：

```text
创建 GameObject
添加 SpriteRenderer
添加 Position2DAuthoring
设置初始位置
设置初始速度
调用 EcsRunner.Convert
```

如果没有指定 Sprite，它会运行时创建一个 1x1 白色 Sprite，让对象开箱可见。

---

## 十、Tests 测试层

测试脚本按模块覆盖 ECS 行为。

### Core / Storage 测试

| 脚本 | 检查重点 |
|---|---|
| `TypeRegistryTests.cs` | 类型注册、TypeIndex、重复注册。 |
| `ComponentMaskTests.cs` | Add/Remove/Contains/ContainsAll。 |
| `EntityStoreTests.cs` | Entity 创建、销毁、版本失效、位置记录。 |
| `ChunkAllocatorTests.cs` | Chunk 分配、对齐、计数、释放。 |
| `ArchetypeStoreTests.cs` | Archetype 复用、布局、Chunk 分配。 |

### World / Query / Command 测试

| 脚本 | 检查重点 |
|---|---|
| `WorldCreateAccessTests.cs` | Create、Has、Get、Set。 |
| `WorldCreateManyTests.cs` | 批量创建和统计。 |
| `WorldDestroyTests.cs` | Destroy 后实体失效。 |
| `WorldStructuralChangeTests.cs` | Add/Remove 迁移 Archetype。 |
| `CommandBufferTests.cs` | 命令记录、合并、排序、Playback。 |
| `QueryTests.cs` | Query 遍历、ReadOnly、Changed、Enableable、ForEachWrite。 |
| `ChangeVersionTests.cs` | Chunk 组件版本递增和过滤。 |

### Systems / Debugging / Unity 测试

| 脚本 | 检查重点 |
|---|---|
| `QuerySystemTests.cs` | QuerySystem 自动缓存 Query。 |
| `SystemPipelineTests.cs` | 系统生命周期、顺序、CommandBuffer 回放。 |
| `WorldStatsTests.cs` | WorldStats 数值正确。 |
| `EcsBenchmarkTests.cs` | Benchmark 能返回结果。 |
| `UnityBridgeTests.cs` | Transform/Sprite 桥接、Authoring、Runner、DemoSpawner。 |

---

## 十一、关键关系图

### 实体创建链路

```text
World.Create
  -> TypeRegistry 获取 ComponentType
  -> ComponentMask 描述组件组合
  -> ArchetypeStore 获取 Archetype
  -> Archetype 分配 Chunk 行
  -> Chunk 写 Entity 和组件数据
  -> EntityStore 记录实体位置
```

### Query 遍历链路

```text
World.Query<T...>
  -> QueryCache 创建或复用 Query
  -> 匹配 Archetype
  -> 缓存组件 offset / slot
  -> ForEach 遍历 Chunk
  -> 按行读写组件
```

### 结构变更链路

```text
World.Add / Remove
  -> 计算新 ComponentMask
  -> 获取目标 Archetype
  -> 在目标 Chunk 分配行
  -> 复制共有组件
  -> 写入新增组件或移除目标组件
  -> 旧 Chunk swap-remove
  -> 更新 EntityStore
```

### Unity 使用链路

```text
EcsRunner.Initialize
  -> 创建 World / Pipeline / Bridge
  -> 注册 Position2DMoveSystem
  -> 注册 TransformSyncSystem
  -> 注册 SpriteRendererSyncSystem
  -> 扫描 Position2DAuthoring
  -> 创建 Entity

EcsRunner.Update
  -> Pipeline.Update
  -> Position2DMoveSystem 更新 Position2D
  -> TransformSyncSystem 写回 Transform
  -> SpriteRendererSyncSystem 写回 SpriteRenderer
```

### Demo 链路

```text
EcsDemoSpawner.Spawn
  -> 生成 GameObject
  -> 添加 SpriteRenderer
  -> 添加 Position2DAuthoring
  -> EcsRunner.Convert
  -> Entity 进入 Archetype + Chunk
  -> EcsDebugOverlay 显示统计
```

---

## 十二、阅读建议

从易到难推荐顺序：

```text
1. EcsDemoSpawner.cs
2. EcsRunner.cs
3. Position2D.cs
4. TransformSyncSystem.cs
5. World.cs / World.Create.cs / World.Access.cs
6. Entity.cs / EntityStore.cs
7. ComponentMask.cs / TypeRegistry.cs
8. ArchetypeStore.cs / Archetype.cs / ArchetypeLayout.cs / Chunk.cs
9. Query.cs / QueryCache.cs / World.Query.cs
10. CommandBuffer.cs
11. World.StructuralChanges.cs
12. SystemPipeline.cs / EcsSystem.cs
```

先理解使用层，再往下拆到存储层，会更容易把整条链路吃透。
