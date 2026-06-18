# CyanMothUnityEcs 脚本与 API 说明

> 这份文档专门解释项目里每个 ECS 脚本、类、字段、属性和 API 的职责。它不是设计文档，而是代码阅读手册。后续每新增一批脚本，都应该同步更新这里。

---

## 一、程序集文件

### `Assets/Scripts/ECS/CyanMothUnityEcs.asmdef`

主 ECS 程序集定义。

#### 字段说明

| 字段 | 功能 |
|---|---|
| `name` | 程序集名称，当前为 `CyanMothUnityEcs`。 |
| `rootNamespace` | 默认命名空间，当前为 `CyanMothUnityEcs`。 |
| `references` | 当前没有依赖其他自定义程序集。 |
| `allowUnsafeCode` | 开启 unsafe 支持。后续 Chunk、指针、native memory 会依赖它。 |
| `autoReferenced` | 允许 Unity 默认程序集引用这个 ECS 程序集。 |
| `noEngineReferences` | 当前为 `false`，表示允许引用 UnityEngine。核心层暂时不依赖 UnityEngine，但后续 Unity Bridge 会用到。 |

---

### `Assets/Scripts/ECS/Tests/CyanMothUnityEcs.Tests.asmdef`

ECS EditMode 测试程序集定义。

#### 字段说明

| 字段 | 功能 |
|---|---|
| `name` | 测试程序集名称，当前为 `CyanMothUnityEcs.Tests`。 |
| `references` | 引用主程序集 `CyanMothUnityEcs`。 |
| `includePlatforms` | 只在 Editor 下编译测试。 |
| `allowUnsafeCode` | 允许测试后续 unsafe 存储层。 |
| `precompiledReferences` | 引用 `nunit.framework.dll`。 |
| `optionalUnityReferences` | 使用 `TestAssemblies`，让 Unity Test Framework 识别该程序集为测试程序集。 |

---

## 二、Core 核心脚本

## `Assets/Scripts/ECS/Core/IComponentData.cs`

### `IComponentData`

ECS 组件标记接口。

```csharp
public interface IComponentData
{
}
```

#### 功能

标记某个 `struct` 可以作为 ECS 组件注册、进入 Archetype，并最终存进 Chunk。

#### 为什么需要它

它让 API 可以写成：

```csharp
where T : unmanaged, IComponentData
```

这样可以同时约束：

- `T` 必须是非托管结构，适合进入 Chunk。
- `T` 必须明确声明自己是 ECS 组件。

#### 当前限制

接口本身没有方法。它只负责表达类型身份。

---

## `Assets/Scripts/ECS/Core/ComponentMask.cs`

### `ComponentMask`

128 位组件集合，用来描述“一个实体/Archetype/Query 拥有哪些组件”。

```csharp
public readonly struct ComponentMask : IEquatable<ComponentMask>
```

### 字段

#### `public static readonly ComponentMask Empty`

空组件集合。

用途：

```text
表示没有任何组件
作为构造 mask 的起点
```

#### `public readonly ulong Lo`

低 64 位组件掩码。

表示 TypeIndex `0..63` 的组件是否存在。

#### `public readonly ulong Hi`

高 64 位组件掩码。

表示 TypeIndex `64..127` 的组件是否存在。

### 属性

#### `public bool IsEmpty`

判断当前 mask 是否没有任何组件。

实现逻辑：

```csharp
(Lo | Hi) == 0UL
```

### 构造函数

#### `ComponentMask(ulong lo, ulong hi)`

直接指定低 64 位和高 64 位。

主要给内部方法使用。

### API

#### `static ComponentMask FromIndex(int index)`

根据组件 TypeIndex 生成只包含一个组件的 mask。

示例：

```csharp
ComponentMask positionMask = ComponentMask.FromIndex(0);
ComponentMask highMask = ComponentMask.FromIndex(70);
```

规则：

- `0..63` 写入 `Lo`。
- `64..127` 写入 `Hi`。
- 超过范围抛异常。

#### `ComponentMask Add(int index)`

返回一个添加指定 TypeIndex 后的新 mask。

注意：

`ComponentMask` 是只读结构体，这个方法不会修改原对象，而是返回新对象。

#### `ComponentMask Add(ComponentMask other)`

返回当前 mask 与另一个 mask 的并集。

用途：

```text
构造 Query IncludeMask
构造 Archetype 组件组合
```

#### `ComponentMask Remove(int index)`

返回移除指定组件后的新 mask。

用途：

```text
RemoveComponent 时计算目标 Archetype mask
```

#### `bool Contains(int index)`

判断当前 mask 是否包含某个 TypeIndex。

用途：

```text
World.Has<T>
Archetype.Has<T>
```

#### `bool ContainsAll(ComponentMask other)`

判断当前 mask 是否完整包含另一个 mask。

用途：

```text
Query 匹配 Archetype
Archetype.Mask.ContainsAll(Query.IncludeMask)
```

#### `bool Intersects(ComponentMask other)`

判断当前 mask 和另一个 mask 是否有交集。

用途：

```text
Query ExcludeMask
判断是否包含任何需要排除的组件
```

#### `bool Equals(ComponentMask other)`

比较两个 mask 是否完全相同。

#### `override bool Equals(object obj)`

支持 object 形式的相等比较。

#### `override int GetHashCode()`

生成哈希值。

用途：

```text
后续可作为 Dictionary key
例如 mask -> ArchetypeId
```

#### `override string ToString()`

把 mask 打印成十六进制字符串。

示例：

```text
0x0000000000000000_0000000000000003
```

调试 Archetype 组件组合时很有用。

#### `operator ==`

比较两个 `ComponentMask` 是否相等。

#### `operator !=`

比较两个 `ComponentMask` 是否不相等。

#### `private static void ValidateIndex(int index)`

检查 TypeIndex 是否在 `0..127` 范围内。

这是内部保护方法，避免位移越界。

---

## `Assets/Scripts/ECS/Core/ComponentType.cs`

### `ComponentType`

单个组件类型的运行时元数据。

```csharp
public readonly struct ComponentType : IEquatable<ComponentType>
```

### 字段

#### `public readonly int Index`

组件类型编号。

用途：

```text
生成 ComponentMask
查找组件 Offset
作为 AddEdge/RemoveEdge 索引
```

#### `public readonly int Size`

组件结构体大小，单位是字节。

用途：

```text
Chunk Layout 计算
组件数组占用空间计算
memcpy 大小
```

#### `public readonly int Align`

组件建议对齐值。

用途：

```text
Chunk 内组件数组起始地址对齐
减少未对齐访问
```

#### `public readonly bool IsTag`

表示该组件是否是 Tag 组件。

Tag 组件通常没有有效数据，只表达状态或分类。

#### `public readonly Type ManagedType`

原始 C# 类型。

用途：

```text
调试显示
错误信息
TypeRegistry 查表
```

热路径不应该依赖它做反射。

#### `public readonly ComponentMask Mask`

只包含当前组件这一位的 mask。

用途：

```text
构造 Archetype mask
构造 Query mask
```

### 构造函数

#### `ComponentType(int index, int size, int align, bool isTag, Type managedType)`

创建组件元数据。

一般只由 `TypeRegistry.Register<T>()` 调用。

### API

#### `bool Equals(ComponentType other)`

当前实现只比较 `Index`。

原因：

`Index` 是组件类型在 ECS 运行时的唯一身份。

#### `override bool Equals(object obj)`

支持 object 比较。

#### `override int GetHashCode()`

返回 `Index`。

#### `override string ToString()`

返回用于调试的组件类型描述。

示例：

```text
Position[0] Size=12 Align=8
```

---

## `Assets/Scripts/ECS/Core/ComponentTypeCache.cs`

### `ComponentTypeCache<T>`

泛型组件类型缓存。

```csharp
public static class ComponentTypeCache<T> where T : unmanaged, IComponentData
```

### 字段

#### `public static readonly ComponentType Type`

当前组件类型 `T` 对应的 `ComponentType`。

第一次访问时会触发：

```text
TypeRegistry.Register<T>()
```

后续访问直接使用静态缓存。

### 为什么需要它

避免热路径反复通过 `typeof(T)` 查字典。

例如：

```csharp
ComponentType type = ComponentTypeCache<Position>.Type;
```

后续 Query、Get、Set 都可以直接拿到组件元数据。

