namespace CyanMothUnityEcs
{
    /// <summary>
    /// CommandBuffer 中记录的命令类型。
    /// 第一版只覆盖结构变更所需的 Add / Remove / Destroy。
    /// </summary>
    internal enum CommandKind
    {
        Add,
        Remove,
        Destroy
    }
}
