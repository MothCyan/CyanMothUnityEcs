using NUnit.Framework;

#pragma warning disable CS0649

namespace CyanMothUnityEcs.Tests
{
    public sealed class ArchetypeStoreTests
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
        public void GetOrCreate_SameTypesDifferentOrder_ReturnsSameArchetype()
        {
            ComponentType position = TypeRegistry.Get<Position>();
            ComponentType velocity = TypeRegistry.Get<Velocity>();
            ArchetypeStore store = new ArchetypeStore();

            Archetype first = store.GetOrCreate(position, velocity);
            Archetype second = store.GetOrCreate(velocity, position);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, store.Count);
            Assert.AreEqual(1, store.Version);
            Assert.AreEqual(position.Index, first.Types[0].Index);
            Assert.AreEqual(velocity.Index, first.Types[1].Index);
        }

        [Test]
        public void ArchetypeLayout_CapacityFitsChunk()
        {
            ComponentType position = TypeRegistry.Get<Position>();
            ComponentType velocity = TypeRegistry.Get<Velocity>();
            Archetype archetype = new ArchetypeStore().GetOrCreate(position, velocity);

            Assert.Greater(archetype.Layout.Capacity, 0);
            Assert.LessOrEqual(archetype.Layout.UsedBytes, Chunk.Size);
            Assert.AreEqual(0, archetype.Layout.EntityOffset % 8);
        }

        [Test]
        public void ArchetypeLayout_ComponentOffsetsAreAligned()
        {
            ComponentType position = TypeRegistry.Get<Position>();
            ComponentType health = TypeRegistry.Get<Health>();
            Archetype archetype = new ArchetypeStore().GetOrCreate(position, health);

            for (int i = 0; i < archetype.Types.Length; i++)
            {
                ComponentType type = archetype.Types[i];
                int offset = archetype.Layout.GetComponentOffset(i);

                Assert.AreNotEqual(ArchetypeLayout.MissingOffset, offset);
                Assert.AreEqual(0, offset % type.Align);
            }
        }

        [Test]
        public void TagComponent_TakesNoDataSpace()
        {
            ComponentType position = TypeRegistry.Get<Position>();
            ComponentType tag = TypeRegistry.Get<TestTag>();
            ArchetypeStore store = new ArchetypeStore();

            Archetype withoutTag = store.GetOrCreate(position);
            Archetype withTag = store.GetOrCreate(position, tag);
            int tagSlot = withTag.GetTypeSlot(tag.Index);

            Assert.IsTrue(tag.IsTag);
            Assert.AreEqual(ArchetypeLayout.MissingOffset, withTag.Layout.GetComponentOffset(tagSlot));
            Assert.AreEqual(0, withTag.Layout.GetComponentStride(tagSlot));
            Assert.AreEqual(withoutTag.Layout.Capacity, withTag.Layout.Capacity);
        }

        [Test]
        public void TryFind_ReturnsArchetypeByMask()
        {
            ComponentType position = TypeRegistry.Get<Position>();
            ComponentType velocity = TypeRegistry.Get<Velocity>();
            ArchetypeStore store = new ArchetypeStore();
            Archetype created = store.GetOrCreate(position, velocity);

            bool found = store.TryFind(position.Mask.Add(velocity.Mask), out Archetype result);

            Assert.IsTrue(found);
            Assert.AreSame(created, result);
        }

        [Test]
        public void ComponentLookup_ReturnsOffsetAndStrideByTypeIndex()
        {
            ComponentType position = TypeRegistry.Get<Position>();
            ComponentType health = TypeRegistry.Get<Health>();
            Archetype archetype = new ArchetypeStore().GetOrCreate(position, health);

            int positionSlot = archetype.GetTypeSlot(position.Index);
            int healthSlot = archetype.GetTypeSlot(health.Index);

            Assert.AreEqual(archetype.Layout.GetComponentOffset(positionSlot), archetype.GetComponentOffset(position.Index));
            Assert.AreEqual(archetype.Layout.GetComponentStride(positionSlot), archetype.GetComponentStride(position.Index));
            Assert.AreEqual(archetype.Layout.GetComponentOffset(healthSlot), archetype.GetComponentOffset(health.Index));
            Assert.AreEqual(archetype.Layout.GetComponentStride(healthSlot), archetype.GetComponentStride(health.Index));
        }

        [Test]
        public void ComponentLookup_MissingType_Throws()
        {
            ComponentType position = TypeRegistry.Get<Position>();
            ComponentType velocity = TypeRegistry.Get<Velocity>();
            Archetype archetype = new ArchetypeStore().GetOrCreate(position);

            Assert.AreEqual(ArchetypeLayout.MissingOffset, archetype.GetTypeSlot(velocity.Index));
            Assert.Throws<System.InvalidOperationException>(() => archetype.GetComponentOffset(velocity.Index));
            Assert.Throws<System.InvalidOperationException>(() => archetype.GetComponentStride(velocity.Index));
        }
    }
}