---

## `Assets/Scripts/ECS/Core/TypeRegistry.cs`

### `TypeRegistry`

组件类型注册表。

```csharp
public static class TypeRegistry
```

它负责给每种组件分配稳定的 TypeIndex。

### 常量

#### `public const int MaxComponentTypes = 128`

第一版最多支持 128 种组件类型。

这个限制和 `ComponentMask` 的 128 位设计对应。

### 字段

#### `private static readonly object SyncRoot`

注册表锁。

用途：

```text
保证注册组件时不会出现并发写入问题
```

第一版大概率单线程注册，但这里提前保护，成本只在注册冷路径。

#### `private static readonly Dictionary<Type, ComponentType> TypesByManagedType`

从 C# 类型到 `ComponentType` 的映射。

用途：

```text
防止重复注册
通过 typeof(T) 找已有 ComponentType
```

#### `private static readonly ComponentType[] TypesByIndex`

从 TypeIndex 到 `ComponentType` 的数组。

用途：

```text
通过 index 快速取回组件元数据
```

#### `private static int _count`

当前已经注册的组件类型数量。

下一个新组件类型会使用 `_count` 作为 Index。

### 属性

#### `public static int Count`

返回当前已注册组件数量。

使用 `lock` 读取，避免和注册过程冲突。

### API

#### `public static ComponentType Register<T>() where T : unmanaged, IComponentData`

注册组件类型。

执行流程：

```text
typeof(T)
-> 如果已经注册，返回已有 ComponentType
-> 检查是否超过 128 上限
-> 计算 Size
-> 估算 Align
-> 判断是否 Tag
-> 分配 Index
-> 写入字典和数组
```

这是组件类型进入 ECS 的入口。

#### `public static ComponentType Get<T>() where T : unmanaged, IComponentData`

获取组件类型元数据。

内部走：

```csharp
ComponentTypeCache<T>.Type
```

这样热路径可以利用泛型静态缓存。

#### `public static ComponentType GetByIndex(int index)`

通过 TypeIndex 获取组件类型。

用途：

```text
Archetype Layout
调试窗口
CommandBuffer 根据 typeIndex 找组件元数据
```

#### `internal static void ClearForTests()`

清空注册表，只给测试使用。

注意：

运行时业务代码不要调用它。组件 TypeIndex 一旦在正常流程中分配，就不应该重排。

#### `private static int SizeOf<T>() where T : unmanaged`

计算组件大小。

当前使用：

```csharp
Marshal.SizeOf<T>()
```

后续进入 unsafe 层后，可以考虑替换为更贴近底层的 size 计算方式。

#### `private static int EstimateAlignment(int size)`

根据 size 估算对齐。

当前规则：

```text
size >= 8 -> align 8
size >= 4 -> align 4
size >= 2 -> align 2
否则 align 1
```

这是第一版保守估算，后续 Chunk Layout 阶段可以继续优化。

---

## `Assets/Scripts/ECS/Core/Entity.cs`

### `Entity`

对外实体句柄。

```csharp
public readonly struct Entity : IEquatable<Entity>
```

### 字段

#### `public static readonly Entity Null`

空实体。

约定：

```text
Id = 0
Version = 0
```

#### `public readonly int Id`

实体槽位编号。

用途：

```text
索引 EntityStore 内部数组
```

#### `public readonly int Version`

实体版本号。

用途：

```text
防止旧句柄访问已经被复用的实体槽位
```

### 属性

#### `public bool IsNull`

判断是否是 `Entity.Null`。

### 构造函数

#### `Entity(int id, int version)`

创建实体句柄。

通常由 `EntityStore.Create()` 创建，业务层不应该随便手动构造。

### API

#### `bool Equals(Entity other)`

比较两个实体是否完全相同。

必须同时满足：

```text
Id 相同
Version 相同
```

#### `override bool Equals(object obj)`

支持 object 形式比较。

#### `override int GetHashCode()`

根据 Id 和 Version 生成哈希值。

#### `override string ToString()`

输出调试字符串。

示例：

```text
Entity.Null
Entity(12:3)
```

#### `operator ==`

判断两个实体是否相等。

#### `operator !=`

判断两个实体是否不相等。

---

## `Assets/Scripts/ECS/Core/EntityStore.cs`

### `EntityStore`

实体位置表。

```csharp
public sealed class EntityStore
```

它负责把 `Entity` 映射到当前 Chunk 位置。

### 常量

#### `private const int NullEntityId = 0`

保留 Id 0 给 `Entity.Null`。

#### `private const int DefaultCapacity = 1024`

默认实体数组容量。

### 字段

#### `private int[] _versions`

每个 Entity Id 当前对应的 Version。

用途：

```text
判断 Entity 是否过期
```

#### `private IntPtr[] _chunks`

每个 Entity 当前所在 Chunk 的地址。

当前用 `IntPtr`，因为 Chunk 类型会在后续阶段实现。

#### `private int[] _indices`

每个 Entity 在 Chunk 内的 slot index。

用途：

```text
定位组件数组中的第几个元素
```

#### `private int[] _archetypeIds`

每个 Entity 当前所属 Archetype Id。

用途：

```text
快速知道实体属于哪个组件组合
```

#### `private int[] _freeIds`

已释放 Entity Id 的回收栈。

#### `private int _freeCount`

当前可复用 Id 数量。

#### `private int _nextId`

下一个从未使用过的 Entity Id。

初始为 1，因为 0 保留给 `Entity.Null`。

### 属性

#### `public int Capacity`

当前内部数组容量。

#### `public int CreatedCapacity`

当前 `_nextId`。

它表示已经分配过的 Id 上界，不等于存活实体数量。

### 构造函数

#### `EntityStore(int initialCapacity = DefaultCapacity)`

创建实体位置表。

会初始化：

```text
versions
chunks
indices
archetypeIds
freeIds
```

### API

#### `Entity Create()`

创建一个实体句柄。

流程：

```text
如果 freeIds 里有可复用 Id
-> 取回旧 Id
否则
-> 使用 nextId
-> 必要时扩容数组
返回 Entity(id, versions[id])
```

注意：

新建出来的 Entity 还没有 Chunk 位置，因此此时 `IsAlive` 仍然可能是 false。后续 `World.Create` 会在写入 Chunk 后调用 `SetLocation`。

#### `bool IsAlive(Entity entity)`

判断实体是否当前存活。

条件：

```text
Id 有效
Version 匹配
Chunk 地址非零
```

#### `void Validate(Entity entity)`

校验实体是否是可访问的存活实体。

失败时抛出明确异常。

用途：

```text
Get<T>
Set<T>
Destroy
结构迁移
```

#### `void Release(Entity entity)`

释放实体。

流程：

```text
Validate
清空 chunk/index/archetype
version++
id 放回 freeIds
```

释放后，旧 Entity 句柄会失效。

#### `void SetLocation(Entity entity, IntPtr chunk, int index, int archetypeId)`

设置实体当前位置。

调用时机：

```text
实体首次写入 Chunk
结构迁移到新 Chunk
swap-remove 后 moved entity 更新位置
```

#### `IntPtr GetChunk(Entity entity)`

获取实体所在 Chunk 地址。

会先调用 `Validate`。

#### `int GetIndex(Entity entity)`

获取实体在 Chunk 中的 slot index。

会先调用 `Validate`。

#### `int GetArchetypeId(Entity entity)`

获取实体所属 Archetype Id。

会先调用 `Validate`。

### 私有方法

#### `bool IsValidId(int id)`

判断 Id 是否在当前 EntityStore 的有效范围内。

#### `void EnsureEntityCapacity(int id)`

确保内部数组能容纳指定 Id。

容量不足时按 2 倍扩容。

#### `void PushFreeId(int id)`

把已释放 Id 放回 free list。

如果 `_freeIds` 容量不足，会扩容。

---

## `Assets/Scripts/ECS/Core/World.cs`

### `World`

ECS 世界的统一入口。

```csharp
public unsafe sealed partial class World : IDisposable
```

它负责把上层 API 转成底层模块调用。

当前阶段已经接入：

```text
EntityStore
ArchetypeStore
ChunkAllocator
```

还没有接入：

```text
CommandBuffer
QueryCache
SystemPipeline
Unity Bridge
```

### 字段

#### `private readonly EntityStore _entities`

实体位置表。

用途：

