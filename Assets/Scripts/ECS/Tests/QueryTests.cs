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

        private struct Active : IEnableableComponent
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

        [Test]
        public void Query_SameMaskDifferentOrder_UsesCorrectCachedOffsets()
        {
            using (World world = new World())
            {
                world.Create(new Position { X = 3 }, new Velocity { X = 5 });

                float positionFirst = 0;
                float velocitySecond = 0;
                world.Query<Position, Velocity>().ForEachChunk((Entity* entities, Position* positions, Velocity* velocities, int count) =>
                {
                    positionFirst = positions[0].X;
                    velocitySecond = velocities[0].X;
                });

                float velocityFirst = 0;
                float positionSecond = 0;
                world.Query<Velocity, Position>().ForEachChunk((Entity* entities, Velocity* velocities, Position* positions, int count) =>
                {
                    velocityFirst = velocities[0].X;
                    positionSecond = positions[0].X;
                });

                Assert.AreEqual(3, positionFirst);
                Assert.AreEqual(5, velocitySecond);
                Assert.AreEqual(5, velocityFirst);
                Assert.AreEqual(3, positionSecond);
            }
        }

        [Test]
        public void ForEachChanged_FiltersChunksByComponentVersion()
        {
            using (World world = new World())
            {
                Entity first = world.Create(new Position { X = 1 }, new Velocity { X = 10 });
                Entity second = world.Create(new Position { X = 2 }, new Velocity { X = 20 });

                int count = 0;
                world.Query<Position, Velocity>().ForEachChanged<Position>(0, (Entity _, ref Position position, ref Velocity velocity) =>
                {
                    count++;
                });

                Assert.AreEqual(2, count);

                int lastVersion = world.ChangeVersion;
                count = 0;
                world.Query<Position, Velocity>().ForEachChanged<Position>(lastVersion, (Entity _, ref Position position, ref Velocity velocity) =>
                {
                    count++;
                });

                Assert.AreEqual(0, count);

                world.Set(first, new Position { X = 3 });
                count = 0;
                float sum = 0;
                world.Query<Position, Velocity>().ForEachChanged<Position>(lastVersion, (Entity _, ref Position position, ref Velocity velocity) =>
                {
                    count++;
                    sum += position.X;
                });

                Assert.AreEqual(2, count);
                Assert.AreEqual(5, sum);
                Assert.AreEqual(2, world.Get<Position>(second).X);
            }
        }

        [Test]
        public void ForEachChanged_IgnoresChunksWhenDifferentComponentChanged()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 }, new Velocity { X = 2 });
                int lastVersion = world.ChangeVersion;

                world.Set(entity, new Velocity { X = 5 });

                int count = 0;
                world.Query<Position, Velocity>().ForEachChanged<Position>(lastVersion, (Entity _, ref Position position, ref Velocity velocity) =>
                {
                    count++;
                });

                Assert.AreEqual(0, count);
            }
        }

        [Test]
        public void ForEachReadOnly_DoesNotChangeVersion()
        {
            using (World world = new World())
            {
                world.Create(new Position { X = 1 }, new Velocity { X = 2 });
                int before = world.ChangeVersion;
                float sum = 0;

                world.Query<Position, Velocity>().ForEachReadOnly((Entity _, in Position position, in Velocity velocity) =>
                {
                    sum += position.X + velocity.X;
                });

                Assert.AreEqual(3, sum);
                Assert.AreEqual(before, world.ChangeVersion);
            }
        }

        [Test]
        public void ForEachWritable_ChangesVersionConservatively()
        {
            using (World world = new World())
            {
                world.Create(new Position { X = 1 });
                int before = world.ChangeVersion;

                world.Query<Position>().ForEach((Entity entity, ref Position position) =>
                {
                    float value = position.X;
                });

                Assert.Greater(world.ChangeVersion, before);
            }
        }

        [Test]
        public void ForEachChangedReadOnly_DoesNotCauseRepeatedMatch()
        {
            using (World world = new World())
            {
                world.Create(new Position { X = 1 }, new Velocity { X = 2 });
                int sinceVersion = 0;

                int count = 0;
                world.Query<Position, Velocity>().ForEachChangedReadOnly<Position>(sinceVersion, (Entity _, in Position position, in Velocity velocity) =>
                {
                    count++;
                });

                Assert.AreEqual(1, count);

                sinceVersion = world.ChangeVersion;
                count = 0;
                world.Query<Position, Velocity>().ForEachChangedReadOnly<Position>(sinceVersion, (Entity _, in Position position, in Velocity velocity) =>
                {
                    count++;
                });

                Assert.AreEqual(0, count);
            }
        }

        [Test]
        public void ForEachEnabledChunk_ProvidesEnableMask()
        {
            using (World world = new World())
            {
                Entity first = world.Create(new Active { Value = 1 }, new Position { X = 10 });
                Entity second = world.Create(new Active { Value = 2 }, new Position { X = 20 });
                world.SetComponentEnabled<Active>(second, false);

                int enabledCount = 0;
                int activeSum = 0;
                float positionSum = 0;
                world.Query<Active, Position>().ForEachEnabledChunk<Active>((EnabledChunk enabled, Entity* entities, Active* actives, Position* positions, int count) =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (!enabled.IsEnabled(i))
                            continue;

                        enabledCount++;
                        activeSum += actives[i].Value;
                        positionSum += positions[i].X;
                    }
                });

                Assert.AreEqual(1, enabledCount);
                Assert.AreEqual(1, activeSum);
                Assert.AreEqual(10, positionSum);
                Assert.IsTrue(world.IsComponentEnabled<Active>(first));
            }
        }

        [Test]
        public void ForEachEnabledChunk_NonEnableableComponentActsAsAllEnabled()
        {
            using (World world = new World())
            {
                world.Create(new Position { X = 1 });
                world.Create(new Position { X = 2 });

                int countFromMask = 0;
                world.Query<Position>().ForEachEnabledChunk<Position>((EnabledChunk enabled, Entity* entities, Position* positions, int count) =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (enabled.IsEnabled(i))
                            countFromMask++;
                    }
                });

                Assert.AreEqual(2, countFromMask);
            }
        }
    }
}
