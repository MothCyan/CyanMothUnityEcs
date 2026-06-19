# CyanMothUnityEcs 从易到难学习路线

> 这份文档不是实现路线，也不是完整设计文档。它的目标是帮你按正确顺序熟悉 ECS 的脉络：先知道怎么用，再理解为什么这样设计，最后再深入到底层内存和优化。
>
> 建议你把它当成“学习地图”。每读完一层，只要能回答本层最后的问题，就可以进入下一层。

---

## 一、先建立总印象

先不要急着看 Chunk、指针、内存布局。

第一步只记住 ECS 的三句话：

```text
Entity    = 这是谁
Component = 它有什么数据
System    = 怎么批量处理这些数据
```

在 CyanMothUnityEcs 里：

```text
Entity 不保存组件
Component 只保存数据
System 批量处理组件
Chunk 才是真正存数据的地方
```

这一层先读：

```text
ECS_TERMS.md
-> ECS
-> Entity
-> Component
-> System
```

读完你要能回答：

```text
为什么 Entity 不是 GameObject？
为什么 Component 里不写行为？
为什么 System 要批量处理数据？
```

---

## 二、第一层：先从“使用者视角”理解

这一层只站在用户角度看 ECS，不看底层怎么实现。

你要先熟悉这几个动作：

```text
定义组件
创建实体
写系统
查询实体
修改组件
销毁实体
```

### 1. 定义组件

例子：

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

先理解成：

```text
Position 是位置数据
Velocity 是速度数据
它们都是纯数据
```

### 2. 创建实体

未来 API 会像这样：

```csharp
Entity entity = world.Create(
    new Position { X = 0, Y = 0, Z = 0 },
    new Velocity { X = 1, Y = 0, Z = 0 });
```

先理解成：

```text
创建了一个拥有 Position 和 Velocity 的实体
```

### 3. 写系统

未来移动系统会像这样：

```csharp
World.Query<Position, Velocity>().ForEach((Entity entity, ref Position position, ref Velocity velocity) =>
{
    position.X += velocity.X * deltaTime;
});
```

先理解成：

```text
找到所有同时拥有 Position 和 Velocity 的实体
然后批量更新它们的位置
```

这一层先不要纠结：

```text
Archetype 是怎么找的
Chunk 里怎么排布
QueryCache 怎么缓存
```

读完你要能回答：

```text
组件是干什么的？
系统是干什么的？
为什么查询要写 Query<Position, Velocity>？
```

---

## 三、第二层：理解 Entity、Component、System 的分工

这一层开始区分 ECS 和 Unity GameObject 的差异。

### GameObject 思路

传统 Unity 里经常是：

```text
GameObject
-> Transform
-> SpriteRenderer
-> PlayerController
-> HealthBehaviour
```

一个对象身上挂很多组件，逻辑也经常跟对象绑在一起。

### ECS 思路

ECS 里变成：

```text
Entity
-> 只是一个 Id

Component
-> Position
-> Velocity
-> Health
-> SpriteProxy

System
-> MovementSystem
-> DamageSystem
-> RenderSyncSystem
```

核心变化是：

```text
数据和行为分开
一批相同结构的数据放一起
系统一次处理一批数据
```

这一层重点理解：

```text
Entity 是身份
Component 是数据
System 是行为
World 是管理入口
```

读完你要能回答：

```text
为什么 Entity 不能直接保存 List<Component>？
为什么 Position 里不应该写 Move 方法？
为什么 MovementSystem 要处理一批 Position？
```

---

## 四、第三层：理解 TypeRegistry 和 ComponentMask

这一层开始进入当前已经实现的代码。

先读这些脚本：

```text
Assets/Scripts/ECS/Core/IComponentData.cs
Assets/Scripts/ECS/Core/ComponentType.cs
Assets/Scripts/ECS/Core/ComponentTypeCache.cs
Assets/Scripts/ECS/Core/TypeRegistry.cs
Assets/Scripts/ECS/Core/ComponentMask.cs
```

### 1. TypeRegistry 解决什么问题

程序里有很多组件类型：

