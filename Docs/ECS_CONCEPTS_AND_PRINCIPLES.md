# CyanMothUnityEcs 概念与原理学习文档

> 这份文档是学习型讲义。它不只是解释名词，而是按一条完整链路说明：为什么需要 ECS、为什么选择 `Archetype + Chunk + SoA`、实体如何进入内存、查询如何跑起来、一帧更新到底发生了什么。

---

## 一、这份文档怎么读

如果你是第一次系统学习 ECS，建议按顺序读，不要跳读。

```text
先理解 ECS 为什么存在
再理解 Entity / Component / System 怎么分工
再理解 Archetype / Chunk / SoA 为什么能快
最后理解 World / Query / CommandBuffer / Bridge 怎么协作
```

如果你只是卡在某个点，也可以直接看对应章节：

| 我卡住的问题 | 先看章节 |
|---|---|
| Entity 为什么不直接存组件 | 第四章 |
| ComponentMask 到底有什么用 | 第七章 |
| EntityStore、Entity、Archetype 是什么关系 | 第八章 |
| Chunk 为什么是性能核心 | 第十章 |
| Query 为什么不逐实体检查 | 第十四章 |
| Add/Remove 为什么要延迟执行 | 第十八章、第十九章 |
| Authoring 怎么替代 Baker | 第二十二章 |
| SpriteRenderer 怎么接入 ECS | 第二十三章 |

---

## 二、ECS 的核心思想

传统 Unity 写法通常是：

```text
GameObject
  -> MonoBehaviour
  -> 字段
  -> Update()
```

这种方式很好理解，但当对象数量变多时会出现几个问题：

- 数据分散在很多对象里，CPU 读取时容易到处跳。
- 每个对象都有自己的脚本入口，调度成本高。
- 同一类逻辑分散在很多实例上，不容易批量处理。
- 引用对象、虚函数、托管分配容易混入热路径。

ECS 的思路是反过来：

```text
Entity 只表示“是谁”
Component 只保存“有什么数据”
System 只负责“对一批数据做什么”
```

也就是说，ECS 不让每个对象自己更新自己，而是让一个系统一次处理一批拥有相同数据的实体。

简单例子：

```text
Position2DMoveSystem
  -> 找到所有同时拥有 Position2D 和 Velocity2D 的实体
  -> 连续遍历它们的位置和速度
  -> 批量更新位置
```

这样做的核心收益是：

- 数据连续，CPU 缓存更容易命中。
- 逻辑集中，系统可以批量处理。
- 查询结果稳定，可以提前缓存。
- 后续更容易接入 Burst、Jobs 或自定义并行调度。

---

## 三、Entity / Component / System 的分工

### Entity 是什么

`Entity` 是实体句柄，可以理解为一个轻量 ID。

它通常包含：

```text
Id       -> 实体编号
Version  -> 版本号
```

`Id` 用来定位实体，`Version` 用来判断这个实体句柄是否还有效。

为什么要有版本号？

因为实体会被销毁，旧的 `Id` 以后可能被复用。如果没有版本号，旧句柄可能误操作新实体。

```text
Entity(10, version 1) 被销毁
Id 10 后来复用给新实体 Entity(10, version 2)
旧句柄拿着 version 1 来访问时，会被识别为无效
```

### Component 是什么

`Component` 是纯数据。

比如：

```csharp
public struct Position2D : IComponentData
{
    public float X;
    public float Y;
}
```

组件不应该负责行为，不应该写复杂逻辑，也不应该默认持有 `GameObject`、`Transform`、`SpriteRenderer` 这类 Unity 对象引用。

原因是：ECS 的性能来自“数据可以被连续、简单、批量地处理”。如果组件里混入大量托管对象引用，Chunk 内存就会被污染，热路径也会变得不可控。

### System 是什么

`System` 是处理数据的逻辑。

比如：

```text
Position2DMoveSystem
  读取 Velocity2D
  读写 Position2D
```

System 不拥有实体，也不拥有组件。它只是向 `World` 发起查询，然后处理查询到的数据。

---

## 四、为什么 Entity 不持有组件

初学时很容易以为：

```text
Entity
  -> Position2D
  -> Velocity2D
  -> SpriteRenderData
```

但高性能 ECS 不这样做。

真正的关系更像：

```text
Entity
  -> EntityStore 中的一条定位记录
  -> 指向某个 Chunk 的某个 index
  -> 组件数据存在 Chunk 里
```

也就是说，Entity 不保存组件本体，只保存定位到组件数据的能力。

