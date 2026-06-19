# ECS 实施路线图

> 这份文档只回答三个问题：从哪里开始、先后顺序是什么、每一步做到什么程度才进入下一步。设计理念看 `ECS_DESIGN.md`，专业名词看 `ECS_TERMS.md`。

---

## 一、先说结论：从哪里开始

不要从 Unity Runner 开始，不要从 Query 开始，也不要从 Jobs/Burst 开始。

第一刀从 **类型系统 + EntityStore** 开始。

原因：

```text
ComponentType / ComponentMask 决定 Archetype 怎么匹配
EntityStore 决定 Entity 怎么定位 Chunk
Archetype / Chunk 依赖前两者
World.Create / Query / CommandBuffer 都依赖 Archetype + Chunk
Unity Runner 只是最外层驱动
```

正确顺序：

```text
0. 工程准备
1. Type Layer：组件类型系统
2. Entity Layer：实体句柄和位置表
3. Memory Layer：unsafe 工具和 Chunk 分配器
4. Storage Layer：Archetype 和 Chunk Layout
5. World Create/Get：创建实体和随机访问
6. Structural Changes：Add/Remove/Destroy 迁移
7. CommandBuffer：延迟结构变更
8. Query：Archetype 匹配和 Chunk 遍历
9. SystemPipeline：系统执行和 Playback 安全点
10. Unity Bridge：Unity 场景接入
11. Debug & Benchmark：调试和性能验证
12. Convenience API：便捷 API
13. Advanced Optimization：高级优化
```

每一步都要能独立跑测试。不要跨阶段写大而全的代码。

### 按阶段推进

后续实现按大阶段推进。每个大阶段完成后先暂停验收，不要直接冲到下一阶段。

| 大阶段 | 包含步骤 | 阶段目标 | 完成标志 |
|---|---|---|---|
| 阶段 A：地基层 | 0-2 | 工程结构、组件类型、Entity 句柄稳定 | TypeRegistry 和 EntityStore 测试通过 |
| 阶段 B：内存与存储层 | 3-4 | native Chunk、Archetype、Layout 能独立工作 | Chunk 分配、布局、容量测试通过 |
| 阶段 C：数据闭环 | 5-6 | 实体能创建、读写、Add/Remove/Destroy 迁移 | World.Create/Get 和结构变更测试通过 |
| 阶段 D：安全执行层 | 7-9 | CommandBuffer、Query、SystemPipeline 串起来 | 系统中 Query + Playback 能跑完整帧 |
| 阶段 E：Unity 接入层 | 10 | ECS 能在 Unity 场景中运行并同步 Transform | Play Mode 下 Runner 可启动/释放 |
| 阶段 F：可观测层 | 11 | 能看见性能和结构数据 | Debug/Benchmark 能输出关键指标 |
| 阶段 G：开发体验层 | 12 | 加便捷 API，但不破坏底层路径 | CreateMany/Prefab/模板系统可用且无热路径 GC |
| 阶段 H：高级优化层 | 13 | 根据 Benchmark 做进一步优化 | 每项优化都有数据证明 |

阶段推进规则：

```text
一个大阶段没验收，不进入下一个大阶段
一个小步骤没测试，不继续叠功能
任何时候发现 Chunk/Layout/EntityStore 错误，回退到对应阶段修
```

最重要的暂停点：

```text
阶段 B 完成后：确认 Chunk layout 没问题
阶段 C 完成后：确认结构迁移没问题
阶段 D 完成后：确认一帧完整 ECS 执行链路没问题
阶段 E 完成后：确认 Unity 生命周期没有泄漏
```

---

## 二、阶段 0：工程准备

### 目标

建立代码目录、命名空间和测试位置，让后续代码有稳定落点。

### 创建目录

```
Assets/Scripts/ECS/
├── Core/
├── Storage/
├── Query/
├── Commands/
├── Systems/
├── Unity/
├── Debugging/
└── Tests/
```

### 建议命名空间

```csharp
namespace CyanMothUnityEcs
{
}
```

如果后续想分层，也可以：