```text
Position
Velocity
Health
Damage
Lifetime
```

底层不想每次都拿 `typeof(Position)` 做复杂查找，所以会给每个组件分配编号：

```text
Position -> 0
Velocity -> 1
Health   -> 2
```

这个编号叫 `TypeIndex`。

### 2. ComponentType 解决什么问题

`ComponentType` 是组件的元数据。

它回答：

```text
这个组件编号是多少？
这个组件占多少字节？
这个组件按多少字节对齐？
这个组件是不是 Tag？
这个组件对应哪个 C# 类型？
```

### 3. ComponentMask 解决什么问题

`ComponentMask` 表示一组组件。

比如：

```text
Position + Velocity
```

可以变成：

```text
0000 0011
```

这样判断一个 Archetype 是否包含某些组件就很快：

```text
ArchetypeMask.ContainsAll(QueryMask)
```

读完你要能回答：

```text
TypeIndex 是什么？
ComponentType 记录了什么？
ComponentMask 为什么能表示组件组合？
Query 为什么需要 ComponentMask？
```

---

## 五、第四层：理解 Entity 和 EntityStore

这一层继续看当前已经实现的代码。

先读：

```text
Assets/Scripts/ECS/Core/Entity.cs
Assets/Scripts/ECS/Core/EntityStore.cs
```

### 1. Entity 是什么

`Entity` 只有两个核心字段：

```text
Id
Version
```

`Id` 用来查表。

`Version` 用来防止旧句柄误用。

例如：

```text
Entity(10:1) 被销毁
Id 10 以后被复用
新的实体变成 Entity(10:2)
旧的 Entity(10:1) 就不能再访问
```

### 2. EntityStore 是什么

`EntityStore` 是实体位置表。

它记录：

```text
这个 Entity 是否还活着
它在哪个 Chunk
它在 Chunk 的第几个 slot
它属于哪个 Archetype
```

### 3. Entity、EntityStore、Archetype 的关系

重点记住：

```text
Entity 不持有 ComponentMask
Archetype 持有 ComponentMask
EntityStore 记录 Entity 当前属于哪个 Archetype
```

关系是：

```text
Entity
-> EntityStore
-> ArchetypeId
-> Archetype
-> ComponentMask
```

读完你要能回答：

```text
Entity 为什么需要 Version？
EntityStore 为什么要记录 Chunk 和 index？
Entity 为什么不直接知道自己有哪些组件？
```

---

## 六、第五层：理解 Archetype

这一层开始进入后续要实现的存储核心。

### Archetype 是什么

Archetype 表示一组“组件组合完全相同”的实体。

例如：

```text
Archetype A = Position + Velocity
Archetype B = Position + Velocity + Health
Archetype C = Position + SpriteProxy
```

如果两个实体都有 `Position` 和 `Velocity`，它们就属于同一个 Archetype。

### Archetype 为什么重要

Query 不想逐个实体问：

```text
你有 Position 吗？
你有 Velocity 吗？
```

而是先问 Archetype：

```text
你这一组实体是否都有 Position 和 Velocity？
```

如果 Archetype 命中，就整批处理。

这就是：

```text
Query 先匹配 Archetype
再遍历 Chunk
```

读完你要能回答：

```text
Archetype 是按什么分组的？
为什么 Query 要先匹配 Archetype？
AddComponent 为什么会导致实体换 Archetype？
```

---

## 七、第六层：理解 Chunk 和 SoA

这一层是性能的核心。

### Chunk 是什么

Chunk 是一块固定大小的连续内存。

一个 Chunk 只属于一个 Archetype。

例如 `[Position, Velocity]` 的 Chunk：

```text
Chunk
├── Header
├── Entity[]
├── Position[]
└── Velocity[]
```

### SoA 是什么

SoA 是 `Structure of Arrays`。

意思是：

```text
Position[] 连续放
Velocity[] 连续放
Health[] 连续放
```

不是每个实体一个对象。

### 为什么 Chunk + SoA 快

移动系统只关心：

```text
Position
Velocity
```

那么它可以连续扫：

