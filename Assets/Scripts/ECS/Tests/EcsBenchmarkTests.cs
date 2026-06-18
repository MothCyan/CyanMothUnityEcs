using NUnit.Framework;

namespace CyanMothUnityEcs.Tests
{
    public sealed class EcsBenchmarkTests
    {
        [Test]
        public void CreatePositionVelocity_ReturnsStats()
        {
            EcsBenchmarkResult result = EcsBenchmark.CreatePositionVelocity(16);

            Assert.AreEqual("Create Position+Velocity", result.Name);
            Assert.AreEqual(16, result.Iterations);
            Assert.AreEqual(16, result.Stats.AliveEntityCount);
            Assert.GreaterOrEqual(result.ElapsedTicks, 0);
        }

        [Test]
        public void QueryPositionVelocity_ReturnsStats()
        {
            EcsBenchmarkResult result = EcsBenchmark.QueryPositionVelocity(16);

            Assert.AreEqual("Query Position+Velocity", result.Name);
            Assert.AreEqual(16, result.Iterations);
            Assert.AreEqual(16, result.Stats.AliveEntityCount);
        }

        [Test]
        public void AddRemoveHealth_ReturnsStats()
        {
            EcsBenchmarkResult result = EcsBenchmark.AddRemoveHealth(16);

            Assert.AreEqual("Add/Remove Health", result.Name);
            Assert.AreEqual(16, result.Iterations);
            Assert.AreEqual(16, result.Stats.AliveEntityCount);
        }
    }
}
