# ECS 专业名词学习手册

> 这份文档专门解释 ECS_DESIGN.md 里出现的专业名词。主设计文档聚焦 ECS 优化链路和创新方案；遇到看不懂的词，回到这里查。

---

## 零、专业名词学习索引

这一节是给阅读文档时随时回头查的。后文会尽量保持术语一致：第一次啃不需要全部记住，只要知道它们分别解决什么问题。

### ECS

ECS 是 `Entity Component System` 的缩写。

通俗理解：

```text
Entity   = 这是谁
Component = 它有什么数据
System   = 怎么处理这些数据
```

在本项目里：

- `Entity` 是实体句柄，不保存业务数据。
- `Component` 是纯数据结构。
- `System` 是批量处理组件数据的逻辑。

### Entity

Entity 表示一个实体，例如一个玩家、怪物、子弹、掉落物。

它不是对象，也不应该像 `GameObject` 一样挂很多脚本。它只是一个轻量句柄：

```csharp
Entity { Id, Version }
```

在本项目里：

- `Id` 用来索引内部数组。
- `Version` 用来判断这个句柄是否已经失效。
- Entity 的真实数据存在 Chunk 里的组件数组中。

### Component

Component 是组件，也就是实体拥有的数据。

例如：

```text
Position 位置
Velocity 速度
Health   生命值
```

在本项目里：

- Component 必须优先是 `unmanaged struct`。
- Component 不写行为。
- Component 不直接保存 UnityEngine.Object。

### System

System 是系统，负责处理一批拥有相同组件的实体。

例如：

```text
MovementSystem 处理 Position + Velocity
DamageSystem   处理 Health
DeathSystem    处理 DeadTag
```

在本项目里：

- System 不拥有数据。
- System 通过 Query 找到匹配实体。
- System 里发生结构变更时写入 CommandBuffer。

### World

World 是 ECS 世界，管理所有实体、组件、Archetype、Chunk、Query 和系统。

通俗理解：

```text
World = 一套 ECS 数据库 + 调度器
```

在本项目里：

- 第一版只做单 World。
- World 负责创建实体、查询、回放命令和释放内存。

### Unity ECS / DOTS

Unity ECS 通常指 Unity 的 DOTS/Entities 技术栈。

DOTS 是 `Data-Oriented Technology Stack`，也就是数据导向技术栈，包含 Entities、Jobs、Burst 等。

在本项目里：

- 借鉴 Unity ECS 的数据导向思想。
- 不照搬它的 Baker、SubScene、复杂安全句柄和完整生态。
- 目标是更可控、更小、更适合从零实现的高性能 ECS。

### Data-Oriented Design

数据导向设计，简称 DOD。

通俗理解：

```text
先想 CPU 怎么连续读数据
再想代码怎么组织
```

它和传统面向对象不一样。面向对象常把数据和行为放在一个对象里；数据导向会把同类数据放在连续内存里批量处理。

在本项目里：

- `Position[]` 连续放。
- `Velocity[]` 连续放。
- System 批量遍历数组。

### Archetype

Archetype 可以理解成“组件组合类型”。

例如：

```text
Archetype A = [Position, Velocity]
Archetype B = [Position, Velocity, Health]
Archetype C = [Position]
```

拥有完全相同组件组合的实体，会放到同一个 Archetype 下。

在本项目里：

- Query 先匹配 Archetype。
- 结构变更本质是实体从一个 Archetype 迁移到另一个 Archetype。
- Archetype 管理自己的 Chunk 链表。

### Chunk

Chunk 是一块固定大小的连续内存，用来存同一个 Archetype 的一批实体。

通俗理解：

```text
Chunk = 一页高性能实体数据
```

在本项目里：

- 默认 Chunk 大小是 16 KiB。
- Chunk 内实体组件组合完全一致。
- Chunk 内组件按 SoA 方式连续存放。

### SoA

SoA 是 `Structure of Arrays`，数组的结构。

示例：

```text
Position[]: P0 P1 P2 P3
Velocity[]: V0 V1 V2 V3
```

