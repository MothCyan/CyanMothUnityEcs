# ECS 使用链路分层解剖

> 这份文档从“用户怎么用”开始，一层层拆到 ECS 底层怎么执行。它不是路线图，也不是术语表，而是一张纵向调用链地图。

---

## 一、整体分层

从使用者视角看，一次 ECS 操作会穿过这些层：

```text
使用层 User Code
-> 便捷 API 层 Convenience API
-> World API 层
-> 执行层 Execution
-> 查询/命令层 Query / Command
-> 存储层 Archetype / Chunk
-> 内存层 Native Memory
```

每一层的职责：

| 层级 | 你看到的东西 | 它负责什么 |
|---|---|---|
| 使用层 | 组件、系统、Runner、Authoring | 写玩法逻辑 |
| 便捷 API 层 | `CreateMany`、`ForEach`、Prefab | 降低使用成本 |
| World API 层 | `World.Create/Get/Query/Commands` | 统一入口 |
| 执行层 | `SystemPipeline`、`Playback` | 控制执行顺序和安全点 |
| 查询/命令层 | `QueryCache`、`CommandBuffer` | 找数据、延迟结构变更 |
| 存储层 | `Archetype`、`Chunk`、`EntityStore` | 管理数据位置 |
| 内存层 | pointer、offset、layout、allocator | 真正读写内存 |

核心原则：

```text
用户可以从上层舒服地写
但热路径最终必须落到 Chunk 连续内存
```

---

## 二、使用层：开发者实际写什么

开发者通常写四类东西：

```text
Component
System
Runner / Bootstrap
Authoring / Spawn 配置
```

### 1. 写组件

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

这一层只是在定义数据形状。

往下发生什么：

```text
Position 类型
-> TypeRegistry.Register<Position>
-> 分配 TypeIndex
-> 计算 Size / Align
-> 生成 ComponentMask
```

### 2. 写系统

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

这一层表达的是玩法行为：速度影响位置。

往下发生什么：

```text
World.Query<Position, Velocity>
-> QueryCache 找匹配 Archetype
-> ForEachChunk 遍历 Chunk
-> Position* / Velocity* 连续读写
```

### 3. 写 Runner

```csharp
public sealed class GameEcsRunner : MonoBehaviour
{
    private World _world;
    private SystemPipeline _pipeline;

    private void Awake()
    {
        _world = new World();
        _pipeline = new SystemPipeline(_world);
        _pipeline.Add(new MovementSystem());
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

这一层把 Unity 生命周期接到 ECS 生命周期。

往下发生什么：

```text
Unity Update
-> SystemPipeline.Update
-> system.OnUpdate
-> World.Playback
```

### 4. 写 Authoring

```csharp
public sealed class EcsSpawnAuthoring : MonoBehaviour
{
    public int Count = 100;
    public Vector3 InitialVelocity;
    public int Health = 100;
}
```

这一层是编辑器配置，不是运行时 ECS 数据。

往下发生什么：

```text
RuntimeAuthoringCollector
-> SpawnCatalog
-> World.CreateMany
-> Chunk 数据
```

---

## 三、创建实体链路：从 `World.Create` 到 Chunk

用户写：

```csharp
Entity e = world.Create(
    new Position { X = 0, Y = 0, Z = 0 },
    new Velocity { X = 1, Y = 0, Z = 0 });
```

完整下钻：

```text
使用层
World.Create(Position, Velocity)

World API 层
-> 读取 ComponentType<Position>
-> 读取 ComponentType<Velocity>
-> 组合 ComponentMask

存储层
-> ArchetypeStore.GetOrCreate([Position, Velocity])
-> 如果不存在，创建 Archetype
-> 计算 Chunk Layout

内存层
-> Archetype.GetWritableChunk
-> 如果没有可写 Chunk，ChunkAllocator.Allocate
-> 在 Chunk 中分配 slot

Entity 层
-> EntityStore.Create
-> 得到 Entity(Id, Version)

写入数据
-> Entity 数组写入 e
-> Position 数组 slot 写入 Position
-> Velocity 数组 slot 写入 Velocity
-> EntityStore.SetLocation(e, chunk, slot, archetypeId)
```

最终数据长这样：

```text
Archetype [Position, Velocity]
└── Chunk
    ├── Entity[]   : e0 e1 e2 ...
    ├── Position[] : p0 p1 p2 ...
    └── Velocity[] : v0 v1 v2 ...
