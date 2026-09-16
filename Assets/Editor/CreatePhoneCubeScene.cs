using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotionControllers.Editor
{
    public static class CreatePhoneCubeScene
    {
        [MenuItem("Tools/Motion Controllers/Create Phone Cube Prototype Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Controller System");
            var manager = root.AddComponent<ControllerManager>();
            root.AddComponent<WebRtcLanControllerTransport>();
            var panel = root.AddComponent<ControllerDebugPanel>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Phone Cube";
            var view = cube.AddComponent<PhoneOrientationVisualizer>();
            view.inputSource = manager;
            panel.visualizer = view;
            MakeControllersPersistent.Configure(root);
            // Raised screen/top markers distinguish faces without creating material assets.
            var front = GameObject.CreatePrimitive(PrimitiveType.Cube);
            front.name = "Screen face (-Z)";
            front.transform.SetParent(cube.transform, false);
            front.transform.localPosition = new Vector3(0, 0, -0.53f);
            front.transform.localScale = new Vector3(0.7f, 0.75f, 0.08f);
            var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            top.name = "Phone top (+Y)";
            top.transform.SetParent(cube.transform, false);
            top.transform.localPosition = new Vector3(0, 0.58f, 0);
            top.transform.localScale = Vector3.one * 0.15f;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            // Leave the left portion of the Game view available for engineering diagnostics.
            camera.transform.position = new Vector3(0, 1.3f, -5);
            camera.transform.LookAt(Vector3.zero);
            camera.rect = new Rect(0.42f, 0, 0.58f, 1);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.085f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(35, -25, 0);
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/PhoneCubePrototype.unity"));
            Selection.activeGameObject = root;
        }
    }
}