它适合批量处理同一类数据。

在本项目里：

- Chunk 内每种组件都是一段连续数组。
- MovementSystem 可以连续读取 Position 和 Velocity。

### AoS

AoS 是 `Array of Structures`，结构的数组。

示例：

```text
EntityData0: Position + Velocity + Health
EntityData1: Position + Velocity + Health
EntityData2: Position + Velocity + Health
```

它写起来直观，但批量处理某一种组件时容易读到不需要的数据。

在本项目里：

- 不采用 AoS 作为核心布局。
- 选择 SoA 提高缓存友好度。

### Cache

Cache 是 CPU 缓存。

CPU 访问内存很慢，所以会把附近的数据提前读到缓存里。如果数据连续，CPU 更容易预测和预取。

在本项目里：

- Chunk 让数据连续。
- SoA 让同类组件连续。
- Query 按 Chunk 连续扫描，减少 cache miss。

### Cache Line

Cache Line 是 CPU 缓存一次加载的最小块，常见大小是 64 字节。

通俗理解：

```text
CPU 不是一个字节一个字节拿数据
而是一小块一小块拿
```

在本项目里：

- Chunk 地址按 64 字节对齐。
- 热数据尽量连续，减少浪费。

### GC

GC 是 Garbage Collection，垃圾回收。

C# 托管对象会由 GC 回收，但 GC 可能造成卡顿。

在本项目里：

- 热路径不 new class。
- Query 不分配临时 List。
- Chunk 使用 native memory。
- 目标是系统 Update 中 `GC Alloc = 0`。

### GC Alloc

GC Alloc 指某段代码产生了托管堆分配。

在 Unity Profiler 里，如果每帧都有 GC Alloc，就可能导致周期性卡顿。

在本项目里：

- Benchmark 必须记录 GC Alloc。
- Query、Playback、System Update 不应该产生 GC Alloc。

### unmanaged

`unmanaged` 是 C# 泛型约束，表示这个类型不包含托管引用。

例如通常可以是：

```text
int
float
Vector3 风格的纯值类型
只包含数值字段的 struct
```

不能是：

```text
string
class
GameObject
Transform
List<T>
```

在本项目里：

- 高性能 Component 优先要求 `where T : unmanaged, IComponentData`。
- 这样才能安全放进 Chunk native 内存。

### blittable

Blittable 指托管内存和非托管内存中的二进制布局一致，可以直接复制。

通俗理解：

```text
这块数据可以 memcpy
```

在本项目里：

- 组件越接近 blittable，越适合放入 Chunk。
- 结构迁移时可以直接复制组件字节。

### native memory

Native memory 是非托管内存，不由 C# GC 管理。

在本项目里：

- Chunk 使用 native memory。
- 需要自己分配和释放。
- `World.Dispose()` 必须释放所有 native block。

### unsafe

`unsafe` 是 C# 里允许使用指针的代码区域。

它危险，但可以换取更低层的内存控制。

在本项目里：

- Storage、Chunk、Query 热路径会使用 unsafe。
- 用户层尽量通过 World/Query API 使用，不直接碰裸指针。

### Pointer

Pointer 是指针，保存一块内存的地址。

例如：

```csharp
Position* positions;
```

表示 `positions` 指向一段连续的 Position 数据。

在本项目里：

- `ForEachChunk` 会给系统传组件指针。
- 最高性能系统可以直接用指针 for 循环。

### ref

`ref` 表示引用某个值本身，而不是复制一份。

例如：

```csharp
ref Health health = ref world.Get<Health>(entity);
health.Current -= 10;
```

在本项目里：

- `Get<T>` 返回 `ref T`。
- `ForEach` 便利版传 `ref` 组件。

### memcpy

`memcpy` 是内存拷贝，把一段字节从 A 复制到 B。

在本项目里：

- 实体迁移到新 Archetype 时，共有组件可以 memcpy。
- 销毁实体 swap-remove 时，末尾组件数据可以复制到被删除槽位。

