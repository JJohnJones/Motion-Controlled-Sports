using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MotionControllers.Tests
{
    public class PersistentControllerTests
    {
        [UnityTest] public IEnumerator SceneReplacementPreservesInputAndRejectsDuplicateRoot()
        {
            yield return new EnterPlayMode();
            var first = SceneManager.CreateScene("Persistence test first");
            var root = new GameObject("Persistent controller test"); root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, first);
            var manager = root.AddComponent<ControllerManager>();
            root.AddComponent<PersistentControllerRoot>(); root.SetActive(true);
            try
            {
                manager.Register("phone");
                manager.Submit(new MotionFrame { ControllerId = "phone", Sequence = 1, TimestampMs = 1,
                    Orientation = Quaternion.identity, ReceivedAtSeconds = Time.realtimeSinceStartupAsDouble }, true);
                var session = manager.Sessions["phone"];
                var second = SceneManager.CreateScene("Persistence test second");
                SceneManager.SetActiveScene(second);
                var duplicate = new GameObject("Duplicate controller test"); duplicate.SetActive(false);
                var duplicateManager = duplicate.AddComponent<ControllerManager>();
                // An invalid default signaling URL must never execute OnEnable on this duplicate.
                duplicate.AddComponent<WebRtcLanControllerTransport>();
                duplicate.AddComponent<PersistentControllerRoot>(); duplicate.SetActive(true);
                Assert.That(duplicate.activeSelf, Is.False);
                Assert.That(ControllerInput.Resolve(duplicateManager), Is.SameAs(manager));
                yield return SceneManager.UnloadSceneAsync(first);
                Assert.That(PersistentControllerRoot.Instance.Manager, Is.SameAs(manager));
                Assert.That(manager.Sessions["phone"], Is.SameAs(session));
                Assert.That(session.IsCalibrated, Is.True);
                var cube = new GameObject("Scene-local input consumer");
                var view = cube.AddComponent<PhoneOrientationVisualizer>(); view.useSmoothing = false;
                yield return null; // Start resolves the persistent input without a serialized scene reference.
                manager.Submit(new MotionFrame { ControllerId = "phone", Sequence = 2, TimestampMs = 2,
                    Orientation = Quaternion.AngleAxis(30, Vector3.up), ReceivedAtSeconds = Time.realtimeSinceStartupAsDouble });
                yield return null;
                Assert.That(Quaternion.Angle(cube.transform.localRotation, Quaternion.AngleAxis(30, Vector3.up)), Is.LessThan(.01));
                yield return SceneManager.UnloadSceneAsync(second);
                Assert.That(cube == null, Is.True);
                Assert.That(manager.Sessions["phone"].HistoryCount, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(root); }
            yield return new ExitPlayMode();
        }
    }
}
