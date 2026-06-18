using System.Collections.Generic;
using NUnit.Framework;

#pragma warning disable CS0649

namespace CyanMothUnityEcs.Tests
{
    public sealed class SystemPipelineTests
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
            RecordingSystem.Events.Clear();
        }

        [Test]
        public void Add_CallsOnCreate()
        {
            using (World world = new World())
            using (SystemPipeline pipeline = new SystemPipeline(world))
            {
                RecordingSystem system = pipeline.Add(new RecordingSystem("A"));

                Assert.IsTrue(system.Created);
                Assert.AreSame(world, system.World);
                CollectionAssert.AreEqual(new[] { "A.Create" }, RecordingSystem.Events);
            }
        }

        [Test]
        public void UpdateOrder_IsAddOrder()
        {
            using (World world = new World())
            using (SystemPipeline pipeline = new SystemPipeline(world))
            {
                pipeline.Add(new RecordingSystem("A"));
                pipeline.Add(new RecordingSystem("B"));

                RecordingSystem.Events.Clear();
                pipeline.Update(0.25f);

                CollectionAssert.AreEqual(new[] { "A.Update:0.25", "B.Update:0.25" }, RecordingSystem.Events);
            }
        }

        [Test]
        public void Update_PlaybackAfterEachSystem()
        {
            using (World world = new World())
            using (SystemPipeline pipeline = new SystemPipeline(world))
            {
                Entity entity = world.Create(new Position { X = 1 });

                pipeline.Add(new AddVelocitySystem(entity));
                pipeline.Add(new AssertVelocitySystem(entity));

                pipeline.Update(1);

                Assert.IsTrue(world.Has<Velocity>(entity));
                Assert.AreEqual(2, world.Get<Velocity>(entity).X);
            }
        }

        [Test]
        public void Dispose_CallsOnDestroyReverseOrder()
        {
            using (World world = new World())
            {
                SystemPipeline pipeline = new SystemPipeline(world);
                RecordingSystem a = pipeline.Add(new RecordingSystem("A"));
                RecordingSystem b = pipeline.Add(new RecordingSystem("B"));

                RecordingSystem.Events.Clear();
                pipeline.Dispose();

                CollectionAssert.AreEqual(new[] { "B.Destroy", "A.Destroy" }, RecordingSystem.Events);
                Assert.IsFalse(a.IsAttached);
                Assert.IsFalse(b.IsAttached);
            }
        }

        private sealed class RecordingSystem : EcsSystem
        {
            public static readonly List<string> Events = new List<string>();
            private readonly string _name;

            public RecordingSystem(string name)
            {
                _name = name;
            }

            public bool Created { get; private set; }

            protected override void OnCreate()
            {
                Created = true;
                Events.Add($"{_name}.Create");
            }

            protected override void OnUpdate(float deltaTime)
            {
                Events.Add($"{_name}.Update:{deltaTime:0.##}");
            }

            protected override void OnDestroy()
            {
                Events.Add($"{_name}.Destroy");
            }
        }

        private sealed class AddVelocitySystem : EcsSystem
        {
            private readonly Entity _entity;

            public AddVelocitySystem(Entity entity)
            {
                _entity = entity;
            }

            protected override void OnUpdate(float deltaTime)
            {
                World.Commands.Add(_entity, new Velocity { X = 2 });
            }
        }

        private sealed class AssertVelocitySystem : EcsSystem
        {
            private readonly Entity _entity;

            public AssertVelocitySystem(Entity entity)
            {
                _entity = entity;
            }

            protected override void OnUpdate(float deltaTime)
            {
                Assert.IsTrue(World.Has<Velocity>(_entity));
                Assert.AreEqual(2, World.Get<Velocity>(_entity).X);
            }
        }
    }
}
