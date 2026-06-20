# CyanMothUnityEcs 总文档

> 这份文档是整个项目的阅读入口。它不替代其他文档，而是告诉你：想解决某个问题时，应该先读哪一份。

---

## 一、先看什么

如果你刚打开项目，建议按这个顺序读：

```text
../README.md
ECS_SCRIPT_OVERVIEW.md
ECS_LEARNING_PATH.md
ECS_CONCEPTS_AND_PRINCIPLES.md
ECS_DESIGN.md
ECS_SCRIPT_DETAILS.md
ECS_API_REFERENCE.md
```

原因：

```text
README 先告诉你项目是什么
脚本速览先建立地图
学习路线帮你按难度理解
概念原理文档帮你把底层机制讲通
设计文档解释为什么这样做
脚本详细说明解释每个文件怎么分工
API 手册用于查字段和方法
```

---

## 二、文档总览

| 文档 | 定位 | 适合什么时候看 |
|---|---|---|
| [README.md](../README.md) | 项目首页 | 第一次打开项目，了解当前状态和运行方式 |
| [ECS_SCRIPT_OVERVIEW.md](ECS_SCRIPT_OVERVIEW.md) | 脚本速览 | 想快速知道每个脚本做什么 |
| [ECS_SCRIPT_DETAILS.md](ECS_SCRIPT_DETAILS.md) | 脚本详细说明 | 想理解每个脚本、每层模块和关键链路 |
| [ECS_API_REFERENCE.md](ECS_API_REFERENCE.md) | API 手册 | 查类、字段、属性、方法的具体作用 |
| [ECS_DESIGN.md](ECS_DESIGN.md) | 主设计文档 | 理解整体架构、优化方案、和 Unity ECS 的区别 |
| [ECS_IMPLEMENTATION_ROADMAP.md](ECS_IMPLEMENTATION_ROADMAP.md) | 实现路线图 | 想知道从哪里开始、按什么顺序实现 |
| [ECS_LEARNING_PATH.md](ECS_LEARNING_PATH.md) | 学习路线 | 想从易到难掌握 ECS 脉络 |
| [ECS_CONCEPTS_AND_PRINCIPLES.md](ECS_CONCEPTS_AND_PRINCIPLES.md) | 概念原理讲义 | 想把 Entity、Archetype、Chunk、Query、CommandBuffer、Bridge 的原理串起来 |
| [ECS_TERMS.md](ECS_TERMS.md) | 专业名词手册 | 遇到陌生概念时查解释 |
| [ECS_USAGE_LAYERED_FLOW.md](ECS_USAGE_LAYERED_FLOW.md) | 使用链路下钻 | 想从“怎么用”一路看到“底层怎么跑” |
| [ECS_ARCHITECTURE_RELATIONSHIP.md](ECS_ARCHITECTURE_RELATIONSHIP.md) | 模块关系图谱 | 想理解内存层、存储层、查询层、应用层的关系 |
| [ECS_NO_BAKER_SUBSCENE.md](ECS_NO_BAKER_SUBSCENE.md) | Baker/SubScene 替代方案 | 想理解为什么和怎么干掉 Baker/SubScene |

---

## 三、按目标选择文档

### 1. 我想快速知道项目做到哪了

读：

```text
README.md
ECS_SCRIPT_OVERVIEW.md
```

你会得到：

```text
当前已实现能力
每个脚本大概功能
最小 Demo 怎么跑
```

### 2. 我想学习 ECS 概念

读：

```text
ECS_LEARNING_PATH.md
ECS_CONCEPTS_AND_PRINCIPLES.md
ECS_TERMS.md
```

你会得到：

```text
Entity / Component / System 是什么
Archetype / Chunk / SoA 是什么
EntityStore / Query / CommandBuffer 如何协作
为什么 Entity 不直接存组件
为什么 Query 不逐实体 Has
```

### 3. 我想理解设计方案

读：

```text
ECS_DESIGN.md
ECS_ARCHITECTURE_RELATIONSHIP.md
ECS_USAGE_LAYERED_FLOW.md
```

你会得到：

```text
为什么不使用 Sparse Set
为什么选择 Archetype + Chunk
完整 ECS 链路
从内存到应用层的模块关系
使用层如何一步步下钻到底层
```

### 4. 我想继续实现功能

读：

```text
ECS_IMPLEMENTATION_ROADMAP.md
ECS_SCRIPT_DETAILS.md
ECS_API_REFERENCE.md
```

你会得到：

```text
阶段化实现顺序
当前脚本职责
每个 API 的具体用途
```

### 5. 我想查某个脚本或 API

