using System.IO;
using System.Linq;
using MotionControllers.Core;
using MotionControllers.SwordDuel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MotionControllers.Editor
{
    public static class CreateSwordDuelScene
    {
        public const string ScenePath="Assets/Scenes/Games/SwordDuel.unity";
        private const string Folder="Assets/SwordDuelGenerated";
        [MenuItem("Tools/Motion Sports/Create or Open Sword Duel")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)return;
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets","SwordDuelGenerated");
            if(!File.Exists(ScenePath)){
                var setup=EditorSceneManager.GetSceneManagerSetup();bool restore=setup.Length>0 && setup.All(s=>!string.IsNullOrEmpty(s.path));
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,restore?NewSceneMode.Additive:NewSceneMode.Single);SceneManager.SetActiveScene(scene);
                var floor=Mat("Floor",new Color(.12f,.26f,.33f));var rim=Mat("Aqua rim",new Color(.25f,.75f,.77f));var wall=Mat("Wall",new Color(.13f,.2f,.25f));
                Box("Ground",new Vector3(0,-.3f,0),new Vector3(24,.5f,24),floor);
                Box("Duel platform",new Vector3(0,-.04f,0),new Vector3(8,.08f,8),rim);
                Box("Duel mat",new Vector3(0,.005f,0),new Vector3(7.6f,.01f,7.6f),floor);
                foreach(float x in new[]{-6f,6f})Box("Side boundary",new Vector3(x,.4f,0),new Vector3(.25f,.8f,12),wall);
                foreach(float z in new[]{-6f,6f})Box("End boundary",new Vector3(0,.4f,z),new Vector3(12,.8f,.25f),wall);
                foreach(float x in new[]{-5f,5f})foreach(float z in new[]{-5f,5f})Box("Arena pillar",new Vector3(x,1.8f,z),new Vector3(.4f,3.6f,.4f),rim);
                new GameObject("Spawn A").transform.position=new Vector3(0,0,-1.55f);new GameObject("Spawn B").transform.position=new Vector3(0,0,1.55f);
                var camera=new GameObject("Duel Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(7,3.8f,-3.2f);camera.transform.LookAt(Vector3.up*1.2f);
                camera.fieldOfView=48;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.32f,.42f);camera.gameObject.AddComponent<AudioListener>();
                var sun=new GameObject("Arena light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(45,-30,0);
                var game=new GameObject("Sword Duel Game").AddComponent<SwordDuelSession>();game.duelCamera=camera;
                EditorSceneManager.SaveScene(scene,ScenePath);if(restore){EditorSceneManager.CloseScene(scene,true);EditorSceneManager.RestoreSceneManagerSetup(setup);}
            }
            string path=CreateApplicationShell.Generated+"/SwordDuel.asset";var definition=AssetDatabase.LoadAssetAtPath<GameDefinition>(path);
            if(definition==null){definition=ScriptableObject.CreateInstance<GameDefinition>();AssetDatabase.CreateAsset(definition,path);}
            definition.displayName="Sword Duel";definition.description="Read the strike. Raise your guard. Time the parry.";
            definition.instructions="Hold the phone in your comfortable sword-ready pose and tap before each round. Deliberate swings attack. Hold your blade across incoming slashes to block; move into that guard just before impact to parry. Recover to a quiet stance between attacks.";
            definition.available=true;definition.minimumPlayers=1;definition.maximumPlayers=2;definition.scenePath=ScenePath;definition.controllerUiMode="sword";definition.accent=new Color(.7f,.9f,1);EditorUtility.SetDirty(definition);
            var prefab=PrefabUtility.LoadPrefabContents(CreateApplicationShell.Generated+"/ApplicationShell.prefab");
            try{var flow=prefab.GetComponent<SceneFlowManager>();if(!flow.games.Contains(definition)){var games=flow.games.ToList();games.Insert(Mathf.Min(2,games.Count),definition);flow.games=games.ToArray();PrefabUtility.SaveAsPrefabAsset(prefab,CreateApplicationShell.Generated+"/ApplicationShell.prefab");}}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();AssetDatabase.SaveAssets();
            if(!Application.isBatchMode)EditorSceneManager.OpenScene(ScenePath);
        }
        private static Material Mat(string name,Color color){string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;AssetDatabase.CreateAsset(m,path);}return m;}
        private static void Box(string name,Vector3 p,Vector3 scale,Material m){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=m;}
    }
}
