using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 延迟结构变更命令缓冲。
    /// 命令头和组件 payload 分离存储，避免每条命令分配委托闭包。
    /// </summary>
    public unsafe sealed class CommandBuffer
    {
        private const int DefaultCommandCapacity = 64;
        private const int DefaultPayloadCapacity = 1024;

        private Command[] _commands;
        private byte[] _payload;
        private int _count;
        private int _payloadBytes;

        public CommandBuffer(int initialCapacity = DefaultCommandCapacity)
        {
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "命令缓冲初始容量必须大于 0。");

            _commands = new Command[initialCapacity];
            _payload = new byte[DefaultPayloadCapacity];
        }

        public int Count => _count;
        public int PayloadBytes => _payloadBytes;

        public void Add<T>(Entity entity, T component)
            where T : unmanaged, IComponentData
        {
            ComponentType type = TypeRegistry.Get<T>();
            int payloadSize = type.IsTag ? 0 : type.Size;
            int existingIndex = FindLastCommand(entity, type.Index);
            if (existingIndex >= 0 && _commands[existingIndex].Kind == CommandKind.Add)
            {
                int payloadOffset = RewritePayload(_commands[existingIndex], type, &component);
                _commands[existingIndex] = new Command(CommandKind.Add, entity, type.Index, payloadOffset, payloadSize);
                return;
            }

            int newPayloadOffset = WritePayload(type, &component);
            Append(new Command(CommandKind.Add, entity, type.Index, newPayloadOffset, payloadSize));
        }

        public void Remove<T>(Entity entity)
            where T : unmanaged, IComponentData
        {
            ComponentType type = TypeRegistry.Get<T>();
            int existingIndex = FindLastCommand(entity, type.Index);
            if (existingIndex >= 0)
            {
                Command existing = _commands[existingIndex];
                if (existing.Kind == CommandKind.Remove)
                    return;

                if (existing.Kind == CommandKind.Add)
                    RemoveCommandAt(existingIndex);
            }

            Append(new Command(CommandKind.Remove, entity, type.Index, payloadOffset: -1, payloadSize: 0));
        }

        public void Destroy(Entity entity)
        {
            RemoveCommandsForEntity(entity);
            Append(new Command(CommandKind.Destroy, entity, componentTypeIndex: -1, payloadOffset: -1, payloadSize: 0));
        }

        public void Playback(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            fixed (byte* payloadBase = _payload)
            {
                for (int i = 0; i < _count; i++)
                {
                    Command command = _commands[i];
                    Execute(world, command, payloadBase);
                    _commands[i] = default;
                }
            }

            _count = 0;
            _payloadBytes = 0;
        }

        public void Clear()
        {
            Array.Clear(_commands, 0, _count);
            _count = 0;
            _payloadBytes = 0;
        }

        private static void Execute(World world, Command command, byte* payloadBase)
        {
            switch (command.Kind)
            {
                case CommandKind.Add:
                    ComponentType addType = TypeRegistry.GetByIndex(command.ComponentTypeIndex);
                    void* data = command.PayloadSize == 0 ? null : payloadBase + command.PayloadOffset;
                    world.AddRaw(command.Entity, addType, data);
                    break;

                case CommandKind.Remove:
                    ComponentType removeType = TypeRegistry.GetByIndex(command.ComponentTypeIndex);
                    world.RemoveRaw(command.Entity, removeType);
                    break;

                case CommandKind.Destroy:
                    world.Destroy(command.Entity);
                    break;

                default:
                    throw new InvalidOperationException($"未知命令类型：{command.Kind}。");
            }
        }

        private int WritePayload(ComponentType type, void* data)
        {
            if (type.IsTag)
                return -1;

            int payloadOffset = _payloadBytes;
            EnsurePayloadCapacity(_payloadBytes + type.Size);

            fixed (byte* payloadBase = _payload)
            {
                UnsafeUtil.Copy(data, payloadBase + payloadOffset, type.Size);
            }

            _payloadBytes += type.Size;
            return payloadOffset;
        }

        private int RewritePayload(Command command, ComponentType type, void* data)
        {
            if (type.IsTag)
                return -1;

            fixed (byte* payloadBase = _payload)
            {
                UnsafeUtil.Copy(data, payloadBase + command.PayloadOffset, type.Size);
            }

            return command.PayloadOffset;
        }

        private void Append(Command command)
        {
            if (_count == _commands.Length)
                Array.Resize(ref _commands, _commands.Length * 2);

            _commands[_count++] = command;
        }

        private int FindLastCommand(Entity entity, int componentTypeIndex)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                Command command = _commands[i];
                if (command.Entity != entity)
                    continue;

                if (command.Kind == CommandKind.Destroy)
                    return -1;

                if (command.ComponentTypeIndex == componentTypeIndex)
                    return i;
            }

            return -1;
        }

        private void RemoveCommandsForEntity(Entity entity)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                if (_commands[i].Entity == entity)
                    RemoveCommandAt(i);
            }
        }

        private void RemoveCommandAt(int index)
        {
            if ((uint)index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "命令索引超出范围。");

            RewindPayloadIfLast(_commands[index]);

            int moveCount = _count - index - 1;
            if (moveCount > 0)
                Array.Copy(_commands, index + 1, _commands, index, moveCount);

            _commands[--_count] = default;
        }

        private void RewindPayloadIfLast(Command command)
        {
            if (command.PayloadSize <= 0)
                return;

            int payloadEnd = command.PayloadOffset + command.PayloadSize;
            if (payloadEnd == _payloadBytes)
                _payloadBytes = command.PayloadOffset;
        }

        private void EnsurePayloadCapacity(int requiredBytes)
        {
            if (requiredBytes <= _payload.Length)
                return;

            int newCapacity = _payload.Length;
            while (newCapacity < requiredBytes)
                newCapacity *= 2;

            Array.Resize(ref _payload, newCapacity);
        }

        private readonly struct Command
        {
            public readonly CommandKind Kind;
            public readonly Entity Entity;
            public readonly int ComponentTypeIndex;
            public readonly int PayloadOffset;
            public readonly int PayloadSize;

            public Command(CommandKind kind, Entity entity, int componentTypeIndex, int payloadOffset, int payloadSize)
            {
                Kind = kind;
                Entity = entity;
                ComponentTypeIndex = componentTypeIndex;
                PayloadOffset = payloadOffset;
                PayloadSize = payloadSize;
            }

            public override string ToString()
            {
                return $"{Kind} {Entity} Type={ComponentTypeIndex} Payload={PayloadOffset}:{PayloadSize}";
            }
        }
    }
}
