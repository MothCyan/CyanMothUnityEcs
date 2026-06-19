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
        public void SpriteRendererBridge_RegisterAndGet_Works()
        {
            GameObject gameObject = new GameObject("sprite-bridge-test");
            try
            {
                SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
                using (SpriteRendererBridge bridge = new SpriteRendererBridge())
                {
                    int id = bridge.Register(renderer);

                    Assert.AreEqual(1, bridge.Count);
                    Assert.IsTrue(bridge.TryGet(id, out SpriteRenderer result));
                    Assert.AreSame(renderer, result);
                    Assert.AreSame(renderer, bridge.Get(id));
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SpriteRendererSyncSystem_UpdatesRendererState()
        {
            GameObject gameObject = new GameObject("sprite-sync-test");
            try
            {
                SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
                using (World world = new World())
                using (SpriteRendererBridge bridge = new SpriteRendererBridge())
                using (SystemPipeline pipeline = new SystemPipeline(world))
                {
                    int proxyId = bridge.Register(renderer);
                    world.Create(
                        new SpriteRendererProxy(proxyId),
                        new SpriteRenderState(0.2f, 0.4f, 0.6f, 0.8f, visible: false));
                    pipeline.Add(new SpriteRendererSyncSystem(bridge));

                    pipeline.Update(0.016f);

                    Assert.IsFalse(renderer.enabled);
                    Assert.AreEqual(0.2f, renderer.color.r);
                    Assert.AreEqual(0.4f, renderer.color.g);
                    Assert.AreEqual(0.6f, renderer.color.b);
                    Assert.AreEqual(0.8f, renderer.color.a);
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
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

        [Test]
        public void EcsRunner_ConvertsPosition2DAuthoring()
        {
            GameObject runnerObject = new GameObject("runner-authoring-test");
            GameObject authoredObject = new GameObject("position-authoring-test");
            try
            {
                authoredObject.transform.position = new Vector3(5, 6, 0);
                Position2DAuthoring authoring = authoredObject.AddComponent<Position2DAuthoring>();
                EcsRunner runner = runnerObject.AddComponent<EcsRunner>();

                runner.Initialize();

                Assert.AreEqual(1, runner.AuthoredEntityCount);
                Assert.IsTrue(authoring.HasEntity);
                Assert.IsTrue(runner.World.Has<Position2D>(authoring.Entity));
                Assert.AreEqual(5, runner.World.Get<Position2D>(authoring.Entity).X);
                Assert.AreEqual(6, runner.World.Get<Position2D>(authoring.Entity).Y);
                Assert.IsTrue(runner.World.Has<TransformProxy>(authoring.Entity));
            }
            finally
            {
                Object.DestroyImmediate(runnerObject);
                Object.DestroyImmediate(authoredObject);
            }
        }

        [Test]
        public void EcsRunner_ConvertsPosition2DAuthoringWithSpriteRenderer()
        {
            GameObject runnerObject = new GameObject("runner-sprite-authoring-test");
            GameObject authoredObject = new GameObject("sprite-authoring-test");
            try
            {
                SpriteRenderer renderer = authoredObject.AddComponent<SpriteRenderer>();
                renderer.color = new Color(0.1f, 0.2f, 0.3f, 0.4f);
                Position2DAuthoring authoring = authoredObject.AddComponent<Position2DAuthoring>();
                EcsRunner runner = runnerObject.AddComponent<EcsRunner>();

                runner.Initialize();

                Assert.IsTrue(authoring.HasEntity);
                Assert.IsTrue(runner.World.Has<SpriteRendererProxy>(authoring.Entity));
                Assert.IsTrue(runner.World.Has<SpriteRenderState>(authoring.Entity));
                Assert.AreEqual(1, runner.SpriteRendererBridge.Count);
                Assert.AreEqual(0.1f, runner.World.Get<SpriteRenderState>(authoring.Entity).R);
                Assert.AreEqual(0.4f, runner.World.Get<SpriteRenderState>(authoring.Entity).A);
            }
            finally
            {
                Object.DestroyImmediate(runnerObject);
                Object.DestroyImmediate(authoredObject);
            }
        }

        [Test]
        public void Position2DAuthoring_CanSkipTransformSync()
        {
            GameObject gameObject = new GameObject("position-authoring-no-sync");
            try
            {
                Position2DAuthoring authoring = gameObject.AddComponent<Position2DAuthoring>();
                authoring.InitialPosition = new Vector2(8, 9);
                authoring.SyncTransform = false;

                using (World world = new World())
                using (TransformBridge bridge = new TransformBridge())
                {
                    Entity entity = authoring.CreateEntity(world, bridge);

                    Assert.IsTrue(world.Has<Position2D>(entity));
                    Assert.IsFalse(world.Has<TransformProxy>(entity));
                    Assert.AreEqual(8, world.Get<Position2D>(entity).X);
                    Assert.AreEqual(9, world.Get<Position2D>(entity).Y);
                    Assert.AreEqual(0, bridge.Count);
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