这样做有三个原因：

1. 实体句柄保持很小，传递成本低。
2. 组件可以按类型连续摆放，方便 CPU 批量读取。
3. 实体移动到新 Archetype 时，只需要更新定位记录，不需要让 Entity 自己管理复杂状态。

可以把 `Entity` 理解成“座位号”，把 `Chunk` 理解成“座位所在的车厢”。乘客的数据不写在票上，而是在车厢座位里。

---

## 五、为什么不用 Sparse Set

Sparse Set 的常见结构是：

```text
Position2D Pool
Velocity2D Pool
Health Pool
```

每种组件都有自己的池。查询 `Position2D + Velocity2D` 时，通常会以一个池为主遍历，再去其他池检查这个实体有没有对应组件。

这种方式实现简单，增删组件也方便，但问题是：

- 多组件查询需要逐实体判断。
- 不同组件的数据分散在不同数组里。
- 查询越复杂，跳转和分支越多。
- 很难保证一批实体的多种组件在同一段连续内存中。

我们的目标是“底层极致优化”，所以不采用 Sparse Set 作为核心存储。

这里选择：

```text
Archetype + Chunk + SoA
```

也就是把拥有相同组件组合的实体放在一起，再按 Chunk 连续存储。

---

## 六、为什么选择 Archetype + Chunk + SoA

### Archetype 解决什么问题

`Archetype` 表示一种组件组合。

比如：

```text
Archetype A = Position2D + Velocity2D
Archetype B = Position2D + Velocity2D + SpriteRenderData
Archetype C = Position2D + Health
```

拥有相同组件组合的实体会被放进同一个 Archetype。

这样 Query 就不需要逐实体检查“你有没有 Position2D，你有没有 Velocity2D”。它只需要先判断：

```text
这个 Archetype 的组件组合是否包含 Query 需要的组件？
```

如果包含，就遍历它下面的所有 Chunk。

### Chunk 解决什么问题

`Chunk` 是固定大小的一块连续内存。

一个 Archetype 会拥有多个 Chunk：

```text
Archetype(Position2D + Velocity2D)
  -> Chunk 0
  -> Chunk 1
  -> Chunk 2
```

每个 Chunk 里放一批实体的组件数据。

Chunk 的价值是让系统遍历时能一块一块处理，减少随机访问，提高缓存命中。

### SoA 解决什么问题

`SoA` 是 Structure of Arrays，意思是“数组组成的结构”。

对于 `Position2D + Velocity2D`，内存不是这样放：

```text
Entity0: Position, Velocity
Entity1: Position, Velocity
Entity2: Position, Velocity
```

而是更接近这样：

```text
Position2D[]:
  Position0, Position1, Position2

Velocity2D[]:
  Velocity0, Velocity1, Velocity2
```

这样当系统只读写 `Position2D` 时，CPU 可以连续读取一整段 `Position2D` 数据。

---

## 七、TypeRegistry / ComponentType / ComponentMask 原理

### TypeRegistry

`TypeRegistry` 是组件类型登记表。

它负责把 C# 类型登记成 ECS 内部能快速处理的类型信息。

比如：

```text
Position2D      -> ComponentType Id 0
Velocity2D      -> ComponentType Id 1
SpriteRenderData -> ComponentType Id 2
```

这样运行时不用反复拿 `System.Type` 做复杂判断，而是用整数 ID 和位掩码做快速判断。

### ComponentType

`ComponentType` 是一个组件类型的元数据。

它通常包含：

```text
Id          -> 类型编号
Size        -> 单个组件占多少字节
Alignment   -> 对齐要求
IsEnableable -> 是否支持启停
```

这些信息会被 `ArchetypeLayout` 用来计算 Chunk 内每种组件应该放在哪里。

### ComponentMask

`ComponentMask` 是组件组合的快速表示。

可以把它理解成一排开关：

```text
Position2D 开
Velocity2D 开
Health 关
SpriteRenderData 开
```

如果 `Position2D` 的类型 ID 是 0，`Velocity2D` 的类型 ID 是 1，那么拥有这两个组件的实体组合可以表示为：

```text
bit 0 = 1
bit 1 = 1
```

Query 判断一个 Archetype 是否匹配时，就可以用位运算：

```text
ArchetypeMask 包含 QueryMask
```

这比逐实体逐组件检查快得多。

---

## 八、EntityStore、Entity、Archetype 的关系

三者关系可以这样理解：