```text
CyanMothUnityEcs.Core
CyanMothUnityEcs.Storage
CyanMothUnityEcs.Querying
CyanMothUnityEcs.Commands
CyanMothUnityEcs.Systems
CyanMothUnityEcs.Unity
```

### 需要确认

- Unity 项目允许 unsafe 代码。
- 代码能正常编译。
- 测试目录可放 EditMode 测试。

### 暂时不要做

- 不写 `MonoBehaviour Runner`。
- 不写 Query。
- 不写 CommandBuffer。
- 不接 Unity Transform。

### 验收

- 目录创建完成。
- 空类能编译。
- unsafe 配置确认可用。

---

## 三、阶段 1：Type Layer 组件类型系统

### 为什么先做

Archetype 的本质是组件组合。没有稳定的组件类型编号和掩码，就无法创建 Archetype、Query，也无法计算 Chunk Layout。

### 文件

```
Assets/Scripts/ECS/Core/IComponentData.cs
Assets/Scripts/ECS/Core/ComponentMask.cs
Assets/Scripts/ECS/Core/ComponentType.cs
Assets/Scripts/ECS/Core/ComponentTypeCache.cs
Assets/Scripts/ECS/Core/TypeRegistry.cs
```

### 实现顺序

1. `IComponentData`

```csharp
public interface IComponentData
{
}
```

2. `ComponentMask`

固定 128 位：

```csharp
public struct ComponentMask
{
    public ulong Lo;
    public ulong Hi;
}
```

必须实现：

```text
FromIndex
ContainsAll
Intersects
Add
Remove
Equals
GetHashCode
```

3. `ComponentType`

保存：

```text
Index
Size
Align
Mask
IsTag
```

4. `TypeRegistry`

负责：

```text
Register<T>
Get<T>
GetByIndex
```

5. `ComponentTypeCache<T>`

泛型静态缓存：

```csharp
public static class ComponentTypeCache<T> where T : unmanaged, IComponentData
{
    public static readonly ComponentType Type = TypeRegistry.Register<T>();
}
```

### 关键约束

- 组件类型上限 128。
- 组件必须 unmanaged。
- 注册后 TypeIndex 不允许变化。
- 热路径不做反射查找。

### 测试

```
ComponentMask_FromIndex_Works
ComponentMask_ContainsAll_Works
TypeRegistry_Register_ReturnsStableIndex
TypeRegistry_DuplicateRegister_ReturnsSameType
TypeRegistry_Over128_Throws
```

### 验收

- 能注册 `Position`、`Velocity`、`Health`。
- 能得到稳定 TypeIndex。
- 能生成正确 ComponentMask。
- 重复注册不会产生新 TypeIndex。

### 暂时不要做

- 不做 Source Generator。
- 不做 managed component。
- 不做动态扩容到 128 以上。

---

## 四、阶段 2：Entity Layer 实体句柄和位置表

### 为什么第二步做

Chunk 里存的是实体数据，但外部拿到的是 Entity 句柄。必须先有 EntityStore，后面 Chunk swap-remove 和 Archetype 迁移才能更新实体位置。

### 文件

```
Assets/Scripts/ECS/Core/Entity.cs
Assets/Scripts/ECS/Core/EntityStore.cs
```

### 实现顺序

1. `Entity`

```csharp
public readonly struct Entity
{
    public readonly int Id;
    public readonly int Version;
}
```

2. `EntityStore`

内部数组：

```text
versions[]
chunks[]
indices[]
archetypeIds[]
freeIds[]
```

方法：

```text
Create
IsAlive
Validate
Destroy
SetLocation
GetChunk
GetIndex
GetArchetypeId
```

### 关键约束

- `Entity.Null` 使用 `Id = 0`。
- Entity 销毁后 `Version++`。
- Id 可以复用，但 Version 必须变化。
- EntityStore 不知道组件数据，只知道位置。

### 测试

```
EntityStore_Create_ReturnsAliveEntity
EntityStore_Destroy_InvalidatesOldVersion
EntityStore_ReusedId_HasNewVersion
EntityStore_SetLocation_CanReadBack
EntityStore_Null_IsNotAlive
```

### 验收

- Entity 创建和销毁正确。
- 旧 Entity 句柄无法通过 Validate。
- 位置写入和读取正确。