```

性能关键点：

- 一次进入目标 Archetype。
- 不经过 Empty -> P -> P,V 多次迁移。
- 组件数据连续写入 Chunk。

---

## 四、批量创建链路：从 `CreateMany` 到连续写入

用户写：

```csharp
world.CreateMany<Position, Velocity>(count, (int i, out Position p, out Velocity v) =>
{
    p = new Position { X = i, Y = 0, Z = 0 };
    v = new Velocity { X = 1, Y = 0, Z = 0 };
});
```

完整下钻：

```text
使用层
CreateMany<Position, Velocity>

便捷 API 层
-> 收集初始化回调
-> 一次确定组件组合

World API 层
-> 获取目标 Archetype
-> 预估需要多少 Chunk 空间

存储层
-> 连续获取 writable Chunk
-> 按 Chunk capacity 分批

内存层
-> 连续写 Entity[]
-> 连续写 Position[]
-> 连续写 Velocity[]

Entity 层
-> 批量 EntityStore.SetLocation
```

和循环 `Create` 的区别：

```text
循环 Create:
每个实体都走一次获取 Archetype / 获取 Chunk / 写入

CreateMany:
一次获取 Archetype
按 Chunk 连续写
减少重复检查
```

性能关键点：

- 用批量写替代逐实体路径。
- 减少函数调用和分支。
- 更容易预热和控制 Chunk 分配。

---

## 五、Query 链路：从 `ForEachChunk` 到指针遍历

用户写：

```csharp
World.Query<Position, Velocity>().ForEachChunk((Entity* entities, Position* positions, Velocity* velocities, int count) =>
{
    for (int i = 0; i < count; i++)
    {
        positions[i].X += velocities[i].X * dt;
    }
});
```

完整下钻：

```text
使用层
Query<Position, Velocity>.ForEachChunk

World API 层
-> 构造 IncludeMask = Position | Velocity

查询层
-> QueryCache.GetOrCreate(includeMask)
-> 得到 matchingArchetypeIds

存储层
-> 遍历每个 Archetype
-> 遍历 Archetype.FirstChunk 链表
-> 跳过 Count == 0 的 Chunk

内存层
-> 根据 Offset 找 Position*
-> 根据 Offset 找 Velocity*
-> 把 Entity* / Position* / Velocity* / count 传给用户回调

使用层回调
-> for i in count
-> 连续读写数组
```

它不会做：

```text
不会遍历所有 Entity
不会逐实体 HasComponent
不会跨 SparseSet 查找
不会每帧 new List
```

性能关键点：

- Query 匹配发生在 Archetype 层。
- Chunk 内部是连续扫描。
- 用户热循环很短、很明确。

---

## 六、便利版 Query 链路：从 `ForEach` 到 `ForEachChunk`

用户写：

```csharp
World.Query<Position, Velocity>().ForEach((Entity e, ref Position p, ref Velocity v) =>
{
    p.X += v.X * dt;
});
```

完整下钻：

```text
ForEach
-> 内部调用 ForEachChunk
-> 拿到 Entity* / Position* / Velocity* / count
-> for i in count
-> 转成 ref p[i] / ref v[i]
-> 调用用户 action
```

所以：

```text
ForEach 是易用包装
ForEachChunk 是底层真实入口
```

注意：

- `ForEach` 会多一层 delegate 调用。
- 热点系统应该用 `ForEachChunk`。
- 原型和普通系统可以先用 `ForEach`。

---

## 七、随机访问链路：从 `Get<T>` 到 Chunk slot

用户写：

```csharp
ref Health health = ref world.Get<Health>(entity);
health.Current -= 10;
```

完整下钻：

```text
World.Get<Health>(entity)
-> EntityStore.Validate(entity)
   -> 检查 Id
   -> 检查 Version
   -> 检查 chunk != null