```text
创建 Entity
判断 Entity 是否存活
记录 Entity 当前所在 Chunk / slot / Archetype
```

#### `private readonly ArchetypeStore _archetypes`

Archetype 管理器。

用途：

```text
根据组件组合获取 Archetype
通过 ArchetypeId 找回 Archetype
```

#### `private readonly ChunkAllocator _chunks`

Chunk native memory 分配器。

用途：

```text
当 Archetype 没有可写 Chunk 时分配新 Chunk
World.Dispose 时释放 native memory
```

#### `private readonly CommandBuffer _commands`

延迟结构变更命令缓冲。

用途：

```text
记录 Add / Remove / Destroy
在 World.Playback 安全点统一回放
```

#### `private readonly QueryCache _queryCache`

Query 到 Archetype 的匹配缓存。

用途：

```text
World.Query<T>
-> QueryCache.GetOrCreate
-> ForEachChunk 时拿匹配 Archetype 列表
```

#### `private bool _disposed`

标记 World 是否已经释放。

### 属性

#### `public int CreatedEntityCapacity`

返回 `EntityStore.CreatedCapacity`。

当前用于测试和调试。

#### `public int ArchetypeCount`

返回当前已经创建的 Archetype 数量。

#### `public CommandBuffer Commands`

返回 World 持有的命令缓冲。

用户可以写：

```csharp
world.Commands.Add(entity, component);
world.Commands.Destroy(entity);
```

### API

#### `World()`

创建 World，并初始化：

```text
EntityStore
ArchetypeStore
ChunkAllocator
```

#### `bool IsAlive(Entity entity)`

判断实体是否当前存活。

#### `void Dispose()`

释放 World 持有的 native memory。

当前主要调用：

```text
CommandBuffer.Clear
ChunkAllocator.Dispose()
```

#### `void Playback()`

回放 `Commands` 中记录的所有命令。

后续 `SystemPipeline` 会在每个系统后自动调用它。

#### `private void ThrowIfDisposed()`

防止释放后的 World 继续被使用。

---

## `Assets/Scripts/ECS/Commands/CommandKind.cs`

### `CommandKind`

CommandBuffer 中记录的命令类型。

当前支持：

```text
Add
Remove
Destroy
```

第一版没有实现 `Create` 和 `Set` 命令。

---

## `Assets/Scripts/ECS/Commands/CommandBuffer.cs`

### `CommandBuffer`

延迟结构变更命令缓冲。

```csharp
public sealed class CommandBuffer
```

### 当前实现说明

第一版使用 typed delegate 保存命令：

```text
Command
-> CommandKind
-> Action<World>
```

这比最终的原始字节命令缓冲更简单，适合先验证：

```text
命令记录
Playback 顺序
World 接入
结构变更安全点
```

后续性能阶段会替换成更接近设计文档的二进制命令格式。

### 字段

#### `private const int DefaultCapacity`

默认命令容量，当前为 64。

#### `private Command[] _commands`

命令数组。

#### `private int _count`

当前已经记录的命令数量。

### 属性

#### `public int Count`

当前命令数量。

### API

#### `CommandBuffer(int initialCapacity = DefaultCapacity)`

创建命令缓冲。

#### `void Add<T>(Entity entity, T component)`

记录添加组件命令。

实际结构变更不会立即发生，要等 `Playback`。

#### `void Remove<T>(Entity entity)`

记录移除组件命令。

#### `void Destroy(Entity entity)`

记录销毁实体命令。

#### `void Playback(World world)`

按记录顺序回放所有命令。

回放完成后自动清空命令。

#### `void Clear()`

清空命令，不执行。

#### `private void Append(Command command)`

把命令追加到数组。

容量不够时扩容。

---

## `Assets/Scripts/ECS/Core/World.Create.cs`

### 创建相关 API

这一份 partial class 负责实体创建和 Chunk 写入。

### API

#### `Entity Create<T1>(T1 c1)`

创建一个拥有 1 个组件的实体。

流程：

```text
TypeRegistry.Get<T1>
-> ArchetypeStore.GetOrCreate
-> AllocateEntity
-> WriteComponent
```

#### `Entity Create<T1, T2>(T1 c1, T2 c2)`

创建一个拥有 2 个组件的实体。

重点：

```text
直接进入最终 Archetype
不会先创建空实体再 AddComponent
```

#### `Entity Create<T1, T2, T3>(T1 c1, T2 c2, T3 c3)`

创建一个拥有 3 个组件的实体。

后续可以继续扩展到固定上限，例如 4 个组件。

#### `private Entity AllocateEntity(Archetype archetype, out Chunk* chunk, out int slot)`

为某个 Archetype 分配一个实体 slot。

流程：

```text
GetWritableChunk
-> chunk.Count++
-> EntityStore.Create
-> 写 Entity[]
-> EntityStore.SetLocation
-> 如果 Chunk 满了，从 free list 移除
```

#### `private Chunk* GetWritableChunk(Archetype archetype)`

获取一个还有空位的 Chunk。

如果没有可写 Chunk：

```text
ChunkAllocator.Allocate
-> LinkChunk
-> AddToFreeList
```

#### `private static void LinkChunk(Archetype archetype, Chunk* chunk)`

把新 Chunk 挂到 Archetype 的 Chunk 链表上。

#### `private static void AddToFreeList(Archetype archetype, Chunk* chunk)`

把有空位的 Chunk 放进 Archetype 的可写 Chunk 链表。

#### `private static void RemoveFromFreeList(Archetype archetype, Chunk* chunk)`

当 Chunk 写满时，把它从可写 Chunk 链表移除。

#### `private static void WriteEntity(Chunk* chunk, Archetype archetype, int slot, Entity entity)`

把 Entity 句柄写入 Chunk 内的 `Entity[]`。

#### `private static void WriteComponent<T>(...)`

把组件数据写入 Chunk 内对应组件数组的 slot。

Tag 组件没有数据区，因此会直接返回。

---

## `Assets/Scripts/ECS/Core/World.Access.cs`

### 随机访问相关 API

这一份 partial class 负责：

```text
Has<T>
Get<T>
Set<T>
```

它适合少量随机访问，不适合每帧大量实体循环。

批量逻辑后续应走 Query。

### API

#### `bool Has<T>(Entity entity)`

判断实体当前 Archetype 是否包含组件 `T`。

流程：

```text
EntityStore.Validate
-> TypeRegistry.Get<T>
-> EntityStore.GetArchetypeId
-> ArchetypeStore.GetById
-> Archetype.Has
```

#### `ref T Get<T>(Entity entity)`

返回组件在 Chunk 内的真实引用。

流程：

```text
TypeRegistry.Get<T>
-> GetEntityChunk
-> Archetype.GetComponentOffset
-> 根据 offset + stride * slot 定位组件
-> return ref
```

注意：

```text
Tag 组件没有数据区，不能 Get ref
```

#### `void Set<T>(Entity entity, T component)`

覆盖实体当前组件数据。

Tag 组件没有数据区，因此 Set Tag 会直接返回。

#### `private Chunk* GetEntityChunk(Entity entity, out int slot, out Archetype archetype)`

通过 EntityStore 定位实体当前所在 Chunk、slot 和 Archetype。

这个方法是 `Get<T>` 和 `Set<T>` 的共同底层入口。

---

## `Assets/Scripts/ECS/Core/World.StructuralChanges.cs`

### 结构变更相关 API

这一份 partial class 当前实现立即结构变更：

```text
Add<T>
Remove<T>
Destroy
```

CommandBuffer 会在后续阶段接入。

### API

#### `void Add<T>(Entity entity, T component)`

给实体添加组件。

如果实体已经拥有该组件，则退化为：

```text
Set<T>(entity, component)
```

如果实体没有该组件，则执行跨 Archetype 迁移：

```text
source Archetype
-> targetMask = source.Mask + T
-> 通过 AddEdges 或 ArchetypeStore 找目标 Archetype
-> 移动实体到目标 Chunk
-> 拷贝共有组件
-> 写入新增组件
-> 从源 Chunk swap-remove
```

#### `void Remove<T>(Entity entity)`

从实体移除组件。

如果实体没有该组件，直接返回。

如果实体拥有该组件，则执行跨 Archetype 迁移：

