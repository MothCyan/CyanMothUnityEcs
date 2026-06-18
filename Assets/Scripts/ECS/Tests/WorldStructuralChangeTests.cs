using NUnit.Framework;

#pragma warning disable CS0649

namespace CyanMothUnityEcs.Tests
{
    public sealed class WorldStructuralChangeTests
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

        private struct TestTag : IComponentData
        {
        }

        [SetUp]
        public void SetUp()
        {
            TypeRegistry.ClearForTests();
        }

        [Test]
        public void AddComponent_MigratesToNewArchetypeAndKeepsOldData()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1, Y = 2 });

                world.Add(entity, new Velocity { X = 3, Y = 4 });

                Assert.IsTrue(world.Has<Position>(entity));
                Assert.IsTrue(world.Has<Velocity>(entity));
                Assert.AreEqual(1, world.Get<Position>(entity).X);
                Assert.AreEqual(2, world.Get<Position>(entity).Y);
                Assert.AreEqual(3, world.Get<Velocity>(entity).X);
                Assert.AreEqual(4, world.Get<Velocity>(entity).Y);
            }
        }

        [Test]
        public void RemoveComponent_MigratesToNewArchetypeAndKeepsRemainingData()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(
                    new Position { X = 1, Y = 2 },
                    new Velocity { X = 3, Y = 4 },
                    new Health { Value = 100 });

                world.Remove<Velocity>(entity);

                Assert.IsTrue(world.Has<Position>(entity));
                Assert.IsFalse(world.Has<Velocity>(entity));
                Assert.IsTrue(world.Has<Health>(entity));
                Assert.AreEqual(1, world.Get<Position>(entity).X);
                Assert.AreEqual(100, world.Get<Health>(entity).Value);
            }
        }

        [Test]
        public void AddExistingComponent_BehavesAsSet()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Health { Value = 10 });

                world.Add(entity, new Health { Value = 25 });

                Assert.AreEqual(25, world.Get<Health>(entity).Value);
                Assert.AreEqual(1, world.ArchetypeCount);
            }
        }

        [Test]
        public void RemoveMissingComponent_DoesNothing()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 7 });

                world.Remove<Velocity>(entity);

                Assert.IsTrue(world.Has<Position>(entity));
                Assert.AreEqual(7, world.Get<Position>(entity).X);
            }
        }

        [Test]
        public void AddTag_MigratesWithoutDataArea()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Add(entity, new TestTag());

                Assert.IsTrue(world.Has<TestTag>(entity));
                Assert.AreEqual(1, world.Get<Position>(entity).X);
            }
        }

        [Test]
        public void StructuralChange_SwapRemoveUpdatesMovedEntityLocation()
        {
            using (World world = new World())
            {
                Entity first = world.Create(new Position { X = 1 }, new Health { Value = 10 });
                Entity second = world.Create(new Position { X = 2 }, new Health { Value = 20 });
                Entity third = world.Create(new Position { X = 3 }, new Health { Value = 30 });

                world.Add(first, new Velocity { X = 100 });

                Assert.IsTrue(world.Has<Velocity>(first));
                Assert.IsTrue(world.Has<Position>(third));
                Assert.IsTrue(world.Has<Health>(third));
                Assert.AreEqual(3, world.Get<Position>(third).X);
                Assert.AreEqual(30, world.Get<Health>(third).Value);
                Assert.AreEqual(2, world.Get<Position>(second).X);
                Assert.AreEqual(20, world.Get<Health>(second).Value);
            }
        }
    }
}