-> chunk = EntityStore.GetChunk(entity)
-> index = EntityStore.GetIndex(entity)
-> archetype = ArchetypeStore[chunk->ArchetypeId]
-> offset = archetype.GetOffset(Health.TypeIndex)
-> ptr = ChunkData.GetComponentPtr<Health>(chunk, offset)
-> return ref ptr[index]
```

适合：

- UI 查询选中单位。
- 单体伤害。
- 目标锁定。
- 少量随机访问。

不适合：

- 每帧对 10 万实体循环调用 `Get<T>`。

批量逻辑应该走 Query。

---

## 八、结构变更链路：从 `Commands.Add` 到 Archetype 迁移

用户写：

```csharp
World.Commands.Add(entity, new Health { Current = 100, Max = 100 });
```

完整下钻：

```text
使用层
Commands.Add(entity, Health)

命令层
-> 写入 CommandBuffer
-> 保存 entity id/version
-> 保存 component typeIndex
-> 保存 payload bytes

执行层
SystemPipeline 在系统后调用 World.Playback

Playback
-> 读取命令
-> Validate Entity
-> sourceChunk = EntityStore.GetChunk(entity)
-> sourceArch = sourceChunk->ArchetypeId
-> targetArch = sourceArch.AddEdge[Health]
   -> 如果 edge 不存在，ArchetypeStore.GetOrCreate(sourceMask | HealthMask)
-> targetChunk = targetArch.GetWritableChunk
-> targetSlot = targetChunk.Count++
-> copy shared components
-> write Health
-> write Entity
-> EntityStore.SetLocation(entity, targetChunk, targetSlot, targetArch.Id)
-> sourceChunk swap-remove
-> 如果有 movedEntity，更新 movedEntity 的 EntityStore location
```

概念上就是：

```text
[Position, Velocity]
-> Add Health
-> [Position, Velocity, Health]
```

性能关键点：

- 结构变更集中在 Playback。
- Query 遍历中不改 Chunk 链表。
- AddEdge 缓存目标 Archetype，避免重复计算组合。

---

## 九、销毁链路：从 `Destroy` 到 Version 失效

用户写：

```csharp
World.Commands.Destroy(entity);
```

完整下钻：

```text
Commands.Destroy
-> 写入 CommandBuffer

World.Playback
-> Validate Entity
-> chunk = EntityStore.GetChunk(entity)
-> index = EntityStore.GetIndex(entity)
-> lastIndex = chunk.Count - 1
-> 如果 index != lastIndex
   -> 把最后一个 Entity 移到 index
   -> 复制每个组件数组的最后一个 slot 到 index
   -> 更新 movedEntity 的 EntityStore location
-> chunk.Count--
-> EntityStore.Release(entity)
   -> chunks[id] = null
   -> indices[id] = 0
   -> archetypeIds[id] = 0
   -> versions[id]++
   -> id 进入 free list
```

结果：

```text
旧 Entity(Id, Version) 失效
未来 Id 可以复用
但 Version 会不同
```

性能关键点：

- 删除是 O(1) swap-remove。
- 不整体搬移 Chunk 数据。
- Version 防止旧句柄误用。

---

## 十、Unity 同步链路：从 ECS 数据到 Transform

用户场景里有：

```text
EcsSpawnAuthoring.LinkTransform = true
```

运行时转换：

```text
RuntimeAuthoringCollector
-> TransformBridge.Register(transform)
-> 得到 proxyId
-> 创建 TransformProxy { Id = proxyId }
```

同步系统：

```csharp
World.Query<Position, TransformProxy>().ForEachChunk((Entity* entities, Position* positions, TransformProxy* proxies, int count) =>
{
    for (int i = 0; i < count; i++)
    {
        Transform t = bridge.Get(proxies[i].Id);
        t.position = new Vector3(positions[i].X, positions[i].Y, positions[i].Z);
    }
});
```

完整下钻：

```text
ECS Position
-> TransformProxy.Id
-> TransformBridge.Get(id)
-> Unity Transform.position
```

性能边界：

- 只有需要表现对象的实体才有 TransformProxy。
- 纯逻辑实体不进入 TransformSyncSystem。
- TransformBridge 是桥接层，不是 ECS 存储层。

---

## 十一、Authoring 链路：从 Inspector 到 ECS Entity

用户在 Inspector 填：

```text
Count = 100
InitialVelocity = (1,0,0)
Health = 100
```

完整下钻：

```text
EcsSpawnAuthoring
-> RuntimeAuthoringCollector.Collect
-> SpawnRequest
-> SpawnCatalog
-> SpawnCatalog.Prewarm(world)
-> SpawnCatalog.Spawn(world)
-> World.CreateMany<Position, Velocity, Health>
-> Chunk data
```

重要边界：

```text
Authoring 是冷路径
SpawnRequest 是纯数据
Chunk 是热路径
```

这意味着：

- 可以在 Authoring 里用 Unity Inspector。
- 不可以每帧读 Authoring。
- 运行时系统只认 ECS Component。

---

## 十二、一帧完整链路

一帧从 Unity 到 ECS 再回 Unity：

```text
Unity Update
-> EcsRunner.Update
-> SystemPipeline.Update
   -> MovementSystem.OnUpdate
      -> Query.ForEachChunk
      -> Position* 连续写
   -> World.Playback
   -> DamageSystem.OnUpdate
      -> Query.ForEach / ForEachChunk
      -> Commands.Destroy / Add
   -> World.Playback
      -> Archetype 迁移或 Destroy
   -> SpawnSystem.OnUpdate
      -> Commands.Create / CreateMany
   -> World.Playback