### 暂时不要做

- 不做 Chunk 真实指针操作。
- 不做 Archetype 迁移。
- 不做 Query。

---

## 五、阶段 3：Memory Layer unsafe 工具和 Chunk 分配器

### 为什么第三步做

Archetype 和 Chunk Layout 需要真实内存承载。先把内存分配、对齐、释放打稳，再往上写 Chunk 数据结构。

### 文件

```
Assets/Scripts/ECS/Core/UnsafeUtil.cs
Assets/Scripts/ECS/Storage/Chunk.cs
Assets/Scripts/ECS/Storage/ChunkAllocator.cs
```

### 实现顺序

1. `UnsafeUtil`

提供：

```text
Align
SizeOf<T>
Copy
Clear
```

2. `Chunk`

Header 字段：

```text
ArchetypeId
Count
Capacity
Sequence
Flags
Next
Prev
NextFree
ChangeVersions
```

3. `ChunkAllocator`

负责：

```text
Allocate
Free
Dispose
```

### 关键约束

- Chunk 默认 16 KiB。
- Chunk 地址 64 字节对齐。
- 分配走 native memory。
- Dispose 释放所有 block。

### 测试

```
UnsafeUtil_Align_Works
ChunkAllocator_Allocate_ReturnsAlignedPointer
ChunkAllocator_Free_ReusesChunk
ChunkAllocator_Dispose_ReleasesAllBlocks
```

### 验收

- 能分配 10000 个 Chunk。
- 无托管分配热路径。
- Dispose 后无泄漏。

### 暂时不要做

- 不写组件数据读写。
- 不写 Query。
- 不做 Jobs/Burst。

---

## 六、阶段 4：Storage Layer Archetype 和 Chunk Layout

### 为什么第四步做

这是整个 ECS 的地基。Archetype 决定哪些实体在一起，Chunk Layout 决定组件在内存里怎么排。

### 文件

```
Assets/Scripts/ECS/Storage/Archetype.cs
Assets/Scripts/ECS/Storage/ArchetypeStore.cs
Assets/Scripts/ECS/Storage/ArchetypeLayout.cs
```

### 实现顺序

1. `ArchetypeLayout`

输入：

```text
ComponentType[]
ChunkSize
HeaderSize
```

输出：

```text
Capacity
EntityOffset
ComponentOffsets[]
ComponentStrides[]
```

2. `Archetype`

保存：

```text
Id
Mask
Types
Layout
FirstChunk
LastChunk
FirstFreeChunk
AddEdges
RemoveEdges
```

3. `ArchetypeStore`

负责：

```text
GetOrCreate(mask, types)
FindByMask
Version++
```

### 关键约束

- 组件类型按 TypeIndex 排序。
- 同组合只创建一个 Archetype。
- Tag 组件不占组件数据区。
- Chunk capacity 不能越过 16 KiB。

### 测试

```
ArchetypeStore_SameTypesDifferentOrder_ReturnsSameArchetype
ArchetypeLayout_Capacity_FitsChunk
ArchetypeLayout_ComponentOffsets_AreAligned
Archetype_TagComponent_TakesNoDataSpace
```

### 验收

- 能创建 `[Position]`。
- 能创建 `[Position, Velocity]`。
- 能根据 Mask 找回同一个 Archetype。
- 能计算正确 Chunk Capacity。

### 暂时不要做

- 不做 Add/Remove 迁移。
- 不做 QueryCache。
- 不做 CommandBuffer。

---

## 七、阶段 5：World Create/Get 创建实体和随机访问

### 为什么第五步做

此时类型、实体、内存、Archetype 都有了，可以跑通第一条真正的数据链路：创建实体 -> 写入 Chunk -> 从 Entity 读回组件。

### 文件

```
Assets/Scripts/ECS/Core/World.cs
Assets/Scripts/ECS/Core/World.Create.cs
Assets/Scripts/ECS/Core/World.Access.cs
```

### 实现顺序

1. `World` 持有核心模块：

```text
TypeRegistry
EntityStore
ArchetypeStore
ChunkAllocator
```

2. 实现：

```text
Create<T1>
Create<T1,T2>
Create<T1,T2,T3>
Has<T>
Get<T>
Set<T>
```

