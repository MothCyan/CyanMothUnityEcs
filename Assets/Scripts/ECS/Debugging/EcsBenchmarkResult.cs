namespace CyanMothUnityEcs
{
    /// <summary>
    /// 单项 Benchmark 结果。
    /// </summary>
    public readonly struct EcsBenchmarkResult
    {
        public readonly string Name;
        public readonly int Iterations;
        public readonly long ElapsedTicks;
        public readonly double ElapsedMilliseconds;
        public readonly WorldStats Stats;

        public EcsBenchmarkResult(string name, int iterations, long elapsedTicks, double elapsedMilliseconds, WorldStats stats)
        {
            Name = name;
            Iterations = iterations;
            ElapsedTicks = elapsedTicks;
            ElapsedMilliseconds = elapsedMilliseconds;
            Stats = stats;
        }

        public override string ToString()
        {
            return $"{Name}: {Iterations} iterations, {ElapsedMilliseconds:0.###} ms, {Stats}";
        }
    }
}