-> TransformSyncSystem.OnUpdate
   -> Position + TransformProxy
   -> TransformBridge
   -> Unity Transform
-> DebugStats.Flush
```

这一帧里：

```text
Simulation 只处理 ECS 数据
结构变更只在 Playback
Unity 对象只在 Bridge 系统接触
Authoring 不参与每帧
```

---

## 十三、三条最重要的链路

### 数据生成链路

```text
Authoring / Code
-> SpawnCatalog / CreateMany
-> Archetype
-> Chunk
-> EntityStore location
```

### 数据处理链路

```text
System
-> QueryCache
-> Archetype list
-> Chunk list
-> Component pointers
-> for loop
```

### 结构变化链路

```text
CommandBuffer
-> Playback
-> ArchetypeGraph
-> Move entity
-> swap-remove
-> EntityStore update
```

只要这三条链路清晰，整个 ECS 就不会乱。

---

## 十四、从上到下看一次 `MovementSystem`

用户看到：

```csharp
positions[i].X += velocities[i].X * dt;
```

底层实际发生：

```text
MovementSystem
-> cached Query<Position, Velocity>
-> QueryCache matching archetypes
-> Archetype [Position, Velocity]
-> Chunk 0
-> PositionOffset
-> VelocityOffset
-> Position* positions
-> Velocity* velocities
-> CPU 连续读取 Velocity
-> CPU 连续写入 Position
```

这就是 ECS 性能来自哪里：

```text
不是 Entity 神奇
不是 System 神奇
而是数据按组件连续放
系统按 Chunk 连续扫
```

---

## 十五、哪些 API 是上层糖，哪些是底层核心

| API | 类型 | 底层落点 |
|---|---|---|
| `ForEach` | 上层糖 | `ForEachChunk` |
| `CreateMany` | 便捷高性能 API | Chunk 连续写 |
| `ArchetypePrefab` | 模板糖 | 已知 Archetype + 默认数据 memcpy |
| `World.Get<T>` | 随机访问 API | EntityStore -> Chunk slot |
| `World.Commands.Add` | 结构变更入口 | CommandBuffer -> Playback -> Migration |
| `TransformProxy` | Unity 桥接 | Bridge 数组查找 |
| `Authoring` | 编辑器输入 | Collector -> SpawnCatalog |

判断标准：

```text
如果一个 API 最终落不到 Chunk 连续读写
就不能放到热路径
```

---

## 十六、学习顺序建议

读这套系统时，建议按这个顺序理解：

```text
1. Component 是什么
2. Entity 为什么只是句柄
3. Archetype 为什么按组件组合分组
4. Chunk 为什么连续存数据
5. Query 为什么先匹配 Archetype
6. ForEachChunk 为什么快
7. CommandBuffer 为什么延迟结构变更
8. EntityStore 为什么要记录 chunk/index/version
9. Unity Bridge 为什么不能把 Transform 放进 Component
10. Authoring 为什么只能是冷路径输入
```

理解完这些，再去看实现代码会轻松很多。
