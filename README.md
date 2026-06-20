# CyanMothUnityEcs

`CyanMothUnityEcs` 是一个从零实现的 Unity 高性能 ECS 实验项目。

它不追求完整复刻 Unity DOTS 生态，而是以更轻、更直接、更容易学习和控制的方式，构建一套基于 `Archetype + Chunk + SoA` 的数据导向 ECS。

核心目标：

- 不使用 Sparse Set。
- 底层走 Archetype + Chunk + SoA。
- 热路径尽量避免 GC 和托管对象访问。
- UnityEngine.Object 不进入 Chunk，只通过 Bridge 间接同步。
- 用运行时 Authoring 替代 Baker/SubScene 的复杂入口。
- 牺牲一部分扩展性，换取更清晰的使用链路和更低的上手难度。

---

## 当前状态

当前已经形成一条轻量级可运行闭环：

```text
EcsRunner 创建 World 和 SystemPipeline
EcsDemoSpawner 生成 GameObject
Position2DAuthoring 转换 Entity
Entity 进入 Archetype + Chunk
Position2DMoveSystem 更新 Position2D
TransformSyncSystem 同步 Unity Transform
SpriteRendererSyncSystem 同步 SpriteRenderer
EcsDebugOverlay 显示 World 统计
```

已经实现的主要能力：

- 组件类型系统：`IComponentData`、`ComponentType`、`TypeRegistry`、`ComponentMask`
- 实体系统：`Entity`、`EntityStore`
- 存储层：`ChunkAllocator`、`Chunk`、`ArchetypeLayout`、`Archetype`、`ArchetypeStore`
- World API：`Create`、`CreateMany`、`Has`、`Get`、`Set`、`Add`、`Remove`、`Destroy`
- Query：`ForEach`、`ForEachChunk`、`ForEachReadOnly`、`ForEachWrite`、Changed Query、Enableable Component
- 系统层：`EcsSystem`、`QuerySystem`、`SystemPipeline`
- 命令层：`CommandBuffer`、结构变更延迟回放、命令合并、回放排序
- 调试层：`WorldStats`、`EcsBenchmark`、`EcsDebugOverlay`
- Unity Bridge：`EcsRunner`、`Position2DAuthoring`、`TransformBridge`、`SpriteRendererBridge`
- Demo：`EcsDemoSpawner`

---

## 文档入口

所有项目文档已经统一整理到 [Docs](Docs/) 文件夹。

先看总文档：

- [Docs/DOCUMENTATION.md](Docs/DOCUMENTATION.md)：文档总入口，按目标、学习、实现、查 API、看源码分路线整理。

最常用文档：

- [Docs/ECS_SCRIPT_OVERVIEW.md](Docs/ECS_SCRIPT_OVERVIEW.md)：所有脚本功能速览。
- [Docs/ECS_SCRIPT_DETAILS.md](Docs/ECS_SCRIPT_DETAILS.md)：所有脚本详细说明。
- [Docs/ECS_API_REFERENCE.md](Docs/ECS_API_REFERENCE.md)：类、字段、属性、API 的逐项说明。
- [Docs/ECS_LEARNING_PATH.md](Docs/ECS_LEARNING_PATH.md)：从易到难的学习路线。
- [Docs/ECS_CONCEPTS_AND_PRINCIPLES.md](Docs/ECS_CONCEPTS_AND_PRINCIPLES.md)：概念与原理学习讲义，把核心机制串成完整理解链路。
- [Docs/ECS_DESIGN.md](Docs/ECS_DESIGN.md)：整体设计、优化链路和创新方案。

专项文档：

- [Docs/ECS_TERMS.md](Docs/ECS_TERMS.md)：专业名词解释。
- [Docs/ECS_IMPLEMENTATION_ROADMAP.md](Docs/ECS_IMPLEMENTATION_ROADMAP.md)：阶段化实现路线图。
- [Docs/ECS_USAGE_LAYERED_FLOW.md](Docs/ECS_USAGE_LAYERED_FLOW.md)：从使用层逐层下钻到底层的调用链。
- [Docs/ECS_ARCHITECTURE_RELATIONSHIP.md](Docs/ECS_ARCHITECTURE_RELATIONSHIP.md)：从内存到应用层的模块关系。
- [Docs/ECS_NO_BAKER_SUBSCENE.md](Docs/ECS_NO_BAKER_SUBSCENE.md)：替代 Baker/SubScene 的方案。

---

## 最小使用方式

在 Unity 场景中：

1. 新建一个 GameObject，挂 `EcsRunner`。
2. 新建一个 GameObject，挂 `EcsDemoSpawner`。
3. 可选：新建一个 GameObject，挂 `EcsDebugOverlay`。
4. 运行场景。

运行后，`EcsDemoSpawner` 会生成一批带速度的 2D 对象；对象会被 `Position2DAuthoring` 转换为 ECS Entity，再由 ECS 系统驱动移动，并同步回 Unity Transform。

---

## 构建验证

命令行验证：

```powershell
dotnet build UnityECS.sln --no-restore
dotnet test LightEcs.Tests.csproj --no-build --verbosity normal
```

Unity 内也可以通过 Test Runner 运行 `Assets/Scripts/ECS/Tests` 下的 EditMode 测试。

---

## 命名空间

运行时代码：

```csharp
namespace CyanMothUnityEcs
{
}
```

测试代码：

```csharp
namespace CyanMothUnityEcs.Tests
{
}
```