```text
source Archetype
-> targetMask = source.Mask - T
-> 通过 RemoveEdges 或 ArchetypeStore 找目标 Archetype
-> 移动实体到目标 Chunk
-> 拷贝剩余共有组件
-> 从源 Chunk swap-remove
```

#### `void Destroy(Entity entity)`

销毁一个实体。

流程：

```text
GetEntityChunk
-> 记录 Chunk 是否原本已满
-> RemoveEntityAt
-> EntityStore.Release
-> 如果 Chunk 变空：从链表移除并回收到 ChunkAllocator
-> 如果 Chunk 原本已满且现在有空位：重新放回可写 Chunk 链表
```

#### `private void MoveEntityToArchetype(...)`

跨 Archetype 迁移实体的核心函数。

职责：

```text
在目标 Archetype 分配 slot
写 Entity[]
拷贝共有组件
写新增组件
更新 EntityStore
从源 Chunk 删除旧 slot
回收或更新源 Chunk 的 free 状态
```

#### `private static void CopySharedComponents(...)`

把源 Archetype 和目标 Archetype 共有的组件数据从旧 Chunk 拷贝到新 Chunk。

Tag 组件会跳过，因为它没有数据区。

#### `private static void WriteRawComponent(...)`

写入新增组件的原始字节。

当前由 `Add<T>` 调用。

#### `private void RecycleSourceChunkAfterMigration(...)`

迁移后处理源 Chunk：

```text
如果源 Chunk 为空 -> 从 Archetype 链表移除并回收到 ChunkAllocator
如果源 Chunk 原本已满 -> 现在有空位，重新放回 free list
```

#### `private void RemoveEntityAt(Archetype archetype, Chunk* chunk, int slot)`

从 Chunk 中删除指定 slot。

如果删除的不是最后一个 slot，会执行 swap-remove：

```text
最后一个 Entity 复制到被删除 slot
最后一行组件数据复制到被删除 slot
更新被移动 Entity 的 EntityStore location
Chunk.Count--
```

#### `private static Entity* GetEntityArray(Chunk* chunk, Archetype archetype)`

根据 `ArchetypeLayout.EntityOffset` 找到 Chunk 内的 `Entity[]` 起始地址。

#### `private static void MoveComponentRows(...)`

把某一行组件数据从 `sourceSlot` 移动到 `targetSlot`。

它会遍历 Archetype 的所有组件：

```text
Tag 组件跳过
普通组件按 offset + stride * slot 定位
UnsafeUtil.Copy 拷贝一行组件数据
```

#### `private static void UnlinkChunk(Archetype archetype, Chunk* chunk)`

把一个 Chunk 从 Archetype 的 Chunk 双向链表中移除。

当 Chunk 删除实体后变空时调用。

---

## `Assets/Scripts/ECS/Core/World.Query.cs`

### Query 入口和 Chunk 遍历执行

这一份 partial class 负责：

```text
World.Query<T1>
World.Query<T1,T2>
World.Query<T1,T2,T3>
ForEachChunk 执行
```

### API

#### `Query<T1> Query<T1>()`

创建单组件 Query。

#### `Query<T1, T2> Query<T1, T2>()`

创建双组件 Query。

#### `Query<T1, T2, T3> Query<T1, T2, T3>()`

创建三组件 Query。

### 内部执行方法

#### `internal void ForEachChunk<T1>(int queryId, ChunkAction<T1> action)`

执行单组件 Chunk 遍历。

流程：

```text
QueryCache.GetMatchingArchetypes
-> 遍历匹配 Archetype
-> 遍历 Archetype 下的 Chunk 链表
-> 根据 Layout offset 找 T1*
-> 调用 ChunkAction
```

#### `internal void ForEachChunk<T1, T2>(...)`

执行双组件 Chunk 遍历。

#### `internal void ForEachChunk<T1, T2, T3>(...)`

执行三组件 Chunk 遍历。

#### `private static Entity* GetEntityArray(...)`

根据 Chunk 和 ArchetypeLayout 找到 `Entity[]` 起始地址。

---

## `Assets/Scripts/ECS/Query/QueryDelegates.cs`

### Query 回调类型

定义 Query 遍历时用户传入的回调。

### `QueryAction`

面向易用的逐实体回调：

```text
QueryAction<T1>
QueryAction<T1,T2>
QueryAction<T1,T2,T3>
```

形态：

```csharp
(Entity entity, ref Position position, ref Velocity velocity) => {}
```

### `ChunkAction`

面向性能的逐 Chunk 回调：

```text
ChunkAction<T1>
ChunkAction<T1,T2>
ChunkAction<T1,T2,T3>
```

形态：

```csharp
(Entity* entities, Position* positions, Velocity* velocities, int count) => {}
```

---

## `Assets/Scripts/ECS/Query/QueryCache.cs`

### `QueryCache`

缓存 Query 命中的 Archetype 列表。

它避免每次执行 Query 都重新扫描所有 Archetype。

### 字段

#### `private readonly ArchetypeStore _archetypes`

QueryCache 依赖的 ArchetypeStore。

#### `private readonly List<QueryRecord> _records`

所有 Query 记录。

### API

#### `int GetOrCreate(ComponentMask include, ComponentMask exclude)`

根据 include/exclude mask 获取 QueryId。

如果不存在则创建新 QueryRecord 并立刻刷新。

#### `int[] GetMatchingArchetypes(int queryId)`

获取该 Query 命中的 ArchetypeId 列表。

如果 ArchetypeStore.Version 变化，会自动刷新缓存。

#### `private void Refresh(int queryId)`

重新扫描所有 Archetype：

```text
archetype.Mask.ContainsAll(include)
并且不与 exclude 相交
```

当前公开 API 还没有 `Without<T>`，所以 exclude 暂时只使用空 mask。

---

## `Assets/Scripts/ECS/Query/Query.cs`

### `Query<T1>` / `Query<T1,T2>` / `Query<T1,T2,T3>`

Query 是用户持有的查询句柄。

它内部只保存：

```text
World
QueryId
```

### API

#### `ForEach(...)`

易用版逐实体遍历。

内部基于 `ForEachChunk` 包装：

```text
ForEach
-> ForEachChunk
-> for i in count
-> 调用用户 QueryAction
```

#### `ForEachChunk(...)`

高性能逐 Chunk 遍历。

它会把连续组件数组指针交给用户。

---

## `Assets/Scripts/ECS/Systems/EcsSystem.cs`

### `EcsSystem`

系统基类。

```csharp
public abstract class EcsSystem
```

它负责把一段业务逻辑包装成可以被 `SystemPipeline` 调度的对象。

系统本身不保存组件数组，也不拥有实体数据。它只通过 `World` 去访问：

```text
Entity
Component
Query
CommandBuffer
```

### 属性

#### `public World World`

当前系统所属的 World。

用途：

```text
创建实体
读写组件
创建 Query
记录 CommandBuffer 命令
```

这个属性在系统加入 `SystemPipeline` 后才有值。

#### `public bool IsAttached`

表示系统是否已经加入某个 `SystemPipeline`。

第一版不允许同一个系统实例挂到多个管线里，因为那样会导致：

```text
World 指向不明确
OnCreate 调用次数不明确
OnDestroy 释放顺序不明确
```

### 内部 API

#### `internal void Attach(World world)`

把系统绑定到 World。

执行流程：

```text
检查 world 不为空
检查系统没有重复加入
记录 World
调用 OnCreate
```

只有 `SystemPipeline.Add` 会调用它。

#### `internal void Update(float deltaTime)`

执行一次系统更新。

执行流程：

```text
检查系统已经绑定 World
调用 OnUpdate(deltaTime)
```

只有 `SystemPipeline.Update` 会调用它。

#### `internal void Detach()`

从管线释放系统。

执行流程：

```text
如果没有绑定，直接返回
调用 OnDestroy
清空 World
```

这里用 `finally` 清空 World，保证即使 `OnDestroy` 里抛异常，系统也不会继续持有旧 World。

### 可重写 API

#### `protected virtual void OnCreate()`

系统加入管线时调用一次。

适合做：

```text
缓存 Query
初始化系统内部状态
准备 Unity Bridge 引用
```

#### `protected abstract void OnUpdate(float deltaTime)`

每帧更新入口。

所有系统都必须实现它。

适合做：

```text
执行 Query
修改组件数据
向 CommandBuffer 写结构变更命令
```

#### `protected virtual void OnDestroy()`

