using NUnit.Framework;

namespace CyanMothUnityEcs.Tests
{
    public sealed class WorldStatsTests
    {
        private struct Position : IComponentData
        {
            public float X;
        }

        private struct Velocity : IComponentData
        {
            public float X;
        }

        private struct Health : IComponentData
        {
            public int Value;
        }

        [SetUp]
        public void SetUp()
        {
            TypeRegistry.ClearForTests();
        }

        [Test]
        public void GetStats_EmptyWorld_ReturnsZeroRuntimeCounts()
        {
            using (World world = new World())
            {
                WorldStats stats = world.GetStats();

                Assert.AreEqual(0, stats.AliveEntityCount);
                Assert.AreEqual(0, stats.ArchetypeCount);
                Assert.AreEqual(0, stats.ChunkCount);
                Assert.AreEqual(0, stats.CommandCount);
                Assert.AreEqual(0, stats.ChunkUtilization);
            }
        }

        [Test]
        public void GetStats_AfterCreate_ReturnsEntityArchetypeAndChunkCounts()
        {
            using (World world = new World())
            {
                world.Create(new Position { X = 1 }, new Velocity { X = 2 });
                world.Create(new Position { X = 3 });

                WorldStats stats = world.GetStats();

                Assert.AreEqual(2, stats.AliveEntityCount);
                Assert.AreEqual(2, stats.ArchetypeCount);
                Assert.AreEqual(2, stats.ChunkCount);
                Assert.GreaterOrEqual(stats.ReservedChunkCount, stats.ChunkCount);
                Assert.GreaterOrEqual(stats.TotalChunkCapacity, stats.AliveEntityCount);
                Assert.Greater(stats.ChunkUtilization, 0);
            }
        }

        [Test]
        public void GetStats_AfterDestroy_UpdatesAliveEntityAndChunkCounts()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Destroy(entity);

                WorldStats stats = world.GetStats();

                Assert.AreEqual(0, stats.AliveEntityCount);
                Assert.AreEqual(1, stats.ArchetypeCount);
                Assert.AreEqual(0, stats.ChunkCount);
                Assert.AreEqual(0, stats.TotalChunkCapacity);
            }
        }

        [Test]
        public void GetStats_BeforePlayback_ReturnsCommandCount()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Commands.Add(entity, new Health { Value = 10 });

                WorldStats stats = world.GetStats();

                Assert.AreEqual(1, stats.CommandCount);
            }
        }
    }
}
