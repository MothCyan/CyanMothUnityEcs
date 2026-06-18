using NUnit.Framework;

namespace CyanMothUnityEcs.Tests
{
    public sealed class QuerySystemTests
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
        public void QuerySystem_OneComponent_CachesQueryAndUpdates()
        {
            using (World world = new World())
            using (SystemPipeline pipeline = new SystemPipeline(world))
            {
                Entity entity = world.Create(new Health { Value = 1 });

                pipeline.Add(new HealthIncrementSystem());
                pipeline.Update(1);

                Assert.AreEqual(2, world.Get<Health>(entity).Value);
            }
        }

        [Test]
        public void QuerySystem_TwoComponents_CachesQueryAndUpdates()
        {
            using (World world = new World())
            using (SystemPipeline pipeline = new SystemPipeline(world))
            {
                Entity entity = world.Create(new Position { X = 1 }, new Velocity { X = 2 });

                pipeline.Add(new MovementSystem());
                pipeline.Update(0.5f);

                Assert.AreEqual(2, world.Get<Position>(entity).X);
            }
        }

        [Test]
        public void QuerySystem_ThreeComponents_CachesQueryAndUpdates()
        {
            using (World world = new World())
            using (SystemPipeline pipeline = new SystemPipeline(world))
            {
                Entity entity = world.Create(
                    new Position { X = 1 },
                    new Velocity { X = 2 },
                    new Health { Value = 3 });

                pipeline.Add(new ThreeComponentSystem());
                pipeline.Update(1);

                Assert.AreEqual(6, world.Get<Position>(entity).X);
            }
        }

        private sealed class HealthIncrementSystem : QuerySystem<Health>
        {
            protected override void OnUpdate(float deltaTime, Query<Health> query)
            {
                query.ForEach((Entity entity, ref Health health) =>
                {
                    health.Value++;
                });
            }
        }

        private sealed class MovementSystem : QuerySystem<Position, Velocity>
        {
            protected override void OnUpdate(float deltaTime, Query<Position, Velocity> query)
            {
                query.ForEach((Entity entity, ref Position position, ref Velocity velocity) =>
                {
                    position.X += velocity.X * deltaTime;
                });
            }
        }

        private sealed class ThreeComponentSystem : QuerySystem<Position, Velocity, Health>
        {
            protected override void OnUpdate(float deltaTime, Query<Position, Velocity, Health> query)
            {
                query.ForEach((Entity entity, ref Position position, ref Velocity velocity, ref Health health) =>
                {
                    position.X += velocity.X + health.Value;
                });
            }
        }
    }
}