3. 创建流程：

```text
读 ComponentType
-> GetOrCreate Archetype
-> 获取 writable Chunk
-> 分配 slot
-> 写 Entity
-> 写组件
-> EntityStore.SetLocation
```

### 测试

```
World_CreateOneComponent_WritesData
World_CreateTwoComponents_WritesData
World_Get_ReturnsRefToStoredData
World_Set_UpdatesStoredData
World_Has_UsesArchetypeMask
```

### 验收

- 能创建实体并读回组件。
- `Get<T>` 修改的是 Chunk 内真实数据。
- `Has<T>` 正确。
- 无 Query 也能跑通基本数据访问。

### 暂时不要做

- 不做 Add/Remove。
- 不做 Destroy。
- 不做 Query。

---

## 八、阶段 6：Structural Changes Add/Remove/Destroy 迁移

### 为什么第六步做

Archetype + Chunk 的难点就是结构变更。必须先把迁移打稳，否则 Query 和 CommandBuffer 都会踩在不可靠地基上。

### 文件

```
Assets/Scripts/ECS/Storage/ArchetypeGraph.cs
Assets/Scripts/ECS/Core/World.StructuralChanges.cs
```

### 实现顺序

1. `ArchetypeGraph`

实现：

```text
GetAddTarget(source, componentType)
GetRemoveTarget(source, componentType)
```

2. AddComponent 迁移：

```text
source Archetype
-> target Archetype
-> target Chunk/slot
-> copy shared components
-> write new component
-> update EntityStore
-> source swap-remove
```

3. RemoveComponent 迁移：

```text
source Archetype
-> target Archetype
-> copy all except removed component
-> update EntityStore
-> source swap-remove
```

4. Destroy：

```text
locate Chunk/index
-> swap-remove
-> update moved EntityStore location
-> EntityStore.Release
```

### 测试

```
World_AddComponent_MigratesToNewArchetype
World_RemoveComponent_MigratesToNewArchetype
World_Destroy_RemovesEntityAndInvalidatesVersion
World_SwapRemove_UpdatesMovedEntityLocation
World_AddExistingComponent_BehavesAsSet
```

### 验收

- Add/Remove 后数据不丢。
- Destroy 后旧 Entity 失效。
- swap-remove 后被移动实体还能正确 Get。
- 空 Chunk 能回收或进入 free list。

### 暂时不要做

- 不做 CommandBuffer。
- 不做 Query 遍历中结构变更。
- 不做命令合并。

---

## 九、阶段 7：CommandBuffer 延迟结构变更

### 为什么第七步做

Query 遍历 Chunk 时不能同时改 Chunk 链表和数据布局。CommandBuffer 提供安全点。

### 文件

```
Assets/Scripts/ECS/Commands/CommandBuffer.cs
Assets/Scripts/ECS/Commands/CommandKind.cs
```

### 实现顺序

1. 命令类型：

```text
Create
Destroy
Add
Remove
Set
```

2. 命令格式：

```text
kind
entity id
entity version
type index
payload size
payload bytes
```

3. 实现：

```text
Add<T>
Remove<T>
Destroy
Playback
Clear
```

4. World 接入：

```text
World.Commands
World.Playback
```

### 测试

```
CommandBuffer_Add_Playback_AddsComponent
CommandBuffer_Remove_Playback_RemovesComponent
CommandBuffer_Destroy_Playback_DestroysEntity
CommandBuffer_InvalidEntityCommand_IsSkipped
CommandBuffer_PlaybackOrder_IsDeterministic
```

### 验收

- 系统中可以只写命令，不直接改结构。
- Playback 后结构变更正确。
- 失效 Entity 命令不会炸。

### 暂时不要做

- 不做命令合并。
- 不做多线程 ECB。
- 不做 Jobs。

---

## 十、阶段 8：Query Archetype 匹配和 Chunk 遍历

### 为什么第八步做

此时创建、访问、结构变更都可靠了，Query 才有意义。Query 必须建立在稳定的 ArchetypeStore 和 Chunk 链表之上。

### 文件

