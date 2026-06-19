# CyanMothUnityEcs 脚本功能速览

> 这份是快速地图：先知道每个脚本大概负责什么，再去看详细文档和源码。

---

## 程序集

| 脚本 | 功能 |
|---|---|
| `Assets/Scripts/ECS/CyanMothUnityEcs.asmdef` | 主 ECS 程序集配置，开启 unsafe，允许 UnityEngine 引用。 |
| `Assets/Scripts/ECS/Tests/CyanMothUnityEcs.Tests.asmdef` | ECS 测试程序集配置，只在 Editor 测试环境编译。 |
| `Assets/Scripts/ECS/Core/AssemblyInfo.cs` | 给程序集开放内部成员，方便测试程序集访问 internal API。 |

---

## Core 核心层

| 脚本 | 功能 |
|---|---|
| `IComponentData.cs` | 定义 ECS 组件标记接口，以及可启用组件接口。 |
| `ComponentMask.cs` | 用 128 位掩码表示组件集合，用于 Archetype、Query、结构变更匹配。 |
| `ComponentType.cs` | 保存组件类型的底层描述，例如类型索引、大小、对齐、是否 enableable。 |
| `ComponentTypeCache.cs` | 泛型组件类型缓存，避免反复查注册表。 |
| `TypeRegistry.cs` | 全局组件类型注册中心，给组件分配 TypeIndex。 |
| `Entity.cs` | 实体句柄，保存实体 Id 和版本号。 |
| `EntityStore.cs` | 实体位置表，负责实体生命周期、版本、所在 Chunk 和行号。 |
| `UnsafeUtil.cs` | unsafe 内存工具，处理对齐、清零、复制、SizeOf。 |
| `World.cs` | ECS 世界入口，持有实体表、ArchetypeStore、ChunkAllocator、CommandBuffer。 |
| `World.Create.cs` | 单个实体创建和原始组件写入逻辑。 |
| `World.CreateMany.cs` | 批量创建实体，按 Chunk 分批连续写入。 |
| `World.Access.cs` | `Has/Get/Set` 等组件访问 API。 |
| `World.StructuralChanges.cs` | `Add/Remove/Destroy` 等结构变更逻辑。 |
| `World.Query.cs` | Query 遍历、只读遍历、变更过滤、enableable 过滤。 |
| `World.Debugging.cs` | 调试统计和 ChangeVersion 读取 API。 |

---

## Storage 存储层

| 脚本 | 功能 |
|---|---|
| `Chunk.cs` | Chunk 头部结构，保存实体数组、组件数组、enabled bitset、change version。 |
| `ChunkAllocator.cs` | Chunk 原生内存分配器，统一申请和释放固定大小 Chunk。 |
| `ArchetypeLayout.cs` | 根据组件组合计算 Chunk 内存布局。 |
| `Archetype.cs` | 同一种组件组合的 Chunk 集合，管理可写 Chunk 和实体分配。 |
| `ArchetypeStore.cs` | Archetype 注册和查找中心，按 ComponentMask 复用 Archetype。 |

---

## Query 查询层

| 脚本 | 功能 |
|---|---|
| `QueryDelegates.cs` | Query 回调委托定义。 |
| `QueryCache.cs` | 缓存 Query 匹配到的 Archetype、组件 offset、slot。 |
| `Query.cs` | 用户侧 Query 句柄，提供 `ForEach`、`ForEachReadOnly`、`ForEachWrite` 等入口。 |

---

## Commands 命令层

| 脚本 | 功能 |
|---|---|
| `CommandKind.cs` | 命令类型枚举：Add、Remove、Destroy。 |
| `CommandBuffer.cs` | 延迟结构变更缓冲区，支持合并、排序回放、raw payload 存储。 |

---

## Systems 系统层

| 脚本 | 功能 |
|---|---|
| `EcsSystem.cs` | ECS 系统基类，提供 World、CommandBuffer、生命周期和版本记录。 |
| `QuerySystem.cs` | 便捷系统基类，自动缓存 Query。 |
| `SystemPipeline.cs` | 系统管线，按顺序执行系统并统一 Playback。 |

---

## Debugging 调试与基准

| 脚本 | 功能 |
|---|---|
| `WorldStats.cs` | World 统计快照。 |
| `EcsBenchmarkResult.cs` | Benchmark 结果结构。 |
| `EcsBenchmark.cs` | 内置基准测试，验证创建、查询、结构变更性能趋势。 |

---

## Unity Bridge 使用层

| 脚本 | 功能 |
|---|---|
| `Position2D.cs` | 2D 位置、速度组件，以及运行时 Authoring 转换组件。 |
| `TransformProxy.cs` | Transform 桥接代理组件，只保存桥接 Id。 |
| `TransformBridge.cs` | Transform 和 SpriteRenderer 的托管桥接表。 |
| `TransformSyncSystem.cs` | 位置运动系统、Transform 同步系统、SpriteRenderer 同步系统。 |
| `EcsRunner.cs` | Unity 场景中的 ECS 启动器，创建 World、Pipeline、Bridge 并驱动更新。 |
| `EcsDebugOverlay.cs` | 运行时调试浮层，显示实体、Chunk、Archetype、桥接数量等统计。 |
| `EcsDemoSpawner.cs` | 最小 Demo 生成器，批量生成会移动的 2D ECS 对象。 |

---

## Tests 测试脚本

| 脚本 | 功能 |
|---|---|
| `TypeRegistryTests.cs` | 验证组件类型注册。 |
| `ComponentMaskTests.cs` | 验证组件掩码。 |
| `EntityStoreTests.cs` | 验证实体生命周期和位置表。 |
| `ChunkAllocatorTests.cs` | 验证 Chunk 分配和释放。 |
| `ArchetypeStoreTests.cs` | 验证 Archetype 创建、复用和布局。 |
| `WorldCreateAccessTests.cs` | 验证创建、读取、写入组件。 |
| `WorldCreateManyTests.cs` | 验证批量创建优化。 |
| `WorldDestroyTests.cs` | 验证实体销毁和版本失效。 |
| `WorldStructuralChangeTests.cs` | 验证 Add/Remove/Destroy 结构变更。 |
| `CommandBufferTests.cs` | 验证延迟命令记录、合并和回放。 |
| `QueryTests.cs` | 验证 Query、只读 Query、变更过滤、enableable、指定写入组件。 |
| `QuerySystemTests.cs` | 验证便捷 QuerySystem 基类。 |
| `SystemPipelineTests.cs` | 验证系统生命周期、执行顺序和版本提交。 |
| `ChangeVersionTests.cs` | 验证 Chunk ChangeVersion。 |
| `WorldStatsTests.cs` | 验证 World 调试统计。 |
| `EcsBenchmarkTests.cs` | 验证基准测试能正常运行。 |
| `UnityBridgeTests.cs` | 验证 Unity Bridge、Authoring、Runner、DemoSpawner。 |

---

## 最小运行链路

```text
EcsRunner 创建 World 和 SystemPipeline
EcsDemoSpawner 生成 GameObject
Position2DAuthoring 转成 Entity
Entity 存入 Archetype + Chunk
Position2DMoveSystem 更新 Position2D
TransformSyncSystem 同步 Transform
SpriteRendererSyncSystem 同步 SpriteRenderer
EcsDebugOverlay 显示 WorldStats
```