管线释放系统时调用一次。

适合释放系统自己持有的资源。

注意：

普通组件数据不需要在这里释放，因为组件数据由 `World` 和 `ChunkAllocator` 统一管理。

---

## `Assets/Scripts/ECS/Systems/SystemPipeline.cs`

### `SystemPipeline`

系统执行管线。

```csharp
public sealed class SystemPipeline : IDisposable
```

它负责三件事：

```text
保存系统顺序
驱动系统每帧更新
在每个系统后自动 World.Playback
```

### 字段

#### `private readonly World _world`

管线驱动的 World。

所有加入管线的系统都会绑定到这个 World。

#### `private readonly List<EcsSystem> _systems`

系统列表。

当前第一版按添加顺序执行。

#### `private bool _disposed`

管线是否已经释放。

释放后不能继续 Add 或 Update。

#### `private bool _isUpdating`

管线当前是否正在执行 `Update`。

用途：

```text
阻止在系统更新过程中动态添加系统
```

第一版先禁止这种行为，让执行顺序保持简单明确。

### 构造函数

#### `SystemPipeline(World world)`

创建系统管线。

参数：

```text
world：要被这条管线驱动的 World
```

### 属性

#### `public int Count`

当前系统数量。

### API

#### `TSystem Add<TSystem>() where TSystem : EcsSystem, new()`

创建并加入一个系统。

适合无构造参数系统：

```csharp
pipeline.Add<MovementSystem>();
```

#### `TSystem Add<TSystem>(TSystem system) where TSystem : EcsSystem`

把已有系统实例加入管线。

适合需要构造参数的系统：

```csharp
pipeline.Add(new SpawnSystem(config));
```

执行流程：

```text
检查管线未释放
检查 system 不为空
检查当前不在 Update 中
system.Attach(world)
加入 _systems
```

`system.Attach(world)` 会立刻触发系统的 `OnCreate`。

#### `void Update(float deltaTime)`

执行一帧系统逻辑。

执行流程：

```text
标记 _isUpdating = true
按顺序执行每个系统
每个系统执行后 World.Playback
最后标记 _isUpdating = false
```

为什么每个系统后都 Playback：

```text
系统 A 可以记录 Add/Remove/Destroy
Playback 把结构变更应用到 Archetype/Chunk
系统 B 查询时能看到系统 A 的结果
```

这就是第一版的安全点。

#### `void Dispose()`

释放管线。

执行流程：

```text
倒序调用系统 Detach
清空系统列表
标记 disposed
```

#### `private void ThrowIfDisposed()`

检查管线是否已经释放。

释放后继续调用 `Add` 或 `Update` 会抛出 `ObjectDisposedException`。

---

## `Assets/Scripts/ECS/Unity/Position2D.cs`

### `Position2D`

2D 位置组件。

```csharp
public struct Position2D : IComponentData
```

它是纯 ECS 数据，可以进入 Chunk。

### 字段

#### `public float X`

横向位置。

#### `public float Y`

纵向位置。

### 构造函数

#### `Position2D(float x, float y)`

创建 2D 位置数据。

---

## `Assets/Scripts/ECS/Unity/TransformProxy.cs`

### `TransformProxy`

Unity Transform 代理组件。

```csharp
public struct TransformProxy : IComponentData
```

它不保存 `Transform` 引用，只保存 `TransformBridge` 返回的整数 Id。

这样做的原因：

```text
Chunk 里保持纯数据
UnityEngine.Object 留在托管桥接表
只有需要表现同步的实体才付出 Unity 对象访问成本
```

### 字段

#### `public int Id`

桥接表里的 Transform 槽位编号。

### 构造函数

#### `TransformProxy(int id)`

创建 Transform 代理组件。

---

## `Assets/Scripts/ECS/Unity/TransformBridge.cs`

### `TransformBridge`

Unity Transform 桥接表。

```csharp
public sealed class TransformBridge : IDisposable
```

它负责把：

```text
TransformProxy.Id
```

映射到：

```text
UnityEngine.Transform
```

### 字段

#### `private readonly List<Transform> _transforms`

Transform 槽位数组。

每个下标就是一个可写入 `TransformProxy.Id` 的 Id。

#### `private readonly Stack<int> _freeIds`

已注销 Id 的复用栈。

用途：

```text
Unregister 后回收槽位
Register 时优先复用旧槽位
```

#### `private int _count`

当前有效 Transform 数量。

### 属性

#### `public int Count`

返回当前有效 Transform 数量。

### API

#### `int Register(Transform transform)`

注册 Transform，并返回 Id。

流程：

```text
如果有空闲槽位，复用空闲 Id
否则追加到 _transforms
_count++
返回 Id
```

#### `bool TryGet(int id, out Transform transform)`

尝试通过 Id 获取 Transform。

返回 false 的情况：

```text
Id 超出范围
槽位为空
Unity 对象已经被销毁
```

#### `Transform Get(int id)`

通过 Id 获取 Transform。

如果找不到，会抛出异常。

#### `void Unregister(int id)`

注销 Transform Id。

流程：

```text
检查 Id 是否有效
清空 _transforms[id]
把 id 放入 _freeIds
_count--
```

#### `void Clear()`

清空所有桥接关系。

#### `void Dispose()`

释放桥接表。

当前等价于 `Clear()`。

---

## `Assets/Scripts/ECS/Unity/TransformSyncSystem.cs`

### `TransformSyncSystem`

把 ECS 位置同步到 Unity Transform 的系统。

```csharp
public sealed unsafe class TransformSyncSystem : EcsSystem
```

它查询：

```text
Position2D
TransformProxy
```

然后把 `Position2D` 写入对应 Transform 的 `position.x/y`。

### 字段

#### `private readonly TransformBridge _bridge`

Transform 桥接表。

系统通过它把 `TransformProxy.Id` 转成真实 Transform。

#### `private Query<Position2D, TransformProxy> _query`

缓存的查询句柄。

在 `OnCreate` 中创建，避免每帧重复创建查询句柄。

### API

#### `TransformSyncSystem(TransformBridge bridge)`

创建同步系统。

#### `protected override void OnCreate()`

缓存 Query：

```text
World.Query<Position2D, TransformProxy>()
```

#### `protected override void OnUpdate(float deltaTime)`

执行同步。

流程：

```text
ForEachChunk 遍历 Position2D + TransformProxy
通过 TransformProxy.Id 查 Transform
找不到 Transform 就跳过
把 Position2D.X/Y 写入 transform.position
保留原本 position.z
```

---

## `Assets/Scripts/ECS/Unity/EcsRunner.cs`

### `EcsRunner`

Unity 场景里的 ECS 启动器。

```csharp
public class EcsRunner : MonoBehaviour
```

它负责把 Unity 生命周期接到 ECS：

```text
Awake -> Initialize
Update -> Tick
OnDestroy -> Shutdown
```

### 字段

#### `private bool addTransformSyncSystem`

是否默认加入 `TransformSyncSystem`。

当前默认开启，让第一版 Unity Bridge 开箱就能同步 Transform。

### 属性

#### `public World World`

Runner 创建并持有的 ECS World。

#### `public SystemPipeline Pipeline`

Runner 创建并驱动的系统管线。

#### `public TransformBridge TransformBridge`

Unity Transform 桥接表。

#### `public bool IsRunning`

Runner 是否已经初始化。

### Unity 生命周期

#### `private void Awake()`

调用 `Initialize()`。

#### `private void Update()`

调用 `Tick(Time.deltaTime)`。

#### `private void OnDestroy()`

调用 `Shutdown()`。

### API

#### `public void Initialize()`

手动初始化 ECS。

流程：

```text
创建 World
创建 SystemPipeline
创建 TransformBridge
调用 Configure
```

重复调用不会重复创建。

#### `public void Tick(float deltaTime)`

执行一帧 ECS 更新。

内部调用：

```text
Pipeline.Update(deltaTime)
```

#### `public void Shutdown()`

释放 ECS。

流程：

```text
Pipeline.Dispose
TransformBridge.Dispose
World.Dispose
清空引用
```

#### `protected virtual void Configure(SystemPipeline pipeline, World world, TransformBridge transformBridge)`

系统注册入口。

子类可以重写这里加入自己的系统。

默认行为：

```text
如果 addTransformSyncSystem 为 true
加入 TransformSyncSystem
```

---

## 三、测试脚本

