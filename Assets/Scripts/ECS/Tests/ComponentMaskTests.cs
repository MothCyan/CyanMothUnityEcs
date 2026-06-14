using NUnit.Framework;

namespace CyanMothUnityEcs.Tests
{
    public sealed class ComponentMaskTests
    {
        [Test]
        public void FromIndex_SetsLowAndHighBits()
        {
            ComponentMask low = ComponentMask.FromIndex(3);
            ComponentMask high = ComponentMask.FromIndex(70);

            Assert.AreEqual(1UL << 3, low.Lo);
            Assert.AreEqual(0UL, low.Hi);
            Assert.AreEqual(0UL, high.Lo);
            Assert.AreEqual(1UL << 6, high.Hi);
        }

        [Test]
        public void ContainsAll_AndIntersects_WorkAcrossBothWords()
        {
            ComponentMask mask = ComponentMask.Empty
                .Add(1)
                .Add(65)
                .Add(90);

            ComponentMask required = ComponentMask.Empty
                .Add(1)
                .Add(90);

            ComponentMask missing = ComponentMask.FromIndex(2);

            Assert.IsTrue(mask.ContainsAll(required));
            Assert.IsFalse(mask.ContainsAll(required.Add(2)));
            Assert.IsTrue(mask.Intersects(ComponentMask.FromIndex(65)));
            Assert.IsFalse(mask.Intersects(missing));
        }

        [Test]
        public void Remove_ClearsOnlyRequestedBit()
        {
            ComponentMask mask = ComponentMask.Empty
                .Add(4)
                .Add(68)
                .Remove(4);

            Assert.IsFalse(mask.Contains(4));
            Assert.IsTrue(mask.Contains(68));
        }
    }
}