```text
Entity
  -> 一个外部句柄

EntityStore
  -> 记录 Entity 当前在哪里

Archetype
  -> 保存拥有相同组件组合的一批实体
```

更具体一点：

```text
Entity(Id, Version)
  -> EntityStore[Id]
      Version
      ArchetypeId
      Chunk
      IndexInChunk
```

当你调用：

```text
World.Get<Position2D>(entity)
```

底层会走：

```text
1. 用 entity.Id 找到 EntityStore 记录
2. 检查 Version 是否匹配
3. 找到 Chunk
4. 找到 Position2D 在 Chunk 里的 offset
5. 用 IndexInChunk 找到该实体对应的 Position2D 数据
```

所以：

- `Entity` 不存组件。
- `EntityStore` 存实体位置。
- `Archetype` 管组件组合。
- `Chunk` 存真正的组件数据。

---

## 九、Archetype 原理

`Archetype` 是 ECS 存储结构的分组单位。

实体的组件组合决定它属于哪个 Archetype。

比如一个实体有：

```text
Position2D
Velocity2D
```

它就进入：

```text
Archetype(Position2D + Velocity2D)
```

当它后来添加 `SpriteRenderData`，组件组合变成：

```text
Position2D
Velocity2D
SpriteRenderData
```

它就必须迁移到另一个 Archetype。

```text
旧 Archetype(Position2D + Velocity2D)
  -> 迁移
新 Archetype(Position2D + Velocity2D + SpriteRenderData)
```

这就是结构变更。

Archetype 的核心职责：

- 保存组件组合的 `ComponentMask`。
- 保存该组合对应的 `ArchetypeLayout`。
- 管理多个 Chunk。
- 提供插入、删除、迁移实体的能力。

---

## 十、Chunk 原理

`Chunk` 是 ECS 的热路径核心。

一个 Chunk 里会存：

```text
Entity[]             -> 这一行对应哪个实体
ComponentArray A     -> 第一种组件数据
ComponentArray B     -> 第二种组件数据
ComponentArray C     -> 第三种组件数据
ChangeVersion[]      -> 每种组件的变化版本
EnableBits[]         -> 可启停组件的启用位
```

它的特点是：

- 固定容量。
- 连续内存。
- 属于某一个 Archetype。
- 只存该 Archetype 组件组合里的组件。

系统遍历时通常是：

```text
for each matching archetype
  for each chunk in archetype
    get component arrays
    for i in chunk.Count
      process component[i]
```

这比“遍历所有实体，再逐个判断有没有组件”更适合高性能场景。

---

## 十一、Chunk Layout / Offset / Align 原理

`ArchetypeLayout` 负责计算一个 Chunk 里每种数据放在哪里。

它要处理三个问题：

### Size

`Size` 是组件大小。

比如：

```text
Position2D = 8 bytes
Velocity2D = 8 bytes
```

如果一个 Chunk 能放 512 个实体，那么 `Position2D` 数组需要：

```text
8 * 512 = 4096 bytes
```

### Offset

`Offset` 是某种组件数组在 Chunk 内的起始位置。

比如：

```text
Chunk memory
  Entity array starts at 0
  Position2D array starts at 1024
  Velocity2D array starts at 5120
```

拿组件时会用：

```text
componentAddress = chunkBase + componentOffset + index * componentSize
```

### Alignment

`Alignment` 是内存对齐。

有些数据类型希望从特定倍数的地址开始，这样 CPU 读取更快，也更安全。

所以布局计算不能只是简单累加大小，还要在每段数据开始前做对齐。

---

## 十二、World 是什么

`World` 是 ECS 的总入口。

它把底层模块组织在一起：

```text
World
  -> TypeRegistry
  -> EntityStore
  -> ArchetypeStore
  -> QueryCache
  -> CommandBuffer
  -> SystemPipeline
```

业务代码通常不直接操作 `Chunk` 或 `Archetype`，而是通过 `World` 做：

```text
Create
Get
Set
Add
Remove
Destroy
Query
```

这样可以保证所有结构变更、版本检查、查询缓存更新都走统一入口。

---

## 十三、创建实体的完整原理

以创建一个拥有 `Position2D + Velocity2D` 的实体为例。

```text
World.Create(Position2D, Velocity2D)
```

底层大致流程是：

```text
1. TypeRegistry 确认 Position2D / Velocity2D 已注册
2. 生成 ComponentMask
3. ArchetypeStore 查找或创建对应 Archetype
4. Archetype 找到一个有空位的 Chunk
5. EntityStore 分配一个 Entity Id 和 Version
6. 把 Entity 写入 Chunk 的 Entity 数组
7. 把组件数据写入 Chunk 对应数组
8. EntityStore 记录 entity -> archetype/chunk/index
9. 标记组件 ChangeVersion
```

