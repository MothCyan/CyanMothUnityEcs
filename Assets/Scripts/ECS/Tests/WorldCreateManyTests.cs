using System;
using NUnit.Framework;

namespace CyanMothUnityEcs.Tests
{
    public sealed class WorldCreateManyTests
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
        public void CreateMany_OneComponent_WritesAllEntities()
        {
            using (World world = new World())
            {
                Position[] positions =
                {
                    new Position { X = 1 },
                    new Position { X = 2 },
                    new Position { X = 3 }
                };

                Entity[] entities = world.CreateMany(positions);

                Assert.AreEqual(3, entities.Length);
                for (int i = 0; i < entities.Length; i++)
                {
                    Assert.IsTrue(world.IsAlive(entities[i]));
                    Assert.AreEqual(positions[i].X, world.Get<Position>(entities[i]).X);
                }
            }
        }

        [Test]
        public void CreateMany_TwoComponents_WritesAllEntities()
        {
            using (World world = new World())
            {
                Position[] positions =
                {
                    new Position { X = 1 },
                    new Position { X = 2 }
                };
                Velocity[] velocities =
                {
                    new Velocity { X = 10 },
                    new Velocity { X = 20 }
                };
                Entity[] entities = new Entity[2];

                world.CreateMany(positions, velocities, entities);

                for (int i = 0; i < entities.Length; i++)
                {
                    Assert.AreEqual(positions[i].X, world.Get<Position>(entities[i]).X);
                    Assert.AreEqual(velocities[i].X, world.Get<Velocity>(entities[i]).X);
                }
            }
        }

        [Test]
        public void CreateMany_ThreeComponents_WritesAllEntities()
        {
            using (World world = new World())
            {
                Position[] positions = { new Position { X = 1 } };
                Velocity[] velocities = { new Velocity { X = 2 } };
                Health[] health = { new Health { Value = 3 } };

                Entity[] entities = world.CreateMany(positions, velocities, health);

                Assert.AreEqual(1, entities.Length);
                Assert.AreEqual(1, world.Get<Position>(entities[0]).X);
                Assert.AreEqual(2, world.Get<Velocity>(entities[0]).X);
                Assert.AreEqual(3, world.Get<Health>(entities[0]).Value);
            }
        }

        [Test]
        public void CreateMany_MoreThanOneChunk_WritesAllEntities()
        {
            using (World world = new World())
            {
                const int count = 3000;
                Position[] positions = new Position[count];
                Entity[] entities = new Entity[count];

                for (int i = 0; i < count; i++)
                    positions[i] = new Position { X = i };

                world.CreateMany(positions, entities);

                WorldStats stats = world.GetStats();
                Assert.Greater(stats.ChunkCount, 1);

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(world.IsAlive(entities[i]));
                    Assert.AreEqual(i, world.Get<Position>(entities[i]).X);
                }
            }
        }

        [Test]
        public void CreateMany_MismatchedComponentLengths_Throws()
        {
            using (World world = new World())
            {
                Position[] positions = { new Position { X = 1 }, new Position { X = 2 } };
                Velocity[] velocities = { new Velocity { X = 3 } };
                Entity[] entities = new Entity[2];

                Assert.Throws<ArgumentException>(() => world.CreateMany(positions, velocities, entities));
            }
        }

        [Test]
        public void CreateMany_OutputArrayTooSmall_Throws()
        {
            using (World world = new World())
            {
                Position[] positions = { new Position { X = 1 }, new Position { X = 2 } };
                Entity[] entities = new Entity[1];

                Assert.Throws<ArgumentException>(() => world.CreateMany(positions, entities));
            }
        }
    }
}
