using NUnit.Framework;

#pragma warning disable CS0649

namespace CyanMothUnityEcs.Tests
{
    public sealed class WorldCreateAccessTests
    {
        private struct Position : IComponentData
        {
            public float X;
            public float Y;
            public float Z;
        }

        private struct Velocity : IComponentData
        {
            public float X;
            public float Y;
            public float Z;
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
        public void CreateOneComponent_WritesData()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1, Y = 2, Z = 3 });

                Assert.IsTrue(world.IsAlive(entity));
                ref Position position = ref world.Get<Position>(entity);
                Assert.AreEqual(1, position.X);
                Assert.AreEqual(2, position.Y);
                Assert.AreEqual(3, position.Z);
            }
        }

        [Test]
        public void CreateTwoComponents_WritesData()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(
                    new Position { X = 1, Y = 2, Z = 3 },
                    new Velocity { X = 4, Y = 5, Z = 6 });

                Assert.IsTrue(world.Has<Position>(entity));
                Assert.IsTrue(world.Has<Velocity>(entity));
                Assert.AreEqual(4, world.Get<Velocity>(entity).X);
                Assert.AreEqual(6, world.Get<Velocity>(entity).Z);
            }
        }

        [Test]
        public void CreateThreeComponents_UsesSingleArchetype()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(
                    new Position { X = 1 },
                    new Velocity { X = 2 },
                    new Health { Value = 100 });

                Assert.AreEqual(1, world.ArchetypeCount);
                Assert.AreEqual(100, world.Get<Health>(entity).Value);
            }
        }

        [Test]
        public void Get_ReturnsRefToStoredData()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                ref Position position = ref world.Get<Position>(entity);
                position.X = 9;

                Assert.AreEqual(9, world.Get<Position>(entity).X);
            }
        }

        [Test]
        public void Set_UpdatesStoredData()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Health { Value = 10 });

                world.Set(entity, new Health { Value = 25 });

                Assert.AreEqual(25, world.Get<Health>(entity).Value);
            }
        }

        [Test]
        public void Has_UsesArchetypeMask()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position(), new TestTag());

                Assert.IsTrue(world.Has<Position>(entity));
                Assert.IsTrue(world.Has<TestTag>(entity));
                Assert.IsFalse(world.Has<Velocity>(entity));
            }
        }

        [Test]
        public void Get_MissingComponent_Throws()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position());

                Assert.Throws<System.InvalidOperationException>(() =>
                {
                    ref Velocity _ = ref world.Get<Velocity>(entity);
                });
            }
        }
    }
}