### Alignment

Alignment 是内存对齐。

某些数据放在特定地址边界上，CPU 访问更高效。

在本项目里：

- Chunk 按 64 字节对齐。
- 组件数组按组件自身 alignment 对齐。
- Layout 计算必须考虑 padding。

### Padding

Padding 是为了对齐而填充的空字节。

在本项目里：

- Chunk layout 计算时会产生 padding。
- 组件排序可以减少 padding 浪费。

### TypeRegistry

TypeRegistry 是组件类型注册表。

它记录：

```text
组件类型 -> typeIndex
组件大小
组件对齐
组件 mask
```

在本项目里：

- 所有组件创建实体和 Query 前都要注册。
- 热路径不做反射查找。

### ComponentType

ComponentType 是组件类型的运行时元数据。

例如 `Position` 的 ComponentType 里会有：

```text
Index
Size
Align
Mask
```

在本项目里：

- Archetype layout 依赖 ComponentType。
- Query mask 依赖 ComponentType。

### TypeIndex

TypeIndex 是组件类型的整数编号。

例如：

```text
Position = 0
Velocity = 1
Health = 2
```

在本项目里：

- 用 TypeIndex 生成 ComponentMask。
- 用 TypeIndex 查组件 offset。
- 固定上限 128 个。

### ComponentMask

ComponentMask 是组件组合的位掩码。

通俗理解：

```text
一个二进制集合
每一位代表一种组件有没有
```

例如：

```text
Position | Velocity
```

表示这个实体/Archetype 同时拥有 Position 和 Velocity。

在本项目里：

- Query 用 mask 匹配 Archetype。
- `Has<T>` 用 mask 判断组件是否存在。

### Query

Query 是查询，用来找到拥有某些组件的实体。

例如：

```csharp
World.Query<Position, Velocity>()
```

表示找所有同时拥有 Position 和 Velocity 的实体。

在本项目里：

- Query 先匹配 Archetype。
- Query 再遍历匹配 Archetype 的 Chunk。
- Query 不逐实体 HasComponent。

### QueryCache

QueryCache 是查询缓存。

它缓存：

```text
Query<Position, Velocity> 命中了哪些 Archetype
```

在本项目里：

- 避免每帧重复扫描所有 Archetype。
- 新 Archetype 创建后再刷新相关 Query。

### IncludeMask

IncludeMask 是 Query 必须包含的组件集合。

例如：

```text
IncludeMask = Position | Velocity
```

表示匹配的 Archetype 必须同时包含这两个组件。

### ExcludeMask

ExcludeMask 是 Query 必须排除的组件集合。

例如：

```text
Include = Position
Exclude = DeadTag
```

表示查询有 Position 但没有 DeadTag 的实体。

第一版可以先不做 Exclude，后续补。

### ForEach

ForEach 是便利版遍历 API。

示例：

```csharp
Query<Position, Velocity>().ForEach((Entity e, ref Position p, ref Velocity v) => {});
```

在本项目里：

- ForEach 面向易用性。
- 它内部仍然应该走 ForEachChunk。

### ForEachChunk

ForEachChunk 是 Chunk 级遍历 API。

示例：

```csharp
Query<Position, Velocity>().ForEachChunk((Entity* e, Position* p, Velocity* v, int count) => {});
```

在本项目里：

- 最高性能系统优先使用它。
- 它直接暴露连续组件数组指针。

### CommandBuffer

CommandBuffer 是命令缓冲区。

它记录“之后再执行”的结构变更命令。

例如：

```text
Add Health
Remove Velocity
Destroy Entity
```

在本项目里：

- Query 遍历中不立刻改 Chunk。
- 结构变更写入 CommandBuffer。
- 系统之间统一 Playback。

### Playback

Playback 是回放 CommandBuffer。

通俗理解：

```text
把刚才攒下来的 Add/Remove/Destroy 一次性执行
```

在本项目里：

- Playback 是结构变更真正发生的地方。
- Playback 会触发 Archetype 迁移。

