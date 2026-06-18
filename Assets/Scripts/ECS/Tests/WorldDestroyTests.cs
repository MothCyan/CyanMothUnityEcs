using System;
using NUnit.Framework;

#pragma warning disable CS0649

namespace CyanMothUnityEcs.Tests
{
    public sealed class WorldDestroyTests
    {
        private struct Position : IComponentData
        {
            public float X;
            public float Y;
            public float Z;
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
        public void Destroy_RemovesEntityAndInvalidatesVersion()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Destroy(entity);

                Assert.IsFalse(world.IsAlive(entity));
                Assert.Throws<InvalidOperationException>(() => world.Has<Position>(entity));
            }
        }

        [Test]
        public void Destroy_SwapRemoveUpdatesMovedEntityLocation()
        {
            using (World world = new World())
            {
                Entity first = world.Create(new Position { X = 1 }, new Health { Value = 10 });
                Entity second = world.Create(new Position { X = 2 }, new Health { Value = 20 });
                Entity third = world.Create(new Position { X = 3 }, new Health { Value = 30 });

                world.Destroy(first);

                Assert.IsFalse(world.IsAlive(first));
                Assert.IsTrue(world.IsAlive(third));
                Assert.AreEqual(3, world.Get<Position>(third).X);
                Assert.AreEqual(30, world.Get<Health>(third).Value);
                Assert.AreEqual(2, world.Get<Position>(second).X);
                Assert.AreEqual(20, world.Get<Health>(second).Value);
            }
        }

        [Test]
        public void Destroy_EmptyChunkCanBeReusedByLaterCreate()
        {
            using (World world = new World())
            {
                Entity first = world.Create(new Position { X = 1 });
                world.Destroy(first);

                Entity second = world.Create(new Position { X = 2 });

                Assert.IsTrue(world.IsAlive(second));
                Assert.AreEqual(2, world.Get<Position>(second).X);
            }
        }
    }
}
