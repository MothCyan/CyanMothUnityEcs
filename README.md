# CyanMothUnityEcs

`CyanMothUnityEcs` 是一个从零实现的 Unity 高性能 ECS 实验项目。

项目目标不是复刻 Unity DOTS 的完整生态，而是在保持可学习、可控、可逐步实现的前提下，构建一套以 `Archetype + Chunk + SoA` 为核心的数据导向 ECS。

## 当前阶段

当前处于阶段 A：地基层。

已经完成：

- ECS 目录结构
- `CyanMothUnityEcs` 程序集
- 组件标记接口 `IComponentData`
- 128 位组件掩码 `ComponentMask`
- 组件类型元数据 `ComponentType`
- 泛型组件类型缓存 `ComponentTypeCache<T>`
- 组件类型注册表 `TypeRegistry`
- 实体句柄 `Entity`
- 实体位置表 `EntityStore`
- 基础 EditMode 测试

尚未完成：

- Chunk 内存层
- Archetype 存储层
- World 创建和随机访问
- CommandBuffer
- Query
- SystemPipeline
- Unity Bridge

## 设计原则

- 底层走 `Archetype + Chunk + SoA`
- 热路径避免 GC 分配
- 结构变更通过 CommandBuffer 延迟处理
- Unity 对象不直接进入 Chunk
- Baker/SubScene 用运行时 Authoring、SpawnCatalog 和 Section Runtime 替代
- 先完成单线程核心闭环，再考虑 Burst、Jobs 和高级优化

## 文档

- [ECS_DESIGN.md](ECS_DESIGN.md)：主设计和优化链路
- [ECS_TERMS.md](ECS_TERMS.md)：专业名词学习手册
- [ECS_IMPLEMENTATION_ROADMAP.md](ECS_IMPLEMENTATION_ROADMAP.md)：实施路线图
- [ECS_NO_BAKER_SUBSCENE.md](ECS_NO_BAKER_SUBSCENE.md)：无 Baker / 无 SubScene 替代方案
- [ECS_USAGE_LAYERED_FLOW.md](ECS_USAGE_LAYERED_FLOW.md)：从使用层下钻到底层的调用链
- [ECS_API_REFERENCE.md](ECS_API_REFERENCE.md)：当前脚本、类、字段和 API 说明

## 构建验证

当前可通过：

```powershell
dotnet build UnityECS.sln --no-restore
```

Unity Test Runner 刷新程序集后，可以运行 `Assets/Scripts/ECS/Tests` 下的 EditMode 测试。

## 命名空间

核心命名空间：

```csharp
namespace CyanMothUnityEcs
{
}
```

测试命名空间：

```csharp
namespace CyanMothUnityEcs.Tests
{
}
```