```
Assets/Scripts/ECS/Query/Query.cs
Assets/Scripts/ECS/Query/QueryCache.cs
Assets/Scripts/ECS/Query/QueryDelegates.cs
```

### 实现顺序

1. `QueryCache`

```text
includeMask
excludeMask
matching archetype ids
cached archetype version
```

2. `Query<T1>`

3. `Query<T1,T2>`

4. `Query<T1,T2,T3>`

5. `ForEachChunk`

6. `ForEach`

### 测试

```
Query_OneComponent_ReturnsMatchingEntities
Query_TwoComponents_ReturnsOnlyMatchingArchetypes
Query_AfterNewArchetype_RefreshesCache
Query_ForEachChunk_NoGC
Query_ForEach_MatchesForEachChunkResults
```

### 验收

- Query 不遍历所有实体。
- Query 不逐实体 Has。
- ForEachChunk 能连续访问组件指针。
- QueryCache 在新 Archetype 创建后刷新。

### 暂时不要做

- 不做 Without。
- 不做 Enableable。
- 不做 ChangeVersion filter。

---

## 十一、阶段 9：SystemPipeline 系统执行链路

### 为什么第九步做

World、CommandBuffer、Query 都可用后，再做系统调度。否则系统只是空壳。

### 文件

```
Assets/Scripts/ECS/Systems/EcsSystem.cs
Assets/Scripts/ECS/Systems/SystemPipeline.cs
```

### 实现顺序

1. `EcsSystem`

```text
Attach(World)
OnCreate
OnUpdate
OnDestroy
```

2. `SystemPipeline`

```text
Add
Update
Dispose
```

3. 每个系统后：

```text
World.Playback()
```

### 测试

```
SystemPipeline_CallsOnCreate
SystemPipeline_UpdateOrder_IsAddOrder
SystemPipeline_PlaybackAfterEachSystem
SystemPipeline_CallsOnDestroyReverseOrder
```

### 验收

- 系统按顺序执行。
- 命令在系统之间回放。
- 系统生命周期正确。

### 暂时不要做

- 不做并行调度。
- 不做 SystemGroup。
- 不做依赖分析。

---

## 十二、阶段 10：Unity Bridge 场景接入

### 为什么第十步做

核心 ECS 已经能独立运行后，再接 Unity。这样可以避免 Unity 生命周期干扰底层实现。

### 文件

```
Assets/Scripts/ECS/Unity/EcsRunner.cs
Assets/Scripts/ECS/Unity/TransformBridge.cs
Assets/Scripts/ECS/Unity/TransformProxy.cs
Assets/Scripts/ECS/Unity/TransformSyncSystem.cs
```

### 实现顺序

1. `EcsRunner`

```text
Awake 创建 World/Pipeline
Update 执行 Pipeline
OnDestroy Dispose
```

2. `TransformBridge`

```text
Register
Get
Unregister
```

3. `TransformProxy`

```text
int Id
```

4. `TransformSyncSystem`

```text
Query<Position, TransformProxy>
-> 写 Transform.position
```

### 测试

```
PlayMode_EcsRunner_CreatesAndDisposesWorld
TransformBridge_RegisterAndGet_Works
TransformSyncSystem_UpdatesTransform
```

### 验收

- Unity 场景能启动 ECS。
- Transform 能同步。
- 停止播放无 native 泄漏。

### 暂时不要做

- 不做完整 Authoring/Baker。
- 不做 SubScene。
- 不做 Hybrid Renderer。

---

## 十三、阶段 11：Debug & Benchmark 调试和性能验证

### 为什么第十一步做

底层系统没有调试和 Benchmark，很容易“看起来能跑，实际很慢”。这一步开始建立性能反馈。

### 文件

```
Assets/Scripts/ECS/Debugging/WorldStats.cs
Assets/Scripts/ECS/Debugging/EcsDebugWindow.cs
Assets/Scripts/ECS/Debugging/EcsBenchmark.cs
```

### 实现内容

统计：

```text
Entity count
Archetype count
Chunk count
Chunk utilization
Command count
Migration count
Query time
Playback time
System time
GC alloc
```

Benchmark：

```text
100k Position + Velocity
100k Destroy
100k Add/Remove 1%
100 Archetypes + 10 Queries
CreateMany 100k
```