## `Assets/Scripts/ECS/Core/UnsafeUtil.cs`

### `UnsafeUtil`

unsafe 层工具类。

#### 功能

集中处理底层指针运算中最容易重复出错的操作：

```text
SizeOf
Align
IsAligned
Copy
Clear
```

#### API

##### `int SizeOf<T>() where T : unmanaged`

返回非托管结构体 `T` 的字节大小。

后续用途：

```text
计算组件数组 stride
计算 Chunk Header 大小
计算 memcpy 字节数
```

##### `int Align(int value, int alignment)`

把整数向上对齐到指定边界。

示例：

```text
Align(65, 64) -> 128
```

##### `long Align(long value, int alignment)`

`long` 版本的向上对齐，主要给指针地址计算使用。

##### `IntPtr Align(IntPtr address, int alignment)`

把地址向上对齐。

后续 `ChunkAllocator` 会用它把原始 native memory 地址推到 64 字节边界。

##### `bool IsAligned(IntPtr address, int alignment)`

判断地址是否已经满足指定对齐。

测试和 Debug 检查会使用它。

##### `void Copy(void* source, void* destination, int byteCount)`

复制一段原始内存。

后续结构迁移时会使用：

```text
从旧 Chunk 拷贝共有组件
到新 Chunk 对应 slot
```

##### `void Clear(void* destination, int byteCount)`

把一段原始内存清零。

当前 `ChunkAllocator` 用它清理新切出来的 Chunk Header。

---

## `Assets/Scripts/ECS/Storage/Chunk.cs`

### `Chunk`

固定大小数据块的 Header。

```csharp
internal unsafe struct Chunk
```

真正的内存布局是：

```text
Chunk Header
Entity[]
ComponentArray[0]
ComponentArray[1]
...
```

当前脚本只定义 Header，组件数组偏移会在后续 `ArchetypeLayout` 中实现。

### 常量

#### `public const int Size`

Chunk 固定大小，当前为 `16 * 1024` 字节。

#### `public const int Alignment`

Chunk 起始地址对齐值，当前为 `64` 字节。

### 字段

#### `public int ArchetypeId`

当前 Chunk 属于哪个 Archetype。

#### `public int Count`

当前 Chunk 已经使用了多少个实体 slot。

#### `public int Capacity`

当前 Chunk 最多能容纳多少个实体。

容量不是固定写死的，而是由后续 `ArchetypeLayout` 根据组件大小计算。

#### `public int Sequence`

Chunk 分配序号。

用途：

```text
调试
稳定排序
后续统计
```

#### `public int Flags`

预留标记位。

#### `public int Reserved`

对齐和扩展预留字段。

#### `public Chunk* Next`

同 Archetype 下 Chunk 链表的下一个节点。

#### `public Chunk* Prev`

同 Archetype 下 Chunk 链表的上一个节点。

#### `public Chunk* NextFree`

空闲 Chunk 链表的下一个节点。

注意：

```text
Next / Prev 服务 Archetype 的数据链表
NextFree 服务 ChunkAllocator 或 Archetype 的空闲链表
```

#### `public int* ChangeVersions`

后续 ChangeVersion 优化预留字段。

第一阶段可以保持 `null`。

### API

#### `void Reset(int archetypeId, int capacity, int sequence)`

重置 Chunk Header。

调用时机：

```text
ChunkAllocator.Allocate
ChunkAllocator.Free
```

---

## `Assets/Scripts/ECS/Storage/ChunkAllocator.cs`

### `ChunkAllocator`

Chunk native memory 分配器。

```csharp
internal unsafe sealed class ChunkAllocator : IDisposable
```

### 功能

一次申请一批 native memory，然后切成多个固定大小 Chunk。

这样做是为了避免：

```text
每创建一个 Chunk 都调用一次系统分配
释放 Chunk 时立刻归还系统导致抖动
```

### 字段

#### `private const int DefaultChunksPerBlock`

每次扩容默认申请多少个 Chunk。

当前为 `64`。

#### `private readonly int _chunksPerBlock`

当前分配器实例每个 native block 包含多少个 Chunk。

测试时可以传较小值，方便验证扩容行为。

#### `private IntPtr[] _rawBlocks`

保存每次 `Marshal.AllocHGlobal` 得到的原始地址。

Dispose 时必须释放这些原始地址。

#### `private int _blockCount`

当前已经申请了多少个 native block。

#### `private Chunk* _freeList`

可复用 Chunk 链表。

#### `private int _nextSequence`

下一个分配出去的 Chunk 序号。

#### `private bool _disposed`

表示分配器是否已经释放。

### 属性

#### `public int BlockCount`

当前已经申请的 native block 数量。

#### `public int ChunkSize`

返回 `Chunk.Size`。

#### `public int Alignment`

返回 `Chunk.Alignment`。

### API

#### `Chunk* Allocate(int archetypeId, int capacity)`

从 free list 取一个 Chunk。

如果 free list 为空，则先申请新的 native block。

流程：

```text
检查参数
如果没有空闲 Chunk -> AllocateBlock
从 free list 弹出一个 Chunk
Reset Header
返回 Chunk*
```

#### `void Free(Chunk* chunk)`

把 Chunk 放回 free list。

当前不会立刻释放 native memory，而是留给后续复用。

#### `void Dispose()`

释放所有 native block。

World 销毁时必须最终调用它，避免 native memory 泄漏。

#### `private void AllocateBlock()`

申请一整块 native memory，并切成多个 64 字节对齐的 Chunk。

#### `private void ThrowIfDisposed()`

防止释放后继续使用分配器。

---

## `Assets/Scripts/ECS/Storage/ArchetypeLayout.cs`

### `ArchetypeLayout`

描述一个 Archetype 的数据在 Chunk 内部如何排布。

它不拥有真实内存，只保存计算结果。

### 常量

#### `public const int MissingOffset`

表示某个组件没有真实数据区。

当前主要用于 Tag 组件。

### 字段

#### `public readonly int ChunkSize`

当前布局使用的 Chunk 总大小。

#### `public readonly int HeaderSize`

Chunk Header 占用的字节数，已经按 64 字节对齐。

#### `public readonly int EntityOffset`

`Entity[]` 在 Chunk 内的起始偏移。

#### `public readonly int EntityStride`

单个 `Entity` 占用的字节数。

#### `public readonly int Capacity`

该 Archetype 的一个 Chunk 最多能容纳多少个实体。

不同组件组合会得到不同容量。

#### `public readonly int UsedBytes`

当前布局实际使用到的字节数。

它必须小于等于 `Chunk.Size`。

#### `public readonly int[] ComponentOffsets`

每个组件数组在 Chunk 内的起始偏移。

数组下标对应 `Archetype.Types` 的槽位，不是全局 TypeIndex。

#### `public readonly int[] ComponentStrides`

每个组件单个元素的大小。

Tag 组件 stride 为 0。

### API

#### `static ArchetypeLayout Create(ComponentType[] types)`

使用默认 Chunk 大小和 Header 大小创建布局。

#### `static ArchetypeLayout Create(ComponentType[] types, int chunkSize, int headerSize)`

使用指定 Chunk 参数创建布局。

主要用于测试和后续自定义 Chunk 策略。

#### `int GetComponentOffset(int typeSlot)`

读取某个组件槽位的偏移。

#### `int GetComponentStride(int typeSlot)`

读取某个组件槽位的 stride。

### 计算规则

```text
Header
-> Entity[]
-> ComponentArray[0]
-> ComponentArray[1]
-> ...
```

每个组件数组都会按该组件的 `Align` 对齐。

如果初始容量计算后放不下，就逐步降低容量，直到布局能放进一个 Chunk。

---

## `Assets/Scripts/ECS/Storage/Archetype.cs`

### `Archetype`

表示一组完全相同的组件组合。

例如：

```text
[Position, Velocity]
[Position, Velocity, Health]
```

它是 Query、结构迁移和 Chunk 管理的核心单位。

### 字段

#### `public readonly int Id`

Archetype 编号。

#### `public readonly ComponentMask Mask`

该 Archetype 拥有哪些组件。

#### `public readonly ComponentType[] Types`

组件类型数组。

数组按 `ComponentType.Index` 排序。

#### `public readonly ArchetypeLayout Layout`

该组件组合对应的 Chunk 布局。

#### `public readonly int[] AddEdges`

