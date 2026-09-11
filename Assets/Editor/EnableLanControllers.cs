using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionControllers.Editor
{
    public static class EnableLanControllers
    {
        [MenuItem("Tools/Motion Controllers/Enable LAN WebRTC On Selected Controller System")]
        public static void Enable()
        {
            var selected = Selection.activeGameObject;
            var manager = selected != null ? selected.GetComponent<ControllerManager>() : null;
            if (manager == null) { Debug.LogWarning("Select the scene object containing ControllerManager first."); return; }
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode before configuring the scene."); return; }
            if (!MakeControllersPersistent.Configure(selected)) return;
            var lan = selected.GetComponent<WebRtcLanControllerTransport>();
            if (lan == null) lan = Undo.AddComponent<WebRtcLanControllerTransport>(selected);
            Undo.RecordObject(lan, "Enable LAN controllers"); lan.enabled = true;
            var old = selected.GetComponent<ControllerReceiver>();
            if (old != null) { Undo.RecordObject(old, "Disable fallback listener"); old.enabled = false; }
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Selection.activeObject = lan;
            Debug.Log("LAN transport added. Set Signaling URL and PWA URL in the Inspector, then save the scene. Legacy receiver is preserved but disabled.");
        }
    }
}
