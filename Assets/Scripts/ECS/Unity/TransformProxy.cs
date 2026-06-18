namespace CyanMothUnityEcs
{
    /// <summary>
    /// ECS 到 Unity Transform 的轻量代理组件。
    /// Chunk 里只存 Id，真正的 Transform 存在 TransformBridge 的托管数组里。
    /// </summary>
    public struct TransformProxy : IComponentData
    {
        public int Id;

        public TransformProxy(int id)
        {
            Id = id;
        }
    }
}
