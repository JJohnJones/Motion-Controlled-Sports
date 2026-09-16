using System.IO;
using System.Linq;
using MotionControllers.Core;
using MotionControllers.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MotionControllers.Editor
{
    public static class CreateTennisScene
    {
        public const string ScenePath = "Assets/Scenes/Games/Tennis.unity";
        private const string Folder = "Assets/TennisGenerated";
        [MenuItem("Tools/Motion Sports/Create or Open Tennis")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)return;
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets","TennisGenerated");
            if(!File.Exists(ScenePath)) {
                var setup=EditorSceneManager.GetSceneManagerSetup();
                bool restore = setup.Length > 0 && setup.All(s=>!string.IsNullOrEmpty(s.path));
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,restore ? NewSceneMode.Additive : NewSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                var court=Mat("Court",new Color(.10f,.42f,.54f));var surround=Mat("Surround",new Color(.14f,.31f,.22f));
                var white=Mat("Lines",Color.white);var net=Mat("Net",new Color(.1f,.14f,.2f));
                Box("Ground",new Vector3(0,-.3f,0),new Vector3(36,.5f,44),surround);
                Box("Court",new Vector3(0,-.035f,0),new Vector3(11.1f,.06f,23.9f),court);
                foreach(float x in new[]{-5.485f,-4.115f,4.115f,5.485f})Box("Sideline",new Vector3(x,.006f,0),new Vector3(.045f,.01f,23.77f),white);
                foreach(float z in new[]{-11.885f,11.885f})Box("Baseline",new Vector3(0,.006f,z),new Vector3(10.97f,.01f,.045f),white);
                foreach(float z in new[]{-6.4f,6.4f})Box("Service line",new Vector3(0,.006f,z),new Vector3(8.23f,.01f,.045f),white);
                Box("Center service line",new Vector3(0,.006f,0),new Vector3(.045f,.01f,12.8f),white);
                Box("Net band",new Vector3(0,.94f,0),new Vector3(12.2f,.06f,.045f),white);
                for(float x=-6;x<=6;x+=.3f)Box("Net cord",new Vector3(x,.47f,0),new Vector3(.012f,.94f,.012f),net);
                for(float y=.15f;y<.94f;y+=.15f)Box("Net weave",new Vector3(0,y,0),new Vector3(12,.012f,.012f),net);
                foreach(float x in new[]{-6.1f,6.1f})Box("Net post",new Vector3(x,.55f,0),new Vector3(.12f,1.1f,.12f),white);
                foreach(float x in new[]{-15f,15f})Box("Boundary wall",new Vector3(x,1,0),new Vector3(.3f,2,38),net);
                foreach(float z in new[]{-19f,19f})Box("Back wall",new Vector3(0,1,z),new Vector3(30,2,.3f),net);
                var cameraObject=new GameObject("Tennis Camera");var camera=cameraObject.AddComponent<Camera>();cameraObject.tag="MainCamera";
                cameraObject.transform.position=new Vector3(0,3.8f,-15);cameraObject.transform.LookAt(new Vector3(0,0,0));
                camera.fieldOfView=65;camera.backgroundColor=new Color(.19f,.4f,.58f);camera.clearFlags=CameraClearFlags.SolidColor;
                cameraObject.AddComponent<AudioListener>();
                var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(50,-30,0);
                var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.name="Tennis Ball";ball.transform.localScale=Vector3.one*.26f;
                Object.DestroyImmediate(ball.GetComponent<Collider>());ball.GetComponent<Renderer>().sharedMaterial=Mat("Ball",new Color(.85f,1,.1f));
                var trail=ball.AddComponent<TrailRenderer>();trail.time=.15f;trail.startWidth=.13f;trail.endWidth=.02f;trail.sharedMaterial=Mat("Trail",new Color(.75f,1,.3f));
                var game=new GameObject("Tennis Game").AddComponent<TennisGameSession>();game.ballVisual=ball.transform;
                cameraObject.AddComponent<TennisFollowCamera>().game=game;
                EditorSceneManager.SaveScene(scene,ScenePath);
                if(restore) { EditorSceneManager.CloseScene(scene,true); EditorSceneManager.RestoreSceneManagerSetup(setup); }
            }
            var definition=AssetDatabase.LoadAssetAtPath<GameDefinition>(CreateApplicationShell.Generated+"/Tennis.asset");
            definition.available=true;definition.minimumPlayers=1;definition.maximumPlayers=4;definition.scenePath=ScenePath;
            definition.description="Swing, rally and serve. Singles or doubles with friends.";
            definition.instructions="Calibrate flat, screen face up, top pointing forward toward the display. Then hold the phone upright, top up, like a racket. Tap the phone to toss on your serve, then swing. During rallies, time your swing as the ball reaches your racket. Tilt the racket to aim; faster swings hit harder.";
            definition.controllerUiMode="tennis";EditorUtility.SetDirty(definition);
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
            AssetDatabase.SaveAssets();
            if(!Application.isBatchMode)EditorSceneManager.OpenScene(ScenePath);
        }
        public static void UpgradeCamera()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var game=Object.FindFirstObjectByType<TennisGameSession>();
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single(c=>c.CompareTag("MainCamera"));
            var follow=camera.GetComponent<TennisFollowCamera>();
            if(follow==null)follow=camera.gameObject.AddComponent<TennisFollowCamera>();
            follow.game=game;camera.fieldOfView=65;
            camera.transform.position=new Vector3(0,3.8f,-15);camera.transform.LookAt(new Vector3(0,1.5f,0));
            EditorSceneManager.SaveScene(scene);
        }
        private static Material Mat(string name,Color color)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;AssetDatabase.CreateAsset(material,path);}return material;
        }
        private static void Box(string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
