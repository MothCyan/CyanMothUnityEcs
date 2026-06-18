using NUnit.Framework;

#pragma warning disable CS0649

namespace CyanMothUnityEcs.Tests
{
    public sealed class CommandBufferTests
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

        private struct TestTag : IComponentData
        {
        }

        [SetUp]
        public void SetUp()
        {
            TypeRegistry.ClearForTests();
        }

        [Test]
        public void Add_PlaybackAddsComponent()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Commands.Add(entity, new Velocity { X = 2 });

                Assert.IsFalse(world.Has<Velocity>(entity));
                Assert.Greater(world.Commands.PayloadBytes, 0);

                world.Playback();

                Assert.IsTrue(world.Has<Velocity>(entity));
                Assert.AreEqual(2, world.Get<Velocity>(entity).X);
                Assert.AreEqual(0, world.Commands.Count);
                Assert.AreEqual(0, world.Commands.PayloadBytes);
            }
        }

        [Test]
        public void Remove_PlaybackRemovesComponent()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 }, new Velocity { X = 2 });

                world.Commands.Remove<Velocity>(entity);

                Assert.IsTrue(world.Has<Velocity>(entity));

                world.Playback();

                Assert.IsFalse(world.Has<Velocity>(entity));
                Assert.AreEqual(1, world.Get<Position>(entity).X);
            }
        }

        [Test]
        public void Destroy_PlaybackDestroysEntity()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Commands.Destroy(entity);

                Assert.IsTrue(world.IsAlive(entity));

                world.Playback();

                Assert.IsFalse(world.IsAlive(entity));
            }
        }

        [Test]
        public void PlaybackOrder_IsDeterministic()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Health { Value = 1 });

                world.Commands.Add(entity, new Health { Value = 2 });
                world.Commands.Add(entity, new Health { Value = 3 });

                world.Playback();

                Assert.AreEqual(3, world.Get<Health>(entity).Value);
            }
        }

        [Test]
        public void Clear_DropsRecordedCommands()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Commands.Add(entity, new Velocity { X = 2 });
                world.Commands.Clear();
                world.Playback();

                Assert.IsFalse(world.Has<Velocity>(entity));
                Assert.AreEqual(0, world.Commands.PayloadBytes);
            }
        }

        [Test]
        public void AddTag_DoesNotWritePayloadBytes()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Commands.Add(entity, new TestTag());

                Assert.AreEqual(1, world.Commands.Count);
                Assert.AreEqual(0, world.Commands.PayloadBytes);

                world.Playback();

                Assert.IsTrue(world.Has<TestTag>(entity));
            }
        }

        [Test]
        public void AddSameComponent_KeepsOnlyLastCommand()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Health { Value = 1 });

                world.Commands.Add(entity, new Health { Value = 2 });
                int firstPayloadBytes = world.Commands.PayloadBytes;
                world.Commands.Add(entity, new Health { Value = 3 });

                Assert.AreEqual(1, world.Commands.Count);
                Assert.AreEqual(firstPayloadBytes, world.Commands.PayloadBytes);

                world.Playback();

                Assert.AreEqual(3, world.Get<Health>(entity).Value);
            }
        }

        [Test]
        public void RemoveSameComponent_KeepsSingleCommand()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 }, new Velocity { X = 2 });

                world.Commands.Remove<Velocity>(entity);
                world.Commands.Remove<Velocity>(entity);

                Assert.AreEqual(1, world.Commands.Count);

                world.Playback();

                Assert.IsFalse(world.Has<Velocity>(entity));
            }
        }

        [Test]
        public void AddThenRemoveSameComponent_CancelsPendingAdd()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Commands.Add(entity, new Velocity { X = 2 });
                world.Commands.Remove<Velocity>(entity);

                Assert.AreEqual(1, world.Commands.Count);

                world.Playback();

                Assert.IsFalse(world.Has<Velocity>(entity));
            }
        }

        [Test]
        public void Destroy_RemovesEarlierCommandsForSameEntity()
        {
            using (World world = new World())
            {
                Entity entity = world.Create(new Position { X = 1 });

                world.Commands.Add(entity, new Velocity { X = 2 });
                world.Commands.Add(entity, new Health { Value = 3 });
                world.Commands.Destroy(entity);

                Assert.AreEqual(1, world.Commands.Count);

                world.Playback();

                Assert.IsFalse(world.IsAlive(entity));
            }
        }
    }
}
