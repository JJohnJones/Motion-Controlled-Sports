using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MotionControllers.Bowling;

namespace MotionControllers.Editor
{
    public static class MakeControllersPersistent
    {
        [MenuItem("Tools/Motion Controllers/Make Selected Controller System Persistent")]
        public static void MakeSelected()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode before migrating the scene."); return; }
            var selected = Selection.activeGameObject;
            if (selected == null || selected.GetComponent<ControllerManager>() == null)
            { Debug.LogWarning("Select the object containing ControllerManager."); return; }
            Configure(selected);
        }

        public static bool Configure(GameObject system)
        {
            if (system.transform.parent != null || system.transform.childCount != 0)
            { Debug.LogWarning("Controller System must be a top-level object without children. Move game/visual objects outside it first."); return false; }
            foreach (var component in system.GetComponents<Component>())
                if (!(component is Transform || component is ControllerManager || component is ControllerReceiver ||
                    component is WebRtcLanControllerTransport || component is PersistentControllerRoot ||
                    component is ControllerDebugPanel || component is BowlingThrowController))
                { Debug.LogWarning("Move non-controller component " + component?.GetType().Name + " to a scene-local object before migration."); return false; }

            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Separate persistent controller systems");
            var oldGame = system.GetComponent<BowlingThrowController>();
            var oldPanel = system.GetComponent<ControllerDebugPanel>();
            BowlingThrowController game = null;
            if (oldGame != null)
            {
                var local = new GameObject("Bowling Gameplay"); Undo.RegisterCreatedObjectUndo(local, "Create scene gameplay");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(local, system.scene);
                game = Undo.AddComponent<BowlingThrowController>(local);
                EditorUtility.CopySerialized(oldGame, game);
                if (game.inputSource is ControllerManager) game.inputSource = null; // Resolve persistent input; preserve alternate adapters.
            }
            if (oldPanel != null)
            {
                var local = new GameObject("Controller Debug UI"); Undo.RegisterCreatedObjectUndo(local, "Create scene UI");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(local, system.scene);
                var panel = Undo.AddComponent<ControllerDebugPanel>(local);
                EditorUtility.CopySerialized(oldPanel, panel);
                panel.controllerSystem = system.GetComponent<ControllerManager>();
                if (oldGame != null && panel.gameDebugSource == oldGame) panel.gameDebugSource = game;
                RemapReferences(system.scene, oldPanel, panel);
                Undo.DestroyObjectImmediate(oldPanel);
            }
            if (oldGame != null) { RemapReferences(system.scene, oldGame, game); Undo.DestroyObjectImmediate(oldGame); }
            if (system.GetComponent<PersistentControllerRoot>() == null) Undo.AddComponent<PersistentControllerRoot>(system);
            EditorSceneManager.MarkSceneDirty(system.scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("Controller-only root is persistent. Gameplay and debug UI remain scene-local. Save the scene; configure only the startup root's network settings.");
            return true;
        }
        private static void RemapReferences(UnityEngine.SceneManagement.Scene scene, Object previous, Object replacement)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == previous)
                            property.objectReferenceValue = replacement;
                    serialized.ApplyModifiedProperties();
                }
        }
    }
}
