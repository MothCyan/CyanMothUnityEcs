using NUnit.Framework;
using UnityEngine;

namespace CyanMothUnityEcs.Tests
{
    public sealed class UnityBridgeTests
    {
        [SetUp]
        public void SetUp()
        {
            TypeRegistry.ClearForTests();
        }

        [Test]
        public void TransformBridge_RegisterAndGet_Works()
        {
            GameObject gameObject = new GameObject("bridge-test");
            try
            {
                using (TransformBridge bridge = new TransformBridge())
                {
                    int id = bridge.Register(gameObject.transform);

                    Assert.AreEqual(1, bridge.Count);
                    Assert.IsTrue(bridge.TryGet(id, out Transform transform));
                    Assert.AreSame(gameObject.transform, transform);
                    Assert.AreSame(gameObject.transform, bridge.Get(id));
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TransformBridge_Unregister_ReusesSlot()
        {
            GameObject first = new GameObject("bridge-first");
            GameObject second = new GameObject("bridge-second");
            try
            {
                using (TransformBridge bridge = new TransformBridge())
                {
                    int firstId = bridge.Register(first.transform);
                    bridge.Unregister(firstId);
                    int secondId = bridge.Register(second.transform);

                    Assert.AreEqual(firstId, secondId);
                    Assert.AreEqual(1, bridge.Count);
                    Assert.AreSame(second.transform, bridge.Get(secondId));
                }
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void TransformSyncSystem_UpdatesTransform()
        {
            GameObject gameObject = new GameObject("sync-test");
            try
            {
                using (World world = new World())
                using (TransformBridge bridge = new TransformBridge())
                using (SystemPipeline pipeline = new SystemPipeline(world))
                {
                    int proxyId = bridge.Register(gameObject.transform);
                    world.Create(new Position2D(3, 4), new TransformProxy(proxyId));
                    pipeline.Add(new TransformSyncSystem(bridge));

                    pipeline.Update(0.016f);

                    Assert.AreEqual(3, gameObject.transform.position.x);
                    Assert.AreEqual(4, gameObject.transform.position.y);
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void EcsRunner_InitializeAndShutdown_ManagesWorld()
        {
            GameObject gameObject = new GameObject("runner-test");
            try
            {
                EcsRunner runner = gameObject.AddComponent<EcsRunner>();

                runner.Initialize();

                Assert.IsTrue(runner.IsRunning);
                Assert.IsNotNull(runner.World);
                Assert.IsNotNull(runner.Pipeline);
                Assert.IsNotNull(runner.TransformBridge);

                runner.Shutdown();

                Assert.IsFalse(runner.IsRunning);
                Assert.IsNull(runner.World);
                Assert.IsNull(runner.Pipeline);
                Assert.IsNull(runner.TransformBridge);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
