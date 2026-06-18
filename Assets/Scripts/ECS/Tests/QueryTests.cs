using NUnit.Framework;

#pragma warning disable CS0649

namespace CyanMothUnityEcs.Tests
{
    public unsafe sealed class QueryTests
    {
        private struct Position : IComponentData
        {
            public float X;
            public float Y;
        }

        private struct Velocity : IComponentData
        {
            public float X;
            public float Y;
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
        public void Query_OneComponent_ReturnsMatchingEntities()
        {
            using (World world = new World())
            {
                world.Create(new Position { X = 1 });
                world.Create(new Velocity { X = 10 });
                world.Create(new Position { X = 2 }, new Velocity { X = 20 });

                int count = 0;
                float sum = 0;
                world.Query<Position>().ForEach((Entity _, ref Position position) =>
                {
                    count++;
                    sum += position.X;
                });

                Assert.AreEqual(2, count);
                Assert.AreEqual(3, sum);
            }
        }

        [Test]
        public void Query_TwoComponents_ReturnsOnlyMatchingArchetypes()
        {
            using (World world = new World())
            {
                world.Create(new Position { X = 1 });
                world.Create(new Position { X = 2 }, new Velocity { X = 3 });
                world.Create(new Position { X = 4 }, new Velocity { X = 5 }, new Health { Value = 6 });

                int count = 0;
                float velocitySum = 0;
                world.Query<Position, Velocity>().ForEach((Entity _, ref Position position, ref Velocity velocity) =>
                {
                    count++;
                    velocitySum += velocity.X;
                });

                Assert.AreEqual(2, count);
                Assert.AreEqual(8, velocitySum);
            }
        }

        [Test]
        public void Query_ThreeComponents_ReturnsMatchingEntities()
        {
            using (World world = new World())
            {
                world.Create(new Position(), new Velocity());
                world.Create(new Position { X = 7 }, new Velocity { X = 8 }, new Health { Value = 9 });

                int count = 0;
                int healthSum = 0;
                world.Query<Position, Velocity, Health>().ForEach((Entity _, ref Position position, ref Velocity velocity, ref Health health) =>
                {
                    count++;
                    healthSum += health.Value;
                });

                Assert.AreEqual(1, count);
                Assert.AreEqual(9, healthSum);
            }
        }

        [Test]
        public void Query_AfterNewArchetype_RefreshesCache()
        {
            using (World world = new World())
            {
                Query<Position, Velocity> query = world.Query<Position, Velocity>();
                world.Create(new Position { X = 1 }, new Velocity { X = 2 });

                int count = 0;
                query.ForEach((Entity _, ref Position position, ref Velocity velocity) => count++);

                Assert.AreEqual(1, count);
            }
        }

        [Test]
        public void ForEachChunk_CanMutateStoredData()
        {
            using (World world = new World())
            {
                Entity first = world.Create(new Position { X = 1 }, new Velocity { X = 10 });
                Entity second = world.Create(new Position { X = 2 }, new Velocity { X = 20 });

                world.Query<Position, Velocity>().ForEachChunk((Entity* entities, Position* positions, Velocity* velocities, int count) =>
                {
                    for (int i = 0; i < count; i++)
                        positions[i].X += velocities[i].X;
                });

                Assert.AreEqual(11, world.Get<Position>(first).X);
                Assert.AreEqual(22, world.Get<Position>(second).X);
            }
        }
    }
}
