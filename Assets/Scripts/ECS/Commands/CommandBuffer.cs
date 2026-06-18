using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 延迟结构变更命令缓冲。
    /// 系统或 Query 遍历期间可以先记录命令，之后由 World.Playback 在安全点统一回放。
    /// </summary>
    public sealed class CommandBuffer
    {
        private const int DefaultCapacity = 64;

        private Command[] _commands;
        private int _count;

        public CommandBuffer(int initialCapacity = DefaultCapacity)
        {
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "命令缓冲初始容量必须大于 0。");

            _commands = new Command[initialCapacity];
        }

        public int Count => _count;

        public void Add<T>(Entity entity, T component)
            where T : unmanaged, IComponentData
        {
            Append(new Command(CommandKind.Add, world => world.Add(entity, component)));
        }

        public void Remove<T>(Entity entity)
            where T : unmanaged, IComponentData
        {
            Append(new Command(CommandKind.Remove, world => world.Remove<T>(entity)));
        }

        public void Destroy(Entity entity)
        {
            Append(new Command(CommandKind.Destroy, world => world.Destroy(entity)));
        }

        public void Playback(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            for (int i = 0; i < _count; i++)
            {
                Command command = _commands[i];
                command.Execute(world);
                _commands[i] = default;
            }

            _count = 0;
        }

        public void Clear()
        {
            Array.Clear(_commands, 0, _count);
            _count = 0;
        }

        private void Append(Command command)
        {
            if (_count == _commands.Length)
                Array.Resize(ref _commands, _commands.Length * 2);

            _commands[_count++] = command;
        }

        private readonly struct Command
        {
            private readonly CommandKind _kind;
            private readonly Action<World> _execute;

            public Command(CommandKind kind, Action<World> execute)
            {
                _kind = kind;
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public void Execute(World world)
            {
                _execute(world);
            }

            public override string ToString()
            {
                return _kind.ToString();
            }
        }
    }
}
