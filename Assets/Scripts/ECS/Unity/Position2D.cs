namespace CyanMothUnityEcs
{
    /// <summary>
    /// 2D 位置组件。
    /// 只保存纯数值，不直接保存 Transform 或 GameObject，这样数据可以安全放进 Chunk。
    /// </summary>
    public struct Position2D : IComponentData
    {
        public float X;
        public float Y;

        public Position2D(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