添加某个组件后的目标 Archetype 缓存。

当前先初始化为 -1，后续结构变更阶段会填充。

#### `public readonly int[] RemoveEdges`

移除某个组件后的目标 Archetype 缓存。

当前先初始化为 -1。

#### `public Chunk* FirstChunk`

该 Archetype 的第一个 Chunk。

#### `public Chunk* LastChunk`

该 Archetype 的最后一个 Chunk。

#### `public Chunk* FirstFreeChunk`

该 Archetype 下还有空位的 Chunk 链表头。

#### `public int Version`

Archetype 内部版本号。

后续可用于调试和缓存刷新。

### API

#### `bool Has(ComponentType type)`

判断该 Archetype 是否包含某个组件。

#### `int GetTypeSlot(int typeIndex)`

根据全局 TypeIndex 找到组件在 `Types` 数组中的槽位。

找不到返回 -1。

#### `int GetComponentOffset(int typeIndex)`

根据全局 TypeIndex 找组件数组偏移。

#### `int GetComponentStride(int typeIndex)`

根据全局 TypeIndex 找组件 stride。

---

## `Assets/Scripts/ECS/Storage/ArchetypeStore.cs`

### `ArchetypeStore`

管理所有 Archetype。

它保证同一个组件组合只会创建一个 Archetype。

### 字段

#### `private readonly Dictionary<ComponentMask, int> _idsByMask`

从组件组合 mask 映射到 ArchetypeId。

#### `private readonly List<Archetype> _archetypes`

保存所有已创建的 Archetype。

### 属性

#### `public int Count`

当前 Archetype 数量。

#### `public int Version`

ArchetypeStore 版本号。

每创建一个新 Archetype，就会增加一次。

后续 QueryCache 会依赖它判断缓存是否过期。

### API

#### `Archetype GetOrCreate(params ComponentType[] types)`

根据组件类型组合获取 Archetype。

如果组合不存在则创建。

执行流程：

```text
复制组件数组
-> 按 TypeIndex 排序
-> 检查重复组件
-> 生成 ComponentMask
-> 查 _idsByMask
-> 不存在则创建 ArchetypeLayout 和 Archetype
```

#### `Archetype GetOrCreate(ComponentMask mask)`

根据组件组合 mask 获取或创建 Archetype。

当前主要给结构变更使用：

```text
Add<T>
-> source.Mask.Add(type.Mask)
-> ArchetypeStore.GetOrCreate(targetMask)

Remove<T>
-> source.Mask.Remove(type.Index)
-> ArchetypeStore.GetOrCreate(targetMask)
```

内部会根据 `TypeRegistry` 中已注册的组件类型，把 mask 还原成有序 `ComponentType[]`。

#### `Archetype GetById(int id)`

根据 ArchetypeId 获取 Archetype。

#### `bool TryFind(ComponentMask mask, out Archetype archetype)`

根据组件组合 mask 查找 Archetype。

---

## `Assets/Scripts/ECS/Tests/ComponentMaskTests.cs`

测试 `ComponentMask` 的位运算行为。

### `FromIndex_SetsLowAndHighBits`

验证：

- 低位 TypeIndex 写入 `Lo`。
- 高位 TypeIndex 写入 `Hi`。

### `ContainsAll_AndIntersects_WorkAcrossBothWords`

验证：

- `ContainsAll` 可以跨 `Lo` 和 `Hi` 工作。
- `Intersects` 可以判断交集。

### `Remove_ClearsOnlyRequestedBit`

验证：

- `Remove` 只清理指定 bit。
- 其他 bit 保持不变。

---

## `Assets/Scripts/ECS/Tests/TypeRegistryTests.cs`

测试组件类型注册表。

### 内部测试组件 `Position`

测试用 unmanaged 组件。

### 内部测试组件 `Velocity`

测试用 unmanaged 组件。

### `SetUp`

每个测试前清空 TypeRegistry，避免测试之间共享 TypeIndex 状态。

### `Register_ReturnsStableIndexForSameType`

验证同一个组件类型重复注册时，返回相同 TypeIndex。

### `Register_AssignsDifferentIndicesForDifferentTypes`

验证不同组件类型会得到不同 TypeIndex。

### `GetByIndex_ReturnsRegisteredType`

验证可以通过 TypeIndex 找回注册好的组件元数据。

---

## `Assets/Scripts/ECS/Tests/EntityStoreTests.cs`

测试实体句柄和位置表。

### `Create_ReturnsEntityHandle`

验证 `EntityStore.Create` 会返回非空实体。

### `SetLocation_MakesEntityAlive`

验证实体设置 Chunk 位置后会变成 alive 状态。

### `Release_InvalidatesOldVersionAndReusesId`

验证：

- 释放实体后 Version 增加。
- Id 可以复用。
- 旧 Entity 句柄失效。

### `Validate_ThrowsForStaleHandle`

验证旧 Entity 句柄调用 `Validate` 会抛异常。

---

## `Assets/Scripts/ECS/Tests/ChunkAllocatorTests.cs`

测试内存层工具和 Chunk 分配器。

### `UnsafeUtil_Align_WorksForIntegersAndPointers`

验证：

- 整数对齐结果正确。
- 指针地址对齐结果正确。
- `IsAligned` 能判断地址是否满足对齐。

### `Allocate_ReturnsAlignedChunkAndInitializesHeader`

验证：

- 分配出的 Chunk 地址满足 64 字节对齐。
- Header 中的 `ArchetypeId`、`Count`、`Capacity`、`Sequence` 初始化正确。
- 链表指针被清空。

### `Free_ReusesChunkFromFreeList`

验证释放后的 Chunk 会进入 free list，并且下一次分配可以复用同一块地址。

### `Allocate_RequestsNewBlockWhenFreeListIsEmpty`

验证 free list 不够用时，分配器会申请新的 native block。

### `Allocate_ThrowsAfterDispose`

验证 `Dispose` 后继续分配会抛出 `ObjectDisposedException`。

---

## `Assets/Scripts/ECS/Tests/ArchetypeStoreTests.cs`

测试 Archetype、ArchetypeLayout 和 ArchetypeStore 的基础行为。

### 内部测试组件 `Position`

测试用位置组件，占用 3 个 `float`。

### 内部测试组件 `Velocity`

测试用速度组件，占用 3 个 `float`。

### 内部测试组件 `Health`

测试用生命值组件，占用 1 个 `int`。

### 内部测试组件 `TestTag`

测试用 Tag 组件，没有字段。

### `SetUp`

每个测试前清空 `TypeRegistry`，避免 TypeIndex 在测试之间互相影响。

### `GetOrCreate_SameTypesDifferentOrder_ReturnsSameArchetype`

验证：

- `[Position, Velocity]` 和 `[Velocity, Position]` 会得到同一个 Archetype。
- `ArchetypeStore.Version` 只在新建 Archetype 时增加。
- Archetype 内部组件类型按 TypeIndex 排序。

### `ArchetypeLayout_CapacityFitsChunk`

验证：

- 计算出的容量大于 0。
- 布局使用字节数不超过 `Chunk.Size`。
- `Entity[]` 起始偏移满足对齐。

### `ArchetypeLayout_ComponentOffsetsAreAligned`

验证组件数组起始偏移满足各自的 `ComponentType.Align`。

### `TagComponent_TakesNoDataSpace`

验证 Tag 组件不占组件数据区：

- offset 为 `MissingOffset`。
- stride 为 0。
- 加 Tag 不会降低 Chunk 容量。

### `TryFind_ReturnsArchetypeByMask`

验证可以通过 `ComponentMask` 找回已经创建的 Archetype。

---

## `Assets/Scripts/ECS/Tests/WorldCreateAccessTests.cs`

测试 `World.Create`、`World.Get`、`World.Set`、`World.Has` 的第一条真实数据链路。

### 内部测试组件 `Position`

测试用位置组件。

### 内部测试组件 `Velocity`

测试用速度组件。

### 内部测试组件 `Health`

测试用生命值组件。

### 内部测试组件 `TestTag`

测试用 Tag 组件。

### `SetUp`

每个测试前清空 `TypeRegistry`。

### `CreateOneComponent_WritesData`

验证创建单组件实体后，可以从 Chunk 中读回组件数据。

### `CreateTwoComponents_WritesData`

验证创建双组件实体后：