创建完成后，外部拿到的只是：

```text
Entity(Id, Version)
```

真正的数据已经进入 Chunk。

---

## 十四、Query 原理

Query 的目标是找到“一批拥有指定组件的数据”。

比如：

```text
Query(Position2D, Velocity2D)
```

它会生成：

```text
QueryMask = Position2D + Velocity2D
```

然后匹配所有 Archetype：

```text
Archetype(Position2D + Velocity2D) -> 匹配
Archetype(Position2D + Velocity2D + SpriteRenderData) -> 匹配
Archetype(Position2D + Health) -> 不匹配
```

匹配结果可以缓存起来。下一帧再执行同样 Query 时，不需要重新扫描所有 Archetype。

这就是 ECS Query 快的关键：

```text
不是每帧检查每个实体
而是先用 Archetype 缩小范围，再连续遍历匹配 Chunk
```

---

## 十五、ForEach / ForEachChunk 原理

### ForEach

`ForEach` 面向易用性。

它把 Chunk 遍历包装起来，让使用者像这样写：

```text
对每个 Position2D + Velocity2D 执行移动逻辑
```

优点是好写、好理解。

缺点是每个实体都会进一次回调或委托包装，极致性能上不如 Chunk 级处理。

### ForEachChunk

`ForEachChunk` 面向底层性能。

它把一整个 Chunk 的数组交给系统：

```text
Position2D span
Velocity2D span
count
```

系统可以自己写紧凑循环：

```text
for i = 0 to count
  position[i] += velocity[i] * deltaTime
```

所以本项目会同时保留两层入口：

- 使用层用 `ForEach` 降低学习成本。
- 性能敏感层用 `ForEachChunk` 减少抽象开销。

---

## 十六、ChangeVersion 原理

`ChangeVersion` 用来记录某种组件在某个 Chunk 上是否变化过。

它解决的问题是：

```text
如果一帧里只有少量 Position2D 变化，某些系统能不能只处理变化过的 Chunk？
```

基本思路：

```text
World 有一个全局版本号
每次写组件时，把对应 Chunk 的组件版本标记为当前版本
Changed Query 只遍历版本号大于上次系统版本的 Chunk
```

注意：ChangeVersion 通常是 Chunk 级，不是单个实体级。

也就是说，只要 Chunk 里某个 `Position2D` 被写过，这个 Chunk 的 `Position2D` 就算变化过。

这样粒度不如逐实体精细，但记录成本更低，更适合高性能 ECS。

---

## 十七、Enableable Component 原理

有时候我们希望实体暂时“禁用某个组件效果”，但又不想真的移除组件。

比如：

```text
敌人暂时停止移动
但不想把 Velocity2D 从实体上 Remove
```

如果真的 Remove 组件，实体会迁移 Archetype，这是结构变更，成本比普通数据修改高。

Enableable Component 的思路是：

```text
组件还在 Chunk 里
但额外用 bit 标记这个组件当前是否启用
```

Query 遍历时会跳过被禁用的实体。

这样适合处理高频开关状态。

但它也不是免费能力：

- 遍历时要检查启用位。
- Enable 位会占额外内存。
- 不是所有组件都应该支持 Enableable。

所以它应该用于“经常开关，但不想频繁迁移 Archetype”的组件。

---

## 十八、结构变更 / 迁移 / swap-remove 原理

结构变更指实体的组件组合发生变化。

比如：

```text
Add<SpriteRenderData>
Remove<Velocity2D>
Destroy(entity)
```

如果添加组件，实体需要从旧 Archetype 迁移到新 Archetype：

```text
旧 Archetype(Position2D + Velocity2D)
新 Archetype(Position2D + Velocity2D + SpriteRenderData)
```

迁移流程通常是：

```text
1. 在新 Archetype 的 Chunk 里分配一个位置
2. 把旧组件数据复制到新位置
3. 写入新增组件数据
4. 从旧 Chunk 删除实体
5. 更新 EntityStore 的 chunk/index/archetype
```

删除旧 Chunk 位置时，为了保持数组紧凑，会用 `swap-remove`。

`swap-remove` 的意思是：

```text
把 Chunk 最后一个实体搬到被删除的位置
Chunk.Count 减 1
更新被搬动实体在 EntityStore 里的 IndexInChunk
```

