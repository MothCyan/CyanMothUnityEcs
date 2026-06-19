using System;
using System.Collections.Generic;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 系统执行管线。
    /// 它按添加顺序执行系统，并在每个系统执行后回放 CommandBuffer，形成安全的结构变更点。
    /// </summary>
    public sealed class SystemPipeline : IDisposable
    {
        private readonly World _world;
        private readonly List<EcsSystem> _systems;
        private bool _disposed;
        private bool _isUpdating;

        public SystemPipeline(World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _systems = new List<EcsSystem>();
        }

        /// <summary>
        /// 当前管线中的系统数量。
        /// </summary>
        public int Count => _systems.Count;

        /// <summary>
        /// 创建一个系统实例并加入管线。
        /// 这是最方便的写法，适合普通业务系统。
        /// </summary>
        public TSystem Add<TSystem>()
            where TSystem : EcsSystem, new()
        {
            return Add(new TSystem());
        }

        /// <summary>
        /// 把已有系统实例加入管线。
        /// 加入时会立刻调用系统的 OnCreate。
        /// </summary>
        public TSystem Add<TSystem>(TSystem system)
            where TSystem : EcsSystem
        {
            ThrowIfDisposed();

            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (_isUpdating)
                throw new InvalidOperationException("不能在 SystemPipeline.Update 过程中添加系统。");

            system.Attach(_world);
            _systems.Add(system);
            return system;
        }

        /// <summary>
        /// 执行一帧系统逻辑。
        /// 每个系统执行完都会自动 World.Playback，让下一个系统看到已经完成的结构变更。
        /// </summary>
        public void Update(float deltaTime)
        {
            ThrowIfDisposed();

            _isUpdating = true;
            try
            {
                for (int i = 0; i < _systems.Count; i++)
                {
                    EcsSystem system = _systems[i];
                    system.Update(deltaTime);
                    _world.Playback();
                    system.CommitVersion();
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            for (int i = _systems.Count - 1; i >= 0; i--)
                _systems[i].Detach();

            _systems.Clear();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SystemPipeline));
        }
    }
}
