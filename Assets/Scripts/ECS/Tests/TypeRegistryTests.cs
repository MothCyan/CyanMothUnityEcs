using NUnit.Framework;

namespace CyanMothUnityEcs.Tests
{
    public sealed class TypeRegistryTests
    {
        private struct Position : IComponentData
        {
            public float X, Y, Z;
        }

        private struct Velocity : IComponentData
        {
            public float X, Y, Z;
        }

        [SetUp]
        public void SetUp()
        {
            TypeRegistry.ClearForTests();
        }

        [Test]
        public void Register_ReturnsStableIndexForSameType()
        {
            Position sample = new Position { X = 1f, Y = 2f, Z = 3f };
            ComponentType first = TypeRegistry.Register<Position>();
            ComponentType second = TypeRegistry.Register<Position>();

            Assert.AreEqual(6f, sample.X + sample.Y + sample.Z);
            Assert.AreEqual(first.Index, second.Index);
            Assert.AreEqual(1, TypeRegistry.Count);
        }

        [Test]
        public void Register_AssignsDifferentIndicesForDifferentTypes()
        {
            Velocity sample = new Velocity { X = 1f, Y = 2f, Z = 3f };
            ComponentType position = TypeRegistry.Register<Position>();
            ComponentType velocity = TypeRegistry.Register<Velocity>();

            Assert.AreEqual(6f, sample.X + sample.Y + sample.Z);
            Assert.AreNotEqual(position.Index, velocity.Index);
            Assert.IsTrue(position.Mask.Contains(position.Index));
            Assert.IsTrue(velocity.Mask.Contains(velocity.Index));
        }

        [Test]
        public void GetByIndex_ReturnsRegisteredType()
        {
            ComponentType position = TypeRegistry.Register<Position>();

            Assert.AreEqual(position, TypeRegistry.GetByIndex(position.Index));
        }
    }
}
