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

## 四、当前阶段总结

当前已经实现的是阶段 A 的地基层：

```text
Component 类型身份
Component 掩码
Entity 句柄
Entity 位置表
```

还没有实现：

```text
Chunk
Archetype
World.Create
Query
CommandBuffer
SystemPipeline
Unity Bridge
```

因此当前代码只能验证 ECS 的身份系统和位置系统，还不能真正存储组件数据。