### Structural Change

Structural Change 是结构变更。

只要改变实体的组件组合，就是结构变更：

```text
Create Entity
Destroy Entity
Add Component
Remove Component
```

在本项目里：

- 结构变更会影响 Archetype。
- 结构变更必须延迟到安全点执行。

### Migration

Migration 是迁移。

当实体组件组合改变时，它要从旧 Archetype 移到新 Archetype。

例如：

```text
[Position, Velocity]
-> Add Health
-> [Position, Velocity, Health]
```

在本项目里：

- Add/Remove 组件本质都是迁移。
- 迁移时复制共有组件。

### swap-remove

swap-remove 是一种 O(1) 删除方法。

删除数组中间元素时，不整体移动后面的元素，而是把最后一个元素搬到被删除的位置。

在本项目里：

- Chunk 删除实体时使用 swap-remove。
- 被移动的实体必须更新 EntityStore 里的 index。

### EntityStore

EntityStore 是实体位置表。

它记录：

```text
Entity.Id -> Chunk
Entity.Id -> index in Chunk
Entity.Id -> ArchetypeId
Entity.Id -> Version
```

在本项目里：

- Entity 本身不存位置。
- EntityStore 负责从 Entity 找到真实数据。

### Version

Version 是实体版本号。

当实体销毁后，Id 可以复用，但 Version 会增加。

这样旧句柄不会误操作新实体。

在本项目里：

```text
Entity(10:1) 销毁
Id 10 被复用为 Entity(10:2)
旧的 Entity(10:1) 失效
```

### Stale Handle

Stale Handle 是过期句柄。

例如外部还保存着一个已经销毁的 Entity。

在本项目里：

- 通过 Version 检查防止过期句柄访问数据。

### ChunkAllocator

ChunkAllocator 是 Chunk 分配器。

它负责分配和回收 Chunk 使用的 native memory。

在本项目里：

- 大块分配。
- 64 字节对齐。
- World.Dispose 统一释放。

### Free List

Free List 是空闲链表。

通俗理解：

```text
不用的东西先串起来
下次需要时直接复用
```

在本项目里：

- 空闲 Entity Id 可以进 free list。
- 空闲 Chunk 可以进 free list。

### Prewarm

Prewarm 是预热。

提前创建 Archetype 和分配 Chunk，避免运行中第一次使用时卡顿。

在本项目里：

```csharp
world.PrewarmArchetype<Position, Velocity>(100_000);
```

### ArchetypeGraph

ArchetypeGraph 是 Archetype 之间的迁移图。

例如：

```text
[Position, Velocity] --Add Health--> [Position, Velocity, Health]
```

在本项目里：

- Add/Remove 组件时通过图快速找到目标 Archetype。

### AddEdge / RemoveEdge

AddEdge 是添加组件后的边。

RemoveEdge 是移除组件后的边。

在本项目里：

```text
sourceArchetype.AddEdge[Health] = targetArchetype
targetArchetype.RemoveEdge[Health] = sourceArchetype
```

这样下一次同样结构变更可以 O(1) 找到目标。

### Layout

Layout 是内存布局。

它描述 Chunk 中每段数据放在哪里。

在本项目里：

- Entity 数组在哪里。
- Position 数组在哪里。
- Velocity 数组在哪里。
- 每种组件 offset 是多少。

### Offset

Offset 是偏移量。

表示某块数据距离 Chunk 起始地址多少字节。

在本项目里：

```text
PositionOffset = 256
VelocityOffset = 1456
```

Query 根据 offset 找到组件数组指针。

### Capacity

Capacity 是容量。

在本项目里指一个 Chunk 最多能放多少个实体。

它由 Chunk 大小、Entity 大小、组件大小和对齐共同决定。

### Tag Component

Tag Component 是标签组件。

它通常没有数据，只表示一种状态或分类。

例如：

```csharp
public struct EnemyTag : IComponentData { }
public struct DeadTag : IComponentData { }
```

在本项目里：