这样删除是 O(1)，不会让 Chunk 中间留下洞。

代价是实体在 Chunk 内的顺序不稳定。所以 ECS 不应该依赖实体遍历顺序表达业务含义。

---

## 十九、CommandBuffer 原理

为什么 Add/Remove/Destroy 不建议在遍历中立刻执行？

因为遍历时如果立刻移动实体，会改变当前 Chunk 的内容，可能导致：

- 当前循环漏处理实体。
- 当前循环重复处理实体。
- Query 缓存和 Chunk 列表正在被修改。
- 调试难度上升。

`CommandBuffer` 的作用是先记录命令，等安全时机统一回放。

```text
System.Update
  -> CommandBuffer.Add(entity, component)
  -> CommandBuffer.Remove<T>(entity)
  -> CommandBuffer.Destroy(entity)

Frame End
  -> CommandBuffer.Playback(world)
```

回放时还可以做优化：

- 合并同一实体的多条命令。
- 按 Archetype 迁移目标分组。
- 统一更新 QueryCache。
- 避免同一实体重复迁移多次。

所以 CommandBuffer 不只是安全工具，也是结构变更优化工具。

---

## 二十、SystemPipeline 原理

`SystemPipeline` 是系统调度器。

它负责按固定顺序执行系统：

```text
InputSystem
MoveSystem
CollisionSystem
TransformSyncSystem
SpriteRendererSyncSystem
```

第一版采用单线程顺序执行，原因是：

- 更容易调试。
- 更容易保证确定性。
- 更适合先把底层存储和查询打稳。

后续如果要优化，可以在系统声明读写组件后做依赖分析：

```text
MoveSystem 写 Position2D
RenderCollectSystem 读 Position2D
两个系统不能乱序
```

并行不是第一步，第一步是让数据布局和热路径足够干净。

---

## 二十一、Unity Bridge 原理

本项目不让 `UnityEngine.Object` 直接进入 Chunk。

原因是：

- Unity 对象是托管引用，不适合热路径连续内存。
- 很多 Unity API 只能在主线程调用。
- Unity 对象生命周期和 ECS 实体生命周期不完全一致。

所以使用 Bridge：

```text
ECS 组件
  -> 纯数据

Bridge
  -> 保存 Entity 和 Unity 对象的映射
  -> 在合适时机把 ECS 数据同步回 Unity
```

比如 Transform 同步：

```text
Position2D in Chunk
  -> TransformBridge
  -> Unity Transform.position
```

这样 ECS 热路径保持干净，Unity 对象只在边界层接触。

---

## 二十二、Authoring 替代 Baker 的原理

Unity ECS 的 Baker 思路是：

```text
编辑器阶段
  GameObject Authoring
  -> Baker
  -> 烘焙成 Entity 数据
  -> SubScene 序列化
```

本项目的替代方案是：

```text
运行时阶段
  GameObject Authoring
  -> EcsRunner.Convert
  -> World.Create
  -> Entity 进入 Chunk
```

也就是说，我们不在编辑器里做复杂烘焙，而是在运行时把 Authoring 组件转换为 ECS 纯数据。

这样做牺牲了一部分大型场景的离线构建能力，但换来：

- 使用链路更短。
- 不依赖 Baker/SubScene 心智负担。
- 调试更直接。
- 更适合轻量项目和自研 ECS 学习。

性能边界在于：

```text
转换成本应该发生在加载期、生成期、对象池初始化期
不要在每帧热路径里大量 Convert
```

只要转换不放进每帧核心循环，就不会破坏 ECS 的主要性能目标。

---

## 二十三、SpriteRenderer / Transform 如何接入 ECS

以 `SpriteRenderer` 为例，不能简单把它塞进组件：

```text
错误方向：
SpriteRendererData
  -> SpriteRenderer 引用
```

更合理的方式是拆成两层：

```text
ECS 纯数据组件：
SpriteRenderData
  Color
  SortingOrder
  FlipX
  FlipY

Bridge 映射层：
Entity -> SpriteRenderer
```

系统只改 ECS 数据：

```text
SpriteColorSystem
  -> 修改 SpriteRenderData.Color
```

最后由 Bridge 同步回 Unity：

```text
SpriteRendererSyncSystem
  -> 读取 SpriteRenderData
  -> 找到 Entity 对应的 SpriteRenderer
  -> 写入 renderer.color / sortingOrder / flipX
```

这样做的原则是：

```text
能变成纯数据的状态进入 ECS
必须依赖 Unity 对象的操作留在 Bridge
```