```text
Position[0], Position[1], Position[2]
Velocity[0], Velocity[1], Velocity[2]
```

CPU 更容易缓存和预取。

读完你要能回答：

```text
Chunk 为什么要固定大小？
为什么同一个 Chunk 只放同一个 Archetype？
SoA 和普通对象数组有什么区别？
```

---

## 八、第七层：理解 World.Create 和 Get

这一层开始把前面的概念串起来。

未来创建实体的链路是：

```text
World.Create(Position, Velocity)
-> TypeRegistry 找组件类型
-> ComponentMask 得到组件组合
-> ArchetypeStore 找到目标 Archetype
-> ChunkAllocator 找可写 Chunk
-> EntityStore.Create 得到 Entity
-> 写入 Entity[] / Position[] / Velocity[]
-> EntityStore.SetLocation
```

未来 `Get<T>` 的链路是：

```text
World.Get<Position>(entity)
-> EntityStore.Validate
-> EntityStore.GetChunk
-> EntityStore.GetIndex
-> 根据 ArchetypeLayout 找 Position 偏移
-> 返回 ref Position
```

这一层重点是：

```text
Create 是把实体写进 Chunk
Get 是通过 EntityStore 找回 Chunk 里的组件
```

读完你要能回答：

```text
创建实体时为什么要先找 Archetype？
EntityStore.SetLocation 在什么时候调用？
Get<T> 为什么不适合每帧大量循环调用？
```

---

## 九、第八层：理解 Query

Query 是批量处理数据的入口。

未来查询链路：

```text
World.Query<Position, Velocity>
-> 生成 IncludeMask
-> QueryCache 找匹配 Archetype
-> 遍历这些 Archetype 的 Chunk
-> 拿到 Position* 和 Velocity*
-> 连续 for 循环
```

最关键的一点：

```text
Query 不遍历所有 Entity
Query 不逐实体 HasComponent
Query 只扫匹配 Archetype 下的 Chunk
```

### ForEach 和 ForEachChunk

易用版：

```text
ForEach
-> 传 Entity 和 ref Component
-> 写起来舒服
```

高性能版：

```text
ForEachChunk
-> 传 Entity* 和 Component*
-> 用户自己写 for 循环
-> 最接近底层
```

关系：

```text
ForEach 是 ForEachChunk 的包装
ForEachChunk 是真正的高性能入口
```

读完你要能回答：

```text
Query 为什么先匹配 Archetype？
ForEachChunk 为什么比 ForEach 更适合热点系统？
为什么不能每个实体都 Has 一遍？
```

---

## 十、第九层：理解结构变更和 CommandBuffer

结构变更指这些操作：

```text
AddComponent
RemoveComponent
DestroyEntity
```

### 为什么 Add/Remove 很特殊

如果一个实体原来是：

```text
Position + Velocity
```

给它 Add `Health` 以后，它就变成：

```text
Position + Velocity + Health
```

这意味着它不再属于原来的 Archetype，必须迁移到另一个 Archetype。

### 为什么需要 CommandBuffer

系统遍历 Chunk 时，不能立刻移动实体。

所以要：

```text
系统里记录命令
系统结束后统一 Playback
```

结构变更链路：

```text
CommandBuffer.Add(entity, Health)
-> Playback
-> 找源 Archetype
-> 找目标 Archetype
-> 拷贝共有组件
-> 写入新组件
-> 更新 EntityStore
-> 旧 Chunk swap-remove
```

读完你要能回答：

```text
AddComponent 为什么不是往原实体上挂一个组件？
为什么结构变更要延迟？
swap-remove 是为了解决什么问题？
```

---

## 十一、第十层：理解 SystemPipeline

系统层负责把一帧串起来。

未来一帧大概是：

```text
Unity Update
-> SystemPipeline.Update
   -> MovementSystem.OnUpdate
   -> World.Playback
   -> DamageSystem.OnUpdate
   -> World.Playback
   -> DeathSystem.OnUpdate
   -> World.Playback
   -> TransformSyncSystem.OnUpdate
   -> World.Playback
```

