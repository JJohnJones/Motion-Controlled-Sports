using System;
using System.Linq;
using System.IO;
using MotionControllers.Core;
using MotionControllers.UI;
using MotionControllers.Bowling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace MotionControllers.Editor
{
    public static class CreateApplicationShell
    {
        public const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        public const string BowlingPath = "Assets/Scenes/Games/Bowling.unity";
        public const string Generated = "Assets/UI/Generated";
        [MenuItem("Tools/Motion Sports/Create or Open Application Shell")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode before generating the application shell."); return; }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapPath) != null)
            { RepairPanelBinding(); ConfigureBuild(); if (!Application.isBatchMode) EditorSceneManager.OpenScene(BootstrapPath); return; }
            EnsureFolder(Generated); EnsureFolder("Assets/Scenes/Games");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if (SceneManager.sceneCount == 0 || !SceneManager.GetActiveScene().IsValid()) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                const string original = "Assets/Scenes/BowlingPrototype.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(original) == null) throw new InvalidOperationException("Create the Bowling prototype first.");
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BowlingPath) != null) throw new InvalidOperationException("Bowling shell scene already exists; keep it and configure a Bootstrap manually, or move it before generating.");
                if (!AssetDatabase.CopyAsset(original, BowlingPath)) throw new IOException("Could not copy the saved Bowling prototype.");
                var bowlingScene = EditorSceneManager.OpenScene(BowlingPath, OpenSceneMode.Additive);
                var components = bowlingScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var originalManager = components.OfType<ControllerManager>().Single();
                var core = UnityEngine.Object.Instantiate(originalManager.gameObject); core.name = "Controller Core";
                // Keep the strict controller-only root and the existing Inspector configuration.
                if (core.GetComponent<PersistentControllerRoot>() == null) core.AddComponent<PersistentControllerRoot>();
                string corePath = Generated + "/ControllerCore.prefab";
                PrefabUtility.SaveAsPrefabAsset(core, corePath); UnityEngine.Object.DestroyImmediate(core);
                var bowling = components.OfType<BowlingThrowController>().Single(); bowling.inputSource = null;
                foreach (var debug in components.OfType<ControllerDebugPanel>()) UnityEngine.Object.DestroyImmediate(debug);
                foreach (var manager in components.OfType<ControllerManager>()) UnityEngine.Object.DestroyImmediate(manager.gameObject);
                foreach (var camera in bowlingScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>())) {
                    camera.rect = new Rect(0, 0, 1, 1);
                    if (camera.CompareTag("MainCamera")) { camera.transform.position = new Vector3(0, 5, -8); camera.transform.LookAt(new Vector3(0, 0, 4)); }
                }
                var adapter = bowling.gameObject.AddComponent<BowlingGameSession>(); adapter.bowling = bowling;
                EditorSceneManager.SaveScene(bowlingScene); EditorSceneManager.CloseScene(bowlingScene, true);

                var definitions = new[] {
                    Definition("Bowling", "Aim your shot. Feel the swing. Clear the lane.", BowlingPath, true, "bowling", new Color(.39f,.91f,.73f)),
                    Definition("Tennis", "Meet your next rally. A future court awaits.", "", false, "tennis", new Color(.95f,.77f,.40f)),
                    Definition("Golf", "Find your rhythm, one swing at a time.", "", false, "golf", new Color(.49f,.74f,.95f)),
                    Definition("Future Game", "More ways to move. More reasons to play.", "", false, "menu", new Color(.79f,.62f,.96f)) };
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution = new Vector2Int(1280, 720);
                panel.screenMatchMode = PanelScreenMatchMode.Expand;
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/Styles/RuntimeTheme.tss");
                AssetDatabase.CreateAsset(panel, Generated + "/ShellPanel.asset");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                var shell = new GameObject("Application Shell");
                var lobby = shell.AddComponent<ControllerLobbyAdapter>();
                var flow = shell.AddComponent<SceneFlowManager>(); flow.controllerLobby = lobby; flow.games = definitions;
                flow.menuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/MenuMusic.mp3");
                var document = shell.AddComponent<UIDocument>(); document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Generated + "/ShellPanel.asset");
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Layout/AppShell.uxml"); document.sortingOrder = 100;
                var view = shell.AddComponent<AppShellView>(); view.flow = flow;
                view.gameCardTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Layout/GameCard.uxml");
                view.theme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Styles/AppShell.uss");
                var cameraObject = new GameObject("Menu Background Camera"); cameraObject.transform.SetParent(shell.transform);
                var menuCamera = cameraObject.AddComponent<Camera>(); menuCamera.clearFlags = CameraClearFlags.SolidColor;
                menuCamera.backgroundColor = new Color(.047f,.075f,.12f); menuCamera.cullingMask = 0; menuCamera.depth = -10;
                flow.menuCamera = menuCamera;
                var shellPrefab = PrefabUtility.SaveAsPrefabAsset(shell, Generated + "/ApplicationShell.prefab"); UnityEngine.Object.DestroyImmediate(shell);
                PrefabUtility.InstantiatePrefab(shellPrefab, scene);
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(corePath), scene);
                EditorSceneManager.SaveScene(scene, BootstrapPath); EditorSceneManager.CloseScene(scene, true);
                RepairPanelBinding(); ConfigureBuild(); AssetDatabase.SaveAssets();
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive) && setup.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            if (!Application.isBatchMode) EditorSceneManager.OpenScene(BootstrapPath);
            Debug.Log("Application shell ready. Open Assets/Scenes/Bootstrap.unity and press Play. Original BowlingPrototype is unchanged.");
        }
        public static void RepairPanelBinding()
        {
            var prefab = PrefabUtility.LoadPrefabContents(Generated + "/ApplicationShell.prefab");
            try
            {
                var document = prefab.GetComponent<UIDocument>();
                var serialized = new SerializedObject(document);
                serialized.FindProperty("m_PanelSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PanelSettings>(Generated + "/ShellPanel.asset");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, Generated + "/ApplicationShell.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        [MenuItem("Tools/Motion Sports/Register Shell Scenes in Build")]
        public static void ConfigureBuild()
        {
            var previous = EditorBuildSettings.scenes.Where(s => s.path != BootstrapPath && s.path != BowlingPath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapPath, true), new EditorBuildSettingsScene(BowlingPath, true) }.Concat(previous).ToArray();
        }
        private static GameDefinition Definition(string title, string description, string path, bool available, string mode, Color accent)
        {
            var game = ScriptableObject.CreateInstance<GameDefinition>(); game.displayName = title; game.description = description;
            if (mode == "bowling") game.maximumPlayers = 4;
            if (mode == "bowling") game.instructions = "Calibrate facing down the lane. Aim, hold the phone button, swing, then lift to release.";
            game.scenePath = path; game.available = available; game.controllerUiMode = mode; game.accent = accent;
            AssetDatabase.CreateAsset(game, Generated + "/" + title.Replace(" ", "") + ".asset"); return game;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