Transform 也是一样：

```text
Position2D / Rotation2D / Scale2D
  -> ECS 纯数据

TransformBridge
  -> 同步到 Unity Transform
```

---

## 二十四、Debug / Benchmark 为什么重要

ECS 很容易写出“看起来很底层，但实际不快”的代码。

所以必须有 Debug 和 Benchmark。

Debug 用来看系统是否正确：

```text
当前 Entity 数量
当前 Archetype 数量
当前 Chunk 数量
每个 Archetype 的实体数量
CommandBuffer 是否有未回放命令
```

Benchmark 用来看优化是否真实：

```text
创建实体耗时
查询遍历耗时
Add/Remove 迁移耗时
CommandBuffer Playback 耗时
Bridge 同步耗时
```

没有 Benchmark，就无法判断某个“优化方案”到底是优化，还是只是代码变复杂。

---

## 二十五、一帧完整执行原理

一帧可以理解成下面这条链：

```text
Unity Update
  -> EcsRunner.Tick
  -> SystemPipeline.Update
  -> 每个 System 执行 Query
  -> Query 遍历匹配 Archetype
  -> Archetype 遍历 Chunk
  -> System 修改组件数据
  -> ChangeVersion 更新
  -> CommandBuffer 记录结构变更
  -> Pipeline 末尾 Playback
  -> Bridge 系统同步 Unity 对象
```

简化成一张图：

```text
GameObject/Unity Input
        |
        v
     EcsRunner
        |
        v
  SystemPipeline
        |
        v
      Query
        |
        v
 Archetype -> Chunk -> Component Arrays
        |
        v
  CommandBuffer Playback
        |
        v
 Unity Bridge Sync
```

这条链路里最重要的原则是：

```text
热路径处理纯 ECS 数据
Unity 对象只在边界同步
结构变更集中回放
查询按 Archetype 和 Chunk 批量执行
```

---

## 二十六、常见误区

### 误区 1：ECS 就是把 MonoBehaviour 换成 Entity

不是。

ECS 的核心不是换 API，而是换数据组织方式。

如果组件里仍然塞满对象引用，系统里仍然逐个对象调用复杂方法，就没有真正得到 ECS 的主要收益。

### 误区 2：Entity 应该像 GameObject 一样拥有组件

不是。

Entity 是句柄，组件数据在 Chunk 里，EntityStore 记录实体位置。

### 误区 3：Archetype 越多越好

不是。

Archetype 代表组件组合。组合过多会导致：

- Chunk 更碎。
- Query 匹配列表更多。
- 结构迁移更复杂。

设计组件时要避免因为细碎标签导致 Archetype 爆炸。

### 误区 4：Enableable Component 可以替代所有 Add/Remove

不是。

Enableable 适合高频开关，不适合表达真正的组件组合变化。

如果某个组件长期不存在，还是应该 Remove。

### 误区 5：无 Baker 就一定慢

不是。

关键看转换发生在哪里。

如果运行时 Authoring 转换发生在加载期或生成期，热路径仍然跑 Chunk 数据，它就不会拖慢每帧核心性能。

### 误区 6：Bridge 意味着受制于 GameObject

不完全是。

Bridge 是边界层。底层 ECS 不依赖 GameObject 存储数据，只是在需要显示、物理、动画或 Unity 组件能力时同步到 Unity 对象。

真正需要注意的是：不要把 Bridge 逻辑扩散进核心存储层。

---

## 二十七、学完后应该能回答的问题

读完这份文档后，你应该能回答：

```text
1. Entity 为什么只是 Id + Version？
2. Component 为什么应该是纯数据？
3. System 为什么不应该拥有实体？
4. EntityStore 记录了什么？
5. Archetype 为什么能减少 Query 成本？
6. Chunk 为什么是性能核心？
7. SoA 为什么比对象数组更适合批量处理？
8. ComponentMask 为什么能快速匹配 Query？
9. Add/Remove 为什么会导致实体迁移？
10. swap-remove 为什么会改变实体顺序？
11. CommandBuffer 为什么要延迟回放？
12. ChangeVersion 为什么通常是 Chunk 级？
13. Enableable Component 适合什么场景？
14. Bridge 为什么能隔离 UnityEngine.Object？
15. Authoring 替代 Baker 的性能边界在哪里？
```

如果这些问题能讲清楚，就说明你已经掌握了这个 ECS 的核心原理。后面继续实现代码时，就不是在“背 API”，而是在把这套内存模型一步步落地。
