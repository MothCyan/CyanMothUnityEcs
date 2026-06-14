using System;
using NUnit.Framework;

namespace CyanMothUnityEcs.Tests
{
    public sealed class EntityStoreTests
    {
        [Test]
        public void Create_ReturnsEntityHandle()
        {
            EntityStore store = new EntityStore();
            Entity entity = store.Create();

            Assert.AreNotEqual(Entity.Null, entity);
            Assert.AreEqual(1, entity.Id);
            Assert.AreEqual(0, entity.Version);
        }

        [Test]
        public void SetLocation_MakesEntityAlive()
        {
            EntityStore store = new EntityStore();
            Entity entity = store.Create();
            IntPtr chunk = new IntPtr(1234);

            store.SetLocation(entity, chunk, 7, 2);

            Assert.IsTrue(store.IsAlive(entity));
            Assert.AreEqual(chunk, store.GetChunk(entity));
            Assert.AreEqual(7, store.GetIndex(entity));
            Assert.AreEqual(2, store.GetArchetypeId(entity));
        }

        [Test]
        public void Release_InvalidatesOldVersionAndReusesId()
        {
            EntityStore store = new EntityStore();
            Entity entity = store.Create();
            store.SetLocation(entity, new IntPtr(42), 0, 1);

            store.Release(entity);
            Entity reused = store.Create();

            Assert.AreEqual(entity.Id, reused.Id);
            Assert.AreEqual(entity.Version + 1, reused.Version);
            Assert.IsFalse(store.IsAlive(entity));
        }

        [Test]
        public void Validate_ThrowsForStaleHandle()
        {
            EntityStore store = new EntityStore();
            Entity entity = store.Create();
            store.SetLocation(entity, new IntPtr(42), 0, 1);
            store.Release(entity);

            Assert.Throws<InvalidOperationException>(() => store.Validate(entity));
        }
    }
}