读：

```text
ECS_SCRIPT_OVERVIEW.md
ECS_SCRIPT_DETAILS.md
ECS_API_REFERENCE.md
```

推荐查法：

```text
先用脚本速览定位文件
再用脚本详细说明理解模块关系
最后用 API 手册查具体字段和方法
```

### 6. 我想理解无 Baker / 无 SubScene

读：

```text
ECS_NO_BAKER_SUBSCENE.md
ECS_DESIGN.md 的“无 Baker / 无 SubScene”章节
ECS_USAGE_LAYERED_FLOW.md 的 Unity Bridge 链路
```

你会得到：

```text
Authoring 是什么
运行时转换怎么替代 Baker
Bridge 怎么隔离 UnityEngine.Object
这种方案的性能边界在哪里
```

---

## 四、当前代码对应的核心链路

### 运行时 Demo 链路

```text
EcsRunner.Initialize
  -> 创建 World
  -> 创建 SystemPipeline
  -> 创建 TransformBridge / SpriteRendererBridge
  -> 注册 Position2DMoveSystem
  -> 注册 TransformSyncSystem
  -> 注册 SpriteRendererSyncSystem

EcsDemoSpawner.Spawn
  -> 创建 GameObject
  -> 添加 SpriteRenderer
  -> 添加 Position2DAuthoring
  -> EcsRunner.Convert
  -> World.Create
  -> Entity 进入 Archetype + Chunk

EcsRunner.Tick
  -> SystemPipeline.Update
  -> Position2DMoveSystem 更新 Position2D
  -> TransformSyncSystem 写回 Transform
  -> SpriteRendererSyncSystem 写回 SpriteRenderer
```

### 底层存储链路

```text
ComponentType / TypeRegistry
  -> ComponentMask
  -> ArchetypeStore
  -> ArchetypeLayout
  -> ChunkAllocator
  -> Chunk
  -> EntityStore
```

### 查询执行链路

```text
World.Query<T...>
  -> QueryCache 匹配 Archetype
  -> 缓存组件 offset / slot
  -> ForEach / ForEachChunk 遍历 Chunk
  -> 标记 ChangeVersion
```

### 结构变更链路

```text
CommandBuffer.Add / Remove / Destroy
  -> 记录命令
  -> 合并同实体命令
  -> Playback 排序
  -> World.AddRaw / RemoveRaw / Destroy
  -> 实体迁移到新 Archetype
```

---

## 五、文档维护规则

后续每次新增功能，按这个顺序更新文档：

```text
1. 更新 ECS_SCRIPT_OVERVIEW.md
2. 更新 ECS_SCRIPT_DETAILS.md
3. 如果新增公开 API，更新 ECS_API_REFERENCE.md
4. 如果改变整体架构，更新 ECS_DESIGN.md
5. 如果改变实现阶段，更新 ECS_IMPLEMENTATION_ROADMAP.md
6. 如果新增概念，更新 ECS_TERMS.md
7. 最后检查 README.md 和 DOCUMENTATION.md 是否需要同步
```

新增代码时至少要同步：

```text
脚本功能速览
脚本详细说明
API 手册
测试说明
```

---

## 六、推荐阅读路线

### 初学路线

```text
README.md
ECS_TERMS.md
ECS_LEARNING_PATH.md
ECS_SCRIPT_OVERVIEW.md
ECS_USAGE_LAYERED_FLOW.md
```

### 实现路线

```text
README.md
ECS_IMPLEMENTATION_ROADMAP.md
ECS_SCRIPT_DETAILS.md
ECS_API_REFERENCE.md
Assets/Scripts/ECS/Tests
```

### 优化路线

```text
ECS_DESIGN.md
ECS_ARCHITECTURE_RELATIONSHIP.md
ECS_API_REFERENCE.md
ECS_SCRIPT_DETAILS.md
```

### Unity 接入路线

```text
ECS_NO_BAKER_SUBSCENE.md
ECS_USAGE_LAYERED_FLOW.md
ECS_SCRIPT_DETAILS.md
ECS_API_REFERENCE.md
```

---

## 七、一句话总览

```text
README.md 是门口
DOCUMENTATION.md 是目录
ECS_DESIGN.md 是设计脑图
ECS_LEARNING_PATH.md 是学习路线
ECS_TERMS.md 是词典
ECS_IMPLEMENTATION_ROADMAP.md 是施工顺序
ECS_SCRIPT_OVERVIEW.md 是脚本地图
ECS_SCRIPT_DETAILS.md 是脚本讲解
ECS_API_REFERENCE.md 是 API 字典
```
