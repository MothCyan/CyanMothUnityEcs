using NUnit.Framework;

namespace CyanMothUnityEcs.Tests
{
    public sealed class ChangeVersionTests
    {
        private struct Position : IComponentData
        {
            public float X;
        }

        private struct Velocity : IComponentData
        {
            public float X;
        }

        [SetUp]
        public void SetUp()
        {
            TypeRegistry.ClearForTests();
        }

        [Test]
        public void Create_WritesInitialChangeVersion()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                Assert.Greater(world.GetChangeVersion<Position>(entity), 0);
            }
        }

        [Test]
        public void Set_IncrementsChangeVersion()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });
                int before = world.GetChangeVersion<Position>(entity);

                world.Set(entity, new Position { X = 2 });
                int after = world.GetChangeVersion<Position>(entity);

                Assert.Greater(after, before);
            }
        }

        [Test]
        public void CreateMany_WritesChangeVersion()
        {
            using (World world = new World())
            {
                Entity[] entities = world.CreateMany(new[]
                {
                    new Position { X = 1 },
                    new Position { X = 2 }
                });

                Assert.Greater(world.GetChangeVersion<Position>(entities[0]), 0);
                Assert.AreEqual(
                    world.GetChangeVersion<Position>(entities[0]),
                    world.GetChangeVersion<Position>(entities[1]));
            }
        }

        [Test]
        public void Add_SetsNewComponentChangeVersion()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Add(entity, new Velocity { X = 2 });

                Assert.Greater(world.GetChangeVersion<Velocity>(entity), 0);
            }
        }

        [Test]
        public void Add_DoesNotLowerSharedComponentChangeVersion()
        {
            using (World world = new World())
            {
                Entity source = world.Create(new Position { X = 1 });
                Entity target = world.Create(new Position { X = 2 }, new Velocity { X = 3 });
                world.Set(target, new Position { X = 4 });
                int targetVersionBefore = world.GetChangeVersion<Position>(target);

                world.Add(source, new Velocity { X = 5 });

                Assert.AreEqual(targetVersionBefore, world.GetChangeVersion<Position>(source));
                Assert.AreEqual(targetVersionBefore, world.GetChangeVersion<Position>(target));
            }
        }
    }
}
