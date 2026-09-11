using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using MotionControllers.Bowling;
using MotionControllers.Editor;

namespace MotionControllers.Tests
{
    public class ControllerMigrationTests
    {
        [Test] public void MigrationSeparatesGameplayAndRemapsSceneReferences()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Controller System");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var manager = root.AddComponent<ControllerManager>();
                var game = root.AddComponent<BowlingThrowController>(); game.inputSource = manager; game.maximumAimDegrees = 9;
                var panel = root.AddComponent<ControllerDebugPanel>(); panel.gameDebugSource = game;
                var observer = new GameObject("Another scene UI").AddComponent<ControllerDebugPanel>(); observer.gameDebugSource = game;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(observer.gameObject, scene);
                Assert.That(MakeControllersPersistent.Configure(root), Is.True);
                Assert.That(root.GetComponent<PersistentControllerRoot>(), Is.Not.Null);
                Assert.That(root.GetComponent<BowlingThrowController>(), Is.Null);
                Assert.That(root.GetComponent<ControllerDebugPanel>(), Is.Null);
                var migrated = observer.gameDebugSource as BowlingThrowController;
                Assert.That(migrated, Is.Not.Null);
                Assert.That(migrated.gameObject, Is.Not.SameAs(root));
                Assert.That(migrated.maximumAimDegrees, Is.EqualTo(9));
                Assert.That(migrated.inputSource, Is.Null);
                Assert.That(MakeControllersPersistent.Configure(root), Is.True); // Repeat safely.
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
