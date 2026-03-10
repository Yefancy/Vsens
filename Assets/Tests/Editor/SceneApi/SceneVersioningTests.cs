using NUnit.Framework;
using UnityEngine;
using VsensAgent.SceneApi.V2;

namespace VsensAgent.Tests.Editor.SceneApi
{
    public class SceneVersioningTests
    {
        [Test]
        public void RegisterMutation_OutsideBatch_IncrementsVersionImmediately()
        {
            var go = new GameObject("SceneRegistryTest");
            try
            {
                var registry = go.AddComponent<SceneRegistry>();
                int initialVersion = registry.CurrentVersion;

                registry.RegisterMutation("manual", "obj_1", "set_transform");

                Assert.AreEqual(initialVersion + 1, registry.CurrentVersion);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RegisterMutation_InsideBatch_OnlyCommitsOnce()
        {
            var go = new GameObject("SceneRegistryBatchTest");
            try
            {
                var registry = go.AddComponent<SceneRegistry>();
                int initialVersion = registry.CurrentVersion;

                registry.BeginMutationBatch("legacy_control");
                registry.RegisterMutation("legacy_control", "obj_1", "set_transform");
                registry.RegisterMutation("legacy_control", "obj_2", "set_state");

                Assert.AreEqual(initialVersion, registry.CurrentVersion);

                registry.CommitMutationBatch();

                Assert.AreEqual(initialVersion + 1, registry.CurrentVersion);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RollbackMutationBatch_DiscardsPendingIncrement()
        {
            var go = new GameObject("SceneRegistryRollbackTest");
            try
            {
                var registry = go.AddComponent<SceneRegistry>();
                int initialVersion = registry.CurrentVersion;

                registry.BeginMutationBatch("scene.execute_actions");
                registry.RegisterMutation("scene.execute_actions", "obj_1", "set_sensor");
                registry.RollbackMutationBatch();

                Assert.AreEqual(initialVersion, registry.CurrentVersion);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