第一版保持简单：

```text
不做并行
不做复杂依赖排序
按添加顺序执行
每个系统后 Playback
```

读完你要能回答：

```text
为什么系统之间要有 Playback 安全点？
为什么第一版不急着做 Jobs？
为什么系统顺序要明确？
```

---

## 十二、第十一层：理解 Unity Bridge

这一层解决 ECS 和 Unity 对象怎么合作。

### 为什么不能把 Transform 放进 Chunk

`Transform`、`SpriteRenderer`、`GameObject` 是 Unity 引用对象。

它们不适合进入 Chunk，因为：

```text
不是纯数据
不能简单 memcpy
访问会跳到 Unity 引擎对象
会破坏连续内存模型
```

### 正确做法

ECS 组件里保存代理 Id：

```csharp
public struct TransformProxy : IComponentData
{
    public int Id;
}
```

Bridge 里保存真实 Unity 对象：

```text
TransformBridge
-> Transform[] transforms
```

同步系统做：

```text
Query<Position, TransformProxy>
-> TransformBridge.Get(proxy.Id)
-> 写 Transform.position
```

读完你要能回答：

```text
为什么 Unity 对象不进 Chunk？
TransformProxy 是什么？
Bridge 层解决了什么边界问题？
```

---

## 十三、第十二层：理解 Authoring 替代 Baker/SubScene

这一层理解编辑器配置怎么进入 ECS。

### Authoring 是什么

Authoring 是给 Unity Inspector 用的配置脚本。

它不是运行时 ECS 数据。

例如：

```text
EcsSpawnAuthoring
-> Count = 100
-> InitialVelocity = (1, 0, 0)
-> Health = 100
```

### 替代 Baker 的思路

不使用 Unity ECS Baker。

我们自己的流程是：

```text
Authoring MonoBehaviour
-> RuntimeAuthoringCollector
-> SpawnCatalog
-> World.CreateMany
-> Chunk 数据
```

重点：

```text
Authoring 是冷路径
Chunk 是热路径
每帧系统不读 Authoring
```

读完你要能回答：

```text
Authoring 和 Component 有什么区别？
为什么 Authoring 不能每帧参与逻辑？
RuntimeAuthoringCollector 替代了 Baker 的哪部分职责？
```

---

## 十四、第十三层：理解性能优化方向

理解完完整链路后，再看优化。

优先级从低风险到高风险：

```text
1. CreateMany 批量创建
2. QueryCache 缓存 Archetype 匹配
3. ForEachChunk 减少委托和逐实体包装
4. CommandBuffer 批量 Playback
5. ArchetypeGraph 缓存 Add/Remove 目标
6. Chunk 利用率统计和碎片整理
7. Enableable Component
8. ChangeVersion
9. Jobs/Burst
10. Source Generator
```

不要一开始就追 Jobs/Burst。

原因：

```text
如果 Chunk Layout 错了，Burst 也救不了
如果 Query 还在逐实体 Has，Jobs 只是并行地慢
如果结构迁移不正确，高级优化会放大 bug
```

读完你要能回答：

```text
为什么先优化数据布局，再谈 Burst？
为什么 CreateMany 比循环 Create 更重要？
为什么 Benchmark 比感觉更可靠？
```

---

## 十五、推荐阅读顺序

第一次阅读：

```text
1. ECS_TERMS.md
2. ECS_ARCHITECTURE_RELATIONSHIP.md
3. ECS_LEARNING_PATH.md
4. ECS_IMPLEMENTATION_ROADMAP.md
5. ECS_API_REFERENCE.md
6. ECS_DESIGN.md
```

第二次阅读，也就是准备实现时：

```text
1. ECS_IMPLEMENTATION_ROADMAP.md
2. ECS_API_REFERENCE.md
3. 当前阶段对应代码
4. ECS_ARCHITECTURE_RELATIONSHIP.md
5. ECS_DESIGN.md
```

遇到术语看不懂：

```text
回到 ECS_TERMS.md
```

遇到模块关系不清楚：