- `Has<T>` 正确。
- 第二个组件也能正确写入和读回。

### `CreateThreeComponents_UsesSingleArchetype`

验证三组件创建会直接进入一个最终 Archetype。

### `Get_ReturnsRefToStoredData`

验证 `Get<T>` 返回的是 Chunk 内真实数据的 `ref`。

修改这个引用后，再次 `Get<T>` 能读到新值。

### `Set_UpdatesStoredData`

验证 `Set<T>` 能覆盖 Chunk 中已有组件数据。

### `Has_UsesArchetypeMask`

验证 `Has<T>` 是通过当前 Archetype 的组件掩码判断。

### `Get_MissingComponent_Throws`

验证访问不存在的组件会抛出异常。

---

## `Assets/Scripts/ECS/Tests/WorldDestroyTests.cs`

测试 `World.Destroy` 和 Chunk `swap-remove` 行为。

### 内部测试组件 `Position`

测试用位置组件。

### 内部测试组件 `Health`

测试用生命值组件。

### `SetUp`

每个测试前清空 `TypeRegistry`。

### `Destroy_RemovesEntityAndInvalidatesVersion`

验证实体销毁后：

- `World.IsAlive` 返回 false。
- 旧 Entity 句柄无法继续访问组件。

### `Destroy_SwapRemoveUpdatesMovedEntityLocation`

验证删除 Chunk 中间实体时：

- 最后一个实体会搬到被删除 slot。
- 被搬动实体的 EntityStore 位置会更新。
- 被搬动实体仍然能通过 `Get<T>` 读到正确组件数据。

### `Destroy_EmptyChunkCanBeReusedByLaterCreate`

验证 Chunk 删除到空后会回收到分配器，后续创建实体仍能正常工作。

---

## `Assets/Scripts/ECS/Tests/WorldStructuralChangeTests.cs`

测试 Add/Remove 组件导致的跨 Archetype 迁移。

### 内部测试组件

```text
Position
Velocity
Health
TestTag
```

### `AddComponent_MigratesToNewArchetypeAndKeepsOldData`

验证添加新组件后：

- 实体迁移到新 Archetype。
- 旧组件数据保留。
- 新组件数据写入正确。

### `RemoveComponent_MigratesToNewArchetypeAndKeepsRemainingData`

验证移除组件后：

- 被移除组件不再存在。
- 剩余组件数据保留。

### `AddExistingComponent_BehavesAsSet`

验证添加已存在组件时不会迁移，而是更新组件数据。

### `RemoveMissingComponent_DoesNothing`

验证移除不存在组件时不会破坏已有数据。

### `AddTag_MigratesWithoutDataArea`

验证添加 Tag 组件可以迁移 Archetype，但不需要写组件数据区。

### `StructuralChange_SwapRemoveUpdatesMovedEntityLocation`

验证结构迁移触发源 Chunk swap-remove 时，被移动实体的位置仍然正确。

---

## `Assets/Scripts/ECS/Tests/CommandBufferTests.cs`

测试 `CommandBuffer` 和 `World.Playback`。

### 内部测试组件

```text
Position
Velocity
Health
```

### `Add_PlaybackAddsComponent`

验证 Add 命令在 Playback 前不生效，Playback 后添加组件。

### `Remove_PlaybackRemovesComponent`

验证 Remove 命令在 Playback 前不生效，Playback 后移除组件。

### `Destroy_PlaybackDestroysEntity`

验证 Destroy 命令在 Playback 后销毁实体。

### `PlaybackOrder_IsDeterministic`

验证命令按记录顺序回放。

### `Clear_DropsRecordedCommands`

验证 `Clear` 会丢弃命令，之后 Playback 不会执行被清掉的命令。

---

## `Assets/Scripts/ECS/Tests/QueryTests.cs`

测试 QueryCache、Query 和 ForEach/ForEachChunk。

### 内部测试组件

```text
Position
Velocity
Health
```

### `Query_OneComponent_ReturnsMatchingEntities`

验证单组件 Query 会命中所有包含该组件的 Archetype。

### `Query_TwoComponents_ReturnsOnlyMatchingArchetypes`

验证双组件 Query 只返回同时包含两个组件的实体。

### `Query_ThreeComponents_ReturnsMatchingEntities`

验证三组件 Query 正确。

### `Query_AfterNewArchetype_RefreshesCache`

验证 Query 创建后，如果新 Archetype 出现，QueryCache 会根据 ArchetypeStore.Version 自动刷新。

### `ForEachChunk_CanMutateStoredData`

验证 ForEachChunk 拿到的是 Chunk 内真实组件指针，修改后可以通过 `World.Get<T>` 读回。

---

## `Assets/Scripts/ECS/Tests/SystemPipelineTests.cs`

测试 `EcsSystem` 和 `SystemPipeline`。

### 内部测试组件

```text
Position
Velocity
```

### `SetUp`

每个测试前清空 `TypeRegistry` 和测试系统的事件记录。

### `Add_CallsOnCreate`

验证系统加入管线时：

```text
OnCreate 会被调用
系统能拿到 World
调用顺序被正确记录
```

### `UpdateOrder_IsAddOrder`

验证系统按照 Add 顺序更新。

例如：

```text
先 Add A
再 Add B
Update 时先执行 A，再执行 B
```

### `Update_PlaybackAfterEachSystem`

验证每个系统执行后都会自动 `World.Playback()`。

测试流程：

```text
系统 A 写入 Commands.Add
管线自动 Playback
系统 B 立刻能 Has/Get 到新组件
```

这证明 SystemPipeline 已经把 `CommandBuffer` 安全点串进完整执行链路。

### `Dispose_CallsOnDestroyReverseOrder`

验证管线释放时会倒序调用系统的 `OnDestroy`。

---

## `Assets/Scripts/ECS/Tests/UnityBridgeTests.cs`

测试 Unity Bridge 最小链路。

### `SetUp`

每个测试前清空 `TypeRegistry`。

### `TransformBridge_RegisterAndGet_Works`

验证 `TransformBridge` 可以：

```text
注册 Transform
返回 Id
通过 Id 找回 Transform
正确维护 Count
```

### `TransformBridge_Unregister_ReusesSlot`

验证注销后的 Id 可以被下一次注册复用。

这可以减少桥接表空洞。

### `TransformSyncSystem_UpdatesTransform`

验证 `TransformSyncSystem` 能把 ECS 数据写回 Unity Transform。

测试流程：

```text
创建 GameObject
TransformBridge.Register
World.Create(Position2D, TransformProxy)
SystemPipeline.Add(TransformSyncSystem)
Pipeline.Update
检查 transform.position.x/y
```

### `EcsRunner_InitializeAndShutdown_ManagesWorld`

验证 `EcsRunner` 可以手动初始化和释放：

```text
Initialize 创建 World / Pipeline / TransformBridge
Shutdown 释放并清空引用
```

---

## 四、当前阶段总结

当前已经实现到阶段 E 的 Unity 接入层第一版：

```text
Component 类型身份
Component 掩码
Entity 句柄
Entity 位置表
UnsafeUtil
ChunkAllocator
Chunk Header
ArchetypeLayout
Archetype
ArchetypeStore
World.Create / Has / Get / Set
World.Add / Remove / Destroy
CommandBuffer / Playback
QueryCache / Query / ForEachChunk
EcsSystem / SystemPipeline
Position2D
TransformProxy
TransformBridge
TransformSyncSystem
EcsRunner
```

还没有实现：

```text
Debug & Benchmark
Convenience API
Advanced Optimization
完整 Authoring
SpriteRenderer Bridge
批量渲染 Bridge
```

因此当前代码已经能跑通两条链路。

核心 ECS 链路：

```text
创建实体
写入 Chunk
系统执行
Query 遍历
CommandBuffer 记录结构变更
Playback 应用结构变更
下一个系统读取变更结果
```

Unity Bridge 链路：

```text
Unity EcsRunner 初始化 World/Pipeline
TransformBridge 注册 Transform
ECS Entity 持有 Position2D + TransformProxy
TransformSyncSystem Query ECS 数据
通过 TransformProxy.Id 找到 Transform
写回 transform.position
```

下一步建议进入阶段 F：Debug & Benchmark，先让实体数、Archetype 数、Chunk 数和系统耗时可见，再继续做更高级的 Authoring 和 SpriteRenderer Bridge。
