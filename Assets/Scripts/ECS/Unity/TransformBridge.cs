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

    /// <summary>
    /// ECS 到 Unity SpriteRenderer 的轻量代理组件。
    /// Chunk 里只保存桥接表 Id，真实 SpriteRenderer 留在托管侧。
    /// </summary>
    public struct SpriteRendererProxy : IComponentData
    {
        public int Id;

        public SpriteRendererProxy(int id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// SpriteRenderer 的 ECS 可写显示状态。
    /// 使用 float 保存颜色，避免 ECS 组件直接依赖 UnityEngine.Color。
    /// </summary>
    public struct SpriteRenderState : IComponentData
    {
        public float R;
        public float G;
        public float B;
        public float A;
        public byte Visible;

        public SpriteRenderState(float r, float g, float b, float a, bool visible = true)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            Visible = visible ? (byte)1 : (byte)0;
        }

        public static SpriteRenderState FromColor(Color color, bool visible = true)
        {
            return new SpriteRenderState(color.r, color.g, color.b, color.a, visible);
        }
    }

    /// <summary>
    /// Unity SpriteRenderer 桥接表。
    /// 它和 TransformBridge 一样，用 int Id 隔离 ECS 纯数据和 Unity 托管对象。
    /// </summary>
    public sealed class SpriteRendererBridge : IDisposable
    {
        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private readonly Stack<int> _freeIds = new Stack<int>();
        private int _count;

        public int Count => _count;

        public int Register(SpriteRenderer renderer)
        {
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));

            int id;
            if (_freeIds.Count > 0)
            {
                id = _freeIds.Pop();
                _renderers[id] = renderer;
            }
            else
            {
                id = _renderers.Count;
                _renderers.Add(renderer);
            }

            _count++;
            return id;
        }

        public bool TryGet(int id, out SpriteRenderer renderer)
        {
            if ((uint)id >= _renderers.Count)
            {
                renderer = null;
                return false;
            }

            renderer = _renderers[id];
            return renderer != null;
        }

        public SpriteRenderer Get(int id)
        {
            if (!TryGet(id, out SpriteRenderer renderer))
                throw new InvalidOperationException($"SpriteRendererProxy.Id {id} 没有对应的有效 SpriteRenderer。");

            return renderer;
        }

        public void Unregister(int id)
        {
            if ((uint)id >= _renderers.Count || _renderers[id] == null)
                return;

            _renderers[id] = null;
            _freeIds.Push(id);
            _count--;
        }

        public void Clear()
        {
            _renderers.Clear();
            _freeIds.Clear();
            _count = 0;
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