```text
回到 ECS_ARCHITECTURE_RELATIONSHIP.md
```

遇到不知道下一步做什么：

```text
回到 ECS_IMPLEMENTATION_ROADMAP.md
```

---

## 十六、按难度分的练习

### 难度 1：只看概念

目标：

```text
能说清 Entity / Component / System
```

练习：

```text
用 ECS 方式描述玩家、子弹、怪物
不要写代码，只写它们有哪些 Component，哪些 System 处理它们
```

### 难度 2：看当前代码

目标：

```text
看懂 TypeRegistry 和 EntityStore
```

练习：

```text
注册 Position / Velocity
观察它们的 TypeIndex
创建 Entity
SetLocation
Release
理解 Version 变化
```

### 难度 3：画出数据关系

目标：

```text
能画出 Entity -> EntityStore -> Chunk -> Archetype 的关系
```

练习：

```text
画一个拥有 3 个实体的 [Position, Velocity] Archetype
标出 EntityStore 里每个 Entity 的 chunk 和 index
```

### 难度 4：理解 Chunk Layout

目标：

```text
理解组件数组怎么放进 Chunk
```

练习：

```text
假设 Chunk 里有 Position[3] 和 Velocity[3]
手动画出 Entity[]、Position[]、Velocity[] 的顺序
```

### 难度 5：理解 Query

目标：

```text
能解释 Query 为什么快
```

练习：

```text
列出 4 个 Archetype
手动判断 Query<Position, Velocity> 会命中哪些
```

### 难度 6：理解结构变更

目标：

```text
能解释 Add/Remove 为什么是迁移
```

练习：

```text
画出 Entity 从 [Position, Velocity] Add Health 后
如何移动到 [Position, Velocity, Health]
```

### 难度 7：理解 Unity Bridge

目标：

```text
能解释 ECS 数据和 Unity 对象怎么同步
```

练习：

```text
设计一个 SpriteRendererBridge
说明 ECS 组件里保存什么，Bridge 表里保存什么
```

---

## 十七、学习时最容易混的点

### 1. Entity 不是数据容器

错误理解：

```text
Entity 里面有一堆 Component
```

正确理解：

```text
Entity 是 Id + Version
真实数据在 Chunk
```

### 2. ComponentMask 不属于单个 Entity

更准确地说：

```text
Archetype 持有 ComponentMask
Query 持有 Include/Exclude Mask
Entity 通过 EntityStore 间接知道自己属于哪个 Archetype
```

### 3. AddComponent 不是原地添加

错误理解：

```text
给 Entity 的组件列表 push 一个 Health
```

正确理解：

```text
实体从旧 Archetype 迁移到新 Archetype
```

### 4. Authoring 不是 ECS 数据

错误理解：

```text
Authoring 每帧参与 ECS 查询
```

正确理解：

```text
Authoring 只负责编辑器配置和启动时转换
运行时热路径只读 Chunk 数据
```

### 5. Burst 不是第一步

错误理解：

```text
加 Burst 就会快
```

正确理解：

```text
先让数据布局、Query、Chunk 遍历正确
再让 Burst 优化已经正确的热路径
```

---

## 十八、最终学习目标

学完这份路线，你应该能从一句业务需求推到底层链路。

例如：

```text
我要让所有子弹移动
```

你应该能拆成：

```text
Component:
-> Position
-> Velocity
-> BulletTag

System:
-> BulletMoveSystem

Query:
-> Query<Position, Velocity, BulletTag>

Archetype:
-> 匹配所有包含 Position + Velocity + BulletTag 的 Archetype

Chunk:
-> 连续读取 Velocity
-> 连续写入 Position

结构变更:
-> 子弹生命周期结束后 CommandBuffer.Destroy
-> Playback 时 swap-remove 并释放 Entity

Unity Bridge:
-> 如果要显示 Sprite，用 SpriteProxyId 找 SpriteRenderer
-> RenderSyncSystem 写回 Unity 对象
```

这就是从应用层一路想到内存层的完整脉络。

等你能这样拆业务，我们再继续实现底层代码时，每一步都会更稳。