- Tag 可以不占组件数据区。
- 但它会参与 Archetype 组合。

### Enableable Component

Enableable Component 是可启停组件。

它不通过 Add/Remove 改变结构，而是用 bit 标记启用或禁用。

在本项目里：

- 第一版不做。
- 后续用于高频开关状态。

### Change Version

Change Version 是变化版本号。

它记录某个 Chunk 的某种组件最近什么时候被写过。

在本项目里：

- 后续可用于跳过没变化的 Chunk。
- 第一版可以先保留字段，不启用过滤。

### Hot Path

Hot Path 是热路径。

指每帧都会大量执行、最影响性能的代码。

例如：

```text
Query 遍历
组件读写
CommandBuffer Playback
```

在本项目里：

- 热路径不能有 GC。
- 热路径不能反射。
- 热路径不能 LINQ。

### Cold Path

Cold Path 是冷路径。

指很少执行或不在关键帧循环里的代码。

例如：

```text
编辑器调试窗口
启动时注册组件
加载时预热 Archetype
```

在本项目里：

- 冷路径可以更偏易用。
- 但不能污染热路径。

### Hot/Cold Data Split

热冷数据拆分。

通俗理解：

```text
每帧都用的数据放一起
很少用的数据拆出去
```

在本项目里：

- Position/Velocity 是热数据。
- NameId/Exp 可能是冷数据。
- 不要写大杂烩组件。

### Source Generator

Source Generator 是 C# 源码生成器。

它可以在编译期生成代码，减少运行时反射和手写模板。

在本项目里：

- 后续可用于生成 Query 静态迭代器。
- 后续可生成组件注册代码。

### Authoring

Authoring 是编辑器里的配置数据。

例如用 MonoBehaviour 在 Inspector 里填写生成数量、初始速度。

在本项目里：

- Authoring 只作为启动时输入。
- 真正运行时数据会转换进 ECS Chunk。

### Baker

Baker 是 Unity ECS 里把 Authoring 数据烘焙成 ECS 数据的工具。

在本项目里：

- 第一版不做完整 Baker。
- 只做轻量运行时转换。

### SubScene

SubScene 是 Unity DOTS 里用于场景数据烘焙和流式加载的机制。

在本项目里：

- 第一版不做 SubScene。
- 场景接入通过 EcsRunner 和轻量 Authoring。

### Bridge

Bridge 是桥接层。

它连接 ECS 纯数据世界和 Unity GameObject 世界。

在本项目里：

- TransformBridge 保存 Transform 引用。
- ECS Chunk 里只保存 `TransformProxy.Id`。

### TransformProxy

TransformProxy 是 ECS 到 Unity Transform 的整数句柄。

在本项目里：

```csharp
public struct TransformProxy : IComponentData
{
    public int Id;
}
```

它不直接保存 Transform，避免托管引用进入 Chunk。

### SystemPipeline

SystemPipeline 是系统流水线。

它决定系统执行顺序。

在本项目里：

- 第一版按添加顺序执行。
- 每个系统执行后自动 Playback。

### Benchmark

Benchmark 是性能基准测试。

它用固定场景测耗时、GC、吞吐量。

在本项目里：

- 100k Position+Velocity 是基础 Benchmark。
- 结构变更和 QueryCache 也必须测。

### Profiler

Profiler 是性能分析工具。

Unity Profiler 可以看到耗时、GC Alloc、内存等。

在本项目里：

- 用 Profiler 检查系统耗时。
- 用自定义统计检查 Chunk/Archetype 状态。

### Dispose

Dispose 是释放资源。

C# 的托管对象可以等 GC，但 native memory 必须手动释放。

在本项目里：

- `World.Dispose()` 释放 ChunkAllocator。
- Play Mode 退出时必须调用。

### MVP

MVP 是 Minimum Viable Product，最小可用版本。

在本项目里：

MVP 不是最终功能全集，只要求跑通：

```text
Entity
-> Component
-> Archetype
-> Chunk
-> Query
-> CommandBuffer
-> SystemPipeline
```

---
