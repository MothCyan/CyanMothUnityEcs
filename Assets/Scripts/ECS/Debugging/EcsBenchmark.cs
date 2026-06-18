using System;
using System.Diagnostics;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 第一版 ECS 基准测试入口。
    /// 它用于建立性能反馈，不参与运行时热路径。
    /// </summary>
    public static class EcsBenchmark
    {
        public static EcsBenchmarkResult CreatePositionVelocity(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Benchmark 数量不能为负数。");

            TypeRegistry.ClearForTests();
            using (World world = new World())
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    world.Create(
                        new BenchmarkPosition { X = i, Y = i },
                        new BenchmarkVelocity { X = 1, Y = 1 });
                }

                stopwatch.Stop();
                return CreateResult("Create Position+Velocity", count, stopwatch, world);
            }
        }

        public static EcsBenchmarkResult QueryPositionVelocity(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Benchmark 数量不能为负数。");

            TypeRegistry.ClearForTests();
            using (World world = new World())
            {
                for (int i = 0; i < count; i++)
                {
                    world.Create(
                        new BenchmarkPosition { X = i, Y = i },
                        new BenchmarkVelocity { X = 1, Y = 1 });
                }

                Query<BenchmarkPosition, BenchmarkVelocity> query = world.Query<BenchmarkPosition, BenchmarkVelocity>();
                float sum = 0;

                Stopwatch stopwatch = Stopwatch.StartNew();
                query.ForEach((Entity entity, ref BenchmarkPosition position, ref BenchmarkVelocity velocity) =>
                {
                    sum += position.X + velocity.X;
                });

                stopwatch.Stop();
                return CreateResult("Query Position+Velocity", count, stopwatch, world);
            }
        }

        public static EcsBenchmarkResult AddRemoveHealth(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Benchmark 数量不能为负数。");

            TypeRegistry.ClearForTests();
            using (World world = new World())
            {
                Entity[] entities = new Entity[count];
                for (int i = 0; i < count; i++)
                    entities[i] = world.Create(new BenchmarkPosition { X = i, Y = i });

                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                    world.Add(entities[i], new BenchmarkHealth { Value = i });

                for (int i = 0; i < count; i++)
                    world.Remove<BenchmarkHealth>(entities[i]);

                stopwatch.Stop();
                return CreateResult("Add/Remove Health", count, stopwatch, world);
            }
        }

        private static EcsBenchmarkResult CreateResult(string name, int iterations, Stopwatch stopwatch, World world)
        {
            return new EcsBenchmarkResult(
                name,
                iterations,
                stopwatch.ElapsedTicks,
                stopwatch.Elapsed.TotalMilliseconds,
                world.GetStats());
        }

        private struct BenchmarkPosition : IComponentData
        {
            public float X;
            public float Y;
        }

        private struct BenchmarkVelocity : IComponentData
        {
            public float X;
            public float Y;
        }

        private struct BenchmarkHealth : IComponentData
        {
            public int Value;
        }
    }
}