### 验收

- 能看到系统耗时。
- 能看到 Chunk 利用率。
- 能看到结构变更次数。
- Benchmark 可重复运行。

---

## 十四、阶段 12：Convenience API 便捷 API

### 为什么放到后面

便捷 API 必须包在稳定底层之上。提前写会诱导架构为易用性妥协。

### 实现内容

```text
CreateMany
ArchetypePrefab
QuerySystem<T1,T2>
World.Add/Remove/Destroy 便捷转 CommandBuffer
轻量 Authoring
更友好的错误信息
```

### 验收

- 原型开发更顺手。
- 便捷 API 不引入热路径 GC。
- 便捷 API 最终走底层高性能路径。

---

## 十五、阶段 13：Advanced Optimization 高级优化

这些等核心稳定后再做。

### 优先级

1. Query offset 缓存。
2. CommandBuffer 命令合并。
3. Enableable Component。
4. ChangeVersion 过滤。
5. Chunk Defragment。
6. Source Generator。
7. Jobs/Burst。

### 判断标准

只有当 Benchmark 证明瓶颈存在时，才进入高级优化。

不要为了“看起来高级”提前做 Jobs/Burst。

---

## 十六、完整实现顺序总表

| 阶段 | 名称 | 主要产出 | 进入下一阶段的条件 |
|---|---|---|---|
| 0 | 工程准备 | 目录、命名空间、unsafe 配置 | 空代码可编译 |
| 1 | Type Layer | ComponentMask、TypeRegistry | 类型注册和 mask 测试通过 |
| 2 | Entity Layer | Entity、EntityStore | 创建/销毁/version 测试通过 |
| 3 | Memory Layer | Chunk、ChunkAllocator | 对齐分配和释放测试通过 |
| 4 | Storage Layer | Archetype、Layout、Store | layout/capacity 测试通过 |
| 5 | World Create/Get | Create、Get、Set、Has | 能创建实体并读写组件 |
| 6 | Structural Changes | Add、Remove、Destroy | 迁移和 swap-remove 正确 |
| 7 | CommandBuffer | 延迟命令和 Playback | Query 前结构变更安全点可用 |
| 8 | Query | QueryCache、ForEachChunk | 只遍历匹配 Archetype |
| 9 | SystemPipeline | EcsSystem、Pipeline | 系统顺序和 Playback 正确 |
| 10 | Unity Bridge | EcsRunner、TransformBridge | Unity 场景可跑 |
| 11 | Debug/Benchmark | Stats、DebugWindow、Benchmark | 能看到性能数据 |
| 12 | Convenience API | CreateMany、Prefab、模板系统 | 易用 API 不破坏热路径 |
| 13 | Advanced Optimization | Enableable、ChangeVersion、Jobs | 由 Benchmark 驱动 |

---

## 十七、第一周建议做到哪里

如果按稳妥节奏，第一周目标只做到阶段 1 到阶段 4：

```text
ComponentMask
TypeRegistry
EntityStore
ChunkAllocator
ArchetypeLayout
ArchetypeStore
```

不要急着写 Query。

原因：

```text
Query 的正确性完全依赖 Archetype 和 Chunk Layout
结构变更的正确性依赖 EntityStore
后面所有性能都依赖 ChunkAllocator
```

第一周验收 demo：

```text
注册 Position / Velocity
创建 [Position, Velocity] Archetype
计算 Chunk capacity
分配 Chunk
写入一个模拟 slot
确认 offset 和 pointer 正确
释放 Chunk
```

这一步看起来小，但它是整个系统的地基。

---

## 十八、不要提前做的事情

这些都先压住：

- 不要先写 Unity Editor 调试窗口。
- 不要先写 Jobs/Burst。
- 不要先写 Enableable。
- 不要先写 ChangeVersion。
- 不要先写 Source Generator。
- 不要先做完整 Authoring。
- 不要先做多线程。
- 不要先做复杂 SystemGroup。

先让单线程核心链路稳定：

```text
Create
-> Chunk write
-> Get
-> Add/Remove migration
-> CommandBuffer
-> Query
-> System
```

这条链路稳定后，再加高级能力才不会返工。
