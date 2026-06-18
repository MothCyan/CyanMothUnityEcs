using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// Unity Transform 桥接表。
    /// ECS Chunk 不能直接存 UnityEngine.Object，所以这里用 int Id 间接找到真实 Transform。
    /// </summary>
    public sealed class TransformBridge : IDisposable
    {
        private readonly List<Transform> _transforms = new List<Transform>();
        private readonly Stack<int> _freeIds = new Stack<int>();
        private int _count;

        /// <summary>
        /// 当前有效 Transform 数量。
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// 注册一个 Transform，并返回可以写入 TransformProxy 的 Id。
        /// </summary>
        public int Register(Transform transform)
        {
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));

            int id;
            if (_freeIds.Count > 0)
            {
                id = _freeIds.Pop();
                _transforms[id] = transform;
            }
            else
            {
                id = _transforms.Count;
                _transforms.Add(transform);
            }

            _count++;
            return id;
        }

        /// <summary>
        /// 尝试通过 Id 获取 Transform。
        /// Unity 对象被销毁后会表现为 null，这里也会返回 false。
        /// </summary>
        public bool TryGet(int id, out Transform transform)
        {
            if ((uint)id >= _transforms.Count)
            {
                transform = null;
                return false;
            }

            transform = _transforms[id];
            return transform != null;
        }

        /// <summary>
        /// 通过 Id 获取 Transform，找不到时抛出异常。
        /// </summary>
        public Transform Get(int id)
        {
            if (!TryGet(id, out Transform transform))
                throw new InvalidOperationException($"TransformProxy.Id {id} 没有对应的有效 Transform。");

            return transform;
        }

        /// <summary>
        /// 注销一个 Transform Id。
        /// Id 会进入复用栈，后续 Register 可以重新使用这个槽位。
        /// </summary>
        public void Unregister(int id)
        {
            if ((uint)id >= _transforms.Count || _transforms[id] == null)
                return;

            _transforms[id] = null;
            _freeIds.Push(id);
            _count--;
        }

        public void Clear()
        {
            _transforms.Clear();
            _freeIds.Clear();
            _count = 0;
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
